// Tests production selection/input logic with stand-ins. Native ballistics,
// networking, rendering and control feel require in-game validation.
using System;
using System.Collections.Generic;
using AceOption.Helpers;

namespace Rewired
{
    public sealed class Player
    {
        public readonly HashSet<int> Buttons = new HashSet<int>();
        public bool GetButton(int id) => Buttons.Contains(id);
    }
}
namespace AceOption { public static class Plugin { public static int FireGunsActionId = 1000; } }
namespace AceOption.Helpers { internal static class CameraCommands { internal static bool InputAllowed = true; } }
public class Unit { }
public sealed class UnitPart { public bool Detached; public bool IsDetached() => Detached; }
public sealed class Aircraft : Unit
{
    public bool remoteSim;
    public UnitPart cockpit = new UnitPart();
    public WeaponManager weaponManager = new WeaponManager();
    public List<WeaponStation> weaponStations = new List<WeaponStation>();
}
public sealed class Pilot
{
    public bool dead;
    public object playerState = new object(), currentState;
    public Aircraft aircraft = new Aircraft();
    public Pilot() { currentState = playerState; }
}
public sealed class WeaponInfo { public bool gun = true; public float costPerRound = 1; }
public sealed class WeaponStation
{
    public WeaponInfo WeaponInfo = new WeaponInfo();
    public int Turrets, Shots;
    public bool IsReady = true, Safety;
    public Unit LastTarget;
    public int TurretCount() => Turrets;
    public bool Ready() => IsReady;
    public void Fire(Unit owner, Unit target)
    {
        if (Safety) return;
        Shots++; LastTarget = target; IsReady = false;
    }
}
public sealed class WeaponManager
{
    public WeaponStation currentWeaponStation;
    public int LinkedCalls;
    // Assert native dispatch rather than reimplementing the game's linked-gun loop.
    public void FireGuns() { LinkedCalls++; }
}

internal static class GunCommandsTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static WeaponStation Gun(float cost = 1) =>
        new WeaponStation { WeaponInfo = new WeaponInfo { gun = true, costPerRound = cost } };
    private static WeaponStation Missile() => new WeaponStation { WeaponInfo = new WeaponInfo { gun = false } };
    public static int Main()
    {
        try
        {
            Input(); CheapestGun(); Selection(); Turrets(); AircraftChanges(); Blocking();
            Console.WriteLine("PASS: " + checks + " gun input/selection assertions"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Input()
    {
        var pilot = new Pilot(); var player = new Rewired.Player();
        Check(!GunCommands.Held(pilot, player, 1), "Unheld action does nothing");
        player.Buttons.Add(1000);
        Check(GunCommands.Held(pilot, player, 1), "Held button accepted");
        Check(GunCommands.Held(pilot, player, .2f), "Native strength threshold accepted");
        Check(!GunCommands.Held(pilot, player, .199f), "GLOC blocks gun input");
        CameraCommands.InputAllowed = false;
        Check(!GunCommands.Held(pilot, player, 1), "Menus/map/pause gate blocks gun input");
        CameraCommands.InputAllowed = true; pilot.dead = true;
        Check(!GunCommands.Held(pilot, player, 1), "Dead pilot cannot fire");
        pilot.dead = false; pilot.currentState = new object();
        Check(!GunCommands.Held(pilot, player, 1), "Ejection/state change cannot fire");
        Check(!GunCommands.Held(null, player, 1), "Missing pilot cannot fire");
        Check(!GunCommands.Held(pilot, null, 1), "Missing player cannot fire");
        pilot.currentState = pilot.playerState; AceOption.Plugin.FireGunsActionId = -1;
        Check(!GunCommands.Held(pilot, player, 1), "Unregistered action cannot fire");
        AceOption.Plugin.FireGunsActionId = 1000;
    }
    private static void Selection()
    {
        var pilot = new Pilot(); var aircraft = pilot.aircraft; var manager = aircraft.weaponManager;
        var first = Gun(); var second = Gun(); var missile = Missile(); var turret = Gun(); turret.Turrets = 1;
        aircraft.weaponStations.AddRange(new[] { missile, first, turret, second }); manager.currentWeaponStation = missile;
        var target = new Unit(); var targets = new List<Unit> { target };
        GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 1 && second.Shots == 0, "Dedicated trigger uses the first fitted gun when prices tie");
        Check(missile.Shots == 0 && turret.Shots == 0, "Unlinked shortcut fires only the chosen gun station");
        Check(first.LastTarget == target, "First target forwarded");
        Check(manager.currentWeaponStation == missile, "Firing preserves selected missile");
        Check(targets.Count == 1 && targets[0] == target, "Target list unchanged");
        manager.currentWeaponStation = second; first.IsReady = true;
        // Normal Fire uses the selected station; the custom postfix then fires the default gun.
        second.Fire(aircraft, target);
        GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 2 && second.Shots == 1, "The two fire buttons can fire different gun stations in the same frame");
        Check(manager.currentWeaponStation == second, "Dedicated firing keeps the other gun selected for normal Fire");
        GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 2 && second.Shots == 1, "Empty/cooling default gun does not switch types");
        manager.currentWeaponStation = missile; first.IsReady = true;
        GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 3 && second.Shots == 1, "Switching from another gun to ordnance keeps the dedicated gun");
        Check(manager.currentWeaponStation == missile, "Dedicated gun firing keeps missile selected");
        turret.IsReady = false; GunCommands.Fire(pilot, true, targets);
        Check(manager.LinkedCalls == 1, "Linked shortcut uses native FireGuns");
        Check(manager.currentWeaponStation == missile, "Linked firing preserves selection");
        first.IsReady = true; GunCommands.Fire(pilot, false, null);
        Check(first.Shots == 4 && first.LastTarget == null, "Works without a marked target");
        // A station that is already unready must not receive another trigger request.
        first.IsReady = true; first.Fire(aircraft, null); second.IsReady = true;
        GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 5 && second.Shots == 1, "Unready default gun never falls back to a ready gun");
        first.IsReady = true; first.Safety = true; GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 5 && second.Shots == 1, "Station safety is preserved without falling back");
        first.Safety = false; turret.IsReady = true;
        manager.currentWeaponStation = turret; GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 6 && second.Shots == 1 && turret.Shots == 0,
            "Selecting a turret does not replace the dedicated fixed gun");
        first.IsReady = true;
        manager.currentWeaponStation = first; GunCommands.Fire(pilot, false, targets);
        Check(first.Shots == 7, "Selecting the default gun still fires it once");
    }
    private static void CheapestGun()
    {
        var pilot = new Pilot(); var aircraft = pilot.aircraft; var manager = aircraft.weaponManager;
        var expensive = Gun(20); expensive.Turrets = 1;
        var basic = Gun(2); basic.Turrets = 1;
        var samePrice = Gun(2); var missile = Missile(); missile.WeaponInfo.costPerRound = 0;
        aircraft.weaponStations.AddRange(new[] { null, missile, expensive,
            new WeaponStation { WeaponInfo = null }, basic, samePrice });
        manager.currentWeaponStation = expensive;
        var target = new Unit(); var targets = new List<Unit> { target };
        expensive.Fire(aircraft, target); GunCommands.Fire(pilot, false, targets);
        Check(basic.Shots == 1 && expensive.Shots == 1 && samePrice.Shots == 0,
            "Dedicated button fires the cheapest turret even when it comes after the selected expensive turret");
        Check(manager.currentWeaponStation == expensive && basic.LastTarget == target,
            "Cheapest turret firing preserves the selected expensive turret and receives the marked target");
        Check(missile.Shots == 0, "Lower-cost ordnance is excluded from gun price comparison");
        expensive.IsReady = true; GunCommands.Fire(pilot, false, targets);
        Check(basic.Shots == 1 && expensive.Shots == 1 && samePrice.Shots == 0,
            "An empty or cooling cheapest gun does not fall back to more expensive or equal-cost guns");
        basic.IsReady = true; basic.Safety = true; GunCommands.Fire(pilot, false, targets);
        Check(basic.Shots == 1 && expensive.Shots == 1 && samePrice.Shots == 0,
            "A safety-blocked cheapest gun does not fall back");
        basic.Safety = false; manager.currentWeaponStation = samePrice;
        GunCommands.Fire(pilot, false, targets);
        Check(basic.Shots == 2 && samePrice.Shots == 0, "Equal ammo prices use native weapon order regardless of selection");
        manager.currentWeaponStation = missile; basic.IsReady = true;
        GunCommands.Fire(pilot, false, targets);
        Check(basic.Shots == 3 && manager.currentWeaponStation == missile,
            "Switching to ordnance keeps the cheapest gun on the dedicated button");
        aircraft.weaponStations.Remove(basic); GunCommands.Fire(pilot, false, targets);
        Check(samePrice.Shots == 1 && expensive.Shots == 1, "Removing the cheapest gun selects the cheapest remaining gun");
        var free = Gun(0); aircraft.weaponStations.Add(free);
        GunCommands.Fire(pilot, false, null);
        Check(free.Shots == 1 && free.LastTarget == null, "Zero-cost ammunition is the cheapest valid choice");
        aircraft.weaponStations.Remove(free); aircraft.weaponStations.Remove(samePrice);
        GunCommands.Fire(pilot, false, null);
        Check(expensive.Shots == 2, "A lone expensive gun still works as the default");
    }
    private static void Turrets()
    {
        var pilot = new Pilot(); var aircraft = pilot.aircraft; var manager = aircraft.weaponManager;
        var turret = Gun(); turret.Turrets = 1;
        var missile = Missile();
        aircraft.weaponStations.AddRange(new[] { missile, turret }); manager.currentWeaponStation = missile;
        GunCommands.Fire(pilot, false, null);
        Check(turret.Shots == 1 && turret.LastTarget == null, "Helicopter turret fires before any gun selection without a target");
        Check(manager.currentWeaponStation == missile && missile.Shots == 0, "Turret trigger preserves selected ordnance");
        var target = new Unit(); var targets = new List<Unit> { target };
        turret.IsReady = true; manager.currentWeaponStation = turret;
        GunCommands.Fire(pilot, false, targets);
        Check(turret.Shots == 2, "Dedicated trigger also works with its turret selected");
        manager.currentWeaponStation = missile; turret.IsReady = true;
        GunCommands.Fire(pilot, false, targets);
        Check(turret.Shots == 3 && turret.LastTarget == target, "Default turret fires after switching to ordnance");
        Check(targets.Count == 1 && targets[0] == target && manager.currentWeaponStation == missile,
            "Turret firing preserves target list and current selection");
        var fixedGun = Gun(); aircraft.weaponStations.Add(fixedGun);
        GunCommands.Fire(pilot, false, targets);
        Check(turret.Shots == 3 && fixedGun.Shots == 0, "Empty or cooling turret never falls back to a fixed gun");
        turret.IsReady = true; turret.Safety = true; GunCommands.Fire(pilot, false, targets);
        Check(turret.Shots == 3, "Unlinked turret firing respects native safety");
        turret.Safety = false;
        var secondTurret = Gun(); secondTurret.Turrets = 2;
        var missileTurret = Missile(); missileTurret.Turrets = 1;
        aircraft.weaponStations.Insert(2, secondTurret); aircraft.weaponStations.Add(missileTurret);
        // Reproduce the helicopter loadout: the basic turret stays on the dedicated trigger
        // while a second turret is selected and fired through normal Fire.
        turret.IsReady = true; manager.currentWeaponStation = secondTurret;
        secondTurret.Fire(aircraft, target); GunCommands.Fire(pilot, false, targets);
        Check(turret.Shots == 4 && secondTurret.Shots == 1,
            "Two helicopter turret types can fire independently with both buttons");
        Check(manager.currentWeaponStation == secondTurret, "The second turret stays selected for normal Fire");
        manager.currentWeaponStation = missile; turret.IsReady = true; secondTurret.IsReady = true;
        GunCommands.Fire(pilot, true, targets);
        Check(manager.LinkedCalls == 1 && fixedGun.Shots == 0, "Linked fixed guns still use the native dispatcher once");
        Check(turret.Shots == 5 && secondTurret.Shots == 2, "Linked trigger fires every ready turret gun station once");
        Check(turret.LastTarget == target && secondTurret.LastTarget == target, "Linked turrets receive the first marked target");
        Check(missile.Shots == 0 && missileTurret.Shots == 0, "Linked trigger excludes ordnance including turret-mounted launchers");
        Check(manager.currentWeaponStation == missile && targets.Count == 1 && targets[0] == target,
            "Linked turret firing preserves selection and targets");
        GunCommands.Fire(pilot, true, targets);
        Check(manager.LinkedCalls == 2 && turret.Shots == 5 && secondTurret.Shots == 2,
            "Linked turret cooldown prevents repeat trigger requests");
        turret.IsReady = true; turret.Safety = true;
        secondTurret.IsReady = true; GunCommands.Fire(pilot, true, null);
        Check(turret.Shots == 5 && secondTurret.Shots == 3 && secondTurret.LastTarget == null,
            "Linked turrets honor independent safety and support no target");
        turret.Safety = false; manager.currentWeaponStation = fixedGun;
        GunCommands.Fire(pilot, false, targets);
        Check(fixedGun.Shots == 0 && turret.Shots == 6, "Selecting a fixed gun leaves the first turret on the dedicated trigger");
        aircraft.weaponStations.Remove(turret); manager.currentWeaponStation = missile;
        secondTurret.IsReady = true; GunCommands.Fire(pilot, false, targets);
        Check(turret.Shots == 6 && secondTurret.Shots == 4,
            "Removing the default turret uses the first remaining fitted gun station");
    }
    private static void AircraftChanges()
    {
        var pilot = new Pilot(); var aircraft = pilot.aircraft; var manager = aircraft.weaponManager;
        var old = Gun(); aircraft.weaponStations.Add(old); manager.currentWeaponStation = old;
        GunCommands.Fire(pilot, false, null);
        var replacement = Gun(); var missile = Missile();
        aircraft.weaponStations.Clear(); aircraft.weaponStations.AddRange(new[] { missile, replacement }); manager.currentWeaponStation = missile;
        GunCommands.Fire(pilot, false, new List<Unit>());
        Check(old.Shots == 1 && replacement.Shots == 1, "Loadout replacement uses the newly fitted default gun");
        var other = new Pilot(); var otherGun = Gun(); other.aircraft.weaponStations.Add(otherGun);
        GunCommands.Fire(other, false, null);
        Check(otherGun.Shots == 1 && replacement.Shots == 1, "Aircraft selections independent");
        other.aircraft.weaponStations.Clear(); GunCommands.Fire(other, false, null);
        Check(otherGun.Shots == 1, "No guns fitted is a no-op");
        aircraft.weaponStations.Clear(); aircraft.weaponStations.AddRange(new[] { missile,
            new WeaponStation { WeaponInfo = null }, new WeaponStation { Turrets = 1, WeaponInfo = new WeaponInfo { gun = false } } });
        GunCommands.Fire(pilot, false, null);
        Check(replacement.Shots == 1 && missile.Shots == 0, "Invalid/ordnance-only loadout never fires ordnance");
    }
    private static void Blocking()
    {
        var pilot = new Pilot(); var aircraft = pilot.aircraft; var manager = aircraft.weaponManager;
        var gun = Gun(); gun.Turrets = 1; aircraft.weaponStations.Add(gun); aircraft.remoteSim = true;
        GunCommands.Fire(pilot, true, null); GunCommands.Fire(pilot, false, null);
        Check(manager.LinkedCalls == 0 && gun.Shots == 0, "Remote aircraft cannot fire");
        aircraft.remoteSim = false; aircraft.cockpit.Detached = true;
        GunCommands.Fire(pilot, true, null); GunCommands.Fire(pilot, false, null);
        Check(manager.LinkedCalls == 0 && gun.Shots == 0, "Detached cockpit cannot fire");
        aircraft.cockpit = null; GunCommands.Fire(pilot, true, null);
        Check(manager.LinkedCalls == 0, "Missing cockpit handled");
        aircraft.cockpit = new UnitPart(); aircraft.weaponStations = null; GunCommands.Fire(pilot, true, null);
        Check(manager.LinkedCalls == 0, "Unloaded stations handled");
        aircraft.weaponManager = null; GunCommands.Fire(pilot, false, null);
        pilot.aircraft = null; GunCommands.Fire(pilot, false, null);
        Check(gun.Shots == 0, "Missing aircraft/manager handled");
    }
}

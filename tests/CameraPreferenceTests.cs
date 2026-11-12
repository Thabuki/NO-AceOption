using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;
using BepInEx.Configuration;

// Native camera/aircraft stand-ins with real BepInEx config save/reload.
internal static class CameraPreferenceTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string directory in new[] { "BepInEx/core", "NuclearOption_Data/Managed" })
            {
                string path = Path.Combine(args[0], directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Run();
    }

    private static void Bind(string path)
    {
        var config = new ConfigFile(path, false);
        AceOption.Plugin.RememberCameraView = config.Bind("Other", "RememberCameraView", true,
            ConfigMenu.Describe("RememberCameraView", "Remember the flight view."));
        AceOption.Plugin.LastCameraView = config.Bind("Other", "LastCameraView", FlightCameraView.Cockpit,
            ConfigMenu.Describe("LastCameraView", "Saved flight view."));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(), "AceOption-CameraPreference-" + Guid.NewGuid() + ".cfg");
        try
        {
            Bind(path);
            var aircraft = new Aircraft();
            var cam = new CameraStateManager { followingUnit = aircraft };
            Check(CameraPreference.InitialState(cam) == cam.cockpitState, "First use retains cockpit until a view is chosen");
            cam.SwitchState(cam.chaseState);
            Check(AceOption.Plugin.LastCameraView.Value == FlightCameraView.Chase, "Chase selection is remembered");
            int changed = 0;
            AceOption.Plugin.LastCameraView.SettingChanged += (_, e) => changed++;
            cam.SwitchState(cam.chaseState);
            Check(changed == 0, "Re-entering the same view does not save again");

            foreach (var temporary in new[] { cam.orbitState, cam.freeState, cam.tvState, cam.selectionState })
            {
                cam.SwitchState(temporary);
                Check(AceOption.Plugin.LastCameraView.Value == FlightCameraView.Chase, "Other views preserve the flight choice");
            }
            // Native setup first follows the aircraft in orbit, then selects its starting view.
            cam = new CameraStateManager { followingUnit = new Aircraft() };
            cam.SwitchState(cam.orbitState);
            cam.SwitchState(CameraPreference.InitialState(cam));
            Check(cam.currentState == cam.chaseState && changed == 0, "New aircraft restores chase without the cockpit reset overwriting it");
            Bind(path);
            cam = new CameraStateManager { followingUnit = aircraft };
            Check(CameraPreference.InitialState(cam) == cam.chaseState, "Chase survives config reload/game restart without an explicit save");
            cam.SwitchState(cam.cockpitState);
            Bind(path);
            Check(CameraPreference.InitialState(cam) == cam.cockpitState, "Switching back to cockpit persists too");

            AceOption.Plugin.LastCameraView.Value = FlightCameraView.Chase;
            AceOption.Plugin.RememberCameraView.Value = false;
            cam.SwitchState(cam.cockpitState);
            Check(CameraPreference.InitialState(cam) == cam.cockpitState, "Disabled feature retains the native cockpit start");
            Check(AceOption.Plugin.LastCameraView.Value == FlightCameraView.Chase, "Disabled feature leaves the saved choice intact");
            AceOption.Plugin.RememberCameraView.Value = true;
            Check(CameraPreference.InitialState(cam) == cam.chaseState, "Re-enabling uses the retained choice on the next spawn");

            Action<Action, Action, string> exclude = (stop, resume, label) => {
                stop();
                cam.SwitchState(cam.cockpitState);
                Check(AceOption.Plugin.LastCameraView.Value == FlightCameraView.Chase, label + ": cannot overwrite the preference");
                Check(CameraPreference.InitialState(cam) == cam.cockpitState, label + ": cannot force chase");
                resume();
            };
            exclude(() => aircraft.Local = false, () => aircraft.Local = true, "Spectating a remote aircraft");
            exclude(() => aircraft.remoteSim = true, () => aircraft.remoteSim = false, "Remote simulation");
            exclude(() => aircraft.disabled = true, () => aircraft.disabled = false, "Destroyed aircraft");
            exclude(() => aircraft.pilots[0].dead = true, () => aircraft.pilots[0].dead = false, "Dead pilot");
            exclude(() => aircraft.pilots[0].currentState = new object(),
                () => aircraft.pilots[0].currentState = aircraft.pilots[0].playerState, "Ejection/lost control");
            exclude(() => aircraft.cockpit.Detached = true, () => aircraft.cockpit.Detached = false, "Detached cockpit");
            var cockpit = aircraft.cockpit;
            exclude(() => aircraft.cockpit = null, () => aircraft.cockpit = cockpit, "Missing cockpit");
            var pilots = aircraft.pilots;
            exclude(() => aircraft.pilots = null, () => aircraft.pilots = pilots, "Missing pilot array");
            exclude(() => aircraft.pilots = new Pilot[0], () => aircraft.pilots = pilots, "Empty pilot array");
            var pilot = pilots[0];
            exclude(() => pilots[0] = null, () => pilots[0] = pilot, "Missing pilot");
            exclude(() => cam.followingUnit = new Unit(), () => cam.followingUnit = aircraft, "Non-aircraft subject");
            exclude(() => cam.followingUnit = null, () => cam.followingUnit = aircraft, "No subject/scene teardown");
            CameraPreference.Remember(null);
            Check(AceOption.Plugin.LastCameraView.Value == FlightCameraView.Chase, "Missing camera does not overwrite the choice");

            AceOption.Plugin.LastCameraView.Value = (FlightCameraView)999;
            Check(CameraPreference.InitialState(cam) == cam.cockpitState, "Unknown saved value falls back to cockpit");
            cam.SwitchState(cam.chaseState);
            Check(AceOption.Plugin.LastCameraView.Value == FlightCameraView.Chase, "A valid choice repairs an unknown saved value");

            var hidden = AceOption.Plugin.LastCameraView.Description.Tags.Single();
            Check((bool?)hidden.GetType().GetField("Browsable").GetValue(hidden) == false,
                "Saved state is hidden from Configuration Manager");
            var visible = AceOption.Plugin.RememberCameraView.Description.Tags.Single();
            Check((string)visible.GetType().GetField("Category").GetValue(visible) == "Chase Camera" &&
                (string)visible.GetType().GetField("DispName").GetValue(visible) == "Remember Camera View",
                "Feature switch appears in the camera group with a readable label");
            Console.WriteLine("PASS: " + checks + " camera preference/spawn/config persistence assertions");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

namespace AceOption
{
    internal static class Plugin
    {
        internal static ConfigEntry<bool> RememberCameraView;
        internal static ConfigEntry<FlightCameraView> LastCameraView;
    }
}
public class Unit { }
public sealed class Aircraft : Unit
{
    public bool Local = true, disabled, remoteSim;
    public Cockpit cockpit = new Cockpit();
    public Pilot[] pilots = { new Pilot() };
}
public sealed class Cockpit { public bool Detached; public bool IsDetached() => Detached; }
public sealed class Pilot
{
    public bool dead;
    public object currentState, playerState = new object();
    public Pilot() { currentState = playerState; }
}
public static class GameManager { public static bool IsLocalAircraft(Aircraft aircraft) => aircraft.Local; }
public class CameraBaseState { }
public sealed class CameraStateManager
{
    public Unit followingUnit;
    public CameraBaseState currentState;
    public CameraBaseState cockpitState = new CameraBaseState(), chaseState = new CameraBaseState(),
        orbitState = new CameraBaseState(), freeState = new CameraBaseState(),
        tvState = new CameraBaseState(), selectionState = new CameraBaseState();
    public void SwitchState(CameraBaseState state) { currentState = state; CameraPreference.Remember(this); }
}

// Runs the production grouping code with a small in-memory hierarchy. These stand-ins
// test ownership of visibility; they do not simulate Unity rendering or game behavior.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AceOption.Helpers;

namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T[] GetComponents<T>() where T : Component => gameObject.Components.OfType<T>().ToArray();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component =>
            gameObject.Descendants().Where(o => includeInactive || o.activeSelf)
                .SelectMany(o => o.Components).OfType<T>().ToArray();
        public T GetComponentInParent<T>(bool includeInactive) where T : Component
        {
            for (var node = transform; node != null; node = node.parent)
                if (includeInactive || node.gameObject.activeSelf)
                {
                    var found = node.GetComponents<T>().FirstOrDefault();
                    if (found != null) return found;
                }
            return null;
        }
    }
    public class Transform : Component { public Transform parent; }
    public class GameObject
    {
        public bool activeSelf = true;
        public readonly List<Component> Components = new List<Component>();
        public readonly List<GameObject> Children = new List<GameObject>();
        public readonly Transform transform;
        public GameObject(GameObject parent = null)
        {
            transform = Add<Transform>();
            transform.parent = parent?.transform;
            parent?.Children.Add(this);
        }
        public T Add<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            Components.Add(component);
            return component;
        }
        public void SetActive(bool active) => activeSelf = active;
        public IEnumerable<GameObject> Descendants()
        {
            yield return this;
            foreach (var child in Children)
                foreach (var descendant in child.Descendants()) yield return descendant;
        }
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
namespace AceOption
{
    public static class Plugin { public static readonly TestLogger Logger = new TestLogger(); }
    public class TestLogger { public void LogInfo(string message) { } }
}
public class SceneSingleton<T> { public static T i; }
public class FlightHud : UnityEngine.Component { public UnityEngine.Component compass; }
public class HeadMountedDisplay : UnityEngine.Component { }
public class SpeedGauge : UnityEngine.Component { }
public class Altitude : UnityEngine.Component { }
public class Bearing : UnityEngine.Component { }

internal static class HudElementsTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    public static void Main()
    {
        var root = new UnityEngine.GameObject();
        var hud = root.Add<FlightHud>();
        SceneSingleton<FlightHud>.i = hud;
        var normal = new[] { new UnityEngine.GameObject(root), new UnityEngine.GameObject(root),
            new UnityEngine.GameObject(root) };
        normal[0].Add<SpeedGauge>(); normal[1].Add<Altitude>(); normal[2].Add<Bearing>();
        var ribbon = new UnityEngine.GameObject(root);
        hud.compass = ribbon.Add<UnityEngine.Component>();
        var hmd = new UnityEngine.GameObject(root);
        hmd.Add<HeadMountedDisplay>();
        hmd.SetActive(false); // Discovery must also exclude an inactive display.
        var nested = new UnityEngine.GameObject(hmd);
        var extra = new[] { new UnityEngine.GameObject(nested), new UnityEngine.GameObject(nested),
            new UnityEngine.GameObject(nested) };
        extra[0].Add<SpeedGauge>(); extra[1].Add<Altitude>(); extra[2].Add<Bearing>();
        foreach (var obj in extra) obj.SetActive(false); // Native forward-looking state.

        HudElements.Apply(true, true, true);
        foreach (var obj in extra) Check(!obj.activeSelf, "Showing main instruments forced on supplementary HUD");
        hmd.SetActive(true);
        HudElements.Apply(false, false, false);
        foreach (var obj in normal) Check(!obj.activeSelf, "Main group hide failed");
        Check(!ribbon.activeSelf, "Main compass hide failed");

        // The native HMD update makes the readouts visible when looking away.
        foreach (var obj in extra) obj.SetActive(true);
        HudElements.Apply(false, false, false);
        foreach (var obj in extra) Check(obj.activeSelf, "Chase settings hid native look-away readout");
        foreach (var obj in extra) obj.SetActive(false); // Look forward again.
        HudElements.ShowAll(); // Leave chase and restore cockpit instruments.
        foreach (var obj in extra) Check(!obj.activeSelf, "Leaving chase forced supplementary HUD on");
        foreach (var obj in normal) Check(obj.activeSelf, "Leaving chase did not restore main instruments");
        Check(ribbon.activeSelf, "Leaving chase did not restore compass");
        HudElements.Apply(false, false, false);
        HudElements.Apply(true, true, true);
        foreach (var obj in normal) Check(obj.activeSelf, "Live setting failed to restore a hidden instrument");
        Console.WriteLine("PASS: " + checks + " HUD visibility assertions (in-memory hierarchy)");
    }
}

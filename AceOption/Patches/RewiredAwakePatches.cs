using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch(typeof(InputManager_Base), "Awake")]
internal static class RewiredAwakePatches
{
    private sealed class Binding
    {
        internal readonly string Name, Label;
        internal readonly Action<int> Assign;
        internal Binding(string name, string label, Action<int> assign) { Name = name; Label = label; Assign = assign; }
    }
    private static readonly Binding[] Bindings =
    {
        new Binding(Plugin.ACTION_TOGGLE, "Chase Cam Toggle", id => Plugin.ToggleActionId = id),
        new Binding(Plugin.ACTION_TARGET_FOCUS, "Chase Target Focus (Hold)", id => Plugin.TargetFocusActionId = id),
        new Binding(Plugin.ACTION_FIRE_GUNS, "Fire Guns (Hold)", id => Plugin.FireGunsActionId = id)
    };
    private static object Field(object obj, string name) => AccessTools.Field(obj.GetType(), name)?.GetValue(obj);
    private static T Property<T>(object obj, string name) => (T)AccessTools.Property(obj.GetType(), name).GetValue(obj, null);
    private static void Set(object obj, string name, object value) => AccessTools.Property(obj.GetType(), name).SetValue(obj, value, null);

    private static void Prefix(InputManager_Base __instance)
    {
        foreach (var binding in Bindings) binding.Assign(-1);
        try
        {
            object data = __instance.userData;
            if (data == null) return;
            var actions = Field(data, "actions") as IList;
            var categories = Field(data, "actionCategories") as IList;
            if (actions == null || categories == null) throw new MissingFieldException("Rewired action lists");
            object flight = null;
            foreach (var category in categories)
                if (string.Equals(Property<string>(category, "name"), "flight", StringComparison.OrdinalIgnoreCase)) { flight = category; break; }
            if (flight == null) throw new InvalidOperationException("Rewired flight category missing");
            int categoryId = Property<int>(flight, "id");
            object map = Field(data, "actionCategoryMap");
            var add = map == null ? null : AccessTools.Method(map.GetType(), "AddAction", new[] { typeof(int), typeof(int) });
            if (add == null) throw new MissingMethodException("Rewired category map AddAction");
            var occupied = new HashSet<int>();
            var names = new Dictionary<string, int>();
            int highest = 0;
            foreach (var action in actions)
            {
                int id = Property<int>(action, "id");
                occupied.Add(id); highest = Math.Max(highest, id);
                string name = Property<string>(action, "name");
                if (!string.IsNullOrEmpty(name)) names[name] = id;
            }
            foreach (var binding in Bindings)
            {
                if (names.TryGetValue(binding.Name, out int existing)) { binding.Assign(existing); continue; }
                int id = StableId(binding.Name);
                if (occupied.Contains(id))
                {
                    id = highest + 1;
                    Plugin.Logger.LogWarning($"Input ID collision for {binding.Label}; fallback {id} may depend on mod load order.");
                }
                var action = (InputAction)Activator.CreateInstance(typeof(InputAction), true);
                Set(action, "id", id); Set(action, "name", binding.Name); Set(action, "descriptiveName", binding.Label);
                Set(action, "categoryId", categoryId); Set(action, "type", InputActionType.Button);
                AccessTools.Field(typeof(InputAction), "_userAssignable").SetValue(action, true);
                actions.Add(action);
                try { add.Invoke(map, new object[] { categoryId, id }); }
                catch { actions.Remove(action); throw; }
                occupied.Add(id); highest = Math.Max(highest, id); names[binding.Name] = id;
                binding.Assign(id);
                Plugin.Logger.LogInfo($"Registered {binding.Label} ({id})");
            }
        }
        catch (Exception error) { Plugin.Logger.LogError("Could not register all flight actions: " + error); }
    }

    // Serialized bindings use the same FNV-1a IDs as the earlier custom build.
    internal static int StableId(string name)
    {
        uint hash = 2166136261;
        unchecked { foreach (char letter in name) hash = (hash ^ letter) * 16777619; }
        return 1000 + (int)(hash % 9000);
    }
}

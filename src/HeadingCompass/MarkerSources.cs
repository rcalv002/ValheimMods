using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace HeadingCompass;

internal sealed class MarkerSources
{
    private readonly Action<Action<string, string, Vector3, Texture2D, Color>>? collectTargets;
    private static readonly FieldInfo? Pins = typeof(Minimap).GetField("m_pins", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? Filters = typeof(Minimap).GetField("m_visibleIconTypes", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? Shared = typeof(Minimap).GetField("m_showSharedMapData", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly List<CompassMarker> pins = new List<CompassMarker>();
    private readonly List<CompassMarker> targets = new List<CompassMarker>();
    private readonly Action<string, string, Vector3, Texture2D, Color> addTarget;
    private float nextPins;
    private Minimap? lastMap;

    internal MarkerSources()
    {
        addTarget = (kind, name, position, icon, color) => targets.Add(new CompassMarker {
            Id = kind, Name = name, Position = position, Icon = icon, Color = color, Priority = 3 });
        // No assembly reference: the compass still loads when Target Call is absent or older.
        if (Chainloader.PluginInfos.TryGetValue("local.targetcall", out var plugin))
        {
            var method = plugin.Instance.GetType().GetMethod("CollectCompassMarkers", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(Action<string, string, Vector3, Texture2D, Color>) }, null);
            if (method != null && method.ReturnType == typeof(void))
                collectTargets = (Action<Action<string, string, Vector3, Texture2D, Color>>)Delegate.CreateDelegate(
                    typeof(Action<Action<string, string, Vector3, Texture2D, Color>>), method);
        }
    }

    internal bool TargetIntegrationAvailable => collectTargets != null;
    internal bool PinIntegrationAvailable => Pins != null && Filters != null && Shared != null;

    internal void Clear() { pins.Clear(); targets.Clear(); lastMap = null; nextPins = 0; }

    internal void Collect(List<CompassMarker> output, Vector3 origin, bool showTargets, bool showPins,
        bool showChecked, string favorites, float maxDistance)
    {
        output.Clear(); targets.Clear();
        // Public target positions are read afresh every frame. Never cache an unloaded creature.
        if (showTargets && collectTargets != null) { collectTargets(addTarget); output.AddRange(targets); }
        var map = Minimap.instance;
        if (!showPins || Game.m_noMap || !map) { pins.Clear(); lastMap = null; return; }
        if (map != lastMap || Time.realtimeSinceStartup >= nextPins)
        {
            lastMap = map;
            nextPins = Time.realtimeSinceStartup + 0.25f;
            pins.Clear();
            if (!(Pins?.GetValue(map) is List<Minimap.PinData> allPins) ||
                !(Filters?.GetValue(map) is bool[] filters) || !(Shared?.GetValue(map) is bool shared)) return;
            foreach (var pin in allPins)
            {
                int type = (int)pin.m_type;
                bool favorite = PinFilter.Favorite(pin.m_name ?? "", favorites);
                double dx = (double)pin.m_pos.x - origin.x, dz = (double)pin.m_pos.z - origin.z;
                if (!PinFilter.Include(pin.m_save, pin.m_checked, showChecked, type >= 0 && type < filters.Length && filters[type],
                    pin.m_ownerID != 0, shared, Math.Sqrt(dx * dx + dz * dz), maxDistance, favorite)) continue;
                var sprite = pin.m_icon;
                if (!sprite || !sprite.texture) continue;
                Rect rect = sprite.textureRect;
                var texture = sprite.texture;
                pins.Add(new CompassMarker { Id = "pin:" + pins.Count, Name = pin.m_name ?? "",
                    Position = pin.m_pos, Icon = texture, Priority = favorite ? 2 : 1,
                    UV = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height) });
            }
        }
        output.AddRange(pins);
    }
}

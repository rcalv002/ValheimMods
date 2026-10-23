using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HeadingCompass;

// Map pins tagged from the large map stay on the compass at any distance. Tags are stored per
// character and world, matching how Valheim keys its own map data, and identify pins by position.
internal sealed class PinTags
{
    private static readonly MethodInfo? PinUnderCursor = typeof(Minimap).GetMethod("GetClosestPinToCursor", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? UpdateRequired = typeof(Minimap).GetField("m_pinUpdateRequired", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly Color Tint = new Color(1f, 0.78f, 0.25f);
    private const float Tolerance = 0.5f;
    private readonly string path;
    private readonly List<(string Scope, float X, float Z)> entries = new List<(string, float, float)>();
    private string scope = "";

    internal PinTags(string path)
    {
        this.path = path;
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split('\t');
            if (parts.Length == 3 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                entries.Add((parts[0], x, z));
        }
    }

    internal bool Available => PinUnderCursor != null;

    internal void UpdateScope()
    {
        var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
        scope = profile != null && ZNet.instance ? profile.GetFilename() + "/" + ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) : "";
    }

    internal bool IsTagged(Vector3 position) => scope.Length > 0 && entries.Any(e => e.Scope == scope &&
        Math.Abs(e.X - position.x) < Tolerance && Math.Abs(e.Z - position.z) < Tolerance);

    // Toggles the saved pin under the large-map cursor. Returns the pin's new state, or null without a pin.
    internal bool? ToggleUnderCursor(out string name)
    {
        name = "";
        var map = Minimap.instance;
        if (!map || scope.Length == 0 || !(PinUnderCursor?.Invoke(map, null) is Minimap.PinData pin)) return null;
        name = pin.m_name ?? "";
        bool tagged = !IsTagged(pin.m_pos);
        if (tagged) entries.Add((scope, pin.m_pos.x, pin.m_pos.z));
        else entries.RemoveAll(e => e.Scope == scope && Math.Abs(e.X - pin.m_pos.x) < Tolerance && Math.Abs(e.Z - pin.m_pos.z) < Tolerance);
        Save();
        // Let the map restore its own icon color after an untag.
        UpdateRequired?.SetValue(map, true);
        return tagged;
    }

    // Valheim redraws pin colors only when its pin layout changes, so reapplying here is cheap and survives redraws.
    internal void TintMapIcons()
    {
        var map = Minimap.instance;
        if (!map || scope.Length == 0 || !(MarkerSources.Pins?.GetValue(map) is List<Minimap.PinData> pins)) return;
        foreach (var pin in pins)
            if (pin.m_iconElement && pin.m_iconElement.color != Tint && IsTagged(pin.m_pos)) pin.m_iconElement.color = Tint;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, entries.Select(e => e.Scope + "\t" +
            e.X.ToString("R", CultureInfo.InvariantCulture) + "\t" + e.Z.ToString("R", CultureInfo.InvariantCulture)));
    }
}

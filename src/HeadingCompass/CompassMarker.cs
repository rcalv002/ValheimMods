using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace HeadingCompass;

internal sealed class CompassMarker
{
    internal string Id = "", Name = "";
    internal Vector3 Position;
    internal Texture2D? Icon;
    internal Rect UV = new Rect(0, 0, 1, 1);
    internal Color Color = Color.white;
    internal int Priority; // 3: target channels; 2: favorite or tagged pin; 1: other saved pins.
}

internal static class MarkerLayout
{
    internal sealed class Placement
    {
        internal CompassMarker Marker = null!;
        internal double Bearing, Distance, Delta;
        internal float X;
        internal float Alpha = 1;
        internal bool Edge => Math.Abs(Delta) > 75;
    }

    internal static double Delta(double bearing, double heading) => (Heading.Normalize(bearing - heading) + 180) % 360 - 180;
    internal static string DistanceText(double meters) => meters >= 1000 ?
        (meters / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " km" :
        Math.Round(meters).ToString("0", CultureInfo.InvariantCulture) + " m";

    // Ordinary pins fade linearly from opaque at the player to farOpacity at the radius edge.
    internal static float Fade(double distance, float radius, float farOpacity) => radius <= 0 ? 1 :
        1 - (1 - Math.Max(0, Math.Min(1, farOpacity))) * (float)Math.Min(1, distance / radius);

    internal static List<Placement> Build(IEnumerable<CompassMarker> markers, Vector3 origin, double heading, float width, int limit,
        float radius = 0, float farOpacity = 1)
    {
        var candidates = new List<Placement>();
        foreach (var marker in markers)
        {
            double dx = (double)marker.Position.x - origin.x, dz = (double)marker.Position.z - origin.z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            // Skip self/arrived markers rather than making an arbitrary bearing at zero distance.
            if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < 1) continue;
            double bearing = Heading.Normalize(Math.Atan2(dx, dz) * 180 / Math.PI);
            double delta = Delta(bearing, heading);
            if (Math.Abs(delta) > 75 && marker.Priority < 2) continue;
            candidates.Add(new Placement { Marker = marker, Bearing = bearing, Distance = distance, Delta = delta,
                X = 12 + (float)(Math.Max(-75, Math.Min(75, delta)) + 75) / 150 * (width - 24),
                Alpha = marker.Priority < 2 ? Fade(distance, radius, farOpacity) : 1 });
        }
        // A single row: an overlapping icon is dropped in favor of higher priority, then the nearer one.
        var placed = new List<Placement>();
        foreach (var candidate in candidates.OrderByDescending(c => c.Marker.Priority)
            .ThenBy(c => c.Distance).ThenBy(c => Math.Abs(c.Delta)).ThenBy(c => c.Marker.Id, StringComparer.Ordinal))
        {
            if (placed.Any(p => Math.Abs(p.X - candidate.X) < (p.Edge || candidate.Edge ? 44 : 28))) continue;
            placed.Add(candidate);
            if (placed.Count >= limit) break;
        }
        return placed;
    }
}

internal static class PinFilter
{
    internal static bool Include(bool saved, bool isChecked, bool showChecked, bool typeVisible, bool shared,
        bool showShared, double distance, float maxDistance, bool favorite) =>
        saved && (!isChecked || showChecked) && typeVisible && (!shared || showShared) &&
        (favorite || maxDistance <= 0 || distance <= maxDistance);

    internal static bool Favorite(string name, string names) => names.Split(',').Any(n =>
        n.Trim().Length > 0 && string.Equals(n.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
}

using System;
using System.Collections.Generic;

namespace ValhallaMeter.Core;

public sealed class PendingDamage
{
    public double Time { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double RemainingHealth { get; set; }
    public Actor Source { get; set; } = new Actor();
    public Actor Target { get; set; } = new Actor();
    public string Ability { get; set; } = "Unknown";
    public string Weapon { get; set; } = "";
    public string Correlation { get; set; } = "";
}

public sealed class DamageFallbackMatcher
{
    private readonly List<PendingDamage> pending = new List<PendingDamage>();
    public double MaxAge { get; set; } = 2;
    public double MaxDistance { get; set; } = 3;
    public int MaxPending { get; set; } = 256;
    public int Count => pending.Count;

    public void Add(PendingDamage hit)
    {
        Prune(hit.Time);
        if (!CombatEvent.Finite(hit.Time) || !CombatEvent.Finite(hit.X) || !CombatEvent.Finite(hit.Y) ||
            !CombatEvent.Finite(hit.Z) || hit.Source == null || !hit.Source.Player || hit.Target == null) return;
        pending.Add(hit);
        while (pending.Count > MaxPending) pending.RemoveAt(0);
    }

    public CombatEvent? Match(double time, double x, double y, double z, double displayedDamage)
    {
        Prune(time);
        if (!CombatEvent.Finite(time) || !CombatEvent.Finite(x) || !CombatEvent.Finite(y) ||
            !CombatEvent.Finite(z) || !CombatEvent.Finite(displayedDamage) || displayedDamage < 0) return null;
        int best = -1;
        double bestScore = double.MaxValue;
        double maxDistanceSquared = MaxDistance * MaxDistance;
        for (int i = 0; i < pending.Count; i++)
        {
            var hit = pending[i];
            double age = time - hit.Time;
            if (age < -0.1 || age > MaxAge) continue;
            double dx = x - hit.X, dy = y - hit.Y, dz = z - hit.Z;
            double distanceSquared = dx * dx + dy * dy + dz * dz;
            if (distanceSquared > maxDistanceSquared) continue;
            double score = distanceSquared + age * age;
            if (score < bestScore) { best = i; bestScore = score; }
        }
        if (best < 0) return null;
        var match = pending[best];
        pending.RemoveAt(best);
        if (displayedDamage <= 0) return null;
        double amount = match.RemainingHealth > 0 ? Math.Min(displayedDamage, match.RemainingHealth) : displayedDamage;
        if (amount <= 0) return null;
        return new CombatEvent
        {
            Kind = "damage", Time = time, Amount = amount, Source = match.Source, Target = match.Target,
            Ability = match.Ability, Weapon = match.Weapon, Correlation = match.Correlation,
            HasPosition = true, X = x, Y = y, Z = z
        };
    }

    public void Clear() => pending.Clear();

    private void Prune(double now)
    {
        if (!CombatEvent.Finite(now)) { pending.Clear(); return; }
        pending.RemoveAll(hit => now - hit.Time > MaxAge || hit.Time - now > 0.1);
    }
}

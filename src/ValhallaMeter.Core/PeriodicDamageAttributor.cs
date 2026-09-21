using System;
using System.Collections.Generic;
using System.Linq;

namespace ValhallaMeter.Core;

public sealed class PeriodicDamageApplication
{
    public double Time { get; set; }
    public string TargetId { get; set; } = "unknown";
    public string DamageType { get; set; } = "";
    public double RawAmount { get; set; }
    public Actor Source { get; set; } = new Actor();
    public string Correlation { get; set; } = "";
    public string Weapon { get; set; } = "";
}

public sealed class PeriodicDamageShare
{
    public Actor Source { get; set; } = new Actor();
    public double Amount { get; set; }
    public string Correlation { get; set; } = "";
    public string Weapon { get; set; } = "";
}

public sealed class PeriodicDamageResult
{
    public List<PeriodicDamageShare> Shares { get; } = new List<PeriodicDamageShare>();
    public double Unattributed { get; set; }
}

public sealed class PeriodicDamageAttributor
{
    private sealed class Contribution
    {
        public Actor Source = new Actor();
        public double Remaining;
        public string Correlation = "";
        public string Weapon = "";
    }
    private sealed class Pool
    {
        public double LastTouched;
        public readonly List<Contribution> Contributions = new List<Contribution>();
    }

    private readonly Dictionary<string, Pool> pools = new Dictionary<string, Pool>();
    public double MaxAge { get; set; } = 180;
    public int MaxPools { get; set; } = 256;
    public int MaxContributors { get; set; } = 32;
    public int Count => pools.Count;

    public void Apply(PeriodicDamageApplication application, bool replace)
    {
        Prune(application.Time);
        if (!CombatEvent.Finite(application.Time) || !CombatEvent.Finite(application.RawAmount) || application.RawAmount <= 0 ||
            string.IsNullOrEmpty(application.TargetId) || application.TargetId.Length > 96 ||
            string.IsNullOrEmpty(application.DamageType) || application.DamageType.Length > 40) return;
        string key = Key(application.TargetId, application.DamageType);
        if (replace) pools.Remove(key);
        if (!pools.TryGetValue(key, out var pool))
        {
            if (pools.Count >= MaxPools)
            {
                string oldest = pools.OrderBy(p => p.Value.LastTouched).First().Key;
                pools.Remove(oldest);
            }
            pools[key] = pool = new Pool();
        }
        pool.LastTouched = application.Time;
        var existing = pool.Contributions.FirstOrDefault(c => c.Source.Id == application.Source.Id &&
            c.Correlation == application.Correlation && c.Weapon == application.Weapon);
        if (existing != null) existing.Remaining += application.RawAmount;
        else
        {
            while (pool.Contributions.Count >= Math.Max(2, MaxContributors)) Compact(pool);
            pool.Contributions.Add(new Contribution { Source = application.Source, Remaining = application.RawAmount,
                Correlation = application.Correlation, Weapon = application.Weapon });
        }
    }

    public PeriodicDamageResult Consume(string targetId, string damageType, double rawDamage, double effectiveDamage, double time)
    {
        var result = new PeriodicDamageResult { Unattributed = Math.Max(0, effectiveDamage) };
        Prune(time);
        if (!CombatEvent.Finite(rawDamage) || rawDamage <= 0 || !CombatEvent.Finite(effectiveDamage) || effectiveDamage <= 0) return result;
        string key = Key(targetId, damageType);
        if (!pools.TryGetValue(key, out var pool)) return result;
        double total = pool.Contributions.Sum(c => Math.Max(0, c.Remaining));
        if (total <= 0) { pools.Remove(key); return result; }
        double consumedRaw = Math.Min(rawDamage, total);
        double accountedEffective = effectiveDamage * Math.Min(1, total / rawDamage);
        foreach (var contribution in pool.Contributions.ToArray())
        {
            double weight = Math.Max(0, contribution.Remaining) / total;
            double amount = accountedEffective * weight;
            contribution.Remaining = Math.Max(0, contribution.Remaining - consumedRaw * weight);
            if (amount > 0.0001) result.Shares.Add(new PeriodicDamageShare { Source = contribution.Source,
                Amount = amount, Correlation = contribution.Correlation, Weapon = contribution.Weapon });
        }
        pool.Contributions.RemoveAll(c => c.Remaining <= 0.0001);
        pool.LastTouched = time;
        if (pool.Contributions.Count == 0) pools.Remove(key);
        result.Unattributed = Math.Max(0, effectiveDamage - result.Shares.Sum(s => s.Amount));
        return result;
    }

    public void Clear(string targetId, string damageType) => pools.Remove(Key(targetId, damageType));
    public void Clear() => pools.Clear();

    private void Prune(double now)
    {
        if (!CombatEvent.Finite(now)) { pools.Clear(); return; }
        foreach (var key in pools.Where(p => now - p.Value.LastTouched > MaxAge || p.Value.LastTouched - now > 1).Select(p => p.Key).ToArray())
            pools.Remove(key);
    }
    private static string Key(string targetId, string damageType) => targetId + "\n" + damageType;
    private static void Compact(Pool pool)
    {
        var first = pool.Contributions[0];
        pool.Contributions.RemoveAt(0);
        var unknown = pool.Contributions.FirstOrDefault(c => c.Source.Id == "unknown" && c.Correlation.Length == 0 && c.Weapon.Length == 0);
        if (unknown == null)
        {
            var second = pool.Contributions[0];
            pool.Contributions.RemoveAt(0);
            pool.Contributions.Add(new Contribution { Remaining = first.Remaining + second.Remaining });
        }
        else unknown.Remaining += first.Remaining;
    }
}

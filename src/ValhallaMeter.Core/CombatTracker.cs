using System;
using System.Collections.Generic;
using System.Linq;

namespace ValhallaMeter.Core;

public sealed class CombatTracker
{
    private readonly HashSet<string> seen = new HashSet<string>();
    private readonly Queue<string> seenOrder = new Queue<string>();
    private readonly Dictionary<string, CombatEvent> origins = new Dictionary<string, CombatEvent>();
    private readonly Queue<string> originOrder = new Queue<string>();
    private readonly Dictionary<string, CombatEvent> statuses = new Dictionary<string, CombatEvent>();
    private readonly List<CombatEvent> prelude = new List<CombatEvent>();
    public Fight? Current { get; private set; }
    public List<Fight> History { get; } = new List<Fight>();
    public double IdleSeconds { get; set; } = 8;
    public int MaxEvents { get; set; } = 50000;
    public double SeparationDistance { get; set; } = 80;
    public double SeparationSeconds { get; set; } = 2;
    public string Coverage { get; set; } = "Local observations; multiplayer coverage may be incomplete";
    public event Action<Fight>? Finished;

    public bool Add(CombatEvent e)
    {
        if (!e.Valid() || !seen.Add(e.Id)) return false;
        seenOrder.Enqueue(e.Id);
        while (seenOrder.Count > 100000) seen.Remove(seenOrder.Dequeue());
        Tick(e.Time);
        if (e.Kind == "metadata")
        {
            if (e.Correlation.Length == 0 || !e.Source.Player) return true;
            var key = OriginKey(e);
            if (!origins.ContainsKey(key)) originOrder.Enqueue(key);
            origins[key] = e;
            while (originOrder.Count > 4096) origins.Remove(originOrder.Dequeue());
            // Metadata travels separately from game damage. It may arrive after the hit.
            if (Current != null)
                foreach (var hit in Current.Events)
                    if (hit.Correlation == e.Correlation && hit.Source.Id == e.Source.Id &&
                        Math.Abs(hit.Time - e.Time) <= (hit.Periodic ? 180 : 2)) Enrich(hit, e);
            return true;
        }
        if (e.Correlation.Length > 0 && origins.TryGetValue(OriginKey(e), out var origin) &&
            Math.Abs(e.Time - origin.Time) <= (e.Periodic ? 180 : 2)) Enrich(e, origin);
        if (e.Kind == "status")
        {
            if (statuses.Count < 128 || statuses.ContainsKey(e.Source.Id)) statuses[e.Source.Id] = e;
        }
        bool combat = ((e.Kind == "damage" && (e.Amount > 0 || e.Result == "Immune")) ||
            (e.Kind == "shield" && e.Amount > 0)) && (e.Source.Player || e.Target.Player);
        if (Current != null && combat && DistantFromCurrent(Current, e)) Finish("Distant combat");
        if (Current == null && combat)
        {
            Current = new Fight { Start = e.Time, LastCombat = e.Time, Coverage = Coverage,
                Title = !e.Target.Player && e.Target.Id != "unknown" ? e.Target.Name : "Combat" };
            foreach (var s in statuses.Values.Where(s => e.Time - s.Time >= 0 && e.Time - s.Time <= 2))
                Current.Events.Add(new CombatEvent { Id = s.Id + ":seed", Time = e.Time, Kind = "status", Source = s.Source, Statuses = s.Statuses });
            Current.Events.AddRange(prelude.Where(p => p.Time >= e.Time - 2 && p.Time <= e.Time));
            prelude.Clear();
        }
        if (Current == null)
        {
            if (e.Kind == "stamina" || e.Kind == "eitr")
            {
                prelude.Add(e);
                prelude.RemoveAll(p => e.Time - p.Time > 2);
                if (prelude.Count > 200) prelude.RemoveAt(0);
            }
            return true;
        }
        if (combat || e.Kind == "death") Current.LastCombat = Math.Max(Current.LastCombat, e.Time);
        Current.Events.Add(e);
        if (combat)
        {
            if (e.HasPosition)
            {
                Current.HasCombatPosition = true;
                Current.LastX = e.X; Current.LastY = e.Y; Current.LastZ = e.Z;
            }
            UpdateTitle(Current);
        }
        if (e.Kind == "death" && e.Boss && Current.Events.Any(hit => hit.Kind == "damage" && hit.Target.Id == e.Target.Id))
        {
            Finish("Boss defeated");
            return true;
        }
        if (Current.Events.Count >= MaxEvents) Finish("Event limit");
        return true;
    }

    private static string OriginKey(CombatEvent e) => e.Source.Id + ":" + e.Correlation;
    private bool DistantFromCurrent(Fight fight, CombatEvent e)
    {
        if (!fight.HasCombatPosition || !e.HasPosition || e.Time - fight.LastCombat < SeparationSeconds) return false;
        double dx = e.X - fight.LastX, dy = e.Y - fight.LastY, dz = e.Z - fight.LastZ;
        double distance = Math.Max(1, SeparationDistance);
        return dx * dx + dy * dy + dz * dz >= distance * distance;
    }
    private static void Enrich(CombatEvent hit, CombatEvent origin)
    {
        hit.Weapon = origin.Weapon;
        if (!hit.Periodic && origin.Ability.Length > 0) hit.Ability = origin.Ability;
    }

    public void Tick(double now)
    {
        if (Current == null) return;
        if (now - Current.LastCombat >= IdleSeconds) Finish("Idle");
        else if (now - Current.Start >= 1800) Finish("30 minute segment");
    }

    public void Finish(string reason)
    {
        if (Current == null) return;
        var completed = Current;
        completed.Complete = true;
        completed.End = completed.LastCombat;
        completed.EndReason = reason;
        // Recovery after the last hit belongs outside the encounter.
        completed.Events.RemoveAll(e => e.Time > completed.End);
        Current = null;
        History.Insert(0, completed);
        if (History.Count > 20) History.RemoveAt(History.Count - 1);
        Finished?.Invoke(completed);
    }

    public void ResetSession()
    {
        Finish("Session ended");
        seen.Clear(); seenOrder.Clear(); origins.Clear(); originOrder.Clear(); statuses.Clear(); prelude.Clear();
    }

    public static List<PlayerRow> Rows(Fight fight)
        => Rows(fight, "");

    public static List<PlayerRow> Rows(Fight fight, string targetId)
    {
        var rows = new Dictionary<string, PlayerRow>();
        PlayerRow? Row(Actor a)
        {
            if (!a.Player) return null;
            if (!rows.TryGetValue(a.Id, out var row)) rows[a.Id] = row = new PlayerRow { Actor = a };
            return row;
        }
        foreach (var e in fight.Events)
        {
            if (e.Kind == "damage" && targetId.Length > 0 && e.Source.Id != targetId && e.Target.Id != targetId) continue;
            var source = Row(e.Source); var target = Row(e.Target);
            switch (e.Kind)
            {
                case "damage":
                    if (source != null) { source.Damage += e.Amount; source.Hits++; }
                    if (target != null) target.Taken += e.Amount;
                    break;
                case "heal": if (target != null) target.HealingReceived += e.Amount; break;
                case "shield": if (target != null) target.Absorbed += e.Amount; break;
                case "stamina": if (source != null) source.Stamina += e.Amount; break;
                case "eitr": if (source != null) source.Eitr += e.Amount; break;
                case "death": if (target != null) target.Deaths++; break;
            }
        }
        return rows.Values.OrderByDescending(r => r.Damage).ToList();
    }

    public static List<Actor> Targets(Fight fight)
    {
        var targets = new Dictionary<string, (Actor Actor, double Damage)>();
        foreach (var e in fight.Events.Where(e => e.Kind == "damage"))
        {
            Actor? enemy = e.Source.Player && !e.Target.Player ? e.Target : e.Target.Player && !e.Source.Player ? e.Source : null;
            if (enemy == null || enemy.Id == "unknown") continue;
            if (!targets.TryGetValue(enemy.Id, out var value)) value = (enemy, 0);
            if (e.Source.Player && e.Target.Id == enemy.Id) value.Damage += e.Amount;
            targets[enemy.Id] = value;
        }
        return targets.Values.OrderByDescending(v => v.Damage).ThenBy(v => v.Actor.Name).Select(v => v.Actor).ToList();
    }

    public static double Duration(Fight fight, double now, string targetId)
    {
        var range = TimeRange(fight, now, targetId);
        return Math.Max(1, range.End - range.Start);
    }

    public static (double Start, double End) TimeRange(Fight fight, double now, string targetId)
    {
        double start = fight.Start;
        double end = fight.Complete ? fight.End : Math.Max(fight.Start, fight.LastCombat);
        if (targetId.Length > 0)
        {
            var times = fight.Events.Where(e => e.Kind == "damage" && (e.Source.Id == targetId || e.Target.Id == targetId))
                .Select(e => e.Time).ToArray();
            if (times.Length > 0) { start = times.Min(); end = times.Max(); }
        }
        if (!CombatEvent.Finite(start) || !CombatEvent.Finite(end)) return (0, 1);
        return (start, Math.Max(start + 1, end));
    }

    public static double[] DpsHistogram(Fight fight, string playerId, string targetId, double now, int bins,
        Func<CombatEvent, double>? amountSelector = null)
    {
        bins = Math.Max(1, Math.Min(120, bins));
        var range = TimeRange(fight, now, targetId);
        double span = Math.Max(1, range.End - range.Start), binSeconds = span / bins;
        var result = new double[bins];
        foreach (var e in fight.Events.Where(e => e.Kind == "damage" && e.Source.Id == playerId &&
            (targetId.Length == 0 || e.Target.Id == targetId)))
        {
            double amount = amountSelector == null ? e.Amount : amountSelector(e);
            if (!CombatEvent.Finite(amount) || amount <= 0) continue;
            int index = Math.Min(bins - 1, Math.Max(0, (int)((e.Time - range.Start) / span * bins)));
            result[index] += amount / binSeconds;
        }
        return result;
    }

    private static void UpdateTitle(Fight fight)
    {
        var targets = Targets(fight);
        fight.Title = targets.Count == 0 ? "Combat" : targets.Count == 1 ? targets[0].Name : targets.Count + " targets";
    }

    public static Dictionary<string, double> BuffSeconds(Fight fight, string actorId, double now)
    {
        var result = new Dictionary<string, double>();
        var samples = fight.Events.Where(e => e.Kind == "status" && e.Source.Id == actorId).OrderBy(e => e.Time).ToArray();
        var end = fight.Complete ? fight.End : now;
        for (int i = 0; i < samples.Length; i++)
        {
            // Missing peers are not assumed to retain buffs indefinitely.
            double stop = Math.Min(end, Math.Min(samples[i].Time + 1.5, i + 1 < samples.Length ? samples[i + 1].Time : end));
            double seconds = Math.Max(0, stop - Math.Max(fight.Start, samples[i].Time));
            foreach (var buff in samples[i].Statuses.Distinct())
                result[buff] = (result.TryGetValue(buff, out double value) ? value : 0) + seconds;
        }
        return result;
    }
}

using System;
using System.Collections.Generic;

namespace ValhallaMeter.Core;

public enum FallbackOfferResult { Queued, Suppressed, Overflow }

/// <summary>Lets an owner's resolved hit supersede one displayed-number estimate before the host relays it.</summary>
public sealed class FallbackReconciler
{
    private sealed class Entry
    {
        public CombatEvent Event = null!;
        public double SeenAt;
        public bool Used;
    }

    private readonly List<Entry> pending = new List<Entry>();
    private readonly List<Entry> owners = new List<Entry>();
    public double HoldSeconds { get; set; } = 2;
    public double MatchWindow { get; set; } = 4;
    public double MaxDistance { get; set; } = 5;
    public int MaxPending { get; set; } = 256;
    public int MaxOwners { get; set; } = 512;
    public int PendingCount => pending.Count;
    public long Suppressed { get; private set; }
    public long Overflowed { get; private set; }

    public FallbackOfferResult Offer(CombatEvent fallback, double now)
    {
        PruneOwners(now);
        int best = FindBest(owners, fallback, now, unusedOnly: true);
        if (best >= 0)
        {
            owners[best].Used = true;
            Suppressed++;
            return FallbackOfferResult.Suppressed;
        }
        if (pending.Count >= MaxPending)
        {
            Overflowed++;
            return FallbackOfferResult.Overflow;
        }
        pending.Add(new Entry { Event = fallback, SeenAt = now });
        return FallbackOfferResult.Queued;
    }

    public void ObserveOwner(CombatEvent owner, double now)
    {
        if (owner.Kind != "damage" || owner.CaptureMode != "owner" || owner.Periodic ||
            !owner.Source.Player || owner.Correlation.Length == 0) return;
        PruneOwners(now);
        int best = FindBest(pending, owner, now, unusedOnly: false);
        bool used = best >= 0;
        if (used)
        {
            pending.RemoveAt(best);
            Suppressed++;
        }
        owners.Add(new Entry { Event = owner, SeenAt = now, Used = used });
        while (owners.Count > MaxOwners) owners.RemoveAt(0);
    }

    public CombatEvent[] ReleaseReady(double now)
    {
        var ready = new List<CombatEvent>();
        for (int i = pending.Count - 1; i >= 0; i--)
            if (now - pending[i].SeenAt >= HoldSeconds)
            {
                ready.Add(pending[i].Event);
                pending.RemoveAt(i);
            }
        ready.Reverse();
        PruneOwners(now);
        return ready.ToArray();
    }

    public int Clear()
    {
        int dropped = pending.Count;
        pending.Clear(); owners.Clear();
        return dropped;
    }

    private int FindBest(List<Entry> entries, CombatEvent hit, double now, bool unusedOnly)
    {
        int best = -1;
        double bestAge = double.MaxValue;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if ((unusedOnly && entry.Used) || Math.Abs(now - entry.SeenAt) > MatchWindow ||
                !SameHit(entry.Event, hit)) continue;
            double age = Math.Abs(now - entry.SeenAt);
            if (age < bestAge) { best = i; bestAge = age; }
        }
        return best;
    }

    private bool SameHit(CombatEvent a, CombatEvent b)
    {
        if (a.Kind != "damage" || b.Kind != "damage" || a.Periodic || b.Periodic ||
            a.CaptureMode == b.CaptureMode ||
            (a.CaptureMode != "fallback" && b.CaptureMode != "fallback") ||
            (a.CaptureMode != "owner" && b.CaptureMode != "owner") ||
            !a.Source.Player || a.Source.Id != b.Source.Id || a.Target.Id != b.Target.Id ||
            a.Correlation.Length == 0 || a.Correlation != b.Correlation) return false;
        if (!a.HasPosition || !b.HasPosition) return true;
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz <= MaxDistance * MaxDistance;
    }

    private void PruneOwners(double now) => owners.RemoveAll(e => now - e.SeenAt > MatchWindow);
}

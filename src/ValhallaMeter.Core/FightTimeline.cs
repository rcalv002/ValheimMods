using System;
using System.Collections.Generic;
using System.Linq;

namespace ValhallaMeter.Core;

public sealed class TimelineMarker
{
    public double Time { get; set; }
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public double Value { get; set; }
}

public static class FightTimeline
{
    public static List<TimelineMarker> Markers(Fight fight, string playerId, string targetId, double now, int bins,
        Func<CombatEvent, double>? amountSelector = null)
    {
        var range = CombatTracker.TimeRange(fight, now, targetId);
        double duration = range.End - range.Start;
        var markers = fight.Events.Where(e => e.Kind == "death" && e.Time >= range.Start && e.Time <= range.End &&
            (e.Target.Player && e.Target.Id == playerId || e.Boss && (targetId.Length == 0 || e.Target.Id == targetId)))
            .Take(24).Select(e => new TimelineMarker
            {
                Time = e.Time,
                Kind = e.Boss ? "boss" : "death",
                Label = (e.Boss ? "Boss defeated · " : "Player death · ") + e.Target.Name
            }).ToList();

        bins = Math.Max(1, Math.Min(120, bins));
        var values = CombatTracker.DpsHistogram(fight, playerId, targetId, now, bins, amountSelector);
        double total = values.Sum() * duration / bins;
        double average = total / duration;
        if (average > 0)
        {
            var spikes = Enumerable.Range(0, bins).Where(i => values[i] >= 2 * average &&
                (i == 0 || values[i] >= values[i - 1]) && (i == bins - 1 || values[i] >= values[i + 1]))
                .OrderByDescending(i => values[i]).Take(3);
            foreach (int i in spikes)
                markers.Add(new TimelineMarker
                {
                    Time = range.Start + (i + 0.5) * duration / bins,
                    Kind = "spike",
                    Label = "Damage spike",
                    Value = values[i]
                });
        }
        return markers.OrderBy(m => m.Time).ThenBy(m => m.Kind).ToList();
    }
}

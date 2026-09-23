using System;
using System.Collections.Generic;
using System.Linq;

namespace ValhallaMeter.Core;

/// <summary>Local counters only: no player names, IDs, positions, addresses, or raw events.</summary>
public sealed class DiagnosticSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string ModVersion { get; set; } = "";
    public long CreatedAtUnix { get; set; }
    public int ActiveFights { get; set; }
    public int SavedFights { get; set; }
    public RelayDiagnostics Relay { get; set; } = new RelayDiagnostics();
    public FightDiagnostics? SelectedFight { get; set; }
    public string Interpretation { get; set; } = "Counters describe observed traffic and recorded events, not hits that never reached this mod. Sent RPCs have no delivery acknowledgement.";
}

public sealed class RelayDiagnostics
{
    public bool GroupEnabled { get; set; }
    public string Role { get; set; } = "local";
    public bool Connected { get; set; }
    public int ReportingPeers { get; set; }
    public int TransportPeersVisible { get; set; }
    public int HelloAttempts { get; set; }
    public double? SecondsSinceWelcome { get; set; }
    public int QueuedEvents { get; set; }
    public long LocallyPublishedEvents { get; set; }
    public long LocallyPublishedDamageEvents { get; set; }
    public long DispatchedEventCopies { get; set; }
    public long AcceptedRemoteEvents { get; set; }
    public long AcceptedRemoteDamageEvents { get; set; }
    public long DuplicateRemoteEvents { get; set; }
    public long RejectedBatches { get; set; }
    public long RateLimitedBatches { get; set; }
    public long StaleRemoteEvents { get; set; }
    public long DroppedQueuedEvents { get; set; }
    public long RelayDisconnects { get; set; }
}

public sealed class FightDiagnostics
{
    public long RecordedAtUnix { get; set; }
    public bool Complete { get; set; }
    public double DurationSeconds { get; set; }
    public int EventCount { get; set; }
    public int ObservedPlayers { get; set; }
    public Dictionary<string, int> EventKinds { get; set; } = new Dictionary<string, int>();
    public double OutgoingPlayerDamage { get; set; }
    public double IncomingPlayerDamage { get; set; }
    public double UnattributedDamage { get; set; }
    public FightCoverage Sources { get; set; } = new FightCoverage();

    public static FightDiagnostics Summarize(Fight fight, double now)
    {
        var players = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in fight.Events)
        {
            if (e.Source.Player) players.Add(e.Source.Id);
            if (e.Target.Player) players.Add(e.Target.Id);
        }
        return new FightDiagnostics
        {
            RecordedAtUnix = fight.RecordedAtUnix,
            Complete = fight.Complete,
            DurationSeconds = CombatTracker.Duration(fight, now, ""),
            EventCount = fight.Events.Count,
            ObservedPlayers = players.Count,
            EventKinds = fight.Events.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count()),
            OutgoingPlayerDamage = fight.Events.Where(e => e.Kind == "damage" && e.Source.Player).Sum(e => e.Amount),
            IncomingPlayerDamage = fight.Events.Where(e => e.Kind == "damage" && e.Target.Player).Sum(e => e.Amount),
            UnattributedDamage = fight.Events.Where(e => e.Kind == "damage" && e.Source.Id == "unknown").Sum(e => e.Amount),
            Sources = FightCoverage.Summarize(fight)
        };
    }
}

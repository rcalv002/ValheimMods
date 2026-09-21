using System;
using System.Collections.Generic;

namespace ValhallaMeter.Core;

public sealed class Actor
{
    public string Id { get; set; } = "unknown";
    public string Name { get; set; } = "Unattributed";
    public bool Player { get; set; }
}

public sealed class CombatEvent
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "damage";
    public double Time { get; set; }
    public Actor Source { get; set; } = new Actor();
    public Actor Target { get; set; } = new Actor();
    public double Amount { get; set; }
    public string Ability { get; set; } = "Unknown";
    public string Weapon { get; set; } = "";
    public string Correlation { get; set; } = "";
    public bool Periodic { get; set; }
    public bool Boss { get; set; }
    public bool HasPosition { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public Dictionary<string, double> Types { get; set; } = new Dictionary<string, double>();
    public string[] Statuses { get; set; } = Array.Empty<string>();

    public bool Valid()
    {
        if (Id == null || Id.Length < 1 || Id.Length > 96 || !Finite(Time) || Time < 0 ||
            !Finite(Amount) || Amount < 0 || Amount > 100000000 ||
            !ValidActor(Source) || !ValidActor(Target) || Ability == null || Ability.Length > 160 ||
            Weapon == null || Weapon.Length > 160 || Correlation == null || Correlation.Length > 128 ||
            Types == null || Types.Count > 16 || Statuses == null || Statuses.Length > 64 ||
            (HasPosition && (!Finite(X) || !Finite(Y) || !Finite(Z) || Math.Abs(X) > 10000000 || Math.Abs(Y) > 10000000 || Math.Abs(Z) > 10000000))) return false;
        foreach (var pair in Types)
            if (pair.Key.Length > 40 || !Finite(pair.Value) || pair.Value < 0 || pair.Value > 100000000) return false;
        foreach (var status in Statuses) if (status == null || status.Length > 160) return false;
        return Kind == "damage" || Kind == "heal" || Kind == "shield" || Kind == "death" ||
               Kind == "stamina" || Kind == "eitr" || Kind == "status" || Kind == "metadata";
    }

    public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    private static bool ValidActor(Actor? a) => a != null && a.Id != null && a.Id.Length > 0 &&
        a.Id.Length <= 96 && a.Name != null && a.Name.Length <= 160;
}

public sealed class Fight
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Combat";
    public double Start { get; set; }
    public double End { get; set; }
    public double LastCombat { get; set; }
    public bool Complete { get; set; }
    public string EndReason { get; set; } = "";
    public string Coverage { get; set; } = "Local observations; multiplayer coverage may be incomplete";
    public bool HasCombatPosition { get; set; }
    public double LastX { get; set; }
    public double LastY { get; set; }
    public double LastZ { get; set; }
    public List<CombatEvent> Events { get; set; } = new List<CombatEvent>();
    public double Duration(double now) => Math.Max(1, (Complete ? End : Math.Max(LastCombat, now)) - Start);
}

public sealed class PlayerRow
{
    public Actor Actor { get; set; } = new Actor();
    public double Damage { get; set; }
    public double Taken { get; set; }
    public double HealingReceived { get; set; }
    public double Absorbed { get; set; }
    public double Stamina { get; set; }
    public double Eitr { get; set; }
    public int Hits { get; set; }
    public int Deaths { get; set; }
}

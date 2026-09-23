namespace ValhallaMeter.Core;

/// <summary>Provenance of recorded outgoing player damage, not an estimate of missing damage.</summary>
public sealed class FightCoverage
{
    public double OwnerDamage { get; private set; }
    public double FallbackDamage { get; private set; }
    public double UnspecifiedDamage { get; private set; }
    public double RelayedDamage { get; private set; }
    public int OwnerHits { get; private set; }
    public int FallbackHits { get; private set; }
    public int UnspecifiedHits { get; private set; }
    public int RelayedHits { get; private set; }
    public double TotalDamage => OwnerDamage + FallbackDamage + UnspecifiedDamage;

    public static FightCoverage Summarize(Fight fight)
    {
        var result = new FightCoverage();
        foreach (var e in fight.Events)
        {
            if (e.Kind != "damage" || !e.Source.Player) continue;
            switch (e.CaptureMode)
            {
                case "owner": result.OwnerDamage += e.Amount; result.OwnerHits++; break;
                case "fallback": result.FallbackDamage += e.Amount; result.FallbackHits++; break;
                default: result.UnspecifiedDamage += e.Amount; result.UnspecifiedHits++; break;
            }
            if (e.Relayed) { result.RelayedDamage += e.Amount; result.RelayedHits++; }
        }
        return result;
    }
}

using System;

namespace ValhallaMeter.Core;

/// <summary>Bounded-rate hello scheduling, independent of game RPCs.</summary>
public sealed class RelayHandshake
{
    public long HostId { get; private set; }
    public double NextHello { get; private set; }
    public int Attempts { get; private set; }

    public void Reset(double now)
    {
        HostId = 0;
        NextHello = now + 3;
        Attempts = 0;
    }

    public bool ObserveHost(long hostId, double now)
    {
        hostId = Math.Max(0, hostId);
        if (hostId == HostId) return false;
        HostId = hostId;
        NextHello = now + 3;
        Attempts = 0;
        return true;
    }

    public bool ShouldHello(double now) => HostId != 0 && now >= NextHello;

    public void HelloSent(double now)
    {
        if (HostId == 0) return;
        if (Attempts < int.MaxValue) Attempts++;
        NextHello = now + 10;
    }
}

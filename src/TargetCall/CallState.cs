using System;

namespace TargetCall;

// Host revisions order calls from different players. A heartbeat with the same
// revision may refresh its expiry, but cannot replace its identity.
internal sealed class CallState
{
    public long UserId { get; private set; }
    public uint ObjectId { get; private set; }
    public long Caller { get; private set; }
    public int Revision { get; private set; } = -1;
    public double ExpiresAt { get; private set; }
    public bool Focused { get; private set; }

    public bool HasIdentity => UserId != 0 || ObjectId != 0;
    public bool Active(double now) => HasIdentity && now < ExpiresAt;

    public bool Apply(long userId, uint objectId, long caller, int revision, float remaining, double now)
    {
        if (revision < 0 || revision < Revision || float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0) return false;
        if (revision == Revision && (userId != UserId || objectId != ObjectId || caller != Caller)) return false;
        if (revision > Revision) Focused = false;
        Revision = revision;
        UserId = userId;
        ObjectId = objectId;
        Caller = HasIdentity ? caller : 0;
        ExpiresAt = HasIdentity ? now + Math.Min(30f, remaining) : 0;
        return true;
    }

    public bool ToggleFocus(double now)
    {
        if (!Active(now)) return false;
        Focused = !Focused;
        return Focused;
    }

    public void Expire(double now)
    {
        // Retain identity so a delayed heartbeat of this revision can refresh
        // it without accepting a different target at the same revision.
        if (HasIdentity && now >= ExpiresAt) Focused = false;
    }

    public void Reset()
    {
        UserId = 0;
        ObjectId = 0;
        Caller = 0;
        Revision = -1;
        ExpiresAt = 0;
        Focused = false;
    }
}

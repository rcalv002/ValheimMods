using System;
using System.Collections.Generic;
using System.Linq;

namespace TargetCall;

// Each instance orders one shared marker. Separate RPC names let attack/assist
// and commander markers coexist without changing the original call protocol.
internal sealed class TargetSync
{
    private readonly string helloRpc;
    private readonly string requestRpc;
    private readonly string stateRpc;
    private readonly string deadRpc;
    private const float Lifetime = 30f;
    private readonly Dictionary<long, double> lastRequest = new Dictionary<long, double>();
    private ZRoutedRpc? rpc;
    private long hostId;
    private double nextHello;
    private double lastState;
    private double nextDeadReport;
    private int revision;
    private bool connected;
    public readonly CallState State = new CallState();

    private static double Clock => UnityEngine.Time.realtimeSinceStartupAsDouble;
    private static bool IsHost => ZNet.instance && ZNet.instance.IsServer();
    private static long HostId => ZNet.instance?.GetServerPeer()?.m_uid ?? 0;
    public bool Connected => IsHost || (connected && Clock - lastState < 25);

    public TargetSync(string channel = "TargetCall")
    {
        helloRpc = channel + "_Hello_v1";
        requestRpc = channel + "_Request_v1";
        stateRpc = channel + "_State_v1";
        deadRpc = channel + "_Dead_v1";
    }

    public void Update()
    {
        long currentHost = IsHost ? ZNet.GetUID() : HostId;
        bool rpcChanged = !ReferenceEquals(rpc, ZRoutedRpc.instance);
        if (rpcChanged)
        {
            rpc = ZRoutedRpc.instance;
            if (rpc != null)
            {
                rpc.Register<int>(helloRpc, (sender, version) => Plugin.Guard(() => Hello(sender, version)));
                rpc.Register<long, uint>(requestRpc, (sender, user, id) => Plugin.Guard(() => Request(sender, user, id)));
                rpc.Register<long, uint, long, int, float>(stateRpc,
                    (sender, user, id, caller, sequence, remaining) =>
                        Plugin.Guard(() => ReceiveState(sender, user, id, caller, sequence, remaining)));
                rpc.Register<long, uint, int>(deadRpc,
                    (sender, user, id, sequence) => Plugin.Guard(() => Dead(sender, user, id, sequence)));
            }
        }
        if (rpcChanged || currentHost != hostId)
        {
            hostId = currentHost;
            State.Reset();
            lastRequest.Clear();
            revision = 0;
            connected = false;
            nextHello = 0;
            nextDeadReport = 0;
        }
        if (rpc == null || !ZNet.instance) return;
        if (IsHost)
        {
            if (State.HasIdentity)
            {
                // Calls remain in effect while their caller is connected. Only
                // clients use the 30-second lease to drop a stale host marker.
                if (State.Caller != ZNet.GetUID() && !ZNet.instance.GetPeers().Any(p => p.m_uid == State.Caller))
                    SetCall(0, 0, 0);
                else State.Apply(State.UserId, State.ObjectId, State.Caller, revision, Lifetime, Clock);
            }
        }
        else
        {
            State.Expire(Clock);
            if (hostId != 0 && Clock >= nextHello)
            {
                nextHello = Clock + (Connected ? 10 : 3);
                rpc.InvokeRoutedRPC(hostId, helloRpc, 1);
            }
        }
    }

    public bool Call(long user, uint id)
    {
        if (rpc == null || !ZNet.instance || !Connected) return false;
        if (IsHost) Request(ZNet.GetUID(), user, id);
        else rpc.InvokeRoutedRPC(hostId, requestRpc, user, id);
        return true;
    }

    public void ReportDead()
    {
        if (rpc == null || !State.Active(Clock) || !Connected || Clock < nextDeadReport) return;
        nextDeadReport = Clock + 2;
        if (IsHost) Dead(ZNet.GetUID(), State.UserId, State.ObjectId, State.Revision);
        else rpc.InvokeRoutedRPC(hostId, deadRpc, State.UserId, State.ObjectId, State.Revision);
    }

    private void Hello(long sender, int version)
    {
        if (!IsHost || version != 1 || sender == 0 ||
            !ZNet.instance.GetPeers().Any(p => p.m_uid == sender)) return;
        SendState(sender);
    }

    private void Request(long sender, long user, uint id)
    {
        if (!IsHost || sender == 0 ||
            (sender != ZNet.GetUID() && !ZNet.instance.GetPeers().Any(p => p.m_uid == sender))) return;
        if (lastRequest.TryGetValue(sender, out double previous) && Clock - previous < 0.5) return;
        lastRequest[sender] = Clock;
        if (user == 0 && id == 0)
        {
            if (State.Active(Clock) && State.Caller == sender) SetCall(0, 0, 0);
            return;
        }
        SetCall(user, id, sender);
    }

    private void Dead(long sender, long user, uint id, int sequence)
    {
        if (!IsHost || sender == 0 ||
            (sender != ZNet.GetUID() && !ZNet.instance.GetPeers().Any(p => p.m_uid == sender))) return;
        if (State.Active(Clock) && sequence == revision && State.UserId == user && State.ObjectId == id)
            SetCall(0, 0, 0);
    }

    private void SetCall(long user, uint id, long caller)
    {
        if (revision == int.MaxValue) revision = 0;
        revision++;
        State.Apply(user, id, caller, revision, user == 0 && id == 0 ? 0 : Lifetime, Clock);
        SendState(0);
    }

    private void SendState(long recipient)
    {
        float remaining = State.Active(Clock) ? (float)Math.Max(0, State.ExpiresAt - Clock) : 0;
        rpc!.InvokeRoutedRPC(recipient, stateRpc, State.Active(Clock) ? State.UserId : 0,
            State.Active(Clock) ? State.ObjectId : 0u, State.Active(Clock) ? State.Caller : 0,
            revision, remaining);
    }

    private void ReceiveState(long sender, long user, uint id, long caller, int sequence, float remaining)
    {
        if (IsHost || sender == 0 || sender != hostId || !ZNet.instance) return;
        if (State.Apply(user, id, caller, sequence, remaining, Clock))
        {
            connected = true;
            lastState = Clock;
        }
    }
}

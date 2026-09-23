using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Time { public static double realtimeSinceStartupAsDouble; }
}

public sealed class ZNetPeer { public long m_uid; }
public sealed class ZNet
{
    public static ZNet instance = null!;
    public static long LocalUid;
    public bool Server;
    public ZNetPeer ServerPeer = null!;
    public List<ZNetPeer> Peers = new List<ZNetPeer>();
    public bool IsServer() => Server;
    public static long GetUID() => LocalUid;
    public ZNetPeer GetServerPeer() => ServerPeer;
    public List<ZNetPeer> GetPeers() => Peers;
    public static implicit operator bool(ZNet? net) => net != null;
}

public sealed class ZRoutedRpc
{
    public static ZRoutedRpc? instance;
    public readonly List<(long Recipient, string Name, object[] Args)> Calls = new List<(long, string, object[])>();
    private readonly Dictionary<string, Delegate> handlers = new Dictionary<string, Delegate>();
    public void Register<T>(string name, Action<long, T> handler) => handlers.Add(name, handler);
    public void Register<T, U>(string name, Action<long, T, U> handler) => handlers.Add(name, handler);
    public void Register<T, U, V>(string name, Action<long, T, U, V> handler) => handlers.Add(name, handler);
    public void Register<T, U, V, B, K>(string name, Action<long, T, U, V, B, K> handler) => handlers.Add(name, handler);
    public void InvokeRoutedRPC(long recipient, string name, params object[] args) => Calls.Add((recipient, name, args));
    public void DeliverHello(long sender, string channel = "TargetCall") =>
        ((Action<long, int>)handlers[channel + "_Hello_v1"])(sender, 1);
    public void DeliverRequest(long sender, long user, uint id, string channel = "TargetCall") =>
        ((Action<long, long, uint>)handlers[channel + "_Request_v1"])(sender, user, id);
    public void DeliverDead(long sender, long user, uint id, int revision, string channel = "TargetCall") =>
        ((Action<long, long, uint, int>)handlers[channel + "_Dead_v1"])(sender, user, id, revision);
    public void DeliverState(long sender, long user, uint id, long caller, int revision, float remaining, string channel = "TargetCall") =>
        ((Action<long, long, uint, long, int, float>)handlers[channel + "_State_v1"])(sender, user, id, caller, revision, remaining);
}

namespace TargetCall
{
    internal sealed class Plugin
    {
        public static void Guard(Action action) => action();
    }
}

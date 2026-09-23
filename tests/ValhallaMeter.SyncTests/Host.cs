using System;
using System.Collections.Generic;
using ValhallaMeter.Core;

namespace UnityEngine
{
    public static class Time { public static double realtimeSinceStartupAsDouble; }
}

public sealed class ZNetPeer { public long m_uid; }
public sealed class ZNet
{
    public static ZNet instance = null!;
    public bool Server;
    public ZNetPeer ServerPeer = null!;
    public List<ZNetPeer> Peers = new List<ZNetPeer>();
    public bool IsServer() => Server;
    public ZNetPeer GetServerPeer() => ServerPeer;
    public List<ZNetPeer> GetPeers() => Peers;
    public static implicit operator bool(ZNet? net) => net != null;
}

public sealed class ZRoutedRpc
{
    public static ZRoutedRpc? instance;
    public readonly List<(long Recipient, string Name, object[] Args)> Calls = new List<(long, string, object[])>();
    private readonly Dictionary<string, Delegate> handlers = new Dictionary<string, Delegate>();
    public void Register<T>(string name, Action<long,T> handler) => handlers[name] = handler;
    public void Register<T1,T2>(string name, Action<long,T1,T2> handler) => handlers[name] = handler;
    public void InvokeRoutedRPC(long recipient, string name, params object[] args) => Calls.Add((recipient, name, args));
    public void DeliverWelcome(long sender, int peers) =>
        ((Action<long,int,int>)handlers["ValhallaMeter_Welcome_v1"])(sender, 1, peers);
}

namespace ValhallaMeter
{
    internal sealed class Plugin
    {
        public readonly CombatTracker Tracker = new CombatTracker();
        public bool GroupEnabled = true;
        public string Issue = "";
        public static double Now => UnityEngine.Time.realtimeSinceStartupAsDouble;
        public bool Accept(CombatEvent e) => Tracker.Add(e);
        public void SetIssue(string issue) => Issue = issue;
        public static void Guard(Action action) => action();
    }
    internal static class Capture
    {
        public static int Clears;
        public static void ClearFallbacks() => Clears++;
    }
}

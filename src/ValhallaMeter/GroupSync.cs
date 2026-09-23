using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ValhallaMeter.Core;

namespace ValhallaMeter;

// A cooperative telemetry relay. Never changes game RPC payloads or simulates combat.
internal sealed class GroupSync
{
    private const string HelloRpc = "ValhallaMeter_Hello_v1";
    private const string WelcomeRpc = "ValhallaMeter_Welcome_v1";
    private const string EventsRpc = "ValhallaMeter_Events_v1";
    private readonly Plugin plugin;
    private readonly Dictionary<long, double> members = new Dictionary<long, double>();
    private readonly Dictionary<long, (double Window, int Count)> rates = new Dictionary<long, (double, int)>();
    private readonly List<CombatEvent> outgoing = new List<CombatEvent>();
    private ZRoutedRpc? rpc;
    private double nextHello;
    private double lastWelcome = -100;
    private double nextFlush;
    private int attempts;
    public bool Connected { get; private set; }
    public int Peers { get; private set; }
    public bool UseLocalDamageFallback => !IsHost && !Connected;
    public string Status => !plugin.GroupEnabled ? "Local capture" :
        IsHost ? "Host relay · " + members.Count + " reporting clients" :
        Connected ? "Group relay · " + Peers + " reporting clients" : "Local capture · no modded host found";
    private static bool IsHost => ZNet.instance && ZNet.instance.IsServer();
    private static long HostId => ZNet.instance?.GetServerPeer()?.m_uid ?? 0;
    private static double Clock => UnityEngine.Time.realtimeSinceStartupAsDouble;

    public GroupSync(Plugin plugin) { this.plugin = plugin; }

    public void Update()
    {
        if (!ReferenceEquals(rpc, ZRoutedRpc.instance))
        {
            plugin.Tracker.ResetSession();
            rpc = ZRoutedRpc.instance;
            members.Clear(); rates.Clear(); outgoing.Clear();
            Connected = false; Peers = 0; attempts = 0; nextHello = Clock + 3; lastWelcome = -100;
            Capture.ClearFallbacks();
            if (rpc != null)
            {
                rpc.Register<int>(HelloRpc, (sender, version) => Plugin.Guard(() => Hello(sender, version)));
                rpc.Register<int, int>(WelcomeRpc, (sender, version, peers) => Plugin.Guard(() => Welcome(sender, version, peers)));
                rpc.Register<string>(EventsRpc, (sender, json) => Plugin.Guard(() => Receive(sender, json)));
            }
        }
        if (rpc == null || !plugin.GroupEnabled) return;
        if (IsHost)
        {
            foreach (long id in members.Where(p => Clock - p.Value > 30).Select(p => p.Key).ToArray()) { members.Remove(id); rates.Remove(id); }
        }
        else
        {
            if (Connected && Clock - lastWelcome > 30)
            {
                Connected = false;
                plugin.Tracker.ResetSession();
                plugin.Tracker.Coverage = "Local observations after relay disconnected; partial coverage";
                outgoing.Clear();
            }
            if (HostId != 0 && Clock >= nextHello && (Connected || attempts < 3))
            {
                rpc.InvokeRoutedRPC(HostId, HelloRpc, 1);
                nextHello = Clock + 10;
                attempts++;
            }
        }
        if (Clock >= nextFlush)
        {
            nextFlush = Clock + 0.1;
            Flush();
        }
    }

    private void Hello(long sender, int version)
    {
        if (!IsHost || !plugin.GroupEnabled || version != 1 || sender == 0 ||
            !ZNet.instance.GetPeers().Any(p => p.m_uid == sender)) return;
        members[sender] = Clock;
        rpc!.InvokeRoutedRPC(sender, WelcomeRpc, 1, members.Count);
    }

    private void Welcome(long sender, int version, int peers)
    {
        if (IsHost || !plugin.GroupEnabled || version != 1 || sender != HostId || sender == 0) return;
        if (!Connected)
        {
            Capture.ClearFallbacks();
            plugin.Tracker.ResetSession();
            plugin.Tracker.Coverage = "Group: peer-reported observations since joining relay; unmodded owners may be missing";
        }
        Connected = true;
        Peers = Math.Max(0, Math.Min(peers, 100));
        lastWelcome = Clock;
    }

    public void Publish(CombatEvent e)
    {
        if (plugin.GroupEnabled && rpc != null && (IsHost || Connected))
        {
            if (IsHost)
            {
                plugin.Tracker.Coverage = "Group: host and reporting clients; unmodded owners may be missing";
                plugin.Accept(e);
            }
            if (outgoing.Count < 4096) outgoing.Add(e);
            else plugin.SetIssue("Telemetry queue full: some group events were lost");
        }
        else plugin.Accept(e);
    }

    private void Flush()
    {
        if (outgoing.Count == 0 || rpc == null) return;
        if (!IsHost && !Connected) { outgoing.Clear(); return; }
        var batch = outgoing.Take(64).ToArray();
        outgoing.RemoveRange(0, batch.Length);
        var json = JsonConvert.SerializeObject(batch);
        if (json.Length > 120000) { plugin.SetIssue("Oversized telemetry batch dropped"); return; }
        if (IsHost)
            foreach (long id in members.Keys.ToArray()) rpc.InvokeRoutedRPC(id, EventsRpc, json);
        else rpc.InvokeRoutedRPC(HostId, EventsRpc, json);
    }

    private void Receive(long sender, string json)
    {
        if (!plugin.GroupEnabled || json == null || json.Length > 120000) return;
        if (IsHost ? !members.ContainsKey(sender) : (!Connected || sender != HostId)) return;
        if (!rates.TryGetValue(sender, out var rate) || Clock - rate.Window >= 1) rate = (Clock, 0);
        if (++rate.Count > 30) { rates[sender] = rate; return; }
        rates[sender] = rate;
        CombatEvent[]? batch;
        try { batch = JsonConvert.DeserializeObject<CombatEvent[]>(json, new JsonSerializerSettings { MaxDepth = 12, TypeNameHandling = TypeNameHandling.None }); }
        catch (JsonException) { return; }
        if (batch == null || batch.Length > 64 || batch.Any(e => e == null || !e.Valid())) return;
        double now = Plugin.Now;
        foreach (var e in batch)
        {
            // Assign one host clock to group events. Clients use the echoed timestamps.
            if (IsHost) e.Time = now;
            else if (Math.Abs(e.Time - now) > 30) continue;
            e.Relayed = true;
            if (!plugin.Accept(e)) continue;
            if (IsHost)
            {
                if (outgoing.Count < 4096) outgoing.Add(e);
                else plugin.SetIssue("Relay queue full: some events were lost");
            }
        }
    }
}

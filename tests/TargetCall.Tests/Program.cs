using System;
using System.Linq;
using TargetCall;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}

var state = new CallState();
Check(state.Apply(12, 34, 7, 1, 20, 100) && state.Active(100), "Initial call is active");
Check(state.ToggleFocus(100) && state.Focused, "Take sets local focus");
Check(!state.Apply(99, 34, 7, 1, 20, 101), "Same revision cannot change target");
Check(!state.Apply(12, 34, 7, 0, 20, 101), "Older revision cannot replace target");
Check(state.Apply(12, 34, 7, 1, 15, 101) && state.Focused, "Heartbeat preserves focus");
state.Expire(117);
Check(!state.Active(117) && !state.Focused, "Expired target is hidden");
Check(state.Apply(12, 34, 7, 1, 5, 118) && state.Active(118), "Late heartbeat can restore same target");
Check(state.Apply(13, 35, 8, 2, 30, 119) && !state.Focused, "New call resets local focus");
Check(state.Apply(0, 0, 0, 3, 0, 120) && !state.Active(120), "Host clear removes call");
Check(state.Apply(0, 44, 7, 4, 30, 121) && state.Active(121), "ZDO with zero user ID is still a target");
Check(state.Apply(55, 0, 7, 5, 30, 122) && state.Active(122), "ZDO with zero object ID is still a target");
var personal = new PersonalMarker();
Check(personal.Toggle(42, 9) && personal.Matches(42, 9), "Personal marker sets locally");
Check(!personal.Toggle(42, 9) && !personal.Active, "Repeating a personal mark clears it");
personal.Toggle(42, 10);
personal.Clear();
Check(!personal.Active, "Personal marker clears on session change");

UnityEngine.Time.realtimeSinceStartupAsDouble = 200;
ZNet.LocalUid = 10;
ZNet.instance = new ZNet { Server = true, Peers = { new ZNetPeer { m_uid = 20 }, new ZNetPeer { m_uid = 30 } } };
var hostRpc = ZRoutedRpc.instance = new ZRoutedRpc();
var host = new TargetSync();
host.Update();
hostRpc.DeliverHello(20);
Check(hostRpc.Calls.Count == 1 && hostRpc.Calls[0].Recipient == 20 && (int)hostRpc.Calls[0].Args[3] == 0,
    "Late joiner receives current host state");
Check(host.Call(1001, 42) && host.State.Active(200), "Host can call a target");
var firstCall = hostRpc.Calls.Last();
Check(firstCall.Recipient == 0 && firstCall.Name == "TargetCall_State_v1" && (int)firstCall.Args[3] == 1,
    "Host broadcasts ordered target state");
hostRpc.DeliverRequest(20, 0, 0);
Check(host.State.Active(200), "Another player cannot clear the caller's target");
UnityEngine.Time.realtimeSinceStartupAsDouble = 201;
hostRpc.DeliverRequest(20, 2002, 55);
Check(host.State.Caller == 20 && host.State.ObjectId == 55, "Connected peer can replace shared call");
UnityEngine.Time.realtimeSinceStartupAsDouble = 201.6;
hostRpc.DeliverRequest(30, 0, 0);
Check(host.State.Active(201.6), "Unrelated peer cannot clear replacement call");
hostRpc.DeliverRequest(20, 0, 0);
Check(!host.State.Active(201.6) && host.State.Revision == 3, "Caller can clear shared call");
UnityEngine.Time.realtimeSinceStartupAsDouble = 202.2;
hostRpc.DeliverRequest(20, 2002, 56);
UnityEngine.Time.realtimeSinceStartupAsDouble = 233;
host.Update();
Check(host.State.Active(233) && host.State.Revision == 4, "Host renews a call past the client lease");
ZNet.instance.Peers.RemoveAll(p => p.m_uid == 20);
host.Update();
Check(!host.State.Active(233) && host.State.Revision == 5, "Host clears a departed caller's target");
Check(host.Call(0, 44) && host.State.Active(233), "Host relays a valid zero-user ZDO identity");
int dyingRevision = host.State.Revision;
hostRpc.DeliverDead(30, 0, 44, dyingRevision - 1);
Check(host.State.Active(233), "Old death report cannot clear a new call");
hostRpc.DeliverDead(30, 0, 44, dyingRevision);
Check(!host.State.Active(233), "A reporting peer clears the called creature on death");

ZNet.LocalUid = 20;
ZNet.instance = new ZNet { Server = false, ServerPeer = new ZNetPeer { m_uid = 10 } };
var clientRpc = ZRoutedRpc.instance = new ZRoutedRpc();
var client = new TargetSync();
client.Update();
Check(clientRpc.Calls.Any(c => c.Name == "TargetCall_Hello_v1" && c.Recipient == 10), "Client requests host state");
Check(!client.Call(3003, 66), "Client cannot call before host handshake");
clientRpc.DeliverState(30, 3003, 66, 30, 6, 30);
Check(!client.Connected, "Client rejects a non-host state sender");
clientRpc.DeliverState(10, 3003, 66, 30, 6, 30);
Check(client.Connected && client.State.ObjectId == 66, "Client accepts host target state");
Check(client.Call(3003, 67) && clientRpc.Calls.Last().Recipient == 10, "Client sends calls to host");
client.State.ToggleFocus(233);
clientRpc.DeliverState(10, 3003, 66, 30, 6, 20);
Check(client.State.Focused, "Host heartbeat retains local take selection");
clientRpc.DeliverState(10, 3004, 68, 30, 7, 30);
Check(!client.State.Focused && client.State.ObjectId == 68, "New shared call replaces local take selection");
ZNet.instance.ServerPeer.m_uid = 11;
client.Update();
Check(!client.Connected && !client.State.Active(233) &&
      clientRpc.Calls.Last().Name == "TargetCall_Hello_v1" && clientRpc.Calls.Last().Recipient == 11,
    "Host change resets call without registering RPCs twice");

UnityEngine.Time.realtimeSinceStartupAsDouble = 300;
ZNet.LocalUid = 10;
ZNet.instance = new ZNet { Server = true, Peers = { new ZNetPeer { m_uid = 20 } } };
var dualRpc = ZRoutedRpc.instance = new ZRoutedRpc();
var attack = new TargetSync();
var leader = new TargetSync("TargetCall_Leader");
attack.Update();
leader.Update();
dualRpc.DeliverHello(20, "TargetCall_Leader");
Check(dualRpc.Calls.Last().Name == "TargetCall_Leader_State_v1", "Leader channel handshakes independently");
Check(attack.Call(111, 1) && leader.Call(222, 2), "Attack and leader markers can coexist");
Check(attack.State.ObjectId == 1 && leader.State.ObjectId == 2, "Shared marker identities remain separate");
dualRpc.DeliverRequest(20, 333, 3, "TargetCall_Leader");
Check(leader.State.ObjectId == 3 && attack.State.ObjectId == 1, "Replacing leader leaves attack call intact");
dualRpc.DeliverDead(20, 333, 3, leader.State.Revision, "TargetCall_Leader");
Check(!leader.State.Active(300) && attack.State.Active(300), "Leader death clears only leader marker");

// The compass observes the existing channels locally without new network traffic.
var compass = Plugin.StartCompass(attack, leader);
Player.m_localPlayer = new Player();
Plugin.Characters[new ZDOID(111, 1)] = new Character { Display = "Boar", transform = { position = new(10, 0, 20) } };
var observed = new System.Collections.Generic.List<(string Kind, string Name, UnityEngine.Vector3 Position, UnityEngine.Texture2D Icon, UnityEngine.Color Color)>();
void Collect() { observed.Clear(); Plugin.CollectCompassMarkers((kind, name, pos, icon, color) => observed.Add((kind, name, pos, icon, color))); }
Collect();
Check(observed.Count == 1 && observed[0].Kind == "attack" && observed[0].Name == "Boar", "Compass exposes live attack identity and icon");
var attackTexture = observed[0].Icon;
attack.State.ToggleFocus(300); Collect();
Check(ReferenceEquals(attackTexture, observed[0].Icon) && observed[0].Color.g > 0.7, "Taking target reuses attack icon with gold focus tint");
Plugin.Characters[new ZDOID(111, 1)].Dead = true; Collect();
Check(observed.Count == 0, "Dead entities disappear from the compass before relay clear");
Plugin.Characters.Clear(); Collect();
Check(observed.Count == 0, "Unloaded creature never becomes a guessed compass position");
UnityEngine.Time.realtimeSinceStartupAsDouble = 301;
ZNet.instance.PublicPlayers.Add(new ZNet.PlayerInfo { m_name = "Leader", m_characterID = new ZDOID(222, 2),
    m_publicPosition = true, m_position = new(1000, 0, 2000) });
leader.Call(222, 2); compass.Personal.Toggle(222, 2); Collect();
Check(observed.Count == 2 && observed.Any(m => m.Kind == "commander" && m.Position.z == 2000) &&
    observed.Any(m => m.Kind == "personal"), "Distant shared player supports commander and private markers");
UnityEngine.Time.realtimeSinceStartupAsDouble = 302;
attack.Call(222, 2); Collect();
Check(observed.Any(m => m.Kind == "assist"), "Public player assist remains classified as assistance");
var remotePlayer = ZNet.instance.PublicPlayers[0]; remotePlayer.m_publicPosition = false;
ZNet.instance.PublicPlayers[0] = remotePlayer; Collect();
Check(observed.Count == 0, "Turning off public position removes all unloaded player bearings");
Player.m_localPlayer = null; Collect();
Check(observed.Count == 0, "No compass markers are exposed outside a player session");

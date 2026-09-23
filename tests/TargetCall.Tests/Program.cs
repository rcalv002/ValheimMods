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

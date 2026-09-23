using System;
using System.Linq;
using ValhallaMeter;
using ValhallaMeter.Core;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Tick(GroupSync sync, double time) { UnityEngine.Time.realtimeSinceStartupAsDouble=time; sync.Update(); }
CombatEvent Hit(string id, double time) => new CombatEvent { Id=id, Time=time, Amount=10,
    Source=new Actor { Id="player", Name="Player", Player=true },
    Target=new Actor { Id="enemy", Name="Enemy" } };

try
{
    var net=new ZNet{ServerPeer=new ZNetPeer{m_uid=7}};
    net.Peers.Add(net.ServerPeer);
    ZNet.instance=net;
    var rpc=new ZRoutedRpc();ZRoutedRpc.instance=rpc;
    var plugin=new Plugin();var sync=new GroupSync(plugin);
    Tick(sync,0);
    for(int i=0;i<6;i++)Tick(sync,3+i*10);
    Check(rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1"&&c.Recipient==7)==6,
        "Client stopped retrying an unmodded or late host after three hellos");
    Check(sync.Diagnostics().HelloAttempts==6&&sync.UseLocalDamageFallback,"Late host retry affected local fallback");

    UnityEngine.Time.realtimeSinceStartupAsDouble=54;
    rpc.DeliverWelcome(7,1);
    Check(sync.Connected&&!sync.UseLocalDamageFallback,"Modded host welcome did not connect");
    int beforeTimeoutRetries=rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1");
    Tick(sync,85);
    Check(!sync.Connected&&sync.UseLocalDamageFallback&&sync.Diagnostics().RelayDisconnects==1,
        "Timed-out relay did not return to local capture");
    Check(rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1")>beforeTimeoutRetries,
        "Hello retries stopped after relay timeout");
    Tick(sync,93);
    Check(rpc.Calls.Last(c=>c.Name=="ValhallaMeter_Hello_v1").Recipient==7,
        "Client did not retry after relay timeout");
    UnityEngine.Time.realtimeSinceStartupAsDouble=94;
    rpc.DeliverWelcome(7,1);
    Check(sync.Connected,"Client did not reconnect after timeout");

    net.ServerPeer=new ZNetPeer{m_uid=9};
    net.Peers.Clear();net.Peers.Add(net.ServerPeer);
    Tick(sync,95);
    Check(!sync.Connected&&sync.Diagnostics().RelayDisconnects==2,"Host change mixed two relay sessions");
    Tick(sync,98);
    Check(rpc.Calls.Last(c=>c.Name=="ValhallaMeter_Hello_v1").Recipient==9,
        "Hello was sent to the former host");
    UnityEngine.Time.realtimeSinceStartupAsDouble=99;
    rpc.DeliverWelcome(9,1);
    Check(sync.Connected,"New host did not reconnect");

    Check(plugin.Tracker.Add(Hit("relay-hit",99)),"Could not seed relay fight");
    int clearsBeforeToggle=Capture.Clears;
    plugin.GroupEnabled=false;Tick(sync,100);
    Check(!sync.Connected&&sync.UseLocalDamageFallback,"Disabling group sync left fallback off");
    Check(plugin.Tracker.Current==null&&plugin.Tracker.History[0].EndReason=="Session ended"&&
        plugin.Tracker.History[0].Events.Any(e=>e.Id=="relay-hit")&&Capture.Clears>clearsBeforeToggle,
        "Disabling group sync mixed relay and local observations in one fight");
    Tick(sync,101);
    sync.Publish(Hit("local-hit",101));
    Check(plugin.Tracker.Current!=null,"Local fight did not start after disabling group sync");
    int beforeReenable=rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1");
    plugin.GroupEnabled=true;Tick(sync,103);Tick(sync,106);
    Check(plugin.Tracker.Current?.Events.Any(e=>e.Id=="local-hit")==true,
        "Enabling group sync split local capture before a host responded");
    Check(rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1")==beforeReenable+1&&
        rpc.Calls.Last(c=>c.Name=="ValhallaMeter_Hello_v1").Recipient==9,
        "Re-enabling group sync did not resume hello");
    UnityEngine.Time.realtimeSinceStartupAsDouble=107;
    rpc.DeliverWelcome(9,1);
    Check(plugin.Tracker.Current==null&&plugin.Tracker.History[0].Events.Any(e=>e.Id=="local-hit")&&
        !plugin.Tracker.History[0].Events.Any(e=>e.Id=="relay-hit"),
        "Rejoining relay mixed local and relay observations in one fight");

    net.Server=true;
    var hostPlugin=new Plugin();var hostSync=new GroupSync(hostPlugin);
    Tick(hostSync,110);
    hostSync.Publish(Hit("host-relay-hit",110));
    Check(hostPlugin.Tracker.Current!=null,"Host relay fight did not start");
    hostPlugin.GroupEnabled=false;Tick(hostSync,111);
    Check(hostPlugin.Tracker.Current==null&&hostPlugin.Tracker.History[0].Events.Any(e=>e.Id=="host-relay-hit"),
        "Host did not close its relay fight when group sync was disabled");
    hostSync.Publish(Hit("host-local-hit",111));
    Check(hostPlugin.Tracker.Current!=null,"Host local fight did not start");
    hostPlugin.GroupEnabled=true;Tick(hostSync,112);
    Check(hostPlugin.Tracker.Current==null&&hostPlugin.Tracker.History[0].Events.Any(e=>e.Id=="host-local-hit")&&
        !hostPlugin.Tracker.History[0].Events.Any(e=>e.Id=="host-relay-hit"),
        "Host did not separate its local fight when group sync was enabled");
    Console.WriteLine("PASS Production relay: retries, timeout recovery, host switch, and group/local fight boundaries");
}
catch(Exception ex)
{
    Console.Error.WriteLine("FAIL Production relay: "+ex.Message);
    Environment.ExitCode=1;
}

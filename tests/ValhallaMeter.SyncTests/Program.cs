using System;
using System.Linq;
using ValhallaMeter;
using ValhallaMeter.Core;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Tick(GroupSync sync, double time) { UnityEngine.Time.realtimeSinceStartupAsDouble=time; sync.Update(); }

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

    plugin.GroupEnabled=false;Tick(sync,100);
    Check(!sync.Connected&&sync.UseLocalDamageFallback,"Disabling group sync left fallback off");
    int beforeReenable=rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1");
    plugin.GroupEnabled=true;Tick(sync,103);Tick(sync,106);
    Check(rpc.Calls.Count(c=>c.Name=="ValhallaMeter_Hello_v1")==beforeReenable+1&&
        rpc.Calls.Last(c=>c.Name=="ValhallaMeter_Hello_v1").Recipient==9,
        "Re-enabling group sync did not resume hello");
    Console.WriteLine("PASS Production relay: late host retries, timeout recovery, host switch, and local fallback");
}
catch(Exception ex)
{
    Console.Error.WriteLine("FAIL Production relay: "+ex.Message);
    Environment.ExitCode=1;
}

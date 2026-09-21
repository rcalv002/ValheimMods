using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using ValhallaMeter;
using ValhallaMeter.Core;

var tests = new List<(string, Action)>();
var a = new Actor { Id = "player:1", Name = "Astrid", Player = true };
var b = new Actor { Id = "player:2", Name = "Bjorn", Player = true };
var enemy = new Actor { Id = "entity:1", Name = "Bonemass" };
int serial = 0;
CombatEvent Event(double time, double amount = 10, string kind = "damage", Actor? source = null, Actor? target = null) =>
    new() { Id = "test:" + (++serial), Time = time, Amount = amount, Kind = kind, Source = source ?? a, Target = target ?? enemy };
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Equal(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 0.0001, $"{message}: expected {expected}, got {actual}");

tests.Add(("Duplicate network delivery counts a hit once", () => {
    var t = new CombatTracker(); var e = Event(10, 75); t.Add(e); Check(!t.Add(e), "Duplicate accepted");
    Equal(CombatTracker.Rows(t.Current!)[0].Damage, 75, "Damage");
}));
tests.Add(("Group DPS uses one encounter duration", () => {
    var t = new CombatTracker { IdleSeconds = 20 }; t.Add(Event(10, 100)); t.Add(Event(20, 40, source: b)); t.Finish("test");
    var f = t.History[0]; var rows = CombatTracker.Rows(f); Equal(f.Duration(999), 10, "Duration");
    Equal(rows.Single(r=>r.Actor.Id==a.Id).Damage/f.Duration(999), 10, "Astrid DPS");
    Equal(rows.Single(r=>r.Actor.Id==b.Id).Damage/f.Duration(999), 4, "Bjorn DPS");
}));
tests.Add(("Idle timeout splits fights and excludes trailing recovery", () => {
    var t = new CombatTracker(); t.Add(Event(10, 50)); t.Add(Event(12, 10, "heal", new Actor(), a)); t.Tick(19);
    Check(t.Current == null && t.History.Count == 1, "Fight not closed");
    Equal(t.History[0].End, 10, "Padding must not count");
    Equal(CombatTracker.Rows(t.History[0])[0].HealingReceived, 0, "Out of combat recovery");
    t.Add(Event(20)); Check(t.Current != null && t.Current.Start == 20, "New fight not started");
}));
tests.Add(("Death on next game tick remains in the finished fight", () => {
    var t = new CombatTracker(); t.Add(Event(10, 100, target: a, source: enemy));
    t.Add(Event(10.02, 0, "death", new Actor(), a)); t.Tick(20);
    Equal(CombatTracker.Rows(t.History[0])[0].Deaths, 1, "Death count");
}));
tests.Add(("Unattributed damage is never assigned to the local player", () => {
    var t = new CombatTracker(); t.Add(Event(10, 100)); t.Add(Event(11, 50, source: new Actor()));
    Equal(CombatTracker.Rows(t.Current!)[0].Damage, 100, "Player damage");
    Equal(t.Current!.Events.Where(e=>e.Source.Id=="unknown").Sum(e=>e.Amount), 50, "Unknown bucket");
}));
tests.Add(("Healing and shields are credited to recipients", () => {
    var t = new CombatTracker(); t.Add(Event(10)); t.Add(Event(11, 25, "heal", new Actor(), a)); t.Add(Event(12, 70, "shield", enemy, a));
    var r = CombatTracker.Rows(t.Current!).Single(r=>r.Actor.Id==a.Id); Equal(r.HealingReceived,25,"Healing"); Equal(r.Absorbed,70,"Shield"); Equal(r.Damage,10,"Damage unchanged");
}));
tests.Add(("Out-of-order weapon metadata enriches only its original attacker", () => {
    var t = new CombatTracker(); var hit = Event(10); hit.Correlation="key"; t.Add(hit);
    var bad = Event(10, 0, "metadata", b); bad.Correlation="key"; bad.Weapon="Wrong"; t.Add(bad);
    Check(hit.Weapon=="", "Cross-player attribution"); var meta=Event(10.1,0,"metadata");meta.Correlation="key";meta.Weapon="Iron mace";t.Add(meta);
    Check(hit.Weapon=="Iron mace","Late metadata missing");
    var late=Event(14);late.Correlation="key";t.Add(late);Check(late.Weapon=="","Stale metadata reused");
}));
tests.Add(("Buff uptime stops across missing telemetry gaps", () => {
    var t = new CombatTracker { IdleSeconds = 20 }; t.Add(Event(10));
    var s=Event(10,0,"status");s.Statuses=new[]{"Rested","Rested"};t.Add(s);
    s=Event(10.5,0,"status");s.Statuses=new[]{"Rested"};t.Add(s);
    t.Add(Event(20));t.Finish("test"); Equal(CombatTracker.BuffSeconds(t.History[0],a.Id,20)["Rested"],2,"Gap handling");
}));
tests.Add(("Buffs already active at combat start are seeded", () => {
    var t = new CombatTracker();var s=Event(9.5,0,"status");s.Statuses=new[]{"Rested"};t.Add(s);t.Add(Event(10));t.Add(Event(11));t.Finish("test");
    Equal(CombatTracker.BuffSeconds(t.History[0],a.Id,11)["Rested"],1,"Seeded buff duration");
}));
tests.Add(("Malformed peer events are rejected", () => {
    foreach (double number in new[]{double.NaN,double.PositiveInfinity,-1,100000001}){var t=new CombatTracker();Check(!t.Add(Event(10,number)),"Bad amount accepted");}
    var bad=Event(10);bad.Source=null!;Check(!bad.Valid(),"Null actor accepted");bad=Event(10);bad.Statuses=new string[65];Check(!bad.Valid(),"Oversized statuses accepted");
}));
tests.Add(("Event and history limits bound memory", () => {
    var t=new CombatTracker { MaxEvents=3 };for(int i=0;i<90;i++)t.Add(Event(10+i*.1));
    Check(t.History.Count==20,"History bound");Check(t.History.All(f=>f.Events.Count<=3),"Event bound");
}));
tests.Add(("Session reset clears status and attribution state", () => {
    var t=new CombatTracker();var s=Event(10,0,"status");s.Statuses=new[]{"Rested"};t.Add(s);t.ResetSession();t.Add(Event(10.1));
    Check(!t.Current!.Events.Any(e=>e.Kind=="status"),"Status leaked between worlds");
}));

tests.Add(("Remote-owner damage text resolves a pending local attack", () => {
    var matcher = new DamageFallbackMatcher();
    matcher.Add(new PendingDamage { Time=10, X=1, Y=2, Z=3, RemainingHealth=80, Source=a, Target=enemy,
        Ability="Horizontal", Weapon="Ancient Bark Spear", Correlation="bear-hit" });
    Check(matcher.Match(10.1,20,20,20,35)==null,"Unrelated nearby damage text was matched");
    var hit=matcher.Match(10.2,1.1,2,3,35)!;
    Equal(hit.Amount,35,"Fallback damage");Check(hit.Source==a&&hit.Target==enemy,"Actors changed");
    Check(hit.Weapon=="Ancient Bark Spear"&&hit.Correlation=="bear-hit","Attack metadata was lost");
    Check(matcher.Count==0,"Matched hit was retained");
}));
tests.Add(("Damage fallback expires and caps an overkill by observed health", () => {
    var matcher=new DamageFallbackMatcher();
    matcher.Add(new PendingDamage { Time=10, X=0, Y=0, Z=0, RemainingHealth=12, Source=a, Target=enemy });
    var hit=matcher.Match(10.2,0,0,0,50)!;Equal(hit.Amount,12,"Overkill cap");
    matcher.Add(new PendingDamage { Time=20, X=0, Y=0, Z=0, Source=a, Target=enemy });
    Check(matcher.Match(22.1,0,0,0,10)==null&&matcher.Count==0,"Expired hit matched later text");
}));
tests.Add(("Zero and malformed damage text cannot create fallback damage", () => {
    var matcher=new DamageFallbackMatcher();
    matcher.Add(new PendingDamage { Time=10, X=0, Y=0, Z=0, Source=a, Target=enemy });
    Check(matcher.Match(10.1,0,0,0,0)==null&&matcher.Count==0,"Immune hit was retained");
    matcher.Add(new PendingDamage { Time=11, X=0, Y=0, Z=0, Source=a, Target=enemy });
    Check(matcher.Match(11.1,0,0,0,double.NaN)==null,"Non-finite damage matched");
}));
tests.Add(("Target views isolate damage, incoming hits and duration", () => {
    var troll=new Actor{Id="entity:troll",Name="Troll"};var deer=new Actor{Id="entity:deer",Name="Deer"};
    var t=new CombatTracker{IdleSeconds=30};t.Add(Event(10,100,target:troll));t.Add(Event(11,20,source:troll,target:a));
    t.Add(Event(15,40,target:deer));t.Add(Event(16,10,source:deer,target:a));
    var targets=CombatTracker.Targets(t.Current!);Check(targets.Select(x=>x.Id).SequenceEqual(new[]{troll.Id,deer.Id}),"Targets not ranked by outgoing damage");
    var trollRows=CombatTracker.Rows(t.Current!,troll.Id);var astrid=trollRows.Single(r=>r.Actor.Id==a.Id);
    Equal(astrid.Damage,100,"Filtered outgoing damage");Equal(astrid.Taken,20,"Filtered incoming damage");
    Equal(CombatTracker.Duration(t.Current!,20,troll.Id),1,"Target duration");
    Check(t.Current!.Title=="2 targets","Mixed encounter title missing");
}));
tests.Add(("DPS histogram preserves damage across elapsed-time buckets", () => {
    var fight=new Fight{Start=0,End=4,LastCombat=4,Complete=true};
    fight.Events.Add(Event(.5,10));fight.Events.Add(Event(1.5,20));fight.Events.Add(Event(3.9,40));
    var bins=CombatTracker.DpsHistogram(fight,a.Id,"",4,4);
    Check(bins.Length==4,"Wrong histogram width");Equal(bins[0],10,"First DPS bucket");Equal(bins[1],20,"Second DPS bucket");
    Equal(bins[2],0,"Empty DPS bucket");Equal(bins[3],40,"Final DPS bucket");
    Equal(bins.Sum()*(CombatTracker.Duration(fight,4,"")/bins.Length),70,"Histogram changed total damage");
    var filtered=CombatTracker.DpsHistogram(fight,a.Id,"",4,4,e=>e.Time<2?e.Amount:0);
    Equal(filtered.Sum()*(CombatTracker.Duration(fight,4,"")/filtered.Length),30,"Filtered histogram changed selected damage");
}));
tests.Add(("Periodic damage is split across applications without inventing attribution", () => {
    var dots=new PeriodicDamageAttributor();var target="entity:troll";
    dots.Apply(new PeriodicDamageApplication{Time=1,TargetId=target,DamageType="Fire",RawAmount=60,Source=a,Correlation="a"},false);
    dots.Apply(new PeriodicDamageApplication{Time=1,TargetId=target,DamageType="Fire",RawAmount=40,Source=b,Correlation="b"},false);
    var first=dots.Consume(target,"Fire",20,10,2);
    Equal(first.Shares.Single(s=>s.Source.Id==a.Id).Amount,6,"First fire contributor share");
    Equal(first.Shares.Single(s=>s.Source.Id==b.Id).Amount,4,"Second fire contributor share");
    Equal(first.Unattributed,0,"Tracked fire became unattributed");
    var rest=dots.Consume(target,"Fire",80,40,3);
    Equal(rest.Shares.Sum(s=>s.Amount),40,"Remaining fire damage");Check(dots.Count==0,"Consumed pool was retained");
    dots.Apply(new PeriodicDamageApplication{Time=4,TargetId=target,DamageType="Poison",RawAmount=20,Source=a},false);
    dots.Apply(new PeriodicDamageApplication{Time=5,TargetId=target,DamageType="Poison",RawAmount=30,Source=b},true);
    var poison=dots.Consume(target,"Poison",10,10,6);
    Check(poison.Shares.Count==1&&poison.Shares[0].Source.Id==b.Id,"Replacement poison retained the old attacker");
    var missing=dots.Consume("entity:other","Fire",5,3,7);Equal(missing.Unattributed,3,"Untracked tick was guessed");
}));
tests.Add(("Late metadata enriches periodic damage beyond the direct-hit window", () => {
    var tracker=new CombatTracker{IdleSeconds=30};
    var tick=Event(15,8);tick.Periodic=true;tick.Correlation="dot:1";tick.Ability="Fire damage over time";tracker.Add(tick);
    var metadata=Event(10,0,"metadata");metadata.Correlation="dot:1";metadata.Weapon="Fire staff";metadata.Ability="Projectile";tracker.Add(metadata);
    var saved=tracker.Current!.Events.Single(e=>e.Periodic);Check(saved.Weapon=="Fire staff","Periodic weapon metadata missing");
    Check(saved.Ability=="Fire damage over time","Periodic ability label was overwritten");
}));
tests.Add(("Previous fight archive restores the newest valid unique history", () => {
    string archiveRoot=Path.Combine(Path.GetTempPath(),"ValhallaMeterArchive-"+Guid.NewGuid().ToString("N"));
    string previous=Path.Combine(archiveRoot,"previous"),current=Path.Combine(archiveRoot,"current");
    Directory.CreateDirectory(previous);Directory.CreateDirectory(current);
    try
    {
        DateTime stamp=DateTime.UtcNow.AddHours(-1);
        for(int i=0;i<22;i++)
        {
            var hit=Event(i+1,10+i);var fight=new Fight{Id="archive:"+i,Title="Fight "+i,Start=i,End=i+1,LastCombat=i+1,Complete=true,Events=new(){hit}};
            string path=Path.Combine(previous,$"fight-{i}.json");File.WriteAllText(path,JsonSerializer.Serialize(fight));File.SetLastWriteTimeUtc(path,stamp.AddMinutes(i));
        }
        string malformed=Path.Combine(previous,"fight-malformed.json");File.WriteAllText(malformed,"{not json");File.SetLastWriteTimeUtc(malformed,stamp.AddMinutes(30));
        var excluded=new Fight{Id="excluded",Title="Excluded",Start=1,End=2,LastCombat=2,Complete=true,Events=new(){Event(2)}};
        File.WriteAllText(Path.Combine(current,"fight-excluded.json"),JsonSerializer.Serialize(excluded));
        var loaded=FightArchive.Load(archiveRoot,current);
        Check(loaded.Error.Length==0,"Archive load failed: "+loaded.Error);Check(loaded.Skipped==1,"Malformed file was not reported");
        Check(loaded.Fights.Count==20,"History limit not enforced");Check(loaded.Fights[0].Title=="Fight 21"&&loaded.Fights[^1].Title=="Fight 2","History order is wrong");
        Check(!loaded.Fights.Any(f=>f.Id=="excluded"),"Current session was reloaded");
    }
    finally { Directory.Delete(archiveRoot,true); }
}));

tests.Add(("Older skips the latest fight already displayed while idle", () => {
    var latest = new Fight { Title = "Troll", Complete = true };
    var older = new Fight { Title = "Boar", Complete = true };
    var history = new List<Fight> { latest, older }; var view = new FightSelection();
    Check(view.Resolve(null, history) == latest, "Initial latest");
    Check(view.Older(null, history), "Older should be available");
    Check(view.Resolve(null, history) == older && !view.IsLive, "Older repeated the displayed fight");
}));
tests.Add(("Live, Older and Newer select different fight totals", () => {
    var t = new CombatTracker(); t.Add(Event(10, 30)); t.Finish("test");
    t.Add(Event(20, 60)); t.Finish("test"); t.Add(Event(30, 90));
    var view = new FightSelection();
    double Damage() => CombatTracker.Rows(view.Resolve(t.Current, t.History)!)[0].Damage;
    Equal(Damage(),90,"Live");view.Older(t.Current,t.History);Equal(Damage(),60,"Previous fight");
    view.Older(t.Current,t.History);Equal(Damage(),30,"Oldest fight");
    view.Newer(t.History);Equal(Damage(),60,"Newer fight");view.Live();Equal(Damage(),90,"Return to live");
}));
tests.Add(("History remains pinned while new fights are recorded", () => {
    var t=new CombatTracker();t.Add(Event(10,10));t.Finish("test");t.Add(Event(20,20));
    var view=new FightSelection();view.Older(t.Current,t.History);var chosen=view.Resolve(t.Current,t.History);
    t.Finish("test");t.Add(Event(30,30));t.Finish("test");
    Check(view.Resolve(null,t.History)==chosen,"Selected history moved when newer fights were inserted");
}));
tests.Add(("History navigation handles empty and single-fight boundaries", () => {
    var view=new FightSelection();var history=new List<Fight>();
    Check(!view.CanOlder(null,history)&&!view.Newer(history),"Empty navigation enabled");
    history.Add(new Fight { Complete=true }); Check(!view.CanOlder(null,history),"Duplicate single fight enabled");
    var current=new Fight();Check(view.Older(current,history),"Previous fight disabled during new combat");
    Check(!view.CanOlder(current,history),"Older enabled at oldest boundary");
    view.Newer(history);Check(view.IsLive&&view.Resolve(current,history)==current,"Newer did not return to live");
}));
tests.Add(("Pinned selection survives retention eviction and can return to recent history", () => {
    var view=new FightSelection();var old=new Fight();var current=new Fight();var history=new List<Fight>{old};
    view.Older(current,history);history.Clear();history.Add(new Fight());
    Check(view.Resolve(current,history)==old,"Eviction changed pinned data");
    Check(!view.CanOlder(current,history),"Older available for evicted fight");
    view.Newer(history);Check(view.Resolve(current,history)==history[0],"Could not return to retained history");
}));
tests.Add(("Native pointer press and release activates navigation exactly once", () => {
    var pointer=new PointerClick();var view=new FightSelection();var history=new List<Fight>{new Fight(),new Fight()};
    Check(pointer.Update(true,true,false,"older")==null,"Activated on press");
    if(pointer.Update(true,false,true,"older")=="older")view.Older(null,history);
    Check(view.Resolve(null,history)==history[1],"Pointer did not navigate");
    Check(pointer.Update(true,false,true,"older")==null,"Duplicate release activated");
}));
tests.Add(("Pointer clicks cancel on drag-away, disabled controls and interaction exit", () => {
    var pointer=new PointerClick();pointer.Update(true,true,false,"older");
    Check(pointer.Update(true,false,true,"newer")==null,"Drag activated another control");
    pointer.Update(true,true,false,"older");pointer.Update(false,false,false,null);
    Check(pointer.Update(true,false,true,"older")==null,"Stale press survived interaction exit");
    pointer.Update(true,true,false,null);Check(pointer.Update(true,false,true,null)==null,"Disabled control activated");
}));

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string game = args.FirstOrDefault() ?? @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
tests.Add(("All Harmony targets and injected fields exist in installed game", () => {
    using var module=ModuleDefinition.ReadModule(Path.Combine(game,"valheim_Data","Managed","assembly_valheim.dll"));
    foreach(var (type,name) in new[]{("GameCamera","UpdateMouseCapture"),("PlayerController","TakeInput"),("DamageText","RPC_DamageText"),("Character","RPC_Damage"),("Character","Damage"),("Character","ApplyDamage"),("Character","RPC_Heal"),("Character","SetHealth"),("SE_Burning","AddFireDamage"),("SE_Burning","AddSpiritDamage"),("SE_Poison","AddDamage"),("StatusEffect","OnDestroy"),("SE_Shield","OnDamaged"),("Player","OnDeath"),("Attack","OnAttackTrigger"),("Attack","FireProjectileBurst"),("Projectile","OnHit"),("Player","UseStamina"),("Player","UseEitr")}){
        var t=module.Types.Single(t=>t.Name==type);Check(t.Methods.Count(m=>m.Name==name)==1,$"Missing or ambiguous {type}.{name}");
    }
    foreach(var (type,field) in new[]{("Attack","m_character"),("Attack","m_weapon"),("Projectile","m_owner"),("Projectile","m_weapon"),("SE_Poison","m_damageLeft")})
        Check(module.Types.Single(t=>t.Name==type).Fields.Any(f=>f.Name==field),$"Missing {type}.{field}");
}));

int failures=0;
foreach(var (name,run) in tests){try{run();System.Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;System.Console.WriteLine("FAIL "+name+": "+ex.Message);}}
if(failures>0){Environment.ExitCode=1;return;}

// A deterministic, explicitly synthetic fixture for visual QA and report interaction testing.
var demo=new CombatTracker { IdleSeconds=20, Coverage="SYNTHETIC DEMO DATA — illustrative output, not a captured Valheim session" };
var c=new Actor{Id="player:3",Name="Freya",Player=true};var people=new[]{a,b,c};
for(int i=0;i<=120;i++){
    double time=100+i*.5;
    if(i%2==0)foreach(var person in people){var hit=Event(time,person==a?42:person==b?31:20,source:person);hit.Weapon=person==a?"Iron mace":person==b?"Huntsman bow":"Abyssal razor";hit.Ability=person==b?"Projectile":"Melee";hit.Types=new(){[person==a?"Blunt":"Pierce"]=hit.Amount};demo.Add(hit);}
    foreach(var person in people){var s=Event(time,0,"status",person);s.Statuses=i<80?new[]{"Rested","Bonemass"}:new[]{"Rested"};demo.Add(s);}
    if(i%10==0){demo.Add(Event(time,15,"damage",enemy,a));demo.Add(Event(time,8,"heal",new Actor(),a));demo.Add(Event(time,12,"stamina",a));demo.Add(Event(time,30,"shield",enemy,b));}
}
demo.Finish("Synthetic example");var sample=demo.History[0];sample.Title="Bonemass · Synthetic example";
string json=JsonSerializer.Serialize(sample);string template=File.ReadAllText(Path.Combine(root,"src","ValhallaMeter","report.html"));
string artifact=Path.Combine(root,"artifacts");Directory.CreateDirectory(artifact);
File.WriteAllText(Path.Combine(artifact,"sample-fight.json"),json);
File.WriteAllText(Path.Combine(artifact,"sample-report.html"),template.Replace("__FIGHT_DATA__",json).Replace("__REPORT_NOW__","160"));
System.Console.WriteLine($"{tests.Count} checks passed. Synthetic report: {Path.Combine(artifact,"sample-report.html")}");

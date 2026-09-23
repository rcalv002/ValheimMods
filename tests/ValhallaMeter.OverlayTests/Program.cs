using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ValhallaMeter;
using ValhallaMeter.Core;

var plugin=new Plugin();var window=new MeterWindow(plugin);
var actor=new Actor {Id="player:1",Name="Astrid",Player=true};int serial=0;
void Damage(double time,double amount,string title,string weapon="",string type=""){var e=new CombatEvent{Id="ui:"+(++serial),Time=time,Amount=amount,Source=actor,Target=new Actor{Id="entity:"+title,Name=title},Weapon=weapon,Result="Normal"};if(type.Length>0)e.Types[type]=amount;plugin.Tracker.Add(e);}
void Check(bool condition,string message){if(!condition)throw new Exception(message);}
void Draw(){window.Refresh();GUI.Commands.Clear();window.Draw();}
bool Has(string label)=>GUI.Commands.Any(c=>c.Text==label);
void Click(string label)
{
    var r=GUI.Commands.Single(c=>c.Text==label).Area;
    ZInput.pointerPosition=new Vector3(r.x+r.width/2,Screen.height-r.y-r.height/2);
    ZInput.Down=true;ZInput.Held=true;ZInput.Up=false;window.HandleInput();
    ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;window.HandleInput();ZInput.Up=false;Draw();
}
void ClickTargetNext()
{
    var r=GUI.Commands.Single(c=>c.Text==">"&&c.Area.y<200).Area;
    ZInput.pointerPosition=new Vector3(r.x+r.width/2,Screen.height-r.y-r.height/2);
    ZInput.Down=true;ZInput.Held=true;window.HandleInput();
    ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;window.HandleInput();ZInput.Up=false;Draw();
}
void HoverPlot(float fraction)
{
    var header=GUI.Commands.Single(c=>c.Text=="VALHALLA").Area;
    float originX=header.x-18,originY=header.y-13;
    float x=originX+54+448*Math.Clamp(fraction,0,1),y=originY+306+52;
    ZInput.pointerPosition=new Vector3(x,Screen.height-y);Draw();
}
try
{
Damage(10,30,"Boar");plugin.Tracker.Finish("test");Damage(20,60,"Troll");plugin.Tracker.Finish("test");
plugin.Tracker.History[0].RecordedAtUnix=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-5*60;Draw();
Check(Has("Troll"),"Latest fight not displayed");
Click("Fights");Check(Has("WHEN")&&Has("TARGET")&&Has("DURATION")&&Has("DAMAGE")&&Has("Boar")&&Has("Troll"),"Fight browser did not list saved encounters");
Click("Live");Check(!Has("WHEN")&&Has("Troll"),"Live did not close the fight browser");
Click("Fights");
Click("Boar");Check(Has("HISTORY")&&Has("Boar")&&!Has("WHEN"),"Fight browser did not pin the chosen encounter");
Click("Live");Check(Has("Troll")&&Has("LATEST"),"Fight browser did not return to latest");
Click("Older");Check(Has("Boar")&&Has("HISTORY"),"Actual Older hit target failed");
Click("Newer");Check(Has("Troll"),"Actual Newer hit target failed");Click("Live");Check(Has("LATEST"),"Actual Live hit target failed");
Click("Compare");Check(Has("CURRENT  ·  Troll")&&Has("PREVIOUS  ·  Boar"),"Fight comparison did not select the preceding encounter");Click("Damage");
Damage(30,90,"Greydwarf","Iron mace","Blunt");Damage(32,10,"Neck","Huntsman bow","Pierce");Draw();Check(Has("LIVE")&&Has("2 targets · ALL"),"Live mixed-target view failed");
ClickTargetNext();Check(Has("Greydwarf"),"First target filter failed");ClickTargetNext();Check(Has("Neck"),"Second target filter failed");
Click("Older");Damage(34,10,"Greydwarf");plugin.Tracker.Finish("test");Draw();Check(Has("Troll"),"History drifted");
Click("Live");Click("Details");Check(Has("Astrid")&&GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("DPS OVER TIME"))&&Has("Iron mace")&&Has("Huntsman bow"),"Details histogram did not render time and weapon damage");
HoverPlot(.7f);Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.Contains("s  ·  ")&&c.Text.EndsWith(" DPS")),"DPS plot hover cursor did not expose its time bucket");
Click("Iron mace");Check(GUI.Commands.Any(c=>c.Text=="Iron mace  ·  1 HIT")&&GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("90 / 110 TOTAL")),"Weapon row did not filter the DPS line");
Click("Iron mace");Check(GUI.Commands.Any(c=>c.Text=="DPS OVER TIME  ·  3 HITS"),"Active weapon row did not restore all damage");
Click("Damage types");Check(Has("Blunt")&&Has("Pierce"),"Damage-type histogram did not render");
Click("Pierce");Check(GUI.Commands.Any(c=>c.Text=="Pierce  ·  1 HIT")&&GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("10 / 110 TOTAL")),"Damage-type row did not filter the DPS line");
Click("Hit results");Check(Has("Normal"),"Hit-result breakdown did not render");Click("Normal");Check(GUI.Commands.Any(c=>c.Text=="Normal  ·  3 HITS"),"Hit-result row did not filter the DPS line");
Click("Buffs");Check(Has("No status samples for this player."),"Buff tab did not switch");
Click("Damage");plugin.Interacting=false;Click("Older");Check(Has("2 targets · ALL"),"Inactive pointer changed selection");
plugin.Interacting=true;plugin.WindowScale.Value=1.5f;Draw();Click("Older");Check(Has("Troll"),"Scaled hit testing failed");
Click("Export report");Check(plugin.Exports==1,"Export control failed");
plugin.WindowScale.Value=1;Draw();
Click("Lock panel");Check(Has("Unlock panel")&&plugin.WindowLocked.Value,"Window lock did not activate");Click("Unlock panel");Check(!plugin.WindowLocked.Value,"Window lock did not clear");
float oldX=plugin.WindowX.Value,oldY=plugin.WindowY.Value;
var header=GUI.Commands.Single(c=>c.Text=="VALHALLA").Area;
ZInput.pointerPosition=new Vector3(header.x+20,Screen.height-header.y-10);
ZInput.Down=true;ZInput.Held=true;window.HandleInput();ZInput.Down=false;
ZInput.pointerPosition=new Vector3(header.x+70,Screen.height-header.y-40);window.HandleInput();
ZInput.Held=false;ZInput.Up=true;window.HandleInput();ZInput.Up=false;Draw();
Check(plugin.WindowX.Value==oldX+50&&plugin.WindowY.Value==oldY+30,"Dragging did not save window position");
Check(Has("F8  ·  RETURN TO GAME"),"Wrong interaction shortcut label");
Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("5m ago · ")),"Fight age and local timestamp were not displayed");
Click("Live");Click("Details");Click("Weapons / skills");
Click("Iron mace");
HoverPlot(.7f);
Check(GUI.Commands.Any(c=>c.Text=="Iron mace  ·  1 HIT")&&GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("90 / 110 TOTAL"))&&GUI.Commands.Any(c=>Math.Abs(c.Rotation)>.1)&&Has("0.0s")&&Has("Iron mace")&&Has("Huntsman bow"),"Final selected-series line-plot preview is incomplete");
plugin.WindowX.Value=30;plugin.WindowY.Value=30;
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));Directory.CreateDirectory(Path.Combine(root,"artifacts"));
File.WriteAllText(Path.Combine(root,"artifacts","overlay-preview.svg"),GUI.Svg());
var browserPlugin=new Plugin();
for(int i=0;i<20;i++)
{
    string name=$"Encounter {i:00}";
    browserPlugin.Tracker.Add(new CombatEvent{Id="browser:"+i,Time=100+i*10,Amount=i+1,Source=actor,
        Target=new Actor{Id="entity:"+name,Name=name}});
    browserPlugin.Tracker.Finish("test");
}
var browserWindow=new MeterWindow(browserPlugin);
void BrowserDraw(){browserWindow.Refresh();GUI.Commands.Clear();browserWindow.Draw();}
void BrowserClick(string label,Func<GUI.Draw,bool>? where=null)
{
    var r=GUI.Commands.Single(c=>c.Text==label&&(where==null||where(c))).Area;
    ZInput.pointerPosition=new Vector3(r.x+r.width/2,Screen.height-r.y-r.height/2);
    ZInput.Down=true;ZInput.Held=true;ZInput.Up=false;browserWindow.HandleInput();
    ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;browserWindow.HandleInput();ZInput.Up=false;BrowserDraw();
}
BrowserDraw();BrowserClick("Fights");
File.WriteAllText(Path.Combine(root,"artifacts","fight-browser-preview.svg"),GUI.Svg());
bool HasBrowserRow(string label)=>GUI.Commands.Any(c=>c.Text==label&&c.Area.y>=300&&c.Area.y<580);
Check(Has("1 / 4")&&HasBrowserRow("Encounter 19")&&HasBrowserRow("Encounter 15")&&!HasBrowserRow("Encounter 00"),"Fight browser first page is wrong");
for(int i=0;i<3;i++)BrowserClick(">",c=>c.Area.y>550);
Check(Has("4 / 4")&&HasBrowserRow("Encounter 00")&&!HasBrowserRow("Encounter 19"),"Fight browser did not reach its oldest page");
BrowserClick("Encounter 00");
Check(Has("HISTORY")&&Has("Fight 20 / 20")&&Has("Encounter 00")&&!Has("WHEN"),"Fight browser selected the wrong retained fight");
browserPlugin.Tracker.Add(new CombatEvent{Id="source:fallback",Time=1000,Amount=25,CaptureMode="fallback",Source=actor,
    Target=new Actor{Id="entity:source",Name="Source check"}});
browserPlugin.Tracker.Add(new CombatEvent{Id="source:owner",Time=1001,Amount=10,CaptureMode="owner",Relayed=true,Source=actor,
    Target=new Actor{Id="entity:source",Name="Source check"}});
browserPlugin.Tracker.Finish("test");BrowserDraw();BrowserClick("Live");BrowserClick("Sources");
Check(Has("Owner-resolved")&&Has("Displayed-number estimate")&&Has("Unspecified / older report")&&
    Has("Relay transport: 10 damage · 1 hits"),"Fight sources did not distinguish measurement and relay delivery");
BrowserClick("Export diagnostics");Check(browserPlugin.DiagnosticExports==1&&browserPlugin.LastDiagnosticFight!=null,
    "Selected-fight diagnostics were not exported");
File.WriteAllText(Path.Combine(root,"artifacts","sources-preview.svg"),GUI.Svg());
var emptyPlugin=new Plugin();var emptyWindow=new MeterWindow(emptyPlugin);
void EmptyDraw(){emptyWindow.Refresh();GUI.Commands.Clear();emptyWindow.Draw();}
void EmptyClick(string label)
{
    var r=GUI.Commands.Single(c=>c.Text==label).Area;
    ZInput.pointerPosition=new Vector3(r.x+r.width/2,Screen.height-r.y-r.height/2);
    ZInput.Down=true;ZInput.Held=true;emptyWindow.HandleInput();
    ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;emptyWindow.HandleInput();ZInput.Up=false;EmptyDraw();
}
EmptyDraw();EmptyClick("Sources");Check(Has("Export diagnostics"),"Diagnostics unavailable before the first fight");
EmptyClick("Export diagnostics");Check(emptyPlugin.DiagnosticExports==1&&emptyPlugin.LastDiagnosticFight==null,
    "Empty-session diagnostics did not export");
var concurrentPlugin=new Plugin();
var bear=new CombatEvent{Id="concurrent:bear",Time=2000,Amount=30,Source=actor,Target=new Actor{Id="entity:bear",Name="Bear"},HasPosition=true,X=0};
var boar=new CombatEvent{Id="concurrent:boar",Time=2001,Amount=20,Source=actor,Target=new Actor{Id="entity:boar",Name="Boar"},HasPosition=true,X=200};
concurrentPlugin.Tracker.Add(bear);concurrentPlugin.Tracker.Add(boar);
var concurrentWindow=new MeterWindow(concurrentPlugin);
void ConcurrentDraw(){concurrentWindow.Refresh();GUI.Commands.Clear();concurrentWindow.Draw();}
void ConcurrentClick(string label)
{
    var r=GUI.Commands.Single(c=>c.Text==label).Area;
    ZInput.pointerPosition=new Vector3(r.x+r.width/2,Screen.height-r.y-r.height/2);
    ZInput.Down=true;ZInput.Held=true;concurrentWindow.HandleInput();
    ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;concurrentWindow.HandleInput();ZInput.Up=false;ConcurrentDraw();
}
ConcurrentDraw();Check(Has("Boar"),"Most recent live encounter not shown");
ConcurrentClick("Fights");Check(Has("Bear")&&Has("Boar")&&Has("2 active · 0 / 20 saved"),"Live encounter browser did not show both groups");
ConcurrentClick("Bear");Check(Has("Bear")&&Has("LIVE")&&Has("2 active · 0 saved"),"Choosing a different active encounter failed");
File.WriteAllText(Path.Combine(root,"artifacts","concurrent-preview.svg"),GUI.Svg());
ConcurrentClick("End fight");Check(concurrentPlugin.Tracker.Active.Count==1&&concurrentPlugin.Tracker.History.Count==1&&Has("HISTORY"),"Ending selected live fight affected another group");
ConcurrentClick("Live");Check(Has("Boar")&&Has("LIVE"),"Live did not return to the remaining active encounter");
var markerPlugin=new Plugin();markerPlugin.Tracker.IdleSeconds=30;
var markerBoss=new Actor{Id="entity:marker-boss",Name="The Elder"};
void MarkerHit(string id,double time,double amount)=>markerPlugin.Tracker.Add(new CombatEvent{Id=id,Time=time,Amount=amount,Source=actor,Target=markerBoss,Weapon="Mace"});
MarkerHit("marker:1",100.2,10);MarkerHit("marker:2",101.2,10);MarkerHit("marker:3",108.2,100);
markerPlugin.Tracker.Add(new CombatEvent{Id="marker:death",Kind="death",Time=109,Target=actor});
markerPlugin.Tracker.Add(new CombatEvent{Id="marker:death-other",Kind="death",Time=109.3,Target=new Actor{Id="player:2",Name="Bjorn",Player=true}});
markerPlugin.Tracker.Add(new CombatEvent{Id="marker:boss",Kind="death",Time=110,Target=markerBoss,Boss=true});
var markerWindow=new MeterWindow(markerPlugin);
void MarkerDraw(){markerWindow.Refresh();GUI.Commands.Clear();markerWindow.Draw();}
void MarkerClick(string label)
{
    var r=GUI.Commands.Single(c=>c.Text==label).Area;
    ZInput.pointerPosition=new Vector3(r.x+r.width/2,Screen.height-r.y-r.height/2);
    ZInput.Down=true;ZInput.Held=true;markerWindow.HandleInput();
    ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;markerWindow.HandleInput();ZInput.Up=false;MarkerDraw();
}
void MarkerHover(double fraction)
{
    var header=GUI.Commands.Single(c=>c.Text=="VALHALLA").Area;
    float x=header.x-18+56+(float)(fraction*444),y=header.y-13+340;
    ZInput.pointerPosition=new Vector3(x,Screen.height-y);MarkerDraw();
}
MarkerDraw();MarkerClick("Details");
Check(Has("Death")&&Has("Boss kill")&&Has("Spike")&&
    GUI.Commands.Any(c=>c.Text==null&&c.Area.height==5&&c.Color.Hex=="#E17A7A")&&
    GUI.Commands.Any(c=>c.Text==null&&c.Area.height==5&&c.Color.Hex=="#D9B46C"),"Timeline marker legend or death/boss ticks missing");
MarkerHover(.85);Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.Contains("Damage spike")),"Spike hover label missing");
MarkerHover(.9);Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.Contains("Player death")),"Death hover label missing");
MarkerHover(1);Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.Contains("Boss defeated")),"Boss hover label missing");
File.WriteAllText(Path.Combine(root,"artifacts","timeline-preview.svg"),GUI.Svg());
var playerNext=GUI.Commands.Single(c=>c.Text==">"&&c.Area.y>250&&c.Area.y<310).Area;
ZInput.pointerPosition=new Vector3(playerNext.x+playerNext.width/2,Screen.height-playerNext.y-playerNext.height/2);
ZInput.Down=true;ZInput.Held=true;markerWindow.HandleInput();
ZInput.Down=false;ZInput.Held=false;ZInput.Up=true;markerWindow.HandleInput();ZInput.Up=false;MarkerDraw();
MarkerHover(.93);Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.Contains("Player death · Bjorn")),"Death-only player's marker could not be inspected");
System.Console.WriteLine("PASS Production overlay: timeline markers, concurrent encounters, source provenance, fight browser and 20-fight paging, Older/Newer/Live, target filters, DPS series, breakdowns, tabs, scaled hit targets, export and dragging");
System.Console.WriteLine("Wrote artifacts/overlay-preview.svg (headless layout preview, not an in-game screenshot).");
}
catch(Exception ex)
{
    System.Console.Error.WriteLine("FAIL Production overlay: " + ex.Message);
    Environment.ExitCode=1;
}

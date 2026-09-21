using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ValhallaMeter;
using ValhallaMeter.Core;

var plugin=new Plugin();var window=new MeterWindow(plugin);
var actor=new Actor {Id="player:1",Name="Astrid",Player=true};int serial=0;
void Damage(double time,double amount,string title,string weapon="",string type=""){var e=new CombatEvent{Id="ui:"+(++serial),Time=time,Amount=amount,Source=actor,Target=new Actor{Id="entity:"+title,Name=title},Weapon=weapon};if(type.Length>0)e.Types[type]=amount;plugin.Tracker.Add(e);}
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
try
{
Damage(10,30,"Boar");plugin.Tracker.Finish("test");Damage(20,60,"Troll");plugin.Tracker.Finish("test");Draw();
Check(Has("Troll"),"Latest fight not displayed");Click("Older");Check(Has("Boar")&&Has("HISTORY"),"Actual Older hit target failed");
Click("Newer");Check(Has("Troll"),"Actual Newer hit target failed");Click("Live");Check(Has("LATEST"),"Actual Live hit target failed");
Damage(30,90,"Greydwarf","Iron mace","Blunt");Damage(32,10,"Neck","Huntsman bow","Pierce");Draw();Check(Has("LIVE")&&Has("2 targets · ALL"),"Live mixed-target view failed");
ClickTargetNext();Check(Has("Greydwarf"),"First target filter failed");ClickTargetNext();Check(Has("Neck"),"Second target filter failed");
Click("Older");Damage(34,10,"Greydwarf");plugin.Tracker.Finish("test");Draw();Check(Has("Troll"),"History drifted");
Click("Live");Click("Details");Check(Has("Astrid")&&GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("DPS OVER TIME"))&&Has("Iron mace")&&Has("Huntsman bow"),"Details histogram did not render time and weapon damage");
Click("Damage types");Check(Has("Blunt")&&Has("Pierce"),"Damage-type histogram did not render");
Click("Buffs");Check(Has("No status samples for this player."),"Buff tab did not switch");
Click("Damage");plugin.Interacting=false;Click("Older");Check(Has("2 targets · ALL"),"Inactive pointer changed selection");
plugin.Interacting=true;plugin.WindowScale.Value=1.5f;Draw();Click("Older");Check(Has("Troll"),"Scaled hit testing failed");
Click("Export report");Check(plugin.Exports==1,"Export control failed");
plugin.WindowScale.Value=1;Draw();
float oldX=plugin.WindowX.Value,oldY=plugin.WindowY.Value;
var header=GUI.Commands.Single(c=>c.Text=="VALHALLA").Area;
ZInput.pointerPosition=new Vector3(header.x+20,Screen.height-header.y-10);
ZInput.Down=true;ZInput.Held=true;window.HandleInput();ZInput.Down=false;
ZInput.pointerPosition=new Vector3(header.x+70,Screen.height-header.y-40);window.HandleInput();
ZInput.Held=false;ZInput.Up=true;window.HandleInput();ZInput.Up=false;Draw();
Check(plugin.WindowX.Value==oldX+50&&plugin.WindowY.Value==oldY+30,"Dragging did not save window position");
Check(Has("F8  ·  RETURN TO GAME"),"Wrong interaction shortcut label");
Click("Live");Click("Details");Click("Weapons / skills");
Check(GUI.Commands.Any(c=>c.Text!=null&&c.Text.StartsWith("DPS OVER TIME"))&&GUI.Commands.Any(c=>Math.Abs(c.Rotation)>.1)&&Has("0.0s")&&Has("Iron mace")&&Has("Huntsman bow"),"Final line-plot preview is incomplete");
plugin.WindowX.Value=30;plugin.WindowY.Value=30;
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));Directory.CreateDirectory(Path.Combine(root,"artifacts"));
File.WriteAllText(Path.Combine(root,"artifacts","overlay-preview.svg"),GUI.Svg());
System.Console.WriteLine("PASS Production overlay: Older/Newer/Live, target filters, DPS line plot, damage breakdowns, stable history, tabs, scaled hit targets, export and dragging");
System.Console.WriteLine("Wrote artifacts/overlay-preview.svg (headless layout preview, not an in-game screenshot).");
}
catch(Exception ex)
{
    System.Console.Error.WriteLine("FAIL Production overlay: " + ex.Message);
    Environment.ExitCode=1;
}

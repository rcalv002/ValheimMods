using System;
using System.Collections.Generic;
using System.Linq;
using HeadingCompass;
using UnityEngine;

static void Check(bool success, string name)
{
    if (!success) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
}
Check(Heading.Readout(0) == "N  000°" && Heading.Readout(90) == "E  090°" &&
    Heading.Readout(180) == "S  180°" && Heading.Readout(270) == "W  270°", "Map cardinal headings");
Check(Heading.Readout(45) == "NE  045°" && Heading.Readout(135) == "SE  135°" &&
    Heading.Readout(225) == "SW  225°" && Heading.Readout(315) == "NW  315°", "Intercardinal headings");
Check(Heading.Readout(359.7) == "N  000°" && Heading.Readout(-90) == "W  270°" &&
    Heading.Readout(720) == "N  000°", "Rounding and wraparound never display 360 degrees");
Check(Heading.Direction(22.4) == "N" && Heading.Direction(22.5) == "NE" &&
    Heading.Direction(337.5) == "N", "Direction sector boundaries");
foreach (double yaw in new[] { 359d, 0d, 1d })
{
    var ticks = Heading.Ticks(yaw).ToArray();
    Check(ticks.Count(t => t.Label == "N") == 1 && ticks.All(t => t.Fraction >= 0 && t.Fraction <= 1),
        "Continuous scale across north at " + yaw);
    var north = ticks.Single(t => t.Label == "N");
    Check(Math.Abs(north.Fraction - (0.5 + (yaw == 359 ? 1 : -yaw) / 150)) < 0.00001,
        "North appears on the correct side of the index at " + yaw);
}
Check(Heading.Readout(double.NaN) == "N  000°" && Heading.Ticks(double.PositiveInfinity).Any(), "Malformed headings remain bounded");

var overlay = new CompassOverlay();
foreach (var size in new[] { (1920, 1080), (1280, 720), (2560, 1440), (3440, 1440), (320, 180) })
{
    Screen.width = size.Item1; Screen.height = size.Item2;
    GUI.Labels.Clear(); GUI.Panels.Clear();
    GUI.matrix = new Matrix4x4 { Scale = 1.7f };
    GUI.color = new Color(0.2f, 0.3f, 0.4f); GUI.depth = 123;
    overlay.Draw(45, 0.5f, 54, 440, 2, 0.82f);
    var panel = GUI.Panels[0];
    Check(panel.x >= 0 && panel.y >= 0 && panel.x + panel.width <= Screen.width && panel.y + panel.height <= Screen.height,
        "Compass fits " + size);
    Check(Math.Abs(panel.x + panel.width / 2 - Screen.width / 2f) < 0.01 && GUI.Labels[0].Text == "NE  045°",
        "Centered heading at " + size);
    Check(GUI.matrix.Scale == 1.7f && GUI.color == new Color(0.2f, 0.3f, 0.4f) && GUI.depth == 123 && GUI.Groups.Count == 0,
        "Shared GUI state restored at " + size);
}
GUI.ThrowLabel = true;
try { overlay.Draw(0, 0.5f, 54, 440, 1, 0.82f); throw new Exception("Expected failure"); }
catch (InvalidOperationException) { }
Check(GUI.matrix.Scale == 1.7f && GUI.depth == 123 && GUI.Groups.Count == 0, "GUI state restored even when drawing fails");
GUI.ThrowLabel = false;

CompassMarker Marker(string id, float x, float z, int priority = 1) => new() {
    Id = id, Name = id, Position = new Vector3(x, 0, z), Priority = priority, Icon = new Texture2D() };
var basePin = Marker("Base", 0, 1800, 2);
var northPins = MarkerLayout.Build(new[] { basePin }, Vector3.zero, 359, 440, 20);
Check(northPins.Count == 1 && northPins[0].X > 220 && Math.Abs(northPins[0].Bearing) < 0.001 &&
    MarkerLayout.DistanceText(northPins[0].Distance) == "1.8 km", "Distant base pin has the correct bearing and distance across north");
var offscreen = MarkerLayout.Build(new[] { Marker("behind", 0, -1000, 2), Marker("ordinary", -200, -1000) }, Vector3.zero, 0, 440, 20);
Check(offscreen.Count == 1 && offscreen[0].Edge && offscreen[0].X == 12 && offscreen[0].Delta == -180,
    "Favorite behind player gets a deterministic turn arrow; ordinary off-arc pin is hidden");
var northEast = MarkerLayout.Build(new[] { Marker("NE", 100, 100) }, Vector3.zero, 0, 440, 20).Single();
var northEastTick = Heading.Ticks(0).Single(t => t.Label == "NE");
Check(Math.Abs(northEast.X - (12 + northEastTick.Fraction * (440 - 24))) < 0.001,
    "Marker bearing aligns exactly with the direction scale");
var many = MarkerLayout.Build(Enumerable.Range(0, 60).Select(i => Marker("pin" + i, i * 10 - 300, 100)), Vector3.zero, 0, 440, 5);
Check(many.Count == 5, "Marker count is bounded even with many map pins");
var crowded = MarkerLayout.Build(new[] { Marker("pin", 0, 30), Marker("Base", 0, 1500, 2),
    Marker("personal", 0, 20, 3), Marker("commander", 0, 20, 3), Marker("assist", 0, 20, 3) }, Vector3.zero, 0, 440, 20);
Check(crowded.Count == 3 && crowded.All(p => p.Marker.Priority == 3) && crowded.Select(p => p.Lane).Distinct().Count() == 3,
    "Three target channels stack ahead of overlapping map pins");
Check(MarkerLayout.Build(new[] { Marker("self", 0, 0, 3), Marker("bad", float.NaN, 2) }, Vector3.zero, 0, 440, 20).Count == 0,
    "Self and invalid positions do not produce arbitrary headings");
Check(!PinFilter.Include(true, true, false, true, false, true, 50, 0, false) &&
    !PinFilter.Include(true, false, false, false, false, true, 50, 0, true) &&
    !PinFilter.Include(true, false, false, true, true, false, 50, 0, true) &&
    PinFilter.Include(true, false, false, true, false, true, 10000, 100, true) &&
    !PinFilter.Include(true, false, false, true, false, true, 10000, 100, false),
    "Checked, hidden and shared pin filters apply, while favorites bypass only distance");
Check(PinFilter.Favorite(" base ", "Home,BASE") && !PinFilter.Favorite("Basement", "Base"), "Favorite names match exactly ignoring case");

// Exercise the actual optional-plugin binding and private minimap field reader.
var source = new MarkerSources();
var markers = new List<CompassMarker>();
Minimap.instance = new Minimap();
Minimap.instance.Pins.Add(new Minimap.PinData { m_name = "Base", m_pos = new Vector3(0, 0, 1800) });
Minimap.instance.Pins.Add(new Minimap.PinData { m_name = "checked", m_pos = new Vector3(1, 0, 50), m_checked = true });
Minimap.instance.Pins.Add(new Minimap.PinData { m_name = "ping", m_pos = new Vector3(1, 0, 50), m_save = false });
source.Collect(markers, Vector3.zero, true, true, false, "Base", 10);
Check(!source.TargetIntegrationAvailable && source.PinIntegrationAvailable && markers.Count == 1 &&
    markers[0].Priority == 2 && markers[0].UV == new Rect(0.1f, 0.2f, 0.3f, 0.4f),
    "Compass without Target Call reads saved distant map pins and sprite atlas coordinates");
BepInEx.Bootstrap.Chainloader.PluginInfos["local.targetcall"] = new() { Instance = new object() };
Check(!new MarkerSources().TargetIntegrationAvailable, "Older Target Call remains optional");
BepInEx.Bootstrap.Chainloader.PluginInfos["local.targetcall"] = new() { Instance = new OptionalTargets() };
source = new MarkerSources(); OptionalTargets.Active = true;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(source.TargetIntegrationAvailable && markers.Any(m => m.Id == "attack" && m.Priority == 3), "Optional Target Call callback preserves target icon priority");
OptionalTargets.Active = false;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(markers.All(m => m.Priority != 3), "Clearing a target removes its compass icon immediately");
Minimap.instance.Pins.Clear(); Time.realtimeSinceStartup = 1;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(markers.Count == 0, "Deleting pins clears cached compass markers");
Minimap.instance.Pins.Add(new Minimap.PinData { m_name = "shared", m_ownerID = 123, m_pos = new Vector3(10, 0, 10) });
Minimap.instance.Shared(false); Time.realtimeSinceStartup = 2;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(markers.Count == 0, "Reader honors the shared-map toggle");
Minimap.instance.Shared(true); Minimap.instance.Filters[0] = false; Time.realtimeSinceStartup = 3;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(markers.Count == 0, "Reader honors map icon-type filters");
Minimap.instance.Filters[0] = true; Time.realtimeSinceStartup = 4;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(markers.Count == 1, "Enabling filters restores saved pins");
Game.m_noMap = true;
source.Collect(markers, Vector3.zero, true, true, false, "Base", 0);
Check(markers.Count == 0, "No-map worlds do not reveal saved pin positions");
Game.m_noMap = false; source.Clear();

foreach (var size in new[] { (1920, 1080), (1280, 720), (3440, 1440), (320, 180) })
{
    Screen.width = size.Item1; Screen.height = size.Item2;
    GUI.Panels.Clear(); GUI.Icons.Clear(); GUI.Labels.Clear();
    overlay.Draw(0, 0.5f, 54, 440, 2, 0.8f, northPins);
    var panel = GUI.Panels[0];
    Check(panel.x >= 0 && panel.y >= 0 && panel.x + panel.width <= Screen.width && panel.y + panel.height <= Screen.height &&
        GUI.Icons.Count == 1 && GUI.Labels.Any(l => l.Text.Contains("Base · 1.8 km")), "Marker lane and destination readout fit " + size);
}
GUI.ThrowTexture = true;
try { overlay.Draw(0, 0.5f, 54, 440, 1, 0.8f, northPins); throw new Exception("Expected failure"); }
catch (InvalidOperationException) { }
Check(GUI.matrix.Scale == 1.7f && GUI.depth == 123 && GUI.Groups.Count == 0, "Icon failure restores shared GUI state");

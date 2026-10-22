using System;
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

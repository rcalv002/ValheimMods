using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace HeadingCompass;

[BepInPlugin("local.headingcompass", "Heading Compass", "0.3.0")]
[BepInDependency("local.targetcall", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private ConfigEntry<bool> visible = null!;
    private ConfigEntry<KeyboardShortcut> toggle = null!, tagKey = null!;
    private ConfigEntry<float> horizontal = null!, top = null!, width = null!, scale = null!, opacity = null!;
    private ConfigEntry<bool> showTargets = null!, showPins = null!, showChecked = null!;
    private ConfigEntry<string> favorites = null!;
    private ConfigEntry<float> radius = null!, farOpacity = null!;
    private ConfigEntry<int> maxMarkers = null!;
    private MarkerSources sources = null!;
    private PinTags tags = null!;
    private readonly List<CompassMarker> markers = new List<CompassMarker>();
    private List<MarkerLayout.Placement> placements = new List<MarkerLayout.Placement>();
    private Player? lastPlayer;
    private readonly CompassOverlay overlay = new CompassOverlay();
    private double heading;
    private bool hasHeading;
    private float nextError;

    private void Awake()
    {
        visible = Config.Bind("Display", "Visible", true, "Show the heading compass. F6 toggles it.");
        toggle = Config.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.F6), "Show/hide the compass while game menus are closed.");
        horizontal = Config.Bind("Display", "HorizontalPosition", 0.5f,
            new ConfigDescription("Screen fraction: 0 is left, 0.5 center, 1 right.", new AcceptableValueRange<float>(0, 1)));
        top = Config.Bind("Display", "Top", 54f,
            new ConfigDescription("Distance from the top of the screen in pixels; default sits below Chat Translator.", new AcceptableValueRange<float>(0, 2160)));
        width = Config.Bind("Display", "Width", 640f,
            new ConfigDescription("Compass width in pixels before scaling; the 150-degree arc stretches to fill it. Capped to fit the screen.", new AcceptableValueRange<float>(240, 1600)));
        scale = Config.Bind("Display", "Scale", 1f,
            new ConfigDescription("Size multiplier; capped to fit the screen.", new AcceptableValueRange<float>(0.6f, 2)));
        opacity = Config.Bind("Display", "Opacity", 0.82f,
            new ConfigDescription("Background opacity.", new AcceptableValueRange<float>(0, 1)));
        showTargets = Config.Bind("Markers", "ShowTargets", true, "Show Target Call markers when Target Call 0.2.3 or later is installed locally.");
        showPins = Config.Bind("Markers", "ShowMapPins", true, "Show saved map pins, respecting map icon filters and shared-map visibility.");
        showChecked = Config.Bind("Markers", "ShowCheckedPins", false, "Include crossed-off pins.");
        favorites = Config.Bind("Markers", "FavoritePins", "Base,Home", "Comma-separated exact pin names (ignoring case). Favorites behave like tagged pins: always shown, with priority and turn arrows outside the compass arc.");
        radius = Config.Bind("Markers", "IconRadius", 300f, new ConfigDescription("Ordinary map pins farther than this horizontal distance in meters are hidden, and nearer ones fade with distance; 0 shows all at full opacity. Tagged pins, favorites, and targets ignore it.", new AcceptableValueRange<float>(0, 100000)));
        farOpacity = Config.Bind("Markers", "FarIconOpacity", 0.3f, new ConfigDescription("Icon opacity at the IconRadius edge; icons fade linearly from fully opaque at your position.", new AcceptableValueRange<float>(0.05f, 1)));
        tagKey = Config.Bind("Controls", "TagPinKey", new KeyboardShortcut(KeyCode.P), "With the large map open, hover a saved pin and press this to tag/untag it as always visible on the compass.");
        maxMarkers = Config.Bind("Markers", "MaxMarkers", 20, new ConfigDescription("Maximum visible marker icons; targets and favorites have priority.", new AcceptableValueRange<int>(3, 40)));
        sources = new MarkerSources();
        tags = new PinTags(System.IO.Path.Combine(Paths.ConfigPath, "local.headingcompass.tags.txt"));
        Logger.LogInfo("Heading Compass 0.3.0 loaded. F6 shows/hides. Map pins: " + sources.PinIntegrationAvailable + "; pin tagging: " + tags.Available +
            "; Target Call integration: " + sources.TargetIntegrationAvailable);
    }

    private void Update()
    {
        if (lastPlayer != Player.m_localPlayer)
        {
            lastPlayer = Player.m_localPlayer;
            sources.Clear(); markers.Clear(); placements.Clear(); tags.UpdateScope();
        }
        if (!Player.m_localPlayer) { hasHeading = false; return; }
        if (!BusyUI() && Pressed(toggle.Value))
            visible.Value = !visible.Value;
        if (Minimap.IsOpen() && !Minimap.InTextInput() && !Console.IsVisible() && !(Chat.instance && Chat.instance.HasFocus()) && Pressed(tagKey.Value))
        {
            try { TagPinUnderCursor(); }
            catch (Exception ex) { LogError(ex); }
        }
    }

    private static bool Pressed(KeyboardShortcut key) =>
        key.MainKey != KeyCode.None && ZInput.GetKeyDown(key.MainKey) && key.Modifiers.All(modifier => ZInput.GetKey(modifier));

    private void TagPinUnderCursor()
    {
        bool? tagged = tags.ToggleUnderCursor(out string name);
        if (tagged == null) return;
        if (name.Length == 0) name = "Map pin";
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, tagged.Value ? name + " is always shown on the compass" : name + " is no longer always shown on the compass");
    }

    private void LateUpdate()
    {
        if (!Player.m_localPlayer) { hasHeading = false; return; }
        try { tags.TintMapIcons(); }
        catch (Exception ex) { LogError(ex); }
        Camera camera = Utils.GetMainCamera();
        hasHeading = camera;
        // Unity yaw is clockwise from +Z, matching the minimap's north/east axes.
        // Using yaw keeps the heading stable when looking straight up or down.
        if (camera) heading = Heading.Normalize(camera.transform.eulerAngles.y);
        if (hasHeading && visible.Value)
        {
            try
            {
                sources.Collect(markers, Player.m_localPlayer.transform.position, showTargets.Value,
                    showPins.Value, showChecked.Value, favorites.Value, radius.Value, tags.IsTagged);
                placements = MarkerLayout.Build(markers, Player.m_localPlayer.transform.position, heading, width.Value, maxMarkers.Value,
                    radius.Value, farOpacity.Value);
            }
            catch (Exception ex) { placements.Clear(); LogError(ex); }
        }
    }

    private static bool BusyUI() => Menu.IsVisible() || InventoryGui.IsVisible() || Console.IsVisible() ||
        TextInput.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || UnifiedPopup.IsVisible() ||
        Hud.IsPieceSelectionVisible() || Hud.InRadial() || (Chat.instance && Chat.instance.HasFocus());

    private void OnGUI()
    {
        if (visible == null || !visible.Value || !hasHeading || !Player.m_localPlayer ||
            Player.m_localPlayer.IsDead() || Hud.IsUserHidden() || Menu.IsVisible() || Minimap.IsOpen()) return;
        try { overlay.Draw(heading, horizontal.Value, top.Value, width.Value, scale.Value, opacity.Value,
            placements); }
        catch (Exception ex) { LogError(ex); }
    }

    private void LogError(Exception ex)
    {
        if (Time.realtimeSinceStartup < nextError) return;
        nextError = Time.realtimeSinceStartup + 30;
        Logger.LogWarning("Compass update failed: " + ex.GetType().Name + ": " + ex.Message);
    }
}

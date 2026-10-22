using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace HeadingCompass;

[BepInPlugin("local.headingcompass", "Heading Compass", "0.2.0")]
[BepInDependency("local.targetcall", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private ConfigEntry<bool> visible = null!;
    private ConfigEntry<KeyboardShortcut> toggle = null!;
    private ConfigEntry<float> horizontal = null!, top = null!, width = null!, scale = null!, opacity = null!;
    private ConfigEntry<bool> showTargets = null!, showPins = null!, showChecked = null!;
    private ConfigEntry<string> favorites = null!;
    private ConfigEntry<float> pinDistance = null!;
    private ConfigEntry<int> maxMarkers = null!;
    private MarkerSources sources = null!;
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
        width = Config.Bind("Display", "Width", 440f,
            new ConfigDescription("Compass width before scaling.", new AcceptableValueRange<float>(240, 800)));
        scale = Config.Bind("Display", "Scale", 1f,
            new ConfigDescription("Size multiplier; capped to fit the screen.", new AcceptableValueRange<float>(0.6f, 2)));
        opacity = Config.Bind("Display", "Opacity", 0.82f,
            new ConfigDescription("Background opacity.", new AcceptableValueRange<float>(0, 1)));
        showTargets = Config.Bind("Markers", "ShowTargets", true, "Show Target Call markers when Target Call 0.2.3 or later is installed locally.");
        showPins = Config.Bind("Markers", "ShowMapPins", true, "Show saved map pins, respecting map icon filters and shared-map visibility.");
        showChecked = Config.Bind("Markers", "ShowCheckedPins", false, "Include crossed-off pins.");
        favorites = Config.Bind("Markers", "FavoritePins", "Base,Home", "Comma-separated exact pin names (ignoring case). Favorites get priority and turn arrows outside the compass arc.");
        pinDistance = Config.Bind("Markers", "MaxPinDistance", 0f, new ConfigDescription("Maximum horizontal distance in meters; 0 is unlimited. Favorites bypass this limit.", new AcceptableValueRange<float>(0, 100000)));
        maxMarkers = Config.Bind("Markers", "MaxMarkers", 20, new ConfigDescription("Maximum visible marker icons; targets and favorites have priority.", new AcceptableValueRange<int>(3, 40)));
        sources = new MarkerSources();
        Logger.LogInfo("Heading Compass 0.2.0 loaded. F6 shows/hides. Map pins: " + sources.PinIntegrationAvailable + "; Target Call integration: " + sources.TargetIntegrationAvailable);
    }

    private void Update()
    {
        if (lastPlayer != Player.m_localPlayer)
        {
            lastPlayer = Player.m_localPlayer;
            sources.Clear(); markers.Clear(); placements.Clear();
        }
        if (!Player.m_localPlayer) { hasHeading = false; return; }
        if (!BusyUI() && ZInput.GetKeyDown(toggle.Value.MainKey) && toggle.Value.Modifiers.All(key => ZInput.GetKey(key)))
            visible.Value = !visible.Value;
    }

    private void LateUpdate()
    {
        if (!Player.m_localPlayer) { hasHeading = false; return; }
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
                    showPins.Value, showChecked.Value, favorites.Value, pinDistance.Value);
                placements = MarkerLayout.Build(markers, Player.m_localPlayer.transform.position, heading, width.Value, maxMarkers.Value);
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

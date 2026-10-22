using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace HeadingCompass;

[BepInPlugin("local.headingcompass", "Heading Compass", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private ConfigEntry<bool> visible = null!;
    private ConfigEntry<KeyboardShortcut> toggle = null!;
    private ConfigEntry<float> horizontal = null!, top = null!, width = null!, scale = null!, opacity = null!;
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
        Logger.LogInfo("Heading Compass 0.1.0 loaded. F6 shows/hides; camera heading: N 000°, E 090°, S 180°, W 270°.");
    }

    private void Update()
    {
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
    }

    private static bool BusyUI() => Menu.IsVisible() || InventoryGui.IsVisible() || Console.IsVisible() ||
        TextInput.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || UnifiedPopup.IsVisible() ||
        Hud.IsPieceSelectionVisible() || Hud.InRadial() || (Chat.instance && Chat.instance.HasFocus());

    private void OnGUI()
    {
        if (visible == null || !visible.Value || !hasHeading || !Player.m_localPlayer ||
            Player.m_localPlayer.IsDead() || Hud.IsUserHidden() || Menu.IsVisible() || Minimap.IsOpen()) return;
        try { overlay.Draw(heading, horizontal.Value, top.Value, width.Value, scale.Value, opacity.Value); }
        catch (Exception ex)
        {
            if (Time.realtimeSinceStartup < nextError) return;
            nextError = Time.realtimeSinceStartup + 30;
            Logger.LogWarning("Compass drawing failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

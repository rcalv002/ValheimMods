using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TargetCall;

[BepInPlugin("local.targetcall", "Target Call", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private static Plugin? instance;
    private ConfigEntry<KeyboardShortcut> callKey = null!;
    private ConfigEntry<KeyboardShortcut> takeKey = null!;
    private ConfigEntry<float> markerDistance = null!;
    private TargetSync sync = null!;
    private Texture2D? pixel;
    private GUIStyle? heading;
    private GUIStyle? targetText;
    private GUIStyle? pointer;
    private double nextErrorLog;

    private void Awake()
    {
        instance = this;
        callKey = Config.Bind("Controls", "CallKey", new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl),
            "Call the hostile creature under the crosshair; repeat on your call to clear it. Ctrl+T with no creature also clears your own call.");
        takeKey = Config.Bind("Controls", "TakeKey", new KeyboardShortcut(KeyCode.T),
            "Take or release the shared call as your local focus. Taking does not aim attacks or turn the camera.");
        markerDistance = Config.Bind("Display", "MarkerDistance", 80f,
            new ConfigDescription("Maximum distance for the on-screen target marker.", new AcceptableValueRange<float>(15f, 150f)));
        sync = new TargetSync();
        Logger.LogInfo("Target Call 0.1.0 loaded. Ctrl+T calls; T takes the shared target.");
    }

    private void Update()
    {
        Guard(() =>
        {
            sync.Update();
            Player player = Player.m_localPlayer;
            if (!player || BusyUI()) return;
            if (ShortcutDown(callKey.Value)) Call(player);
            else if (ShortcutDown(takeKey.Value)) Take(player);
        });
    }

    private static bool ShortcutDown(KeyboardShortcut shortcut) =>
        ZInput.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(key => ZInput.GetKey(key));

    private static bool BusyUI() => Cursor.visible || Menu.IsVisible() || InventoryGui.IsVisible() ||
        Console.IsVisible() || TextInput.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() ||
        UnifiedPopup.IsVisible() || Hud.IsPieceSelectionVisible() || Hud.InRadial() ||
        (Chat.instance && Chat.instance.HasFocus());

    private void Call(Player player)
    {
        Character creature = player.GetHoverCreature();
        if (!creature || creature.IsPlayer() || creature.IsDead() ||
            creature.GetBaseAI() == null || !BaseAI.IsEnemy(player, creature))
        {
            if (sync.State.Active(Time.realtimeSinceStartupAsDouble) && sync.State.Caller == ZNet.GetUID())
            {
                if (sync.Call(0, 0)) Notice(player, "Clearing your target call");
            }
            else Notice(player, "Aim at a hostile creature to call it");
            return;
        }
        ZDOID id = creature.GetZDOID();
        if (id.IsNone()) { Notice(player, "This creature has no network target ID"); return; }
        bool clearing = sync.State.Active(Time.realtimeSinceStartupAsDouble) &&
            sync.State.Caller == ZNet.GetUID() && sync.State.UserId == id.UserID && sync.State.ObjectId == id.ID;
        if (!sync.Call(clearing ? 0 : id.UserID, clearing ? 0u : id.ID))
        {
            Notice(player, "Target sharing needs Target Call on the host");
            return;
        }
        Notice(player, clearing ? "Clearing your target call" : "Called: " + Name(creature));
    }

    private void Take(Player player)
    {
        if (!sync.State.Active(Time.realtimeSinceStartupAsDouble))
        {
            Notice(player, "No shared target to take");
            return;
        }
        bool focused = sync.State.ToggleFocus(Time.realtimeSinceStartupAsDouble);
        Notice(player, focused ? "Took the shared target" : "Released your target focus");
    }

    private static string Name(Character creature)
    {
        string name = Localization.instance != null ? Localization.instance.Localize(creature.GetHoverName()) : creature.GetHoverName();
        return name.Length > 28 ? name.Substring(0, 27) + "…" : name;
    }

    private static void Notice(Player player, string message) => player.Message(MessageHud.MessageType.TopLeft, message);

    private Character? Resolve()
    {
        if (!ZNetScene.instance || !sync.State.Active(Time.realtimeSinceStartupAsDouble)) return null;
        var go = ZNetScene.instance.FindInstance(new ZDOID(sync.State.UserId, sync.State.ObjectId));
        var creature = go ? go.GetComponent<Character>() : null;
        return creature && !creature.IsDead() && !creature.IsPlayer() ? creature : null;
    }

    private void OnGUI()
    {
        Guard(() =>
        {
            if (!Player.m_localPlayer || Hud.IsUserHidden() || BusyUI()) return;
            Character? creature = Resolve();
            if (!creature || Vector3.Distance(Player.m_localPlayer.transform.position, creature.transform.position) > markerDistance.Value) return;
            Camera camera = Utils.GetMainCamera();
            if (!camera) return;
            Vector3 screen = camera.WorldToScreenPoint(creature.GetTopPoint() + Vector3.up * 0.25f);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return;
            EnsureStyle();
            float x = Mathf.Clamp(screen.x, 86, Screen.width - 86);
            float y = Mathf.Clamp(Screen.height - screen.y - 76, 4, Screen.height - 76);
            Color old = GUI.color;
            try
            {
                Color accent = sync.State.Focused ? new Color32(246, 194, 88, 255) : new Color32(89, 216, 220, 255);
                GUI.color = new Color32(12, 22, 29, 220);
                GUI.DrawTexture(new Rect(x - 84, y, 168, 49), pixel!);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(x - 84, y, 168, 2), pixel!);
                heading!.normal.textColor = accent;
                pointer!.normal.textColor = accent;
                GUI.Label(new Rect(x - 80, y + 3, 160, 17), sync.State.Focused ? "TAKEN TARGET" : "CALLED TARGET", heading);
                GUI.color = Color.white;
                GUI.Label(new Rect(x - 80, y + 21, 160, 22), Name(creature), targetText);
                GUI.Label(new Rect(x - 12, y + 46, 24, 23), "▼", pointer);
            }
            finally { GUI.color = old; }
        });
    }

    private void EnsureStyle()
    {
        if (pixel == null)
        {
            pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
        if (heading != null) return;
        heading = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
        targetText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
        pointer = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
    }

    internal static void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            if (instance == null || Time.realtimeSinceStartupAsDouble < instance.nextErrorLog) return;
            instance.nextErrorLog = Time.realtimeSinceStartupAsDouble + 10;
            instance.Logger.LogWarning("Target Call error: " + ex);
        }
    }

    private void OnDestroy()
    {
        if (pixel) Destroy(pixel);
        sync?.State.Reset();
        instance = null;
    }
}

using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TargetCall;

[BepInPlugin("local.targetcall", "Target Call", "0.1.3")]
public sealed class Plugin : BaseUnityPlugin
{
    private static Plugin? instance;
    private ConfigEntry<KeyboardShortcut> callKey = null!;
    private ConfigEntry<KeyboardShortcut> takeKey = null!;
    private ConfigEntry<float> markerDistance = null!;
    private TargetSync sync = null!;
    private Texture2D? pixel;
    private Texture2D? attackIcon;
    private Texture2D? defendIcon;
    private GUIStyle? markerText;
    private double nextErrorLog;

    private void Awake()
    {
        instance = this;
        callKey = Config.Bind("Controls", "CallKey", new KeyboardShortcut(KeyCode.C),
            "Call the player or hostile creature under the crosshair; repeat on your call to clear it. C with no target also clears your own call.");
        takeKey = Config.Bind("Controls", "TakeKey", new KeyboardShortcut(KeyCode.Z),
            "Take or release the shared call as your local focus. Taking does not aim attacks or turn the camera.");
        // Migrate only the original 0.1.0 and 0.1.1 default bindings.
        if ((callKey.Value.MainKey == KeyCode.T && callKey.Value.Modifiers.SequenceEqual(new[] { KeyCode.LeftControl })) ||
            (callKey.Value.MainKey == KeyCode.F6 && !callKey.Value.Modifiers.Any()))
            callKey.Value = new KeyboardShortcut(KeyCode.C);
        if ((takeKey.Value.MainKey == KeyCode.T && !takeKey.Value.Modifiers.Any()) ||
            (takeKey.Value.MainKey == KeyCode.F6 && takeKey.Value.Modifiers.SequenceEqual(new[] { KeyCode.LeftShift })))
            takeKey.Value = new KeyboardShortcut(KeyCode.Z);
        markerDistance = Config.Bind("Display", "MarkerDistance", 80f,
            new ConfigDescription("Maximum distance for the on-screen target marker.", new AcceptableValueRange<float>(15f, 150f)));
        sync = new TargetSync();
        Logger.LogInfo("Target Call 0.1.3 loaded. C calls; Z takes the shared target.");
    }

    private void Update()
    {
        Guard(() =>
        {
            sync.Update();
            Player player = Player.m_localPlayer;
            ObserveDeath();
            if (!player || BusyUI()) return;
            if (ShortcutDown(takeKey.Value)) Take(player);
            else if (ShortcutDown(callKey.Value)) Call(player);
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
        if (!creature || creature == player || creature.IsDead() ||
            (!creature.IsPlayer() && (creature.GetBaseAI() == null || !BaseAI.IsEnemy(player, creature))))
        {
            if (sync.State.Active(Time.realtimeSinceStartupAsDouble) && sync.State.Caller == ZNet.GetUID())
            {
                if (sync.Call(0, 0)) Notice(player, "Clearing your target call");
            }
            else Notice(player, "Aim at a player or hostile creature to call it");
            return;
        }
        ZDOID id = creature.GetZDOID();
        if (id.IsNone()) { Notice(player, "This target has no network ID"); return; }
        bool clearing = sync.State.Active(Time.realtimeSinceStartupAsDouble) &&
            sync.State.Caller == ZNet.GetUID() && sync.State.UserId == id.UserID && sync.State.ObjectId == id.ID;
        if (!sync.Call(clearing ? 0 : id.UserID, clearing ? 0u : id.ID))
        {
            Notice(player, "Target sharing needs Target Call on the host");
            return;
        }
        Notice(player, clearing ? "Clearing your target call" :
            (creature.IsPlayer() ? "Defend: " : "Attack: ") + Name(creature));
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
        string rawName = creature.GetHoverName();
        string name = !creature.IsPlayer() && Localization.instance != null ?
            Localization.instance.Localize(rawName) : rawName;
        return name.Length > 28 ? name.Substring(0, 27) + "…" : name;
    }

    private static void Notice(Player player, string message) => player.Message(MessageHud.MessageType.TopLeft, message);

    private void ObserveDeath()
    {
        if (!ZNetScene.instance || !sync.State.Active(Time.realtimeSinceStartupAsDouble)) return;
        var go = ZNetScene.instance.FindInstance(new ZDOID(sync.State.UserId, sync.State.ObjectId));
        var creature = go ? go.GetComponent<Character>() : null;
        if (creature && creature.IsDead()) sync.ReportDead();
    }

    private Character? Resolve()
    {
        if (!ZNetScene.instance || !sync.State.Active(Time.realtimeSinceStartupAsDouble)) return null;
        var go = ZNetScene.instance.FindInstance(new ZDOID(sync.State.UserId, sync.State.ObjectId));
        var creature = go ? go.GetComponent<Character>() : null;
        return creature && !creature.IsDead() ? creature : null;
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
            bool defend = creature.IsPlayer();
            Texture2D markerIcon = defend ? defendIcon! : attackIcon!;
            float x = Mathf.Clamp(screen.x, 86, Screen.width - 86);
            float y = Mathf.Clamp(Screen.height - screen.y - 78, 4, Screen.height - 78);
            Color old = GUI.color;
            try
            {
                Color accent = sync.State.Focused ? new Color32(246, 194, 88, 255) :
                    defend ? new Color32(89, 216, 220, 255) : new Color32(244, 113, 93, 255);
                GUI.color = new Color32(3, 10, 16, 255);
                GUI.DrawTexture(new Rect(x - 26, y - 2, 52, 52), markerIcon);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(x - 24, y, 48, 48), markerIcon);
                GUI.color = new Color32(12, 22, 29, 228);
                GUI.DrawTexture(new Rect(x - 82, y + 45, 164, 25), pixel!);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(x - 82, y + 45, 164, 2), pixel!);
                GUI.color = Color.white;
                string name = Name(creature);
                if (name.Length > 16) name = name.Substring(0, 15) + "…";
                GUI.Label(new Rect(x - 79, y + 46, 158, 23),
                    (defend ? "DEFEND  " : "ATTACK  ") + name, markerText);
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
        if (attackIcon == null) attackIcon = CreateIcon((x, y) => SwordPixel(x, y, false) || SwordPixel(x, y, true));
        if (defendIcon == null) defendIcon = CreateIcon(ShieldPixel);
        if (markerText != null) return;
        markerText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
    }

    private static Texture2D CreateIcon(Func<float, float, bool> ink)
    {
        var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                texture.SetPixel(x, y, ink(x - 23.5f, y - 23.5f) ? Color.white : Color.clear);
        texture.Apply();
        return texture;
    }

    private static bool SwordPixel(float x, float y, bool mirror)
    {
        if (mirror) x = -x;
        float along = (x + y) * 0.70710678f;
        float across = Math.Abs((x - y) * 0.70710678f);
        bool blade = along >= -19 && along <= 7 && across <=
            (along < -15 ? (along + 19) * 0.5f : 2f);
        bool guard = along > 7 && along <= 10 && across <= 6f;
        bool grip = along > 10 && along <= 17 && across <= 2f;
        bool pommel = along > 17 && along <= 19 && across <= 3f;
        return blade || guard || grip || pommel;
    }

    private static bool ShieldPixel(float x, float y)
    {
        if (y < -18 || y > 20) return false;
        float edge = y <= -3 ? 15f : 15f * (20f - y) / 23f;
        float width = Math.Abs(x);
        bool outline = (y <= -15 && width <= edge) || width >= edge - 2.5f;
        bool cross = (width <= 2.5f && y >= -11 && y <= 10) ||
            (Math.Abs(y) <= 2.5f && width <= 9f);
        return width <= edge && (outline || cross);
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
        if (attackIcon) Destroy(attackIcon);
        if (defendIcon) Destroy(defendIcon);
        sync?.State.Reset();
        instance = null;
    }
}

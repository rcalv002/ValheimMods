using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TargetCall;

[BepInPlugin("local.targetcall", "Target Call", "0.2.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private static Plugin? instance;
    private ConfigEntry<KeyboardShortcut> callKey = null!;
    private ConfigEntry<KeyboardShortcut> takeKey = null!;
    private ConfigEntry<KeyboardShortcut> assistKey = null!;
    private ConfigEntry<KeyboardShortcut> personalKey = null!;
    private ConfigEntry<KeyboardShortcut> leaderKey = null!;
    private ConfigEntry<float> markerDistance = null!;
    private TargetSync sync = null!;
    private TargetSync leaderSync = null!;
    private readonly PersonalMarker personal = new PersonalMarker();
    private long personalHost;
    private int announcedCall = -1, pendingCall = -1;
    private int announcedLeader = -1, pendingLeader = -1;
    private double callPendingSince, leaderPendingSince;
    private Texture2D? attackIcon;
    private Texture2D? defendIcon;
    private Texture2D? personalIcon;
    private Texture2D? leaderIcon;
    private double nextErrorLog;

    private void Awake()
    {
        instance = this;
        callKey = Config.Bind("Controls", "CallKey", new KeyboardShortcut(KeyCode.C),
            "Call a hostile creature under the crosshair; repeat on your call or aim at nothing to clear it.");
        assistKey = Config.Bind("Controls", "AssistKey", new KeyboardShortcut(KeyCode.C, KeyCode.LeftShift),
            "Call a player for assistance. Aim at a player, then press Left Shift+C.");
        personalKey = Config.Bind("Controls", "PersonalKey", new KeyboardShortcut(KeyCode.C, KeyCode.LeftAlt),
            "Toggle a private marker on the aimed player or hostile creature. Left Alt+C with no target clears it.");
        leaderKey = Config.Bind("Controls", "LeaderKey", new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl),
            "Designate a shared commander. Aim at a player, or aim at nothing to designate yourself. Repeat to clear.");
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
        leaderSync = new TargetSync("TargetCall_Leader");
        Logger.LogInfo("Target Call 0.2.0 loaded. C attacks; Shift+C assists; Alt+C marks privately; Ctrl+C designates a commander; Z takes the shared target.");
    }

    private void Update()
    {
        Guard(() =>
        {
            sync.Update();
            leaderSync.Update();
            long host = ZNet.instance ? (ZNet.instance.IsServer() ? ZNet.GetUID() :
                (ZNet.instance.GetServerPeer()?.m_uid ?? 0)) : 0;
            if (host != personalHost) { personal.Clear(); personalHost = host; }
            Player player = Player.m_localPlayer;
            ObserveDeath(sync);
            ObserveDeath(leaderSync);
            if (personal.Active && FindCharacter(personal.UserId, personal.ObjectId)?.IsDead() == true)
                personal.Clear();
            if (!player || BusyUI()) return;
            Announce(player, sync, false, ref announcedCall, ref pendingCall, ref callPendingSince);
            Announce(player, leaderSync, true, ref announcedLeader, ref pendingLeader, ref leaderPendingSince);
            if (ShortcutDown(takeKey.Value)) Take(player);
            else if (ShortcutDown(leaderKey.Value)) DesignateLeader(player);
            else if (ShortcutDown(personalKey.Value)) MarkPersonal(player);
            else if (ShortcutDown(assistKey.Value)) Call(player, true);
            else if (ShortcutDown(callKey.Value)) Call(player, false);
        });
    }

    private static readonly KeyCode[] ModifierKeys = {
        KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.LeftAlt, KeyCode.RightAlt
    };

    private static bool ShortcutDown(KeyboardShortcut shortcut) =>
        ZInput.GetKeyDown(shortcut.MainKey) &&
        shortcut.Modifiers.All(key => ZInput.GetKey(key)) &&
        ModifierKeys.All(key => key == shortcut.MainKey || shortcut.Modifiers.Contains(key) || !ZInput.GetKey(key));

    private static bool BusyUI() => Cursor.visible || Menu.IsVisible() || InventoryGui.IsVisible() ||
        Console.IsVisible() || TextInput.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() ||
        UnifiedPopup.IsVisible() || Hud.IsPieceSelectionVisible() || Hud.InRadial() ||
        (Chat.instance && Chat.instance.HasFocus());

    private void Call(Player player, bool assist)
    {
        Character? creature = HoverTarget(player, assist);
        if (!creature)
        {
            if (!player.GetHoverCreature() && sync.State.Active(Time.realtimeSinceStartupAsDouble) &&
                sync.State.Caller == ZNet.GetUID())
            {
                if (sync.Call(0, 0)) Notice(player, "Clearing your target call");
            }
            else Notice(player, assist ? "Aim at a player to call for assistance" :
                "Aim at a hostile creature to call an attack");
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
            (assist ? "Assist: " : "Attack: ") + Name(creature));
    }

    private static Character? HoverTarget(Player player, bool players)
    {
        Camera camera = Utils.GetMainCamera();
        if (!camera) return null;
        int mask = LayerMask.GetMask("character", "character_net", "character_ghost", "Default",
            "Default_small", "piece", "piece_nonsolid", "static_solid", "terrain", "vehicle");
        RaycastHit[] hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, 50f, mask);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Character? creature = hit.collider.GetComponentInParent<Character>();
            if (creature)
            {
                if (creature == player || creature.IsDead() || creature.IsPlayer() != players) continue;
                if (players || (creature.GetBaseAI() != null && BaseAI.IsEnemy(player, creature)))
                    return creature;
                continue;
            }
            if (!hit.collider.isTrigger) break;
        }
        return null;
    }

    private void MarkPersonal(Player player)
    {
        Character? creature = player.GetHoverCreature();
        if (!creature || creature == player || creature.IsDead() ||
            (!creature.IsPlayer() && (creature.GetBaseAI() == null || !BaseAI.IsEnemy(player, creature))))
        {
            if (personal.Active) { personal.Clear(); Notice(player, "Personal marker cleared"); }
            else Notice(player, "Aim at a player or hostile creature to mark privately");
            return;
        }
        ZDOID id = creature.GetZDOID();
        if (id.IsNone()) { Notice(player, "This target has no network ID"); return; }
        bool marked = personal.Toggle(id.UserID, id.ID);
        Notice(player, marked ? "Personal marker: " + Name(creature) : "Personal marker cleared");
    }

    private void DesignateLeader(Player player)
    {
        Character? hovered = player.GetHoverCreature();
        Player? leader = HoverTarget(player, true) as Player;
        if (!leader && !hovered) leader = player;
        if (!leader || leader.IsDead()) { Notice(player, "Aim at a player to designate a commander"); return; }
        ZDOID id = leader.GetZDOID();
        if (id.IsNone()) { Notice(player, "This player has no network ID"); return; }
        bool clearing = leaderSync.State.Active(Time.realtimeSinceStartupAsDouble) &&
            leaderSync.State.Caller == ZNet.GetUID() &&
            leaderSync.State.UserId == id.UserID && leaderSync.State.ObjectId == id.ID;
        if (!leaderSync.Call(clearing ? 0 : id.UserID, clearing ? 0u : id.ID))
        {
            Notice(player, "Commander sharing needs Target Call 0.2.0 on the host");
            return;
        }
        Notice(player, clearing ? "Commander marker cleared" : "Commander: " + Name(leader));
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

    private void ObserveDeath(TargetSync channel)
    {
        if (!channel.State.Active(Time.realtimeSinceStartupAsDouble)) return;
        Character? creature = FindCharacter(channel.State.UserId, channel.State.ObjectId);
        if (creature && creature.IsDead()) channel.ReportDead();
    }

    private static Character? FindCharacter(long user, uint id)
    {
        if (!ZNetScene.instance) return null;
        var go = ZNetScene.instance.FindInstance(new ZDOID(user, id));
        return go ? go.GetComponent<Character>() : null;
    }

    private static Character? Resolve(CallState state, bool playerOnly = false)
    {
        if (!state.Active(Time.realtimeSinceStartupAsDouble)) return null;
        Character? creature = FindCharacter(state.UserId, state.ObjectId);
        return creature && !creature.IsDead() && (!playerOnly || creature.IsPlayer()) ? creature : null;
    }

    private Character? ResolvePersonal()
    {
        if (!personal.Active) return null;
        Character? creature = FindCharacter(personal.UserId, personal.ObjectId);
        return creature && !creature.IsDead() ? creature : null;
    }

    private static void Announce(Player player, TargetSync channel, bool commander,
        ref int announced, ref int pending, ref double pendingSince)
    {
        CallState state = channel.State;
        if (state.Revision < announced) announced = -1;
        if (state.Revision == announced) return;
        if (!state.Active(Time.realtimeSinceStartupAsDouble) || state.Caller == ZNet.GetUID())
        {
            announced = state.Revision;
            return;
        }
        if (pending != state.Revision)
        {
            pending = state.Revision;
            pendingSince = Time.realtimeSinceStartupAsDouble;
        }
        Character? creature = Resolve(state, commander);
        if (creature)
        {
            Notice(player, (commander ? "Commander: " : creature.IsPlayer() ? "Assist: " : "Attack: ") + Name(creature));
            announced = state.Revision;
        }
        else if (Time.realtimeSinceStartupAsDouble - pendingSince >= 3)
        {
            Notice(player, commander ? "Commander designated" : "Shared target called");
            announced = state.Revision;
        }
    }

    private void OnGUI()
    {
        Guard(() =>
        {
            if (!Player.m_localPlayer || Hud.IsUserHidden() || BusyUI()) return;
            Character? called = Resolve(sync.State);
            Character? commander = Resolve(leaderSync.State, true);
            Character? privateTarget = ResolvePersonal();
            if (!called && !commander && !privateTarget) return;
            Camera camera = Utils.GetMainCamera();
            if (!camera) return;
            EnsureStyle();
            if (called)
                DrawMarker(camera, called, called.IsPlayer() ? defendIcon! : attackIcon!,
                    sync.State.Focused ? new Color32(246, 194, 88, 255) :
                    called.IsPlayer() ? new Color32(89, 216, 220, 255) : new Color32(244, 113, 93, 255), 0);
            if (commander)
                DrawMarker(camera, commander, leaderIcon!, new Color32(249, 209, 82, 255),
                    called && called == commander ? 1 : 0);
            if (privateTarget)
            {
                int stack = (called && called == privateTarget ? 1 : 0) +
                    (commander && commander == privateTarget ? 1 : 0);
                DrawMarker(camera, privateTarget, personalIcon!, new Color32(231, 242, 248, 255),
                    stack);
            }
        });
    }

    private void DrawMarker(Camera camera, Character creature, Texture2D markerIcon,
        Color accent, int stack)
    {
        if (Vector3.Distance(Player.m_localPlayer.transform.position, creature.transform.position) > markerDistance.Value) return;
        Vector3 screen = camera.WorldToScreenPoint(creature.GetTopPoint() + Vector3.up * 0.25f);
        if (screen.z <= 0 || screen.x < 24 || screen.x > Screen.width - 24 ||
            screen.y < 0 || screen.y > Screen.height) return;
        float x = screen.x;
        float y = Screen.height - screen.y - 16 + stack * 52;
        if (y < 2 || y + 50 > Screen.height) return;
        Color old = GUI.color;
        try
        {
            GUI.color = new Color32(3, 10, 16, 255);
            GUI.DrawTexture(new Rect(x - 26, y - 2, 52, 52), markerIcon);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x - 24, y, 48, 48), markerIcon);
        }
        finally { GUI.color = old; }
    }

    private void EnsureStyle()
    {
        if (attackIcon == null) attackIcon = CreateIcon((x, y) => SwordPixel(x, y, false) || SwordPixel(x, y, true));
        if (defendIcon == null) defendIcon = CreateIcon((x, y) => ShieldPixel(x, -y));
        if (personalIcon == null) personalIcon = CreateIcon(ArrowPixel);
        if (leaderIcon == null) leaderIcon = CreateIcon(CrownPixel);
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

    private static bool ArrowPixel(float x, float y)
    {
        bool shaft = Math.Abs(x) <= 3 && y >= -18 && y <= 3;
        bool head = y >= 1 && y <= 19 && Math.Abs(x) <= (19 - y) * 0.9f;
        return shaft || head;
    }

    private static bool CrownPixel(float x, float y)
    {
        bool band = Math.Abs(x) <= 14 && y >= 9 && y <= 15;
        bool outline = LinePixel(x, y, -17, -12, -13, 8) ||
            LinePixel(x, y, -17, -12, -9, -2) ||
            LinePixel(x, y, -9, -2, -5, -14) ||
            LinePixel(x, y, -5, -14, 0, -2) ||
            LinePixel(x, y, 0, -2, 5, -14) ||
            LinePixel(x, y, 5, -14, 9, -2) ||
            LinePixel(x, y, 9, -2, 17, -12) ||
            LinePixel(x, y, 17, -12, 13, 8);
        return band || outline;
    }

    private static bool LinePixel(float x, float y, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
        float ox = x - (ax + t * dx), oy = y - (ay + t * dy);
        return ox * ox + oy * oy <= 3f;
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
        if (attackIcon) Destroy(attackIcon);
        if (defendIcon) Destroy(defendIcon);
        if (personalIcon) Destroy(personalIcon);
        if (leaderIcon) Destroy(leaderIcon);
        sync?.State.Reset();
        leaderSync?.State.Reset();
        personal.Clear();
        instance = null;
    }
}

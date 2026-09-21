using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValhallaMeter.Core;

namespace ValhallaMeter;

[DefaultExecutionOrder(-10000)]
[BepInPlugin("local.valhallameter", "Valhalla Meter", "0.2.0")]
public sealed class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance;
    internal readonly CombatTracker Tracker = new CombatTracker();
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private long sequence;
    private Harmony? harmony;
    private GroupSync sync = null!;
    private MeterWindow window = null!;
    private LogWriter logs = null!;
    private ConfigEntry<bool> group = null!;
    private ConfigEntry<bool> visible = null!;
    private ConfigEntry<KeyboardShortcut> toggle = null!;
    private ConfigEntry<KeyboardShortcut> interact = null!;
    private ConfigEntry<float> idle = null!;
    internal ConfigEntry<float> WindowX = null!;
    internal ConfigEntry<float> WindowY = null!;
    internal ConfigEntry<float> WindowScale = null!;
    internal bool Interacting { get; private set; }
    private double sampleAt;
    private double stamina;
    private double eitr;
    private double nextErrorLog;
    internal string Issue = "";
    internal bool GroupEnabled => group.Value;
    internal string Coverage => LocalDamageFallbackEnabled ? "Local capture · personal DPS fallback" : sync.Status;
    internal bool LocalDamageFallbackEnabled => sync == null || sync.UseLocalDamageFallback;
    internal static double Now => ZNet.instance ? ZNet.instance.GetTimeSeconds() : Time.realtimeSinceStartupAsDouble;

    private void Awake()
    {
        Instance = this;
        visible = Config.Bind("Overlay", "Visible", true, "Show the meter. F7 toggles it; F8 enables mouse interaction.");
        toggle = Config.Bind("Overlay", "ToggleKey", new KeyboardShortcut(KeyCode.F7), "Toggle the overlay.");
        interact = Config.Bind("Overlay", "InteractKey", new KeyboardShortcut(KeyCode.F8), "Toggle mouse interaction while in the world with game menus closed. F8 or Esc exits.");
        // Migrate 0.1.0's default so an existing config cannot bind both actions to F8.
        if (toggle.Value.MainKey == KeyCode.F8 && interact.Value.MainKey == KeyCode.F8 &&
            !toggle.Value.Modifiers.Any() && !interact.Value.Modifiers.Any())
            toggle.Value = new KeyboardShortcut(KeyCode.F7);
        WindowX = Config.Bind("Overlay", "X", 30f, "Horizontal window position.");
        WindowY = Config.Bind("Overlay", "Y", 100f, "Vertical window position.");
        WindowScale = Config.Bind("Overlay", "Scale", 1f, new ConfigDescription("Overlay size multiplier; automatically capped to fit the screen.", new AcceptableValueRange<float>(0.75f, 2f)));
        group = Config.Bind("Recording", "GroupSync", true, "Exchange cooperative combat telemetry with a modded host. Install on all players and host for best coverage; reconnect after changing.");
        idle = Config.Bind("Recording", "IdleSeconds", 8f, new ConfigDescription("Close a fight after this many seconds without damage or absorption.", new AcceptableValueRange<float>(2, 60)));
        logs = new LogWriter(Paths.BepInExRootPath);
        var archive = FightArchive.Load(logs.LogRoot, logs.Directory);
        Tracker.History.AddRange(archive.Fights);
        if (archive.Error.Length > 0) Logger.LogWarning("Previous fight history could not be loaded: " + archive.Error);
        else Logger.LogInfo("Loaded " + archive.Fights.Count + " previous fights" + (archive.Skipped > 0 ? "; skipped " + archive.Skipped + " invalid or oversized files" : "") + ".");
        Tracker.Finished += fight => Guard(() => logs.Export(fight, Now));
        sync = new GroupSync(this);
        window = new MeterWindow(this);
        try
        {
            harmony = new Harmony("local.valhallameter");
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Valhalla Meter 0.2.0 loaded. F7 shows/hides; F8 enables interaction. Logs: " + logs.Directory);
        }
        catch (Exception ex)
        {
            harmony?.UnpatchSelf();
            SetIssue("Combat hooks failed; recording disabled. See BepInEx log.");
            Logger.LogError(ex);
        }
    }

    private void Update()
    {
        Guard(() =>
        {
            sync.Update();
            Tracker.Coverage = LocalDamageFallbackEnabled ?
                "Local observations; personal outgoing damage includes displayed-number fallback" :
                "Group: owner-resolved observations from the modded relay";
            Tracker.IdleSeconds = idle.Value;
            Tracker.Tick(Now);
            if (ShortcutDown(toggle.Value)) { visible.Value = !visible.Value; if (!visible.Value) Interacting = false; }
            if (Interacting && (!Player.m_localPlayer || BusyUI() || ZInput.GetKeyDown(KeyCode.Escape))) Interacting = false;
            if (ShortcutDown(interact.Value))
            {
                if (Interacting) Interacting = false;
                else if (Player.m_localPlayer && !BusyUI()) { visible.Value = true; Interacting = true; window.Notice = ""; }
                else window.Notice = "Close game menus, then press F8 to interact.";
            }
            window.HandleInput();
            if (Time.realtimeSinceStartupAsDouble >= sampleAt)
            {
                sampleAt = Time.realtimeSinceStartupAsDouble + 0.5;
                SamplePlayer();
                window.Refresh();
            }
            if (logs.Error.Length > 0) SetIssue(logs.Error);
        });
    }

    private static bool ShortcutDown(KeyboardShortcut shortcut) => ZInput.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(key => ZInput.GetKey(key));
    private static bool BusyUI() => Menu.IsVisible() || InventoryGui.IsVisible() || Console.IsVisible() ||
        TextInput.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || UnifiedPopup.IsVisible() ||
        Hud.IsPieceSelectionVisible() || Hud.InRadial() || (Chat.instance && Chat.instance.HasFocus());
    private void OnApplicationFocus(bool focused) { if (!focused) Interacting = false; }

    private void SamplePlayer()
    {
        var p = Player.m_localPlayer;
        if (!p || !p.IsOwner()) { stamina = 0; eitr = 0; return; }
        var actor = Capture.ActorOf(p);
        if (stamina > 0) Publish(new CombatEvent { Kind = "stamina", Source = actor, Amount = stamina, Ability = "All stamina use" });
        if (eitr > 0) Publish(new CombatEvent { Kind = "eitr", Source = actor, Amount = eitr, Ability = "All eitr use" });
        stamina = 0; eitr = 0;
        Publish(new CombatEvent { Kind = "status", Source = actor,
            Statuses = p.GetSEMan().GetStatusEffects().Select(s => Capture.Localize(s.m_name)).Distinct().Take(64).ToArray() });
    }

    internal void AccumulateResource(string kind, double amount) { if (kind == "stamina") stamina += amount; else eitr += amount; }
    internal void Publish(CombatEvent e)
    {
        if (!ZNet.instance || sync == null) return;
        e.Id = sessionId + ":" + (++sequence);
        e.Time = Now;
        if (e.Valid()) sync.Publish(e);
    }
    internal bool Accept(CombatEvent e)
    {
        if (!Tracker.Add(e)) return false;
        logs.Event(e);
        return true;
    }
    internal void Export(Fight fight) { logs.Export(fight, Now); window.Notice = "Export queued: BepInEx/ValhallaMeter/logs"; }
    internal void SetIssue(string message)
    {
        Issue = message;
        if (Time.realtimeSinceStartupAsDouble >= nextErrorLog)
        {
            nextErrorLog = Time.realtimeSinceStartupAsDouble + 10;
            Logger.LogWarning(message);
        }
    }
    internal static void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { Instance?.SetIssue("Meter error: " + ex.GetType().Name + ": " + ex.Message); }
    }
    private void OnGUI()
    {
        if (visible != null && visible.Value && Player.m_localPlayer && window != null)
            Guard(() => window.Draw());
    }
    private void OnDestroy()
    {
        Interacting = false;
        Guard(() => { Capture.ClearFallbacks(); Tracker.ResetSession(); logs?.Dispose(); });
        harmony?.UnpatchSelf();
        Instance = null;
    }
}

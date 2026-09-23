using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValhallaMeter.Core;

namespace ValhallaMeter;

internal sealed class MeterWindow
{
    private const float Width = 520, Height = 620;
    private const int PageSize = 8;
    private readonly Plugin plugin;
    private readonly FightSelection selection = new FightSelection();
    private readonly PointerClick click = new PointerClick();
    private readonly List<HitTarget> targets = new List<HitTarget>();
    private readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
    private readonly Dictionary<string, double> browserDamage = new Dictionary<string, double>();
    private Rect rect;
    private List<PlayerRow> rows = new List<PlayerRow>();
    private List<Actor> enemyTargets = new List<Actor>();
    private Fight? fight;
    private int tab, page, pageCount = 1, detailMode;
    private int browserPage;
    private bool browserOpen;
    private string selected = "";
    private string selectedTarget = "";
    private string selectedBreakdown = "";
    private double duration = 1, total, unknown;
    private FightCoverage coverage = new FightCoverage();
    private bool dragging;
    private Vector2 dragOffset;
    public string Notice = "";
    private static readonly string[] Tabs = { "Damage", "Taken", "Healing", "Shields", "Details", "Buffs", "Deaths", "Compare", "Sources" };
    private static readonly Color Background = new Color32(15, 21, 28, 246);
    private static readonly Color Surface = new Color32(25, 34, 43, 255);
    private static readonly Color Border = new Color32(48, 63, 74, 255);
    private static readonly Color Ink = new Color32(235, 241, 244, 255);
    private static readonly Color Muted = new Color32(153, 172, 184, 255);
    private static readonly Color Accent = new Color32(110, 213, 211, 255);
    private static readonly Color Gold = new Color32(217, 180, 108, 255);
    private static readonly Color Dim = new Color32(81, 100, 112, 255);
    private sealed class HitTarget { public string Key = ""; public Rect Area; public Action Action = null!; }
    public MeterWindow(Plugin plugin)
    {
        this.plugin = plugin;
        rect = new Rect(plugin.WindowX.Value, plugin.WindowY.Value, Width, Height);
    }
    private float Scale => Math.Min(plugin.WindowScale.Value, Math.Min(Screen.width / Width, Screen.height / Height));
    private Vector2 Pointer
    {
        get { Vector3 p = ZInput.pointerPosition; return new Vector2(p.x, Screen.height - p.y) / Math.Max(0.1f, Scale); }
    }
    public void Refresh()
    {
        Fight? next = selection.Resolve(plugin.Tracker.Current, plugin.Tracker.History);
        if (next?.Id != fight?.Id) { page = 0; selectedTarget = ""; selectedBreakdown = ""; Notice = ""; }
        fight = next;
        if (fight == null) { rows.Clear(); enemyTargets.Clear(); total = unknown = 0; coverage = new FightCoverage(); return; }
        enemyTargets = CombatTracker.Targets(fight);
        if (selectedTarget.Length > 0 && !enemyTargets.Any(t => t.Id == selectedTarget)) selectedTarget = "";
        duration = CombatTracker.Duration(fight, Plugin.Now, selectedTarget);
        rows = CombatTracker.Rows(fight, selectedTarget);
        total = rows.Sum(r => r.Damage);
        coverage = FightCoverage.Summarize(fight);
        unknown = fight.Events.Where(e => e.Kind == "damage" && e.Source.Id == "unknown" &&
            (selectedTarget.Length == 0 || e.Target.Id == selectedTarget)).Sum(e => e.Amount);
        if (!rows.Any(r => r.Actor.Id == selected)) selected = rows.FirstOrDefault()?.Actor.Id ?? "";
    }

    // Use Valheim's new-input-system pointer/buttons. Rendering never depends on
    // GUI.Button receiving Unity's legacy MouseDown/MouseUp events.
    public void HandleInput()
    {
        bool active = plugin.Interacting;
        Vector2 pointer = Pointer;
        Vector2 local = pointer - rect.position;
        bool down = active && ZInput.GetMouseButtonDown(0), up = active && ZInput.GetMouseButtonUp(0);
        if (down && !plugin.WindowLocked.Value && new Rect(0, 0, Width, 43).Contains(local) &&
            !new Rect(410, 12, 92, 24).Contains(local)) { dragging = true; dragOffset = local; }
        if (dragging && active && ZInput.GetMouseButton(0)) { rect.position = pointer - dragOffset; Clamp(); }
        if (dragging && (!active || up)) { dragging = false; SavePosition(); }
        var target = !dragging ? targets.LastOrDefault(t => t.Area.Contains(local)) : null;
        string? activated = click.Update(active && !dragging, down, up, target?.Key);
        if (activated != null && target != null) { target.Action(); Refresh(); }
        if (active && new Rect(0, 238, Width, 310).Contains(local))
        {
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel != 0)
            {
                if (browserOpen) browserPage = Math.Clamp(browserPage + (wheel < 0 ? 1 : -1), 0, BrowserPageCount - 1);
                else page = Math.Clamp(page + (wheel < 0 ? 1 : -1), 0, pageCount - 1);
            }
        }
    }
    private void Clamp()
    {
        float scale = Math.Max(0.1f, Scale);
        rect.x = Mathf.Clamp(rect.x, 0, Math.Max(0, Screen.width / scale - Width));
        rect.y = Mathf.Clamp(rect.y, 0, Math.Max(0, Screen.height / scale - Height));
    }
    private void SavePosition() { plugin.WindowX.Value = rect.x; plugin.WindowY.Value = rect.y; }
    public void Draw()
    {
        if (Event.current.type != EventType.Repaint) return;
        Clamp(); targets.Clear();
        Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color;
        bool oldEnabled = GUI.enabled; int oldDepth = GUI.depth;
        try
        {
            GUI.enabled = true; GUI.depth = -100;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Math.Max(0.1f, Scale));
            GUI.BeginGroup(rect);
            try { Contents(); } finally { GUI.EndGroup(); }
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth; }
    }
    private void Contents()
    {
        Box(new Rect(0, 0, Width, Height), Border);
        Box(new Rect(1, 1, Width - 2, Height - 2), new Color(Background.r, Background.g, Background.b, plugin.WindowOpacity.Value));
        Box(new Rect(1, 1, Width - 2, 3), Accent);
        Text(new Rect(18, 13, 205, 24), "VALHALLA", 17, Ink, bold: true);
        Text(new Rect(245, 13, plugin.Interacting ? 157 : 257, 22), plugin.Interacting ? "F8  ·  RETURN TO GAME" : "F8  ·  INTERACT     F7  ·  HIDE", 10, plugin.Interacting ? Accent : Muted, TextAnchor.MiddleRight);
        if (plugin.Interacting) Button("window:lock", new Rect(410, 12, 92, 24), plugin.WindowLocked.Value ? "Unlock panel" : "Lock panel", true,
            () => plugin.WindowLocked.Value = !plugin.WindowLocked.Value, plugin.WindowLocked.Value);
        Line(44);
        Text(new Rect(18, 50, 365, 18), fight == null ? plugin.Coverage : SourceLabel(), 10, Muted);
        string mode = fight != null && !fight.Complete ? "LIVE" : !selection.IsLive ? "HISTORY" : "LATEST";
        Text(new Rect(396, 50, 106, 18), mode, 11, mode == "HISTORY" ? Gold : Accent, TextAnchor.MiddleRight, true);
        bool canSelectTarget = enemyTargets.Count > 1;
        Button("target:prev", new Rect(18, 73, 27, 27), "<", canSelectTarget, () => CycleTarget(-1));
        string targetLabel = fight == null ? "Waiting for combat" : selectedTarget.Length == 0 ?
            (enemyTargets.Count > 1 ? fight.Title + " · ALL" : fight.Title) :
            enemyTargets.FirstOrDefault(t => t.Id == selectedTarget)?.Name ?? fight.Title;
        Text(new Rect(53, 72, 267, 29), targetLabel, 20, selectedTarget.Length > 0 ? Gold : Ink, bold: true);
        Button("target:next", new Rect(326, 73, 27, 27), ">", canSelectTarget, () => CycleTarget(1));
        int index = selection.Index(plugin.Tracker.History);
        string position = fight != null && !fight.Complete ? $"{plugin.Tracker.Active.Count} active · {plugin.Tracker.History.Count} saved" :
            !selection.IsLive ? (index >= 0 ? $"Fight {index + 1} / {plugin.Tracker.History.Count}" : "Pinned fight") : $"{plugin.Tracker.History.Count} / 20 saved";
        Text(new Rect(365, 76, 137, 23), position, 11, Muted, TextAnchor.MiddleRight);
        if (fight != null)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string relative = FightClock.Relative(fight.RecordedAtUnix, nowUnix);
            string absolute = DateTimeOffset.FromUnixTimeSeconds(fight.RecordedAtUnix).ToLocalTime().ToString("MMM d, yyyy h:mm tt");
            Text(new Rect(270, 97, 232, 13), relative + " · " + absolute, 9, Muted, TextAnchor.MiddleRight);
        }
        Stat(18, "GROUP DPS", fight == null ? "—" : (total / duration).ToString("N1"), Accent);
        Stat(183, "TOTAL DAMAGE", fight == null ? "—" : total.ToString("N0"), Ink);
        Stat(348, "DURATION", fight == null ? "—" : duration.ToString("0.0") + "s", Ink);
        Button("live", new Rect(18, 173, 63, 28), "Live", !selection.IsLive || browserOpen,
            () => { selection.Live(); browserOpen = false; }, selection.IsLive && !browserOpen);
        Button("older", new Rect(87, 173, 63, 28), "Older", selection.CanOlder(plugin.Tracker.Current, plugin.Tracker.History),
            () => { selection.Older(plugin.Tracker.Current, plugin.Tracker.History); browserOpen = false; });
        Button("newer", new Rect(156, 173, 63, 28), "Newer", !selection.IsLive,
            () => { selection.Newer(plugin.Tracker.History); browserOpen = false; });
        Button("fights", new Rect(225, 173, 74, 28), "Fights", plugin.Tracker.History.Count + plugin.Tracker.Active.Count > 0,
            () => { browserOpen = !browserOpen; browserPage = 0; }, browserOpen);
        Button("end", new Rect(305, 173, 86, 28), "End fight", fight != null && !fight.Complete,
            () => { if (fight != null) plugin.Tracker.Finish(fight, "Manual"); });
        Button("export", new Rect(397, 173, 105, 28), "Export report", fight != null, () => { if (fight != null) plugin.Export(fight); });
        for (int i = 0; i < Tabs.Length; i++)
        {
            int value = i; float tabWidth = 484f / Tabs.Length;
            Button("tab:" + i, new Rect(18 + i * tabWidth, 214, tabWidth, 28), Tabs[i], true, () => { browserOpen = false; tab = value; if (value >= 7) selectedTarget = ""; page = 0; }, !browserOpen && tab == i, true);
        }
        if (browserOpen) DrawFightBrowser();
        else if (fight == null)
        {
            pageCount = 1;
            Text(new Rect(30, 296, 460, 30), "Your next fight starts here.", 17, Ink, TextAnchor.MiddleCenter);
            Text(new Rect(30, 331, 460, 30), "Deal or take damage to start recording.", 12, Muted, TextAnchor.MiddleCenter);
        }
        else if (tab < 4) DrawRanking();
        else if (tab == 4) DrawDetails();
        else if (tab == 5) DrawBuffs();
        else if (tab == 6) DrawDeaths();
        else if (tab == 7) DrawComparison();
        else DrawSources();
        Button("page:prev", new Rect(18, 552, 31, 23), "<", browserOpen ? browserPage > 0 : page > 0,
            () => { if (browserOpen) browserPage--; else page--; });
        Text(new Rect(54, 552, 110, 23), browserOpen ? $"{browserPage + 1} / {BrowserPageCount}" : $"{page + 1} / {pageCount}", 10, Muted);
        Button("page:next", new Rect(163, 552, 31, 23), ">", browserOpen ? browserPage + 1 < BrowserPageCount : page + 1 < pageCount,
            () => { if (browserOpen) browserPage++; else page++; });
        Text(new Rect(212, 552, 290, 23), browserOpen ? $"{plugin.Tracker.Active.Count} active · {plugin.Tracker.History.Count} / 20 saved" :
            fight == null ? "" : $"Unattributed {unknown:N0}   ·   {fight.Events.Count:N0} events", 10, Muted, TextAnchor.MiddleRight);
        Line(582);
        string interaction = browserOpen ? "INTERACTING  ·  Select an active or saved fight to inspect it." :
            tab == 4 && fight != null ? "INTERACTING  ·  Click a breakdown row to isolate it. Click again to clear." :
            plugin.WindowLocked.Value ? "INTERACTING  ·  Panel locked. Use Unlock panel to enable dragging." :
            "INTERACTING  ·  Click a player for details. Drag the header to move.";
        string status = plugin.Issue.Length > 0 ? plugin.Issue : Notice.Length > 0 ? Notice : plugin.Interacting ? interaction : "Press F8 to browse fights, change tabs, or move this panel.";
        Text(new Rect(18, 588, 484, 24), status, 10, plugin.Issue.Length > 0 ? Gold : Muted);
    }
    private void Stat(float x, string label, string value, Color color)
    {
        Box(new Rect(x, 112, 154, 50), Surface);
        Text(new Rect(x + 10, 115, 134, 15), label, 9, Muted);
        Text(new Rect(x + 10, 132, 134, 28), value, 22, color, bold: true);
    }
    private string SourceLabel()
    {
        if (coverage.TotalDamage <= 0) return "No outgoing player damage recorded";
        var parts = new List<string>();
        if (coverage.OwnerDamage > 0) parts.Add($"{coverage.OwnerDamage:N0} owner");
        if (coverage.FallbackDamage > 0) parts.Add($"{coverage.FallbackDamage:N0} estimated");
        if (coverage.UnspecifiedDamage > 0) parts.Add($"{coverage.UnspecifiedDamage:N0} unspecified");
        return "Recorded: " + string.Join(" · ", parts);
    }
    private void DrawSources()
    {
        Pages(0);
        Text(new Rect(22, 250, 478, 25), "RECORDED OUTGOING PLAYER DAMAGE", 11, Gold, bold: true);
        SourceRow(285, "Owner-resolved", coverage.OwnerDamage, coverage.OwnerHits);
        SourceRow(337, "Displayed-number estimate", coverage.FallbackDamage, coverage.FallbackHits);
        SourceRow(389, "Unspecified / older report", coverage.UnspecifiedDamage, coverage.UnspecifiedHits);
        Text(new Rect(22, 448, 478, 21), $"Relay transport: {coverage.RelayedDamage:N0} damage · {coverage.RelayedHits} hits", 11, Accent);
        Text(new Rect(22, 476, 478, 20), "Relay describes delivery; owner/fallback describes measurement.", 10, Muted);
        Text(new Rect(22, 500, 478, 20), "These totals cannot reveal hits missed by unmodded owners.", 10, Muted);
        Text(new Rect(22, 524, 478, 18), fight!.Coverage, 9, Muted);
    }
    private void SourceRow(float y, string label, double amount, int hits)
    {
        var area = new Rect(18, y, 484, 45);
        Box(area, Surface);
        if (coverage.TotalDamage > 0) Box(new Rect(18, y + 42, (float)(484 * amount / coverage.TotalDamage), 3), Accent);
        Text(new Rect(28, y + 3, 294, 38), label, 12, Ink);
        Text(new Rect(325, y + 3, 167, 38), $"{amount:N0} · {hits} hits", 11, Accent, TextAnchor.MiddleRight);
    }
    private int BrowserPageCount => Math.Max(1, (plugin.Tracker.Active.Count + plugin.Tracker.History.Count + 4) / 5);
    private void DrawFightBrowser()
    {
        var history = plugin.Tracker.History;
        var fights = plugin.Tracker.Active.OrderByDescending(f => f.LastCombat).Concat(history).ToArray();
        foreach (var id in browserDamage.Keys.Where(id => !fights.Any(f => f.Id == id)).ToArray()) browserDamage.Remove(id);
        browserPage = Math.Clamp(browserPage, 0, BrowserPageCount - 1);
        Text(new Rect(24, 250, 140, 18), "WHEN", 9, Muted);
        Text(new Rect(167, 250, 150, 18), "TARGET", 9, Muted);
        Text(new Rect(317, 250, 80, 18), "DURATION", 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(400, 250, 96, 18), "DAMAGE", 9, Muted, TextAnchor.MiddleRight);
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int slot = 0;
        foreach (var item in fights.Skip(browserPage * 5).Take(5))
        {
            float y = 274 + slot++ * 52;
            var area = new Rect(18, y, 484, 48);
            bool active = item.Id == fight?.Id;
            Box(area, active ? new Color32(33, 66, 73, 255) : Surface);
            if (active) Box(new Rect(18, y, 3, 48), Accent);
            Text(new Rect(28, y + 3, 132, 20), item.Complete ? FightClock.Relative(item.RecordedAtUnix, nowUnix) : "LIVE · " + FightClock.Relative(item.RecordedAtUnix, nowUnix), 12, active ? Accent : Ink, bold: active);
            Text(new Rect(28, y + 23, 132, 17), DateTimeOffset.FromUnixTimeSeconds(item.RecordedAtUnix).ToLocalTime().ToString("MMM d, yyyy h:mm tt"), 9, Muted);
            Text(new Rect(167, y + 5, 146, 38), item.Title, 12, Ink);
            Text(new Rect(317, y + 5, 80, 38), item.Duration(Plugin.Now).ToString("0.0") + "s", 11, Muted, TextAnchor.MiddleRight);
            if (!item.Complete || !browserDamage.TryGetValue(item.Id, out double damage))
                browserDamage[item.Id] = damage = CombatTracker.Rows(item).Sum(r => r.Damage);
            Text(new Rect(400, y + 5, 96, 38), damage.ToString("N0"), 12, active ? Accent : Ink, TextAnchor.MiddleRight, active);
            Hit("fight:" + item.Id, area, () =>
            {
                if (!item.Complete) { selection.SelectActive(item); browserOpen = false; page = 0; }
                else if (selection.Select(item, history)) { browserOpen = false; page = 0; }
            });
        }
    }
    private void Pages(int count, int size = PageSize) { pageCount = Math.Max(1, (count + size - 1) / size); page = Math.Clamp(page, 0, pageCount - 1); }
    private double Value(PlayerRow row) => tab switch { 1 => row.Taken, 2 => row.HealingReceived, 3 => row.Absorbed, _ => row.Damage };
    private void DrawRanking()
    {
        var ranking = rows.OrderByDescending(Value).ToArray(); Pages(ranking.Length);
        double max = Math.Max(1, ranking.Select(Value).DefaultIfEmpty(0).Max()), all = ranking.Sum(Value);
        double rateDuration = tab == 2 || tab == 3 ? fight!.Duration(Plugin.Now) : duration;
        Text(new Rect(28, 248, 208, 19), "PLAYER", 9, Muted);
        Text(new Rect(240, 248, 86, 19), "PER SECOND", 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(331, 248, 90, 19), "TOTAL", 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(424, 248, 68, 19), "SHARE", 9, Muted, TextAnchor.MiddleRight);
        int n = 0;
        foreach (var r in ranking.Skip(page * PageSize).Take(PageSize))
        {
            float y = 272 + n++ * 31; double amount = Value(r); var area = new Rect(18, y, 484, 28);
            Box(area, Surface);
            Box(new Rect(18, y, (float)(484 * amount / max), 28), new Color32(33, 66, 73, 255));
            Box(new Rect(18, y + 26, (float)(484 * amount / max), 2), Accent);
            Text(new Rect(28, y, 205, 27), r.Actor.Name, 12, Ink);
            Text(new Rect(240, y, 86, 27), (amount / rateDuration).ToString("N1"), 13, Accent, TextAnchor.MiddleRight, true);
            Text(new Rect(331, y, 90, 27), amount.ToString("N0"), 12, Ink, TextAnchor.MiddleRight);
            Text(new Rect(424, y, 68, 27), (all > 0 ? amount / all * 100 : 0).ToString("0.0") + "%", 11, Muted, TextAnchor.MiddleRight);
            Hit("player:" + r.Actor.Id, area, () => { selected = r.Actor.Id; selectedBreakdown = ""; tab = 4; page = 0; });
        }
        if (tab == 2) Text(new Rect(22, 526, 478, 18), "Effective healing received · healer identity unavailable", 10, Muted);
        if (tab == 3) Text(new Rect(22, 526, 478, 18), "Absorbed on recipient · measured before armor", 10, Muted);
    }
    private void PlayerSelector()
    {
        Button("player:prev", new Rect(18, 252, 27, 26), "<", rows.Count > 1, () => Cycle(-1));
        Text(new Rect(54, 252, 410, 26), rows.FirstOrDefault(r => r.Actor.Id == selected)?.Actor.Name ?? "No player", 13, Gold, bold: true);
        Button("player:next", new Rect(475, 252, 27, 26), ">", rows.Count > 1, () => Cycle(1));
    }
    private void Cycle(int delta)
    {
        if (rows.Count == 0) return;
        selected = rows[(Math.Max(0, rows.FindIndex(r => r.Actor.Id == selected)) + delta + rows.Count) % rows.Count].Actor.Id;
        selectedBreakdown = ""; page = 0;
    }
    private void DrawDetails()
    {
        PlayerSelector(); var r = rows.FirstOrDefault(row => row.Actor.Id == selected);
        if (r == null) { Pages(0); return; }
        var hits = fight!.Events.Where(e => e.Kind == "damage" && e.Source.Id == selected &&
            (selectedTarget.Length == 0 || e.Target.Id == selectedTarget)).ToArray();
        double combined = hits.Sum(e => e.Amount);
        Button("detail:weapons", new Rect(18, 436, 112, 23), "Weapons / skills", true, () => { detailMode = 0; selectedBreakdown = ""; page = 0; }, detailMode == 0, true);
        Button("detail:types", new Rect(136, 436, 100, 23), "Damage types", true, () => { detailMode = 1; selectedBreakdown = ""; page = 0; }, detailMode == 1, true);
        Button("detail:results", new Rect(242, 436, 96, 23), "Hit results", true, () => { detailMode = 2; selectedBreakdown = ""; page = 0; }, detailMode == 2, true);
        List<(string Key, string Label, double Amount, int Hits)> values;
        if (detailMode == 0)
            values = hits.GroupBy(WeaponCategory)
                .Select(g => (g.Key, g.Key, g.Sum(e => e.Amount), g.Count())).OrderByDescending(v => v.Item3).ToList();
        else if (detailMode == 1)
        {
            values = hits.SelectMany(e => e.Types.Select(p => (p.Key, p.Value, e.Id))).GroupBy(p => p.Key)
                .Select(g => (g.Key, g.Key, g.Sum(p => p.Value), g.Select(p => p.Id).Distinct().Count())).OrderByDescending(v => v.Item3).ToList();
            double unresolved = Math.Max(0, combined - values.Sum(v => v.Amount));
            int unresolvedHits = hits.Count(e => DamageTypeAmount(e, "Unresolved / fallback") > 0.05);
            if (unresolved > 0.05) values.Add(("Unresolved / fallback", "Unresolved / fallback", unresolved, unresolvedHits));
        }
        else values = hits.GroupBy(ResultCategory).Select(g => (g.Key, g.Key, g.Sum(e => e.Amount), g.Count()))
            .OrderByDescending(v => v.Item3).ThenByDescending(v => v.Item4).ToList();
        if (selectedBreakdown.Length > 0 && !values.Any(v => v.Key == selectedBreakdown)) selectedBreakdown = "";
        Func<CombatEvent, double>? selector = null;
        Func<CombatEvent, bool>? matcher = null;
        if (selectedBreakdown.Length > 0)
        {
            matcher = detailMode == 0 ? e => WeaponCategory(e) == selectedBreakdown : detailMode == 1 ?
                e => DamageTypeAmount(e, selectedBreakdown) > 0 : e => ResultCategory(e) == selectedBreakdown;
            selector = detailMode == 1 ? e => DamageTypeAmount(e, selectedBreakdown) : e => matcher(e) ? e.Amount : 0;
        }
        double selectedTotal = selector == null ? combined : hits.Sum(selector);
        int selectedHits = matcher == null ? hits.Length : hits.Count(matcher);
        DrawDpsLinePlot(r, combined, selectedTotal, selectedHits, selector);
        Pages(values.Count, 3); int i = 0;
        foreach (var value in values.Skip(page * 3).Take(3))
        {
            double share = combined > 0 ? value.Amount / combined * 100 : 0;
            string key = value.Key;
            string summary = detailMode == 2 ? value.Hits + (value.Hits == 1 ? " hit  ·  " : " hits  ·  ") + value.Amount.ToString("N0") :
                value.Amount.ToString("N0") + "  " + share.ToString("0.0") + "%";
            BreakdownBar(466 + i++ * 25, 22, value.Label, value.Amount, Math.Max(1, combined), summary, key == selectedBreakdown,
                () => { selectedBreakdown = selectedBreakdown == key ? "" : key; });
        }
        if (values.Count == 0) Text(new Rect(22, 474, 480, 30), detailMode == 0 ? "No weapon or skill damage recorded." : "No damage-type data recorded.", 11, Muted);
    }
    private void CycleTarget(int delta)
    {
        if (enemyTargets.Count <= 1) return;
        var options = new List<string> { "" };
        options.AddRange(enemyTargets.Select(t => t.Id));
        int index = options.IndexOf(selectedTarget);
        selectedTarget = options[(Math.Max(0, index) + delta + options.Count) % options.Count];
        selectedBreakdown = ""; page = 0;
    }
    private void DrawBuffs()
    {
        PlayerSelector();
        var buffs = CombatTracker.BuffSeconds(fight!, selected, Plugin.Now).OrderByDescending(b => b.Value).ToArray(); Pages(buffs.Length); int i = 0;
        double fightDuration = fight!.Duration(Plugin.Now);
        foreach (var buff in buffs.Skip(page * PageSize).Take(PageSize))
            DetailLine(287 + i++ * 27, buff.Key, (buff.Value / fightDuration * 100).ToString("0.0") + "%   " + buff.Value.ToString("0.0") + "s");
        if (buffs.Length == 0) Text(new Rect(22, 305, 480, 30), "No status samples for this player.", 12, Muted);
        Text(new Rect(22, 526, 480, 18), "Positive and negative effects · sampled every 0.5s", 10, Muted);
    }
    private void DrawDeaths()
    {
        var deaths = fight!.Events.Where(e => e.Kind == "death").Reverse().ToArray(); Pages(deaths.Length, 1);
        if (deaths.Length == 0) { Text(new Rect(22, 285, 480, 30), "No recorded deaths in this fight.", 13, Muted); return; }
        var death = deaths[page];
        Text(new Rect(22, 254, 480, 30), death.Target.Name + "  ·  " + Math.Max(0, death.Time - fight.Start).ToString("0.0") + "s", 14, Gold, bold: true); int i = 0;
        foreach (var e in fight.Events.Where(e => e.Kind == "damage" && e.Target.Id == death.Target.Id && e.Time <= death.Time && e.Time >= death.Time - 10).Reverse().Take(5))
            DetailLine(292 + i++ * 27, e.Source.Name + " · " + e.Ability, e.Amount.ToString("N0"));
    }
    private Fight? ComparisonFight()
    {
        if (fight == null) return null;
        if (plugin.Tracker.Current?.Id == fight.Id) return plugin.Tracker.History.FirstOrDefault();
        int index = plugin.Tracker.History.FindIndex(candidate => candidate.Id == fight.Id);
        return index >= 0 && index + 1 < plugin.Tracker.History.Count ? plugin.Tracker.History[index + 1] : null;
    }
    private void DrawComparison()
    {
        var previous = ComparisonFight();
        if (previous == null)
        {
            Pages(0);
            Text(new Rect(22, 292, 476, 28), "No earlier fight is available for comparison.", 13, Muted, TextAnchor.MiddleCenter);
            Text(new Rect(22, 328, 476, 24), "Finish another encounter, then return to this tab.", 11, Muted, TextAnchor.MiddleCenter);
            return;
        }
        var currentRows = CombatTracker.Rows(fight!); var previousRows = CombatTracker.Rows(previous);
        double currentDuration = fight!.Duration(Plugin.Now), previousDuration = previous.Duration(Plugin.Now);
        double currentDamage = currentRows.Sum(r => r.Damage), previousDamage = previousRows.Sum(r => r.Damage);
        Text(new Rect(18, 251, 235, 24), "CURRENT  ·  " + fight.Title, 11, Gold, bold: true);
        Text(new Rect(267, 251, 235, 24), "PREVIOUS  ·  " + previous.Title, 11, Muted, TextAnchor.MiddleRight, true);
        ComparisonMetric(18, "GROUP DAMAGE", currentDamage, previousDamage, "N0");
        ComparisonMetric(183, "GROUP DPS", currentDamage / currentDuration, previousDamage / previousDuration, "N1");
        ComparisonMetric(348, "DURATION", currentDuration, previousDuration, "0.0", "s");
        Text(new Rect(24, 354, 206, 19), "PLAYER", 9, Muted);
        Text(new Rect(235, 354, 82, 19), "CURRENT", 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(322, 354, 82, 19), "PREVIOUS", 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(409, 354, 83, 19), "CHANGE", 9, Muted, TextAnchor.MiddleRight);
        var previousById = previousRows.ToDictionary(r => r.Actor.Id);
        var actors = currentRows.Select(r => r.Actor).Concat(previousRows.Select(r => r.Actor)).GroupBy(a => a.Id).Select(g => g.First()).ToArray();
        var comparisons = actors.Select(actor =>
        {
            double current = currentRows.FirstOrDefault(r => r.Actor.Id == actor.Id)?.Damage / currentDuration ?? 0;
            double prior = previousById.TryGetValue(actor.Id, out var row) ? row.Damage / previousDuration : 0;
            return (Actor: actor, Current: current, Previous: prior, Delta: current - prior);
        }).OrderByDescending(value => value.Current).ThenByDescending(value => value.Previous).ToArray();
        Pages(comparisons.Length, 6); int line = 0;
        foreach (var value in comparisons.Skip(page * 6).Take(6))
        {
            float y = 376 + line++ * 27; Box(new Rect(18, y, 484, 25), Surface);
            Text(new Rect(27, y, 200, 25), value.Actor.Name, 11, Ink);
            Text(new Rect(235, y, 82, 25), value.Current.ToString("N1"), 11, Accent, TextAnchor.MiddleRight, true);
            Text(new Rect(322, y, 82, 25), value.Previous.ToString("N1"), 11, Muted, TextAnchor.MiddleRight);
            Text(new Rect(409, y, 83, 25), Signed(value.Delta, "N1"), 11, value.Delta >= 0 ? Accent : Gold, TextAnchor.MiddleRight, true);
        }
    }
    private void ComparisonMetric(float x, string label, double current, double previous, string format, string suffix = "")
    {
        Box(new Rect(x, 280, 154, 62), Surface);
        Text(new Rect(x + 9, 282, 136, 14), label, 8, Muted);
        Text(new Rect(x + 9, 297, 136, 22), current.ToString(format) + suffix, 16, Ink, bold: true);
        Text(new Rect(x + 9, 320, 136, 17), Signed(current - previous, format) + suffix, 9, current >= previous ? Accent : Gold, bold: true);
    }
    private static string Signed(double value, string format) => (value >= 0 ? "+" : "") + value.ToString(format);
    private void DetailLine(float y, string name, string amount)
    {
        Box(new Rect(18, y, 484, 25), Surface);
        Text(new Rect(27, y, 337, 25), name, 11, Ink);
        Text(new Rect(366, y, 126, 25), amount, 12, Accent, TextAnchor.MiddleRight);
    }
    private static string WeaponCategory(CombatEvent e) => e.Weapon.Length > 0 ? e.Weapon :
        e.Ability.Length > 0 ? e.Ability + " · weapon unknown" : "Unknown";
    private static double DamageTypeAmount(CombatEvent e, string type)
    {
        if (type == "Unresolved / fallback") return Math.Max(0, e.Amount - e.Types.Values.Sum());
        return e.Types.TryGetValue(type, out double amount) ? amount : 0;
    }
    private static string ResultCategory(CombatEvent e) => e.Result.Length > 0 ? e.Result : "Unresolved";
    private void BreakdownBar(float y, float height, string name, double amount, double maximum, string value, bool active, Action action)
    {
        var area = new Rect(18, y, 484, height);
        Box(area, active ? new Color32(52, 61, 48, 255) : Surface);
        float fill = (float)(484 * Math.Clamp(amount / maximum, 0, 1));
        Box(new Rect(18, y, fill, height), active ? new Color32(76, 67, 45, 255) : new Color32(32, 61, 69, 255));
        Box(new Rect(18, y + height - 2, fill, 2), active ? Gold : Accent);
        Text(new Rect(27, y, 305, height), name, 10, active ? Gold : Ink, bold: active);
        Text(new Rect(334, y, 158, height), value, 10, active ? Gold : Accent, TextAnchor.MiddleRight, active);
        Hit("breakdown:" + detailMode + ":" + name, area, action);
    }
    private void DrawDpsLinePlot(PlayerRow row, double combined, double selectedTotal, int hits, Func<CombatEvent, double>? selector)
    {
        int bins = Math.Clamp((int)Math.Ceiling(duration), 1, 30);
        var values = CombatTracker.DpsHistogram(fight!, row.Actor.Id, selectedTarget, Plugin.Now, bins, selector);
        double peak = values.DefaultIfEmpty(0).Max();
        string series = selectedBreakdown.Length == 0 ? "DPS OVER TIME" : selectedBreakdown;
        Text(new Rect(18, 282, 180, 20), $"{series}  ·  {hits} {(hits == 1 ? "HIT" : "HITS")}", 10, selectedBreakdown.Length == 0 ? Muted : Gold, bold: true);
        string summary = selectedBreakdown.Length == 0 ? $"{combined:N0} TOTAL   ·   {combined / duration:N1} AVG   ·   {peak:N1} PEAK" :
            $"{selectedTotal:N0} / {combined:N0} TOTAL   ·   {selectedTotal / duration:N1} AVG   ·   {peak:N1} PEAK";
        Text(new Rect(198, 282, 304, 20), summary, 10, Ink, TextAnchor.MiddleRight, true);
        var plot = new Rect(54, 306, 448, 104);
        Box(plot, new Color32(19, 28, 36, 255));
        for (int i = 1; i < 4; i++) Box(new Rect(plot.x, plot.y + i * plot.height / 4, plot.width, 1), Border);
        if (peak > 0)
        {
            float Y(double value) => plot.yMax - 3 - (float)((plot.height - 7) * value / peak);
            if (bins == 1) PlotSegment(plot.x + 2, Y(values[0]), plot.x + plot.width - 2, Y(values[0]));
            else
            {
                float step = (plot.width - 4) / (bins - 1);
                for (int i = 1; i < bins; i++)
                    PlotSegment(plot.x + 2 + (i - 1) * step, Y(values[i - 1]), plot.x + 2 + i * step, Y(values[i]));
            }
            var pointer = Pointer - rect.position;
            if (plugin.Interacting && plot.Contains(pointer))
            {
                int bucket = bins == 1 ? 0 : Math.Clamp((int)Math.Round((pointer.x - plot.x - 2) * (bins - 1) / (plot.width - 4)), 0, bins - 1);
                float x = bins == 1 ? Mathf.Clamp(pointer.x, plot.x + 2, plot.x + plot.width - 2) : plot.x + 2 + bucket * (plot.width - 4) / (bins - 1);
                float y = Y(values[bucket]);
                Box(new Rect(x, plot.y, 1, plot.height), new Color32(110, 213, 211, 105));
                Box(new Rect(x - 3, y - 3, 7, 7), Accent);
                double time = bins == 1 ? duration / 2 : duration * bucket / (bins - 1);
                string label = $"{time:0.0}s  ·  {values[bucket]:0.0} DPS";
                float labelX = Mathf.Clamp(x - 55, plot.x + 4, plot.x + plot.width - 114);
                Box(new Rect(labelX, plot.y + 5, 110, 19), Background);
                Text(new Rect(labelX + 5, plot.y + 5, 100, 19), label, 9, Ink, TextAnchor.MiddleCenter, true);
            }
        }
        Text(new Rect(18, 302, 31, 18), peak.ToString("N0"), 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(18, 394, 31, 18), "0", 9, Muted, TextAnchor.MiddleRight);
        Text(new Rect(54, 411, 100, 18), "0.0s", 9, Muted);
        Text(new Rect(402, 411, 100, 18), duration.ToString("0.0") + "s", 9, Muted, TextAnchor.MiddleRight);
    }
    private static void PlotSegment(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0) return;
        Matrix4x4 previous = GUI.matrix;
        GUIUtility.RotateAroundPivot((float)(Math.Atan2(dy, dx) * 180 / Math.PI), new Vector2(x1, y1));
        Box(new Rect(x1, y1 - 1, length, 2), Accent);
        GUI.matrix = previous;
    }
    private void Button(string key, Rect area, string label, bool enabled, Action action, bool active = false, bool isTab = false)
    {
        bool hovered = plugin.Interacting && area.Contains(Pointer - rect.position);
        Box(area, active ? new Color32(35, 65, 70, 255) : enabled && hovered ? new Color32(43, 57, 67, 255) : Surface);
        if (active) Box(new Rect(area.x, area.yMax - 2, area.width, 2), Accent);
        Text(area, label, isTab ? 10 : 11, active ? Accent : enabled ? Ink : Dim, TextAnchor.MiddleCenter);
        if (enabled) Hit(key, area, action);
    }
    private void Hit(string key, Rect area, Action action) => targets.Add(new HitTarget { Key = key, Area = area, Action = action });
    private static void Box(Rect area, Color color) { GUI.color = color; GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = Color.white; }
    private static void Line(float y) => Box(new Rect(18, y, 484, 1), Border);
    private void Text(Rect area, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
    {
        string key = size + ":" + anchor + ":" + bold;
        if (!styles.TryGetValue(key, out var style)) styles[key] = style = new GUIStyle(GUI.skin.label) { fontSize = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = anchor, richText = false, wordWrap = false,
            clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
        style.normal.textColor = color; GUI.Label(area, text, style);
    }
}

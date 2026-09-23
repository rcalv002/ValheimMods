# Valhalla Meter

An experimental Valheim combat meter inspired by arcdps. This is an independent C# mod; it does not load, modify, or redistribute arcdps. Version 0.2.10 is built against the locally installed Steam build 25390630 and BepInExPack Valheim 5.4.2350.

## What is implemented

- Dark in-game panel with aligned stats, damage bars, paging, and configurable size. **F7** shows/hides it. With game menus closed, **F8** enables clicking and dragging; **F8** again returns to gameplay. Interaction suspends movement/camera controls, but does not pause multiplayer combat. Esc, hiding the meter, opening a game menu, or losing focus also exits interaction.
- Group damage/DPS, damage taken, effective healing **received**, shield absorption **on the recipient**, deaths, and player details.
- Weapon and attack breakdowns when attack/projectile metadata can be matched; otherwise skill/attack categories with an explicit unknown weapon.
- Damage-type breakdowns, total stamina/eitr consumption, and damage per recorded stamina spent.
- Positive and negative status uptime, including Forsaken powers, sampled every 0.5 seconds.
- Automatic encounter closure after 8 seconds without combat activity, spatial separation of concurrent fights, immediate boss-kill completion, 20 recent fights in memory, manual end/export, and 30-minute/50,000-event segmentation.
- Host-relayed group telemetry and local observation fallback.
- Personal outgoing DPS fallback when an unmodded player owns the enemy, matched against Valheim's built-in damage-number broadcasts.
- Owner-side fire, spirit, and poison attribution that carries effective periodic health loss back to the original contributors without inflating total damage.
- Conservative companion attribution: damage from a tamed creature that is actively following a player is credited to that player under a distinct `Companion · name` weapon row.
- Target-aware encounters: mixed combat starts in a combined view, and header arrows cycle through individual enemies with recalculated damage, incoming damage, duration, and weapon details.
- Player Details with an arcdps-style DPS-over-time line plot (elapsed time on x, bucketed DPS on y), an interactive time/DPS cursor, death/boss-kill/spike markers, total/average/peak values, and clickable weapon/skill, damage-type, or hit-result rows that isolate a series on the graph.
- Persistent Older/Newer history and a paged Fights browser that restores at most the newest 20 validated snapshots across game restarts. Each browser row shows age, local date/time, target, duration, and group damage; click one to inspect it.
- A Compare tab showing current-versus-previous group damage, DPS, duration, and per-player DPS deltas for live or historical fights.
- A Sources tab showing how much recorded player damage came from owner-resolved health loss, displayed-number estimates, or unspecified older events, with relayed delivery counted separately. The selected fight's source mix also appears in the header.
- Background event logs and offline HTML reports with selectable player/target/weapon/type timelines, hover DPS values, resistance-result summaries, buff uptime, death and kill recaps, searchable events, pagination, and CSV export.

## Install

The release ZIP contains only this mod and its core library. It contains no Valheim game libraries, arcdps DLL, or loader binaries. It is a manual-install archive, not a published Thunderstore package.

1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Extract the ZIP's `BepInEx/plugins/ValhallaMeter` folder into your game folder or your active mod-manager profile's `BepInEx/plugins` folder.
3. Start Valheim through the same loader/profile. Look for `Valhalla Meter 0.2.10 loaded` in `BepInEx/LogOutput.log`.
4. Enter a world and attack a creature. Press **F7** if the panel is hidden, then **F8** to interact. Drag the header to move the panel; use **Fights** to browse saved encounters and the bottom arrows or mouse wheel to page through lists.

From this source checkout, `scripts/install.ps1 -InstallLoader` installs into a clean Windows Steam installation after `scripts/build.ps1` has generated the release. It refuses conflicting existing files, writes a receipt, and does not stop/restart the game. `-WhatIf` previews every file operation. Administrator rights may be needed for Program Files.

For group data, install this mod on the host/dedicated server and **all participating clients**. Clients negotiate with the host, which relays batched observations. Unmodded peers may still join, but missing ownership-side events mean incomplete totals. No game RPC formats or ownership rules are changed. Dedicated-server runtime compatibility remains to be tested on a dedicated build.

Configuration is generated at `BepInEx/config/local.valhallameter.cfg`. `GroupSync` controls group telemetry; reconnect after changing it. `IdleSeconds` controls automatic encounter closure. `DistantSplitMeters` sets the separation between simultaneous encounters; the old `DistantSplitGapSeconds` setting is ignored. Window position, `Scale`, `Opacity`, `Locked`, `ToggleKey` (F7), and `InteractKey` (F8) are configurable. The in-panel lock button prevents accidental dragging. The previous default F8 toggle is migrated to F7 when it conflicts with the new interaction key.

To disable this mod, close the game and move only `BepInEx/plugins/ValhallaMeter` outside the plugins directory. Leave a shared BepInEx installation in place for other mods.

## Logs and reports

Each run writes to `BepInEx/ValhallaMeter/logs/<UTC-session-id>/`:

- `events.jsonl`: accepted telemetry, flushed regularly in the background.
- `fight-<id>.json`: an encounter snapshot.
- `fight-<id>.html`: a self-contained report. Open it in your usual browser; no hosting or internet connection is required.

Reports save automatically when fights end. **Export report** also saves the displayed fight. **Live** follows the ongoing/latest fight; **Older/Newer** navigate distinct fights. **Fights** opens a five-row-per-page browser of saved encounters; click any row to inspect it. The selected fight stays pinned as new ones are recorded. The panel shows age such as `5m ago` or `3mo ago` and the encounter's local date/time. The newest 20 valid saved snapshots are restored after a restart; older files stay on disk but are not loaded into the panel. Corrupt, duplicate, oversized, and active-session files are skipped. The mode and history position are displayed; unavailable actions are dimmed. Reports and logs remain on disk until you remove them. They contain player names and combat events; nothing is uploaded to a website. With group synchronization enabled, telemetry is exchanged with the connected modded host and reporting clients.

`artifacts/sample-report.html` is generated by the test runner using **synthetic data**, solely to demonstrate the report layout.

## Accuracy and known limits

This release is an alpha, not a claim of arcdps feature parity. Live solo DPS, revised controls, and the remote-owner fallback have been exercised in game. The target selector, DPS line plot and markers, damage breakdowns, and persistent history have passed their automated harnesses and still require broader in-game validation.

- When this client owns the target, damage observes actual health changes inside `ApplyDamage` and excludes overkill. With no modded relay and another client owning the target, personal attacks are matched to Valheim's broadcast damage numbers. Those values are rounded to one decimal place, can precede final world-difficulty scaling, and cap overkill using the last replicated target health, so fallback totals are an estimate. The panel labels this mode `personal DPS fallback`.
- Valheim discards the attacker on native fire, poison, and spirit ticks. When the target owner runs this mod, an application ledger divides each effective tick among the outstanding contributors and preserves unknown shares instead of guessing. An unmodded remote owner cannot provide later ticks, so the displayed-number fallback still cannot recover that periodic damage. A tame is credited only while its AI follow target is a player; stationary, commanded, or ownerless tames remain under their own non-player identity.
- Weapon matching uses an unmodified hit fingerprint and a separate metadata event. Remote metadata may be absent, delayed, or ambiguous for identical hits. Custom attacks, area effects outside the observed attack/projectile paths, and mixed-mod games may have unknown weapons. Attack labels are broad attack categories, not exact combo/secondary attack names. Hit results use Valheim's final significant resistance category; they do not estimate armor prevented, block prevented, parries, or dodges.
- Healing shows the recipient's effective HP gain. Healer identity and overhealing are unavailable in this version. Shield absorption is measured before downstream armor/resistance and is not credited to its caster; it is not directly comparable to effective HP damage.
- Statuses are sampled, not event-perfect. Short effects may be missed, missing samples expire after 1.5 seconds, and status names are localized. All recorded stamina/eitr use counts, including movement.
- Group reports are cooperative peer telemetry, not a cheat-proof authority. Group timestamps use host receipt time and can shift with latency. Players who join mid-fight have partial history. A relay outage falls back to local capture after 30 seconds; queued/unacknowledged events can be lost.
- The Sources tab describes only damage that was recorded. Its owner/fallback figures are measurement methods, while relay is a delivery path that overlaps them. It cannot quantify hits missed when an enemy owner or peer lacks the mod. Fights saved before 0.2.8 show unspecified source data.
- Timeline markers come from recorded deaths and boss kills. A spike marks a local DPS bucket at least twice the selected series' average, capped at three markers; it does not identify critical hits or a game-defined burst phase. In Player Details, death markers follow the selected player and boss markers follow the selected target when one is chosen.
- Positioned hits from distant groups now remain in separate active encounters, even when their events interleave. The Fights browser lists active encounters above saved fights, and a recorded boss death closes only its own encounter. Eight fights can remain active; the least recently active is saved if that limit is reached. Events without a position fall back to the most recently active fight, so old peers or unusual modded events can still be ambiguous. The distance threshold defaults to 80 meters.
- Encounters start on player-related damage or shield absorption and finish after idle time. Final duration ends at the last combat/death event; subsequent recovery is excluded. Resource use within two seconds before the first hit is retained. Durations are floored at one second for short encounters.
- Event queues and memory are bounded. Loss or logging errors are shown in the panel/log. Long-term disk retention is manual.
- Game updates can change patch targets. A patch failure removes this mod's patches and reports an error. This does not establish compatibility with other mods or every Valheim platform.

## Build and verify

Requires a .NET SDK, the Valheim installation, and BepInEx libraries. No Unity editor is needed for this IMGUI overlay.

```powershell
.\scripts\prepare.ps1
.\scripts\build.ps1
# For another game install:
.\scripts\build.ps1 -ValheimPath 'D:\SteamLibrary\steamapps\common\Valheim'
```

Build output: `dist/ValhallaMeter-0.2.10.zip`. Its extracted release folder includes `SHA256SUMS.txt`; build, install, and update reject any missing, changed, duplicate, escaped, or unlisted release file and verify that the plugin assembly version matches the manifest. Run `scripts/verify.ps1` at any time for a read-only check of both the package and installed DLLs, or add `-ReleaseOnly` to check only the package. The build references the installed game's managed assemblies without distributing them. `BepInExPath` can be overridden as an MSBuild property. Dependency downloads, private decompilation notes, and generated outputs are ignored by Git. For an existing installation, `scripts/update.ps1` backs up this mod's DLLs and replaces them atomically when Windows permits; it never stops the game. The new version takes effect on restart.

The automated checks cover duplicate delivery, group timing, encounter and 30-minute boundaries, death timing, immune attempts, unknown sources, healing/shield aggregation, late metadata, periodic-damage allocation, report controls, buff sampling gaps, malformed events, memory bounds, session reset, remote-owner damage matching, target filtering, persistent archive loading, history selection, pointer click cancellation, and actual installed-game method/field presence. A second harness compiles the production `MeterWindow` against a headless drawing/input host and exercises its actual history and target navigation, line plots, breakdown bars, tabs, scaled hit targets, dragging, disabled interaction, and export. It emits `artifacts/overlay-preview.svg`, a layout preview, not an in-game screenshot. These checks do not substitute for runtime Harmony/Unity/network testing. Browser visual QA of the HTML report is blocked by the tool's local-file URL policy; its JavaScript is syntax-checked independently.

Before calling a release stable, test in a separate character/world: melee, bows, weapon swaps in flight, resisted/blocked hits, overkill, fire/poison ticks, healing, shields, deaths, reconnects, and two players attacking the same target under both ownership arrangements. Compare both clients' final totals and verify no duplicate hits, then test on a dedicated server and with the intended mod list.

## Code layout

- `src/ValhallaMeter.Core`: event validation, deduplication, encounters, summaries, and uptime calculations.
- `src/ValhallaMeter/Capture.cs`: observational Harmony hooks and weapon context.
- `src/ValhallaMeter/GroupSync.cs`: negotiated host relay.
- `src/ValhallaMeter/MeterWindow.cs`: Unity overlay.
- `src/ValhallaMeter/LogWriter.cs`, `report.html`: local persistence and report UI.
- `tests/ValhallaMeter.Tests`: deterministic tests and installed-game contract checks.

References: [BepInEx plugin development](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html), [Valheim RPC documentation](https://valheim-modding.github.io/Jotunn/tutorials/rpcs.html), and [JereKuusela's DPS mod](https://github.com/JereKuusela/valheim-dps) as a research reference. No source from that mod is included.

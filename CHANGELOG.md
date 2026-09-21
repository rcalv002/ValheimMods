# 0.2.3 — fight comparison and panel controls

- Add a Compare tab for current-versus-previous group damage, DPS, duration, and per-player DPS deltas.
- Resolve the comparison baseline consistently for live combat, the latest completed fight, and pinned Older/Newer history.
- Add configurable panel opacity and a persisted in-panel lock that prevents accidental dragging.
- Fit all eight analysis tabs into the existing panel without increasing its footprint.

# 0.2.2 — hit results and report parity

- Capture Valheim's final significant resistance result for resolved hits and periodic ticks.
- Retain immune player attacks as zero-damage attempts so hit counts and target activity remain honest.
- Add a selectable Hit results breakdown to the in-game Details view.
- Upgrade offline reports with player, target, weapon, and damage-type chart filters; hover DPS values; clickable breakdown rows; result summaries; enemy kill recaps; and result-aware event/CSV columns.
- Keep armor prevention, block prevention, parries, and dodges explicitly outside the recorded result instead of estimating them.

# 0.2.1 — encounter boundaries

- Attach bounded world-position telemetry to resolved damage, periodic ticks, shields, fallback hits, and deaths.
- Split combat that resumes at least 80 meters away after a configurable two-second gap, covering travel, portals, and dungeon-coordinate changes.
- Record enemy deaths and close a fight immediately when its recorded boss dies.
- Validate saved position data and installed-game hooks, while leaving nearby target swaps and adds in the same encounter.

# 0.2.0 — periodic-damage attribution

- Track fire, spirit, and poison applications on the target's network owner before Valheim discards their attacker.
- Allocate each effective periodic health-loss tick proportionally across outstanding contributors while preserving unknown shares.
- Carry correlation metadata into later ticks so weapon names can arrive through the cooperative relay without replacing the periodic-damage label.
- Model poison replacement, effect removal, difficulty scaling, bounded memory, overkill, and late metadata explicitly.
- Keep periodic fallback honest: later ticks remain unavailable when an unmodded remote peer owns the target.

# 0.1.9 — selectable damage series

- Make weapon/skill and damage-type rows clickable in F8 interaction mode.
- Filter the DPS line, hit count, total, average, peak, and hover values to the selected category while retaining the full-fight total for context.
- Highlight the active breakdown row; click it again to return to all player damage.

# 0.1.8 — interactive DPS cursor

- Show the nearest line-plot bucket's elapsed time and exact DPS while hovering the graph in F8 interaction mode.
- Add a vertical guide and point marker so spikes can be matched to their time bucket.
- Keep the cursor bounded inside the plot at the first and last samples.

# 0.1.7 — arcdps-style DPS line plot

- Replace the DPS-over-time columns with a continuous teal line connecting each time bucket.
- Preserve elapsed time on the x-axis, DPS on the y-axis, grid lines, and total/average/peak/hit summaries.
- Keep weapon/skill and damage-type breakdown bars below the plot and respect the selected enemy target.

# 0.1.6 — DPS over time histogram

- Add a true per-player histogram to Details with elapsed fight time on the x-axis and bucketed DPS on the y-axis.
- Use approximately one-second buckets for short fights and up to 30 adaptive buckets for longer encounters.
- Show the selected player's total damage, average DPS, peak bucket DPS, and hit count above the plot.
- Keep the proportional weapon/skill and damage-type breakdown bars below the time histogram.
- Apply the active enemy target filter to both the time series and category breakdowns.

# 0.1.5 — persistent fight history

- Restore the newest 20 saved encounters into Older/Newer history when the plugin starts.
- Preserve newest-first ordering across sessions and deduplicate fight IDs.
- Validate every restored fight and combat event, with limits on file size, total loaded data, scanned sessions, and candidate files.
- Skip corrupt, invalid, oversized, duplicate, and active-session snapshots without disabling live capture.
- Prevent development overlay-test failures from creating standalone Windows crash dialogs.

# 0.1.4 — player damage breakdown bars

- Replace the Details tab's text-only breakdown with horizontal category bars for the selected player.
- Show combined fight damage and DPS at the top, followed by proportional weapon/skill bars with totals and shares.
- Add a Damage types view for Blunt, Slash, Pierce, elemental, and other captured components.
- Keep damage-number fallback honest by placing damage without reliable type data in an `Unresolved / fallback` bar.
- Respect the active enemy target filter throughout the histogram.

# 0.1.3 — target-aware encounter views

- Detect the distinct enemies involved in an encounter and label mixed combat with its target count.
- Add header arrows that cycle between the combined encounter and each individual enemy.
- Recalculate outgoing damage, incoming damage, DPS duration, unattributed damage, and weapon details for the selected target.
- Keep healing, shields, buffs, and deaths encounter-wide because Valheim does not reliably associate those events with a specific enemy.
- Add core and production-overlay regressions for mixed encounters and target navigation.

# 0.1.2 — personal DPS across network ownership

- Match local attacks against Valheim's built-in damage-number broadcasts when another unmodded client owns the creature.
- Start encounters from matched fallback hits and retain their player, target, weapon, and attack metadata.
- Disable the fallback whenever a modded group relay is connected so owner-resolved telemetry cannot be counted twice.
- Reject unrelated, expired, immune, malformed, and implausibly distant damage-number matches; cap obvious overkill by the last replicated target health.
- Add deterministic matcher tests and an installed-game contract check for the damage-number receiver.

# 0.1.1 — usable history controls and redesigned overlay

- F7 shows/hides the meter; F8 toggles mouse interaction and dragging. The previous F8 show/hide default migrates automatically to avoid a conflict.
- Use Valheim's pointer input instead of relying on legacy IMGUI mouse events. Camera/player controls are suspended only while explicitly interacting with the meter; F8, F7, Esc, losing focus, or opening a game menu exits interaction.
- Fix Older selecting the same latest fight while idle. Pin history by fight identity so new fights do not shift the selected stats.
- Show LIVE/LATEST/HISTORY, fight position/count, and visibly disabled navigation at boundaries.
- Replace stock GUI buttons with a dark panel, teal bars, aligned numerical columns, summary cards, paging, and configurable scale.
- Add history/pointer regressions and a headless harness that runs the production overlay's actual click/drag/render code.

# 0.1.0 — experimental first release

- Observe actual health writes during damage and healing resolution; cap damage by remaining health.
- Movable F8 overlay with damage rankings, damage taken, healing received, shield absorption, player details, status uptime, and death recaps.
- Capture weapon/attack context separately from native network payloads, matching it to resolved hits when available.
- Cooperative host relay with handshake, bounded batches, duplicate suppression, and partial-coverage labels.
- Automatic encounter segmentation, bounded fight history, and resource-use totals.
- Background JSONL logging and standalone HTML/JSON reports with event filtering and CSV export.
- Deterministic core checks and installed-game patch-target validation.

Runtime validation in Valheim is still required. Damage-over-time source attribution, summoned-creature ownership, exact healer/caster attribution, and compatibility with other combat mods are not yet implemented or verified.

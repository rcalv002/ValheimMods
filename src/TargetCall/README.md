# Target Call

A Valheim BepInEx plugin for shared attack, assist, and commander markers, plus a private personal marker. It does not change combat, camera aim, or Valhalla Meter.

## Controls

- **C** calls a hostile creature for **attack** (red crossed swords). It skips players in the line of sight, so an enemy can still be called when players are mixed into the fight.
- **Left Shift+C** calls a player for **assistance** (cyan, inverted shield and cross). It skips creatures in the line of sight. Everyone running the mod sees an on-screen announcement for a new attack or assist call.
- **Left Alt+C** toggles a **personal** marker (white down arrow) on the aimed player or hostile creature. Only this client sees it. Aim at nothing to clear it.
- **Left Ctrl+C** designates a player as the shared **commander** (gold crown). Aim at a player or aim at nothing to designate yourself. Repeat on the same player to clear it.
- **Z** takes or releases the current shared attack/assist call locally. A taken call uses a gold icon. Taking does not turn the camera or redirect attacks.

All keys are configurable at `BepInEx/config/local.targetcall.cfg`. Existing 0.1.0 and 0.1.1 default bindings migrate automatically; custom bindings are preserved. Repeat C or Shift+C on your current call, or aim at nothing, to clear your attack/assist call. Attack/assist and commander are separate shared markers, each with one current target. The personal marker is stored only in the local client and clears on session change. Enemy icons appear below Valheim's nameplate; player icons appear above it. Icons have no second text label and use Valheim's screen-scaled projection to stay centered across resolutions. Markers follow their target while it is loaded, alive, on screen, and within the configurable `MarkerDistance` (80 meters by default). Each shared marker persists until its caller clears or replaces it, another player replaces it, its caller disconnects, or a modded player observes the target's death. A short client lease hides stale markers when the host is lost, and the host sends current shared markers to newly connected modded clients.

## Install

Install BepInExPack Valheim, then copy `BepInEx/plugins/TargetCall/TargetCall.dll` from this release into each participating client's Valheim directory and into the host or dedicated server's BepInEx plugins directory. Restart the game/server to load it. Look for `Target Call 0.2.2 loaded` in the BepInEx log. This plugin and Valhalla Meter can be installed together.

The dedicated server orders and relays attack/assist and commander calls. The commander channel requires Target Call 0.2.0 or later on the server and clients. This 0.2.2 release only changes client-side drawing and works with a 0.2.0 server. Older clients continue to receive the original attack/assist channel but cannot show the new private, inverted assist, or commander markers. A client without Target Call sees none of these markers. Valheim's built-in location ping remains available separately. This plugin draws on-screen markers only; it does not add map markers, off-screen arrows, or combat lock-on. A target unloaded by zoning or distance is hidden locally until it becomes available again.

## Build and checks

Run `scripts/build-targetcall.ps1` from the repository after `scripts/prepare.ps1` has supplied BepInEx. The script builds against the installed Steam game's managed assemblies, runs the host/peer state harness, and produces `dist/TargetCall-0.2.2.zip` with SHA-256 checksums. No game DLL or loader binary is distributed.

The automated checks cover host-authoritative ordering, sender validation, clearing, persistent calls, client lease expiry, late joins, reconnection, independent shared channels, and local personal/take selection. The new marker shapes, hotkeys, and on-screen announcements still need live game validation. Game updates may change the APIs used by this plugin.

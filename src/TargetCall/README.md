# Target Call

A separate Valheim BepInEx plugin for a shared moving-target marker. It does not change combat, camera aim, or Valhalla Meter.

## Controls

- **C** calls the player or hostile creature under the crosshair. Calling an enemy shows a crossed-swords **attack** icon; calling a player shows a shield-and-cross **defend** icon so teammates can see who needs help. The most recent call becomes the shared target for modded players. Repeat on your own called target, or press C while aiming at nothing, to clear your call.
- **Z** takes or releases the current shared call locally. A taken call uses a gold marker; an untaken attack marker is red and an untaken defend marker is cyan. Taking does not turn the camera or redirect attacks.

Both keys are configurable at `BepInEx/config/local.targetcall.cfg`. Existing 0.1.0 and 0.1.1 default bindings migrate automatically; custom bindings are preserved. The icon follows the called enemy or player above their head while they are loaded, alive, on screen, and within the configurable `MarkerDistance` (80 meters by default). A call persists until its caller clears it, another player replaces it, its caller disconnects, or a modded player observes the target's death. Each client renews a short network lease with the host so a lost connection cannot leave a stale marker. The host sends its current call to a newly connected modded client.

## Install

Install BepInExPack Valheim, then copy `BepInEx/plugins/TargetCall/TargetCall.dll` from this release into each participating client's Valheim directory and into the host or dedicated server's BepInEx plugins directory. Restart the game/server to load it. Look for `Target Call 0.1.3 loaded` in the BepInEx log. This plugin and Valhalla Meter can be installed together.

The dedicated server orders and relays calls. Target Call 0.1.3 clients use the same network protocol as 0.1.1 and 0.1.2, so an existing dedicated server can relay both enemy and player calls without a restart. Every client needs 0.1.3 to see defend markers on players; older clients hide player calls. A client without Target Call will not see the moving marker. Valheim's built-in location ping remains available separately, but is not sent by this plugin. This release keeps one shared call at a time and draws only an on-screen marker; it does not add a map marker, off-screen arrow, or combat lock-on. A target unloaded by zoning or distance is hidden locally until it becomes available again or the call expires.

## Build and checks

Run `scripts/build-targetcall.ps1` from the repository after `scripts/prepare.ps1` has supplied BepInEx. The script builds against the installed Steam game's managed assemblies, runs the host/peer state harness, and produces `dist/TargetCall-0.1.3.zip` with SHA-256 checksums. No game DLL or loader binary is distributed.

The automated checks cover host-authoritative call ordering, sender validation, clearing, persistent host calls, client lease expiry, late joins, reconnection, and local take selection. The new icons and player targeting still need live game validation. Game updates may change the APIs used by this plugin.

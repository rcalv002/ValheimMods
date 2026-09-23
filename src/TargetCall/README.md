# Target Call

A separate Valheim BepInEx plugin for a shared moving-creature marker. It does not change combat, camera aim, or Valhalla Meter.

## Controls

- **Left Ctrl+T** calls the hostile creature under the crosshair. The most recent call becomes the shared target for modded players. Repeat on your own called creature, or press Left Ctrl+T while aiming at nothing, to clear your call.
- **T** takes or releases the current shared call locally. A taken call uses a gold marker; an untaken call uses a cyan marker. Taking does not turn the camera or redirect attacks.

Both keys are configurable at `BepInEx/config/local.targetcall.cfg`. The marker follows the target above its head while it is loaded, alive, on screen, and within the configurable `MarkerDistance` (80 meters by default). A call expires after 30 seconds. Another player can replace it immediately; only the current caller can clear it. The host sends its current call to a newly connected modded client.

## Install

Install BepInExPack Valheim, then copy `BepInEx/plugins/TargetCall/TargetCall.dll` from this release into each participating client's Valheim directory and into the host or dedicated server's BepInEx plugins directory. Restart the game/server to load it. Look for `Target Call 0.1.0 loaded` in the BepInEx log. This plugin and Valhalla Meter can be installed together.

The dedicated server orders and relays calls. A client without Target Call will not see the moving marker. Valheim's built-in location ping remains available separately, but is not sent by this plugin. This release keeps one shared call at a time and draws only an on-screen marker; it does not add a map marker, off-screen arrow, or combat lock-on. A target unloaded by zoning or distance is hidden locally until it becomes available again or the call expires.

## Build and checks

Run `scripts/build-targetcall.ps1` from the repository after `scripts/prepare.ps1` has supplied BepInEx. The script builds against the installed Steam game's managed assemblies, runs the host/peer state harness, and produces `dist/TargetCall-0.1.0.zip` with SHA-256 checksums. No game DLL or loader binary is distributed.

The automated checks cover host-authoritative call ordering, sender validation, clearing, TTL, late joins, reconnection, and local take selection. The on-screen marker and multiplayer controls still need live game validation. Game updates may change the APIs used by this plugin.

# Heading Compass

A separate, client-only Valheim plugin for calling out directions to other players: for example, “NE, 045 degrees.” It works on any server without a server plugin or changes to the other mods.

The top-center ribbon shows the camera heading in degrees, an eight-direction label, and a scrolling direction scale under a fixed center index. North is 000°, east 090°, south 180°, and west 270°, matching Valheim's map. It follows where you look, including while standing still or moving sideways. Headings wrap from 359° to 000° and stay stable while looking up or down.

Press **F6** to show/hide while game menus are closed. This does not capture mouse input or interfere with the meter's F7/F8 controls. The default position is below Chat Translator's toggle.

## Install

Install BepInExPack Valheim, then copy the release's `BepInEx/plugins/HeadingCompass/HeadingCompass.dll` into your Valheim installation or active mod-manager profile. Launch/restart Valheim. Look for `Heading Compass 0.2.0 loaded` in `BepInEx/LogOutput.log`.

From this source checkout, `scripts/install-headingcompass.ps1` installs the built release with hash verification and backs up an existing DLL. Use `-ValheimPath` for another client or `-WhatIf` to preview. No server deployment is needed.

Configuration is created at `BepInEx/config/local.headingcompass.cfg`. Close the game before editing it. `[Display]` contains `Visible`, `HorizontalPosition` (0–1 screen fraction), `Top` (pixels), `Width`, `Scale`, and `Opacity`. `[Controls]` contains `ToggleKey`. The compass fits within the screen at different resolutions and restores the shared GUI state after drawing.

The plugin shows your current view direction and bearings to existing targets and pins; it does not create map pins or transmit your position. Another player needs a compass of their own to use the same numeric headings.

## Target icons and map pins

Install **Target Call 0.2.3 or later on this client** to show the same attack, assist, personal, and commander icons on the compass. Taking a target retains its gold focus tint. Target Call is optional: the compass and map pins work without it, including alongside older versions. Its existing 0.2.0+ server protocol is unchanged, so the server does not need an update for this integration. Personal markers stay private to the client that made them.

Creature bearings require a live, loaded creature. Distant player bearings use Valheim's public map positions only when that player has enabled position sharing; those positions may update less often than nearby characters. The compass does not retain a stale last-known position after a creature unloads. Dead loaded targets, cleared calls, expired relay leases, and disconnected sessions stop contributing markers.

Saved map pins use their existing icon and horizontal world position, including far beyond render distance. Temporary pings and live player map pins are excluded. Checked-off pins are hidden by default, and the map's icon-type filters and shared-map toggle are respected. Map pins are disabled in no-map worlds. Pin changes refresh within about a quarter second.

Under `[Markers]` in `local.headingcompass.cfg`:

```ini
ShowTargets = true
ShowMapPins = true
ShowCheckedPins = false
FavoritePins = Base,Home
MaxPinDistance = 0
MaxMarkers = 20
```

Favorites match complete pin names ignoring case. Rename a saved pin to `Base` or add its name to `FavoritePins`. `MaxPinDistance` is in meters; zero means unlimited, and favorites bypass this distance limit. Favorites still respect checked, shared, and map icon filters.

Markers appear below the heading scale at their bearing. Active targets have priority over favorites, which have priority over ordinary map pins. Up to three overlapping markers stack vertically; excess icons are omitted rather than drawn on top of one another. Targets and favorites outside the 150-degree compass arc get left/right turn arrows. Ordinary off-arc pins stay hidden. The closest displayed bearing to the center gets a name, horizontal distance, and bearing readout, such as `Base · 1.8 km · N 000°`. Arrows at the ends indicate a turn direction rather than an exact position on the visible scale.

## Build and checks

Run `scripts/build-headingcompass.ps1` after `scripts/prepare.ps1`. It builds against the installed game, runs heading and rendering checks, verifies release checksums, and creates `dist/HeadingCompass-0.2.0.zip`. The release contains no game or loader DLLs. Automated rendering checks use a headless host; they do not confirm the final appearance in a running Valheim session.

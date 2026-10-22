# Heading Compass

A separate, client-only Valheim plugin for calling out directions to other players: for example, “NE, 045 degrees.” It works on any server without a server plugin or changes to the other mods.

The top-center ribbon shows the camera heading in degrees, an eight-direction label, and a scrolling direction scale under a fixed center index. North is 000°, east 090°, south 180°, and west 270°, matching Valheim's map. It follows where you look, including while standing still or moving sideways. Headings wrap from 359° to 000° and stay stable while looking up or down.

Press **F6** to show/hide while game menus are closed. This does not capture mouse input or interfere with the meter's F7/F8 controls. The default position is below Chat Translator's toggle.

## Install

Install BepInExPack Valheim, then copy the release's `BepInEx/plugins/HeadingCompass/HeadingCompass.dll` into your Valheim installation or active mod-manager profile. Launch/restart Valheim. Look for `Heading Compass 0.1.0 loaded` in `BepInEx/LogOutput.log`.

From this source checkout, `scripts/install-headingcompass.ps1` installs the built release with hash verification and backs up an existing DLL. Use `-ValheimPath` for another client or `-WhatIf` to preview. No server deployment is needed.

Configuration is created at `BepInEx/config/local.headingcompass.cfg`. Close the game before editing it. `[Display]` contains `Visible`, `HorizontalPosition` (0–1 screen fraction), `Top` (pixels), `Width`, `Scale`, and `Opacity`. `[Controls]` contains `ToggleKey`. The compass fits within the screen at different resolutions and restores the shared GUI state after drawing.

The plugin shows your current view direction; it does not mark locations or share your position. Another player needs a compass of their own to use the same numeric headings.

## Build and checks

Run `scripts/build-headingcompass.ps1` after `scripts/prepare.ps1`. It builds against the installed game, runs heading and rendering checks, verifies release checksums, and creates `dist/HeadingCompass-0.1.0.zip`. The release contains no game or loader DLLs. Automated rendering checks use a headless host; they do not confirm the final appearance in a running Valheim session.

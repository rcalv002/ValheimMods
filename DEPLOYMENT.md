# Valheim mod deployment guide

This guide installs the current Valhalla Meter, Target Call, and optional Chat Translator builds in a Steam Valheim client and, when group features are wanted, a Docker dedicated server.

The plugins are independent:

- **Valhalla Meter 0.2.14**: client overlay, combat capture, and cooperative DPS relay.
- **Target Call 0.2.2**: shared attack, assist, commander, and personal markers.
- **Chat Translator 0.1.2**: client-only incoming-chat translation through OpenRouter.

Valheim, BepInEx, and the plugins must be compatible with the same game installation. Install the same Target Call version on clients when possible. A Target Call 0.2.0 server is protocol-compatible with 0.2.2 clients.

## Windows client

### 1. Install BepInEx

Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) using a mod manager or its manual archive. For a manual installation, extract the package contents into the Valheim game directory, normally:

~~~
C:\Program Files (x86)\Steam\steamapps\common\Valheim
~~~

The directory should contain `BepInEx`, `doorstop_config.ini`, and the BepInEx loader files. Start Valheim once through Steam, wait for the main menu, then close it. This confirms that the loader created its folders.

If the game is installed elsewhere, use that directory instead. Do not extract the release ZIPs into `BepInEx\plugins\` as an extra nesting level; the ZIPs already contain the `BepInEx\plugins\` path.

### 2. Install the plugins

Extract each release archive into the Valheim game directory:

~~~
ValhallaMeter-0.2.14.zip
TargetCall-0.2.2.zip
ChatTranslator-0.1.2.zip       (optional)
~~~

The resulting files should be:

~~~
Valheim\BepInEx\plugins\ValhallaMeter\ValhallaMeter.dll
Valheim\BepInEx\plugins\ValhallaMeter\ValhallaMeter.Core.dll
Valheim\BepInEx\plugins\TargetCall\TargetCall.dll
Valheim\BepInEx\plugins\ChatTranslator\ChatTranslator.dll
~~~

Chat Translator is client-only. It does not need to be installed on the dedicated server or on clients that do not want translation.

### 3. First launch and verification

Start Valheim through the same Steam or mod-manager profile used for BepInEx. Check `BepInEx\LogOutput.log` for:

~~~
Valhalla Meter 0.2.14 loaded
Target Call 0.2.2 loaded
Chat Translator 0.1.2 loaded
~~~

The Chat Translator line appears only when that optional plugin is installed. If a plugin is missing from the log, close the game and check the folder layout before configuring it.

The plugins create these configuration files after the first load:

~~~
BepInEx\config\local.valhallameter.cfg
BepInEx\config\local.targetcall.cfg
BepInEx\config\local.chattranslator.cfg
~~~

Valhalla Meter defaults to **F7** for show/hide and **F8** for interaction and dragging. Target Call defaults to **C** for attack, **Shift+C** for assist, **Alt+C** for a private marker, **Ctrl+C** for commander, and **Z** to take the shared call.

For Chat Translator, edit `local.chattranslator.cfg`:

~~~
[OpenRouter]
ApiKey = YOUR_OPENROUTER_KEY
TargetLanguage = English
Enabled = true
~~~

The API key is stored in plain text in the local config. Keep it private. The on-screen translator toggle can disable and re-enable translation without removing the key.

## Dedicated Docker server

Install Valhalla Meter and Target Call on the dedicated server when group DPS and shared markers are required. Do not install Chat Translator on the server.

The server stack used by this project is rooted at:

~~~
/dockerdata/valheim
~~~

This image uses two relevant BepInEx trees:

~~~
/dockerdata/valheim/config/bepinex       # persistent plugin/config source
/dockerdata/valheim/data/bepinex/BepInEx # active runtime loaded by the server
~~~

The plugin must be present in the active runtime tree. Keeping a matching copy in the persistent `config` tree prevents it from being lost when the container is recreated.

### 1. Check the server prerequisites

Run these commands as the server administrator:

~~~
cd /dockerdata/valheim
grep '^BEPINEX=' .env
docker compose ps valheim
~~~

The environment must contain `BEPINEX=true`, and the `valheim` service must be running. If the stack uses a conflicting loader such as ValheimPlus, resolve that conflict before enabling BepInEx.

### 2. Install Valhalla Meter

Copy the two Valhalla Meter files from the release into both plugin trees. The example assumes the DLLs were staged in `/dockerdata/valheim` first.

~~~
cd /dockerdata/valheim

mkdir -p \
  config/bepinex/plugins/ValhallaMeter \
  data/bepinex/BepInEx/plugins/ValhallaMeter

install -m 644 ValhallaMeter.dll \
  config/bepinex/plugins/ValhallaMeter/ValhallaMeter.dll
install -m 644 ValhallaMeter.Core.dll \
  config/bepinex/plugins/ValhallaMeter/ValhallaMeter.Core.dll
install -m 644 ValhallaMeter.dll \
  data/bepinex/BepInEx/plugins/ValhallaMeter/ValhallaMeter.dll
install -m 644 ValhallaMeter.Core.dll \
  data/bepinex/BepInEx/plugins/ValhallaMeter/ValhallaMeter.Core.dll
~~~

The server does not draw the overlay, but it must load the meter to receive and relay owner-side combat events.

### 3. Install Target Call

Install the current Target Call DLL into both trees:

~~~
cd /dockerdata/valheim

mkdir -p \
  config/bepinex/plugins/TargetCall \
  data/bepinex/BepInEx/plugins/TargetCall

install -m 644 TargetCall.dll \
  config/bepinex/plugins/TargetCall/TargetCall.dll
install -m 644 TargetCall.dll \
  data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll
~~~

Target Call 0.2.0 or later can order and relay the shared attack/assist and commander channels. Use 0.2.2 when deploying a new server so the server and clients stay aligned.

### 4. Restart and verify the active runtime

Restart the service after copying DLLs:

~~~
cd /dockerdata/valheim
docker compose restart valheim
~~~

The authoritative server BepInEx log is:

~~~
/dockerdata/valheim/data/bepinex/BepInEx/LogOutput.log
~~~

Check it directly:

~~~
grep -iE 'Target Call|TargetCall|Valhalla Meter|ValhallaMeter|Chainloader' \
  /dockerdata/valheim/data/bepinex/BepInEx/LogOutput.log
~~~

Expected entries include:

~~~
Target Call 0.2.2 loaded
Valhalla Meter 0.2.14 loaded
Chainloader startup complete
~~~

The Docker supervisor log may show only that BepInEx was enabled; use the file above to verify that individual plugins actually loaded.

### 5. Verify hashes and paths

Check that the persistent and active copies are identical:

~~~
sha256sum \
  config/bepinex/plugins/TargetCall/TargetCall.dll \
  data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll

sha256sum \
  config/bepinex/plugins/ValhallaMeter/ValhallaMeter.dll \
  data/bepinex/BepInEx/plugins/ValhallaMeter/ValhallaMeter.dll
~~~

The two hashes in each pair must match. A plugin found only under `config/bepinex` is not sufficient if the server loads from `data/bepinex/BepInEx/plugins`.

## Multiplayer requirements

For complete cooperative DPS coverage, install Valhalla Meter on the dedicated server and every participating client. The server relays events from modded clients; unmodded players may still join, but their owner-side events cannot be reported reliably.

For shared markers, install Target Call on the dedicated server and every client that should see or create markers. A client without Target Call will not see the shared markers.

No new Valheim ports are required. The plugins use the existing Valheim routed-RPC connection. Restart the client after installing or replacing a DLL, and reconnect after restarting the server.

## Upgrades and rollback

Stop or close the affected game process before replacing client DLLs. On the server, copy the existing DLLs to a timestamped backup before replacement, then restart the container. Keep matching copies in both server trees.

To roll back, restore the backed-up DLLs to both `config/bepinex/plugins` and `data/bepinex/BepInEx/plugins`, restart the service, and confirm the restored version in `LogOutput.log`. Do not delete the shared BepInEx installation when disabling one plugin; move only that plugin's directory out of `BepInEx/plugins`.

## Troubleshooting

**The client says “Target sharing needs Target Call on the host.”** Check that the server DLL exists under `data/bepinex/BepInEx/plugins/TargetCall`, that `LogOutput.log` contains `Target Call ... loaded`, and reconnect after the server restart.

**The client sees only personal DPS.** Confirm Valhalla Meter is loaded on the server and all participating clients. With no modded relay, the client can estimate only its own direct outgoing hits through Valheim's displayed damage numbers.

**The overlay does not appear.** Press F7. If it is visible but cannot be moved, press F8 after closing inventory, chat, and other menus.

**Chat is not translated.** Confirm `local.chattranslator.cfg` has a nonempty OpenRouter key, `TargetLanguage = English`, and `Enabled = true`. Translation is client-only and the visible toggle must be ON.

**A plugin is not listed in the log.** Check for an extra extraction level such as `BepInEx/plugins/ValhallaMeter/BepInEx/plugins/...`, remove duplicate old DLLs, and restart. The active server tree is `data/bepinex/BepInEx/plugins`, not just the persistent config tree.

## Related documentation

- [Valhalla Meter README](README.md)
- [Target Call README](src/TargetCall/README.md)
- [Chat Translator README](src/ChatTranslator/README.md)

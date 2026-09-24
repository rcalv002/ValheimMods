# Chat Translator

A client-side Valheim BepInEx plugin that shows incoming player chat in your chosen language (English by default). It does not translate your own messages or change what other players see. It does not need a server-side plugin.

For each incoming player chat line, the plugin asks [Jev 1.13](https://openrouter.ai/typesafe/jev-1.13) a `noul` yes/no question through OpenRouter's Decisions API: is the text predominantly in the configured target language? Jev returns a yes probability; at 0.8 or higher the line stays unchanged. Names, numbers, and language-neutral messages are treated as already in the target language. Otherwise, the plugin asks [GPT-6 Luna](https://openrouter.ai/openai/gpt-6-luna) through OpenRouter's Chat Completions API to translate into the target language. The original line appears immediately, then is replaced in the chat window when the translation arrives. The sender's name and chat color are preserved. The overhead speech bubble remains in its original language.

A small badge at the top center of the game shows `TRANSLATION ON` or `TRANSLATION OFF`. Open the inventory or another screen that frees the mouse cursor, then click the badge to switch modes. The choice is saved as `Enabled` under `[Translation]` in the config file. Turning it off stops new API requests and discards translations still in flight. If no key is configured, the badge reads `TRANSLATION NO KEY`.

## Install and API key

Install BepInExPack Valheim, copy `BepInEx/plugins/ChatTranslator/ChatTranslator.dll` from this release into each client that wants translations, and launch the game once. Set `ApiKey = YOUR_OPENROUTER_KEY` under `[OpenRouter]` in `BepInEx/config/local.chattranslator.cfg`, then restart Valheim. The `OPENROUTER_API_KEY` environment variable also works and takes precedence over the config entry. Keep the key private; the config file stores it as plain text. With no key, the plugin makes no requests and leaves chat untouched. The BepInEx log says whether translation is enabled, without printing the key.

Set `TargetLanguage = English` under `[Translation]` in the same config file to the language you want. Examples: `Portuguese`, `Spanish`, or `pt-BR`. The value defaults to English. Restart Valheim after changing it.

If your OpenRouter account restricts providers, allow **TypeSafe** for Jev and an eligible GPT-6 Luna provider for translation. A blocked Jev request can return HTTP 404 with "No allowed providers are available" even when the API key is valid; the plugin then leaves the original chat line visible. Provider settings apply to new requests without restarting Valheim.

Only the text of eligible incoming messages is sent to OpenRouter. Messages blocked by Valheim's own communication permission checks are never submitted. A request failure, timeout, or malformed answer leaves the original chat line intact. Up to two requests run at once and the queue is limited to 32 messages; messages longer than 1,000 characters are left unchanged. Classification and translation each have a 12-second network timeout. Chat lines that have scrolled out of Valheim's buffer are not changed retroactively.

## Build and checks

Run `scripts/build-chattranslator.ps1` to build against the locally installed Steam version of Valheim and BepInEx, run the request/response checks, and create `dist/ChatTranslator-0.1.2.zip` with SHA-256 checksums. The release contains no API key or game/loader DLL. Live OpenRouter calls require your key, so the automated checks use a fake HTTP server. OpenRouter's Decisions API is currently under `/api/alpha/decisions` and may change.

# Chat Translator

A client-side Valheim BepInEx plugin that shows incoming player chat in English. It does not translate your own messages or change what other players see. It does not need a server-side plugin.

For each incoming player chat line, the plugin asks [Jev 1.13](https://openrouter.ai/typesafe/jev-1.13) through OpenRouter's Decisions API whether the text is predominantly English. It leaves English, names, numbers, and language-neutral messages unchanged. For other languages, it asks [GPT-6 Luna](https://openrouter.ai/openai/gpt-6-luna) through OpenRouter's Chat Completions API for an English translation. The original line appears immediately, then is replaced in the chat window when the translation arrives. The sender's name and chat color are preserved. The overhead speech bubble remains in its original language.

## Install and API key

Install BepInExPack Valheim, copy `BepInEx/plugins/ChatTranslator/ChatTranslator.dll` from this release into each client that wants translations, and launch the game once. Set `ApiKey = YOUR_OPENROUTER_KEY` under `[OpenRouter]` in `BepInEx/config/local.chattranslator.cfg`, then restart Valheim. The `OPENROUTER_API_KEY` environment variable also works and takes precedence over the config entry. Keep the key private; the config file stores it as plain text. With no key, the plugin makes no requests and leaves chat untouched. The BepInEx log says whether translation is enabled, without printing the key.

Only the text of eligible incoming messages is sent to OpenRouter. Messages blocked by Valheim's own communication permission checks are never submitted. A request failure, timeout, or malformed answer leaves the original chat line intact. Up to two requests run at once and the queue is limited to 32 messages; messages longer than 1,000 characters are left unchanged. Classification and translation each have a 12-second network timeout. Chat lines that have scrolled out of Valheim's buffer are not changed retroactively.

## Build and checks

Run `scripts/build-chattranslator.ps1` to build against the locally installed Steam version of Valheim and BepInEx, run the request/response checks, and create `dist/ChatTranslator-0.1.0.zip` with SHA-256 checksums. The release contains no API key or game/loader DLL. Live OpenRouter calls require your key, so the automated checks use a fake HTTP server. OpenRouter's Decisions API is currently under `/api/alpha/decisions` and may change.

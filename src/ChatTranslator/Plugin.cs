using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace ChatTranslator;

[BepInPlugin("local.chattranslator", "Chat Translator", "0.1.1")]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly FieldInfo BufferField = AccessTools.Field(typeof(Terminal), "m_chatBuffer");
    private static readonly MethodInfo RefreshMethod = AccessTools.Method(typeof(Terminal), "UpdateChat");
    private static Plugin? instance;
    private readonly ConcurrentQueue<Completed> completed = new ConcurrentQueue<Completed>();
    private readonly SemaphoreSlim workers = new SemaphoreSlim(2);
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    private ConfigEntry<string> apiKey = null!;
    private ConfigEntry<string> targetLanguage = null!;
    private OpenRouterClient client = null!;
    private Harmony? harmony;
    private int pending;
    private float nextErrorLog;

    private sealed class Completed
    {
        internal Chat Chat = null!;
        internal string OriginalLine = null!;
        internal string? Translation;
        internal string? Error;
    }

    private void Awake()
    {
        instance = this;
        apiKey = Config.Bind("OpenRouter", "ApiKey", "",
            "OpenRouter API key for private, local chat translation. OPENROUTER_API_KEY takes precedence when set. Do not share this config file.");
        targetLanguage = Config.Bind("Translation", "TargetLanguage", "English",
            "Language to show incoming player chat in, such as English, Portuguese, Spanish, or pt-BR.");
        client = new OpenRouterClient();
        harmony = new Harmony("local.chattranslator");
        MethodInfo addChatLine = AccessTools.Method(typeof(Terminal), nameof(Terminal.AddString),
            new[] { typeof(PlatformUserID), typeof(string), typeof(Talker.Type), typeof(bool) });
        if (addChatLine == null || BufferField == null || RefreshMethod == null)
            throw new MissingMethodException("Valheim's chat API changed.");
        harmony.Patch(addChatLine,
            prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeChatLine)),
            postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterChatLine)));
        Logger.LogInfo(string.IsNullOrWhiteSpace(Key())
            ? "Chat Translator 0.1.1 loaded; set OpenRouter ApiKey to enable translation."
            : "Chat Translator 0.1.1 loaded; incoming player chat translation enabled; target language: " + Language() + ".");
    }

    private string Key()
    {
        string environment = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
        return string.IsNullOrWhiteSpace(environment) ? apiKey.Value.Trim() : environment.Trim();
    }

    private string Language()
    {
        string value = targetLanguage.Value.Trim();
        if (value.Length < 2 || value.Length > 48) return "English";
        foreach (char character in value)
            if (!char.IsLetter(character) && character != ' ' && character != '-' &&
                character != '(' && character != ')') return "English";
        return value;
    }

    private static List<string>? Buffer(Terminal terminal) => BufferField.GetValue(terminal) as List<string>;

    private static void BeforeChatLine(Terminal __instance, out string? __state)
    {
        List<string>? lines = Buffer(__instance);
        __state = lines != null && lines.Count != 0 ? lines[lines.Count - 1] : null;
    }

    private static void AfterChatLine(Terminal __instance, PlatformUserID user, string text,
        Talker.Type type, string? __state)
    {
        try
        {
            Plugin? plugin = instance;
            if (plugin == null || !plugin || !(__instance is Chat chat) ||
                !Player.m_localPlayer || type == Talker.Type.Ping ||
                user == PlatformManager.DistributionPlatform.LocalUser.PlatformUserID ||
                string.IsNullOrWhiteSpace(text) || text.Length > 1000) return;
            string key = plugin.Key();
            if (key.Length == 0) return;
            string language = plugin.Language();
            List<string>? lines = Buffer(chat);
            if (lines == null || lines.Count == 0) return;
            string line = lines[lines.Count - 1];
            if (ReferenceEquals(line, __state) || ChatLine.WithTranslation(line, "test") == null) return;
            if (Interlocked.Increment(ref plugin.pending) > 32)
            {
                Interlocked.Decrement(ref plugin.pending);
                return;
            }
            _ = Task.Run(() => plugin.ProcessAsync(chat, line, text, key, language));
        }
        catch (Exception ex)
        {
            instance?.LogError(ex);
        }
    }

    private async Task ProcessAsync(Chat chat, string line, string text, string key, string language)
    {
        bool acquired = false;
        var result = new Completed { Chat = chat, OriginalLine = line };
        try
        {
            await workers.WaitAsync(stopping.Token).ConfigureAwait(false);
            acquired = true;
            result.Translation = await client.TranslateIfNeededAsync(text, key, language, stopping.Token).ConfigureAwait(false);
            if (result.Translation != null) completed.Enqueue(result);
        }
        catch (OperationCanceledException)
        {
            if (!stopping.IsCancellationRequested)
            {
                result.Error = "OpenRouter request timed out.";
                completed.Enqueue(result);
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.GetType().Name + ": " + ex.Message;
            completed.Enqueue(result);
        }
        finally
        {
            if (acquired) workers.Release();
            Interlocked.Decrement(ref pending);
        }
    }

    private void Update()
    {
        while (completed.TryDequeue(out Completed result))
        {
            try
            {
                if (result.Error != null) { LogError(result.Error); continue; }
                if (!result.Chat || result.Chat != Chat.instance || result.Translation == null) continue;
                List<string>? lines = Buffer(result.Chat);
                if (lines == null) continue;
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    if (!ReferenceEquals(lines[i], result.OriginalLine)) continue;
                    string? replacement = ChatLine.WithTranslation(lines[i], result.Translation);
                    if (replacement != null)
                    {
                        lines[i] = replacement;
                        RefreshMethod.Invoke(result.Chat, null);
                    }
                    break;
                }
            }
            catch (Exception ex) { LogError(ex); }
        }
    }

    private void LogError(Exception ex) => LogError(ex.GetType().Name + ": " + ex.Message);

    private void LogError(string message)
    {
        if (Time.realtimeSinceStartup < nextErrorLog) return;
        nextErrorLog = Time.realtimeSinceStartup + 30f;
        Logger.LogWarning("Chat translation failed; original text remains. " + message);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        harmony?.UnpatchSelf();
        stopping.Cancel();
        client?.Dispose();
    }
}

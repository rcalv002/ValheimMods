using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChatTranslator;
using Newtonsoft.Json.Linq;

static void Check(bool value, string name)
{
    if (!value) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
}

const string line = "<color=orange>Ana</color>: <color=#FFFFFFFF>Olá, tudo bem?</color>";
Check(ChatLine.WithTranslation(line, "Hello, how are you?") ==
    "<color=orange>Ana</color>: <color=#FFFFFFFF>Hello, how are you?</color>",
    "Translation retains player name and chat styling");
Check(ChatLine.WithTranslation(line, "<b>hello</b>\nworld") ==
    "<color=orange>Ana</color>: <color=#FFFFFFFF> b hello /b  world</color>",
    "Translation cannot inject rich text or a second chat line");
Check(ChatLine.WithTranslation("system line", "hello") == null,
    "Only formatted player lines are replaced");

using var handler = new FakeHandler();
using var client = new OpenRouterClient(new HttpClient(handler));
string? english = await client.TranslateIfNeededAsync("Hello!", "test-key", "English", default);
Check(english == null && handler.Decisions == 1 && handler.Translations == 0,
    "English takes only the Jev decision path");
string? portuguese = await client.TranslateIfNeededAsync("Olá, tudo bem?", "test-key", "English", default);
Check(portuguese == "Hello, how are you?" && handler.Decisions == 2 && handler.Translations == 1,
    "Portuguese takes Jev then Luna and returns English");
Check(handler.Authenticated && handler.ClassificationPromptValid && handler.TranslationPromptValid,
    "Requests use the expected API shapes and bearer authentication");
string? alreadyPortuguese = await client.TranslateIfNeededAsync("Olá, tudo bem?", "test-key", "Portuguese", default);
string? intoPortuguese = await client.TranslateIfNeededAsync("Hello!", "test-key", "Portuguese", default);
Check(alreadyPortuguese == null && intoPortuguese == "Olá!",
    "Configurable target language changes both classification and translation");
using var lowHandler = new FakeHandler { ForcedProbability = 0.79 };
using var lowClient = new OpenRouterClient(new HttpClient(lowHandler));
using var highHandler = new FakeHandler { ForcedProbability = 0.81 };
using var highClient = new OpenRouterClient(new HttpClient(highHandler));
Check(await lowClient.TranslateIfNeededAsync("Olá, tudo bem?", "test-key", "English", default) != null &&
    lowHandler.Translations == 1 &&
    await highClient.TranslateIfNeededAsync("Olá, tudo bem?", "test-key", "English", default) == null &&
    highHandler.Translations == 0,
    "A Jev probability below 0.8 translates; 0.8 or higher stays unchanged");

sealed class FakeHandler : HttpMessageHandler
{
    internal int Decisions;
    internal int Translations;
    internal bool Authenticated = true;
    internal bool ClassificationPromptValid = true;
    internal bool TranslationPromptValid = true;
    internal double? ForcedProbability;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Authenticated &= request.Headers.Authorization?.Scheme == "Bearer" &&
            request.Headers.Authorization.Parameter == "test-key";
        JObject body = JObject.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (request.RequestUri!.AbsolutePath == "/api/alpha/decisions")
        {
            Decisions++;
            string instructions = (string?)body["questions"]?["language"]?["instructions"] ?? "";
            ClassificationPromptValid &= (string?)body["model"] == "typesafe/jev-1.13" &&
                (string?)body["questions"]?["language"]?["type"] == "noul" &&
                body["questions"]?["language"]?["criteria"]?["true"] != null &&
                body["questions"]?["language"]?["criteria"]?["false"] != null;
            bool portugueseTarget = instructions.Contains("Portuguese");
            bool portugueseText = (string?)body["state"] == "Olá, tudo bem?";
            double probability = ForcedProbability ?? (portugueseTarget == portugueseText ? 0.99 : 0.01);
            return Reply("{\"answers\":{\"language\":{\"type\":\"noul\",\"noul\":" +
                probability.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}}");
        }
        Translations++;
        string prompt = (string?)body["messages"]?[0]?["content"] ?? "";
        string message = (string?)body["messages"]?[1]?["content"] ?? "";
        TranslationPromptValid &= request.RequestUri.AbsolutePath == "/api/v1/chat/completions" &&
            (string?)body["model"] == "openai/gpt-6-luna" &&
            ((message == "Olá, tudo bem?" && prompt.Contains("English")) ||
             (message == "Hello!" && prompt.Contains("Portuguese")));
        string output = message == "Hello!" ? "Olá!" : "Hello, how are you?";
        return Reply(new JObject
        {
            ["choices"] = new JArray(new JObject
            {
                ["message"] = new JObject { ["content"] = output }
            })
        }.ToString(Newtonsoft.Json.Formatting.None));
    }

    private static HttpResponseMessage Reply(string json) => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}

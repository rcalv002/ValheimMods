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
string? english = await client.TranslateIfNeededAsync("Hello!", "test-key", default);
Check(english == null && handler.Decisions == 1 && handler.Translations == 0,
    "English takes only the Jev decision path");
string? portuguese = await client.TranslateIfNeededAsync("Olá, tudo bem?", "test-key", default);
Check(portuguese == "Hello, how are you?" && handler.Decisions == 2 && handler.Translations == 1,
    "Portuguese takes Jev then Luna and returns English");
Check(handler.Authenticated && handler.ClassificationPromptValid && handler.TranslationPromptValid,
    "Requests use the expected API shapes and bearer authentication");

sealed class FakeHandler : HttpMessageHandler
{
    internal int Decisions;
    internal int Translations;
    internal bool Authenticated = true;
    internal bool ClassificationPromptValid = true;
    internal bool TranslationPromptValid = true;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Authenticated &= request.Headers.Authorization?.Scheme == "Bearer" &&
            request.Headers.Authorization.Parameter == "test-key";
        JObject body = JObject.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (request.RequestUri!.AbsolutePath == "/api/alpha/decisions")
        {
            Decisions++;
            ClassificationPromptValid &= (string?)body["model"] == "typesafe/jev-1.13" &&
                (string?)body["questions"]?["language"]?["type"] == "choice" &&
                body["questions"]?["language"]?["criteria"]?["english"] != null &&
                body["questions"]?["language"]?["criteria"]?["other"] != null;
            string choice = (string?)body["state"] == "Hello!" ? "english" : "other";
            return Reply("{\"answers\":{\"language\":{\"type\":\"choice\",\"choice\":\"" + choice + "\"}}}");
        }
        Translations++;
        TranslationPromptValid &= request.RequestUri.AbsolutePath == "/api/v1/chat/completions" &&
            (string?)body["model"] == "openai/gpt-6-luna" &&
            (string?)body["messages"]?[1]?["content"] == "Olá, tudo bem?";
        return Reply("{\"choices\":[{\"message\":{\"content\":\"Hello, how are you?\"}}]}");
    }

    private static HttpResponseMessage Reply(string json) => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}

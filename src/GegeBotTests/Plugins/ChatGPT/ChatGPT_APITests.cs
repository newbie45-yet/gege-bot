using GegeBot.Plugins.ChatGPT;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;

namespace GegeBot.Plugins.ChatGPT.Tests
{
    [TestClass()]
    public class ChatGPT_APITests
    {
        ChatGPT_API _API = new ChatGPT_API("http://127.0.0.1:8000", File.ReadAllText("chatgpt/cookies.txt"));

        JsonNode GetAskResult(JsonArray messages)
        {
            var result = _API.Completions(messages);
            return result["choices"][0]["message"].AsObject();
        }

        [TestMethod()]
        public void CompletionsTest()
        {
            JsonArray messages = new();
            JsonObject jsonObj = new()
            {
                { "role", "user" },
                { "content", "你是谁" }
            };
            messages.Add(jsonObj);
            var result = GetAskResult(messages);
            string role = result["role"].GetValue<string>();
            string content = result["content"].GetValue<string>();
        }
    }
}
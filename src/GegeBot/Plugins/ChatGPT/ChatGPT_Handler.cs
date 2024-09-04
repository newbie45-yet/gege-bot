using CQHttp;
using CQHttp.DTOs;
using GegeBot.Plugins.EdgeGPT;
using GegeBot.Plugins.LlamaCpp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GegeBot.Plugins.ChatGPT
{
    internal class ChatGPT_Handler : IPlugin
    {
        readonly CQBot cqBot;
        readonly Log log = new("chatgpt");

        ChatGPT_API _API;
        readonly Hashtable dictionary = new();

        public ChatGPT_Handler(CQBot bot)
        {
            cqBot = bot;
            cqBot.ReceivedMessage += CqBot_ReceivedMessage;
            cqBot.ReceivedGroupBan += CqBot_ReceivedGroupBan;

            Reload();
        }

        public void Reload()
        {
            if (!ChatGPT_Config.Enable) return;

            _API = new(ChatGPT_Config.ServerAddress, File.ReadAllText(ChatGPT_Config.CookieFilePath));

            dictionary.Clear();

            foreach (string file in ChatGPT_Config.DictFile)
            {
                Console.WriteLine($"[ChatGPT]加载词库 {file}");

                string data = File.ReadAllText(file);
                var jsonArray = Json.ToJsonNode(data).AsArray();
                foreach (var item in jsonArray)
                {
                    dictionary.Add(item.ToString(), null);
                }
            }

            Console.WriteLine($"[ChatGPT]词库加载完毕，共计 {dictionary.Count} 条。");
        }


        private void SaveDbValue(string key, string text)
        {
            ChatGPT_Db.Db.SetValue(key, text);
        }

        JsonNode GetAskResult(JsonArray messages, string conversation_id = "")
        {
            var result = _API.Completions(messages, conversation_id);
            foreach (var item in result["choices"].AsArray())
            {
                if (item["message"]["role"].GetValue<string>() == "assistant")
                    return item["message"].AsObject();
            }
            return null;
        }

        private string GetUserName(CQEventMessageEx msg)
        {
            string userName = msg.sender.nickname;
            if (LlamaCppConfig.UseGroupCard && msg.message_type == CQMessageType.Group)
            {
                if (!string.IsNullOrEmpty(msg.sender.card))
                    userName = msg.sender.card;
            }
            return userName;
        }

        void InitFirstPrompt(out string prompt, out string role, out string content)
        {
            prompt = "";
            role = "";
            content = "";

            if (string.IsNullOrWhiteSpace(ChatGPT_Config.PromptFilePath) || !File.Exists(ChatGPT_Config.PromptFilePath))
                return;

            prompt = File.ReadAllText(ChatGPT_Config.PromptFilePath);

            if (string.IsNullOrWhiteSpace(prompt)) return;

            JsonArray messages = new();
            JsonObject jsonObj = new()
            {
                { "role", "user" },
                { "content", prompt }
            };
            messages.Add(jsonObj);
            var result = GetAskResult(messages);
            role = result["role"].GetValue<string>();
            content = result["content"].GetValue<string>();

            if (EdgeGptConfig.WriteMessageLog)
                log.WriteInfo($"\nfirst:{prompt}\nbot:{content}");
        }

        string Ask(string key, string text)
        {
            JsonArray messages = new JsonArray();
            string role, content;

            string value = ChatGPT_Db.Db.GetValue(key);
            ChatGptConversations model = null;
            if (!string.IsNullOrEmpty(value))
                model = Json.FromJsonString<ChatGptConversations>(value);
            if (model == null)
            {
                model = new ChatGptConversations();

                InitFirstPrompt(out string prompt, out role, out content);
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    model.Chats.Add(new ChatGptModel()
                    {
                        Name = "user",
                        Content = prompt,
                        Created = DateTime.Now
                    });
                    model.Chats.Add(new ChatGptModel()
                    {
                        Name = role,
                        Content = content,
                        Created = DateTime.Now
                    });
                }
            }

            JsonObject jsonObj;
            foreach (var chat in model.Chats)
            {
                jsonObj = new()
                {
                    { "role", chat.Name },
                    { "content", chat.Content }
                };
                messages.Add(jsonObj);
            }

            jsonObj = new()
            {
                { "role", "user" },
                { "content", text }
            };
            messages.Add(jsonObj);

            model.Chats.Add(new ChatGptModel()
            {
                Name = "user",
                Content = text,
                Created = DateTime.Now
            });

            var result = GetAskResult(messages);
            role = result["role"].GetValue<string>();
            content = result["content"].GetValue<string>();

            model.Chats.Add(new ChatGptModel()
            {
                Name = role,
                Content = content,
                Created = DateTime.Now
            });

            SaveDbValue(key, Json.ToJsonString(model));

            return content;
        }

        string Ask(CQEventMessageEx msg, string text)
        {
            string key = BotSession.GetSessionKey(msg);
            return Ask(key, text);
        }

        void Reset(CQEventMessageEx msg)
        {
            string key = BotSession.GetSessionKey(msg);
            ChatGPT_Db.Db.SetValue(key, "");
        }

        private void CqBot_ReceivedGroupBan(CQEventGroupBan obj)
        {
            if (obj.sub_type == CQEventGroupBanSubType.Ban)
            {
                if (obj.user_id.ToString() == cqBot.BotID)
                {
                    if (!string.IsNullOrWhiteSpace(ChatGPT_Config.BannedPromptFilePath))
                    {
                        string text = File.ReadAllText(ChatGPT_Config.BannedPromptFilePath);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            string key = $"{CQMessageType.Group}_{obj.group_id}";
                            string content = Ask(key, text);
                            if (ChatGPT_Config.WriteMessageLog)
                                log.WriteInfo($"\n{ChatGPT_Config.BannedPromptFilePath}\nbot:{content}");
                        }
                    }
                }
            }
        }

        private void CqBot_ReceivedMessage(CQEventMessageEx msgObj)
        {
            if (!ChatGPT_Config.Enable) return;

            string text = CQCode.GetText(msgObj.message, out var atList, out var imageList).TrimStart();

            foreach (string keyword in ChatGPT_Config.FilterText)
            {
                if (text.StartsWith(keyword))
                {
                    return;
                }
            }

            bool isKeyword = ChatGPT_Config.ReplyProbability > 0 && dictionary.Count > 0
                             && !string.IsNullOrWhiteSpace(text) && dictionary.ContainsKey(text);
            if (isKeyword)
            {
                double num = new Random().NextDouble();
                if (num >= ChatGPT_Config.ReplyProbability)
                    isKeyword = false;
            }

            if (!isKeyword && msgObj.message_type == CQMessageType.Group &&
                (!atList.Any() || !atList.Contains(msgObj.self_id.ToString())))
                return;

            if (!string.IsNullOrEmpty(ChatGPT_Config.ResetCommand) && text == ChatGPT_Config.ResetCommand)
            {
                Reset(msgObj);
                if (!string.IsNullOrEmpty(ChatGPT_Config.ResetMessage))
                    cqBot.Message_QuickReply(msgObj, new CQCode().SetReply(msgObj.message_id).SetText(ChatGPT_Config.ResetMessage));
                return;
            }

            int retryCounter = 0;

        RETRY:

            string userName = GetUserName(msgObj);
            string content = "";

            try
            {
                content = Ask(msgObj, $"{userName}说：{text}");
                //string keyword = "[内容由于不合规被停止生成，我们换个话题吧]";
                //if (content.Contains(keyword))
                //{
                //    content = "";
                //    throw new Exception(keyword);
                //}
            }
            catch (Exception ex)
            {
                log.WriteError(ex.ToString());
            }

            if (string.IsNullOrWhiteSpace(content) && retryCounter < 1)
            {
                Reset(msgObj);
                retryCounter++;
                goto RETRY;
            }

            if (ChatGPT_Config.WriteMessageLog)
                log.WriteInfo($"\n{userName}:{text}\nbot:{content}");

            if (string.IsNullOrWhiteSpace(content))
                content += $"\n检测到Bot无法回复，如持续出现此问题，请对我说“{ChatGPT_Config.ResetCommand}”。";

            CQCode cqCode = new CQCode();
            cqCode.SetReply(msgObj.message_id);
            cqCode.SetText(content);
            cqBot.Message_QuickReply(msgObj, cqCode);
        }
    }
}

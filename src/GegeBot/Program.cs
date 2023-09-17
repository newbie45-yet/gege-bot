﻿using CQHttp;
using CQHttp.DTOs;
using GegeBot.Plugins;
using System.Reflection;

namespace GegeBot
{
    internal class Program
    {
        static IEnumerable<Type> GetAllPlugins()
        {
            var asmTypes = Assembly.GetExecutingAssembly().GetTypes().Where(a => !a.IsAbstract && a.IsClass);
            var baseType = typeof(IPlugin);
            var implClasses = asmTypes.Where(a => a.GetCustomAttribute<PluginAttribute>() != null
            || ((TypeInfo)a).ImplementedInterfaces.Any(a => a == baseType));
            return implClasses;
        }

        static CQBot cqBot;
        static Config config;
        static Log log;

        static void Main(string[] args)
        {
            log = new Log("logs");

            config = new Config("config.json");
            config.Load();

            cqBot = new CQBot(BotConfig.WSAddress);
            cqBot.ReceivedMessage += CqBot_ReceivedMessage;
            cqBot.Exception += CqBot_Exception;

            foreach (var p in GetAllPlugins())
            {
                Console.WriteLine($"[插件]加载 {p.Name}");
                try
                {
                    if (Activator.CreateInstance(p, cqBot) is IPlugin instance)
                    {

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[插件]加载失败 {p.Name}，{ex.Message}");
                }
            }
            Console.WriteLine($"[插件]加载完毕");

            cqBot.Run().Wait();
        }

        private static void CqBot_ReceivedMessage(CQEventMessageEx obj)
        {
            string text = obj.raw_message;
            if (obj.message_type == CQMessageType.Private)
            {
                Console.WriteLine($"[私聊消息]收到 {obj.sender.nickname}({obj.sender.user_id}) 的消息：{text}");
                if (obj.user_id == BotConfig.ManagerQQ)
                {
                    if (text == "--重载配置")
                    {
                        config.Load();

                        CQRequestMessage requestMessage = new();
                        requestMessage.message_type = obj.message_type;
                        requestMessage.user_id = obj.user_id;
                        requestMessage.message = new CQCode($"已重新加载配置").SetReply(obj.message_id).ToJson();
                        cqBot.Message_SendMsg(requestMessage);
                    }
                }
            }
            else if (obj.message_type == CQMessageType.Group)
            {
                Console.WriteLine($"[群消息]收到群({obj.group_id})成员 {obj.sender.nickname}({obj.sender.user_id}) 的消息：{text}");
            }
        }

        private static void CqBot_Exception(Exception ex, CQSession session)
        {
            if (session.CurrentMessage != null)
            {
                var obj = session.CurrentMessage;

                if (obj.message_type == CQMessageType.Private)
                {
                    log.WriteError($"[私聊消息]收到 {obj.sender.nickname}({obj.sender.user_id}) 的消息：{obj.raw_message}");
                }
                else if (obj.message_type == CQMessageType.Group)
                {
                    log.WriteError($"[群消息]收到群({obj.group_id})成员 {obj.sender.nickname}({obj.sender.user_id}) 的消息：{obj.raw_message}");
                }

                log.WriteError($"{ex}");

                CQRequestMessage requestMessage = new();
                requestMessage.message_type = obj.message_type;

                if (obj.message_type == CQMessageType.Private)
                {
                    requestMessage.user_id = obj.user_id;
                }
                else if (obj.message_type == CQMessageType.Group)
                {
                    requestMessage.group_id = obj.group_id;
                }
                else { return; }

                requestMessage.message = new CQCode($"发生错误：{ex.Message}").SetReply(obj.message_id).ToJson();
                cqBot.Message_SendMsg(requestMessage, result =>
                {
                    if (BotConfig.DeleteErrorMessageTimeout < 1) return;

                    Task.Delay(1000 * BotConfig.DeleteErrorMessageTimeout).Wait();
                    cqBot.Message_DeleteMsg(result.message_id);
                });
            }
            else log.WriteError($"{ex}");

        }
    }
}
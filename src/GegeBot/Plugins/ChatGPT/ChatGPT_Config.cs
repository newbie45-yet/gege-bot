
namespace GegeBot.Plugins.ChatGPT
{
    [Config("ChatGPT")]
    internal class ChatGPT_Config
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public static bool Enable { get; set; }
        /// <summary>
        /// 服务地址
        /// </summary>
        public static string ServerAddress { get; set; }
        /// <summary>
        /// 记录消息日志
        /// </summary>
        public static bool WriteMessageLog { get; set; }
        /// <summary>
        /// cookies 文件路径
        /// </summary>
        public static string CookieFilePath { get; set; }
        /// <summary>
        /// 默认提示词文件路径，不填不做默认对话处理
        /// </summary>
        public static string PromptFilePath { get; set; }
        /// <summary>
        /// 被禁言提示词文件路径，不填不发送给 GPT
        /// </summary>
        public static string BannedPromptFilePath { get; set; }
        /// <summary>
        /// 记忆重置命令
        /// </summary>
        public static string ResetCommand { get; set; }
        /// <summary>
        /// 重置后发送的消息，不填不发送
        /// </summary>
        public static string ResetMessage { get; set; }
        /// <summary>
        /// 要过滤掉的文本，避免触发回复
        /// </summary>
        public static List<string> FilterText { get; set; } = new List<string>();
        /// <summary>
        /// 字典文件集，用于自动回复
        /// </summary>
        public static List<string> DictFile { get; set; } = new List<string>();
        /// <summary>
        /// 自动回复概率 0.0 - 1.0
        /// </summary>
        public static double ReplyProbability { get; set; }
    }
}

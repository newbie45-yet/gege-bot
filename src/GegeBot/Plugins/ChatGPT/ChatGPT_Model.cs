namespace GegeBot.Plugins.ChatGPT
{
    internal class ChatGptModel
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public DateTime Created { get; set; }
    }

    internal class ChatGptConversations
    {
        public List<ChatGptModel> Chats { get; set; } = new();
    }
}

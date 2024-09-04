using GegeBot.Db;

namespace GegeBot.Plugins.ChatGPT
{
    internal class ChatGPT_Db
    {
        static IDb _db;
        public static IDb Db
        {
            get
            {
                _db ??= DbProvider.GetDb("chatgpt");
                return _db;
            }
        }
    }
}

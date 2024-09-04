using CQHttp;
using CQHttp.DTOs;
using GegeBot.Plugins.LlamaCpp;
using GegeBot.Plugins.Pixiv;
using GegeBot.Plugins.Setu;

namespace GegeBot.Plugins.Poke
{
    [Plugin]
    internal class PokeHandler
    {
        readonly CQBot cqBot;
        readonly SetuAPI setuAPI = new SetuAPI();
        readonly PixivAPI pixivAPI = new PixivAPI();

        public PokeHandler(CQBot bot)
        {
            cqBot = bot;
            cqBot.ReceivedPoke += CqBot_ReceivedPoke; ;
        }

        List<string> GetFiles()
        {
            string[] extensions = [".png", ".jpg", ".jpeg", ".gif"];
            return Directory.EnumerateFiles(PokeConfig.FilePath, "*.*").Where(x => extensions.Any(a => x.ToLower().EndsWith(a))).ToList();
        }

        private void CqBot_ReceivedPoke(CQEventPoke obj)
        {
            if (obj.target_id.ToString() != cqBot.BotID) return;

            int action = new Random().Next(0, 3);
            var cqCode = new CQCode();

            if (action == 1)
            {
                var result = setuAPI.Get()["data"][0];
                string tips = result["title"].GetValue<string>() + " - " + result["author"].GetValue<string>() + "\n";
                tips += $"id{result["pid"]} - 画师id{result["uid"]}\n\n";
                cqCode.SetText(tips);

                string url = result["urls"]["original"].GetValue<string>();
                var images = pixivAPI.DownloadImages([url]);
                if (images.Length > 0)
                    cqCode.SetImage(images[0].Value);
            }
            else
            {
                var files = GetFiles();
                if (files.Count < 1) return;
                int n = new Random().Next(files.Count);
                byte[] data = File.ReadAllBytes(files[n]);
                cqCode = new CQCode().SetImage(data, true);
            }


            Action<CQResponseMessage> callback = null;
            if (action == 1)
            {
                callback = result =>
                {
                    if (result == null || SetuConfig.DeleteSeconds < 1) return;

                    Task.Delay(1000 * SetuConfig.DeleteSeconds).Wait();
                    cqBot.Message_DeleteMsg(result.message_id);
                };
            }

            if (obj.group_id != 0)
            {
                cqBot.Message_SendGroupMsg(obj.group_id, cqCode, callback);
            }
            else
            {
                cqBot.Message_SendPrivateMsg(obj.user_id, cqCode, callback);
            }
        }
    }
}

using CQHttp;
using RestSharp;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace GegeBot.Plugins.ChatGPT
{
    public class ChatGPT_API
    {
        private readonly RestClient client;
        private readonly RestClient download_client;
        private readonly string _serverAddress;

        public ChatGPT_API(string serverAddress, string refresh_token)
        {
            _serverAddress = serverAddress;
            client = new RestClient();
            client.AddDefaultHeader("Content-Type", "application/json");
            client.AddDefaultHeader("Accept", "application/json");
            client.AddDefaultHeader("Authorization", $"Bearer {refresh_token}");

            download_client = new RestClient();
        }

        public JsonNode Completions(JsonArray messages, string model = "kimi", string conversation_id = "", bool use_search = false, bool stream = false)
        {
            JsonObject requestObj = new JsonObject();
            var request = new RestRequest($"{_serverAddress}/v1/chat/completions", Method.Post);
            requestObj.Add("model", model);
            requestObj.Add("conversation_id", conversation_id);
            requestObj.Add("messages", messages);
            requestObj.Add("use_search", use_search);
            requestObj.Add("stream", stream);
            request.AddJsonBody(requestObj.ToJsonString(), false);
            RestResponse response = client.Execute(request);
            JsonNode json_result = Json.ToJsonNode(response.Content);
            return json_result;
        }

        private async Task<RestResponse> ExecuteDownloadAndRetryAsync(RestRequest request, int retryCount = 3)
        {
            int retryCounter = 0;
            request.Timeout = 1000 * 30;

            RestResponse response;
            do
            {
                response = await download_client.ExecuteAsync(request);
                if (!response.IsSuccessful || response.ContentLength < 1)
                {
                    retryCounter++;
                }
                else break;
            }
            while (retryCounter < retryCount);

            return response;
        }

        private async Task<byte[]> Download(string url)
        {
            Console.WriteLine($"[kimi]下载 {url}");
            var request = new RestRequest(url, Method.Get);
            RestResponse response = await ExecuteDownloadAndRetryAsync(request);
            if (response.RawBytes != null && response.RawBytes.Length > 0)
            {
                return response.RawBytes;
            }
            Console.WriteLine($"[kimi]下载失败 {url}");
            return null;
        }

        public KeyValuePair<int, byte[]>[] DownloadImages(List<string> urls)
        {
            ConcurrentDictionary<int, byte[]> imageDict = new();

            Parallel.For(0, urls.Count, (i) =>
            {
                string url = urls[i];
                var result = Download(url).Result;
                if (result != null)
                {
                    imageDict.TryAdd(i, result);
                }
            });

            return imageDict.OrderBy(a => a.Key).ToArray();
        }
    }
}

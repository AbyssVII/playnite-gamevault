using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace GameVault
{
    /// <summary>AI 配置（存在 settings.json 里，跟着插件设置一起走）。</summary>
    [DataContract]
    public class AiConfig
    {
        /// <summary>
        /// OpenAI 兼容的 chat/completions 完整地址。
        /// 默认指向火山方舟（豆包）—— 用户明确要求用豆包。
        /// </summary>
        public const string DefaultEndpoint = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";

        /// <summary>默认模型：豆包 1.6 的通用版本（官方文档里的常见 ID）。</summary>
        public const string DefaultModel = "doubao-seed-1-6-250615";

        [DataMember(Name = "Endpoint")]
        public string Endpoint { get; set; }

        [DataMember(Name = "ApiKey")]
        public string ApiKey { get; set; }

        [DataMember(Name = "Model")]
        public string Model { get; set; }

        /// <summary>是否已经配置好（有 Key 才算）。</summary>
        public bool Ready
        {
            get { return !string.IsNullOrWhiteSpace(ApiKey); }
        }

        public string EndpointOr => string.IsNullOrWhiteSpace(Endpoint) ? DefaultEndpoint : Endpoint.Trim();
        public string ModelOr => string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model.Trim();
    }

    /// <summary>AI 返回的一条「按类型分类的库外推荐」。</summary>
    public class AiGenrePick
    {
        /// <summary>类型名（如"动作""角色扮演"）。</summary>
        public string Genre { get; set; }

        /// <summary>该类型下推荐的库外游戏名，按推荐度排序。</summary>
        public List<string> Games { get; set; } = new List<string>();

        /// <summary>一句话说明为什么按这个类型推（AI 给的）。</summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// 库外游戏推荐：调用 OpenAI 兼容的聊天补全接口，让模型按类型分类并推荐
    /// "用户库里没有、但同类型值得玩"的游戏。
    ///
    /// 设计取舍：
    ///   - 只把**口味摘要**（各类型时长占比 + 若干已玩代表作）发给模型，**不上传整个库**。
    ///     这既省 token 又保护隐私 —— 用户明确关心过"不上传数据"。
    ///   - 模型被要求返回**严格的 JSON**（而不是自然语言），解析起来稳定得多。
    ///   - 模型不做"幻觉防护"——它推荐的东西可能不存在或早已停服。这是我们
    ///     标注为"库外推荐"，且不做 Metacritic 评分之类断言的原因。
    ///   - 任何异常（网络/超时/解析失败）都返回 null，由调用方回退到本地推荐。
    /// </summary>
    public static class AiRecommender
    {
        private const int TimeoutMs = 25000;

        /// <summary>
        /// 让模型按类型给出库外推荐。失败/未配置时返回 null。
        /// </summary>
        public static List<AiGenrePick> Fetch(VaultData data, AiConfig config)
        {
            if (data == null || config == null || !config.Ready) return null;
            try
            {
                var payload = BuildPrompt(data);
                var body = BuildRequestBody(config, payload);
                var response = Post(config, body);
                if (string.IsNullOrEmpty(response)) return null;
                return ParseResponse(response);
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------
        // 请求构造
        // ------------------------------------------------------------------

        /// <summary>
        /// 构造给模型的口味摘要。刻意**只发统计特征 + 少量代表作**：
        /// 完整游戏库可能上千条，既浪费 token 也没必要 —— 判断口味不需要每一款。
        /// </summary>
        private static string BuildPrompt(VaultData data)
        {
            var sb = new StringBuilder();
            var facts = data.Facts ?? new ProfileFacts();

            sb.AppendLine("以下是一位玩家的游戏库统计，请据此推荐**他库里没有的**同类优秀游戏。");
            sb.AppendLine();
            sb.AppendLine("【统计概况】");
            sb.AppendLine("- 库存总数：" + data.MergedCount + " 款");
            sb.AppendLine("- 已玩过：" + facts.PlayedGames + " 款，未玩：" + facts.UnplayedGames + " 款");
            sb.AppendLine("- 总时长：" + TimeFmt.Short(data.TotalPlaytime));
            if (facts.MedianPlayedTime > 0)
                sb.AppendLine("- 已玩游戏的中位时长：" + TimeFmt.Short(facts.MedianPlayedTime));
            if (data.ScoredCount > 0)
                sb.AppendLine("- 有评分游戏的平均分：" + data.AverageScore.ToString("0.#"));

            sb.AppendLine();
            sb.AppendLine("【各类型时长占比（这是判断口味的关键）】");
            foreach (var slice in data.GenreByTime.Take(8))
                sb.AppendLine("- " + slice.Name + "：" + slice.ShareText + "，共 " + slice.GameCount + " 款");

            var top = data.Entries
                .Where(e => e.TotalPlaytime > 0)
                .OrderByDescending(e => e.TotalPlaytime)
                .Take(12)
                .ToList();
            if (top.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【玩得最多的代表作】");
                foreach (var e in top)
                    sb.AppendLine("- " + e.Name + "（" + e.TotalLongText + "）");
            }

            sb.AppendLine();
            sb.AppendLine("【输出要求】");
            sb.AppendLine("1. 只推荐上面没出现过的游戏（即玩家库里没有的），不要推荐榜单里已有的。");
            sb.AppendLine("2. 按类型分类，每个类型的推荐放在一起。");
            sb.AppendLine("3. 每个类型推荐 3 到 6 款，优先推荐口碑好、有代表性的作品。");
            sb.AppendLine("4. 只输出 JSON，不要任何解释文字、不要 markdown 代码块。");
            sb.AppendLine("5. JSON 格式严格如下：");
            sb.AppendLine("{\"genres\":[{\"genre\":\"类型名\",\"note\":\"一句话说明\",\"games\":[\"游戏名1\",\"游戏名2\"]}]}");
            sb.AppendLine();
            sb.AppendLine("注意：类型名与 note 要用简体中文；游戏名用其官方名称（英文游戏给英文名）。");
            return sb.ToString();
        }

        private static string BuildRequestBody(AiConfig config, string prompt)
        {
            var serializer = new DataContractJsonSerializer(typeof(ChatRequest));
            var request = new ChatRequest
            {
                model = config.ModelOr,
                messages = new List<ChatMessage>
                {
                    new ChatMessage { role = "system", content = SystemPrompt },
                    new ChatMessage { role = "user", content = prompt }
                },
                // 要求严格 JSON，降低解析失败率（火山方舟与 OpenAI 都支持这个字段）
                response_format = new ResponseFormat { type = "json_object" }
            };

            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, request);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private const string SystemPrompt =
            "你是一位资深的电子游戏推荐编辑，熟悉各平台各年代的优秀作品。" +
            "你会根据玩家的真实口味推荐他们没玩过的同类佳作。只输出 JSON。";

        // ------------------------------------------------------------------
        // 网络
        // ------------------------------------------------------------------

        private static string Post(AiConfig config, string jsonBody)
        {
            var request = (HttpWebRequest)WebRequest.Create(config.EndpointOr);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers["Authorization"] = "Bearer " + config.ApiKey.Trim();
            request.Timeout = TimeoutMs;
            request.ReadWriteTimeout = TimeoutMs;
            request.KeepAlive = false;
            // 沙箱/公司网络下常见代理，跟随系统默认设置
            request.Proxy = WebRequest.DefaultWebProxy;

            var bytes = Encoding.UTF8.GetBytes(jsonBody);
            request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        // ------------------------------------------------------------------
        // 响应解析
        // ------------------------------------------------------------------

        /// <summary>
        /// 从聊天补全响应里抠出模型输出的 JSON。
        ///
        /// 不用 DataContractJsonSerializer 反序列化最外层，是因为不同厂商的
        /// 响应结构略有差异（usage/finish_reason 的字段不完全一致），用正则
        /// 抓 content 字段更宽容；抓到之后再严格反序列化内容本身。
        /// </summary>
        private static List<AiGenrePick> ParseResponse(string body)
        {
            var content = ExtractContent(body);
            if (string.IsNullOrEmpty(content)) return null;

            // 模型有时会裹一层 ```json ... ```，剥掉
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var firstNewline = content.IndexOf('\n');
                if (firstNewline > 0) content = content.Substring(firstNewline + 1);
                var fence = content.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) content = content.Substring(0, fence);
                content = content.Trim();
            }

            // 只取第一个 { 到最后一个 } 之间的内容，忽略前后的客套话
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            content = content.Substring(start, end - start + 1);

            var serializer = new DataContractJsonSerializer(typeof(AiPayload));
            AiPayload payload;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                payload = serializer.ReadObject(stream) as AiPayload;
            }
            if (payload == null || payload.genres == null) return null;

            var picks = new List<AiGenrePick>();
            foreach (var item in payload.genres)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.genre)) continue;
                if (item.games == null) continue;
                var games = item.games.Where(g => !string.IsNullOrWhiteSpace(g))
                                      .Select(g => g.Trim())
                                      .Distinct()
                                      .Take(6)
                                      .ToList();
                if (games.Count == 0) continue;
                picks.Add(new AiGenrePick
                {
                    Genre = item.genre.Trim(),
                    Note = item.note,
                    Games = games
                });
            }
            return picks.Count > 0 ? picks : null;
        }

        /// <summary>从 {"choices":[{"message":{"content":"..."}}]} 里抓 content。</summary>
        private static string ExtractContent(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ChatResponse));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body)))
                {
                    var response = serializer.ReadObject(stream) as ChatResponse;
                    if (response != null && response.choices != null && response.choices.Count > 0)
                    {
                        var message = response.choices[0].message;
                        if (message != null && !string.IsNullOrEmpty(message.content))
                            return message.content;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        // ------------------------------------------------------------------
        // 传输结构
        // ------------------------------------------------------------------

        [DataContract]
        private class ChatRequest
        {
            [DataMember(Name = "model", Order = 1)] public string model { get; set; }
            [DataMember(Name = "messages", Order = 2)] public List<ChatMessage> messages { get; set; }
            [DataMember(Name = "response_format", Order = 3)] public ResponseFormat response_format { get; set; }
        }

        [DataContract]
        private class ResponseFormat
        {
            [DataMember(Name = "type")] public string type { get; set; }
        }

        [DataContract]
        private class ChatMessage
        {
            [DataMember(Name = "role")] public string role { get; set; }
            [DataMember(Name = "content")] public string content { get; set; }
        }

        [DataContract]
        private class ChatResponse
        {
            [DataMember(Name = "choices")] public List<Choice> choices { get; set; }
        }

        [DataContract]
        private class Choice
        {
            [DataMember(Name = "message")] public ChatMessage message { get; set; }
        }

        [DataContract]
        private class AiPayload
        {
            [DataMember(Name = "genres")] public List<AiGenreItem> genres { get; set; }
        }

        [DataContract]
        private class AiGenreItem
        {
            [DataMember(Name = "genre")] public string genre { get; set; }
            [DataMember(Name = "note")] public string note { get; set; }
            [DataMember(Name = "games")] public List<string> games { get; set; }
        }
    }
}

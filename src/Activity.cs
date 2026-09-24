using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameVault
{
    [System.Runtime.Serialization.DataContract]
    public class ActivityFile
    {
        [System.Runtime.Serialization.DataMember(Name = "SessionPlaytime")]
        public long SessionPlaytime { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "Items")]
        public List<ActivityItem> Items { get; set; }
    }

    [System.Runtime.Serialization.DataContract]
    public class ActivityItem
    {
        [System.Runtime.Serialization.DataMember(Name = "DateSession")]
        public string DateSession { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "ElapsedSeconds")]
        public long ElapsedSeconds { get; set; }
    }

    /// <summary>
    /// 读取 GameActivity 插件记录的游玩会话，用于计算"最近 N 天"的时长。
    /// Playnite 本体只保存总时长，历史分段时长只有 GameActivity 有。
    /// </summary>
    public class ActivityStore
    {
        private readonly Dictionary<string, List<KeyValuePair<DateTime, ulong>>> perGame =
            new Dictionary<string, List<KeyValuePair<DateTime, ulong>>>();

        public bool Available { get; private set; }
        public string SourcePath { get; private set; }

        public static ActivityStore Load(string extensionsDataPath)
        {
            var store = new ActivityStore();
            try
            {
                if (string.IsNullOrEmpty(extensionsDataPath) || !Directory.Exists(extensionsDataPath))
                    return store;

                var gaDirs = Directory.GetDirectories(extensionsDataPath)
                    .Select(d => Path.Combine(d, "GameActivity"))
                    .Where(Directory.Exists)
                    .ToList();
                if (gaDirs.Count == 0) return store;

                store.SourcePath = gaDirs[0];
                store.Available = true;
                var files = new List<string>();
                foreach (var dir in gaDirs)
                    files.AddRange(Directory.GetFiles(dir, "*.json"));
                // 并行解析：GameActivity 的历史会话可能有数百个文件
                Parallel.ForEach(files, file => store.Parse(Path.GetFileNameWithoutExtension(file), file));
            }
            catch
            {
            }
            return store;
        }

        private readonly object sync = new object();

        private static readonly Regex DateRx = new Regex("\"DateSession\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex SecRx = new Regex("\"ElapsedSeconds\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        private void Parse(string id, string file)
        {
            try
            {
                var text = File.ReadAllText(file);
                var dateMatches = DateRx.Matches(text);
                if (dateMatches.Count == 0) return;
                var secMatches = SecRx.Matches(text);

                var list = new List<KeyValuePair<DateTime, ulong>>();

                if (dateMatches.Count == secMatches.Count)
                {
                    // 快速路径：两个字段在每个会话对象里始终成对且顺序一致，直接顺序配对，
                    // 比走 DataContractJsonSerializer 的反射解析快很多。
                    for (var i = 0; i < dateMatches.Count; i++)
                    {
                        var when = ParseDate(dateMatches[i].Groups[1].Value);
                        if (!when.HasValue) continue;
                        ulong sec;
                        if (!ulong.TryParse(secMatches[i].Groups[1].Value, out sec) || sec == 0) continue;
                        list.Add(new KeyValuePair<DateTime, ulong>(when.Value, sec));
                    }
                }
                else
                {
                    ParseStrict(file, list);
                }

                if (list.Count == 0) return;
                lock (sync)
                {
                    List<KeyValuePair<DateTime, ulong>> target;
                    if (!perGame.TryGetValue(id, out target))
                    {
                        target = new List<KeyValuePair<DateTime, ulong>>();
                        perGame[id] = target;
                    }
                    target.AddRange(list);
                }
            }
            catch
            {
            }
        }

        /// <summary>字段数量不匹配时的严格回退解析。</summary>
        private static void ParseStrict(string file, List<KeyValuePair<DateTime, ulong>> list)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ActivityFile));
                ActivityFile data;
                using (var stream = new MemoryStream(File.ReadAllBytes(file)))
                {
                    data = serializer.ReadObject(stream) as ActivityFile;
                }
                if (data == null || data.Items == null) return;
                foreach (var item in data.Items)
                {
                    if (item == null) continue;
                    var when = ParseDate(item.DateSession);
                    if (!when.HasValue || item.ElapsedSeconds <= 0) continue;
                    list.Add(new KeyValuePair<DateTime, ulong>(when.Value, (ulong)item.ElapsedSeconds));
                }
            }
            catch
            {
            }
        }

        private static DateTime? ParseDate(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            DateTime when;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out when))
                return when;
            return null;
        }

        public ulong SecondsSince(Guid gameId, DateTime fromUtc)
        {
            return SecondsSince(gameId.ToString(), fromUtc);
        }

        public ulong SecondsSince(string key, DateTime fromUtc)
        {
            List<KeyValuePair<DateTime, ulong>> list;
            if (key == null || !perGame.TryGetValue(key, out list)) return 0;
            ulong sum = 0;
            foreach (var pair in list)
                if (pair.Key >= fromUtc) sum += pair.Value;
            return sum;
        }

        public ulong TotalSeconds(string key)
        {
            List<KeyValuePair<DateTime, ulong>> list;
            if (key == null || !perGame.TryGetValue(key, out list)) return 0;
            ulong sum = 0;
            foreach (var pair in list) sum += pair.Value;
            return sum;
        }
    }
}

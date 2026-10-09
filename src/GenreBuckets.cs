using System;
using System.Collections.Generic;
using System.Linq;

namespace GameVault
{
    /// <summary>
    /// 把 Playnite 库里五花八门的类型名（英文原文）归并成一套固定的、
    /// 覆盖市面上绝大多数游戏的分类，最多 <see cref="MaxBuckets"/> 个。
    ///
    /// 为什么要归并：
    /// Playnite 的类型来自各商店元数据，同一款游戏可能带
    /// "Action" / "Shooter" / "FPS" / "Arena" 这样一堆近义标签，
    /// 直接统计会得到几十个碎片类别（图上一圈细条），既不好看也不好读。
    /// 这里把近义词并进同一个桶，少数确实没有合适归处的才单列，
    /// 并且永远只保留占比最大的前 <see cref="MaxBuckets"/> 个，
    /// 其余全部并入「其他」——保证图上最多就这么多块。
    ///
    /// 归并表按「先匹配先生效」顺序排列，所以更具体的词要放在更前面
    /// （例如 "Battle Royale" 必须排在 "Action" 前面，否则会被吃掉）。
    /// </summary>
    public static class GenreBuckets
    {
        /// <summary>界面上最多显示多少个类别。</summary>
        public const int MaxBuckets = 15;

        /// <summary>「其他」这一桶的名字（中文）。</summary>
        public const string OtherLabel = "其他";

        /// <summary>
        /// 归并规则：key 是桶的名字，value 是能落进这个桶的关键词。
        /// 匹配用「包含」而不是「相等」，因为实际数据里常见
        /// "Action Adventure"、"Roguelike RPG" 这类组合写法。
        /// </summary>
        private static readonly KeyValuePair<string, string[]>[] Rules =
        {
            // ---- 射击类要放在 Action / RPG 前面，否则会被它们抢走 ----
            new KeyValuePair<string, string[]>("射击", new[]
                { "Shooter", "FPS", "First-Person Shooter", "Third-Person Shooter", "TPS",
                  "弹幕", "Shoot 'em up", "Shmup", "枪" }),

            new KeyValuePair<string, string[]>("角色扮演", new[]
                { "Role-playing", "Role Playing", "RPG", "JRPG", "CRPG", "Tactical RPG",
                  "角色扮演", "MMORPG", "MMO", "DRPG" }),

            new KeyValuePair<string, string[]>("动作", new[]
                { "Action", "动作", "Hack and Slash", "Hack'n'Slash", "Beat 'em up",
                  "Apartment", "Character Action" }),

            new KeyValuePair<string, string[]>("冒险", new[]
                { "Adventure", "冒险", "Point & Click", "Visual Novel", "冒险解谜",
                  "Interactive Fiction", "Choices", "Story" }),

            new KeyValuePair<string, string[]>("策略", new[]
                { "Strategy", "策略", "RTS", "Real-Time Strategy", "Turn-Based Strategy",
                  "TBS", "4X", "Tower Defense", "MOBA", "Roguelike", "Roguelite",
                  "Slay the Spire", "Deck Building", "Card Game", "Card & Board",
                  "Board Game", "桌游", " autobattler", "Auto Battler" }),

            new KeyValuePair<string, string[]>("模拟", new[]
                { "Simulation", "模拟", "Sim", "Life Sim", "Simulator", "Farming",
                  "Farm", "Simulation Sports", "Sports", "体育", "运动", "Racing",
                  "竞速", "赛车", "Driving", "Flight", "飞行", "Train", "火车" }),

            new KeyValuePair<string, string[]>("竞速", new[]
                { "Racing", "竞速", "赛车", "Driving" }),

            new KeyValuePair<string, string[]>("体育", new[]
                { "Sports", "体育", "运动", "Football", "Soccer", "Basketball",
                  "Baseball", "Tennis", "Golf", "格斗", "Fighting" }),

            new KeyValuePair<string, string[]>("格斗", new[]
                { "Fighting", "格斗", "Beat 'em up", "Duel" }),

            new KeyValuePair<string, string[]>("恐怖", new[]
                { "Horror", "恐怖", "Survival Horror", "惊悚" }),

            new KeyValuePair<string, string[]>("解谜", new[]
                { "Puzzle", "解谜", "Puzzle Platformer", "Match" }),

            new KeyValuePair<string, string[]>("平台", new[]
                { "Platformer", "平台", "Platform", "跳跃" }),

            new KeyValuePair<string, string[]>("大逃杀", new[]
                { "Battle Royale", "大逃杀", "吃鸡" }),

            new KeyValuePair<string, string[]>("开放世界", new[]
                { "Open World", "开放世界", "沙盒", "Sandbox", "生存", "Survival",
                  "Craft", "建造", "生存模拟" }),

            new KeyValuePair<string, string[]>("音乐", new[]
                { "Music", "音乐", "节奏", "Rhythm", "Dance", "音游" }),

            new KeyValuePair<string, string[]>("休闲", new[]
                { "Casual", "休闲", "Arcade", "街机", "Party", "派对",
                  "Trivia", "益智", "棋牌" }),

            new KeyValuePair<string, string[]>("教育", new[]
                { "Education", "教育", "学习", "Training" }),

            new KeyValuePair<string, string[]>("工具", new[]
                { "Utility", "工具", "Productivity" }),
        };

        /// <summary>
        /// 把一个原始类型名归到某个桶；归不进去返回 null（调用方决定要不要单列）。
        /// </summary>
        public static string Resolve(string rawGenre)
        {
            if (string.IsNullOrWhiteSpace(rawGenre)) return null;
            var text = rawGenre.Trim();
            if (text.Length == 0) return null;

            foreach (var rule in Rules)
                foreach (var keyword in rule.Value)
                    if (keyword.Length > 0 &&
                        text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        return rule.Key;

            return null;
        }

        /// <summary>
        /// 把一堆（类型名 → 游戏列表）归并成最多 <see cref="MaxBuckets"/> 个桶。
        /// 输入是 <see cref="VaultData.GenreIndex"/> 那种字典。
        ///
        /// 返回的字典按占比从大到小排好序；剩下的长尾全部并进「其他」。
        /// </summary>
        public static Dictionary<string, List<GameEntry>> Merge(
            Dictionary<string, List<GameEntry>> genreIndex, ulong totalSeconds, int totalCount)
        {
            var merged = new Dictionary<string, List<GameEntry>>();
            var unmatched = new List<GameEntry>();

            if (genreIndex == null) return merged;

            foreach (var pair in genreIndex)
            {
                var bucketName = Resolve(pair.Key);
                if (bucketName == null)
                {
                    // 没命中任何规则：先攒着，最后统一进「其他」
                    if (pair.Value != null) unmatched.AddRange(pair.Value);
                    continue;
                }

                List<GameEntry> bucket;
                if (!merged.TryGetValue(bucketName, out bucket))
                {
                    bucket = new List<GameEntry>();
                    merged[bucketName] = bucket;
                }
                if (pair.Value != null) bucket.AddRange(pair.Value);
            }

            // 长尾并入「其他」：一款游戏多个类型时可能被并进同一个桶，这里去重
            var others = unmatched
                .Concat(merged.Values.SelectMany(v => v))
                .Distinct()
                .ToList();

            // 按占比排序取前 N-1 个，剩下的全进「其他」
            var ranked = merged
                .Select(kv => new KeyValuePair<string, List<GameEntry>>(
                    kv.Key,
                    kv.Value.Distinct().ToList()))
                .OrderByDescending(kv => totalSeconds > 0
                    ? kv.Value.Aggregate(0UL, (acc, e) => acc + (ulong)Math.Max(0, e.TotalPlaytime))
                    : (ulong)kv.Value.Count)
                .ThenByDescending(kv => kv.Value.Count)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            var result = new Dictionary<string, List<GameEntry>>();
            var keep = ranked.Take(MaxBuckets - 1).ToList();
            foreach (var kv in keep) result[kv.Key] = kv.Value;

            // 「其他」= 没命中规则的 + 排名靠后的 + 已展示桶里的重复项
            var shown = new HashSet<Guid>(keep.SelectMany(kv => kv.Value).Select(e => e.PrimaryId));
            var rest = others.Where(e => !shown.Contains(e.PrimaryId)).ToList();
            rest.Sort((a, b) => b.TotalPlaytime.CompareTo(a.TotalPlaytime));
            if (rest.Count > 0) result[OtherLabel] = rest;

            return result;
        }

        /// <summary>保留给测试：规则表里是否出现了重复的桶名。</summary>
        public static IEnumerable<string> BucketNames
        {
            get { return Rules.Select(r => r.Key); }
        }
    }
}
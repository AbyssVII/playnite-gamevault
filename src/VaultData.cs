using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace GameVault
{
    public class VaultData
    {
        public List<GameEntry> Entries = new List<GameEntry>();

        public int RawGameCount;
        public int MergedCount;
        public int PlayedCount;
        public int InstalledCount;
        public ulong TotalPlaytime;
        public ulong RecentPlaytime;
        public double AverageScore;
        public int ScoredCount;
        public string TopGameName;
        public ulong TopGameTime;
        public int ActiveInPeriod;

        public bool ActivityAvailable;
        public string ActivityPath;

        static DateTime? SafeReleaseDate(Game game)
        {
            try
            {
                if (!game.ReleaseDate.HasValue) return null;
                DateTime parsed;
                if (DateTime.TryParse(game.ReleaseDate.Value.ToString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out parsed))
                    return parsed;
            }
            catch
            {
            }
            return null;
        }

        static string Join(IEnumerable<string> values, int max = 3)
        {
            var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().Take(max).ToList();
            return list.Count == 0
                ? L10n.T("LocUnknown")
                : string.Join(L10n.T("LocSeparator"), list);
        }

        static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var text = Regex.Replace(input, "<br\\s*/?>|</p>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", "");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, "\\n{2,}", "\n").Trim();
            return text.Length > 260 ? text.Substring(0, 260) + "…" : text;
        }

        public static VaultData Build(IPlayniteAPI api, int recentDays, bool includeHidden)
        {
            var result = new VaultData();
            var activity = ActivityStore.Load(api.Paths.ExtensionsDataPath);
            result.ActivityAvailable = activity.Available;
            result.ActivityPath = activity.SourcePath;

            var since = DateTime.UtcNow.AddDays(-recentDays);
            var groups = new Dictionary<string, List<Game>>();

            foreach (var game in api.Database.Games)
            {
                if (game == null) continue;
                if (game.Hidden && !includeHidden) continue;
                result.RawGameCount++;
                if (game.IsInstalled) result.InstalledCount++;
                if (game.Playtime > 0) result.PlayedCount++;

                var key = NameKey.Normalize(game.Name);
                if (string.IsNullOrEmpty(key)) key = "id:" + game.Id;
                List<Game> bucket;
                if (!groups.TryGetValue(key, out bucket))
                {
                    bucket = new List<Game>();
                    groups[key] = bucket;
                }
                bucket.Add(game);
            }

            foreach (var pair in groups)
            {
                var bucket = pair.Value;
                var primary = bucket.OrderByDescending(g => g.Playtime)
                                    .ThenByDescending(g => g.LastActivity ?? DateTime.MinValue)
                                    .First();

                var entry = new GameEntry
                {
                    PrimaryId = primary.Id,
                    Name = primary.Name,
                    TotalPlaytime = (ulong)bucket.Sum(g => (long)g.Playtime),
                    CoverPath = SafePath(api, primary.CoverImage),
                    PosterPath = SafePath(api, primary.BackgroundImage),
                    IconPath = SafePath(api, primary.Icon),
                    CriticScore = bucket.Select(g => g.CriticScore).FirstOrDefault(s => s.HasValue && s.Value > 0),
                    UserScore = bucket.Select(g => g.UserScore).FirstOrDefault(s => s.HasValue && s.Value > 0),
                    CommunityScore = bucket.Select(g => g.CommunityScore).FirstOrDefault(s => s.HasValue && s.Value > 0),
                    IsInstalled = bucket.Any(g => g.IsInstalled),
                    Favorite = bucket.Any(g => g.Favorite),
                    Copies = bucket.Count,
                    Description = StripHtml(primary.Description),
                };

                foreach (var game in bucket)
                {
                    entry.RecentPlaytime += activity.SecondsSince(game.Id, since);

                    var storeName = game.Source?.Name;
                    if (!string.IsNullOrEmpty(storeName) && !entry.Stores.Contains(storeName))
                        entry.Stores.Add(storeName);

                    if (game.Platforms != null)
                        foreach (var platform in game.Platforms)
                            if (platform != null && !string.IsNullOrEmpty(platform.Name) && !entry.Devices.Contains(platform.Name))
                                entry.Devices.Add(platform.Name);

                    var last = game.LastActivity ?? game.RecentActivity;
                    if (last.HasValue && (!entry.LastActivity.HasValue || last.Value > entry.LastActivity.Value))
                        entry.LastActivity = last;

                    var added = game.Added;
                    if (added.HasValue && (!entry.Added.HasValue || added.Value > entry.Added.Value))
                        entry.Added = added;
                }

                if (primary.Developers != null)
                    entry.Developers = Join(primary.Developers.Select(c => c?.Name));
                if (primary.Publishers != null)
                    entry.Publishers = Join(primary.Publishers.Select(c => c?.Name));
                if (primary.Genres != null)
                    entry.Genres = Join(primary.Genres.Select(c => c?.Name));
                entry.ReleaseDate = SafeReleaseDate(primary);

                entry.SearchBlob = (entry.Name + " " + entry.Developers + " " + entry.Publishers + " " +
                                    entry.StoreList + " " + entry.DeviceList).ToLowerInvariant();

                result.TotalPlaytime += entry.TotalPlaytime;
                result.RecentPlaytime += entry.RecentPlaytime;
                if (entry.RecentPlaytime > 0) result.ActiveInPeriod++;
                if (entry.HasScore)
                {
                    result.AverageScore += entry.CriticScore.Value;
                    result.ScoredCount++;
                }
                if (entry.TotalPlaytime > result.TopGameTime)
                {
                    result.TopGameTime = entry.TotalPlaytime;
                    result.TopGameName = entry.Name;
                }

                result.Entries.Add(entry);
            }

            result.MergedCount = result.Entries.Count;
            if (result.ScoredCount > 0) result.AverageScore = result.AverageScore / result.ScoredCount;
            return result;
        }

        static string SafePath(IPlayniteAPI api, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return null;
            try
            {
                return api.Database.GetFullFilePath(relative);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 全局缓存。插件加载时就在后台预热，用户点开视图时可直接拿现成结果渲染，
    /// 避免每次进入都重新遍历游戏库、解析数百个 GameActivity 会话文件。
    /// </summary>
    public static class VaultCache
    {
        public const int RecentDays = 14;
        public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

        private static readonly object Sync = new object();
        private static VaultData cached;
        private static DateTime stamp = DateTime.MinValue;
        private static Task<VaultData> inFlight;

        public static VaultData Get()
        {
            lock (Sync) return cached;
        }

        public static bool HasFresh
        {
            get
            {
                lock (Sync) return cached != null && DateTime.Now - stamp < MaxAge;
            }
        }

        public static Task<VaultData> Ensure(IPlayniteAPI api, bool force)
        {
            lock (Sync)
            {
                if (!force && cached != null && DateTime.Now - stamp < MaxAge)
                    return Task.FromResult(cached);
                if (inFlight != null)
                    return inFlight;
                inFlight = Task.Run(() =>
                {
                    try
                    {
                        var data = VaultData.Build(api, RecentDays, false);
                        lock (Sync)
                        {
                            // 数据库尚未就绪时可能统计到 0 款游戏，这种结果一律不写缓存，
                            // 否则会把"空库存"锁定住，用户就再也看不到游戏了。
                            if (data.RawGameCount > 0)
                            {
                                cached = data;
                                stamp = DateTime.Now;
                            }
                        }
                        return data;
                    }
                    finally
                    {
                        lock (Sync) inFlight = null;
                    }
                });
                return inFlight;
            }
        }
    }
}

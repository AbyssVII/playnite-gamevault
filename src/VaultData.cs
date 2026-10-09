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

        // ---- 分析页用的聚合结果 ----
        /// <summary>按总时长排序的类型分布（饼图数据）</summary>
        public List<GenreSlice> GenreByTime = new List<GenreSlice>();

        /// <summary>按款数排序的类型分布（饼图的另一种口径）</summary>
        public List<GenreSlice> GenreByCount = new List<GenreSlice>();

        /// <summary>总时长 Top 50（已按降序排好）</summary>
        public List<GameEntry> TopByTime = new List<GameEntry>();

/// <summary>
        /// 按库（Steam / Epic / Xbox …）聚合的游戏总时长，已按时长降序。
        /// 「总游戏时长」浮窗用它逐库列出，来源未设置的游戏归到 Playnite 本体。
        /// </summary>
        public List<SourcePlayRow> SourcePlaytimes = new List<SourcePlayRow>();

        /// <summary>
        /// 按库统计的游戏款数及占库存的百分比，已按款数降序。
        /// 「库存总数」浮窗用它逐库列出。合并平台副本后按主条目的来源计数，
        /// 所以各库之和正好等于库存总数。
        /// </summary>
        public List<SourceCountRow> SourceGameCounts = new List<SourceCountRow>();

        /// <summary>供报告生成器使用的原始事实</summary>
        public ProfileFacts Facts = new ProfileFacts();

        /// <summary>按年的游玩时间轴（数据来自 GameActivity 逐局记录），默认展示最新的一年</summary>
        public TimelineData Timeline = new TimelineData();

        /// <summary>时间轴可选年份，倒序（最新年在前）</summary>
        public List<int> TimelineYears = new List<int>();

        /// <summary>
        /// 这一次统计用到的 GameActivity 逐局数据。
        /// 界面切换时间轴年份时直接复用它重算，不必再读一次磁盘。
        /// 不参与序列化（缓存落盘时忽略）。
        /// </summary>
        [System.NonSerialized]
        public ActivityStore Activity;

        /// <summary>类型名 -> 条目，用于统计时避免把"动作/冒险"这类多标签重复计数</summary>
        [System.NonSerialized]
        public Dictionary<string, List<GameEntry>> GenreIndex = new Dictionary<string, List<GameEntry>>();

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

        /// <summary>
        /// 归一化库来源名：Steam Family Sharing 的时长 / 库存都并入 Steam，
        /// 没设来源的归到 Playnite 本体。
        /// </summary>
        static string NormalizeSourceName(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "Playnite";
            var name = source.Trim();
            if (name.StartsWith("Steam", StringComparison.OrdinalIgnoreCase) &&
                name.IndexOf("family sharing", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Steam";
            return name;
        }

        public static VaultData Build(IPlayniteAPI api, int recentDays, bool includeHidden)
        {
            var result = new VaultData();
            var activity = ActivityStore.Load(api.Paths.ExtensionsDataPath);
            result.Activity = activity;
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
                    SourceName = primary.Source != null ? primary.Source.Name : null,
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
                    entry.ActivityKeys.Add(game.Id.ToString());

                    var storeName = game.Source?.Name;
                    if (!string.IsNullOrEmpty(storeName) && !entry.Stores.Contains(storeName))
                        entry.Stores.Add(storeName);

                    // Steam 来源的游戏：GameId 就是 AppID；另外再从 Links 里找一遍
                    // store.steampowered.com/app/<id> 兜底（有些条目靠元数据插件补的链接）。
                    // 拿到 AppID 后查价格是精确查询，比按名字搜索准得多。
                    if (entry.SteamAppId <= 0)
                    {
                        var direct = ExtractSteamAppId(storeName, game.GameId);
                        if (direct > 0) entry.SteamAppId = direct;
                        else entry.SteamAppId = ExtractSteamAppIdFromLinks(game.Links);
                    }

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

            // 按库聚合总时长：浮窗里要逐库列出「Steam 多少、Epic 多少」。
            // 游戏没设来源时归到 Playnite 本体，避免那些时长凭空消失；
            // Steam Family Sharing 也是 Steam 的一份时长，并入 Steam。
            var playBySource = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in api.Database.Games)
            {
                if (game == null || game.Playtime <= 0) continue;
                if (game.Hidden && !includeHidden) continue;

                var source = NormalizeSourceName(game.Source != null ? game.Source.Name : null);
                ulong existing;
                playBySource.TryGetValue(source, out existing);
                playBySource[source] = existing + (ulong)game.Playtime;
            }
            result.SourcePlaytimes = playBySource
                .Where(kv => kv.Value > 0)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new SourcePlayRow
                {
                    Name = kv.Key,
                    Seconds = kv.Value,
                    Text = TimeFmt.Short(kv.Value)
                })
                .ToList();

            // 按库统计游戏款数：合并平台副本后，一条记录算一款，归到主条目的来源。
            // 这样各库之和正好等于库存总数，百分比加总也是 100%。
            var countBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in result.Entries)
            {
                var source = NormalizeSourceName(entry.SourceName);
                countBySource[source] = countBySource.ContainsKey(source) ? countBySource[source] + 1 : 1;
            }
            result.SourceGameCounts = countBySource
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new SourceCountRow
                {
                    Name = kv.Key,
                    Count = kv.Value,
                    ShareText = result.MergedCount > 0
                        ? Math.Round(kv.Value * 100.0 / result.MergedCount) + "%"
                        : "0%"
                })
                .ToList();

            BuildAnalytics(result, api, activity);
            return result;
        }

        /// <summary>
        /// 生成分析页所需的三块数据：类型分布（饼图）、时长 Top 50、玩家画像事实。
        /// 刻意在 Build 的最后一次性算完 —— 这些结果会被缓存，切页面/换语言时无需重算。
        /// </summary>
        private static void BuildAnalytics(VaultData result, IPlayniteAPI api, ActivityStore activity)
        {
            // ---- 1. 类型分布 ----
            // 一款游戏可能同时属于"动作"和"冒险"两个类型，所以按类型统计的款数之和
            // 会大于库存总数。这是行业惯例（Steam 的标签统计同理），不额外做归一化，
            // 但要在界面上说明，免得用户以为算错了。
            var genreEntries = new Dictionary<string, List<GameEntry>>();
            foreach (var entry in result.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Genres)) continue;
                foreach (var raw in entry.Genres.Split(new[] { '、', ',', '，', '/', '|' },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = raw.Trim();
                    if (name.Length == 0 || name == L10n.T("LocUnknown")) continue;
                    List<GameEntry> bucket;
                    if (!genreEntries.TryGetValue(name, out bucket))
                    {
                        bucket = new List<GameEntry>();
                        genreEntries[name] = bucket;
                    }
                    bucket.Add(entry);
                }
            }

            // 原始类型名（英文碎片）先归并成最多 15 个大类别，再切片统计。
            // 不归并的话图上会是一圈几十根细条，什么也看不出来。
            var mergedGenres = GenreBuckets.Merge(genreEntries, result.TotalPlaytime, result.MergedCount);

            // GenreIndex 保留原始数据（Top50 / 详情等可能还要用），图上用的两个切片走归并后的结果。
            result.GenreIndex = genreEntries;
            result.GenreByTime = BuildSlices(mergedGenres, true, result.TotalPlaytime);
            result.GenreByCount = BuildSlices(mergedGenres, false, (ulong)result.MergedCount);

            // ---- 2. 时长 Top 50 ----
            result.TopByTime = result.Entries
                .OrderByDescending(e => e.TotalPlaytime)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(50)
                .ToList();

            // ---- 3. 玩家画像事实 ----
            result.Facts = BuildFacts(result);

            // ---- 4. 游玩时间轴（按年）----
            result.TimelineYears = BuildTimelineYears(activity);
            result.Timeline = BuildTimeline(result, activity, result.TimelineYears.Count > 0
                ? result.TimelineYears[0]
                : DateTime.Today.Year);
        }

        /// <summary>
        /// 时间轴可以选哪些年。倒序（最新年在前），界面上作为下拉列表。
        /// 取 GameActivity 里所有会话覆盖到的年份 —— 没有会话的年份没必要让用户翻。
        /// </summary>
        public static List<int> BuildTimelineYears(ActivityStore activity)
        {
            var years = new List<int>();
            if (activity == null || !activity.Available) return years;

            var set = new HashSet<int>();
            foreach (var session in activity.AllSessions())
            {
                if (session.Seconds == 0) continue;
                set.Add(session.When.Year);
            }
            // 当年一定给一个入口，否则"今年刚开始还没玩"时用户看不到自己的年
            set.Add(DateTime.Today.Year);
            years.AddRange(set);
            years.Sort();
            years.Reverse();
            return years;
        }

        /// <summary>
        /// 生成「按年」的游玩时间轴：把 <paramref name="year"/> 整年的每一周都排出来
        /// （1 月 1 日所在周的周一 → 12 月 31 日所在周的周一），没有记录的周保留空位，
        /// 这样"这一年哪几周在玩"一眼可见。
        ///
        /// 数据来源是 GameActivity 的逐局记录 —— Playnite 本体只保存总时长与
        /// 最后游玩时间，没有"这一周玩了什么、玩了多久"这种粒度。没有装 GameActivity
        /// 时返回一个 Available=false 的空结构，界面上显示指引文案而不是空白。
        /// </summary>
        public static TimelineData BuildTimeline(VaultData data, ActivityStore activity, int year)
        {
            var timeline = new TimelineData();
            timeline.Year = year;
            if (data == null) return timeline;
            if (activity == null || !activity.Available)
            {
                timeline.Available = false;
                return timeline;
            }
            timeline.Available = true;

            // 把 GameActivity 的 key（Game.Id）映射回合并后的条目：
            // 同一款游戏在多平台有多个副本，时间轴要把它们算成一款。
            var keyToEntry = new Dictionary<string, GameEntry>();
            foreach (var entry in data.Entries)
                foreach (var key in entry.ActivityKeys)
                    keyToEntry[key] = entry;

            // 该年第一周的周一 … 最后一周的周一
            var firstMonday = MondayOf(new DateTime(year, 1, 1));
            var lastMonday = MondayOf(new DateTime(year, 12, 31));

            var today = DateTime.Today;
            var thisMonday = MondayOf(today);

            // 预建所有周桶，保证时间轴是连续的（没有记录的周也要留位置，否则节奏失真）
            var buckets = new Dictionary<DateTime, WeekBucket>();
            for (var start = firstMonday; start <= lastMonday; start = start.AddDays(7))
            {
                buckets[start] = new WeekBucket
                {
                    WeekStart = start,
                    WeekOfYear = IsoWeek(start),
                    IsCurrent = start == thisMonday
                };
            }

            // 逐局数据按周归并到"游戏"级别
            foreach (var session in activity.AllSessions())
            {
                if (session.Seconds == 0) continue;
                if (session.When.Year != year) continue;
                var start = MondayOf(session.When);
                WeekBucket bucket;
                if (!buckets.TryGetValue(start, out bucket)) continue;   // 落在窗口外

                GameEntry entry;
                if (!keyToEntry.TryGetValue(session.GameKey, out entry)) continue;

                bucket.TotalSeconds += session.Seconds;
                ulong accum;
                if (!bucket.accum.TryGetValue(entry, out accum)) accum = 0;
                bucket.accum[entry] = accum + session.Seconds;
            }

            // 定型：算每周冠军、柱高、月份分组
            foreach (var bucket in buckets.Values)
            {
                bucket.GameCount = bucket.accum.Count;
                if (bucket.accum.Count > 0)
                {
                    var best = bucket.accum.OrderByDescending(p => p.Value).First();
                    bucket.Top = new WeekHighlight
                    {
                        Name = best.Key.Name,
                        Seconds = best.Value,
                        Entry = best.Key
                    };
                }
                if (bucket.TotalSeconds > timeline.PeakSeconds) timeline.PeakSeconds = bucket.TotalSeconds;
                timeline.TotalSeconds += bucket.TotalSeconds;
                if (bucket.TotalSeconds > 0) timeline.ActiveWeeks++;
            }

            var ordered = buckets.Values.OrderBy(b => b.WeekStart).ToList();
            timeline.Weeks = ordered;

            // 月份分组 + 展示字段
            MonthGroup current = null;
            var previousMonth = -1;
            foreach (var bucket in ordered)
            {
                bucket.WeekLabel = bucket.WeekStart.Month + "/" + bucket.WeekStart.Day;
                // 整周区间（周一 ~ 周日），悬停卡里要显示"这是哪一周"而不只是一个起始日
                var weekEnd = bucket.WeekStart.AddDays(6);
                bucket.WeekRangeLabel = bucket.WeekStart.Month + "/" + bucket.WeekStart.Day
                                        + "-" + weekEnd.Month + "/" + weekEnd.Day;
                bucket.TotalText = TimeFmt.Short(bucket.TotalSeconds);
                bucket.IsEmpty = bucket.TotalSeconds == 0;
                if (bucket.Top != null)
                {
                    bucket.TopNameText = bucket.Top.Name;
                    bucket.TopTimeText = TimeFmt.Short(bucket.Top.Seconds);
                }

                // 月份分组：按**该周的周一**归属。1 月 1 日所在周可能跨到上一年 12 月，
                // 这种"跨年周"统一算进所选年份的 1 月 —— 分组键和显示文案都要按 1 月来，
                // 否则会出现"2025 年 12 月"这种跨年标签，跟"选 2026 年"的语境打架。
                var crossesYear = bucket.WeekStart.Year < year;
                var monthNo = crossesYear ? 1 : bucket.WeekStart.Month;
                var monthYear = crossesYear ? year : bucket.WeekStart.Year;
                if (monthNo != previousMonth)
                {
                    previousMonth = monthNo;
                    bucket.MonthLabel = L10n.F("LocTimelineMonthLabel", monthYear, monthNo);
                    current = new MonthGroup
                    {
                        Label = L10n.F("LocTimelineMonth", monthYear, monthNo),
                        ShortLabel = L10n.F("LocTimelineMonthLabel", monthYear, monthNo),
                        StartIndex = timeline.Weeks.IndexOf(bucket),
                        WeekCount = 0
                    };
                    timeline.Months.Add(current);
                }
                if (current != null)
                {
                    current.WeekCount++;
                    current.TotalSeconds += bucket.TotalSeconds;
                }
            }
            foreach (var month in timeline.Months)
                month.TotalText = TimeFmt.Short(month.TotalSeconds);

            // 柱高：相对峰值换算成像素（最小 3px，让"几乎没玩"的周也能看见）
            const double maxBar = 118.0;
            foreach (var bucket in ordered)
                bucket.BarHeight = timeline.PeakSeconds > 0
                    ? Math.Max(3, maxBar * bucket.TotalSeconds / timeline.PeakSeconds)
                    : 3;

            timeline.TotalText = TimeFmt.Short(timeline.TotalSeconds);
            timeline.SpanText = L10n.F("LocTimelineSpan", year, timeline.ActiveWeeks);
            return timeline;
        }

        /// <summary>某天所在周的周一（本地日期，归零到 00:00）。周一=0 … 周日=6。</summary>
        private static DateTime MondayOf(DateTime date)
        {
            return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        }

        /// <summary>
        /// 「游戏周报」：最近一年（默认 53 周）的逐周聚合。
        ///
        /// 和 <see cref="BuildTimeline"/> 的区别只有两点：
        ///   ① 窗口是「最近 53 周」而不是「某一个自然年」，所以不需要年份下拉；
        ///   ② 不画柱子、不分月份，每一周只关心"当周玩得最多的那款游戏"，
        ///      展示形式是海报缩略图（见 WeeklyReportChart）。
        ///
        /// 聚合口径与时间轴完全一致（周一为一周之始、时长含跨平台副本合并），
        /// 这样两个控件上的数字能对得上，不会出现"同一周时长不一样"的困惑。
        /// </summary>
        public static TimelineData BuildWeekly(VaultData data, ActivityStore activity, int weeks = 53)
        {
            var weekly = new TimelineData();
            if (data == null || activity == null || !activity.Available)
            {
                weekly.Available = false;
                return weekly;
            }
            weekly.Available = true;

            var keyToEntry = new Dictionary<string, GameEntry>();
            if (data.Entries != null)
                foreach (var entry in data.Entries)
                    if (entry.ActivityKeys != null)
                        foreach (var key in entry.ActivityKeys)
                            keyToEntry[key] = entry;

            var thisMonday = MondayOf(DateTime.Today);

            // 预建 53 个周桶，保证时间轴连续（没记录的周也要留位置，否则节奏失真）
            var buckets = new Dictionary<DateTime, WeekBucket>();
            for (var i = weeks - 1; i >= 0; i--)
            {
                var start = thisMonday.AddDays(-7 * i);
                buckets[start] = new WeekBucket
                {
                    WeekStart = start,
                    WeekOfYear = IsoWeek(start),
                    IsCurrent = i == 0
                };
            }

            foreach (var session in activity.AllSessions())
            {
                if (session.Seconds == 0) continue;
                var start = MondayOf(session.When);
                WeekBucket bucket;
                if (!buckets.TryGetValue(start, out bucket)) continue;   // 落在一年窗口外

                GameEntry entry;
                if (!keyToEntry.TryGetValue(session.GameKey, out entry)) continue;

                bucket.TotalSeconds += session.Seconds;
                ulong accum;
                if (!bucket.accum.TryGetValue(entry, out accum)) accum = 0;
                bucket.accum[entry] = accum + session.Seconds;
            }

            foreach (var bucket in buckets.Values)
            {
                bucket.GameCount = bucket.accum.Count;
                if (bucket.accum.Count > 0)
                {
                    var best = bucket.accum.OrderByDescending(p => p.Value).First();
                    bucket.Top = new WeekHighlight
                    {
                        Name = best.Key.Name,
                        Seconds = best.Value,
                        Entry = best.Key
                    };
                }
                if (bucket.TotalSeconds > weekly.PeakSeconds) weekly.PeakSeconds = bucket.TotalSeconds;
                weekly.TotalSeconds += bucket.TotalSeconds;
                if (bucket.TotalSeconds > 0) weekly.ActiveWeeks++;
            }

            weekly.Weeks = buckets.Values.OrderBy(b => b.WeekStart).ToList();

            // 展示字段
            foreach (var bucket in weekly.Weeks)
            {
                var weekEnd = bucket.WeekStart.AddDays(6);
                // 周报要的就是这个区间标签（"3.2-3.8"），不是时间轴那种 "3/2-3/8"
                bucket.WeekRangeLabel = bucket.WeekStart.Month + "." + bucket.WeekStart.Day
                                        + "-" + weekEnd.Month + "." + weekEnd.Day;
                bucket.WeekLabel = bucket.WeekStart.Month + "/" + bucket.WeekStart.Day;
                bucket.TotalText = TimeFmt.Short(bucket.TotalSeconds);
                bucket.IsEmpty = bucket.TotalSeconds == 0;
                if (bucket.Top != null)
                {
                    bucket.TopNameText = bucket.Top.Name;
                    bucket.TopTimeText = TimeFmt.Short(bucket.Top.Seconds);
                    // 冠军占当周的比例；分母为 0 的极端情况（只有 0 秒会话）直接不给
                    bucket.TopShareText = bucket.TotalSeconds > 0
                        ? Math.Round(100.0 * bucket.Top.Seconds / bucket.TotalSeconds) + "%"
                        : "";
                }
            }

            weekly.TotalText = TimeFmt.Short(weekly.TotalSeconds);
            return weekly;
        }
        private static int IsoWeek(DateTime date)
        {
            // net48 没有 System.Globalization.ISOWeek（那是 .NET Core 3.0+ 才有的），
            // 这里用 Calendar.GetWeekOfYear + ISO 规则手工对齐。
            var cal = CultureInfo.InvariantCulture.Calendar;
            var week = cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            // 跨年边界：12 月末尾若落到第 1 周，说明属于下一年的第 1 周
            if (week >= 52 && date.Month == 1) week = 1;
            return week;
        }

        /// <summary>
        /// 把「类型 -> 条目列表」变成饼图用的切片。
        /// byTime=true 时按总时长占比，否则按款数占比。
        ///
        /// 不做「只留前 N 个、其余合并成其他」的截断 —— 用户明确要求把所有类型都
        /// 分到各自的类别里，所以这里**保留全部类型**。类型多时靠加长配色表 + 图例滚动条承载。
        ///
        /// 公开是为了让离线段测试（vaulttest）能直接喂数据验证"不合并/不丢类型"。
        /// </summary>
        public static List<GenreSlice> BuildSlices(
            Dictionary<string, List<GameEntry>> index, bool byTime, ulong total)
        {
            var all = new List<GenreSlice>();
            foreach (var pair in index)
            {
                var slice = new GenreSlice { Name = pair.Key, GameCount = pair.Value.Count };
                foreach (var g in pair.Value) slice.Playtime += g.TotalPlaytime;
                all.Add(slice);
            }

            var ordered = (byTime
                    ? all.OrderByDescending(s => s.Playtime).ThenByDescending(s => s.GameCount)
                    : all.OrderByDescending(s => s.GameCount).ThenByDescending(s => s.Playtime))
                .ToList();

            // 款数口径的分母是"各类型款数之和"（一款可能属于多个类型），不是库存总数
            var countDenominator = (ulong)Math.Max(1, index.Sum(p => p.Value.Count));
            var palette = GenrePalette.Colors;
            for (var i = 0; i < ordered.Count; i++)
            {
                var slice = ordered[i];
                var basis = byTime ? slice.Playtime : (ulong)slice.GameCount;
                var denominator = byTime ? total : countDenominator;
                slice.Share = denominator > 0 ? Math.Min(1.0, (double)basis / denominator) : 0;
                slice.Color = palette[i % palette.Length];
                slice.CountText = L10n.F("LocGenreCount", slice.GameCount);
                slice.PlaytimeText = TimeFmt.Short(slice.Playtime);
                slice.ShareText = (slice.Share * 100).ToString("0.#") + "%";
            }
            return ordered;
        }

        private static ProfileFacts BuildFacts(VaultData data)
        {
            var facts = new ProfileFacts
            {
                TotalGames = data.MergedCount,
                InstalledGames = data.InstalledCount,
                TotalPlaytime = data.TotalPlaytime,
                RecentPlaytime = data.RecentPlaytime,
                ActiveInPeriod = data.ActiveInPeriod,
                ScoredCount = data.ScoredCount,
                AverageScore = data.AverageScore,
                TopGameNames = new List<string>(),
                TopGames = new List<GameEntry>()
            };

            var playedTimes = new List<ulong>();
            var playedYears = new HashSet<int>();
            var addedDates = new List<DateTime>();
            DateTime? lastPlay = null;
            ulong playedTotal = 0;

            foreach (var e in data.Entries)
            {
                if (e.TotalPlaytime > 0)
                {
                    facts.PlayedGames++;
                    playedTimes.Add(e.TotalPlaytime);
                    playedTotal += e.TotalPlaytime;
                }
                else
                {
                    facts.UnplayedGames++;
                    if (e.IsInstalled) facts.InstalledUnplayed++;
                }

                if (e.Favorite) facts.FavoriteCount++;

                var hours = e.TotalPlaytime / 3600.0;
                if (e.TotalPlaytime == 0) facts.TierNone++;
                else if (hours < 5) { facts.TierLight++; facts.ShortGameCount++; }
                else if (hours < 30) { facts.TierMedium++; facts.ShortGameCount++; }
                else if (hours < 100) facts.TierHeavy++;
                else { facts.TierDeep++; facts.CompletedishCount++; }

                if (e.HasScore)
                {
                    if (e.CriticScore.Value >= 85) facts.HighScoreCount++;
                    else if (e.CriticScore.Value < 70) facts.LowScoreCount++;

                    // MC 分数分布（与 GameEntry.ScoreTier 完全同口径，避免两处判据漂移）
                    switch (e.ScoreTier)
                    {
                        case 5: facts.ScoreBucket90++; break;
                        case 4: facts.ScoreBucket80++; break;
                        case 3: facts.ScoreBucket70++; break;
                        case 2: facts.ScoreBucket60++; break;
                        case 1: facts.ScoreBucketLow++; break;
                    }
                }

                if (e.LastActivity.HasValue)
                {
                    if (!lastPlay.HasValue || e.LastActivity.Value > lastPlay.Value)
                        lastPlay = e.LastActivity.Value;
                    playedYears.Add(e.LastActivity.Value.Year);
                }

                if (e.Added.HasValue) addedDates.Add(e.Added.Value);

                if (!e.LastActivity.HasValue && e.Added.HasValue)
                {
                    var age = (int)(DateTime.Now - e.Added.Value).TotalDays;
                    if (age > facts.OldestUnplayedAgeDays) facts.OldestUnplayedAgeDays = age;
                }

                if (e.ReleaseDate.HasValue)
                {
                    var year = e.ReleaseDate.Value.Year;
                    if (year > 1970)
                    {
                        if (facts.OldestReleaseYear == 0 || year < facts.OldestReleaseYear)
                            facts.OldestReleaseYear = year;
                        if (year > facts.NewestReleaseYear) facts.NewestReleaseYear = year;
                        if (DateTime.Now.Year - year <= 5) facts.ModernCount++;
                    }
                }
            }

            // ---- 时间 / 习惯（报告扩充）----
            if (playedTimes.Count > 0)
            {
                playedTimes.Sort();
                var mid = playedTimes.Count / 2;
                facts.MedianPlayedTime = playedTimes.Count % 2 == 1
                    ? playedTimes[mid]
                    : (playedTimes[mid - 1] + playedTimes[mid]) / 2;
                facts.AvgPlayedTime = playedTotal / (ulong)playedTimes.Count;
            }
            if (lastPlay.HasValue)
                facts.DaysSinceLastPlay = Math.Max(0, (int)(DateTime.Now - lastPlay.Value).TotalDays);
            if (addedDates.Count > 0)
            {
                var oldest = addedDates.Min();
                facts.LibraryAgeDays = Math.Max(0, (int)(DateTime.Now - oldest).TotalDays);
                var cutoff = DateTime.Now.AddDays(-730);
                foreach (var d in addedDates) if (d >= cutoff) facts.RecentlyAdded++;
            }
            facts.DistinctPlayedYears = playedYears.Count;

            facts.BacklogAgeDays = facts.OldestUnplayedAgeDays;
            facts.InstallRate = data.MergedCount > 0
                ? (double)data.InstalledCount / data.MergedCount : 0;

            // 时长集中度：Top1 / Top3 / Top10
            var byTime = data.Entries.OrderByDescending(e => e.TotalPlaytime).ToList();
            if (byTime.Count > 0)
            {
                facts.Top1Time = byTime[0].TotalPlaytime;
                facts.Top1Name = byTime[0].Name;
                facts.Top3Time = byTime.Take(3).Aggregate(0UL, (acc, e) => acc + e.TotalPlaytime);
                facts.Top10Time = byTime.Take(10).Aggregate(0UL, (acc, e) => acc + e.TotalPlaytime);
                facts.TopGameNames = byTime.Take(10).Select(e => e.Name).ToList();
                facts.TopGames = byTime.Take(50).ToList();
            }
            if (data.TotalPlaytime > 0)
            {
                facts.Top1Share = (double)facts.Top1Time / data.TotalPlaytime;
                facts.Top10Share = (double)facts.Top10Time / data.TotalPlaytime;
                facts.RecentShare = (double)data.RecentPlaytime / data.TotalPlaytime;
            }

            // 类型
            facts.GenreCount = data.GenreByTime.Count;
            if (data.GenreByTime.Count > 0)
            {
                facts.TopGenreName = data.GenreByTime[0].Name;
                facts.TopGenreShare = data.GenreByTime[0].Share;
                if (data.GenreByTime.Count > 1) facts.SecondGenreName = data.GenreByTime[1].Name;
            }

            // 商店
            var storeCounts = new Dictionary<string, int>();
            foreach (var e in data.Entries)
                foreach (var s in e.Stores)
                {
                    var label = StoreVisual.DisplayName(s) ?? s;
                    int n;
                    storeCounts.TryGetValue(label, out n);
                    storeCounts[label] = n + 1;
                }
            if (storeCounts.Count > 0)
            {
                var top = storeCounts.OrderByDescending(p => p.Value).First();
                facts.TopStoreName = top.Key;
                facts.TopStoreCount = top.Value;
            }

            return facts;
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

        // ==================================================================
        // Steam AppID 提取（库存价值用）
        // ==================================================================

        /// <summary>
        /// 从「来源名 + GameId」里提取 Steam AppID。
        /// Playnite 的 Steam 库插件把 GameId 直接存成 AppID（纯数字），
        /// 所以来源是 Steam 且 GameId 是纯数字时即可采用。
        /// </summary>
        public static long ExtractSteamAppId(string sourceName, string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return 0;
            if (!IsSteamSource(sourceName)) return 0;
            return ParseLong(gameId);
        }

        /// <summary>从 Links 里找 store.steampowered.com/app/&lt;id&gt; 形式的链接。</summary>
        public static long ExtractSteamAppIdFromLinks(IEnumerable<Playnite.SDK.Models.Link> links)
        {
            if (links == null) return 0;
            foreach (var link in links)
            {
                var id = ExtractSteamAppIdFromUrl(link == null ? null : link.Url);
                if (id > 0) return id;
            }
            return 0;
        }

        /// <summary>
        /// 从任意 URL 里抠出 Steam AppID。
        /// 支持 store.steampowered.com/app/620/... 与 steam://run/620 两种形态。
        /// </summary>
        public static long ExtractSteamAppIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return 0;
            var lower = url.ToLowerInvariant();

            // store.steampowered.com/app/<数字>
            var marker = "steampowered.com/app/";
            var at = lower.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0)
                return ParseLong(url.Substring(at + marker.Length));

            // steam://run/<数字>
            var run = "steam://run/";
            var atRun = lower.IndexOf(run, StringComparison.Ordinal);
            if (atRun >= 0)
                return ParseLong(url.Substring(atRun + run.Length));

            return 0;
        }

        /// <summary>来源名是否指 Steam（Playnite 里叫 "Steam"，但也容忍 "steam"）。</summary>
        public static bool IsSteamSource(string sourceName)
        {
            return !string.IsNullOrEmpty(sourceName) &&
                   sourceName.Trim().Equals("Steam", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>取字符串开头连续的数字，转成 long；不是数字开头则返回 0。</summary>
        static long ParseLong(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var i = 0;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            if (i == 0) return 0;
            long value;
            return long.TryParse(text.Substring(0, i), out value) ? value : 0;
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

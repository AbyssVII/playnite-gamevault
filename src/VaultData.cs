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

        /// <summary>供报告生成器使用的原始事实</summary>
        public ProfileFacts Facts = new ProfileFacts();

        /// <summary>按周的游玩时间轴（数据来自 GameActivity 逐局记录）</summary>
        public TimelineData Timeline = new TimelineData();

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
                    entry.ActivityKeys.Add(game.Id.ToString());

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

            result.GenreIndex = genreEntries;
            result.GenreByTime = BuildSlices(genreEntries, true, result.TotalPlaytime);
            result.GenreByCount = BuildSlices(genreEntries, false, (ulong)result.MergedCount);

            // ---- 2. 时长 Top 50 ----
            result.TopByTime = result.Entries
                .OrderByDescending(e => e.TotalPlaytime)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(50)
                .ToList();

            // ---- 3. 玩家画像事实 ----
            result.Facts = BuildFacts(result);

            // ---- 4. 游玩时间轴（按周）----
            result.Timeline = BuildTimeline(result, activity, weeks: 26);
        }

        /// <summary>
        /// 生成「按周」的游玩时间轴：以本周为右端，往回收 <paramref name="weeks"/> 周。
        ///
        /// 数据来源是 GameActivity 的逐局记录 —— Playnite 本体只保存总时长与
        /// 最后游玩时间，没有"这一周玩了什么、玩了多久"这种粒度。没有装 GameActivity
        /// 时返回一个 Available=false 的空结构，界面上显示指引文案而不是空白。
        /// </summary>
        public static TimelineData BuildTimeline(VaultData data, ActivityStore activity, int weeks)
        {
            var timeline = new TimelineData();
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

            // 本周周一（本地时间，归零到 00:00）
            var today = DateTime.Today;
            var offset = ((int)today.DayOfWeek + 6) % 7;   // 周一=0 … 周日=6
            var thisMonday = today.AddDays(-offset);
            var firstMonday = thisMonday.AddDays(-7 * (weeks - 1));

            // 预建所有周桶，保证时间轴是连续的（没有记录的周也要留位置，否则节奏失真）
            var buckets = new Dictionary<DateTime, WeekBucket>();
            for (var i = 0; i < weeks; i++)
            {
                var start = firstMonday.AddDays(7 * i);
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
                var start = session.When.Date.AddDays(-(((int)session.When.DayOfWeek + 6) % 7));
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
                bucket.TotalText = TimeFmt.Short(bucket.TotalSeconds);
                bucket.IsEmpty = bucket.TotalSeconds == 0;
                if (bucket.Top != null)
                {
                    bucket.TopNameText = bucket.Top.Name;
                    bucket.TopTimeText = TimeFmt.Short(bucket.Top.Seconds);
                }

                if (bucket.WeekStart.Month != previousMonth)
                {
                    previousMonth = bucket.WeekStart.Month;
                    bucket.MonthLabel = L10n.F("LocTimelineMonthLabel",
                        bucket.WeekStart.Year, bucket.WeekStart.Month);
                    current = new MonthGroup
                    {
                        Label = L10n.F("LocTimelineMonth", bucket.WeekStart.Year, bucket.WeekStart.Month),
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
            timeline.SpanText = L10n.F("LocTimelineSpan", weeks, timeline.ActiveWeeks);
            return timeline;
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
        /// 只保留前 9 个，其余合并成「其他」，避免饼图颜色过多、图例太长。
        /// </summary>
        private static List<GenreSlice> BuildSlices(
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

            const int maxSlices = 9;
            List<GenreSlice> final;
            if (ordered.Count > maxSlices)
            {
                final = ordered.Take(maxSlices - 1).ToList();
                var rest = ordered.Skip(maxSlices - 1).ToList();
                var other = new GenreSlice
                {
                    Name = L10n.T("LocOtherGenres"),
                    GameCount = rest.Sum(s => s.GameCount),
                    Playtime = rest.Aggregate(0UL, (acc, s) => acc + s.Playtime),
                    IsOther = true
                };
                final.Add(other);
            }
            else
            {
                final = ordered;
            }

            var palette = GenrePalette.Colors;
            for (var i = 0; i < final.Count; i++)
            {
                var slice = final[i];
                var basis = byTime ? slice.Playtime : (ulong)slice.GameCount;
                var denominator = byTime ? total : (ulong)Math.Max(1, index.Sum(p => p.Value.Count));
                slice.Share = denominator > 0 ? Math.Min(1.0, (double)basis / denominator) : 0;
                // "其他" 固定用灰色，其余按序取色
                slice.Color = slice.IsOther ? GenrePalette.Other : palette[i % palette.Length];
                slice.CountText = L10n.F("LocGenreCount", slice.GameCount);
                slice.PlaytimeText = TimeFmt.Short(slice.Playtime);
                slice.ShareText = (slice.Share * 100).ToString("0.#") + "%";
            }
            return final;
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

        /// <summary>
        /// 从报告读出的口味里挑几款"同类好游戏"推荐。
        ///
        /// 刻意做成 <see cref="VaultData"/> 上的静态方法而不是视图里的私有方法：
        /// 挑选逻辑全是纯计算（不碰任何控件），放在数据层才能被离线测试覆盖，
        /// 也免得"推荐不推荐"这种判断和 UI 代码缠在一起。
        ///
        /// 规则（按优先级）：
        /// ① 候选来自用户时长占比最高的 <paramref name="genreCount"/> 个类型；
        /// ② 排除掉**已经玩过**的作品（玩过 = 已知，不构成推荐）；
        /// ③ 已安装优先（最容易被采纳）→ 评分高优先 → 发行新优先；
        /// ④ 每个类型最多出 <paramref name="perGenre"/> 款，总数不超过 <paramref name="limit"/>。
        /// </summary>
        public static List<RecommendRow> PickRecommendations(VaultData data,
            int genreCount = 3, int perGenre = 3, int limit = 8)
        {
            var rows = new List<RecommendRow>();
            if (data == null || data.GenreByTime == null || data.GenreByTime.Count == 0)
                return rows;

            var played = new HashSet<GameEntry>();
            foreach (var e in data.Entries)
                if (e.TotalPlaytime > 0) played.Add(e);

            var picked = new HashSet<GameEntry>();

            foreach (var slice in data.GenreByTime.Take(genreCount))
            {
                List<GameEntry> bucket;
                if (!data.GenreIndex.TryGetValue(slice.Name, out bucket) || bucket == null)
                    continue;

                var candidates = bucket
                    .Where(e => !played.Contains(e) && !picked.Contains(e))
                    .OrderByDescending(e => e.IsInstalled)
                    .ThenByDescending(e => e.HasScore ? e.CriticScore.Value : 0)
                    .ThenByDescending(e => e.ReleaseDate ?? DateTime.MinValue)
                    .Take(perGenre);

                foreach (var e in candidates)
                {
                    if (rows.Count >= limit) break;
                    picked.Add(e);
                    rows.Add(new RecommendRow { Entry = e, Reason = BuildReason(e, slice.Name) });
                }
                if (rows.Count >= limit) break;
            }

            return rows;
        }

        /// <summary>拼一句推荐理由，如「动作 · 已安装未玩 · Metacritic 92」。</summary>
        private static string BuildReason(GameEntry e, string genre)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(genre)) parts.Add(genre);

            // 推荐对象一定是"没玩过"的（PickRecommendations 已过滤），
            // 区别只在于"装了没装"——这决定了推荐的可执行性，比库龄更有用。
            if (e.IsInstalled) parts.Add(L10n.T("LocReasonInstalled"));
            if (e.HasScore) parts.Add(L10n.F("LocReasonScore", e.CriticScore.Value));

            // 既没装又没评分时，退回到一个通用标签，避免理由行空着
            if (parts.Count <= 1) parts.Add(L10n.T("LocReasonOwned"));
            return string.Join(" · ", parts.Distinct());
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

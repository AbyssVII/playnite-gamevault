using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameVault
{
    public static class TimeFmt
    {
        /// <summary>简短格式，只用 h / m，不用天。</summary>
        public static string Short(ulong seconds)
        {
            if (seconds <= 0) return "—";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalMinutes < 1) return "<1m";
            var hours = (int)ts.TotalHours;
            if (hours < 1) return ts.Minutes + "m";
            return ts.Minutes == 0 ? hours + "h" : hours + "h " + ts.Minutes + "m";
        }

        /// <summary>完整格式，只用小时 / 分钟（跟随界面语言）。</summary>
        public static string Long(ulong seconds)
        {
            if (seconds <= 0) return L10n.T("LocNotPlayed");
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalMinutes < 1) return "<1m";
            var hours = (int)ts.TotalHours;
            if (hours < 1) return L10n.F("LocMinutes", ts.Minutes);
            if (ts.Minutes == 0) return L10n.F("LocHoursOnly", hours);
            return L10n.F("LocHoursMinutes", hours, ts.Minutes);
        }
    }

    public static class StoreVisual
    {
        // 来源名称 -> (显示名, 徽章配色)
        private static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            { "steam",       new[] { "Steam",    "#66C0F4" } },
            { "epic",        new[] { "Epic",     "#D9D9D9" } },
            { "xbox",        new[] { "Xbox",     "#9BCB3C" } },
            { "microsoft",   new[] { "Xbox",     "#9BCB3C" } },
            { "ea",          new[] { "EA app",   "#FF6B57" } },
            { "origin",      new[] { "EA app",   "#FF6B57" } },
            { "ubisoft",     new[] { "Uplay",    "#4CC2FF" } },
            { "uplay",       new[] { "Uplay",    "#4CC2FF" } },
            { "gog",         new[] { "GOG",      "#B48BFF" } },
            { "playstation", new[] { "PSN",      "#5B8DEF" } },
            { "nintendo",    new[] { "Nintendo", "#FF5C6C" } },
            { "battle.net",  new[] { "战网",      "#3FA9F5" } },
            { "battlenet",   new[] { "战网",      "#3FA9F5" } },
            { "amazon",      new[] { "Amazon",   "#FFA53C" } },
            { "itch",        new[] { "itch.io",  "#FF7A7A" } },
            { "rockstar",    new[] { "RGL",      "#F5C542" } },
        };

        public static string DisplayName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var key = raw.ToLowerInvariant();
            foreach (var kv in Map)
                if (key.Contains(kv.Key)) return kv.Value[0];
            return raw;
        }

        public static string Color(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "#8B98A5";
            var key = raw.ToLowerInvariant();
            foreach (var kv in Map)
                if (key.Contains(kv.Key)) return kv.Value[1];
            return "#8B98A5";
        }
    }

    /// <summary>
    /// 图片缓存 + 后台异步解码。封面/海报在后台线程解码并 Freeze 后回传 UI 线程，
    /// 避免打开视图时同步解码大量图片造成的卡顿。
    /// </summary>
    public static class ImageCache
    {
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();
        private static readonly Dictionary<string, List<Action<ImageSource>>> Waiters =
            new Dictionary<string, List<Action<ImageSource>>>();
        private static readonly object Lock = new object();
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(4);

        public static ImageSource TryGet(string path, int decodeWidth)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (Lock)
            {
                ImageSource cached;
                return Cache.TryGetValue(Key(path, decodeWidth), out cached) ? cached : null;
            }
        }

        public static void LoadAsync(string path, int decodeWidth, Action<ImageSource> callback)
        {
            if (callback == null) return;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                callback(null);
                return;
            }

            var key = Key(path, decodeWidth);
            var start = false;
            lock (Lock)
            {
                ImageSource cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    callback(cached);
                    return;
                }
                List<Action<ImageSource>> list;
                if (!Waiters.TryGetValue(key, out list))
                {
                    list = new List<Action<ImageSource>>();
                    Waiters[key] = list;
                    start = true;
                }
                list.Add(callback);
            }

            if (!start) return;

            _ = Task.Run(async () =>
            {
                ImageSource image = null;
                try
                {
                    // 用 WaitAsync 限流，避免大量解码任务把线程池线程全占住
                    await Gate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        image = Decode(path, decodeWidth);
                    }
                    finally
                    {
                        Gate.Release();
                    }
                }
                catch
                {
                }

                List<Action<ImageSource>> callbacks;
                lock (Lock)
                {
                    if (image != null) Cache[key] = image;
                    callbacks = Waiters.ContainsKey(key) ? Waiters[key] : new List<Action<ImageSource>>();
                    Waiters.Remove(key);
                }

                var app = Application.Current;
                if (app == null)
                {
                    // 没有 WPF Application（例如离线自检）时直接回调，避免回调被静默丢掉
                    Dispatch(callbacks, image);
                    return;
                }
                _ = app.Dispatcher.BeginInvoke(new Action(() => Dispatch(callbacks, image)));
            });
        }

        private static void Dispatch(List<Action<ImageSource>> callbacks, ImageSource image)
        {
            foreach (var cb in callbacks)
            {
                try
                {
                    cb(image);
                }
                catch
                {
                }
            }
        }

        private static string Key(string path, int decodeWidth)
        {
            return decodeWidth + "|" + path;
        }

        private static ImageSource Decode(string path, int decodeWidth)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.DecodePixelWidth = decodeWidth;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }

    /// <summary>Metacritic 评分的配色。</summary>
    public static class ScorePalette
    {
        public const string NoScore = "#5A6673";
        public const string Low = "#EF4444";     // < 60  红
        public const string Fair = "#FACC15";    // 60-69 黄
        public const string Good = "#F97316";    // 70-79 橙
        public const string Great = "#22C55E";   // 80-89 绿
        public const string Top = "#FFD700";     // 90+   动态流彩的起始色

        // 90+ 的循环配色：金 → 青 → 紫 → 粉 → 绿
        private static readonly Color[] Rainbow =
        {
            Color.FromRgb(0xFF, 0xD7, 0x00),
            Color.FromRgb(0x4C, 0xC2, 0xFF),
            Color.FromRgb(0xA7, 0x8B, 0xFA),
            Color.FromRgb(0xF4, 0x72, 0xB6),
            Color.FromRgb(0x4A, 0xDE, 0x80),
        };

        private static readonly Dictionary<string, SolidColorBrush> Cache =
            new Dictionary<string, SolidColorBrush>();
        private static readonly object Sync = new object();

        private static double phase;

        /// <summary>
        /// 按 5 个彩虹色依次填充渐变色标（首尾同色 + SpreadMethod.Repeat = 无缝循环）。
        /// 这样同一时刻元素上会同时出现好几种颜色，而不是"一个颜色渐变到另一个颜色"。
        /// </summary>
        public static void FillFlowStops(GradientStopCollection stops)
        {
            var n = Rainbow.Length;
            for (var i = 0; i < n; i++)
                stops.Add(new GradientStop(Rainbow[i], (double)i / n));
            stops.Add(new GradientStop(Rainbow[0], 1.0));
        }

        /// <summary>取（并缓存）冻结的静态画刷，避免每个卡片都新建一个。</summary>
        public static SolidColorBrush Get(string hex)
        {
            lock (Sync)
            {
                SolidColorBrush brush;
                if (Cache.TryGetValue(hex, out brush)) return brush;
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                Cache[hex] = brush;
                return brush;
            }
        }

        /// <summary>推进彩虹流动的相位（0→1 循环）。60ms 一帧、每帧 +0.035 → 约 1.7 秒走一轮。</summary>
        public static double NextPhase()
        {
            phase += 0.035;
            if (phase >= 1) phase -= 1;
            return phase;
        }
    }

    /// <summary>类型饼图的配色。刻意避开 ScorePalette 里已用于评分的红/黄/橙/绿，
    /// 免得用户把"类型颜色"误读成"评分高低"。</summary>
    public static class GenrePalette
    {
        public static readonly string[] Colors =
        {
            "#4CC2FF",  // 亮蓝
            "#A78BFA",  // 紫
            "#F472B6",  // 粉
            "#2DD4BF",  // 青绿
            "#FBBF24",  // 琥珀
            "#60A5FA",  // 中蓝
            "#FB923C",  // 橙棕
            "#C084FC",  // 淡紫
            "#34D399",  // 翠绿
        };

        public const string Other = "#64748B";
    }

    /// <summary>库存中的一条游戏记录（同一名称的多个平台副本会被合并）</summary>
    public class GameEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const int CoverWidth = 220;
        private const int PosterWidth = 640;

        private ImageSource cover;
        private ImageSource poster;
        private LinearGradientBrush animatedBrush;
        private TranslateTransform flowTransform;
        private bool coverRequested;
        private bool posterRequested;
        private double recentPercent;
        private double totalPercent;

        public Guid PrimaryId { get; set; }
        public string Name { get; set; }
        public ulong TotalPlaytime { get; set; }
        public ulong RecentPlaytime { get; set; }

        /// <summary>
        /// 该条目对应（合并前）的所有游戏 Id，字符串形式。
        /// 用于把 GameActivity 的会话记录归并到合并后的条目上 ——
        /// 同一款游戏在多平台有多个副本时，时间轴要把它们算作同一款。
        /// </summary>
        public List<string> ActivityKeys { get; set; } = new List<string>();

        public string CoverPath { get; set; }
        public string PosterPath { get; set; }
        public string IconPath { get; set; }

        public int? CriticScore { get; set; }
        public int? UserScore { get; set; }
        public int? CommunityScore { get; set; }

        public string Developers { get; set; }
        public string Publishers { get; set; }
        public string Genres { get; set; }
        public string Description { get; set; }

        public List<string> Stores { get; set; } = new List<string>();
        public List<string> Devices { get; set; } = new List<string>();

        public DateTime? LastActivity { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public DateTime? Added { get; set; }
        public bool IsInstalled { get; set; }
        public bool Favorite { get; set; }
        public int Copies { get; set; } = 1;

        // ---- 展示属性 ----
        public string TotalText => TimeFmt.Short(TotalPlaytime);
        public string RecentText => RecentPlaytime > 0 ? TimeFmt.Short(RecentPlaytime) : "—";
        public string RecentLongText => TimeFmt.Long(RecentPlaytime);
        public string TotalLongText => TimeFmt.Long(TotalPlaytime);

        public bool HasRecent => RecentPlaytime > 0;
        public bool HasScore => CriticScore.HasValue && CriticScore.Value > 0;
        public string ScoreText => HasScore ? CriticScore.Value.ToString(CultureInfo.InvariantCulture) : "—";

        /// <summary>评分档位：5=90+ / 4=80-89 / 3=70-79 / 2=60-69 / 1=&lt;60 / 0=无评分</summary>
        public int ScoreTier
        {
            get
            {
                if (!HasScore) return 0;
                var s = CriticScore.Value;
                if (s >= 90) return 5;
                if (s >= 80) return 4;
                if (s >= 70) return 3;
                if (s >= 60) return 2;
                return 1;
            }
        }

        public bool IsTopTier => ScoreTier == 5;

        public string ScoreColor
        {
            get
            {
                switch (ScoreTier)
                {
                    case 5: return ScorePalette.Top;
                    case 4: return ScorePalette.Great;
                    case 3: return ScorePalette.Good;
                    case 2: return ScorePalette.Fair;
                    case 1: return ScorePalette.Low;
                    default: return ScorePalette.NoScore;
                }
            }
        }

        /// <summary>
        /// 评分画刷。90+ 返回一条**横向彩虹渐变**（金/青/紫/粉/绿同屏，
        /// 并靠 RelativeTransform 平移 + SpreadMethod.Repeat 无缝流动），
        /// 其余档位是共享的冻结单色画刷。
        /// </summary>
        public Brush ScoreBrush
        {
            get
            {
                if (ScoreTier != 5) return ScorePalette.Get(ScoreColor);

                if (animatedBrush == null)
                {
                    flowTransform = new TranslateTransform(0, 0);
                    var gradient = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 0),
                        SpreadMethod = GradientSpreadMethod.Repeat,
                        RelativeTransform = flowTransform
                    };
                    ScorePalette.FillFlowStops(gradient.GradientStops);
                    animatedBrush = gradient;
                }
                return animatedBrush;
            }
        }

        /// <summary>由视图的定时器驱动彩虹流动（未渲染过的卡片直接跳过）。</summary>
        public void ApplyFlow(double phase)
        {
            if (flowTransform != null) flowTransform.X = phase;
        }

        /// <summary>开发商 · 发行商，用于条形视图直接展示。</summary>
        public string DevPubText
        {
            get
            {
                var unknown = L10n.T("LocUnknown");
                var dev = string.IsNullOrWhiteSpace(Developers) ? unknown : Developers;
                var pub = string.IsNullOrWhiteSpace(Publishers) ? unknown : Publishers;
                return dev == pub ? dev : dev + " · " + pub;
            }
        }

        public string StoreList => Stores.Count > 0
            ? string.Join(" · ", Stores.Select(StoreVisual.DisplayName))
            : (Devices.Count > 0 ? string.Join(" · ", Devices) : "本地");
        public string DeviceList => Devices.Count > 0 ? string.Join(" · ", Devices) : "";
        public string ReleaseText => ReleaseDate.HasValue ? ReleaseDate.Value.ToString("yyyy-MM-dd") : "";
        public string LastPlayedText => LastActivity.HasValue
            ? LastActivity.Value.ToString("yyyy-MM-dd")
            : L10n.T("LocNeverPlayed");

        public double RecentPercent
        {
            get { return recentPercent; }
            set
            {
                if (Math.Abs(recentPercent - value) < 0.01) return;
                recentPercent = value;
                Raise("RecentPercent");
            }
        }

        public double TotalPercent
        {
            get { return totalPercent; }
            set
            {
                if (Math.Abs(totalPercent - value) < 0.01) return;
                totalPercent = value;
                Raise("TotalPercent");
            }
        }

        /// <summary>封面：命中缓存直接返回，否则触发后台解码，完成后通知绑定刷新。</summary>
        public ImageSource Cover
        {
            get
            {
                if (cover != null) return cover;
                if (coverRequested) return null;
                coverRequested = true;
                var direct = ImageCache.TryGet(CoverPath, CoverWidth);
                if (direct != null)
                {
                    cover = direct;
                    return cover;
                }
                ImageCache.LoadAsync(CoverPath, CoverWidth, src =>
                {
                    cover = src;
                    Raise("Cover");
                });
                return null;
            }
        }

        /// <summary>海报（只有悬停展开时才请求，避免无谓解码）。</summary>
        public ImageSource Poster
        {
            get
            {
                if (poster != null) return poster;
                if (posterRequested) return null;
                posterRequested = true;
                var direct = ImageCache.TryGet(PosterPath, PosterWidth)
                             ?? ImageCache.TryGet(CoverPath, PosterWidth);
                if (direct != null)
                {
                    poster = direct;
                    return poster;
                }
                ImageCache.LoadAsync(PosterPath ?? CoverPath, PosterWidth, src =>
                {
                    if (src != null || string.IsNullOrEmpty(CoverPath) || CoverPath == PosterPath)
                    {
                        poster = src;
                        Raise("Poster");
                        return;
                    }
                    // 背景图不可用（不存在或损坏）时再退回封面，保证浮层里总有图
                    ImageCache.LoadAsync(CoverPath, PosterWidth, cover =>
                    {
                        poster = cover;
                        Raise("Poster");
                    });
                });
                return null;
            }
        }

        public ImageSource Icon => ImageCache.TryGet(IconPath, 48);

        public List<StoreBadge> Badges
        {
            get
            {
                var list = new List<StoreBadge>();
                foreach (var s in Stores)
                    list.Add(new StoreBadge { Label = StoreVisual.DisplayName(s), Color = StoreVisual.Color(s) });
                return list;
            }
        }

        public string SearchBlob { get; set; }

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }

    public class StoreBadge
    {
        public string Label { get; set; }
        public string Color { get; set; }
    }

    /// <summary>饼图里的一瓣。</summary>
    public class GenreSlice
    {
        public string Name { get; set; }

        /// <summary>该类型下的游戏款数（合并平台副本后）</summary>
        public int GameCount { get; set; }

        /// <summary>是否是"其他"兜底切片（配色与文案不同）</summary>
        public bool IsOther { get; set; }

        /// <summary>该类型的总时长（秒）</summary>
        public ulong Playtime { get; set; }

        /// <summary>占总时长或总款数的比例（0~1）</summary>
        public double Share { get; set; }

        public string Color { get; set; }

        /// <summary>预生成的展示文案，避免在 XAML 里做多路绑定</summary>
        public string CountText { get; set; }
        public string PlaytimeText { get; set; }
        public string ShareText { get; set; }

        /// <summary>饼图几何数据（由 PieChart 填充）</summary>
        public string Geometry { get; set; }
    }

    /// <summary>
    /// 生成「你是什么样的玩家」报告所需的全部原始指标。
    /// 刻意做成一个扁平的事实集合：报告生成器只读它，不碰游戏库，
    /// 这样换文风 / 换语言时重新生成既快又不会有一致性问题。
    /// </summary>
    public class ProfileFacts
    {
        // ---- 规模 ----
        public int TotalGames;
        public int PlayedGames;
        public int UnplayedGames;
        public int InstalledGames;
        public ulong TotalPlaytime;
        public ulong RecentPlaytime;
        public int ActiveInPeriod;

        // ---- 集中度 ----
        public ulong Top1Time;
        public string Top1Name;
        public ulong Top3Time;
        public ulong Top10Time;
        public double Top1Share;      // 0~1
        public double Top10Share;
        public double RecentShare;

        // ---- 时长分层（款数）----
        public int TierNone;          // 0
        public int TierLight;         // < 5h
        public int TierMedium;        // 5 ~ 30h
        public int TierHeavy;         // 30 ~ 100h
        public int TierDeep;          // >= 100h
        public int CompletedishCount; // >= 100h 视为"深挖"

        // ---- 评分 ----
        public int ScoredCount;
        public double AverageScore;
        public int HighScoreCount;    // >= 85
        public int LowScoreCount;     // < 70

        // ---- 结构 ----
        public int BacklogAgeDays;    // 最早入库的未玩游戏距今天数
        public int OldestUnplayedAgeDays;
        public double InstallRate;    // 已安装 / 总数

        // ---- 类型 ----
        public string TopGenreName;
        public double TopGenreShare;
        public int GenreCount;
        public string SecondGenreName;

        // ---- 平台 / 商店 ----
        public string TopStoreName;
        public int TopStoreCount;

        // ---- 年份 ----
        public int OldestReleaseYear;
        public int NewestReleaseYear;
        public int ModernCount;       // 近 5 年发行的款数

        // ---- 列表（供报告引用具体游戏名）----
        public List<string> TopGameNames = new List<string>();
        public List<GameEntry> TopGames = new List<GameEntry>();

        // ---- 时间 / 习惯（报告扩充用）----
        /// <summary>已玩游戏中位时长（秒）——比平均值更抗极端值</summary>
        public ulong MedianPlayedTime;
        /// <summary>库里"短平快"作品（&lt; 10h）的数量，用于判断是否偏好小品</summary>
        public int ShortGameCount;
        /// <summary>平均每款已玩游戏玩了几轮/多久</summary>
        public ulong AvgPlayedTime;
        /// <summary>最近一次游玩距今天数（负数/0 表示无记录）</summary>
        public int DaysSinceLastPlay;
        /// <summary>入库年份跨度（最早入库 → 现在）</summary>
        public int LibraryAgeDays;
        /// <summary>近两年入库的款数，反映"最近还在买吗"</summary>
        public int RecentlyAdded;
        /// <summary>最长连续活跃感受：同时活跃（同一天有记录）的峰值不便算，这里用"两周内活跃款数"</summary>
        public int DistinctPlayedYears;
        /// <summary>收藏（Favorite）数量</summary>
        public int FavoriteCount;
        /// <summary>已安装但从未玩过的款数——最典型的"待办"</summary>
        public int InstalledUnplayed;
    }

    /// <summary>分析页「推荐」卡片的一行：一款被推荐的游戏 + 推荐理由。</summary>
    public class RecommendRow
    {
        public GameEntry Entry { get; set; }

        /// <summary>推荐理由，如「同类型 · 92 分 · 已入库未玩」</summary>
        public string Reason { get; set; }

        /// <summary>
        /// true = 推荐的游戏**不在用户库里**（AI / 知识库给出的库外作品）；
        /// false = 来自用户自己的库存。两种卡片样式不同（库外的显示"可入库/尝试"标识）。
        /// </summary>
        public bool IsExternal { get; set; }

        /// <summary>库外推荐时的游戏名（因为 Entry 为 null）。</summary>
        public string ExternalName { get; set; }

        /// <summary>库外推荐时所属的类型标签。</summary>
        public string ExternalGenre { get; set; }
    }

    // ==================================================================
    // 游玩时间轴（周维度）
    // ==================================================================

    /// <summary>某一周里玩得最多的一款游戏。</summary>
    public class WeekHighlight
    {
        public string Name { get; set; }
        public ulong Seconds { get; set; }
        public GameEntry Entry { get; set; }
    }

    /// <summary>时间轴上的一个点：一周。</summary>
    public class WeekBucket
    {
        /// <summary>该周周一（本地日期，已归零到 00:00）。</summary>
        public DateTime WeekStart { get; set; }

        /// <summary>ISO 周序号（用于显示"第 N 周"）。</summary>
        public int WeekOfYear { get; set; }

        /// <summary>该周总游玩秒数。</summary>
        public ulong TotalSeconds { get; set; }

        /// <summary>该周玩过的游戏数。</summary>
        public int GameCount { get; set; }

        /// <summary>该周玩得最多的游戏（按秒数），可能为 null（没记录）。</summary>
        public WeekHighlight Top { get; set; }

        // ---- 展示字段（由 View 层/聚合层填好，XAML 直接绑）----
        public double BarHeight { get; set; }      // 柱高（像素，按当周时长相对峰值换算）
        public string TotalText { get; set; }      // "12h 30m"
        public string TopNameText { get; set; }    // 冠军游戏名
        public string TopTimeText { get; set; }    // 冠军游玩时长
        public string WeekLabel { get; set; }      // "8/24"（该周周一）
        public string MonthLabel { get; set; }     // "9 月"（仅当月第一周有值）
        public bool IsEmpty { get; set; }          // 当周无记录
        public bool IsCurrent { get; set; }        // 是否本周

        /// <summary>
        /// 聚合中间状态：该周每款游戏的累计秒数。
        /// 只在 BuildTimeline 里用，算完就定型到 Top/GameCount，界面不读它。
        /// </summary>
        [System.NonSerialized]
        public Dictionary<GameEntry, ulong> accum = new Dictionary<GameEntry, ulong>();
    }

    /// <summary>时间轴上的一个月份分组（用于画月份分隔标签）。</summary>
    public class MonthGroup
    {
        public string Label { get; set; }          // "2026 年 9 月"
        public int StartIndex { get; set; }        // 在 Weeks 列表中的起始下标
        public int WeekCount { get; set; }
        /// <summary>该月总时长。</summary>
        public ulong TotalSeconds { get; set; }
        public string TotalText { get; set; }
    }

    /// <summary>
    /// 整个时间轴的数据：一串周 + 月份分组。
    /// 由 <see cref="VaultData.BuildTimeline"/> 生成，View 只负责渲染。
    /// </summary>
    public class TimelineData
    {
        public List<WeekBucket> Weeks { get; set; } = new List<WeekBucket>();
        public List<MonthGroup> Months { get; set; } = new List<MonthGroup>();

        /// <summary>时间跨度（周数）。</summary>
        public int WeekCount { get { return Weeks.Count; } }

        /// <summary>峰值周时长（秒），画柱高时做归一化用。</summary>
        public ulong PeakSeconds { get; set; }

        /// <summary>有记录的周数。</summary>
        public int ActiveWeeks { get; set; }

        /// <summary>时间轴范围内总时长。</summary>
        public ulong TotalSeconds { get; set; }

        /// <summary>数据是否可用（没有 GameActivity 时整块显示空态）。</summary>
        public bool Available { get; set; }

        public string TotalText { get; set; }
        public string SpanText { get; set; }
    }

    public static class NameKey
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder();
            var normalized = name.ToLowerInvariant();
            for (var i = 0; i < normalized.Length; i++)
            {
                var c = normalized[i];
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }
    }
}

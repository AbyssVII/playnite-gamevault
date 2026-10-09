using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace GameVault
{
    /// <summary>
    /// 一款游戏的评价：0–10 星的自评分 + 富文本正文。
    ///
    /// <see cref="Body"/> 存的是 FlowDocument 的 XAML 片段（TextRange 的 DataFormats.Xaml），
    /// 里面的插图在存盘前会被替换成占位文本（见 VaultView 里的存/取逻辑），
    /// 真正的图片文件拷到 <see cref="VaultSettings.ReviewImageDirectory"/> 下，只记文件名。
    /// </summary>
    [DataContract]
    public class GameReview
    {
        /// <summary>我的评分，0–10 星（0 = 没打星）。</summary>
        [DataMember(Name = "Stars")]
        public int Stars { get; set; }

        /// <summary>正文的 FlowDocument XAML 片段；为空表示没写正文。</summary>
        [DataMember(Name = "Body")]
        public string Body { get; set; }

        /// <summary>最后一次修改时间（yyyy-MM-dd HH:mm），只用于展示/排查。</summary>
        [DataMember(Name = "Updated")]
        public string Updated { get; set; }

        public GameReview()
        {
            Stars = 0;
        }

        public GameReview Clone()
        {
            return new GameReview { Stars = Stars, Body = Body, Updated = Updated };
        }
    }

    /// <summary>插件的持久化设置（保存在 ExtensionsData\&lt;插件Id&gt;\settings.json）。</summary>
    [DataContract]
    public class GameVaultSettings
    {
        public const double DefaultCardWidth = 172;
        public const double MinCardWidth = 118;
        public const double MaxCardWidth = 268;

        /// <summary>网格卡片宽度，由右下角缩放条控制，跨会话保留。</summary>
        [DataMember(Name = "CardWidth")]
        public double CardWidth { get; set; }

        /// <summary>界面语言：zh / en。</summary>
        [DataMember(Name = "Language")]
        public string Language { get; set; }

        /// <summary>分析页「类型分布」列的宽度（用户拖动分隔条后保存）。0 = 用默认值。</summary>
        [DataMember(Name = "AnalyzeGenreWidth")]
        public double AnalyzeGenreWidth { get; set; }

        /// <summary>分析页第一排（饼图 + Top 50）的高度。0 = 用默认值。</summary>
        [DataMember(Name = "AnalyzeTopHeight")]
        public double AnalyzeTopHeight { get; set; }

        /// <summary>分析页「游玩时间轴」面板的高度。0 = 用默认值。</summary>
        [DataMember(Name = "AnalyzeTimelineHeight")]
        public double AnalyzeTimelineHeight { get; set; }

        /// <summary>分析页「游戏周报」面板的高度。0 = 用默认值。</summary>
        [DataMember(Name = "AnalyzeWeeklyHeight")]
        public double AnalyzeWeeklyHeight { get; set; }

        /// <summary>趣味页的待玩清单：按用户排列的顺序存游戏 Guid 字符串。</summary>
        [DataMember(Name = "Wishlist")]
        public List<string> Wishlist { get; set; }

        /// <summary>
        /// 趣味页的游戏评价：key = 游戏 PrimaryId，value = 星级 + 正文。
        /// 只记打过星或写过正文的游戏，纯浏览过的不会进来。
        /// </summary>
        [DataMember(Name = "Reviews")]
        public Dictionary<string, GameReview> Reviews { get; set; }

        public GameVaultSettings()
        {
            CardWidth = DefaultCardWidth;
            Language = L10n.Zh;
            Wishlist = new List<string>();
            Reviews = new Dictionary<string, GameReview>();
        }

        public static double Clamp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return DefaultCardWidth;
            if (value < MinCardWidth) return MinCardWidth;
            if (value > MaxCardWidth) return MaxCardWidth;
            return value;
        }

        /// <summary>分隔条尺寸的合理区间。太小的值会让面板挤成一条，读不出内容。</summary>
        public static double ClampPanel(double value, double fallback, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// 设置读写。刻意自己写文件而不是用 LoadPluginSettings：
    /// 存在 GetPluginUserDataPath() 下（即 ExtensionsData\&lt;插件Id&gt;\），
    /// Playnite 备份"扩展数据"时会一并带走，且不受 SDK 泛型约束影响。
    /// </summary>
    public static class VaultSettings
    {
        private const string FileName = "settings.json";

        private static readonly object Sync = new object();
        private static GameVaultSettings current = new GameVaultSettings();
        private static string filePath;
        private static DispatcherTimer saveTimer;
        private static bool initialized;

        public static bool Initialized
        {
            get { lock (Sync) return initialized; }
        }

        /// <summary>插件数据目录（settings.json 所在目录）。未初始化时返回 null。</summary>
        public static string DataDirectory
        {
            get
            {
                lock (Sync)
                {
                    return string.IsNullOrEmpty(filePath) ? null : Path.GetDirectoryName(filePath);
                }
            }
        }

        public static double CardWidth
        {
            get { lock (Sync) return current.CardWidth; }
        }

        public static string Language
        {
            get { lock (Sync) return current.Language ?? L10n.Zh; }
        }

        public static void SetLanguage(string language)
        {
            var value = string.Equals(language, L10n.En, StringComparison.OrdinalIgnoreCase) ? L10n.En : L10n.Zh;
            lock (Sync)
            {
                if (string.Equals(current.Language, value, StringComparison.Ordinal)) return;
                current.Language = value;
            }
            ScheduleSave();
        }

        public static void Init(string userDataPath)
        {
            lock (Sync)
            {
                try
                {
                    if (string.IsNullOrEmpty(userDataPath)) return;
                    if (!Directory.Exists(userDataPath)) Directory.CreateDirectory(userDataPath);
                    filePath = Path.Combine(userDataPath, FileName);
                    initialized = true;
                    if (!File.Exists(filePath)) return;

                    var serializer = new DataContractJsonSerializer(typeof(GameVaultSettings));
                    using (var stream = File.OpenRead(filePath))
                    {
                        var loaded = serializer.ReadObject(stream) as GameVaultSettings;
                        if (loaded != null)
                        {
                            loaded.CardWidth = GameVaultSettings.Clamp(loaded.CardWidth);
                            loaded.AnalyzeGenreWidth =
                                GameVaultSettings.ClampPanel(loaded.AnalyzeGenreWidth, 0, 300, 1100);
                            loaded.AnalyzeTopHeight =
                                GameVaultSettings.ClampPanel(loaded.AnalyzeTopHeight, 0, 240, 1400);
                            loaded.AnalyzeTimelineHeight =
                                GameVaultSettings.ClampPanel(loaded.AnalyzeTimelineHeight, 0, 170, 900);
                            loaded.AnalyzeWeeklyHeight =
                                GameVaultSettings.ClampPanel(loaded.AnalyzeWeeklyHeight, 0, 190, 620);
                            if (loaded.Wishlist == null) loaded.Wishlist = new List<string>();
                            if (loaded.Reviews == null) loaded.Reviews = new Dictionary<string, GameReview>();
                            current = loaded;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public static void SetCardWidth(double value)
        {
            lock (Sync)
            {
                var clamped = GameVaultSettings.Clamp(value);
                if (Math.Abs(current.CardWidth - clamped) < 0.01) return;
                current.CardWidth = clamped;
            }
            ScheduleSave();
        }

        // ---- 分析页面板尺寸 ----

        public static double AnalyzeGenreWidth
        {
            get { lock (Sync) return current.AnalyzeGenreWidth; }
        }

        public static double AnalyzeTopHeight
        {
            get { lock (Sync) return current.AnalyzeTopHeight; }
        }

        public static void SetAnalyzeGenreWidth(double value)
        {
            lock (Sync)
            {
                var clamped = GameVaultSettings.ClampPanel(value, 0, 300, 1100);
                if (Math.Abs(current.AnalyzeGenreWidth - clamped) < 1) return;
                current.AnalyzeGenreWidth = clamped;
            }
            ScheduleSave();
        }

        public static void SetAnalyzeTopHeight(double value)
        {
            lock (Sync)
            {
                var clamped = GameVaultSettings.ClampPanel(value, 0, 240, 1400);
                if (Math.Abs(current.AnalyzeTopHeight - clamped) < 1) return;
                current.AnalyzeTopHeight = clamped;
            }
            ScheduleSave();
        }

        public static double AnalyzeTimelineHeight
        {
            get { lock (Sync) return current.AnalyzeTimelineHeight; }
        }

        public static void SetAnalyzeTimelineHeight(double value)
        {
            lock (Sync)
            {
                var clamped = GameVaultSettings.ClampPanel(value, 0, 170, 900);
                if (Math.Abs(current.AnalyzeTimelineHeight - clamped) < 1) return;
                current.AnalyzeTimelineHeight = clamped;
            }
            ScheduleSave();
        }

        // ---- 游戏周报 ----

        public static double AnalyzeWeeklyHeight
        {
            get { lock (Sync) return current.AnalyzeWeeklyHeight; }
        }

        public static void SetAnalyzeWeeklyHeight(double value)
        {
            lock (Sync)
            {
                // 上下限和 XAML 里 RowWeekly 的 MinHeight / 实际可用高度对齐
                var clamped = GameVaultSettings.ClampPanel(value, 0, 190, 620);
                if (Math.Abs(current.AnalyzeWeeklyHeight - clamped) < 1) return;
                current.AnalyzeWeeklyHeight = clamped;
            }
            ScheduleSave();
        }

        // ---- 待玩清单 ----

        /// <summary>清单快照（返回副本，避免调用方无意中改到内部状态）。</summary>
        public static List<string> Wishlist
        {
            get
            {
                lock (Sync) return new List<string>(current.Wishlist ?? new List<string>());
            }
        }

        public static void SetWishlist(List<string> ids)
        {
            lock (Sync)
            {
                current.Wishlist = ids == null ? new List<string>() : new List<string>(ids);
            }
            ScheduleSave();
        }

        // ---- 游戏评价 ----

        /// <summary>
        /// 全部评价的快照（深拷贝，调用方随便改都不会影响内部状态）。
        /// 顺序不保证 —— 展示顺序由视图按"最近评价的排前面"自己排。
        /// </summary>
        public static Dictionary<string, GameReview> Reviews
        {
            get
            {
                lock (Sync)
                {
                    var copy = new Dictionary<string, GameReview>();
                    var source = current.Reviews;
                    if (source == null) return copy;
                    foreach (var pair in source)
                    {
                        if (pair.Value == null) continue;
                        copy[pair.Key] = pair.Value.Clone();
                    }
                    return copy;
                }
            }
        }

        /// <summary>取某款游戏的评价；没有则返回 null。</summary>
        public static GameReview GetReview(Guid gameId)
        {
            lock (Sync)
            {
                if (current.Reviews == null) return null;
                GameReview found;
                return current.Reviews.TryGetValue(gameId.ToString(), out found) && found != null
                    ? found.Clone()
                    : null;
            }
        }

        /// <summary>写入 / 覆盖某款游戏的评价。</summary>
        public static void SetReview(Guid gameId, GameReview review)
        {
            lock (Sync)
            {
                if (current.Reviews == null) current.Reviews = new Dictionary<string, GameReview>();
                if (review == null) current.Reviews.Remove(gameId.ToString());
                else current.Reviews[gameId.ToString()] = review.Clone();
            }
            ScheduleSave();
        }

        public static void RemoveReview(Guid gameId)
        {
            var removed = false;
            lock (Sync)
            {
                if (current.Reviews == null) return;
                removed = current.Reviews.Remove(gameId.ToString());
            }
            if (removed) ScheduleSave();
        }

        /// <summary>
        /// 评价里插图的存放目录（DataDirectory\reviews）。放进插件数据目录，
        /// Playnite 备份扩展数据时会跟着一起带走。返回 null 表示还没初始化。
        /// </summary>
        public static string ReviewImageDirectory
        {
            get
            {
                var dir = DataDirectory;
                if (string.IsNullOrEmpty(dir)) return null;
                return Path.Combine(dir, "reviews");
            }
        }

        /// <summary>延迟 600ms 落盘，避免拖动时每一帧都写文件。</summary>
        private static void ScheduleSave()
        {
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                Save();
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (saveTimer == null)
                {
                    saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                    saveTimer.Tick += (s, e) =>
                    {
                        saveTimer.Stop();
                        _ = Task.Run(new Action(Save));
                    };
                }
                saveTimer.Stop();
                saveTimer.Start();
            }));
        }

        private static void Save()
        {
            string path;
            GameVaultSettings snapshot;
            lock (Sync)
            {
                path = filePath;
                snapshot = current;
            }
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(GameVaultSettings));
                using (var stream = File.Create(path))
                    serializer.WriteObject(stream, snapshot);
            }
            catch
            {
            }
        }
    }
}

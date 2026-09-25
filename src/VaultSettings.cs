using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace GameVault
{
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

        /// <summary>AI 推荐配置（接口地址 / 密钥 / 模型）。缺省即不启用 AI，只走本地推荐。</summary>
        [DataMember(Name = "Ai")]
        public AiConfig Ai { get; set; }

        public GameVaultSettings()
        {
            CardWidth = DefaultCardWidth;
            Language = L10n.Zh;
            Ai = new AiConfig();
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
                            if (loaded.Ai == null) loaded.Ai = new AiConfig();
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

        // ---- AI 推荐配置 ----

        /// <summary>取当前 AI 配置的副本（调用方只读，不要直接改）。</summary>
        public static AiConfig Ai
        {
            get
            {
                lock (Sync)
                {
                    if (current.Ai == null) current.Ai = new AiConfig();
                    return new AiConfig
                    {
                        Endpoint = current.Ai.Endpoint,
                        ApiKey = current.Ai.ApiKey,
                        Model = current.Ai.Model
                    };
                }
            }
        }

        public static void SetAi(string endpoint, string apiKey, string model)
        {
            lock (Sync)
            {
                if (current.Ai == null) current.Ai = new AiConfig();
                current.Ai.Endpoint = (endpoint ?? "").Trim();
                current.Ai.ApiKey = (apiKey ?? "").Trim();
                current.Ai.Model = (model ?? "").Trim();
            }
            ScheduleSave();
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

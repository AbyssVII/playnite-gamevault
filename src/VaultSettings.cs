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

        public GameVaultSettings()
        {
            CardWidth = DefaultCardWidth;
            Language = L10n.Zh;
        }

        public static double Clamp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return DefaultCardWidth;
            if (value < MinCardWidth) return MinCardWidth;
            if (value > MaxCardWidth) return MaxCardWidth;
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

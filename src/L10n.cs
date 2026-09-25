using System;
using System.Collections.Generic;
using System.Windows;

namespace GameVault
{
    /// <summary>
    /// 界面本地化。
    ///
    /// XAML 侧用 {DynamicResource LocXxx}（会把整份字典挂到 Application.Resources，
    /// 换语言时替换字典即可让所有绑定自动刷新——包括 Popup/ToolTip 里的元素，
    /// 它们不在可视树上，用不了 RelativeSource 绑定）。
    /// C# 侧生成的动态文案用 <see cref="T"/> / <see cref="F"/>。
    /// </summary>
    public static class L10n
    {
        public const string Zh = "zh";
        public const string En = "en";

        private const string MarkerKey = "__gamevault_l10n__";

        private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
        {
            // 顶部
            { "LocTitle",           "游戏库存" },
            { "LocSearchHint",      "搜索游戏…" },
            { "LocSortTotal",       "总时长" },
            { "LocSortRecent",      "近两周" },
            { "LocSortScore",       "MC评分" },
            { "LocTipLanguage",     "界面语言" },
            { "LocTipList",         "切换到条形视图" },
            { "LocTipGrid",         "切换到网格视图" },
            { "LocTipShare",        "复制库存摘要到剪贴板" },
            { "LocTipZoom",         "← → 微调卡片大小 · 拖动滑块连续缩放" },

            // 概览卡片
            { "LocStatGames",       "库存总数" },
            { "LocStatPlaytime",    "总游戏时长" },
            { "LocStatRecent",      "近两周时长" },
            { "LocStatTop",         "最肝游戏" },
            { "LocStatScore",       "平均 Metacritic" },

            // 卡片 / 条形
            { "LocNotRecently",     "近期未玩" },
            { "LocNone",            "未玩" },
            { "LocNeverPlayed",     "从未游玩" },

            // 悬停详情
            { "LocDeveloper",       "开发商" },
            { "LocPublisher",       "发行商" },
            { "LocTotalTime",       "总时长" },
            { "LocRecentTime",      "近两周" },
            { "LocLastPlayed",      "最后游玩" },
            { "LocReleased",        "发行日期" },

            // 状态
            { "LocLoading",         "正在统计游戏库…" },
            { "LocEmpty",           "没有匹配的游戏" },

            // 动态文案
            { "LocUnknown",         "未知" },
            { "LocSeparator",       "、" },
            { "LocNotPlayed",       "未游玩" },
            { "LocMinutes",         "{0} 分钟" },
            { "LocHoursOnly",       "{0} 小时" },
            { "LocHoursMinutes",    "{0} 小时 {1} 分钟" },
            { "LocRawCount",        "库内 {0} 条记录 · 已安装 {1}" },
            { "LocPlayedCount",     "{0} 款已游玩" },
            { "LocActiveCount",     "{0} 款近期活跃" },
            { "LocScoredCount",     "{0} 款有 Metacritic 评分" },
            { "LocNoScoreData",     "暂无评分数据" },
            { "LocStatus",          "显示 {0} 款 · 排序：{1} · 近两周区间 {2} ~ {3}" },
            { "LocSourceOk",        "近两周时长来源：GameActivity 会话记录" },
            { "LocSourceMissing",   "未检测到 GameActivity，近两周时长不可用" },
            { "LocCopied",          "已复制库存摘要到剪贴板" },
            { "LocCopyFailed",      "复制失败，请重试" },
            { "LocSortNameTotal",   "总时长" },
            { "LocSortNameRecent",  "近两周时长" },
            { "LocSortNameScore",   "Metacritic 评分" },
            { "LocNoScore",         "无评分" },

            // 分享文本
            { "LocShareTitle",      "【我的游戏库存】" },
            { "LocShareCount",      "库存总数：{0} 款（同游戏多平台副本已合并）" },
            { "LocShareTotal",      "总时长：{0}" },
            { "LocShareRecent",     "近两周：{0}" },
            { "LocShareTop",        "最肝游戏：{0}（{1}）" },
            { "LocShareAvg",        "平均 Metacritic：{0}" },
            { "LocShareTopListTotal", "总时长 Top 50：" },
            { "LocShareTopListRecent","近两周时长 Top 50：" },
            { "LocShareTopListScore", "Metacritic 评分 Top 50：" },
            { "LocShareFooter",     "由 Playnite「游戏库存」生成 · {0}" },
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "LocTitle",           "Game Vault" },
            { "LocSearchHint",      "Search games…" },
            { "LocSortTotal",       "Total time" },
            { "LocSortRecent",      "Last 2 weeks" },
            { "LocSortScore",       "MC score" },
            { "LocTipLanguage",     "Language" },
            { "LocTipList",         "Switch to list view" },
            { "LocTipGrid",         "Switch to grid view" },
            { "LocTipShare",        "Copy library summary" },
            { "LocTipZoom",         "← → fine-tune size · drag the slider to zoom" },

            { "LocStatGames",       "Games" },
            { "LocStatPlaytime",    "Total playtime" },
            { "LocStatRecent",      "Last 2 weeks" },
            { "LocStatTop",         "Most played" },
            { "LocStatScore",       "Avg. Metacritic" },

            { "LocNotRecently",     "Not recently" },
            { "LocNone",            "None" },
            { "LocNeverPlayed",     "Never played" },

            { "LocDeveloper",       "Developer" },
            { "LocPublisher",       "Publisher" },
            { "LocTotalTime",       "Total" },
            { "LocRecentTime",      "Last 2 weeks" },
            { "LocLastPlayed",      "Last played" },
            { "LocReleased",        "Released" },

            { "LocLoading",         "Scanning your library…" },
            { "LocEmpty",           "No games match" },

            { "LocUnknown",         "Unknown" },
            { "LocSeparator",       ", " },
            { "LocNotPlayed",       "Not played" },
            { "LocMinutes",         "{0} min" },
            { "LocHoursOnly",       "{0}h" },
            { "LocHoursMinutes",    "{0}h {1}m" },
            { "LocRawCount",        "{0} records · {1} installed" },
            { "LocPlayedCount",     "{0} played" },
            { "LocActiveCount",     "{0} active recently" },
            { "LocScoredCount",     "{0} with a Metacritic score" },
            { "LocNoScoreData",     "No score data" },
            { "LocStatus",          "Showing {0} · Sorted by {1} · Last 2 weeks {2} ~ {3}" },
            { "LocSourceOk",        "Recent playtime from GameActivity sessions" },
            { "LocSourceMissing",   "GameActivity not found — recent playtime unavailable" },
            { "LocCopied",          "Library summary copied to clipboard" },
            { "LocCopyFailed",      "Copy failed, please retry" },
            { "LocSortNameTotal",   "total time" },
            { "LocSortNameRecent",  "recent playtime" },
            { "LocSortNameScore",   "Metacritic score" },
            { "LocNoScore",         "No score" },

            { "LocShareTitle",      "[My Game Library]" },
            { "LocShareCount",      "Games: {0} (multi-platform copies merged)" },
            { "LocShareTotal",      "Total playtime: {0}" },
            { "LocShareRecent",     "Last 2 weeks: {0}" },
            { "LocShareTop",        "Most played: {0} ({1})" },
            { "LocShareAvg",        "Avg. Metacritic: {0}" },
            { "LocShareTopListTotal", "Top 50 by total time:" },
            { "LocShareTopListRecent","Top 50 by recent playtime:" },
            { "LocShareTopListScore", "Top 50 by Metacritic score:" },
            { "LocShareFooter",     "Generated by Playnite \"Game Vault\" · {0}" },
        };

        public static string Current { get; private set; }

        public static bool IsEnglish { get { return Current == En; } }

        static L10n()
        {
            Current = Zh;
        }

        /// <summary>切到指定语言并刷新 Application 级资源字典。</summary>
        public static void Apply(string language)
        {
            Current = string.Equals(language, En, StringComparison.OrdinalIgnoreCase) ? En : Zh;

            var app = Application.Current;
            if (app == null) return;

            var table = Current == En ? English : Chinese;

            ResourceDictionary stale = null;
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Contains(MarkerKey))
                {
                    stale = dict;
                    break;
                }
            }

            var fresh = new ResourceDictionary();
            foreach (var pair in table)
                fresh[pair.Key] = pair.Value;
            fresh[MarkerKey] = MarkerKey;

            if (stale != null) app.Resources.MergedDictionaries.Remove(stale);
            app.Resources.MergedDictionaries.Add(fresh);
        }

        /// <summary>取当前语言的文案。</summary>
        public static string T(string key)
        {
            var table = Current == En ? English : Chinese;
            string value;
            return table.TryGetValue(key, out value) ? value : key;
        }

        /// <summary>取当前语言的文案并格式化。</summary>
        public static string F(string key, params object[] args)
        {
            try
            {
                return string.Format(T(key), args);
            }
            catch
            {
                return T(key);
            }
        }
    }
}

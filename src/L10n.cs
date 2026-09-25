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
            { "LocTipZoom",         "← → 微调卡片大小 · 拖动滑块连续缩放" },
            { "LocTipAnalyze",      "打开数据分析（类型分布 · 时长 Top 50 · 玩家画像）" },
            { "LocTipBack",         "返回游戏库存" },
            { "LocTipCopyShare",    "复制数据到剪贴板" },
            { "LocTipShareImage",   "生成分享长图" },

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
            { "LocCopyFailed",      "复制失败，请重试" },
            { "LocSortNameTotal",   "总时长" },
            { "LocSortNameRecent",  "近两周时长" },
            { "LocSortNameScore",   "Metacritic 评分" },

            // 分享文本（分析页的复制功能仍复用页脚）
            { "LocShareFooter",     "由 Playnite「游戏库存」生成 · {0}" },

            // ---- 分享长图 ----
            { "LocShareEyebrow",    "PLAYNITE · 游戏库存" },
            { "LocShareTitle",      "我的游戏画像" },
            { "LocShareSubtitle",   "{0} 款游戏 · 累计 {1}" },
            { "LocShareStatGames",  "游戏总数" },
            { "LocShareStatPlaytime","累计时长" },
            { "LocShareStatPlayed", "已玩过" },
            { "LocShareStatTop",    "最多的一款" },
            { "LocShareGenres",     "游戏类型分布" },
            { "LocShareTop",        "时长榜 Top 8" },
            { "LocShareReport",     "你是一个什么样的玩家" },
            { "LocShareSaving",     "正在生成长图…" },
            { "LocShareCopied",     "分享长图已复制到剪贴板" },
            { "LocShareSaved",      "分享长图已保存并复制：{0}" },
            { "LocShareFailed",     "生成分享长图失败" },
            { "LocShareNoData",     "库里还没有数据，无法生成长图" },

            // ============ 分析页 ============
            { "LocAnalyzeTitle",    "数据分析" },
            { "LocAnalyzeSubtitle", "从 {0} 款游戏的库存里读出来的你" },
            { "LocPanelGenres",     "游戏类型分布" },
            { "LocPanelGenresSub",  "按总时长占比 · 一款游戏可同时属于多个类型" },
            { "LocPanelTop",        "时长 Top 50" },
            { "LocPanelTopSub",     "累计占比 {0}" },
            { "LocPanelReport",     "你是一个什么样的玩家" },
            { "LocPanelReportSub",  "完全基于本地统计生成，不会上传任何数据" },
            { "LocBasisTime",       "按时长" },
            { "LocBasisCount",      "按款数" },
            { "LocGenreCount",      "共 {0} 款" },
            { "LocOtherGenres",     "其他" },
            { "LocNoGenreData",     "库里还没有类型数据" },
            { "LocNoGenreHint",     "在 Playnite 里为游戏补上「类型」，这里就会出现分布图" },
            { "LocChartCenter",     "总时长" },
            { "LocChartCenterCount","总款数" },
            { "LocGenreFilterOn",   "已筛选：{0}（{1} 款）" },
            { "LocGenreFilterOff",  "已取消类型筛选" },
            { "LocStyleSnarky",     "锐评" },
            { "LocStyleFormal",     "正式" },
            { "LocTipReportStyle",  "切换报告文风" },
            { "LocCopyAnalysis",    "已复制数据分析到剪贴板" },
            { "LocReportTitle",     "【我的游戏画像】" },
            { "LocReportGenres",    "类型 Top 5：" },
            { "LocTipSplitV",       "拖动调整左右宽度" },
            { "LocTipSplitH",       "拖动调整上下高度" },
            { "LocPanelRecommend",  "推荐同类好游戏" },
            { "LocRecommendHint",   "按你的口味与评分挑出来的" },
            { "LocRecommendHintGenres", "根据你的 {0} 偏好挑出来的" },
            { "LocRecommendNone",   "暂时没有可推荐的游戏——库里的好货你大概都玩过了" },
            { "LocRecommendMore",   "另有 {0} 款同类游戏值得一看" },
            { "LocReasonOwned",     "已入库未玩" },
            { "LocReasonScore",     "Metacritic {0} 分" },
            { "LocReasonInstalled", "已安装未玩" },
            { "LocReasonTop",       "高口碑" },

            // ---- 游玩时间轴 ----
            { "LocPanelTimeline",    "游玩时间轴" },
            { "LocPanelTimelineSub", "每一周你玩得最多的是什么、玩了多久" },
            { "LocTimelineSpan",     "最近 {0} 周，其中 {1} 周有记录" },
            { "LocTimelineMonth",    "{0} 年 {1} 月" },
            { "LocTimelineMonthLabel", "{1} 月" },
            { "LocTimelineTotal",    "共 {0}" },
            { "LocTimelineEmpty",    "这一周没有游玩记录" },
            { "LocTimelineNoData",   "没有找到 GameActivity 的逐局记录" },
            { "LocTimelineNoDataHint","要看到每周明细，需要安装 GameActivity 插件并游玩一段时间" },
            { "LocTimelineWeek",     "第 {0} 周" },
            { "LocThisWeek",         "本周" },
            { "LocTimelineTop",      "最爱：{0}" },
            { "LocTimelinePeak",     "峰值 {0}" },

            // ---- 推荐（AI / 库外）----
            { "LocRecommendExternalTitle", "你可能还想玩（库外）" },
            { "LocRecommendExternalHint",  "由 AI 根据你的口味推荐的库外作品" },
            { "LocRecommendInLibTitle",    "你库里还没玩的同类" },
            { "LocRecommendInLibHint",     "根据你的 {0} 偏好挑出来的" },
            { "LocRecommendAnalyzing",     "正在用 AI 分析你的口味…" },
            { "LocRecommendAiOff",         "未配置 AI 接口，显示本地推荐" },
            { "LocRecommendAiFailed",      "AI 分析失败，已回退到本地推荐" },
            { "LocRecommendBadge",         "库外" },
            { "LocRecommendTryBadge",      "可尝试" },
            { "LocTipRecommendRefresh",    "重新用 AI 分析并推荐" },

            // ---- AI 配置面板 ----
            { "LocAiBadgeOn",       "AI 已启用" },
            { "LocAiBadgeOff",      "AI 未配置" },
            { "LocAiConfigBtn",     "AI 设置" },
            { "LocAiRefreshBtn",    "重新分析" },
            { "LocAiConfigTitle",   "接入 AI 模型（豆包 / 任意 OpenAI 兼容接口）" },
            { "LocAiConfigHint",    "填 API Key 即可用。默认走火山方舟（豆包），也支持任何 OpenAI 兼容的 chat/completions 接口。Key 只存在本地 settings.json，不会上传。" },
            { "LocAiLabelKey",      "API Key" },
            { "LocAiLabelModel",    "模型 ID" },
            { "LocAiLabelEndpoint", "接口地址" },
            { "LocAiSave",          "保存并分析" },
            { "LocAiSaved",         "已保存" },
            { "LocAiNeedKey",       "请先填写 API Key" },
            { "LocAiCalling",       "正在分析…" },
            { "LocAiOk",            "分析完成" },
            { "LocAiFail",          "调用失败" },
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
            { "LocTipZoom",         "← → fine-tune size · drag the slider to zoom" },
            { "LocTipAnalyze",      "Open library analytics (genres · top 50 · player profile)" },
            { "LocTipBack",         "Back to game vault" },
            { "LocTipCopyShare",    "Copy the data to clipboard" },
            { "LocTipShareImage",   "Generate a shareable image" },

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

            { "LocShareFooter",     "Generated by Playnite \"Game Vault\" · {0}" },

            // ============ Analytics page ============
            { "LocAnalyzeTitle",    "Analytics" },
            { "LocAnalyzeSubtitle", "You, as read from a library of {0} games" },
            { "LocPanelGenres",     "Genre breakdown" },
            { "LocPanelGenresSub",  "Share of total playtime · a game can have several genres" },
            { "LocPanelTop",        "Top 50 by playtime" },
            { "LocPanelTopSub",     "{0} of your total playtime" },
            { "LocPanelReport",     "What kind of player are you?" },
            { "LocPanelReportSub",  "Generated entirely on your machine — nothing is uploaded" },
            { "LocBasisTime",       "By time" },
            { "LocBasisCount",      "By count" },
            { "LocGenreCount",      "{0} games" },
            { "LocOtherGenres",     "Other" },
            { "LocNoGenreData",     "No genre data in your library yet" },
            { "LocNoGenreHint",     "Tag your games with genres in Playnite and the chart appears here" },
            { "LocChartCenter",     "Total time" },
            { "LocChartCenterCount","Games" },
            { "LocGenreFilterOn",   "Filtered: {0} ({1} games)" },
            { "LocGenreFilterOff",  "Genre filter cleared" },
            { "LocStyleSnarky",     "Snarky" },
            { "LocStyleFormal",     "Formal" },
            { "LocTipReportStyle",  "Switch report tone" },
            { "LocCopyAnalysis",    "Analytics copied to clipboard" },
            { "LocReportTitle",     "[My player profile]" },
            { "LocReportGenres",    "Top 5 genres:" },
            { "LocTipSplitV",       "Drag to resize columns" },
            { "LocTipSplitH",       "Drag to resize rows" },
            { "LocPanelRecommend",  "More games like these" },
            { "LocRecommendHint",   "Picked from your taste and review scores" },
            { "LocRecommendHintGenres", "Based on your taste for {0}" },
            { "LocRecommendNone",   "Nothing left to recommend — you've probably played all the good ones" },
            { "LocRecommendMore",   "{0} more similar games worth a look" },
            { "LocReasonOwned",     "Owned, unplayed" },
            { "LocReasonScore",     "Metacritic {0}" },
            { "LocReasonInstalled", "Installed, unplayed" },
            { "LocReasonTop",       "Highly rated" },

            // ---- Play timeline ----
            { "LocPanelTimeline",    "Play timeline" },
            { "LocPanelTimelineSub", "What you played most each week, and for how long" },
            { "LocTimelineSpan",     "{0} weeks back · {1} with activity" },
            { "LocTimelineMonth",    "{0}-{1}" },
            { "LocTimelineMonthLabel", "{1}" },
            { "LocTimelineTotal",    "{0} total" },
            { "LocTimelineEmpty",    "No playtime recorded this week" },
            { "LocTimelineNoData",   "No per-session records from GameActivity" },
            { "LocTimelineNoDataHint","Install the GameActivity add-on and play for a while to see weekly detail" },
            { "LocTimelineWeek",     "Week {0}" },
            { "LocThisWeek",         "This week" },
            { "LocTimelineTop",      "Top: {0}" },
            { "LocTimelinePeak",     "Peak {0}" },

            // ---- Recommendations (AI / external) ----
            { "LocRecommendExternalTitle", "You might also like (outside your library)" },
            { "LocRecommendExternalHint",  "External picks generated by AI from your taste" },
            { "LocRecommendInLibTitle",    "Similar games you own but haven't played" },
            { "LocRecommendInLibHint",     "Based on your taste for {0}" },
            { "LocRecommendAnalyzing",     "Analysing your taste with AI…" },
            { "LocRecommendAiOff",         "No AI endpoint configured — showing local picks" },
            { "LocRecommendAiFailed",      "AI analysis failed — fell back to local picks" },
            { "LocRecommendBadge",         "External" },
            { "LocRecommendTryBadge",      "Try" },
            { "LocTipRecommendRefresh",    "Re-run AI analysis" },

            // ---- Share long image ----
            { "LocShareEyebrow",    "PLAYNITE · GAME VAULT" },
            { "LocShareTitle",      "My Gaming Profile" },
            { "LocShareSubtitle",   "{0} games · {1} total" },
            { "LocShareStatGames",  "Games" },
            { "LocShareStatPlaytime","Total time" },
            { "LocShareStatPlayed", "Played" },
            { "LocShareStatTop",    "Most played" },
            { "LocShareGenres",     "Genre breakdown" },
            { "LocShareTop",        "Top 8 by playtime" },
            { "LocShareReport",     "What kind of player are you" },
            { "LocShareSaving",     "Generating share image…" },
            { "LocShareCopied",     "Share image copied to clipboard" },
            { "LocShareSaved",      "Share image saved & copied: {0}" },
            { "LocShareFailed",     "Failed to generate the share image" },
            { "LocShareNoData",     "No data in the library yet" },

            // ---- AI config panel ----
            { "LocAiBadgeOn",       "AI on" },
            { "LocAiBadgeOff",      "AI off" },
            { "LocAiConfigBtn",     "AI settings" },
            { "LocAiRefreshBtn",    "Re-analyse" },
            { "LocAiConfigTitle",   "Connect an AI model (Doubao / any OpenAI-compatible API)" },
            { "LocAiConfigHint",    "An API key is all you need. Defaults to Volcano Ark (Doubao), but any OpenAI-compatible chat/completions endpoint works. The key is stored only in your local settings.json and never uploaded." },
            { "LocAiLabelKey",      "API key" },
            { "LocAiLabelModel",    "Model ID" },
            { "LocAiLabelEndpoint", "Endpoint" },
            { "LocAiSave",          "Save & analyse" },
            { "LocAiSaved",         "Saved" },
            { "LocAiNeedKey",       "Enter an API key first" },
            { "LocAiCalling",       "Analysing…" },
            { "LocAiOk",            "Done" },
            { "LocAiFail",          "Request failed" },
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

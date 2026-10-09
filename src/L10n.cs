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
            { "LocTipCopyShare",    "复制数据到剪贴板" },
            { "LocTipShareImage",   "生成分享长图" },

            // 概览卡片
            { "LocStatGames",       "库存总数" },
            { "LocStatScoreDist",   "MC 分数分布" },
            { "LocScoreBelow60",    "60 以下" },
            { "LocScoreDistSub",    "{0} 款优秀（{1}）· 共 {2} 款有分" },
            { "LocScoreDistNone",   "暂无 Metacritic 评分数据" },
            { "LocStatPlaytime",    "总游戏时长" },
            { "LocStatRecent",      "近两周时长" },
            { "LocStatTop",         "时长 Top 5" },
            { "LocRecentMore",      "另有 {0} 款未列出" },

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
            { "LocEmptyRecent",     "近两周没有玩过的游戏" },

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
            { "LocStatus",          "显示 {0} 款 · 排序：{1} · 近两周区间 {2} ~ {3}" },
            { "LocSourceOk",        "近两周时长来源：GameActivity 会话记录" },
            { "LocSourceMissing",   "未检测到 GameActivity，近两周时长不可用" },
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
            { "LocShareTop",        "时长榜 Top 20" },
            { "LocShareReport",     "玩家形象" },
            { "LocShareSaving",     "正在生成长图…" },
            { "LocShareCopied",     "分享长图已复制到剪贴板" },
            { "LocShareFailed",     "生成分享长图失败" },
            { "LocShareNoData",     "库里还没有数据，无法生成长图" },
            { "LocShareDialogTitle","保存分享长图" },
            { "LocShareFilter",     "PNG 图片|*.png|JPEG 图片|*.jpg|所有文件|*.*" },
            { "LocShareSavedPick",  "长图已保存并复制：{0}" },
            { "LocShareCanceled",   "已取消保存" },

            // ============ 分析页 ============
            { "LocAnalyzeSubtitle", "从 {0} 款游戏的库存里读出来的你" },
            { "LocPanelGenres",     "游戏类型分布" },
            { "LocPanelGenresSub",  "按总时长占比 · 一款游戏可同时属于多个类型" },
            { "LocPanelTop",        "时长 Top 50" },
            { "LocPanelTopSub",     "累计占比 {0}" },
            { "LocPanelWeekly",     "游戏周报" },
            { "LocPanelWeeklySub",  "近一年每周玩得最多的游戏" },
            { "LocWeeklySpan",       "近一年 {0} 周有记录 · 共 {1}" },
            { "LocWeeklyEmptyHint", "还没有游玩时长记录，装上 Playnite 成就后即可看到每周战报" },
            { "LocTipSplitWeekly",  "拖动调整周报高度" },
            { "LocPanelReport",     "玩家形象" },
            { "LocPanelReportSub",  "完全基于本地统计生成，不会上传任何数据" },
            { "LocBasisTime",       "按时长" },
            { "LocBasisCount",      "按款数" },
            { "LocGenreCount",      "共 {0} 款" },
            { "LocNoGenreData",     "库里还没有类型数据" },
            { "LocNoGenreHint",     "在 Playnite 里为游戏补上「类型」，这里就会出现分布图" },
            { "LocChartCenter",     "总时长" },
            { "LocChartCenterCount","总款数" },
            { "LocGenreFilterOn",   "已筛选：{0}（{1} 款）" },
            { "LocGenreFilterOff",  "已取消类型筛选" },
            { "LocStyleSnarky",     "评价" },
            { "LocTipReportStyle",  "傲娇锐评（点击重新生成）" },
            { "LocCopyAnalysis",    "已复制数据分析到剪贴板" },
            { "LocReportTitle",     "【我的游戏画像】" },
            { "LocReportGenres",    "类型 Top 5：" },
            { "LocTipSplitV",       "拖动调整左右宽度" },
            { "LocTipSplitH",       "拖动调整上下高度" },
            { "LocTipSplitTimeline", "拖动调整时间轴高度" },

            // ---- 游玩时间轴 ----
            { "LocPanelTimeline",    "游玩时间轴" },
            { "LocTimelineSpan",     "{0} 年 · 共 {1} 周有记录" },
            { "LocTimelineYear",     "年份" },
            { "LocTimelineMonth",    "{0} 年 {1} 月" },
            { "LocTimelineMonthLabel", "{1} 月" },
            { "LocTimelineTotal",    "共 {0}" },
            { "LocTimelineEmpty",    "这一周没有游玩记录" },
            { "LocTimelineNoData",   "没有找到 GameActivity 的逐局记录" },
            { "LocTimelineNoDataHint","要看到每周明细，需要安装 GameActivity 插件并游玩一段时间" },
            { "LocThisWeek",         "本周" },

            // ============ 顶部视图切换 ============
            { "LocPageStats",       "库存统计" },
            { "LocPageAnalyze",     "数据分析" },
            { "LocPageFun",         "趣味功能" },
            { "LocTipPageSelect",   "切换功能：库存统计 / 数据分析 / 趣味功能" },

            // ============ 趣味功能页（只剩待玩清单） ============
            { "LocFunListTitle",    "待玩清单" },
            { "LocFunListSub",      "攒一份「总有一天要玩」的名单，按住左边的拖动柄可以调整顺序" },
            { "LocFunAddToggle",    "添加游戏" },
            { "LocFunPickHint",     "展开后搜索并勾选游戏，勾上即加入清单" },
            { "LocFunPickNoMatch",  "没有匹配的游戏" },
            { "LocFunPickCount",    "清单里已有 {0} 款" },
            { "LocFunPin",          "置顶" },
            { "LocFunRemove",       "移出清单" },
            { "LocFunDragTip",      "按住这一行拖动可调整顺序" },
            { "LocFunClear",        "清空清单" },
            { "LocFunListEmpty",    "清单还是空的" },
            { "LocFunListEmptyHint","点上面的「添加游戏」，搜索并勾选想玩的游戏" },
            { "LocFunAdded",        "已加入「{0}」" },
            { "LocFunAlreadyIn",    "「{0}」已经在清单里了" },
            { "LocFunRemoved",      "已移出「{0}」" },
            { "LocFunCleared",      "清单已清空" },
            { "LocFunListTip",      "总时长 {0} · {1}" },

            // ============ 趣味功能页 · 子标签 ============
            { "LocFunTabWish",      "待玩清单" },
            { "LocFunTabReview",    "游戏评价" },

            // ============ 趣味功能页 · 游戏评价 ============
            { "LocReviewTitle",     "我的游戏评价" },
            { "LocReviewSub",       "给库存里的游戏打分、写评价；编辑器支持一级/二级标题、插图、加粗、首行缩进" },
            { "LocReviewPickToggle","选择游戏" },
            { "LocReviewPickNoMatch","没有匹配的游戏" },
            { "LocReviewPickCount", "库存共 {0} 款游戏" },
            { "LocReviewRated",     "已评价" },
            { "LocReviewNeedGame",  "请先选择一款游戏" },
            { "LocReviewEditorHint","在上面的下拉里搜索并点选一款游戏，就能开始打分、写评价了" },
            { "LocReviewEditing",   "正在评价「{0}」" },
            { "LocReviewStars",     "我的评分" },
            { "LocReviewStarsValue","{0} / 10 星" },
            { "LocReviewStarsHint", "点星星打分（0–10 星），再点同一颗可取消" },
            { "LocReviewLastUpdated", "上次评价 {0}" },
            { "LocReviewBody",      "正文" },
            { "LocReviewH1",        "一级标题" },
            { "LocReviewH2",        "二级标题" },
            { "LocReviewBold",      "加粗" },
            { "LocReviewIndent",    "首行缩进" },
            { "LocReviewInsertImg", "插图" },
            { "LocReviewImgDialog", "选择要插入的图片" },
            { "LocReviewImgFilter", "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*" },
            { "LocReviewImgFailed", "这张图片插不进去，换一张试试" },
            { "LocReviewImgMissing","有一张插图找不到了，已经跳过" },
            { "LocReviewBodyBroken","评价正文读取失败，已按纯文本还原" },
            { "LocReviewSave",      "保存评价" },
            { "LocReviewDelete",    "删除评价" },
            { "LocReviewSaved",     "已保存「{0}」的评价" },
            { "LocReviewDeleted",   "已删除「{0}」的评价" },
            { "LocReviewRateSaved", "已给「{0}」打 {1} 星" },
            { "LocReviewNeedBody",  "正文还是空的 —— 只打分也行，保存后随时可以回来补" },
            { "LocReviewListTitle", "已评价" },
            { "LocReviewListEmpty", "还没有评价过任何游戏" },
            { "LocReviewListEmptyHint","在上面选一款游戏打个分或写点评价，它就会出现在这里" },
            { "LocReviewCount",     "已评价 {0} 款" },
            { "LocReviewTime",      "总时长" },
            { "LocReviewMc",        "Metacritic" },
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
            { "LocTipCopyShare",    "Copy the data to clipboard" },
            { "LocTipShareImage",   "Generate a shareable image" },

            { "LocStatGames",       "Games" },
            { "LocStatScoreDist",   "MC score spread" },
            { "LocScoreBelow60",    "Below 60" },
            { "LocScoreDistSub",    "{0} rated 80+ ({1}) · {2} scored" },
            { "LocScoreDistNone",   "No Metacritic scores yet" },
            { "LocStatPlaytime",    "Total playtime" },
            { "LocStatRecent",      "Last 2 weeks" },
            { "LocStatTop",         "Top 5 by playtime" },
            { "LocRecentMore",      "{0} more not listed" },

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
            { "LocEmptyRecent",     "Nothing played in the last 2 weeks" },

            { "LocUnknown",         "Unknown" },
            { "LocSeparator",       ", " },
            { "LocNotPlayed",       "Not played" },
            { "LocMinutes",         "{0} min" },
            { "LocHoursOnly",       "{0}h" },
            { "LocHoursMinutes",    "{0}h {1}m" },
            { "LocRawCount",        "{0} records · {1} installed" },
            { "LocPlayedCount",     "{0} played" },
            { "LocActiveCount",     "{0} active recently" },
            { "LocStatus",          "Showing {0} · Sorted by {1} · Last 2 weeks {2} ~ {3}" },
            { "LocSourceOk",        "Recent playtime from GameActivity sessions" },
            { "LocSourceMissing",   "GameActivity not found — recent playtime unavailable" },
            { "LocSortNameTotal",   "total time" },
            { "LocSortNameRecent",  "recent playtime" },
            { "LocSortNameScore",   "Metacritic score" },

            { "LocShareFooter",     "Generated by Playnite \"Game Vault\" · {0}" },

            // ============ Analytics page ============
            { "LocAnalyzeSubtitle", "You, as read from a library of {0} games" },
            { "LocPanelGenres",     "Genre breakdown" },
            { "LocPanelGenresSub",  "Share of total playtime · a game can have several genres" },
            { "LocPanelTop",        "Top 50 by playtime" },
            { "LocPanelTopSub",     "{0} of your total playtime" },
            { "LocPanelWeekly",     "Weekly report" },
            { "LocPanelWeeklySub",  "Most-played game each week over the past year" },
            { "LocWeeklySpan",      "{0} active weeks in the past year - {1} total" },
            { "LocWeeklyEmptyHint", "No playtime records yet - install Playnite Achievements to see your weekly report" },
            { "LocTipSplitWeekly",  "Drag to resize the weekly report" },
            { "LocPanelReport",     "Player profile" },
            { "LocPanelReportSub",  "Generated entirely on your machine — nothing is uploaded" },
            { "LocBasisTime",       "By time" },
            { "LocBasisCount",      "By count" },
            { "LocGenreCount",      "{0} games" },
            { "LocNoGenreData",     "No genre data in your library yet" },
            { "LocNoGenreHint",     "Tag your games with genres in Playnite and the chart appears here" },
            { "LocChartCenter",     "Total time" },
            { "LocChartCenterCount","Games" },
            { "LocGenreFilterOn",   "Filtered: {0} ({1} games)" },
            { "LocGenreFilterOff",  "Genre filter cleared" },
            { "LocStyleSnarky",     "Review" },
            { "LocTipReportStyle",  "Tsundere review (click to regenerate)" },
            { "LocCopyAnalysis",    "Analytics copied to clipboard" },
            { "LocReportTitle",     "[My player profile]" },
            { "LocReportGenres",    "Top 5 genres:" },
            { "LocTipSplitV",       "Drag to resize columns" },
            { "LocTipSplitH",       "Drag to resize rows" },
            { "LocTipSplitTimeline", "Drag to resize the timeline" },

            // ---- Play timeline ----
            { "LocPanelTimeline",    "Play timeline" },
            { "LocTimelineSpan",     "{0} · {1} weeks with activity" },
            { "LocTimelineYear",     "Year" },
            { "LocTimelineMonth",    "{0}-{1}" },
            { "LocTimelineMonthLabel", "{1}" },
            { "LocTimelineTotal",    "{0} total" },
            { "LocTimelineEmpty",    "No playtime recorded this week" },
            { "LocTimelineNoData",   "No per-session records from GameActivity" },
            { "LocTimelineNoDataHint","Install the GameActivity add-on and play for a while to see weekly detail" },
            { "LocThisWeek",         "This week" },

            // ---- Share long image ----
            { "LocShareEyebrow",    "PLAYNITE · GAME VAULT" },
            { "LocShareTitle",      "My Gaming Profile" },
            { "LocShareSubtitle",   "{0} games · {1} total" },
            { "LocShareStatGames",  "Games" },
            { "LocShareStatPlaytime","Total time" },
            { "LocShareStatPlayed", "Played" },
            { "LocShareStatTop",    "Most played" },
            { "LocShareGenres",     "Genre breakdown" },
            { "LocShareTop",        "Top 20 by playtime" },
            { "LocShareReport",     "Player profile" },
            { "LocShareSaving",     "Generating share image…" },
            { "LocShareCopied",     "Share image copied to clipboard" },
            { "LocShareFailed",     "Failed to generate the share image" },
            { "LocShareNoData",     "No data in the library yet" },
            { "LocShareDialogTitle","Save share image" },
            { "LocShareFilter",     "PNG image|*.png|JPEG image|*.jpg|All files|*.*" },
            { "LocShareSavedPick",  "Share image saved & copied: {0}" },
            { "LocShareCanceled",   "Save canceled" },

            // ============ Top page switcher ============
            { "LocPageStats",       "Library" },
            { "LocPageAnalyze",     "Analytics" },
            { "LocPageFun",         "Fun & Games" },
            { "LocTipPageSelect",   "Switch view: library / analytics / fun" },

            // ============ Fun page (to-play list only) ============
            { "LocFunListTitle",    "To-play list" },
            { "LocFunListSub",      "Build a \"someday\" pile — drag the handle on the left to reorder" },
            { "LocFunAddToggle",    "Add games" },
            { "LocFunPickHint",     "Expand, search and tick games to add them" },
            { "LocFunPickNoMatch",  "No matching games" },
            { "LocFunPickCount",    "{0} on the list" },
            { "LocFunPin",          "Move to top" },
            { "LocFunRemove",       "Remove from list" },
            { "LocFunDragTip",      "Drag anywhere on the row to reorder" },
            { "LocFunClear",        "Clear list" },
            { "LocFunListEmpty",    "The list is empty" },
            { "LocFunListEmptyHint","Hit \"Add games\" above, then search and tick what you fancy" },
            { "LocFunAdded",        "Added \"{0}\"" },
            { "LocFunAlreadyIn",    "\"{0}\" is already on the list" },
            { "LocFunRemoved",      "Removed \"{0}\"" },
            { "LocFunCleared",      "List cleared" },
            { "LocFunListTip",      "Total {0} · {1}" },

            // ============ Fun page · sub-tabs ============
            { "LocFunTabWish",      "To-play list" },
            { "LocFunTabReview",    "Reviews" },

            // ============ Fun page · game reviews ============
            { "LocReviewTitle",     "My game reviews" },
            { "LocReviewSub",       "Rate and review the games in your library — the editor takes H1/H2 headings, images, bold and first-line indent" },
            { "LocReviewPickToggle","Choose a game" },
            { "LocReviewPickNoMatch","No matching games" },
            { "LocReviewPickCount", "{0} games in your library" },
            { "LocReviewRated",     "Reviewed" },
            { "LocReviewNeedGame",  "Pick a game first" },
            { "LocReviewEditorHint","Search and click a game in the dropdown above to start rating and reviewing" },
            { "LocReviewEditing",   "Reviewing \"{0}\"" },
            { "LocReviewStars",     "My score" },
            { "LocReviewStarsValue","{0} / 10" },
            { "LocReviewStarsHint", "Click a star to rate (0–10); click it again to clear" },
            { "LocReviewLastUpdated", "Last reviewed {0}" },
            { "LocReviewBody",      "Body" },
            { "LocReviewH1",        "Heading 1" },
            { "LocReviewH2",        "Heading 2" },
            { "LocReviewBold",      "Bold" },
            { "LocReviewIndent",    "First-line indent" },
            { "LocReviewInsertImg", "Insert image" },
            { "LocReviewImgDialog", "Pick an image to insert" },
            { "LocReviewImgFilter", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*" },
            { "LocReviewImgFailed", "That image could not be inserted — try another one" },
            { "LocReviewImgMissing","One inserted image is missing and was skipped" },
            { "LocReviewBodyBroken","The review body could not be read and was restored as plain text" },
            { "LocReviewSave",      "Save review" },
            { "LocReviewDelete",    "Delete review" },
            { "LocReviewSaved",     "Saved your review of \"{0}\"" },
            { "LocReviewDeleted",   "Deleted the review of \"{0}\"" },
            { "LocReviewRateSaved", "Rated \"{0}\" {1} stars" },
            { "LocReviewNeedBody",  "The body is empty — rating it alone is fine, you can always add text later" },
            { "LocReviewListTitle", "Reviewed" },
            { "LocReviewListEmpty","Nothing reviewed yet" },
            { "LocReviewListEmptyHint","Pick a game above to rate or review it and it will show up here" },
            { "LocReviewCount",     "{0} reviewed" },
            { "LocReviewTime",      "Playtime" },
            { "LocReviewMc",        "Metacritic" },
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
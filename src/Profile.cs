using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameVault
{
    /// <summary>报告里的一段：小标题 + 正文。</summary>
    public class ReportSection
    {
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }

        /// <summary>该段对应的强调色，用于段落左侧的小竖条与标题</summary>
        public string Color { get; set; }

        /// <summary>段落里的关键数字，单独放大展示（可为 null）</summary>
        public string Metric { get; set; }
        public string MetricLabel { get; set; }

        /// <summary>
        /// 段落版式。八段刻意**不做成同一个样子**：
        ///   plain  —— 常规：小标题 + 正文
        ///   quote  —— 先给一句「判词」，下面才是正文
        ///   stat   —— 正文前先摆一行关键数字
        ///   list   —— 正文按 ①②③ 分条展开
        /// 由 XAML 侧的触发器按此切换布局，避免八段长得一模一样。
        /// </summary>
        public string Layout { get; set; }

        /// <summary>版式需要的附加文本（判词 / 数字行 / 分条内容的标题），可为 null。</summary>
        public string Lead { get; set; }
    }

    /// <summary>
    /// 「玩家形象」报告生成器。
    ///
    /// 完全本地运行：不联网、不上传、不需要 API Key。
    /// 实现方式是"筛选 + 套模板"——先从 <see cref="ProfileFacts"/> 里
    /// 挑出最突出的几个特征（哪个档位人数最多、时长是不是高度集中、
    /// 未玩积压有多严重……），再按固定文风把数字填进去。
    ///
    /// 七段的写作要求（用户明确提过）：
    ///   ① **每段 50 字上下**（中文按字符数算）—— 宁短不冗长，像 Steam 年度总结那样一眼扫完；
    ///   ② **七段不能用同一个符号** —— 图标、左侧色条、版式都各自不同；
    ///   ③ **文风/格式要有差异化** —— 有的先下判词、有的先摆数字、有的分条列点，
    ///      不能七段都是「先嫌弃一句、再摊数据、最后嘴硬找补」的同一套腔调；
    ///   ④ **必须落到具体游戏** —— 能引游戏名就引（Top1、TopGameNames、类型冠军），
    ///      不写"玩得多/玩得少"这种放之四海皆准的空话；
    ///   ⑤ **语气要生动、打趣、有梗** —— 用网络热词/流行语（电子木乃伊、赛博功德、
    ///      电子养生、白嫖、喜加一、电子榨菜、精神状态、塌房、破防、氪金、遥遥领先……）
    ///      让它读起来像朋友在群里被公开处刑，而不是一份正经体检报告。
    ///   ⑥ **少用破折号** —— 正文里不写「——」，改用逗号 / 句号断句（用户明确要求）。
    ///
    /// 文风基调仍是「傲娇锐评」（先嫌弃、后认可），但只作为底色，
    /// 具体节奏随段落变化，避免读起来像同一段复制七遍。
    /// </summary>
    public static class ProfileReport
    {
        /// <summary>每段正文的最少字数（中文按字符数计），vaulttest 会据此断言。新文案统一压到 50 字上下。</summary>
        public const int MinBodyLength = 16;

        public static List<ReportSection> Build(VaultData data)
        {
            var sections = new List<ReportSection>();
            if (data == null || data.Facts == null || data.Facts.TotalGames == 0)
                return sections;

            var f = data.Facts;
            var zh = !L10n.IsEnglish;

            // 顺序刻意错开：先给「人设」，再摆「证据」（节奏 / 集中度），
            // 然后转到「口味」（类型 / 评分），最后收在「现状」（积压 / 活跃）。
            AddPlayerArchetype(sections, f, zh);   // 判词式
            AddPlayRhythm(sections, f, zh);        // 数字式
            AddFocus(sections, f, zh);             // 常规式
            AddGenre(sections, data, f, zh);       // 分条式
            AddTaste(sections, f, zh);             // 数字式
            AddBacklog(sections, f, zh);           // 常规式
            AddMomentum(sections, f, zh);          // 判词式

            return sections;
        }

        // ------------------------------------------------------------------
        // 各段落
        // ------------------------------------------------------------------

        /// <summary>
        /// 第一段：给玩家贴一个"人设"标签。
        /// 版式 = **判词式**：先用一句话把结论甩出来（像判决书的主文），正文再解释。
        /// 口吻 = **雌小鬼**：嘴上嫌弃、句句扎人，但每个结论最后都会不情不愿地认账。
        /// </summary>
        private static void AddPlayerArchetype(List<ReportSection> s, ProfileFacts f, bool zh)
        {
            var playedRate = f.TotalGames > 0 ? (double)f.PlayedGames / f.TotalGames : 0;
            var deepRate = f.TotalGames > 0 ? (double)f.TierDeep / f.TotalGames : 0;
            var topGame = Top(f);

            string title, verdict, body;
            if (playedRate < 0.2 && f.TotalGames >= 20)
            {
                title = zh ? "囤积型玩家" : "The Collector";
                verdict = zh
                    ? "哼，你买东西是为了「拥有」，才不是为了玩呢，笨蛋。"
                    : "买游戏是为了拥有，不是为了玩。";
                body = Pick(zh,
                    "{0} 款只玩过 {1} 款，{2} 款连封面都没点开，妥妥的囤货大师。",
                    "{1} of {0} played, {2} never launched. Professional hoarder.",
                    f.TotalGames, f.PlayedGames, f.UnplayedGames, topGame);
            }
            else if (deepRate > 0.15)
            {
                title = zh ? "深挖型玩家" : "The Deep Diver";
                verdict = zh
                    ? "哼，你哪是在玩游戏，你是在那几口井里定居了吧。"
                    : "Hmph — you don't play games, you settle into a few of them.";
                body = Pick(zh,
                    "{1}/{0} 款熬过 100 小时，占 {2}。《{3}》就是你的命门。",
                    "{1} of {0} past 100h ({2}). {3} is your white whale.",
                    f.TotalGames, f.TierDeep, Pct(deepRate), topGame, TimeFmt.Short(f.Top1Time));
            }
            else if (f.ScoredCount >= 15 && f.AverageScore >= 82)
            {
                title = zh ? "挑剔型玩家" : "The Connoisseur";
                verdict = zh
                    ? "哼，你的库不过是一份别人替你筛过一遍的榜单罢了。"
                    : "游戏是收藏，不是作业。";
                body = Pick(zh,
                    "有评分的 {0} 款平均 {1} 分，稳稳压过大盘。",
                    "Your {0} scored games average {1}. Comfortably above the curve.",
                    f.ScoredCount, f.AverageScore.ToString("0.#"), topGame);
            }
            else if (playedRate > 0.75)
            {
                title = zh ? "实干型玩家" : "The Closer";
                verdict = zh
                    ? "哼，你的库总算不是摆设了，勉强算个工作台吧。"
                    : "你的库不是展示柜，是仓库。";
                body = Pick(zh,
                    "{0} 款全开过，别人还在挑，你已经在通关了。",
                    "All {0} launched. While everyone browses, you finish.",
                    Pct(playedRate), f.PlayedGames, topGame, TimeFmt.Short(f.Top1Time));
            }
            else if (f.ActiveInPeriod > 0 && f.ActiveInPeriod <= 3 && f.TotalGames >= 30)
            {
                title = zh ? "独宠型玩家" : "The Monogamist";
                verdict = zh
                    ? "哼，别人还在挑挑拣拣，你已经认准一家不走了吧。"
                    : "别人逛超市，你在清仓。";
                body = Pick(zh,
                    "{0} 款库存只碰了 {1} 款，注意力是真的稀缺。",
                    "{0} games, {1} touched. Your focus is an endangered species.",
                    f.TotalGames, f.ActiveInPeriod, topGame);
            }
            else
            {
                title = zh ? "均衡型玩家" : "The All-Rounder";
                verdict = zh
                    ? "哼，没短板，也没棱角，无聊得很呢。"
                    : "Hmph — no weak spots, and no sharp edges. Boring, honestly.";
                body = Pick(zh,
                    "{0} 款、{1} 游玩率、{2} 评分偏好，各项都卡在中间。",
                    "{0} games, {1} played, {2} on scores. Perfectly average.",
                    f.TotalGames, Pct(playedRate), f.ScoredCount > 0 ? f.AverageScore.ToString("0") : "—",
                    topGame, Pct(f.Top1Share));
            }

            s.Add(new ReportSection
            {
                Icon = "\uE77B",              // 人物（Contact）
                Color = "#4CC2FF",
                Title = title,
                Lead = verdict,               // 判词单独一行
                Layout = "quote",
                Body = body,
                Metric = f.TotalGames.ToString(),
                MetricLabel = zh ? "款游戏" : "games"
            });
        }

        /// <summary>
        /// 游玩节奏：一款游戏你通常玩多久、多久玩一次、是速通还是细水长流。
        /// 版式 = **数字式**：正文之前先把「中位 / 均值 / 长短篇占比」摆成一行，
        /// 让读者先看到分布，再读解释。
        /// </summary>
        private static void AddPlayRhythm(List<ReportSection> s, ProfileFacts f, bool zh)
        {
            if (f.PlayedGames == 0)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE916",          // 沙漏
                    Color = "#64748B",
                    Title = zh ? "还没有节奏可谈" : "No rhythm yet",
                    Layout = "plain",
                    Body = Pick(zh,
                        "一条时长记录都没有，我实在没料下嘴。",
                        "Not a single playtime record. Nothing to work with.")
                });
                return;
            }

            var median = TimeFmt.Short(f.MedianPlayedTime);
            var avg = TimeFmt.Short(f.AvgPlayedTime);
            var shortRate = f.PlayedGames > 0 ? (double)f.ShortGameCount / f.PlayedGames : 0;
            var champion = Top(f);

            // 数字行：让「中位 / 均值 / 短篇占比」一眼可见
            var statLine = zh
                ? "中位 {0}  ｜  均值 {1}  ｜  10 小时以内 {2}（{3} 款）"
                : "Median {0}  |  Mean {1}  |  Under 10h {2} ({3} games)";
            var lead = Fmt(statLine, median, avg, Pct(shortRate), f.ShortGameCount);

            string title, body;
            if (f.AvgPlayedTime > 0 && f.MedianPlayedTime > 0
                && (double)f.AvgPlayedTime / f.MedianPlayedTime >= 2.5)
            {
                title = zh ? "少数几款撑起平均值" : "A few titles carry the average";
                body = Pick(zh,
                    "中位 {0}、平均 {1}，差 {2} 倍，少数游戏吃掉了你的夜。",
                    "Median {0}, mean {1} — {2}x apart. A few games ate your nights.",
                    median, avg, ((double)f.AvgPlayedTime / f.MedianPlayedTime).ToString("0.#"), champion);
            }
            else
            {
                title = zh ? "节奏相当均匀" : "A remarkably even rhythm";
                body = Pick(zh,
                    "中位 {0}、平均 {1}，几乎分毫不差。稳得可怕。",
                    "Median {0}, mean {1} — identical. Terrifyingly steady.",
                    median, avg, "", champion);
            }

            // 库龄 & 最近是否还在入库（作为正文的收尾一句，不再另起一段）
            if (f.LibraryAgeDays > 0)
            {
                var years = f.LibraryAgeDays / 365.0;
                if (f.RecentlyAdded > 0 && years >= 2)
                {
                    body += Pick(zh,
                        "库陪了你 {0} 年，近两年还进了 {1} 款新货。",
                        "{0} years old, {1} new arrivals lately.",
                        years.ToString("0.#"), f.RecentlyAdded);
                }
                else if (years >= 2)
                {
                    body += Pick(zh,
                        "攒了 {0} 年，最近没添新货，是收手了还是满足了？",
                        "{0} years of collecting, nothing new. Done, or satisfied?",
                        years.ToString("0.#"));
                }
            }

            s.Add(new ReportSection
            {
                Icon = "\uE9D9",              // 计时器
                Color = "#60A5FA",
                Title = title,
                Lead = lead,                  // 数字行单独一行
                Layout = "stat",
                Body = body,
                Metric = median,
                MetricLabel = zh ? "中位时长" : "median"
            });
        }

        /// <summary>
        /// 时长集中度：是把时间摊开了，还是全砸在几款上。
        /// 版式 = **常规式**，但正文用「三段递进」的写法（现象 → 解释 → 忠告），
        /// 与收藏段的「单线陈述」区分开。
        /// </summary>
        private static void AddFocus(List<ReportSection> s, ProfileFacts f, bool zh)
        {
            if (f.TotalPlaytime == 0)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE81E",          // 时钟（Clock）
                    Color = "#64748B",
                    Title = zh ? "还没有时长记录" : "No playtime yet",
                    Layout = "plain",
                    Body = Pick(zh,
                        "一点时长都没有，这一项我没法评。",
                        "No playtime at all — cannot score this one.")
                });
                return;
            }

            var top1 = f.Top1Time;
            string title, body;
            if (f.Top1Share >= 0.35)
            {
                title = zh ? "一人扛起整片天" : "One game carries it all";
                body = Pick(zh,
                    "《{0}》一款吃掉 {1}（{2}），Top 10 占 {3}，是头号钉子户。",
                    "{0} alone is {1} ({2}); your top ten {3}. One game owns you.",
                    f.Top1Name, Pct(f.Top1Share), TimeFmt.Short(top1), Pct(f.Top10Share));
            }
            else if (f.Top1Share >= 0.15)
            {
                title = zh ? "有主心骨，也有周边" : "A main course and its sides";
                body = Pick(zh,
                    "最肝的《{0}》占 {1}，Top 10 合计 {2}，有主力也有新血。",
                    "{0} takes {1}, top ten {2}. A clear lead, still making room.",
                    f.Top1Name, Pct(f.Top1Share), Pct(f.Top10Share));
            }
            else
            {
                title = zh ? "时间摊得很开" : "Evenly spread";
                body = Pick(zh,
                    "最肝的《{0}》也只占 {1}，Top 10 合计 {2}，时间撒得很匀。",
                    "Even {0} is only {1}; top ten {2}. Your time is spread thin.",
                    f.Top1Name, Pct(f.Top1Share), Pct(f.Top10Share));
            }

            s.Add(new ReportSection
            {
                Icon = "\uE9D2",              // 汇总（AllApps）
                Color = "#A78BFA",
                Title = title,
                Layout = "plain",
                Body = body,
                Metric = TimeFmt.Short(f.TotalPlaytime),
                MetricLabel = zh ? "累计时长" : "total time"
            });
        }

        /// <summary>
        /// 未玩积压。
        /// 版式 = **常规式**，但正文口吻最"欠"，用连续反问推进。
        /// </summary>
        private static void AddBacklog(List<ReportSection> s, ProfileFacts f, bool zh)
        {
            var backlogRate = f.TotalGames > 0 ? (double)f.UnplayedGames / f.TotalGames : 0;

            string title, body;
            if (f.UnplayedGames == 0)
            {
                title = zh ? "零积压" : "Zero backlog";
                body = Pick(zh,
                    "库里每款都开过至少一次，是收藏党里的濒危保护动物。",
                    "Every single game launched once. Endangered behaviour.");
            }
            else if (backlogRate >= 0.7)
            {
                title = zh ? "积压重灾区" : "A backlog emergency";
                body = Pick(zh,
                    "{0} 款未玩，占 {1}，这不是收藏，是仓库。",
                    "{0} unplayed, {1} of the library. A warehouse, not a collection.",
                    f.UnplayedGames, Pct(backlogRate));
            }
            else if (backlogRate >= 0.4)
            {
                title = zh ? "积压在可控范围" : "A manageable backlog";
                body = Pick(zh,
                    "{0} 款没动过（{1}），克制得算体面。",
                    "{0} untouched ({1}). Restrained, almost disciplined.",
                    f.UnplayedGames, Pct(backlogRate));
            }
            else
            {
                title = zh ? "积压很轻微" : "Barely any backlog";
                body = Pick(zh,
                    "只有 {0} 款没玩（{1}），买了就开，很稀有。",
                    "Only {0} untouched ({1}). Buy-and-play. Rare.",
                    f.UnplayedGames, Pct(backlogRate));
            }

            s.Add(new ReportSection
            {
                Icon = "\uE7BA",              // 警告
                Color = "#F472B6",
                Title = title,
                Layout = "plain",
                Body = body,
                Metric = f.UnplayedGames.ToString(),
                MetricLabel = zh ? "款未玩" : "unplayed"
            });
        }

        /// <summary>
        /// 类型偏好。
        /// 版式 = **分条式**：正文用 ①②③ 把「主类型 / 次类型 / 代表游戏」拆开，
        /// 这是八段里唯一的分点结构。
        /// </summary>
        private static void AddGenre(List<ReportSection> s, VaultData data, ProfileFacts f, bool zh)
        {
            if (data.GenreByTime == null || data.GenreByTime.Count == 0)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE8EC",          // 标签
                    Color = "#64748B",
                    Title = zh ? "类型数据缺失" : "No genre data",
                    Layout = "plain",
                    Body = Pick(zh,
                        "类型标签太少，口味分析不出来，补几个就有戏了。",
                        "Not enough genre tags. Add a few and this gets interesting.")
                });
                return;
            }

            var top = data.GenreByTime.Take(3).ToList();
            var names = top.Select(g => g.Name).ToList();
            var joined = JoinNames(names, zh);

            // 分条式：①②③ 三段，每段一句
            var champion = (f.TopGameNames != null && f.TopGameNames.Count > 0) ? f.TopGameNames[0] : "";
            var lead = zh ? "按时长排出你的三个主类型：" : "Your top three genres by playtime:";
            var body = Pick(zh,
                "① 《{0}》独占 {1}，嘴上说都玩，时间很诚实。" +
                "② {2} 紧随其后，台阶分明：一超多强。" +
                "③ 代表作是《{3}》，只留一款就它。" +
                "你的库比你的嘴诚实多了。",
                "(1) {0} takes {1} — your mouth says everything, your hours disagree." +
                "(2) {2} next, a clear step down. One giant, a few majors." +
                "(3) {3} is the one you would keep. The rest are supporting cast." +
                "Your library is more honest than you are.",
                top[0].Name, top[0].ShareText, string.Join(zh ? "、" : ", ", names.Skip(1)), champion);

            s.Add(new ReportSection
            {
                Icon = "\uE8EC",              // 标签
                Color = "#2DD4BF",
                Title = zh ? "你的口味偏向" : "Where your taste lies",
                Lead = lead,                  // 分条前的引导句
                Layout = "list",
                Body = body,
                Metric = top[0].ShareText,
                MetricLabel = top[0].Name
            });
        }

        /// <summary>
        /// 评分偏好：你是吃高分还是随便玩。
        /// 版式 = **数字式**（和节奏段共用版式，但内容/口吻完全不同）。
        /// </summary>
        private static void AddTaste(List<ReportSection> s, ProfileFacts f, bool zh)
        {
            if (f.ScoredCount < 5)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE9D9",          // 计时器（样本不足时用中性图标）
                    Color = "#64748B",
                    Title = zh ? "样本不足" : "Not enough data",
                    Layout = "plain",
                    Body = Pick(zh,
                        "只有 {0} 款带评分，样本太少，我不乱下结论。",
                        "Only {0} rated games. Too few to judge, and I will not guess.",
                        f.ScoredCount)
                });
                return;
            }

            var modernRate = f.TotalGames > 0 ? (double)f.ModernCount / f.TotalGames : 0;
            var highShare = (double)f.HighScoreCount / f.ScoredCount;
            var lowShare = (double)f.LowScoreCount / f.ScoredCount;
            var champion = Top(f);

            var lead = Fmt(zh
                ? "平均 {0} 分  ｜  85 分以上 {1} 款（{2}）  ｜  70 分以下 {3} 款（{4}）"
                : "Avg {0}  |  Scored 85+ {1} ({2})  |  Below 70 {3} ({4})",
                f.AverageScore.ToString("0.#"), f.HighScoreCount, Pct(highShare),
                f.LowScoreCount, Pct(lowShare));

            string title, body;
            if (highShare >= 0.4)
            {
                title = zh ? "口碑优先" : "Reputation first";
                body = Pick(zh,
                    "{1}/{0} 款 85 分以上（{2}），成分党本党。",
                    "{1} of {0} scored 85+ ({2}). Label reader confirmed.",
                    f.ScoredCount, f.HighScoreCount, Pct(highShare), champion);
            }
            else if (lowShare >= 0.4)
            {
                title = zh ? "什么都吃得下" : "An omnivore";
                body = Pick(zh,
                    "{1}/{0} 款 70 分以下（{2}），专挑雷区下手。",
                    "{1} of {0} below 70 ({2}). You hunt for bombs.",
                    f.ScoredCount, f.LowScoreCount, Pct(lowShare), champion, TimeFmt.Short(f.Top1Time));
            }
            else
            {
                title = zh ? "口味居中" : "Middle of the road";
                body = Pick(zh,
                    "有评分的平均 {0} 分，不偏不倚，端水大师。",
                    "Scored games average {0}. Perfectly balanced mediocrity.",
                    f.AverageScore.ToString("0.#"), f.HighScoreCount, Pct(highShare), champion);
            }

            // 年份偏好：作为正文补充
            if (modernRate >= 0.6 && f.NewestReleaseYear > 0)
            {
                body += Pick(zh,
                    "{0} 是近五年的新作，你明显更想留在当下。",
                    "{0} of your library is from the last five years. You live in the present.",
                    Pct(modernRate));
            }
            else if (f.OldestReleaseYear > 0 && f.OldestReleaseYear <= 2005)
            {
                body += Pick(zh,
                    "最早的作品能追到 {0} 年，是老玩家了。",
                    "Oldest title: {0}. Some things do age well.",
                    f.OldestReleaseYear);
            }

            s.Add(new ReportSection
            {
                Icon = "\uE734",              // 实心星（评分偏好）
                Color = "#FBBF24",
                Title = title,
                Lead = lead,
                Layout = "stat",
                Body = body,
                Metric = f.AverageScore.ToString("0.#"),
                MetricLabel = zh ? "平均分" : "avg. score"
            });
        }

        /// <summary>
        /// 近期活跃度：现在还在玩吗。
        /// 版式 = **判词式**（和第一段呼应，但语气不同：第一段是「人设判词」，
        /// 这里是「现状判词」）。
        /// </summary>
        private static void AddMomentum(List<ReportSection> s, ProfileFacts f, bool zh)
        {
            var recentShare = f.TotalPlaytime > 0 ? (double)f.RecentPlaytime / f.TotalPlaytime : 0;
            var champion = Top(f);

            string title, verdict, body;
            if (f.RecentPlaytime == 0)
            {
                title = zh ? "暂时停滞" : "Currently dormant";
                verdict = zh ? "现状：你的库还亮着灯呢，只是已经没人进门了，哼。" : "Status: the lights are on, but nobody's home. Hmph.";
                body = Pick(zh,
                    "近两周零时长，不问了，问了你也不会说。",
                    "Zero playtime in two weeks. Not asking why.",
                    champion, TimeFmt.Short(f.Top1Time));
            }
            else if (f.ActiveInPeriod >= 8)
            {
                title = zh ? "多线操作" : "Juggling several";
                verdict = zh ? "现状：同时开着八条战线呢，结果哪条都没打完，哼。" : "Status: eight fronts open, none of them finished. Hmph.";
                body = Pick(zh,
                    "近两周碰了 {0} 款，合计 {1}，注意力比金鱼还短。",
                    "{0} games in two weeks, {1} total. Goldfish-level focus.",
                    f.ActiveInPeriod, TimeFmt.Short(f.RecentPlaytime));
            }
            else if (recentShare >= 0.1)
            {
                title = zh ? "热得发烫" : "Running hot";
                verdict = zh ? "现状：正和某款游戏处于蜜月期呢，真是藏都藏不住。" : "Status: honeymoon phase. Not even hiding it.";
                body = Pick(zh,
                    "近两周的 {0} 就占历史总时长 {1}，你正和《{2}》热恋。",
                    "{0} in two weeks is {1} of your lifetime. You are inseparable from {2}.",
                    TimeFmt.Short(f.RecentPlaytime), Pct(recentShare), champion);
            }
            else
            {
                title = zh ? "细水长流" : "Steady as she goes";
                verdict = zh ? "现状：不冲刺，但也从没真正停下来过呢。" : "Status: no sprints — and no real stops either.";
                body = Pick(zh,
                    "近两周玩了 {0}，集中在 {1} 款，《{2}》是主力，不快但没断。",
                    "{0} over two weeks, {1} games, {2} leading. Slow but steady.",
                    TimeFmt.Short(f.RecentPlaytime), f.ActiveInPeriod, champion);
            }

            s.Add(new ReportSection
            {
                Icon = "\uE823",              // 活动/脉搏
                Color = "#4CC2FF",
                Title = title,
                Lead = verdict,
                Layout = "quote",
                Body = body,
                Metric = f.ActiveInPeriod.ToString(),
                MetricLabel = zh ? "款近期活跃" : "recently active"
            });
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

        /// <summary>取库里时长最高的那款游戏名，取不到给个中性占位。</summary>
        private static string Top(ProfileFacts f)
        {
            if (f == null) return "—";
            if (!string.IsNullOrEmpty(f.Top1Name)) return f.Top1Name;
            if (f.TopGameNames != null && f.TopGameNames.Count > 0) return f.TopGameNames[0];
            return "—";
        }

        private static string Fmt(string template, params object[] args)
        {
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        /// <summary>
        /// 从"中文模板 / 英文模板"里挑一条并按同一组参数格式化。
        ///
        /// 刻意做成一个方法而不是写 `zh ? Fmt(中, args) : Fmt(英, args)`：后者极易写错 ——
        /// 三元表达式的两个分支各是一次独立调用，很容易只给中文分支传参数，
        /// 英文那半就会因为 Format 抛异常而原样吐出带 {0} 的模板（本项目踩过）。
        /// 放在一起调用，参数只有一份，两边不可能写岔。
        /// </summary>
        private static string Pick(bool zh, string chinese, string english, params object[] args)
        {
            return Fmt(zh ? chinese : english, args);
        }

        private static string Pct(double value)
        {
            return (value * 100).ToString("0.#") + "%";
        }

        private static string JoinNames(List<string> names, bool zh)
        {
            if (names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            var sep = zh ? "、" : ", ";
            return string.Join(sep, names.Take(names.Count - 1)) + (zh ? " 和 " : " and ") + names.Last();
        }

        /// <summary>把整份报告拼成纯文本，用于"复制到剪贴板"。</summary>
        public static string ToPlainText(List<ReportSection> sections, VaultData data)
        {
            var sb = new StringBuilder();
            if (sections == null || sections.Count == 0) return "";

            sb.AppendLine(L10n.T("LocReportTitle"));
            sb.AppendLine();
            foreach (var section in sections)
            {
                sb.AppendLine("【" + section.Title + "】");
                if (!string.IsNullOrEmpty(section.Lead))
                {
                    sb.AppendLine(section.Lead);
                }
                sb.AppendLine(section.Body);
                sb.AppendLine();
            }

            if (data != null && data.GenreByTime != null && data.GenreByTime.Count > 0)
            {
                sb.AppendLine(L10n.T("LocReportGenres"));
                for (var i = 0; i < Math.Min(5, data.GenreByTime.Count); i++)
                {
                    var g = data.GenreByTime[i];
                    sb.AppendLine(string.Format("  {0}. {1} — {2}（{3}）", i + 1, g.Name, g.ShareText, g.PlaytimeText));
                }
            }

            sb.AppendLine();
            sb.Append(L10n.F("LocShareFooter", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
            return sb.ToString();
        }
    }
}

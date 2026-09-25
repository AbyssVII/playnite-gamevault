using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameVault
{
    public enum ReportTone
    {
        Snarky,
        Formal
    }

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
    }

    /// <summary>
    /// 「你是一个什么样的玩家」报告生成器。
    ///
    /// 完全本地运行：不联网、不上传、不需要 API Key。
    /// 实现方式是"筛选 + 套模板"——先从 <see cref="ProfileFacts"/> 里
    /// 挑出最突出的几个特征（哪个档位人数最多、时长是不是高度集中、
    /// 未玩积压有多严重……），再按文风选一套句式把数字填进去。
    ///
    /// 之所以不做成"直接念数字"，是因为用户看过概览卡片后已经知道总数了；
    /// 报告的价值在于**给出解释和判断**（"你属于深挖型" / "这是典型的松鼠党"），
    /// 数字只是支撑这个判断的证据。
    /// </summary>
    public static class ProfileReport
    {
        public static List<ReportSection> Build(VaultData data, ReportTone tone)
        {
            var sections = new List<ReportSection>();
            if (data == null || data.Facts == null || data.Facts.TotalGames == 0)
                return sections;

            var f = data.Facts;
            var snarky = tone == ReportTone.Snarky;
            var zh = !L10n.IsEnglish;

            AddPlayerArchetype(sections, f, snarky, zh);
            AddPlayRhythm(sections, f, snarky, zh);
            AddFocus(sections, f, snarky, zh);
            AddBacklog(sections, f, snarky, zh);
            AddGenre(sections, data, f, snarky, zh);
            AddTaste(sections, f, snarky, zh);
            AddMomentum(sections, f, snarky, zh);
            AddCompany(sections, f, snarky, zh);

            return sections;
        }

        // ------------------------------------------------------------------
        // 各段落
        // ------------------------------------------------------------------

        /// <summary>第一段：给玩家贴一个"人设"标签。</summary>
        private static void AddPlayerArchetype(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            var playedRate = f.TotalGames > 0 ? (double)f.PlayedGames / f.TotalGames : 0;
            var deepRate = f.TotalGames > 0 ? (double)f.TierDeep / f.TotalGames : 0;

            string title, body;
            if (playedRate < 0.2 && f.TotalGames >= 20)
            {
                title = zh ? "囤积型玩家" : "The Collector";
                body = Style(snarky, zh,
                    "{0} 款游戏里只玩过 {1} 款，剩下 {2} 款安安静静躺在库里。你买游戏的时候买的其实是「我有一天会玩它」的那个想象 —— 而那个想象，目前还没到货。",
                    "You've played {1} of {0} games. The other {2} are still waiting patiently. What you bought was never the game — it was the fantasy of one day playing it, and that shipment is still in transit.",
                    "库存共 {0} 款，其中已游玩 {1} 款，未游玩 {2} 款。购买行为更多基于预期而非当下需求，属于典型的库存积累型消费模式。",
                    "The library holds {0} titles, {1} of which have been played and {2} of which have not. Purchases appear to be driven by intention rather than immediate demand — a classic accumulation pattern.",
                    f.TotalGames, f.PlayedGames, f.UnplayedGames);
            }
            else if (deepRate > 0.15)
            {
                title = zh ? "深挖型玩家" : "The Deep Diver";
                body = Style(snarky, zh,
                    "{0} 款游戏里有 {1} 款玩超过 100 小时，占比 {2}。你不太在意库有多大，你在意的是能不能一头扎进一款游戏里很久不出来 —— 对你来说，广度是别人的事。",
                    "{1} of your {0} games have passed 100 hours — {2} of the library. You don't care how wide the shelf is; you care whether you can dive into one game and stay under for a long time. Breadth is somebody else's problem.",
                    "{0} 款游戏中，有 {1} 款累计游玩超过 100 小时，占比 {2}。游玩行为呈现明显的纵向深入特征，而非横向覆盖。",
                    "Of {0} titles, {1} have exceeded 100 hours — {2} of the library. Play behaviour shows a clear preference for depth over breadth.",
                    f.TotalGames, f.TierDeep, Pct(deepRate));
            }
            else if (f.ScoredCount >= 15 && f.AverageScore >= 82)
            {
                title = zh ? "挑剔型玩家" : "The Connoisseur";
                body = Style(snarky, zh,
                    "库里有评分的 {0} 款游戏平均分 {1}，明显高于大盘。你大概率不会因为打折就手滑 —— 能被你收进来的，多少得有点东西。",
                    "Your {0} scored games average {1}, comfortably above the curve. You don't buy things just because they're on sale — if it made it into your library, it earned its place.",
                    "库内 {0} 款有 Metacritic 评分的游戏平均得分 {1}，显著高于同期平均水平，反映出入库决策对品质评价有较强依赖。",
                    "{0} scored titles average {1}, notably above the typical range — suggesting that quality ratings play a significant role in your acquisition decisions.",
                    f.ScoredCount, f.AverageScore.ToString("0.#"));
            }
            else if (playedRate > 0.75)
            {
                title = zh ? "实干型玩家" : "The Closer";
                body = Style(snarky, zh,
                    "{0} 的游戏你都真的开过。在这个平均只玩两成库存的时代，这个数字相当罕见 —— 你买游戏是为了玩，不是为了摆。",
                    "You've actually launched {0} of what you own. In an era where the average is closer to twenty, that's genuinely rare — you buy games to play them, not to display them.",
                    "{0} 的库存已实际启动过。这一比例明显高于常见水平，说明购买与游玩之间存在较强的直接关联。",
                    "{0} of your library has actually been launched — well above the typical figure, indicating a close link between buying and playing in your habits.",
                    Pct(playedRate));
            }
            else if (f.ActiveInPeriod > 0 && f.ActiveInPeriod <= 3 && f.TotalGames >= 30)
            {
                title = zh ? "独宠型玩家" : "The Monogamist";
                body = Style(snarky, zh,
                    "库里 {0} 款游戏，近两周你只碰了 {1} 款。别人在库里挑挑拣拣，你是认准了一个就从一而终 —— 或者只是最近太忙了。",
                    "{0} games in the library, and you've touched {1} of them in the past two weeks. Others browse; you commit. Or you've just been busy.",
                    "库存 {0} 款，近两周实际游玩 {1} 款。游玩焦点高度集中，短期内的注意力资源几乎全部投入在少数作品上。",
                    "With {0} titles available, only {1} saw play in the past two weeks — your short-term attention is heavily concentrated on a small number of games.",
                    f.TotalGames, f.ActiveInPeriod);
            }
            else
            {
                title = zh ? "均衡型玩家" : "The All-Rounder";
                body = Style(snarky, zh,
                    "{0} 款游戏、{1} 的游玩率、平均 {2} 的评分偏好 —— 各项指标都在中间偏上的位置。你不是某一类极端玩家，你是那种朋友问「最近玩什么」时最答得上来的人。",
                    "{0} games, a {1} play rate, an average score preference of {2} — everything sits comfortably above the middle. You're not an extreme of any kind; you're the friend who actually has an answer when someone asks what's good right now.",
                    "库存 {0} 款，游玩率 {1}，评分偏好均值 {2}，各项指标均处于中上区间，未呈现单一的极端倾向。",
                    "{0} titles, a {1} play rate, and an average score preference of {2} — every indicator sits in the upper-middle range without a dominant extreme.",
                    f.TotalGames, Pct(playedRate), f.ScoredCount > 0 ? f.AverageScore.ToString("0") : "—");
            }

            s.Add(new ReportSection
            {
                Icon = "\uE7FC",
                Color = "#4CC2FF",
                Title = title,
                Body = body,
                Metric = f.TotalGames.ToString(),
                MetricLabel = zh ? "款游戏" : "games"
            });
        }

        /// <summary>游玩节奏：一款游戏你通常玩多久、多久玩一次、是速通还是细水长流。</summary>
        private static void AddPlayRhythm(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            if (f.PlayedGames == 0)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE916",
                    Color = "#64748B",
                    Title = zh ? "还没有节奏可谈" : "No rhythm yet",
                    Body = Pick(zh,
                        "目前还没有任何一款游戏留下过时长记录，所以这一段先空着。等你开始玩，这里会画出你的日常节奏。",
                        "No game has recorded playtime yet, so this section stays empty. Once you start playing, your daily rhythm shows up here.")
                });
                return;
            }

            var median = TimeFmt.Short(f.MedianPlayedTime);
            var avg = TimeFmt.Short(f.AvgPlayedTime);
            var shortRate = f.PlayedGames > 0 ? (double)f.ShortGameCount / f.PlayedGames : 0;

            // 中位 vs 均值：中位远小于均值 → 大多数游戏浅尝，少数几款拉高平均数
            string title, body;
            if (f.AvgPlayedTime > 0 && f.MedianPlayedTime > 0
                && (double)f.AvgPlayedTime / f.MedianPlayedTime >= 2.5)
            {
                title = zh ? "少数几款撑起平均值" : "A few titles carry the average";
                body = Style(snarky, zh,
                    "你玩过的游戏里，中位数只有 {0}，平均却是 {1} —— 差了 {2} 倍。翻译一下：大部分游戏你尝一口就走，但有那么几款，你几乎住在了里面。",
                    "The median game you've played sits at {0}, but the average is {1} — a {2}× gap. Translation: you take one bite of most games and walk away, but a few of them you practically live inside.",
                    "已玩游戏的中位时长为 {0}，平均时长为 {1}，两者相差约 {2} 倍。表明多数作品为浅层接触，而少数作品贡献了绝大部分时长。",
                    "Median playtime among played games is {0} against an average of {1} — roughly a {2}× gap, indicating shallow contact with most titles and heavy investment in a few.",
                    median, avg, ((double)f.AvgPlayedTime / f.MedianPlayedTime).ToString("0.#"));
            }
            else
            {
                title = zh ? "节奏相当均匀" : "A remarkably even rhythm";
                body = Style(snarky, zh,
                    "中位 {0}、平均 {1}，两者咬得很紧。你对待游戏的态度比较一致 —— 不会因为某款特别火就一头扎进去，也不太会浅尝辄止。",
                    "Median {0}, average {1} — the two are tightly aligned. You treat games consistently: you don't dive headfirst into whatever's trending, and you rarely just skim.",
                    "中位时长 {0} 与平均时长 {1} 接近，说明各作品的投入程度分布较为均衡，未出现极端的长尾。",
                    "Median {0} and average {1} are close together, indicating a fairly even investment across titles with no extreme long tail.",
                    median, avg);
            }

            if (shortRate >= 0.5)
            {
                body += Style(snarky, zh,
                    " 顺带一提，你有 {0} 的已玩游戏停在 10 小时以内 —— 小品级作品在你这里很受欢迎，你显然不觉得「没玩够 100 小时」是种失败。",
                    " And note: {0} of the games you've played stopped inside ten hours. Short games clearly suit you — you don't treat that as a failure.",
                    " 其中停在 10 小时以内的作品占已玩游戏的 {0}，短时长作品占有较高比重。",
                    " {0} of played titles stopped within ten hours, so short-form games hold a substantial share of your habits.",
                    Pct(shortRate));
            }

            // 库龄 & 最近是否还在入库
            if (f.LibraryAgeDays > 0)
            {
                var years = f.LibraryAgeDays / 365.0;
                if (f.RecentlyAdded > 0 && years >= 2)
                {
                    body += Style(snarky, zh,
                        " 你的库已经陪伴你 {0} 年了，而最近两年还新进了 {1} 款 —— 收藏这件事，你一直在继续。",
                        " Your library has been with you for about {0} years, and {1} titles joined in the last two alone. The collecting hasn't stopped.",
                        " 库龄约 {0} 年，其中近两年新增 {1} 款，入库行为持续进行。",
                        " The library is roughly {0} years old, with {1} titles added in the last two — acquisition is ongoing.",
                        years.ToString("0.#"), f.RecentlyAdded);
                }
                else if (years >= 2)
                {
                    body += Style(snarky, zh,
                        " 这个库你已经攒了 {0} 年，最近两年却没怎么添新东西 —— 是收手了，还是该玩的都在里面了？",
                        " You've been building this library for {0} years, yet hardly anything new arrived in the last two. Either you've stopped, or everything worth having is already in there.",
                        " 库龄约 {0} 年，但近两年新增极少，入库行为明显放缓。",
                        " The library is roughly {0} years old, yet almost nothing was added in the last two — acquisition has slowed markedly.",
                        years.ToString("0.#"));
                }
            }

            s.Add(new ReportSection
            {
                Icon = "\uE9D9",
                Color = "#60A5FA",
                Title = title,
                Body = body,
                Metric = median,
                MetricLabel = zh ? "中位时长" : "median"
            });
        }

        /// <summary>收藏与社交倾向：库里真正"被你收藏"的有多少。</summary>
        private static void AddCompany(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            if (f.TotalGames < 5) return;

            var favRate = (double)f.FavoriteCount / f.TotalGames;
            var installedUnplayed = f.InstalledUnplayed;

            string title, body;
            if (f.FavoriteCount == 0)
            {
                title = zh ? "一视同仁" : "No favourites marked";
                body = Style(snarky, zh,
                    "{0} 款游戏，一个收藏都没标。你要么是那种「每一款都值得平等对待」的人，要么就是懒得点那个星 —— 通常后者居多。",
                    "{0} games and not a single one marked as a favourite. Either you believe all games deserve equal treatment, or you just never bother with the star. Usually the latter.",
                    "库内 {0} 款游戏均未标记为收藏，未表现出特别的偏好分层。",
                    "None of the {0} titles in the library are marked as favourites, showing no explicit preference tiering.",
                    f.TotalGames);
            }
            else if (favRate >= 0.3)
            {
                title = zh ? "收藏家心态" : "A collector's heart";
                body = Style(snarky, zh,
                    "你给 {0} 款游戏点了收藏，占整个库的 {1}。这个比例挺高的 —— 你对很多游戏都抱有真实的好感，而不只是「买来囤着」。",
                    "You've favourited {0} games — {1} of the whole library. That's a high share: you hold genuine affection for a lot of them, not just a hoarding instinct.",
                    "标记为收藏的游戏共 {0} 款，占库容 {1}，反映出较强的作品认同感。",
                    "{0} titles are marked as favourites — {1} of the library — reflecting strong attachment to a broad set of games.",
                    f.FavoriteCount, Pct(favRate));
            }
            else
            {
                title = zh ? "收藏很克制" : "Sparing with the star";
                body = Style(snarky, zh,
                    "只有 {0} 款进了你的收藏夹（{1}）。你的喜欢是有门槛的 —— 能把星点下去，说明那款游戏确实动了你。",
                    "Only {0} games earned your favourite mark ({1}). Your approval has a threshold: if the star went on, that game genuinely moved you.",
                    "标记为收藏的游戏 {0} 款，占库容 {1}，收藏标准较为严格。",
                    "{0} titles carry your favourite mark ({1}) — your approval comes with a high threshold.",
                    f.FavoriteCount, Pct(favRate));
            }

            s.Add(new ReportSection
            {
                Icon = "\uE735",
                Color = "#34D399",
                Title = title,
                Body = body,
                Metric = f.FavoriteCount.ToString(),
                MetricLabel = zh ? "款收藏" : "favourites"
            });
        }

        /// <summary>时长集中度：是把时间摊开了，还是全砸在几款上。</summary>
        private static void AddFocus(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            if (f.TotalPlaytime == 0)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE916",
                    Color = "#64748B",
                    Title = zh ? "还没有时长记录" : "No playtime yet",
                    Body = Pick(zh,
                        "库里暂时没有累计时长，所以这一项无从谈起。等你玩起来之后，这里会告诉你时间都花到哪去了。",
                        "There's no accumulated playtime yet, so there's nothing to measure. Once you start playing, this section will show exactly where the hours went.")
                });
                return;
            }

            var top1 = f.Top1Time;
            string title, body;
            if (f.Top1Share >= 0.35)
            {
                title = zh ? "一人扛起整片天" : "One game carries it all";
                body = Style(snarky, zh,
                    "《{0}》一款就吃掉你 {1} 的总时长（{2}），Top 10 加起来占了 {3}。你的游戏库看起来很大，实际上是被一款游戏统治着的。",
                    "\"{0}\" alone accounts for {1} of your total playtime ({2}), and your top ten take {3}. The library looks big, but in practice it's ruled by a single game.",
                    "时长最高的一款为《{0}》，占总时长 {1}（{2}）；前 10 款合计占 {3}。时间投入高度集中。",
                    "Your single most-played title, \"{0}\", accounts for {1} of total playtime ({2}); the top ten together take {3} — a strongly concentrated distribution.",
                    f.Top1Name, Pct(f.Top1Share), TimeFmt.Short(top1), Pct(f.Top10Share));
            }
            else if (f.Top1Share >= 0.15)
            {
                title = zh ? "有主心骨，也有周边" : "A main course and its sides";
                body = Style(snarky, zh,
                    "最肝的《{0}》占了 {1}，Top 10 合计 {2}。你有明确的常驻游戏，但也留了足够的空间给新东西 —— 这种结构挺健康。",
                    "\"{0}\" takes {1} and your top ten take {2}. You have a clear standby game but still leave room for new things — that's a healthy shape.",
                    "时长最高的《{0}》占 {1}，前 10 款合计占 {2}。既有稳定的长期投入对象，也为新作品保留了空间。",
                    "\"{0}\" takes {1} and your top ten take {2} — a stable long-term anchor with room left for new titles.",
                    f.Top1Name, Pct(f.Top1Share), Pct(f.Top10Share));
            }
            else
            {
                title = zh ? "时间摊得很开" : "Evenly spread";
                body = Style(snarky, zh,
                    "最肝的《{0}》也只占 {1}，Top 10 合计 {2}。你的时间没有砸在任何一款上，几乎是在库里均匀漫游 —— 要么你口味很杂，要么你换游戏比换衣服还勤。",
                    "Even your most-played game, \"{0}\", is only {1}, and the top ten total {2}. Nothing dominates; you drift evenly across the shelf. Either your taste is very broad, or you change games like socks.",
                    "时长最高的《{0}》仅占 {1}，前 10 款合计占 {2}。时间分布相当均匀，未出现明显的头部集中。",
                    "Even your most-played game, \"{0}\", is only {1}, and the top ten total {2} — an unusually even distribution with no dominant title.",
                    f.Top1Name, Pct(f.Top1Share), Pct(f.Top10Share));
            }

            s.Add(new ReportSection
            {
                Icon = "\uE9D9",
                Color = "#A78BFA",
                Title = title,
                Body = body,
                Metric = TimeFmt.Short(f.TotalPlaytime),
                MetricLabel = zh ? "累计时长" : "total time"
            });
        }

        /// <summary>未玩积压。</summary>
        private static void AddBacklog(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            var backlogRate = f.TotalGames > 0 ? (double)f.UnplayedGames / f.TotalGames : 0;

            string title, body;
            if (f.UnplayedGames == 0)
            {
                title = zh ? "零积压" : "Zero backlog";
                body = Style(snarky, zh,
                    "库里每一款游戏你都开过至少一次。这在收集游戏的人里属于稀有物种，请珍惜。",
                    "Every single game in your library has been launched at least once. Among people who collect games, you're a rare species. Treasure it.",
                    "库存中所有游戏均已被启动过至少一次，不存在完全未接触的作品。",
                    "Every title in the library has been launched at least once — there are no entirely untouched games.",
                    new object[0]);
            }
            else if (backlogRate >= 0.7)
            {
                title = zh ? "积压重灾区" : "A backlog emergency";
                body = Style(snarky, zh,
                    "{0} 款未玩，占库存的 {1}。按你现在每周玩一款的速度，清空它们大概需要 —— 还是别算了，算了会难过。",
                    "{0} unplayed games — {1} of the library. At your current pace, clearing them would take roughly… actually, let's not do that math. The math is upsetting.",
                    "未游玩游戏 {0} 款，占库存 {1}。按当前推进速度，清理周期将显著超出合理范围。",
                    "{0} unplayed titles make up {1} of the library — at the present pace, clearing them would take considerably longer than is practical.",
                    f.UnplayedGames, Pct(backlogRate));
            }
            else if (backlogRate >= 0.4)
            {
                title = zh ? "积压在可控范围" : "A manageable backlog";
                body = Style(snarky, zh,
                    "{0} 款没玩过，占 {1}。这个比例在游戏玩家群体里其实算相当克制的了，说明你下手之前多少还是会想一想。",
                    "{0} games untouched, {1} of the library. By gamer standards that's actually restrained — evidence that you think before you click buy.",
                    "未游玩游戏 {0} 款，占库存 {1}。该比例处于常见区间，购买决策相对克制。",
                    "{0} untouched titles, {1} of the library — a rate within the common range, suggesting relatively disciplined purchasing.",
                    f.UnplayedGames, Pct(backlogRate));
            }
            else
            {
                title = zh ? "积压很轻微" : "Barely any backlog";
                body = Style(snarky, zh,
                    "只有 {0} 款没玩过（{1}）。你的库基本处在「买了就玩」的状态，是那种会让打折季销售额下降的顾客。",
                    "Only {0} untouched ({1}). Your library is largely in a bought-it-played-it state, and you are the kind of customer who ruins a sale season.",
                    "未游玩游戏仅 {0} 款（{1}），库存整体处于购买后即游玩的状态。",
                    "Only {0} untouched titles ({1}) — the library is largely in a bought-then-played state.",
                    f.UnplayedGames, Pct(backlogRate));
            }

            s.Add(new ReportSection
            {
                Icon = "\uE7BA",
                Color = "#F472B6",
                Title = title,
                Body = body,
                Metric = f.UnplayedGames.ToString(),
                MetricLabel = zh ? "款未玩" : "unplayed"
            });
        }

        /// <summary>类型偏好。</summary>
        private static void AddGenre(List<ReportSection> s, VaultData data, ProfileFacts f, bool snarky, bool zh)
        {
            if (data.GenreByTime == null || data.GenreByTime.Count == 0)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE8EC",
                    Color = "#64748B",
                    Title = zh ? "类型数据缺失" : "No genre data",
                    Body = Pick(zh,
                        "库里没有足够的类型标签，所以分析不出你的口味。去给游戏补上「类型」，这份报告会立刻变得有说服力。",
                        "There aren't enough genre tags in your library to read your taste. Tag your games and this report becomes considerably more convincing.")
                });
                return;
            }

            var top = data.GenreByTime.Take(3).ToList();
            var names = top.Select(g => g.Name).ToList();
            var joined = JoinNames(names, zh);

            var body = Style(snarky, zh,
                "时长占比最高的三个类型是 {0}。其中《{1}》这一类占了 {2} —— 你嘴上说「什么都玩」，但数据说你的时间大部分给了它。",
                "Your top three genres by playtime are {0}. {1} alone takes {2} — you may say you'll play anything, but the data says most of your hours went to that one.",
                "时长占比最高的三个类型依次为 {0}，其中 {1} 占比 {2}，构成主要的类型倾向。",
                "The three leading genres by playtime are {0}; {1} alone accounts for {2}, forming the dominant genre preference.",
                joined, top[0].Name, top[0].ShareText);

            // 类型最多的那款游戏，作为"代表人物"
            if (f.TopGameNames.Count > 0)
            {
                var champion = f.TopGameNames[0];
                body += Style(snarky, zh,
                    " 最能代表这个口味的大概是《{0}》。",
                    " If one game sums up that taste, it's \"{0}\".",
                    " 该倾向的代表性作品为《{0}》。",
                    " The title most representative of this preference is \"{0}\".",
                    champion);
            }

            s.Add(new ReportSection
            {
                Icon = "\uE8EC",
                Color = "#2DD4BF",
                Title = zh ? "你的口味偏向" : "Where your taste lies",
                Body = body,
                Metric = top[0].ShareText,
                MetricLabel = top[0].Name
            });
        }

        /// <summary>评分偏好：你是吃高分还是随便玩。</summary>
        private static void AddTaste(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            if (f.ScoredCount < 5)
            {
                s.Add(new ReportSection
                {
                    Icon = "\uE735",
                    Color = "#64748B",
                    Title = zh ? "样本不足" : "Not enough data",
                    Body = Pick(zh,
                        "只有 {0} 款游戏有 Metacritic 评分，样本太少，不好判断你是不是个看分数买游戏的人。",
                        "Only {0} games carry a Metacritic score — too few to tell whether you're the type to buy by the number.",
                        f.ScoredCount)
                });
                return;
            }

            var modernRate = f.TotalGames > 0 ? (double)f.ModernCount / f.TotalGames : 0;
            var highShare = (double)f.HighScoreCount / f.ScoredCount;
            var lowShare = (double)f.LowScoreCount / f.ScoredCount;

            string title, body;
            if (highShare >= 0.4)
            {
                title = zh ? "口碑优先" : "Reputation first";
                body = Style(snarky, zh,
                    "{0} 款有评分的游戏里，{1} 款拿到 85 分以上（{2}）。你基本只收公认的好货，踩雷概率很低 —— 代价是可能会错过一些有意思的小众作品。",
                    "{1} of your {0} scored games sit at 85 or above ({2}). You mostly buy the consensus greats, which keeps your hit rate high — at the cost of maybe missing something strange and good.",
                    "{0} 款有评分的游戏中，{1} 款得分高于 85（{2}）。选品明显偏向高评价作品，同时对低分作品的接触率较低。",
                    "{1} of {0} scored titles rate 85 or above ({2}) — a clear bias toward highly rated releases, with limited exposure to lower-scored works.",
                    f.ScoredCount, f.HighScoreCount, Pct(highShare));
            }
            else if (lowShare >= 0.4)
            {
                title = zh ? "什么都吃得下" : "An omnivore";
                body = Style(snarky, zh,
                    "{0} 款有评分的游戏里，{1} 款在 70 分以下（{2}）。你显然不靠评分挑游戏 —— 要么你专挑冷门挖宝，要么你就是那个在别人都说烂的时候依然玩得开心的人。",
                    "{1} of your {0} scored games are below 70 ({2}). You clearly don't shop by review score — either you're a dedicated delver of the obscure, or you're the person happily playing what everyone else called a mess.",
                    "{0} 款有评分的游戏中，{1} 款低于 70 分（{2}）。评分对该玩家的选品影响有限，类型与题材的权重更高。",
                    "{1} of {0} scored titles fall below 70 ({2}) — review scores appear to carry limited weight in your selection, with genre and subject matter mattering more.",
                    f.ScoredCount, f.LowScoreCount, Pct(lowShare));
            }
            else
            {
                title = zh ? "口味居中" : "Middle of the road";
                body = Style(snarky, zh,
                    "库里有评分的游戏平均 {0} 分，高分和低分都不算多。你大概属于「看题材和心情」那类人，评分只是参考项之一。",
                    "Your scored games average {0}. Neither a trophy hunter nor a junk collector — you probably pick by subject matter and mood, with scores as just one input.",
                    "有评分游戏的平均得分为 {0}，分布集中在中间区间，未表现出明显的高分或低分偏好。",
                    "Scored titles average {0}, clustered in the middle band — no pronounced preference for either highly or poorly rated games.",
                    f.AverageScore.ToString("0.#"));
            }

            if (modernRate >= 0.6 && f.NewestReleaseYear > 0)
            {
                body += Style(snarky, zh,
                    " 另外，{0} 的游戏是近五年发行的，你明显更愿意待在当下。",
                    " Also, {0} of your library came out in the last five years — you clearly prefer to live in the present.",
                    " 此外，近五年发行的作品占 {0}，对新作的接受度较高。",
                    " In addition, {0} of the library was released within the last five years, indicating a strong preference for recent titles.",
                    Pct(modernRate));
            }
            else if (f.OldestReleaseYear > 0 && f.OldestReleaseYear <= 2005)
            {
                body += Style(snarky, zh,
                    " 顺带一提，你库里的老作品能追溯到 {0} 年，有些东西确实经得起时间。",
                    " Your oldest title dates back to {0} — some things really do hold up.",
                    " 库内最早的发行年份为 {0}，长周期作品仍占一定比重。",
                    " The earliest release in the library dates to {0}, so long-lived titles still hold a share.",
                    f.OldestReleaseYear);
            }

            s.Add(new ReportSection
            {
                Icon = "\uE735",
                Color = "#FBBF24",
                Title = title,
                Body = body,
                Metric = f.AverageScore.ToString("0.#"),
                MetricLabel = zh ? "平均分" : "avg. score"
            });
        }

        /// <summary>近期活跃度：现在还在玩吗。</summary>
        private static void AddMomentum(List<ReportSection> s, ProfileFacts f, bool snarky, bool zh)
        {
            var recentShare = f.TotalPlaytime > 0 ? (double)f.RecentPlaytime / f.TotalPlaytime : 0;

            string title, body;
            if (f.RecentPlaytime == 0)
            {
                title = zh ? "暂时停滞" : "Currently dormant";
                body = zh
                    ? "近两周的时长统计是零。可能是最近确实没空，也可能是你正在酝酿一次回归 —— 不管哪种，你的库还在等着。"
                    : "Zero recorded playtime in the past two weeks. Maybe life got busy, or maybe you're gearing up for a return. Either way, the library is still waiting.";
            }
            else if (f.ActiveInPeriod >= 8)
            {
                title = zh ? "多线操作" : "Juggling several";
                body = Style(snarky, zh,
                    "近两周你碰了 {0} 款游戏，合计 {1}。你不是那种一款玩到通关再换的人 —— 你的注意力是分散的，而且看起来乐在其中。",
                    "You've played {0} different games in the past two weeks, {1} in total. You're not a finish-one-then-start-the-next person; your attention is scattered and you seem fine with it.",
                    "近两周游玩游戏 {0} 款，累计时长 {1}。短期内并行推进多款作品，注意力分布较分散。",
                    "{0} titles were played in the past two weeks for a total of {1} — several games progressed in parallel, with attention spread across them.",
                    f.ActiveInPeriod, TimeFmt.Short(f.RecentPlaytime));
            }
            else if (recentShare >= 0.1)
            {
                title = zh ? "热得发烫" : "Running hot";
                body = Style(snarky, zh,
                    "近两周的 {0} 占了你历史总时长的 {1}。这个强度说明你最近正处在一款游戏的蜜月期 —— 珍惜这种感觉。",
                    "{0} in the past two weeks is {1} of your entire recorded history. You're in the honeymoon phase with something. Enjoy it.",
                    "近两周时长 {0}，占总时长的 {1}，近期活跃度明显高于历史平均水平。",
                    "{0} in the last two weeks represents {1} of your total recorded playtime — recent activity is well above your historical average.",
                    TimeFmt.Short(f.RecentPlaytime), Pct(recentShare));
            }
            else
            {
                title = zh ? "细水长流" : "Steady as she goes";
                body = Style(snarky, zh,
                    "近两周玩了 {0}，集中在 {1} 款上。节奏不快，但一直没断 —— 这种长期主义者往往才是最后通关最多的人。",
                    "{0} over two weeks, spread across {1} games. Not a sprint, but never a full stop — the long-haul players are usually the ones who finish the most in the end.",
                    "近两周游玩 {1} 款，累计 {0}。游玩节奏平稳，长期持续性良好。",
                    "{0} of play across {1} games in the past two weeks — a steady, continuous rhythm rather than a burst.",
                    TimeFmt.Short(f.RecentPlaytime), f.ActiveInPeriod);
            }

            s.Add(new ReportSection
            {
                Icon = "\uE823",
                Color = "#4CC2FF",
                Title = title,
                Body = body,
                Metric = f.ActiveInPeriod.ToString(),
                MetricLabel = zh ? "款近期活跃" : "recently active"
            });
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

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
        /// 刻意做成一个方法而不是写 `zh ? Fmt(中, args) : Fmt(英)`：后者极易写错 ——
        /// 三元表达式的两个分支各是一次独立调用，很容易只给中文分支传参数，
        /// 英文那半就会因为 Format 抛异常而原样吐出带 {0} 的模板（本项目踩过）。
        /// 放在一起调用，参数只有一份，两边不可能写岔。
        /// </summary>
        private static string Pick(bool zh, string chinese, string english, params object[] args)
        {
            return Fmt(zh ? chinese : english, args);
        }

        /// <summary>
        /// 文风选择：先在「锐评」和「正式」两组措辞里选一套，再按语言选一套，
        /// 最后统一格式化。参数同样只有一份，避免上面那个坑。
        ///
        /// 之所以不是"锐评 = 原文 + 正式 = 重新写一遍"，而是两套完整文案：
        /// 两种文风的差别不只是语气词，"毒舌"版会下判断（"这是典型的松鼠党"），
        /// 正式版只陈述事实与推论，句式结构完全不同，没法靠替换词实现。
        /// </summary>
        private static string Style(bool snarky, bool zh,
            string snarkyZh, string snarkyEn, string formalZh, string formalEn,
            params object[] args)
        {
            var template = snarky
                ? (zh ? snarkyZh : snarkyEn)
                : (zh ? formalZh : formalEn);
            return Fmt(template, args);
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

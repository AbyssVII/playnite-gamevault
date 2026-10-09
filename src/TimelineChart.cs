using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameVault
{
    /// <summary>
    /// 「游玩时间轴」——按年份横向铺开的每周柱状图。
    ///
    /// 与前版的区别（用户要求）：
    ///   ① 不再压缩到容器宽度 —— 每周固定步长（<see cref="WeekStep"/>），
    ///      一年 52~53 周自然排成一条**长条**，由外层 ScrollViewer 横向拖动查看；
    ///   ② 悬停某周时弹出的信息卡里带**游戏封面缩略图 + 名字**，卡片对比度更高；
    ///   ③ 视觉走"科技感"：柱子用上亮下暗的纵向渐变、基线上有一层淡光晕、
    ///      峰值周顶部有一枚高亮点、月份分隔用点线而不是实线。
    ///
    /// 仍然自己画（不用 ItemsControl）的原因和老版本一样：
    ///   ① 周序号、月份分隔线要和柱子严格对齐，交给布局系统会被 Margin 一点点挪歪；
    ///   ② 悬停要按 x 坐标反查是哪一周；
    ///   ③ 步长变化时要连续重算，自绘只多一次 InvalidateVisual。
    /// </summary>
    public class TimelineChart : FrameworkElement
    {
        // ---- 布局常量 ----
        private const double MonthLabelHeight = 40;   // 顶部月份标签区（要放下错位的两行标签）
        private const double WeekLabelHeight = 34;    // 底部周标签区（周号 + 日期两行）
        private const double BarAreaTopPad = 12;      // 柱形区顶部留白
        private const double WeekStep = 30;           // 每周固定步长（决定整条时间轴的长度）
        private const double MinBarWidth = 6;
        private const double BarGapRatio = 0.42;      // 柱间空隙占步长的比例
        private const double ContentPadLeft = 24;     // 左侧留白（第一个月标签不被切掉）
        private const double ContentPadRight = 16;

        // ---- 配色（科技感：蓝青为主，峰值/本周用暖色点缀）----
        private static readonly Color BarTop = Color.FromRgb(0x7A, 0xD8, 0xFF);
        private static readonly Color BarBottom = Color.FromRgb(0x2B, 0x8C, 0xD6);
        private static readonly Color BarTopDim = Color.FromRgb(0x39, 0x4C, 0x63);
        private static readonly Color BarBottomDim = Color.FromRgb(0x25, 0x33, 0x45);
        private static readonly Color CurrentTop = Color.FromRgb(0x6E, 0xEA, 0xB8);
        private static readonly Color CurrentBottom = Color.FromRgb(0x1F, 0xA8, 0x77);
        private static readonly Color PeakColor = Color.FromRgb(0xF5, 0xB9, 0x42);
        private static readonly Color TextColor = Color.FromRgb(0x8A, 0x99, 0xAD);
        private static readonly Color TextBright = Color.FromRgb(0xEC, 0xF3, 0xFA);
        private static readonly Color GridColor = Color.FromRgb(0x1E, 0x28, 0x36);
        private static readonly Color MonthLineColor = Color.FromRgb(0x2E, 0x3C, 0x50);
        private static readonly Color BaselineGlow = Color.FromRgb(0x2A, 0x6C, 0xA0);

        private TimelineData data = new TimelineData();
        private double[] barX = new double[0];       // 每根柱的左边界
        private double contentWidth;                  // 整条时间轴需要的宽度
        private int hoverIndex = -1;
        private int peakIndex = -1;

        public TimelineChart()
        {
            SnapsToDevicePixels = true;
            // 最小高度要跟外层那一行的 MinHeight 相容：卡片自身有 28px 上下 padding + 约 36px 标题行，
            // 行高 170 时留给图表的只有 ~106px。这里设 72 留足余量，否则拖到最矮会把底部周标签裁掉。
            MinHeight = 72;
            // 长条要能横向拖动，所以控件本身不裁剪鼠标 —— 交给外层 ScrollViewer 滚
            ClipToBounds = false;
        }

        /// <summary>整条时间轴（含左右留白）需要的宽度。外层 ScrollViewer 用它撑开可滚动区域。</summary>
        public double ContentWidth
        {
            get { return contentWidth; }
        }

        public void SetData(TimelineData value)
        {
            data = value ?? new TimelineData();
            hoverIndex = -1;
            peakIndex = -1;
            Recalc();
            InvalidateVisual();
        }

        /// <summary>
        /// 强制指定"当前悬停第几周"，-1 表示不悬停。
        /// 仅供**离线渲染/单元测试**用来把信息卡画出来核对排版，运行时鼠标事件自己维护 hoverIndex。
        /// </summary>
        public void SetHoverForTest(int index)
        {
            hoverIndex = index;
            InvalidateVisual();
        }

        // ------------------------------------------------------------------
        // 命中测试
        // ------------------------------------------------------------------

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var index = HitWeek(e.GetPosition(this));
            if (index == hoverIndex) return;
            hoverIndex = index;
            Cursor = index >= 0 ? System.Windows.Input.Cursors.Hand : null;
            InvalidateVisual();   // 悬停信息卡要跟着重画
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex == -1) return;
            hoverIndex = -1;
            InvalidateVisual();
        }

        /// <summary>按 x 找命中的周：命中柱子本体或它所在的整个"列"都算，
        /// 否则用户要精准压在细柱子上才能看到提示，太挑手。</summary>
        private int HitWeek(Point point)
        {
            if (data == null || data.Weeks == null || barX.Length == 0) return -1;
            for (var i = 0; i < barX.Length; i++)
            {
                var columnLeft = ContentPadLeft + WeekStep * i;
                if (point.X >= columnLeft && point.X <= columnLeft + WeekStep) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------------
        // 布局计算
        // ------------------------------------------------------------------

        private void Recalc()
        {
            var count = data != null && data.Weeks != null ? data.Weeks.Count : 0;
            barX = new double[count];
            peakIndex = -1;
            if (count == 0)
            {
                contentWidth = 0;
                Width = double.NaN;
                return;
            }

            var barWidth = WeekStep * (1 - BarGapRatio);
            for (var i = 0; i < count; i++)
                barX[i] = ContentPadLeft + WeekStep * i + (WeekStep - barWidth) / 2;

            // 峰值周（用来点那枚高亮点）
            ulong peak = 0;
            for (var i = 0; i < count; i++)
                if (data.Weeks[i].TotalSeconds > peak)
                {
                    peak = data.Weeks[i].TotalSeconds;
                    peakIndex = i;
                }

            contentWidth = ContentPadLeft + WeekStep * count + ContentPadRight;
            Width = contentWidth;   // 关键：撑大自身宽度，外层 ScrollViewer 才会出现横向滚动条
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            // 宽度由我们自己定，尺寸变化只影响高度，不需要重算 x 布局
            InvalidateVisual();
        }

        // ------------------------------------------------------------------
        // 绘制
        // ------------------------------------------------------------------

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (data == null || !data.Available || data.Weeks == null || data.Weeks.Count == 0)
                return;
            if (barX.Length != data.Weeks.Count) Recalc();
            if (barX.Length == 0) return;

            var h = ActualHeight;

            // 顶部月份区 / 底部周标签区是按"空间充足"设计的固定高度。当用户把视窗拖矮时，
            // 这两个固定区会挤掉柱形区（甚至让柱形区变成负数）。所以这里按实际高度做收缩：
            //   ① 底部周标签至少要留得住两行文字（周号 + 日期），下限 26px；
            //   ② 顶部月份区可以从"两行错位标签"的 40px 一路压到只剩一行（14px），
            //      压缩后第二行的月份接受被裁（这是极端矮时的可接受降级）；
            //   ③ 柱形区优先保证 minBarZone。
            var monthZone = MonthLabelHeight;
            var weekZone = WeekLabelHeight;
            const double minBarZone = 40;
            const double weekFloor = 26;
            const double monthFloor = 14;

            var overflow = (MonthLabelHeight + WeekLabelHeight + BarAreaTopPad + minBarZone) - h;
            if (overflow > 0)
            {
                // 先从"顶部的富余"里扣（月份区可从 40 压到 14），扣完还有富余再压底部。
                var monthSlack = MonthLabelHeight - monthFloor;
                var take = Math.Min(overflow, monthSlack);
                monthZone = MonthLabelHeight - take;
                overflow -= take;

                if (overflow > 0)
                {
                    var weekSlack = WeekLabelHeight - weekFloor;
                    var take2 = Math.Min(overflow, weekSlack);
                    weekZone = WeekLabelHeight - take2;
                }
            }

            var barBottom = h - weekZone;
            var barTop = monthZone + BarAreaTopPad;
            // 兜底：极端矮的时候也别让区间翻转，至少留出可读的柱高
            if (barBottom - barTop < minBarZone) barTop = Math.Max(0, barBottom - minBarZone);
            var barMaxHeight = Math.Max(16, barBottom - barTop);

            DrawGrid(dc, barTop, barBottom);
            DrawBaseline(dc, barTop, barBottom);
            DrawMonthSeparators(dc, barTop, barBottom);
            DrawBars(dc, barTop, barBottom, barMaxHeight);
            DrawWeekLabels(dc, barBottom);
            DrawHoverCard(dc, barTop, barBottom, h);
        }

        /// <summary>水平参考网格 + 峰值高光层，让"高矮"更容易读。</summary>
        private void DrawGrid(DrawingContext dc, double barTop, double barBottom)
        {
            if (contentWidth <= 0) return;
            var pen = new Pen(new SolidColorBrush(GridColor), 1) { DashStyle = DashStyles.Dot };
            pen.Freeze();
            for (var i = 1; i <= 3; i++)
            {
                var y = barBottom - (barBottom - barTop) * i / 4.0;
                dc.DrawLine(pen, new Point(0, y), new Point(contentWidth, y));
            }
        }

        private void DrawBaseline(DrawingContext dc, double barTop, double barBottom)
        {
            // 底色线 + 一层淡光晕：基线看起来像会发光的电路板底边
            var glow = new SolidColorBrush(Color.FromArgb(0x55, BaselineGlow.R, BaselineGlow.G, BaselineGlow.B));
            glow.Freeze();
            dc.DrawRectangle(glow, null, new Rect(0, barBottom - 1, contentWidth, 3));

            var pen = new Pen(new SolidColorBrush(GridColor), 1.2);
            pen.Freeze();
            dc.DrawLine(pen, new Point(0, barBottom + 0.5), new Point(contentWidth, barBottom + 0.5));
        }

        private void DrawMonthSeparators(DrawingContext dc, double barTop, double barBottom)
        {
            if (data.Months == null) return;
            var pen = new Pen(new SolidColorBrush(MonthLineColor), 1) { DashStyle = DashStyles.Dash };
            pen.Freeze();

            // 每个月都要显示出来，但"3 月 200h 30m"这种标签在 30px 的周步长下相互挨得很近。
            // 早先的做法是"和前一个标签重叠就整条跳过"—— 结果有的月份直接不显示（用户反馈的 3 月）。
            // 现在改成**两行错位摆放**：第 1 行放得下就放，放不下退到第 2 行；
            // 两行都放不下才跳过（实际几乎不会发生 —— 短标签已经足够窄）。
            // 每一行各记一个"右边界"，两行互不干扰。
            var rowRight = new[] { double.NegativeInfinity, double.NegativeInfinity };
            var rowY = new[] { 3.0, 17.0 };   // 两行的基线 y

            foreach (var month in data.Months)
            {
                // 只用短标签（"9 月"）—— 年份已在标题栏的年份选择器里给出，不必每个月重复。
                var label = MakeText(month.ShortLabel ?? month.Label, 11.5, TextColor);
                var total = string.IsNullOrEmpty(month.TotalText)
                    ? null
                    : MakeText(month.TotalText, 10.5, TextColor);

                var index = month.StartIndex;
                var x = ContentPadLeft + WeekStep * index - (WeekStep * BarGapRatio) / 2;

                if (index > 0)
                    dc.DrawLine(pen, new Point(x, barTop - 6), new Point(x, barBottom));

                // 月份强调刻度（始终画，作为月份起点的锚）
                var tick = new SolidColorBrush(Color.FromArgb(0xAA, BarTop.R, BarTop.G, BarTop.B));
                tick.Freeze();
                var tickX = x + (index > 0 ? 5 : 2);
                dc.DrawRectangle(tick, null, new Rect(tickX, 7, 3, 3));

                var tx = tickX + 7;
                var labelRight = tx + label.Width + (total != null ? total.Width + 7 : 0);

                // 分两行挑一个放得下的槽位
                var row = -1;
                for (var r = 0; r < 2; r++)
                    if (tx >= rowRight[r] + 8) { row = r; break; }
                if (row < 0) continue;   // 两行都挤不下（罕见）

                dc.DrawText(label, new Point(tx, rowY[row]));
                if (total != null)
                    dc.DrawText(total, new Point(tx + label.Width + 7, rowY[row] + 1));
                rowRight[row] = labelRight;
            }
        }

        private void DrawBars(DrawingContext dc, double barTop, double barBottom, double barMaxHeight)
        {
            var weeks = data.Weeks;
            var peak = data.PeakSeconds;
            if (peak == 0) return;
            var barWidth = WeekStep * (1 - BarGapRatio);

            for (var i = 0; i < weeks.Count; i++)
            {
                var week = weeks[i];
                if (week.TotalSeconds == 0)
                {
                    // 空周画一根细小的"基座"，让时间轴的节奏感连续
                    var stubBrush = new SolidColorBrush(BarBottomDim);
                    stubBrush.Freeze();
                    dc.DrawRoundedRectangle(stubBrush, null,
                        new Rect(barX[i], barBottom - 2.5, barWidth, 2.5), 1.5, 1.5);
                    continue;
                }

                var height = Math.Max(4, barMaxHeight * week.TotalSeconds / peak);
                var rect = new Rect(barX[i], barBottom - height, barWidth, height);

                Color top, bottom;
                if (week.IsCurrent) { top = CurrentTop; bottom = CurrentBottom; }
                else if (i == peakIndex) { top = TopBright(PeakColor); bottom = PeakColor; }
                else { top = BarTop; bottom = BarBottom; }

                if (i == hoverIndex)
                {
                    top = Lighten(top, 46);
                    bottom = Lighten(bottom, 46);
                }

                var brush = new LinearGradientBrush(top, bottom, 90);
                brush.Freeze();

                // 悬停的那根带一点外发光，指示"就是这根"
                if (i == hoverIndex)
                {
                    var halo = new SolidColorBrush(Color.FromArgb(0x40, top.R, top.G, top.B));
                    halo.Freeze();
                    dc.DrawRoundedRectangle(halo, null,
                        new Rect(rect.X - 2.5, rect.Y - 2.5, rect.Width + 5, rect.Height + 5), 4.5, 4.5);
                }

                dc.DrawRoundedRectangle(brush, null, rect, 3, 3);

                // 峰值周顶上点一颗高亮，一眼找到"这一年最疯的一周"
                if (i == peakIndex && i != hoverIndex)
                {
                    var dot = new SolidColorBrush(PeakColor);
                    dot.Freeze();
                    dc.DrawEllipse(dot, null, new Point(rect.X + rect.Width / 2, rect.Y - 4), 2.6, 2.6);
                }
            }
        }

        private static Color TopBright(Color c)
        {
            return Lighten(c, 60);
        }

        private static Color Lighten(Color color, int amount)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, color.R + amount),
                (byte)Math.Min(255, color.G + amount),
                (byte)Math.Min(255, color.B + amount));
        }

        private void DrawWeekLabels(DrawingContext dc, double barBottom)
        {
            var weeks = data.Weeks;
            var barWidth = WeekStep * (1 - BarGapRatio);
            // 步长固定 30px，标签能逐周放下（周号一行 + 日期一行）
            for (var i = 0; i < weeks.Count; i++)
            {
                var week = weeks[i];
                var isCurrent = week.IsCurrent;
                var center = barX[i] + barWidth / 2;

                if (isCurrent)
                {
                    // 本周：加一个小胶囊把它框出来
                    var pill = new SolidColorBrush(Color.FromArgb(0x33, CurrentTop.R, CurrentTop.G, CurrentTop.B));
                    pill.Freeze();
                    var pillRect = new Rect(center - 16, barBottom + 4, 32, 25);
                    dc.DrawRoundedRectangle(pill, null, pillRect, 6, 6);
                }

                var weekNo = MakeText(week.WeekOfYear.ToString(CultureInfo.InvariantCulture), 11,
                    isCurrent ? CurrentTop : (week.TotalSeconds > 0 ? TextBright : TextColor));
                dc.DrawText(weekNo, new Point(center - weekNo.Width / 2, barBottom + 5));

                var label = MakeText(week.WeekLabel, 9.5, isCurrent ? CurrentTop : TextColor);
                dc.DrawText(label, new Point(center - label.Width / 2, barBottom + 18));
            }
        }

        // ------------------------------------------------------------------
        // 悬停信息卡：游戏封面 + 名字 + 时长
        // ------------------------------------------------------------------

        private const double CardMinW = 236;
        private const double CardMaxW = 380;
        private const double CardPadX = 12;
        private const double CoverW = 44;
        private const double CoverH = 58;

        private void DrawHoverCard(DrawingContext dc, double barTop, double barBottom, double h)
        {
            if (hoverIndex < 0 || hoverIndex >= data.Weeks.Count) return;
            var week = data.Weeks[hoverIndex];

            // ---- 先量文字，按最长一行决定卡片宽度 ----
            // （用户要求"显示游戏全名"，所以名字不再截成一行：卡片宽度跟着名字走，
            //   放不下就换行，最多三行，这样长名字也能完整显示。）
            var nameText = week.Top != null ? (week.Top.Name ?? "") : "";
            // 卡片顶部显示"这是哪一周"，用整周区间（7/20-7/26）而不是单个起始日（7/20），
            // 这样鼠标移到柱子上时一眼就知道覆盖的是哪一周。
            var headText = week.IsCurrent
                ? L10n.T("LocThisWeek") + "  " + (week.WeekRangeLabel ?? week.WeekLabel)
                : (week.WeekRangeLabel ?? week.WeekLabel);
            var subText = week.Top != null && week.Top.Entry != null
                ? week.TopTimeText + "  ·  " + L10n.F("LocTimelineTotal", week.TotalText)
                : L10n.T("LocTimelineEmpty");

            var head = MakeText(headText, 12, week.IsCurrent ? CurrentTop : TextBright);
            var sub = MakeText(subText, 11, TextColor);

            // 名字：先按"最大卡片宽度"排一次，让卡片尽量收窄；
            // 卡片宽度定下来后，名字实际能用的宽度更小、可能折出更多行，
            // 所以按**最终宽度**再量一遍。行高与时长位置全部用 FormattedText 的
            // 真实 Height，不做任何估算 —— 之前靠估算行数，折行行数对不上时
            // 游戏名第二行正好压在时长行上（用户实测翻过车）。
            FormattedText name = null;
            if (nameText.Length > 0)
            {
                name = MakeText(nameText, 15, TextBright, bold: true);
                name.MaxTextWidth = CardMaxW - (CardPadX * 2) - CoverW - 12;
            }

            // 卡片宽度 = 封面 + 间距 + 正文最宽一行 + 左右内边距，夹在上下限之间
            var widest = Math.Max(head.Width, name != null ? name.Width : 0);
            widest = Math.Max(widest, sub.Width);
            var cardW = widest + CoverW + 12 + CardPadX * 2;
            if (cardW < CardMinW) cardW = CardMinW;
            if (cardW > CardMaxW) cardW = CardMaxW;

            // 第二轮：按卡片实际正文宽度重量名字（绘制时用的就是这个宽度）。
            // Trimming 必须在这里一起设好：绘制阶段再设置 Trimming 会触发
            // FormattedText 重新布局，同样的宽度折行结果却不同（实测单行变两行），
            // 游戏名第二行就压到时长上了。绘制阶段一律不再改任何布局属性。
            var bodyW = cardW - (CardPadX * 2) - CoverW - 12;
            var nameH = MakeText("M", 15, TextBright, bold: true).Height;   // 无名字时占一行
            if (name != null)
            {
                name.MaxTextWidth = bodyW;
                name.Trimming = TextTrimming.CharacterEllipsis;
                nameH = name.Height;
            }
            head.MaxTextWidth = bodyW;
            head.Trimming = TextTrimming.CharacterEllipsis;
            sub.MaxTextWidth = bodyW;
            sub.Trimming = TextTrimming.CharacterEllipsis;
            var subH = sub.Height;

            // 卡片高度 = 标题带 28 + 名字实高 + 间距 5 + 时长实高 + 底部留白 12
            var cardH = 28 + nameH + 5 + subH + 12;
            if (cardH < 82) cardH = 82;

            // ---- 卡片位置：贴着柱子顶部，越界就翻到另一侧 ----
            var anchor = barX[hoverIndex] + (WeekStep * (1 - BarGapRatio)) / 2;
            var left = anchor - cardW / 2;
            if (left < 2) left = 2;
            if (left + cardW > contentWidth - 2) left = Math.Max(2, contentWidth - 2 - cardW);
            var top = barTop - cardH - 10;
            if (top < 2) top = 2;

            var cardRect = new Rect(left, top, cardW, cardH);

            // 阴影层（比卡片大一圈的半透明黑）
            var shadow = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0));
            shadow.Freeze();
            dc.DrawRoundedRectangle(shadow, null,
                new Rect(cardRect.X + 2, cardRect.Y + 3, cardW, cardH), 10, 10);

            // 卡片本体：比旧版更亮、不透明度更高 —— 用户要求"对比度稍微明显点"
            var bg = new LinearGradientBrush(
                Color.FromArgb(0xFC, 0x1A, 0x24, 0x33),
                Color.FromArgb(0xFC, 0x11, 0x18, 0x24), 90);
            bg.Freeze();
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x4A, 0x8F, 0xC8)), 1.4);
            borderPen.Freeze();
            dc.DrawRoundedRectangle(bg, borderPen, cardRect, 10, 10);

            // ---- 左：封面缩略图（圆角用"裁剪几何"实现）----
            var coverRect = new Rect(left + CardPadX, top + (cardH - CoverH) / 2, CoverW, CoverH);
            var cover = week.Top != null && week.Top.Entry != null ? week.Top.Entry.Cover : null;
            DrawCover(dc, cover, coverRect);

            // ---- 右：文字（周/日期 · 游戏全名 · 时长）----
                        // 注意：head/name/sub 的 MaxTextWidth、Trimming 已在测量阶段设好，
                        // 这里直接绘制同一对象，不要再碰任何布局属性（改一个就重新布局，
                        // 折行可能与测量不一致，游戏名就会压到时长上）。
                        var textLeft = coverRect.Right + 12;

                        dc.DrawText(head, new Point(textLeft, top + 9));

                        var nameTop = top + 28;
                        if (week.Top != null && name != null)
                        {
                            // 游戏全名按测量宽度自动折行，卡片高度已按折行后的实高撑起来
                            dc.DrawText(name, new Point(textLeft, nameTop));
                        }

                        // 时长行永远排在名字实高的下方，物理上不可能重叠
                        dc.DrawText(sub, new Point(textLeft, nameTop + nameH + 5));
                    }

        /// <summary>
        /// 把封面以"圆角矩形 + UniformToFill"画进指定区域。
        /// DrawingContext 没有现成的圆角图片 API，这里用矩形裁剪几何把图像裁圆角。
        /// 没封面时画一个带首字母的占位块（总比一个黑洞好看）。
        /// </summary>
        private static void DrawCover(DrawingContext dc, ImageSource source, Rect rect)
        {
            var radius = 7.0;
            var clip = new RectangleGeometry(rect, radius, radius);
            clip.Freeze();

            var plateBrush = new SolidColorBrush(Color.FromRgb(0x0D, 0x14, 0x1E));
            plateBrush.Freeze();

            if (source == null)
            {
                dc.DrawRoundedRectangle(plateBrush, null, rect, radius, radius);
                var glyph = MakeText("\uE7FC", 17, Color.FromRgb(0x46, 0x58, 0x70));
                glyph.SetFontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                dc.DrawText(glyph,
                    new Point(rect.X + (rect.Width - glyph.Width) / 2,
                              rect.Y + (rect.Height - glyph.Height) / 2));
                return;
            }

            dc.PushClip(clip);
            // 先铺底色，避免图片有透明边时露出后面的卡片文字
            dc.DrawRectangle(plateBrush, null, rect);
            // UniformToFill：按较长的边铺满，多出来的用裁剪吃掉
            var brush = new ImageBrush(source)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            try { brush.Freeze(); } catch { }
            dc.DrawRectangle(brush, null, rect);
            dc.Pop();
        }

        // ------------------------------------------------------------------
        // 文字工具
        // ------------------------------------------------------------------

        private static FormattedText MakeText(string text, double size, Color color, bool bold = false)
        {
            return new FormattedText(
                text ?? "",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                    bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                size,
                new SolidColorBrush(color),
                1.0);
        }
    }
}

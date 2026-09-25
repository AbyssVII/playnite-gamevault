using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace GameVault
{
    /// <summary>
    /// 「游玩时间轴」——横向排列的每周柱状图。
    ///
    /// 刻意自己画而不是用 ItemsControl + Border 堆柱：
    ///   ① 周的序号与月份分隔线需要跟柱子严格对齐，交给布局系统会被
    ///      Margin/Padding 一点点挪歪；
    ///   ② 悬停要能按 x 坐标反查是哪一周（ItemsControl 得给每根柱子挂事件）；
    ///   ③ 宽度变化时柱宽要连续重算，自绘只多一次 InvalidateVisual。
    ///
    /// 视觉结构（自下而上）：
    ///   月份标签行（"2026 年 9 月" + 月总时长）
    ///   ↑ 月份分隔竖线
    ///   柱形区（柱高 = 当周时长 / 峰值）
    ///   周标签行（"8/24"）
    /// 悬停某周时在光标旁弹一个信息卡：冠军游戏名 + 时长 + 本周总时长。
    /// </summary>
    public class TimelineChart : FrameworkElement
    {
        // ---- 布局常量 ----
        private const double MonthLabelHeight = 22;   // 顶部月份标签区
        private const double WeekLabelHeight = 20;    // 底部周标签区
        private const double BarAreaTopPad = 8;       // 柱形区顶部留白（给峰值数字）
        private const double MinBarWidth = 5;         // 柱最小宽度
        private const double MaxBarWidth = 26;        // 柱最大宽度（周数少时不要一根柱子占满屏）
        private const double BarGapRatio = 0.34;      // 柱间空隙占步长的比例

        // ---- 配色 ----
        private static readonly Color BarColor = Color.FromRgb(0x4C, 0xC2, 0xFF);
        private static readonly Color BarColorDim = Color.FromRgb(0x24, 0x3A, 0x52);
        private static readonly Color CurrentColor = Color.FromRgb(0x34, 0xD3, 0x99);
        private static readonly Color TextColor = Color.FromRgb(0x8A, 0x99, 0xAD);
        private static readonly Color TextBright = Color.FromRgb(0xE6, 0xED, 0xF3);
        private static readonly Color GridColor = Color.FromRgb(0x25, 0x2F, 0x3E);
        private static readonly Color MonthLineColor = Color.FromRgb(0x33, 0x41, 0x54);

        private TimelineData data = new TimelineData();
        private double[] barX = new double[0];       // 每根柱的左边界
        private double step;                          // 柱步长（含间隙）
        private double barWidth;
        private int hoverIndex = -1;

        public TimelineChart()
        {
            SnapsToDevicePixels = true;
            MinHeight = 180;
        }

        public void SetData(TimelineData value)
        {
            data = value ?? new TimelineData();
            hoverIndex = -1;
            Recalc();
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

        private int HitWeek(Point point)
        {
            if (data == null || data.Weeks == null || barX.Length == 0) return -1;
            for (var i = 0; i < barX.Length; i++)
            {
                if (point.X >= barX[i] && point.X <= barX[i] + barWidth) return i;
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
            if (count == 0 || ActualWidth <= 0) return;

            step = ActualWidth / count;
            barWidth = Math.Max(MinBarWidth, Math.Min(MaxBarWidth, step * (1 - BarGapRatio)));

            for (var i = 0; i < count; i++)
            {
                // 居中于该周的步长区间
                barX[i] = step * i + (step - barWidth) / 2;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            Recalc();
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

            var w = ActualWidth;
            var h = ActualHeight;
            var barBottom = h - WeekLabelHeight;
            var barTop = MonthLabelHeight + BarAreaTopPad;
            var barMaxHeight = Math.Max(20, barBottom - barTop);

            DrawBaseline(dc, barTop, barBottom, w);
            DrawMonthSeparators(dc, barTop, barBottom);
            DrawBars(dc, barTop, barBottom, barMaxHeight);
            DrawWeekLabels(dc, barBottom, w);
            DrawHoverCard(dc, barTop, barBottom, w, h);
        }

        private void DrawBaseline(DrawingContext dc, double barTop, double barBottom, double w)
        {
            var pen = new Pen(new SolidColorBrush(GridColor), 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(0, barBottom + 0.5), new Point(w, barBottom + 0.5));
        }

        private void DrawMonthSeparators(DrawingContext dc, double barTop, double barBottom)
        {
            if (data.Months == null) return;
            var pen = new Pen(new SolidColorBrush(MonthLineColor), 1) { DashStyle = DashStyles.Dash };
            pen.Freeze();

            foreach (var month in data.Months)
            {
                var index = month.StartIndex;
                if (index <= 0 || index >= barX.Length) continue;   // 第一个月不画左边线
                var x = barX[index] - (step - barWidth) / 2;
                dc.DrawLine(pen, new Point(x, barTop - 4), new Point(x, barBottom));

                // 月份标签（在分隔线右侧）
                var label = MakeText(month.Label, 11, TextColor);
                dc.DrawText(label, new Point(x + 5, 2));

                if (!string.IsNullOrEmpty(month.TotalText))
                {
                    var total = MakeText(month.TotalText, 10.5, TextColor);
                    dc.DrawText(total, new Point(x + 5 + label.Width + 7, 3.5));
                }
            }

            // 第一个月的标签（StartIndex == 0 时上面被跳过了）
            if (data.Months.Count > 0 && data.Months[0].StartIndex == 0)
            {
                var first = data.Months[0];
                var label = MakeText(first.Label, 11, TextColor);
                dc.DrawText(label, new Point(2, 2));
                if (!string.IsNullOrEmpty(first.TotalText))
                {
                    var total = MakeText(first.TotalText, 10.5, TextColor);
                    dc.DrawText(total, new Point(2 + label.Width + 7, 3.5));
                }
            }
        }

        private void DrawBars(DrawingContext dc, double barTop, double barBottom, double barMaxHeight)
        {
            var weeks = data.Weeks;
            var peak = data.PeakSeconds;
            if (peak == 0) return;

            for (var i = 0; i < weeks.Count; i++)
            {
                var week = weeks[i];
                if (week.TotalSeconds == 0)
                {
                    // 空周画一根细小的"基座"，让时间轴的节奏感连续
                    var stub = new Rect(barX[i], barBottom - 2, barWidth, 2);
                    dc.DrawRectangle(new SolidColorBrush(BarColorDim), null, stub);
                    continue;
                }

                var height = Math.Max(3, barMaxHeight * week.TotalSeconds / peak);
                var rect = new Rect(barX[i], barBottom - height, barWidth, height);

                var color = week.IsCurrent ? CurrentColor : BarColor;
                var hovered = i == hoverIndex;
                var brush = new SolidColorBrush(hovered ? Lighten(color) : color);
                brush.Freeze();

                dc.DrawRoundedRectangle(brush, null, rect, 3, 3);
            }
        }

        private static Color Lighten(Color color)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, color.R + 40),
                (byte)Math.Min(255, color.G + 40),
                (byte)Math.Min(255, color.B + 40));
        }

        private void DrawWeekLabels(DrawingContext dc, double barBottom, double w)
        {
            var weeks = data.Weeks;
            // 周标签太密会糊成一片：只在步长够宽时逐周标，否则每 2~4 周标一个
            var every = step >= 34 ? 1 : step >= 20 ? 2 : step >= 13 ? 3 : 6;

            for (var i = 0; i < weeks.Count; i++)
            {
                var isCurrent = weeks[i].IsCurrent;
                // 保证最后一周（本周）一定标出来，否则用户看不到"现在"在哪
                if (i % every != 0 && !isCurrent) continue;

                var text = MakeText(weeks[i].WeekLabel, 10.5, isCurrent ? CurrentColor : TextColor);
                var x = barX[i] + barWidth / 2 - text.Width / 2;
                x = Math.Max(0, Math.Min(w - text.Width, x));
                dc.DrawText(text, new Point(x, barBottom + 4));
            }
        }

        private void DrawHoverCard(DrawingContext dc, double barTop, double barBottom, double w, double h)
        {
            if (hoverIndex < 0 || hoverIndex >= data.Weeks.Count) return;
            var week = data.Weeks[hoverIndex];

            var lines = new List<(string Text, double Size, Color Color)>();
            lines.Add((week.IsCurrent ? L10n.T("LocThisWeek") : week.WeekLabel, 12.5, TextBright));
            if (week.Top != null)
            {
                lines.Add((L10n.F("LocTimelineTop", week.TopNameText), 11.5, BarColor));
                lines.Add((week.TopTimeText + "  ·  " + L10n.F("LocTimelineTotal", week.TotalText),
                    11, TextColor));
            }
            else
            {
                lines.Add((L10n.T("LocTimelineEmpty"), 11, TextColor));
            }

            var formatted = lines.Select(l => MakeText(l.Text, l.Size, l.Color)).ToList();
            var padX = 10.0;
            var padY = 8.0;
            var lineGap = 4.0;
            var maxW = formatted.Max(f => f.Width);
            var totalH = formatted.Sum(f => f.Height) + lineGap * (formatted.Count - 1);
            var cardW = maxW + padX * 2;
            var cardH = totalH + padY * 2;

            // 卡片跟随柱子，贴不到右边就翻到左侧
            var anchor = barX[hoverIndex] + barWidth / 2;
            var left = anchor - cardW / 2;
            if (left < 2) left = 2;
            if (left + cardW > w - 2) left = w - 2 - cardW;
            var top = Math.Max(2, barTop - cardH - 6);

            var bg = new SolidColorBrush(Color.FromArgb(0xF2, 0x15, 0x1C, 0x26));
            bg.Freeze();
            var border = new Pen(new SolidColorBrush(Color.FromRgb(0x2C, 0x39, 0x4B)), 1);
            border.Freeze();
            dc.DrawRoundedRectangle(bg, border, new Rect(left, top, cardW, cardH), 6, 6);

            var y = top + padY;
            foreach (var f in formatted)
            {
                dc.DrawText(f, new Point(left + padX, y));
                y += f.Height + lineGap;
            }
        }

        // ------------------------------------------------------------------
        // 文字工具（DrawingContext.DrawText 需要 FormattedText，
        // 这里统一构造，避免各处重复写 Typeface / 字重参数）
        // ------------------------------------------------------------------

        private static FormattedText MakeText(string text, double size, Color color)
        {
            return new FormattedText(
                text ?? "",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                    FontWeights.Normal, FontStretches.Normal),
                size,
                new SolidColorBrush(color),
                1.0);
        }
    }
}

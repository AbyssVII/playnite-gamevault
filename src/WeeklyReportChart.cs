using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK;

namespace GameVault
{
    /// <summary>
    /// 「游戏周报」：近一年的逐周时间轴，每一周用一款「当周玩得最多的游戏」的海报代表。
    ///
    /// 布局（奇偶周分居轴的两侧 —— 这是"一个在上一个在下"的字面意思）：
    ///
    ///      10-6-10-12        ← 上半周的日期 + 占比（靠卡片上边缘）
    ///      ┌──────┐
    ///      │ 海报 │  2:3      ← 上半周的海报（紧贴轴线之上）
    ///      └──────┘
    ///  ══════════════════════════  ← 轴线
    ///      ┌──────┐
    ///      │ 海报 │            ← 下半周的海报（紧贴轴线之下）
    ///      └──────┘
    ///        68%              ← 下半周的日期 + 占比（靠卡片下边缘）
    ///      10-13-10-19
    ///
    /// 所有尺寸都由控件实际高度算出来（见 <see cref="Recalc"/>），
    /// 所以拖分隔条调高卡片，海报和轴线会跟着一起长高，两侧始终填满。
    ///
    /// 横向不再由外层 ScrollViewer 负责：外层那套自定义模板会让 ScrollViewer
    /// 自动补一条系统水平滚动条，挂在卡片底部既占地方又难看。
    /// 现在控件自己占满视口宽度，超出部分靠 <see cref="Offset"/> 平移出去，
    /// 左右移动由外部（滚轮 / 触控板）改这个偏移完成。
    /// </summary>
    public class WeeklyReportChart : FrameworkElement
    {
        // ---- 固定节奏（不随高度变的部分）----
        private const double LabelHeight = 15.0;   // 日期行
        private const double ShareHeight = 14.0;   // 占比行
        private const double AxisGap = 12.0;       // 轴线到海报的呼吸位
        private const double PadV = 6.0;           // 上下边缘留白
        private const double PadL = 16.0;
        private const double PadR = 16.0;
        private const double ColGap = 12.0;        // 相邻两周海报之间的横向间距
        private const double PosterRatio = 2.0 / 3.0;  // 宽:高 = 2:3（竖版海报）
        private const double MinPosterHeight = 34.0;
        private const double MaxPosterHeight = 132.0;

        private TimelineData data;
        private int hoverIndex = -1;
        private double contentWidth;
        private double viewportWidth;
        private double offset;
        private IPlayniteAPI api;

        // ---- 由 Recalc 算出的布局结果 ----
        private double posterW, posterH, step, axisY, lastLayoutHeight = -1;

        public WeeklyReportChart()
        {
            MinHeight = 150;
            ClipToBounds = true;
            Focusable = false;
            Cursor = Cursors.Hand;
            SizeChanged += OnSizeChanged;
        }
        /// <summary>整条时间轴的内容宽度（与视口宽度无关）。</summary>
        public double ContentWidth { get { return contentWidth; } }

        /// <summary>横向偏移（像素）。0 = 最早一周，<see cref="MaxOffset"/> = 最新一周。</summary>
        public double Offset
        {
            get { return offset; }
            set
            {
                var clamped = Math.Max(0, Math.Min(MaxOffset, value));
                if (Math.Abs(clamped - offset) < 0.01) return;
                offset = clamped;
                InvalidateVisual();
            }
        }

        /// <summary>最右侧能停下的偏移；内容比视口窄时为 0。</summary>
        public double MaxOffset
        {
            get { return Math.Max(0, contentWidth - viewportWidth); }
        }

        /// <summary>周数（供测试与外部判断）。</summary>
        public int WeekCount
        {
            get { return data != null && data.Weeks != null ? data.Weeks.Count : 0; }
        }

        /// <summary>数据接入：和 TimelineChart 一样由外部直接调方法，不做 DependencyProperty。</summary>
        public void SetData(TimelineData value)
        {
            data = value ?? new TimelineData();
            hoverIndex = -1;
            Recalc();
            InvalidateVisual();
        }

        /// <summary>给测试用：直接指定悬停第几周。</summary>
        public void SetHoverForTest(int index)
        {
            hoverIndex = index;
            InvalidateVisual();
        }

        /// <summary>给测试用：某个海报矩形（周序号）。</summary>
        public Rect GetPosterRectForTest(int index)
        {
            return PosterRectOf(index);
        }

        /// <summary>给测试用：某一侧海报的顶边 y（奇偶周应分居轴线两侧）。</summary>
        public double PosterTopForTest(int index)
        {
            return PosterRectOf(index).Y;
        }

        /// <summary>给测试用：当前轴线的 y。</summary>
        public double AxisYForTest
        {
            get { return axisY; }
        }

        /// <summary>
        /// 给测试用：按给定高度重算布局。
        /// 会真的把控件高度设上去，这样 Recalc 读 ActualWidth/Height 才是真值
        /// ——只改字段的话 Recalc 仍然读实际高度，测出来的"自适应"是假的。
        /// </summary>
        public void LayoutForTest(double height)
        {
            Height = height;
            Recalc(height);
            lastLayoutHeight = height;
        }

        /// <summary>
        /// 注入 Playnite API —— 双击海报要跳到库视图，构造时还拿不到，
        /// 由 VaultView 在 FindName 之后塞进来。
        /// </summary>
        public IPlayniteAPI Api
        {
            get { return api; }
            set { api = value; }
        }


        /// <summary>
        /// 按控件实际高度算所有尺寸。
        ///
        /// 海报高度 = (可用高度 - 两行标签 - 轴线留白) / 2，
        /// 这样上下两侧各放一张海报 + 标签，正好把卡片高度填满；
        /// 拖高卡片 → 海报等比变大，纵向不会留大片空白。
        /// </summary>
        private void Recalc(double? forcedHeight = null)
        {
            var count = data != null && data.Weeks != null ? data.Weeks.Count : 0;
            if (count == 0)
            {
                contentWidth = 0;
                return;
            }

            var h = forcedHeight.HasValue ? forcedHeight.Value : (ActualHeight > 0 ? ActualHeight : 220);
            var textBlock = LabelHeight + ShareHeight;
            // 两侧各能分到的海报高度
            var side = (h - PadV * 2 - AxisGap * 2) / 2 - textBlock;
            if (side < MinPosterHeight) side = MinPosterHeight;
            if (side > MaxPosterHeight) side = MaxPosterHeight;

            posterH = side;
            posterW = side * PosterRatio;
            step = posterW + ColGap;

            // 轴线：上半周 = 标签 + 海报 + 留白
            axisY = PadV + textBlock + posterH + AxisGap;

            contentWidth = PadL + step * count + PadR;
        }

        /// <summary>
        /// 只占视口本身的宽度 —— 内容再宽也不把 DesiredSize 撑大，
        /// 否则父级 Grid / 页面会被一起撑宽，底部就会冒出一条水平滚动条。
        /// 超出的部分交给 <see cref="Offset"/> 平移。
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var w = double.IsInfinity(availableSize.Width)
                ? Math.Max(contentWidth, 1)
                : availableSize.Width;
            var h = double.IsInfinity(availableSize.Height) ? 220 : availableSize.Height;

            viewportWidth = w;
            Recalc(h);

            return new Size(w, h);
        }

        /// <summary>
        /// 高度变了要重算（海报随之长高/缩小）。
        ///
        /// FrameworkElement 没有可重写的 OnSizeChanged（那是 Control 才有的），
        /// 所以挂 SizeChanged 事件；并且只在**高度**真变时才重算 ——
        /// 布局参数都跟着高度算，跟着宽度变化重算只会白白重绘。
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0) viewportWidth = e.NewSize.Width;

            if (Math.Abs(e.NewSize.Height - lastLayoutHeight) < 0.5) return;
            lastLayoutHeight = e.NewSize.Height;
            Recalc(e.NewSize.Height);
            InvalidateVisual();
        }
        // ==================================================================
        // 周与几何
        // ==================================================================

        /// <summary>奇数周在轴上方，偶数周在轴下方 —— 需求里的"一个上一个下"。</summary>
        private static bool IsUpper(int index)
        {
            return (index & 1) == 1;
        }

        private double ColumnX(int index)
        {
            return PadL + step * index;
        }

        /// <summary>第 index 周海报的矩形。两侧都贴着轴线，长度相同、只是方向相反。</summary>
        private Rect PosterRectOf(int index)
        {
            var x = ColumnX(index) + (step - ColGap - posterW) / 2;
            var y = IsUpper(index)
                ? axisY - AxisGap - posterH
                : axisY + AxisGap;
            return new Rect(x, y, posterW, posterH);
        }

        /// <summary>按坐标反查是哪一周。上下两档海报 + 中间轴线都算命中。</summary>
        private int HitWeek(Point p)
        {
            if (data == null || data.Weeks == null || data.Weeks.Count == 0) return -1;
            if (posterH <= 0) return -1;

            // 先按 x 粗筛：列宽内都算
            var i = (int)Math.Floor((p.X - PadL) / step);
            if (i < 0 || i >= data.Weeks.Count) return -1;

            // 再用海报矩形精确判断，命中不了就算 miss（不要把悬停高亮涂到空处）
            var rect = PosterRectOf(i);
            var pad = 4;
            if (p.X >= rect.X - pad && p.X <= rect.X + rect.Width + pad &&
                p.Y >= rect.Y - pad && p.Y <= rect.Y + rect.Height + pad) return i;
            return -1;
        }

        // ==================================================================
        // 交互
        // ==================================================================

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var index = HitWeek(ContentPoint(e));
            if (index == hoverIndex) return;

            hoverIndex = index;
            InvalidateVisual();
            ToolTip = index >= 0 ? BuildToolTip(index) : null;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex < 0) return;
            hoverIndex = -1;
            ToolTip = null;
            InvalidateVisual();
        }

        /// <summary>
        /// 双击海报 → 跳到 Playnite 库视图里这款游戏的详情。
        /// 和库存列表的双击行为一致（不直接启动游戏）。
        /// </summary>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ClickCount != 2) return;

            var index = HitWeek(ContentPoint(e));
            if (index < 0 || data == null || data.Weeks == null) return;

            var entry = data.Weeks[index].Top != null ? data.Weeks[index].Top.Entry : null;
            if (entry == null || api == null) return;

            try
            {
                api.MainView.SelectGame(entry.PrimaryId);
                api.MainView.SwitchToLibraryView();
            }
            catch
            {
            }
            e.Handled = true;
        }

        /// <summary>
        /// 屏幕坐标 → 内容坐标。绘制整体平移了 <see cref="Offset"/>，
        /// 命中判断要减掉同样的量，否则横向移动之后鼠标会指错周。
        /// </summary>
        private Point ContentPoint(MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            p.X += offset;
            return p;
        }

        /// <summary>
        /// 悬停提示。**不显示占比** —— 海报旁边已经常驻写着百分比了，
        /// 悬浮窗里再重复一遍是冗余。这里只补海报上放不下的信息：游戏名和当周总时长。
        /// </summary>
        private string BuildToolTip(int index)
        {
            var week = data.Weeks[index];
            if (week.Top == null)
                return L10n.IsEnglish
                    ? week.WeekRangeLabel + "\nNothing played that week."
                    : week.WeekRangeLabel + "\n这周没有游玩记录";

            return L10n.IsEnglish
                ? week.Top.Name + "\nWeek total: " + week.TotalText + "\n(双击查看该游戏)"
                : week.Top.Name + "\n本周共 " + week.TotalText + "\n（双击查看该游戏）";
        }
        // ==================================================================
        // 绘制
        // ==================================================================

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (data == null || data.Weeks == null || data.Weeks.Count == 0) return;
            if (posterH <= 0) Recalc(ActualHeight);
            if (posterH <= 0) return;

            // 整体横向平移：超出视口的部分由 Offset 决定露出哪一段
            var shift = new TranslateTransform(-offset, 0);
            shift.Freeze();
            dc.PushTransform(shift);
            try
            {
                DrawBody(dc);
            }
            finally
            {
                dc.Pop();
            }
        }

        private void DrawBody(DrawingContext dc)
        {
            var last = data.Weeks.Count - 1;
            if (last >= 0 && data.Weeks[last].IsCurrent)
            {
                var wash = new SolidColorBrush(Color.FromArgb(0x1C, 0x4C, 0x8C, 0xFF));
                wash.Freeze();
                dc.DrawRectangle(wash, null,
                    new Rect(ColumnX(last), PadV, step, Math.Max(0, ActualHeight - PadV * 2)));
            }

            for (var i = 0; i < data.Weeks.Count; i++)
                DrawWeek(dc, i, data.Weeks[i]);

            DrawAxis(dc);
            DrawHoverCard(dc);
        }

        private void DrawWeek(DrawingContext dc, int index, WeekBucket week)
        {
            var rect = PosterRectOf(index);
            var centerX = rect.X + rect.Width / 2;
            var upper = IsUpper(index);

            // 海报按 2:3 竖版比例铺（Stretch.UniformToFill 会按长边铺满、裁掉多余部分）
            if (week.Top != null && week.Top.Entry != null)
                DrawPoster(dc, week.Top.Entry.Cover, rect);
            else
                DrawEmptySlot(dc, rect);

            if (hoverIndex == index)
            {
                var hl = new SolidColorBrush(Color.FromArgb(0x3A, 0x6E, 0x9B, 0xFF));
                hl.Freeze();
                dc.DrawRoundedRectangle(hl, null,
                    new Rect(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), 9, 9);
            }

            // 从海报朝轴线拉一条细线，把两侧的周和轴连起来
            var stem = new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x2B, 0x66)), 1);
            stem.Freeze();
            dc.DrawLine(stem,
                new Point(centerX, upper ? rect.Y + rect.Height : rect.Y),
                new Point(centerX, axisY));

            // 日期 + 占比：上半周放在海报上方、下半周放在海报下方，
            // 也就是都朝卡片外缘走，轴线附近留白给海报，视觉更整齐。
            var dateY = upper ? rect.Y - LabelHeight - ShareHeight - 3
                              : rect.Y + rect.Height + 3;
            var date = MakeText(week.WeekRangeLabel, 10,
                week.IsEmpty ? Color.FromRgb(0x5A, 0x51, 0x84) : Color.FromRgb(0xC6, 0xBE, 0xE6));
            dc.DrawText(date, new Point(centerX - date.Width / 2, dateY));

            // 占比常驻显示在日期旁（所以悬停卡里不再重复它）
            if (!week.IsEmpty && !string.IsNullOrEmpty(week.TopShareText))
            {
                var share = MakeText(week.TopShareText, 10,
                    week.IsCurrent ? Color.FromRgb(0x7E, 0xAA, 0xFF) : Color.FromRgb(0x8B, 0x82, 0xB8), true);
                dc.DrawText(share, new Point(centerX - share.Width / 2, dateY + LabelHeight));
            }
        }

        private void DrawAxis(DrawingContext dc)
        {
            var line = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x31, 0x70)), 1.5);
            line.Freeze();
            dc.DrawLine(line, new Point(PadL, axisY),
                             new Point(PadL + step * data.Weeks.Count, axisY));
        }

        /// <summary>没玩过的那一周：画个空槽位，保留位置感（否则会以为这周不存在）。</summary>
        private void DrawEmptySlot(DrawingContext dc, Rect rect)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x2B, 0x66)), 1);
            pen.Freeze();
            var brush = new SolidColorBrush(Color.FromRgb(0x16, 0x11, 0x30));
            brush.Freeze();
            dc.DrawRoundedRectangle(brush, pen, rect, 7, 7);
        }
        /// <summary>
        /// 画海报缩略图。DrawingContext 没有现成的圆角图片 API，
        /// 这里用矩形裁剪几何把图像裁圆角（和时间轴的 DrawCover 同一套做法）。
        /// </summary>
        private static void DrawPoster(DrawingContext dc, ImageSource source, Rect rect)
        {
            var radius = 7.0;
            var clip = new RectangleGeometry(rect, radius, radius);
            clip.Freeze();

            var plate = new SolidColorBrush(Color.FromRgb(0x0D, 0x14, 0x1E));
            plate.Freeze();

            if (source == null)
            {
                dc.DrawRoundedRectangle(plate, null, rect, radius, radius);
                var glyph = MakeText("\uE7FC", 16, Color.FromRgb(0x46, 0x3E, 0x78));
                glyph.SetFontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                dc.DrawText(glyph, new Point(rect.X + (rect.Width - glyph.Width) / 2,
                                              rect.Y + (rect.Height - glyph.Height) / 2));
                return;
            }

            dc.PushClip(clip);
            // 先铺底色，避免图片有透明边时露出后面的卡片
            dc.DrawRectangle(plate, null, rect);
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

        /// <summary>
        /// 悬停卡：显示游戏名 + 当周总时长。
        /// **刻意不显示占比** —— 海报旁已经常驻写着百分比，重复一遍是冗余。
        /// 画在控件内部（而不是用系统 ToolTip）是为了让它贴着海报出现、并跟着横向滚动。
        /// </summary>
        private void DrawHoverCard(DrawingContext dc)
        {
            if (hoverIndex < 0 || hoverIndex >= data.Weeks.Count) return;
            var week = data.Weeks[hoverIndex];
            if (week.Top == null) return;

            var lines = new[]
            {
                week.WeekRangeLabel + "  " + (L10n.IsEnglish ? "week" : week.WeekOfYear + " 周"),
                week.Top.Name,
                (L10n.IsEnglish ? "Week total: " : "本周共 ") + week.TotalText,
                L10n.IsEnglish ? "(double-click to open)" : "（双击查看该游戏）",
            };

            const double padX = 11;
            const double padY = 9;
            const double lineH = 15;
            double maxW = 0;
            var texts = new FormattedText[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                var bold = i == 1;
                texts[i] = MakeText(lines[i], bold ? 12 : 11,
                    bold ? Color.FromRgb(0xED, 0xE9, 0xFA) : Color.FromRgb(0xC6, 0xBE, 0xE6), bold);
                if (texts[i].Width > maxW) maxW = texts[i].Width;
            }

            var boxW = maxW + padX * 2;
            var boxH = lineH * lines.Length + padY * 2;

            var rect = PosterRectOf(hoverIndex);
            var centerX = rect.X + rect.Width / 2;
            var boxX = centerX - boxW / 2;

            // 上半周：卡片放在海报上方（靠卡片顶缘）；下半周放下方。
            var boxY = IsUpper(hoverIndex) ? rect.Y - boxH - 6 : rect.Y + rect.Height + 6;
            boxY = Math.Max(0, Math.Min(boxY, Math.Max(0, ActualHeight - boxH)));
            boxX = Math.Max(0, Math.Min(boxX, Math.Max(0, contentWidth - boxW)));

            var bg = new SolidColorBrush(Color.FromRgb(0x1E, 0x19, 0x40));
            bg.Freeze();
            var border = new Pen(new SolidColorBrush(Color.FromRgb(0x4C, 0x8C, 0xFF)), 1);
            border.Freeze();
            var shadow = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
            shadow.Freeze();

            dc.DrawRectangle(shadow, null, new Rect(boxX + 2, boxY + 3, boxW, boxH));
            dc.DrawRoundedRectangle(bg, border, new Rect(boxX, boxY, boxW, boxH), 8, 8);

            for (var i = 0; i < lines.Length; i++)
                dc.DrawText(texts[i], new Point(boxX + padX, boxY + padY + i * lineH));
        }

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
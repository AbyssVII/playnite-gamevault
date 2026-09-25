using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GameVault
{
    /// <summary>
    /// 环形饼图。刻意不依赖任何第三方图表库 —— 一个环形图的几何量很小，
    /// 用 WPF 自带的 Path + ArcSegment 手画反而更可控（配色、动画、命中测试都能自己定）。
    ///
    /// 画法是每瓣一条独立的 Path（不合并成一个 Geometry），带来三个好处：
    ///   ① 悬停时单独放大某一瓣很容易；
    ///   ② 相邻两瓣之间可以留 1.6° 的缝，视觉上更干净；
    ///   ③ 某一瓣占比 100% 时（只有一个类型），单独走整圆路径 ——
    ///      ArcSegment 画出 360° 的弧会退化成不渲染，这是手写饼图最常踩的坑。
    /// </summary>
    public class PieChart : FrameworkElement
    {
        /// <summary>每瓣之间留的缝隙（度）。太小看不出来，太大占比会失真。</summary>
        private const double GapDegrees = 1.6;

        /// <summary>悬停时该瓣向外推的像素。</summary>
        private const double PopOut = 9;

        private static readonly Color CenterTitleColor = Color.FromRgb(0xC1, 0xCD, 0xDB);
        private static readonly Color CenterValueColor = Color.FromRgb(0xE6, 0xED, 0xF3);

        private List<GenreSlice> slices = new List<GenreSlice>();
        private string centerTitle = "";
        private string centerValue = "";

        private readonly List<PieSliceVisual> visuals = new List<PieSliceVisual>();
        private PieSliceVisual hovered;
        private int hoveredIndex = -1;

        /// <summary>某一瓣被点击时触发（参数是它的索引，-1 表示点了空白）。</summary>
        public event Action<int> SliceClicked;

        public double Thickness { get; set; }

        public PieChart()
        {
            Thickness = 30;
            SnapsToDevicePixels = true;
        }

        /// <summary>设置数据。label 是环形中心的标题与主数值。</summary>
        public void SetData(List<GenreSlice> data, string title, string value)
        {
            slices = data ?? new List<GenreSlice>();
            centerTitle = title ?? "";
            centerValue = value ?? "";
            Rebuild();
        }

        /// <summary>外部把选中的那一瓣高亮出来（图例点击时同步）。-1 = 全部正常显示。</summary>
        public void SetHighlight(int index)
        {
            hoveredIndex = index;
            ApplyHighlight();
        }

        private void Rebuild()
        {
            visuals.Clear();
            hovered = null;

            // 按占比从大到小排（数据层已排好，这里只做防御）
            var total = 0.0;
            foreach (var s in slices) total += Math.Max(0, s.Share);
            if (total <= 0) { InvalidateVisual(); return; }

            for (var i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                var share = Math.Max(0, slice.Share) / total;

                var visual = new PieSliceVisual
                {
                    Index = i,
                    Share = share,
                    Brush = FrozenBrush(slice.Color),
                    // 每瓣分配时长：让动画看起来是"依次长出"，而不是齐刷刷一起转
                    Delay = 0.06 * i + 0.05
                };

                // 用一条 0→1 的动画驱动 Progress 属性；真正的几何在 OnRender 里按当前进度算。
                // 注意：PieSliceVisual 继承 Animatable，它的属性变化不会自动让父元素重绘，
                // 所以这里额外挂一个 Changed 回调去 InvalidateVisual。
                var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(620))
                {
                    BeginTime = TimeSpan.FromMilliseconds(visual.Delay * 1000),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                };
                visual.SetProgressDriver(animation, this);

                visuals.Add(visual);
            }

            ApplyHighlight();
            InvalidateVisual();
        }

        private static SolidColorBrush FrozenBrush(string hex)
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    string.IsNullOrEmpty(hex) ? "#64748B" : hex));
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        // ------------------------------------------------------------------
        // 命中测试与鼠标交互
        // ------------------------------------------------------------------

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var index = HitSlice(e.GetPosition(this));
            if (index == hoveredIndex) return;

            hoveredIndex = index;
            ApplyHighlight();
            Cursor = index >= 0 ? System.Windows.Input.Cursors.Hand : null;
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoveredIndex == -1) return;
            hoveredIndex = -1;
            ApplyHighlight();
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var index = HitSlice(e.GetPosition(this));
            if (index >= 0 && SliceClicked != null) SliceClicked(index);
            else if (SliceClicked != null) SliceClicked(-1);
        }

        private void ApplyHighlight()
        {
            for (var i = 0; i < visuals.Count; i++)
            {
                var target = (hoveredIndex == -1 || hoveredIndex == i) ? 1.0 : 0.35;
                var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(140))
                {
                    FillBehavior = FillBehavior.HoldEnd
                };
                visuals[i].BeginAnimation(PieSliceVisual.OpacityProperty, anim);
            }
            hovered = hoveredIndex >= 0 && hoveredIndex < visuals.Count ? visuals[hoveredIndex] : null;
            InvalidateVisual();
        }

        /// <summary>判断某个点落在哪一瓣上（只认环形那一圈，中心空洞不响应）。</summary>
        private int HitSlice(Point point)
        {
            var size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) return -1;

            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var outer = size / 2 - PopOut - 1;
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > outer || distance < outer - Thickness) return -1;

            // -90 是因为 0° 要落在 12 点方向，而不是 3 点
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
            if (angle < 0) angle += 360;

            var cursor = 0.0;
            foreach (var visual in visuals)
            {
                var sweep = visual.Share * 360;
                if (angle >= cursor && angle < cursor + sweep) return visual.Index;
                cursor += sweep;
            }
            return -1;
        }

        // ------------------------------------------------------------------
        // 绘制
        // ------------------------------------------------------------------

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var size = Math.Min(ActualWidth, ActualHeight);
            if (size < 40 || visuals.Count == 0) return;

            // 画布可能不是正方形；按短边定位，保证圆始终居中且不被裁掉。
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var outerRadius = size / 2 - PopOut - 1;
            var innerRadius = Math.Max(0, outerRadius - Thickness);

            // 锚点（每瓣弹出来时沿这个方向平移）
            var cursor = 0.0;
            foreach (var visual in visuals)
            {
                var sweep = visual.Share * 360;
                visual.MidAngle = cursor + sweep / 2;
                cursor += sweep;
            }

            dc.DrawGeometry(null, null, Geometry.Empty); // 预热，避免首帧的布局抖动

            cursor = 0;
            foreach (var visual in visuals)
            {
                var sweep = visual.Share * 360 * visual.Progress;
                if (sweep <= 0.01) { cursor += visual.Share * 360; continue; }

                var isHovered = ReferenceEquals(visual, hovered);
                // 悬停的瓣沿中线方向外推
                var offset = new Vector(0, 0);
                if (isHovered)
                {
                    var rad = (visual.MidAngle - 90) * Math.PI / 180;
                    offset = new Vector(Math.Cos(rad) * PopOut, Math.Sin(rad) * PopOut);
                }

                var geometry = BuildSlice(center + offset, outerRadius, innerRadius,
                    cursor, sweep, visual.Share);

                dc.PushOpacity(visual.Opacity);
                dc.DrawGeometry(visual.Brush, null, geometry);
                dc.Pop();
                cursor += visual.Share * 360;
            }

            DrawCenterText(dc, center, innerRadius);
        }

        /// <summary>
        /// 构造一瓣的几何。
        /// 注意 sweep 必须严格小于 360°：正好 360 时 ArcSegment 的起点与终点重合，
        /// WPF 会认为弧长为零而什么都不画。所以只有一瓣时改画"圆环"。
        /// </summary>
        private Geometry BuildSlice(Point center, double outerRadius, double innerRadius,
            double startAngle, double sweep, double share)
        {
            // 唯一一瓣（占比 100%）时不能走 ArcSegment，否则整圈消失。
            // 但也不能简单画一个实心圆 —— 那样环形中间就没有空洞，
            // 会和两瓣以上时的视觉样式对不上。所以用两段整圆拼一个真·圆环：
            //   外圆顺时针 + 内圆逆时针，靠 EvenOdd 填充规则把中间掏空。
            if (share >= 0.9999)
                return BuildFullRing(center, outerRadius, innerRadius);

            var gap = visuals.Count > 1 ? Math.Min(GapDegrees, sweep / 3) : 0;
            var from = startAngle + gap / 2;
            var to = startAngle + sweep - gap / 2;
            if (to <= from) return Geometry.Empty;

            var outerStart = OnCircle(center, outerRadius, from);
            var outerEnd = OnCircle(center, outerRadius, to);
            var innerEnd = OnCircle(center, innerRadius, to);
            var innerStart = OnCircle(center, innerRadius, from);

            var figure = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
            figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius),
                0, to - from > 180, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(innerEnd, true));
            figure.Segments.Add(new ArcSegment(innerStart, new Size(innerRadius, innerRadius),
                0, to - from > 180, SweepDirection.Counterclockwise, true));

            var path = new PathGeometry();
            path.Figures.Add(figure);
            path.Freeze();
            return path;
        }

        /// <summary>
        /// 画一个完整圆环（中间有洞）。
        /// 用两个半圆拼外圈、两个半圆拼内圈，而不是 EllipseGeometry ——
        /// EllipseGeometry 是实心的，没法做洞；靠 EvenOdd 规则叠两个圆虽然也能掏空，
        /// 但两瓣时的边缘是 ArcSegment 画的，混用两种来源会在缝隙处产生细微的宽度差。
        /// 全部用 ArcSegment 拼，边缘粗细才能保证一致。
        /// </summary>
        private static Geometry BuildFullRing(Point center, double outerRadius, double innerRadius)
        {
            var path = new PathGeometry { FillRule = FillRule.EvenOdd };

            path.Figures.Add(Ring(center, outerRadius, true));
            path.Figures.Add(Ring(center, innerRadius, false));

            path.Freeze();
            return path;
        }

        private static PathFigure Ring(Point center, double radius, bool clockwise)
        {
            // 从 12 点方向开始，用两段 180° 弧画满整圈
            var top = new Point(center.X, center.Y - radius);
            var bottom = new Point(center.X, center.Y + radius);

            var figure = new PathFigure { StartPoint = top, IsClosed = true, IsFilled = true };
            figure.Segments.Add(new ArcSegment(bottom, new Size(radius, radius), 0, false,
                clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
            figure.Segments.Add(new ArcSegment(top, new Size(radius, radius), 0, false,
                clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
            return figure;
        }

        private static Point OnCircle(Point center, double radius, double angleDegrees)
        {
            var rad = (angleDegrees - 90) * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }

        private void DrawCenterText(DrawingContext dc, Point center, double innerRadius)
        {
            if (innerRadius < 30) return;

            // 字号跟着环宽走：环越粗（饼图越大）中心文字越大，
            // 这样把饼图调大之后文字不会显得缩在中间一小团。
            var scale = Math.Max(1.0, Math.Min(1.6, innerRadius / 82.0));
            var maxWidth = innerRadius * 1.75;
            var title = MakeText(centerTitle, 12.5 * scale, CenterTitleColor, maxWidth);
            var value = MakeText(centerValue, 21 * scale, CenterValueColor, maxWidth, FontWeights.SemiBold);
            title.Measure(new Size(maxWidth, double.PositiveInfinity));
            value.Measure(new Size(maxWidth, double.PositiveInfinity));

            var blockWidth = Math.Max(title.DesiredSize.Width, value.DesiredSize.Width);
            var blockHeight = title.DesiredSize.Height + value.DesiredSize.Height;

            // 直接按测量结果画文字，而不是拿 StackPanel 做 VisualBrush ——
            // VisualBrush 会缓存可视树的渲染结果，动画推进时不会自动更新，
            // 中心文字就会一直停在第一帧。
            var left = center.X - blockWidth / 2;
            var top = center.Y - blockHeight / 2;

            dc.DrawText(MakeFormatted(title), new Point(left + (blockWidth - title.DesiredSize.Width) / 2, top));
            dc.DrawText(MakeFormatted(value),
                new Point(left + (blockWidth - value.DesiredSize.Width) / 2, top + title.DesiredSize.Height));
        }

        /// <summary>把 TextBlock 的属性搬进 FormattedText，供 DrawingContext 直接绘制。</summary>
        private static FormattedText MakeFormatted(System.Windows.Controls.TextBlock template)
        {
            return new FormattedText(
                template.Text ?? "",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                    template.FontWeight, FontStretches.Normal),
                template.FontSize,
                template.Foreground,
                1.0);
        }

        private static System.Windows.Controls.TextBlock MakeText(
            string text, double size, Color color, double maxWidth, FontWeight? weight = null)
        {
            return new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                Foreground = new SolidColorBrush(color),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = maxWidth
            };
        }

        /// <summary>
        /// 一瓣的状态。继承 Animatable 是为了能用 BeginAnimation 直接驱动 Progress。
        ///
        /// 关键点：Animatable 上的属性动画**不会**自动触发父元素重绘 —— 它不像
        /// UIElement 那样有渲染管线接管。所以构造时要把宿主 PieChart 记下来，
        /// 每次属性变化都手动 InvalidateVisual。
        /// </summary>
        private class PieSliceVisual : Animatable
        {
            public static readonly DependencyProperty ProgressProperty =
                DependencyProperty.Register("Progress", typeof(double), typeof(PieSliceVisual),
                    new PropertyMetadata(1.0, OnAnyChanged));

            /// <summary>
            /// 自己声明 Opacity，而不是从 UIElement 继承 —— 这个类继承的是 Animatable，
            /// 没有 UIElement.Opacity。声明成 DP 之后就能用 BeginAnimation 驱动。
            /// </summary>
            public static readonly DependencyProperty OpacityProperty =
                DependencyProperty.Register("Opacity", typeof(double), typeof(PieSliceVisual),
                    new PropertyMetadata(1.0, OnAnyChanged));

            public int Index { get; set; }
            public double Share { get; set; }
            public double Delay { get; set; }
            public double MidAngle { get; set; }
            public Brush Brush { get; set; }

            private PieChart host;

            public void SetProgressDriver(DoubleAnimation animation, PieChart owner)
            {
                host = owner;
                BeginAnimation(ProgressProperty, animation);
            }

            public double Progress
            {
                get { return (double)GetValue(ProgressProperty); }
                set { SetValue(ProgressProperty, value); }
            }

            public double Opacity
            {
                get { return (double)GetValue(OpacityProperty); }
                set { SetValue(OpacityProperty, value); }
            }

            private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var visual = d as PieSliceVisual;
                if (visual != null && visual.host != null) visual.host.InvalidateVisual();
            }

            protected override Freezable CreateInstanceCore()
            {
                return new PieSliceVisual();
            }
        }
    }
}

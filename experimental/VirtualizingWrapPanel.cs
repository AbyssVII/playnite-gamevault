using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace GameVault
{
    /// <summary>
    /// 带虚拟化的自动换行面板，按固定格子尺寸排列子项。
    ///
    /// 为什么要自己写：
    /// - WPF 自带的 WrapPanel 会换行但不虚拟化，500 张卡片全部实例化会又慢又占内存；
    /// - VirtualizingStackPanel 会虚拟化但不换行。
    /// 而"拖动缩放条时卡片实时重新换行"恰好同时需要这两点，所以两者结合。
    ///
    /// 格子尺寸通过静态字段传入：本插件同时只存在一个网格面板实例，
    /// 用静态值比从模板里做 RelativeSource 绑定更可靠（ItemsPanelTemplate 里的绑定上下文不稳定）。
    /// </summary>
    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        /// <summary>格子宽度 = 卡片宽度 + 卡片间距</summary>
        public static double CellWidth = 182;

        /// <summary>格子高度 = 封面高 + 卡片文字区高 + 卡片间距</summary>
        public static double CellHeight = 315;

        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset;
        private int _columns = 1;

        #region IScrollInfo

        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }

        public double ExtentWidth { get { return _extent.Width; } }
        public double ExtentHeight { get { return _extent.Height; } }
        public double ViewportWidth { get { return _viewport.Width; } }
        public double ViewportHeight { get { return _viewport.Height; } }
        public double HorizontalOffset { get { return _offset.X; } }
        public double VerticalOffset { get { return _offset.Y; } }

        public ScrollViewer ScrollOwner { get; set; }

        public void LineUp() { SetVerticalOffset(VerticalOffset - CellHeight * 0.25); }
        public void LineDown() { SetVerticalOffset(VerticalOffset + CellHeight * 0.25); }
        public void LineLeft() { }
        public void LineRight() { }
        public void PageUp() { SetVerticalOffset(VerticalOffset - ViewportHeight); }
        public void PageDown() { SetVerticalOffset(VerticalOffset + ViewportHeight); }
        public void PageLeft() { }
        public void PageRight() { }
        public void MouseWheelUp() { SetVerticalOffset(VerticalOffset - CellHeight * 0.6); }
        public void MouseWheelDown() { SetVerticalOffset(VerticalOffset + CellHeight * 0.6); }
        public void MouseWheelLeft() { }
        public void MouseWheelRight() { }
        public void SetHorizontalOffset(double offset) { }

        /// <summary>让指定子元素滚动到可视区域（ScrollIntoView / BringIntoView 会走这里）。</summary>
        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (visual == null) return rectangle;

            var generator = GetGenerator();
            if (generator == null) return rectangle;
            for (var i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];
                if (!ReferenceEquals(child, visual) && !child.IsAncestorOf(visual)) continue;

                var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
                if (itemIndex < 0) break;

                var row = itemIndex / _columns;
                var top = row * CellHeight;
                var bottom = top + CellHeight;

                if (top < _offset.Y) SetVerticalOffset(top);
                else if (bottom > _offset.Y + _viewport.Height) SetVerticalOffset(bottom - _viewport.Height);

                return new Rect(0, top - _offset.Y, _viewport.Width, Math.Min(CellHeight, _viewport.Height));
            }
            return rectangle;
        }

        public void SetVerticalOffset(double offset)
        {
            var max = Math.Max(0, _extent.Height - _viewport.Height);
            var value = Math.Max(0, Math.Min(offset, max));
            if (Math.Abs(value - _offset.Y) < 0.01) return;
            _offset.Y = value;
            InvalidateMeasure();
            if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo();
        }

        private void NotifyScrollOwner()
        {
            if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo();
        }

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            var itemsControl = ItemsControl.GetItemsOwner(this);
            var itemCount = itemsControl == null ? 0 : itemsControl.Items.Count;


            var cellWidth = Math.Max(1, CellWidth);
            var cellHeight = Math.Max(1, CellHeight);

            if (itemCount == 0)
            {
                if (InternalChildren.Count > 0) RemoveInternalChildRange(0, InternalChildren.Count);
                _extent = new Size(0, 0);
                _columns = 1;
                NotifyScrollOwner();
                return new Size(0, 0);
            }

            var width = Resolve(availableSize.Width, _viewport.Width, 800,
                ScrollOwner != null ? ScrollOwner.ViewportWidth : 0);
            var height = Resolve(availableSize.Height, _viewport.Height, 600,
                ScrollOwner != null ? ScrollOwner.ViewportHeight : 0);

            _columns = Math.Max(1, (int)Math.Floor(width / cellWidth));
            var rows = (int)Math.Ceiling(itemCount / (double)_columns);
            _extent = new Size(_columns * cellWidth, rows * cellHeight);
            _viewport = new Size(width, height);

            // 把偏移夹回合法范围（缩放导致列数变化时，行数变少，旧偏移可能越界）
            var maxOffset = Math.Max(0, _extent.Height - _viewport.Height);
            if (_offset.Y > maxOffset) _offset.Y = maxOffset;

            // 只生成可见区域（多留一行，减少快速滚动时的闪烁）
            var firstRow = Math.Max(0, (int)Math.Floor(_offset.Y / cellHeight));
            var visibleRows = (int)Math.Ceiling(height / cellHeight) + 1;
            var firstIndex = Math.Min(firstRow * _columns, Math.Max(0, itemCount - 1));
            var lastIndex = Math.Min(itemCount - 1, (firstRow + visibleRows) * _columns - 1);

            // 注意：基类的 VirtualizingPanel.ItemContainerGenerator 在首次测量的时序下
            // 可能返回 null（已实测：此时 ItemsControl.ItemContainerGenerator 是可用的），
            // 因此统一走 GetGenerator() 做回退。
            var generator = GetGenerator();
            if (generator == null) return new Size(width, height);

            var containerGenerator = generator;
            var startPos = containerGenerator.GeneratorPositionFromIndex(firstIndex);
            var childIndex = startPos.Offset == 0 ? startPos.Index : startPos.Index + 1;

            using (containerGenerator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                for (var i = firstIndex; i <= lastIndex; i++, childIndex++)
                {
                    bool isNewlyRealized;
                    var child = containerGenerator.GenerateNext(out isNewlyRealized) as UIElement;
                    if (child == null) break;

                    if (isNewlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count) AddInternalChild(child);
                        else InsertInternalChild(childIndex, child);
                        generator.PrepareItemContainer(child);
                    }
                    child.Measure(new Size(cellWidth, cellHeight));
                }
            }

            CleanUpItems(firstIndex, lastIndex);
            NotifyScrollOwner();

            return new Size(width, height);
        }

        private static double Resolve(double primary, double remembered, double fallback, double scrollOwnerValue)
        {
            if (!double.IsInfinity(primary) && primary > 0) return primary;
            if (scrollOwnerValue > 0) return scrollOwnerValue;
            if (remembered > 0) return remembered;
            return fallback;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var cellWidth = Math.Max(1, CellWidth);
            var cellHeight = Math.Max(1, CellHeight);
            var generator = GetGenerator();
            if (generator == null) return finalSize;

            for (var i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];
                var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
                if (itemIndex < 0) continue;

                var row = itemIndex / _columns;
                var col = itemIndex % _columns;
                child.Arrange(new Rect(col * cellWidth, row * cellHeight - _offset.Y, cellWidth, cellHeight));
            }
            return finalSize;
        }

        /// <summary>回收滚出可视范围的容器，避免长列表把内存撑起来。</summary>
        private void CleanUpItems(int minDesired, int maxDesired)
        {
            var children = InternalChildren;
            var generator = GetGenerator();
            if (generator == null) return;

            for (var i = children.Count - 1; i >= 0; i--)
            {
                var position = new GeneratorPosition(i, 0);
                var itemIndex = generator.IndexFromGeneratorPosition(position);
                if (itemIndex < minDesired || itemIndex > maxDesired)
                {
                    generator.Remove(position, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    if (args.Position.Index >= 0 && args.ItemUICount > 0)
                        RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    RemoveInternalChildRange(0, InternalChildren.Count);
                    _offset.Y = 0;
                    break;
            }
            InvalidateMeasure();
        }

        /// <summary>
        /// 取容器生成器。基类 VirtualizingPanel.ItemContainerGenerator 在首次测量的时序下
        /// 可能返回 null（已实测此时 ItemsControl.ItemContainerGenerator 是好的），故做回退。
        /// </summary>
        private IItemContainerGenerator GetGenerator()
        {
            var generator = ItemContainerGenerator;
            if (generator != null) return generator;
            var owner = ItemsControl.GetItemsOwner(this);
            return owner != null ? owner.ItemContainerGenerator : null;
        }

        /// <summary>控件尺寸或格子尺寸变化后由外部调用，触发重新排列。</summary>
        public void Refresh()
        {
            InvalidateMeasure();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameVault
{
    /// <summary>
    /// 正文里插入的图片。
    ///
    /// 为什么不用裸 <see cref="System.Windows.Controls.Image"/>：FlowDocument 存盘走 XAML 序列化，
    /// 而 BitmapSource 没有 XAML 转换器，Image 会被写成空元素、读回来就白了。
    /// 所以图片统一由本控件承载，存盘前把它换成一段带文件名的占位文本，
    /// 读回来再按占位文本换回图片 —— 见 <see cref="VaultView.SerializeReviewBody"/>。
    /// </summary>
    public class ReviewImageBox : Border
    {
        /// <summary>插图在插件数据目录下的文件名（不含目录）。</summary>
        public string FileName { get; set; }

        /// <summary>按文件名载入并显示。文件没了就留个空框，不抛异常。</summary>
        public void Load(string directory)
        {
            CornerRadius = new CornerRadius(6);
            Background = Frozen(Color.FromRgb(0x0E, 0x14, 0x1C));
            BorderBrush = Frozen(Color.FromRgb(0x2C, 0x38, 0x49));
            BorderThickness = new Thickness(1);
            Padding = new Thickness(6);
            Margin = new Thickness(4, 6, 4, 6);
            HorizontalAlignment = HorizontalAlignment.Left;

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(FileName)) return;

            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path)) return;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                // 正文区最宽也就六百来像素，解到 760 足够清晰，又不会把原图整张塞进内存
                bitmap.DecodePixelWidth = 760;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                Child = new Image
                {
                    Source = bitmap,
                    MaxWidth = 368,
                    MaxHeight = 300,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
            }
            catch
            {
                // 图片损坏 / 格式不支持：留个空框，正文其他内容照常显示
            }
        }

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>「选择游戏」下拉里的一行候选。</summary>
    public class ReviewPickItem
    {
        /// <summary>已评价过的游戏 Id。由 <see cref="VaultView.FillReviewPicker"/> 整体刷新。</summary>
        public static HashSet<Guid> Rated = new HashSet<Guid>();

        public GameEntry Entry { get; set; }
        public string Name { get { return Entry == null ? "" : Entry.Name; } }
        public string SearchKey { get; set; }

        // 刻意**不**暴露 Cover / Icon 转发属性：GameEntry 的图片是懒加载的，
        // 首次取值返回 null，靠 Raise 通知绑定刷新。绑在包装类的转发属性上，
        // 通知只发到 GameEntry、再传不到绑定，图片就永远空白。
        // XAML 里统一写 {Binding Entry.Icon} 这种嵌套路径，WPF 会自己去订阅
        // Entry.PropertyChanged。

        /// <summary>副标题：商店 · 时长。</summary>
        public string Sub
        {
            get
            {
                if (Entry == null) return "";
                var time = Entry.TotalPlaytime > 0
                    ? TimeFmt.Short(Entry.TotalPlaytime)
                    : L10n.T("LocNotPlayed");
                return Entry.StoreList + " · " + time;
            }
        }

        /// <summary>已经评价过的打个小标记，省得在长列表里翻。</summary>
        public Visibility RatedFlag
        {
            get
            {
                return Entry != null && Rated.Contains(Entry.PrimaryId)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>已评价海报列表里的一张卡。</summary>
    public class ReviewCard
    {
        /// <summary>星级上限。卡片和编辑器都以此为准，只在这里定义一次。</summary>
        public const int MaxStars = 10;

        public GameEntry Entry { get; set; }
        public string Name { get { return Entry == null ? "" : Entry.Name; } }

        // 同 ReviewPickItem：海报要走 {Binding Entry.Poster} 嵌套路径，
        // 不要在这里加转发属性（收不到懒加载完成的通知）。

        /// <summary>0–10 星，0 表示没打星。</summary>
        public int Stars { get; set; }

        /// <summary>最后修改时间。既是列表排序依据，也会在编辑器标题上显示出来。</summary>
        public string Updated { get; set; }

        /// <summary>总游玩时长。没玩过显示「还没玩过」。</summary>
        public string TimeLabel
        {
            get
            {
                if (Entry == null) return "";
                var time = Entry.TotalPlaytime > 0
                    ? TimeFmt.Short(Entry.TotalPlaytime)
                    : L10n.T("LocNotPlayed");
                return L10n.T("LocReviewTime") + " " + time;
            }
        }

        /// <summary>MC 评分（无数据时显示破折号）。</summary>
        public string McLabel
        {
            get
            {
                if (Entry == null) return "";
                return L10n.T("LocReviewMc") + " " + Entry.ScoreText;
            }
        }

        /// <summary>星级记号。0 星画一排空星，让「有卡片但没打星」和「有星星」一眼能分开。</summary>
        public string StarsText
        {
            get
            {
                if (Stars <= 0) return new string('☆', MaxStars);
                return new string('★', Stars) + new string('☆', MaxStars - Stars);
            }
        }

        /// <summary>星星右侧的「x/10」数字。0 星也照实写 0，不含糊。</summary>
        public string StarsValueText
        {
            get { return Stars.ToString(CultureInfo.InvariantCulture) + "/10"; }
        }

        /// <summary>
        /// 星级 → 画刷。分档：0–5 红 / 6–7 橙 / 8 绿 / 9–10 动态流彩。
        ///
        /// 编辑器顶部那 10 颗打分星和下面的卡片共用这一个方法，两处颜色必然一致。
        ///
        /// 9–10 星直接复用 <see cref="ScorePalette.SharedFlow"/> —— 那条画刷本来就是给
        /// 「MC 90+」用的，每帧由视图的定时器推进相位（见 VaultView.OnTintTick），
        /// 所以这里的满分档和库存里的 90+ 徽章是同一份、同一相位、同一套颜色。
        /// 颜色取自 ScorePalette，和 MC 档位配色保持一致，不另造一套色值。
        /// </summary>
        public static Brush BrushForStars(int stars)
        {
            if (stars >= 9) return ScorePalette.SharedFlow;
            if (stars >= 8) return ScorePalette.Get(ScorePalette.Great);
            if (stars >= 6) return ScorePalette.Get(ScorePalette.Good);
            return ScorePalette.Get(ScorePalette.Low);
        }

        /// <summary>本卡的星级画刷。</summary>
        public Brush StarsBrush
        {
            get { return BrushForStars(Stars); }
        }
    }

    /// <summary>
    /// 趣味功能页 · 游戏评价。
    ///
    /// 三块东西：
    ///   1) 顶部可搜索下拉 —— 选中游戏后展开编辑器；
    ///   2) 编辑器 —— 0–10 星 + 富文本正文（一级/二级标题、加粗、首行缩进两字、插图）；
    ///   3) 已评价海报列表 —— 每张海报下面带总时长、MC 评分、我的评星，点一下能回来改。
    ///
    /// 正文持久化用 FlowDocument 的 XAML 片段（TextRange + DataFormats.Xaml）。
    /// 插图在存盘前被换成「私用区定界符 + 文件名 + 私用区定界符」这样的占位文本，
    /// 真正的图片拷到插件数据目录的 reviews\ 下，正文里只记文件名 ——
    /// 这样占位符是自描述的，图片增删都不用再维护映射表。
    /// </summary>
    public partial class VaultView
    {
        /// <summary>插图占位符的定界符。用 Unicode 私用区字符，正常正文里不会出现。</summary>
        private const char ImageTokenStart = '\uE000';
        private const char ImageTokenEnd = '\uE001';

        /// <summary>星级上限与卡片共用一份定义，避免两处各写一个 10 走偏。</summary>
        private const int MaxStars = ReviewCard.MaxStars;

        // ---- 选择游戏 ----
        private ToggleButton reviewPickerToggle;
        private Popup reviewPickerPopup;
        private Border reviewPickerPanel;
        private TextBox reviewSearchBox;
        private TextBlock reviewSearchHint;
        private ItemsControl reviewPickList;
        private TextBlock reviewPickEmpty;
        private TextBlock reviewPickCount;

        // ---- 编辑器 ----
        private StackPanel reviewEditorHint;
        private Border reviewEditor;
        private StackPanel reviewStarsPanel;
        private TextBlock reviewStarsValue;
        private TextBlock reviewEditingText;
        private TextBlock reviewUpdatedText;
        private ToggleButton reviewBlockBody;
        private ToggleButton reviewBlockH1;
        private ToggleButton reviewBlockH2;
        private ToggleButton reviewBoldToggle;
        private ToggleButton reviewIndentToggle;
        private Button reviewImageButton;
        private Button reviewSaveButton;
        private Button reviewDeleteButton;
        private RichTextBox reviewBody;

        // ---- 已评价列表 ----
        private TextBlock reviewCountText;
        private ItemsControl reviewList;
        private StackPanel reviewListEmpty;

        private readonly ObservableCollection<ReviewPickItem> reviewPickItems = new ObservableCollection<ReviewPickItem>();
        private readonly ObservableCollection<ReviewCard> reviewRows = new ObservableCollection<ReviewCard>();
        private readonly List<TextBlock> reviewStarGlyphs = new List<TextBlock>();

        /// <summary>正在编辑的那款游戏；null 表示编辑器收起。</summary>
        private GameEntry reviewTarget;

        /// <summary>编辑器里的当前分值（还没落盘）。</summary>
        private int reviewDraftStars;

        /// <summary>抑制工具栏的勾选同步，避免程序化设值时把自己再套一遍。</summary>
        private bool suppressReviewToolSync;

        // ==================================================================
        // 接线
        // ==================================================================

        private void WireReviewPane()
        {
            reviewPickerToggle = root.FindName("ReviewPickerToggle") as ToggleButton;
            reviewPickerPopup = root.FindName("ReviewPickerPopup") as Popup;
            reviewPickerPanel = root.FindName("ReviewPickerPanel") as Border;
            reviewSearchBox = root.FindName("ReviewSearchBox") as TextBox;
            reviewSearchHint = root.FindName("ReviewSearchHint") as TextBlock;
            reviewPickList = root.FindName("ReviewPickList") as ItemsControl;
            reviewPickEmpty = root.FindName("ReviewPickEmpty") as TextBlock;
            reviewPickCount = root.FindName("ReviewPickCount") as TextBlock;

            reviewEditorHint = root.FindName("ReviewEditorHint") as StackPanel;
            reviewEditor = root.FindName("ReviewEditor") as Border;
            reviewStarsPanel = root.FindName("ReviewStarsPanel") as StackPanel;
            reviewStarsValue = root.FindName("ReviewStarsValue") as TextBlock;
            reviewEditingText = root.FindName("ReviewEditingText") as TextBlock;
            reviewUpdatedText = root.FindName("ReviewUpdatedText") as TextBlock;
            reviewBlockBody = root.FindName("ReviewBlockBody") as ToggleButton;
            reviewBlockH1 = root.FindName("ReviewBlockH1") as ToggleButton;
            reviewBlockH2 = root.FindName("ReviewBlockH2") as ToggleButton;
            reviewBoldToggle = root.FindName("ReviewBoldToggle") as ToggleButton;
            reviewIndentToggle = root.FindName("ReviewIndentToggle") as ToggleButton;
            reviewImageButton = root.FindName("ReviewImageButton") as Button;
            reviewSaveButton = root.FindName("ReviewSaveButton") as Button;
            reviewDeleteButton = root.FindName("ReviewDeleteButton") as Button;
            reviewBody = root.FindName("ReviewBody") as RichTextBox;

            reviewCountText = root.FindName("ReviewCountText") as TextBlock;
            reviewList = root.FindName("ReviewList") as ItemsControl;
            reviewListEmpty = root.FindName("ReviewListEmpty") as StackPanel;

            if (reviewPickList != null) reviewPickList.ItemsSource = reviewPickItems;
            if (reviewList != null) reviewList.ItemsSource = reviewRows;

            BuildReviewStars();

            if (reviewPickerToggle != null)
            {
                reviewPickerToggle.Checked += (s, e) =>
                {
                    SizeReviewPicker();
                    if (reviewPickerPopup != null) reviewPickerPopup.IsOpen = true;
                    if (reviewSearchBox != null)
                    {
                        reviewSearchBox.Text = "";
                        reviewSearchBox.Focus();
                    }
                    FillReviewPicker();
                };
                reviewPickerToggle.Unchecked += (s, e) =>
                {
                    if (reviewPickerPopup != null) reviewPickerPopup.IsOpen = false;
                };
            }

            if (reviewPickerPopup != null)
                reviewPickerPopup.Closed += (s, e) =>
                {
                    if (reviewPickerToggle != null && reviewPickerToggle.IsChecked == true)
                        reviewPickerToggle.IsChecked = false;
                };

            if (reviewSearchBox != null)
            {
                reviewSearchBox.TextChanged += (s, e) =>
                {
                    if (reviewSearchHint != null)
                        reviewSearchHint.Visibility = string.IsNullOrEmpty(reviewSearchBox.Text)
                            ? Visibility.Visible : Visibility.Collapsed;
                    ApplyReviewPickFilter();
                };
            }

            // 候选行是模板生成的，点哪一行都一样：在容器上接一次冒泡，
            // 靠 DataContext 反查是哪款游戏。
            if (reviewPickList != null)
                reviewPickList.PreviewMouseLeftButtonUp += OnReviewPickRowClick;

            // 点海报卡片 → 回到编辑器改这一款
            if (reviewList != null)
                reviewList.PreviewMouseLeftButtonUp += OnReviewCardClick;

            if (reviewBlockBody != null) reviewBlockBody.Click += (s, e) => ApplyBlockStyle("body");
            if (reviewBlockH1 != null) reviewBlockH1.Click += (s, e) => ApplyBlockStyle("h1");
            if (reviewBlockH2 != null) reviewBlockH2.Click += (s, e) => ApplyBlockStyle("h2");
            if (reviewBoldToggle != null) reviewBoldToggle.Click += (s, e) => ApplyBold();
            if (reviewIndentToggle != null) reviewIndentToggle.Click += (s, e) => ApplyIndent();
            if (reviewImageButton != null) reviewImageButton.Click += OnReviewInsertImageClick;
            if (reviewSaveButton != null) reviewSaveButton.Click += OnReviewSaveClick;
            if (reviewDeleteButton != null) reviewDeleteButton.Click += OnReviewDeleteClick;

            if (reviewBody != null)
            {
                reviewBody.TextChanged += (s, e) => SyncReviewToolbarFromCaret();
                reviewBody.SelectionChanged += (s, e) => SyncReviewToolbarFromCaret();
            }

            SetReviewEditorOpen(null);
        }

        /// <summary>星级按钮在 XAML 里写 10 遍太啰嗦，直接按分值上限生成。</summary>
        private void BuildReviewStars()
        {
            if (reviewStarsPanel == null) return;

            var style = root.TryFindResource("ReviewStar") as Style;
            for (var i = 1; i <= MaxStars; i++)
            {
                var glyph = new TextBlock
                {
                    Text = "☆",
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center
                };
                glyph.SetValue(TextBlock.ForegroundProperty, FaintStarBrush);

                var captured = i;
                var button = new Button
                {
                    Style = style,
                    Tag = i,
                    ToolTip = i.ToString(CultureInfo.InvariantCulture),
                    Content = glyph
                };
                button.Click += (s, e) => OnReviewStarClick(captured);

                reviewStarsPanel.Children.Add(button);
                reviewStarGlyphs.Add(glyph);
            }
        }

        // ==================================================================
        // 选择游戏
        // ==================================================================

        /// <summary>下拉面板尺寸：跟待玩清单一个思路，按窗口实际大小现算。</summary>
        private void SizeReviewPicker()
        {
            if (reviewPickerPanel == null) return;

            double winH = 0, winW = 0;
            var win = Window.GetWindow(this);
            if (win != null)
            {
                winH = win.ActualHeight;
                winW = win.ActualWidth;
            }
            if (!(winH > 0)) winH = SystemParameters.WorkArea.Height;
            if (!(winW > 0)) winW = SystemParameters.WorkArea.Width;

            double h = Math.Round(winH * 0.5);
            if (h < 260) h = 260;
            if (h > 700) h = 700;
            reviewPickerPanel.Height = h;

            double w = Math.Round(winW * 0.36);
            if (w < 400) w = 400;
            if (w > 620) w = 620;
            reviewPickerPanel.Width = w;
        }

        /// <summary>重建候选列表（整个库存，按名称排序），并标出哪些已经评价过。</summary>
        private void FillReviewPicker()
        {
            if (reviewPickList == null) return;

            var rated = new HashSet<Guid>();
            foreach (var pair in VaultSettings.Reviews)
            {
                Guid id;
                if (Guid.TryParse(pair.Key, out id)) rated.Add(id);
            }
            ReviewPickItem.Rated = rated;

            var candidates = entries == null
                ? new List<GameEntry>()
                : entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            reviewPickItems.Clear();
            foreach (var entry in candidates)
            {
                reviewPickItems.Add(new ReviewPickItem
                {
                    Entry = entry,
                    SearchKey = (entry.Name ?? "").ToLowerInvariant()
                });
            }

            ApplyReviewPickFilter();
        }

        /// <summary>按搜索词过滤候选；搜索框为空则全部显示。</summary>
        private void ApplyReviewPickFilter()
        {
            if (reviewPickList == null) return;

            var query = reviewSearchBox == null ? "" : (reviewSearchBox.Text ?? "").Trim().ToLowerInvariant();

            // ItemsControl 没有内置过滤，用 CollectionView 的 Filter 更省事
            var view = CollectionViewSource.GetDefaultView(reviewPickItems);
            if (view != null)
            {
                view.Filter = o =>
                {
                    if (query.Length == 0) return true;
                    var item = o as ReviewPickItem;
                    return item != null && item.SearchKey != null
                        && item.SearchKey.IndexOf(query, StringComparison.Ordinal) >= 0;
                };
            }

            var shown = 0;
            foreach (var item in reviewPickItems)
                if (query.Length == 0 || (item.SearchKey != null &&
                    item.SearchKey.IndexOf(query, StringComparison.Ordinal) >= 0)) shown++;

            if (reviewPickEmpty != null)
                reviewPickEmpty.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (reviewPickCount != null)
                reviewPickCount.Text = L10n.F("LocReviewPickCount", entries != null ? entries.Count : 0);
        }

        private void OnReviewPickRowClick(object sender, MouseButtonEventArgs e)
        {
            var item = FindDataContext<ReviewPickItem>(e.OriginalSource as DependencyObject);
            if (item == null || item.Entry == null) return;

            // 点中就收起面板，顺手把 toggle 也放下来
            if (reviewPickerPopup != null) reviewPickerPopup.IsOpen = false;
            if (reviewPickerToggle != null) reviewPickerToggle.IsChecked = false;

            OpenReviewEditor(item.Entry);
            e.Handled = true;
        }

        private void OnReviewCardClick(object sender, MouseButtonEventArgs e)
        {
            var card = FindDataContext<ReviewCard>(e.OriginalSource as DependencyObject);
            if (card == null || card.Entry == null) return;
            OpenReviewEditor(card.Entry);
            e.Handled = true;
        }

        /// <summary>从事件源往上找第一个指定类型的 DataContext（行是模板生成的）。</summary>
        private static T FindDataContext<T>(DependencyObject node) where T : class
        {
            while (node != null)
            {
                var element = node as FrameworkElement;
                if (element != null)
                {
                    var context = element.DataContext as T;
                    if (context != null) return context;
                }
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        // ==================================================================
        // 编辑器：打开 / 关闭
        // ==================================================================

        /// <summary>切到某款游戏（或传 null 收起编辑器），并把这款已存的评价读进来。</summary>
        private void OpenReviewEditor(GameEntry entry)
        {
            reviewTarget = entry;
            SetReviewEditorOpen(entry);
            if (entry == null) return;

            var saved = VaultSettings.GetReview(entry.PrimaryId);
            reviewDraftStars = saved != null ? ClampStars(saved.Stars) : 0;
            LoadReviewBody(saved != null ? saved.Body : null);

            RefreshReviewHeader(entry, saved);

            SyncReviewStars();
            SyncReviewToolbarFromCaret();

            if (reviewBody != null) reviewBody.Focus();
        }

        /// <summary>
        /// 刷新编辑器右上角的两行标题：游戏名单行，「上次评价 时间」另起一行。
        /// 还没评价过的只显示游戏名，第二行留空（而不是写「未评价」，避免多一行废话）。
        /// 保存 / 删除后时间戳会变，所以这两个地方都要重刷一次。
        /// </summary>
        private void RefreshReviewHeader(GameEntry entry, GameReview saved)
        {
            if (reviewEditingText == null || entry == null) return;

            reviewEditingText.Text = L10n.F("LocReviewEditing", entry.Name);

            if (reviewUpdatedText != null)
                reviewUpdatedText.Text = saved != null && !string.IsNullOrEmpty(saved.Updated)
                    ? L10n.F("LocReviewLastUpdated", saved.Updated)
                    : "";
        }

        private void SetReviewEditorOpen(GameEntry entry)
        {
            var open = entry != null;
            SetVisible(reviewEditor, open);
            SetVisible(reviewEditorHint, !open);

            // 还没评价过才给「删除评价」；打过星或写过正文的都算评价过
            SetVisible(reviewDeleteButton, open && VaultSettings.GetReview(entry.PrimaryId) != null);
        }

        private static int ClampStars(int value)
        {
            if (value < 0) return 0;
            if (value > MaxStars) return MaxStars;
            return value;
        }

        // ==================================================================
        // 编辑器：星级
        // ==================================================================

        private void OnReviewStarClick(int value)
        {
            if (reviewTarget == null) return;

            // 再点同一颗 = 取消打分
            reviewDraftStars = reviewDraftStars == value ? 0 : value;
            SyncReviewStars();

            // 星级是个独立、离散的动作，点一下就该存 —— 否则为了打个星还得再点一次「保存评价」。
            // 只更新星级不动正文：编辑器里可能还有没保存的草稿。
            SaveReview(false);
        }

        private void SyncReviewStars()
        {
            // 和「已评价」卡片走同一个取色方法，两处颜色必然一致：
            // 0–5 红 / 6–7 橙 / 8 绿 / 9–10 流彩。空星恒为灰色，不参与分档。
            var brush = ReviewCard.BrushForStars(reviewDraftStars);
            for (var i = 0; i < reviewStarGlyphs.Count; i++)
            {
                var filled = i < reviewDraftStars;
                reviewStarGlyphs[i].Text = filled ? "★" : "☆";
                reviewStarGlyphs[i].SetValue(TextBlock.ForegroundProperty,
                    filled ? brush : FaintStarBrush);
            }

            if (reviewStarsValue != null)
            {
                reviewStarsValue.Text = L10n.F("LocReviewStarsValue", reviewDraftStars);
                // 读数也跟着当前档位变色，和星星呼应
                reviewStarsValue.SetValue(TextBlock.ForegroundProperty, brush);
            }
        }

        /// <summary>没打星时星星的灰色。</summary>
        private static readonly Brush FaintStarBrush = Frozen(Color.FromRgb(0x5A, 0x66, 0x73));

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        // ==================================================================
        // 编辑器：富文本命令
        // ==================================================================

        /// <summary>段落样式：正文 / 一级标题 / 二级标题。作用在选区覆盖的段落上。</summary>
        private void ApplyBlockStyle(string style)
        {
            if (reviewBody == null) return;

            var paragraphs = SelectedParagraphs();
            if (paragraphs.Count == 0) return;

            var heading = style == "h1" || style == "h2";
            var size = style == "h1" ? 19.0 : style == "h2" ? 15.5 : 13.5;
            var top = style == "h1" ? 10.0 : style == "h2" ? 8.0 : 2.0;

            foreach (var paragraph in paragraphs)
            {
                // 段落级格式直接挂在 Paragraph 上：它是 TextElement，附加属性可以直接设实例值
                TextElement.SetFontSize(paragraph, size);
                TextElement.SetFontWeight(paragraph, heading ? FontWeights.Bold : FontWeights.Normal);
                paragraph.Margin = new Thickness(0, top, 0, 4);

                // 标题默认不缩进，免得和「首行缩进两字」打架
                if (heading) paragraph.TextIndent = 0;
            }

            SetReviewBlockState(style);
            SetReviewIndentState(false);
            SyncReviewToolbarFromCaret();
        }

        private void ApplyBold()
        {
            if (reviewBody == null || reviewBody.CaretPosition == null) return;

            // 选区为空时作用在"接下来要输入的字"上，所以拿光标往后一个字符的字重作为翻转基准
            var probe = reviewBody.Selection;
            var range = probe.IsEmpty
                ? new TextRange(reviewBody.CaretPosition,
                    reviewBody.CaretPosition.GetPositionAtOffset(1) ?? reviewBody.CaretPosition)
                : new TextRange(probe.Start, probe.End);

            // GetPropertyValue 只有一个参数（没有默认值重载），取不到时自己兜底
            var current = range.GetPropertyValue(TextElement.FontWeightProperty);
            var turningOff = current is FontWeight && (FontWeight)current == FontWeights.Bold;

            // 选区级格式只能通过 TextRange.ApplyPropertyValue 施加 ——
            // TextRange / TextSelection 本身都没有 FontWeight 这样的属性。
            range.ApplyPropertyValue(TextElement.FontWeightProperty,
                turningOff ? FontWeights.Normal : FontWeights.Bold);
            reviewBody.Focus();
        }

        /// <summary>首行缩进两字。缩进宽度按当前字号算 —— 一个汉字约等于一个 em。</summary>
        private void ApplyIndent()
        {
            if (reviewBody == null) return;

            var paragraphs = SelectedParagraphs();
            if (paragraphs.Count == 0) return;

            var already = IsIndented(paragraphs[0]);
            foreach (var paragraph in paragraphs)
                paragraph.TextIndent = already ? 0 : CurrentFontSize(paragraph) * 2;

            SetReviewIndentState(!already);
            reviewBody.Focus();
        }

        private static bool IsIndented(Paragraph paragraph)
        {
            return Math.Abs(paragraph.TextIndent) > 0.01;
        }

        private static double CurrentFontSize(Paragraph paragraph)
        {
            var size = TextElement.GetFontSize(paragraph);
            // 空段落取不到有效字号（继承不上），回落到编辑区的默认字号
            return double.IsNaN(size) || size <= 0 ? 13.5 : size;
        }

        private static bool IsBold(Paragraph paragraph)
        {
            return TextElement.GetFontWeight(paragraph) == FontWeights.Bold;
        }

        /// <summary>选区覆盖到的所有段落；选区为空时就是光标那一段。</summary>
        private List<Paragraph> SelectedParagraphs()
        {
            var result = new List<Paragraph>();
            if (reviewBody == null) return result;

            if (reviewBody.Selection.IsEmpty)
            {
                var single = reviewBody.CaretPosition == null ? null : reviewBody.CaretPosition.Paragraph;
                if (single != null) result.Add(single);
                return result;
            }

            // Paragraph 之间用 Block.NextBlock 走（Paragraph 本身没有 Next 方法，
            // 而 NextBlock 是个属性不是方法）
            var start = reviewBody.Selection.Start.Paragraph;
            var end = reviewBody.Selection.End.Paragraph;
            if (start == null) return result;

            for (var block = (Block)start; block != null; block = block.NextBlock)
            {
                var paragraph = block as Paragraph;
                if (paragraph != null) result.Add(paragraph);
                if (block == end) break;
            }
            return result;
        }

        private void SetReviewBlockState(string style)
        {
            suppressReviewToolSync = true;
            try
            {
                if (reviewBlockBody != null) reviewBlockBody.IsChecked = style == "body";
                if (reviewBlockH1 != null) reviewBlockH1.IsChecked = style == "h1";
                if (reviewBlockH2 != null) reviewBlockH2.IsChecked = style == "h2";
            }
            finally
            {
                suppressReviewToolSync = false;
            }
        }

        private void SetReviewIndentState(bool on)
        {
            suppressReviewToolSync = true;
            try
            {
                if (reviewIndentToggle != null) reviewIndentToggle.IsChecked = on;
            }
            finally
            {
                suppressReviewToolSync = false;
            }
        }

        private void SetReviewBoldState(bool on)
        {
            suppressReviewToolSync = true;
            try
            {
                if (reviewBoldToggle != null) reviewBoldToggle.IsChecked = on;
            }
            finally
            {
                suppressReviewToolSync = false;
            }
        }

        /// <summary>光标移动 / 选区变化时，把工具栏状态刷成光标所在段落的样子。</summary>
        private void SyncReviewToolbarFromCaret()
        {
            if (reviewBody == null || suppressReviewToolSync) return;

            var paragraphs = SelectedParagraphs();
            if (paragraphs.Count == 0) return;
            var paragraph = paragraphs[0];

            var size = CurrentFontSize(paragraph);
            SetReviewBlockState(size >= 18 ? "h1" : size >= 15 ? "h2" : "body");
            SetReviewIndentState(IsIndented(paragraph));
            SetReviewBoldState(IsBold(paragraph));
        }

        // ==================================================================
        // 编辑器：插图
        // ==================================================================

        private void OnReviewInsertImageClick(object sender, RoutedEventArgs e)
        {
            if (reviewBody == null || reviewTarget == null) return;

            string source;
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = L10n.T("LocReviewImgDialog"),
                    Filter = L10n.T("LocReviewImgFilter"),
                    Multiselect = false
                };
                if (dialog.ShowDialog() != true) return;
                source = dialog.FileName;
            }
            catch
            {
                return;
            }

            var directory = VaultSettings.ReviewImageDirectory;
            if (string.IsNullOrEmpty(directory)) return;

            try
            {
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                // 拷进插件数据目录：正文里只记文件名，不留本机绝对路径
                var fileName = Guid.NewGuid().ToString("N") + GuessImageExtension(source);
                File.Copy(source, Path.Combine(directory, fileName), true);

                var box = new ReviewImageBox { FileName = fileName };
                box.Load(directory);

                reviewBody.Focus();
                InsertInlineAtCaret(new InlineUIContainer(box));
            }
            catch
            {
                FlashStatus(L10n.T("LocReviewImgFailed"));
            }
        }

        /// <summary>
        /// 在光标处插入一个 Inline。
        ///
        /// WPF 没有「TextSelection.InsertEmbedded」这种便捷方法（TextRange / TextSelection
        /// 都只有 ApplyPropertyValue / GetPropertyValue），唯一稳妥的路径是拿到光标所在
        /// Inline 的 InlineCollection，再用它的 InsertBefore / InsertAfter。
        /// 段落是空的（一个 Inline 都没有）时退化成追加到段末。
        /// </summary>
        private void InsertInlineAtCaret(Inline inline)
        {
            if (reviewBody == null || inline == null) return;

            var caret = reviewBody.CaretPosition;
            if (caret == null) return;

            // 光标往前找它所在（或紧邻）的那个 Inline，插在它后面
            var before = caret.GetAdjacentElement(LogicalDirection.Backward) as Inline;
            if (before != null && before.SiblingInlines != null)
            {
                before.SiblingInlines.InsertAfter(inline, before);
                return;
            }

            // 光标在段首：插到后一个 Inline 之前
            var after = caret.GetAdjacentElement(LogicalDirection.Forward) as Inline;
            if (after != null && after.SiblingInlines != null)
            {
                after.SiblingInlines.InsertBefore(inline, after);
                return;
            }

            // 空段落：直接追加
            var paragraph = caret.Paragraph;
            if (paragraph != null) paragraph.Inlines.Add(inline);
        }

        private static string GuessImageExtension(string path)
        {
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) || ext.Length > 6 ? ".png" : ext.ToLowerInvariant();
        }

        // ==================================================================
        // 编辑器：存 / 取正文
        // ==================================================================

        /// <summary>
        /// 把编辑器内容存成 FlowDocument 的 XAML 片段。
        ///
        /// 插图必须先换成「定界符 + 文件名 + 定界符」的占位文本 —— 裸 Image 里的 BitmapSource
        /// 没法被 XAML 序列化（实测会抛 InvalidOperationException），不换就存不下来。
        /// 占位符自描述，所以不需要额外维护「序号 → 文件名」的映射表。
        ///
        /// 替换是在**活动文档**上做的（没法先序列化再改副本：带着图的文档根本序列化不了），
        /// 所以序列化完必须把图片原样塞回去 —— 否则用户点一次保存，眼前的图就全没了。
        /// </summary>
        private string SerializeReviewBody()
        {
            if (reviewBody == null || reviewBody.Document == null) return null;

            var images = new List<KeyValuePair<Paragraph, InlineUIContainer>>();
            CollectImageContainers(reviewBody.Document.Blocks, images);

            // 记录 (所在段落, 占位 Run, 原图片容器)，最后靠它原样还原
            var swapped = new List<KeyValuePair<Paragraph, Run>>();
            var boxes = new List<InlineUIContainer>();
            foreach (var image in images)
            {
                var paragraph = image.Key;
                var container = image.Value;
                var box = container.Child as ReviewImageBox;
                if (box == null || string.IsNullOrEmpty(box.FileName)) continue;

                var token = new Run(BuildImageToken(box.FileName));
                // InsertBefore 按引用定位，先插占位再删原图，锚点始终有效
                paragraph.Inlines.InsertBefore(token, container);
                paragraph.Inlines.Remove(container);

                swapped.Add(new KeyValuePair<Paragraph, Run>(paragraph, token));
                boxes.Add(container);
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    var range = new TextRange(reviewBody.Document.ContentStart, reviewBody.Document.ContentEnd);
                    range.Save(stream, DataFormats.Xaml);
                    var bytes = stream.ToArray();
                    return bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                // 无论成败都要把图片放回去，否则编辑器会被留下一个只有占位符的文档
                for (var i = 0; i < swapped.Count; i++)
                {
                    var paragraph = swapped[i].Key;
                    var token = swapped[i].Value;
                    paragraph.Inlines.InsertBefore(boxes[i], token);
                    paragraph.Inlines.Remove(token);
                }
            }
        }

        private static void CollectImageContainers(
            BlockCollection blocks, List<KeyValuePair<Paragraph, InlineUIContainer>> found)
        {
            foreach (var block in blocks)
            {
                var paragraph = block as Paragraph;
                if (paragraph != null)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        var container = inline as InlineUIContainer;
                        if (container != null && container.Child is ReviewImageBox)
                            found.Add(new KeyValuePair<Paragraph, InlineUIContainer>(paragraph, container));
                    }
                    continue;
                }

                var section = block as Section;
                if (section != null) CollectImageContainers(section.Blocks, found);
            }
        }

        private static string BuildImageToken(string fileName)
        {
            return ImageTokenStart + fileName + ImageTokenEnd;
        }

        /// <summary>把存好的 XAML 片段读回编辑器；读不了就按纯文本兜底，别把用户的字弄丢。</summary>
        private void LoadReviewBody(string body)
        {
            if (reviewBody == null) return;

            var document = new FlowDocument { PagePadding = new Thickness(0) };
            var directory = VaultSettings.ReviewImageDirectory;

            if (!string.IsNullOrEmpty(body))
            {
                var loaded = false;
                try
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body)))
                    {
                        var range = new TextRange(document.ContentStart, document.ContentEnd);
                        range.Load(stream, DataFormats.Xaml);
                    }
                    loaded = true;
                }
                catch
                {
                    loaded = false;
                }

                if (!loaded)
                {
                    // 版本升级 / 文件损坏导致 XAML 读不回来：至少把纯文字保住
                    document = new FlowDocument { PagePadding = new Thickness(0) };
                    document.Blocks.Add(new Paragraph(new Run(body)));
                    FlashStatus(L10n.T("LocReviewBodyBroken"));
                }
                else
                {
                    RestoreImageTokens(document, directory);
                }
            }

            reviewBody.Document = document;
        }

        /// <summary>把占位文本换回真正的插图控件。图片文件不在了就跳过，只提示一次。</summary>
        private void RestoreImageTokens(FlowDocument document, string directory)
        {
            var missing = 0;
            var targets = new List<KeyValuePair<Paragraph, Run>>();

            foreach (var paragraph in document.Blocks.OfType<Paragraph>())
                foreach (var run in paragraph.Inlines.OfType<Run>())
                    if (!string.IsNullOrEmpty(run.Text) && run.Text.IndexOf(ImageTokenStart) >= 0)
                        targets.Add(new KeyValuePair<Paragraph, Run>(paragraph, run));

            foreach (var target in targets)
            {
                var paragraph = target.Key;
                var run = target.Value;
                var text = run.Text;

                // 占位符可能和普通文字混在同一段 Run 里（用户没敲回车就插了图），
                // 所以按定界符切开，逐段判断哪一段是文件名、哪一段是正文。
                // 统一往 run 前面插（InsertBefore 锚点固定是 run），最后再把原 run 删掉。
                var pieces = new List<Inline>();
                var cursor = 0;
                while (cursor < text.Length)
                {
                    var open = text.IndexOf(ImageTokenStart, cursor);
                    if (open < 0)
                    {
                        pieces.Add(new Run(text.Substring(cursor)));
                        break;
                    }

                    if (open > cursor) pieces.Add(new Run(text.Substring(cursor, open - cursor)));

                    var close = text.IndexOf(ImageTokenEnd, open + 1);
                    if (close < 0)
                    {
                        // 定界符不成对：当成普通文字处理，避免吃掉后面的内容
                        pieces.Add(new Run(text.Substring(open)));
                        break;
                    }

                    var fileName = text.Substring(open + 1, close - open - 1);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        var full = string.IsNullOrEmpty(directory)
                            ? null : Path.Combine(directory, fileName);

                        // 图片文件不在了（手动删过 / 换了台机器）就跳过并计数，
                        // 免得正文里留一个空框还以为图没加载出来
                        if (full == null || !File.Exists(full)) missing++;
                        else
                        {
                            var box = new ReviewImageBox { FileName = fileName };
                            box.Load(directory);
                            pieces.Add(new InlineUIContainer(box));
                        }
                    }
                    cursor = close + 1;
                }

                foreach (var piece in pieces) paragraph.Inlines.InsertBefore(piece, run);
                paragraph.Inlines.Remove(run);
            }

            if (missing > 0) FlashStatus(L10n.T("LocReviewImgMissing"));
        }

        // ==================================================================
        // 保存 / 删除
        // ==================================================================

        private void OnReviewSaveClick(object sender, RoutedEventArgs e)
        {
            if (reviewTarget == null)
            {
                FlashStatus(L10n.T("LocReviewNeedGame"));
                return;
            }

            if (reviewDraftStars == 0 && IsBodyEmpty()) FlashStatus(L10n.T("LocReviewNeedBody"));
            SaveReview(true);
        }

        /// <summary>正文里既没有字也没有插图。</summary>
        private bool IsBodyEmpty()
        {
            if (reviewBody == null || reviewBody.Document == null) return true;

            foreach (var paragraph in reviewBody.Document.Blocks.OfType<Paragraph>())
            {
                if (new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim().Length > 0)
                    return false;
                foreach (var inline in paragraph.Inlines)
                    if (inline is InlineUIContainer) return false;
            }
            return true;
        }

        private void OnReviewDeleteClick(object sender, RoutedEventArgs e)
        {
            if (reviewTarget == null) return;

            var name = reviewTarget.Name;
            VaultSettings.RemoveReview(reviewTarget.PrimaryId);

            reviewDraftStars = 0;
            LoadReviewBody(null);
            SyncReviewStars();
            SetReviewEditorOpen(reviewTarget);
            // 删掉了，标题里的「上次评价」也要跟着没
            RefreshReviewHeader(reviewTarget, null);
            RebuildReviewRows();
            FlashStatus(L10n.F("LocReviewDeleted", name));
        }

        /// <summary>
        /// 落盘。<paramref name="withBody"/> 为 false 时只更新星级 ——
        /// 此时编辑器里的字可能还是草稿，不能顺手一起存进去。
        /// </summary>
        private void SaveReview(bool withBody)
        {
            if (reviewTarget == null) return;

            var existing = VaultSettings.GetReview(reviewTarget.PrimaryId);
            var body = existing != null ? existing.Body : null;

            if (withBody)
            {
                // 先判空再序列化：空文档序列化出来仍是一段"只有一个空段落"的 XAML，
                // 不归一化成 null 的话这款游戏会被当成"评价过"混进下面的列表（0 星 + 没字）。
                // 判空必须在序列化之前做，那时文档才是完好的原始状态。
                body = IsBodyEmpty() ? null : SerializeReviewBody();
            }

            var name = reviewTarget.Name;
            var stars = reviewDraftStars;

            // 星级和正文都空了 = 这款其实没被评价过，删记录而不是留一条空的
            if (stars == 0 && string.IsNullOrEmpty(body))
            {
                VaultSettings.RemoveReview(reviewTarget.PrimaryId);
            }
            else
            {
                VaultSettings.SetReview(reviewTarget.PrimaryId, new GameReview
                {
                    Stars = stars,
                    Body = body,
                    Updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                });
            }

            SetReviewEditorOpen(reviewTarget);
            // 刚落盘，时间戳是新的 —— 标题上的「上次评价」要跟着刷新，
            // 不然用户会看到自己刚写完的正文还标着旧时间
            RefreshReviewHeader(reviewTarget, VaultSettings.GetReview(reviewTarget.PrimaryId));
            RebuildReviewRows();

            if (withBody) FlashStatus(L10n.F("LocReviewSaved", name));
            else if (stars > 0) FlashStatus(L10n.F("LocReviewRateSaved", name, stars));
        }

        // ==================================================================
        // 已评价海报列表
        // ==================================================================

        /// <summary>重建已评价列表。库存刷新、切语言、切进这一页时都会调。</summary>
        private void RefreshReviewPane()
        {
            if (reviewPickList == null && reviewList == null) return;

            // 候选列表里的「已评价」小标记要跟着最新记录走
            if (reviewPickerPopup != null && reviewPickerPopup.IsOpen) FillReviewPicker();

            RebuildReviewRows();
        }

        private void RebuildReviewRows()
        {
            if (reviewList == null) return;

            var byId = new Dictionary<Guid, GameEntry>();
            if (entries != null)
                foreach (var entry in entries)
                    if (!byId.ContainsKey(entry.PrimaryId)) byId[entry.PrimaryId] = entry;

            var records = VaultSettings.Reviews;
            var cards = new List<ReviewCard>();
            var orphans = new List<Guid>();

            foreach (var pair in records)
            {
                Guid id;
                if (pair.Value == null || !Guid.TryParse(pair.Key, out id)) continue;

                GameEntry entry;
                if (!byId.TryGetValue(id, out entry))
                {
                    // 游戏已经从库里删掉了：评价留着也没地方显示，顺手清掉
                    orphans.Add(id);
                    continue;
                }

                cards.Add(new ReviewCard
                {
                    Entry = entry,
                    Stars = ClampStars(pair.Value.Stars),
                    Updated = pair.Value.Updated ?? ""
                });
            }

            foreach (var id in orphans) VaultSettings.RemoveReview(id);

            // 最近改过的排前面，其余按名字排
            var ordered = cards
                .OrderByDescending(c => c.Updated, StringComparer.Ordinal)
                .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);

            reviewRows.Clear();
            foreach (var card in ordered) reviewRows.Add(card);

            if (reviewCountText != null) reviewCountText.Text = reviewRows.Count.ToString();
            SetVisible(reviewListEmpty, reviewRows.Count == 0);
        }
    }
}

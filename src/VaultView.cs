using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Playnite.SDK;

namespace GameVault
{
    public enum SortMode
    {
        Total,
        Recent,
        Score
    }

    public enum ViewMode
    {
        Grid,
        Bar
    }

    public class VaultViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string entryCountText = "0";
        private string rawCountText = "";
        private string totalPlaytimeText = "0h";
        private string playedCountText = "";
        private string recentPlaytimeText = "0h";
        private string activeCountText = "";
        private string topGameName = "—";
        private string topGameTimeText = "";
        private string averageScoreText = "—";
        private string scoredCountText = "";
        private string statusText = "正在加载…";
        private string sourceText = "";
        private double cardWidth = 172;
        private double coverHeight = 241;

        public string EntryCountText { get { return entryCountText; } set { entryCountText = value; Raise("EntryCountText"); } }
        public string RawCountText { get { return rawCountText; } set { rawCountText = value; Raise("RawCountText"); } }
        public string TotalPlaytimeText { get { return totalPlaytimeText; } set { totalPlaytimeText = value; Raise("TotalPlaytimeText"); } }
        public string PlayedCountText { get { return playedCountText; } set { playedCountText = value; Raise("PlayedCountText"); } }
        public string RecentPlaytimeText { get { return recentPlaytimeText; } set { recentPlaytimeText = value; Raise("RecentPlaytimeText"); } }
        public string ActiveCountText { get { return activeCountText; } set { activeCountText = value; Raise("ActiveCountText"); } }
        public string TopGameName { get { return topGameName; } set { topGameName = value; Raise("TopGameName"); } }
        public string TopGameTimeText { get { return topGameTimeText; } set { topGameTimeText = value; Raise("TopGameTimeText"); } }
        public string AverageScoreText { get { return averageScoreText; } set { averageScoreText = value; Raise("AverageScoreText"); } }
        public string ScoredCountText { get { return scoredCountText; } set { scoredCountText = value; Raise("ScoredCountText"); } }
        public string StatusText { get { return statusText; } set { statusText = value; Raise("StatusText"); } }
        public string SourceText { get { return sourceText; } set { sourceText = value; Raise("SourceText"); } }

        /// <summary>网格卡片宽度（由右下角缩放条控制，会持久化）</summary>
        public double CardWidth
        {
            get { return cardWidth; }
            set
            {
                if (Math.Abs(cardWidth - value) < 0.01) return;
                cardWidth = value;
                Raise("CardWidth");
            }
        }

        /// <summary>封面高度，按卡片宽度等比换算（竖版封面 1:1.4）</summary>
        public double CoverHeight
        {
            get { return coverHeight; }
            set
            {
                if (Math.Abs(coverHeight - value) < 0.01) return;
                coverHeight = value;
                Raise("CoverHeight");
            }
        }

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>饼图图例的一行。包一层是为了让 XAML 里的绑定路径短一点。</summary>
    public class GenreLegendRow
    {
        public GenreLegendRow(GenreSlice slice)
        {
            Slice = slice;
        }

        public GenreSlice Slice { get; private set; }
        public string Name { get { return Slice.Name; } }
        public string Color { get { return Slice.Color; } }
        public string ShareText { get { return Slice.ShareText; } }
    }

    /// <summary>Top 50 的一行。</summary>
    public class Top50Row
    {
        public string Rank { get; set; }
        public GameEntry Entry { get; set; }

        /// <summary>占比条宽度（像素），由视图按"相对榜首"换算好</summary>
        public double BarWidth { get; set; }
    }

    public class VaultView : UserControl
    {
        /// <summary>网格模式每次渲染的卡片数。WrapPanel 不虚拟化，分批追加避免一次性建几百个卡片。</summary>
        private const int PageSize = 150;
        private const double ZoomStep = 4;

        /// <summary>点中滚动条后用方向键微调的步长（像素），刻意取小值以便精细定位</summary>
        private const double ScrollStep = 40;

        private readonly IPlayniteAPI api;
        private readonly VaultViewModel vm = new VaultViewModel();
        private readonly UserControl root;

        private ListBox gameList;
        private TextBox searchInput;
        private Border loadingMask;
        private TextBlock emptyHint;
        private Button viewToggle;
        private Border zoomPanel;
        private Slider zoomSlider;
        private ComboBox languageBox;
        private ItemsPanelTemplate wrapPanelTemplate;
        private ItemsPanelTemplate stackPanelTemplate;
        private DataTemplate cardTemplate;
        private DataTemplate barTemplate;

        // ---- 分析页 ----
        private Grid vaultPage;
        private Grid analyzePage;
        private PieChart genreChart;
        private ItemsControl genreLegend;
        private ItemsControl top50List;
        private ItemsControl reportSections;
        private ItemsControl recommendList;
        private TextBlock analyzeSubtitle;
        private TextBlock top50Subtitle;
        private TextBlock recommendSubtitle;
        private TextBlock toneBadgeText;
        private StackPanel genreEmptyHint;
        private TimelineChart playTimeline;
        private StackPanel timelineEmptyHint;
        private TextBlock timelineSpanText;
        private Border toast;
        private TextBlock toastText;

        // ---- AI 推荐 / 配置 ----
        private Border aiBadge;
        private Ellipse aiBadgeDot;
        private TextBlock aiBadgeText;
        private Button aiRefreshButton;
        private TextBlock aiRefreshText;
        private Button aiConfigButton;
        private TextBlock aiConfigText;
        private Border aiConfigPanel;
        private TextBlock aiConfigTitle;
        private TextBlock aiConfigHint;
        private TextBlock aiLabelKey;
        private TextBlock aiLabelModel;
        private TextBlock aiLabelEndpoint;
        private TextBox aiKeyBox;
        private TextBox aiModelBox;
        private TextBox aiEndpointBox;
        private TextBlock aiConfigStatus;
        private Button aiSaveButton;
        private TextBlock aiSaveText;
        private TextBlock aiStatusLine;
        private StackPanel inLibBlock;
        private TextBlock inLibTitle;
        private StackPanel externalBlock;
        private TextBlock externalTitle;
        private TextBlock externalHint;
        private ItemsControl externalList;
        private readonly ObservableCollection<AiGenrePick> externalRows =
            new ObservableCollection<AiGenrePick>();

        /// <summary>AI 分析进行中的标志，防止重复发起请求。</summary>
        private bool aiBusy;

        /// <summary>上一次 AI 请求的结果缓存（同一份数据不重复问）。</summary>
        private List<AiGenrePick> aiPicks;
        private VaultData aiPicksFor;

        private ColumnDefinition colGenre;
        private ColumnDefinition colTop;
        private RowDefinition rowTop;
        private RowDefinition rowReport;
        private readonly Dictionary<string, Button> toneButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> basisButtons = new Dictionary<string, Button>();
        private readonly ObservableCollection<GenreLegendRow> legendRows = new ObservableCollection<GenreLegendRow>();
        private readonly ObservableCollection<Top50Row> top50Rows = new ObservableCollection<Top50Row>();
        private readonly ObservableCollection<ReportSection> reportItems = new ObservableCollection<ReportSection>();
        private readonly ObservableCollection<RecommendRow> recommendRows = new ObservableCollection<RecommendRow>();

        private readonly Dictionary<string, Button> sortButtons = new Dictionary<string, Button>();
        private readonly ObservableCollection<GameEntry> displayed = new ObservableCollection<GameEntry>();
        private readonly DispatcherTimer searchTimer;
        private readonly DispatcherTimer statusTimer;
        private readonly DispatcherTimer tintTimer;
        private readonly DispatcherTimer toastTimer;

        private List<GameEntry> entries = new List<GameEntry>();
        private List<GameEntry> visible = new List<GameEntry>();
        private SortMode sortMode = SortMode.Total;
        private ViewMode viewMode = ViewMode.Grid;
        private string normalStatus = "";
        private ScrollViewer listScroll;
        private ScrollBar listBar;
        private VaultData lastData;

        // 分析页状态
        private ReportTone tone = ReportTone.Snarky;
        private bool genreBasisByTime = true;
        private int selectedGenre = -1;

        /// <summary>从分析页点类型后带过来的过滤集合；null 表示不过滤。</summary>
        private HashSet<GameEntry> genreFilter;

        public VaultView(IPlayniteAPI api)
        {
            this.api = api;
            DataContext = vm;

            // 恢复上次调整过的卡片缩放，避免每次启动都回到默认大小
            vm.CardWidth = VaultSettings.CardWidth;
            vm.CoverHeight = Math.Round(VaultSettings.CardWidth * 1.4);

            // 保险：确保语言资源已经挂到 Application 上（正常情况下插件构造函数里已做过）。
            // DynamicResource 找不到键时不会抛异常、只会静默显示成空白，所以这里必须兜住。
            try
            {
                L10n.Apply(L10n.Current);
            }
            catch
            {
            }

            var xaml = ReadEmbeddedXaml();
            root = (UserControl)XamlReader.Parse(xaml);
            root.DataContext = vm;
            Content = root;

            gameList = root.FindName("GameList") as ListBox;
            searchInput = root.FindName("SearchInput") as TextBox;
            loadingMask = root.FindName("LoadingMask") as Border;
            emptyHint = root.FindName("EmptyHint") as TextBlock;
            viewToggle = root.FindName("ViewToggleButton") as Button;
            zoomPanel = root.FindName("ZoomPanel") as Border;
            zoomSlider = root.FindName("ZoomSlider") as Slider;
            languageBox = root.FindName("LanguageBox") as ComboBox;
            toast = root.FindName("Toast") as Border;
            toastText = root.FindName("ToastText") as TextBlock;

            wrapPanelTemplate = root.Resources["WrapItemsPanel"] as ItemsPanelTemplate;
            stackPanelTemplate = root.Resources["StackItemsPanel"] as ItemsPanelTemplate;
            cardTemplate = root.Resources["GameCardTemplate"] as DataTemplate;
            barTemplate = root.Resources["BarRowTemplate"] as DataTemplate;

            WireSortButtons();
            WireZoomBar();

            if (searchInput != null)
                searchInput.TextChanged += OnSearchTextChanged;

            if (viewToggle != null)
                viewToggle.Click += OnViewToggleClick;

            if (languageBox != null)
            {
                // 0 = 中文，1 = English；先设初值再挂事件，避免初始化时触发一次切换
                languageBox.SelectedIndex = L10n.IsEnglish ? 1 : 0;
                languageBox.SelectionChanged += OnLanguageChanged;
            }

            var analyze = root.FindName("AnalyzeButton") as Button;
            if (analyze != null)
                analyze.Click += OnAnalyzeClick;

            var share = root.FindName("ShareButton") as Button;
            if (share != null)
                share.Click += OnShareImageClick;

            if (gameList != null)
            {
                gameList.MouseDoubleClick += OnItemDoubleClick;
                gameList.Loaded += (s, e) => AttachScroll();
            }

            WireAnalyzePage();

            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplyFilter(); };

            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            statusTimer.Tick += (s, e) =>
            {
                statusTimer.Stop();
                if (!string.IsNullOrEmpty(normalStatus)) vm.StatusText = normalStatus;
            };

            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
            toastTimer.Tick += (s, e) => { toastTimer.Stop(); HideToast(); };

            // 90+ 评分的流彩：由视图统一驱动，只改已渲染卡片的画刷颜色，
            // 不涉及任何布局，所以开销可以忽略。
            tintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            tintTimer.Tick += OnTintTick;
            Loaded += (s, e) => { if (!tintTimer.IsEnabled) tintTimer.Start(); };
            Unloaded += (s, e) => tintTimer.Stop();
            tintTimer.Start();

            // 命中缓存就立刻渲染（缓存里绝不会是空结果），否则显示加载态
            var cached = VaultCache.Get();
            var cacheUsable = cached != null && cached.RawGameCount > 0;
            if (cacheUsable)
            {
                entries = cached.Entries;
                ApplyData(cached);
                ApplyFilter();
            }

            if (!cacheUsable || !VaultCache.HasFresh)
                RefreshAsync(!cacheUsable);
        }

        private static string ReadEmbeddedXaml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("GameVault.VaultView.xaml"))
            {
                if (stream == null)
                    throw new InvalidOperationException("找不到嵌入的 VaultView.xaml 资源");
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        private void WireSortButtons()
        {
            AddSortButton("SortTotal", SortMode.Total);
            AddSortButton("SortRecent", SortMode.Recent);
            AddSortButton("SortScore", SortMode.Score);
        }

        private void AddSortButton(string name, SortMode mode)
        {
            var button = root.FindName(name) as Button;
            if (button == null) return;
            sortButtons[name] = button;
            button.Click += (s, e) =>
            {
                if (sortMode == mode) return;
                sortMode = mode;
                foreach (var pair in sortButtons) pair.Value.Tag = null;
                button.Tag = "Active";
                ApplyFilter();
            };
        }

        /// <summary>右下角缩放条：拖动连续缩放，点击后可用 ←/→ 微调。</summary>
        private void WireZoomBar()
        {
            if (zoomSlider != null)
            {
                zoomSlider.Value = vm.CardWidth;
                zoomSlider.ValueChanged += OnZoomValueChanged;
                zoomSlider.PreviewKeyDown += OnZoomKeyDown;
                zoomSlider.PreviewMouseLeftButtonDown += (s, e) => zoomSlider.Focus();
                zoomSlider.GotKeyboardFocus += (s, e) => SetZoomFocus(true);
                zoomSlider.LostKeyboardFocus += (s, e) => SetZoomFocus(false);
            }

            if (zoomPanel != null)
                zoomPanel.MouseLeftButtonDown += (s, e) =>
                {
                    if (zoomSlider != null) zoomSlider.Focus();
                };
        }

        // ==================================================================
        // 数据分析页
        // ==================================================================

        private void WireAnalyzePage()
        {
            vaultPage = root.FindName("VaultPage") as Grid;
            analyzePage = root.FindName("AnalyzePage") as Grid;
            genreChart = root.FindName("GenreChart") as PieChart;
            genreLegend = root.FindName("GenreLegend") as ItemsControl;
            top50List = root.FindName("Top50List") as ItemsControl;
            reportSections = root.FindName("ReportSections") as ItemsControl;
            recommendList = root.FindName("RecommendList") as ItemsControl;
            analyzeSubtitle = root.FindName("AnalyzeSubtitle") as TextBlock;
            top50Subtitle = root.FindName("Top50Subtitle") as TextBlock;
            recommendSubtitle = root.FindName("RecommendSubtitle") as TextBlock;
            toneBadgeText = root.FindName("ToneBadgeText") as TextBlock;
            genreEmptyHint = root.FindName("GenreEmptyHint") as StackPanel;
            playTimeline = root.FindName("PlayTimeline") as TimelineChart;
            timelineEmptyHint = root.FindName("TimelineEmptyHint") as StackPanel;
            timelineSpanText = root.FindName("TimelineSpanText") as TextBlock;

            // AI 推荐相关控件
            aiBadge = root.FindName("AiBadge") as Border;
            aiBadgeDot = root.FindName("AiBadgeDot") as Ellipse;
            aiBadgeText = root.FindName("AiBadgeText") as TextBlock;
            aiRefreshButton = root.FindName("AiRefreshButton") as Button;
            aiRefreshText = root.FindName("AiRefreshText") as TextBlock;
            aiConfigButton = root.FindName("AiConfigButton") as Button;
            aiConfigText = root.FindName("AiConfigText") as TextBlock;
            aiConfigPanel = root.FindName("AiConfigPanel") as Border;
            aiConfigTitle = root.FindName("AiConfigTitle") as TextBlock;
            aiConfigHint = root.FindName("AiConfigHint") as TextBlock;
            aiLabelKey = root.FindName("AiLabelKey") as TextBlock;
            aiLabelModel = root.FindName("AiLabelModel") as TextBlock;
            aiLabelEndpoint = root.FindName("AiLabelEndpoint") as TextBlock;
            aiKeyBox = root.FindName("AiKeyBox") as TextBox;
            aiModelBox = root.FindName("AiModelBox") as TextBox;
            aiEndpointBox = root.FindName("AiEndpointBox") as TextBox;
            aiConfigStatus = root.FindName("AiConfigStatus") as TextBlock;
            aiSaveButton = root.FindName("AiSaveButton") as Button;
            aiSaveText = root.FindName("AiSaveText") as TextBlock;
            aiStatusLine = root.FindName("AiStatusLine") as TextBlock;
            inLibBlock = root.FindName("InLibBlock") as StackPanel;
            inLibTitle = root.FindName("InLibTitle") as TextBlock;
            externalBlock = root.FindName("ExternalBlock") as StackPanel;
            externalTitle = root.FindName("ExternalTitle") as TextBlock;
            externalHint = root.FindName("ExternalHint") as TextBlock;
            externalList = root.FindName("ExternalList") as ItemsControl;

            if (externalList != null) externalList.ItemsSource = externalRows;
            WireAiPanel();

            // 可拖拽的列 / 行：记住用户调好的尺寸
            colGenre = root.FindName("ColGenre") as ColumnDefinition;
            colTop = root.FindName("ColTop") as ColumnDefinition;
            rowTop = root.FindName("RowTop") as RowDefinition;
            rowReport = root.FindName("RowReport") as RowDefinition;

            if (genreLegend != null) genreLegend.ItemsSource = legendRows;
            if (top50List != null) top50List.ItemsSource = top50Rows;
            if (reportSections != null) reportSections.ItemsSource = reportItems;
            if (recommendList != null) recommendList.ItemsSource = recommendRows;

            if (genreChart != null)
                genreChart.SliceClicked += OnSliceClicked;

            var back = root.FindName("BackButton") as Button;
            if (back != null) back.Click += (s, e) => ShowPage(false);

            var copy = root.FindName("AnalyzeCopyButton") as Button;
            if (copy != null) copy.Click += OnAnalyzeCopyClick;

            // 用户拖过分隔条后把尺寸存下来，下次打开还是这个样子。
            // 注意：不能用 ColumnDefinition/RowDefinition 的 SizeChanged —— 这两个类
            // 继承自 FrameworkContentElement 的子集，根本没有 SizeChanged 事件（编译期就报错）。
            // 挂在 GridSplitter 的 DragCompleted 上更准：只在用户真的拖动后落盘，
            // 窗口缩放引起的布局变化不会污染设置。
            var splitV = root.FindName("SplitV") as GridSplitter;
            if (splitV != null)
                splitV.DragCompleted += (s, e) =>
                {
                    if (colGenre != null) VaultSettings.SetAnalyzeGenreWidth(colGenre.ActualWidth);
                };
            var splitH = root.FindName("SplitH") as GridSplitter;
            if (splitH != null)
                splitH.DragCompleted += (s, e) =>
                {
                    if (rowTop != null) VaultSettings.SetAnalyzeTopHeight(rowTop.ActualHeight);
                };

            AddToneButton("ToneSnarky", ReportTone.Snarky);
            AddToneButton("ToneFormal", ReportTone.Formal);
            AddBasisButton("BasisTime", true);
            AddBasisButton("BasisCount", false);
        }

        /// <summary>把上次存下来的面板尺寸恢复到 XAML 定义上（只在首次进入分析页时调）。</summary>
        private void RestoreAnalyzeLayout()
        {
            if (colGenre != null && VaultSettings.AnalyzeGenreWidth > 0)
                colGenre.Width = new GridLength(VaultSettings.AnalyzeGenreWidth, GridUnitType.Pixel);
            if (rowTop != null && VaultSettings.AnalyzeTopHeight > 0)
                rowTop.Height = new GridLength(VaultSettings.AnalyzeTopHeight, GridUnitType.Pixel);
        }

        private void AddToneButton(string name, ReportTone value)
        {
            var button = root.FindName(name) as Button;
            if (button == null) return;
            toneButtons[name] = button;
            button.Click += (s, e) =>
            {
                if (tone == value) return;
                tone = value;
                foreach (var pair in toneButtons) pair.Value.Tag = null;
                button.Tag = "Active";
                UpdateToneBadge();
                BuildReport();
            };
        }

        private void AddBasisButton(string name, bool byTime)
        {
            var button = root.FindName(name) as Button;
            if (button == null) return;
            basisButtons[name] = button;
            button.Click += (s, e) =>
            {
                if (genreBasisByTime == byTime) return;
                genreBasisByTime = byTime;
                foreach (var pair in basisButtons) pair.Value.Tag = null;
                button.Tag = "Active";
                // 换口径时清掉筛选：同一个下标在两种口径下不是同一个类型
                selectedGenre = -1;
                BuildGenreChart();
            };
        }

        /// <summary>右上角图标按钮：从库存页切到分析页。</summary>
        private void OnAnalyzeClick(object sender, RoutedEventArgs e)
        {
            ShowPage(true);
        }

        private void ShowPage(bool analyze)
        {
            if (vaultPage != null)
                vaultPage.Visibility = analyze ? Visibility.Collapsed : Visibility.Visible;
            if (analyzePage != null)
                analyzePage.Visibility = analyze ? Visibility.Visible : Visibility.Collapsed;

            if (!analyze) return;

            // 每次都按当前数据重建一次：库存可能在别的页面刷新过
            BuildAnalyzePage();
        }

        private void BuildAnalyzePage()
        {
            RestoreAnalyzeLayout();

            BuildGenreChart();
            BuildTop50();
            BuildTimeline();
            BuildReport();
            BuildRecommend();

            if (analyzeSubtitle != null && lastData != null)
                analyzeSubtitle.Text = L10n.F("LocAnalyzeSubtitle", lastData.MergedCount);

            UpdateToneBadge();
        }

        private void UpdateToneBadge()
        {
            if (toneBadgeText != null)
                toneBadgeText.Text = L10n.T(tone == ReportTone.Snarky ? "LocStyleSnarky" : "LocStyleFormal");
        }

        // ---- 饼图 ----

        private void BuildGenreChart()
        {
            if (genreChart == null || genreLegend == null) return;

            var slices = lastData == null
                ? null
                : (genreBasisByTime ? lastData.GenreByTime : lastData.GenreByCount);

            var hasData = slices != null && slices.Count > 0;
            if (genreEmptyHint != null)
                genreEmptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            genreChart.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
            genreLegend.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;

            if (!hasData)
            {
                genreChart.SetData(null, "", "");
                legendRows.Clear();
                return;
            }

            // 中心显示总量；按款数口径时中心数字是款数，避免和饼图口径打架
            var centerValue = genreBasisByTime
                ? TimeFmt.Short(lastData.TotalPlaytime)
                : lastData.MergedCount.ToString();
            var centerTitle = L10n.T(genreBasisByTime ? "LocChartCenter" : "LocChartCenterCount");

            genreChart.SetData(slices, centerTitle, centerValue);
            genreChart.SetHighlight(selectedGenre);

            legendRows.Clear();
            foreach (var slice in slices)
                legendRows.Add(new GenreLegendRow(slice));
        }

        private void OnSliceClicked(int index)
        {
            selectedGenre = selectedGenre == index ? -1 : index;
            if (genreChart != null) genreChart.SetHighlight(selectedGenre);

            // 点饼图 = 按该类型过滤下面的游戏列表，这样"分类"才真的能用来找游戏
            ApplyGenreFilter(selectedGenre);
        }

        /// <summary>
        /// 把选中的类型作为额外过滤条件作用到库存列表上，然后回到库存页。
        /// 传 -1 表示清除。
        /// </summary>
        private void ApplyGenreFilter(int index)
        {
            if (index < 0 || lastData == null || lastData.GenreByTime == null
                || index >= lastData.GenreByTime.Count)
            {
                genreFilter = null;
                FlashStatus(L10n.T("LocGenreFilterOff"));
                ApplyFilter();
                return;
            }

            var slice = lastData.GenreByTime[index];
            List<GameEntry> bucket;
            // 类型索引在按款数口径下是同名的另一份列表；统一从 GenreIndex 取，避免口径不一致
            if (lastData.GenreIndex.TryGetValue(slice.Name, out bucket))
                genreFilter = new HashSet<GameEntry>(bucket);
            else
                genreFilter = null;

            FlashStatus(L10n.F("LocGenreFilterOn", slice.Name, slice.GameCount));
            ApplyFilter();
        }

        // ---- Top 50 ----

        private void BuildTop50()
        {
            top50Rows.Clear();
            if (lastData == null || lastData.TopByTime == null) return;

            var top = lastData.TopByTime;
            var max = top.Count > 0 ? top[0].TotalPlaytime : 0;
            var sum = top.Aggregate(0UL, (acc, e) => acc + e.TotalPlaytime);

            for (var i = 0; i < top.Count; i++)
            {
                top50Rows.Add(new Top50Row
                {
                    Rank = (i + 1).ToString(),
                    Entry = top[i],
                    // 条形长度按"相对榜首"换算，而不是相对总和 —— 后者会让所有条都很短
                    BarWidth = max > 0 ? Math.Max(3, 76.0 * top[i].TotalPlaytime / max) : 3
                });
            }

            if (top50Subtitle != null)
            {
                var share = lastData.TotalPlaytime > 0
                    ? (double)sum / lastData.TotalPlaytime
                    : 0;
                top50Subtitle.Text = L10n.F("LocPanelTopSub", (share * 100).ToString("0.#") + "%");
            }
        }

        // ---- 报告 ----

        private void BuildReport()
        {
            reportItems.Clear();
            if (lastData == null) return;
            foreach (var section in ProfileReport.Build(lastData, tone))
                reportItems.Add(section);
        }

        // ---- 游玩时间轴 ----

        /// <summary>
        /// 把按周聚合的游玩数据交给时间轴控件渲染。
        /// 没有 GameActivity 数据时显示安装指引（而不是一根柱子都没有的空白）。
        /// </summary>
        private void BuildTimeline()
        {
            var timeline = lastData != null ? lastData.Timeline : null;
            var hasData = timeline != null && timeline.Available
                          && timeline.Weeks != null && timeline.Weeks.Count > 0
                          && timeline.TotalSeconds > 0;

            if (timelineEmptyHint != null)
                timelineEmptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            if (playTimeline != null)
            {
                playTimeline.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
                playTimeline.SetData(hasData ? timeline : null);
            }
            if (timelineSpanText != null)
                timelineSpanText.Text = hasData && !string.IsNullOrEmpty(timeline.SpanText)
                    ? timeline.SpanText
                    : "";
        }

        // ---- 推荐 ----

        /// <summary>
        /// 把「同类好游戏」的推荐卡片填进报告底部。
        /// 挑选逻辑在 <see cref="VaultData.PickRecommendations"/>（纯计算，可离线测试），
        /// 这里只负责刷新 UI 与空态文案。
        ///
        /// 库内推荐始终显示（本地算法，不需要联网）；
        /// 库外推荐需要配置了 AI，异步拉取，失败/未配置则整块隐藏。
        /// </summary>
        private void BuildRecommend()
        {
            UpdateAiBadge();

            // 固定文案先刷一遍（换语言时要跟着变）
            if (aiRefreshText != null) aiRefreshText.Text = L10n.T("LocAiRefreshBtn");
            if (aiConfigText != null) aiConfigText.Text = L10n.T("LocAiConfigBtn");
            if (inLibTitle != null) inLibTitle.Text = L10n.T("LocRecommendInLibTitle");
            if (externalTitle != null) externalTitle.Text = L10n.T("LocRecommendExternalTitle");
            if (externalHint != null) externalHint.Text = L10n.T("LocRecommendExternalHint");

            // ---- 库内推荐 ----
            recommendRows.Clear();
            if (recommendSubtitle != null)
                recommendSubtitle.Text = L10n.T("LocRecommendHint");

            var picks = lastData == null
                ? new List<RecommendRow>()
                : VaultData.PickRecommendations(lastData);
            foreach (var row in picks) recommendRows.Add(row);

            if (inLibBlock != null)
                inLibBlock.Visibility = recommendRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (recommendSubtitle != null)
            {
                if (recommendRows.Count == 0 && lastData != null)
                    recommendSubtitle.Text = L10n.T("LocRecommendNone");
                else if (lastData != null && lastData.GenreByTime.Count > 0)
                {
                    // 副标题写上"根据哪几个类型挑的"，让推荐看起来有依据而不是凭空冒出来
                    var names = lastData.GenreByTime.Take(3).Select(g => g.Name).ToList();
                    var joined = string.Join(L10n.T("LocSeparator"), names);
                    recommendSubtitle.Text = L10n.F("LocRecommendHintGenres", joined);
                }
            }

            // ---- 库外推荐（AI）----
            BuildExternalRecommend();
        }

        // ==================================================================
        // AI 库外推荐
        // ==================================================================

        private void WireAiPanel()
        {
            if (aiConfigButton != null)
                aiConfigButton.Click += (s, e) => ToggleAiPanel();

            if (aiRefreshButton != null)
                aiRefreshButton.Click += (s, e) => RunAiRecommend(force: true);

            if (aiSaveButton != null)
                aiSaveButton.Click += OnAiSaveClick;

            // 把上次保存的配置回填到输入框
            var cfg = VaultSettings.Ai;
            if (aiKeyBox != null) aiKeyBox.Text = cfg.ApiKey ?? "";
            if (aiModelBox != null) aiModelBox.Text = cfg.Model ?? "";
            if (aiEndpointBox != null) aiEndpointBox.Text = cfg.Endpoint ?? "";
            if (aiModelBox != null && string.IsNullOrEmpty(aiModelBox.Text))
                aiModelBox.ToolTip = AiConfig.DefaultModel;
            if (aiEndpointBox != null && string.IsNullOrEmpty(aiEndpointBox.Text))
                aiEndpointBox.ToolTip = AiConfig.DefaultEndpoint;

            // 面板里的静态文案
            if (aiConfigTitle != null) aiConfigTitle.Text = L10n.T("LocAiConfigTitle");
            if (aiConfigHint != null) aiConfigHint.Text = L10n.T("LocAiConfigHint");
            if (aiLabelKey != null) aiLabelKey.Text = L10n.T("LocAiLabelKey");
            if (aiLabelModel != null) aiLabelModel.Text = L10n.T("LocAiLabelModel");
            if (aiLabelEndpoint != null) aiLabelEndpoint.Text = L10n.T("LocAiLabelEndpoint");
            if (aiSaveText != null) aiSaveText.Text = L10n.T("LocAiSave");
        }

        private void ToggleAiPanel()
        {
            if (aiConfigPanel == null) return;
            var show = aiConfigPanel.Visibility != Visibility.Visible;
            aiConfigPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            // 打开面板时把静态文案重刷一次（可能在切语言之后才第一次打开）
            if (show)
            {
                if (aiConfigTitle != null) aiConfigTitle.Text = L10n.T("LocAiConfigTitle");
                if (aiConfigHint != null) aiConfigHint.Text = L10n.T("LocAiConfigHint");
                if (aiLabelKey != null) aiLabelKey.Text = L10n.T("LocAiLabelKey");
                if (aiLabelModel != null) aiLabelModel.Text = L10n.T("LocAiLabelModel");
                if (aiLabelEndpoint != null) aiLabelEndpoint.Text = L10n.T("LocAiLabelEndpoint");
                if (aiSaveText != null) aiSaveText.Text = L10n.T("LocAiSave");
            }
        }

        private void OnAiSaveClick(object sender, RoutedEventArgs e)
        {
            var key = aiKeyBox != null ? aiKeyBox.Text : "";
            if (string.IsNullOrWhiteSpace(key))
            {
                if (aiConfigStatus != null) aiConfigStatus.Text = L10n.T("LocAiNeedKey");
                return;
            }

            VaultSettings.SetAi(
                aiEndpointBox != null ? aiEndpointBox.Text : null,
                key,
                aiModelBox != null ? aiModelBox.Text : null);

            if (aiConfigStatus != null) aiConfigStatus.Text = L10n.T("LocAiSaved");
            UpdateAiBadge();

            // 缓存作废，重新问一次
            aiPicks = null;
            aiPicksFor = null;
            RunAiRecommend(force: true);
        }

        private void UpdateAiBadge()
        {
            if (aiBadgeText == null) return;
            var ready = VaultSettings.Ai.Ready;
            aiBadgeText.Text = L10n.T(ready ? "LocAiBadgeOn" : "LocAiBadgeOff");
            if (aiBadgeDot != null)
                aiBadgeDot.Fill = ready
                    ? new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99))
                    : new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8D));
            if (aiBadge != null)
                aiBadge.Background = ready
                    ? new SolidColorBrush(Color.FromRgb(0x14, 0x2E, 0x24))
                    : new SolidColorBrush(Color.FromRgb(0x1C, 0x26, 0x34));
            if (aiRefreshButton != null)
                aiRefreshButton.IsEnabled = ready && !aiBusy;
        }

        /// <summary>库外推荐：配置了 AI 才显示；结果按数据对象缓存，避免每次进页面都重新请求。</summary>
        private void BuildExternalRecommend()
        {
            var ready = VaultSettings.Ai.Ready;

            if (!ready)
            {
                // 未配置：整块隐藏，只留徽章和设置按钮引导用户去配
                if (externalBlock != null) externalBlock.Visibility = Visibility.Collapsed;
                externalRows.Clear();
                if (aiStatusLine != null)
                {
                    aiStatusLine.Text = L10n.T("LocRecommendAiOff");
                    aiStatusLine.Visibility = Visibility.Visible;
                }
                return;
            }

            // 同一份数据已经问过 → 直接用缓存（除非用户点"重新分析"）
            if (aiPicksFor == lastData && aiPicks != null)
            {
                ShowExternal(aiPicks);
                return;
            }

            if (externalBlock != null) externalBlock.Visibility = Visibility.Collapsed;
            if (aiStatusLine != null)
            {
                aiStatusLine.Text = L10n.T("LocRecommendAiOff");
                aiStatusLine.Visibility = Visibility.Collapsed;
            }
            RunAiRecommend(force: false);
        }

        /// <summary>异步请求 AI。失败静默回退（只在状态行提示），不打断界面。</summary>
        private void RunAiRecommend(bool force)
        {
            var data = lastData;
            if (data == null) return;

            var config = VaultSettings.Ai;
            if (!config.Ready)
            {
                BuildExternalRecommend();
                return;
            }

            if (aiBusy) return;

            if (!force && aiPicksFor == data && aiPicks != null)
            {
                ShowExternal(aiPicks);
                return;
            }

            aiBusy = true;
            UpdateAiBadge();
            if (aiStatusLine != null)
            {
                aiStatusLine.Text = L10n.T("LocRecommendAnalyzing");
                aiStatusLine.Visibility = Visibility.Visible;
            }
            if (aiConfigStatus != null) aiConfigStatus.Text = L10n.T("LocAiCalling");

            Task.Run(() =>
            {
                var result = AiRecommender.Fetch(data, config);
                return result;
            }).ContinueWith(t =>
            {
                // 回到 UI 线程更新界面
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    aiBusy = false;
                    UpdateAiBadge();

                    // 期间数据可能已经换了，那就作废这次结果
                    if (!ReferenceEquals(data, lastData))
                    {
                        if (aiStatusLine != null) aiStatusLine.Visibility = Visibility.Collapsed;
                        if (aiConfigStatus != null) aiConfigStatus.Text = "";
                        return;
                    }

                    var result = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                    if (result == null || result.Count == 0)
                    {
                        aiPicks = null;
                        aiPicksFor = null;
                        if (externalBlock != null) externalBlock.Visibility = Visibility.Collapsed;
                        externalRows.Clear();
                        if (aiStatusLine != null)
                        {
                            aiStatusLine.Text = L10n.T("LocRecommendAiFailed");
                            aiStatusLine.Visibility = Visibility.Visible;
                        }
                        if (aiConfigStatus != null) aiConfigStatus.Text = L10n.T("LocAiFail");
                        return;
                    }

                    aiPicks = result;
                    aiPicksFor = data;
                    if (aiConfigStatus != null) aiConfigStatus.Text = L10n.T("LocAiOk");
                    if (aiStatusLine != null) aiStatusLine.Visibility = Visibility.Collapsed;
                    ShowExternal(result);
                }));
            }, TaskScheduler.Default);
        }

        private void ShowExternal(List<AiGenrePick> picks)
        {
            externalRows.Clear();
            if (picks == null || picks.Count == 0)
            {
                if (externalBlock != null) externalBlock.Visibility = Visibility.Collapsed;
                return;
            }
            foreach (var pick in picks) externalRows.Add(pick);
            if (externalBlock != null) externalBlock.Visibility = Visibility.Visible;
            if (aiStatusLine != null) aiStatusLine.Visibility = Visibility.Collapsed;
        }

        /// <summary>分析页顶栏的分享按钮：生成分享长图（而不是复制文字）。</summary>
        private void OnAnalyzeCopyClick(object sender, RoutedEventArgs e)
        {
            GenerateShareImage();
        }

        /// <summary>库存页顶栏的分享按钮。</summary>
        private void OnShareImageClick(object sender, RoutedEventArgs e)
        {
            GenerateShareImage();
        }

        /// <summary>
        /// 生成分享长图：渲染 → 写剪贴板（+ 存到「图片\GameVault」）。
        ///
        /// 渲染本身不慢（百毫秒级），但图片编码 + 剪贴板写入可能卡一下，
        /// 所以先给个"正在生成"的反馈，再同步做完 —— 不放到后台线程是因为
        /// WPF 的 RenderTargetBitmap 要求在同一 UI 线程上操作视觉树。
        /// </summary>
        private void GenerateShareImage()
        {
            if (lastData == null || lastData.MergedCount == 0)
            {
                FlashStatus(L10n.T("LocShareNoData"));
                return;
            }

            FlashStatus(L10n.T("LocShareSaving"));

            byte[] png;
            try
            {
                png = ShareCard.Render(lastData, tone);
            }
            catch
            {
                png = null;
            }

            if (png == null)
            {
                // 长图生成失败（极少见）：退回复制文字，至少别让按钮点了没反应
                try
                {
                    Clipboard.SetText(BuildAnalysisText());
                    FlashStatus(L10n.T("LocCopyAnalysis"));
                }
                catch
                {
                    FlashStatus(L10n.T("LocShareFailed"));
                }
                return;
            }

            var saved = ShareCard.SaveToPictures(png);

            // 剪贴板优先：多数人要的是"直接粘贴到聊天窗"
            var copied = TryCopyImage(png);

            if (copied)
                FlashStatus(saved != null
                    ? L10n.F("LocShareSaved", saved)
                    : L10n.T("LocShareCopied"));
            else if (saved != null)
                FlashStatus(L10n.F("LocShareSaved", saved));
            else
                FlashStatus(L10n.T("LocShareFailed"));
        }

        /// <summary>把 PNG 字节塞进剪贴板。WPF 的 Clipboard 偶尔被别的进程占用会抛异常，所以兜住。</summary>
        private static bool TryCopyImage(byte[] png)
        {
            try
            {
                var image = new BitmapImage();
                using (var stream = new MemoryStream(png))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();
                Clipboard.SetImage(image);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string BuildAnalysisText()
        {
            var sb = new StringBuilder();
            var sections = new List<ReportSection>(reportItems);
            sb.Append(ProfileReport.ToPlainText(sections, lastData));
            sb.AppendLine();
            sb.AppendLine(L10n.T("LocPanelTop"));
            for (var i = 0; i < top50Rows.Count; i++)
                sb.AppendLine(string.Format("{0}. {1} — {2}", i + 1,
                    top50Rows[i].Entry.Name, top50Rows[i].Entry.TotalText));
            return sb.ToString();
        }

        /// <summary>
        /// 缩放时只需改变卡片尺寸：WrapPanel 会自己按新宽度重新换行，
        /// 因此拖动过程是实时跟手的，不需要重建任何集合。
        /// </summary>
        private void OnZoomValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var width = Math.Round(e.NewValue);
            if (Math.Abs(vm.CardWidth - width) < 0.5) return;

            vm.CardWidth = width;
            vm.CoverHeight = Math.Round(width * 1.4);
            VaultSettings.SetCardWidth(width);
        }

        private void OnZoomKeyDown(object sender, KeyEventArgs e)
        {
            if (zoomSlider == null) return;
            if (e.Key == Key.Left)
            {
                zoomSlider.Value = Math.Max(zoomSlider.Minimum, zoomSlider.Value - ZoomStep);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                zoomSlider.Value = Math.Min(zoomSlider.Maximum, zoomSlider.Value + ZoomStep);
                e.Handled = true;
            }
        }

        private void SetZoomFocus(bool focused)
        {
            if (zoomPanel == null) return;
            zoomPanel.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(focused ? "#4CC2FF" : "#33415A"));
            if (focused)
                FlashStatus(L10n.T("LocTipZoom"));
        }

        private void AttachScroll()
        {
            var scroll = FindChild<ScrollViewer>(gameList);
            if (scroll != null && !ReferenceEquals(scroll, listScroll))
            {
                if (listScroll != null) listScroll.ScrollChanged -= OnScrollChanged;
                listScroll = scroll;
                listScroll.ScrollChanged += OnScrollChanged;
            }

            var bar = FindVerticalBar(gameList);
            if (bar != null && !ReferenceEquals(bar, listBar))
            {
                if (listBar != null)
                {
                    listBar.PreviewKeyDown -= OnScrollBarKeyDown;
                    listBar.PreviewMouseLeftButtonDown -= OnScrollBarMouseDown;
                }
                listBar = bar;
                listBar.Focusable = true;
                listBar.PreviewKeyDown += OnScrollBarKeyDown;
                listBar.PreviewMouseLeftButtonDown += OnScrollBarMouseDown;
            }
        }

        private void OnScrollBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 点一下滚动条就把键盘焦点交给它，之后可以用方向键微调
            var bar = sender as ScrollBar;
            if (bar != null) bar.Focus();
        }

        /// <summary>点中滚动条后，用 ←/→（或 ↑/↓）按 40px 的步长微调位置。</summary>
        private void OnScrollBarKeyDown(object sender, KeyEventArgs e)
        {
            if (listScroll == null) return;

            double target;
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    target = listScroll.VerticalOffset - ScrollStep;
                    break;
                case Key.Right:
                case Key.Down:
                    target = listScroll.VerticalOffset + ScrollStep;
                    break;
                default:
                    return;
            }

            var max = Math.Max(0, listScroll.ScrollableHeight);
            listScroll.ScrollToVerticalOffset(Math.Max(0, Math.Min(target, max)));
            e.Handled = true;
        }

        private static ScrollBar FindVerticalBar(DependencyObject parent)
        {
            if (parent == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var bar = child as ScrollBar;
                if (bar != null && bar.Orientation == Orientation.Vertical) return bar;
                var nested = FindVerticalBar(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 网格模式是分批渲染的，快到底部时再追加一批
            if (viewMode != ViewMode.Grid) return;
            if (e.ExtentHeight <= 0) return;
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 700)
                AppendPage();
        }

        private void ResetPaging()
        {
            displayed.Clear();
            AppendPage();
        }

        private void AppendPage()
        {
            if (displayed.Count >= visible.Count) return;
            var next = Math.Min(visible.Count, displayed.Count + PageSize);
            for (var i = displayed.Count; i < next; i++)
                displayed.Add(visible[i]);
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var typed = child as T;
                if (typed != null) return typed;
                var nested = FindChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private async void RefreshAsync(bool force)
        {
            try
            {
                if (force || VaultCache.Get() == null)
                {
                    if (loadingMask != null) loadingMask.Visibility = Visibility.Visible;
                    vm.StatusText = L10n.T("LocLoading");
                }

                var data = await VaultCache.Ensure(api, force);
                if (data.RawGameCount == 0)
                {
                    // 数据库可能尚未就绪，稍等再试一次，避免停在空白页
                    if (loadingMask != null) loadingMask.Visibility = Visibility.Visible;
                    await Task.Delay(1500);
                    data = await VaultCache.Ensure(api, true);
                }

                entries = data.Entries;
                ApplyData(data);
                ApplyFilter();
            }
            catch
            {
            }
            finally
            {
                if (loadingMask != null) loadingMask.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyData(VaultData data)
        {
            lastData = data;
            vm.EntryCountText = data.MergedCount.ToString();
            vm.RawCountText = L10n.F("LocRawCount", data.RawGameCount, data.InstalledCount);
            vm.TotalPlaytimeText = TimeFmt.Short(data.TotalPlaytime);
            vm.PlayedCountText = L10n.F("LocPlayedCount", data.PlayedCount);
            vm.RecentPlaytimeText = TimeFmt.Short(data.RecentPlaytime);
            vm.ActiveCountText = L10n.F("LocActiveCount", data.ActiveInPeriod);
            vm.TopGameName = string.IsNullOrEmpty(data.TopGameName) ? "—" : data.TopGameName;
            vm.TopGameTimeText = TimeFmt.Short(data.TopGameTime);
            vm.AverageScoreText = data.ScoredCount > 0 ? data.AverageScore.ToString("0") : "—";
            vm.ScoredCountText = data.ScoredCount > 0
                ? L10n.F("LocScoredCount", data.ScoredCount)
                : L10n.T("LocNoScoreData");
            vm.SourceText = data.ActivityAvailable ? L10n.T("LocSourceOk") : L10n.T("LocSourceMissing");
        }

        private void ApplyFilter()
        {
            if (entries == null) return;

            var query = searchInput == null ? "" : searchInput.Text.Trim().ToLowerInvariant();
            IEnumerable<GameEntry> list = entries;
            if (genreFilter != null)
                list = list.Where(e => genreFilter.Contains(e));
            if (!string.IsNullOrEmpty(query))
                list = list.Where(e => e.SearchBlob != null && e.SearchBlob.Contains(query));

            switch (sortMode)
            {
                case SortMode.Recent:
                    list = list.OrderByDescending(e => e.RecentPlaytime).ThenByDescending(e => e.TotalPlaytime);
                    break;
                case SortMode.Score:
                    list = list.OrderByDescending(e => e.CriticScore ?? -1).ThenByDescending(e => e.TotalPlaytime);
                    break;
                default:
                    list = list.OrderByDescending(e => e.TotalPlaytime);
                    break;
            }

            visible = list.ToList();

            var maxRecent = visible.Count > 0 ? visible.Max(e => e.RecentPlaytime) : 0;
            foreach (var entry in visible)
                entry.RecentPercent = maxRecent > 0 ? entry.RecentPlaytime * 100.0 / maxRecent : 0;

            ResetPaging();
            ApplyView();

            var since = DateTime.Now.AddDays(-VaultCache.RecentDays).ToString("MM-dd");
            var modeText = new Dictionary<SortMode, string>
            {
                { SortMode.Total, L10n.T("LocSortNameTotal") },
                { SortMode.Recent, L10n.T("LocSortNameRecent") },
                { SortMode.Score, L10n.T("LocSortNameScore") }
            }[sortMode];

            normalStatus = L10n.F("LocStatus", visible.Count, modeText, since, DateTime.Now.ToString("MM-dd"));
            if (!statusTimer.IsEnabled)
                vm.StatusText = normalStatus;

            if (emptyHint != null)
                emptyHint.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyView()
        {
            if (gameList == null) return;

            var isGrid = viewMode == ViewMode.Grid;
            gameList.ItemTemplate = isGrid ? cardTemplate : barTemplate;
            gameList.ItemsPanel = isGrid ? wrapPanelTemplate : stackPanelTemplate;
            gameList.Padding = isGrid ? new Thickness(16, 0, 6, 64) : new Thickness(16, 0, 6, 16);
            // 网格用 WrapPanel（像素滚动），条形用 VirtualizingStackPanel（按项滚动才能虚拟化）
            ScrollViewer.SetCanContentScroll(gameList, !isGrid);
            gameList.ItemsSource = isGrid ? (IEnumerable<GameEntry>)displayed : visible;

            UpdateZoomPanelVisibility();
            Dispatcher.BeginInvoke(new Action(AttachScroll), DispatcherPriority.Loaded);
        }

        private void UpdateZoomPanelVisibility()
        {
            if (zoomPanel != null)
                zoomPanel.Visibility = viewMode == ViewMode.Grid ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>推进 90+ 评分的彩虹流动相位。</summary>
        private void OnTintTick(object sender, EventArgs e)
        {
            if (!IsVisible) return;

            var phase = ScorePalette.NextPhase();
            var list = viewMode == ViewMode.Grid ? (IList<GameEntry>)displayed : visible;
            for (var i = 0; i < list.Count; i++)
                list[i].ApplyFlow(phase);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            searchTimer.Stop();
            searchTimer.Start();
        }

        private void OnViewToggleClick(object sender, RoutedEventArgs e)
        {
            viewMode = viewMode == ViewMode.Grid ? ViewMode.Bar : ViewMode.Grid;
            if (viewToggle != null)
                viewToggle.Content = viewMode == ViewMode.Grid ? "\uE8FD" : "\uE80A";
            UpdateViewToggleText();
            ApplyView();
        }

        private void UpdateViewToggleText()
        {
            if (viewToggle != null)
                viewToggle.ToolTip = L10n.T(viewMode == ViewMode.Grid ? "LocTipList" : "LocTipGrid");
        }

        /// <summary>
        /// 切换界面语言。XAML 里的文案走 DynamicResource，替换资源字典后会自动刷新；
        /// C# 生成的动态文案（状态栏、概览卡等）需要重新算一遍。
        /// </summary>
        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            var language = languageBox != null && languageBox.SelectedIndex == 1 ? L10n.En : L10n.Zh;
            if (language == L10n.Current) return;

            L10n.Apply(language);
            VaultSettings.SetLanguage(language);

            if (lastData != null) ApplyData(lastData);
            ApplyFilter();
            UpdateViewToggleText();

            // 分析页的文案（中心数字、副标题、报告正文）全是 C# 生成的，
            // 替换资源字典刷不到，必须整页重建一次。
            if (analyzePage != null && analyzePage.Visibility == Visibility.Visible)
                BuildAnalyzePage();
        }

        private void FlashStatus(string message)
        {
            vm.StatusText = message;
            statusTimer.Stop();
            statusTimer.Start();

            // 分析页没有状态栏，所以一并弹个 toast —— 否则用户点分享后看不到任何反馈
            if (analyzePage != null && analyzePage.Visibility == Visibility.Visible)
                ShowToast(message);
        }

        /// <summary>淡入一个轻量提示。用于没有状态栏的页面（分析页）。</summary>
        private void ShowToast(string message)
        {
            if (toast == null || toastText == null) return;
            toastText.Text = message;
            toast.Visibility = Visibility.Visible;

            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
                TimeSpan.FromMilliseconds(160));
            toast.BeginAnimation(OpacityProperty, fade);

            toastTimer.Stop();
            toastTimer.Start();
        }

        private void HideToast()
        {
            if (toast == null) return;
            var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0,
                TimeSpan.FromMilliseconds(280));
            fade.Completed += (s, e) => { if (toast != null) toast.Visibility = Visibility.Collapsed; };
            toast.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>双击卡片 → 跳到 Playnite 库视图里的该游戏详情（不直接启动游戏）。</summary>
        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var entry = FindEntry(e.OriginalSource as DependencyObject);
            if (entry == null) return;
            e.Handled = true;
            try
            {
                api.MainView.SelectGame(entry.PrimaryId);
                api.MainView.SwitchToLibraryView();
            }
            catch
            {
            }
        }

        private static GameEntry FindEntry(DependencyObject source)
        {
            while (source != null)
            {
                var element = source as FrameworkElement;
                if (element != null && element.DataContext is GameEntry)
                    return (GameEntry)element.DataContext;
                source = source is Visual || source is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }
            return null;
        }
    }
}

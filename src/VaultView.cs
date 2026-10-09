using System;
using System.Globalization;
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
using System.Windows.Data;
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

    /// <summary>顶部下拉框能切到的三个功能页。</summary>
    public enum AppPage
    {
        Stats,
        Analyze,
        Fun
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
        private string scoreBucket90Text = "—";
        private string scoreBucket80Text = "—";
        private string scoreBucket70Text = "—";
        private string scoreBucket60Text = "—";
        private string scoreBucketLowText = "—";
        private string scoreDistSubText = "";
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

        // ---- MC 分数分布（90+ / 80-89 / 70-79 / 60-69 / <60）----
        public string ScoreBucket90Text { get { return scoreBucket90Text; } set { scoreBucket90Text = value; Raise("ScoreBucket90Text"); } }
        public string ScoreBucket80Text { get { return scoreBucket80Text; } set { scoreBucket80Text = value; Raise("ScoreBucket80Text"); } }
        public string ScoreBucket70Text { get { return scoreBucket70Text; } set { scoreBucket70Text = value; Raise("ScoreBucket70Text"); } }
        public string ScoreBucket60Text { get { return scoreBucket60Text; } set { scoreBucket60Text = value; Raise("ScoreBucket60Text"); } }
        public string ScoreBucketLowText { get { return scoreBucketLowText; } set { scoreBucketLowText = value; Raise("ScoreBucketLowText"); } }
        public string ScoreDistSubText { get { return scoreDistSubText; } set { scoreDistSubText = value; Raise("ScoreDistSubText"); } }

        // ---- 五档的配色。刻意**不做成硬编码十六进制**，而是直接复用评分徽章那套画刷，
        //      这样封面右上角的徽章和这张卡的色点/数字永远是同一个颜色。
        //      90+ 走全局共享的流光渐变（与封面徽章同源同步），其余四档是冻结的单色画刷。
        public Brush ScoreBrush90 { get { return ScorePalette.SharedFlow; } }
        public Brush ScoreBrush80 { get { return ScorePalette.Get(ScorePalette.Great); } }
        public Brush ScoreBrush70 { get { return ScorePalette.Get(ScorePalette.Good); } }
        public Brush ScoreBrush60 { get { return ScorePalette.Get(ScorePalette.Fair); } }
        public Brush ScoreBrushLow { get { return ScorePalette.Get(ScorePalette.Low); } }

        // ---- 「近两周时长」浮出层的逐款明细 ----
        private ObservableCollection<RecentRow> recentRows = new ObservableCollection<RecentRow>();
        private string recentRowsHint = "";

        /// <summary>近期活跃游戏逐款时长（已按近两周时长降序）。浮出层用。</summary>
        public ObservableCollection<RecentRow> RecentRows { get { return recentRows; } }
        public string RecentRowsHint { get { return recentRowsHint; } set { recentRowsHint = value; Raise("RecentRowsHint"); } }

        // ---- 「最肝游戏」浮出层的时长 Top 5 ----
        private ObservableCollection<TopPlayRow> topPlayRows = new ObservableCollection<TopPlayRow>();


        /// <summary>总游玩时长 Top 5（已降序）。浮出层用。</summary>
        public ObservableCollection<TopPlayRow> TopPlayRows { get { return topPlayRows; } }

        // ---- 「总游戏时长」浮出层的逐库明细 ----
        private readonly ObservableCollection<SourcePlayRow> sourcePlayRows = new ObservableCollection<SourcePlayRow>();

        /// <summary>每个库（Steam / Epic / Xbox …）的游戏总时长之和（按时长降序）。浮出层用。</summary>
        public ObservableCollection<SourcePlayRow> SourcePlayRows { get { return sourcePlayRows; } }

        // ---- 「库存总数」浮出层的逐库明细 ----
        private readonly ObservableCollection<SourceCountRow> sourceCountRows = new ObservableCollection<SourceCountRow>();

        /// <summary>每个库有多少款游戏、占库存百分之几（按款数降序）。浮出层用。</summary>
        public ObservableCollection<SourceCountRow> SourceCountRows { get { return sourceCountRows; } }

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

    /// <summary>「近两周时长」卡片浮出层里的一行：一款近期活跃游戏 + 它这两周玩了多久。</summary>
    public class RecentRow
    {
        public RecentRow(string name, string time)
        {
            Name = name;
            Time = time;
        }

        public string Name { get; private set; }
        public string Time { get; private set; }
    }

    /// <summary>「最肝游戏」卡片浮出层里的一行：一款时长 Top 游戏 + 它的总游玩时长。</summary>
    public class TopPlayRow
    {
        public TopPlayRow(string rank, string name, string time)
        {
            Rank = rank;
            Name = name;
            Time = time;
        }

        public string Rank { get; private set; }
        public string Name { get; private set; }
        public string Time { get; private set; }
    }

    /// <summary>Top 50 的一行。</summary>
    public class Top50Row
    {
        public string Rank { get; set; }
        public GameEntry Entry { get; set; }

        /// <summary>占比条宽度（像素），由视图按"相对榜首"换算好</summary>
        public double BarWidth { get; set; }
    }

    /// <summary>待玩清单里的一行。项目很轻，直接重建集合比做增量更新简单可靠。</summary>
    public class WishRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string index;

        /// <summary>序号（从 1 开始）。拖拽重排后会重算，所以要能通知 UI。</summary>
        public string Index
        {
            get { return index; }
            set
            {
                if (index == value) return;
                index = value;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs("Index"));
            }
        }

        public GameEntry Entry { get; set; }

        public string Name { get { return Entry == null ? "" : Entry.Name; } }

        public ImageSource Cover
        {
            get { return Entry == null ? null : Entry.Cover; }
        }

        /// <summary>副标题：商店 · 总时长（或"还没玩过"）。</summary>
        public string Sub
        {
            get
            {
                if (Entry == null) return "";
                var store = Entry.StoreList;
                var time = Entry.TotalPlaytime > 0
                    ? TimeFmt.Short(Entry.TotalPlaytime)
                    : L10n.T("LocNotPlayed");
                return store + " · " + time;
            }
        }

        public string Tip
        {
            get
            {
                if (Entry == null) return "";
                return L10n.F("LocFunListTip", TimeFmt.Long(Entry.TotalPlaytime), Entry.DevPubText);
            }
        }
    }

    /// <summary>
    /// 待玩清单「添加」下拉里的一行：一款库存游戏 + 是否已加入清单。
    /// 勾选 = 加入，取消勾选 = 移出（用户要求「可搜索的下拉复选框」）。
    /// </summary>
    public class WishPickItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isChecked;

        public GameEntry Entry { get; set; }
        public string Name { get { return Entry == null ? "" : Entry.Name; } }

        /// <summary>副标题：商店 · 时长。</summary>
        public string Sub
        {
            get
            {
                if (Entry == null) return "";
                var store = Entry.StoreList;
                var time = Entry.TotalPlaytime > 0
                    ? TimeFmt.Short(Entry.TotalPlaytime)
                    : L10n.T("LocNotPlayed");
                return store + " · " + time;
            }
        }

        public ImageSource Cover { get { return Entry == null ? null : Entry.Cover; } }

        /// <summary>搜索结果过滤用（预先小写）。</summary>
        public string SearchKey { get; set; }

        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }
    }

    /// <summary>
    /// 拖拽排序时挂在鼠标下的"幽灵"行：一个半透明的小卡片，跟着鼠标走。
    /// 用 Popup 承载是为了能画到窗口外面，且不受父级裁剪影响。
    /// </summary>
    public class DragGhost
    {
        public string Index { get; set; }
        public string Name { get; set; }
        public ImageSource Cover { get; set; }
    }

    public partial class VaultView : UserControl
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
        private TextBlock analyzeSubtitle;
        private TextBlock top50Subtitle;
        private StackPanel genreEmptyHint;
        private TimelineChart playTimeline;
        private StackPanel timelineEmptyHint;
        private TextBlock timelineSpanText;
        private ComboBox timelineYearSelect;
        private ScrollViewer timelineScroll;
        private bool suppressYearChange;   // 程序化填充年份下拉时，别触发切换
        private Border toast;
        private TextBlock toastText;

        private ColumnDefinition colGenre;
        private ColumnDefinition colTop;
        private RowDefinition rowTop;
        private RowDefinition rowReport;
        private RowDefinition rowTimeline;
        private RowDefinition rowWeekly;
        private WeeklyReportChart weeklyChart;
        private FrameworkElement weeklyScroll;
        private ScrollBar weeklyHBar;
        private TextBlock weeklySpanText;
        private StackPanel weeklyEmptyHint;
        private readonly Dictionary<string, Button> basisButtons = new Dictionary<string, Button>();
        private readonly ObservableCollection<GenreLegendRow> legendRows = new ObservableCollection<GenreLegendRow>();
        private readonly ObservableCollection<Top50Row> top50Rows = new ObservableCollection<Top50Row>();
        private readonly ObservableCollection<ReportSection> reportItems = new ObservableCollection<ReportSection>();

        // ---- 趣味功能页 ----
        private Grid vaultBody;
        private Grid funPage;
        private ComboBox pageSelect;
        private bool suppressPageChange;   // 程序化设置下拉选中项时别触发切页
        private AppPage currentPage = AppPage.Stats;

        // 共用顶栏里按页面显隐的控件
        private Border sortGroup;
        private Border searchDivider;
        private Border toneGroup;
        private Button shareButton;
        private Button analyzeCopyButton;

        // ---- 趣味功能页的子标签：待玩清单 / 游戏评价 ----
        private enum FunTab { Wish = 0, Review = 1 }
        private Grid wishPane;
        private Grid reviewPane;
        private Button funTabWish;
        private Button funTabReview;
        private FunTab currentFunTab = FunTab.Wish;

        // ---- 待玩清单 ----
        private ToggleButton wishPickerToggle;
        private Popup wishPickerPopup;
        private Border wishPickerPanel;
        private TextBox wishSearchBox;
        private TextBlock wishSearchHint;
        private ItemsControl wishPickList;
        private TextBlock wishPickEmpty;
        private TextBlock wishPickCount;
        private ItemsControl wishList;
        private StackPanel wishEmptyHint;
        private TextBlock wishCountText;
        private Button wishClearButton;
        private readonly List<Guid> wishIds = new List<Guid>();
        private readonly ObservableCollection<WishRow> wishRows = new ObservableCollection<WishRow>();
        private readonly ObservableCollection<WishPickItem> wishPickItems = new ObservableCollection<WishPickItem>();
        private readonly List<WishRow> pickRowCache = new List<WishRow>();   // 拖拽重排时按顺序保留引用

        // ---- 库变更自动刷新（debounce 用，见 InvalidateForLibraryChange）----
        private DispatcherTimer libraryChangeTimer;

        // ---- 拖拽排序 ----
        private Point dragStartPoint;
        private WishRow dragRow;
        private Popup dragGhostPopup;
        private int dragSourceIndex = -1;
        private int dragTargetIndex = -1;

        private readonly Dictionary<string, Button> sortButtons = new Dictionary<string, Button>();
        // 注意：这不是 readonly。网格视图是「分批追加」的（WrapPanel 不虚拟化，靠分批控制首屏开销），
        // 而分批追加必须往一个**已经绑在 ItemsSource 上**的集合里 Add。
        // 所以「换一批数据」不能原地 Clear+Add（那会逐条触发布局，产生大量"半填充"闪帧），
        // 而是构造一个新集合、一次性替换本字段并重新赋值 ItemsSource。
        private ObservableCollection<GameEntry> displayed = new ObservableCollection<GameEntry>();
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

            var share = root.FindName("ShareButton") as Button;
            if (share != null)
                share.Click += OnShareImageClick;

            if (gameList != null)
            {
                gameList.MouseDoubleClick += OnItemDoubleClick;
                gameList.Loaded += (s, e) => AttachScroll();
            }

            WireAnalyzePage();
            WireFunPage();

            // 顶栏按「库存统计」页的形态初始化（分析页专用的两个按钮收起）
            UpdateTopBarForPage(currentPage);

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
            Unloaded += (s, e) =>
            {
                tintTimer.Stop();
            };
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
            vaultBody = root.FindName("VaultBody") as Grid;
            funPage = root.FindName("FunPage") as Grid;
            analyzePage = root.FindName("AnalyzePage") as Grid;

            // 顶部左上角的下拉框：三个功能页的入口。
            // 项目顺序必须与 AppPage 枚举顺序一致（靠 SelectedIndex 直接转枚举）。
            pageSelect = root.FindName("PageSelect") as ComboBox;
            if (pageSelect != null)
            {
                pageSelect.Items.Add(new ComboBoxItem { Content = L10n.T("LocPageStats") });
                pageSelect.Items.Add(new ComboBoxItem { Content = L10n.T("LocPageAnalyze") });
                pageSelect.Items.Add(new ComboBoxItem { Content = L10n.T("LocPageFun") });
                suppressPageChange = true;
                try { pageSelect.SelectedIndex = 0; }
                finally { suppressPageChange = false; }
                pageSelect.SelectionChanged += OnPageSelected;
            }

            // 共用顶栏里按页面显隐的控件（搜索 / 排序 / 视图 / 分享 属于库存统计页；
            // 评价 / 复制 属于数据分析页）
            sortGroup = root.FindName("SortGroup") as Border;
            searchDivider = root.FindName("SearchDivider") as Border;
            toneGroup = root.FindName("ToneGroup") as Border;
            shareButton = root.FindName("ShareButton") as Button;
            analyzeCopyButton = root.FindName("AnalyzeCopyButton") as Button;

            genreChart = root.FindName("GenreChart") as PieChart;            genreLegend = root.FindName("GenreLegend") as ItemsControl;
            top50List = root.FindName("Top50List") as ItemsControl;
            reportSections = root.FindName("ReportSections") as ItemsControl;
            analyzeSubtitle = root.FindName("AnalyzeSubtitle") as TextBlock;
            top50Subtitle = root.FindName("Top50Subtitle") as TextBlock;
            genreEmptyHint = root.FindName("GenreEmptyHint") as StackPanel;
            playTimeline = root.FindName("PlayTimeline") as TimelineChart;
            timelineEmptyHint = root.FindName("TimelineEmptyHint") as StackPanel;
            timelineSpanText = root.FindName("TimelineSpanText") as TextBlock;
            timelineYearSelect = root.FindName("TimelineYearSelect") as ComboBox;
            timelineScroll = root.FindName("TimelineScroll") as ScrollViewer;

            // 时间轴是「横向」滚动的 ScrollViewer，它嵌在分析页那个「纵向」滚动的 ScrollViewer 里。
            // WPF 的 ScrollViewer 会吃掉鼠标滚轮事件（即便它在某方向滚不动）→ 鼠标停在时间轴框内
            // 上下滚，分析页纹丝不动。这里把纵向滚轮转发给外层的页面 ScrollViewer。
            if (timelineScroll != null)
                timelineScroll.PreviewMouseWheel += OnTimelineWheel;

            // 可拖拽的列 / 行：记住用户调好的尺寸
            colGenre = root.FindName("ColGenre") as ColumnDefinition;
            colTop = root.FindName("ColTop") as ColumnDefinition;
            rowTop = root.FindName("RowTop") as RowDefinition;
            rowTimeline = root.FindName("RowTimeline") as RowDefinition;
            rowWeekly = root.FindName("RowWeekly") as RowDefinition;
            weeklyChart = root.FindName("WeeklyChart") as WeeklyReportChart;
            weeklyScroll = root.FindName("WeeklyScroll") as FrameworkElement;
            weeklyHBar = root.FindName("WeeklyHBar") as ScrollBar;
            weeklySpanText = root.FindName("WeeklySpanText") as TextBlock;
            weeklyEmptyHint = root.FindName("WeeklyEmptyHint") as StackPanel;
            rowReport = root.FindName("RowReport") as RowDefinition;

            if (genreLegend != null) genreLegend.ItemsSource = legendRows;
            if (top50List != null) top50List.ItemsSource = top50Rows;
            if (reportSections != null) reportSections.ItemsSource = reportItems;

            if (genreChart != null)
                genreChart.SliceClicked += OnSliceClicked;

            // 周报横向滚动条：拖它左右浏览整年时间线；
            // 图表尺寸一变（窗口缩放 / 拖分隔条调高度），滚动条参数要跟着对齐。
            if (weeklyHBar != null)
                weeklyHBar.ValueChanged += OnWeeklyBarScroll;
            if (weeklyChart != null)
                weeklyChart.SizeChanged += (s, e) => SyncWeeklyBar();

            // 年份下拉：切换后重新聚合该年的时间轴
            if (timelineYearSelect != null)
                timelineYearSelect.SelectionChanged += OnTimelineYearChanged;

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
            // 时间轴 / 报告 之间的分隔条：拖它给时间轴加高或压矮，尺寸跨会话保留
            var splitTimeline = root.FindName("SplitTimeline") as GridSplitter;
            if (splitTimeline != null)
                splitTimeline.DragCompleted += (s, e) =>
                {
                    if (rowTimeline != null) VaultSettings.SetAnalyzeTimelineHeight(rowTimeline.ActualHeight);
                };
            // 周报 / 报告 之间的分隔条：拖它给周报加高或压矮，尺寸跨会话保留
            var splitWeekly = root.FindName("SplitWeekly") as GridSplitter;
            if (splitWeekly != null)
                splitWeekly.DragCompleted += (s, e) =>
                {
                    if (rowWeekly != null) VaultSettings.SetAnalyzeWeeklyHeight(rowWeekly.ActualHeight);
                };

            AddToneButton();
            AddBasisButton("BasisTime", true);
            AddBasisButton("BasisCount", false);
        }

        // ==================================================================
        // 趣味功能页
        // ==================================================================

        private void WireFunPage()
        {
            wishPickerToggle = root.FindName("WishPickerToggle") as ToggleButton;
            wishPickerPopup = root.FindName("WishPickerPopup") as Popup;
            wishPickerPanel = root.FindName("WishPickerPanel") as Border;
            wishSearchBox = root.FindName("WishSearchBox") as TextBox;
            wishSearchHint = root.FindName("WishSearchHint") as TextBlock;
            wishPickList = root.FindName("WishPickList") as ItemsControl;
            wishPickEmpty = root.FindName("WishPickEmpty") as TextBlock;
            wishPickCount = root.FindName("WishPickCount") as TextBlock;
            wishList = root.FindName("WishList") as ItemsControl;
            wishEmptyHint = root.FindName("WishEmptyHint") as StackPanel;
            wishCountText = root.FindName("WishCountText") as TextBlock;
            wishClearButton = root.FindName("WishClearButton") as Button;

            // ---- 子标签 ----
            wishPane = root.FindName("WishPane") as Grid;
            reviewPane = root.FindName("ReviewPane") as Grid;
            funTabWish = root.FindName("FunTabWish") as Button;
            funTabReview = root.FindName("FunTabReview") as Button;

            if (funTabWish != null) funTabWish.Click += (s, e) => SelectFunTab(FunTab.Wish);
            if (funTabReview != null) funTabReview.Click += (s, e) => SelectFunTab(FunTab.Review);

            SelectFunTab(FunTab.Wish);

            if (wishPickList != null) wishPickList.ItemsSource = wishPickItems;
            if (wishList != null) wishList.ItemsSource = wishRows;

            if (wishPickerToggle != null)
            {
                wishPickerToggle.Checked += (s, e) =>
                {
                    SizeWishPicker();
                    if (wishPickerPopup != null) wishPickerPopup.IsOpen = true;
                    if (wishSearchBox != null)
                    {
                        wishSearchBox.Text = "";
                        wishSearchBox.Focus();
                    }
                    FillWishPicker();
                };
                wishPickerToggle.Unchecked += (s, e) =>
                {
                    if (wishPickerPopup != null) wishPickerPopup.IsOpen = false;
                };
            }

            if (wishPickerPopup != null)
                wishPickerPopup.Closed += (s, e) =>
                {
                    if (wishPickerToggle != null && wishPickerToggle.IsChecked == true)
                        wishPickerToggle.IsChecked = false;
                };

            // 搜索框：输入过滤候选列表，占位提示跟着显隐
            if (wishSearchBox != null)
            {
                wishSearchBox.TextChanged += (s, e) =>
                {
                    if (wishSearchHint != null)
                        wishSearchHint.Visibility = string.IsNullOrEmpty(wishSearchBox.Text)
                            ? Visibility.Visible : Visibility.Collapsed;
                    ApplyWishPickFilter();
                };
            }

            // 勾选 = 加入 / 移出清单（Popup 不关，方便连着勾几款）
            if (wishPickList != null)
            {
                wishPickList.AddHandler(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                    new RoutedEventHandler(OnWishPickToggle));

                // 点行内任意位置都能勾选，不必非得点左边那个方框。
                // 用 Preview 而非 Click：CheckBox 的 Click 到达时状态已经翻过了，
                // 这里要读的是「翻之前」的状态，所以挂在 Preview 阶段取 IsChecked 的原值。
                wishPickList.PreviewMouseLeftButtonUp += OnWishPickRowClick;
            }

            if (wishClearButton != null)
                wishClearButton.Click += OnWishClearClick;

            // 清单行的置顶 / 移除按钮是模板里生成的，用冒泡接一次就够
            if (wishList != null)
                wishList.AddHandler(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                    new RoutedEventHandler(OnWishRowButton));

            // 拖拽排序：拖动柄起拖，拖动中算落点，松手重排
            HookWishDrag();

            LoadWishlist();

            // 库变更自动刷新：Playnite 批量导入时会连发事件，延迟 1.5 秒合并成一次重算。
            libraryChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };

            WireReviewPane();
        }

        /// <summary>
        /// 把「添加游戏」下拉面板定成约占窗口总高度的 1/2（用户要求），宽度按窗口宽适当放宽。
        /// Popup 是独立窗口层，没法用 Binding 直接量到主窗口尺寸，所以打开时现算一次。
        /// </summary>
        private void SizeWishPicker()
        {
            if (wishPickerPanel == null) return;

            double winH = 0, winW = 0;
            var win = Window.GetWindow(this);
            if (win != null)
            {
                winH = win.ActualHeight;
                winW = win.ActualWidth;
            }
            if (!(winH > 0)) winH = SystemParameters.WorkArea.Height;
            if (!(winW > 0)) winW = SystemParameters.WorkArea.Width;

            // 高度取窗口一半，但留出上下边距地板/天花板，避免在极小的窗口里溢出
            double h = Math.Round(winH * 0.5);
            if (h < 260) h = 260;
            if (h > 720) h = 720;
            wishPickerPanel.Height = h;

            // 宽度随窗口走，但夹在一个舒服的区间里（太窄放不下"封面+名称+副行"，太宽不像下拉）
            double w = Math.Round(winW * 0.42);
            if (w < 420) w = 420;
            if (w > 720) w = 720;
            wishPickerPanel.Width = w;
        }

        /// <summary>进入趣味页时把数据重新算一遍（库存可能在别的页面刷新过）。</summary>
        private void RefreshFunPage()
        {
            FillWishPicker();
            RebuildWishRows();
            UpdateWishChrome();
            UpdateWishPickCount();
            RefreshReviewPane();
        }

        /// <summary>
        /// 趣味功能页的两个子标签：待玩清单 / 游戏评价。
        ///
        /// 和顶栏的页面切换同一套做法 —— 只切 Visibility，不重建控件，
        /// 切回来时清单滚动位置、已解码封面、评价正文草稿都原样保留。
        /// 选中态靠 SegButton 样式里的 <c>Tag="Active"</c> 触发器，两个按钮互斥地刷一遍。
        /// </summary>
        private void SelectFunTab(FunTab tab)
        {
            currentFunTab = tab;
            SetVisible(wishPane, tab == FunTab.Wish);
            SetVisible(reviewPane, tab == FunTab.Review);

            if (funTabWish != null) funTabWish.Tag = tab == FunTab.Wish ? "Active" : null;
            if (funTabReview != null) funTabReview.Tag = tab == FunTab.Review ? "Active" : null;
        }

        // ==================================================================
        // 待玩清单 —— 数据
        // ==================================================================

        private void LoadWishlist()
        {
            wishIds.Clear();
            foreach (var raw in VaultSettings.Wishlist)
            {
                Guid id;
                if (Guid.TryParse(raw, out id)) wishIds.Add(id);
            }
        }

        private void SaveWishlist()
        {
            var list = new List<string>(wishIds.Count);
            foreach (var id in wishIds) list.Add(id.ToString());
            VaultSettings.SetWishlist(list);
        }

        /// <summary>把清单里的 Guid 解析成库存条目。找不到的（游戏被删了）自动剔除。</summary>
        private List<GameEntry> ResolveWishlist()
        {
            var byId = new Dictionary<Guid, GameEntry>();
            if (entries != null)
                foreach (var e in entries)
                    if (!byId.ContainsKey(e.PrimaryId)) byId[e.PrimaryId] = e;

            var resolved = new List<GameEntry>();
            var pruned = false;
            for (var i = wishIds.Count - 1; i >= 0; i--)
            {
                GameEntry entry;
                if (byId.TryGetValue(wishIds[i], out entry))
                {
                    resolved.Insert(0, entry);
                }
                else
                {
                    // 游戏已从库里移除：顺手清掉，免得清单里永远挂着一个空壳
                    wishIds.RemoveAt(i);
                    pruned = true;
                }
            }
            if (pruned) SaveWishlist();
            return resolved;
        }

        private void RebuildWishRows()
        {
            wishRows.Clear();
            pickRowCache.Clear();
            var resolved = ResolveWishlist();
            for (var i = 0; i < resolved.Count; i++)
            {
                var row = new WishRow { Index = (i + 1).ToString(), Entry = resolved[i] };
                wishRows.Add(row);
                pickRowCache.Add(row);
            }
        }

        /// <summary>清单序号重排（拖拽 / 移动后调）。只改进序号文本，不重建控件。</summary>
        private void ReindexWishRows()
        {
            for (var i = 0; i < wishRows.Count; i++)
                wishRows[i].Index = (i + 1).ToString();
            SyncWishIdsFromRows();
        }

        /// <summary>把当前行的顺序写回 wishIds 并落盘。</summary>
        private void SyncWishIdsFromRows()
        {
            wishIds.Clear();
            foreach (var row in wishRows)
                if (row.Entry != null) wishIds.Add(row.Entry.PrimaryId);
            SaveWishlist();
        }

        private void UpdateWishChrome()
        {
            if (wishCountText != null)
                wishCountText.Text = wishRows.Count.ToString();
            if (wishEmptyHint != null)
                wishEmptyHint.Visibility = wishRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==================================================================
        // 待玩清单 —— 添加（可搜索的下拉复选框）
        // ==================================================================

        /// <summary>重建候选列表（全部库存游戏，按名称排序），并同步勾选状态。</summary>
        private void FillWishPicker()
        {
            if (wishPickList == null) return;

            var candidates = entries == null
                ? new List<GameEntry>()
                : entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            wishPickItems.Clear();
            foreach (var entry in candidates)
            {
                wishPickItems.Add(new WishPickItem
                {
                    Entry = entry,
                    SearchKey = (entry.Name ?? "").ToLowerInvariant(),
                    IsChecked = wishIds.Contains(entry.PrimaryId)
                });
            }
            ApplyWishPickFilter();
        }

        /// <summary>按搜索词过滤候选列表；搜索框为空则全部显示。</summary>
        private void ApplyWishPickFilter()
        {
            if (wishPickList == null) return;

            var query = wishSearchBox == null ? "" : (wishSearchBox.Text ?? "").Trim().ToLowerInvariant();

            // ItemsControl 没有内置过滤，用 CollectionView 的 Filter 更省事
            var view = CollectionViewSource.GetDefaultView(wishPickItems);
            if (view != null)
            {
                view.Filter = o =>
                {
                    if (query.Length == 0) return true;
                    var item = o as WishPickItem;
                    if (item == null || item.SearchKey == null) return false;
                    return item.SearchKey.IndexOf(query, StringComparison.Ordinal) >= 0;
                };
            }

            var shown = 0;
            foreach (var item in wishPickItems)
                if (query.Length == 0 || (item.SearchKey != null &&
                    item.SearchKey.IndexOf(query, StringComparison.Ordinal) >= 0)) shown++;

            if (wishPickEmpty != null)
                wishPickEmpty.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>候选行里的复选框被点：勾上加入清单，取消勾选移出清单。</summary>
        private void OnWishPickToggle(object sender, RoutedEventArgs e)
        {
            var check = e.OriginalSource as CheckBox;
            if (check == null) return;

            var item = check.DataContext as WishPickItem;
            if (item == null || item.Entry == null) return;

            ApplyWishPick(item, check.IsChecked == true);
        }

        /// <summary>
        /// 候选行里点整行也能勾选 —— 不必非得点左边那个方框。
        ///
        /// 点在复选框本身上的情况要跳过：CheckBox 已经通过 IsChecked 双向绑定
        /// 自己改了状态，再翻一次等于什么都没发生（勾上立刻又取消）。
        /// </summary>
        private void OnWishPickRowClick(object sender, MouseButtonEventArgs e)
        {
            if (IsInteractive(e.OriginalSource as DependencyObject)) return;

            var item = FindDataContext<WishPickItem>(e.OriginalSource as DependencyObject);
            if (item == null || item.Entry == null) return;

            ApplyWishPick(item, !item.IsChecked);
            e.Handled = true;
        }

        /// <summary>加入 / 移出清单的实际动作。复选框和整行点击都汇到这里。</summary>
        private void ApplyWishPick(WishPickItem item, bool want)
        {
            if (item == null || item.Entry == null) return;

            // 幂等地对齐状态：整行点击时这里要真的写值；
            // 勾选框路径下 IsChecked 已经被双向绑定改过了，重复赋同一个值也无所谓。
            // 千万别写成「状态已一致就 return」—— 勾选框路径会被这里直接挡掉，整个勾选就失效了。
            if (item.IsChecked != want) item.IsChecked = want;

            if (want)
            {
                if (wishIds.Contains(item.Entry.PrimaryId))
                {
                    FlashStatus(L10n.F("LocFunAlreadyIn", item.Entry.Name));
                    return;
                }
                wishIds.Add(item.Entry.PrimaryId);
                SaveWishlist();
                RebuildWishRows();
                UpdateWishChrome();
                UpdateWishPickCount();
                FlashStatus(L10n.F("LocFunAdded", item.Entry.Name));
            }
            else
            {
                if (!wishIds.Contains(item.Entry.PrimaryId)) return;
                wishIds.Remove(item.Entry.PrimaryId);
                SaveWishlist();
                RebuildWishRows();
                UpdateWishChrome();
                UpdateWishPickCount();
                FlashStatus(L10n.F("LocFunRemoved", item.Entry.Name));
            }
        }

        /// <summary>
        /// 判断事件源是否落在按钮 / 复选框这类自带交互的元素上。
        /// 整行都能点、整行都能拖，但这两种地方要让控件自己处理。
        /// </summary>
        private static bool IsInteractive(DependencyObject node)
        {
            while (node != null)
            {
                if (node is System.Windows.Controls.Primitives.ButtonBase) return true;
                node = node is Visual || node is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return false;
        }

        /// <summary>Popup 底部那行小字：「清单里已有 N 款」。</summary>
        private void UpdateWishPickCount()
        {
            if (wishPickCount != null)
                wishPickCount.Text = L10n.F("LocFunPickCount", wishRows.Count);
        }

        // ==================================================================
        // 待玩清单 —— 行内按钮 / 清空
        // ==================================================================

        private void OnWishRowButton(object sender, RoutedEventArgs e)
        {
            var target = FindTaggedButton(e.OriginalSource as DependencyObject);
            if (target == null) return;

            var row = target.DataContext as WishRow;
            if (row == null || row.Entry == null) return;

            var action = target.Tag as string;
            if (action == "remove")
            {
                wishRows.Remove(row);
                ReindexWishRows();
                UpdateWishChrome();
                UpdateWishPickCount();
                RefreshPickChecked(row.Entry.PrimaryId, false);
                FlashStatus(L10n.F("LocFunRemoved", row.Entry.Name));
            }
            else if (action == "pin")
            {
                var at = wishRows.IndexOf(row);
                if (at > 0)
                {
                    wishRows.Move(at, 0);
                    ReindexWishRows();
                }
            }
            e.Handled = true;
        }

        /// <summary>从冒泡事件源往上找第一个带 Tag 的 Button（模板里的行内按钮）。</summary>
        private static Button FindTaggedButton(DependencyObject node)
        {
            while (node != null)
            {
                var asButton = node as Button;
                if (asButton != null && asButton.Tag != null) return asButton;
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        /// <summary>把候选列表里某款的勾选状态同步成指定值（清单本身被改动时调）。</summary>
        private void RefreshPickChecked(Guid id, bool isChecked)
        {
            foreach (var item in wishPickItems)
                if (item.Entry != null && item.Entry.PrimaryId == id)
                    item.IsChecked = isChecked;
        }

        private void OnWishClearClick(object sender, RoutedEventArgs e)
        {
            if (wishIds.Count == 0) return;
            wishIds.Clear();
            SaveWishlist();
            RebuildWishRows();
            UpdateWishChrome();
            UpdateWishPickCount();
            foreach (var item in wishPickItems) item.IsChecked = false;
            FlashStatus(L10n.T("LocFunCleared"));
        }

        // ==================================================================
        // 待玩清单 —— 拖拽排序（只在拖动柄上起拖）
        // ==================================================================

        /// <summary>
        /// 拖动柄上按下 = 开始拖；拖动中根据鼠标位置算出落点并高亮；松手后重排。
        /// 用 ItemsControl 的 PreviewMouse 事件是因为行是模板生成的，
        /// 只能在容器层统一挂监听，再用 VisualTreeHelper 找回是哪一行。
        /// </summary>
        private void HookWishDrag()
        {
            if (wishList == null) return;

            wishList.PreviewMouseLeftButtonDown += OnWishDragStart;
            wishList.PreviewMouseMove += OnWishDragMove;
            wishList.PreviewMouseLeftButtonUp += OnWishDragEnd;
        }

        private void OnWishDragStart(object sender, MouseButtonEventArgs e)
        {
            // 整行都能起拖，不必非得点左边那两根杠。
            // 但「置顶 / 移除」这两个按钮上要让控件自己处理，否则点按钮会变成拖拽。
            if (IsInteractive(e.OriginalSource as DependencyObject)) return;

            var container = FindWishRowContainer(e.OriginalSource as DependencyObject);
            if (container == null) return;

            dragRow = container.DataContext as WishRow;
            if (dragRow == null) return;

            dragStartPoint = e.GetPosition(wishList);
            dragSourceIndex = wishRows.IndexOf(dragRow);
            dragTargetIndex = dragSourceIndex;

            // 占住鼠标捕获，拖动过程才跟得住；不设 e.Handled，按钮点击仍能正常冒泡
            wishList.CaptureMouse();
        }

        private void OnWishDragMove(object sender, MouseEventArgs e)
        {
            if (dragRow == null || e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(wishList);

            // 起拖前先设一个阈值，避免手一抖就把行拽走
            if (dragGhostPopup == null &&
                Math.Abs(pos.Y - dragStartPoint.Y) < 4) return;

            if (dragGhostPopup == null) ShowDragGhost();

            if (dragGhostPopup != null)
                dragGhostPopup.HorizontalOffset = e.GetPosition(wishList).X;
            if (dragGhostPopup != null)
                dragGhostPopup.VerticalOffset = e.GetPosition(wishList).Y;

            dragTargetIndex = WishIndexAt(pos);
        }

        private void OnWishDragEnd(object sender, MouseButtonEventArgs e)
        {
            if (dragRow == null) return;

            if (dragGhostPopup != null)
            {
                dragGhostPopup.IsOpen = false;
                dragGhostPopup = null;
            }
            if (wishList != null && wishList.IsMouseCaptured) wishList.ReleaseMouseCapture();

            if (dragTargetIndex >= 0 && dragTargetIndex < wishRows.Count &&
                dragTargetIndex != dragSourceIndex)
            {
                wishRows.Move(dragSourceIndex, dragTargetIndex);
                ReindexWishRows();
            }

            dragRow = null;
            dragSourceIndex = -1;
            dragTargetIndex = -1;
            e.Handled = true;
        }

        /// <summary>鼠标纵向位置落在清单的第几行上（用行容器的实际高度做区间判断）。</summary>
        private int WishIndexAt(Point pos)
        {
            var index = 0;
            for (var i = 0; i < pickRowCache.Count; i++)
            {
                var container = FindRowContainerByData(pickRowCache[i]);
                if (container == null) continue;
                try
                {
                    var top = container.TranslatePoint(new Point(0, 0), wishList).Y;
                    if (pos.Y >= top) index = i;
                    else break;
                }
                catch { }
            }
            return index;
        }

        private ContentPresenter FindWishRowContainer(DependencyObject node)
        {
            while (node != null)
            {
                var presenter = node as ContentPresenter;
                if (presenter != null && presenter.DataContext is WishRow) return presenter;
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        private ContentPresenter FindRowContainerByData(object data)
        {
            if (wishList == null) return null;
            return FindContainerRecursive(wishList, data);
        }

        private static ContentPresenter FindContainerRecursive(DependencyObject node, object data)
        {
            if (node == null) return null;
            var presenter = node as ContentPresenter;
            if (presenter != null && ReferenceEquals(presenter.DataContext, data)) return presenter;

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var hit = FindContainerRecursive(VisualTreeHelper.GetChild(node, i), data);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>在鼠标下挂一个半透明小卡片，代替被拖的行。</summary>
        private void ShowDragGhost()
        {
            try
            {
                var ghost = new DragGhost
                {
                    Index = dragRow.Index,
                    Name = dragRow.Name,
                    Cover = dragRow.Cover
                };
                var content = BuildDragGhostContent(ghost);

                dragGhostPopup = new Popup
                {
                    AllowsTransparency = true,
                    IsHitTestVisible = false,
                    Child = content,
                    Placement = PlacementMode.Relative,
                    PlacementTarget = wishList
                };
                dragGhostPopup.IsOpen = true;
            }
            catch { }
        }

        private FrameworkElement BuildDragGhostContent(DragGhost ghost)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0.9 };
            panel.Children.Add(new TextBlock
            {
                Text = ghost.Index,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            if (ghost.Cover != null)
            {
                panel.Children.Add(new Border
                {
                    Width = 22,
                    Height = 30,
                    CornerRadius = new CornerRadius(3),
                    Background = new ImageBrush(ghost.Cover)
                    {
                        Stretch = Stretch.UniformToFill
                    },
                    Margin = new Thickness(0, 0, 10, 0)
                });
            }
            panel.Children.Add(new TextBlock
            {
                Text = ghost.Name,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center
            });
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x27, 0x35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x38, 0x49)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 14, 7),
                Child = panel
            };
        }

        /// <summary>把上次存下来的面板尺寸恢复到 XAML 定义上（只在首次进入分析页时调）。</summary>
        private void RestoreAnalyzeLayout()        {
            if (colGenre != null && VaultSettings.AnalyzeGenreWidth > 0)
                colGenre.Width = new GridLength(VaultSettings.AnalyzeGenreWidth, GridUnitType.Pixel);
            if (rowTop != null && VaultSettings.AnalyzeTopHeight > 0)
                rowTop.Height = new GridLength(VaultSettings.AnalyzeTopHeight, GridUnitType.Pixel);
            if (rowTimeline != null && VaultSettings.AnalyzeTimelineHeight > 0)
                rowTimeline.Height = new GridLength(VaultSettings.AnalyzeTimelineHeight, GridUnitType.Pixel);
            // 周报区块高度：拖分隔条改完存在设置里，切回分析页时恢复
            if (rowWeekly != null && VaultSettings.AnalyzeWeeklyHeight > 0)
                rowWeekly.Height = new GridLength(VaultSettings.AnalyzeWeeklyHeight, GridUnitType.Pixel);
        }

        private void AddToneButton()
        {
            // 文风开关已取消：报告统一为「傲娇锐评」（顶部标签显示为「评价」）。
            // 这里只保留按钮的鼠标手型样式，点它等同于重新生成一次（方便用户看完重读）。
            var button = root.FindName("ToneSnarky") as Button;
            if (button == null) return;
            button.Click += (s, e) => BuildReport();
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

        /// <summary>
        /// 三个功能页的统一入口：库存统计 / 数据分析 / 趣味功能。
        /// 顶栏（含这个下拉框自己）常驻不隐藏，切换的只是正文区。
        /// </summary>
        private void SelectPage(AppPage page)
        {
            currentPage = page;

            // 下拉框显示同步（用户点下拉项时这里已是同一项，加哨兵避免递归）
            if (pageSelect != null && pageSelect.SelectedIndex != (int)page)
            {
                suppressPageChange = true;
                try { pageSelect.SelectedIndex = (int)page; }
                finally { suppressPageChange = false; }
            }

            if (vaultBody != null)
                vaultBody.Visibility = page == AppPage.Stats ? Visibility.Visible : Visibility.Collapsed;
            if (analyzePage != null)
                analyzePage.Visibility = page == AppPage.Analyze ? Visibility.Visible : Visibility.Collapsed;
            if (funPage != null)
                funPage.Visibility = page == AppPage.Fun ? Visibility.Visible : Visibility.Collapsed;

            UpdateTopBarForPage(page);

            if (page == AppPage.Analyze) BuildAnalyzePage();
            else if (page == AppPage.Fun) RefreshFunPage();
        }

        /// <summary>
        /// 共用顶栏里，哪些控件属于哪个页面。
        /// 搜索框 / 排序 / 视图切换 / 分享是「库存统计」专用的；
        /// 评价 / 复制分享是「数据分析」专用的；「趣味功能」两个都不用。
        /// 靠 Visibility 切换而不是重建，切回来时状态（搜索词、排序选中项）原样保留。
        /// </summary>
        private void UpdateTopBarForPage(AppPage page)
        {
            var stats = page == AppPage.Stats;
            var analyze = page == AppPage.Analyze;

            SetVisible(searchInput, stats);
            SetVisible(sortGroup, stats);
            SetVisible(searchDivider, stats);
            SetVisible(viewToggle, stats);
            SetVisible(shareButton, stats);

            SetVisible(toneGroup, analyze);
            SetVisible(analyzeCopyButton, analyze);
        }

        private static void SetVisible(UIElement element, bool visible)
        {
            if (element != null)
                element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>兼容旧调用（返回库存页 = 切回库存统计）。</summary>
        private void ShowPage(bool analyze)
        {
            SelectPage(analyze ? AppPage.Analyze : AppPage.Stats);
        }

        private void OnPageSelected(object sender, SelectionChangedEventArgs e)
        {
            if (suppressPageChange) return;
            if (pageSelect == null) return;
            var index = pageSelect.SelectedIndex;
            if (index < 0) return;
            SelectPage((AppPage)index);
        }

        private void BuildAnalyzePage()
        {
            RestoreAnalyzeLayout();

            BuildGenreChart();
            BuildTop50();
            BuildTimeline();

            BuildWeekly();
            BuildReport();

            if (analyzeSubtitle != null && lastData != null)
                analyzeSubtitle.Text = L10n.F("LocAnalyzeSubtitle", lastData.MergedCount);
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
            foreach (var section in ProfileReport.Build(lastData))
                reportItems.Add(section);
        }

        // ---- 游玩时间轴 ----

        /// <summary>
        /// 把按年聚合的游玩数据交给时间轴控件渲染。
        /// 没有 GameActivity 数据时显示安装指引（而不是一根柱子都没有的空白）。
        /// </summary>
        private void BuildTimeline()
        {
            // 先刷年份下拉：数据刷新后可用年份可能变了
            FillTimelineYears();

            var timeline = lastData != null ? lastData.Timeline : null;
            var hasData = timeline != null && timeline.Available
                          && timeline.Weeks != null && timeline.Weeks.Count > 0
                          && timeline.TotalSeconds > 0;

            if (timelineEmptyHint != null)
                timelineEmptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            if (timelineScroll != null)
                timelineScroll.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
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

        /// <summary>
        /// 填充「游戏周报」：近一年每一周的海报时间轴。
        ///
        /// 和 <see cref="BuildTimeline"/> 分开而不是合在一起，是因为两者的空态判定
        /// 和标题栏文案都不一样：时间轴按「所选年」算，周报固定是「最近 53 周」。
        /// 每次数据刷新都重算 —— 周报窗口是滚动的，缓存旧数据会让"最近一周"停在过去。
        /// </summary>
        private void BuildWeekly()
        {
            // 双击海报要跳到库视图，构造时拿不到 api，这里补一次注入
            if (weeklyChart != null && weeklyChart.Api == null) weeklyChart.Api = api;

            var weekly = lastData != null && lastData.Activity != null
                ? VaultData.BuildWeekly(lastData, lastData.Activity)
                : null;

            var hasData = weekly != null && weekly.Available
                          && weekly.Weeks != null && weekly.Weeks.Count > 0
                          && weekly.TotalSeconds > 0;

            if (weeklyEmptyHint != null)
                weeklyEmptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            if (weeklyScroll != null)
                weeklyScroll.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
            if (weeklyHBar != null)
                weeklyHBar.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
            if (weeklyChart != null)
            {
                weeklyChart.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
                weeklyChart.SetData(hasData ? weekly : null);
            }

            // 标题右侧补一句概览：一年里玩了 52 周、共多少时长
            if (weeklySpanText != null)
                weeklySpanText.Text = hasData
                    ? L10n.F("LocWeeklySpan", weekly.ActiveWeeks, weekly.TotalText)
                    : "";

            if (hasData) ScrollWeeklyToEnd();
        }

        /// <summary>
        /// 把滚动条参数对齐到图表当前的内容宽度 / 视口宽度。
        /// 数据刷新、窗口缩放、拖分隔条调高度之后都要跑一遍，
        /// 否则 thumb 长度和可拖范围跟实际内容对不上。
        /// </summary>
        private void SyncWeeklyBar()
        {
            if (weeklyHBar == null || weeklyChart == null) return;

            var max = weeklyChart.MaxOffset;
            weeklyHBar.Maximum = max;
            weeklyHBar.ViewportSize = Math.Max(0, weeklyChart.ActualWidth);
            weeklyHBar.LargeChange = Math.Max(60, weeklyChart.ActualWidth * 0.8);
            weeklyHBar.SmallChange = 40;
            weeklyHBar.Visibility = max > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>拖滚动条 → 图表跟着平移。</summary>
        private void OnWeeklyBarScroll(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (weeklyChart != null) weeklyChart.Offset = e.NewValue;
        }

        /// <summary>
        /// 周报横向铺了 53 周，最新的一周在最右边。打开就看一年前很莫名其妙，
        /// 所以每次刷新数据都先贴到最右端。
        /// </summary>
        private void ScrollWeeklyToEnd()
        {
            if (weeklyChart == null) return;

            weeklyChart.Offset = weeklyChart.MaxOffset;
            SyncWeeklyBar();
            if (weeklyHBar != null) weeklyHBar.Value = weeklyChart.Offset;

            // SetData 之后这一帧视口宽度还是旧值，等布局跑完再对齐一次。
            weeklyChart.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (weeklyChart == null) return;
                SyncWeeklyBar();
                if (weeklyHBar != null) weeklyHBar.Value = weeklyChart.Offset;
            }));
        }
        /// <summary>
        /// 填充「年份」下拉：数据层给的是倒序年份列表（最新年在前），默认选中第一项。
        /// 尽量保留用户当前选中的年份，避免刷新后跳回最新年。
        /// </summary>
        private void FillTimelineYears()
        {
            if (timelineYearSelect == null) return;

            var years = lastData != null && lastData.TimelineYears != null
                ? lastData.TimelineYears
                : new List<int>();
            var previous = timelineYearSelect.SelectedItem is int ? (int)timelineYearSelect.SelectedItem : 0;

            suppressYearChange = true;
            try
            {
                timelineYearSelect.Items.Clear();
                foreach (var year in years)
                    timelineYearSelect.Items.Add(year);
                if (years.Count == 0) return;

                var pick = previous != 0 && years.Contains(previous) ? previous : years[0];
                timelineYearSelect.SelectedItem = pick;
            }
            finally
            {
                suppressYearChange = false;
            }
        }

        /// <summary>
        /// 鼠标落在时间轴框内滚动时，把**纵向**滚轮转发给外层页面 ScrollViewer。
        ///
        /// 为什么需要：时间轴自己是个横向 ScrollViewer。WPF 里内层 ScrollViewer 会把
        /// MouseWheel 事件标记为已处理（哪怕它在竖直方向压根滚不动），于是页面上下滚不了。
        /// 这里用 PreviewMouseWheel 抢在它处理之前判断：只有纵向滚动需求（delta 有意义）
        /// 才转发给父级，横向的滚轮/触摸板手势仍留给时间轴自己。
        /// </summary>
        private void OnTimelineWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0) return;

            // 找到最近的、能在纵向滚动的祖先 ScrollViewer（就是分析页那个整页滚动条）
            var parent = FindVerticalScrollParent(timelineScroll);
            if (parent == null || parent.ScrollableHeight <= 0) return;

            // 自己手动滚，然后把事件标记为已处理，避免内层 ScrollViewer 再吃一次
            parent.ScrollToVerticalOffset(parent.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>沿视觉树向上找第一个「纵向可滚动」的祖先 ScrollViewer。</summary>
        private static ScrollViewer FindVerticalScrollParent(DependencyObject start)
        {
            var node = start == null ? null : VisualTreeHelper.GetParent(start);
            while (node != null)
            {
                var sv = node as ScrollViewer;
                if (sv != null && sv.ScrollableHeight > 0) return sv;
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        /// <summary>
        /// 年份切换：用选中的年重新聚合时间轴（不重新读库，只重算这一年的周）。
        /// </summary>
        private void OnTimelineYearChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressYearChange) return;
            if (timelineYearSelect == null || !(timelineYearSelect.SelectedItem is int)) return;
            if (lastData == null) return;

            var year = (int)timelineYearSelect.SelectedItem;
            var timeline = VaultData.BuildTimeline(lastData, lastData.Activity, year);

            var hasData = timeline.Available && timeline.Weeks != null
                          && timeline.Weeks.Count > 0 && timeline.TotalSeconds > 0;

            if (timelineEmptyHint != null)
                timelineEmptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            if (timelineScroll != null)
                timelineScroll.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
            if (playTimeline != null)
            {
                playTimeline.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
                playTimeline.SetData(hasData ? timeline : null);
            }
            if (timelineSpanText != null)
                timelineSpanText.Text = hasData ? timeline.SpanText : "";

            // 换年先滚回最左边，否则去年看到一半的位置会串到今年
            if (timelineScroll != null) timelineScroll.ScrollToHorizontalOffset(0);
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
        /// 生成分享长图：渲染 → 弹保存对话框让用户选路径 → 写文件 + 写剪贴板。
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
                png = ShareCard.Render(lastData);
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

            // 先问保存路径：默认落在「图片\GameVault」，用户可以自己挑别处。
            // 这一步必须在渲染之后（渲染失败就别弹窗了），但在写文件之前。
            var target = AskSavePath();
            if (string.IsNullOrEmpty(target))
            {
                // 用户取消：图已经生成好了，顺手复制到剪贴板，不白点一次
                var copiedAnyway = TryCopyImage(png);
                FlashStatus(copiedAnyway ? L10n.T("LocShareCopied") : L10n.T("LocShareCanceled"));
                return;
            }

            var saved = ShareCard.SaveTo(png, target) ? target : null;

            // 剪贴板优先：多数人要的是"直接粘贴到聊天窗"
            var copied = TryCopyImage(png);

            if (saved != null)
                FlashStatus(L10n.F("LocShareSavedPick", saved));
            else if (copied)
                FlashStatus(L10n.T("LocShareCopied"));
            else
                FlashStatus(L10n.T("LocShareFailed"));
        }

        /// <summary>
        /// 弹「另存为」对话框。默认文件名 GameVault_时间戳.png，初始目录是「图片\GameVault」。
        /// 用户取消返回 null。对话框本身抛异常（极少见）时退回默认路径，保证功能不中断。
        /// </summary>
        private static string AskSavePath()
        {
            var fallback = ShareCard.DefaultPath();
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = L10n.T("LocShareDialogTitle"),
                    FileName = fallback != null ? System.IO.Path.GetFileName(fallback) : "GameVault.png",
                    DefaultExt = ".png",
                    Filter = L10n.T("LocShareFilter"),
                    AddExtension = true,
                    OverwritePrompt = true,
                };
                if (fallback != null)
                {
                    var dir = System.IO.Path.GetDirectoryName(fallback);
                    if (!string.IsNullOrEmpty(dir)) dialog.InitialDirectory = dir;
                }
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            }
            catch
            {
                return fallback;
            }
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

        /// <summary>
        /// 造一个新的分页集合（首屏一页），**不就地改**当前的 displayed。
        /// 关键点：绝不能对当前已绑定的 displayed 做 Clear() + 逐条 Add()——WrapPanel 非虚拟化，
        /// 每一条 Add 都会单独触发一轮 measure/arrange，滚动条 extent 先塌成 0 再逐条长回来，
        /// 中间会出现二十多次"只有一部分海报、且没排完版"的密集闪帧（用户可见的 bug）。
        /// 由 ApplyView 一次性把这个新集合赋给 ItemsSource，只会触发 1 次布局，直接到位。
        /// </summary>
        private ObservableCollection<GameEntry> BuildFirstPage()
        {
            var page = new ObservableCollection<GameEntry>();
            var next = Math.Min(visible.Count, PageSize);
            for (var i = 0; i < next; i++)
                page.Add(visible[i]);
            return page;
        }

        private void AppendPage()
        {
            if (displayed == null || displayed.Count >= visible.Count) return;
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

        /// <summary>
        /// 库发生变化后由插件类调用：丢弃缓存并整份重算。
        ///
        /// 走的是和首次加载完全相同的路径（VaultCache.Ensure(force:true) 会重新遍历
        /// 数据库、重算时长/类型/评分/画像），所以库存、统计、分析页、待玩清单的
        /// 候选、已评价列表的可用性判断都会跟着更新，不需要各处单独处理。
        ///
        /// 做了两层防抖：
        ///   ① Playnite 批量导入 / 刮削时会在很短时间内连发很多次事件，
        ///      每次都重算等于把数据库白遍历若干遍，所以延迟 1.5 秒合并；
        ///   ② 期间再来新事件就重新计时（debounce 而不是 throttle）。
        /// </summary>
        public void InvalidateForLibraryChange()
        {
            if (libraryChangeTimer == null) return;

            libraryChangeTimer.Stop();
            libraryChangeTimer.Tick -= OnLibraryChangeTick;
            libraryChangeTimer.Tick += OnLibraryChangeTick;
            libraryChangeTimer.Start();
        }

        private void OnLibraryChangeTick(object sender, EventArgs e)
        {
            if (libraryChangeTimer == null) return;
            libraryChangeTimer.Stop();
            libraryChangeTimer.Tick -= OnLibraryChangeTick;

            try
            {
                RefreshAsync(true);
            }
            catch
            {
            }
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
            vm.SourceText = data.ActivityAvailable ? L10n.T("LocSourceOk") : L10n.T("LocSourceMissing");

            // ---- MC 分数分布 ----
            ApplyScoreDistribution(data.Facts);

            // ---- 「近两周时长」浮出层的逐款明细 ----
            ApplyRecentRows(data);

            // ---- 「最肝游戏」浮出层的时长 Top 5 ----
            ApplyTopPlayRows(data);

            // ---- 「总游戏时长」浮出层的逐库明细 ----
            vm.SourcePlayRows.Clear();
            if (data.SourcePlaytimes != null)
                foreach (var row in data.SourcePlaytimes)
                    vm.SourcePlayRows.Add(row);

            // ---- 「库存总数」浮出层的逐库明细 ----
            vm.SourceCountRows.Clear();
            if (data.SourceGameCounts != null)
                foreach (var row in data.SourceGameCounts)
                    vm.SourceCountRows.Add(row);

            // ---- 趣味功能 · 游戏评价 ----
            // 库存变了（游戏被删 / 合并）时，已评价列表要重新对一遍库存条目，
            // 顺带把「游戏已不在库里」的评价记录清掉。
            RefreshReviewPane();
        }

        /// <summary>
        /// 「最肝游戏」卡片浮出层：总游玩时长前 5 名，每行「名次 + 游戏名 + 总时长」。
        /// 只统计玩过的（时长 &gt; 0），全都没玩时为空白。
        /// </summary>
        private void ApplyTopPlayRows(VaultData data)
        {
            vm.TopPlayRows.Clear();
            if (data == null || data.Entries == null) return;

            var ranked = data.Entries
                .Where(e => e.TotalPlaytime > 0)
                .OrderByDescending(e => e.TotalPlaytime)
                .ToList();

            const int maxRows = 5;
            for (int i = 0; i < ranked.Count && i < maxRows; i++)
                vm.TopPlayRows.Add(new TopPlayRow(
                    (i + 1).ToString(), ranked[i].Name, TimeFmt.Short(ranked[i].TotalPlaytime)));

        }

        /// <summary>
        /// 「近两周时长」卡片浮出层：列出这两周真正玩过的每一款（按这两周时长降序）。
        /// 最多列 8 行，其余用一行"还有 N 款"带过 —— 浮出层太高会顶到屏幕底部。
        /// </summary>
        private void ApplyRecentRows(VaultData data)
        {
            vm.RecentRows.Clear();
            if (data == null || data.Entries == null) { vm.RecentRowsHint = ""; return; }

            var active = data.Entries
                .Where(e => e.RecentPlaytime > 0)
                .OrderByDescending(e => e.RecentPlaytime)
                .ToList();

            const int maxRows = 8;
            foreach (var e in active.Take(maxRows))
                vm.RecentRows.Add(new RecentRow(e.Name, TimeFmt.Short(e.RecentPlaytime)));

            vm.RecentRowsHint = active.Count > maxRows
                ? L10n.F("LocRecentMore", active.Count - maxRows)
                : "";
        }

        /// <summary>
        /// 把 MC 分数分布写进概览卡。五档与 GameEntry.ScoreTier 同口径，
        /// 无评分的游戏不计入任何一档（分档总数 = ScoredCount）。
        /// </summary>
        private void ApplyScoreDistribution(ProfileFacts f)
        {
            if (f == null) return;
            int b90 = f.ScoreBucket90, b80 = f.ScoreBucket80, b70 = f.ScoreBucket70,
                b60 = f.ScoreBucket60, blow = f.ScoreBucketLow;
            bool en = L10n.IsEnglish;

            vm.ScoreBucket90Text = b90.ToString();
            vm.ScoreBucket80Text = b80.ToString();
            vm.ScoreBucket70Text = b70.ToString();
            vm.ScoreBucket60Text = b60.ToString();
            vm.ScoreBucketLowText = blow.ToString();

            // 副行优先给"最高档占比"这个最有信息量的数字；没有评分时退回提示。
            var scored = b90 + b80 + b70 + b60 + blow;
            if (scored <= 0)
            {
                vm.ScoreDistSubText = L10n.T("LocScoreDistNone");
                return;
            }

            var excellent = b90 + b80;
            var share = (double)excellent / scored;
            vm.ScoreDistSubText = L10n.F("LocScoreDistSub", excellent, Pct(share), scored);
        }

        static string Pct(double v)
        {
            return (v * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>把一段操作切到 UI 线程执行（进度回调来自后台线程）。</summary>
        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (Dispatcher.CheckAccess()) action();
            else Dispatcher.BeginInvoke(action);
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
                    // 「近两周」是一个**筛选**口径，不只是排序：没在近两周玩过的直接不显示，
                    // 否则列表里混着一堆 0 分钟的游戏，"最近在玩什么"这个意图就表达不出来。
                    list = list.Where(e => e.RecentPlaytime > 0)
                               .OrderByDescending(e => e.RecentPlaytime)
                               .ThenByDescending(e => e.TotalPlaytime);
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

            // 造一个新的分页集合，并把它一次性交给列表（不在旧集合上原地增删，避免密集闪帧）。
            var page = BuildFirstPage();
            ApplyView(page);

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
            {
                // 「近两周」空列表是很正常的状态（这两周真的没玩游戏），
                // 用专门的一句话解释，比通用的"没有匹配的游戏"更不容易让人以为坏了。
                emptyHint.Text = L10n.T(sortMode == SortMode.Recent ? "LocEmptyRecent" : "LocEmpty");
                emptyHint.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyView()
        {
            ApplyView(null);
        }

        /// <param name="gridSource">
        /// 网格视图要绑的分页集合；传 null 表示沿用当前 displayed。
        /// 由 ApplyFilter 传进来的是 BuildFirstPage() 新造好的集合——一次性替换，
        /// 避免在已绑定的集合上原地增删导致的密集闪帧。
        /// </param>
        private void ApplyView(ObservableCollection<GameEntry> gridSource)
        {
            if (gameList == null) return;

            var isGrid = viewMode == ViewMode.Grid;
            var replacedSource = gridSource != null && !ReferenceEquals(gridSource, displayed);
            if (gridSource != null) displayed = gridSource;

            // 先把「面板 / 模板 / 滚动模式」全部切换到位，最后一刻才换 ItemsSource。
            // 顺序很重要：若先换 ItemsSource，新数据会先按旧模板渲染一帧再重排，
            // 会看到"旧样式的新内容"闪一下。
            gameList.ItemsPanel = isGrid ? wrapPanelTemplate : stackPanelTemplate;
            gameList.ItemTemplate = isGrid ? cardTemplate : barTemplate;
            gameList.Padding = isGrid ? new Thickness(16, 0, 6, 64) : new Thickness(16, 0, 6, 16);
            // 网格用 WrapPanel（像素滚动），条形用 VirtualizingStackPanel（按项滚动才能虚拟化）
            ScrollViewer.SetCanContentScroll(gameList, !isGrid);

            gameList.ItemsSource = isGrid ? (IEnumerable<GameEntry>)displayed : visible;

            // 整批换数据时把滚动位置复位，否则会停在上一次的偏移量上直接看中段。
            if (replacedSource && listScroll != null)
                listScroll.ScrollToVerticalOffset(0);

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
            ScorePalette.ApplySharedFlow(phase);   // MC 分数分布卡上的 90+ 流光，与封面徽章同相位
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
            UpdatePageSelectText();
            RebuildWishRows();

            // 分析页 / 趣味页的文案（中心数字、副标题、报告正文、清单提示）全是 C# 生成的，
            // 替换资源字典刷不到，必须整页重建一次。
            if (analyzePage != null && analyzePage.Visibility == Visibility.Visible)
                BuildAnalyzePage();
            if (funPage != null && funPage.Visibility == Visibility.Visible)
            {
                RefreshFunPage();
            }
        }

        /// <summary>换语言时刷新顶部下拉框的三个选项文字（ComboBoxItem 的 Content 是 C# 设的）。</summary>
        private void UpdatePageSelectText()
        {
            if (pageSelect == null) return;
            var keys = new[] { "LocPageStats", "LocPageAnalyze", "LocPageFun" };
            for (var i = 0; i < pageSelect.Items.Count && i < keys.Length; i++)
            {
                var item = pageSelect.Items[i] as ComboBoxItem;
                if (item != null) item.Content = L10n.T(keys[i]);
            }
        }

        private void FlashStatus(string message)
        {
            vm.StatusText = message;
            statusTimer.Stop();
            statusTimer.Start();

            // 分析页 / 趣味页没有状态栏，所以一并弹个 toast —— 否则用户点分享后看不到任何反馈
            var pageVisible = (analyzePage != null && analyzePage.Visibility == Visibility.Visible)
                              || (funPage != null && funPage.Visibility == Visibility.Visible);
            if (pageVisible)
                ShowToast(message);
        }

        /// <summary>
        /// 淡入一个轻量提示：内容区底部的覆盖层，不参与顶栏行布局、也不抢窗口焦点
        /// （用 Popup 的话每次弹出都会抢激活，导致紧接着的那一下列表点击被 Windows 吞掉）。
        /// </summary>
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
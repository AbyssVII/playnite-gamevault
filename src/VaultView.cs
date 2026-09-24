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

        private readonly Dictionary<string, Button> sortButtons = new Dictionary<string, Button>();
        private readonly ObservableCollection<GameEntry> displayed = new ObservableCollection<GameEntry>();
        private readonly DispatcherTimer searchTimer;
        private readonly DispatcherTimer statusTimer;
        private readonly DispatcherTimer tintTimer;

        private List<GameEntry> entries = new List<GameEntry>();
        private List<GameEntry> visible = new List<GameEntry>();
        private SortMode sortMode = SortMode.Total;
        private ViewMode viewMode = ViewMode.Grid;
        private string normalStatus = "";
        private ScrollViewer listScroll;
        private ScrollBar listBar;
        private VaultData lastData;

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
                share.Click += OnShareClick;

            if (gameList != null)
            {
                gameList.MouseDoubleClick += OnItemDoubleClick;
                gameList.Loaded += (s, e) => AttachScroll();
            }

            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplyFilter(); };

            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            statusTimer.Tick += (s, e) =>
            {
                statusTimer.Stop();
                if (!string.IsNullOrEmpty(normalStatus)) vm.StatusText = normalStatus;
            };

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
        }

        private void OnShareClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(BuildShareText());
                FlashStatus(L10n.T("LocCopied"));
            }
            catch
            {
                FlashStatus(L10n.T("LocCopyFailed"));
            }
        }

        private string BuildShareText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(L10n.T("LocShareTitle"));
            sb.AppendLine(L10n.F("LocShareCount", vm.EntryCountText));
            sb.AppendLine(L10n.F("LocShareTotal", vm.TotalPlaytimeText));
            sb.AppendLine(L10n.F("LocShareRecent", vm.RecentPlaytimeText));
            sb.AppendLine(L10n.F("LocShareTop", vm.TopGameName, vm.TopGameTimeText));
            sb.AppendLine(L10n.F("LocShareAvg", vm.AverageScoreText));
            sb.AppendLine();

            var recentMode = sortMode == SortMode.Recent;
            var scoreMode = sortMode == SortMode.Score;
            sb.AppendLine(L10n.T(recentMode
                ? "LocShareTop10Recent"
                : (scoreMode ? "LocShareTop10Score" : "LocShareTop10Total")));

            var top = visible.Take(10).ToList();
            for (var i = 0; i < top.Count; i++)
            {
                var g = top[i];
                string value;
                if (recentMode) value = g.RecentText;
                else if (scoreMode) value = g.HasScore ? "MC " + g.ScoreText : L10n.T("LocNoScore");
                else value = g.TotalText;
                sb.AppendLine(string.Format("{0}. {1} — {2}", i + 1, g.Name, value));
            }

            sb.AppendLine();
            sb.Append(L10n.F("LocShareFooter", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
            return sb.ToString();
        }

        private void FlashStatus(string message)
        {
            vm.StatusText = message;
            statusTimer.Stop();
            statusTimer.Start();
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

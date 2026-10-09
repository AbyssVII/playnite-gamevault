using System;
using System.Windows;
using System.Windows.Controls;

namespace GameVault
{
    /// <summary>
    /// 扩展设置面板。
    ///
    /// 目前插件没有可配置项（原「AI 补全」功能已随库存价值一起移除），
    /// 这里保留一个说明页：Playnite 在扩展没有设置视图时会显示一句
    /// "This extension has no settings"，容易让人以为是装坏了；
    /// 给一段说明比留空白清楚。
    /// </summary>
    public class GameVaultSettingsView : UserControl
    {
        public GameVaultSettingsView()
        {
            var panel = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };

            panel.Children.Add(MakeHeader("游戏库存"));

            var zh =
                "本插件无需配置。\n\n" +
                "· 侧边栏「游戏库存」页展示全部 Playnite 游戏，支持排序、搜索与网格/条形两种视图。\n" +
                "· 顶栏下拉选项卡可切换到「数据分析」（类型分布、时长 Top 50、玩家画像报告）" +
                "与「趣味功能」（待玩清单）。\n" +
                "· 卡片大小用库存页右下角的缩放条调整，会自动记住。\n\n" +
                "语言跟随插件顶栏右上角的语言选择，改完立即生效。";

            var en =
                "This extension needs no configuration.\n\n" +
                "· The Library page in the sidebar shows every Playnite game with sorting, " +
                "search, and both grid and bar views.\n" +
                "· The dropdown tab in the top bar switches to Analytics (genre breakdown, " +
                "Top 50 by playtime, player profile report) and Fun & Games (to-play list).\n" +
                "· Card size is adjusted with the zoom bar at the bottom-right of the Library " +
                "page and remembered automatically.\n\n" +
                "Language follows the selector at the top-right of the plugin's top bar and " +
                "applies immediately.";

            panel.Children.Add(MakeHint(L10n.IsEnglish ? en : zh));

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel
            };
        }

        /// <summary>Playnite 点「保存」时调用（这里无可保存项）。</summary>
        public void SaveSettings()
        {
        }

        /// <summary>Playnite 点「取消」时调用（这里无可还原项）。</summary>
        public void ResetSettings()
        {
        }

        // ---- UI 小工具 ----

        static TextBlock MakeHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        static TextBlock MakeHint(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 560,
                LineHeight = 20,
                Opacity = 0.8,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace GameVault
{
    public class GameVaultPlugin : GenericPlugin
    {
        public static readonly Guid PluginGuid = new Guid("7f3a91c4-6e28-4d15-b0aa-4c19d7e53b82");

        /// <summary>
        /// 侧边栏项目显示的标题。
        ///
        /// ⚠️ 这个字符串同时承担「排序键」的职责，不要随手改。
        /// Playnite 的侧边栏顺序完全由 DesktopAppViewModel.LoadSideBarItems() 决定：
        ///     sideItems = sideItems.OrderByDescending(a =&gt; (int)a.SideItem.Type)
        ///                          .ThenBy(a =&gt; a.SideItem.Title);
        ///     sideItems.Insert(0, 统计); sideItems.Insert(0, 库);   // 库/统计 写死在最前，无法移动
        /// 所有插件的侧边栏项 Type 都是 View(1)，所以插件之间**只按 Title 的 UTF-16 码点比较**。
        /// 本机实测（zh_CN）：「Playnite 成就」= 0x50… 、「最近活动」= 0x6700…。
        /// 以 'G'(0x47) 开头 ⇒ 排在「Playnite 成就」(0x50) 之前 ⇒ 插件中第一 ⇒ 侧边栏第 3 位。
        /// 见 vaulttest 的 SidebarOrderTest()。
        /// </summary>
        public const string SidebarTitle = "GameVault 游戏库存";

        private VaultView view;

        public GameVaultPlugin(IPlayniteAPI api) : base(api)
        {
            // 读取上次保存的界面设置（卡片缩放、界面语言），保证视图创建时就是用户上次的状态
            try
            {
                VaultSettings.Init(GetPluginUserDataPath());
            }
            catch
            {
            }

            // 必须在任何 XAML 加载之前把语言资源挂上去，否则 DynamicResource 找不到键
            try
            {
                L10n.Apply(VaultSettings.Language);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 预热库存数据。
        /// 注意：必须放在 OnApplicationStarted 而不是构造函数里 ——
        /// 插件构造函数执行时 Playnite 的游戏数据库还没打开（日志中 Loaded plugin 早于
        /// Opening db），此时遍历 Database.Games 只会得到 0 款游戏。
        /// </summary>
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            base.OnApplicationStarted(args);
            try
            {
                VaultCache.Ensure(PlayniteApi, false);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 库里有游戏被添加 / 移除 / 更新后，丢弃缓存并让已打开的视图整份重算。
        ///
        /// 刻意走 OnLibraryUpdated 而不是自己订阅 Database.Games.CollectionChanged：
        /// 前者是 Playnite 在**导入/刮削/手动编辑都完成之后**才发出的"库已定稿"信号，
        /// 一次操作只触发一次；后者在批量导入时会连发几百次，还得自己判断何时算"完"。
        ///
        /// view 为 null 说明侧边栏还没被打开过 —— 那就什么都不用做：
        /// 视图下次创建时本来就会读到最新数据（VaultCache 的 5 分钟过期也会自然兜底）。
        /// </summary>
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            base.OnLibraryUpdated(args);
            try
            {
                if (view == null) return;

                // Playnite 可能在导入用的工作线程上发这个事件，而 DispatcherTimer
                // 只能在创建它的线程（UI 线程）上 Start —— 跨线程调用会抛异常，
                // 所以先切回 UI 线程再让视图自己处理。
                var dispatcher = view.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                    dispatcher.BeginInvoke(new Action(view.InvalidateForLibraryChange));
                else
                    view.InvalidateForLibraryChange();
            }
            catch
            {
            }
        }

        public override Guid Id
        {
            get { return PluginGuid; }
        }

        /// <summary>
        /// 扩展设置面板：只放「AI 补全」需要的三项（API Key / 接口地址 / 模型）。
        /// 刻意用代码搭 UI 而不是 XAML —— 设置面板很小，省一份嵌入资源与解析开销。
        /// </summary>
        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GameVaultSettingsView();
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = SidebarTitle,
                Type = SiderbarItemType.View,
                Visible = true,
                Icon = new TextBlock
                {
                    Text = "\uE7F4",
                    FontSize = 20,
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC2FF"))
                },
                Opened = () =>
                {
                    if (view == null)
                        view = new VaultView(PlayniteApi);
                    return view;
                },
                Closed = () => { }
            };
        }
    }
}

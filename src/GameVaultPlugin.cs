using System;
using System.Collections.Generic;
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

        public override Guid Id
        {
            get { return PluginGuid; }
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = "游戏库存",
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

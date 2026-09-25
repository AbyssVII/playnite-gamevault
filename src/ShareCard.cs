using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameVault
{
    /// <summary>
    /// 「分享长图」生成器。
    ///
    /// 为什么不直接截屏分析页：
    ///   - 分析页是 1280×N 的窗口，比例不适合竖版社交分享；
    ///   - 里面有可拖拽面板、滚动条、按钮等"操作痕迹"，截图会显得很乱；
    ///   - 分享图需要稳定的排版 —— 用户换了窗口大小/语言/文风，出图都该是同一套版式。
    ///
    /// 因此这里**重新搭一棵视觉树**，只挑最有信息量的内容（概览数字、类型分布、
    /// 时长 Top、玩家画像摘要），按固定 1080 宽的竖版排版渲染成 PNG。
    /// 所有颜色写死为深色主题 —— 分享图是独立作品，不跟随 Playnite 主题。
    /// </summary>
    public static class ShareCard
    {
        private const int Width = 1080;
        private const double Pad = 56;

        // 配色（与插件深色主题一致，但刻意独立于资源字典，避免受主题/语言影响）
        private static readonly Brush Bg = Frozen("#0B0E14");
        private static readonly Brush Panel = Frozen("#141B25");
        private static readonly Brush PanelSoft = Frozen("#1A222E");
        private static readonly Brush Border = Frozen("#232E3D");
        private static readonly Brush TextMain = Frozen("#E9F0F8");
        private static readonly Brush TextDim = Frozen("#A9B7C8");
        private static readonly Brush TextFaint = Frozen("#7A8899");
        private static readonly Brush Accent = Frozen("#4CC2FF");
        private static readonly Brush Green = Frozen("#34D399");
        private static readonly Brush Gold = Frozen("#F5B942");

        private static Brush Frozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 生成分享长图并返回 PNG 字节。失败返回 null（调用方负责提示）。
        /// </summary>
        public static byte[] Render(VaultData data, ReportTone tone)
        {
            if (data == null || data.MergedCount == 0) return null;

            try
            {
                var visual = BuildVisual(data, tone);
                return Rasterize(visual);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>把分享图存到「图片\GameVault」目录，返回完整路径（失败返回 null）。</summary>
        public static string SaveToPictures(byte[] png)
        {
            if (png == null) return null;
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (string.IsNullOrEmpty(root)) return null;
                var dir = Path.Combine(root, "GameVault");
                Directory.CreateDirectory(dir);
                var name = "GameVault_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                var path = Path.Combine(dir, name);
                File.WriteAllBytes(path, png);
                return path;
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------
        // 视觉树
        // ------------------------------------------------------------------

        private static FrameworkElement BuildVisual(VaultData data, ReportTone tone)
        {
            var root = new Border
            {
                Background = Bg,
                Padding = new Thickness(Pad),
                Width = Width,
                SnapsToDevicePixels = true,
            };

            var stack = new StackPanel();
            root.Child = stack;

            stack.Children.Add(BuildHeader(data));
            stack.Children.Add(BuildStatStrip(data));
            stack.Children.Add(BuildGenreBlock(data));
            stack.Children.Add(BuildTopBlock(data));
            stack.Children.Add(BuildReportBlock(data, tone));
            stack.Children.Add(BuildFooter());

            // 先把宽度定死再测量，这样子元素的换行/自适应才有确定结果
            root.Measure(new Size(Width, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, Width, root.DesiredSize.Height));
            root.UpdateLayout();
            return root;
        }

        private static FrameworkElement BuildHeader(VaultData data)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 26) };

            var eyebrow = new TextBlock
            {
                Text = L10n.T("LocShareEyebrow"),
                FontSize = 17,
                Foreground = Accent,
                FontWeight = FontWeights.SemiBold,
            };
            panel.Children.Add(eyebrow);

            var title = new TextBlock
            {
                Text = L10n.T("LocShareTitle"),
                FontSize = 46,
                FontWeight = FontWeights.Bold,
                Foreground = TextMain,
                Margin = new Thickness(0, 8, 0, 0),
            };
            panel.Children.Add(title);

            var sub = new TextBlock
            {
                Text = L10n.F("LocShareSubtitle", data.MergedCount, TimeFmt.Short(data.TotalPlaytime)),
                FontSize = 20,
                Foreground = TextDim,
                Margin = new Thickness(0, 12, 0, 0),
            };
            panel.Children.Add(sub);

            panel.Children.Add(new Border
            {
                Height = 3,
                Width = 92,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Accent,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 22, 0, 0),
            });

            return panel;
        }

        /// <summary>概览数字条：4 个关键指标横排。</summary>
        private static FrameworkElement BuildStatStrip(VaultData data)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 26) };
            for (var i = 0; i < 4; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var top = data.TopByTime.FirstOrDefault();
            var items = new (string Value, string Label, Brush Color)[]
            {
                (data.MergedCount.ToString(CultureInfo.InvariantCulture), L10n.T("LocShareStatGames"), Accent),
                (TimeFmt.Short(data.TotalPlaytime), L10n.T("LocShareStatPlaytime"), Green),
                (data.Facts != null ? data.Facts.PlayedGames.ToString(CultureInfo.InvariantCulture) : "—",
                    L10n.T("LocShareStatPlayed"), Gold),
                (top != null ? top.TotalText : "—", L10n.T("LocShareStatTop"), Frozen("#C084FC")),
            };

            for (var i = 0; i < items.Length; i++)
            {
                var cell = new Border
                {
                    Background = Panel,
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(22, 20, 22, 20),
                    Margin = new Thickness(i == 0 ? 0 : 6, 0, i == items.Length - 1 ? 0 : 6, 0),
                };
                var inner = new StackPanel();
                inner.Children.Add(new TextBlock
                {
                    Text = items[i].Value,
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Foreground = items[i].Color,
                });
                inner.Children.Add(new TextBlock
                {
                    Text = items[i].Label,
                    FontSize = 15,
                    Foreground = TextFaint,
                    Margin = new Thickness(0, 6, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                cell.Child = inner;
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }

            return grid;
        }

        /// <summary>类型分布：横向条形（比饼图在长图里更省空间、数字更好读）。</summary>
        private static FrameworkElement BuildGenreBlock(VaultData data)
        {
            var slices = data.GenreByTime.Take(7).ToList();
            if (slices.Count == 0) return new StackPanel();

            var wrap = new Border
            {
                Background = Panel,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(26, 24, 26, 22),
                Margin = new Thickness(0, 0, 0, 22),
            };
            var stack = new StackPanel();
            wrap.Child = stack;

            stack.Children.Add(SectionTitle(L10n.T("LocShareGenres"), "\uE8FD"));

            foreach (var s in slices)
            {
                var row = new Grid { Margin = new Thickness(0, 14, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

                var name = new TextBlock
                {
                    Text = s.Name,
                    FontSize = 18,
                    Foreground = TextDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 14, 0),
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);

                var track = new Border
                {
                    Background = PanelSoft,
                    CornerRadius = new CornerRadius(6),
                    Height = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var barHost = new Grid();
                track.Child = barHost;
                var bar = new Border
                {
                    Background = ToBrush(s.Color),
                    CornerRadius = new CornerRadius(6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(10, 640 * s.Share),
                };
                barHost.Children.Add(bar);
                Grid.SetColumn(track, 1);
                row.Children.Add(track);

                var share = new TextBlock
                {
                    Text = s.ShareText,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMain,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                Grid.SetColumn(share, 2);
                row.Children.Add(share);

                stack.Children.Add(row);
            }

            return wrap;
        }

        /// <summary>时长 Top 8 榜单。</summary>
        private static FrameworkElement BuildTopBlock(VaultData data)
        {
            var top = data.TopByTime.Take(8).ToList();
            if (top.Count == 0) return new StackPanel();

            var wrap = new Border
            {
                Background = Panel,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(26, 24, 26, 22),
                Margin = new Thickness(0, 0, 0, 22),
            };
            var stack = new StackPanel();
            wrap.Child = stack;

            stack.Children.Add(SectionTitle(L10n.T("LocShareTop"), "\uE735"));

            var max = top[0].TotalPlaytime;
            for (var i = 0; i < top.Count; i++)
            {
                var e = top[i];
                var row = new Grid { Margin = new Thickness(0, 15, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

                var rank = new TextBlock
                {
                    Text = (i + 1).ToString(CultureInfo.InvariantCulture),
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = i == 0 ? Gold : TextFaint,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(rank, 0);
                row.Children.Add(rank);

                var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                nameStack.Children.Add(new TextBlock
                {
                    Text = e.Name,
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMain,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                if (!string.IsNullOrEmpty(e.Genres))
                    nameStack.Children.Add(new TextBlock
                    {
                        Text = e.Genres,
                        FontSize = 14,
                        Foreground = TextFaint,
                        Margin = new Thickness(0, 3, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                Grid.SetColumn(nameStack, 1);
                row.Children.Add(nameStack);

                var time = new TextBlock
                {
                    Text = e.TotalText,
                    FontSize = 19,
                    Foreground = TextDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                Grid.SetColumn(time, 2);
                row.Children.Add(time);

                stack.Children.Add(row);

                // 细进度条：一眼看出与榜首的差距
                var bar = new Border
                {
                    Height = 4,
                    Background = PanelSoft,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(46, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                var fill = new Border
                {
                    Background = Accent,
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = max > 0 ? Math.Max(6, 900.0 * e.TotalPlaytime / max) : 6,
                };
                bar.Child = fill;
                stack.Children.Add(bar);
            }

            return wrap;
        }

        /// <summary>玩家画像摘要：挑 3 段有代表性的，避免长图过长。</summary>
        private static FrameworkElement BuildReportBlock(VaultData data, ReportTone tone)
        {
            var sections = ProfileReport.Build(data, tone);
            if (sections.Count == 0) return new StackPanel();

            var picked = new List<ReportSection>();
            foreach (var s in sections)
            {
                if (picked.Count >= 3) break;
                if (string.IsNullOrEmpty(s.Title) || string.IsNullOrEmpty(s.Body)) continue;
                picked.Add(s);
            }
            if (picked.Count == 0) return new StackPanel();

            var wrap = new Border
            {
                Background = Panel,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(26, 24, 26, 24),
                Margin = new Thickness(0, 0, 0, 22),
            };
            var stack = new StackPanel();
            wrap.Child = stack;

            stack.Children.Add(SectionTitle(L10n.T("LocShareReport"), "\uE9F9"));

            foreach (var s in picked)
            {
                var row = new Grid { Margin = new Thickness(0, 16, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

                var stripe = new Border
                {
                    Background = ToBrush(s.Color),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 2, 0, 2),
                };
                Grid.SetColumn(stripe, 0);
                row.Children.Add(stripe);

                var body = new StackPanel { Margin = new Thickness(16, 0, 16, 0) };
                body.Children.Add(new TextBlock
                {
                    Text = s.Title,
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMain,
                });
                if (!string.IsNullOrEmpty(s.Body))
                    body.Children.Add(WrapText(s.Body, 15, TextDim, new Thickness(0, 7, 0, 0)));
                Grid.SetColumn(body, 1);
                row.Children.Add(body);

                if (!string.IsNullOrEmpty(s.Metric))
                {
                    var metric = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
                    metric.Children.Add(new TextBlock
                    {
                        Text = s.Metric,
                        FontSize = 30,
                        FontWeight = FontWeights.Bold,
                        Foreground = ToBrush(s.Color),
                        HorizontalAlignment = HorizontalAlignment.Right,
                    });
                    if (!string.IsNullOrEmpty(s.MetricLabel))
                        metric.Children.Add(new TextBlock
                        {
                            Text = s.MetricLabel,
                            FontSize = 14,
                            Foreground = TextFaint,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Margin = new Thickness(0, 3, 0, 0),
                        });
                    Grid.SetColumn(metric, 2);
                    row.Children.Add(metric);
                }

                stack.Children.Add(row);
            }

            return wrap;
        }

        private static FrameworkElement BuildFooter()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = Border,
                Margin = new Thickness(0, 0, 0, 18),
            });
            stack.Children.Add(new TextBlock
            {
                Text = L10n.F("LocShareFooter", DateTime.Now.ToString("yyyy-MM-dd")),
                FontSize = 16,
                Foreground = TextFaint,
            });
            return stack;
        }

        // ------------------------------------------------------------------
        // 小工具
        // ------------------------------------------------------------------

        private static FrameworkElement SectionTitle(string text, string glyph)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = Accent,
                VerticalAlignment = VerticalAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = TextMain,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            return panel;
        }

        /// <summary>长文本按固定宽度折行。Measure 之前没法用 TextWrapping 精确控宽，所以给个上限。</summary>
        private static TextBlock WrapText(string text, double size, Brush color, Thickness margin)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = color,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = size * 1.6,
                MaxWidth = Width - Pad * 2 - 26 * 2 - 4 - 16 - 16 - 150,
                Margin = margin,
            };
        }

        private static Brush ToBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return TextDim;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return TextDim;
            }
        }

        private static byte[] Rasterize(FrameworkElement visual)
        {
            var height = (int)Math.Ceiling(visual.ActualHeight);
            var width = (int)Math.Ceiling(visual.ActualWidth);
            if (height <= 0 || width <= 0) return null;

            // 2x 缩放：分享图在小屏上被压缩也不会糊
            const double scale = 2.0;
            var rtb = new RenderTargetBitmap(
                (int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
    }
}

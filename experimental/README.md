# experimental/ — 未采用的实现，留档备查

这个目录里的代码**不参与编译**（`GameVault.csproj` 只通配编译 `src/*.cs`），保留在这里是为了记录走过的弯路。

## `VirtualizingWrapPanel.cs`

**目标**：网格视图既要「拖动缩放条时卡片实时换行」，又要「只渲染可见项」。

**为什么放弃**：`VirtualizingPanel` 的子类化在 WPF 里有两个绕不过去的坑——

1. `base.ItemContainerGenerator` 在**首次测量的时序下返回 `null`**（此时 `ItemsControl.ItemContainerGenerator` 明明有值），导致第一轮测量拿不到容器生成器；
2. 改用 `ItemsControl` 的生成器绕过去之后，回收滚出视区的容器时 `ItemContainerGenerator.Remove` 会在内部抛异常。

两条路都在 WPF 内部崩掉，所以最终改成了更朴实的组合：

- **换行**交给 `WrapPanel`（卡片宽度一变就自动重排，天然实时、零代码）
- **性能**用分批加载解决（首批 150 项，滚动接近底部再追加 150 项）

效果上，拖动缩放条完全跟手；代价是换行面板不支持虚拟化，超大库存下滚动条长度是动态的。

如果你有兴趣修好这条路，欢迎提 PR —— 关键是在 `MeasureOverride` 里对生成器为空的情况做正确的降级，而不是直接返回 `availableSize`。

# V3-03 代码锚点

状态：`CURRENT — V3-03 R1 INPUT CLUSTER RELEASE CANDIDATE`

## 几何与权限

- `src/DesktopUp.Domain/PosterInteraction.cs`：FrameGeometry、展开/收起高度、最小尺寸、USING/EDITING/LOCKED 权限，以及连续 Item viewport/滚轮 offset 策略；
- `src/DesktopUp.Application/Contracts.cs`：InputIsland 区域、ItemScroll、wheel event、layout revision 与 `IInputRegionHost` 端口；
- `tests/DesktopUp.Contracts.Tests/PosterInteractionTests.cs`：收起锚点、锁定、编辑缩放、尺寸边界、部分行连续滚动和 offset 夹紧测试。

## PosterWindowCluster

- `src/DesktopUp.App/PosterVisualWindow.cs`：NIGHT OPS 视觉、USING 零外框/EDITING 状态边线、WinUI `ScrollViewer`、六 Item、滚动 offset 命中、按帧拖缩合并、commit/rollback、锁定、顶部锚定收起和 post-composition 修复；
- `src/DesktopUp.Windows/PosterInputIslandManager.cs`：稀疏原生窗口区域、pointer capture、屏幕坐标 delta、按钮/Item invoke、Item/滚动轨 wheel、光标、owner 重挂与清理；
- `src/DesktopUp.Windows/DesktopLayerAttachment.cs`：视觉 HWND 的 `WS_EX_LAYERED | WS_EX_TRANSPARENT` 合成穿透、样式健康检测、视觉/输入岛共同 owner attach；
- `src/DesktopUp.Windows/DesktopEnvironmentWatcher.cs`：视觉 HWND 返回 `HTTRANSPARENT`，并继续执行 V3-02 激活和 Z-order 守卫；
- `src/DesktopUp.App/TrayService.cs`、`src/DesktopUp.App/App.xaml.cs`：显式进入/完成海报编辑入口。

## 自动证据

- `scripts/smoke-desktop-locator.ps1`：真实 DefView 上创建 poster cluster、结构日志、退出和 resource ledger 清理；
- `tests/DesktopUp.Contracts.Tests/DesktopLayerAttachmentTests.cs`：WinUI 清除 layered style 后的失效检测与 reassert；
- `tests/DesktopUp.Contracts.Tests/ArchitectureTests.cs`：透明命中、desktop host 和架构依赖守卫。

## 后续边界

- V3-04 接管 geometry commit 的 SQLite/revision/CAS；
- V3-05 用真实 ContentItem 行为替换六个测试意图；
- V3-06 在当前六项标准 ScrollViewer 基线上接管大集合虚拟化、完整动态列数、活跃度和可撤销排版。

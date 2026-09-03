# V3-02 代码锚点

状态：`CLOSED — V3-02 LAYER REVISION R2`

## 端口与稳定状态

- `src/DesktopUp.Application/Contracts.cs`：`IDesktopShellLocator`、稳定 host snapshot、`IDesktopLayerAttachment`、显示拓扑 snapshot/服务、海报逻辑布局状态和双 generation 版本对；
- `src/DesktopUp.Windows/DesktopShellStabilityTracker.cs`：连续候选采样、缺失重置、显式失效和单调 generation；
- `src/DesktopUp.Windows/ExplorerDesktopLocator.cs`：有界等待、缓存有效性复查和稳定快照；
- `src/DesktopUp.Windows/ExplorerDesktopNativeApi.cs`：只枚举已存在的 `SHELLDLL_DefView`，校验 Progman/WorkerW 父级和 Explorer PID；不生成 WorkerW；
- `src/DesktopUp.Windows/DisplayTopologyService.cs`：枚举显示器、物理 bounds、工作区和 DPI，按签名连续稳定采样并生成独立 topology generation；
- `src/DesktopUp.Windows/PosterGeometryProjector.cs`：以 DIP 尺寸和归一化锚点投影物理 bounds，并夹紧至目标工作区；
- `src/DesktopUp.Windows/DesktopRecoveryVersionGate.cs`：恢复请求版本和 shell/topology generation 对的迟到提交拒绝。

## 桌面 owner 与诊断海报

- `src/DesktopUp.Windows/DesktopLayerAttachment.cs`：保存原 owner/style，移除非客户区，应用 toolwindow/no-activate/non-topmost，设置 DefView owner，按 shell generation 识别过期 attachment；提供原子 bottom/show 和 no-show bottom reassert，失败回滚和 detach；
- `src/DesktopUp.Windows/DesktopEnvironmentWatcher.cs`：在现有 poster HWND 上监听 `TaskbarCreated`、`WM_DISPLAYCHANGE`、`WM_DPICHANGED` 和相关 `WM_SETTINGCHANGE`；同一 subclass 在 `WM_MOUSEACTIVATE` 中同步 reassert 并返回 `MA_NOACTIVATE`，在 `WM_ACTIVATE`/`WM_ACTIVATEAPP` 中同步归位，并在 `WM_WINDOWPOSCHANGING` 提交前约束非 bottom Z-order；
- `src/DesktopUp.App/PosterVisualWindow.cs`：420×240 DIP 的纯色 WinUI 诊断面；隐藏态准备内容/bounds 后以 native bottom/show 一次变为可见，不再调用独立 `AppWindow.Show`；恢复复用同一 HWND 并最终 no-show reassert；
- `src/DesktopUp.App/App.xaml.cs`：启动时并行等待 shell/topology 稳定；合并信号、防抖、单航班恢复、有界重试、双 generation 提交前后验证、2 秒只读健康兜底以及有序停机；
- `tests/DesktopUp.Contracts.Tests/DisplayTopologyTests.cs`：拓扑稳定、fresh signature、100/125/150/200% DPI、工作区夹紧和版本门；
- `tests/DesktopUp.Contracts.Tests/DesktopEnvironmentSignalTests.cs`：原生消息分类、pointer/activation 同步归位及 `WINDOWPOS` 提交前约束合同；
- `tests/DesktopUp.Contracts.Tests/DesktopLayerAttachmentTests.cs`：旧 attachment generation 拒绝、新 generation 重挂、原子 show flag 与 no-show reassert；
- `scripts/smoke-desktop-locator.ps1`：真实 Explorer stable + topology stable + poster attached/shown + monitoring start/stop + no error + clean ledger 证据。

## 当前明确未完成

- NIGHT OPS、透明空白输入岛、拖缩、锁定、收起和 Item。
- Explorer 真实重启、Win+D、层级和显示变化仍需在最新 Release 上完成人工 P0 门。

第一项属于 V3-03；第二项是 V3-02 关闭条件，不能从自动测试或启动 smoke 推导为人工 PASS。

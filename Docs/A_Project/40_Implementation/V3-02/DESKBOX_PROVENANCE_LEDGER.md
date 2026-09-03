# DeskBox 来源登记 — V3-02

来源仓库：`https://github.com/Tianyu199509/DeskBox.git`

来源分支：`main`

来源 commit：`64f27e1aa0f16867abe20074f35ea05cd7b12ce7`

许可证：`GPL-3.0-only`

## 当前参考范围

| DeskBox 锚点 | DesktopUp 用途 | 处理 |
| --- | --- | --- |
| `src/DeskBox/Services/WidgetLayerService.cs` | 只找既有 DefView、owner read-back、失败恢复、启动稳定采样、原子 bottom/show、no-show bottom reassert 与 pointer activation policy | 按独立端口重新实现；增加 generation、PID/父级校验、失效缓存和统一 resting-layer 合同 |
| `tests/DeskBox.Tests/DesktopShellStartupSafetyTests.cs` | 连续采样和禁止生成 WorkerW 的测试意图 | 重新建立 DesktopUp 测试，不复制测试类和项目路径 |
| `src/DeskBox/Views/WidgetWindowBase.Bounds.cs` | AppWindow 无边框 presenter、移除 Win32 非客户区、desktop-pinned subclass、`WM_MOUSEACTIVATE`/`MA_NOACTIVATE` 和 Activated 后恢复 | 采用窗口边界与激活守卫顺序；DesktopUp 使用新的纯色诊断窗口和统一 no-show bottom reassert，并把 Activated 落成 `WM_ACTIVATE`/`WM_ACTIVATEAPP`；实机新增的 `WM_WINDOWPOSCHANGING` 约束是 V3 补强，不宣称来自 DeskBox |
| `src/DeskBox/Helpers/Win32Helper.cs` | owner/style/SetWindowPos/DWM API 集合 | 仅实现 V3-02 当前所需最小 P/Invoke，不整体移植 helper |
| `src/DeskBox/Services/AppLifecycleRecoveryWatcher.cs`、`AppLifecycleRecoverySignalClassifier.cs` | `TaskbarCreated`、display/DPI、resume/session 信号分类与恢复防抖 | 吸收信号来源；V3 将 Explorer 恢复升级为独立 generation 与单航班显式重挂 |
| `src/DeskBox/Services/WidgetDisplayChangeWatcher.cs` | 单窗口 display/DPI/work-area/TaskbarCreated 监听及交互期间抑制 | 吸收消息矩阵和交互互斥意图；不复制“等待后续窗口动作顺便恢复”的缺口 |
| `src/DeskBox/Services/DisplayAreaWatcherService.cs` | 低频拓扑轮询、签名与防抖 | 吸收为原生消息的兜底，不用于周期性抢层级 |
| `src/DeskBox/Services/DisplayTopologyTransitionCoordinator.cs` | topology generation、连续稳定样本、应用后验证、迟到结果拒绝和有界重试 | 独立重建 DesktopUp 协调器，并与 `DesktopShellGeneration` 组成版本对 |
| `src/DeskBox/Services/WidgetPositioningService.cs`、`WidgetTopologyLayoutService.cs` | DIP/物理像素换算、显示器身份、四角锚点、margin、profile 与工作区夹紧 | 首发投影到主屏单海报；保留未来扩展字段，不声明多屏能力已完成 |
| `src/DeskBox/Services/WidgetManager.cs` | 拓扑事务期间停止持久化、批量窗口恢复与后置层级整理 | 吸收事务边界；DesktopUp 要求同一 PosterWindowCluster 原子提交和逐项读回验证 |

当前代码未复制 DeskBox 品牌、图标、文案或 Widget 业务。DesktopUp 探索分支保持 GPL-3.0-only；Explorer recovery、显示 watcher、稳定 topology generation、工作区投影、原子 bottom/show、pointer/activation guard 已按上表用途独立实现并登记。`WM_WINDOWPOSCHANGING` 提交前约束属于 DesktopUp V3 根据 R1 实机失败独立形成的补强。

完整的吸收/补强裁决及验收合同见 `DESKBOX_RECOVERY_REFERENCE_AND_V3_STRENGTHENING.md`。V3-02-08/09 自动实现已完成，真实 Explorer 重建与显示变化仍以 V3-02 完整人工 P0 门为关闭依据。

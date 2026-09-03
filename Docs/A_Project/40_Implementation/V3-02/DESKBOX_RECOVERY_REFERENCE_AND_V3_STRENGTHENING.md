# DeskBox 恢复机制吸收与 V3 补强记录

状态：`ABSORBED — V3-02-08/09 + DESKTOP-PINNED LAYER REVISION R2 IMPLEMENTED / MANUAL P0 PENDING`

日期：2026-09-03

适用分支：`impl/poster-redesign-V3`

## 1. 目的与裁决

本记录冻结 DeskBox 在 Explorer 重建、显示拓扑、DPI、工作区变化和 desktop-pinned 鼠标激活方面可直接吸收的工程经验，以及 DesktopUp V3 不能照搬、必须补强的部分。V3-02 自动实现已按本记录落地；真实 Z-order、Explorer 重建和显示变化仍以人工 P0 为准。

DeskBox 是实现证据，不是 DesktopUp 的需求权威。DeskBox 当前实现中的缺口不得被复制成 DesktopUp 合同。

## 2. DeskBox 已证实的参考事实

### 2.1 Explorer 与桌面宿主

- 在隐藏托盘 HWND 和各 Widget HWND 上监听注册消息 `TaskbarCreated`；应用级恢复合并约 420 ms，单窗口显示变化恢复合并约 280 ms；
- Explorer、显示或 DPI 信号到达后使缓存的 `SHELLDLL_DefView` 失效；
- 重新枚举 Explorer 已经创建的 `SHELLDLL_DefView`，不发送 `0x052C`，不主动生成 WorkerW；
- 启动阶段最多探测 48 次、间隔 250 ms，并要求连续 5 次获得同一有效 DefView；
- 使用 `SetWindowLongPtr(GWLP_HWNDPARENT)` 建立 owner 关系，而不是把窗口改成真正 child；
- attach 后读回 owner，清除 topmost，并以 `SWP_NOACTIVATE` 放回桌面静止层；失败时恢复原 owner 并再次使 host 缓存失效。

### 2.2 显示拓扑与重投影

- 单窗口监听 `WM_DISPLAYCHANGE`、`WM_DPICHANGED`、相关 `WM_SETTINGCHANGE` 和 `TaskbarCreated`；
- 全局每 2000 ms 枚举显示器，变化后防抖 500 ms；签名包含设备名、主屏标记、DPI、monitor bounds 和 work area；
- XAML `RasterizationScale` 变化作为额外 DPI 信号；
- `DisplayTopologyTransitionCoordinator` 为每次恢复请求增加 generation，要求连续两次相同签名，首次应用后延迟约 140 ms 验证，未完成时最多重试 8 次，并拒绝旧 generation 的迟到结果；
- 在拓扑事务期间禁止把系统暂态位置写回正式配置；拖动/缩放期间抑制单窗口恢复，交互结束后的用户最终位置保持权威；
- 为拓扑保留独立布局 profile；使用逻辑尺寸、物理 HWND bounds、四角锚点和相对工作区 margin 进行重投影，最后夹入有效工作区。

### 2.3 Desktop-pinned 激活与可见提交

- `MoveToDesktopBottom` 先应用 resting `WS_EX_NOACTIVATE`，再附着 DefView owner、清除 topmost 并放入 `HWND_BOTTOM`；
- 初次显示时，`TryAttachToDesktopIconLayer` 把 `SWP_SHOWWINDOW` 与 `HWND_BOTTOM` 放在同一次 `SetWindowPos` 中，避免先排底后单独 Show 又被系统重新插入普通窗口队列；
- 每个 desktop-pinned Widget HWND 通过 `SetWindowSubclass` 监听 `WM_MOUSEACTIVATE`；
- 当鼠标交互不得夺取前台时，消息返回 `MA_NOACTIVATE`，保留鼠标消息但不激活窗口；
- 返回 `MA_NOACTIVATE` 前同步执行 `MoveToDesktopBottom(showWindow:false)`，修复 owner/Z-order，但绝不重新显示一个本应隐藏的窗口；
- WinUI `Activated` 变化是补充恢复点；需要键盘输入时才显式临时解除 no-activate，并在交互结束恢复桌面层。

## 3. V3 直接吸收项

### 3.1 V3-02-08 Explorer 恢复

1. 使用 `TaskbarCreated`、HWND 有效性、Explorer PID/父级变化和 owner read-back 失败作为失效信号；
2. 只发现已有 DefView，不强制创建 WorkerW；
3. 失效时立即废弃旧快照，重新执行有界稳定采样；
4. owner attach 必须保存原值、设置、读回验证、失败回滚；
5. 全程保持 non-topmost、no-activate，不通过周期抢层、临时置顶或 Win+D 后 hide/show 恢复；
6. 恢复完成必须留下可关联的 handle、owner、PID、generation 和结果诊断事件。

### 3.2 V3-02-09 显示变化

1. 同时接收窗口原生消息与低频全局拓扑兜底检测；
2. 把设备身份、主屏、DPI、monitor bounds 和 work area 纳入稳定签名；
3. 变化先合并并等待稳定，再进行一次重投影；
4. 使用 DIP 语义尺寸和目标显示器 DPI 生成物理 bounds；
5. 优先保持已冻结锚点及其逻辑 margin；无可靠锚点时才按工作区相对位置投影；
6. 最终 bounds 必须位于当前主屏有效工作区；
7. 拖动/缩放和拓扑恢复互斥，恢复期间不持久化暂态几何。

### 3.3 Desktop-pinned 激活与首次显示

1. 完整创建 XAML 内容、设置 presenter 并计算/提交隐藏态 bounds；
2. 最后一次 native 提交同时完成 owner、non-topmost、no-activate、`HWND_BOTTOM` 和 `SWP_SHOWWINDOW`，之后不得再调用会重新插入 Z-order 的 `AppWindow.Show`；
3. 在同一 poster HWND 上安装 `WM_MOUSEACTIVATE` guard；guard 必须同步调用统一的 `ReassertDesktopBottom`，随后返回 `MA_NOACTIVATE`；
4. `MA_NOACTIVATE` 不能吞掉原鼠标消息，未来标题拖动、按钮与 Item 点击仍可在不激活整个窗口的情况下工作；
5. 指针守卫只做 no-show 的 resting-layer 修复；若 owner/generation 已失效，则排队进入既有恢复协调器，不能在消息回调中创建窗口；
6. 后续若引入文本输入，必须建立显式、可撤销的键盘激活 lease；不得为了普通指针操作永久移除 `WS_EX_NOACTIVATE`。
7. DeskBox 的 WinUI `Activated` 恢复点必须落成原生 `WM_ACTIVATE`/`WM_ACTIVATEAPP` 同步归位，不能只留在参考说明中。

## 4. V3 必须补强项

### 4.1 显式 DesktopShellGeneration

DeskBox 明确的 generation 主要用于显示拓扑；Explorer 恢复更接近“清缓存后再次发现”。V3 必须建立独立的 `DesktopShellGeneration`：

- stable snapshot 至少包含 DefView HWND、直接容器 HWND、Explorer PID、稳定时间和单调 generation；
- 同一有效身份连续采样只确认稳定，不增加 generation；
- `TaskbarCreated`、任一身份字段变化、旧 HWND 失效或 owner read-back 不一致都使当前 generation 失效；
- 每个 PosterWindowCluster 记录 `attachedGeneration`，只有它等于当前 stable generation 且 owner 读回一致时才算已挂载；
- DisplayTopologyGeneration 与 DesktopShellGeneration 分离，不能用一个计数器混淆两类恢复。

### 4.2 显式单航班重挂事务

Explorer generation 改变后，不等待未来交互或普通层级整理“顺便修复”，而是执行一次显式事务：

```text
invalidate old shell snapshot
→ wait for one stable new shell generation
→ capture the existing single PosterWindowCluster
→ freeze geometry persistence
→ reattach the same HWND set
→ read back owner/style/visibility and verify relative layer
→ commit attachedGeneration
→ unfreeze persistence
```

约束：

- 任一阶段看到更新 generation，当前结果立即作废，由新 generation 接管；
- 同一 generation 只能有一个有效提交，允许有界退避重试；
- 禁止创建第二张海报、第二套 input island 或第二个托盘；
- 禁止为了重挂销毁/重建窗口、调用 hide/show、激活窗口或临时 topmost；
- 失败时保持单实例和资源所有权可解释，不能把未验证 attach 记为成功。

### 4.3 原子显示重投影

- 先从一个稳定 `DisplayTopologySnapshot` 完整计算候选物理 bounds，再对窗口做一次 no-activate 提交；
- `WM_DPICHANGED` 的 suggested rect 只是信号/候选输入，不单独成为持久化权威；
- 提交后再次读取实际 bounds、DPI 和 work area 验证；拓扑再次变化时丢弃旧结果；
- 临时断屏、任务栏动画和设置面板中间态不得覆盖上一个稳定 profile；
- 首发只认证主屏单海报，但模型保留稳定显示器身份和未来多屏扩展字段，不宣称多屏已完成。

### 4.4 两类 generation 同时变化时

恢复协调器必须捕获一对版本：

```text
(DesktopShellGeneration, DisplayTopologyGeneration)
```

先等待 shell host 与显示拓扑各自稳定，再计算几何和提交 owner/bounds/style；提交前后任一 generation 变化都回滚本轮结果并重新排队。Explorer 恢复不应改写用户几何，显示重投影也不应绕过 owner 验证。

### 4.5 层级健康不能等同于 owner 健康

owner read-back、style 与 generation 一致只证明 attachment 身份正确，不证明窗口仍低于普通程序。V3 因此把防止抬层放在更早的原生激活边界：首次显示原子置底，`WM_MOUSEACTIVATE`、`WM_ACTIVATE` 或 `WM_ACTIVATEAPP` 到达时同步 reassert。低频健康探针不得周期性调用 Z-order API“抢回层级”，以免制造规律性闪烁。

R1 实机日志证明 Explorer 可以在 poster 没有收到 `WM_MOUSEACTIVATE`、owner/generation 也未失效时调整整个 DefView owner 组。DeskBox 的 Activated 恢复属于必要参考，但它仍是“激活后归位”。V3 R2 因而增加一项明确补强：poster 收到 `WM_WINDOWPOSCHANGING` 时，若该提交包含 Z-order 且目标不是 `HWND_BOTTOM`，就在 Windows 应用新层级之前将其约束为 `HWND_BOTTOM + SWP_NOACTIVATE`。纯移动/缩放的 `SWP_NOZORDER` 不修改。

此约束当前只服务于已冻结的主屏单海报静止层。未来若需要多个海报之间的明确排序，必须引入受控 Z-order lease，不能绕开本合同随意调用 `SetWindowPos`。

## 5. 诊断与验收合同

至少记录以下事件：

- `desktop_shell.invalidated`、`desktop_shell.stable`；
- `poster_reattach.started`、`poster_reattach.committed`、`poster_reattach.retry`、`poster_reattach.stale`；
- `display_topology.changed`、`display_topology.stable`；
- `poster_reproject.started`、`poster_reproject.committed`、`poster_reproject.stale`。
- `poster.pointer_activation_suppressed`，并记录 desktop-bottom 是否成功恢复。
- `poster.layer_activation_reasserted`，记录 `WM_ACTIVATE`/`WM_ACTIVATEAPP` 的同步归位结果；
- `poster.z_order_change_constrained`，记录被拒绝的原始 `hWndInsertAfter`。

V3-02-08/09 只有同时满足下列条件才可关闭：

- Explorer 重启后仍是原单海报集，自动恢复且没有第二个托盘/窗口集；
- 重挂期间没有白条、灰框、隐藏/恢复闪烁、抢前台或覆盖普通程序；
- 每个 shell generation 最多一个有效 reattach commit，旧 generation 不能覆盖新状态；
- 分辨率、DPI 或工作区变化后，海报在当前主屏工作区内，逻辑尺寸与锚点语义正确；
- 交互和恢复并发时不跳位、不写入暂态 bounds；
- 退出后所有 HWND、owner、watcher、timer 和恢复任务均从 resource ledger 清零。
- 冷启动时已有普通窗口也不得被首次 Show 覆盖；点击海报后当前普通前台窗口仍在海报之上。

## 6. DeskBox 源码锚点

- `src/DeskBox/Services/AppLifecycleRecoveryWatcher.cs`
- `src/DeskBox/Services/AppLifecycleRecoverySignalClassifier.cs`
- `src/DeskBox/Services/WidgetDisplayChangeWatcher.cs`
- `src/DeskBox/Services/DisplayAreaWatcherService.cs`
- `src/DeskBox/Services/DisplayTopologyTransitionCoordinator.cs`
- `src/DeskBox/Services/WidgetLayerService.cs`
- `src/DeskBox/Services/WidgetManager.cs`
- `src/DeskBox/Services/WidgetPositioningService.cs`
- `src/DeskBox/Services/WidgetTopologyLayoutService.cs`
- `src/DeskBox/Views/WidgetWindowBase.Bounds.cs`

来源 commit 与许可证见同目录 `DESKBOX_PROVENANCE_LEDGER.md`。

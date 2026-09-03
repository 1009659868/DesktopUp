# V3-02 验证记录

状态：`CLOSED — LAYER REVISION R2 AUTO PASS / USER MANUAL P0 PASS`

日期：2026-09-03

分支：`impl/poster-redesign-V3`

## 已实现边界

- 只枚举 Explorer 已存在的 `SHELLDLL_DefView`；
- 连续 5 次稳定采样，最大 48 次、间隔 250 ms；
- stable snapshot 携带 DefView、容器、Explorer PID、时间和单调 generation；
- 旧句柄失效立即清除，不从缓存继续返回；
- 诊断窗口保存原 owner/style，挂至 DefView 后读回验证；
- 移除系统标题栏/边框，应用 toolwindow、no-activate、non-topmost 和 owner 组 bottom；
- 失败立即恢复原 owner/style；退出 detach 后销毁窗口；
- 内容、bounds、边框策略和 owner 全部在隐藏态准备，最后以一次 native desktop-bottom/show 提交变为可见；
- `TaskbarCreated` 和 owner/host 健康探针触发 shell 失效与稳定重发现；
- shell 恢复复用同一 poster HWND，不执行 hide/show、激活或临时 topmost；
- 显示 bounds/work-area/DPI 使用独立稳定 topology generation；
- 420×240 DIP 按目标 DPI 投影至物理像素，并按归一化锚点夹紧到工作区；
- 信号合并、防抖、有界重试，提交前后校验最新请求及 `(shell, topology)` generation 对；
- 退出先停止 watcher/timer 和待处理恢复，再 detach/close。
- 隐藏态先准备完整内容和物理 bounds，最后以 `HWND_BOTTOM + SWP_SHOWWINDOW` 一次变为可见，后续不再调用 `AppWindow.Show`；
- `WM_MOUSEACTIVATE` 到达时同步执行 no-show desktop-bottom reassert，并返回 `MA_NOACTIVATE`；reassert 失败才进入异步 generation 恢复。
- `WM_ACTIVATE`/`WM_ACTIVATEAPP` 到达时同步执行 no-show desktop-bottom reassert，补齐 DeskBox 的 Activated 恢复点；
- `WM_WINDOWPOSCHANGING` 在系统提交前把未经授权的 Z-order 目标改写为 `HWND_BOTTOM + SWP_NOACTIVATE`，不等待海报抬高后再回落。

## 自动证据

### Release 构建

结果：PASS，0 warning / 0 error。

### 合同测试

结果：PASS，45/45。

新增覆盖：

- 连续相同 host 才稳定；缺失样本重置进度；
- 同一 stable host 不增加 generation；
- invalidate 后重新稳定增加 generation；
- stale handle 不从缓存返回；
- 等待次数有界；源码禁止 `0x052C`/`SendMessageTimeout`/WorkerW spawn；
- attach owner/style/read-back、幂等 detach、失败回滚和无效 host 不变更窗口。
- attachment 必须匹配当前 shell generation，旧 generation 不得继续被视为已挂载；
- display topology 需要连续稳定签名，同一签名不增加 generation，fresh native signature 可使旧快照失效；
- 100/125/150/200% 四档 DPI 的 DIP/物理像素换算与工作区夹紧；
- `TaskbarCreated`、display、DPI、work-area 消息分类；
- 恢复请求版本和双 generation 对拒绝迟到提交。
- 初始 attach 可携带原子 show flag，普通 reassert 不得带 show；
- pointer reassert 保持当前 generation，旧 generation 被拒绝；
- 源码合同禁止原子 bottom/show 后再次调用 `AppWindow.Show`，并要求 `WM_MOUSEACTIVATE`、`MA_NOACTIVATE` 和同步 reassert 同时存在。
- 激活切换必须同步归位；带 Z-order 的非 bottom `WINDOWPOS` 必须在提交前被约束，而纯几何 `SWP_NOZORDER` 不得受影响。

### 真实 Explorer/窗口 smoke

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\smoke-desktop-locator.ps1"
```

结果：`PASS (PID=42648, generation=1, poster=shown)`。

同一 PID 日志必须恰好包含一个 `desktop_shell.stable`、一个 `display_topology.stable`、成功的 `poster.attached`、一个 `poster.shown`、monitoring start/stop 和零 error；退出后 resource ledger 为 clean 且零资源。

## 首次失败及修订

首个实际窗口 smoke 以 `0xc000027b` 退出。WER 指向 WinUI/CoreMessaging，但加入边界结构日志后确认真实原因是 `DesktopWindowNativeApi.IsWindowNative` 漏写 `EntryPoint="IsWindow"`，导致 `EntryPointNotFoundException`。修订 P/Invoke 后，smoke 同时强制校验 attached/shown/no-error，避免仅凭进程退出码产生假通过。

### 层级人工门失败与 R1

用户在完整 P0 门中确认：桌面刷新后点击 poster 会持续覆盖普通程序，且重启后 poster 可能默认位于普通窗口前方。PID 23476 的结构日志只有初始 shell/topology stable、attach、show 和 control-center show，没有 `change_queued`、reattach、reproject 或 recovery commit，因此排除 V3-02-08/09 恢复任务主动抬层。

根因是此前只移植 owner attach、清除 topmost 和 `WS_EX_NOACTIVATE`，但首次 bottom 与 Show 分为两次提交，且没有吸收 DeskBox 的 native pointer activation guard。R1 已完成：

- 初始 bounds 在隐藏态准备；最后一次 `SetWindowPos` 同时使用 `HWND_BOTTOM`、`SWP_NOACTIVATE` 和 `SWP_SHOWWINDOW`；
- 删除随后独立的 `AppWindow.Show(false)`；
- poster subclass 拦截 `WM_MOUSEACTIVATE`，同步 no-show bottom reassert 后返回 `MA_NOACTIVATE`；
- reassert 校验 owner、shell generation、no-activate 和 non-topmost；失败才排队进入既有恢复协调器；
- DeskBox 对应来源和 V3 补强见 `DESKBOX_RECOVERY_REFERENCE_AND_V3_STRENGTHENING.md` 与 `DESKBOX_PROVENANCE_LEDGER.md`。

### R1 实机复测失败与 R2

R1 Release 在“桌面右键后任意点击”和“刷新”后仍会持续抬到普通窗口之上。对应 PID 644 日志没有 `poster.pointer_activation_suppressed`，也没有 `change_queued`、reattach、reproject 或 recovery 事件。这证明本轮不是 poster 自身收到 `WM_MOUSEACTIVATE`，也不是恢复任务主动抬层；触发源是 Explorer 激活/刷新对 DefView owner 组的外部 Z-order 调整。owner、style 和 generation 仍正确，所以旧健康探针不会发现该问题。

R2 在保留 R1 原子首次显示和 pointer guard 的基础上：

- 吸收 DeskBox 已登记但 R1 漏接的 Activated 恢复点，以 `WM_ACTIVATE`/`WM_ACTIVATEAPP` 同步执行 no-show desktop-bottom reassert；
- 增加 V3 实机补强：在 `WM_WINDOWPOSCHANGING` 中仅拦截携带 Z-order 且目标不是 `HWND_BOTTOM` 的提交，原位改写为 `HWND_BOTTOM + SWP_NOACTIVATE`；
- `SWP_NOZORDER` 的普通移动/尺寸提交保持原样，为 V3-03 拖缩留出正确边界；
- 回调内设置重入门，避免 reassert 自身的 `SetWindowPos` 递归修复；失败仍进入既有 generation 恢复协调器。

R2 自动证据已通过；真实 Explorer owner 组的相对层级仍须人工观察，不能由 45/45 或启动 smoke 推导为通过。

## 完整人工 P0 门

运行当前 Release 后验证：

1. 先打开资源管理器或其他普通窗口，再冷启动 DesktopUp；主屏工作区中央只出现一张青色 `DESKTOP LAYER PROBE`，且普通窗口始终在海报之上；
2. 海报没有系统标题栏、顶部白条、灰框或未绘制矩形；
3. 单击海报暴露区域及重复点击按钮区域，原前台普通窗口不被海报覆盖，鼠标交互不产生抬层闪烁；
4. 桌面右键后任意点击、点击刷新、打开 Windows 文件选择器，海报都不瞬时或持续覆盖普通/系统窗口；
5. 连续 Win+D 时海报不隐藏、恢复或闪烁；
6. 在任务管理器真实重启 Windows 资源管理器：海报自动恢复，只存在原来的一张海报和一个托盘图标，普通程序层级不改变；
7. 改变主屏分辨率、DPI 或任务栏工作区：稳定后海报仍在主屏工作区内，逻辑尺寸语义正确，不出现白条/灰框/第二张海报；
8. 托盘右键退出后海报、托盘和进程完全消失。

Explorer 重建必须使用真实系统操作进行人工验证；本轮没有发送合成 `TaskbarCreated` 消息。以上全部通过后才能关闭 V3-02 并进入 V3-03。

## 关闭结论

2026-09-03，用户确认上述 8 项完整人工 P0 门全部通过。V3-02 R2 因此正式关闭；本记录保留 R1/R2 的失败证据和根因链，不将后续 V3-03 输入行为反推为 V3-02 证据。

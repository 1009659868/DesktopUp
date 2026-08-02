# WorkerW 适配器、回退诊断与原生命中区域记录

日期：2026-08-02

状态：`C-DHF-02 COMPLETE WITH ACCEPTED FALLBACK / C-DHF-03 COMPLETE`

## 目标

- 把 WorkerW 发现和挂载限制在独立 Windows 适配层；
- WorkerW 不可用或不安全时自动恢复已有 `ToolWindow`，并把它插入 `Progman` 正上方、普通应用下方；
- 暴露真实挂载模式和回退原因；
- 让海报区域可命中，透明外边界从原生窗口区域中移除。

## API 约束

- `SetParent` 不自动维护 `WS_CHILD/WS_POPUP`，适配器显式切换窗口样式；
- 向 Explorer 发送请求使用 1000ms `SendMessageTimeoutW` 和 `SMTO_ABORTIFHUNG/SMTO_ERRORONEXIT`；
- 不把 `HTTRANSPARENT` 作为跨进程桌面穿透的唯一保证，因为它只保证同线程向下传递；
- 当前采用 `SetWindowRgn` 圆角交互区域，区域外不是窗口的一部分，后续可扩展为多海报区域合并。

参考：

- <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setparent>
- <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw>
- <https://learn.microsoft.com/windows/win32/inputdev/wm-nchittest>
- <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwineventhook>
- <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-showwindow>
- <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos>

## 实现

- 新增 `src-tauri/src/desktop_host.rs`；
- 枚举 `Progman/WorkerW/SHELLDLL_DefView`，只选择可见 WorkerW；
- 使用环境变量 `DESKTOP_UP_DISABLE_WORKERW=1` 提供确定性强制回退测试入口；
- 失败时恢复 `WS_POPUP + WS_EX_TOOLWINDOW`、解除父窗口，并通过 `GW_HWNDPREV` 锚定到 `Progman` 正上方；若 Shell 锚点缺失才最终使用 `HWND_BOTTOM`；
- 运行诊断进入 `RuntimeSnapshot`，托盘显示“WorkerW”或“ToolWindow 回退”；
- 海报无边框标题同步包含真实模式，方便黑盒探针；
- 海报窗体增加透明测试边界；JS 在尺寸变化和折叠后使用 WebView 的 `devicePixelRatio` 统一换算原生窗体物理尺寸与圆角区域，并读取不受入场动画影响的 `offsetLeft/offsetTop/offsetWidth/offsetHeight` 布局矩形；
- 最后一次 DOM 区域写入运行诊断和无边框标题，便于黑盒验证 DPI/合成偏移。

## 本机 Shell 结论

Explorer 层级只发现：

- 一个可见 `Progman`，其直接子窗口包含 `SHELLDLL_DefView`；
- 多个不可见 WorkerW；
- 发送有超时保护的请求后仍没有可见 WorkerW。

适配器拒绝把海报挂到不可见 WorkerW，正常策略自动进入 `ToolWindow Fallback`。这是可观察、安全且已由用户要求保留的回退，不注入 Explorer，也不修改桌面图标。

## Release 探针

正常策略最终进程：PID `65760`（仅用于本次记录），Release Demo 保持运行。

| 检查 | 强制 ToolWindow | 正常策略 |
| --- | --- | --- |
| 实际模式 | `ToolWindow Fallback` | `ToolWindow Fallback` |
| 控制中心冷启动 | 隐藏 | 隐藏 |
| 海报可见 | 是 | 是 |
| 父窗口 | Desktop | Desktop |
| ToolWindow Z 序 | `Progman` 正上方 | `Progman` 正上方 |
| 原生区域类型 | 圆角复杂区域 | 圆角复杂区域 |
| `(2,2)` 透明边界在区域内 | 否 | 否 |
| 窗口中心在区域内 | 是 | 是 |
| 透明边界命中海报 | 否 | 否 |
| 正常策略中心命中海报/WebView | — | 是 |

## 输入矩阵追加发现

- 当前前台 `Slay the Spire 2` 覆盖了海报原坐标，`WindowFromPoint` 正确命中游戏进程而不是海报，证明普通/全屏应用位于桌面海报上方；测试没有最小化或关闭用户游戏；
- 新回退 Z 序探针确认海报的下一窗口是 `Progman`，因此它已经位于桌面图标层上方、普通应用下方；
- 首轮自动点击坐标错误地按模型尺寸 `490×392` 计算；实际 WebView DOM 报告为 `R(13,20 400×319)`，现已改为 DOM 实测矩形驱动原生区域；
- DPI 感知后的探针确认区域包围盒为 `13,20,413,340`，拖动点和锁按钮点都命中 `Chrome_RenderWidgetHostHWND`；
- 系统级 `mouse_event` 注入仍未产生锁定、拖动或折叠状态变化，因此不能把它当作人工点击通过证据；锁按钮、折叠按钮、左键框选、右键菜单和桌面图标点击仍需真实用户输入验收；
- 校准截图保留为本机诊断证据；因背景包含第三方游戏画面与用户标识，不进入远程仓库。

## DPI 与完整显示修复

- 用户实机截图暴露出右侧和底部被裁切：Tauri 原生窗口采用 `490×392` 物理像素，而 WebView 在当前显示缩放下把它解释为更小的 CSS 视口；
- WebView 现上报实际 `devicePixelRatio=1.20`，Rust 以同一倍率把含 10px 四周边界的 `510×412` CSS 尺寸换算为 `612×494` 物理窗体；
- 窗体尺寸和 `SetWindowRgn` 不再分别依赖不同的 DPI 来源，避免视觉范围与命中范围再次偏离；
- 首次命中区域测量原本发生在 360ms 入场缩放动画期间，曾得到 `R(14,21 483×387)`；改用布局尺寸后最终稳定为 `R(10,10 490×392) @1.20x`；
- Release 黑盒探针确认窗口为 `612×494`、六个图标、右边界和底部状态栏完整；核验截图保留为本机诊断证据，因背景包含第三方游戏画面与用户标识，不进入远程仓库。

## Win+D 失败样本与恢复补丁

- 用户实测报告 Win+D 后海报不可见，因此 `C-DHF-04` 由计划状态进入进行中；
- 旧 Release 的事后探针显示海报仍为 `IsWindowVisible=true`、非最小化、`DWMWA_CLOAKED=0`，圆角窗口区域仍有效，说明不是状态丢失或区域清空；
- 原 ToolWindow 回退只在页面加载或手动“重新连接桌面宿主”时锚定，没有消费 Shell 切换到桌面的事件；
- Windows 适配层现使用 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` 监听 `Progman/WorkerW` 成为前台；事件经单容量通道合并并等待 140ms Shell 稳定后，再以 `SW_SHOWNOACTIVATE` 和 `SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS | SWP_SHOWWINDOW` 恢复显示及 Z 序；
- 恢复仅在 ToolWindow 回退生效且托盘状态要求海报可见时执行，不会把用户主动隐藏的海报重新弹出，也没有高频轮询；
- 自动化契约、Rust 检查和 Release 构建通过；包含最终空标题修复的当前 Release PID 为 `55216`。为避免擅自最小化用户游戏，Win+D 行为仍明确标记为等待用户人工复测。

## ToolWindow 标题栏修复

- 用户截图发现海报顶部出现 Windows 浅蓝标题栏，其中绘制了内部宿主诊断标题；该区域不是海报设计，而是 ToolWindow 回退保留的非客户区；
- WorkerW 子窗口和 ToolWindow 回退现在都显式清除 `WS_CAPTION`、`WS_THICKFRAME`、`WS_SYSMENU`、`WS_MINIMIZEBOX` 和 `WS_MAXIMIZEBOX`，并通过 `SWP_FRAMECHANGED` 让样式立即生效；
- 第一轮仍保留了诊断标题字符串；用户二次截图证明即使 `WS_CAPTION` 已关闭，DWM 仍可能把该文本合成到透明窗体顶部，因此不能继续依赖“标题存在但不可见”的假设；
- 最终修复把 `tauri.conf.json` 中的海报标题和运行时两处 `set_title` 全部改为空字符串，宿主模式、区域与 DPI 诊断继续保留在 `RuntimeSnapshot`、托盘和命令返回值中；
- 空标题版本的原生探针为 `TitleLength=0`、`Style=0x94000000`、`HasCaption=false`，但用户第三次截图仍发现顶部存在约 10px 的无文字亮带，因此空标题不能作为“原生非客户区完全消失”的最终证据；
- 第二阶段在 Windows 适配层显式设置 `DWMWA_NCRENDERING_POLICY=DWMNCRP_DISABLED`、`DWMWA_BORDER_COLOR=DWMWA_COLOR_NONE` 和 `DWMWA_WINDOW_CORNER_PREFERENCE=DWMWCP_DONOTROUND`，圆角继续完全由 `SetWindowRgn` 和 CSS 负责；
- 同时关闭透明海报 WebView 上的 `backdrop-filter`，避免无实际桌面模糊收益的边缘合成，并降低渲染成本；
- 当前修复版 Release PID `60216` 的只读探针结果为 `TitleLength=0`、`Style=0x94000000`、`HasCaption=false`、`DwmNc=0`。由于前台游戏覆盖桌面宿主，本轮没有擅自 Win+D 或最小化游戏，顶部亮带等待用户返回桌面后做最终视觉复测。

## C-DHF-03 人工验收结论

- 用户于 2026-08-02 明确确认“C-DHF-03 鼠标命中与穿透通过”；
- 该结论与已完成的原生圆角区域、透明边界不命中、海报中心命中和 DPI 区域探针共同构成通过证据；
- C-DHF-03 状态更新为 `COMPLETE — USER MANUAL MATRIX PASS`，后续工作转入 C-DHF-04。

## 自动化验证

```text
pnpm run check
  13 tests passed
  40 Markdown files verified

cargo check --locked --manifest-path src-tauri/Cargo.toml
  PASS

cargo tauri build --no-bundle
  PASS
```

## 结论与下一步

`C-DHF-02` 在本机结论为 `FAIL WITH ACCEPTED FALLBACK`：WorkerW 适配器保留，但当前 Shell 使用桌面 Z 序锚定的 `ToolWindow`。`C-DHF-03` 已由原生探针和用户人工矩阵共同通过；下一步进入 C-DHF-04 的原生位置持久化、Win+D、Explorer、显示器和 DPI 恢复。

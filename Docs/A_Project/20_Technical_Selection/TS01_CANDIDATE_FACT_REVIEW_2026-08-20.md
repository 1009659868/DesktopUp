# Desktop-Up V2 TS-01 候选事实审查

状态：`COMPLETE — FACTS FROZEN FOR TS-02`

审查日期：2026-08-20

## 1. 审查结论

TS-01 已完成静态事实审查，但没有选出 UI/渲染框架，也没有候选通过产品硬门。

本轮得到的首要结论不是“Qt、WinUI 或其他框架胜出”，而是：

1. Desktop-Up 的首个待证对象必须是独立的 Windows `Desktop Overlay Host`；
2. 目标层级固定为：`普通程序/系统对话框/全屏程序 > 海报 > 桌面图标 > 壁纸`；
3. Lively 等壁纸软件公开展示的“图标下方”挂载方式不能直接作为 Desktop-Up 的实现；
4. `WorkerW`、`Progman`、`SHELLDLL_DefView` 及 `0x052C` 没有被 Microsoft 公共文档定义为稳定 Desktop-Up 宿主契约，只能作为受观测的 Shell 拓扑；
5. 每个 UI 候选都能提供某种 Windows 原生句柄或原生适配点，但这不等于能够满足 Win+D、Explorer 重建、跨进程输入穿透和精确 z-order；
6. 因此 TS-02 必须先完成不含任何候选 UI 框架的原生宿主探针；宿主通过后，渲染候选才接入同一个 `DesktopHost` 边界。

事实卡完整的含义是“已记录已知事实、明确未知并映射到实验”，不是候选已经 `PASS`。

## 2. 事实等级

| 等级 | 含义 |
| --- | --- |
| F0 | 未确认或仅有假设 |
| F1 | 官方产品/支持文档可确认 |
| F2 | 官方 API、源码仓库或许可证可确认 |
| F3 | 本机命令、SDK 或运行时可确认 |
| F4 | 统一探针在冻结环境产生可复现证据 |

TS-01 只允许把 F1–F3 写成事实。桌面层级、Win+D、输入和渲染边界仍为 F0，必须在 TS-02 升级为 F4。

## 3. 本机冻结环境

| 项目 | 观测值 | 结论 |
| --- | --- | --- |
| 操作系统 | Windows x64，build `26100` | TS-02 首轮冻结为 Windows 11 24H2 系列 build 26100；产品名字符串可能显示旧营销名，不作为版本权威 |
| Windows SDK | `10.0.22621.0`、`10.0.26100.0` | 原生探针固定使用 `10.0.26100.0` |
| Visual Studio | Community 2026，`18.3.2`，VC 工具负载存在 | 可建立 x64 MSVC Release 探针；普通终端未预置开发环境 PATH |
| CMake | `4.4.2` | 可作为候选通用构建入口 |
| .NET | SDK `10.0.103`；runtime/WindowsDesktop `10.0.3`，另有 WindowsDesktop `8.0.24` | 可运行 .NET/WPF/Avalonia 基线，但若候选冻结到更新补丁需先显式安装并记录 |
| Rust | `1.97.1`，target `x86_64-pc-windows-msvc` | 可用于后续核心候选；不因此获得 UI/宿主优先级 |
| Qt | `qmake`、`qtpaths`、`ninja` 未在 PATH，未发现可用 Qt 工具链 | Qt 候选事实通过，当前机器尚不可执行固定版本原型 |
| UI 候选包 | Windows App SDK、Avalonia、WebView2 SDK、Slint 未在 V2 仓库固定 | TS-02 不允许使用“机器上碰巧存在”的包；进入渲染探针前单独锁版本与哈希 |

## 4. 公共 Windows 宿主事实

### 4.1 已确认事实

- Win32 子窗口受父窗口客户区约束，子窗口有自己的兄弟 z-order；普通顶层窗口、topmost 窗口和子窗口的顺序规则不同。[Window Features](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- `SetParent` 不会自动修正 `WS_CHILD`/`WS_POPUP`，而跨进程 DPI awareness 不匹配可能触发强制 DPI 重置或失败；探针必须在创建任何 HWND 前声明 Per-Monitor v2，并记录双方 DPI context。[SetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)
- DWM 可以报告窗口是否被 cloak，并提供 extended frame bounds；这些状态必须进入连续遥测，而不能只肉眼判断。[DWM window attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
- Direct2D 是 Windows 自带的原生 2D API，支持硬件加速和软件回退；Direct2D 设备相关资源在 device loss 后必须重建。[Direct2D overview](https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-overview)
- DirectComposition/Windows Composition 只解决视觉合成，不定义 Explorer 桌面层级、Win+D 或空白输入穿透。[DirectComposition architecture](https://learn.microsoft.com/en-us/windows/win32/directcomp/architecture-and-components)
- `HTTRANSPARENT` 的继续命中语义受线程边界约束；Desktop-Up 与 Explorer 跨进程，不能把返回该值当作空白区域必然落到桌面图标的证明。[WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)

### 4.2 Lively 源码能证明与不能证明的内容

Lively 当前公开源码会识别 raised desktop，发送未公开的 `0x052C` Shell 消息，并分别处理 `Progman`/`WorkerW`、Explorer 重启和显示变化。其 raised desktop 路径把壁纸安排在 `SHELLDLL_DefView` 下方，这与 Desktop-Up 的“海报高于图标”要求相反。[Lively WinDesktopCore.cs](https://github.com/rocksdanister/lively/blob/core-separation/src/Lively/Lively/Core/WinDesktopCore.cs)

所以该源码只证明以下工程经验值得借鉴：

- 桌面拓扑必须运行时发现；
- Explorer 会重建相关窗口；
- 需要监听任务栏/Explorer、显示器和会话变化；
- 挂载失败必须有恢复和 degraded 状态。

它不能证明 Desktop-Up 的目标 z-order、Win+D 连续可见或跨进程空白命中已经可行。

### 4.3 必须由 TS-02 回答的 Shell 未知项

| 未知项 | 对应硬门 |
| --- | --- |
| raised desktop 下，作为 `Progman` 子窗口并排在 `SHELLDLL_DefView` 上方，是否能稳定满足目标层级 | HG-Z-01/02/03/07 |
| traditional desktop 下，能否在图标宿主 `WorkerW` 内安全获得 `SHELLDLL_DefView` 上方的位置 | HG-Z-01/03/07 |
| Win+D 三种入口是否完全不触发 hide/minimize/cloak/detach/re-attach 可见帧 | HG-Z-03/04/05 |
| 海报覆盖图标时，空白能否在不注入 Explorer 的前提下到达桌面与图标 | HG-IN-01/02/03 |
| 交互岛与空白区域能否原子切换，且不依赖整窗 `WS_EX_TRANSPARENT` | HG-IN-04/05 |
| Explorer/WorkerW 重建后能否恢复唯一海报，不临时 TopMost、不覆盖普通程序 | HG-Z-02/07 |

## 5. 海报呈现候选事实卡

### TS-C01 — Qt 6 / Qt Quick + 原生宿主适配

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | Qt `6.8.3`，2025-03-27；6.11.2 安装失败后于 2026-08-24 受控修订，实际 SDK 为 MSVC 2022 x64 |
| 平台/工具链 | 官方支持 Windows 11 x64 与 MSVC 2022；本机未安装 Qt |
| 运行/部署 | 需要 Qt DLL、平台插件及使用模块；官方提供 `windeployqt`，仍需生成依赖与许可证清单 |
| 许可证 | 商业、LGPLv3、GPLv3 并存；部分模块可能仅 GPL。自用不取消依赖归属与再分发审查 |
| 窗口/渲染 | Qt Quick scene graph 在 Windows 通常使用图形后端；`QWindow::fromWinId` 的 foreign-window 操作是平台相关且能力有限，不能取代宿主适配层 |
| 输入/透明 | Qt 能表达透明无框窗口和自定义命中，但 Explorer 跨进程穿透及桌面层级仍未知 |
| 已知限制 | Qt 自身不提供 WorkerW/Progman 稳定契约；foreign window 行为不应作为跨版本保证 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / P0 PROBE REQUIRED` |

来源：[Qt 6.8.3 release](https://www.qt.io/blog/qt-6.8.3-released)、[Qt 6.8 supported platforms](https://doc.qt.io/qt-6.8/supported-platforms.html)、[6.8.3 MSVC 2022 x64 package](https://download.qt.io/online/qtsdkrepository/windows_x86/desktop/qt6_683/qt6_683/qt.qt6.683.win64_msvc2022_64/)、[Licensing](https://doc.qt.io/qt-6/licensing.html)、[Windows deployment](https://doc.qt.io/qt-6/windows-deployment.html)、[QWindow](https://doc.qt.io/qt-6/qwindow.html)。

### TS-C02 — Win32 + Direct2D/DirectComposition

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | Windows SDK `10.0.26100.0`；x64 MSVC Release |
| 平台/工具链 | Windows 11 x64；本机已具备 Windows SDK、VC 工具负载与 CMake |
| 运行/部署 | User32、DWM、Direct2D、DirectWrite、WIC、DXGI/D3D、DirectComposition 均来自 Windows；仍需交付 VC runtime 或采用经审查的静态/应用本地策略 |
| 许可证 | 使用 Windows SDK/Visual Studio 再分发条款；没有单独第三方 UI runtime |
| 窗口/渲染 | 能直接控制 HWND、style、parent/owner、z-order、region、消息和 DWM/渲染遥测；绘制层本身不提供 Shell 层级保证 |
| 输入/透明 | 可精确实验窗口 region、分离交互岛、layered/composition 和命中路径；跨进程桌面穿透仍需实测 |
| 已知限制 | 主题、布局、无障碍和高层控件需要自建或组合；device loss、DPI、文字和资源生命周期由项目负责 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / FIRST P0 HOST PROBE` |

来源：[Direct2D overview](https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-overview)、[Direct2D API](https://learn.microsoft.com/en-us/windows/win32/direct2d/the-direct2d-api)、[DirectComposition architecture](https://learn.microsoft.com/en-us/windows/win32/directcomp/architecture-and-components)。

### TS-C03 — WinUI 3 / Windows App SDK + 原生适配

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | Windows App SDK `2.3.1`，2026-07-16 |
| 平台/工具链 | Windows 11 x64；C#/.NET 或 C++；本机未在 V2 固定对应 NuGet/runtime |
| 运行/部署 | 默认 framework-dependent，也可 self-contained；未打包应用需要正确初始化/交付 Windows App Runtime；部分功能仍依赖额外包 |
| 许可证 | Windows App SDK 源码 MIT；具体 NuGet、运行时和第三方 notices 仍随产物审计 |
| 窗口/渲染 | Desktop WinUI Window 对应 HWND，可调用 Win32；XAML/Composition 能承载视觉，但 Shell 宿主必须由原生边界控制 |
| 输入/透明 | WinUI 3 XAML 岛背景可透明；这不证明宿主窗口边界、空白命中或 Win+D 行为 |
| 已知限制 | 自包含会增加产物；框架依赖模式增加净机/runtime 前置条件；UI 线程和窗口模型需实测子窗口重挂载 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / P0 PROBE REQUIRED` |

来源：[Windows App SDK downloads](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)、[Win32 interop](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/desktop-winui3-app-with-basic-interop)、[Self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)、[WindowsAppSDK repository](https://github.com/microsoft/WindowsAppSDK)。

### TS-C04 — WPF + 原生宿主适配

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | .NET 10 / WPF；正式原型锁定到当时最新 .NET 10 servicing patch，本机当前 runtime `10.0.3` |
| 平台/工具链 | Windows-only，`Microsoft.NET.Sdk.WindowsDesktop`；本机具备 .NET 10 SDK/WindowsDesktop runtime |
| 运行/部署 | 可 framework-dependent 或 .NET self-contained；需记录单文件、ReadyToRun、体积和 native 依赖策略 |
| 许可证 | .NET/WPF 仓库 MIT，仍需生成第三方 notices |
| 窗口/渲染 | WPF Window 创建顶层 HWND；`HwndSource` 可把 WPF 内容放入 Win32 父窗口并暴露消息 hook、style、parent 和 HWND |
| 输入/透明 | WPF 支持 per-pixel transparent top-level window；`UsesPerPixelOpacity` 等关键选项需在创建时确定，并存在 HWND airspace/互操作约束 |
| 已知限制 | 透明顶层窗口成功不等于 Explorer 子宿主成功；WPF 与其他 HWND/DirectX 内容混合存在 airspace 限制 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / P0 PROBE REQUIRED` |

来源：[WPF and Win32 interop](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation)、[WPF windows](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/)、[WPF repository/license](https://github.com/dotnet/wpf)。

### TS-C05 — Avalonia + 原生宿主适配

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | Avalonia `12.1.0`，2026-07-08 |
| 平台/工具链 | Windows 11 24H2 build 26100 x64 为 Tier 1；桌面端要求 .NET 8+；本机具备兼容 .NET，但包未固定 |
| 运行/部署 | Avalonia assemblies、Skia/平台依赖与选定 .NET 部署模式；需净机测量体积与 native 文件 |
| 许可证 | 核心 MIT；商业附加产品不默认进入候选 |
| 窗口/渲染 | Windows 后端直接使用 Win32，渲染为 Skia/D3D 并有软件回退；可取得 HWND |
| 输入/透明 | Windows 支持透明窗口；官方明确普通 Avalonia API 不提供 WPF 式透明 click-through，需要 native interop |
| 已知限制 | `NativeControlHost` 有 airspace、透明和变换限制；宿主层与交互岛必须单独验证 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / P0 PROBE REQUIRED` |

来源：[Avalonia 12.1](https://avaloniaui.net/blog/release-12-1)、[Supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)、[Windows backend](https://docs.avaloniaui.net/docs/platform-specific-guides/windows)、[License](https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md)。

### TS-C06 — WebView2 + 原生宿主适配

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | Release SDK `1.0.4129.50`，Runtime 151，2026-08-03 |
| 平台/工具链 | Windows 10/11；C++、.NET/WinRT 等宿主；SDK/runtime 未在 V2 固定 |
| 运行/部署 | Evergreen runtime 可共享和自动更新；Fixed Version 体积较大。应用必须检查 runtime 并明确版本/更新策略 |
| 许可证 | SDK NuGet 与 WebView2 Runtime 使用 Microsoft 分发条款；需随固定模式记录许可和 notices |
| 窗口/渲染 | composition controller 可连接 DirectComposition/Windows Composition visual tree；空间输入由宿主转发 |
| 输入/透明 | 支持完全透明背景，但仅 0/255 alpha；官方记录默认白色以及背景初始化不当造成白闪的已知问题 |
| 已知限制 | Chromium 多进程、runtime 更新、首帧/导航背景、输入转发和渲染故障面与历史顶部条带风险直接相关 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / P0 PROBE REQUIRED / HISTORICAL RISK` |

来源：[SDK release notes](https://learn.microsoft.com/en-us/microsoft-edge/webview2/release-notes/)、[API overview](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/overview-features-apis)、[Runtime distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)、[Controller background](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2controller2)。

### TS-C07 — Slint + 原生宿主适配

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | Slint `1.17.1`，2026-07-07 |
| 平台/工具链 | Windows 10/11 x64；Rust 或 C++；本机 Rust/MSVC/CMake 可用，Slint SDK/crate 未固定 |
| 运行/部署 | 依所选语言和 renderer 交付；winit 为常用后端，可选 FemtoVG、Skia、software 等 renderer |
| 许可证 | GPLv3、商业许可及带条件的 royalty-free desktop/mobile/web 许可并存；必须在分发前固定许可路径和披露义务 |
| 窗口/渲染 | 支持自定义 platform backend，理论上可与原生宿主分层；实际 HWND/child/composition 行为需要原型 |
| 输入/透明 | 声明式 UI 和 renderer 能力不等于 Explorer 空白穿透；需由原生 adapter 提供命中契约 |
| 已知限制 | 后端/renderer 组合较多，必须先冻结唯一组合，避免用不同后端规避统一矩阵 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / P0 PROBE REQUIRED` |

来源：[Slint 1.17](https://slint.dev/blog/slint-1.17-released)、[Platforms](https://docs.slint.dev/latest/docs/slint/guide/platforms/desktop/)、[Backends/renderers](https://docs.slint.dev/latest/docs/slint/guide/backends-and-renderers/backends_and_renderers/)、[License](https://github.com/slint-ui/slint/blob/master/LICENSE.md)。

### TS-C08 — 原生海报 + 独立高层控制中心

| 字段 | 冻结事实 |
| --- | --- |
| 被审查版本 | 组合候选，不独立产生框架版本；继承所选原生海报与控制中心候选的固定版本 |
| 运行/部署 | 至少两个 UI/运行时边界时，必须合并计算包体、升级、日志、崩溃、IPC 和状态投影成本 |
| 窗口/渲染 | 允许海报优先满足 P0，控制中心保持普通应用窗口；不允许控制中心改变真实海报层级 |
| 输入/状态 | 必须有 revision/CAS 与唯一权威状态，预览只投影，不可创建第二真实海报角色 |
| 已知限制 | 双栈可能改善故障隔离，也可能增加资源、维护和状态分叉；TS-01 无法静态判定净收益 |
| TS-01 状态 | `FACT CHECKED / STATIC SCREEN PASS / COMPOSITION AFTER P0` |

## 6. 支撑域事实边界

TS-D03 至 TS-D09 在 TS-03 才进入组合审查。本轮只冻结三条约束：

1. 海报宿主通过 P0 前，不利用控制中心技术反向决定海报技术；
2. 原生探针只使用最少 Windows API，不引入状态存储、主题、AI、内容排名、安装器或业务 Item；
3. Qt/WinUI/WPF/Avalonia/WebView2/Slint 的控制中心适用性不能替代海报 P0 结果。

Wallpaper Engine 和商业“小智桌面”公开材料可以作为内容平台、规则、主题、生命周期与产品行为参考，但没有公开、可审计的 Desktop-Up 目标层级实现，因此不构成 TS-02 宿主通过证据。

## 7. TS-01 状态结果

| Candidate | Fact card | Static screen | TS-02 入口 |
| --- | --- | --- | --- |
| TS-C01 | COMPLETE / 6.8.3 CONTROLLED REVISION | PASS | R0 PASS：Release build/deploy/QML self-check；P0 NOT RUN |
| TS-C02 | COMPLETE | PASS | 先承担无框架公共宿主探针 |
| TS-C03 | COMPLETE | PASS | 等待公共宿主通过后接入 |
| TS-C04 | COMPLETE | PASS | 等待公共宿主通过后接入 |
| TS-C05 | COMPLETE | PASS | 等待公共宿主通过后接入 |
| TS-C06 | COMPLETE | PASS WITH HISTORICAL RISK | 等待公共宿主通过后接入 |
| TS-C07 | COMPLETE | PASS | 等待公共宿主通过后接入 |
| TS-C08 | COMPLETE | PASS AS COMPOSITION | 海报候选出现 P0 PASS 后再组合 |

静态筛选没有淘汰候选，也没有产生排名。TS-02 的第一个产物是公共宿主证据，不是八套同时开工的 UI 原型。

## 8. TS-02 交接

唯一下一入口为 [Desktop Overlay Host 探针章程](DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md)。

在该探针 `PASS` 前：

- 不宣布 Qt、WinUI、WPF、Avalonia、WebView2 或 Slint 胜出；
- 不建立生产架构；
- 不把 Lively 的图标下方路径改名为“已满足”；
- 不使用 hide/show、临时 TopMost、激活窗口或 Win+D 后重挂作为通过方案；
- 不把一次截图或肉眼“看起来正常”当成 Shell 契约证据。

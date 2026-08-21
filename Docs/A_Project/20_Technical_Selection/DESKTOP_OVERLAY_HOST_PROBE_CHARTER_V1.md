# Desktop-Up V2 Desktop Overlay Host 探针章程 V1

状态：`TS-H01-E COMPLETE WITH RECORDED D EXCEPTIONS / CANDIDATE ADAPTERS NEXT`

建立日期：2026-08-20

## 1. 目的

本探针只回答一个基础问题：在不修改或注入 Explorer 的前提下，Desktop-Up 是否能建立一个稳定 Windows 桌面宿主，使单张海报同时满足：

```text
普通程序 / 控制中心 / 系统对话框 / 全屏程序
                         ↓ 高于
                    Desktop-Up 海报
                         ↓ 高于
                     Windows 桌面图标
                         ↓ 高于
                          壁纸
```

并且三种“显示桌面”、Explorer 重建、桌面输入和 DPI/尺寸变化都不破坏该关系。

本探针是所有渲染框架的公共前置条件 `TS-H01`，不是 TS-C02 已经获选，也不是生产架构。

## 2. 探针边界

### 2.1 必须包含

- x64 原生 Win32 Release 可执行文件；
- Windows SDK `10.0.26100.0`；
- 一张矩形海报夹具，含半透明背景、圆角可见边界、标题区、一个 Item 按钮和明确空白区；
- Per-Monitor v2 DPI awareness 在创建任何 HWND 前生效；
- Shell 拓扑发现、挂载、z-order、输入、Win+D 和 Explorer 重建遥测；
- 单调时钟事件日志与录屏对齐；
- 正常退出时完整清理自有窗口、hook 和资源。

### 2.2 明确不包含

- Qt、WinUI、WPF、Avalonia、WebView2、Slint；
- Night Ops 完整视觉、主题系统、AI、内容排名、SQLite、IPC、安装器；
- 多海报、多显示器和混合 DPI 发布门；
- Explorer 注入、子类化 Explorer 窗口、修改图标坐标或替换 `SHELLDLL_DefView`；
- 通过隐藏/显示、最小化恢复、临时 TopMost、前台激活或每帧强制 z-order 修复闪烁。

## 3. 非协商层级合同

1. 海报不得成为 `WS_EX_TOPMOST`。
2. 海报不得覆盖资源管理器、浏览器、文件选择器、控制中心或普通/全屏程序，哪怕只有一帧。
3. 海报必须在桌面图标上方；图标被海报内容视觉遮挡是预期，但海报空白必须允许原生图标操作。
4. 壁纸始终在最下方。
5. Win+D 期间海报必须连续可见，不能出现 hide、minimize、cloak、detach 或 re-attach 的可见帧。
6. Explorer 重建时若无法安全恢复，进入明确 `DEGRADED_NO_DESKTOP_HOST` 并隐藏自有表面；禁止退化为普通顶层或 TopMost 窗口。

## 4. Shell 拓扑发现

探针每次挂载前重新枚举并记录：

- `Progman`；
- 所有 `WorkerW`；
- `SHELLDLL_DefView` 及其直接父窗口；
- `SysListView32`/`FolderView` 等可见图标视图（只观察）；
- 每个相关窗口的 process/thread ID、class、parent、owner、rect、styles、DPI、visible/minimized/cloaked 状态；
- 目标父窗口内部自上而下的兄弟 z-order；
- taskbar/Explorer 进程世代和重建时间。

类名只能用于发现和证据，不能单独证明角色。角色至少由父子关系、进程、可见状态和图标视图关联共同确认。

## 5. 待证宿主假设

以下只是实验假设。任何一条失败都必须保留原始证据，不允许用另一条的成功倒推它也正确。

### H-RAISED

当 `SHELLDLL_DefView` 位于 raised `Progman` 拓扑中时：

- 创建自有子宿主并挂入相同桌面父窗口；
- 将自有宿主排在 `SHELLDLL_DefView` 上方，但仍受桌面顶层父窗口约束；
- 验证普通顶层窗口天然在整个桌面父树上方；
- 验证 Win+D 不改变该父子和兄弟顺序。

### H-TRADITIONAL

当 `SHELLDLL_DefView` 位于 traditional `WorkerW` 图标宿主中时：

- 尝试在同一安全桌面父窗口内把自有子宿主排到 `SHELLDLL_DefView` 上方；
- 不把其他“壁纸 WorkerW”误认作图标宿主；
- 若不存在不覆盖普通程序且高于图标的稳定位置，则本拓扑结果为 `FAIL/UNKNOWN`，不能回退到 TopMost。

### H-NO-SAFE-HOST

如果当前 Explorer build 不暴露可满足合同的安全父树：

- 探针报告 `DEGRADED_NO_DESKTOP_HOST`；
- 不创建普通顶层假海报；
- 不发送无限 `0x052C`；
- 不修改 Explorer；
- 该 OS/build 的宿主硬门判定为 FAIL，而不是“稍后可修”。

## 6. 输入模型实验

海报位于图标上方意味着“视觉顺序”和“输入顺序”必须分别证明。

探针至少比较以下有界策略：

1. 单 HWND + 动态 window region，只让标题、Item、按钮等交互岛进入窗口区域；
2. 背景视觉表面与交互岛窗口分离，视觉层不接管输入；
3. layered/composition alpha hit-test 的官方行为，记录是否能跨 Explorer 线程落到图标；
4. `WM_NCHITTEST/HTTRANSPARENT` 仅作为对照，不预设其能跨进程把输入交给 Explorer；
5. `WS_EX_TRANSPARENT` 只按官方定义评估，不能把绘制顺序属性误写成通用输入穿透。

通过条件：

- 空白左键和拖动产生 Windows 原生桌面框选；
- 空白右键产生 Windows 桌面菜单；
- 海报空白覆盖下的快捷方式可以单击选中、双击启动；
- Item/标题/按钮仍由海报命中；
- USING/EDITING 切换后命中区原子更新，连续操作不失效；
- 不读取或修改 Explorer 图标坐标来伪造命中。

若单个覆盖全矩形的 HWND 无法满足，优先判定该形态失败，再评估分离视觉/交互岛；不能通过转发合成的鼠标消息冒充原生桌面输入。

## 7. Win+D 与生命周期实验

每条入口分别执行 10 轮：

1. 键盘 `Win+D`；
2. 任务栏“显示桌面”；
3. 触控板显示桌面手势（当前设备不可用则保持 UNKNOWN）。

每轮记录前后至少 500 ms 高频窗口快照，并以事件驱动日志覆盖：

- `IsWindowVisible`；
- `IsIconic`；
- `DWMWA_CLOAKED`；
- parent/owner；
- 前后相邻 z-order；
- `WS_EX_TOPMOST`；
- 窗口矩形、客户区、DPI；
- Explorer process generation；
- attach/detach/recreate 次数与原因；
- 首帧和每次 commit 时间。

通过不是“最后又出现了”，而是录屏和遥测均证明没有消失帧。

Explorer 重启另执行 10 轮：旧句柄失效后最多产生一个新自有宿主；在新安全宿主确认前保持 degraded，不把海报抬为普通顶层窗口。

## 8. 尺寸、DPI 与绘制实验

首轮主屏验证：

- 100%、125%、150%、200% DPI；
- 分辨率/工作区改变；
- 主屏中部连续拖动 20 次；
- 最小、默认、最大尺寸连续缩放 20 次；
- 收起/展开 20 次；
- 冷启动首次显示 20 次。

必须区分并记录：逻辑海报矩形、宿主 HWND rect、client rect、DWM extended frame bounds、渲染目标像素尺寸。任何顶部白条、灰框、非客户区、边框残影、裁切或一帧错位均为失败。

## 9. 诊断输出合同

每个采样至少输出：

```text
monotonic_timestamp
probe_build_hash / os_build
explorer_pid / explorer_generation
topology_id / host_state / recovery_reason
self_hwnd / parent / owner / prev_z / next_z
styles / ex_styles / visible / iconic / cloaked / active / topmost
window_rect / client_rect / extended_frame_bounds / dpi
hit_region_revision / interaction_mode / last_hit_result
render_generation / first_frame / commit_count / device_loss
```

事件日志不得只记录最终状态。视频、截图、窗口树快照和日志文件使用同一个 Evidence ID。

## 10. 阶段门

### Gate H0 — 构建与观测

- x64 Release 可在无 IDE 环境启动；
- HG-BLD-01/02 与 HG-DIAG-01 所需字段齐全；
- 录屏能与单调时间戳对齐。

### Gate H1 — 精确层级

- HG-Z-01/02/08 全部 PASS；
- 任何一帧覆盖普通程序、掉到图标下方或使用 TopMost 均失败。

### Gate H2 — 输入矩阵

- HG-IN-01 至 HG-IN-05 全部 PASS；
- 桌面框选、菜单与快捷方式必须是 Explorer 原生行为。

### Gate H3 — 显示桌面

- HG-Z-03/04 PASS；
- HG-Z-05 有设备则 PASS，无设备保持 UNKNOWN 并不阻断当前单机开发，但不得宣称完整发布通过。

### Gate H4 — Explorer 重建与退化

- HG-Z-07 PASS；
- 恢复过程无普通顶层/TopMost 闪现、无重复海报。

### Gate H5 — 边界/DPI

- HG-RND-01 至 HG-RND-05、HG-GEO-04 在首轮单屏范围 PASS。

只有 H0–H5 达到规定状态，`TS-H01` 才能标记 `PASS`。

## 11. 失败规则

以下任一出现即停止向 UI 候选扩散：

- 只能靠 TopMost 或激活窗口保持层级；
- Win+D 必须先隐藏再恢复或先脱锚再重挂；
- 海报空白不能原生操作桌面和图标；
- Explorer 重建后只能退化为普通顶层窗口；
- 顶部白条、灰框或非客户区只能靠固定像素补偿；
- 结果依赖固定 HWND、固定枚举序号或只适用于一次 Explorer 实例；
- 探针无法观测 cloak、parent、z-order、DPI、hit region 或恢复原因。

失败后先修正宿主假设或认定当前 build 不可行；不得先接入 Qt/WinUI 来掩盖宿主失败。

## 12. 通过后的公共接口输入

探针通过后，只冻结实现中立的 `DesktopHost` 能力，不冻结内部 HWND 技巧：

```text
DiscoverTopology()
Attach(surface_handle, geometry, hit_regions)
UpdateGeometry(logical_geometry, anchor)
UpdateHitRegions(revision, regions)
SetInteractionMode(USING | EDITING, locked)
ObserveHostState()
Recover(shell_generation)
Detach()
```

每个渲染候选只能把自己的 surface/visual 接入该边界，再执行同一 Night Ops 与 P0 矩阵。候选不得绕过 `DesktopHost` 自行切换 TopMost、parent、z-order 或 Win+D 恢复逻辑。

## 13. TS-02 顺序

```text
TS-H01-A  原生窗口树与诊断探针
→ TS-H01-B raised/traditional 宿主与层级
→ TS-H01-C 空白穿透与交互岛
→ TS-H01-D Win+D/Explorer/DPI/边界完整门
→ 冻结 DesktopHost 原型接口
→ TS-C01..C07 渲染适配器同构接入
→ TS-C08 组合候选评估
```

`TS-H01-A` 已以 Evidence `TS-H01-A-20260820-01` 通过只读观测门。`TS-H01-B-20260820-01` 只证明 sibling HWND 自动结构；Evidence `TS-H01-B-20260820-02` 证明 ordinary GDI child 没有进入最终桌面合成，该视觉分支 FAIL；Evidence `TS-H01-B/2026-08-20_03_layered-visual-pass` 则证明 Windows 8-aware layered child 在当前 `RAISED_PROGMAN` 拓扑下达到 80/80 自动结构 PASS，并由用户确认四项人工视觉门全部通过。

TS-H01-B、TS-H01-C、TS-H01-D1 与 TS-H01-D3-3 已在当前 `RAISED_PROGMAN` layered/split-input 路径达到自动与人工 PASS。用户接受 D2、D3-1 和 D3-2 的已登记覆盖例外并要求继续，因此 TS-H01-D 以 `USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS` 收敛；strict H4/H5 仍不能写成完整自动 PASS。traditional WorkerW、多屏/混合 DPI 和未测 DPI 档继续为 UNKNOWN/DEFERRED。TS-H01-E 已完成 DesktopHost 数据、生命周期、原子投影、诊断夹具和候选交接冻结；下一步是 TS-C01 至 TS-C07 独立 adapter/P0，仍没有 UI 框架赢家。

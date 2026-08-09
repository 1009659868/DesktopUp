# C-DRP-02 单宿主拓扑与响应缩放实施

日期：2026-08-09

状态：`IMPLEMENTED / PHYSICAL MATRIX PENDING`

## 目标

把 A0 的单海报窗口收敛为可枚举的唯一生产桌面宿主，并让显示器工作区、DPI、WebView2 栅格比例和分辨率响应缩放进入同一原生几何写入路径。当前只关闭主显示器单海报闭环；混合 DPI 跨屏仍需真实物理矩阵。

## 实现

### 单全局宿主

- 新增 `host_topology.rs`，冻结生产宿主标签 `poster`、控制中心标签 `main` 和 `EXPECTED_DESKTOP_HOST_COUNT = 1`；
- 所有生产宿主窗口查找统一通过 `desktop_host_window` / `optional_desktop_host_window`，不再在业务路径分散使用字符串查找；
- RuntimeSnapshot 与 HostSurfaceSyncResult 新增 `hostTopology`，暴露宿主数量、控制中心数量、WebView 总数和 `invariantSatisfied`；
- Tauri 配置测试确认只有一个透明 `poster.html` 宿主；当前单海报在该宿主内渲染，后继多表面登记仍归 C-DRP-03。

### 显示拓扑与响应比例

- DisplayDescriptor 新增 `rendererScaleFactor`、`effectiveLogicalWorkArea` 和 `responsiveScale`；DisplayTopology 新增版本化 `scalePolicy`；
- 响应策略冻结为：`raw = min(logicalWorkWidth / 1920, logicalWorkHeight / 1080)`，`raw` 在 1.0 ± 0.05 内吸附为 1.0，最终限制在 `0.80–1.25` 并保留三位小数；
- 1366×768、1080p、2K、4K 自动化结果分别为 `0.80 / 1.00 / 1.25 / 1.25`；4K@200% 与 1080p@100%、4K@150% 与 2K@100% 得到等价响应比例；
- 真实 Release 发现本机 `monitorScaleFactor = 1.0`，但 WebView2 `devicePixelRatio = 1.2`。因此契约明确区分 monitor scale 与 renderer scale：renderer scale 用于 CSS/物理像素映射，monitor scale 保留为原生显示诊断；
- 目标显示器的 effective logical work area 使用 renderer scale 计算；最终 `effectiveScale = rendererScale × responsiveScale × userScale`，其中当前 `userScale = 1.0`；
- HostSurfaceSnapshot 同时公开 targetDisplayId、monitor/device/renderer/responsive/user/effective scale，避免再用一个模糊比例解释全部坐标。

### 原生宿主与前端呈现

- `sync_host_surface` 根据 anchor intent、当前显示器和持久位置选择目标 DisplayDescriptor，再以同一 effective scale 计算宿主物理尺寸和原生命中区域；
- interaction region 已修正为使用 effective scale，而非只用前端 DPI；该偏差由第一轮 Win32 探针发现并形成静态架构回归测试；
- 前端新增 `host-presentation.js`，CSS 表面保持设计逻辑尺寸，通过独立 `responsiveScale × userScale` 等比变换填满 WebView viewport，不改写海报持久宽高或触发图标重排；
- ScaleFactorChanged 会用最新 HostSurface 重新提交 preserve 请求，统一重算目标显示器、宿主尺寸和命中区域；
- 新增 `scripts/probe-poster-cdp.mjs`，测试进程临时启用本机 CDP 时可读取 DOM/RuntimeSnapshot，并通过真实按钮事件验证展开/收起往返。普通 Release 不启用调试端口。

## 自动化验证

- `pnpm run check`：44 项 Node/契约/静态架构测试通过，90 个 Markdown 链接在本日志登记前通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：31 项 Rust 测试通过；
- `cargo fmt --manifest-path src-tauri/Cargo.toml --check`：通过；
- `pnpm run build`：通过；
- `cargo tauri build --no-bundle`：Release 构建通过；
- 响应矩阵覆盖 1366×768、1920×1080、2560×1440、3840×2160、100%/150%/200% 等效组合、任务栏基线吸附、monitor/renderer 比例不一致和 user scale 独立组合；
- 单宿主配置、RuntimeSnapshot 序列化、宿主与命中共用 effective scale、收起旧测量回归均有测试保护。

## Release 真实证据

测试显示器：`\\.\DISPLAY1`，monitor `2560×1440`，work area `2560×1392`。

- monitor scale：`1.0`；WebView renderer scale：约 `1.2`；
- effective logical work area：约 `2133.33×1160`；
- responsive scale：`1.074`；effective scale：约 `1.2888`；
- 收起逻辑宿主 `510×136` → physical `657×175`；逻辑 region `490×116` → Win32 region bbox `631×149`、原点约 `(13,13)`；
- 展开逻辑宿主 `510×412` → physical `657×531`；DOM surface `547.74×442.48 CSS px` 与 viewport `548×443 CSS px` 吻合，无裁切；
- 展开后再次收起恢复 `657×175`，revision 稳定推进且未形成 resize 循环；
- HostTopology：`liveDesktopHostCount = 1`、`controlCenterCount = 1`、`totalWebviewCount = 2`、`invariantSatisfied = true`；
- 控制中心保持隐藏；唯一可见 Tauri 宿主标题为空、非前台，`Style=0x94000000`、`ExStyle=0x8000090`，继续满足 ToolWindow/NoActivate/无任务栏契约；
- 最终测试后已关闭临时 CDP 进程，并以无调试端口的普通 Release PID `67532` 重新启动，`Responding=True`。

## 未关闭矩阵

- 当前环境只枚举到一台物理显示器，尚未实测主屏/副屏之间移动；
- 负坐标副屏、混合 monitor/renderer DPI、主屏切换和热插拔仍需用户物理环境；
- 上述矩阵未通过前，C-DRP-02 保持 `IN PROGRESS`，不得标记 `COMPLETE`；
- WorkerW 当前仍因访问拒绝安全回退到已验证 ToolWindow；恢复协调的统一收口归 C-DRP-04，不在本任务强挂 WorkerW。

## 下一步

用户先确认当前单屏响应视觉无裁切；若具备第二显示器，再执行 100%/125%/150% 混合 DPI 的拖动、主屏切换与重启恢复矩阵。通过后关闭 C-DRP-02 并进入 C-DRP-03 表面登记与合并命中。

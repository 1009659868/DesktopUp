# Desktop Host Feasibility 当前交接

最后更新：2026-08-04

状态：`ACTIVE / C-DHF-04 + C-DHF-05 IN PROGRESS — M1 IMPLEMENTATION RESET`

## 已完成

- 工作流范围、任务、硬门和验证记录格式已建立；
- 产品/架构/交付文档已将桌面宿主列为首要技术风险；
- 回退模式和不注入 Explorer 的边界已明确；
- Git 仓库与实现分支、零依赖交互前端、Node 构建脚本和 Tauri 原生外壳已经建立；
- Node/pnpm、Rust/Cargo、Tauri CLI、MSVC、WebView2 与 Windows SDK 已验证可用；
- `cargo check`、Debug、Release、单实例和主页关闭收纳均已通过；
- 独立透明海报宿主已作为 Windows `ToolWindow` 运行，不进入任务栏，关闭主页后保持可见；
- 每张海报默认锁定，解锁后可拖动原生窗体，且支持收起和独立页面状态同步；
- A0 通知区域菜单已包含主页、设置、海报显隐、编辑、全局锁定、暂停、快捷主题、Agent 占位、安全重扫和退出；
- 通知区域快捷入口与可选本地 BYOK AI 设计已同步到 A/B/C 计划。
- 已接受并登记 [`运行表面模型 V0.1`](../../../A_Project/10_Plan/RUNTIME_SURFACE_MODEL_V0.1.md)：正常启动只显示桌面收纳层，主页/设置/Agent 均按需独立呼出。
- 已消除控制中心冷启动可见偏差：Release 冷启动只有海报可见，第二实例可呼出主页，关闭主页后海报继续可见；验证见 [`A0 运行表面角色代码对齐记录`](../20_Log/2026-08/2026-08-02_a0_surface_role_implementation.md)。
- 已将主页和收纳窗的米白清新风改版登记为功能硬门完成后的延期任务，当前不修改 A0 深色原型。
- 已建立隔离 WorkerW 适配器、1000ms Explorer 请求超时、可查询运行诊断和 `DESKTOP_UP_DISABLE_WORKERW` 强制回退测试入口；
- 本机 Shell 没有可见 WorkerW，适配器拒绝强挂隐藏窗口并自动使用桌面 Z 序锚定的 `ToolWindow`；它位于 `Progman` 正上方、普通应用下方，`C-DHF-02` 以接受回退结论完成；
- 原生窗体尺寸和圆角区域统一使用 WebView `devicePixelRatio` 换算，命中矩形改由不受入场动画影响的布局尺寸驱动，已通过“边界不命中、中心命中”和 DPI/区域包围盒探针；
- 已修复当前 1.20x WebView 缩放下的右侧/底部裁切：Release 原生窗体为 `612×494`，最终区域诊断为 `R(10,10 490×392) @1.20x`；完整显示截图因包含第三方游戏画面与用户标识，仅保留为本机诊断证据；
- 当前前台游戏覆盖海报坐标且系统级鼠标注入未改变 DOM 状态，因此没有把真实按钮点击伪记为通过。
- 用户实测发现 Win+D 后海报不可见，`C-DHF-04` 已据此提前进入进行中；原生探针确认旧窗体仍为可见、未最小化、未 DWM Cloak 且圆角区域有效，暴露的是 ToolWindow 回退缺少 Shell 切换后的主动恢复协调；
- 已新增基于 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` 的事件驱动恢复：仅在 Shell 成为前台、ToolWindow 回退生效且海报逻辑上应可见时，合并事件并延迟 140ms，以 `SW_SHOWNOACTIVATE + SWP_NOACTIVATE` 重新显示和锚定，不使用轮询；当前 Release PID `60216` 已运行，尚待用户手动 Win+D 复测。
- 海报配置和运行时标题均已为空，且原生样式已清除 `WS_CAPTION/WS_THICKFRAME/WS_SYSMENU/WS_MINIMIZEBOX/WS_MAXIMIZEBOX`；用户仍发现约 10px 无文字亮带后，Windows 适配层进一步禁用 DWM 非客户区、系统边框和系统圆角，WebView 同时停用透明窗体 `backdrop-filter`。当前探针为 `TitleLength=0`、`Style=0x94000000`、`HasCaption=false`、`DwmNc=0`，等待用户视觉复测，不提前标记通过。
- 用户已于 2026-08-02 确认 C-DHF-03 鼠标命中与穿透人工矩阵通过；结合原生边界/中心探针，C-DHF-03 正式完成。
- 已新增版本化原生位置存储：保存显示器设备名、工作区归一化锚点和绝对坐标回退，连续移动事件通过 300ms 合并写盘，配置位于用户 `%APPDATA%/com.allmight.desktopup/poster-placement-v1.json`；
- Release 黑盒验证完成 `(72,154) → (120,190) → 重启准确恢复 → 恢复原位` 闭环；
- 已接入 `EVENT_OBJECT_CREATE..EVENT_OBJECT_SHOW` Shell 生命周期事件，以及 `WM_DISPLAYCHANGE/WM_DPICHANGED/WM_SETTINGCHANGE` 窗口子类消息；离屏 `(5000,5000)` 测试在模拟显示变化后被拉回当前工作区右下边界 `(1948,898)`，随后恢复原位；
- WebView 现在在 resize/DPI 变化后重新同步物理尺寸和交互区域；C-DHF-04 恢复基础通过，证据见 [`原生位置持久化与显示恢复记录`](../20_Log/2026-08/2026-08-02_position_and_display_recovery.md)。
- 已建立 C-DHF-05 统一自动暂停状态：前台全屏/最大化、WTS 锁屏和显示器电源通知通过事件进入 `RuntimeSnapshot`，与手动暂停合并为 `effectivePaused`，不使用高频轮询；
- WinEvent 回调直接携带前台 HWND，启动后追加一次 1500ms 单次复核；主页和海报通过 `runtime-pause-state` 同步，托盘显示暂停原因，海报暂停时冻结全部 CSS 主题动画；
- 当前 Release PID `74264` 的三类监视器能力位为 `7/7`，14 项 Node/契约测试、6 项 Rust 测试、Release 构建均通过；自动化桌面会话无法替代真实全屏/锁屏/熄屏操作，证据和待测矩阵见 [`C-DHF-05 自动暂停基础记录`](../20_Log/2026-08/2026-08-02_runtime_auto_pause_foundation.md)。
- Windows 应用日志确认旧 Release PID `74264` 于 `2026-08-02 23:58:52` 发生 Event 1002 Application Hang；定位到自动暂停后台线程持有 `RuntimeState` 时同步托盘 UI 与主线程形成互等风险，现已改为释放状态锁后统一投递主线程；
- 15 项 Node/契约测试、6 项 Rust 测试、`cargo check` 与 Release 重建通过；首轮修复 PID `63628` 的冷启动连续采样和第二实例回呼均保持单实例、`Responding=True`，最终对齐重建 PID `70600` 也持续响应，证据见 [`启动与单实例回呼挂起修复`](../20_Log/2026-08/2026-08-03_startup_hang_recovery.md)。
- C-DHF-05 人工矩阵发现“Codex 最大化后右键托盘仍显示静态待机”失败；原因是 `Shell_TrayWnd` 被旧逻辑与 `Progman/WorkerW` 一同视为真正回桌面，托盘打开时错误清除覆盖暂停；
- 临时托盘/系统弹层现会继承上一个有效覆盖，真正桌面仍清除覆盖；16 项 Node/契约测试、8 项 Rust 测试与 Release 重建通过，修复版 PID `71836` 已运行，等待用户复测，详见 [`托盘前台切换暂停状态修复`](../20_Log/2026-08/2026-08-03_tray_pause_state_preservation.md)；
- Release 入口已切换为 Windows GUI 子系统，PE 标记为 `Subsystem 2`，启动不应再出现 Windows Terminal 命令窗口。

## 尚未完成

- `C-DHF-04` 的 Win+D、Explorer 真重启、多屏、主屏切换和 100%/125%/150%/200% DPI 仍待真实环境人工矩阵；
- 多海报原生窗体尚未验证；C-DHF-05 的真实全屏、锁屏、显示器关闭、恢复和跨屏不误停矩阵仍待人工验证；
- 工作集约 `26.7 MB` 仅是 A0 单次观测，性能预算尚未形成正式基线；
- 尚未生成最终 `DesktopUpSetup.exe` 与安装目录内的 `uninstall.exe`。
- 原 M1 海报重构实现已撤回，不再视为完成；大幅面、模板、状态机、透明背景、降级和 Release 视觉验收均需在 V1 重新实现与验证。

## 精确下一步

先在 `impl/poster-redesign-V1` 冻结并测试 M1 的状态、模板 DOM、窗口几何和失败回退契约，再实现最小大幅面海报切片。C-DHF-04/C-DHF-05 的真实环境矩阵仍保持待验证，后续按 Win+D、最大化/全屏、Explorer、锁屏、显示器和 DPI 顺序继续。

## 工作区注意事项

- 当前实现分支是 `impl/poster-redesign-V1`；该分支保留远程 `impl/poster-redesign-a0` 的设计文档，代码恢复到 M1 落地前基线；未经用户明确要求不推送、不合并、不改写远端；
- 不覆盖 Docs 中已建立的用户需求和卸载安全规则；
- 工程初始化不得把依赖缓存或构建输出提交到文档目录。

## M1 实现重置（2026-08-04）

- 原 `impl/poster-redesign-a0` 与后续修复线的 M1 完成/封板结论撤回；历史提交和审查记录只作为失败样本，不作为当前代码事实。
- `impl/poster-redesign-V1` 保留已接受的 `POSTER_*` 设计文档，将 `app/`、`src-tauri/` 和 `tests/` 恢复到提交 `8eac6eb` 的 M1 落地前状态。
- 原实现暴露的结构性问题包括：模板 DOM 双根、状态与原生窗口几何多数据源、模板加载/净化边界不足、透明背景使用不稳定指针路径、以及测试只检查源码字符串而未覆盖行为。
- V1 不直接复用失败实现；先建立可执行契约和行为测试，再按 minimal-fallback → 大幅面 → 状态机 → Neon HUD → Cinema Lightbox → 交互/降级/Release 验收推进。
- M2 本地收纳继续保持 `PLANNED`，不得在 M1 和宿主硬门通过前启动真实桌面文件操作。

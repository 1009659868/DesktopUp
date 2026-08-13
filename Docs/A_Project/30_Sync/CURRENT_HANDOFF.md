# A 层当前交接

最后更新：2026-08-13

状态：`V1-P1 ACTIVE / C-IBC COMPLETE / C-RLC READY TO ACTIVATE`

## 已建立

- 产品目标和首版 MVP 边界已形成文档基线；
- Rust + Tauri 2 + Svelte/TypeScript 是当前提议技术栈；
- 安装、恢复和卸载被列为发布硬门；
- A/B/C 活跃链已指向桌面宿主可行性验证；
- Git 仓库已经建立，当前实现分支为 `impl/poster-redesign-V1`；`impl/poster-redesign-V1-fix` 的已验证修复已于 2026-08-08 合入，设计文档继续有效，M1 未重新封板；
- 已建立零额外依赖的交互前端 Demo 与 Tauri 原生双窗体应用，并完成 Debug/Release 编译；
- 已验证通知区域入口、主页关闭收纳、单实例回呼，以及无任务栏的独立透明海报 `ToolWindow` 回退宿主；
- 已接受 [`运行表面模型 V0.1`](../10_Plan/RUNTIME_SURFACE_MODEL_V0.1.md)：桌面海报是唯一默认可见表面，当前主页只作为按需控制中心；
- 通知区域快捷入口与本地 BYOK AI 助手已进入产品、架构和交付基线。
- [`快捷方式导入与安全收纳模型 V0.1`](../10_Plan/SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md) 已排入 A1：主动导入/扫描后先预览，再由用户选择仅添加或安全收纳；收纳共用恢复账本、撤销与卸载恢复。
- 已完成 A0 表面角色代码对齐：冷启动只显示海报，控制中心由托盘或第二实例按需呼出，关闭后不影响海报。
- 已记录主页与收纳窗后续改为米白、清新风；视觉制作延后到核心功能硬门完成后。
- [`DesktopUP V1 目标设计基线`](../10_Plan/DESKTOPUP_V1_DESIGN_BASELINE.md) 已建立；已冻结 Transparent Default（暂名）+ Neon HUD + Cinema Lightbox 三模板、主屏右侧、默认解锁展开、USING 自制标题栏拖动、Shadow DOM、完整模板安装和单全局宿主；上述均尚未实现和验证；
- [`DesktopUP V1 落地推进计划`](../10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md) 已成为唯一活跃 A 实施计划，拆分为 9 个 B 模块和 35 个 C 工作流；旧 `PROJECT_MAINLINE.md` 与 `DELIVERY_PLAN_V0.1.md` 已冻结。
- 2026-08-09 路线调整为先完成生产宿主/核心契约，再交付“仅添加 + 真实启动”可用 MVP 和安全收纳 MVP，之后建设完整模板、布局、主题、AI 与游戏平台来源；新增 C-PSS，并将旧 C-LRC 拆为 C-LCH/C-RCV。
- C-DRP-01 已完成版本化 HostSurface/DisplayTopology/InteractionRegion/RuntimeSnapshot 契约；海报前端改用单次 `sync_host_surface`，旧两步命令仅保留为同一内部路径的兼容包装。Release 实机窗口尺寸、圆角命中区域、单实例和响应状态通过。
- C-DRP-02 主屏代码闭环、冷启动去白条、收起/展开裁切和按钮命中已通过自动化、Release 与用户复测；第二显示器/混合 DPI 物理矩阵由用户明确延期至 C-DRP-06，因此 C-DRP-02 关闭当前门但不声称多屏实机通过；
- C-DRP-03 已按单海报范围完成：同一登记项拆分完整 `visualRegion` 与标题栏/卡片 `interactionRegions`，零/一/多/重叠仅指输入矩形，不代表多海报产品能力；
- 用户在最新 Release 上确认空白框选、桌面右键、原生快捷方式、六张卡片、标题栏拖动/锁定、无白条与收起/展开均通过，C-DRP-03 已关闭；
- 已建立 WorkerW 安全适配与 ToolWindow 自动回退诊断；本机无可见 WorkerW，当前采用已验证回退，不强挂隐藏 Shell 窗口。
- C-DRP-03 的旧版 FAIL 截图与新版 PASS 矩阵均保留为历史证据；第二显示器/混合 DPI 等物理矩阵仍延期至 C-DRP-06。
- C-DHF-04 已实现原生位置持久化、显示器工作区锚点和显示/Shell 事件恢复基础；重启恢复与模拟离屏拉回通过，真实 Win+D、Explorer、多屏和 DPI 矩阵仍待完成。
- 2026-08-04 决定撤回原 `impl/poster-redesign-a0` 的 M1 完成/封板结论：原实现存在模板、状态、窗口几何和测试契约的结构性缺陷，不作为 V1 代码基线；设计文档继续有效。
- 2026-08-08 已修复 Dev/Release 海报状态分裂、大尺寸破坏性截断、锁定派生状态与提示错误、托盘快捷操作误开主页、首次快照丢海报和任务栏可见等缺陷；采用过渡 Rust JSON 共享桥接，正式状态仓库由后续 C-PSS 收敛。
- 同日后续修复 ToolWindow 点击后覆盖普通应用与顶部亮带：当前海报不可激活并原生锚定在 `Progman` 正上方；2026-08-10 用户确认新版 Release 首次显示无顶部白条，收起/展开无裁切且按钮命中正常。

- C-DRP-04 的最终 Release 连续 Win+D 已由用户确认较稳定并接受通过；多屏/DPI 物理矩阵仍延期至 C-DRP-06；
- 2026-08-13 用户报告 C-DRP-04 的旧 48ms TOPMOST 防护在桌面右键关闭、刷新和 Windows 文件选择器出现时造成瞬时覆盖普通程序；维护版已改为真实桌面 Shell 过滤和有界 Progman 相对重锚，用户随后确认桌面右键关闭、刷新、文件选择器、连续 Win+D 与核心交互矩阵全部通过；
- C-DRP-05 的版本化 lifecycle、锁外统一事件、状态优先显隐、退出状态、前端乱序保护和静态/暂停无持续轮询已通过自动测试、Release/NSIS 与用户 5/5 人工矩阵，任务关闭；
- C-DRP-06 已按用户确认升级为主进程 50 MiB + 完整进程树 256 MiB 私有工作集双预算，并将控制中心改为首次请求创建；`performance-gate-v3` / `performance-baseline-v4`、58 项前端/契约、52 项 Rust、Release/NSIS 和最新版普通 Release 四项烟雾全部通过；用户随后接受主屏单海报认证限制，C-DRP-06 与 C-DRP 关闭；
- C-PCB-01..05 与 C-PSS-01..05 已完成；SQLite 已成为唯一正式状态权威，Release 隔离恢复矩阵与用户四项核心回归通过，C-PSS 关闭；
- C-IBC-01 冻结当前用户无提权安装、程序/数据分离、现有 roaming SQLite authority 保留到 C-UGM、自启默认关闭和显式运行时依赖基线；
- C-IBC-02 已完成只读运行时路径解析和 `install-path-diagnostics-v1`；setup 继续使用 roaming authority；
- C-IBC-03 已完成运行时/构建侧双层只读探针，current-user/无提权/NSIS 卸载入口得到证据，五项发布差距归 C-RCG；
- C-IBC-04 已完成 `autostart-contract-v1`、只读 HKCU 诊断、正式路径/参数门和 C-URV 清理责任，实际自启仍禁用；
- C-IBC-05 后续维护已把原十项收敛为个人自用契约包装门零缺口：五项实际修复，五项许可证相关项不适用；当前仍未通过 C-RCG 净机/升级/卸载恢复/签名认证；
- 控制中心 legacy `accent/opacity/density` 视觉参数调整回弹，用户决定后续修复；该缺陷归后继 Template Platform 参数权威，不记为通过且不重开 C-PSS；
## 尚未完成

- C-DHF-04/05 剩余物理矩阵（Explorer 真重启、热插拔、主屏/DPI 切换等）因物理环境延后；已确认的真实游戏全屏、Win+L、显示器关闭/唤醒通过；
- C-DHF-06 四态性能采集、采样保存和 Desktop-Up 内性能数值可视化已实现，真实四态基线采样和收口核对已完成；
- C-DHF-04 已由用户手动确认通过；
- C-DHF-05 已由用户手动确认通过；
- C-DHF-07 已由用户手动确认通过；
- C-DHF-08 决策封板已完成，`V1-P0` 宿主硬门关闭；
- 托盘行为契约已调整：单击托盘不改变海报，双击切换主页，海报显隐仅通过右键菜单“显示海报宿主”控制；
- M1 海报重构代码已重置为 `PLANNED`，尚未通过大幅面、模板、状态机、透明背景和 Release 实机验收；
- 技术架构仍为 `PROPOSED`，需通过 A0 原型决定；
- 产品暂名为 `DesktopUP`，品牌图标和 Transparent Default 的正式名称/完整视觉规格尚未冻结；AI 低风险免确认范围仍为 `TBD`；
- 尚未生成 `DesktopUpSetup.exe` 或 `uninstall.exe`。

## 下一动作

当前 `C-DHF-01..08`、`C-DRP-01..06`、`C-PCB-01..05`、`C-PSS-01..05` 与 `C-IBC-01..05` 已关闭，C-DRP-04 维护矩阵也已通过。当前没有正在实施的 C；下一候选 C-RLC 等待用户明确激活。当前默认与发布范围仍为单海报，多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未认证。当前改动尚未提交或推送，最近远端同步基点为 `d225de1`。

A1 真实快捷方式能力保持排队状态；C-ROD/C-PAA/C-LCH 只允许用户主动触发的只读发现、仅添加和启动，任何真实文件移动必须等待 C-SVT 的保险库、账本、逐批确认和故障注入通过。

## 发布底线

最终交付不能只生成开发可执行文件。必须生成可在净机使用的 `DesktopUpSetup.exe`，且安装目录提供独立 `uninstall.exe`，完成快捷方式恢复和应用痕迹清理。

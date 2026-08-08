# A 层当前交接

最后更新：2026-08-08

状态：`V1-P0 DESKTOP HOST GATE CLOSED / NEXT C-DRP + C-PCB`

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
- [`DesktopUP V1 落地推进计划`](../10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md) 已成为唯一活跃 A 实施计划，拆分为 9 个 B 模块和 33 个 C 工作流；旧 `PROJECT_MAINLINE.md` 与 `DELIVERY_PLAN_V0.1.md` 已冻结。
- 已建立 WorkerW 安全适配与 ToolWindow 自动回退诊断；本机无可见 WorkerW，当前采用已验证回退，不强挂隐藏 Shell 窗口。
- 单海报透明边界、按钮/拖动与桌面输入人工矩阵已经通过，C-DHF-03 完成。
- C-DHF-04 已实现原生位置持久化、显示器工作区锚点和显示/Shell 事件恢复基础；重启恢复与模拟离屏拉回通过，真实 Win+D、Explorer、多屏和 DPI 矩阵仍待完成。
- 2026-08-04 决定撤回原 `impl/poster-redesign-a0` 的 M1 完成/封板结论：原实现存在模板、状态、窗口几何和测试契约的结构性缺陷，不作为 V1 代码基线；设计文档继续有效。
- 2026-08-08 已修复 Dev/Release 海报状态分裂、大尺寸破坏性截断、锁定派生状态与提示错误、托盘快捷操作误开主页、首次快照丢海报和任务栏可见等缺陷；采用过渡 Rust JSON 共享桥接，正式状态仓库仍由后续 C-DRP/C-PCB 收敛。
- 同日后续修复 ToolWindow 点击后覆盖普通应用与顶部亮带：当前海报不可激活，原生锚定在 `Progman` 正上方，独立桌面表面不再运行可能被自动暂停冻结的入场位移动画；自动探针通过，等待用户目视确认。

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

当前 `C-DHF-01..08` 已全部关闭，`V1-P0` 宿主硬门具备关闭结论。下一阶段激活 `C-DRP Desktop Runtime Productionization` 与 `C-PCB Poster Contract Baseline`，由 `ACTIVE_PATH.md` 唯一选中；在此之前不把 M1 海报实现标记为完成。精确状态见 [`Desktop Host Feasibility 当前交接`](../../C_Workstreams/DesktopHostFeasibility/30_Sync/CURRENT_HANDOFF.md)。

A1 真实快捷方式能力保持排队状态，在上述 A0 硬门通过前不得开始扫描或移动用户桌面文件。

## 发布底线

最终交付不能只生成开发可执行文件。必须生成可在净机使用的 `DesktopUpSetup.exe`，且安装目录提供独立 `uninstall.exe`，完成快捷方式恢复和应用痕迹清理。

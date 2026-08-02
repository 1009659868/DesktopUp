# Desktop-Up 项目更迭记录

本文件只记录项目级已发生事实。局部实现过程进入对应 C 工作流日志。

## 2026-08-02 — WorkerW 适配形成可观察的安全回退结论

状态：`COMPLETE WITH ACCEPTED FALLBACK`

- WorkerW 发现、超时请求、父窗口/样式切换被隔离到 Windows 适配层；
- 本机 Shell 没有可见 WorkerW，应用拒绝强挂隐藏窗口并自动使用桌面 Z 序锚定的 `ToolWindow`；
- 托盘状态和无边框窗口标题可观察实际宿主模式，测试环境可以显式强制回退；
- 单海报原生圆角交互区域由实际 DOM 矩形驱动，透明边界不再参与窗口命中；
- 后续仍需完成人工桌面输入矩阵和 Explorer/显示器恢复。

关联记录：

- [`2026-08-02_workerw_adapter_and_hit_region.md`](../../C_Workstreams/DesktopHostFeasibility/20_Log/2026-08/2026-08-02_workerw_adapter_and_hit_region.md)

## 2026-08-02 — A0 默认可见表面完成代码对齐

状态：`COMPLETE`

- 控制中心改为冷启动隐藏，桌面海报保持默认可见；
- 托盘主页/设置与第二实例仍能显式呼出控制中心；
- 关闭控制中心只执行隐藏，不结束运行时或改变海报可见性；
- Release 实测通过“冷启动仅海报 → 第二实例打开主页 → 关闭主页仍保留海报”闭环；
- 当前 `ToolWindow` 仍是回退宿主，WorkerW 挂载与完整 C-DHF-02 仍待完成。

关联记录：

- [`2026-08-02_a0_surface_role_implementation.md`](../../C_Workstreams/DesktopHostFeasibility/20_Log/2026-08/2026-08-02_a0_surface_role_implementation.md)

## 2026-08-02 — 记录米白清新风视觉改版方向

状态：`DEFERRED`

- 当前主页和收纳窗的深色主题不作为最终视觉基线；
- 后续统一改为米白色、清新、轻盈的主页与海报视觉语言；
- 视觉制作延后到宿主、收纳、恢复和性能等核心功能完善后，避免现在打断 A0 硬门。

## 2026-08-02 — 将真实快捷方式导入与安全收纳模型排入 A1

状态：`COMPLETE`

- 明确当前 A0 图标目录仍为假数据，不在桌面宿主硬门完成前扫描或移动真实桌面项目；
- 接受用户主动拖入或主动扫描桌面的入口，不在冷启动时静默扫描；
- 接受写入前预览，以及“仅添加”和“收纳并移出桌面”两种明确模式；
- `.lnk`/`.url` 只移动快捷方式文件，`.exe` 只登记，永不移动或复制游戏程序；
- 将保险库、两阶段恢复账本、异常事务恢复、撤销和卸载恢复列为 A1 阶段门；
- 将 A1 拆为数据/只读扫描、预览/仅添加、安全收纳/撤销、启动/卸载恢复四个切片。

关联文档：

- [`SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md`](../10_Plan/SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md)
- [`DELIVERY_PLAN_V0.1.md`](../10_Plan/DELIVERY_PLAN_V0.1.md)

## 2026-08-02 — 接受桌面收纳层与控制中心分离模型

状态：`COMPLETE`

- 接受 Desktop-Up 为后台常驻桌面收纳工具，而非以主页窗口为默认主体的传统应用；
- 普通启动和开机自启后，唯一默认可见的产品表面是融入真实桌面的海报收纳层；
- 当前 A0 主页视觉保留为按需控制中心，设置和 Agent 也属于按需控制面；
- 关闭控制中心不得影响海报，隐藏海报不得强制打开控制中心；
- 推荐每显示器共享一个透明宿主承载多张海报，避免每海报一个永久 WebView2；最终拓扑仍由 A0 验证冻结；
- 当前 A0 启动时显示主页被记录为已知偏差，本次只记录模型，不继续修改代码。

关联文档：

- [`RUNTIME_SURFACE_MODEL_V0.1.md`](../10_Plan/RUNTIME_SURFACE_MODEL_V0.1.md)
- [`2026-08-02_runtime_surface_model_alignment.md`](../../C_Workstreams/DesktopHostFeasibility/20_Log/2026-08/2026-08-02_runtime_surface_model_alignment.md)

## 2026-08-02 — A0 原生 Demo 首次形成可运行闭环

状态：`COMPLETE`

- Node/pnpm、Rust/Cargo、Tauri CLI、MSVC、WebView2 与 Windows SDK 工具链已经验证。
- 前端自动化覆盖动态添加、主题切换、编辑、锁定阻止拖动、解锁拖动、暂停和独立海报状态共享。
- 建立主页与透明海报双窗体；海报使用 Windows `ToolWindow` 回退宿主，无任务栏入口并保持 bottom-most。
- 建立主页关闭收纳、海报独立保持、单实例回呼和原生 Release 构建闭环。
- A0 通知区域右键菜单已包含主页、设置、海报显隐、编辑、全锁、暂停、快捷主题和后续能力入口。
- 当前交付仍是 A0 Demo；WorkerW、空白命中测试、自动暂停、Explorer 恢复和最终安装/卸载包仍是后续硬门。

关联记录：

- [`2026-08-02_a0_demo_scaffold.md`](../../C_Workstreams/DesktopHostFeasibility/20_Log/2026-08/2026-08-02_a0_demo_scaffold.md)

## 2026-08-02 — 接受通知区域入口与可选本地 AI 助手

状态：`COMPLETE`

- 接受 Windows 通知区域图标作为常驻入口；图标处于主通知区域或“∧”收纳栏由 Windows 和用户设置决定。
- 接受单击显示/隐藏海报、双击打开主页、右键打开主页/设置/快捷主题/编辑/暂停/自启等原生菜单契约。
- 明确透明桌面宿主固定到显示器，用户拖动和收纳的是海报模块；每张海报支持独立锁定，托盘支持全局锁定/解锁。
- 接受用户自备 Key 的本地 AI 助手，可配置 OpenAI、DeepSeek 与 OpenAI-Compatible Provider。
- 无 API Key 时不初始化 Agent、不创建 Provider、不产生 AI 网络请求。
- 项目不建设账号、官方 Key、计费、额度或云端代理；费用只由用户选择的 Provider 与 Key 决定。
- AI 仅通过受控工具生成 `ChangePlan`，所有修改遵循预览、确认、应用和撤销契约。
- AI 参数控制进入首版规划；受限 JavaScript 主题不作为最小可发布版本的阻塞项。

关联文档：

- [`TRAY_AND_QUICK_ACCESS_DESIGN_V0.1.md`](../10_Plan/TRAY_AND_QUICK_ACCESS_DESIGN_V0.1.md)
- [`AI_ASSISTANT_DESIGN_V0.1.md`](../10_Plan/AI_ASSISTANT_DESIGN_V0.1.md)
- [`DELIVERY_PLAN_V0.1.md`](../10_Plan/DELIVERY_PLAN_V0.1.md)

## 2026-08-02 — 项目文档与设计基线建立

状态：`COMPLETE`

- 接受 A/B/C 三层文档模型：A 项目主线、B 能力模块、C 可执行工作流。
- 建立 Catalog、Plan、Log、Sync 固定分区和权威阅读路径。
- 建立产品设计 V0.1、技术架构 V0.1 和制作交付计划 V0.1。
- 当前项目阶段定为 `A0 可行性与体验定型`。
- 当前活跃 B 模块定为 `Desktop Runtime Foundation`。
- 当前活跃 C 工作流定为 `Desktop Host Feasibility`。
- 最终 Windows 安装入口暂定为 `DesktopUpSetup.exe`。
- 确立安装目录必须包含独立的 `uninstall.exe`。
- 确立卸载前恢复托管快捷方式、失败时保留保险库的安全底线。

关联文档：

- [`PROJECT_MAINLINE.md`](../10_Plan/PROJECT_MAINLINE.md)
- [`PRODUCT_DESIGN_V0.1.md`](../10_Plan/PRODUCT_DESIGN_V0.1.md)
- [`TECHNICAL_ARCHITECTURE_V0.1.md`](../10_Plan/TECHNICAL_ARCHITECTURE_V0.1.md)
- [`DELIVERY_PLAN_V0.1.md`](../10_Plan/DELIVERY_PLAN_V0.1.md)

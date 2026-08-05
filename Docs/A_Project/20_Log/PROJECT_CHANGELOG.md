# Desktop-Up 项目更迭记录

本文件只记录项目级已发生事实。局部实现过程进入对应 C 工作流日志。

## 2026-08-06 — 调整托盘单击/双击契约

状态：`ACCEPTED / ACTIVE`

- 单击托盘不再显示/隐藏海报，避免误隐藏桌面常驻程序；
- 双击托盘切换主页显示/隐藏；
- 海报显隐仅通过右键菜单“显示海报宿主”控制；
- 同步更新 `TRAY_AND_QUICK_ACCESS_DESIGN`、`RUNTIME_SURFACE_MODEL`、`PRODUCT_DESIGN`、B 模块计划和 C 工作流计划。

## 2026-08-04 — 冻结旧制作计划并建立 V1 A/B/C 落地路线

状态：`PLAN BASELINE / ACTIVE`

- 建立 [`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)，作为唯一活跃 A 层实施计划；
- 将 V1 拆分为 9 个 B 能力模块和 33 个可验证 C 工作流，定义依赖、阶段门、完成信号和证据要求；
- `PROJECT_MAINLINE.md`、`DELIVERY_PLAN_V0.1.md` 与旧 DesktopRuntimeFoundation `MODULE_PLAN.md` 标记为 `SUPERSEDED`，保留追溯但停止新增任务；
- C-DHF-01..03 的完成结论和 C-DHF-04..08 的未完成状态原样保留；当前仍从 C-DHF-04/C-DHF-05 推进，不因计划重组跳过硬门；
- 下一工作流确定为 `C-PCB Poster Contract Baseline`，只能在 C-DHF-08 关闭后激活。

## 2026-08-04 — 建立 V1 总设计基线并冻结关键决策

状态：`DESIGN BASELINE / NOT IMPLEMENTED`

- [`DesktopUP V1 目标设计基线`](../10_Plan/DESKTOPUP_V1_DESIGN_BASELINE.md) 已建立，作为跨文档默认值与关键决策入口；
- 冻结首发三模板、主显示器工作区右侧、默认解锁并展开、`USING` 状态通过自制标题栏拖动、Shadow DOM 模板隔离、完整模板安装能力和单全局宿主；
- 上述决策均尚未实现、尚未验证；
- AI 低风险免确认范围、默认模板正式名、产品正式名和正式图标仍为 `TBD`。

## 2026-08-04 — 撤回原 M1 封板并建立 V1 重做分支

状态：`RESET / ACTIVE`

- 确认原 `impl/poster-redesign-a0` 及修复线存在模板、状态、窗口几何、安全边界和测试覆盖的结构性问题，继续堆叠修复不再作为主线方案；
- 原 M1 完成、硬门通过和里程碑封板标记全部撤回，M2 本地收纳继续保持排队状态；
- 保留远程 `impl/poster-redesign-a0` 中已接受的产品、视觉、状态机、数据和模板设计文档；
- 新建 `impl/poster-redesign-V1`，将实际代码恢复到提交 `8eac6eb` 的 M1 落地前状态，作为重新实施基线；
- 原修复分支现场已独立保存，不合并到 V1；V1 先建立契约与行为测试，再逐片实现和验收。

## 2026-08-03 — 接受海报四级风格化定制体系与 Agent 共用 API

状态：`COMPLETE`

- 接受四级控制深度：开关与预设（深度 0）→ 结构化参数（深度 1）→ 素材替换（深度 2）→ 代码覆盖（深度 3）；
- 接受参数面板由模板 `params-schema.json` 自动生成，新增模板或用户导入自定义设计不需要改写控制中心前端代码；
- 接受深度 3 使用纯代码编辑器，首版不做可视化拖拽编辑器；
- 接受首版只做文件导入导出，不做在线主题市场；
- 接受首版使用纯 CSS 变量 + 结构化参数 schema，不引入 Tailwind/UnoCSS 等原子 CSS 框架；
- Agent 通过统一读写 API 控制所有定制层级，与手动 UI 共用 `DesktopCommand`/`ChangePlan`/预览/确认/撤销链；
- 已登记 [`POSTER_THEME_CUSTOMIZATION_V0.1.md`](../10_Plan/POSTER_THEME_CUSTOMIZATION_V0.1.md) 为权威设计基线；
- 重构分支 `impl/poster-redesign-a0` 已创建，允许在当前 C-DHF 硬门通过前开始切片 1（大幅面窗体 + 亮带根治），切片 4–6（排序/锚定/跳变）延后到视觉效果确定后推进。

## 2026-08-03 — 接受海报图标排布与排序系统

状态：`COMPLETE`

- 接受三维加权排序得分公式：`0.40 × recency + 0.35 × frequency + 0.25 × newness`；
- 接受三层活跃度衰减模型（存在时间保护 × 总启动次数抵抗力 × 长期不玩惩罚），下限 0.1；
- 接受行式弹性流 + 子列填充排布算法：大图标右侧剩余空间垂直堆叠小图标，形成"主列 + 附属区"的海报视觉语言；
- 接受三层排序控制（全自动 → 锚定+自动 → 全手动），锚定项使用浅金色图钉标记；
- 接受拖拽触发式活跃度跳变机制：用户将底部游戏拖到上半区并在 5 分钟内启动，该项 rank_score 临时提升至邻居水平，跳变每 24 小时衰减 50%，每次正常启动加速 30% 衰减，真实数据追上后自动归零；
- 实施分四切片：数据模型 → 布局引擎 → 锚定控制 → 跳变系统；
- 已登记 [`POSTER_LAYOUT_AND_SORT_V0.1.md`](../10_Plan/POSTER_LAYOUT_AND_SORT_V0.1.md) 为权威设计基线。

## 2026-08-03 — 接受大幅面海报与动态图标缩放重设计

状态：`COMPLETE`

- 接受海报从"小型悬浮窗"升级为"桌面级大幅面展示面板"的方向；
- 接受对数映射算法（`scale = min + (max-min) × log(1+n)/log(1+N)`）作为图标动态缩放的默认曲线；
- 接受热度衰减：超过 30 天未启动的游戏按每 7 天 15% 衰减有效启动次数，重新启动后恢复；
- 接受 50%–300% 可调范围，未启动游戏退到 50% 但仍可见，热门游戏最大 3 倍；
- 接受动画过渡使用 400ms 弹性曲线，启动后即时重新计算，展开时批量过渡；
- 接受大幅面默认尺寸：高度近满工作区（上下 12px 安全间距），宽度 `max(工作区宽 × 0.22, 280px)`，用户可拖动调整并持久保存；
- 接受 Neon HUD 和 Cinema Lightbox 为首批内置模板，后续逐步加入 Glass Gallery、Blueprint、Comic Panel 和 Minimal Shelf；
- 接受 WebView2 `DefaultBackgroundColor` 全透明方案作为窗体顶部亮带的根治方向，在大幅面重构时同步实施；
- 模板插槽完整 API 契约和大小不等图标的网格排布算法延后到基础功能稳定后展开；
- 认可当前 C-DHF 硬门通过后直接重构海报，不继续修补现有 490×392 小型原型；
- 已登记 [`POSTER_REDESIGN_V0.1.md`](../10_Plan/POSTER_REDESIGN_V0.1.md) 为权威设计基线。

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

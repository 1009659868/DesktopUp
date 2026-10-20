# R4收口后的当前架构与目标对照

本页图源仍是R4历史快照，不代表当前源码。2026-10-20 当前：**本轮基础架构整理 USER ACCEPTED / CLOSED；R1～R8、P1～P3均已验。** 用户已授权正式收口，[当前架构图、收口范围与交付限制](../../../40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#31-本轮基础架构正式收口与当前架构图2026-10-20)已记录。图稿showcase 9/9通过，浏览器/视觉复验未完成；不与业务验收混记。剩余规模成本和历史锁冲突继续携带，动态模板/AI/更新器等未实现。后续Git授权已执行：架构3c2417e推送并合入develop@bbbe417；精简main@fe5741d已同步，完成限定构建/打包/隔离运行验证，最终回develop。详细交接以HANDOFF和发布记录为准；一次卸载中断重试成功但首因未定，独立保留。真实热度HOLD，真实登录自启和其他未验环境继续保留。下方为阶段历史。

2026-10-19，分支为 `impl/architecture-foundation`。用户确认R4两组集中验证均通过，R1～R4 USER PASS / CLOSED。本次关闭的是**现有业务的基础职责整理**，不是原目标图中的全部未来能力。对应源码、测试和图源已随b90768b提交并推送，未合并；待用户确认整体架构优化完成后再考虑集成。

## 两张图如何阅读

- [当前架构 HTML](../../../../../out/architecture-r4-closed/current.architecture.html)对应本工作区已验收源码；[图源](current.architecture.json)随开发资料保存。
- [原目标架构 HTML](../../../../../out/architecture-r4-closed/target.architecture.html)由[原图源](../target.architecture.json)原字节重新生成，节点、连线和目标含义均未修改。
- 原[R4待验快照](../R4/README.md)保留历史身份，不覆盖新结论。

当前图的箭头表达职责协作和数据去向，不是每次操作必经的同步调用栈，也不是程序集引用图。例如应用用例的结果经既有投影/运行器进入海报会话；Presenter不直接调用输入岛，而由窗口宿主协调命中应用、绘制与空间发布。每海报会话/窗口由工厂创建，Presenter随窗口创建；组合根负责注入它们所需依赖，不是所有对象都直接在一个构造函数里生成。

## 与原目标逐项对照

| 原目标模块 | 当前实际状态 | 尚需实现或保持的边界 |
| --- | --- | --- |
| 薄界面层 | **现有业务边界已落地**。窗口/中心发类型化意图、呈现结果；不自建文件业务或查询具体SQLite | WinUI、Dispatcher、原生输入与场景材质仍属于宿主。薄界面不等于移除所有partial或所有UI逻辑 |
| 应用用例层 | **已落地**。新建/命名/删除/Win菜单、拖放、映射/同步、归还/卸载、收纳位置迁移、配置/图标/引用修复经应用用例与端口 | 新业务继续按功能分治；不增加万能仓库或消息总线 |
| 稳定海报内核 | **现有边界已落地**。会话拥有草稿/提案保护；纯布局输出同代帧；Presenter持有稳定卡片/暖图，宿主负责原生提交 | 不是所有代码都成为纯内核：UI资源和HWND操作必须留平台侧 |
| 持久化适配 | **已落地**。沿用SQLite、原子事务、CAS及安全账本；用例通过窄端口调用 | 不新增第二份权威状态，不让策略直接读取具体Store |
| 平台与部署宿主 | **本轮基础边界已落地**。显式组合根管理唯一数据库/Worker，平台共享元数据探测；Worker不链接Infra源码、不带持久库 | 安装器既有能力不等于发布自动更新；发布同步/真实登录自启仍暂停 |
| 能力接入与验证 | **部分具备**。热度/主题提案已有版本、ItemSet、参数和交互保护检查及同代发布 | 还没有通用能力权限、插件加载、模板包准入或AI计划验证器 |
| 热度策略模块 | **事实记录与提案接点已有，真实评分未实现** | 下一批补事实读取/幂等消费与游标、纯评分策略和提案生产；策略热替换另需生命周期/版本合同 |
| 模板与形状模块 | **仅有内置模块和参数边界**。Lamp/Rain等继续使用已有场景端口 | 声明式模板包热加载、无需重启换结构、圆形/异形/离散区域尚未实现 |
| AI计划生成 | **未实现** | 脱敏上下文、受限计划、审查/应用链仍待制作，不直接赋予文件或窗口权限 |
| 独立更新器 | **未实现** | 包校验、受控切换、回滚和更新渠道仍待制作；安装/卸载不能代替它 |

因此，当前已经达到“先整理现有业务职责，再接真实热度”的基础出口；没有达到“所有可替换能力均已可用”的完整平台目标。

## 代码入口与验证依据

以下源码已纳入b90768b检查点；本次Git同步没有改变产品逻辑或图源字节。完整候选指纹、清理记录与自动结果归[实施计划第19节](../../../40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#r4-composition-boundary)。

| 边界 | 实际入口 | 对应证据 |
| --- | --- | --- |
| R1用例与副作用 | [ContentRenameUseCase](../../../../../src/DesktopUp.Application/Features/Content/FileActions/ContentRenameUseCase.cs)、[CrossPosterTransferUseCase](../../../../../src/DesktopUp.Application/Features/Content/Transfers/CrossPosterTransferUseCase.cs)、[StorageRelocationUseCase](../../../../../src/DesktopUp.Application/Features/Content/Storage/StorageRelocationUseCase.cs)；[窄端口](../../../../../src/DesktopUp.Application/Ports/Content/ContentRenamePorts.cs)和[SQLite适配](../../../../../src/DesktopUp.Infrastructure/ContentActions/SqliteContentRenameJournal.cs) | R1第8～16节各切片定向结果与USER PASS；R4连续新建/命名/跨海报人工通过 |
| R2会话所有权 | [PosterContentSession](../../../../../src/DesktopUp.Application/Features/Content/Layout/PosterContentSession.cs)、[宿主提案适配](../../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs) | R2 Session真实宿主注入：保护期不发布、解除后仅最新候选、拒绝过时/错ItemSet、派生不改作者状态；USER PASS |
| R3布局与呈现 | [ContentPresentationLayout](../../../../../src/DesktopUp.Application/Features/Content/Layout/ContentPresentationLayout.cs)、[PosterContentPresenter](../../../../../src/DesktopUp.App/Presentation/PosterContentPresenter.cs)、[ContentRasterBinding](../../../../../src/DesktopUp.App/Presentation/ContentRasterBinding.cs)、[ContentPublication](../../../../../src/DesktopUp.App/PosterVisualWindow.ContentPublication.cs) | R3同代拒绝/失败保留，内部/悬停40次与收展6轮保留卡片暖图；USER PASS。R4 Application/Domain与R3字节一致 |
| R4组合根与寿命 | [ApplicationComposition](../../../../../src/DesktopUp.App/Bootstrap/ApplicationComposition.cs)、[App.Startup](../../../../../src/DesktopUp.App/Bootstrap/App.Startup.cs)、[App.Lifetime](../../../../../src/DesktopUp.App/App.Lifetime.cs) | R4完整生产编译集合依赖检查、正常/Review共用接线、Source/Offline及正常退出资源0；两组人工USER PASS |
| R4 Worker与平台 | [Worker项目](../../../../../src/DesktopUp.ContentWorker/DesktopUp.ContentWorker.csproj)、[共享探测](../../../../../src/DesktopUp.Windows/Content/LocalContentReferenceProbe.cs) | 取消Compile链接，Worker独立发布载荷不含Infrastructure/SQLite；58个不同定向测试通过（含真实Worker元数据/图标） |
| 未来热度接点 | [打开事实账本](../../../../../src/DesktopUp.Infrastructure/Persistence/Content/SqliteContentOpenJournal.cs)、[现有提案注入夹具](../../../../../tools/review/App/App.ContentHeatRuntimeProbe.cs) | 已证明合法提案可进入会话并发布；尚未证明真实评分、统计消费或热替换，因为这些能力尚未实现 |

本次只补用户验收记录、静态对应和图文，没有重跑历史探针；复用各最终代码对应证据，不将R2/R3探针说成R4新跑结果。R4两组人工范围为：①新建/行内命名/重命名/跨海报/编排收展；②中心开关、隐藏重显、正常退出重开，内容布局与菜单保持。

## 程序集依赖与合法宿主职责

| 项目 | 直接项目引用 |
| --- | --- |
| Domain | 无 |
| Application | Domain |
| Windows | Application、Domain |
| Infrastructure | Application、Domain、Windows |
| ContentWorker | Application、Windows |
| App | Application、Domain、Infrastructure、Windows；ContentWorker仅作为构建/部署依赖，不引用其输出程序集 |

Infrastructure引用Windows是复用同一份只读平台探测，并非Worker反向读取持久库。App引用Infrastructure在Bootstrap组装中合法；运行时视图/业务旁路由依赖规则约束。工厂的每会话创建、Presenter的每窗口UI资源、原生环境监听器均有明确寿命，不为了消灭所有new而引入服务定位器。

## 后续如何增量开发

真实热度可从“成功打开事实读取端口/游标适配 → 纯统计与评分 → ContentHeatProposal → 会话保护/同代发布”接入，在Bootstrap组装。窗口只接收通知与当前视图，不实现评分或SQL；作者布局、稳定卡片和已有文件事务继续复用。此处是可行接入边界，不是已实现热度功能。

模板、AI和更新器仍按各自能力合同分批实现。只有它们确实需要新的公共边界时才扩展，不能把算法或文件操作重新堆回窗口，也不能用空接口代替实际能力。

OPT-CONTENT-SYNC-01的计时/扫描规模化问题继续待研究；本轮没有优化其调度或证明整体性能提升。原长测、多屏/DPI、真实满盘及真实登录自启等未验项保持，不扩为此次新验收。

## 图表交付回执

两张图均为architecture，9/9 showcase，0错误、0警告。Edge对1440×900、1600×1000、1920×1080、2048×1320的自动检查通过；最大尺寸深浅主题截图已目视核对，节点/标签/线条无相互遮挡，主图及说明卡在首屏完整。自动证据与目视结论分开记录；本轮没有测试交互导出功能。

[完整指纹和状态](delivery.receipts.json)绑定两份图源和HTML。当前图首轮发现三条垂直边标签压节点，统一只调整这些标签的纵向偏移后通过；correction_rounds=1。原目标图未修改，correction_rounds=0。浏览器原始回执在各HTML同目录的visual-check.json，保留visualReview=pending；独立目视结论归本记录，不改自动回执。

重生成入口：

```powershell
node C:/Users/allmightwen/.agents/skills/archify/bin/archify.mjs deliver architecture Docs/A_Project/20_Architecture/ArchitectureReview/R4-Closed/current.architecture.json out/architecture-r4-closed/current.architecture.html --quality showcase --json
node C:/Users/allmightwen/.agents/skills/archify/bin/archify.mjs deliver architecture Docs/A_Project/20_Architecture/ArchitectureReview/target.architecture.json out/architecture-r4-closed/target.architecture.html --quality showcase --json
```

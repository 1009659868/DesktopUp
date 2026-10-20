# 源码职责与架构入口

当前仍为六个生产程序集。2026-10-20 当前：**本轮基础架构整理 USER ACCEPTED / CLOSED；R1～R8、P1～P3均已验。** 用户已授权正式收口，[当前架构图、收口范围与交付限制](../Docs/A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#31-本轮基础架构正式收口与当前架构图2026-10-20)已记录。图稿showcase 9/9通过，浏览器/视觉复验未完成；不与业务验收混记。剩余规模成本和历史锁冲突继续携带，动态模板/AI/更新器等未实现。当前impl/architecture-foundation及未提交成果保留，本轮不改产品、不提交/推送/合并；真实热度实施HOLD，发布/登录自启仍暂停。下方为阶段历史。

| 位置 | 职责与边界 |
| --- | --- |
| Domain/Content | 实体、能力、作者数据、纯布局/提案规则，无外部I/O或UI |
| Application/Features/Content/Open | 成功交接事实、打开/定位用例；每海报视图/资源适配不另造事实库 |
| Application/Features/Content/FileActions、Transfers | 新建、命名、删除、Win菜单、拖入/出及跨海报的应用编排；R5映射新建有独立用例，存储端口不包整段流程 |
| Application/Features/Content/ContentOperationLifetime | 宿主内容操作的活动范围、停止令牌与任务排空；不执行文件操作，不替代每海报会话保护或事务锁 |
| Application/Features/Content/Observation、Lifecycle、Storage | 映射/最小外部同步、启动观察、归还/卸载、位置迁移的业务顺序 |
| Application/Features/Content/Layout | PosterContentSession保护/草稿/提案；ContentPresentationLayout准备帧；ContentInputGeometry同代区域；无窗口或SQLite |
| Application/Ports | 请求、结果、版本/取消及原子操作端口，不暴露任意SQL或窗口私有状态 |
| Infrastructure/Persistence/State、Transfers、Content | SQLite原子事务、打开事实、图标资产、CAS和账本；25份迁移资源冻结，旧记录不重写 |
| Infrastructure各Content/Storage适配 | 文件身份、租约、原子提交及系统执行；复用Windows只读探测；不回调视图私有状态 |
| Infrastructure/ManagedContentMonitor、ContentObservation/DirectoryObservationQueue | P2/P3已验：每配置一个worker/timer，按已验证路由去重排队、每轮最多4根、失联退避，独立周期兜底；调度提示不作为归属/删卡依据，剩余成本携带OPT-CONTENT-SYNC-01 |
| App/Bootstrap | ApplicationComposition显式构造端口、唯一store/Worker；App.Startup按原顺序分流/接线；其余组装文件负责对应应用用例 |
| App/Runtime/Posters、Views/Poster | 每代会话与原生窗口寿命；控件、Dispatcher、输入采集、同代提交/回滚、场景材质适配 |
| App/Presentation | PosterContentPresenter稳定卡片/暖位图；ContentRasterBinding有界需求、订阅及迟到准入 |
| App/App.Lifetime、App.DesktopEnvironment | 停止顺序；原生环境监听/恢复与时钟适配，不含单文件业务 |
| Windows/Content/LocalContentReferenceProbe | 本机元数据探测的单一实现，不执行目标或跟随重解析路径 |
| ContentWorker | 独立Shell/图标进程；只引用Application/Windows，不链接Infrastructure源码、不打开配置库 |
| Modules | 已有Lamp/Rain的声明、场景和适配；不是动态模板平台 |

六程序集的依赖方向：Application→Domain；Infrastructure→Application/Domain/Windows；Windows→Application/Domain；Worker→Application/Windows；App在Bootstrap组装这些实现，运行与视图不直接引用存储。App→Worker只保证独立进程构建，不作为运行时程序集引用。运行时ServiceProvider查询、DI包、两个无用仓储注册、旧Worker源码链接及旧所有者已删除，不保留兼容影子字段。

正常构建不包含独立验收执行器；[tools/review](../tools/review/README.md)仅由DesktopUpReviewTools=true增加同一生产接线外的探针。check-architecture.ps1检查实际生产编译集合（含模块partial）和精确Bootstrap例外，ArchitectureTests检查工程方向与主要调用链。

目录移动和结构变更分开记录：历史纯移动见[A_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/A_MOVES.json)，R4探测文件先字节一致移动再改依赖。源码入口/清理/验证见实施计划，不以代码行数、接口数量或图表通过代替行为证据。

R5退役MappedDirectoryService及四个生产测试包装；映射校验/登记归MappedDirectoryUseCase.Actions，文件创建见证归LocalOwnedContentCreationFiles。测试包装仅在tests/Shared与Contracts.Tests/Support，以DesktopUp.Testing Fixture命名；不编入产品。移动前后字节检查和后续结构修改分开记录于第21节。

R8纯移动67份，清单及原始工作树摘要见[R8_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/R8_MOVES.json)。XAML伴随partial仍在App根目录；不为目录对称引入接口。生产ContentDeletionService及两个拖放适配器的测试Execute/Checkpoint便捷入口已删，实际存储适配保留。该目录归位不计性能收益，也不代表拆分了partial运行类型。

增量开发：规则/顺序落Domain或Application现有用例；确有新副作用才增端口/适配；Bootstrap显式组装一次；窗口仅接交互。R1～R8/P1～P3已验，最终职责/成本复审完成，本轮基础架构已获用户授权正式收口，不无限叠层。剩余全局校验/慢单根/版本竞争不得通过权威缓存、跳过语义检查或删除兜底来掩盖。[当前架构图与收口记录](../Docs/A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#31-本轮基础架构正式收口与当前架构图2026-10-20)明确图的浏览器/视觉复验限制，不与业务验收混记。

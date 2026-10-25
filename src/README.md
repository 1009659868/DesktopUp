# 源码职责与架构入口

2026-10-25：统计消费提交后以`StatisticsChanged`通知UI队列，按当前ContentItemId归属刷新相关海报；单计算合并最新待更新，交互保护释放自动续作，不必收展。实时更新定向与原生结果见[热度实施记录](../Docs/A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md)。

2026-10-24：六个应用/运行时工程（App、Application、Domain、Infrastructure、Windows、ContentWorker），另有独立安装辅助工程 **DesktopUp.Setup**，当前共七个源码csproj。基础架构R1～R8/P1～P3已验收并集成；当前热度工作分支`impl/06-real-activity`基于`impl/develop@d922669`，发布基线`impl/main@ecb0c04`。[近期审计](../Docs/A_Project/40_Implementation/PROJECT_STATUS_AUDIT_2026-10-24.md)保留开工基线；工作树已接真实统计/producer、连续空间及配置，状态 **IN PROGRESS**，两批定向共211项通过、隔离生产链与UI保存/取消/重启验证已通过，待集中人工体验，唯一详细C为[真实热度实施记录](../Docs/A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md)。未作USER PASS/完成结论；动态模板/在线AI/在线更新器及历史成本、锁冲突、真实登录未验仍携带。

| 位置 | 职责与边界 |
| --- | --- |
| Domain/Content | 实体、能力、作者数据；ContentActivityPolicy批准衰减/低样本平滑及ContentHeatSizing连续空间投影，无外部I/O或UI |
| Application/Features/Content/Open | 成功交接事实、打开/定位用例；ContentActivityConsumer事件触发有界增量消费，不重放真实打开 |
| Application/Features/Content/FileActions、Transfers | 新建、命名、删除、Win菜单、拖入/出及跨海报的应用编排；R5映射新建有独立用例，存储端口不包整段流程 |
| Application/Features/Content/ContentOperationLifetime | 宿主内容操作的活动范围、停止令牌与任务排空；不执行文件操作，不替代每海报会话保护或事务锁 |
| Application/Features/Content/Observation、Lifecycle、Storage | 映射/最小外部同步、启动观察、归还/卸载、位置迁移的业务顺序 |
| Application/Features/Content/Layout | ContentActivityProducer从统计/成员快照生成真实连续提案；PosterContentSession保护/草稿/提案；ContentPresentationLayout准备帧；ContentInputGeometry同代区域；无窗口或SQLite |
| Application/Ports | 请求、结果、版本/取消及原子操作端口，不暴露任意SQL或窗口私有状态 |
| Infrastructure/Persistence/State、Transfers、Content | SQLite原子事务、打开事实、图标资产、CAS和账本；工作树schema27新增显式待消费序列/位置、按ContentItemId聚合及每对象最近32条历史，每批最多128条同事务消费；001～026迁移冻结，不重写旧事实 |
| Infrastructure各Content/Storage适配 | 文件身份、租约、原子提交及系统执行；复用Windows只读探测；不回调视图私有状态 |
| Infrastructure/ManagedContentMonitor、ContentObservation/DirectoryObservationQueue | P2/P3已验：每配置一个worker/timer，按已验证路由去重排队、每轮最多4根、失联退避，独立周期兜底；调度提示不作为归属/删卡依据，剩余成本携带OPT-CONTENT-SYNC-01 |
| App/Bootstrap | ApplicationComposition显式构造端口、唯一store/Worker；App.Startup按原顺序分流/接线；其余组装文件负责对应应用用例 |
| App/Runtime/Posters、Views/Poster | 每代会话与原生窗口寿命；RefreshActivityAtSafePoint/PosterSpatialRuntime.Activity承接生产提案、共用扩框与限幅；控件、Dispatcher、同代提交/回滚、场景材质适配 |
| App/Presentation | PosterContentPresenter稳定卡片/暖位图；ContentRasterBinding有界需求、订阅及迟到准入 |
| App/App.Lifetime、App.DesktopEnvironment | 停止顺序；原生环境监听/恢复与时钟适配，不含单文件业务 |
| Windows/Content/LocalContentReferenceProbe | 本机元数据探测的单一实现，不执行目标或跟随重解析路径 |
| ContentWorker | 独立Shell/图标进程；只引用Application/Windows，不链接Infrastructure源码、不打开配置库 |
| Setup | 独立安装事务辅助程序，无其他源码工程ProjectReference；固定用户安装身份、新装/原位/换路径升级、失败恢复及入口登记，不读取配置库/搬收纳内容，不是在线Updater |
| Modules | 已有Lamp/Rain的声明、场景和适配；不是动态模板平台 |

六个应用/运行时工程的依赖方向：Application→Domain；Infrastructure→Application/Domain/Windows；Windows→Application/Domain；Worker→Application/Windows；App在Bootstrap组装这些实现，运行与视图不直接引用存储。App→Worker只保证独立进程构建，不作为运行时程序集引用。Setup由打包脚本独立发布、安装器调用，不加入上述业务依赖链。运行时ServiceProvider查询、DI包、两个无用仓储注册、旧Worker源码链接及旧所有者已删除，不保留兼容影子字段。

本批热度规则已批准：加入30天/最近打开14天双保护、7～28天连续半衰期/10%下限；同ContentItemId转移保留，重加不按路径继承；海报级可配置平滑默认0.5～3/无数据1.5/K=20。整体倍率与逐项热度独立，ContentHeatExpansion复用空间规划，热度受阻限幅、手动尺度受阻回弹；用户resize重投影已接受目标，不触发同目标撑回。ControlCenterWindow.Activity接现有草稿所有权/版本/空间保护，成功保存后关闭、取消不写；保留现有模式、动态缩放开关和手工覆盖。源码接线与原生验收分开，详细公式、证据及未验边界归A/C，不在此建立第二份实施记录。

正常构建不包含独立验收执行器；[tools/review](../tools/review/README.md)仅由DesktopUpReviewTools=true增加同一生产接线外的探针。check-architecture.ps1检查实际生产编译集合（含模块partial）和精确Bootstrap例外，ArchitectureTests检查工程方向与主要调用链。

目录移动和结构变更分开记录：历史纯移动见[A_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/A_MOVES.json)，R4探测文件先字节一致移动再改依赖。源码入口/清理/验证见实施计划，不以代码行数、接口数量或图表通过代替行为证据。

R5退役MappedDirectoryService及四个生产测试包装；映射校验/登记归MappedDirectoryUseCase.Actions，文件创建见证归LocalOwnedContentCreationFiles。测试包装仅在tests/Shared与Contracts.Tests/Support，以DesktopUp.Testing Fixture命名；不编入产品。移动前后字节检查和后续结构修改分开记录于第21节。

R8纯移动67份，清单及原始工作树摘要见[R8_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/R8_MOVES.json)。XAML伴随partial仍在App根目录；不为目录对称引入接口。生产ContentDeletionService及两个拖放适配器的测试Execute/Checkpoint便捷入口已删，实际存储适配保留。该目录归位不计性能收益，也不代表拆分了partial运行类型。

增量开发：规则/顺序落Domain或Application现有用例；确有新副作用才增端口/适配；Bootstrap显式组装一次；窗口仅接交互。R1～R8/P1～P3已验，最终职责/成本复审完成，本轮基础架构已获用户授权正式收口，不无限叠层。剩余全局校验/慢单根/版本竞争不得通过权威缓存、跳过语义检查或删除兜底来掩盖。[当前架构图与收口记录](../Docs/A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#31-本轮基础架构正式收口与当前架构图2026-10-20)明确图的浏览器/视觉复验限制，不与业务验收混记。

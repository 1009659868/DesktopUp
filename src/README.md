# 源码职责与架构入口

当前六个生产程序集。R1～R4 USER PASS / CLOSED，现有业务基础架构出口通过；真实热度尚未开工，动态模板/AI/独立更新器未实现。状态见[基础架构记录](../Docs/A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#r4-composition-boundary)，[当前图与目标对照](../Docs/A_Project/20_Architecture/ArchitectureReview/R4-Closed/README.md)给出实际职责和未来差距。

| 位置 | 职责与边界 |
| --- | --- |
| Domain/Content | 实体、能力、作者数据、纯布局/提案规则，无外部I/O或UI |
| Application/Features/Content/Open | 成功交接事实、打开/定位用例；每海报视图/资源适配不另造事实库 |
| Application/Features/Content/FileActions、Transfers | 新建、命名、删除、Win菜单、拖入/出及跨海报的应用编排；平台/事务经窄端口 |
| Application/Features/Content/Observation、Lifecycle、Storage | 映射/最小外部同步、启动观察、归还/卸载、位置迁移的业务顺序 |
| Application/Features/Content/Layout | PosterContentSession保护/草稿/提案；ContentPresentationLayout准备帧；ContentInputGeometry同代区域；无窗口或SQLite |
| Application/Ports | 请求、结果、版本/取消及原子操作端口，不暴露任意SQL或窗口私有状态 |
| Infrastructure/Persistence | SQLite原子事务、打开事实、图标资产、CAS和账本；25份迁移资源冻结，旧记录不重写 |
| Infrastructure各Content/Storage适配 | 文件身份、租约、原子提交及系统执行；复用Windows只读探测；不回调视图私有状态 |
| Infrastructure/ManagedContentMonitor | 既有监听/兜底定时器；规模化议题OPT-CONTENT-SYNC-01待实测，不在R4改策略 |
| App/Bootstrap | ApplicationComposition显式构造端口、唯一store/Worker；App.Startup按原顺序分流/接线；其余组装文件负责对应应用用例 |
| App/WinUiPosterSession、PosterVisualWindow | 每代会话与原生窗口寿命；控件、Dispatcher、输入采集、同代提交/回滚、场景材质适配 |
| App/Presentation | PosterContentPresenter稳定卡片/暖位图；ContentRasterBinding有界需求、订阅及迟到准入 |
| App/App.Lifetime、App.DesktopEnvironment | 停止顺序；原生环境监听/恢复与时钟适配，不含单文件业务 |
| Windows/Content/LocalContentReferenceProbe | 本机元数据探测的单一实现，不执行目标或跟随重解析路径 |
| ContentWorker | 独立Shell/图标进程；只引用Application/Windows，不链接Infrastructure源码、不打开配置库 |
| Modules | 已有Lamp/Rain的声明、场景和适配；不是动态模板平台 |

六程序集的实际方向与合法宿主例外见[本轮边界表及现状图](../Docs/A_Project/20_Architecture/ArchitectureReview/R4/README.md)。运行时ServiceProvider查询、DI包、两个无用仓储注册、旧Worker源码链接及旧所有者已删除，不保留兼容影子字段。

正常构建不包含独立验收执行器；[tools/review](../tools/review/README.md)仅由DesktopUpReviewTools=true增加同一生产接线外的探针。check-architecture.ps1检查实际生产编译集合（含模块partial）和精确Bootstrap例外，ArchitectureTests检查工程方向与主要调用链。

目录移动和结构变更分开记录：历史纯移动见[A_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/A_MOVES.json)，R4探测文件先字节一致移动再改依赖。源码入口/清理/验证见实施计划，不以代码行数、接口数量或图表通过代替行为证据。

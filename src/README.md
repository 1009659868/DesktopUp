# 源码职责与首批整理入口

当前仍是六个生产程序集，命名空间未全库改名。A完成目录整理；B抽出打开/定位服务；C抽出热度派生运行状态，不意味着所有内容事务或窗口已解耦。

当前继续impl/architecture-foundation，按[基础架构余项](../Docs/A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md)补齐已有业务的用例、会话、呈现/命中及组装边界后再审查真实热度准入。下表是当前已实现职责，不是余项目标已完成。

| 位置 | 当前职责与约束 |
| --- | --- |
| DesktopUp.Domain/Content | 内容实体、能力、图标资产规则；无外部I/O或UI依赖 |
| DesktopUp.Application/Features/Content/Open | ContentActionService负责打开/定位执行与事实完成；ReferenceContentActions只观察结果，ReferenceContentRuntime负责每海报视图/图标缓存 |
| DesktopUp.Application/Features/Content/FileActions | R1新建/改名/删除/Win菜单编排、结果登记与索引策略；通过Ports访问事务与文件/Shell适配，不引用SQLite/WinUI |
| DesktopUp.Application/Features/Content/Transfers | 普通/映射拖入、跨海报交接、原生拖出观察的执行次序、取消与结果发布；不含OLE实现、具体数据库或窗口 |
| DesktopUp.Application/Features/Content/Layout | ContentHeatSession持有缓冲/已接受热度/派生文档/档位；不含UI、调度器或存储，原Domain提案/求解规则不改 |
| DesktopUp.Application/Ports/Content | 来源/Worker/Shell UI请求结果协议；不依赖具体数据库/WinUI |
| DesktopUp.Infrastructure/Persistence/Content | 打开成功事实与图标存储的SQLite实现 |
| DesktopUp.Infrastructure/ContentActions | 文件动作原子准备/提交/观察、执行租约与本机文件身份；原三个Service兼容入口转向Application用例 |
| DesktopUp.Infrastructure/ContentTransfers与原拖放账本 | 普通准备端口及原安全执行器；CrossPosterTransfer/MappedDirectoryIngress的attempt保留原事务/身份锁，Execute兼容入口转向同一Application用例 |
| DesktopUp.App/Bootstrap | 服务注册和真实内容组装；按原启动次序创建应用级执行服务，原Worker仍由App管理 |
| DesktopUp.App其余根目录/Scenes | 现有窗口、空间会话和呈现；C只将热度状态抽出，交互保护与同帧发布继续归窗口，不假称已全部拆成独立组件 |
| DesktopUp.Windows | 原生窗口、输入、拖放和桌面适配；本批未搬动 |
| DesktopUp.ContentWorker | 独立Shell/图标执行进程；本批未改变协议和路径 |

六个工程的依赖门在现有ArchitectureTests中。正常App构建不包含独立验收执行器，开发验收源在[tools/review](../tools/review/README.md)，显式编译开关为DesktopUpReviewTools=true。原始生产诊断、来源探测、安全账本继续保留。

暂存例外：文件动作首片与本批拖放生产入口已退出具体存储接线，但映射创建/同步、归还/配置等仍持有SqliteStateStore，旧确定性Transfers/Outgoing仅Review调用但尚在产品编译集合，留待R4；Infrastructure启动收尾及Worker链接LocalContentReferenceProbe也待整理。MappedPosterCreation只是原方法独立移动，并未完成解耦。新功能不可扩大这些例外；整体R1～R4未关闭，不引入空接口或插件框架。

目录移动与结构接线分别记录在[A批](../Docs/A_Project/40_Implementation/Architecture/A_FOUNDATION.md)和[A_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/A_MOVES.json)。后续服务/运行状态抽取见[B批](../Docs/A_Project/40_Implementation/Architecture/B_CONTENT_USE_CASES.md)与[C批](../Docs/A_Project/40_Implementation/Architecture/C_HEAT_RUNTIME.md)，不混作纯文件移动；其余目录按实际用例逐步归组，不做一次性全库改名。

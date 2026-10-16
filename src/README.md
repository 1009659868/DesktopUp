# 源码职责与首批整理入口

当前仍是六个生产程序集，命名空间未全库改名。A完成目录整理；B抽出打开/定位服务；C抽出热度派生运行状态，不意味着所有内容事务或窗口已解耦。

| 位置 | 当前职责与约束 |
| --- | --- |
| DesktopUp.Domain/Content | 内容实体、能力、图标资产规则；无外部I/O或UI依赖 |
| DesktopUp.Application/Features/Content/Open | ContentActionService负责打开/定位执行与事实完成；ReferenceContentActions只观察结果，ReferenceContentRuntime负责每海报视图/图标缓存 |
| DesktopUp.Application/Features/Content/Layout | ContentHeatSession持有缓冲/已接受热度/派生文档/档位；不含UI、调度器或存储，原Domain提案/求解规则不改 |
| DesktopUp.Application/Ports/Content | 来源/Worker/Shell UI请求结果协议；不依赖具体数据库/WinUI |
| DesktopUp.Infrastructure/Persistence/Content | 打开成功事实与图标存储的SQLite实现 |
| DesktopUp.App/Bootstrap | 服务注册和真实内容组装；按原启动次序创建应用级执行服务，原Worker仍由App管理 |
| DesktopUp.App其余根目录/Scenes | 现有窗口、空间会话和呈现；C只将热度状态抽出，交互保护与同帧发布继续归窗口，不假称已全部拆成独立组件 |
| DesktopUp.Windows | 原生窗口、输入、拖放和桌面适配；本批未搬动 |
| DesktopUp.ContentWorker | 独立Shell/图标执行进程；本批未改变协议和路径 |

六个工程的依赖门在现有ArchitectureTests中。正常App构建不包含独立验收执行器，开发验收源在[tools/review](../tools/review/README.md)，显式编译开关为DesktopUpReviewTools=true。原始生产诊断、来源探测、安全账本继续保留。

暂存例外：App会话的文件业务仍持有具体SqliteStateStore、Infrastructure中仍有用例编排、Worker链接LocalContentReferenceProbe。B只让打开账本退出空间运行器、执行退出视图适配器；新功能不可扩大其余例外。不引入空接口或运行时插件框架，原未使用的IContentOpenDispatcher已退役，执行统一走IContentActionService。

目录移动与结构接线分别记录在[A批](../Docs/A_Project/40_Implementation/Architecture/A_FOUNDATION.md)和[A_MOVES.json](../Docs/A_Project/40_Implementation/Architecture/A_MOVES.json)。后续服务/运行状态抽取见[B批](../Docs/A_Project/40_Implementation/Architecture/B_CONTENT_USE_CASES.md)与[C批](../Docs/A_Project/40_Implementation/Architecture/C_HEAT_RUNTIME.md)，不混作纯文件移动；其余目录按实际用例逐步归组，不做一次性全库改名。

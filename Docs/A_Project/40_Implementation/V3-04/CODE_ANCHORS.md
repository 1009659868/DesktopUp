# V3-04 代码锚点

## 2026-10-24 当前源码与热度接入锚点

基线`impl/develop@d922669`，以下入口已补入`impl/06-real-activity`工作区实现。旧日期条目保留原提交的路径/符号，不保证适用于现HEAD；职责总览见[src/README](../../../../src/README.md)，热度验证进度见[V3-06记录](../V3-06/REAL_ACTIVITY_DELIVERY.md)。

| 当前入口 | 已存在的能力 / 下一接入边界 |
| --- | --- |
| [ContentActionService](../../../../src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs) / [ContentOpenContract](../../../../src/DesktopUp.Application/Features/Content/Open/ContentOpenContract.cs) | `OpenAsync`统一打开编排，`Complete`限定可信成功交接事实，不把Reveal、扫描或无成功证明的Handled计数 |
| [IContentOpenJournal](../../../../src/DesktopUp.Application/Ports/Content/IContentOpenJournal.cs) / [SqliteContentOpenJournal](../../../../src/DesktopUp.Infrastructure/Persistence/Content/SqliteContentOpenJournal.cs) | `TryStart`去重，`Complete`原子写完成与成功事实；027的触发器将新事实加入正式统计队列 |
| [ContentActivityConsumer](../../../../src/DesktopUp.Application/Features/Content/Open/ContentActivityConsumer.cs) / [SqliteContentActivityStore](../../../../src/DesktopUp.Infrastructure/Persistence/Content/SqliteContentActivityStore.cs) | 有界增量消费、统计/游标原子提交、重启幂等，不重放真实打开 |
| [ContentActivityPolicy](../../../../src/DesktopUp.Domain/Content/ContentActivity.cs) / [ContentActivityProducer](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentActivityProducer.cs) | 连续衰减/评分/可配置平滑，生产连续提案v2；算法与I/O分开 |
| [ContentHeatSession](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentHeatSession.cs) / [ContentHeatSizing](../../../../src/DesktopUp.Domain/Content/ContentHeatSizing.cs) | 保留目标、从作者基准按当前视口限幅；v1旧注入仍兼容五档，不走生产v2 |
| [PosterVisualWindow.Activity](../../../../src/DesktopUp.App/Views/Poster/PosterVisualWindow.Activity.cs) / [PosterSpatialRuntime.Activity](../../../../src/DesktopUp.App/Runtime/Posters/PosterSpatialRuntime.Activity.cs) | 生产计算、安全时点/版本门与原生扩框；成功打开不立即挪卡片，用户resize不重新排名 |
| [ContentScaleExpansion](../../../../src/DesktopUp.Application/ContentScaleExpansion.cs) / [ContentHeatExpansion](../../../../src/DesktopUp.Application/ContentHeatExpansion.cs) / [ContentFrameMinimumPolicy](../../../../src/DesktopUp.Domain/ContentFrameMinimumPolicy.cs) | 共用空间规划；手动尺度受阻回弹，热度受阻限幅，允许外框降至热度/可读下限 |
| [ControlCenterWindow.Activity](../../../../src/DesktopUp.App/ControlCenterWindow.Activity.cs) | 行为页海报级倍率/K/尺寸开关；沿既有草稿/CAS，取消无写入 |
| [PosterContentPresenter](../../../../src/DesktopUp.App/Presentation/PosterContentPresenter.cs) / [App组装](../../../../src/DesktopUp.App/Bootstrap/ApplicationComposition.cs) | 增量呈现/局部悬停及单次组合根；新策略不另造窗口状态库或整表图标重载 |
| [SqliteStateStore](../../../../src/DesktopUp.Infrastructure/Persistence/State/SqliteStateStore.cs) / [迁移目录](../../../../src/DesktopUp.Infrastructure/Persistence/Migrations/catalog.xml) | 当前schema27；027新增统计队列/游标/聚合与有限历史，001～027及指纹冻结 |
| [StartupIntent](../../../../src/DesktopUp.Domain/StartupIntent.cs) / [PosterConfigurationFactory](../../../../src/DesktopUp.Domain/PosterConfigurationFactory.cs) / [SqlitePosterCatalogRepository](../../../../src/DesktopUp.Infrastructure/Persistence/State/SqlitePosterCatalogRepository.cs) | `ShouldShowControlCenter`、基础收纳默认与`InitializeEmptyProfile`；登录自启保持后台，普通零海报启动开中心 |
| [Setup](../../../../src/DesktopUp.Setup/DesktopUp.Setup.csproj) / [UpgradeTransaction](../../../../src/DesktopUp.Setup/UpgradeTransaction.cs) | 独立安装辅助工程，当前单安装/原位/迁移升级共用事务；无业务工程ProjectReference，不是在线Updater |

可复用的定向测试入口：`tests/DesktopUp.Contracts.Tests/ContentActivity*Tests.cs`、`RealContentOpenTests.cs`、`ContentHeatSessionTests.cs`、`ContentFrameResizeTests.cs`及`ContentScaleExpansionTests.cs`。本批结果只在V3-06记录登记，未验的原生/人工不在此补PASS。

## 2026-10-23 唯一安装与迁移升级

两组集中人工USER PASS / CLOSED；功能f6a09c2→develop876293d→精简main ecb0c04已推送，preview-20261023.1准确绑定20261023.5包源码及附件，已回develop。发布只裁剪测试Checkpoint等既有非产品入口；证据统一见HANDOFF及发布交付，历史未定因不销项。

- `DesktopUp.Setup/InstallationIdentity.cs`：枚举固定/历史登记后验证唯一身份，Run 值只按已知原命令切换，不改 StartupApproved。
- `InstallationMetadata.cs`：固定 DesktopUp 卸载身份、历史键兼容、快捷方式及用户安装事务目录。
- `UpgradeTransaction.cs`：新装/原位/跨路径统一事务；旧根本卷备份、新根本卷准备、全局 pending 索引、旧 schema1 恢复兼容；不读取数据库或搬收纳内容。
- `Program.cs` 与 `packaging/preview/installer.nsi`、`upgrade.nsh`：安装/卸载维护租约、唯一安装发现、用户可选择目标目录；撤原 NSIS 独立新装写登记双路径。
- `WindowsStartupRegistration.cs`：固定登记优先、旧版回读，同时检查版本与路径。
- `InstallerUpgradeTests` 及 `tests/Packaging/New-SingleInstallFixture.ps1`、`Review-SingleInstall.ps1`：本批定向和独立身份的安装器/集中人工夹具；不对日常安装运行升级或卸载。实际状态见 [本批记录](SINGLE_INSTALL_MIGRATION.md)。

## 2026-10-23 安装包原位升级（USER PASS / CLOSED）

- `src/DesktopUp.Setup/InstallationFiles.cs`：清单、摘要与路径归属；`InstallationMetadata.cs`：版本登记及自有快捷方式适配；`UpgradeTransaction.cs`：暂存/备份/替换/回退/明确恢复，精确清理；`Program.cs`：维护租约、独立诊断及进程入口，不调用内容/数据库业务。
- `packaging/preview/upgrade.nsh`：同通道识别、原路径选择、辅助进程与恢复UI；`installer.nsi`：复用新装路径保护，旧安装转升级，不调用旧卸载器。`scripts/package-preview.ps1`：独立发布Setup，共用包内运行时，保持源码/载荷绑定。
- `InstallerUpgradeTests` 与 `tests/Packaging/Test-InstallerUpgrade.ps1`：本批直接风险；`scripts/start-installer-upgrade-review.ps1`：两组隔离人工及可恢复清理。[实现/证据范围](INSTALLER_UPGRADE_DELIVERY.md)；未替换历史卸载即时取证及其安全保护。

## 2026-10-23 新增海报默认确认

- 发布绑定：功能 `52529a1` → 集成 `508d7ec` → 精简 main/tag `7160078` / `preview-20261023.1`，已推送发布；两项产品源码一致，回归与打包证据见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-23-两项海报修复与新预发布)。开发文档/测试不进入发布树。
- `src/DesktopUp.Domain/ContentFrameMinimumPolicy.cs`：有内容主轴使用 48 DIP 可读内容加装饰，交叉轴仍完整容纳最大 Item，空海报保持 240；拖框/控制中心/提交沿既有共享入口。`ContentFrameResizeTests` 覆盖方向切换后的双向往返及同一约束，不改循环策略。
- `src/DesktopUp.App/ControlCenterWindow.Editing.cs / ShowCatalogDialog`：仅 create 的 DefaultButton 为 Primary；名称非空校验和原目录命令复用不变，其他分支保留原默认按钮。[本批验证（USER PASS / CLOSED）](CONTROL_CENTER_USABILITY_DELIVERY.md#2026-10-23-新增海报回车确认)。

## 2026-10-21 集成后真实热度准备锚点（仅核对，未实施）

- Application/Features/Content/Open/ContentActionService、ContentOpenContract → Infrastructure/Persistence/Content/SqliteContentOpenJournal：已有可信打开交接、ActivationId幂等事实；尚无生产统计聚合/消费游标。
- Domain/ContentLayoutProposals：已有带版本提案，但注入路径仍五档Quantize/Math.Round；不能当作已实现的真实连续热度。Application/Features/Content/Layout/ContentHeatSession及PosterContentSession提供最新提案、会话/交互保护与作者意图区分。
- 当前生产源仍为本批已验集成，无热度代码变化；方案、待决规则和接续顺序见[主计划第7.1节](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#71-2026-10-21-真实热度准备未实施)。发布及清理记录归PREVIEW_INSTALLER_DELIVERY，不在此复制证据。

## 2026-10-21 卸载即时取证

- packaging/preview/uninstall-evidence.nsh：替代旧DeleteInstalledFile宏的清单单路径删除边界，原始API错误即时捕获；失败后有界只读RM快照、不可覆盖的profile报告。installer.nsi只接入include及错误提示/marker，不改内容归还、维护租约和归属保护。
- tests/Packaging/Test-UninstallEvidence.ps1及Fixtures/uninstall-evidence.nsi：六个自建文件直接风险边界；Test-UninstallFailure.ps1复用实际包原三段并保存首次实际退出与报告，不新增多轮人工矩阵。
- 当前候选、结果和未定因限制见[本批记录](UNINSTALL_IMMEDIATE_EVIDENCE.md)；下方“原Delete宏”是被本批替代的历史入口。

## 2026-10-21 连续尺寸与悬停

追加修订：Application/ContentScaleExpansion 复用 ControlCenterSpatial 规划并由 StateCommandPolicy 在同一事务内复核尺度+几何；Runtime/PosterSpatialRuntime.Usability 提供当前空间及预览/提交接线；PosterVisualWindow.ControlCenter 保存同版/同代原框供取消恢复；ControlCenterSettingsSession.RejectPreview 的 discard 分支仅释放被拒绝的滑块草稿，精细文字草稿保护不变。缩放步长同时修正运行时初始化覆盖 XAML 的旧 0.01 值为 0.001。

- Domain：PosterContentPreferences 整体 0.4～3；ContentLayout/Editing/DragPolicy 小数尺寸及 JSON v4；Solver 保守整数占位与精确绘制；ContentFrameMinimumPolicy 动态交叉轴。
- Application：PortableContentLayout 保留小数；IncomingContentPlacement/IncomingLayoutPreview 使用整数占位推进下一落点，不改变文件移动。
- App：ControlCenterWindow 的尺度、尺寸字段及动态下限文案；PosterContentPresenter 保留卡片局部悬停；PosterVisualWindow.Content 同代几何校验。
- Windows：PosterInputIslandManager 复用鼠标移动/离开事件，无新增计时器。
- Persistence：Migrations/026.sql 与 catalog.xml；SqliteStateStore 只按显式 statement-break 分步执行新迁移，共享事务；001～025 原字节不变。
- 直接风险验证、隔离迁移、候选/脚本与验收入口统一见[本批记录](POSTER_SCALE_AND_HOVER.md)，不增设每小步验收。

## 2026-10-21 卸载失败定位

已验功能c8a6714已进入develop940ff96；发布main4e79035同源同步，最终包编译基点9e84ad2。实际打包/卸载验证及源码绑定见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-21-已验卸载诊断集成与发布同步)，以下锚点不变。

- `packaging/preview/installer.nsi`：DeleteInstalledFile宏、uninstall_failed记录/提示；原Delete/失败即停语义不变。
- `scripts/package-preview.ps1`：按原清单顺序生成上述宏调用。
- `tests/Packaging/Test-UninstallFailure.ps1`：受控占用、实际执行码、释放后再次执行及普通卸载；-ReviewFailure只将受控失败段交人工核对。根因未定边界和证据见[本批记录](UNINSTALL_INTERRUPTION.md)。

## 2026-10-20 已验架构Git集成与发布对齐（当前）

- 开发产品锚点继续沿用本轮收口内容：架构`3c2417e`已进入develop`bbbe417`；发布`fe5741d`来自相同业务源码，不把开发资料裁剪回灌。
- 发布入口：`scripts/build.ps1`、`scripts/package-preview.ps1`、`scripts/validate-source-text.ps1`、`packaging/preview/installer.nsi`；最终包源码绑定干净`74e5088`，之后仅README验证说明。迁移资源、默认配置、内置模块与运行依赖保留。
- 发布分支仅剔除开发执行器/测试通道、诊断回调/读回和友元测试声明；ControlCenterService的发布CurrentWindow对应开发WindowForPortableProbe，调用接线同步，未替换用例或存储逻辑。比较/构建/运行及保留问题见[发布验证](PREVIEW_INSTALLER_DELIVERY.md#2026-10-20-架构已验成果集成与发布同步)。

## 2026-10-20 基础架构正式收口与当前图（当前）

- 本轮USER ACCEPTED / CLOSED；[收口记录与图稿边界](../Architecture/FOUNDATION_REMAINDER_PLAN.md#31-本轮基础架构正式收口与当前架构图2026-10-20)。本轮没有新的产品代码或依赖变化，下方第30节源码锚点继续适用。
- [当前图源](../../20_Architecture/ArchitectureReview/Foundation-Closed/current.architecture.json)：9个职责节点、8条主要运行协作关系；内置场景明确为编译期，不把未来热度/模板/AI/更新器画成已实现。
- [交付及源码证据](../../20_Architecture/ArchitectureReview/Foundation-Closed/delivery.receipt.json)：24份实际工作区源码摘要；HEAD仅为基础检查点，包含未提交变化，未伪造远程源码链接。箭头不是全量程序集引用图。
- [HTML产物](../../../../out/architecture-foundation-closed-20261020/current.architecture.html)确定性检查9/9通过；浏览器/视觉复验未完成，详见VERIFICATION。R4图源不再作为当前图。

## 2026-10-20 最终架构与成本复审（历史复审，代码锚点仍有效）

R1～R8/P1～P3 USER PASS / CLOSED；本轮只更新记录，未修改下列实现。完整目标对照、依赖方向及后续处理条件见[第30节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#30-p2p3收口后的最终架构与成本复审2026-10-20)。

- App/Bootstrap/App.ContentServices.cs、ApplicationComposition.cs：实际用例与适配接线/资源所有者；App/App.Lifetime.cs按屏障顺序停止并排空。
- Application/Features/Content/FileActions/MappedContentCreationUseCase.cs、ContentOperationLifetime.cs：业务顺序及文件/宿主操作生命周期，不将完整流程留在存储端口内。
- Application/Features/Content/Layout/PosterContentSession.cs、ContentPresentationLayout.cs；App/Presentation/PosterContentPresenter.cs、Views/Poster/PosterVisualWindow.ContentPublication.cs：状态/保护、纯帧、UI资源、同代原生发布各自所有者。
- Infrastructure/ContentObservation/SqliteContentStartupSteps.cs：现有持久收尾兼容桥；总体启动顺序归Application/Features/Content/Observation/ContentStartupUseCase.cs，桥不得承接新政策。
- Infrastructure/Persistence/State/SqliteStateStore.cs的ExecuteRead/Write：全局_gate、连接与校验成本仍在；不是已定因的历史执行锁冲突。
- Application/Features/Content/Observation/ManagedContentSyncUseCase.cs与Infrastructure/ContentObservation/SqliteManagedContentObservationStorage.cs：无卡片变化也可能SaveEvidence写事务，完整扫描/全局版本稳定见证及提交前复核保持；是否优化先测量。
- Application/Features/Content/Open/ContentActionService.cs、Ports/Content/IContentOpenJournal.cs：现有成功事实写入；尚无生产热度消费游标/评分器，本轮不新增。
- tests/DesktopUp.Contracts.Tests/ArchitectureTests.cs、scripts/check-architecture.ps1：职责规则与实际编译集合；检查通过不等同规模性能认证。

## 2026-10-20 P3按目录观察与有界公平调度（历史交付，现已人工通过）

- Infrastructure/ContentObservation/DirectoryObservationQueue.cs：内部路由有界队列，去重FIFO/每批最多4根由monitor调用、独立兜底入队、失联退避与扫描期间事件保护；不持有文件权限或状态证据。
- Infrastructure/ManagedContentMonitor.cs：单worker/timer，RefreshTargets与完整Invalidate分离；替换旧全局dirty位，按根注册/退避，串行调用范围核对，退出取消与watcher释放。App/App.Transfers.cs投影发布改用RefreshTargets，避免自己的提交触发所有根扫描。
- Application/Ports/Content/ContentObservationPorts.cs、Features/Content/Observation/{ContentObservationService,ManagedContentSyncUseCase,MappedDirectoryUseCase}.cs：可选路径范围仅匹配新鲜登记/绑定，逐根重试反馈；提交安全逻辑不变，默认null仍完整核对。
- Infrastructure/ContentObservation/SqliteMappedDirectoryStorage.ReadObservation：本批目录证据准备；全局语义和跨根关系仍验证，提交后重读与提交前新鲜保护保留。
- tests/DesktopUp.Contracts.Tests/DirectoryObservationQueueTests、ContentMonitorDiscoveryTests及现有MappedObservationReadTests/ManagedContentSyncTests：新增调度/范围风险并复用旧安全覆盖。SessionFixture/DirectoryObservationCostFixture.cs为同一新旧测量入口--architecture-directory-cost；脚本start-architecture-observation-review.ps1新增-Batch P3，旧批入口不变。
- 当前CODE/AUTO、集中人工待验，详细证据/摘要/两组步骤归[第29节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#29-p3按目录观察与有界公平调度2026-10-20)。无本批目录移动/分支合并，P2人工仍待确认；下方为历史。

## 2026-10-20 P2目标发现与无变化投影（历史）

- Infrastructure/ManagedContentMonitor.cs：目标失效与内容脏位分离；启动/显式失效/监听错误/独立周期读取目标；缺失watcher按原tick重挂接，递归模式变化替换，不新增计时器。失败保留有效监听并停止本轮核对。
- App/App.Transfers.cs InvalidateObservationTargets、App/App.xaml.cs ApplyStateProjection：利用既有投影发布与IContentMonitor.Invalidate通知目录相关变化；系统恢复和启动入口保留。没有具体存储依赖外溢。
- Application/StateProjectionCoordinator.cs：本批未修改。未变化对象复用已存在，完整新鲜读取/同Revision损坏拒绝保留，不另建跨轮投影缓存。
- tests/DesktopUp.Contracts.Tests/ContentMonitorDiscoveryTests.cs、ReadObservationCostTests.cs：4项实际worker/FSW边界与7项已验证读取保护，本批11项PASS。SessionFixture/ReadObservationCostFixture.cs新增63秒发现观测模式，保留旧33秒默认；没有生产计数钩子。
- scripts/start-architecture-observation-review.ps1 -Batch P2：正常候选/实际monitor探针，R1/R7/P1默认不变。状态及唯一详细证据：[第28节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#28-p2目标发现与无变化投影成本2026-10-20)，CODE/AUTO、集中人工待验；下方为历史。

## 2026-10-20 P1同轮安全共享读取（历史）

- Application/Ports/Content/ContentObservationPorts.cs：MappedObservationRead/Entry为本轮脱离事务的数据及逐目录错误；IMappedDirectoryStorage.ReadObservation接受取消令牌。
- Infrastructure/ContentObservation/SqliteMappedDirectoryStorage：沿ExecuteRead完整校验，在短事务共享ReadSnapshot、ReadStorageLocations和目录证据；Root关系检查复用，原新鲜入口仍服务创建/动作/提交。
- Application/Features/Content/Observation/MappedDirectoryUseCase.Reconcile：移除逐目录重复读取；保留租约/二次扫描/新鲜Evidence/CAS，提交后重建上下文，无持久缓存。
- Infrastructure/MappedDirectoryStore：形状错误/null成员转局部IOException，不把坏证据当空目录；全局校验未放宽。
- tests/DesktopUp.Contracts.Tests/MappedObservationReadTests、ReadObservationCostTests：新的读边界/目录隔离/版本与证据变化/锁拒绝；共22项最终定向范围见第26节。
- scripts/measure-architecture-shared-read.ps1：保留旧fixture03和新candidate02，三对交错、全新隔离目录、记录摘要/环境、失败停止。start-architecture-observation-review.ps1 -Batch P1复用原真实monitor探针并准备两组人工，不改R1/R7默认路径。
- 状态、摘要与证据：[第26节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#26-p1-同轮安全共享读取2026-10-20)，两组集中人工USER PASS / CLOSED；本轮无代码入口变化。后续P2/P3见第27节，尚未开始；下方为归因阶段历史。

## 2026-10-20 P1读取归因（历史，仅测试诊断）

- tests/DesktopUp.SessionFixture/Program.cs：--architecture-attribution进入ReadValidationCostFixture；原R7 --architecture-cost保留。
- ReadObservationCostFixture.SeedMappings：R7原内联准备改为共享方法，P1不再复制样例构造流程；CostPortProxy可选包围计时只用于单独归因，不改变原计数模式。
- ReadValidationCostFixture：原方法反射委托、暖态读基线、单独端口/SQL回调计数、真实隔离提交。先落盘读取证据，失败保存原异常并停止；不重试。
- SqliteExecutionCounter：fixture进程的SQLite auto-extension/trace_v2回调；普通开始、注释/子程序事件分开，只记分类次数，退出解绑自身回调。
- 产品热点位置未改：Persistence/State/SqliteStateStore.VerifyCurrentSchema、SqliteStateCommandRepository.ReadSnapshot；ContentObservation/SqliteMappedDirectoryStorage.ValidateRoot/Evidence及Application/Features/Content/Observation/MappedDirectoryUseCase.Reconcile。完整证据/安全读计划见[第25节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#25-p1-读取与校验成本归因2026-10-20)，不要在窗口添加缓存或跳过校验。

## 2026-10-20 R8目录与最终复审（当前生产路径，已USER PASS）

- App/Runtime/Posters：PosterSpatialRuntime各partial及WinUiPosterSession；原生宿主/交互采集与应用用例接线，不持有SQLite。App/Views/Poster：PosterVisualWindow各partial；布局/卡片仍在Application/Features/Content/Layout与App/Presentation，不因目录移动复制实现。
- Infrastructure/Persistence/State：SqliteStateStore、SqliteStateCommandRepository及内容/海报/空间仓储；Persistence/Transfers：SqliteTransferJournal与SqliteStorageRelocationJournal各partial。Persistence/Migrations资源及逻辑名称未改。
- ContentDeletionService已删；CrossPosterTransfer/MappedDirectoryIngress仅保留存储适配，旧Execute/Checkpoint测试入口删除。tests/Shared/ContentTestServices直接组装Application用例；ContentDeletionTests在本测试内直接组装删除用例，无替代生产包装。
- ArchitectureTests路径锚点、运行宿主非空检查与Worker递归检查防止移动后漏查；scripts/check-architecture.ps1仍核对实际生产/Review编译集合。
- scripts/start-architecture-file-actions-review.ps1 -Batch R8：独立candidate01和样例，不复跑旧Probe；R1/R5/R6路径/默认保留。
- 详细职责/成本后续见[第24节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#24-r8-目录整理与最终架构复审2026-10-20)，纯移动映射见[R8_MOVES.json](../Architecture/R8_MOVES.json)。下方历史根路径按该映射查找，不改写旧图/证据。

## 2026-10-20 R7读取与观察成本

- Application/Ports/Content/ContentObservationPorts.cs：IContentWatchTargetSource只提供当前监听目标，不暴露数据库连接；ContentObservationService只组合两类核对用例并转交发现读取。
- Infrastructure/ContentObservation/SqliteContentWatchTargetSource.cs：一次ExecuteRead共享完整ReadSnapshot与受管登记，保留全部旧解析/校验；SqliteTransferJournal.Recovery.cs的ReadStorageLocations只接受同事务快照。
- Bootstrap/App.ContentObservation.cs：唯一profile monitor接新的读端口；ManagedContentMonitor调度、Managed/Mapped Reconcile、StateProjectionCoordinator均未改。ManagedContentSyncUseCase旧ActivePaths已删，Mapped.ActivePaths仍供跨海报检查。
- tests/DesktopUp.SessionFixture/ReadObservationCostFixture.cs：--architecture-cost同进程旧/新读取对照、1/6/12映射及真实Watcher样例；tests/DesktopUp.Contracts.Tests/ReadObservationCostTests.cs守语义校验、登记解析与目标更新。均非产品诊断框架。
- 集中入口scripts/start-architecture-observation-review.ps1 -Batch R7；默认R1包/配置不变。候选candidate02、最终数据final01和余项统一见[第23节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#23-r7-读取与观察成本2026-10-20)。没有文件移动；不将撤回的窄读取实验计为成果。

## 2026-10-20 R6操作生命周期

- Application/Features/Content/ContentOperationLifetime.cs：文件活动范围、停止令牌、UI任务登记/排空；窗口/文件服务不在其中执行。
- App/PosterSpatialRuntime.Transfers、FileActions、Incoming、NativeExit、ContentReturn、RealContent：唯一所有者接线，finally释放、源端交接及重复完成防护；原分散字段/ObserveForShutdown删除。
- PosterVisualWindow.FileActions/FileDialogs/NewContent/RealContent：停止后的入场保护及会话忙态撤回；App.Lifetime退出顺序不变。
- ContentOperationLifetimeTests、Review/PosterSpatialRuntime.OperationLifetimeProbe：定向异常/取消/排空及真实创建后刷新失败；仅Review编译。启动start-architecture-file-actions-review.ps1 -Batch R6。证据、携带项和人工见[第22节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#22-r6-操作生命周期与异常安全2026-10-20)。

## 2026-10-20 R5映射动作与测试入口退役

- Application/Features/Content/FileActions/MappedContentCreationUseCase.cs：映射新建次序与未知提交清理决定；ContentCreationUseCase显式路由。
- Application/Features/Content/Observation/MappedDirectoryUseCase.Actions.cs：位置解析、替代对象校验、拖入/Win新建结果登记。
- Infrastructure/ContentActions/LocalOwnedContentCreationFiles.cs：物理创建见证、固定句柄及精确清理；SqliteContentRenameJournal接旧schema核验；原MappedDirectoryService删除。
- tests/Shared/ContentCreationFixture、ContentTestServices及Contracts.Tests/Support三Fixture：测试专用构造/故障注入，不编入产品；生产旧四包装删除。MappedContentCreationUseCaseTests与ArchitectureTests守新边界。
- Bootstrap/App.ContentServices接新用例；Review IncomingProbe新增R5限定分支，复用InlineNamingProbe；启动入口start-architecture-file-actions-review.ps1 -Batch R5。进度/纯移动/证据归[第21节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#21-复审修订与r5至r8实施计划)。

## 2026-10-19 R4组装与Worker边界（USER PASS / CLOSED）

- App/Bootstrap/ApplicationComposition.cs：显式按需组装及唯一数据库/Worker；App.Startup.cs：正常/维护分流、宿主与用例接线。
- App.Lifetime.cs / App.DesktopEnvironment.cs：资源停止顺序／原生环境调度；原App主文件不再混放整套环境恢复流程。
- Windows/Content/LocalContentReferenceProbe.cs：共享本机元数据；ContentWorker/Infrastructure项目显式引用，取消旧Compile链接。
- ArchitectureTests与check-architecture.ps1：项目方向、完整生产partial边界、产品/Review隔离；Source/Offline复用原探针。集中入口start-architecture-presentation-review.ps1 -Batch R4。
- 当前图源ArchitectureReview/R4-Closed/current.architecture.json与[目标对照/源码映射](../../20_Architecture/ArchitectureReview/R4-Closed/README.md)；R4目录保留为待验时快照。证据与基础出口见[第19～20节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r4-composition-boundary)。

## 2026-10-19 R3呈现与同代命中

- Application/Features/Content/Layout/ContentPresentationLayout.cs：纯布局准备、缓存/代次、锚点及空间候选组合；ContentPresentationFrame供绘制/命中共享。
- 同目录ContentInputGeometry.cs、ContentLayoutTileIdentity.cs：标题/动作/内容/留白/导航/编辑区域决策及循环副本身份；Contracts.cs的PosterInputLayoutSnapshot新增内容代次标签。
- App/Presentation/PosterContentPresenter.cs：稳定卡片树、CardPaint差异、选择/命名外观和暖位图；ContentRasterBinding.cs：需求、缓存、请求和订阅生命周期。
- PosterVisualWindow.Content.cs / ContentAssets.cs / ContentPublication.cs：事实及线程适配、原生接纳后绘制；PosterVisualWindow.cs：整帧准备/批次失败保留，不再拥有卡片差异或输入几何算法。场景仍通过SurfacePaint原端口。
- ContentPresentationLayoutTests、ArchitectureTests职责项；IncrementalRenderProbe及ContentHeatRuntimeProbe复用既有增量/悬停/收展并补同代失败检查；入口scripts/start-architecture-presentation-review.ps1。详细证据/人工见[第18节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r3-presentation-boundary)。

## 2026-10-19 R2会话边界

- Application/Features/Content/Layout/PosterContentSession.cs：每代海报草稿、历史、交互保护、热度/主题与发布票据；复用ContentHeatSession，不含UI/持久化/定时器。
- 同目录PosterSourceRefresh.cs：每次来源绑定独立的线程安全合并标记；旧通知不能消耗新绑定的工作。
- App/WinUiPosterSession.cs：构造并关闭内容会话；PosterVisualWindow.ContentProposals.cs只负责Dispatcher接线、结果绘制/反馈，不再拼接多个partial私有Busy字段。
- ContentEditing/ContentMarquee/Transfers/Transition/FileActions/InlineRename/ContentSource：在原开始/结束点接会话状态，原生资源和图片预算保持；ContentGesture.Proposal及窗口旧草稿/缓冲/排队字段已删除。
- PosterContentSessionTests.cs、原ContentHeatSession/Registry/架构测试；App.ContentHeatRuntimeProbe扩展中心释放分支并复用App.ContentSourceProbe；入口scripts/start-architecture-session-review.ps1。详情见[第17节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r2-session-boundary)。

## 2026-10-19 R1来源修复与遗留入口清理

- Application/Features/Content/FileActions/ContentReferenceRepairUseCase.cs：捕获目标与版本、来源预览、提交前复核和宿主守卫，再复用现有命令；不搬动真实文件。
- Application/Ports/Content/IContentReferenceSource.cs：来源元数据端口与原LocalReferencePreview；Infrastructure/LocalContentReferenceSource.cs复用LocalContentReferenceProbe，原身份检查保持。
- ControlCenterWindow.References.cs：仅保留RepairSelectedReference交互；无调用的添加/DEMO转换分支删除。RemoveSelectedReference仍供键盘使用，不误删。
- PosterSpatialRuntime.Transfers.cs：产品只保留启动/忙态/资源边界，删除具体存储、RunTransfer与失效确认回调；App.Transfers.cs的partial接线只在Review提供实现。
- tools/review/App/PosterSpatialRuntime.LegacyOutgoingProbe.cs：原Outgoing文件先字节一致迁出，旧模拟拖出仅编入Review；正常原生收/出/跨海报用例不变。
- ContentReferenceRepairTests.cs、ArchitectureTests.cs和scripts/start-architecture-reference-review.ps1：定向边界与隔离候选入口；证据统一见[第16节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r1-reference-cleanup)。

## 2026-10-19 R1配置与图标入口

- Application/Features/Configuration/PortableProfileUseCase：预览、文件复核、备份与守卫后一次提交；Ports/Configuration/ProfileStoragePorts：文件IO、离线恢复端口及结果。
- App/PortableProfileController.cs已删除；ControlCenterWindow.Portable保留交互/文案，App.Portable保留重启和单实例生命周期，Bootstrap/App.Configuration创建具体适配器。
- ContentIconImport.LoadAsync及IContentIconSource：图标检查/解码复用原Worker；LocalContentIconSource复用原本地检查。窗口不再读元数据或强转StateProjectionCoordinator，使用已有命令接口。
- StateBackupRecovery实现应用端口，内部原子安全步骤保持；StateRecoveryWindow只依赖端口。未完成的来源修复入口及职责清单见[第15节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r1-configuration-icons)。
- scripts/start-architecture-configuration-review.ps1：正常产品两组隔离人工，不与Review探针混装。

## 2026-10-19 R1位置迁移与旧入口退役

- Application/Features/Content/Storage：StorageLocationUseCase负责位置/首次创建/预览/结果；StorageRelocationUseCase负责阶段和失败收尾顺序。Ports/Storage承载位置DTO与窄接口。
- Infrastructure/StorageLocation：SqliteStorageLocationStorage、LocalStorageRelocationExecution、SqliteTransferLocationNotices；原复制/证明/锁/原子提交和Recovery保留。ManagedStorageService及StorageRelocationFiles.Execute旧入口已删除。
- Bootstrap/App.ContentObservation共享位置用例，App.xaml首次收纳、SqliteContentStartupSteps和ControlCenterWindow.Transfers均接用例。ControlCenterService及中心位置文件不依赖Infrastructure/SQLite；无用forceChoose/SqliteTransferJournalNotice删除。
- 原ManagedStorageServiceTests/StorageRelocationFilesTests及StorageCopyCrashFixture已转接新用例；scripts/start-architecture-storage-review.ps1对应当前正常候选。职责、精简验证与人工见[第13节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r1-storage-location)，以下“位置待迁出”为历史。

## 2026-10-19 R1归还／卸载用例

- Application/Features/Content/Lifecycle：ContentReturnUseCase（删海报/单项归还/重启观察）、UninstallContentUseCase（卸载库存与批次）、UninstallPreparation（保留/静默/报告）；Ports/Content/ContentLifecyclePorts为类型化事实与窄存储、文件及配置端口。
- Infrastructure/ContentLifecycle：SqliteContentReturnStorage及Uninstall分部保留原子提交/身份与归属；LocalContentReturnFiles保留目的地限制；LocalUninstallProfile保留既有配置打开与清单写盘。PosterContentReturn兼容入口无另一份循环。
- Bootstrap/App.ContentLifecycle组装；PosterSpatialRuntime.ContentReturn、ControlCenterService删除前核对、SqliteContentStartupSteps.ReconcileReturns、App.Uninstall调用应用边界。位置/配置入口仍待R1。
- 复用PosterContentReturnTests、UninstallContentTests与ContentReturnProbe；新增准备边界3项和依赖1项。scripts/start-architecture-lifecycle-review.ps1绑定独立正常候选；职责/精简证据/人工步骤见[第12节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#r1-lifecycle)。
- 后台观察成本证据：ManagedContentMonitor → ContentObservationService → 两类Observation用例；[OPT-CONTENT-SYNC-01](../../30_Plans/ON_DEMAND_OPTIMIZATIONS.md)仅核实记录，调度/扫描源码本批未改。

## 2026-10-19 R1映射、观察与启动接线

- Application/Features/Content/Observation：MappedDirectoryUseCase、MappedDirectoryCreation、ManagedContentSyncUseCase、ContentObservationService、ContentStartupUseCase；Ports/Content/ContentObservationPorts及ContentStartupPorts提供类型化事实/原子操作。
- Infrastructure/ContentObservation：SqliteMappedDirectoryStorage、SqliteManagedContentObservationStorage、LocalContentObservationFiles、SqliteContentStartupSteps；原MappedDirectoryService/ManagedContentSynchronizer复用同一用例，ManagedContentMonitor只依赖IContentObservation。
- App/Bootstrap/App.ContentObservation与App.ContentServices：生产组装；App.Transfers和PosterSpatialRuntime.MappedPosterCreation/MappingStatus不再直接引用Infrastructure。
- ContentObservationUseCaseTests / ArchitectureTests及既有映射/同步用例；tools/review/App/App.ContentObservationProbe使用真实生产监视器，scripts/start-architecture-observation-review.ps1绑定独立候选。职责、证据与待验两组见[第11节](../Architecture/FOUNDATION_REMAINDER_PLAN.md#11-r1-映射创建观察同步与启动接线2026-10-19)，下方例外为历史范围。

## 2026-10-19 迁移身份与文本规则

- src/DesktopUp.Infrastructure/Persistence/Migrations：001.sql～025.sql、catalog.xml固定SQL字节/身份/已核实历史指纹；SqliteMigrationCatalog严格加载嵌入资源。SqliteStateStore沿原事务/备份路径调用，不再散列业务类raw string。
- .editorconfig / .gitattributes / scripts/validate-source-text.ps1 / Directory.Build.targets：编辑、检出、共同构建和发布的编码/BOM/EOL检查；package-preview.ps1前置检查及源绑定。
- tests/DesktopUp.Contracts.Tests/MigrationFingerprintTests.cs、tests/Packaging/Test-SourceTextPolicy.ps1：只针对迁移与字节策略的隔离回归。证据及发布边界见[修复记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-19-迁移指纹与统一编码修复)。

## 2026-10-16 基础架构余项入口

当前 R1 文件动作、拖放两片均 USER PASS，职责及证据见[余项计划第8～9节](../Architecture/FOUNDATION_REMAINDER_PLAN.md)。源码 b3bfdc6 已同步，继续 impl/architecture-foundation；下一片为 PosterSpatialRuntime.MappedPosterCreation、App.Transfers 与 ManagedContentMonitor/ManagedContentSynchronizer 的创建和同步调用链。生命周期等其余 R1、R2～R4 仍待做，真实热度 HOLD；拉取接续见该计划第10节。

- Application/Features/Content/FileActions：ContentFileActionService、Creation/Rename/DeletionUseCase、ContentFileActionPlans，分别承接操作编排和原索引规则；Ports/Content 为请求结果与窄事务/系统端口。
- Infrastructure/ContentActions：实际文件系统、创建/删除事务、改名回执和上下文适配；原三个Service只兼容转接；ShellCreatedContentService仍负责原子登记。
- App/Bootstrap/App.ContentServices.cs：本片生产组装；PosterSpatialRuntime.FileActions、PosterVisualWindow.FileActions/NewContent/InlineRename 仅接应用边界。MappedPosterCreation原方法体独立移动，仍为后续R1例外。
- ContentFileActionUseCaseTests、ArchitectureTests；tools/review/App/App.IncomingProbe.cs 的 --r1-file-actions-probe 只复用行内命名检查，不附带历史拖放矩阵。
- Application/Features/Content/Transfers：ManagedIngressUseCase、MappedIngressUseCase、CrossPosterTransferUseCase、NativeShellExitUseCase；Ports/Content/ContentTransferPorts为原回执及窄准备/提交/观察端口。
- Infrastructure/ContentTransfers/SqliteManagedIngressStorage及CrossPosterTransfer/MappedDirectoryIngress attempt适配原账本、文件身份与原子提交；NativeShellExitJournal实现观察端口。Bootstrap统一组装，运行器Incoming/NativeExit与窗口Transfers无具体存储依赖。
- ContentTransferUseCaseTests、ArchitectureTests.TransferVerticalSlice；App.IncomingProbe的--r1-transfers-probe只复用本片窗口接线，不附带历史交互矩阵。

## 2026-10-16 架构C热度运行入口

- 展开闪烁修订：App/PosterVisualWindow.ContentAssets.cs将收起暂停加载与真正释放分开；tools/review/App/PosterVisualWindow.IncrementalRenderProbe.cs逐合成回调检查保留图像；smoke-content-heat-runtime.ps1 -PostureOnly只跑收展路径。C及修订视觉复验均已确认通过，详见下方C记录。

- Application/Features/Content/Layout/ContentHeatSession.cs：海报可视会话唯一热度状态所有者；ContentProjectionValidation.cs复用原主动布局校验。
- App/PosterVisualWindow.ContentProposals.cs：安全点/排队失效/主题宿主适配；Content.cs的作者布局变更触发Session.Reconcile。原RefreshContentSurface/同帧输入与绘制路径不变。
- tools/review/App/App.ContentHeatRuntimeProbe.cs与scripts/smoke-content-heat-runtime.ps1：仅隔离同帧/稳定卡片集成，不产生真实统计。
- 当前状态和已验候选归[C记录](../Architecture/C_HEAT_RUNTIME.md)。A/B/C均USER PASS，本轮本地集成；其余窗口/具体存储耦合未宣称完成。

## 2026-10-16 架构B真实打开/定位入口

- Application/Features/Content/Open/ContentActionService.cs：请求/见证、执行、完成记录；ContentActionResult.cs：反馈和带来源见证的观察结果；ReferenceContentActions.cs：原UI动作接口的薄适配器。
- Application/Ports/Content/IContentActionService.cs、IContentOpenJournal.cs：执行/事实边界；Infrastructure/Persistence/Content/SqliteContentOpenJournal.cs原实现不改。
- App/Bootstrap/App.ContentServices.cs：按原启动顺序组装；PosterSpatialRuntime.ContentSource.cs仅保留服务端口，不持有打开账本。其他具体存储耦合尚未全部消除。
- 结果和候选归[B批](../Architecture/B_CONTENT_USE_CASES.md)；A_MOVES.json仅是A批纯移动哈希基线，不用于宣称B修改后的源码字节未变。

## 2026-10-16 架构A批目录与构建入口

- src/README.md：当前源码职责、允许依赖与暂存例外；Application/Features/Content/Open、Application/Ports/Content、Domain/Content、Infrastructure/Persistence/Content是首批原样移动目的地。
- App/Bootstrap/App.Services.cs：原服务组装；tools/review/App/App.ReviewEntry.cs：原验收分发及可选partial钩子，正常编译没有实现。
- Directory.Build.props/targets、scripts/build.ps1 -ReviewTools：产品/验收编译边界；scripts/check-architecture.ps1：检查实际Compile集合和发布互斥，-VerifyMoveBytes仅用于本次纯移动核对。
- 详细旧/新路径、验证、候选及未完成项见[架构A批](../Architecture/A_FOUNDATION.md)和A_MOVES.json。下方锚点是历史位置；类型/命名空间不变。

## 2026-10-16 V3-09-03自启动

- Application/StartupRegistration.cs：纯登记状态、显式变更与读回；Windows/WindowsStartupRegistration.cs：实际安装身份、HKCU64 Run/DesktopUp适配、其他安装保护，无后台补登记或StartupApproved私有格式。
- Domain/StartupIntent.cs、Application/StartupIntentParser.cs、App.xaml.cs：精确AutoStart意图，零海报不弹中心、第二实例不转发唤醒。
- ControlCenterWindow.Startup.cs / xaml、ControlCenterService.cs：全局开关、激活/展开刷新、系统启动设置入口、读回成功后保存现有偏好。
- packaging/preview/installer.nsi：内容准备成功后仅移除自身启动命令；DistributionBuild.cs / App.Transfers.cs / package-preview.ps1：仅显式StartupReview构建隔离配置/存储，常规包默认不变。
- StartupRegistrationTests / StartupIntentTests、tests/Packaging/Test-StartupCleanup.ps1 / Test-StartupPackage.ps1：本批定向覆盖；scripts/start-startup-review.ps1：同包集中人工。结果/已知边界见[自启C记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-16-v3-09-03开机自启动前移计划)。

## 2026-10-16 05-13兼容准入

- Application/LocalContentCompatibility.cs：纯路径、实体类型和未落地属性策略；供IncomingDropSession、LocalContentProtocol及Infrastructure的引用/搬运/映射入口复用，不替代身份/归属和事务。
- tests/DesktopUp.Contracts.Tests/LocalContentCompatibilityTests.cs：路径拒绝、实体链接字节/目标与重启；SessionFixture/ContentCompatibilityReviewFixture.cs和scripts/start-content-compatibility-review.ps1：隔离四海报集中验收。
- 结果/未支持范围见[兼容范围](../V3-05/CONTENT_COMPATIBILITY.md)，旧卸载验收入口保留但不用于当前05-13。

2026-10-16验收环境补充：卸载验收脚本目标改为review01/installed-user，复用原profile，旧installed不删除。由用户正常终端完成安装登记；工具环境仍有旧登记时Mismatch保持阻止，不绕过。Status补UserSid及RegistryKey帮助区分执行上下文；程序业务代码无变化。

2026-10-16登记识别：同一验收脚本 `Get-ReviewInstallation`显式读取当前用户Registry64，并核对安装标记/位置/程序文件；`Status`在任何工具链或进程操作之前返回。`Test-ReviewInstallationState.ps1`仅运行Status，含函数局部HKCU PSDrive错误映射模拟，不改注册表、安装文件或用户数据。程序/安装器没有修改。

2026-10-16：`scripts/start-uninstall-content-review.ps1`必须保留UTF-8 BOM，确保Windows PowerShell 5.1不按ANSI误解中文提示；`tests/Packaging/Test-ReviewScriptEncoding.ps1`仅检查编码、原生Parser及中文AST，不执行脚本或文件操作。本轮不改App/卸载器业务代码。

## 2026-10-15 卸载归还增量

- `PosterContentReturn.ReturnOne`：删海报与卸载共享归还执行/记录/结果提交；`PosterContentReturn.Uninstall.cs`负责全登记受管集合清点、无卡片内容、最终余项检查及卸载关系移除。归属元数据保留，不作为用户文件搬动。
- `App.Uninstall.cs`：维护启动参数、简单进度/取消、本机位置清单、保留原位/原路归还；不启动普通海报会话。`ProfileLifecycleGate`与安装器共用维护锁；`SingleInstanceCoordinator`维护请求不转发给已运行应用。
- `packaging/preview/installer.nsi`：内容处置页、先准备再精确卸载、取消/失败保留程序、显式保留重试标记绑定安装路径和配置路径；未修改用户收纳文件的清理范围。
- `UninstallContentTests`、`Test-UninstallContent.ps1`、`smoke-uninstall-content.ps1`为本批直接影响面；`start-uninstall-content-review.ps1`为独立安装候选的集中人工入口。当前证据/未验见C§16，旧“卸载器尚未调用”仅对上一批成立。

## 2026-10-15 05-12来源与归还

2026-10-15用户已确认本批四组人工通过；以下实现不变，卸载器尚未调用归还服务。右键相关 `PosterInputIslandManager.TryReadContentContextHit` / `NativeDesktopPointerPlatform.CoversPoint` 仅检查未修订，按用户决定停止Snipaste相关专项。

- `ContentReturnPolicy`区分受管卡片与映射；`StateCommandPolicy`阻止直接删除仍有受管内容的海报。
- `ContentReturnStore`、SQLite schema25：独立来源与归还结果；普通入库/索引收尾/CrossPoster提交同事务记录来源；排除新建暂存，旧成功账本补录。
- `PosterContentReturn`：Shell逐项移动、目录/桌面回退、实际结果核实、公用关系移除及最后删除海报；`Reconcile`仅补索引，不自动移动/删海报。`ManagedContentSynchronizer`避让未完成归还。
- `ControlCenterService.RequestRemoval`、`PosterSpatialRuntime.ContentReturn`：既有删除确认与文件生命周期/退出等待接线；`App.Transfers`启动结果核对及异常位置提示。
- `PosterContentReturnTests`（19项）、`ContentReturnProbe`与`smoke-incoming.ps1 -ContentReturn`；`ContentReturnReviewFixture`和`start-content-return-review.ps1`为独立人工入口。

2026-10-15：以下05-07实现已获四组人工通过，本轮授权集成；下一05-12从develop另开分支，先补原始路径/安全归还及海报删除接线，不重做原生拖放。

## 2026-10-15 V3-05-07 跨海报直接拖放（当前）

- `PosterSpatialRuntime.NativeExit.cs`：冻结源意图、按路径核对同步Drop交接，禁止源自己走外部入库；`PosterVisualWindow.Transfers.cs`仍使用同一OLE源/接收器、跟手与悬停预览。
- `PosterSpatialRuntime.Incoming.cs`：等待源退栈后分流跨海报MOVE；`Transfers.cs`同时保护源/目标。外部拖入及映射Ctrl COPY沿用原链路。
- `CrossPosterTransfer.cs`：schema24目录操作表中带Kind的意图/实际结果；同目录归属交接、跨目录Shell执行、身份核对与仅结果恢复。`App.Transfers.cs`在常规观察前核对未完成索引。
- `CrossPosterPlacement.cs`：复用移除与目标落点规则，源/目标关系、自定义图标同事务；`ContentReferenceIntegrity.cs`仅对内部已授权观察/移动收据允许同实体图标转交。
- `CrossPosterTransferTests.cs`；`PosterSpatialRuntime.CrossPosterProbe.cs`与`smoke-incoming.ps1 -CrossPoster`；人工入口`start-cross-poster-review.ps1`及`CrossPosterReviewFixture.cs`。详细证据归[C§14](../V3-05/DIRECT_TRANSFER_DELIVERY.md#14-v3-05-07-跨海报直接拖放2026-10-15进行中)。

## 2026-10-15 外观UI统一（当前）

- 集成追溯：功能0a75494 → develop d66265f → 精简发布f80765e；产品设置和XAML保留，以下探针/验收脚本仅留开发线。发布构建/安装入口仍为 `scripts/build.ps1`、`package-preview.ps1` 与 `packaging/preview/installer.nsi`，同源和成品证据见 `PREVIEW_INSTALLER_DELIVERY.md` 最新节。
- `ControlCenterWindow.xaml`：UiCAppearanceCard/UiCAppearanceFolder/UiCColorAction；SurfaceCard与TitleCard、状态摘要、TitleVisibilitySwitch归组，三处颜色按钮统一XAML声明。
- `ControlCenterWindow.Surface.cs`、`TitleColors.cs`：只调整呈现/控件绑定，原ChangeSurface、ChangeTitleColors、AutoPosture.TitleVisibility_Changed保存逻辑不变。
- `ControlCenterWindow.AppearanceProbe.cs`：实际加载/关闭后的颜色交互、目标切换、重启、主题与宽窄排版；`App.IncomingProbe.cs --appearance-layout-probe`复用隔离数据守卫并提前返回，不搭载文件搬移场景。
- `scripts/start-control-center-appearance-review.ps1`：独立集中验收；`smoke-incoming.ps1 -AppearanceLayout`：UI专用首启/重启。详细结果归当前C记录。

## 2026-10-15 下一步控制中心外观UI（仅准备）

- `ControlCenterWindow.xaml`：LayoutFolder、SurfaceFolder、SurfaceColorButton、SurfaceOpacitySlider、TitleVisibilitySwitch、TitleColorsFolder；现有UiCTemplateFolder/UiC资源作为风格参照。
- `ControlCenterWindow.Surface.cs`、`ControlCenterWindow.TitleColors.cs`、`ControlCenterWindow.AutoPosture.cs`（TitleVisibility_Changed）：沿用背景/颜色/显隐提交，先整理UI不换底层参数。
- `ControlCenterWindow.Theme.cs`、`ControlCenterWindow.UiCWorkspace.cs`：主题与样片夹视觉。保留分支中的已验原生内容操作，不顺带重构文件业务。

## 2026-10-15 Win菜单新建

- `ContentWorker/ShellUiHost.cs`：保留子菜单初始化通知；`ShellNewMenuSite.cs`：INewMenuClient实际对象回调、Shell生命周期内接收。
- `Application/ShellUiProtocol.cs`：统一Win菜单名称、创建结果直系路径/身份边界；`Infrastructure/ShellCreatedContentService.cs`：原地登记、不搬移/删除、不批量扫描收编。
- `App/PosterVisualWindow.FileActions.cs` → `PosterSpatialRuntime.FileActions.cs`：创建结果登记后进入原有InlineRename。
- `tests/DesktopUp.Contracts.Tests/ShellNewMenuTests.cs`：真实Shell新建＋索引／重启／保留失败结果；命令选择注入仅在测试ContentFixture入口，生产worker不接受该CLI。

## 2026-10-15 N4删除/属性/新增映射

- `PosterVisualWindow.ContentMenu.cs` → `FileActions.cs` → `PosterSpatialRuntime.FileActions.cs` → `Infrastructure/ContentDeletionService.cs`：删除所选真实文件，持久观察、确认消失后公共关系清理。
- `Infrastructure/ShellFileDeletion.cs`：复用ShellDirectoryTransfer的COM ABI，专用STA调用IFileOperation回收；原生安全提示保留。`NativeShellExitJournal.cs`的ExplicitDelete标记兼容旧JSON，不重放删除。
- `ContentWorker/ShellUiLifetime.cs`/`ShellUiHost.cs`：SHCreateThreadRef、SHSetThreadRef、SHSetInstanceExplorer和消息循环，修复属性页创建后worker提前退出。
- `ControlCenterWindow.Editing.cs`：新增海报分普通/目录映射，复用可取消目录选择器；`PosterSpatialRuntime.Transfers.cs`区分底层FileServiceReady与对外TransfersAvailable，防止中心自身租约挡住映射创建。旧`PosterVisualWindow.DirectoryMapping.cs`删除，已有映射状态/目录操作保留。
- `ContentDeletionTests`、`ShellPropertyLifetimeTests`、`ControlCenterWindow.MappingProbe`及`smoke-incoming.ps1 -MappingCenter`：定向删除安全、真实属性页可见/持续、生产中心创建/取消/重启。结果只查[C记录](../V3-05/CONTENT_NATIVE_ACTIONS.md#2026-10-15-真实删除属性生命周期与映射入口)。

2026-10-14 Git检查点：下列N1～N4修订源码/测试归提交 `8ded3aa`，仅同步当前功能分支；本次没有新代码变更，不合回impl/develop。验收状态及验证记录保持原边界。

## 2026-10-14 N4反馈修订

- `Infrastructure/ContentWorkerLocation.cs`：Shell UI与图标提取共同使用完整 `content-worker/DesktopUp.ContentWorker.exe`；`App.xaml.cs` / `PosterVisualWindow.FileActions.cs` 使用同一定位。`ShellUiClient`增加有界准备失败诊断；`ShellUiHost --shell-ui-prepare-v1`仅准备COM菜单，不执行用户命令。
- `PosterVisualWindow.NewContent.cs`直接新建→`PosterSpatialRuntime.CreateContent`返回已提交路径→`ContentItemAtPath`取真实卡片→`PosterVisualWindow.InlineRename.cs`名称行编辑。快捷方式仅先选目标；旧`RequestFileName`窗口删除。
- `InlineRename`复用 `DesktopMenuInteraction` 输入/层级租约，TextBox处理选择/中文输入；`LocalFileNamePolicy.RenameSelectionLength`默认只选文件名主干、文件夹全选；`ContentSource`在编辑期间暂缓元数据引起的重发布，收尾增量刷新。
- `ContentNativeActionTests`共享worker路径/真实Shell准备/命名选择；`App.IncomingProbe --inline-name-probe`及`PosterVisualWindow.InlineRenameProbe`只在自建隔离数据上检查真实Popup/焦点/业务回调，不代替原生键鼠人工验证。详见[本轮记录](../V3-05/CONTENT_NATIVE_ACTIONS.md)。

## 2026-10-14 N3收尾与N4候选

`Application/IncomingDropSession.cs`映射MOVE/Ctrl COPY反馈 → `Windows/NativeIncomingDropSession.cs`保留原生跟手图像/完成NONE，禁止源二次清理 → `App/PosterVisualWindow.Transfers.cs`及`PosterSpatialRuntime.Incoming.cs`按目标分流。`Infrastructure/MappedDirectoryIngress.cs`（schema24）保存意图、逐项Shell结果、单一归属与落点；`ShellDirectoryTransfer.cs`专用STA/IFileOperation、系统冲突提示与原生结果路径；普通受管流程不替换。跨海报到映射接收在OLE释放回调先标交接，等源会话结束再执行，不补一次鼠标拖动。

失联反馈沿 `MappedDirectoryService.Reconcile` → `ManagedContentMonitor` → `App.Transfers` → `PosterSpatialRuntime.MappingStatus`/`PosterVisualWindow.MappingStatus`，只更新轻量标记、不整组刷新。`MappedDirectoryIngressTests`与既有N1/N2/N3/输入/持久化覆盖，启动 `scripts/start-content-native-review.ps1`。结果/未验项见[唯一C记录](../V3-05/CONTENT_NATIVE_ACTIONS.md)，下方锚点为历史。

## 2026-10-14 真实内容基础操作N3

`App/PosterVisualWindow.DirectoryMapping.cs`选择目录 → `PosterSpatialRuntime.FileActions.cs`/`ControlCenter.cs`复用新海报空间校验 → `Infrastructure/MappedDirectoryService.cs`扫描/创建/观察/新建/操作前身份验证 → `Application/DirectoryMappingPolicy.cs`增量关系与布局；`MappedDirectoryStore.cs`及`SqliteStateStore.cs`追加schema23。`ManagedContentMonitor.cs`共用单worker、直接子项watcher/稳定窗口/周期兜底；删除沿`ContentRelationRemoval`。`DirectoryMappingTests`22项、最终直接影响面212项通过。`DirectTransferPreparation.Incoming`和`PosterVisualWindow.CanReceiveFiles`暂时拒绝把受管拖入错接到映射目录，待N3收尾；N4实际界面未验。[本轮记录](../V3-05/CONTENT_NATIVE_ACTIONS.md)为准，下方为历史。

## 2026-10-14 真实内容基础操作N2

`App/PosterVisualWindow.FileActions.cs`空白新建子菜单 → `PosterVisualWindow.NewContent.cs`名称/目标选择与退出等待 → `PosterSpatialRuntime.FileActions.cs`位置、版本、文件任务保护 → `Infrastructure/ContentCreationService.cs`生成物、同名检查与既有转移账本。`ShellShortcutFile.cs`专用STA序列化真实.lnk，不执行目标；`LocalTransferFiles.NewContent.cs`CreateNew及原句柄快照。`ContentCreationTests`新增22项，N2直接影响面共100项通过。schema仍22，N3映射未接；构建/GUI未验边界以[实施日志N2](../V3-05/CONTENT_NATIVE_ACTIONS.md#n2空白区新建2026-10-14)为准。

## 2026-10-14 真实内容基础操作N1

`Application/PosterInputRouter.cs`、`PosterContentContextRequest.cs`与`Windows/PosterInputIslandManager.cs`在同一快照/版本门内开放空白右键；`App/PosterVisualWindow.ContentMenu.cs`及`FileActions.cs`接菜单。`FileDialogs.cs`名称窗口/退出保护，`PosterSpatialRuntime.FileActions.cs`桥接；`Infrastructure/ContentRenameService.cs`与`LocalTransferFiles.Rename.cs`负责持久收据、同目录身份改名和安全回退/观察，`SqliteStateStore.cs`追加schema22。`Application/ShellUiProtocol.cs`→`Infrastructure/ShellUiClient.cs`→`ContentWorker/ShellUiHost.cs`是独立STA系统菜单/属性路径；`ReferenceContentRuntime.RefreshItem`保证单项刷新。`ContentNativeActionTests`、`PosterContentContextRouterTests`、`IncrementalContentRasterTests`及受版本影响存储测试提供定向证据。N2见上节，N3映射尚无实现锚点，实际GUI未验；[实施日志](../V3-05/CONTENT_NATIVE_ACTIONS.md)为准。

## 2026-10-14 海报增量呈现

外部悬停：`IncomingLayoutPreview.Create`纯预测，`ContentMemberPlacement.Append`与正式引用新增共享默认占位，`IncomingContentPlacement.Apply/Advance`与实际收纳共用；`PosterVisualWindow.Transfers.cs`保存临时预览，`Content.cs`只改变绘制，不污染原生命中依据；`IncomingLayoutPreviewTests`检查与正式提交等价，`VerifyIncomingHoverForProbe`检查真实节点位移/取消/稳定性。新人工入口 `scripts/start-poster-hover-review.ps1`。

`ReferenceContentRuntime` 按成员对账/单项像素代次，`IContentRasterIdentity` 声明布局无关规范图，`IAtomicContentItemViewSource` 声明锁内观察快照；`PosterSpatialRuntime.ContentSource.cs` 保留适配器；`PosterVisualWindow.Content.cs` 保留可见卡片、`ContentAssets.cs` 定点图像更新。新增 `IncrementalContentRasterTests` 及 `PosterVisualWindow.IncrementalRenderProbe.cs`，复用 `smoke-incoming.ps1 -IncrementalRender -ManagedSync`；范围与结果见[增量呈现记录](../V3-05/POSTER_INCREMENTAL_RENDERING.md)。

## 2026-10-14 所选卡片图标入口

`src/DesktopUp.Application/ContentIconPreview.cs` 冻结预览身份；`src/DesktopUp.App/ControlCenterWindow.ContentIcons.cs` 复用原生选择器/资产命令/对话框生命周期，`UiCWorkspace.cs` 提供单卡菜单；`src/DesktopUp.Windows/NativeProfilePicker.cs` 新增 CardIcon 过滤。`tests/DesktopUp.Contracts.Tests/ContentIconPreviewTests.cs` 复核迟到结果、只读预览和恢复；既有 SettingsSmoke/IncomingProbe/PickerSmoke 扩展定点覆盖。人工入口 `scripts/start-card-icon-review.ps1`，状态归[V2记录](../V3-05/CONTENT_ICON_ASSETS.md)。

## 2026-10-14 开发集成与发布边界

`Docs/Rules/GIT_STRATEGY.md`：功能分支 → `impl/develop` 完整集成 → `impl/main` 精简发布；根 README / 冷启动 / 当前入口同步职责。产品源 `f6a8199` 合入发布 `484fc02`，ContentWorker/安装器一致；发布 README 单独维护。验证及本地证据见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-14-开发集成线与发布同步)，不在发布线保留 Docs/tests。

## 2026-10-14 桌面快捷方式安装选项

`packaging/preview/installer.nsi`：`InstallOptionsCreate/Changed/Leave`保存可选状态、`CreateOptionalDesktopShortcut`禁止覆盖创建、`un.RemoveOwnedDesktopShortcut`核实记录/目标后清理；`.onInit`解析静默0/1。`tests/Packaging/Test-DesktopShortcut.ps1`用惰性载荷运行同一NSIS脚本，结果归[安装交付记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-14-可选桌面快捷方式)。既有打包入口无需新参数，用户在安装时决定。

## 2026-10-14 原始图标资源与填充回退

- `ContentWorker/OriginalIconResource.cs`：快捷方式只读元数据、ICO/PE所选图标组与最大有效原始帧，资源映射不执行目标。
- `ContentWorker/ShellIconQuality.cs`、`LargeShellIcon.cs`：多个Shell请求的保守整图选择，不裁原始留白；`Program.cs`接入优先级与原生HICON尺寸回退。
- `OriginalIconResourceTests.cs`：原资源、索引、自定义图标、透明留白、坏帧和PNG回归；`RealContentNativeTests`原多尺寸细节保留。源码路径相对src，测试相对tests/DesktopUp.Contracts.Tests。证据归[显示记录§13](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#13-shell填充画布修复2026-10-14)。显示层和文件业务未变。

## 2026-10-13 发布同步

- `scripts/run.ps1`：每次构建当前源码；`scripts/package-preview.ps1`：产品指纹/路径元数据、自包含完整重建后发布（避免缺WinUI主题的PRI缓存）、项目及依赖许可证，不依赖测试目录。
- `packaging/preview/installer.nsi`：目录页、首启中心、精确清单卸载及Windows卸载登记；`PREVIEW-README.txt`随包说明。
- `App/App.xaml.cs`：只对Created的新配置打开首次中心，不植入DEMO；`Application/ManagedStorageLocationPolicy.cs`现有用户数据默认路径保持。
- 构建/安装/分支证据见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-13-发布同步)。

## 2026-10-13 05-14界面减法与通用背景

- `Domain/PosterPresentationParameters.cs`：surface及schema8保留合同；`CanShowContentHeading`按模板装配声明判定。
- `Application/ScenePresentationAdapter.cs`、`ControlCenterSettingsSession.cs`：通用背景投影、现有预览/提交链；不改变内容或文件业务。
- `App/ControlCenterWindow.Surface.cs`、`ContentHeading.cs`、`TemplateParameters.cs`：公共颜色/不透明度、动态折叠介绍组件、私有参数去重；`ControlCenterWindow.xaml`/`UiCWorkspace.cs`撤重复菜单。
- `App/PosterVisualWindow.Content.cs`/`ContentFeedback.cs`：无普通空提示/底部测试框；故障和内部通知保留。
- `PosterSurfaceAppearanceTests.cs`/`ContentHeadingTests.cs`、`ControlCenterWindow.AppearanceProbe.cs`与`--appearance-cleanup-probe`：具体覆盖及未验边界见[§13](../V3-05/DIRECT_TRANSFER_DELIVERY.md#13-05-14界面减法与通用背景2026-10-13)。源码路径相对src，合同测试相对tests/DesktopUp.Contracts.Tests。

## 2026-10-13 05-14入口收口

- `App/ControlCenterWindow.xaml`：撤下添加/修复常驻按钮；所选项旁`ContentManageButton`。
- `App/ControlCenterWindow.UiCWorkspace.cs`：按钮与右键/Shift+F10共用原菜单；`ForSameItem`版本保护、单选真实项修复资格。`ContentLayout.cs`同步空海报/草稿/交互禁用。
- `App/ControlCenterWindow.References.cs`：保留引用/修复编辑能力及原文件不动守卫，撤日常添加处理器及共享文案；底层引用命令不改。
- `App/ControlCenterWindow.SettingsSmoke.cs`、`App.IncomingProbe.cs`：独立配置`--content-entry-probe`验证空/单/多选入口及修复取消；`scripts/smoke-incoming.ps1 -ContentEntry`复用首启/重启。

## 2026-10-13 D4-N 原生拖出与单一归属

- `Windows/NativeShellDragSource.cs`、`NativeDragDataObject.cs`：真实Shell/CF_HDROP、私有图像格式、OLE源生命周期、原海报往返与取消；不执行磁盘动作。
- `Windows/PosterInputIslandManager.cs` 的 `HandOffContentCapture`、`NativeFileDropTarget.cs`：同次按住交接；可信本进程原海报目标先走布局，外部文件仍走原入站会话。
- `Windows/NativeContentDragImage.cs`、`ShellDropImages.cs`：自定义图标→独立Shell位图，正确透明度与位图所有权。
- `App/PosterVisualWindow.Transfers.cs`、`PosterSpatialRuntime.NativeExit.cs`、`PosterVisualWindow.TransferConfirmation.cs`：真实源准备/跟手/返回布局/退出观察；只在释放后对起点已失效项确认。
- `Infrastructure/NativeShellExitJournal.cs`（schema21）、`ManagedContentSynchronizer.cs`：持久化释放凭据＋逐项源核实，共用 `Application/ContentRelationRemoval.cs`，没有二次移动/删除。
- `Application/ContentSingleOwnerPolicy.cs`、`ContentReferenceIntegrity.cs`、`ContentReferenceCommand.cs`；`Infrastructure/SingleOwnerMigration.cs`、`SqliteTransferJournal.IndexPlan`：唯一归属、旧库保全、跨归属收纳及图标延续。
- 测试 `NativeShellExitTests`、`NativeShellPayloadTests`；入口 `scripts/start-incoming-review.ps1 -NativeShellExit`。以上路径均相对 `src/`，测试相对 `tests/DesktopUp.Contracts.Tests/`。

## 2026-10-13 原生落点缺口定位（未修）

- `PosterVisualWindow.Transfers.HandleDesktopTransfer`：外框外松手仅识别桌面，再传选择ID，未向Explorer交接Drop坐标或文件数据。
- `DirectTransferPreparation.Outgoing`：DesktopDirectory＋源文件名；`PosterSpatialRuntime.Outgoing.RunOutgoing`：本应用逐项执行文件取出。`NativeContentDragImage`仅视觉层，不能视为原生源。
- `NativeFileDropTarget`／`NativeIncomingDropSession`与`PosterVisualWindow.AttachFileDrop`：入拖已走IDropTarget、真实点/ticket和Shell图像，应复用，不为出拖缺口重建。
- DeskBox参考路径、差异及拟议责任在[直接拖放§10](../V3-05/DIRECT_TRANSFER_DELIVERY.md#10-d4-n原生shell拖出整改2026-10-13)；新Shell源/完成适配尚无实现锚点，不用计划类名冒充代码。

## 2026-10-13 框选与D4

- ContentSelection.Marquee、PosterInputRouter/Contracts：按下时选择集合、相交去重、内容viewport优先级末位；Windows PosterInputIslandManager复用原捕获/阈值/Esc，不新增输入HWND。
- [PosterVisualWindow.ContentMarquee.cs](../../../../src/DesktopUp.App/PosterVisualWindow.ContentMarquee.cs)：当前视口框选、取消还原；ContentEditing.HasContentGesture统一保护；Content.DrawContentSnapshot绘制透明框。
- [NativeContentDragImage.cs](../../../../src/DesktopUp.Windows/NativeContentDragImage.cs)、ContentDragPixels、PosterVisualWindow.ContentDragImage：缓存BGRA/数量、不可命中图像层、内部/外部单一所有者和终态释放；不是通用OLE源。
- [PosterSpatialRuntime.Outgoing.cs](../../../../src/DesktopUp.App/PosterSpatialRuntime.Outgoing.cs)、DirectTransferPreparation.OutgoingSources/Outgoing：冻结来源、逐项取出，跨卷复用既有保留源执行器；PosterVisualWindow.TransferConfirmation提供近旁确认。
- OutgoingTransferIntegrationTests/ContentSelectionTests及App.IncomingProbe `--d5-combined`、CombinedSelectionProbe；入口`scripts/start-incoming-review.ps1 -CombinedD5`。证据见[§9](../V3-05/DIRECT_TRANSFER_DELIVERY.md#9-空白框选d4与d5合包2026-10-13)。

## 2026-10-13 05-11-A公共移除与外部同步

- [ContentRelationRemoval.cs](../../../../src/DesktopUp.Application/ContentRelationRemoval.cs)：按精确关系ID一次裁剪各海报成员/布局/图钉；ContentDetachPolicy和外部同步复用，原拖出经ContentDetachPolicy保持用户确认/锁定语义。
- [ManagedContentSynchronizer.cs](../../../../src/DesktopUp.Infrastructure/ManagedContentSynchronizer.cs)：归属与完整扫描、身份改名、稳定观察、共享范围、CAS提交；只改索引。SqliteStateCommandRepository限制内部观察授权，不能由UI伪造。
- [ManagedContentObservationStore.cs](../../../../src/DesktopUp.Infrastructure/ManagedContentObservationStore.cs)：schema20本地身份基线；SqliteTransferJournal及IndexRecovery、SqliteStorageRelocationJournal.Transactions在提交时保存身份，旧凭证只作迁移引导。
- [ManagedContentMonitor.cs](../../../../src/DesktopUp.Infrastructure/ManagedContentMonitor.cs) / [App.Transfers.cs](../../../../src/DesktopUp.App/App.Transfers.cs)：每配置共享监测、2秒合并/30秒补查、启动/恢复/退出生命周期、正式投影刷新。SqliteTransferJournal观察专用短租约不占用扫描等待阶段。
- PosterVisualWindow.Content裁剪真实选择集合；ManagedContentSyncTests新增隔离边界验证，App.IncomingProbe与smoke-incoming.ps1的`-ManagedSync`复用实际进程链。候选入口增加`-ManagedSyncRevision`，详情见[实施§8](../V3-05/DIRECT_TRANSFER_DELIVERY.md#8-05-11-a最小外部同步与公共移除2026-10-13)。

## 2026-10-13 DEC-043 全框输入

- PosterInputIslandManager.BuildWindowRegion与PosterDropSurfacePolicy：原生命中覆盖当前可见矩形，隐藏/暂停仍清空；逻辑命中不自动变成留白操作。
- PosterInputRouter.Resolve：上层框内无命令则停止，不路由到下层Item；标题/按钮/卡片及捕获语义保持。
- PosterDropReception：同一输入HWND的可选OLE注册、会话暂停和释放，无额外窗口/捕获识别/宽限计时器；NativeFileDropTarget、Shell图像和原业务通道保留。
- 原NativeOleDragMonitor迁至tests/DesktopUp.NativeDragProbe，仅保留历史实验源码；Program拒绝已退役--routing-*及--cross-*入口。
- PosterDropComponentTests、SparseContentInputTests、PosterInputRouterTests、PosterContentContextRouterTests、IncomingDropNativeTests更新新边界。独立候选入口scripts/start-incoming-review.ps1 -FullFrameRevision，当前证据归[D3§8](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#8-取消普通点击穿透并收敛接收组件)。

下方稀疏输入/独立接收窗描述为此前实现，已由本节替代。

## 2026-10-13 原生重复拖动诊断入口

[NativeDragProbeTests.cs](../../../../tests/DesktopUp.Contracts.Tests/NativeDragProbeTests.cs)新增原生COM vtable枚举检查，覆盖Next的FORMATETC/fetched写回与终态；仅定向1项通过，不涉及鼠标或文件。

[RoutingMechanismProbe.cs](../../../../tests/DesktopUp.NativeDragProbe/RoutingMechanismProbe.cs)与[Program.cs](../../../../tests/DesktopUp.NativeDragProbe/Program.cs)新增`--routing-repeat`，只运行生产组件的连续提交/取消/暂停三组；保留`--routing-auto`静态对照。底板自身维护测试层级，输入前核对实际鼠标和进程，限定采样时间并对无返回的自建测试源以失败码退出。生产代码未改，最新夹具构建及尚未通过的暂停边界见[D3§7.2.1](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#721-解锁后复验与夹具整改)。

## 2026-10-12 输入与拖放组件化

- [PosterInputIslandManager.cs](../../../../src/DesktopUp.Windows/PosterInputIslandManager.cs)：普通输入始终稀疏；InstallDropReceiver注册与几何/暂停/重挂载/释放接线，不再暴露全矩形区域切换。
- [PosterDropReception.cs](../../../../src/DesktopUp.Windows/PosterDropReception.cs)、[NativeOleDragMonitor.cs](../../../../tests/DesktopUp.NativeDragProbe/NativeOleDragMonitor.cs)：默认空区域的独立接收面、经核验的OLE捕获、捕获/协议结束分离及新会话复位。Windows实现细节兼容边界见[D3§7.1](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#71-已实施的组件边界)。（该监测器现仅保留于测试目录，本条描述为历史。）
- [NativeFileDropTarget.cs](../../../../src/DesktopUp.Windows/NativeFileDropTarget.cs)、[ShellDropImages.cs](../../../../src/DesktopUp.Windows/ShellDropImages.cs)：协议终态通知与独立COM包装；App只持有IPosterDropRegistration，原ticket/文件通道不变。
- [PosterDropComponentTests.cs](../../../../tests/DesktopUp.Contracts.Tests/PosterDropComponentTests.cs)验证可选组件不改基础区域；[RoutingMechanismProbe.cs](../../../../tests/DesktopUp.NativeDragProbe/RoutingMechanismProbe.cs)用独立底板/OLE源验证实际跨进程命中、取消、暂停、连续会话与资源释放。自建诊断路径，无文件执行器。
- [修订候选入口](../../../../scripts/start-incoming-review.ps1)：`-ComponentRevision`使用新manifest/profile，旧默认入口保留。当前只完成PrepareOnly，最终原生待解锁，见[D3§7.2](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#72-证据与当前验证范围)。

## 2026-10-12 D3点击穿透回归调查

[CrossProcessClickProbe.cs](../../../../tests/DesktopUp.NativeDragProbe/CrossProcessClickProbe.cs)新增独立进程底板及A/E原生对照，TargetSurface的颜色键候选仅用于探针；Program接三个独立探针模式。[启动器](../../../../scripts/start-incoming-click-probe.ps1)每次创建新诊断目录，不启动产品、不移动文件。生产PosterInputIslandManager的全矩形/HTTRANSPARENT仍为待整改实现，见[D3§6](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#6-普通点击穿透回归与窄范围机制验证)。

## 2026-10-12 D3全区域实际入库

- [IncomingContentPlacement.cs](../../../../src/DesktopUp.Application/IncomingContentPlacement.cs)：原插入顺位/局部让位复用、贴边限制、成功后推进批量位置；SqliteTransferJournal.IndexPlan与关系单revision提交。
- [IncomingTransferCopies.cs](../../../../src/DesktopUp.Infrastructure/IncomingTransferCopies.cs)：既有receipt内的跨卷创建身份、保留源复制与受限收尾；LocalTransferExecutor及AutomaticTransferCompletion复用重启/取消流程，schema19不变。
- [PosterSpatialRuntime.Incoming.cs](../../../../src/DesktopUp.App/PosterSpatialRuntime.Incoming.cs)、[PosterVisualWindow.Transfers.cs](../../../../src/DesktopUp.App/PosterVisualWindow.Transfers.cs)：生产全区域启用、最终ticket、逐项执行、近旁反馈、目标占用与退出取消。
- [MissingSourceDragImage.cs](../../../../src/DesktopUp.Windows/MissingSourceDragImage.cs)：仅缺源图像格式时采用系统文档图标，结束释放；有图沿Shell helper。
- [IncomingTransferIntegrationTests.cs](../../../../tests/DesktopUp.Contracts.Tests/IncomingTransferIntegrationTests.cs)、App.IncomingProbe.cs、scripts/smoke-incoming.ps1：本批事务/跨卷故障与同包门；[人工启动器](../../../../scripts/start-incoming-review.ps1)。当前证据及待验归[D3§4.2/§5](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#42-d3完整接线与集中验证)，以下D3-01为历史。

## 2026-10-12 D3-01接收基础与默认关闭的原生能力

- [IncomingDropSession.cs](../../../../src/DesktopUp.Application/IncomingDropSession.cs)：冻结路径、坐标/代次依据，最后物理点，单次消费；标题无伪网格，循环归一化。未产生布局或文件提交。
- [ContentScreenCoordinates.cs](../../../../src/DesktopUp.Domain/ContentScreenCoordinates.cs)：共享屏幕换算；PosterVisualWindow.ContentEditing.cs仅将原内拖换算迁入此函数。
- [PosterDropSurfacePolicy.cs](../../../../src/DesktopUp.Application/PosterDropSurfacePolicy.cs)/PosterInputIslandManager.cs：OLE矩形与稀疏普通命中分离；TrySetFullAreaDropReception默认关闭且无产品调用。
- [NativeIncomingDropSession.cs](../../../../src/DesktopUp.Windows/NativeIncomingDropSession.cs)/[ShellDropImages.cs](../../../../src/DesktopUp.Windows/ShellDropImages.cs)/NativeFileDropTarget.cs：新ticket构造入口、Shell图像四回调、失败/重入/退出清理；旧路径构造入口保持兼容。缺源图像的兜底仍待做。
- [IncomingDropSessionTests.cs](../../../../tests/DesktopUp.Contracts.Tests/IncomingDropSessionTests.cs)/[IncomingDropNativeTests.cs](../../../../tests/DesktopUp.Contracts.Tests/IncomingDropNativeTests.cs)：39项新增；完整3236合同，4个生产命令/Canvas回归，证据范围见[D3§4](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#4-实现与验证记录)。

下一步D3-02落点进入原关系/布局单事务，再D3-03逐项跨卷和D3-04产品准入；D2-04人工已收口。

## 2026-10-12 D2-04默认根与位置设置

- [ManagedStorageService.cs](../../../../src/DesktopUp.Infrastructure/ManagedStorageService.cs)：产品门面；只读位置、按需创建/物理身份回执、旧根选择、负载数量预览、真实迁移和启动收尾；UI不提供文件证明。SqliteStateStore仅增加内部配置路径，不改schema19。
- App.Transfers.cs/App.xaml.cs：通道/隔离基目录与启动收尾，共用同一服务；PosterSpatialRuntime.Transfers.cs不再按集合数猜测选址。ControlCenterService.cs传递服务；[ControlCenterWindow.Transfers.cs](../../../../src/DesktopUp.App/ControlCenterWindow.Transfers.cs)/xaml接全局位置窗口、打开、确认、进度取消、结果。读取中退出检查防止迟到弹窗；取消并等待沿原生命周期通道。
- [ManagedStorageServiceTests.cs](../../../../tests/DesktopUp.Contracts.Tests/ManagedStorageServiceTests.cs)：22项；[App.StorageSettingsProbe.cs](../../../../src/DesktopUp.App/App.StorageSettingsProbe.cs)只接受out/drop-d2带哨兵自建配置，程序驱动同包7流程及seed，不执行用户文件。原生鼠标不冒称覆盖。
- [smoke-storage-location.ps1](../../../../scripts/smoke-storage-location.ps1)、smoke-native-picker.ps1、[start-storage-location-review.ps1](../../../../scripts/start-storage-location-review.ps1)：7设置＋4目录选择器流程、源码/产物/TRX指纹绑定和人工入口；Start-DesktopUpFixtureProcess.ps1为测试显式注入同级存储根。

当前证据和人工清单见[D2§10](../V3-05/D2_STORAGE_LOCATION_AND_MIGRATION.md#10-d2-04默认位置与设置接线)。D3全区域/跨卷逐项入库仍未接；下方“尚无App调用”为历史范围。

## 2026-10-12 D2-03真实保留源文件通道

- [StorageRelocationFiles.cs](../../../../src/DesktopUp.Infrastructure/StorageRelocationFiles.cs)：内部真实Prepare/Verify/Execute，源清单先行、归属准备、只读源复制、完整路径/内容/身份/文件元数据证明；源/目标句柄跨SQLite提交持有。没有App生产调用。
- [StorageRelocationFiles.Recovery.cs](../../../../src/DesktopUp.Infrastructure/StorageRelocationFiles.Recovery.cs)：可信收据决定提交前后分支；精确前缀证明清理半成品，提交后仅清已核实旧负载，外来/替换/损坏证据保留，取消停止后续删除，不递归清空选定根。
- [StorageCopyLedger.cs](../../../../src/DesktopUp.Infrastructure/StorageCopyLedger.cs)：state/storage-relocations下只写一次且刷盘的库存、创建/目标/结束证据；绑定物理配置/提案/DB意图，验证版本、相对路径、数量和连续回执；不是可移植配置或用户任务表。
- [LocalTransferFiles.Storage.cs](../../../../src/DesktopUp.Infrastructure/LocalTransferFiles.Storage.cs)：CreateNew复制、原生身份/句柄锁、SHA256、时间/常见属性、逐个按句柄删除、拒绝ADS/硬链接/不支持文件属性；旧Move逻辑未替换。
- StorageBundleOwnership.cs新增准备对象创建回执；SqliteStorageRelocationJournal.cs公开内部现有源租约/当前计划复核；SqliteStateStore.cs仅加sidecar位置，schema19不变。首次默认根/设置/启动收尾仍归D2-04。
- [StorageRelocationFilesTests.cs](../../../../tests/DesktopUp.Contracts.Tests/StorageRelocationFilesTests.cs)及[.Recovery.cs](../../../../tests/DesktopUp.Contracts.Tests/StorageRelocationFilesTests.Recovery.cs)：新增46项；[StorageCopyCrashFixture.cs](../../../../tests/DesktopUp.TransferFixture/StorageCopyCrashFixture.cs)在带哨兵的自建数据执行10个独立进程退出点；Program.cs入口先路由该夹具。真实不同卷与Windows快捷方式均不操作用户内容。

最终完整3175通过，App独立构建0警告/错误；证据与支持矩阵归[D2§9](../V3-05/D2_STORAGE_LOCATION_AND_MIGRATION.md#9-d2-03保留源复制与故障验证)。下方“没有文件执行器/待实现”为D2-02历史快照。

## 2026-10-12 D2-02持久化与归属

- src/DesktopUp.Infrastructure/SqliteStorageRelocationJournal.cs：schema19、活跃根/根集合版本、物理配置身份、只读库存及目录/声明句柄；内部文件证明租约端口尚待D2-03实现。
- src/DesktopUp.Infrastructure/SqliteStorageRelocationJournal.Transactions.cs：持久意图、目标证明、完整CAS、共享来源定位重写、单revision和独立收据；重放/取消/占用保护。提交仍只由隔离测试驱动，未接生产UI。
- src/DesktopUp.Infrastructure/StorageBundleOwnership.cs：独立v2多集合根/子声明；意图先行、只建空目标声明，可信提交加当前登记才激活；没有文件复制执行器。
- SqliteStateStore.cs追加19不改旧SQL；SqliteStateCommandRepository.cs、SqliteContentOpenJournal.cs、SqliteTransferJournal.cs接写入/打开/重叠传输保护；ManagedStorageProvisioner.cs、ManagedStorageAdoption.cs保留旧归属格式，写声明前检查迁移占用，并拒绝相互覆盖。
- tests/DesktopUp.Contracts.Tests/StorageRelocationPersistenceTests.cs及.Reliability.cs：48项；StorageSchemaFixture.cs只负责旧库测试构造；tests/DesktopUp.TransferFixture/StorageRelocationCrashFixture.cs是带哨兵、空库的独立进程中断夹具，不属于产品文件执行器。完整3129通过，范围见[D2§8](../V3-05/D2_STORAGE_LOCATION_AND_MIGRATION.md#8-d2-02持久化与归属保护)。

## 2026-10-12 D2-01默认位置与整库迁移纯提案

- src/DesktopUp.Application/ManagedStorageLocationPolicy.cs：显式活跃/单旧根/多旧根/默认候选决策；日常、Preview与强制独立测试路径提案。无IO或创建权限，最终原生核实另做。
- src/DesktopUp.Application/ManagedStorageRelocationPolicy.cs：全部受管集合、归属、别名/共享影响、冻结输入和完整确认指纹；持久意图前禁止复制，全部目标核实后单事务提案，提交前取消保旧、可信提交后不回滚，清理需再次证明。
- tests/DesktopUp.Contracts.Tests/ManagedStorageLocationPolicyTests.cs与ManagedStorageRelocationPolicyTests.cs：新增86项，全量3081通过；反例包括异常活跃根、多根/空集合、漏库存、旧确认、版本变化和崩溃前后阶段/收据不一致。
- 尚无生产调用、持久化或真实文件搬迁；实施及下一批归[D2记录](../V3-05/D2_STORAGE_LOCATION_AND_MIGRATION.md)。D1本机A的1～8项用户通过，不代表生产输入岛已替换。

## 2026-10-12 D1环境核对修订

- tests/Shared/DragProbe/ProbeEnvironment.cs：GetShellWindow定位实际Explorer，只用进程／令牌查询权限；共享纯判定检查SID／会话／完整性，失败拒绝，Medium与High分开标记。
- tests/DesktopUp.NativeDragProbe/Program.cs：--check-environment输出报告；目标／独立源／smoke启动都重新检查，不保留管理员一律拒绝或smoke绕过。
- scripts/start-native-drag-probe.ps1：新proof-permission01绑定候选；PrepareOnly准备、CheckOnly无界面环境预检、正常启动带环境说明；不修改安全设置。
- tests/DesktopUp.Contracts.Tests/NativeDragProbeEnvironmentTests.cs：22项，包含真实自身令牌的固定／变长字段读取及失败拒绝模型。当前完整2995项及probe06证据见[D1实施](../V3-05/D1_NATIVE_DRAG_PROBE.md)。下方probe03为此前范围。

## 2026-10-12 D1隔离探针

- tests/DesktopUp.NativeDragProbe：TargetForm四种命中策略、底层点击／原生OLE计数、收起与前台覆盖对照；SourceForm独立进程单／多图标原生DoDragDrop，不提供文件路径。
- tests/Shared/DragProbe：ProbeProtocol生命周期和NONE安全反馈；ProbeDataObject按格式保存HGLOBAL与IStream；ShellProbeImages包装系统图像辅助；ProbeSource记录目标HWND及结束结果，不清理文件。
- tests/DesktopUp.Contracts.Tests/NativeDragProbeTests：26项新增及真实COM初始化；测试与探针链接同一份实现，不把直接回调断言当成真实鼠标证据。
- scripts/start-native-drag-probe.ps1：指纹绑定、普通权限与独立review入口；详细范围归[D1实施](../V3-05/D1_NATIVE_DRAG_PROBE.md)。产品输入岛、拖出执行器、默认根未改。

## 2026-10-12 默认空内容与拖放缺口

Domain/ContentLayout.cs的ContentLayoutPolicy.TryRead(null)：默认零成员，读取不写库；显式样片仍由ContentLayoutSamples提供。AuthoritativeState注释同步空内容语义。App/PosterVisualWindow.Content.cs：空态／零成员计数去DEMO文案。Contracts.Tests/ContentLayoutPersistenceTests.EmptyDefault.cs新增5项，旧布局编辑／历史夹具显式创建样片。

当前缺口锚点：Windows/NativeFileDropTarget.cs注册输入岛且accept仅传路径，Drop屏幕坐标未进入布局；PosterInputIslandManager.BuildWindowRegion按CapturesPointer建稀疏区域；App/PosterVisualWindow.Transfers.cs沿原捕获处理桌面拖出；PosterSpatialRuntime.Transfers.cs首选址／必要确认仍委托中心；ControlCenterWindow.Transfers.cs尚无默认根或实际迁移。新机制只在[方案](../../20_Architecture/DROP_SURFACE_AND_STORAGE_PROPOSAL.md)中，不把文档当代码完成。

## 2026-10-10 发布源码分支

开发线38187bb保存基础拖放及此前未同步工作；impl/main的d6ca237单独退役测试工程、专用探针、启动分发和测试依赖，六个产品工程保持。scripts/package-preview.ps1显式传播SelfContained到ContentWorker并绑定Modules／Directory.Build.targets等发布输入，此打包修正单独回移开发线。发布线scripts/run.ps1先构建当前源码，避免分支切换后误用旧产物；该入口差异不等于开发线统一启动路径问题已全面解决。分支边界与下一次里程碑合并见[Git规则](../../../Rules/GIT_STRATEGY.md)。

## 2026-10-10 自动收尾与直接拖放

Infrastructure/AutomaticTransferCompletion：生产执行后与启动收尾；未移动结束、已移动核实关联、保留不明结果，旧占用关闭不重放Move。SqliteTransferJournal.IndexRecovery的automatic门拒绝覆盖已编辑原目标；旧手工诊断API保留。DirectTransferPreparation：真实载荷／卡片映射、活跃引用复用与同卷准入。

Windows/NativeFileDropTarget：既有输入岛上的IDropTarget、CF_HDROP有界快照、Ctrl／Alt／右拖拒绝、NONE完成反馈、不授源重复清理；注册与撤销资源计数。NativeDesktopDropSite：只读验证Shell桌面归属，不注入或设置图标坐标。Application/DirectDesktopDropPolicy：正常捕获中的内外边界／完成判定。

App/PosterSpatialRuntime.Transfers＋PosterVisualWindow.Transfers：单一文件执行协调、原捕获拖回桌面、当前代次／版本和退出等待；Pointer按目标保护收纳交互不影响其他海报。ControlCenterWindow.Transfers只保留存储选址、必要源路径确认、异常位置；旧恢复任务控件已移除。App.Transfers启动自动收尾不重放Move。

新增AutomaticTransferCompletionTests／DirectTransferPreparationTests／DirectDesktopDropPolicyTests／NativeFileDropTargetTests共36项。当前证据、普通权限启动脚本、限制与人工清单见[交付](../V3-05/DIRECT_TRANSFER_DELIVERY.md)；下方旧锚点按历史范围理解。

## 2026-10-10 显式认领与schema18

Infrastructure/ManagedStorageAdoption.cs：已登记旧目录只读预览、配置/根/子文件身份及清单绑定、准备记录先行、排他凭证、二次核对和事务激活；拒绝未知/其他配置/未结束操作，不自动入库额外内容。ManagedStorageProvisioner.ValidateRegistration同时识别完成的旧目录认领；新创建不得覆盖认领凭证。SqliteStateStore仅追加18，1～17不改。

Infrastructure/LocalTransferFiles.cs：CopyFileExW进度取消、原生回调异常隔离、FAIL_IF_EXISTS、复制验证/收据先于源处理；STOP后的真实目标需检查，不保证系统必留部分文件。ManagedStorageAdoptionTests与ManagedTransferCopyProgressTests新增34项（含独立进程边界）；TransferFixture新增--adoption-crash与--prepare-adoption-review。

App/ControlCenterWindow.Transfers.cs：在现有全局存储入口合并显式认领选择与说明，不增加日常管理面板；App启动仍只读发现。scripts/start-transfer-adoption-review.ps1绑定独立candidate01/review02及当前proof。当前完整2906与人工待验边界见[实施§10](../V3-05/MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md#10-旧目录显式认领与剩余故障边界)。

## 2026-10-10 保留停止与schema17

Infrastructure/SqliteTransferJournal.Closures.cs：停止票据、当前revision/原证据确认、独立结束记录及schema17。DurableTransferItem.IsFinished统一调度、占用、恢复和App提示；原阶段不改，旧票据不能复活，未知/不匹配结束记录拒绝执行。

Infrastructure/ManagedStorageProvisioner.Closures.cs：目录创建停止与历史、配置归属/未登记校验、幂等和事务；不认领外部目录。ManagedStorageProvisioner.Preview/Confirm及SqliteTransferJournal.Register均拒绝绕过已停止创建。App/ControlCenterWindow.Transfers.cs将这两项放入异常入口，混合待办保留已结束位置查询；无日常任务中心。

ManagedTransferClosureTests、ManagedStorageClosureTests新增40项；SqliteStateStore追加17而不改历史迁移。完整2872与candidate02证据归[实施§9](../V3-05/MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md#9-保留文件停止重试与目录创建中断出口)。下方§8分段的schema16是历史；旧目录认领、新界面人工、原生仍OPEN。

## 2026-10-10 已移动文件关联恢复与简化入口

Infrastructure/SqliteTransferJournal.IndexRecovery.cs：只读预览、独立确认票据、当前共享影响与完整读集、文件证明租约、单事务IndexPlan/回执/完成标记；原始移动批次与兄弟读集不重写。SqliteTransferJournal.IndexPlan的confirmedRecovery只允许已删除取出卡片不重建，不为普通命令开放存储移除。

App/ControlCenterWindow.Transfers.cs与XAML：收纳恢复备用入口、仅显示待处理项、技术详情折叠、统一继续完成动作、选择现有海报；启动仍只读发现。测试ManagedTransferIndexRecoveryTests新增24项，当前证据/余项见[实施§8](../V3-05/MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md#8-异常入口简化与已移动文件关联恢复)。schema16保持，原生拖放未接。

## 2026-10-10 用户可达恢复与存储所有权

Infrastructure/ManagedStorageProvisioner负责选址预演、预写创建意图、配置/根/集合真实身份与可证阶段恢复；SqliteTransferJournal.Recovery提供只读列表、原子换新意图和安全释放未移动项。App/ControlCenterWindow.Transfers复用PortableAction、既有选择器取消和全局菜单；App.Transfers只读启动发现且退出等待。NativeProfilePicker增加ManagedStorageFolder目的，不新增第二套STA取消。

ManagedTransferRecoveryTests新增28、ManagedTransferSafetyTests新增6；TransferFixture追加仅项目out新目录的人工夹具生成。scope/证据及余门归[实施§7](../V3-05/MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md#7-恢复入口与目录所有权)。原生拖放未接，不复用早期“未实现恢复UI”的历史判断。

## 2026-10-10 持久账本与受限文件后端

Infrastructure/SqliteTransferJournal负责schema16注册、预写/占用/阶段和共享索引事务；LocalTransferPreview组合实际文件事实与原纯计划；LocalTransferFiles负责有界清单、文件/卷身份、句柄保护和验证；LocalTransferExecutor负责执行、取消、持久恢复。SqliteStateCommandRepository内部事务授权与ContentReferenceIntegrity共同限制存储成员移除，StateBackupRecovery受库外guard保护。

新增ManagedTransferPersistenceTests共50项及独立DesktopUp.TransferFixture；原纯70项保持。证据/限制见[实施记录](../V3-05/MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md)。App/原生拖放与恢复UI未接；下方第一批“没有文件移动器/schema16”仅为历史。

## 2026-10-10 受管存储与拖放纯合同

Domain/ContentDropIntent：根目录资格、载荷分类及修饰键/能力矩阵。Application/ContentTransferPlanning：冻结计划、共享/后代影响、StateReadSet、确认指纹和关系差量；ContentTransferRecoveryPolicy：逐项事实恢复、执行/提交门和受管集合注册共存；ContentTransferGesture：对原ContentGesture附加内外边界仲裁，尚未接App或替代原布局算法。

测试ManagedStorageDropContractTests与ContentTransferContinuityTests共70项；[实施记录](../V3-05/MANAGED_STORAGE_AND_DROP_CONTRACT.md)列出DeskBox→适配→反例。没有文件移动器、原生DropTarget或schema16；这些仍归后续账本/安全与接线。

## 2026-10-10 独立图标资产

Domain/ContentIconAsset与PosterItem.IconAssetId；Application/ContentIconImport、ContentIconCommand及ContentReferenceIntegrity最终屏障；Infrastructure/SqliteContentIconStore、SqliteContentRepository和schema15追加迁移；ContentWorker/IconImageDecoder固定WIC解码；ReferenceContentRuntime复用既有缓存并由App注入同库读取。源路径／共享关系与单卡覆盖分离。

新增ContentIconPersistenceTests 21、ContentIconDecodeTests 15、ContentIconRuntimeTests 11项；App.ContentReferenceProbe追加真实WriteableBitmap字节、恢复默认及重启读回。完整实现与预算见[V1记录](../V3-05/CONTENT_ICON_ASSETS.md)。生产读取已接，V2控件未接；下方“SQLite14／图标资产未实施”为当时历史。

## 2026-10-09 Git源码检查点

当前实现保存于`d8a1fda`：覆盖下方R7可靠性与V3-05实体、引用、显示使用及模板/默认值锚点。提交准备仅整理12个新增文件的EOF空白，不新增功能。2607合同与Release构建证据见[本轮验证](VERIFICATION.md#2026-10-09-git提交前复验)，最近续作仍为工作流§6.4；历史源码/候选绑定不改写。

## 2026-10-09 默认基础收纳

`PosterVisualWindow.CreateDefaultPoster`复用`PosterConfigurationFactory.NewBasicBox`，保留首次定位；中心/托盘新增仍走原同一工厂及DistributionBuild默认覆盖。`packaging/preview/package-settings.json`默认basic-box/null；`PosterCreationDefaultsTests`新增2例绑定该配置。已有海报不迁移。验证/范围归[§10](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#10-默认基础收纳2026-10-09)。

## 2026-10-09 模板内容介绍区

Domain/TemplateHeadingVisibility及PosterPresentationParameters的presentation v7负责逐模板偏好/兼容和MetaHeight；Application/ControlCenterSettingsSession原事务、TemplatePresentationRefreshPolicy结构刷新、PortableTemplateMerge同ID映射；App/ControlCenterWindow.ContentHeading.cs和XAML模板夹入口，TemplateRecipe副本继承。ContentHeadingTests新增16例，App.ContentReferenceProbe通过真实控件事件及XAML/命中坐标读回验证关/开/关和重启。证据与边界归[§9](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#9-模板内容介绍区2026-10-09)，SQLite仍14，05-05-V1尚未实施。

## 2026-10-09 无底板与高清图标

`ContentSelection.ShowHighlight`、`ContentCardPresentationPolicy.ShowOutline`与原ContentSelection/Content.cs接线区分普通目标与显式集合；SurfacePaint阻止模板回画真实卡片底板。`ContentWorker/LargeShellIcon.cs`封装IShellItemImageFactory/GDI资源，原Program保留回退；`ContentIconPresentationTests`7项、RealContentNativeTests多尺寸ICO1项、App.ContentReferenceProbe无底板/普通无描边/Ctrl单项检查。当前证据归[§8](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#8-修饰键选择与无底板高清图标2026-10-09)。自定义图标覆盖只进入设计/计划，源码尚无资产存储/上传入口。

## 2026-10-09 05-05／06三项增量

`Application/ContentCardPresentationPolicy.cs`统一真实卡片图片区/选中判定，接`App/PosterVisualWindow.Content.cs`；`Application/ContentReferencePreviewPolicy.cs`按活跃PosterItem查复用海报，接`App/ControlCenterWindow.References.cs`。`ContentUsageRefinementTests.cs`8例；`App.ContentReferenceProbe.cs`读回Image实际比例尺寸与单选边框、验证跨海报选择隔离和重启。现候选/启动入口归[修订§7](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#7-人工反馈后三项修订2026-10-09)，不改正式关系移除/打开事实合同。

## 2026-10-09 V3-05-05／06 真实显示与动作

当前入口：[05-05／06代码锚点](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#5-代码和验证入口)。新增ReferenceContentRuntime/Actions、LocalContentProtocol、独立ContentWorker、LocalContentWorkerClient、SqliteContentOpenJournal、ContentDetachCommand、ContentSelection，接回原海报/中心输入、可见缓存及CAS。schema14只追加迁移；Shell只默认打开/定位，不做磁盘传输。以下未接线描述均限定其历史阶段。

## 2026-10-09 V3-05-04正式引用生产接线

当前入口见[05-04代码锚点](../V3-05/NON_DESTRUCTIVE_REFERENCE_ADD.md#6-代码锚点)：ContentReferenceCommand/Integrity、SqliteContentRepository、LocalContentReferenceProbe、ReferenceContentViewSource、ControlCenterWindow.References及原NativeProfilePicker退出通道。不是新增第二状态源，也未接真实Shell打开。后续05-01段仅描述纯合同落地时的范围。

## 2026-10-09 V3-05-01纯合同完成

新增Domain/RealContent.cs、ContentCapabilityPolicy.cs；Application/ContentRelations.cs、ContentOpenContract.cs、ContentCatalogCodec.cs、ContentLegacyCompatibility.cs及RealContent*Tests。属于纯实体/命令准备/结果归约/兼容夹具，未替换现App的DEMO void处理器或引入第二持久状态。

当前主线的合同、代码锚点、验证与交接转[V3-05实施记录](../V3-05/CONTENT_ENTITY_AND_OPEN_CONTRACT.md)。以下05-01准备/R7/R8记录保持原日期范围，不作为新模型尚未实现的判断依据。

## 2026-10-09 对齐审计与05-01准备（无新代码）

差异/版本域见[执行§14](LR_MILESTONE_EXECUTION.md#14-文档源码对齐审计与05-01准备2026-10-09)，下一步任务归[工作流§6.1](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#61-v3-05-01实施准备实体能力与打开结果)。现Open为void/DEMO no-op，Assets无图像解码；ContentLayout/ContentPlacement是本地布局，不是已实现的正式内容库。下列R8入口可复用，05-01正式实体/结果类型尚不存在。ContentLayout.cs热度注释落后于ResolveHeat的注入Rank能力，登记为后续局部注释同步项，不冒称真实热度已接。

## 2026-10-09 R8接入交接（现有代码，无新生产实现）

- Domain/ContentLayout.cs：ContentPlacement/ContentLayoutDocument版本与布局局部身份；IContentItemViewSource/IObservableContentItemViewSource元数据端口，正式ContentItem映射仍归05-01。
- Application/ContentSourcePorts.cs：Views/Assets/Open统一可信宿主绑定，ContentSourceProjection不允许元数据修改成员/顺序；App/PosterSpatialRuntime.ContentSource.cs负责目录级绑定。
- App/PosterVisualWindow.ContentSource.cs：UI排队、版本/旧草稿失效、R4预制树的恢复安全点、订阅释放；源adapter须提供快照，不把慢I/O放Read/Version。
- Application/ContentAssetDemand.cs、ContentAssetCache.cs与App/PosterVisualWindow.ContentAssets.cs：可见tile需求/循环去重、有界缓存与迟到结果准入；真图提取/解码和跨海报总预算尚未接入。
- Application/ContentActivationSession.cs与App/PosterVisualWindow.ContentActivation.cs：激活去重/呈现见证/DEMO打开分发；同步void不等于真实打开成功结果合同。
- App/App.ContentSourceProbe.cs与scripts/probe-content-source.ps1：受控内存假源五场景；本轮同一final09候选1/3/6张全部通过。原C1相关62合同与新三进程证据层级分开，见[交接§13](LR_MILESTONE_EXECUTION.md#13-r8真实item接入交接2026-10-09)。

## 2026-10-09 BUG-021定点整改与R7-D入口

- `DesktopLayerAttachment.cs / DesktopWindowNativeApi.InspectVisualSurface`：一次直接GetWindowRgnBox替代临时HRGN副本，只改边界读取；实际缺失/尺寸错误继续失败，区域重装及事务不变。`DesktopVisualSurfaceReadback.RegionReadMethod`区分历史读回方式。
- `VisualRegionNativeReadbackTests.cs`：11项原生正反例/只读不修复/单次查询约束；无效复制目标故障仅证明API语义，不冒充自然故障的内部原因。原始自然失败及低干扰观察归里程碑§11.9。
- `PosterSpatialRuntime.Posture.cs / RestorePresentedGroup`的可选探针回调、`PosterSpatialRuntime.TitleSurfaceSmoke.cs`：新增最终恢复阶段真实HRGN清除；正常调用不安装回调，12案例/4清除由`smoke-title-surface.ps1`强制检查。
- `smoke-auto-lifetime.ps1`：continuous不使用硬上限60秒的通用启动退出参数，保留175秒探针取消/185秒脚本等待；不更改生产退出逻辑。
- `verify-r7-continuity.ps1`：2424合同门，17进程，18份proof含正常关闭的三模板DEMO种子及进程清理。`start-r7-review.ps1`：同包/源码/证明校验、独立数据目录与重开绑定，真实原生验收不代填PASS。
- 结果：final09 C1 AUTO COMPLETE；review09 D1/D2/D4已问范围USER PASS、D3未验；最终退出现场差异待核对，随后R7出口审查。本次只更新记录，无新增代码锚点，详见[§11.9](LR_MILESTONE_EXECUTION.md#119-bug-021读回路径整改与r7-d准入2026-10-09)。

## 2026-10-09 R7-C1短时连续可靠性

- `App.ContinuousLifetimeProbe.cs`：复用隔离lifetime入口，0/1/3/6张、每组20轮中心开→收起→全隐藏→中心关→重显→展开；原命令/CAS、会话代次、XAML/原生读回及资源所有权，不合成OS手势。新夹具仅首轮允许本机坐标物化，实际外框/用户字段不变，后续完整定义无漂移。
- `PresentationReadbackEquality.cs`、`PosterVisualWindow.Presentation.cs`的`VerifyPresentation`：验证专用的完整值比较；修正模板重读后字典/声明列表引用不同导致的假阴性，不改变生产绘制/空间策略，也不把paint-only准入当配置相等。
- `PresentationReadbackEqualityTests.cs`：17项同值重读/字典顺序/选中与未选参数/声明/丢失新增/标题等反例；链接同一验证helper，不复制测试模型。
- `scripts/smoke-auto-lifetime.ps1 -Mode continuous`：要求恰好20轮、正常退出/清理；原restart核对完整海报定义与revision。原exercise仍验证本机自有环境HWND消息分类与恢复；零海报提前完成路径不冒充系统信号已注入。
- `scripts/verify-r7-continuity.ps1`：冻结源码/候选/证明，组合12个lifetime进程、4个原组隔离/连续操作进程及1个R4故障进程；任何门失败不封存PASS，不强杀，不覆盖旧包。最终执行结果归[里程碑§11.8](LR_MILESTONE_EXECUTION.md#118-r7-c1短时连续可靠性实施2026-10-09)。
- C1不是原生菜单/桌面空白手势、真实锁屏/显示器/DPI、桌面合成像素或长测认证；这些分别留R7-D和总计划§14.1。

## 2026-10-08 R7-A/B完整配置往返

- Application/PortableProfileCodec.cs：v5必需中心主题/模板册，presentation v1～6严格数据验证；旧v1～4字段边界保持；raw参数不经过渲染投影重写。
- Application/PortableTemplateMerge.cs：纯定义级合并、内嵌变体保真、完整声明比较/确定性副本、缺模块只读与恢复容量预留，不放宽普通RegisterTemplate。
- Application/PortableProfilePlanner.cs、StateProjectionCoordinator.Portable.cs：原空间/待安置策略与完整CAS；模板目录独立于全局设置选择，创建/替换/目录单事务，重检参数及警告。
- Domain/PosterPresentationParameters.cs：ReadStoredParameters/ValidateStoredParameters区分可安全存储与模块是否可编辑；不赋予未知模板执行权限。
- App/PortableProfileController.cs、ControlCenterWindow.Portable.cs：原备份/文件哈希/原生环境守卫，更新导出范围和全局设置文案；不新增管理入口。
- R7PortableFidelityTests/R7PortableTransactionTests：完整实例/目录/标题false、兼容、冲突、回滚、重启再编辑；原Portable/ContentLayout/文件故障合同继续跑。
- App.PortableFidelityProbe.cs、App.PortableSmoke.cs及smoke-portable-profile.ps1 -FullConfiguration：0/1/3张实际controller与正常重启，未认证鼠标/原生选择器。
- SceneModuleFixture/PortableModuleFixture.cs、verify-scene-module-removal.ps1：物理缺Lamp/Rain时导出不变、导入只读/重启、模块恢复可编辑。
- scripts/verify-r7-portable.ps1：2396合同/4门/14应用进程＋两模块各5段console检查、544源码/161产物锁定。唯一证据与后续门归[里程碑§11.6](LR_MILESTONE_EXECUTION.md#116-r7-ab实施与验证2026-10-08)，不替整体R7或原生人工认证。

## 2026-10-08 R7-0可靠性

- `src/DesktopUp.Infrastructure/SqliteStateStore.cs` OpenWritableConnection：文件WAL＋Private cache；既有有界等待、读取快照、写事务/CAS不变。
- `tests/DesktopUp.Contracts.Tests/PosterCatalogTests.cs`：CatalogReadDuringUncommittedFirstSeed、HeldCatalogSnapshot、FailedSeed、ConcurrentFirstSeedRepeated（固定32新库）；原并发first-run测试保留。
- `src/DesktopUp.App/PosterVisualWindow.cs` CommitSpatialFrames/ApplyVisualRegion：SurfacePreparation与FinalRestore跨缓存重装HRGN，失败清缓存、严格原生读回；原Animation缓存复用不变。
- `PosterVisualWindow.SurfaceProbe.cs`＋`PosterSpatialRuntime.TitleSurfaceSmoke.cs`＋`scripts/smoke-title-surface.ps1`：自有视觉HWND区域丢失注入，11案例/3注入、共享恢复无数据库写入；不模拟真实鼠标。
- `ControlCenterWindow.ThemeProbe.cs`：中心控件和弹层控件的状态验证分阶段，避免同时禁用弹层锚点却要求弹层保持加载；所有颜色、状态及连接断言保留。仅探针修订，不改变产品开关/弹层行为。
- 候选、反例、完整门与后续便携合同唯一见[里程碑§11.4/11.5](LR_MILESTONE_EXECUTION.md#114-r7-0可靠基线实施2026-10-08)。便携仍v4，未实现v5。

## 2026-10-08 标题显隐

- PosterPresentationParameters.ShowTitle/IsTitleVisible与PosterPresentationPolicy：v6按需保存、旧版本默认显示、保留nullable显式值、拒绝未知/非法值与静默降级。
- PosterSetting.TitleVisibility/ControlCenterSettingsSession、ControlCenterWindow.AutoPosture.cs及LayoutFolder中的TitleVisibilitySwitch：原CAS与即时完成通道，无新状态库。
- PosterVisualWindow.TitleChrome.cs/CreateMeta：名称文字透明但布局不塌陷；TemplatePresentationRefreshPolicy/Content paint key不因开关重建Item；动作/云/原生命中不改。
- TitleVisibilityTests、PortableProfileModelTests、Lamp/Rain原生探针：目标隔离、单revision、保值/冲突、真实控件/切模板/收展/重启；证据归[模块§17](LR_SCENE_MODULE_EXECUTION.md#17-标题显隐与r7准入2026-10-08)。

## 2026-10-08 云尺寸与增强风摆（同包AUTO PASS / USER PENDING）

- TitleCloudPose.Size/Scale/Fit/Transform与PosterVisualWindow.PaintTitlePose：相同枢轴/缩放和有界摇摆，TitleCloudInputGeometry复用；1e-9数值尘埃不反复发布。
- TitleCloudInputGeometry.AllowsProtection与PosterInputIslandManager.CanUpdateTitleSize：显式尺寸与闲置风摆分离；只允许外部按键/捕获位，自身捕获/菜单/事务不豁免，尺寸更新不借权限推进风摆。
- RainDeclaration v3/cloud.size、RainSceneInstance.TitlePose、RainSceneMotion.Cloud：声明参数→场景提案→宿主；SceneCapabilityRegistry.PreviousBuiltIns兼容精确v1/v2，不改用户配方。
- TitleCloudMotionTests/RainSceneContractTests与App.RainSceneProbe：尺寸/DPI/周期、真实alpha差分/HRGN、无动画刷新、保存重启；候选证据归[模块§16](LR_SCENE_MODULE_EXECUTION.md#16-云尺寸参数与增强起伏倾斜2026-10-08)。

## 2026-10-08 最新：UI C风感与随机闪电（同包AUTO PASS / USER PENDING）

- Domain/TitleCloudShape.cs：UI C主体统一轮廓与保守内切条带，单槽有界缓存；TitleCloudPose.Fit：发布前拟合标题预算，避免整周期出框。
- App/PosterVisualWindow.TitleChrome.cs：唯一云组、固定名称/按钮、两片有界内部材质；雨/闪电不进入云命中。
- Modules/Rain/Application/RainSceneMotion.cs：19秒升降/偏移/摇摆与27/23秒材质相位；RainLightningSchedule.cs：按活动时间的间隔/逐次种子，RainLightningGeometry.cs：有界变位及分叉连续性。
- RainDeclaration.cs：间隔动态控件及精确旧内置声明；TemplateRecipeInstancePolicy.InstalledBuiltInRevision、PosterPresentationParameters.Read：只读默认投影/显式编辑保存，不覆盖自定义声明。
- App.RainSceneProbe.cs：独立云层5位置边界、实际HRGN、闪电seed像素对照、间隔控件/保存重启；TitleCloudMotionTests/RainLightningScheduleTests/RainSceneContractTests覆盖几何与兼容。
- 候选与失败证据见[模块§15](LR_SCENE_MODULE_EXECUTION.md#15-ui-c风感对齐整周期防裁切与随机闪电2026-10-08)；下方§14为被用户要求续修的候选25，非当前验收入口。

## 2026-10-08 最新：整云动态命中与贯穿闪电（AUTO PASS / USER PENDING）

- Domain/TitleCloudPose.cs、Application/TitleCloudInputGeometry.cs：共同有界变换，物理像素行保守内切命中；TitleCloudMotionTests覆盖不同DPI/仅标题替换/暂停。
- App/PosterVisualWindow.TitleChrome.cs：宿主云形变换、明确标题Clip及title-only发布；PosterVisualWindow.cs的全量输入更新复用当前云位姿。SceneHost只读模块提案、原所有者裁决；IPosterTitleMotion取代旧IPosterTitleDecoration。
- Windows/PosterInputIslandManager.cs：CanAnimateTitle/TryApplyTitleMotion保护捕获与菜单，原生HRGN更新及拒绝回退，不动窗口或完整布局；VerifyTitleRegionForProbe读回实际HRGN。
- Modules/Rain/Application/RainLightningGeometry.cs与App/RainLightningDecoration.cs：3路径16点的贯穿电光及两条固定描边；RainSceneInstance使用原共享时钟，单独累计云活动时间，无额外timer。RainTitleDecoration风纹/小闪电退役。
- App.RainSceneProbe：固定整海报根表面的采样坐标，整云alpha轮廓、实际HRGN同步/非标题不变、持有路由捕获及上中下闪电像素；candidate25同包16门/2308合同/42进程通过。真实鼠标首拖与桌面合成仍待人工，不以旧§13 PASS交付。详见[模块§14](LR_SCENE_MODULE_EXECUTION.md#14-整云摆动动态命中与贯穿闪电2026-10-08)。

## 2026-10-08 历史：设置夹保留，Rain标题前景装饰已退役

- `src/DesktopUp.App/ControlCenterWindow.xaml`：LayoutFolder/LayoutFolderPanel收纳通用外观与浏览；ContentLayoutPanel外层保留所选Item入口；原事件/草稿/只读门不变。
- `ControlCenterWindow.UiCSample.cs`、`App.UiCSampleProbe.cs`：7场景宽窄折叠/独立展开，折叠按声明关系验证，展开验证实际可见边界；TitleColors/SettingsSmoke探针显式展开父级。
- `src/DesktopUp.App/Scenes/IPosterSceneInstance.cs`：可选IPosterTitleDecoration；`PosterVisualWindow.SceneHost.cs`与`TitleChrome.cs`在静态云与文字之间接入，释放/失败时共同卸载，不新增输入协议。
- `Modules/Rain/App/RainTitleDecoration.cs`：复用Domain标题形状，固定主体上的光带/风纹及云下60～82DIP闪电；有界节点，无时钟/命中/状态写。`RainSceneInstance.cs`只在Cloud标题下隐藏旧后景整云，同一共享更新驱动前后两层。
- `Modules/Rain/App/App.RainSceneProbe.cs`：真实标题层XAML像素对照（大风／无风、闪电开／关）及原命中、对象、层级不变；确定时刻绘制不等于桌面连续帧认证。ResourceProbe计入新增5个固定装饰节点，每场景72个逻辑绘制节点，仍64雨滴上限。
- 详细候选、测试与人工范围归[模块§13](LR_SCENE_MODULE_EXECUTION.md#13-rr-06收口与r7前增量修订2026-10-08)。RR-06原验收已关闭；下节待人工为历史。

## 2026-10-08：无底板标题与交互追光

- Domain/PosterPresentationParameters.cs：按需v5 titleColors、严格解析及旧字段保留；Application/ControlCenterSettingsSession.cs沿原命令编辑标题色。
- App/PosterVisualWindow.TitleChrome.cs、SurfacePaint.cs、Content.cs：独立字色/符号色，无背景板，标题色不触发Item缓存失效；TitleColors.cs与XAML折叠小节复用LampColors.cs共享弹层。
- Application/ScenePointerAdmissionPolicy.cs、App/PosterSpatialRuntime.SceneMotion.cs、PosterVisualWindow.SceneHost.cs：只读装饰准入及刷新样本连续性，原自动收展保护不变。
- tests/TitleColorTests.cs、ScenePointerAdmissionTests.cs、PortableProfileModelTests.cs；Modules/Lamp/App/App.LampParameterProbe.cs与probe-lamp-parameters.ps1新增真实控件和原Item协议门。输入不是OS自动化，最新证据见模块§12。

## 2026-10-08：RR-06组合验证与删除恢复

- `src/DesktopUp.App/App.PostureCombinationProbe.cs`：显式`--posture-groups-probe --rr-combinations`隔离分支；真实R4接受首帧后暂缓以确定性穿插B参数/Item协议、自动A与手动B、锁定邻居、删除/排队/迟到回调。正常启动不安装观察器，不模拟OS输入，不用于时长或像素认证。
- `src/DesktopUp.App/PosterSpatialRuntime.Posture.cs/RestorePresentedGroup`：仅committed确认已删除成员免除“无恢复几何但HWND仍在”的误报，释放租约并停用输入；存活/未知状态严格恢复。BUG-033红绿证据见缺陷清单。
- `scripts/probe-posture-groups.ps1 -Combinations`：两独立进程，完整退出/error/资源/hash门；原Groups分支保留。
- `scripts/verify-rr06.ps1`：新out目录中2274合同及16项同包门，源码/运行文件不变才生成Schema2 seal；任何失败保留证据且停止，不封存。
- `scripts/start-lr-rain-review.ps1`：Schema2在原11门上增加Groups/Combinations/Spatial/AnimationRain/AnimationLamp，逐文件及proof哈希校验后新建隔离DEMO。Schema1仅兼容其原证据范围，不给旧包补新PASS。
- 本轮不处理BUG-031、不进入R7；当前自动/人工结果见[模块执行记录](LR_SCENE_MODULE_EXECUTION.md)最新节。

## 2026-09-30：RR唯一标题、呈现节奏与收展组隔离

- `src/DesktopUp.Domain/PosterTitleChrome.cs`＋`src/DesktopUp.App/PosterVisualWindow.TitleChrome.cs`：唯一云标题实体/保守命中带/固定动作槽位/独立底板；`PosterVisualWindow.cs`发布原输入岛，`src/DesktopUp.Domain/PosterPresentationParameters.cs`共用度量。
- `src/DesktopUp.Domain/TemplateParameterValues.cs`、`Modules/Rain/Domain/RainDeclaration.cs`、`src/DesktopUp.App/PosterVisualWindow.SceneHost.cs`：注册能力→已校验颜色/标题kind→只读scene context。场景失败不带走宿主标题。
- `Application/SpatialAnimationSchedule.cs`：Create保留证明帧，CreatePresentation按时间合并同段，cursor精确前缀；`PosterSpatialRuntime.Posture.cs`原R4呈现/恢复与Title接管兼容。
- `Application/PostureGroupCoordination.cs`：整路径预约、完整CAS rebase、pending策略；`App/PosterSpatialRuntime.PostureGroups.cs`：多收展组、最新意图、失效/peer及无关编辑准入；`PosterSpatialRuntime.Title.cs`仍保留全空间捕获保护。
- `App/App.PostureGroupsProbe.cs`＋`scripts/probe-posture-groups.ps1`：实际WinUI/R4/SQLite隔离门，不模拟原生鼠标；`PosterTitleChromeTests`、`SpatialPresentationTimingTests`、`PostureGroupCoordinationTests`为对应纯合同。
- 同包证据与未验范围归[模块§10](LR_SCENE_MODULE_EXECUTION.md#10-rr整改生产接线与同包验证2026-09-30)。BUG-031延期，旧取证入口保留，不以提速当修复。

## 2026-09-30：RR-01取证与唯一标题纯模型（未接生产）

- `src/DesktopUp.Domain/PosterTitleChrome.cs` / `tests/DesktopUp.Contracts.Tests/PosterTitleChromeTests.cs`：固定动作/名称、保守云抓取区、空白透传和基础回退；纯模型，原生产标题仍未切换。
- `PosterVisualWindow.Transition.cs/ReadTransitionForProbe`：显式诊断读取根/structure/clip/footer与控件身份；`BeginTransitionLayout/ApplyTransitionViewport`仍为原固定收展租约，responsive缩框独立。
- `Modules/Lamp/App/App.BackdropContinuousProbe.cs` / `App.BackdropProbe.cs`：显式有界、不暂停动画的连续取证；自身WGC/桌面局部双平面或完全不启用WGC的screen-only分支；采样等待扣除BitBlt时间。前台遮挡检查有已知漏检，必须核图；正式启动不采集。
- `scripts/probe-posture-live.ps1`：独立源库副本/新out/hash/模式守卫；`scripts/analyze-posture-capture.ps1`：离线宽白行筛选及QPC近似关联，未命中不是PASS；`scripts/verify-rain-title-sample.cjs`：独立headless局部HTML检查。当前结论见[RR-01基线§9](LR_SCENE_MODULE_EXECUTION.md#9-rr-01无遮挡重复基线2026-09-30)，RR-02生产未接。

## 2026-09-30 当前：Rain受信任模块和独立验证入口

- Modules/Rain/Domain/RainDeclaration.cs：8项专属参数和通用TransparentSurface/ReplacesMetaText声明；无无效背景透明度控件。
- Modules/Rain/Application/RainSceneMotion.cs：云/雨/微光的纯数学、64滴上限、暂停/时钟重置不补跑；不参与item solver。
- Modules/Rain/App/RainSceneInstance.cs、RainRegistration.cs：固定装饰树、共享时钟帧驱动、保留内容身份/选择样式；模板册样片独立注册。固定代码内的原型路径不是导入XAML执行入口。
- Application/SceneFrameContext.cs新增只读HeaderBottom/ContentTop/FooterTop；ScenePresentationAdapter按可信能力声明处理透明背景和顶部装饰，宿主无Rain/Lamp模板ID判断。
- RainSceneContractTests.cs：13项追加合同；ArchitectureTests同时检查Lamp/Rain无输入/事务/私有计时器依赖。
- Modules/Rain/App/App.RainSceneProbe.cs、App.RainResourceProbe.cs：实际表单/颜色/副本/热装入/逐海报参数/重启及1/3/6最大雨量/暂停基线；经App显式隔离目录/单probe门。
- scripts/probe-rain-scene.ps1、verify-scene-module-removal.ps1 -Module Rain|Lamp：连续门/资源采样、实际不复制对应模块源码后构建/备份/恢复。
- scripts/start-lr-rain-review.ps1：只从封存证据和完整产物hash启动独立三海报人工候选，不用旧包或日常配置冒充验收。
- Infrastructure/SqliteStateStore.cs的CreateBackupFromOpenConnection、StateBackupRecoveryTests.cs：BUG-028新备份封存DELETE journal红绿门，未清理旧备份或修改恢复拒绝规则。

以下同日B/C描述为对应阶段历史，当前结论以[模块实施记录](LR_SCENE_MODULE_EXECUTION.md)为准。

## 2026-09-30 本轮：C参数兼容与D物理移除门

- Domain/TemplateParameterValues.cs、TemplateRecipeInstancePolicy.cs：通用不可变参数、能力声明、逐模板实例；Compatibility/LegacyTemplateParameterCalls.cs仅适配v3/v4旧灯光调用。
- Domain/TemplateRecipes.cs、PosterPresentationParameters.cs：入站严格校验与存量缺能力只读分开；缺模块不丢原JSON，不准通过保存重置。
- Directory.Build.targets、Modules/Lamp/{Domain,Application,App}：构建期可选源码收集和受信任初始化注册；Lamp声明/运动/绘制/样片及专属诊断在模块内，不是用户目录插件加载。
- App/PosterVisualWindow.CaptureProbe.cs、PosterSpatialRuntime.SurfaceProbe.cs：通用内容捕获和原收展观察接口，不能随某个装饰模块一起删除。
- SceneModuleCompatibilityTests.cs、App.SceneModuleProbe.cs、tests/DesktopUp.SceneModuleFixture、scripts/verify-scene-module-removal.ps1：异构schema、只读存量、物理排除源码构建、同库启动/备份/恢复/重新装回的证据入口。D运行结论见模块记录。

## 2026-09-30 当前：模块B通用宿主与基础绘制

- `Application/SceneFrameContext.cs`：只读几何/指针/暂停原因，以及逐实例故障锁；不含输入或持久命令。
- `Application/ScenePresentationAdapter.cs`：v4旧保存字段的只读兼容桥，输出通用bindings与SurfacePaint；未迁移持久模型，C仍须消除Lamp类型依赖。
- `App/PosterVisualWindow.SceneHost.cs`：装饰实例生命周期、单实例故障隔离；`SurfacePaint.cs`与`Backdrop.cs`独立静态绘制；宿主无Lamp分支。
- `App/PosterSpatialRuntime.SceneMotion.cs`：原唯一共享时钟/被动观察/减少动效和资源账本；没有新输入通道。
- `App/Scenes/IPosterSceneInstance.cs`、`SceneModuleRegistry.cs`、`LampSceneInstance.cs`：可信编译期绘制边界、注册入口和独立Lamp。物理可移除清单/通用参数保存属于C/D，未以本次类提取冒充完成。
- `SceneHostContractTests.cs`与`ArchitectureTests.cs`：暂停、材质/参数保全、故障不重试与依赖边界；`App.LampParameterProbe.cs`新增装饰故障注入，沿受隔离保护的旧入口验证内容/其他海报/背景/资源不受损。

旧`PosterVisualWindow.LampScene.cs`和`PosterSpatialRuntime.LampMotion.cs`已迁入上述文件；历史定位按原候选/源码快照查阅。当前验证见[模块记录](LR_SCENE_MODULE_EXECUTION.md)。

## 2026-09-30 当前：R6第三批资源与连续组合

- `Domain/ContentLayoutFlow.ProjectTiles`可选一节距overscan准入，不更改viewport/loop语义；`Application/ContentAssetDemand.cs`复用tile，去重ItemId，visible优先，完整DPI/尺寸/source version key。
- `Application/ContentAssetCache.cs`保留真实flight预算，新增槽位完成通知/`IsLoading`；`App/PosterVisualWindow.ContentAssets.cs`负责合并补位、同key返回、失败不自旋及生命周期/可见key再校验。
- `Application/ContentSourcePorts.cs`是可信view/resource/open适配与安全元数据投影；`App/PosterSpatialRuntime.ContentSource.cs`按目录身份绑定，`PosterVisualWindow.ContentSource.cs`负责notify订阅、版本/编辑失效和R4安全点。普通生产仍DEMO，不执行真实文件/脚本。
- `ContentProposals.ProjectContentEdit`仅用同owner/itemset的热度校验候选，新成员意图不套旧热度；CAS成功前不清空原last-good。来源R4刷新复用原proposal恢复入口。
- `App.ContentSourceProbe.cs`＋`probe-content-source.ps1`的隔离1/3/6受控provider；`ContentLoopDragProbe`新增接缝Resize/history。全量旧smoke夹具更新为合法跨轴空间与保留旧区域，原生产保护不放宽。
- 人工入口`scripts/start-lr-r6-review.ps1`复用已绑UI C门并追加R6源/浏览/资源/合同/R4证据；`CheckOnly`不启动、不冒称验收。阶段及最终候选只查[R6记录](LR_R6_CONTINUOUS_OPERATIONS.md)与HANDOFF。

## 2026-09-28 当前：R6第二批2B

- `Domain/ContentSpaceOwnerAdmission.cs`从原空间owner的工作区/DPI和完整Item下限分类原因；既有`ContentSpacePolicy.cs`决定准入/安全点/恢复，新增`ContentSpacePublication.cs`只保存最后成功发布，拒绝部分失败、pending和过期见证。
- `App/PosterVisualWindow.ContentSpace.cs`管理RAM环境/发布见证及受限导航；`PosterSpatialRuntime.Effective.ContentEnvironment`复用运行代次；`PosterVisualWindow.Show/Recover/AdoptPosterProjection`接权威环境和最新作者/成员；`PrepareSpatialFrame/CommitSpatialFrames/ApplyFrame`区分候选与成功整组提交，`Transition.RestoreTransitionLayout`使终态重算。
- `Content.cs`/`ContentEditing.cs`/`ContentMenu.cs`统一携带获准BrowseMode到Reclip、绘制、命中与抓点；`Application/ContentWheelScrollPolicy.cs`仅受限时允许另一轴且有限夹紧；`ContentFeedback.cs`在原底栏显示导航；`ContentProposals.cs`受限时保留有界热度提案、恢复后排队消费。
- `ControlCenterWindow.ContentFields.cs`的草稿增加surface session/空间epoch见证，原文保留但旧草稿不能自动复活；海报物理切换先取消旧手势/定时回调。
- `ContentSpacePublicationTests.cs`新增12项合同；`PosterVisualWindow.ContentSpaceProbe.cs`→`App.ContentBrowseProbe.cs`→`scripts/probe-content-browse.ps1`在真实生产通道输入模拟显示事实，中心探针复用真实草稿校验器。证据/边界见[R6记录§6](LR_R6_CONTINUOUS_OPERATIONS.md#6-第二批2b被动空间不足与恢复2026-09-28)。下一步tile资源/假源及完整连续门；以下“2B未接”为历史。

2026-09-28按需打包：[Preview流程/撤回与证据](PREVIEW_INSTALLER_DELIVERY.md)。`App/DistributionBuild.cs`＋`DesktopUpPreview`隔离身份/root并读取有界声明式新建默认文件；`Application/PosterCreationDefaults.cs`与17项合同校验内置模板、背景不透明度及格式边界。`PosterVisualWindow.CreateInitialDefault`和`PosterSpatialRuntime.ControlCenter.Create`共用该入口，复制分支/保存配置不受影响；`App.xaml.cs`记录实际默认值及无效文件警告。`Properties/PublishProfiles/Preview-x64.pubxml`负责双自包含及可调.NET版本。根`Package-Preview.cmd`→`scripts/package-preview.ps1`读取`packaging/preview/package-settings.json`，执行新目录打包、源码/测试绑定、licenses与hash；`installer.nsi`负责自选路径、安装位置绑定和精确安全卸载。主线布局/schema及下方R6阶段不变；完整用法见[打包README](../../../../packaging/preview/README.md)。

最新（2026-09-28，R6第二批2A）：`Domain/ContentFrameMinimumPolicy.cs`从唯一solver得跨轴下限，`PosterVisualWindow.Content.cs/TryReadContentFrameMinimum`合并当前有效与作者下限，`PosterVisualWindow.OnInput`冻结到原空间会话；`PosterSpatialRuntime.ControlCenter`先验当前呈现，`ControlCenterSpatial`/`SpatialGestureCommitPolicy`重验权威内容；`PosterFrameResizePolicy`安全拒绝不可能固定边缩框。`ContentFrameResizeTests`及`PosterVisualWindow.ContentFrameProbe`→原浏览探针提供证据，见[R6记录§5](LR_R6_CONTINUOUS_OPERATIONS.md#5-第二批2a主动缩框下限2026-09-28)。下一步仍为2B ContentSpacePolicy/ReadOnlyOverflow生产发布/恢复接线，不重做主动下限；以下第一批“主动下限待做”为当时快照。

当前（2026-09-28，R6第一批）：`Domain/ContentDragNavigation.cs`保存实际副本/按下内点/未折返累计导航；`ContentDragPolicy.Map`已支持循环规范化，不再拒绝活动loop。`PosterVisualWindow.ContentEditing.cs`接首按/16ms原调度/末次松手/取消及结束锚；`Content.cs`优先消费一次性结束锚并在表面释放时清空。`ContentLoopDragTests`/`ContentDragNavigationTests`及`PosterVisualWindow.ContentLoopDragProbe`→`App.ContentBrowseProbe`→`scripts/probe-content-browse.ps1`提供第一批证据，见[R6记录](LR_R6_CONTINUOUS_OPERATIONS.md)。下一批仍需ContentSpacePolicy、主动MinimumCrossAxisDip、ReadOnlyOverflow；随后tile资源/假源。下方09-27“先接抓点”的文字是实施前地图，不表示此批仍未接线。

当前阶段/修改入口（2026-09-27）：[R5收口及代码/计划审计§3～4](LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)汇总已修旧机制与R6真缺口。R6先接`ContentDragPolicy.Map`、`PosterVisualWindow.ContentEditing.FlushContentMotion`的循环抓点/累计导航/结束锚，再接`ContentSpacePolicy`、主动MinimumCrossAxisDip、ReadOnlyOverflow和tile资源集；候选25已有wheel/TileKey探针不重复制作。模块解耦与Rain排在R6后，当前`TemplateRecipeInstance`/`PosterVisualWindow`仍依赖Lamp；Portable v4拒绝自定义目录/实例和非light中心主题，不能据R4旧报告称全配置可导出。

最新循环反向修正：[报告§9](LR_R5_POINTER_AND_MODULE_REVIEW.md#9-候选25循环起点反向滚动与主线审阅节点)。生产入口`PosterVisualWindow.Content.cs/ScrollItems`→`Application/ContentWheelScrollPolicy.Advance`→既有Flow规范化/取模→Reclip、绘制和Input发布；只保留RAM偏移，不提交布局。合同`ContentWheelScrollPolicyTests`覆盖两轴/小增量/长序列/边界；`App.ContentBrowseProbe`与`scripts/probe-content-browse.ps1`检查真实共享Scrolled处理器/XAML/发布区域。App.xaml.cs新旗标受单探针与隔离DataRoot守卫，日常启动不运行测试。

最新展开过程修正：[报告§8](LR_R5_POINTER_AND_MODULE_REVIEW.md#8-候选24展开过程灰底与ab不透明度对照)。`DesktopRoundedRegion`定义物理局部圆角区域；`DesktopLayerAttachment.ClearExposedVisualBackground`守护视觉HWND归属，native `ClearExposedBackground`以RGN_DIFF及ERASENOW清新区域；`PosterVisualWindow.CommitSpatialFrames/ApplyFrame`在容量/HRGN就绪后调用，失败清缓存，FinalRestore完整擦底保留。`App.BackdropProbe`、`PosterSpatialRuntime.Posture`隔离采样钩子、`scripts/probe-poster-backdrop-motion.ps1`提供A/B及三个中间HRGN原生像素对照；普通运行钩子为空。

最新灰底/灯光诊断：[报告§7](LR_R5_POINTER_AND_MODULE_REVIEW.md#7-候选23半透明收展灰底与系统动画策略)。生产修正`DesktopLayerAttachment.cs/RequestFrameRedraw`在FinalRestore后加入RDW_ERASE；App.BackdropProbe.cs与scripts/probe-poster-backdrop.ps1提供自身HWND原生截图及终态像素回归。灯光系统策略入口`PosterSpatialRuntime.LampMotion.cs/SetLampMotionDemand/ArmLampMotion/LampAnimationsEnabledChanged`未改；ControlCenterWindow.ThemeProbe.cs补逐状态连接和实际主题证据，避免把卸载弹窗当可见主题验收。

最新修改入口见[本页输入与模块小节](#2026-09-27-最新输入与模块入口)，状态见[输入报告](LR_R5_POINTER_AND_MODULE_REVIEW.md)。以下较早“只读/未接”按其当时阶段理解。

## 2026-09-27：R5意图与UI C纠偏入口

[对齐记录](LR_R5_UI_C_INTENT_ALIGNMENT.md)逐项连接E1六任务、用户确认及代码。优先入口：`PosterVisualWindow.Content.cs/DrawContentSnapshot`与`ContentEditing.cs/FlushContentMotion/CancelContentGesture/CommitContentDocument/AddContentMenu`（结果/预告/恢复），`ContentActivation.cs/DispatchContentOpen`（DEMO可见结果），`PosterInputIslandManager.HandleMessage`与`PosterVisualWindow.HandleInvocation`（Item就近管理）；`ContentLayoutEditResult.AdoptsProjection`已有模型但App未消费。

中心纠偏入口为`ControlCenterWindow.xaml`、`.UiCSample.cs`、`.Editing.cs`、`.TemplateParameters.cs`和`.ContentLayout.cs`。保留`.ContentFields.cs`的唯一草稿/成组事务，不能为减少表单改成逐字符保存。当前仅只读定位、未改源码；R5 IN PROGRESS，执行次序归主计划§4.2.1，不沿下方历史PENDING推导新状态。

## 2026-09-26：R5 固定候选通过协议/控件验证，早期人工 PENDING

R5 仍 IN PROGRESS。候选3的13组真实处理器协议、同库进程重启、字段事务/提案唤醒、非法有效预览拒绝及Lamp回归通过；全量合同为1921/1921。四张最新宽窄截图已查看。系统鼠标尝试因遮挡零输入停止，不能称为NativeMouse通过。已打开独立 `human-review-01/data`（启动PID2812）并请用户体验；人工结果尚未收到，不准自动进入R6出口。源码/包/证据、运行入口与具体限制见[核心交互记录](LR_R5_CORE_INTERACTION.md)。

所有子agent文件已移交root，未提交/推送/迁移日常数据；正在运行的体验实例不得被自动测试关闭或重建。R6只读接线地图在[执行记录§9](LR_MILESTONE_EXECUTION.md)，尚未实施。下方为历史阶段事实。

## 2026-09-26：R4保存合同/代表模板完成，进入R5

当前R1合同齐全，R2/R3完成，**R4 SAVE CONTRACT + SAMPLE READY / R5 IN PROGRESS**。1868项合同全部通过；完整CAS、逐操作保真、模式返回态、尺寸覆盖、Portable v4和草稿仲裁已有真实SQLite证据。独立原生候选完成Lamp六参数/取消/冲突/两海报/中心重开验证，退出资源0。详见[执行记录§7～8](LR_MILESTONE_EXECUTION.md)及[配置保真报告](LR_R4_CONFIGURATION_FIDELITY.md)。

R5入口为ContentEditing首按拖动、点击/打开仲裁和UI C操作接线。当前仍DEMO，未提供真实文件操作。原生鼠标/键盘/滚轮、App同库进程重启、光池截图异常与最终UI C视觉尚未通过；不要沿用下方历史PASS。原有工作区/数据不覆盖，不自动Git提交/推送。

## 当前：R3完成，R4开始（2026-09-26）

- `ContentLayoutSolver.Resolve`：唯一主/交叉轴占用、跨度不缩、普通Manual从原意图投影、Legacy冲突分类；`Reclip`要求交叉轴/度量见证一致。
- `ContentLayoutFlow` / `ContentBrowseModel`：有限/循环资格、signed offset、同身份tile、物理XY锚与删除后继；`ContentSpacePolicy`：被动原因/有效环境见证/安全恢复纯模型，不能仅C<M自动授权双轴。
- `PosterVisualWindow.Content.EnsureContentSnapshot`：完整宽高度量缓存、新tile锚、派生只读；未知原文的空回退不得交给E当有效几何。
- `ControlCenterWindow.xaml` / `.UiCSample.cs`、`App.UiCSampleProbe.cs`、`scripts/probe-ui-c-sample.ps1`：UI C中心原生骨架及四状态隔离观测。并未接新两模式或代表场景效果。
- `ContentLayoutCommandPolicy.ValidateAuthoring`：轴对称canonical写入闸。Portable/Profile/Clone仍复用，R4须分开无损存档与编辑；Editing/热度提案也需当前空间闸。来源、测试与限制见[执行记录](LR_MILESTONE_EXECUTION.md)。

## 当前：R2保真修复，R3模型实施中（2026-09-26）

- `src/DesktopUp.Application/PortableContentLayout.cs`：`PortableContentLayoutCodec.Export/Import`保留ReservedSpaces的独立只读集合快照，不清字段、不放宽既有authoring校验。
- `src/DesktopUp.Domain/ContentLayout.cs`：`ContentLayoutPolicy.Validate`在访问Name.Length前拒绝null；未知或非法载荷保留原文并明确失败。
- `tests/DesktopUp.Contracts.Tests/ContentLayoutPortableTests.cs`：完整字段/独立注入/调用方数组修改/非法数据/身份映射，真实ExecutePortable→SQLite→store重开→再导出；R2定向25、全量1664通过，构建0警告错误，证据归[VERIFICATION](VERIFICATION.md)。
- R3正在修改Solver/Flow派生合同；下方只读审查是当时源码事实，后续完成后按实际实现补锚点。持久schema及旧枚举不因统一几何而删除；每单元归属见[执行记录](LR_MILESTONE_EXECUTION.md)。

## 最新：操作与真实空间不足方案（只读核对，无生产修改）

[操作与空间方案](../../20_Architecture/CONTENT_LAYOUT_OPERATION_SPACE_REVIEW_V1.md)§7对应现源码差距：ContentLayoutSolver.Resolve的squeeze-single-column、ResolveHorizontalLoop的squeeze-single-row/viewport-height-clamped不能当作DEC-011/022实现；Reclip旧轴分支不能证明受限资格/恢复；ContentLayoutFlowPolicy.SetFlowAxis会转换旧Manual→Anchored，不能用它进入临时二维浏览；PosterSpatialRuntime.Effective.ResolveEffectiveBounds继续是外框有效空间入口。未修改上述源码，不关闭旧Bug。

E1仅确认体验任务，尚无受限双轴实现；后续应通过有效浏览策略消费同一布局/呈现快照，不修改保存FlowAxis或创建第二个空间所有者。正式验证矩阵见新文稿OS-01～12，仍NOT RUN。

## 最新：整体体验E1（仅Docs原型，非生产接线）

- [experience.html](../../20_Architecture/Designs/content-layout-experience.html)与[CSS](../../20_Architecture/Designs/content-layout-experience.css)：六任务、基础手势/退出试验条件、最小控制中心、独立海报、预期/实际记录。保持已采纳UI C原稿。
- [experience.model.js](../../20_Architecture/Designs/content-layout-experience.model.js)：8项DEMO、普通纵向有限投影/局部推让、整图承接、顺序图钉、当前画面切手动及退出A/B；仅候选几何，不调用或替代生产solver。
- [experience.js](../../20_Architecture/Designs/content-layout-experience.js)：稳定Item节点直接捕获、模拟打开、取消与整次内存撤销、中心关闭继续操作；无真实文件/数据库/网络。试验热度注入在拖动时禁用，不冒充原生调度。
- [experience.check.cjs](../../20_Architecture/Designs/content-layout-experience.check.cjs)：41项纯演算/无头Edge检查；不是正式合同/原生输入门。

范围与风险归[体验评审](../../20_Architecture/CONTENT_LAYOUT_EXPERIENCE_REVIEW_V1.md)。既有ContentLayout/原生/R4/CAS锚点未变；旧源码问题未修。

## 最新：DEC-020手动构图只读核对（无生产修改）

[手动专项](../../20_Architecture/CONTENT_LAYOUT_MANUAL_COMPOSITION_REVIEW_V1.md)§7记录锚点：ContentLayoutEditing.cs的Mode分支（约80～112行）退出会清Column/Row并按旧ManualExit改覆盖；PushManualPeers（131～170行）按previous.ReadOrder推让，166行只更新Y到blocker.Bottom。ContentLayout.cs的单组Placements不代表已有双模式独立构图记忆，也不应为已被否决的该方案新增仓库。后续要保留单命令/full CAS，当前事实不能冒充DEC-020实现。

DEC-019只读核查补充：`PosterVisualWindow.ContentEditing.HandleContentInput`的canEdit仍以IsEditing门控；`ControlCenterWindow.ContentLayout.cs`仍显示Automatic/Anchored/Manual三类。新目标为两模式＋两入口，须保留旧枚举兼容；不是删判断/改标签即可完成。生产代码本轮未改，组合与安全接线待LR-R1收口。

## 当前：2026-09-24 UI C已采纳，图钉/拖动组合审查（无生产修改）

[组合文稿](../../20_Architecture/CONTENT_LAYOUT_PIN_DRAG_REVIEW_V1.md)对应DEC-017/018。只读复核：`src/DesktopUp.Domain/ContentLayoutEditing.cs`的Apply/Move（坐标固定）与Pinned（顺序锚）仍不同；`src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs`的HandleContentInput/canEdit仍有旧门槛；`src/DesktopUp.Application/ContentLayoutCommand.cs`的Prepare保留单语义版本和完整目标校验。前两者是后续整改锚点，未被本文修复；后者不可放宽。UI C文件未改，原型认可不等于有原生图钉实现。

最新C细化：[c.html](../../20_Architecture/Designs/content-layout-controls-c.html)中folder-body包含template-settings、scene-option和property-section；[c.css](../../20_Architecture/Designs/content-layout-controls-c.css)定义雨云留白/透明材质/风动暂停；[c-scenes.js](../../20_Architecture/Designs/content-layout-controls-c-scenes.js)负责受控云形、DEMO图形、尺寸内约束与逐海报模板动效偏好；[c-model.js](../../20_Architecture/Designs/content-layout-controls-c-model.js)声明cloud.wind。原core不变；检查扩为60组，仅原型。

## 2026-09-24：UI 原型 C（非生产代码）

[页面](../../20_Architecture/Designs/content-layout-controls-c.html)与[设计](../../20_Architecture/CONTENT_TEMPLATE_SCHEMA_UI_C.md)为最新入口。[c-model.js](../../20_Architecture/Designs/content-layout-controls-c-model.js)提供校验/注册/实例值；[c.js](../../20_Architecture/Designs/content-layout-controls-c.js)只有一套 schema 表单工厂，并负责草稿、候选与确认装入；[c-scenes.js](../../20_Architecture/Designs/content-layout-controls-c-scenes.js)消费受控绑定；[c-library.js](../../20_Architecture/Designs/content-layout-controls-c-library.js)替换 C 页固定表单，旧 B+ 库不变。对应 [原型检查](../../20_Architecture/Designs/content-layout-controls-c.check.cjs)与[导入样例](../../20_Architecture/Designs/template-mint-rain.example.json)。不代表生产热更新加载器。

最新雨天修订：[scene CSS](../../20_Architecture/Designs/content-layout-controls-scenes.css)的data-scene=rain去卡片、调整绘制层序；[scene JS](../../20_Architecture/Designs/content-layout-controls-scenes.js)内联云形、全宽固定雨粒、云体占位和副标题显隐。顶灯计算/样式与B+核心几何脚本不变；仅原型。

## 2026-09-24：场景模板探索（非生产代码）

[场景CSS](../../20_Architecture/Designs/content-layout-controls-scenes.css)绘制灯具、局部光池、纸质/玻璃Item、固定雨粒和边框柔光；[场景JS](../../20_Architecture/Designs/content-layout-controls-scenes.js)合并一次性指针帧、静态降级和可见性/整理暂停。[studio](../../20_Architecture/Designs/content-layout-controls-studio.js)增加样片折叠；B+共用模板表单只补可选effect/family保留与缩略图标记，B+不加载新效果。核心几何脚本未修改。设计边界见[场景评审](../../20_Architecture/CONTENT_TEMPLATE_SCENES_REVIEW.md)。

## 2026-09-24：样片夹呈现探索（非生产代码）

[HTML](../../20_Architecture/Designs/content-layout-controls-studio.html)、[CSS](../../20_Architecture/Designs/content-layout-controls-studio.css)、[翻页/样片选择](../../20_Architecture/Designs/content-layout-controls-studio.js)复用B+的模板/主题状态与原缩框脚本。B+仅主题文案、复用选择入口和可选演示初始主题调整；没有生产solver/状态迁移。完整候选范围见[样片夹说明](../../20_Architecture/CONTENT_LAYOUT_UI_REVIEW_STUDIO.md)。

## 2026-09-24：B+模板库/配色原型（非生产代码）

[B+ HTML](../../20_Architecture/Designs/content-layout-controls-bplus.html)、[扩展样式](../../20_Architecture/Designs/content-layout-controls-bplus.css)、[内存模板/配色逻辑](../../20_Architecture/Designs/content-layout-controls-bplus.js)、[缩框演示副本](../../20_Architecture/Designs/content-layout-controls-bplus-core.js)；复用未修改的B基础CSS，solve函数与B一致。不是主题加载器、持久化仓储或生产布局实现。范围与假设归[B+说明](../../20_Architecture/CONTENT_LAYOUT_UI_REVIEW_BPLUS.md)。

## 2026-09-23：UI方案B（非生产代码）

[HTML](../../20_Architecture/Designs/content-layout-controls-v3.html)定义目录/外观/行为/独立演示区，[CSS](../../20_Architecture/Designs/content-layout-controls-v3.css)定义深色面板、节点、按需提示与覆盖式滚动条，[JS](../../20_Architecture/Designs/content-layout-controls-v3.js)仅提供内存状态、提示/滚动与评审用保序排布。范围见[UI V3](../../20_Architecture/CONTENT_LAYOUT_UI_REVIEW_V3.md)。V2原型和生产锚点均未改；不是实施新循环、图钉或自动收展通道。

## 2026-09-23：UI先行原型（非生产代码）

[设计稿](../../20_Architecture/Designs/content-layout-controls-v2.html)的solve/render仅供DEC-011缩框/控制中心控件评审，不是生产Solver修复；[UI映射](../../20_Architecture/CONTENT_LAYOUT_UI_REVIEW_V2.md)限定其范围。生产源码、测试和下方证据锚点本轮未变。下一步LR-R1评审，不把原型JS直接移植为第二引擎。

## 2026-09-23：内容布局审查定位（无生产修改）

当前事实与15个隔离用例见[审查](CONTENT_LAYOUT_REVIEW_2026-09-23.md)，整改后的职责分工为[设计V2](../../20_Architecture/CONTENT_LAYOUT_REMEDIATION_DESIGN_V2.md)草案，不把它当已有代码。

- [Solver](../../../../src/DesktopUp.Domain/ContentLayoutSolver.cs)的ResolveHorizontalLoop/Reclip、[Flow](../../../../src/DesktopUp.Domain/ContentLayoutFlow.cs)和[内容宿主](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)的ScrollItems/EnsureContentSnapshot：轴/循环、容量/区域、高度失效和可达性问题。
- [Editing](../../../../src/DesktopUp.Domain/ContentLayoutEditing.cs)、[生产编辑](../../../../src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs)、[FlowPolicy](../../../../src/DesktopUp.Application/ContentLayoutFlowPolicy.cs)与[Command](../../../../src/DesktopUp.Application/ContentLayoutCommand.cs)：纯模型/鼠标门分叉、恢复自动、切轴+2与单版本校验。
- [PortableContentLayout](../../../../src/DesktopUp.Application/PortableContentLayout.cs)的留白清空；[ResolveHeat](../../../../src/DesktopUp.Domain/ContentLayoutProposals.cs)缺少新尺寸开关；[FlowTests](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutFlowTests.cs)与[PolicyTests](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutFlowPolicyTests.cs)为未提交测试，不足以证明生产链正确。
- 下方schema10/便携v2/R4记录只属于09-17基线；当前wb实验包含schema11/布局v2/便携v3，不据版本号推定兼容或验收完成。

## 2026-09-17：04-L跨格实现入口（L01～06自动通过，L07待人工）

- [ContentLayout](../../../../src/DesktopUp.Domain/ContentLayout.cs)、[Solver](../../../../src/DesktopUp.Domain/ContentLayoutSolver.cs)、[Editing](../../../../src/DesktopUp.Domain/ContentLayoutEditing.cs)：稳定身份/跨度/三样例，区域留白、有界求解/裁切/锚，纯编辑与事务确认后移动的RAM历史。
- [ContentLayoutCommand](../../../../src/DesktopUp.Application/ContentLayoutCommand.cs)、[事务](../../../../src/DesktopUp.Application/StateProjectionCoordinator.ContentLayout.cs)、[PortableContentLayout](../../../../src/DesktopUp.Application/PortableContentLayout.cs)：单revision/full CAS、成员和放置版本、schema10载荷及便携v2映射。内部布局不写外框LayoutGeneration或空间连续关系。
- [生产内容呈现](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)、[输入编辑](../../../../src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs)、[中心桥接](../../../../src/DesktopUp.App/ControlCenterWindow.ContentLayout.cs)：同一Snapshot供Canvas/裁切/输入区域；捕获稳定ID，16ms最新输入合并/边缘滚动、release flush/CAS、Esc/失效取消。
- [提案](../../../../src/DesktopUp.Domain/ContentLayoutProposals.cs)、[生产安全点](../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)、[素材预算](../../../../src/DesktopUp.Application/ContentAssetCache.cs)：热度5档/人工保护/最新合并、主题last-good、Null provider与有界缓存；不统计热度或读取文件。
- [进程门](../../../../src/DesktopUp.App/App.ContentLayoutSmoke.cs)、[控件/输入读回](../../../../src/DesktopUp.App/PosterVisualWindow.ContentSmoke.cs)、[同包脚本](../../../../scripts/smoke-content-layout.ps1)、[人工入口](../../../../scripts/start-content-layout-manual.ps1)：14布局门＋21相关回归，25文件指纹绑定R4；native/pixel标志false。R4异步ready中间帧和暂停恢复均保留实际观测检查。

当前状态只归[CONTENT_LAYOUT_DELIVERY](CONTENT_LAYOUT_DELIVERY.md)，下面开工盘点是历史，不再将其中“待冻结/schema9”当作当前实现。

## 历史：2026-09-16 04-L开工复用图（当时只读盘点，无新布局代码）

当前[04-L01 READY](../../30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)：合同/schema/三参考夹具尚未交付；设计中的LayoutDocument、Placement、Snapshot职责不能当作已有生产跨格协议。现有源码入口如下，逐项复用，不因跳过04-07/08收口而重写宿主、输入或退出。

| 现有入口 | L阶段复用与边界 |
| --- | --- |
| [PosterContentPreferences](../../../../src/DesktopUp.Domain/PosterContentPreferences.cs)、[PosterPresentationParameters](../../../../src/DesktopUp.Domain/PosterPresentationParameters.cs) | 已有宏观尺度/疏密、RelativeScale/Order输入与参数版本；不是热度评分器或跨格占位求解器 |
| [PosterVisualWindow](../../../../src/DesktopUp.App/PosterVisualWindow.cs)、[Transition](../../../../src/DesktopUp.App/PosterVisualWindow.Transition.cs) | 现有DEMO六卡、视口/滚动、输入布局与R4预布局/恢复；L04接统一内容快照，不新建宿主或第二空间所有者 |
| [ScopedStateContracts](../../../../src/DesktopUp.Application/ScopedStateContracts.cs)、[StateProjectionCoordinator](../../../../src/DesktopUp.Application/StateProjectionCoordinator.cs) | 已有目标化命令、读集CAS与权威投影；L01冻结新布局命令语义，L03才接持久化 |
| [SqliteStateStore](../../../../src/DesktopUp.Infrastructure/SqliteStateStore.cs)、[PortableProfileCodec](../../../../src/DesktopUp.Application/PortableProfileCodec.cs)、[PortableProfilePlanner](../../../../src/DesktopUp.Application/PortableProfilePlanner.cs) | 当前schema9与便携v1；L03扩展载荷/迁移/ID映射，不从v8重复创建数据库或导入服务 |
| [ControlCenterSettingsSession](../../../../src/DesktopUp.Application/ControlCenterSettingsSession.cs)、[ControlCenterService](../../../../src/DesktopUp.App/ControlCenterService.cs) | 现有目标设置/关闭释放；新增编辑保护和暂态不能绕过同一生命周期 |
| [PosterSessionRegistry](../../../../src/DesktopUp.Application/PosterSessionRegistry.cs)、[App退出](../../../../src/DesktopUp.App/App.xaml.cs) | 逐海报会话及共享退出/恢复已有；L新增资源只做对应增量验证，历史分项测试不冒充新功能已通过 |

BUG-007已获R3用户复验通过，下面代码修订记录保留；旧失败/待验描述按当时版本理解。当前工作/延期状态只查计划及HANDOFF，不在代码锚点另建验收台账。

## 2026-09-16：BUG-007原生取消修订

- `Windows/NativeProfilePicker.cs`：独立STA和消息端点；COM实例取得HWND并核验进程/线程，WM_CLOSE只请求取消，Show返回且COM/端点/账本释放后才发布Completion。不枚举窗口、不跳过实际IO。
- `Windows/PickerCancellationGate.cs`与`PickerCancellationGateTests.cs`：绑定/取消/完成竞争、重复请求幂等、迟到路径丢弃、清理前解除句柄目标，7项新增测试。
- `App/ControlCenterWindow.Portable.cs`／`ControlCenterService.cs`：替换选择器适配，保留PortableAction与文件服务，补取消/完成日志和停止期间结果守卫。
- `App.PickerSmoke.cs`／`App.xaml.cs`：仅隔离参数允许真实原生8场景；中心系统关闭走WM_CLOSE，不用直接Window.Close冒充Closing。`scripts/smoke-native-picker.ps1`查路径、时限、窗口结束、数据库不变和资源0。
- `scripts/start-portable-manual.ps1 -Candidate picker`＋`portable-picker-candidate.json`：独立R3/人工根、25指纹与8+21门绑定，PS5.1支持；旧stable保留。`smoke-state-recovery.ps1 -PackageDirectory`允许同包复验。证据/人工下一步见[BUG007](BUG007_NATIVE_PICKER.md)。

## 2026-09-16：P9人工失败定位（只读，无生产变更）

- BUG-006：ControlCenterService.Show/OnClosed重新创建窗口；ControlCenterWindow.InitializeView固定初始大小，无最后位置存储/恢复。按用户决定延期。
- BUG-007：App.Tray.QueueTrayAction Exit分支→App.xaml.cs/ExitApplication（先exit_requested，再await取消）→ControlCenterService.CancelPortableAndWaitAsync→ControlCenterWindow.Portable.CancelPortableAndWaitAsync（Cancel+等待completion）。PickPortableFile等待原生结果；PortableAction finally释放lease并完成completion。
- 原生取消子阶段无细日志，现仅确认退出已到达、等待约25秒后正常释放；P9-4 FAIL不由21旧自动门或资源0覆盖。具体证据见KNOWN_ISSUES/VERIFICATION。

## 2026-09-16：D2-P9独立生命周期人工入口

- `scripts/start-portable-manual.ps1 -Scenario lifecycle`：独立lifecycle目录首次三张，重复执行保留配置，25指纹/21原证据门不变；不改App/Fixture。
- 既有生产路径：`ControlCenterWindow.Portable.ConfigurePortable`的Closing取消与等待、`CancelPortableAndWaitAsync`取消picker/dialog并等待lease释放；`ControlCenterService`复用唯一窗口，App正常退出等待配置操作结束。仅核对，不修改生产逻辑。
- P6已USER PASS；P9 PID12380就绪、原生手工待验，下一步见[交付](PORTABLE_PROFILE_DELIVERY.md)。

## 2026-09-16：D2-P6独立备份恢复人工入口

- `scripts/start-portable-manual.ps1 -Scenario backup`：新增独立backup目录/一张展开种子，沿用25指纹和21证据校验；已有目录不重置，支持原有`-Recovery`。不修改生产代码/二进制。
- 已有生产锚点：`ControlCenterWindow.Portable.cs`的PortableBackup_Click/PortableRecovery_Click；`App.Portable.cs`的RequestReplacement/StartRequestedReplacement保留隔离根并正常结束旧生命周期；`StateRecoveryWindow.RestoreSelectedAsync`选择/二次确认、归档信息与明确正常启动。
- P4及P6恢复往返已获USER PASS；恢复后PID39620继续零目录全局入口人工。`ControlCenterWindow.xaml`的MoreActionsButton/PortableProfileMenu与当前海报选择独立；现有0目录控件门不代替真实删除/导入体验。本轮不改代码，下一步和范围见[交付](PORTABLE_PROFILE_DELIVERY.md)。


## 2026-09-16：D2-P4定点人工夹具

- `scripts/start-portable-manual.ps1 -Scenario pending`：原25指纹/21进程绑定不变，新增独立pending目录和单张展开种子，返回导入样例路径；不改旧three库，不重置重复启动的配置。
- `scripts/fixtures/portable-p4-pending.desktopup.json`及`PortablePendingManualFixtureTests`：实际JSON被复制到测试输出，由生产Codec/Planner验证占位/失败重试/腾空后原尺寸原身份安置；固定2560×1392工作区/96 DPI，不能作为跨机预设。仅增加脚本/测试/数据，生产包不变，3项和完整1455项通过，人工待反馈。

## 2026-09-16：配置与恢复收入更多（入口微调）

- `ControlCenterWindow.xaml`：移除独立顶栏按钮；`MoreActionsButton`保持可用，`PortableProfileMenu`改为全局子菜单，五个原Click处理不变，与单张操作以分隔线区分。
- `ControlCenterWindow.EditingSmoke.VerifyMoreMenuForProbe`接现有`VerifyProjection`：实际XAML对象验证层级、五项AutomationId、零选择全局可用且单张操作禁用，复用0/1/3控件进程门；不称原生鼠标验收。
- `portable-candidate.json`绑定菜单修订App/PRI；原包保留`accepted-before-menu`，同一人工目录和启动器不变。1452合同/21进程/PS5.1通过；用户暂停下一步，范围见[交付](PORTABLE_PROFILE_DELIVERY.md)。

## 2026-09-16：04-06便携配置/待安置/显式恢复（AUTO PASS，人工待验）

- App/App.Portable、App.xaml：恢复入口先于开库；显式退出释放后重启；PortableController绑定共享运行空间守卫。StateRecoveryWindow提供只读候选检查、显式恢复/中断处理、busy关闭保护及正常重启。
- App/ControlCenterWindow.Portable：全局导入导出/待处理/备份/恢复，SDK原生picker与取消句柄，关闭/退出等待IO及dialog lease。
- App/App.PortableSmoke、App.RecoverySmoke；scripts/smoke-portable-profile、smoke-state-recovery、smoke-tray-management -PackageDirectory；start-portable-manual及portable-candidate：6+3+12进程门、退出资源0和25产物指纹均通过。便携夹具在外层退出之前记录失败，并按变化输出portable_readiness，不冒充原生点击/真实跨机。
- Application/PosterSessionRegistry.SuspendEnvironment/Drain/Recover与Entry.RecoveredRequest：清除和验证每请求恢复见证，同代次也重发旧会话输入；Start不冗余Recover，完成复核捕获request，旧回调不认证新请求。Tests/PosterSessionRecoveryRequestTests新增6例，合计注册55例、全量1452通过。

- Application/PortableProfileCodec、PortableProfilePlanner：v1数据格式/严格容量隐私验证、一致快照、屏幕映射与有限安全找位；缺空间保留待安置、专用重试不换ID。
- Domain/AuthoritativeState.PlacementPending、PosterLayoutGenerationPolicy；Infrastructure/SqliteStateStore schema9及SqliteStateRepositories：意图分离、旧迁移不改、默认false、列/值校验、完整读写与空间代次。
- Application/StateProjectionContracts.IsEffectivelyVisible、StateProjectionCoordinator.BuildProjection、PosterSessionRegistry、ControlCenterSelection及空间/自动命令守卫：列表待处理、可见数与待安置数区分、不创建/恢复待安置窗口、不允许普通显隐绕过安置。
- Application/StateProjectionCoordinator.Portable：冻结提案、完整CAS、runtime guard、确定性重规划比对、原仓储一次提交；StateRequestFingerprint对pending=false省略以保留旧回执。
- Infrastructure/PortableProfileFile、StateBackupRecovery：有界本地IO/原子发布；离线备份校验/暂存迁移、原DB/WAL/SHM归档、恢复journal与补偿。App/PortableProfileController、ControlCenterWindow.Portable、PosterSpatialRuntime.Portable承接全局UI/预览确认/实际空间守卫；同包自动门通过，真实picker/确认/关闭体验仍D2。
- Tests/PortableProfileModelTests、PortableProfileFileTests、StateBackupRecoveryTests、PendingPlacementTests、PortableImportTransactionTests：纯模型、文件/恢复、待安置、SQLite原子导入回归。证据范围见[交付](PORTABLE_PROFILE_DELIVERY.md)，没有同包/人工证据不得称整体PASS。

## 2026-09-16：布局统一与受控兼容修复

- Domain/PosterConfigurationFactory.NewBasicBox、PosterPresentationPolicy.AdaptiveGridLayout：中心/托盘共用新建参数和唯一受支持布局标识；PosterVisualWindow默认值同源。
- App/PosterSpatialRuntime.ControlCenter：新建改用工厂，原空间落位/CAS不变；ControlCenterWindow.AutoPosture：不支持外观显示只读原因。
- Infrastructure/SqliteStateStore：schema8 canonical-created-poster-layout，旧v1～v7 SQL未改；来源回执＋精确参数窄匹配，原备份/事务、一批一次revision，未知保留。
- Tests/CanonicalPosterLayoutTests、LegacyStateDatabase：新增13项覆盖双入口/编辑重启、幂等修复、未知/无来源保留、备份与晚失败回滚；完整1315通过。
- App.TraySmoke、ControlCenterWindow.SettingsSmoke：新增真实外观控件链；scripts/smoke-tray-management.ps1和start-tray-manual.ps1以-Candidate layout绑定新包，tray-layout-candidate.json保留独立25指纹；旧global默认不变。
- [设计](../../20_Architecture/CONTENT_LAYOUT_FOUNDATION_V1.md)与[04-L](../../30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)仅设计/计划；多格引擎/编辑器尚未接生产。实际证据见[交付](LAYOUT_UNIFICATION_DELIVERY.md)。

## 2026-09-15：05-02/04整体托盘交付，05-05人工NEXT

- Application/TrayManagement：六类纯菜单与完整创建读集；App/TrayService、App.Tray：统一延后派发、状态/失败反馈，旧单张编辑耦合退役。
- PosterSpatialRuntime.ControlCenter：隐藏时复用ResolveEffectiveBounds潜在占位，普通模式仍要求实时读回；App恢复链保留全局隐藏，AutoPosture隐藏时无观察目标。
- ControlCenterSelection/Service/Window：有效隐藏/降级状态及提示动效真实作用域；未改BUG-004整表替换机制。
- App.TraySmoke：隔离真实菜单绑定/0、1、3/生命周期/在途R4/重启；scripts/smoke-tray-management.ps1、start-tray-manual.ps1和tray-candidate.json绑定独立包。
- [交付证据](TRAY_DELIVERY.md)：1302合同、12进程、资源0及PS5.1/25指纹通过；当前人工G1～G8，不把下方实施前审查当未接线现状。

## 2026-09-15：DEC-006范围审查（本轮未改源码）

- 当前结论见[托盘代码审查](TRAY_SCOPE_AND_CODE_AUDIT.md)。TrayService._editItem/Create/SetPosterEditing/VerifyProjection与App.TogglePosterEditing/选择同步分支是下一步退役点，不能误删共用的自动观察或海报编辑。
- TrayManagement.Poster/TargetCurrent仅测试调用，新范围不再需要；Create与Registry.GloballyHidden保留待接。05-03取消，05-02安排专用代码/4例退役与安全新增。
- App.ShowControlCenter的Dispatcher、批量锁定完整读集/单事务、唯一退出复用；DynamicEffectsPaused直接视觉消费只在PosterVisualWindow.Feedback，不能称整段收展已暂停。
- 下节05-01为范围修订前的基础实现快照与旧1294证据，不说明DEC-006已落地；完整计划见[05专项](../../30_Plans/V3_04_05_TRAY_PLAN.md)。

## 2026-09-15：04-05托盘基础（尚未接生产菜单）

- Application/TrayManagement.cs：冻结单张身份/实体CAS和新增完整目录读集；不引用中心选择。
- Application/ControlCenterCatalogRequest.cs、App/PosterSpatialRuntime.ControlCenter.cs：新增Source，默认中心不变；托盘复用安全落位与原命令。
- Application/PosterSessionRegistry.cs：运行期GloballyHidden原因，Wants统一资格、隐藏取消释放、恢复最新目录；没有改写Poster.IsVisible。
- tests/DesktopUp.Contracts.Tests/TrayManagementTests.cs：11项，复用原Registry测试的隔离SQLite/FakeSession。1294/构建证据及下一接线边界见[本步计划](../../30_Plans/V3_04_05_TRAY_PLAN.md)。TrayService和App调用尚未修改，不当作用户菜单已可用。

## 2026-09-15：紧凑滑块与轨道节点

- App/ControlCenterWindow.xaml、AutoPosture.cs：去独立预设按钮，320 DIP/窄区适配；原生手势结束时点选取值，复用原自动提交/取消。
- App/SliderPresetMarks.cs：实际HorizontalTrackRect/Thumb坐标、非命中绘制、完整移动轨迹取消吸附、Loaded/Unloaded清理；不自建拖动捕获。
- Application/SliderPresetGeometry.cs及18项测试：非等距点位/端点/轨道宽度、点击与拖动区分；范围仍来自Domain合同。
- SettingsSmoke/App.AutoSettingsSmoke：实际thumb中心对齐、预设单revision及XAML校验；隔离控件异常日志保留。脚本-Candidate slider和独立25项指纹见[修订记录](CONTROL_CENTER_SLIDER_REFINEMENT.md)。列表生产逻辑未改。

## 2026-09-15：UX-05自动门与人工出口

- App.ControlCenterSmoke.cs、App.AutoSettingsSmoke.cs：隔离0/1/3真实控件回归；COM消息泵重入保护，稳定切页再截图。
- ControlCenterWindow.EditingSmoke.cs、SettingsSmoke.cs：先打开实际页/折叠组、再编辑/Invoke；无强行启用按钮。原目录、R4在途冲突及四次中心开关继续验。
- PosterSpatialRuntime.UsabilitySmoke.cs：定位前后台/owner/Z-order/样式/Bounds读回、自然到期、宏观实际XAML差异；只由隔离旗标调用。
- PosterVisualWindow.Presentation.cs/VerifyPresentation：只读验收接受double期望值或其精确float32表示，拒绝NaN；不放宽原生像素/CAS，不修改生产布局。
- 最终六组资源0、1265纯门通过。候选与人工入口见[实施记录§4/5](CONTROL_CENTER_USABILITY_DELIVERY.md#4-最终自动证据与修订经过)，PID30652人工待验；BUG-001/M08余门保留。

## 历史：2026-09-15 UX实现锚点（当时实机待验）

- Domain/PosterContentPreferences.cs、PosterPresentationParameters.cs：尺度/间距偏好、中性v1兼容/v2写入、有效指标、异尺寸/排序输入口；无热度计分实现。
- Application/ControlCenterGeometryBasis.cs、ControlCenterPosterEditor.cs、ControlCenterSettingsSession.cs：可信位置、字段mask、原目标/版本/一动作一命令、失败恢复/自身成功推进队列。
- App/PosterSpatialRuntime.ControlCenter.cs：实际读回与原空间/R4事务；App/ControlCenterWindow.xaml及Editing/AutoPosture：三页管理、宏观滑块和自动提交，高级精细组仍原子确认。
- App/PosterVisualWindow.ControlCenter.cs、PosterSpatialRuntime.Usability.cs：同窗口描边和同Bounds内容预览；资源/关闭/恢复/手动优先清理。未替换宿主、原生输入所有者或动画引擎。
- 旧Application/ControlCenterDesktopPreview.cs及其专用读口/7项测试已退役；App.AutoSettingsSmoke.cs升级新控件，不再验证旧常用Apply/Cancel。
- scripts/smoke-control-center-usability.ps1、start-control-center-usability.ps1、control-center-usability-candidate.json：[UX实施记录](CONTROL_CENTER_USABILITY_DELIVERY.md)，六组进程门尚未运行。

## 历史：2026-09-15 UX文档对齐与待修边界（当时无代码修改）

- [新设计](../../20_Architecture/CONTROL_CENTER_USABILITY_DESIGN_V1.md)、[UX计划](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)为下一步依据；生产仍是下面旧02～06实现。
- Application/ControlCenterEditContracts.cs：PosterGeometryText.From读取FallbackX/Y，需与App/PosterSpatialRuntime.Effective.cs锚点/工作区恢复区分；BUG-002及字段修改意图是UX-01/02目标。
- Application/ControlCenterDesktopPreview.cs的Fit＋App/ControlCenterWindow.Editing.cs固定160高/整画布DimGray：BUG-003工作区留白机制；UX-03退役专用UI/映射，App/PosterSpatialRuntime.ControlCenter.cs共享有效读回与空间证明继续复用。
- ControlCenterPosterEditor与App/ControlCenterWindow.AutoPosture.cs等旧Apply/Cancel适配：身份/basis/latest/CAS可复用，常用UI改自动提交尚未实施；测试需区分保留安全断言与退役交互断言。
- M05参数→宏观映射、异尺寸热度stub输入、定位瞬态所有权是待设计验证端口，不在锚点虚构已存在的新类。

## 历史实现基线：2026-09-15 04-04-02～06生产接线

- Application/ControlCenterDesktopPreview.cs、ControlCenterSpatial.cs、ControlCenterCatalogRequest.cs：比例预览、精确组合提案、场景/读集证明、安全落位；复用Domain空间算法。
- App/PosterSpatialRuntime.ControlCenter.cs：实际占位、完整环境/实体读回，复用PostureRun和R4准备/结束恢复，目录命令统一事务。
- App/ControlCenterWindow.Editing.cs、ControlCenterWindow.xaml、ControlCenterService.cs：目标草稿、分区控件、目录对话框、原M08交互保护和生命周期；旧AutoPosture专用编辑器仍复用。
- Application/StateCommandPolicy.cs、StateProjectionContracts.cs、ScopedStateContracts.cs：原命令增加可选空间证明，新中心不走裸几何/半创建路径，不改数据库schema。
- tests/DesktopUp.Contracts.Tests/ControlCenterSpatialTests.cs：新31项，完整1236通过；App.ControlCenterSmoke.cs、ControlCenterWindow.EditingSmoke.cs及scripts/smoke-control-center-editing.ps1：隔离0/1/3真实控件/在途恢复/退出门。
- scripts/start-control-center-manual.ps1、control-center-candidate.json：[本阶段人工出口](CONTROL_CENTER_DELIVERY.md)，与M07/M08旧候选分离。

## 历史：2026-09-15 04-04-01目标化编辑纯模型与事务

- Application/ControlCenterPosterEditor.cs：纯窗口会话，basis/latest/pending、单脏分区、结果身份/版本、取消/关闭/NotFound；不引用WinUI或仓储。
- Application/ControlCenterEditContracts.cs：原文几何字段与有限DIP校验、不可直接写库的几何请求、冻结全目录读集；原参数写入复用PosterPresentationDraft。
- tests/DesktopUp.Contracts.Tests/ControlCenterPosterEditorTests.cs：41项目标切换/迟到/无效输入/权限/有效高度；同目录ControlCenterEditTransactionTests.cs：12项实际SQLite/单revision/回滚/无关与完整读集CAS/零目录，空间桥接为纯夹具非原生接线。
- [完整合同与证据](CONTROL_CENTER_EDIT_CONTRACT.md)：新53、全1205通过，Release独立构建通过。下一项02预览；App/ControlCenterWindow和M08输入路径未接新模型。

## 历史：2026-09-15缺陷延期与主线准备（仅文档）

以下为实施前盘点，当前01新增锚点见上节；不覆盖后续交付状态。

同日增补[静态审查锚点](V3_04_IMPLEMENTATION_PLAN_AUDIT_2026-09-15.md)：ControlCenterWindow/Service/Selection和AutoPostureEditor已有入口；StateCommandPolicy.Create/Clone及SpatialGestureCommitPolicy为底层能力，尚不能当中心安全落位/几何编辑已完成；TrayService＋Coordinator已有汇总/全部锁定；IStateStore已有备份但无便携配置/恢复入口。未改这些代码，未重新执行旧测试。

[V3-BUG-001](KNOWN_ISSUES.md)登记用户现象，根因未确认、修复暂停；既有代码锚点不代表缺陷已修好。本轮只读盘点ControlCenterWindow/Service、StateProjectionContracts及草稿/空间入口，形成[04-04计划](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)，无生产代码/测试变更。

## 2026-09-14：TI-04 最新位置、已呈现前缀和分类结束（当前）

- Application/TitlePresentationDriver.cs：首次阈值只保留一次；拖动后latest优先，在整组已验证边界替换尾段；InputEnded区分Released/Interrupted；前缀达到NeedsCheckpoint先让出原封段入口。
- Application/TitleSpatialKernel.cs / TitleSpatialSession.cs：PresentedFrames仅接受重新推导的合法扫掠前缀；保留邻居Home/cohort及源真实锚点，Interrupt独立终态仍可完整重放/CAS。
- Application/TitleNativeInput.cs / Windows/PosterInputIslandManager.cs：原生结束来源与路由/光标证据分类；仅CaptureTransferred允许申请安全中止，其余硬取消；无新输入窗口或全局钩子。
- App/PosterSpatialRuntime.Title.cs：同一TitleRun、R4、可信租约及SQLite通道；InputEnded仅解除已结束捕获要求，其余运行校验不变；日志保留NativeEnd/Interrupted/FailureReason。
- Application/DesktopVisualSurfaceReadback.cs、Windows/DesktopLayerAttachment.cs、App/PosterVisualWindow.cs：失败当次window/client/HRGN分类读回；App/PosterSpatialRuntime.TitleSurfaceSmoke.cs新增translate-fast组件项，仍非真实鼠标测试。
- TitleLatestMotionTests / TitleSafeStopTests / TitleNativeEndPolicyTests及TitleSpatialTransactionTests、TitleRuntimeAuthorityTests：新34项；另13项表面诊断纯测试，全1152项PASS。候选指纹、RED证据与未关闭门见[TI-04§7](M08_TI04_ISOLATED_GATES.md#7-最新位置与安全中止2026-09-14)。下方为历史阶段锚点。

## 2026-09-14：TI-04 首次接管 R4 原生容量基线（组件RED/GREEN，真实输入待验）

- App/PosterVisualWindow.cs：CommitSpatialFrames的SurfacePreparation分别验证CurrentNativeBounds与_bounds；失败日志spatial.surface_preparation_baseline_rejected记录旧容量、旧可见区及目标容量，不移除读回/HRGN安全门。
- App/PosterVisualWindow.Transition.cs：BeginTransitionLayout先保存PreviousNativeBounds再释放旧布局租约；CurrentNativeBounds在新容量未提交时仍返回实际旧容量；已有足够容量的BeginTitlePreview继续retag，无取消capture/恢复旧位置。
- App/PosterSpatialRuntime.TitleSurfaceSmoke.cs：handoff-reuse/grow从已提交展开前缀接管，验证容量/裁切及恢复；未修复PID58304得到reuse RED，修订PID50188七项GREEN、clean/0，仍不是原生输入验证。
- scripts/start-ti04-title-manual.ps1 / ti04-candidate.json：独立候选已绑定handoff修订包及新manual数据根，25项指纹仅App程序集变化；PS5.1启动/退出/同库重启PASS，当前PID14408待真实输入反馈。正式M08/R1绑定不变。
- 独立RED/修订包构建PASS，修订全合同1105/1105 PASS；旧候选首次按住失败日志已定位。范围及下一步见[TI-04报告§5](M08_TI04_ISOLATED_GATES.md#5-首次标题接管失败定位与修订2026-09-14)，不进入TI-05。

## 2026-09-14：TI-04长拖分段证明、组件故障及既有进程门（当前，原生标题门OPEN）

- Application/TitleSpatialSession.cs：NeedsCheckpoint、ProgressTrace、Seal与ReplayBounded；完整接受后封段，原Press/意图/因果保留，只存有界尾段和当前checkpoint。
- Application/TitleRuntimeAuthority.cs / StateProjectionCoordinator.cs / TitleSpatialCommit.cs：同一租约内验证完整读集/实际运行起点和独立分段重放，checkpoint身份绑定，最终仍原完整CAS/单revision；普通JSON不能签发凭据。
- App/PosterSpatialRuntime.Title.cs：原Driver无pending呈现时请求CheckpointTitle，失败仍CancelTitle/统一恢复，日志SealedSegments；没有重启按下或逐帧持久化。
- App/PosterSpatialRuntime.TitleSurfaceSmoke.cs / App.xaml.cs：仅隔离flag的R4组件故障，不注入输入、不走伪造title租约、不写库。使用原BeginTitlePreview/CommitSpatialFrames/RestorePresentedGroup，五场景读回。
- scripts/smoke-title-surface.ps1 / start-ti04-title-manual.ps1 / ti04-candidate.json：PS5.1组件回归、25项候选指纹；正式R1绑定未变。
- 新25/全1105项，R2-A独立4项；九次最终包进程exit0/clean0。范围及剩余门见[TI-04报告](M08_TI04_ISOLATED_GATES.md)。下一步继续TI-04原生标题整合，通过后TI-05。

## 2026-09-14：TI-03原输入/所有者/R4接线（此前步骤）

- Windows/PosterInputIslandManager.cs：TitleInput从原capture按下即发；标题领取后直到Release/Cancel不回落到legacy invoke/Item；保留原按钮/编辑模式路径。
- App/PosterVisualWindow.cs / Transition.cs：Using+auto标题空白入口；R4内容租约可重标记所有者，必要时扩大容量，原位裁切，不取消title capture。标题指示按可见高度，不按预布局内部展开标志。
- App/PosterSpatialRuntime.Title.cs：同源PostureRun成功AcceptedFrames移交、完整实际读回/有效布局见证、单所有者、捕获/环境/会话/全读集门；发布后的物理恢复结束才释放所有者。
- Application/TitlePresentationDriver.cs：首次阈值与最新移动合并，Release顺序屏障，完整proposal接受；快展只提交终态，失败/预算原因可追踪。
- Application/TitleRuntimeAuthority.cs / StateProjectionCoordinator.cs：一次可信runtime绑定、不透明一次性租约，事务内重验见证；TitleSpatialCommit仍重放/核验终态和因果，完整CAS推导单revision写集。
- App/PosterSpatialRuntime.Posture.cs：成功组帧计数，抽出RestorePresentedGroup供原收展与标题共同使用，恢复检查异常进入原重建路径。App.AutoPosture及AutoPostureEvidence接回释放退出抑制，不改AutoPostureModel。
- 新TitlePresentationDriverTests 24 / TitleRuntimeAuthorityTests 13；全1080项、独立构建PASS。[TI-03报告](M08_TI03_PRODUCTION_TITLE_ADAPTER.md)给出证据和未验边界；下一项TI-04，人工绑定仍R1。

## 2026-09-14：TI-02空间/呈现/事务（此前步骤）

- Application/TitleSpatialKernel.cs：原计划已接受前缀、固定抓取点、实际占位碰撞、并行收拢、原连续关系复用；半收起快展反向前缀保留上展锚点。
- TitleSpatialSession.cs：不透明提案、完整接受、ImmediateGroup快展终态、代次失效、语义预算/释放预留、终态与因果见证重放。
- TitleSpatialCommit.cs及StateProjectionCoordinator.Execute(TitleSpatialCommand)：初始占位/布局见证、完整catalog实体CAS、推导写集、单revision和三端投影；原生无调用。
- TitleSpatialTests 31、TitleSpatialSequenceTests 26、TitleSpatialTransactionTests 25；新82/全1043项、独立构建PASS。[TI-02报告](M08_TI02_SPATIAL_TRANSACTION.md)记录反例与接线约束。下一项TI-03。

## 2026-09-14：TI-01退役与标题纯模型（此前步骤）

- Application/TitleInteractionModel.cs：复用PosterInputCapture/SessionKey、原5px口径和AutoPosturePointer；固定按下阶段，输出Pause/FastExpand/CompactDrag/ExpandedDrag及结束意图，无空间/DB/native调用。
- TitleAutoRearmSuppression：将释放时真实退出计数接为原AutoPostureObservation.ManualCollapseExitVersion；新增接合测试，不修改原自动模型或原生观察器。
- TitleInteractionModelTests 50、TitleInteractionSequenceTests 55、TitleInteractionRearmTests 4；新109/全961项PASS；独立App构建0警告/0错误。[完整TI-01证据](M08_TI01_TITLE_MODEL.md)。
- 旧ExpansionDrag四实现/五测试及IExpansionDragCommitter/协调器专用方法已删除，旧端口不能继续被调用；下方旧类型锚点均为历史，不表示源码仍存在。
- App/Windows/R4未接入；下一步TI-02空间提案/因果/CAS证明。Phase.Released不等于已提交或已释放共享空间所有者；Delta不授权直接移动窗口。


## 2026-09-14：TI规则冻结（此前文档事件）

[TI合同](M08_TITLE_INTERACTION_CONTRACT.md)承接V2-DEC-003；旧增长合同删除，TBD-019由新CompactDrag语义替代。下面增长实验属于旧设计证据，不是今日接线指令。

- 当前共用路径：App/PosterSpatialRuntime.Automatic.cs的StartAutomatic与Posture.cs的ToggleCollapsed均调用SetCollapsed；Domain/SpatialContinuitySolver.cs调用SpatialExpansionPolicy.Plan，组动画与事务一致。
- Windows/PosterInputIslandManager.cs的pressFrame/5px、App/PosterSpatialRuntime.cs的Begin取消恢复是下一步接管边界；本轮未改变。
- Application/ExpansionDrag*.cs及对应测试为未接App/Windows的旧实验，按M08-07-TI-01分类退役/重验，不直接接旧增长默认。
- 新标题按下/快展/双类拖动未实现；本轮无新测试PASS，旧921项只保留实验事实。实施顺序以[M08计划§4.1](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#41-标题交互修订的最近顺序)为准。


## 2026-09-14：旧组合空间/事务实验（HISTORICAL，未接App）

- `Application/ExpansionDragSpatialModel.cs`：按当前占位移动、held向下增长/暂缓、release重算、邻居Home/因果积累；`ExpansionDragModel.cs`补捕获期禁止增长抬走抓取点。
- `Application/ExpansionDragSession.cs`：不透明提案、实际呈现后接受、丢弃/迟到门、暂停不积压、为结束预留轨迹预算。
- `Application/ExpansionDragCommit.cs`、`StateProjectionCoordinator.ExecuteExpansionDrag`：有限语义重放、初始几何证据、完整CAS及一次提交；新受限口不扩展System通用权限。
- `ExpansionDragSpatialTests`14、`ExpansionDragReplayTests`6、`ExpansionDragSessionTests`4、`ExpansionDragTransactionTests`17；全921/921，独立App构建通过。[历史证据及反例](M08_COMBINED_VALIDATION_R3.md)。App/Windows没有组合调用；TBD-019现由新TI替代，旧实验不再继续照原计划接线。

## 2026-09-11 M08-07 R2：组合前置与外部点击独立修订（当时源码/测试范围）

- `Application/ExpansionDragModel.cs`、`ExpansionDragModelTests.cs`：组合生命周期、捕获token/代次、已呈现高度及整组扫掠帧结构门；28项通过，尚无App调用或CAS入口。`StructuralSafetyIsNotProofThatNeighborMotionIsAuthorized`明确纯几何不能代替因果/权限证明。
- `Windows/PassivePosterPointerObserver.cs`：原始按键/capture的实际海报归属及释放前保留；auto关闭的邻居仍保护，前台遮挡/无关桌面点击不阻塞，零需求/环境/移除失效。`PassivePosterPointerOwnershipTests.cs`9项；修改前4/4失败、最终全880/880。
- [旧组合实验记录](M08_COMBINED_VALIDATION_R3.md)及[R2分线证据](M08_INPUT_ARBITRATION_R2.md)：旧模型证据后续补为921项，再由TI替代行为目标；独立App构建没有替换R1人工包，R2-A实机门仍单列。

## 2026-09-11 M08-07 R2：输入仲裁诊断（历史，无生产修改）

- `Application/AutoPostureModel.cs`：Observe保护分支→Inactive/ClearWork清空离开截止；Windows原始保护尚无交互归属区分。
- `Windows/PosterInputIslandManager.cs`：BeginResolvedGesture缓存按下帧；5px后才发GesturePreview。`App/PosterSpatialRuntime.cs`的Begin先取消自动事务，后比较恢复后Bounds与pressFrame；`PosterVisualWindow.CancelSpatialGesture`在尚无gesture时不记录拒绝详情。
- [R2根因记录](M08_INPUT_ARBITRATION_R2.md)：当时只做诊断；被否定的200ms及先恢复再拖步骤已删除，不再待确认。

## 2026-09-11 M08-07 R1：动画中普通点击（此前交付锚点，现两项人工PASS）

- `Application/AutoPostureModel.cs`：PointerCapture与PointerButton为InFlight准入专用保护，所有新请求仍受全部保护；明确手动/菜单/恢复取消保持。
- `Windows/NativeDesktopPointerPlatform.cs`、`PassivePosterPointerObserver.cs`：原生与路由按下capture不再冒充真实Gesture；输入岛阈值/命中未改。
- `Application/AutoPostureScheduler.cs`：失效点稀疏记录保护/代次，不记录坐标轨迹。
- `AutoPostureInFlightInputTests.cs`及原有模型/观察/调度测试：完整843/843；旧模型4/4复现失败。独立25项R1包绑定，PS5.1预检及6次隔离进程PASS，之后两项普通点击已由用户确认，见[R1](M08_ANIMATION_INPUT_R1.md)。

## 2026-09-11 M08-07 验证工具（初始产物记录；当前状态见上）

- `scripts/Get-M08Build.ps1`/`m08-build.json`：25项当前验证包绑定；`start-m08-manual.ps1`保持分场景同库，不带测试动作。
- `App/App.AutoMeasurement.cs`/`App.xaml.cs`：只在隔离数据/单探针旗标下运行，OFF程序化对照、ON真实调度；Ready/QPC阶段、稀疏采集次数、revision、timer/恢复校验。日常路径不创建测量timer。
- `scripts/measure-m08.ps1`：同包1/3/6、OFF/ON、3负载、双轮交叉次序采样；QPC区间、M07统计口径及无效样本拒绝。
- `scripts/M08MeasurementRules.cs`/`M08MeasurementRulesTests.cs`：共享计数合同30项；`M08InputProbe.cs`/`Test-M08NativeEnvironment.ps1`仅只读前置。
- `test-m08-tools.ps1`：PS5.1语法/入口/正常退出与同库重启。全798/798；人工与有效外采0/36仍待反馈，见[07合同](M08_EXPERIENCE_AND_RESOURCE_GATE.md)。

## 2026-09-11 M08-06 生命周期（该阶段证据；余门OPEN）

- `Application/SystemLifecycleGate.cs`：休眠/锁定/断连独立原因，部分解除不恢复，恢复重新建立基线。
- `Windows/DesktopLifecycleSignals.cs` / `DesktopEnvironmentWatcher.cs`：原生电源/WTS分类、当前session过滤、共享注册/重试/释放，远程连接/断开使用官方3/4。
- `App/App.SystemLifecycle.cs` / `App.xaml.cs`：统一暂停使旧自动/恢复代次失效，显示/健康/旧异步结果不得越过系统暂停；复用原恢复/空间/事务。
- `SystemLifecycleTests.cs`：新增32项消息/多原因/旧回调/0/1/3/6连续生命周期；全768/768。
- `App/App.AutoLifetimeSmoke.cs` / `scripts/smoke-auto-lifetime.ps1`：只允许隔离数据的本地HWND通知、显隐/删除、启用退出/重启，PS5.1；不是实际系统锁屏或鼠标模拟。
- Release0警告0错误；13个成功隔离进程exit0/clean0；最终自动仍Unavailable/error5，详见[本项合同](M08_LIFECYCLE_REGRESSION.md)。

## 2026-09-11 M08-05 控制中心设置（该阶段证据）

- `Application/ControlCenterAutoPostureEditor.cs`：窗口会话草稿、原文校验、latest/basis分离、目标CAS、pending/requestId及取消；复用`PosterAutoPostureDraft`，无新增状态权威。
- `App/ControlCenterWindow.xaml`/`ControlCenterWindow.AutoPosture.cs`：逐海报开关、秒数、应用/取消、过期/错误提示；TextChanging只更新纯草稿，TextChanged更新视觉；提交前重验实际控件。
- `App/ControlCenterService.cs`：关联回执、关闭/目标切换释放、SettingsInteractionChanged；`App.AutoPosture.cs`/`App.PassivePointer.cs`复用共享Editing保护，不改空间求解和输入岛。
- `ControlCenterAutoPostureEditorTests.cs`新增27项；`ArchitectureTests.cs`纳入新UI partial。`App.AutoSettingsSmoke.cs`/`ControlCenterWindow.SettingsSmoke.cs`及`scripts/smoke-auto-settings.ps1`仅供隔离真实XAML测试。
- 全736/736、Release0警告0错误，最终控件PID43568/49852 PASS；最终自动回归输入桌面不可读仍需复测，详见[M08-05合同](M08_CONTROL_CENTER_SETTINGS.md)。

## 2026-09-11 M08-04 共享调度/空间接线（该阶段证据）

- `Application/AutoPostureScheduler.cs`：原生观察与延后效果分离、一次出队重验、共享单航班、精确完成/取消、零需求停timer；`AutoPostureEvidence.cs`区分用户意图与独立空间变化。
- `Application/AutoPostureModel.cs`补可选ManualCollapseExitVersion，保留标题移动前的手动抑制证据；旧规则不改。
- `Application/AutomaticPostureCommit.cs`与`StateProjectionCoordinator.ExecuteAutomaticPosture`是受限System姿态事务口；普通Execute仍拒绝System。
- `App/App.AutoPosture.cs`/`App.PassivePointer.cs`接唯一计时器、输入/菜单唤醒、当前会话/环境、投影和退出；`PosterSpatialRuntime.Automatic.cs`复用既有空间/R4/CAS/恢复回执，手动优先且无新手动队列。
- `Windows/DesktopMenuInteraction.ActivityChanged`、`App/ControlCenterService.RemovalInteractionChanged`成对通知保护；不改输入区域、菜单层级或窗口样式。
- `App.AutoPostureSmoke.cs`与`scripts/smoke-auto-posture.ps1`仅限隔离配置，真实原生计时/动画中关闭/资源归零；不移动鼠标。新增38、全709及最终PID43140证据见[M08-04合同](M08_SHARED_SCHEDULER.md)。

以下锚点为对应阶段事实；“未接循环”等限定当时版本，不覆盖上方当前实现。

## 2026-09-11 M08-03 观察适配器/保护（未启动生产循环）

- `Application/PosterInputRouter.cs`的ReadSurface/PointerActivity及PosterInputGeometry复用原命中几何；事件有session/layout版本门，回调失败不破坏主动输入。
- `Application/PosterPointerObservation.cs`维护只读标准presence/保护与真实跨界证据；不将窗口移动、解除遮挡或恢复当鼠标进入，不保存轨迹。
- `Windows/PassivePosterPointerObserver.cs`、`NativeDesktopPointerPlatform.cs`：一次全体采样、精确原生根/桌面身份/遮挡与输入范围读回；无timer/hook/状态写入。`DesktopMenuInteraction.IsActive`及已有router.Capture提供共享保护。
- `Windows/PosterInputIslandManager.cs`按订阅需求TME_LEAVE，转发移动/离开等唤醒提示，观察故障不干扰原处理；没有改输入区域/样式/层级。
- `App/PosterVisualWindow.Pointer.cs`、`PosterSpatialRuntime.Pointer.cs`、`App.PassivePointer.cs`当前会话采集读口；`ControlCenterService.IsRemovalInteractionActive`覆盖确认异步结束。M08-04负责真正订阅/调度，不把端口存在当运行完成。
- 三份`PassivePosterPointer*Tests.cs`新增52项；完整671/671及屏幕外真实HWND/Release证据见[M08-03合同](M08_POINTER_OBSERVATION.md)。

## 2026-09-11 M08-02 设置与迁移（未接自动运行器）

- `Domain/AuthoritativeState.cs`新增AutoCollapseDelaySeconds，`PosterAutoPosturePolicy.cs`统一3秒默认/1～300校验；无全局设置或计时器持久化。
- `Application/StateProjectionContracts.cs`、`StateCommandPolicy.cs`、`PosterAutoPostureDraft.cs`：SetAutoPosture追加编号、目标CAS/白名单、绑定实体草稿、同revision投影；`AutoPosturePolicy.From`复用实体字段。
- `Application/StateRequestFingerprint.cs`、`StateProjectionCoordinator.cs`、`Infrastructure/SqlitePosterCatalogRepository.cs`：仅请求指纹省略默认3秒，保留旧v6回执；非默认秒数参与指纹，普通序列化不省略字段。
- `Infrastructure/SqliteStateStore.cs`追加schema7及列/数据验证；`SqliteStateRepositories.cs`完整读取/插入/更新。v1..6固定指纹保留。
- `AutoPostureSettingsTests.cs`、`AutoPostureSettingsMigrationTests.cs`：数据/CAS/投影、旧库/备份/回滚/重放和旧仓储验证；[完整证据](M08_AUTO_POSTURE_SETTINGS.md)。

## M08-01 纯模型（合同已冻结）

- [Application/AutoPostureModel](../../../../src/DesktopUp.Application/AutoPostureModel.cs)：策略/观察/不可变状态、全目录版本门、真实边界证据、保护/候选仲裁、请求与取消精确回执；无HWND、真实计时器或持久化。
- `tests/DesktopUp.Contracts.Tests/AutoPostureModelTests.cs`、`AutoPostureLifetimeTests.cs`、`AutoPostureSequenceTests.cs`：虚拟时钟/保护/失败/恢复/长序列，复用现有SpatialContinuityTests.Model验证连续归位。
- [实际合同与证据](M08_AUTO_POSTURE_MODEL.md)：TBD-017已由用户确认，手动收起后须真正离开再进入，原测试保留并去掉Proposed标记。

[M08其余任务](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)待实施：生产观察循环/共享调度/实际中心控件，复用registry/恢复与Windows/DesktopMenuInteraction保护。System来源在命令提交层仍被拒绝，04需补受控入口及精确完成/取消，不能冒称已有自动提交。稳定App未替换。
M07已收口；前两项[按需优化](../../30_Plans/ON_DEMAND_OPTIMIZATIONS.md)无实施排期。

## M07 收口当前入口（2026-09-10）

- `scripts/Get-M07StableBuild.ps1`：当前唯一24项绑定；动画/人工/资源/空间/安全均共用。
- `start-m07-manual.ps1`：五场景、首次生成/后续复用、新closeout隔离目录；零目录不重建，return-sync仅首次排列。
- `measure-m07.ps1`：冻结配置/逐格哈希/显示一致性、工作集/私有提交/句柄/线程完整统计；`start-m07-measurement.ps1`跑36次。
- `measure-m07-spatial.ps1`：稳定包1/3/6、每帧覆盖和最终恢复；共用`smoke-spatial.ps1`。
- `test-m07-safety.ps1`：编排已有确认/多实例/无空间/零目录回归；`smoke-input-confirmation.ps1`/`smoke-multi-instance.ps1`改用共用子进程启动器支持PS5.1，未改App测试钩子。
- `test-m07-launcher.ps1`：11脚本AST/24指纹、只读预检和实际3张启动/退出；`-CheckOnly`不创建数据。
- [自动证据与人工清单](M07_CLOSEOUT.md)、[36次资源报告](M07_RESOURCE_BASELINE_20260910.md)。生产呈现实现/产物没有变。

日期：2026-09-11。本页维护当前代码映射及历史代码索引，不维护另一个任务队列；执行状态查细化工作流，交接查[HANDOFF](HANDOFF.md)。旧阶段测试计数属于对应版本，计划端口只有上方实际锚点覆盖部分才是现存代码，未执行验收不当作PASS。

## 当前呈现与动画

- `Application/PosterTransitionSurface.cs`：逻辑可见几何/临时容量分离，受限有效高度不写回首选高度；`PosterTransitionSurfaceTests.cs`覆盖嵌套顺序/步长/上扩/越界。
- `Application/PosterTransitionViewport.cs`：视口/底栏的连续裁切；对应纯测试保留。
- `App/PosterVisualWindow.Transition.cs`：整段内容租约，准备容量、根实际覆盖验证、视口变化与完整树/Clip恢复。
- `App/PosterVisualWindow.cs`：SurfacePreparation / Animation / FinalRestore 分阶段提交；动画视觉固定容量，输入当前大小；收起 meta/footer 行隐藏，空间快照只用可见几何。
- `App/PosterSpatialRuntime.Posture.cs`：共享 Rendering 时钟、双回调就绪门、批次更新与失败恢复；`spatial.surfaces_ready`、`spatial.presentation_restored`保留。不是 GPU fence。
- `Windows/DesktopPresentationFlags.cs`：只对已登记视觉窗口应用 NOSIZE/NOREDRAW，输入/桌面层级不改。
- `Windows/DesktopLayerAttachment.cs`：实际窗口/客户区/HRGN容量读回、最终重绘；`DesktopLayerAttachmentTests.cs`验证标志隔离与失败回传。
- `App/PosterVisualWindow.Backdrop.cs`、`Presentation.cs`、`WinUiPosterSession.cs`与`Windows/DesktopEnvironmentWatcher.cs`：独立窗口背景、主题更新、擦底归属和成对解除。背景检查不等于像素呈现证明。
- `scripts/start-m07-animation.ps1` + `m07-animation-build.json`：唯一当前动画入口/24项指纹；`config/poster-animation.json`仅启动配置。
- 临时 PresentationDiagnostic partial/API/菜单/重绘测试与 R2/R3/R4 分叉入口已移出活动目录；本地 out/archive 可恢复，源码不再依赖它们。

## M07 其余当前锚点

- 连续空间合同/模型/SQLite v6与同批推让归位见 [M07_SPATIAL_CONTINUITY](M07_SPATIAL_CONTINUITY.md)；`PosterSpatialRuntime.ContinuitySmoke.cs`保留8命令生产通道回归，删除其额外重绘实验。
- 菜单共享租约与输入让位见 [M07_MENU_REPAIR_R1](M07_MENU_REPAIR_R1.md)，`DesktopMenuInteraction.cs`/`NativeDesktopMenuPlatform.cs`；旧菜单取证为历史，不代表仍未修复。
- [M07安全/测量合同](M07_MEASUREMENT_AND_SAFETY.md)：`measure-m07.ps1`、`MeasurementMath.cs`、`MeasurementFixtureData.cs`与测量合同测试。
- M07当前人工/测量入口已共用24项稳定绑定；历史输出保留其当时版本身份，不得混入最新基线。
- 证据：[R4](M07_ANIMATION_R4.md)、[VERIFICATION](VERIFICATION.md)；方法：[呈现诊断](../../20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。

## M06 当前代码锚点

- [MultiPosterFixtureData](../../../../tests/Shared/MultiPosterFixtureData.cs)：A游戏/B文件/C应用的三套模板/参数/状态；Contracts.Tests与SessionFixture通过Compile Link复用，不影响生产默认。`tests/DesktopUp.SessionFixture/Program.cs`新增`--multi-instance`，要求新隔离目录/3张。
- [PosterSessionRegistryTests.M06](../../../../tests/DesktopUp.Contracts.Tests/PosterSessionRegistryTests.M06.cs)：新增10项，复用M03私有Fixture/Factory；原测试类改为partial并加可控FailApply，不另造业务注册表。完整405/405。
- [App.MultiInstanceSmoke](../../../../src/DesktopUp.App/App.MultiInstanceSmoke.cs)：显式隔离CLI驱动真实命令/query/会话/XAML，9个exercise快照＋restart，字段白名单、取消/CAS与读回按原路径执行；不合成鼠标、不重启Explorer。
- `PosterSpatialRuntime.PresentationSmoke.cs`复用M05的XAML校验并给出只读物理快照；`ControlCenterService.Smoke.cs`/`ControlCenterWindow.xaml.cs`读实际列表与revision，`TrayService.cs`读托盘revision和能力。`App.xaml.cs`加独立数据根与互斥smoke开关保护。
- [smoke-multi-instance.ps1](../../../../scripts/smoke-multi-instance.ps1)：Exercise/Restart、同一PID证据、单实例短进程、中心顺序关闭重开、最终目录完整对照和台账clean0。2026-09-09 PID44928/48160/47076实际通过；M05参数两轮/空间尾门同步通过。

实际范围/结果见[M06合同](M06_MULTI_INSTANCE_FIXTURES.md)。以下M05/M04为基础与历史；M05/M06进程读回PASS不替代未执行的鼠标/视觉安全门，后者保留至M07。

## M05 基础代码锚点

- [Domain/PosterPresentationParameters](../../../../src/DesktopUp.Domain/PosterPresentationParameters.cs)：版本化声明参数、模板默认/范围/组合/颜色校验、legacy保留/未知只读fallback、列数/行高/ContentMetrics。没有执行主题代码或读取文件。
- [Application/PosterPresentationDraft](../../../../src/DesktopUp.Application/PosterPresentationDraft.cs)、`StateCommandPolicy.cs`、`StateProjectionContracts.cs`：绑定目标与实体版本的不可变草稿、SetPresentation白名单、投影解析，复用原SQLite命令事务，无新schema。
- `Application/SpatialPostureCommit.cs`、`EffectivePosterLayouts.cs`与`App/PosterSpatialRuntime.Effective.cs/.Posture.cs`：目标内容度量参与展开/恢复重算；外框兼容时缓存不因颜色或参数变化主动重排，重新绑定度量身份。
- [App/PosterVisualWindow.Presentation](../../../../src/DesktopUp.App/PosterVisualWindow.Presentation.cs)、`PosterVisualWindow.cs`：独立文本/颜色/图标与格子参数，basic-box/night-ops内置结构；只重建本实例XAML，不换HWND。命中/滚动复用同源度量；中心`PresentationDetailText`仅只读摘要。
- [PosterPresentationParameterTests](../../../../tests/DesktopUp.Contracts.Tests/PosterPresentationParameterTests.cs)：39个新增自动用例；完整395/395。`ArchitectureTests`仅替换背景写死RGB的旧断言，保留prepared bounds/erase/no-copy检查。
- [PresentationSmoke](../../../../src/DesktopUp.App/PosterSpatialRuntime.PresentationSmoke.cs)、[脚本](../../../../scripts/smoke-presentation.ps1)：明确CLI＋隔离profile/互斥smoke保护，检查三实例实际XAML、同HWND/同bounds/独立身份与非法草稿不写库；M05两次及空间尾门已于2026-09-09通过。

参数精确语义见[M05合同](M05_PRESENTATION_PARAMETERS.md)，结果/指纹见[VERIFICATION](VERIFICATION.md)。下方均为M04历史锚点，不覆盖顶部当前项。

## R2 历史代码锚点

- 本次向下优先修订：`SpatialExpansionPolicy.cs`删除纯向上提前返回，统一合法下容量→同前缀上容量。`SpatialDownFirstExpansionTests.cs`新增9项，原80组穷举增加同高最大下扩量；完整356/356。`PosterSpatialRuntime.Posture.cs/.Smoke.cs`日志增加上下距离与经过物理读回的端点，不改变宿主提交逻辑。
- [完整策略矩阵](../../20_Architecture/POSTER_SPATIAL_STRATEGY_MATRIX_V1.md)覆盖各类收展、推让、归位和恢复；下方347项为原R2基线，当前指纹/运行结果见验证记录。人工启动脚本默认`expand-down-first`，旧`expand-up`只是别名。

- `Domain/SpatialExpansionPolicy.cs`、`PosterContentMetrics.cs`：完整候选优先、同场景上下容量、阈值和预算分类；renderer复用内容度量。
- `Application/EffectivePosterLayouts.cs`：版本化、无持久化写口的有效Bounds；新场景准备、恢复种子与完整邻居保留。
- `Application/SpatialPostureCommit.cs`：同策略/度量重算，完整读集CAS、保存高度校验。
- `App/PosterSpatialRuntime.Effective.cs`、`.Posture.cs`：目标显示器共享投影、批次结果发布、状态不足分类，复用R1时序。
- `App/PosterVisualWindow.Feedback.cs`、`Domain/ExpansionFeedback.cs`：glyph微动/静态强调、合并和清理；不变更原生边界与输入。
- `ControlCenterWindow.xaml.cs`、`App.xaml.cs`：中心临时高度说明及托盘受限数量，从同一有效读口派生。
- `SpatialExpansionPolicyTests.cs`、`EffectiveExpansionTests.cs`：新增23项，完整347/347；`scripts/smoke-spatial.ps1`增加三档结果和保留展开后重启断言。
- [R2实际合同](M04_EXPANSION_R2.md) 与 [集中人工](M04_MANUAL_VALIDATION.md) 是当前交付入口，下面为实施前历史。

## M04-07 R2 实施前审计锚点（历史）

- `Domain/SpatialPushSolver.cs`：当前默认source轨迹的推让一旦遇锁定/边界就返回失败；尚无整请求的完整方向候选与受限高度回退。
- `App/PosterSpatialRuntime.Posture.cs`：调用Plan后失败立即完成失败命令，尚无正常空间不足结果/按钮反馈；复用现有批次/取消和完整读集，不另建执行器。
- `Application/SpatialPostureCommit.cs`：事务内重算同一组计划，R2必须与运行裁决共用同一候选/度量版本；临时结果不能被提交刷新解释为完整尺寸。
- `App/PosterVisualWindow.cs`：TryReadSpatial展开态从_bounds.Height取ExpandedHeight、BuildInputLayout与有效几何投影；`Application/SpatialGestureSession.cs`区分显式resize与自动受限高度，均需审计。
- 下一实施范围见 [工作流§4.2](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#42-m04-07-r2-展开回退修订执行卡)，新增端口/代码名称尚未落地，以下R1记录是实际现状。

## M04-07 R1 当前运行改动

- `Domain/SpatialPushSolver.cs`：FindReturnTargets先冻结合法全程归位子集；AdvanceReturns在每个source收起帧中按释放像素/帧前预算推进整链；保留连续CrossesMoving及原资格，去掉归位尾帧。
- `App/PosterSpatialRuntime.Posture.cs`：仅增加联合帧/归位尾帧诊断，原16ms执行器、prepared batch、输入岛与CAS路径复用。
- `SpatialReturnAnimationTests.cs`：11项新增回归，含200组性质场景，完整324/324；`SessionFixture --compact-pair`新增A/B均收起且B在上方的独立场景。
- `scripts/start-m04-07-manual.ps1`：指纹锁定R1产物，新增return-sync；四个场景使用独立R1目录、不覆盖旧人工证据。
- [人工记录表](M04_MANUAL_VALIDATION.md)：G1～G4已由用户确认，G6-R1及余项待反馈；无全项人工PASS。

## M04-07 准备阶段入口（历史，当时未改生产代码）

- `scripts/start-m04-07-manual.ps1`：固定版本/独立配置的可见人工入口，CheckOnly不启动；6个二进制指纹门、已有实例拒绝、环境变量恢复。
- `out/manual/v3-04-m04-07/{interaction,chain,removal}`：三套新配置，不覆盖日常和M04-06证据；由既有SessionFixture生成。
- [集中人工记录表](M04_MANUAL_VALIDATION.md)：G1～G10及工具1385限制，当前全部待用户反馈。不得把启动预检记为GUI通过。

## M04-06 当前实现

- `Application/PosterInputRouter.cs`：PosterId/epoch注册、不可变区域快照、单capture、实际顺序参数路由、旧几何失效；不写状态库。
- `Windows/PosterInputIslandManager.cs`：实际视觉HWND顺序/区域路由、原生capture读回、成功呈现后发布几何版本、释放与恢复取消；不改owner/bottom策略。
- `App/PosterVisualWindow.cs`、`WinUiPosterSession.cs`、`App.xaml.cs`：实例身份传递、事件门、局部恢复失效/就绪恢复、带版本的菜单删除入口。
- `Application/PosterRemovalConfirmation.cs`：固定目标/实体/目录的单次ticket与后果文案；`ControlCenterService.cs`及`ControlCenterWindow.xaml(.cs)`：共用ContentDialog、取消默认、关闭/退出释放、既有CAS执行。
- `PosterInputRouterTests.cs`、`PosterRemovalConfirmationTests.cs`：新增25项，最终313/313；无新增migration。
- `ControlCenterService.Smoke.cs`、`scripts/smoke-input-confirmation.ps1`：隔离实例真实对话框开关及带确认退出；只取消、不自动点删除。
- [本项合同、证据及启动](M04_INPUT_CONFIRMATION.md)。以下条目保留阶段历史，不是当前“仍待接线”列表。

## M04-03..05 当前实现

- Domain/SpatialCollisionSolver、SpatialPostureSolver、SpatialPushSolver：连续截停/滑动、原尺寸两段/逆收起、有界最小链推让/回程安全子集。
- Application/SpatialGestureSession、SpatialPostureCommit、PosterInputRegionClipPolicy：实际已呈现轨迹、完整读集/提交重算、中间高度命中裁剪。
- App/PosterSpatialRuntime（含Posture/Smoke分文件）、PosterVisualWindow：共享物理读回与互斥会话、16ms收展、整组准备/读回/恢复；App统一中心和海报收展入口。
- Infrastructure/SqliteStateStore v5、SqliteSpatialProofRepository、SqliteStateRepositories：布局代次、持久因果、同事务消费、提交前迁移检查；不改v1..4。
- Windows/DesktopLayerAttachment.ApplyPreparedBoundsBatch：视觉窗/输入岛同批，无抬层标志；旧WM_MOUSEACTIVATE/底层保护保留。
- 新增三组Spatial*IntegrationTests及原生批次/输入中间高度测试；完整288/288。scripts/smoke-spatial.ps1与SessionFixture --spatial-chain提供实际三窗验证。
- 完整证据/范围/启动：[空间运行合同](M04_SPATIAL_RUNTIME.md)。下方M04-02及更早条目是阶段历史。

## M04-02 当前实现

- `src/DesktopUp.Domain/PosterSpatialGeometry.cs`：物理像素矩形、外扩取整、环境/快照/只读计划、连续线性路径检查及展开分段采样。
- `src/DesktopUp.Domain/PosterSpatialSolver.cs`：布局合法性、8向候选接受/拒绝、先下后上展开、归位证明资格；不接capture/窗口/SQLite。
- `src/DesktopUp.Application/SpatialCommandContracts.cs`：异构几何信封、完整目录实体读集、环境/候选预检；无写库执行器，不能绕过既有CAS。
- 三个新增测试：`PosterSpatialSolverTests.cs`、`SpatialReturnContractTests.cs`、`SpatialCommandContractTests.cs`，新增67项，完整243/243。
- [M04空间实际合同](M04_SPATIAL_CONTRACT.md)：旧测试替代关系、v5迁移登记、证据及03→04→05接入边界。运行窗口行为本轮不变。

## M04-01 当前实现

- `src/DesktopUp.Application/ControlCenterSelection.cs`：UI会话选择、完整目录/运行状态视图、旧投影拒绝、所选删除清空；ControlCenterView在点击时冻结目标ID/实体版本/意图。
- `ControlCenterWindow.xaml(.cs)`：WinUI ListView、ID稳定选择、所选锁定/收展/显隐、全局设置区；选择变化不重建ItemsSource。
- `ControlCenterService.cs`：窗口关闭后保留选择、重开新View，命令冲突提示；会话快照只作运行状态显示。
- `App.xaml.cs`：ExecuteControlCenterPosterCommand不重定向目标；托盘编辑采用中心选择，批量锁定仍保持全局；环境/会话状态复用已有调度更新中心。
- `tests/DesktopUp.Contracts.Tests/ControlCenterSelectionTests.cs`：13项执行用例，真实SQLite证明目标/冲突/删除/零目录/隐藏和UI状态不写库；完整176/176。
- [实际合同](M04_CONTROL_CENTER_SELECTION.md)、[DeskBox空间吸收记录](DESKBOX_SPATIAL_REFERENCE.md)。下方默认别名审计是修复前记录。

## 2026-09-07 空间补充审计（只读定位，非代码交付）

- `StateProjectionCoordinator.cs:180`：默认ID/首张兼容选择；`StateProjectionContracts.cs:111`：SelectedPosterId仍从该别名派生。
- `ControlCenterWindow.xaml.cs:40/57`：界面只读projection.Poster，因此只显示“A / Session fixture”；M04-01补列表/选择，不改目录权威。
- `StateCommandPolicy.cs` / `PosterVisualWindow.cs`：现有收展/几何策略需接新空间合同；本轮没有修改其实现。
- DeskBox具体来源/commit、吸附与组平移的能力边界、保尺寸两阶段新规则见 [空间设计 §3](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md#3-deskbox-实际参考与不照搬部分)。没有新增碰撞引擎代码锚点，M04实现后补。

## 合同

- `src/DesktopUp.Application/PosterSessionRegistry.cs`：M03 会话端口、目录调和、epoch/lifetime取消、环境版本门回调、逐实例恢复/局部失败与清理重试。
- `src/DesktopUp.Application/PosterSessionResources.cs`：实例/epoch资源命名空间、分实例日志。
- `src/DesktopUp.App/WinUiPosterSession.cs`：工厂、绑定ID的visual/input/原生watcher；转发信号到共享调度。
- `App.xaml.cs`：A08共享发现/健康/节流/代际检查、零海报隐藏监听锚点、退出取消并等待共享task；单一状态订阅驱动registry。
- `DesktopEnvironmentWatcher.cs`：新增signal-only选项用于共享锚点；默认仍保留全部既有海报原生防护。
- `tests/DesktopUp.Contracts.Tests/PosterSessionRegistryTests.cs`：16项，真实SQLite驱动的0/1/3、局部失败、恢复等待中删除/隐藏、过期请求、清理失败隔离与日志可序列化。
- `tests/DesktopUp.SessionFixture` / `scripts/smoke-control-center.ps1`：新目录测试数据生成、真实0/1/3会话与唯一中心/共享监听/退出验证。详细证据见 [M03合同](M03_SESSION_RECOVERY.md)。

- `src/DesktopUp.Application/StateStoreContracts.cs`：初始化、head、CAS、备份结果和 `IStateStore` 端口；
- `src/DesktopUp.Application/Contracts.cs`：`IUserDataPaths.Backups` 用户数据路径合同；
- `src/DesktopUp.Infrastructure/UserDataPaths.cs`：state/backups 目录建立。
- `src/DesktopUp.Domain/AuthoritativeState.cs`：Poster/Settings 权威领域值和持久状态验证；
- `src/DesktopUp.Application/RepositoryContracts.cs`：snapshot、write result、Poster/Settings repository 端口；
- `src/DesktopUp.Application/StateProjectionContracts.cs`：command、result、Poster/Control/Tray projection、读写分离端口；
- `src/DesktopUp.Application/StateProjectionCoordinator.cs`：输入冻结/指纹、同事务集合快照、三端投影与 FIFO 增量发布；不再拼接 committed fallback。
- `src/DesktopUp.Application/ScopedStateContracts.cs`：实体读集、Create/Clone/Remove/Batch、删除影响预览及事务端口。
- `src/DesktopUp.Application/StateCommandPolicy.cs`：目标与必要读集、权限、纯状态批量、几何/收起兼容策略。

## SQLite adapter

- `src/DesktopUp.Infrastructure/SqliteStateStore.cs`：schema v1、migration SHA-256 账本、application id、WAL/FULL、全局 revision、immediate transaction CAS、operation 幂等、online backup、验证与失败保留；
- `src/DesktopUp.Infrastructure/SqliteStateStore.cs`：immutable migration v2 与一致只读 transaction 入口；
- `src/DesktopUp.Infrastructure/SqliteStateRepositories.cs`：Poster/Settings SQLite adapter、snapshot 映射、CAS 写入、冲突/重放/失败返回；
- `src/DesktopUp.Infrastructure/SqliteStateRepositories.cs`：`SqliteStateProjectionRepository` 在同一个 read transaction 中生成聚合 snapshot；
- `src/DesktopUp.Infrastructure/SqliteStateCommandRepository.cs`：生产命令同一 immediate transaction 内检查全读集、批量写入、持久回执和完整快照；零初始化/重放/失败保留。
- `SqliteStateStore.cs`：追加 migration v4、Settings EntityVersion、state_command_receipts、当前/源版本结构验证；旧 v1..3 不变。
- `src/DesktopUp.Infrastructure/DesktopUp.Infrastructure.csproj`、`Directory.Packages.props`：`Microsoft.Data.Sqlite 10.0.11` 固定依赖；
- `THIRD_PARTY_NOTICES.md`：Microsoft.Data.Sqlite 与 SQLitePCLRaw 许可证登记。

## 启动门

- `src/DesktopUp.App/App.xaml.cs`：`IStateStore.Initialize()` 位于 ControlCenter/Tray/Poster 创建之前；失败时走正常清理退出，不启动默认状态。
- `src/DesktopUp.App/PosterVisualWindow.cs`：启动采用 committed Poster snapshot；拖缩完成、锁定和收起提交同一 revision；冲突/失败恢复权威快照；显示重投影不持久化。
- `src/DesktopUp.App/App.xaml.cs`：唯一 projection 订阅和三端分发；Control Center/Tray 命令转换；
- `src/DesktopUp.App/ControlCenterService.cs`、`ControlCenterWindow.xaml(.cs)`：ControlCenterProjection、关闭重开恢复和三项状态命令；
- `src/DesktopUp.App/TrayService.cs`：TrayProjection、tooltip、锁定/暂停勾选和 revision 守卫；
- `src/DesktopUp.App/PosterVisualWindow.cs`：Poster command、外部 PosterProjection 和 settings-only revision 无物理帧重建。

## 自动证据

- `src/DesktopUp.App/DesktopUp.App.csproj`：`ValidateWinUiOutputPath` / `DUV3001` 防止 XBF 输出路径拼接错误；
- `scripts/build.ps1 -OutputDirectory`：自定义构建目录末尾分隔符规范化及 native exit code 检查；
- `scripts/smoke-control-center.ps1`：PowerShell 7.4+，强制独立 DataRoot，拒绝已有运行实例；M02 实际 XAML 开窗、二次单实例激活和零残留退出已通过。
- `ControlCenterService.Show` / `ControlCenterWindow.InitializeView`：可清理的两阶段创建及开窗异常隔离；
- `App.ShowControlCenter`：托盘/启动意图共用 Dispatcher 入口；`OnXamlResourceReferenceFailed` 记录解析失败；

- `tests/DesktopUp.Contracts.Tests/SqliteStateStoreTests.cs`：首次创建、重复初始化、提交/冲突/幂等、事务回滚、双实例 revision、在线备份、旧库迁移前备份、损坏文件原样保留和迁移账本篡改拒绝；
- `tests/DesktopUp.Contracts.Tests/StateRepositoryTests.cs`：schema v2、跨连接恢复、共享 revision、CAS 冲突/重放及验证失败；
- `tests/DesktopUp.Contracts.Tests/StateProjectionCoordinatorTests.cs`：三端同 revision、command、冲突、幂等、权限、聚合读取和收起锚点；
- `tests/DesktopUp.Contracts.Tests/ArchitectureTests.cs`：UI 不依赖 repository/SQLite，只依赖 command/query projection 端口；
- `out/verify/v3-04-03/tests-final/`：97/97 Release 合同测试输出；
- `out/verify/v3-04-03/app-final/`：0 warning / 0 error Release 构建。

## 下一项

下一项M04-07集中多窗口人工；M04-06自动完成但实际鼠标、菜单/确认层级及空间观感不代填PASS。

## M02 自动证据

- tests/DesktopUp.Contracts.Tests/ScopedStateCommandTests.cs：27 项，独立/同实体 CAS、完整读集、批量失败、重放和跨协议保护、删除确认/零状态、并发初始化/写入、发布重入与坏订阅隔离。
- PosterCatalogTests.cs：追加 v3→v4 备份/空目录/Settings 保留、迁移失败回滚及当前命令表缺失检测；LegacyStateDatabase 固定 v1..3 migration hash。
- out/verify/v3-04-m02/results/m02.trx：147/147；out/verify/v3-04-m02/app/：Release 0 warning / 0 error。
- [M02 实际合同](TARGETED_COMMAND_PROJECTION_V2.md)、[验证记录](VERIFICATION.md)。

## M01 新增实现与证据

- AuthoritativeState.cs：Purpose/IsVisible/StackOrder/CollectionId/ThemeInstanceId；RepositoryContracts.cs：PosterStateSnapshot.EntityVersion。
- src/DesktopUp.Application/PosterCatalogContracts.cs：目录快照、结果与 IPosterCatalogRepository。
- src/DesktopUp.Infrastructure/SqlitePosterCatalogRepository.cs：原子目录读、创建/克隆新身份、移除配置、零初始化、请求指纹与持久回执。
- SqliteStateRepositories.cs：旧默认入口只初始化一次；旧 Save 保留新字段并推进实体版本，层序变更推进目录版本。
- SqliteStateStore.cs：只追加 migration v3；提交前目录结构读回、profile 和外键校验；源版本备份保留。
- tests/DesktopUp.Contracts.Tests/PosterCatalogTests.cs：20 项 SQLite 多海报合同用例。
- tests/DesktopUp.Contracts.Tests/LegacyStateDatabase.cs：从先前 Release 冻结 v1/v2 指纹，建立真实旧 schema 起点。
- out/verify/v3-04-m01/results/m01.trx：117/117；out/verify/v3-04-m01/app/：完整 Release，0 warning / 0 error。
- [实现合同](POSTER_CATALOG_SCHEMA_V3.md)：版本、初始化、关联、重放和后续边界。

## 多海报待改锚点（不是已实现清单）

- App.xaml.cs：M03 已去除单 `_poster`；共享发现/恢复、独立隐藏监听锚点、任务取消与等待退出。
- PosterCatalogContracts.cs / SqlitePosterCatalogRepository.cs：M01 compatibility global-CAS 保留；生产 M02 已接统一命令事务入口，不重复建库。
- StateProjectionContracts.cs / StateProjectionCoordinator.cs：M02 已实现集合投影/实体读集 CAS/动态 Tray 汇总；实际 UI 目录选择与新增删除按钮仍待后续。
- PosterVisualWindow.cs：M03 已接受显式绑定ID的初始projection、按保存显示器恢复；固定元信息/演示Item在M05/V3-05替换。
- DesktopLayerAttachment / PosterInputIslandManager：保留已验证算法，新增跨 cluster identity/排序与命中矩阵 → M04。
- 新合同与工作流见 [HANDOFF](HANDOFF.md)；M02 改状态链和现有三端消费者，未改宿主/命中/拖缩算法。

## 2026-09-27：R5 UI C纠偏后的新增入口

- `PosterContentContextRequest` / `PosterInputRouter.ResolveContext` / Windows input island：原生右键按当前session/layout/frame/Item见证分发，和左键capture分开。
- `ContentInteractionFeedbackPolicy.Describe`：从已求解结果派生整图承接/让位/屏外范围，不增加求解器或持久状态。App `ContentFeedback`用既有footer显示，`ContentEditing`同笔保存/撤销。
- `PosterMenuActionGate`：弹出菜单单次/当前实例权限。海报级`ShowPosterMenu`只保留四类动作；Item右键与中心设置分开。
- Windows `DesktopMenuInteraction.PlaceAndVerify` / `NativeDesktopMenuPlatform`：仅自身Popup/Input两步排列；每步完整序见证、末态严格层级，第二步精确同步许可；异常关闭恢复。`DesktopMenuInteractionTests`32项，candidate14的原生层级与命令读回通过，完整命中受遮挡尚未通过。
- `ControlCenterWindow.UiCWorkspace` / XAML：实际三模板纸样片、外观/行为、单原生标题及按需精调。`TemplateRecipe`为真实模板册、只读配方与本地AI说明；没有线上AI或包导入。
- `TemplateParameterSchema` / `TemplateParameterBindingAdapter`：声明按Kind造表单，按Binding连接既有Lamp实例与SettingsSession；默认值不可被日常参数覆盖。测试见`TemplateParameterSchemaTests`。
- `PosterGeometryTransitionSurface` / `PosterSpatialRuntime.ControlCenter` / `PosterVisualWindow.Transition`：中心resize的双轴原生容量与逐帧逻辑构图；`content.geometry_frame`记录实际Canvas/Input/Clip/native readback。原等宽收展独立合同保留。
- `App.ContentCompositionProbe` / `scripts/probe-content-composition.ps1`：100项正式几何命令、缩窄不写内容、松手整图承接、一次Undo、进程重启；从真实handler与发布表面取证。其probe协议不等于OS输入。

源码/包/验证与尚未交付范围见[对齐记录§7](LR_R5_UI_C_INTENT_ALIGNMENT.md#7-用户截图反馈后的实际c核对与第二轮纠偏)。

## 2026-09-27 六项反馈补充入口（候选16，运行待验）

- 日常/编辑饰件和命中：`PosterVisualWindow.Content.cs`/`ContentEditing.cs`；诊断footer显式开关：`ContentFeedback.cs`与`Start-DesktopUpFixtureProcess.ps1`。
- 配方声明/能力边界/不可变默认：Domain `TemplateRecipes.cs`、`TemplateParameterSchema.cs`；实例保真：`PosterPresentationParameters.cs`；注册CAS：Application `TemplateRecipeCommands.cs`及SQLite状态命令仓库，核心符号`RegisterTemplateCommand`/`TemplateRecipeCodec`。
- 通用表单/装入/副本/模板册/分页：`ControlCenterWindow.TemplateParameters.cs`、`TemplateRecipe.cs`、`UiCWorkspace.cs`；控制中心主题及统一弹窗：`Theme.cs`，只操作全局CenterTheme。
- 当前模板实际参数：`ResolvedPosterPresentation.TemplateParameters`，原始`Parameters.Lamp`只代表内置lamp，不能以custom覆盖它。
- 光照跟随/闲置摆动：`LampSceneMotion`→`PosterSpatialRuntime.LampMotion`共享frame→`PosterVisualWindow.LampScene`；`TemplatePresentationRefreshPolicy`仅在同定义的参数变化时保持原卡片直接更新绘制。
- 新验证：`probe-template-recipes.ps1`、`probe-content-interaction.ps1 -Mode Daily/Protocol`、33步`probe-lamp-parameters.ps1`。当前合同2065及App构建通过，不等于上述运行门已通过；当前状态以HANDOFF/VERIFICATION为准。
## 2026-09-27 最新输入与模块入口

- 控制中心默认黑底：`ControlCenterWindow.Theme.cs/RenderCenterTheme`仅修改字典直接拥有的画刷；`ControlCenterWindow.xaml`完整按钮状态/UiCDropDown；`LampColors.cs`弹窗主题作用域，`TemplateParameters.cs`显式动态按钮样式。`ThemeProbe.cs`经配方门检验四主题及system重启，证据见输入报告§6。
- `Windows/NativeDesktopPointerPlatform.CoversPoint`：只在分层窗口属性读取成功且整体alpha=0时排除遮挡；未知/色键独用/非零alpha保持保守。`PassivePosterPointerNativeTests`包含真实自有HWND透明度切换；候选20真实鼠标跟随与离开轻摆通过。
- `Application/PosterInputRouter.cs`的`CapturesPointer/Resolve/Begin/ResolveContext`及`Windows/PosterInputIslandManager.cs`的`BuildWindowRegion/HitTest/CompleteContentContext`：排除Viewport整面原生命中，右键按下/抬起同目标见证。
- `Domain/ContentPinPresentationPolicy.cs`、`App/PosterVisualWindow.Content.cs`、`ContentMenu.cs`：共享22 DIP图标边界，正式解除/撤销，旧锁和循环剪裁。
- `Windows/PassivePosterPointerObserver.cs/LastDiagnostics`、`App/PosterSpatialRuntime.LampMotion.cs`、`App.LampPointerProbe.cs`、`scripts/probe-lamp-pointer.ps1`：同次原生指针诊断与真实外部移动门，生产规则未放松。
- `App/PosterVisualWindow.SparseInputProbe.cs`、`ContentInteractionProbe.cs`及`SparseContentInputTests`：实际HRGN、接收者读回和图钉处理器，不能替代OS手势。

当前证据见[输入报告](LR_R5_POINTER_AND_MODULE_REVIEW.md)，框架改造边界见[模块提案](../../20_Architecture/CONTENT_SCENE_MODULE_DECOUPLING_PROPOSAL.md)。以下旧入口的“未实现”等应结合最新记录，不独立裁决状态。

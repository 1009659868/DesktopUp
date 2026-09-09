# V3-04 代码锚点

状态：`M07 IN PROGRESS — CONTRACT/BUILD PASS；CURRENT-ENVIRONMENT / MANUAL PENDING`

## M07 当前锚点

- [M07合同与人工矩阵](M07_MEASUREMENT_AND_SAFETY.md)：18格×2同环境基线、单核/整机CPU口径、工作集与私有提交区分、原生提交不等于呈现FPS、S1～S5集中安全门。
- [外部采样](../../../../scripts/measure-m07.ps1)、[固定入口](../../../../scripts/start-m07-measurement.ps1)、[统计数学](../../../../scripts/MeasurementMath.cs)：500ms外部读进程，8～18秒稳态窗口、完整性检查、实际权限记录；不因管理员权限拒绝启动，输出原始JSON，不改正常App轮询。
- [MeasurementFixtureData](../../../../tests/Shared/MeasurementFixtureData.cs)、SessionFixture的`--measure-expanded/collapsed/hidden`：仅测试1/3/6配置，既有生成模式仍0..3；[MeasurementContractTests](../../../../tests/DesktopUp.Contracts.Tests/MeasurementContractTests.cs)新增6项，完整411/411。
- `PosterSpatialRuntime.Smoke.cs/.Posture.cs`：显式隔离smoke开计时，记录`spatial.probe_frame`实际提交成功/序号/QPC/耗时，以及`spatial.probe_command_completed`调用到回调；[时序脚本](../../../../scripts/measure-m07-spatial.ps1)验证计划/实际帧数，不假设每个计划帧都呈现。
- [人工入口](../../../../scripts/start-m07-manual.ps1)与[6产物指纹](../../../../scripts/m07-build.json)：4个隔离配置、当前5.1兼容、已有进程拒绝、数据不重建；5.1真实启动/退出已通过，未代填鼠标/视觉PASS。
- [共享启动器](../../../../scripts/Start-DesktopUpFixtureProcess.ps1)：ProcessStartInfo子进程环境，兼容5.1/7、不修改父进程环境或系统权限；[启动回归](../../../../scripts/test-m07-launcher.ps1)检查7脚本AST、统计兼容、入口指纹及真实三海报/单中心/托盘/正常退出。

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

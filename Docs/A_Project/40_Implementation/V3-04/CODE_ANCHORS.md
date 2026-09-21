# V3-04 代码锚点

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

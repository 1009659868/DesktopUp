# V3-04 代码锚点

状态：`M04-01 AUTO PASS / MANUAL PENDING`

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

完成 M03 短人工门后进入 M04 多 cluster 输入/层序。M03 最终0/1/3真实进程和等待中退出已通过；不以自动读回代替人工层级与交互观察。

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

# V3-04 Command/Query 与三端 Projection 合同

状态：`IMPLEMENTED FOR V3-04-03 — MANUAL SURFACE GATE PENDING`

最后更新：2026-09-04

## 1. 目标

V3-04-03 在 V3-04-02 的 Poster/Settings repository 之上建立唯一进程内状态协调边界。真实海报、控制中心与托盘不得各自读取数据库、保存本地业务副本或根据按钮文字反推状态；三端只消费从同一 committed revision 生成的 projection。

## 2. 端口分离

- `IStateCommandService`：接收版本化 Poster/Settings command；UI 只能通过该端口请求状态变更；
- `IStateProjectionCoordinator`：初始化、查询当前 projection、发布单调递增的 `ProjectionChanged`；读取端口不附带写权限；
- `IStateProjectionRepository`：在同一个 SQLite read transaction 中读取 Poster、Settings 和 global revision；
- `IPosterRepository` / `ISettingsRepository`：仍是 Application 层端口，具体 SQLite adapter 只存在于 Infrastructure。

当前两个 Application 端口由同一 `StateProjectionCoordinator` 实例实现，以一把进程内串行锁协调 Poster/Settings 命令顺序；端口权限仍保持分离，未来拆成独立进程或消息总线时不要求 UI 改写。

## 3. Command 合同

每个命令包含：

- 唯一 request ID；
- 来源：Poster、Control Center 或 Tray；System 不能伪装成交互命令；
- 命令类型；
- expected global revision；
- Poster target（Poster 命令）；
- 请求的完整目标领域值。

结果区分 `Committed`、`NoChange`、`Replayed`、`Conflict`、`PermissionDenied`、`ValidationFailed`、`NotReady` 和 `Failed`，并尽可能返回当前 projection。旧 expected revision 不写入；锁定海报拒绝移动/缩放；收起海报拒绝缩放；锁定、收起和 Settings 命令不得夹带无关字段变更。

同一 request ID 在单进程内使用有界缓存重放，返回 `Replayed`，不进行第二次数据库提交，也不进行第二次 projection 广播。

## 4. Projection 合同

一个 `DesktopUpProjection` 原子包含：

- `PosterSurfaceProjection`：Poster committed definition、poster row revision 和移动/缩放/收起权限；
- `ControlCenterProjection`：同一 Poster projection 与 Settings projection；
- `TrayProjection`：海报数量、全局锁定、动态效果暂停和 tooltip；
- 顶层 global revision。

顶层、Poster、Control Center、Control Center 内嵌 Poster/Settings 和 Tray 的 revision 必须完全相同。Poster row revision 或 Settings row revision只表示对应行最后变化的 global revision，不能替代顶层版本。

聚合读取失败但 repository 已明确提交时，协调器用 repository 返回的 committed snapshot 和当前未变化侧构造同 revision 兜底 projection，避免数据库已经提交而三端停留在旧版本。

## 5. 发布与防回弹

1. 命令先通过权限和字段边界校验；
2. repository 使用 expected revision CAS 提交；
3. 成功后执行一次聚合查询并构造三端 projection；
4. 先原子替换 coordinator 当前值，再广播一次；
5. 每个表面拒绝低于自身已应用 revision 的 projection；
6. revision 只由 Settings 推进时，真实海报只更新 global revision，不移动、缩放或重建窗口；
7. Control Center 关闭时保存的只是最近 projection 引用，重新创建窗口后先应用最新正式 projection；
8. 命令冲突返回当前 projection，UI 不用旧的本地状态回写。

## 6. 三端当前接入

### Poster

- 启动通过 query 端初始化并恢复 committed Poster；
- 拖动/缩放完成、锁定和收起发出明确 Poster command；
- 来自 Control Center/Tray 的 Poster projection 更新真实几何、输入权限和视觉状态；
- 自己 request 的同步广播不重复应用物理帧。

### Control Center

- 不再显示 V3-01 静态说明，显示 global/poster/settings revision、几何、锁定、收起、暂停和性能模式；
- 锁定、收起、暂停按钮只调用 App 组合层提供的 Application command；
- 关闭后不影响海报和托盘；重新打开立即显示最新 projection。

### Tray

- tooltip、锁定勾选和暂停勾选来自 `TrayProjection`；
- 锁定与暂停菜单在 projection ready 前禁用；
- 点击后只发 command，不先乐观翻转勾选；
- 单击、双击、编辑和退出的既有语义不变。

## 7. 收起位置语义

收起/展开改变可见高度但不得改变海报顶部。协调器依据 `NormalizedExpandedHeight`、当前可见高度和工作区锚点，把同一 normalized top 转换为目标高度对应的 `VerticalAnchor`；因此来自海报、控制中心或托盘的收起命令使用同一算法，重启后也不会跳位。

## 8. 范围边界

V3-04-03 不宣称：

- 控制中心桌面画布与可拖缩缩略预览；
- 标题、模板、主题和内容参数完整编辑；
- 海报显隐/系统暂停/degraded 等运行时 projection；
- 最近可用数据库恢复 UI；
- 真实 Item、热度和动态排版。

完整控制中心预览与编辑属于 V3-04-04；其余能力按 V3-04-05 至 V3-06 推进。

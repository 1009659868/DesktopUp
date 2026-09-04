# V3-04 验证记录

状态：`IN PROGRESS — V3-04-03 IMPLEMENTED / MANUAL THREE-SURFACE GATE NEXT`

最后更新：2026-09-04

## V3-04-01 交付

- 建立 Application 层 `IStateStore`，SQLite API 仅存在于 Infrastructure；
- 建立 schema v1 的 migration ledger、单例 state head 和 operation journal；
- 建立 expected revision CAS、幂等重放和同事务业务 mutation 入口；
- 建立旧库迁移前在线备份、手动在线备份、临时文件验证和原子改名；
- 建立 `quick_check`、application id、user version、migration checksum、head/revision 验证；
- 损坏或不兼容状态不被删除或空白默认值替换；
- 状态初始化已置于托盘、控制中心和海报创建之前。

详细合同见 [STATE_STORE_SCHEMA_V1.md](STATE_STORE_SCHEMA_V1.md)，实现位置见 [CODE_ANCHORS.md](CODE_ANCHORS.md)。

## V3-04-02 交付

- 追加 immutable schema migration v2：`posters` 与单例 `app_settings`；
- 冻结 Poster/Settings 持久字段和明确的运行时非持久字段；
- 建立 `IPosterRepository`、`ISettingsRepository`、一致 snapshot 和结构化写入结果；
- 所有 repository 写入复用 global expected revision CAS，不绕过 V3-04-01 transaction；
- 建立 Application service 边界，海报 UI 不接触 SQLite 或具体 repository adapter；该临时端口在 V3-04-03 收敛为读写分离的 `IStateCommandService`/`IStateProjectionCoordinator`；
- 海报首次启动读取/创建 `poster-main`，并从 committed snapshot 恢复几何、锁定和收起；
- 拖动/缩放只在完成时提交一次，锁定/收起写入同一 Poster snapshot；
- settings-only revision 冲突只安全重基一次；Poster 行并发变化或失败时恢复最新 committed snapshot；
- 显示拓扑重投影不写数据库，编辑模式、输入岛、HWND 和物理像素不持久化。

字段与 revision 合同见 [POSTER_SETTINGS_REPOSITORY_V2.md](POSTER_SETTINGS_REPOSITORY_V2.md)。

V3-04-02 的普通退出/重启恢复门已获用户确认并进入 V3-04-03。

## V3-04-03 交付

- 建立分离的 `IStateCommandService` 与 `IStateProjectionCoordinator` Application 端口；
- 建立单事务 `IStateProjectionRepository` 聚合查询，Poster/Settings 不再跨 transaction 拼接；
- 建立 Poster、Control Center、Tray 三份同 global revision projection；
- 建立 request/source/kind/expected revision command 与八类明确结果；
- 对 stale revision、锁定缩放、收起缩放和夹带字段执行拒绝，不覆盖正式状态；
- 同 request ID 有界幂等重放，不二次提交或广播；
- Control Center 显示 committed revision、Poster/Settings 状态，并通过 command 切换锁定、收起和暂停；
- Tray tooltip、锁定/暂停勾选消费 TrayProjection，ready 前不允许状态写入；
- Poster 消费来自其他表面的 projection；settings-only revision 不触发物理帧重建；
- 收起/展开在 Application 核心换算目标锚点，保持 normalized top 不变；
- 三端均拒绝旧 projection，控制中心关闭重开先应用保存的最新正式 projection。

完整合同见 [COMMAND_QUERY_PROJECTION_V1.md](COMMAND_QUERY_PROJECTION_V1.md)。

## 自动测试

命令：

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-01\tests-final"
```

V3-04-01 结果：`PASS — 81/81`。

新增八项真实 SQLite 夹具覆盖：

1. fresh schema 创建及重复初始化幂等；
2. CAS commit/conflict/replay；
3. 业务 mutation、revision 和 operation journal 整体回滚；
4. 两个 StateStore 实例共享同一数据库 revision；
5. 在线备份保留已提交 revision 并通过验证；
6. legacy v0 在迁移前产生可读备份；
7. 损坏数据库初始化失败且原字节不被替换。
8. migration ledger 名称/校验值被篡改时拒绝启动。

V3-04-02 命令：

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-02\repositories-final"
```

结果：`PASS — 87/87`。新增六项真实 SQLite repository 夹具覆盖 schema v2 默认值、默认 Poster 创建、跨连接恢复、CAS 冲突/重放、Poster/Settings 共享 revision 及非法状态不推进 revision。

V3-04-03 命令：

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-03\tests-final"
```

结果：`PASS — 97/97`。新增 command/projection 与 v1→v2 回归覆盖：三端同 revision、Control Center Poster command、Tray Settings command、stale 冲突、重复 request、锁定权限、单事务聚合查询、收起顶部保持、UI 端口边界、源版本备份及临时边车零残留。

## Release 构建

```powershell
dotnet build "G:\A_\Tools\DesktopUp_V2\src\DesktopUp.App\DesktopUp.App.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-01\app-final"
```

结果：`PASS — 0 warning / 0 error`。

V3-04-02 Release 构建：

```powershell
dotnet build "G:\A_\Tools\DesktopUp_V2\src\DesktopUp.App\DesktopUp.App.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-02\app-final"
```

结果：`PASS — 0 warning / 0 error`。

V3-04-03 Release 构建：

```powershell
dotnet build "G:\A_\Tools\DesktopUp_V2\src\DesktopUp.App\DesktopUp.App.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-03\app-final"
```

结果：`PASS — 0 warning / 0 error`。

## 真实启动顺序 smoke

从 `out/verify/v3-04-01/app-clean/DesktopUp.exe` 以 `--exit-after-ms=5000` 启动，PID 45840。结果：

- 创建 `%LOCALAPPDATA%\DesktopUp\V3\state\desktopup.sqlite3`，大小 24576 bytes；
- 结构日志顺序为 `state_store.initialized(status=Created, schemaVersion=1, revision=0)`、`tray.created`、`poster.shown`；
- 退出记录 `application.stopped(activeResources=0)`；
- 退出后无 DesktopUp 进程残留。

## V3-04-02 人工重启门

使用 `out/verify/v3-04-02/app-final/DesktopUp.exe`：

1. 拖动并八向缩放海报后普通退出、重新启动，位置和尺寸恢复；
2. 切换锁定后普通退出、重新启动，锁定状态恢复且使用模式不能拖动；
3. 切换收起后普通退出、重新启动，收起状态及展开高度恢复；
4. 反复拖动/缩放期间无逐帧数据库写入引起的卡顿、白底或层级回退；
5. 退出后无进程或窗口残留。

结果：用户确认通过并要求进入 V3-04-03。

## V3-04-03 真实 smoke

- 首轮真实 v1 数据库迁移发现迁移前备份错误按 v2 校验；启动被安全拒绝，原数据库未被覆盖；
- 已改为迁移前备份按源 schema 校验、普通备份按当前 schema 校验，并加入 v1→v2 自动回归；
- 同时修复临时备份校验产生的 `.tmp-wal/.tmp-shm` 边车残留，并清理本轮 smoke 产生的精确临时文件；
- 修订后 PID 31072 成功创建 v1 备份并迁移至 v2；
- 最终 PID 16392 从 schema v2/revision 1 正常启动，发布 System projection revision 1，挂载 Poster 后正常退出；
- 最终 `activeResources=0`、DesktopUp 进程 0、`*.tmp*` 备份残留 0。

## V3-04-03 人工三端门

使用 `out/verify/v3-04-03/app-final/DesktopUp.exe`：

1. 双击托盘打开控制中心，确认显示 committed、poster、settings revision；
2. 在控制中心切换锁定，真实海报立即不可拖缩，托盘“锁定全部海报”同步勾选；
3. 从托盘解除锁定，控制中心按钮/状态和真实海报输入权限同步恢复；
4. 在控制中心连续收起、展开三轮，真实海报顶部不跳位，控制中心状态同步；
5. 分别从控制中心和托盘切换“暂停动态效果”，两处按钮/勾选与 tooltip 同步；
6. 拖动并缩放真实海报，手势完成后控制中心几何与 revision 更新，拖动过程不逐帧增长 revision；
7. 关闭并重新打开控制中心，立即显示最新状态，不回弹到旧值；
8. 正常退出并重启，锁定、收起、暂停和几何保持；退出无进程/窗口/托盘残留。

控制中心当前是状态/命令面板；真实桌面缩略画布和预览拖缩属于 V3-04-04，不在本门判定。

## 范围边界

V3-04-03 仍没有宣称：

- 完整控制中心预览编辑和用户可理解的冲突 UI；
- 最近可用状态恢复 UI；
- 真实 ContentItem、启动统计、热度、动态缩放或排序。

这些能力依次属于 V3-04-04 至 V3-04-08、V3-05 和 V3-06。

## 下一步

先执行 V3-04-03 人工三端门。通过后进入 V3-04-04：实现控制中心桌面预览画布、位置/尺寸编辑及内容参数入口，继续复用本项 command/projection，不建立第二状态权威。

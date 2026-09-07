# V3-04 验证记录

状态：`M04-01 AUTO PASS / MANUAL PENDING`

最后更新：2026-09-07

## M04-01 最新实现与验证

中心完整列表/稳定ID选择、所选锁定/收展/显隐、进程内重开保留、版本绑定命令和运行状态已实现。完整176/176（新增13项）；最终Release0warning0error。最后三张PID20360/零张39740：目录3/0与真实会话一致，1中心/2次show、1共享监听、exit0/resources0；首次PID26340是列表焦点优化前开窗证据。TRX、最终hash和隔离目录见 [M04合同](M04_CONTROL_CENTER_SELECTION.md)。

实际点击未执行：computer-use初始化及reset重试均因CreateProcessWithLogonW1385退出；不能据此标记人工通过。未重启Explorer或运行Win+D输入，原核心门保留。测试为elevated=true，不代表普通权限认证；无schema/用户真实数据/桌面文件变更；未commit/push。[DeskBox吸收记录](DESKBOX_SPATIAL_REFERENCE.md) 为来源/适配边界，空间算法未接生产。

## 2026-09-07 多海报空间补充（文档变更）

用户确认目前多海报可正常交互，反馈中心仅A；不可重叠、收展推让及两阶段保尺寸展开新增为需求。明确回答：锁定者不可被推、固定障碍；未再调整者条件归位。未报告的Explorer/Win+D等不记PASS。

本轮只读当前中心/投影和DeskBox源码，修改需求、空间设计、计划与导航，没有应用代码或数据变更，没有重跑build/test/smoke，没有commit/push。历史163/163和0/1/3进程证据保留，不覆盖新增空间功能。中心根因和来源证据见 [空间设计](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md)。M04仍PLANNED；集中人工出口见细化工作流§4.1。

文档检查：本次18份Markdown中的107个本地文件链接均存在；按父章节分组检查无重复标题，无冲突标记；`git diff --check -- Docs` exit0。最初全文件同名标题检查提示总计划重复“工作项/出口门”等，经父章节核对为合法复用，未改写。§5三种展开算例算术核对3/3，仅验证文档公式，非应用自动测试或动画验收。

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

V3-04-03 R1 Release 构建（2026-09-07 起替换旧开窗失败产物）：

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\build.ps1" -Configuration Release -OutputDirectory "out\verify\v3-04-03\app-final"
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

## V3-04-03 R1：托盘双击崩溃回归（2026-09-07）

历史故障：原人工门首项失败，双击托盘打不开控制中心并使整个进程崩溃。当时本项保持未关闭，不以 97 项合同测试替代真实开窗；后续用户人工确认见本文件末尾。

根因证据：

- Windows Application 事件 1026（2026-09-07 10:35:26，PID 5788）记录 `XamlParseException → ControlCenterWindow.InitializeComponent → ControlCenterService.Show → H.NotifyIcon.TaskbarIcon.OnMouseEvent`；
- 用 Windows SDK `makepri dump` 检查原 `app-final/DesktopUp.pri`，其 XBF 名称是 `app-finalApp.xbf`、`app-finalControlCenterWindow.xbf`；而生成的连接器查找 `ms-appx:///App.xaml`、`ms-appx:///ControlCenterWindow.xaml`；
- 实际 WinUI 依赖为 `Microsoft.WindowsAppSDK.WinUI/2.3.6`。其 `Prep_ComputeProcessXamlFiles` 使用 `$(OutputPath)` 直接拼接 XAML 相对文件名；先前 `dotnet build -o .../app-final` 的全局 OutputPath 缺末尾目录分隔符，导致 XBF 被写到输出目录旁边并以错误名字打包；
- 无参构造对照不能解决（PID 23832）；仅给输出目录补分隔符后，真实控制中心成功打开（PID 31884）。此问题不需要修改海报层级、数据库或主题样式。

修复：

- `scripts/build.ps1 -OutputDirectory` 统一解析绝对输出目录并补末尾分隔符，且检查 restore/build 退出码；
- App 工程增加 `DUV3001` 构建检查，禁止缺分隔符的自定义 OutputPath 再静默生成错误 XBF；
- 托盘和启动意图共用 UI Dispatcher 开窗入口；窗口分为 native Window 构造和 XAML 初始化，以便失败时关闭未完成窗口；
- 开窗局部异常记录阶段、HRESULT、堆栈和 restricted description，清理失败窗口，不让异常越过原生托盘回调；XAML 资源解析和控制中心应用 projection 增加日志；
- 新增 `scripts/smoke-control-center.ps1`，必须实际创建控制中心、显示 projection、再次激活复用唯一窗口、保持一张海报并零残留退出；仅进程存活不再算该 smoke 通过。

自动证据：

- 非法输出目录构建按预期失败，错误码 `DUV3001`；规范入口 Release 0 warning / 0 error；
- 修订产物 PRI 名称恢复为 `App.xbf`、`ControlCenterWindow.xbf`；
- 去掉无效构造实验后的修订版（PID 22768）创建控制中心 1 次、显示请求 2 次、应用 projection 1 次、海报 1 张，exit code 0，activeResources 0；
- 现有合同测试 `97/97 PASS`（`out/verify/v3-04-03/tests-tray-fix/`）；
- 失败隔离对照（PID 18980）仍检测到 XAML 错误，但程序不崩溃，限时正常退出且 activeResources 0；
- 用户原启动目录 `out/verify/v3-04-03/app-final/` 已重建并完成相同 smoke（PID 13440）：控制中心创建 1 次、显示请求 2 次、应用 projection 1 次、海报 1 张，exit code 0，activeResources 0；该目录 PRI 也确认是正确的 `App.xbf` 与 `ControlCenterWindow.xbf`。

运行真实开窗 smoke：

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\smoke-control-center.ps1" -ExecutablePath "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-03\app-final\DesktopUp.exe"
```

限制：Windows computer-use 初始化两次均失败于 `CreateProcessWithLogonW 1385`，没有执行真实鼠标双击或关闭后重开。CLI 的二次激活覆盖相同开窗处理入口，但不能替代实际托盘输入。该自动修复轮次当时等待用户确认“双击打开 → 重复双击保持唯一 → 关闭后再双击恢复”；后续确认已收到，见下方用户 PASS 记录。

## V3-04-03 人工三端门（R1 复测）

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

2026-09-07 用户明确“人工验证通过”。按当前三端门关闭 V3-04-03（USER-REPORTED PASS；本轮未由工具重复执行鼠标操作）。随后用户确认通用多海报方向，下一步改为 V3-04-M01，多海报前置完成后再进入 V3-04-04。

## 2026-09-07 需求/架构/计划同步记录

- 原位修订七份需求：通用类型/安全收纳、多海报独立配置与主题、唯一中心/托盘、热度/AI范围；保留旧特色细节。
- 新建通用多海报设计合同与详细工作流；修正 journal/真实移动的依赖、参数合同前置、重复任务、基础可用发行包顺序。
- 仅文档，未运行新测试、未改业务代码/用户数据库/文件。多海报新门全部待实现；97/97 等数据保留为已有证据。
- 具体立即下一步、代码缺口与受影响验收见 [HANDOFF](HANDOFF.md)。

## 2026-09-07 V3-04-M01：数据/目录仓储门完成

结论：PASS（本项出口是数据/仓储合同；没有新增桌面鼠标人工门，不代表 M03..07 通过）。

- 分支 impl/poster-redesign-V3，HEAD 为 1766070＋dirty；保留上一轮未提交的托盘开窗修复，本轮未 commit/push。
- 新增 schema v3、Poster 独立显隐/用途/层序/主题关联、EntityVersion、profileInitialized/CatalogVersion。
- IPosterCatalogRepository：List/Create/CloneConfiguration/Remove/InitializeEmptyProfile；沿用同一 SqliteStateStore/global CAS；身份由核心生成、请求指纹和回执与状态事务提交。
- 旧默认入口与 Save 保留；并发初始化只播种一次；移除至零后重启或重放旧请求不会复活配置。
- 从已验证 v3-04-03/app-final 程序集提取并固定 v1/v2 指纹；迁移不改 SQL/hash/旧字段，.gitattributes 固定该 SQL 源文件 LF，避免 Windows checkout 改变原始字符串哈希。

### 自动证据

命令：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m01/tests/ --logger "trx;LogFileName=m01.trx" --results-directory out/verify/v3-04-m01/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m01/app/'
```

- 测试：117/117，通过；0 失败、0 跳过；新增 PosterCatalogTests 20 项，保留此前 97 项。
- 完整 WinUI Release：0 warning、0 error，退出码 0。
- 文档校验：38 份 Markdown 的本地链接失效 0；当前入口无旧 NEXT M01；git diff --check 退出码 0；产物指纹与 TRX 117/117 已重新读回核对。
- 测试环境：本机 Windows、.NET 10/x64、Microsoft.Data.Sqlite；命令因当前工作区写权限边界获批准运行。数据库为测试创建的独立临时目录，测试结束清理；本轮没有测试普通用户窗口权限矩阵。
- 覆盖：创建/复制/移除/排序/0-1-3目录、独立身份与配置、显隐、实体与全局/目录版本、双连接冲突、并发首次初始化、跨重启幂等、改目标/请求体拒绝、缺失目标、删至零、插入和移除后故障回滚。
- v1/v2 兼容：新建真实旧 schema 起点；旧海报全部 21 列及 Settings 字段原样比较；源版本备份、当前在线备份、重复初始化、损坏数据库/账本/profile 保留。
- 故障用例一度失败：同名错误 profile 表存在时，原流程更新账本并报告 Migrated，但表结构错误。补提交前结构/profile/外键读回，确认 v3 全事务回滚、原库/源备份保留后，完整回归通过。没有把失败测试删除或改成预期成功。

| 产物 | SHA-256 |
| --- | --- |
| out/verify/v3-04-m01/app/DesktopUp.exe | 4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8 |
| out/verify/v3-04-m01/app/DesktopUp.Infrastructure.dll | 0BBED987568C964CC88C6C5EE4B82883E42FE6CA4284142F886EDF4050A660FA |
| out/verify/v3-04-m01/results/m01.trx | 2A2576AC34BCBBED791ED9B42F2675ACD65847EE535203ACDAFEA215B30E4DE4 |

### 未执行与后续

没有启动用户实际配置库或操作桌面文件。现有 smoke-control-center.ps1 使用真实 LOCALAPPDATA/V3，不是隔离配置脚本，本轮未运行它以免顺带迁移真实库；既有开窗/托盘人工 PASS 保留但不算本次新增证据。没有修改窗口/输入逻辑，不重新要求 DPI/拖缩人工矩阵。

当前运行/投影仍单海报。下一项 V3-04-M02 扩实体读集 CAS、目标化命令和目录/三端投影；M03 才建立多海报会话。实际语义和限制见 [POSTER_CATALOG_SCHEMA_V3](POSTER_CATALOG_SCHEMA_V3.md)。

## 2026-09-07 M01 后续需求澄清（DOCUMENTATION ONLY）

用户明确：首次无配置一张默认；中心/托盘新增；中心/海报“...”删除并弹出后果确认；单文件控制习惯配置以便跨主机应用。已更新画像、运行表面、视觉、主题、追溯/风险、架构、模块、总计划、细化工作流和交接。

SQLite 继续作为运行时唯一权威，PortableProfile 是待实现的显式导出/导入边界；位置/尺寸/状态与主题依赖可携带，实际文件/凭据不隐式复制。04-06-A..D 增加基础配置迁移，07-04复用自定义主题载荷，M02仍是下一项。

本次未改业务代码、测试或用户数据库，未运行新的功能测试；没有把117/117扩展为新增确认弹窗或跨机导入的PASS。

文档检查：38份 Markdown，本地链接失效0、完整章节号重复0；git diff --check退出码0。未执行commit/push。

## 2026-09-07 V3-04-M02：目标化命令、投影与实体读集 CAS

状态：AUTO PASS（代码/合同/Release/隔离实际进程 smoke）；受影响人工交互回归待用户确认，尚未关闭为 PASS。没有提前执行 M03，也不宣称多海报窗口完成。

### 本轮实现

- 生产统一 StateProjectionCoordinator → StateCommandPolicy → IStateCommandRepository → SqliteStateCommandRepository，同一 SQLite immediate transaction 内完成读集校验、写入、回执及完整快照。
- Poster/Settings EntityVersion 与 global revision 分开；额外读依赖全部校验；目录成员相关命令额外检查 CatalogVersion。无关 A/B/Settings 写入不再伪冲突；同实体旧版本、缺失实体及批量多个失败目标均明确返回。
- Create/Clone/Remove/Batch 命令及删除影响预览；确认携带原 ID/实体/目录版本，不动实际文件。首次仅一张，已初始化0或N张不补默认，晚到/重放请求不复活已删除目标。
- PostersById/有序列表/changed IDs、SettingsChanged/CatalogChanged、托盘总数/显隐/锁定汇总；三端同 revision，FIFO 发布/订阅异常隔离。移除旧内存 requestId-only 重放和混合 committed fallback。
- 追加 schema v4 的 Settings 实体版本和命令持久回执；v1..3 SQL/hash 不变；v3→v4 的源版本备份及提交前结构检查通过。
- App 与现有海报消费者显式传目标 EntityVersion，窗口只查自身 ID；托盘“锁定全部”调用完整读集批量命令；中心/托盘支持零目录；不重写 Host/拖缩/命中算法。
- smoke 加入强制隔离 DataRoot（PowerShell 7.4+）；普通用户路径不变；测试覆盖不能转发给仍运行的旧版。

### 自动执行与结果

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m02/tests/ --logger "trx;LogFileName=m02.trx" --results-directory out/verify/v3-04-m02/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m02/app/'
& ./scripts/smoke-control-center.ps1 -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\app\DesktopUp.exe' -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\smoke-data'
```

- 147/147 测试通过，0失败、0跳过。新增 ScopedStateCommandTests 27项及 v4 迁移3项；保留117项。包括双 SQLite 连接竞争同/不同实体、同时首次初始化、批量第二目标 SQL 失败全回滚、全目标成员变化冲突、NoChange 持久重放、跨旧协议请求 ID 防护、额外读依赖及坏订阅者/重入有序。
- 首轮旧测试5项只因当前 schema 期望3而实际4失败，已更新“当前版本”断言；旧 migration 指纹仍固定。新增夹具一处备份属性命名编译错误已修正。最终完整回归通过，未删除失败场景。
- 完整 WinUI Release：退出码0、0 warning、0 error。中间暴露 ControlCenterService 的可空 Poster 日志解引用，已修复；没有忽略零目录警告。
- 实际进程 smoke：旧版 PID8600 最初占用单实例；未由工具终止。其退出后启动新产物 PID40212，日志15:22:18～15:22:28（Asia/Shanghai）。1个控制中心创建，2次显示请求复用，1张海报创建；中心投影 revision1、海报revision1，schema4；退出码0，errors0，resource-ledger clean=true/resources=[]，结束后无 DesktopUp 进程。
- 环境：本机 Windows/.NET10/x64/PowerShell7.6.5；构建与测试因工作区权限边界批准运行。smoke 日志明确 isElevated=true、manifest asInvoker，因此只证明该权限环境下开窗/投影/退出，不能充当普通用户 Shell/输入认证。人工请使用普通权限终端。
- 数据：合同测试只使用临时 SQLite 夹具；实际 smoke 使用 out/verify/v3-04-m02/smoke-data，未迁移用户 LOCALAPPDATA/V3 实际库、未改用户文件/快捷方式。测试进程已正常退出。

| 产物（相对项目根） | SHA-256 |
| --- | --- |
| out/verify/v3-04-m02/app/DesktopUp.exe | 4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8 |
| out/verify/v3-04-m02/app/DesktopUp.dll | 6737DCBF19C41CA249589AA5EA2F9EDC37104BCE26EEC9AD4027782AC036461D |
| out/verify/v3-04-m02/app/DesktopUp.Application.dll | 3F26A44E826878667D7E2B0B23F7012C255365A6A8E902D1E9B625BE7C8E4B7D |
| out/verify/v3-04-m02/app/DesktopUp.Infrastructure.dll | E3F44BD7565334D1D5D7FF12EBA89774EC68707A4D1A4F31AA1443D01A307863 |
| out/verify/v3-04-m02/results/m02.trx | 6B8C8D031492282681D059DB2C76229A266596FAAB08FD6559C76529D4D7BCEF |
| out/verify/v3-04-m02/smoke-data/logs/desktopup.jsonl | B7D08FE682E9ADECC923ABB751B218D5CAFA01BCFF6D16A8C7949AA2222AE205 |
| out/verify/v3-04-m02/smoke-data/state/resource-ledger.json | 1B5523C3B79695D20AA6661364E981A44AFFD0580FBDA2068BA9ABBE998257D8 |

EXE 为 .NET 启动宿主，其 hash 可与旧版相同；实际实现变更请同时核对三个 DLL，不只比较 EXE。

### 人工门与下一项

使用普通权限 PowerShell7.4+，无旧实例时启动隔离手工配置（不会15秒自动退出）：

```powershell
Start-Process -FilePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\app\DesktopUp.exe' `
  -ArgumentList '--show-control-center' -WindowStyle Hidden `
  -Environment @{ DESKTOPUP_V3_TEST_DATA_ROOT = 'G:\A_\Tools\DesktopUp_V2\out\manual\v3-04-m02' }
```

1. 中心锁定/托盘解锁后，海报权限与两端显示同步；暂停状态也同步。
2. 收起/展开保持顶部位置；拖动/缩放结束后中心几何/revision更新，无白底或交互回归。
3. 关闭中心后海报继续运行，托盘重新打开显示最新状态；普通退出/重启恢复。
4. 点击/刷新、普通窗口覆盖、Win+D、退出无残留维持既有行为；不重跑全量 DPI 或安装测试。

确认后关闭 M02，进入 M03 会话注册/共享恢复。本次没有提供新增/删除按钮、多窗口 registry、真实文件收纳、便携配置/主题/热度/AI。完整合同见 [TARGETED_COMMAND_PROJECTION_V2](TARGETED_COMMAND_PROJECTION_V2.md)。

分支 impl/poster-redesign-V3，HEAD1766070+dirty；保留前轮未提交代码与文档。本轮未 commit/push。

最终文档检查：38份 Markdown（包含新 M02 合同），本地链接失效0；smoke 脚本语法错误0；按仓库原有换行配置运行 git diff --check 退出码0；再次检查 DesktopUp 运行进程数0。

## 2026-09-07 V3-04-M03：会话注册与共享恢复

状态：AUTO PASS / MANUAL PENDING。用户同意下一步M03，未据此编造M02逐项人工通过。分支impl/poster-redesign-V3，HEAD1766070＋dirty；未commit/push。

实现：PosterSessionRegistry、IPosterSession/Factory、WinUiPosterSession与按ID/epoch的资源/日志；共享环境发现/节流/版本门；零目录独立监听；按保存显示器恢复；取消旧回调、局部失败隔离、每张读回和等待异步恢复退出。DesktopLayerAttachment及既有拖缩/输入算法未重写。SQLite schema仍v4，无新迁移。

### 自动与构建

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m03/tests/ --logger 'trx;LogFileName=m03.trx' --results-directory out/verify/v3-04-m03/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m03/app/'
```

最终163/163，新增16项registry合同夹具，旧147项保留；TRX：out/verify/v3-04-m03/results/m03.trx。完整Release0warning/0error。旧UI架构断言从“窗口必须持有coordinator”更新为“窗口仅消费projection/command、初始化属于bootstrap”，未弱化宿主/输入断言。

### 实际 WinUI smoke（最终构建）

| 场景 | PID | 实际结果 | 隔离目录（out/verify/v3-04-m03 下） |
| --- | --- | --- | --- |
| 首次未初始化 | 34984 | 1海报、1中心、2次show、1共享监听、exit0/resources0 | smoke-fresh-final |
| 既有3张恢复/再次启动 | 40316 | 3个独立ID/visual/input、同Shell/display generation逐实例读回、1中心、2次show、1共享监听、exit0/resources0 | smoke-three |
| 已初始化0张 | 8876 | 0海报、1中心、2次show、1共享监听和恢复提交、exit0/resources0 | smoke-zero |
| 等待共享恢复时退出（500ms） | 36364 | 1次recovery_cancelled、0海报、exit0/resources0；无迟到建窗 | smoke-cancel |

各目录保留 logs/desktopup.jsonl 和 state/resource-ledger.json。三张配置由tests/DesktopUp.SessionFixture通过正式coordinator生成，A常规/B锁定/C收起；最终读回框分别为300×480、300×480、300×60且位置不同。锁定的人工手感、文案与重叠输入不由这些尺寸记录证明。

首次 smoke PID5928：窗口与退出正常，但恢复日志包含IntPtr导致Json序列化异常，因此判FAIL（smoke-fresh日志保留）。改用十六进制文本并补真实JsonLineLog夹具后通过。中间成功PID28944/24484/16456仅为迭代记录；上表才对应最终构建。

实际测试进程application.started记录isElevated=true、manifest仍asInvoker；不能作为普通用户权限认证。没有主动重启Explorer、改变分辨率/DPI或操作用户桌面文件；未执行自动像素截图/交互观察，不声称肉眼无白框/闪烁已经验证。

最终SHA256：

- DesktopUp.dll：185A3E700BE499EC0B1A5E5B1E2E1754D214B603C52D3010A201B5FBA4FD4C4C
- DesktopUp.exe：4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8
- DesktopUp.Application.dll：F92D6D19F5CCD0D279F5B487717371C7A788F920DEA0B3258AFD6670A0E8442D

剩余人工门：三张独立状态与一中心一托盘；基本点击/刷新/Win+D/普通程序覆盖；按用户许可手工Explorer重启后全部恢复、退出重启不重复。启动命令与明确后续边界见 [M03合同](M03_SESSION_RECOVERY.md)。通过后进入M04，不把M03标成完整多海报功能已交付。

收尾检查：git diff --check 退出码0；smoke脚本语法错误0；39份Markdown的本地链接失效0；DesktopUp运行进程0。未删除测试证据、未操作日常用户配置、未commit/push。

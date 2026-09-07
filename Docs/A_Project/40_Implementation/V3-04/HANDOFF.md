# V3-04 当前交接

日期：2026-09-07
当前任务：M04-01 AUTO PASS / MANUAL PENDING；176/176、Release0警告0错误、最终3/0实际开窗与退出通过。下一开发项M04-02；空间功能尚未接入。

## M04-01最新交接（优先于下方历史记录）

已读DeskBox吸附、组移动和展开计算/测试，新增 [来源吸收记录](DESKBOX_SPATIAL_REFERENCE.md)，明确复用点/不照搬分支/commit和指纹。没有复制参考代码或启动DeskBox。已实现中心列表、进程内SelectedPosterId、冻结目标/版本的锁定/收展/显隐命令和运行状态；现有Host/schema/M03会话不重写。见 [M04-01合同](M04_CONTROL_CENTER_SELECTION.md)。

176/176；最终out/verify/v3-04-m04-01/app/ Release0warning0error。最终smoke三张PID20360/零张39740：中心目录ID完整、唯一窗口/监听、exit0/resources0。首次26340为列表焦点优化前证据。环境elevated=true。computer-use两次初始化因Windows沙箱1385失败，实际GUI点击未执行，短人工仍待确认。

已准备out/manual/v3-04-m04-01三张隔离配置；命令及5项短门见合同§4。当前无测试实例常驻。下一开发项M04-02纯空间合同/夹具，继而碰撞、保尺寸展开、推动与条件归位；不把本版拿来验收尚未接入的空间功能。未commit/push，保留此前dirty改动。

## 上轮需求补充与设计工作卡（历史）

用户反馈多海报可正常交互，但中心只有A；新增不可重叠、收展推让、保尺寸三种展开。两项明确裁决：已锁定者不能被推、作为固定障碍；未被再次调整者条件归位。已修订画像/视觉/运行需求、架构合同/模块、总计划和细化工作流，详见 [空间设计](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md)。本次只改文档，没有改代码/SQLite/桌面文件，也未commit/push。

中心原因：StateProjectionCoordinator兼容别名优先默认/首张，ControlCenterWindow只绑定projection.Poster；集合和会话并未丢B/C。下一开发项M04-01先建最小目录/稳定SelectedPosterId，原04-04复用选择器；随后M04-02..07完成空间合同、碰撞、保尺寸展开、推让/归位、输入/确认和集中回归。不要为补目录重新写M03 registry。

用户没有逐项报告Explorer/Win+D等，保留该人工证据缺口至M04-07；不声称M03完整PASS。Space实现前须给无解/锁定/满屏/历史重叠/二次修改/目录幻影和多窗口物理失败建立夹具。旧固定顶边/缩小适配是待替代代码，不因本轮文档更新而已修复。

## M03 实现交接（历史证据，当前动作以上方工作卡为准）

用户同意推进 M03。本轮已建立 Application/PosterSessionRegistry、IPosterSession/Factory、按ID/epoch隔离的ledger/log和WinUiPosterSession；App从单 `_poster` 切换为共享环境调度＋0..N会话。独立隐藏顶级监听锚点在零张时仍工作，单张原有防护保留。删除/隐藏取消生命周期，失败仅隔离目标，重建用新epoch；退出等待共享恢复取消完成。初始化只在bootstrap进行，既有空目录不补造默认。

证据：163/163；out/verify/v3-04-m03/app/ 完整Release 0 warning/error。最终PID：40316（三张）、8876（零张）、34984（首次一张）、36364（恢复等待中退出），均exit0/resources0；前三者1中心、2次show复用、1共享监听。测试环境为elevated，不冒充普通用户或完整多屏认证。最初发现的IntPtr日志序列化错误已修复并增加测试，失败日志保留。

立即动作：按 [M03合同 §4](M03_SESSION_RECOVERY.md#4-短人工门与启动命令) 的三张隔离配置执行短人工门；之后 M04。不重复全量安装/DPI；未主动重启Explorer；没有把自动窗口读回当人工视觉PASS。M05还负责去除固定NIGHT OPS文案，M06完善跨实例端到端夹具，原04-04才建立完整管理/预览入口。

此轮未 commit/push；保留前轮dirty文件。实际用户数据库/桌面文件未改；隔离配置与日志留在out/verify/v3-04-m03。新增/删除按钮尚未提供；不要通过日常库注入配置代替UI合同。

## M02 历史交接（当前以顶部 M03 工作卡为准）

生产 StateProjectionCoordinator 已改为 IStateCommandRepository 同事务快照/实体读集 CAS，新增 StateCommandPolicy、ScopedStateContracts、SqliteStateCommandRepository。SQLite 追加 v4：Settings 实体版本与持久命令回执；v1..3 指纹未变。三端 PostersById/有序目录/changed IDs/托盘计数与锁定汇总已实现；托盘原“锁定全部”接入批量事务。实际合同见 [TARGETED_COMMAND_PROJECTION_V2](TARGETED_COMMAND_PROJECTION_V2.md)。

App/海报现有操作传目标 EntityVersion，窗口按自身 ID 应用投影；中心及托盘支持空目录，不强制造默认。删除影响预览绑定 ID/实体/目录版本，取消不提交，删除不动文件。新增/删除 UI、实际多会话和动态显隐恢复尚未实现。

产物：out/verify/v3-04-m02/app/；测试结果：out/verify/v3-04-m02/results/m02.trx。最初旧版 PID 8600 占用单实例，未擅自终止；随后旧版退出，隔离新版 smoke 已通过（PID 40212，1 中心/1 海报、2次显示、退出资源0）。smoke 脚本已改为必须指定隔离 DataRoot，普通启动路径不变。

立即动作：用同一测试产物确认中心/托盘锁定、收起顶部锚点、拖缩后投影更新、中心关闭重开、点击/刷新/普通窗口覆盖/Win+D/正常退出。不要重新要求全量 DPI/安装矩阵。下面是已通过的 smoke 复现命令（PowerShell 7.4+）：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\smoke-control-center.ps1' `
  -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\app\DesktopUp.exe' `
  -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\smoke-data'
```

M03 工作准备：建立 PosterSessionRegistry，以 PosterId 消费集合投影、创建/停止会话；去除 App._poster 的单实例持有方式，共享 Shell/显示发现及恢复节流。按配置恢复0..N，首启只一张；每实例账本、删除/隐藏/局部失败与恢复取消。沿用 DesktopLayerAttachment 和已通过的拖缩，不借迁移重写算法。M03 不在本轮提前实施。

旧 GetOrCreateDefault、M01 global-CAS 仓储和单 Poster projection 合同保留为兼容/历史证据；生产采用新端口，不再用失败回退拼接混合快照。新 command 回执与 legacy operation ID 相互防冲突。未 commit/push，保留先前 dirty 修改。

## 已验证与未完成（截至 M02 的历史状态）

用户确认 V3-04-03 R1 人工通过；托盘开窗资源名修复、97/97 合同测试与真实开窗 smoke 为既有证据。V3-01/02/03 单海报宿主和交互保留。当前代码尚未支持多个运行海报、真实内容收纳、生产主题或热度/AI。

M01 数据/仓储门已完成：schema v3、目录 list/create/clone/remove、独立身份/显隐/层序、实体版本与 profile 初始化标志；117/117 合同测试、完整 WinUI Release 构建 0 warning / 0 error。没有迁移用户真实数据库、操作桌面文件或新开实际窗口。保留此前未提交的托盘修复，不自动提交或推送。图目录 V3_CURRENT 仍是旧单海报快照。

## 新权威链

1. [需求索引](../../../00_Catalog/REQUIREMENTS_INDEX.md)：七份需求已原位修订。
2. [通用多海报合同](../../20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)：数据、版本、实例/共享、文件归属、主题与参考来源。
3. [总计划](../../30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md) 与 [细化工作流](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)：保留原阶段，插入 M 前置，逐项出口与依赖。
4. [代码锚点](CODE_ANCHORS.md)、[验证记录](VERIFICATION.md)：真实实现与证据范围。

## M01 完成记录

输入为 schema v2、原有仓储及通用多海报合同 §3/4。实际合同见 [POSTER_CATALOG_SCHEMA_V3](POSTER_CATALOG_SCHEMA_V3.md)；验证和产物指纹见 [VERIFICATION](VERIFICATION.md)。

产出：
- catalog/list/create/clone-configuration/remove 的数据和 repository 合同；
- 每实例身份、显隐/内部层序、实体版本，以及 profileInitialized（区分首次启动与零海报）；
- 新增迁移，不修改 v1/v2 migration/hash；旧 poster-main 几何/锁定/收起完整保留；
- 真实 SQLite 测试：新增/查询/删除/克隆新身份、删至零重启、旧库迁移与备份、失败回滚/损坏保留、并发冲突/幂等。

本项不做：真实多窗口、控制中心新画布、文件移动、完整主题或热度模型。集合/主题先冻结最小关联与兼容接口，正式内容实体按 V3-05 进入。

## M02 工作卡（本轮实现前输入，完成状态以上方交接为准）

输入：M01 的 IPosterCatalogRepository、PosterStateSnapshot.EntityVersion、CatalogVersion/global revision；现有单 Poster command/query/projection。

- 输出：PostersById、目录/目标投影、changed IDs、Tray 汇总，唯一控制中心/托盘不复制。
- 用户补充的入口合同：中心/托盘共用新增；中心/海报“...”共用删除，必须准备目标名称/影响摘要并确认后重校验版本。确认 UI 在 M04、04-04 接入，托盘新增在04-05接入。
- 实现实体/必要读集 CAS；global revision 继续表示全局提交序列。M01 的目录仓储仍是 global CAS，不能假定 A/B 无关写入已无冲突。
- 批量锁定/显隐等纯状态事务整体校验；NotFound、Conflict、幂等和删除后晚到命令不能复活。
- 现有 GetOrCreateDefault 在已初始化零海报时会明确拒绝；M02/M03 必须用 catalog 初始化查询/会话，不能以异常恢复为由重新播种默认海报。
- 普通首次确无配置只生成一张默认；已有或显式导入的0..N方案按集合恢复。空初始化 API 仅供明确空方案/夹具，不是普通首启选项。
- 目录写结果的 Snapshot 是最新同事务读视图，CommittedRevision 是原提交；接投影时不得混淆。
- 验收：A/B 无关写入、同 A 过期版本、批量失败回滚、操作重放、删除晚到命令、各投影同 revision。

后续 M03 才实现多会话，M04 输入/层序，M05 参数，M06 夹具，M07 测量/人工门，再进 V3-04-04。M01 不要求重做既有 DPI/宿主人工矩阵；新多窗口能力仍未通过人工验证。

## 单文件习惯配置新增计划

需求画像 §32 / RP-PORT-01/02 已记录跨机应用；运行仍以 SQLite 为唯一权威，通过控制中心导出一致快照、导入预览确认后事务应用。文件不与数据库双写；不包含 Key、实际文件或机器句柄。

04-06-A..D 冻结格式/已有布局策略、导出、导入映射和实际跨机验证；基础内置主题参数先进入，自定义主题素材复用07-04，来源失效修复复用05。数量/位置/锁定/收起/显隐/主题/内容与排版偏好都在范围；屏幕或依赖不同需提示并安全映射/fallback。

本次只补需求/设计/计划，未实现便携格式、确认弹窗或新增入口；未运行新的功能测试，117/117 仍是 M01 既有证据。

## 必须保留的安全和产品边界

- 游戏海报是特色场景；通用内容与多海报是当前正式方向，不再退回“未来扩展”。
- 一个控制中心/一个托盘不因海报数增加而复制。
- 主题/配置独立，不等于共享源文件变化不传播，也不等于每张独立进程。
- 引用、映射、受管内容区分；删海报不删文件；journal 先于真实移动。
- 不为增加实例重写已验证的 DesktopLayerAttachment 或恢复旧 Qt/WebView 技术线。

## 前一轮仅文档校验（历史）

2026-09-07：检查 37 份 Markdown，本地链接失效 0、顶层章节编号重复 0；git diff --check（Docs/README 范围）退出码 0。现行入口/需求/架构/计划中旧“只创建一张”“多海报仍延期”“普通文件一律不能移动”及旧人工待复测状态已检查；历史审计和单海报实现证据明确保留原范围。没有运行新的功能测试，不增加多海报 PASS 声明。

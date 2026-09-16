# M08-07 R2-B：组合暂缓/恢复、空间重放与事务前置

日期：2026-09-14。状态：**HISTORICAL / SUPERSEDED DESIGN EVIDENCE**。V2-DEC-003已替代本报告的旧增长行为；本文件只保留可复查的实验、反例和安全经验，不能作为活动合同或生产接线计划。当前目标见[TI合同](M08_TITLE_INTERACTION_CONTRACT.md)。

## 1. 当时完成范围（未接App/Windows）

- `ExpansionDragSpatialModel`：复用现有扫掠/滑动、push和先下后上展开策略。标题捕获中只接受不移动抓取点的向下增长；无合法增长时暂缓，当前占位下合法移动继续。松手后从当前整组几何重算，保留偏好高度。
- `ExpansionDragModel`：补上`growth-would-move-grab-point`帧门；旧纯几何安全不能继续授权捕获中的向上增长。
- `ExpansionDragSession`：不透明提案＋呈现后接受；被替代、丢弃、重复、其他会话的提案不能前进。暂停tick不进入轨迹。总输入上限4096且受参与者工作量预算约束，预留松手/结束步骤，耗尽移动预算不锁死捕获。
- `ExpansionDragReplay`：从初始场景重放语义动作，不接收任意邻居位移作为授权；邻居Home/因果关系保留，手动只重建源锚点。最终每个受影响实体的layout generation只推进一次。
- `IExpansionDragCommitter`：独立受限手动接管口，经原`StateProjectionCoordinator`和SQLite事务。完整目录/实体读集、策略/最小高度、身份/环境、初始几何证据、终态字段均重验；位置、姿态、邻居及关系一个revision提交。没有放宽一般System入口或修改schema。

R2-A外部连续点击问题未改源码；既有归属修订随全量测试回归，但其真实鼠标门仍单列。

## 2. 本轮证据

根目录：`out/verify/v3-04-m08-07-combined`（忽略产物，不提交Git）。

| 证据 | 结果与边界 |
| --- | --- |
| `tests/spatial-before.trx` | 首轮14项中3失败：1项实证旧结构门允许抓取点被抬走；2项测试误把采样上限32px当精确增量，已校正夹具。不是3个生产根因 |
| `tests/spatial-after.trx` | 新空间14＋旧模型28＝42/42；包括暂缓/恢复、两种结束顺序、先下后上、受限高度及无解窄缝检测 |
| `tests/transactions-first.trx` | 新SQLite17/17：单revision、重复请求、持久关系、完整归位、伪造终态/邻居/身份/指标/不完整证明、实体/目录冲突、SQL注入故障回滚、无关settings revision |
| `tests/replay-first.trx` | 新重放6/6：链/分支/合流、反向增长、已被推源的接管、新障碍不部分归位、用户介入、100轮收展无漂移 |
| `tests/session.trx` | 新会话4/4初次通过；随后增强预算结束保留量，最终结果以下方完整回归为准 |
| `tests/combined-full.trx` | 最终**921/921、0 skipped**。本轮新增41项；包含10000次暂停不累积输入，4个DPI参数各1000轮往返及预算耗尽仍可释放/完成 |
| `app/` | 独立WinUI Release构建**0警告/0错误**；未启动、未播种新运行库、未替换R1指纹/人工入口 |

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --no-restore --logger 'trx;LogFileName=combined-full.trx' --results-directory out/verify/v3-04-m08-07-combined/tests
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 --no-restore -o 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m08-07-combined\app\'
```

纯模型和数据库测试不证明HWND/输入岛或GPU呈现。无新实机PASS、有效资源仍0/36；没有commit/push。

## 3. 旧设计反例：V2-TBD-019（现已被替代，不再未决）

夹具`LegalDragIsNotRejectedForFutureExpandedFootprintAndReleaseExposesImpossibleMinimum`：

- 工作区1200×1000；源240×60，从`(0,400)`增长到100高。
- 固定障碍：`(400,0,240,400)`与`(400,500,240,500)`，中间可用高度100。
- 拖动源到`(400,400,240,100)`扫掠合法，保持跟手。继续增长为零，锁定邻居不可推动。
- 松手后，夹具要求的最小内容高度220仍无法容纳；220是此夹具指标，不是所有主题的统一最小值。

不能同时满足保持落点、不回退当前高度、不提交低于最小高度的展开态、锁定/不重叠和有限结束。当前模型明确给出`release-below-minimum`/`BlockedAfterRelease`，无新帧、无提交；**检测无解不是完成了安全终态交互**，禁止把它接成无限busy运行器。

当时提出“松手不足时特例收起并提示”，并未获得该提议的直接确认。随后用户改选五意图仲裁，V2-DEC-003规定未完全展开时确认拖动即采用收起态，因此TBD-019按设计替代关闭，不再阻塞等待产品裁决。不能据此免验新模型收拢途中实际占位与释放安全。

## 4. 清理结论与可复用经验

旧CD01～18矩阵、增长暂缓/恢复接线顺序和“先解决TBD-019再接生产”的步骤撤销。源文件中未接线的ExpansionDrag实验已由[TI-01](M08_TI01_TITLE_MODEL.md)分类退役；报告所述类/测试是当时证据，不表示仍存在于活动源码。删除前当前工作副本快照保存在TI-01证据根。

可复用的是安全要求：当前真实呈现几何/身份的受控证据、有界语义重放、源与邻居因果、完整读集CAS、统一组帧呈现和结束恢复。算法或测试能否直接保留，须以新TI语义重新证明，不能只因921项曾通过而保留相反断言。

更早28项模型证据保留于`out/verify/v3-04-m08-07-r2/tests/combination-model.trx`；全880项为同批R2-A与旧组合模型证据。结构安全不等于移动授权的反例仍有价值；本报告上方41项及全921项属于后续旧设计实验。新TI矩阵全部待验；新规则没有人工包或资源PASS。

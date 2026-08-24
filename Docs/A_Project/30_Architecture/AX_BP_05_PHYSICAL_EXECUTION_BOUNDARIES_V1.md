# AX-BP-05：DesktopUp V2 物理执行边界冻结

状态：FROZEN — AX-BP-06 PASS

完成日期：2026-08-24

## 1. 文档职责

本文接收 AX-BP-03 的模块端口与 AX-BP-04 的因果流，冻结纯定义平面与物理执行平面之间的边界要求。

本文只冻结：

- 哪些关系必须跨物理边界；
- 边界的请求/事件、顺序、可靠性、取消和迟到语义；
- 资源所有权；
- 可能的线程/进程亲和约束；
- 安全和故障隔离要求；
- 性能与诊断测量点；
- 后续由哪个技术选型域决定。

本文不选择或实现 runtime、线程库、队列、IPC、数据库、UI、renderer、DesktopHost 或安装器。

axiom-runtime 明确排除。

## 2. 定义平面与物理平面

~~~text
Axiom Core definition plane
  PortCell / Wire / Chain / Diamond / Slot / Blueprint
  pure values and relations
                 |
                 | PhysicalExecutionBoundary
                 v
Future physical plane
  OS / window / renderer / store / file / process / network / installer
~~~

边界上方不能保存物理 handle；边界下方不能修改业务权威。

## 3. 纯定义子图

以下子图必须能够脱离 OS 和 I/O 执行：

| Pure graph | 输入 | 输出 |
| --- | --- | --- |
| InputNormalization | typed external event value | RuntimeInput |
| CommandValidation | CommandEnvelope + policy | accepted/rejected |
| MutationPlanning | snapshot + command | MutationPlan |
| SnapshotProjection | accepted snapshot | all logical projections |
| ItemNormalization | candidate values | normalized Item |
| ActivityRanking | Item snapshot + policy | ranked items |
| LayoutPlanning | ranked items + constraints | logical layout |
| ThemeValidation | package metadata/value | validation/migration plan |
| ChangePlanValidation | AI untrusted value | validated plan/reject |
| LifecycleDecision | logical state + event | transition/effect intents |
| DiagnosticNormalization | event value | redacted/classified value |

这些子图不得依赖 wall clock 的隐式读取；时间必须作为显式输入值。

## 4. 边界总表

| PEB | 边界 | 物理能力 | 后续选型域 |
| --- | --- | --- | --- |
| PEB-01 | Surface/System → RuntimeIngress | UI/OS event acquisition | UI/OS integration |
| PEB-02 | Core ↔ StateStore | persistent I/O, transaction, CAS | State storage |
| PEB-03 | Core ↔ DesktopHost | HWND/desktop host/z-order/hit | Windows host |
| PEB-04 | Core ↔ Renderer | surface/frame/device resources | Renderer |
| PEB-05 | Gesture live path | pointer acquisition and live geometry | UI/host integration |
| PEB-06 | Discovery/FileSafety | filesystem/Shell read/move/restore | File integration |
| PEB-07 | Item launch | process/protocol/Shell activation | Shell integration |
| PEB-08 | Theme package | archive/file/font/image/style processing | Theme platform |
| PEB-09 | AI provider | credential/network/provider SDK | AI integration |
| PEB-10 | Tray/System | notification area and system events | Shell integration |
| PEB-11 | Diagnostics sink | log/evidence storage/export | Diagnostics |
| PEB-12 | Distribution | install/autostart/update/uninstall | Packaging |
| PEB-13 | Single-instance forwarding | process identity/message forwarding | Process/IPC |
| PEB-14 | Time source | monotonic/wall-clock sampling | Platform core |
| PEB-15 | Power/session | suspend/resume/session ending | OS integration |

## 5. PEB-01 Surface/System 输入

请求/事件：

- PosterCommand；
- ControlCommand；
- TrayCommand；
- pointer/keyboard gesture；
- Shell/display/DPI/power/session event。

必须：

- 每个 mutation command 有 RequestId/source/expected Revision；
- 事件来源可诊断；
- 生命周期终态不静默丢失；
- 输入无框架对象后再进入 RuntimeIngress；
- 整窗透明空白不产生输入事件。

开放：

- UI thread 模型；
- 原生消息、框架事件或其他 acquisition；
- 进程内/跨进程。

## 6. PEB-02 StateStore

请求：

- load/migrate/recover；
- commit expected revision；
- ledger；
- flush。

事件：

- Loaded/NoData/Corrupt/UnknownVersion/IoFailure；
- CommitAccepted/Rejected；
- Ledger/Flush result。

物理语义：

- transaction 或等价原子替换；
- CAS；
- crash-safe failure preservation；
- migration idempotence；
- original legacy data retention；
- one authoritative writer contract；
- result 必须回到 Core 序列；
- 每个 request 有终态。

资源所有者：

- StateStore adapter 独占连接、文件锁、事务和 schema。

开放：

- SQLite 或其他存储技术；
- 进程内/独立进程；
- 序列化和文件位置。

## 7. PEB-03 DesktopHost

物理能力：

- Windows desktop topology discovery；
- parent/owner/z-order；
- physical geometry；
- hit regions；
- attach/recover/detach；
- Shell/display generation；
- HWND 和关联资源。

必须：

- 普通/系统/全屏程序 > Poster > desktop icons > wallpaper；
- Win+D 连续可见，不 hide/restore 补救；
- no temporary TopMost；
- surface epoch；
- idempotent/single-flight lifecycle；
- bounded recovery；
- atomic projection of geometry/hit/render identity；
- first-frame publish gate；
- complete owned-resource ledger；
- events 回到 Core。

资源所有者：

- DesktopHost adapter 独占宿主窗口、关系、注册回调和 hit map physical projection。

开放：

- 具体 Win32 路径；
- 同/异进程；
- 所需线程亲和；
- 具体物理窗口数量。

## 8. PEB-04 Renderer

物理能力：

- visual resources；
- surface/device；
- frame production；
- theme assets；
- ready/fault/device loss/release。

必须：

- frame 携带 Revision + SurfaceEpoch；
- 不拥有 z-order、hit 或业务状态；
- first frame 前不发布白底/旧帧；
- release 有终态；
- resource budget 可测；
- capability fallback 显式；
- 像素/visual 不经过通用业务消息；
- device loss 不污染 accepted state。

资源所有者：

- Renderer adapter 独占绘制资源、device context 和 frame lifecycle。

开放：

- Qt/WinUI/WPF/Avalonia/WebView2/Slint/native/GPU API；
- 与 Host surface 的绑定技术；
- 渲染线程模型。

## 9. PEB-05 Gesture

事件：

- Begin(SessionId, base Revision)；
- Update(sequence, live value)；
- End(final value)；
- Cancel(reason)。

必须：

- Begin/End/Cancel 有序；
- 一个 Session 仅一个终态；
- Update 可合并但不可越过终态；
- locked 时不启动 geometry session；
- live preview 与正式 commit 分离；
- session 失效可诊断；
- End 最终进入 normal command/CAS。

开放：

- pointer API；
- live rendering technique；
- frame cadence。

## 10. PEB-06 Discovery 与 FileSafety

物理能力：

- Shell/文件只读枚举；
- metadata/icon read；
- backup；
- .lnk/.url move/restore；
- recovery ledger physical persistence。

必须：

- 默认只读；
- 可取消；
- 200 候选不阻塞 Poster interaction；
- path normalization 与 symlink/junction safety；
- 普通文件禁止 move；
- ledger + verifiable backup + confirmation before move；
- no overwrite；
- failure retention；
- restore conflict preserved。

资源所有者：

- File integration adapter 拥有 enumeration handles 和 file operation handles；
- 正式 ledger 语义受 StateStore/FileSafety 合同约束。

## 11. PEB-07 Item 启动

物理能力：

- executable/Shell/protocol activation；
- optional process observation。

必须：

- exactly-once gesture interpretation；
- structured LaunchResult；
- cancellation where meaningful；
- unknown/failure 不更新 usage；
- success gate 不由 adapter 私自定义；
- untrusted target validation；
- no shell command concatenation。

V2-TBD-011 未冻结前，adapter 只报告事实，不报告产品意义上的 UsageSuccess。

## 12. PEB-08 Theme package

物理能力：

- archive inspection/extraction；
- path/resource validation；
- image/SVG/font/style decode/compile；
- atomic install/uninstall；
- backup/rollback。

必须：

- pre-extract limits；
- traversal/symlink/junction/zip-bomb defense；
- file count/size/type budget；
- no native code；
- no unrestricted script；
- no generic file/network/process/registry/IPC；
- temporary staging；
- atomic publish；
- selected theme failure 不写回 fallback。

资源所有者：

- Theme package adapter 拥有 staging、decode/compile resources 和 installed package inventory。

## 13. PEB-09 AI Provider

物理能力：

- credential access；
- network；
- provider request/stream/cancel；
- rate/timeout handling。

必须：

- BYOK；
- credentials 不进入 Core/log/theme package；
- minimal redacted context；
- provider output untrusted；
- structured final result/error；
- cancellation on provider switch/disable/exit；
- no direct tool execution；
- core works without provider。

资源所有者：

- provider adapter owns credential handle, connection and request lifecycle。

开放：

- provider SDK/HTTP；
- local/remote provider；
- streaming policy；
- R0/R1/R2 confirmation policy。

## 14. PEB-10 Tray/System

物理能力：

- notification area icon/menu；
- single/double click；
- Explorer restart recovery；
- OS notifications；
- display/power/session event acquisition。

必须：

- one tray instance；
- single click no action；
- double click opens ControlCenter；
- menu state from TrayProjection；
- Explorer recovery idempotent；
- no direct Host/Store mutation。

## 15. PEB-11 Diagnostics

物理能力：

- log/evidence file；
- optional ETW/event tracing；
- export bundle；
- health sampling。

必须：

- redaction before boundary；
- bounded storage；
- stable schema；
- error/cleanup terminal events retained；
- performance sampling policy explicit；
- evidence includes environment/version/scenario；
- diagnostics cannot modify state。

开放：

- log library/format；
- sink process；
- retention duration。

## 16. PEB-12 Distribution

物理能力：

- installer package；
- program files；
- autostart registration；
- upgrade transaction；
- uninstall cleanup。

必须：

- canonical install paths/names；
- explicit autostart opt-in；
- preserve/migrate valid state；
- owned-resource inventory；
- no unknown file/registry deletion；
- user data removal explicit；
- failure recoverable/diagnosable；
- resources and dependency packaging verified。

具体 installer、自更新、签名和发布技术未选。

## 17. PEB-13 Single instance

物理能力：

- process identity；
- second-instance intent forwarding；
- primary liveness detection。

必须：

- exactly one primary Core/Tray/Poster set；
- second ordinary start forwards OpenControlCenter；
- autostart second start does not open ControlCenter；
- secondary exits；
- stale primary identity has bounded recovery；
- forwarded intent is typed and validated。

开放：

- mutex/socket/named pipe/window message/other IPC；
- process topology。

## 18. PEB-14 Time source

物理能力：

- monotonic duration；
- wall-clock calendar time。

必须：

- pure graphs receive time explicitly；
- ordering/deadline uses monotonic source；
- user-facing timestamps identify wall-clock/zone；
- clock rollback does not corrupt ranking or expiry；
- test can inject deterministic time。

## 19. PEB-15 Power/session

事件：

- suspend/resume；
- session ending；
- logoff/shutdown；
- lock/unlock if relevant。

必须：

- normalize once；
- preserve accepted state；
- stop/cancel external jobs according to boundary；
- resume uses epoch/recovery rules；
- session ending respects sticky exit；
- no UI focus side effect.

## 20. 逻辑执行域

这些是约束域，不是线程决定：

| Domain | 串行/并发要求 | 可阻塞 |
| --- | --- | --- |
| CoreDecisionDomain | 单一有序决策序列 | 否 |
| StateStoreDomain | transactions serialized per authority | 不阻塞 Core |
| DesktopHostDomain | obey chosen platform affinity | 不阻塞 Core |
| RendererDomain | obey chosen surface/device affinity | 不阻塞 Core |
| SurfaceUiDomain | obey chosen UI affinity | 不执行 store/file/network |
| JobDomain | scan/theme/AI cancellable | 不阻塞 Core/UI |
| DistributionDomain | external lifecycle, exclusive where needed | 不与运行资源无序竞争 |
| DiagnosticsDomain | bounded async observation | 不阻塞 critical path |

一个线程可以承载多个逻辑域，一个逻辑域也可由多个物理执行单元支撑；后续架构必须证明保持关系。

## 21. 进程拓扑约束

AX-BP 不选择单进程或多进程，只冻结：

- 业务 authority 只能有一个；
- ControlCenter 可销毁而 Core/Poster 不退出；
- Renderer 或 UI fault 的隔离级别必须有证据；
- 跨进程时 RequestId/Revision/Epoch 不丢；
- credentials 不跨越不需要的进程；
- pixels 不经通用 IPC；
- shutdown 有资源 ownership 和 acknowledgement；
- second-instance forwarding 与内部 IPC 不是同一语义；
- process crash 不把 draft 提升为正式状态。

## 22. 线程约束

- Core reducer 不并发写；
- UI/Host/Renderer affinity 由候选声明；
- 回调先值化、再进入 RuntimeIngress；
- 不在 OS callback 内执行阻塞 store/network/file job；
- no unbounded polling；
- no thread-per-event assumption；
- cancellation and deadline are explicit；
- thread shutdown result observable；
- AX-BP 不提供 executor。

## 23. IPC 约束

若后续选择多进程：

- schema/version；
- authentication/peer identity；
- bounded message size；
- backpressure/failure semantics；
- request/result correlation；
- restart epoch；
- capability negotiation；
- no raw pointers/handles without explicit safe transfer；
- unknown required message fail closed；
- redaction；
- no business write bypass。

IPC 技术仍未选。

## 24. 资源所有权台账

| Resource family | Logical owner |
| --- | --- |
| accepted persistent data | StateStore adapter |
| migration originals | StateStore migration boundary |
| recovery ledger/backup | FileSafety + StateStore/File boundary |
| host windows/parent relations/hit maps | DesktopHost adapter |
| render device/surface/frame resources | Renderer adapter |
| ControlCenter window/preview | ControlCenter adapter |
| tray icon/menu callbacks | Shell/Tray adapter |
| file enumeration/move handles | File adapter |
| theme staging/installed inventory | Theme package adapter |
| credentials/provider requests | AI provider adapter |
| diagnostics sink/export files | Diagnostics adapter |
| install/autostart/upgrade inventory | Distribution adapter |

Core owns none of these resources.

## 25. 可靠性和终态

| Operation | Required terminal events |
| --- | --- |
| State load | Loaded / NoData / Failed |
| State commit | Accepted / Rejected |
| Host attach | Attached / Failed / Cancelled |
| Projection commit | Committed / Failed / Superseded |
| Render request | FrameReady / Fault / Superseded |
| Gesture | End / Cancel |
| Discovery job | Completed / Failed / Cancelled |
| File move/restore | Succeeded / Failed |
| Launch | Accepted fact / Failed / Unknown fact |
| Theme install | Installed / Failed / RolledBack |
| AI request | Completed / Failed / Cancelled |
| Detach/release | Released / Failed |
| Exit | resource ledger result |
| Distribution | Succeeded / Failed / RolledBack |

Superseded 只能用于文档允许替换的非终态投影，不能用于逃避 commit/cleanup 结案。

## 26. 取消与 deadline

- cancel 是显式 intent；
- cancel result 可观察；
- cancellation 不等于 rollback；
- store commit 是否可取消由后续实现契约决定，但结果必须结案；
- exit 可 fence 迟到结果；
- deadline expiry 产生 stable error；
- automatic retry 有上限、退避和诊断；
- user mutation 不静默自动重试到覆盖新 revision；
- long job 使用 JobEpoch；
- recovery 使用 SurfaceEpoch/RequestId。

## 27. 性能测量点

| Measure ID | 区间 |
| --- | --- |
| PM-01 | input acquisition → RuntimeIngress |
| PM-02 | RuntimeIngress → Core decision |
| PM-03 | accepted snapshot → logical projections |
| PM-04 | projection → Host committed |
| PM-05 | projection → FrameReady |
| PM-06 | matching Host+Frame → visible publish |
| PM-07 | gesture event → live visual response |
| PM-08 | command → Store result |
| PM-09 | ControlCenter cold open/close resource release |
| PM-10 | discovery 200 candidates + cancel |
| PM-11 | theme import/validation/install |
| PM-12 | AI request/cancel |
| PM-13 | Shell loss → restored publish |
| PM-14 | exit → zero owned resources |
| PM-15 | install/upgrade/uninstall |

具体预算沿冻结需求；AX-BP 只冻结测量位置。

## 28. 安全信任区

| Zone | Trust |
| --- | --- |
| Core pure values | trusted after validation |
| StateStore loaded data | validate version/integrity before accept |
| UI command | untrusted until validation |
| DesktopHost/Renderer observation | technical fact, epoch checked |
| file candidate/package | untrusted |
| AI provider output | untrusted |
| theme resources | untrusted until validation/compile |
| diagnostics input | redact before external sink |
| installer package | verify integrity/source according to release policy |

任何低信任输入必须经过 ValidationPolicy，不能因来自本机就默认可信。

## 29. 故障隔离

| Fault | Must retain | Must not do |
| --- | --- | --- |
| StateStore fault | original/last valid state | empty default overwrite |
| DesktopHost fault | accepted snapshot | TopMost fallback |
| Renderer fault | state/host contract | z-order mutation |
| ControlCenter fault | Poster/Core/Tray | exit application |
| Tray fault | Core/Poster | duplicate primary |
| File fault | source/backup/ledger | delete/overwrite |
| Theme fault | selected identity/package evidence | fallback write-back |
| AI fault | manual product | block UI/Core |
| Diagnostics fault | business operation | become authority |
| Distribution fault | installed/data recoverability | broad cleanup |

## 30. Shutdown 物理边界

~~~text
BeginExit
  -> reject new mutation
  -> cancel/fence Job boundaries
  -> request Store flush
  -> reconcile critical ledgers
  -> close ControlCenter
  -> release Renderer
  -> detach DesktopHost
  -> remove Tray/System callbacks
  -> close Diagnostics sink
  -> verify ResourceOwnershipLedger
  -> exit
~~~

具体并行/串行实现未选；依赖顺序和终态要求冻结。

## 31. 后续选型交接矩阵

| AX-BP boundary | 后续域 |
| --- | --- |
| PEB-01/05 Surface/Gesture | renderer/UI candidate adapters |
| PEB-02 Store | TS-03 storage |
| PEB-03 Host | frozen DesktopHost + candidate adapter |
| PEB-04 Renderer | TS-C01～C07 |
| PEB-06/07 File/Launch | Shell/file architecture |
| PEB-08 Theme | theme format/runtime selection |
| PEB-09 AI | provider/security architecture |
| PEB-10/13/15 Shell/process/system | process/IPC/Windows integration |
| PEB-11 Diagnostics | observability selection |
| PEB-12 Distribution | packaging/release selection |
| PEB-14 Time | platform core selection |

每个选型只能占据边界，不能改写边界上方的业务语义。

## 32. Axiom Core 停止点

Axiom Core 用于：

- 把边界两侧写成 typed logical ports；
- 描述纯子图和组合；
- 验证部分类型接线；
- 保留 Blueprint。

Axiom Core 不用于：

- 选择/实现任何 PEB；
- 调度；
- 队列；
- 背压执行；
- 线程；
- IPC；
- lifetime management；
- resource ownership implementation。

axiom-runtime 不进入本项目。

## 33. AX-BP-05 退出门

| 检查 | 结果 |
| --- | --- |
| 15 个 PEB 完整 | PASS |
| 每个边界请求/事件语义明确 | PASS |
| 资源所有者唯一 | PASS |
| 逻辑执行域不伪装线程决定 | PASS |
| 单/多进程保持开放 | PASS |
| IPC 条件式约束完整 | PASS |
| 可靠性/终态/取消/deadline 完整 | PASS |
| 性能测量点完整 | PASS |
| 信任与故障隔离完整 | PASS |
| axiom-runtime 明确排除 | PASS |
| 未选择或实现物理技术 | PASS |

## 34. 交接 AX-BP-06

AX-BP-06 必须跨 AX-BP-01～05、七份冻结需求、DesktopHost 合同和技术选型文档检查重复、矛盾、遗漏、未决项丢失、实现假设和 runtime 越界，并形成冻结清单与后续输入。

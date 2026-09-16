# M08-07-TI-02：空间／呈现提案与事务纯验证

日期：2026-09-14。状态：**PASS（纯模型、隔离 SQLite、构建范围）**。
产品规则仍以[V2-DEC-003 对应标题合同](M08_TITLE_INTERACTION_CONTRACT.md)为准；顺序以[M08计划 §4.1](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#41-标题交互修订的最近顺序)为准。本步不接生产鼠标、不替换人工包。

## 1. 交付与权威边界

| 单元 | 本步职责 |
| --- | --- |
| Application/TitleSpatialKernel.cs | 从已接受的原收展计划前缀恢复按下画面；组合移动／收拢，复用碰撞、连续归位及动画细分策略 |
| Application/TitleSpatialSession.cs | 不透明提案、完整呈现成功后接受、旧/异会话/失败提案拒绝；有限轨迹、结束预留及运行代次核验 |
| Application/TitleSpatialCommit.cs | 重放轨迹，核对实际接受终态及因果见证，推导合法写集；不接受调用方任意邻居位移 |
| StateProjectionCoordinator.Execute(TitleSpatialCommand) | 复用原 Run → SQLite repository → CAS/receipt/单 revision/三端 projection；只允许 Poster 来源，不放开 System 任意写位置 |

没有修改 Domain 原空间求解器、App/Windows 输入、R4 宿主、Infrastructure schema/仓储、脚本或启动配置。没有恢复旧 ExpansionDrag 增长默认，也没有逐帧保存或第二份状态权威。

## 2. 空间及呈现提案

### 起点、抓取点与占位

- 起点带完整初始物理场景、标题 capture/session/操作身份、按下阶段、动画参数，以及可选的原收展 operation ID＋**已成功接受**的计划帧数。不能把尝试提交次数、定时 tick 或任意邻居矩形当成已呈现证据。
- 原收展前缀由 SpatialContinuitySolver＋SpatialAnimationSchedule 重新产生。按下矩形/阶段必须与该前缀一致；不能先回滚到 committed 位置再接拖动。
- 位置按绝对鼠标坐标减按下抓取偏移计算，不累加受限位移。向上展开时使用当前被抬高的标题，不恢复旧 compact Y。
- 移动先按**仍可见的完整高度**调用 SpatialCollisionSolver，不能把未来收起后的区域提前当空白。随后求完整合法收拢/归位，再取本次呈现步；同次采样的移动和收拢在联合扫掠安全时合为一个组帧。
- 拐角需要折线路径时保留已验证的安全分段，不用直线切角。工作区或碰撞造成的抓取点误差是显式约束；空间释放后按同一绝对抓取点继续跟手。

### 关系与归位

- 源进入手动拖动后只重设源 Home/UpInset；邻居的原 Home、cohort、锁定及用户修改证据不被清空。
- 每次先求**完整最终归位目标**，再采样；不能把某帧可用的半段空间重新命名为完整归位。新障碍阻断完整路径时保持原地，不推动无关海报。
- 同源部分展开生成的合流关系在接管时保留；部分收起保留尚未完成的 cohort。呈现期间只重绑 Expected 物理矩形，不每帧增加持久 layout generation。
- CompactDrag 松手结束剩余收拢、保持新位置和收起态，不恢复自动增长。退出计数抑制仍由 TI-01 提供，空 Rearm 不是清除旧抑制的命令。
- ExpandedDrag 保持已接受的有效高度，包括受限高度；偏好 ExpandedHeightDip 不被临时可见高度覆盖。

### 快展与接受协议

- 初始收起点击：仍由同一 SpatialContinuitySolver/SpatialExpansionPolicy 裁决全高、先下后上、受限与不足。
- 展开中点击：完成原合法计划的剩余路径，不重新选择另一个展开终点。
- 收起中点击：沿原**已接受前缀反向**回到原展开组，保留原上展锚点。没有拖动时不可把半收起矩形当成新的 compact 起点。
- 快展提案标记 ImmediateGroup；Frames 是扫掠证明，生产适配器应一次准备并提交完整组终态，不能按这些证明帧把剩余动画再播放一遍。
- 普通移动/收拢提案提交协调帧；整个提案成功才 Accept。旧提案、重复接受、其他会话的提案不能改变状态；部分准备/提交失败不得接受模型，后续交由 TI-03 的原 R4 恢复。
- Pending 无时钟动作；无几何/阶段变化的采样不积压语义历史。最多256项运动记录（按参与者数再收紧工作预算），另预留一次释放/取消；预算满后拒绝新运动采样，但仍可安全结束。本纯内核最多64个场景参与者，超出显式拒绝，不冒充已认证的无限规模产品能力。

## 3. 事务证明与失效

1. 完整 catalog version＋全部实体版本读集，包含未移动、隐藏、其他工作区的海报；SQLite 事务内部再次 CAS。
2. 当前工作区的可见占位集合不能遗漏；初始姿态、锁定、代次、偏好高度和几何均核对。几何只接受正常锚点投影或同环境/同代 continuity 的有效布局见证。
3. 从事务当前因果状态及当前内容指标重放有界语义轨迹，不信任外部写集。
4. 重放结果必须与冻结的**实际接受终态和完整因果见证**一致，防止合法但不同的重放结果被误写入。
5. 一次推导源和合法邻居更新、因果关系、必要 layout touch；一次 committed revision、一次投影发布。往返终点相同但确有用户移动时仍触碰源布局代次，不能抹去用户介入。
6. 重复同请求走原 receipt；SQL 失败回滚实体、continuity、head 与 receipt。无关 settings-only revision 不制造空间假冲突；已删除读实体沿用仓储 NotFound，其余过期读集返回 Conflict。

SQLite 不知道 HWND 生命周期：TitleSpatialSession.ValidateCurrent 必须在生产的 prepare/accept/commit 前由同一 dispatcher 用当前 Shell/display/workspace/DPI/runtime-session/capture token 调用。不得用数据库 CAS 冒充这道运行门。

## 4. 验证矩阵与结果

新增 **82项**：TitleSpatialTests 31、TitleSpatialSequenceTests 26、TitleSpatialTransactionTests 25。最终全库 **1043/1043，0 skipped**（TI-01基线961＋82）；独立 WinUI Release **0警告/0错误**。

| 类别 | 实际覆盖 |
| --- | --- |
| 占位与抓取 | 顶/中/底×展开早中晚；收起早中晚；受限终态拖动；100px锁定窄缝先挡住实际高度、收拢后进入、松手60px合法结束、再次展开仍按最小高度拒绝 |
| 展开一致性 | 原共享计划对比初始点击和中途快展；全高/先下后上/受限/不足；快展 ImmediateGroup 标记 |
| 因果连续性 | C/B/A嵌套、分支合流、向上推动；源移动后继续收其他源；锁定/移动/往返修改邻居；新障碍不部分归位 |
| 长序列 | 30种子×100次运动，逐帧安全和双运行器确定性＋语义重放；100轮接管往返不漂移；96/120/144/192 DPI负坐标往返（仅模型证据） |
| 呈现/失效 | Pending1000次不动不收拢/不积压；旧/异会话/失败提案拒绝；8种运行证据变化、9类取消；超预算仍可释放，重复终态不再执行 |
| 事务 | 源/邻居/隐藏/其他工作区/目录/删除冲突；伪造起点、代次、阶段、前缀、终态、Home、读集、来源、超预算；单revision及三端发布；SQLite触发器失败回滚；同请求幂等、重开库和后续归位 |

证据目录：仓库 out/verify/v3-04-m08-07-ti02/。

- tests/ti02-preflight.trx：原标题109/109。
- tests/ti02-spatial-first.trx：空间31/31首次通过。
- tests/ti02-transaction-first.trx：21通过/1失败，失败为测试把已删除读实体预期成 Conflict；已对照既有仓储改为 NotFound，不改仓储行为。
- tests/ti02-up-anchor-before.trx：修复前反例 **0/1**；期待回到Y=940，实际Y=640。
- tests/ti02-combined-final.trx：中间版本81/81；之后增加反向链式及快展标记断言，最终以全库文件为准。
- tests/ti02-full-final.trx：最终1043/1043。早先1038项文件保留为中间证据，不替代最终结果。
- app/：独立构建，仅编译验证；最终指纹见下方。

最终 SHA256：
- DesktopUp.dll：`CE279CE0BC2141C4F4214E7A713D4A4D19BC0CEBE1B17C2D03B28A6A8DB9C082`。
- DesktopUp.Application.dll：`920AA83312895F97517A4F5A1A168500008FB18B3041FBB9666617E5EDC0D63A`。

文档检查：81份Markdown、529个本地链接、缺失0；git diff --check通过（仓库既有LF/CRLF提示不视作错误）。仅构建独立输出，未运行应用。

## 5. 本步发现的连续操作缺陷

反例：底部 compact Y=940 → 向上展开 → 收起一半 → 标题快展 → 再收起。

最初组合实现将半收起帧当成新的 compact 展开种子，虽然当次展开尺寸正确，原 UpInset/Home 关系却丢失，下一次收起停在 Y=640。修订为反向使用原已接受收起前缀、恢复原关系；该连续反例现已通过。源真正拖动时仍按用户当前抓取点重设锚点，两者不能混同。

同时加强了最终呈现见证比较：仅证明重放可得某个合法结果不足以证明它就是已经显示的结果。

## 6. TI-03 接线前必须保留的边界

- 本步没有原生鼠标、动画视觉、菜单层级、恢复或资源采样 PASS。R1人工/测量绑定未变；06/07余门、R2-A外部连点和有效资源0/36继续保留。
- 当前场景只接受正常投影或当前有效 continuity 见证。恢复后新重建的临时布局、显示器回退映射等若没有这种见证，必须在TI-03建立受控的会话级初始见证后才能接管；不能删起点核验以让测试通过，也不能把本限制当成最终产品行为。
- TI-03需衔接原收展所有者 → 标题capture，保证旧自动回执不能回滚新手势；把当前实际接受的schedule prefix交给纯模型，失败恢复走原R4。
- ImmediateGroup只跳过表现帧，不绕开整组容量/预布局/输入岛准备。协调拖动提案不能在旧样本尚未完成时被乱序接受；正常释放与真实失效分开。
- 生效后仍须TI-04固定新包/隔离回归，TI-05人工和增量资源门，不用旧包验证新规则。之后回原04-04，主线不变。
- 本次没有commit/push；旧TI-01退役快照、日常配置及用户文件均未改。

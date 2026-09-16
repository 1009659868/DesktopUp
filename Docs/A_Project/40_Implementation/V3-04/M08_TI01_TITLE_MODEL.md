# M08-07-TI-01：旧实验退役与标题仲裁纯模型

日期：2026-09-14。状态：**PASS（本项纯模型/退役范围）**，不是生产输入/空间/呈现PASS。
任务正文：[M08计划§4.1](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#41-标题交互修订的最近顺序)；产品依据：V2-DEC-003及[冻结TI合同](M08_TITLE_INTERACTION_CONTRACT.md)。
下一项：**M08-07-TI-02：空间/呈现提案与事务纯验证**。外部连点R2-A独立，M08-06/07余门与有效资源0/36保持。

## 1. 退役审查与保留边界

检索确认旧ExpansionDrag只由下面实验代码、测试及StateProjectionCoordinator专用口引用，没有App/Windows调用。

| 对象 | 处置/理由 |
| --- | --- |
| Application/ExpansionDragModel.cs | 删除：ExpandingAndDragging、增长不回退、松手恢复增长与新固定分类相反 |
| Application/ExpansionDragSpatialModel.cs | 删除：held向下增长/暂缓、松手继续求展开及BlockedAfterRelease是被替代行为，不再作为接线前置 |
| Application/ExpansionDragSession.cs | 删除：提案与轨迹预算直接依赖旧Grow语义；不能改名后直接复用 |
| Application/ExpansionDragCommit.cs | 删除：旧重放强制源终态展开，专用命令/接口及策略一并退役 |
| StateProjectionCoordinator.ExecuteExpansionDrag及IExpansionDragCommitter实现 | 移除：无产品调用的旧专用写口；通用协调器恢复到现有HEAD内容，不放宽System权限 |
| ExpansionDragModel/Spatial/Session/Replay/TransactionTests.cs | 删除5份旧测试文件，共69个测试案例；新规则不是将这些相反断言改名后冒充通过 |
| Domain碰撞/路径/展开/连续归位；SpatialGestureSession/SpatialPostureCommit；通用SQLite/CAS；R4 | 原实现及现有测试保留，本轮未修改；完整回归证明未受本次退役影响 |
| 安全经验 | 保留受控呈现证据、身份/完整读集、因果重放、准备后接受与有限结束要求；新并行收拢路径的具体证明在TI-02重建，不保留一个仍可调用的旧增长验证器 |

删除前的当前工作副本（含未提交新文件）已保存：
`out/verify/v3-04-m08-07-ti01/retired-growth-before-ti01.zip`，10个条目：上述4份实现、5份测试和当时协调器。
SHA256：`B80157D2E0F6C3781D6E6CDD57CEA852FA9AF191B944C4ACC964F29607B1C6E6`。
压缩包按文件名保存，原路径映射见表。已跟踪的Model及ModelTests另可从73c7f85恢复；未跟踪实验不能只靠Git恢复，故保留此快照。恢复必须到隔离目录核对，不覆盖当前代码。
旧[实验报告](M08_COMBINED_VALIDATION_R3.md)、TRX、运行包、用户配置均保留；快照是忽略的本地产物，不进入活动代码或Git提交。

## 2. 新纯模型与输出合同

[TitleInteractionModel.cs](../../../../src/DesktopUp.Application/TitleInteractionModel.cs)：

- Begin接收上一会话状态及已取得的PosterInputCapture、当前呈现阶段/按下坐标、会话/环境和序列。当前所有者未结束、新旧身份或序列不符时不能重建Pending；重复按下不重置按下阶段。
- 只接启用自动、Ready、非编辑的TitleBar；其他区域交回原路径。锁定仍可点击展开，不能拖动。
- Begin输出PauseGroup；Pending保持，不包含时钟或长按计时。达到原5px欧氏阈值时直接输出BeginCompactDrag或BeginExpandedDrag，同时携带从按下点计算的当前位置增量。
- 分类只看按下的已接受呈现阶段，不读committed IsCollapsed。合法受限展开终态也属于Expanded；GrabOffset和按下Frame不可被后续姿态事件重写。
- 松手未拖才输出FastExpand（已展开则ResumeGroup、不重复提交）；已拖输出FinishCompactDrag/FinishExpandedDrag，并携带最后一次位置。已越阈值后返回原点不变点击。
- 松手才发现越阈值但从未收到移动：取消并释放，不在抬起后伪造一次拖动或点击。不能invoke或释放在原标题区域外的Pending不产生点击。
- Locked的超阈值输入进入BlockedDrag，释放只让被暂停的组恢复；不补成点击。真实失效输出CancelGroup；过期/重复事件无副作用。
- 同手势LegacyInvoke/重复Release保持Consumed，避免原生与XAML各执行一次；不同operation/session/environment/capture的事件不能接管。真实跨窗口残余点击序列路由仍须TI-03/05验证。
- Phase=Released只代表指针仲裁完成，不代表动画、SQLite提交或R4恢复完成。该模型不操作HWND，不创建timer，不生成空间帧，不写库，不决定邻居能否移动。
- 位置增量用long且始终从按下原点计算，防止极端坐标溢出与累计取整漂移；它只是用户意图，不能直接作为合法空间帧提交。

## 3. 自动重入复用，而非第二套自动模型

CompactDrag释放输出TitleAutoRearmSuppression，记录本会话/环境以及释放时的真实ExitVersion。ForObservation映射到现有AutoPostureObservation.ManualCollapseExitVersion；真实边界计数继续由既有PosterPointerTracker提供，申请资格仍由AutoPostureModel决定。

- 静止鼠标下边界/空间变化、Unknown或只有进入计数变化，都不能替代真正离开。
- 拖动期间已经发生过的进出不算释放之后的新离开；模型测试使用释放时计数建立抑制。
- 真实退出后解除抑制，真实进入后原自动模型发起一次展开；不会由此模型发起另一套空间行为。
- 抑制不泄漏到其他PosterId/session/environment。显式标题点击不受自动抑制否决。
- Rearm输出非null表示本次产生的新抑制证据；null不是清除既有抑制的命令。显式快展若失败，TI-02/03不得因一个点击意图就撤销仍有效的旧抑制。
- 本轮仅验证纯数据与旧自动模型接合，未改AutoPostureModel、AutoPostureEvidence或App观察适配器。

## 4. 验证证据

根目录：`out/verify/v3-04-m08-07-ti01`。

| 证据 | 实测结果 |
| --- | --- |
| tests/before-retirement.trx | 清理前921/921、0 skipped |
| tests/ti01-first.trx | 新109/109一次通过：仲裁50、连续操作55、原自动抑制接合4 |
| tests/ti01-full.trx | 最终961/961、0 skipped；921−69旧规则＋109新规则＝961 |
| app/ | 独立WinUI Release构建0警告/0错误；没有启动或替换人工绑定 |
| 源码检查 | src中ExpansionDrag/IExpansionDragCommitter引用为0；测试仅保留“旧类型/端口不存在”的防回归断言；App/Windows/Domain/Infrastructure/脚本/配置无本步差异 |

新测试覆盖TI-02..07/11的纯仲裁部分：6种中间高度/方向、末帧与按下顺序、微移/长按、锁定、非目标区域、重复/跨身份输入、9类失效×4活动阶段、10000次静止/微抖、4个DPI参数各1000次反向意图，以及200×100种子随机连续输入。测试不是GPU帧率或原生DPI认证。

App程序集SHA256：`5DCE714A8E7CDD70FBDB8EE5832B897F0DB496E2692C4BFB8F8495B6A239BC24`。
人工/测量绑定仍为R1；当前源码新增类不会让旧包自动获得新交互。无新增实机或资源PASS，无commit/push。

## 5. 下一项的必要出口

TI-02复用既有空间求解器，证明当前位置并行收拢/移动的每一步实际占位、标题抓取点、窄缝有限结束、链/分支/合流条件归位，以及单revision/完整读集/错误恢复。
同时验证同一快照的悬停、按钮和标题快展使用相同合法空间结果；不能用TitleInteractionAction或Delta绕过求解/CAS，也不能恢复旧Grow终态。
这些通过后才进入TI-03输入/单一空间所有者/R4接线；之后TI-04隔离包、TI-05人工与资源收口。每次阶段交付均列出本项计划文档、验证范围和下一步，不以构建成功提前关闭后续门。

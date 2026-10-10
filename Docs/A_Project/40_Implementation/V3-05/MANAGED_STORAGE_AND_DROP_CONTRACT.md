# 受管存储与拖放意图合同

日期：2026-10-10。归属：V3-05-03／05-07第一批历史合同与证据。纯合同/自动验证已完成；当时schema15、无文件执行。最新实现转[第二批账本与隔离恢复](MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md)：schema16与受限后端已验证，原生拖放仍未接。下文“本批/下一步”保持第一批的范围，不覆盖第二批现状。

产品决定归 [DEC-039](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md#31-已明确的近期优先级)，实施顺序归 [主工作流 §6.5](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径)。[DeskBox 收纳参考](../../20_Architecture/DESKBOX_COLLECTION_REFERENCE_V1.md)记录分类、执行和手势来源；本合同是 DesktopUp 的适配，不继承参考项目的测试结论。

## 1. 存储与海报关系

受管根目录由用户明确选定，必须是持久用户内容位置。安装目录、缓存、临时目录、未知位置类型不可用于收纳。根目录以稳定 ID、提供方解析的对象身份和版本见证登记；路径不是安全证明。未选路径返回 ChooseRoot，离线/只读拒绝，未知配置版本保留并拒绝写入。本批不创建目录，也不将盘符相同视为同一卷证明。

每个受管集合拥有稳定 CollectionId 和自己的子目录，不用可改的海报标题作文件身份。ManagedCollectionRegistrationPolicy 只产生注册提案；不能复用旧 Reference 的 ID 将其改义，也不重绑已有海报。

首批关系模型：

- 海报的主集合继续是 Reference **视图集合**，保留 User membership、PosterItem、布局和独立图标。
- 实际收纳的文件另外进入 Managed **存储集合**，以 Source membership 记录物理归属；这不是第二份文件，也不是第二套可变 ContentItem。
- 拖入后同一 ContentItem 可以同时拥有存储成员关系和一个或多个视图成员关系。旧引用只有视图关系，不因为上线新功能自动获得存储归属。
- 拖出成功后撤去已不在受管目录中的存储成员关系，更新共享来源，并只解除本次成功取出的当前 PosterItem。仍有其他海报引用的内容保持可用；各卡图标/图钉/跨度/顺序不变。
- 删除海报、解除最后一个视图引用、退出和卸载入口均不得隐式删除存储集合、内容文件或未完成账本。存储集合不以海报存活为条件。
- 直接绑定 Mapped/Managed 目录的视图属于后续目录功能，本批明确拒绝，不将它们偷偷变成 Reference。真正受管与旧引用依据存储成员关系区分，不能仅看海报视图类型。

生产迁移仍归第二批：追加根目录/集合注册及账本存储；原 schema13～15 不重写、不扫描搬移旧来源。注册目录和发布关系之间的失败也须可恢复。首批代码只验证模型共存，不声称上述生产持久化已完成。

## 2. 分类与操作意图

| 来源与动作 | 首批决策 | 边界 |
| --- | --- | --- |
| 本机文件/文件夹/已有快捷方式，默认或 Shift | Move | 必须声明移动能力且来源可用；快捷方式本体移动 |
| Ctrl | Copy 意图，尚未开放 | 不回退为 Move |
| Alt 或 Ctrl+Shift | Link 意图，尚未开放 | 不回退为 Move；右键拖放菜单后续 |
| Application/ShellItem/VirtualStream/Unknown | 结构化拒绝 | 后续合法 AppsFolder 链接、受限物化不能冒充移动安装目录 |
| 海报内拖动 | Layout | 复用 ContentDragPolicy、ContentDragNavigation 和既有图钉/手动布局规则 |
| 向另一个海报/未支持目标 | 拒绝 | 不把跨海报关系操作当成桌面取回 |
| 缺少持久目标 | 选择存储或取消 | 不降为 Reference |
| 桌面目标无法由提供方解析 | 拒绝 | 不硬编码 Desktop，不读取 Explorer 图标坐标 |

CF_HDROP 物理路径优先于重复虚拟格式；有虚拟描述而无物理路径时单独归类。实际 COM 解析、能力探测、重解析点/目录自包含/权限/云占位验证仍由未来原生/文件适配器提供；纯模型接受的是经过解析的事实，不提供文件系统授权。

Ctrl/Shift 选中集合发生在手势起始阶段；外拖修饰键发生在跨边界后的意图阶段。内部排布不因 Ctrl 改成复制文件。新增合同没有替换现有 ContentActivationSession：单击选中、双击/Enter 打开；进入拖动必须重置打开配对。

## 3. 预演、确认与完整读集

每批最多 100 个物理源。入口先归一重复格式/相同对象；仍重复的源、重复目标和同时选择目录及其后代，纯计划拒绝，防止一次批次搬两次。

ContentTransferPlanning 冻结原始来源、目标候选、对象身份、源变化见证、目标父目录见证、运行代次、完整 ContentVersion 和受影响海报版本。源失效、目标变化、根目录重连/替换、关系集合改变、海报锁定/隐藏/只读都需重新预演；无关海报单独改布局不因全局 revision 变化使此计划失效。

影响范围必须包括：

1. 当前 ContentItem 的全部海报引用；
2. SameSource 的不同独立 ContentItem 身份；
3. 移动文件夹时，提供方针对同一 ContentVersion 解析出的子项引用。

提供方必须证明子项影响收集完整，不可让 UI 随意填空列表。重解析目标、路径别名与硬链接的判定仍是第二批安全适配器工作；此处的事实快照不替代真实身份校验。

旧引用拖出必须展示并确认真实源路径；共享/子项影响列出全部海报。首次无主集合的海报还需确认初始化/DEMO 归档，复用原引用添加原则。确认绑定整个预演指纹，换来源、换目标、改关系或改动作后旧确认失效。

同名不覆盖：选择新的空闲目标后重新预演、确认。相同路径且仍是同一对象只返回 NoOp；不创建副本、不清源、不隐式撤去卡片。不同路径即使对象 ID 相同也不伪报已移动（如硬链接）；首批要求显式处理冲突。

ReadyForJournal 仅意味着可以准备账本，**不是可以立刻执行文件移动**。执行前仍需持久意图、路径占用、当前读集及取消检查。

## 4. 文件事实与恢复

逐项阶段：Planned → IntentDurable → Executing → FileVerified → IndexCommitted；另有明确 NoOp。只有 IntentDurable 且占用/读集有效、未取消时允许开始执行。源删除责任必须只有一个所有者，Shell 反馈与异步文件任务分离，不能让源和目标各清理一次。

| 观测事实 | 恢复决定 |
| --- | --- |
| 未写持久意图 | 先写意图，不能执行 |
| 源仍在、目标不存在 | 重新校验并明确重试；不自动重放 |
| 源不在、目标身份和完整性已验证 | 恢复索引；数据库冲突不意味着搬回原路径 |
| 两份都在 | 保留两份进入核对，不直接删其中之一 |
| 任一路径已变成其他对象 | 保留冲突，不覆盖后来产生的文件 |
| 身份/完整性未知 | 重新观测 |
| 两处都找不到 | 报告待找回，不伪造成功 |
| 索引收据已提交 | 幂等完成，不再次搬移 |

手势取消、隐藏、退出或异常断触不能改写已经发生的文件事实。取消后到达的真实完成结果仍需进入恢复/索引通道。批次逐项发布，失败项保留关系；不得因为一个成功项而把整组标成完成。

DescribeVerifiedMove 只输出关系变更提案，既不证明磁盘成功，也不执行数据库提交。第二批必须将提供方逐项验证收据、完整 CAS、存储成员关系增删、别名/后代 locator 更新及幂等结果接到同一仓储事务；不以 SQLite 回滚假装回滚文件系统。

## 5. 连续手势与生命周期

ContentTransferGesture 是附加到现 ContentGesture 的**边界仲裁模型**，不负责计算布局、捕获鼠标或另开计时器，当前尚未接生产。

- 按下冻结 CaptureId、PosterId、generation 和选中集合；沿系统拖动距离门，不加长按等待。
- 海报内跨阈值移动只预览现有布局；松手最多提交一次。
- 离开来源海报，先撤内部预览，再最多一次交给原生会话。
- 原生会话取得所有权后，本地鼠标 Up 和重复 Motion 不得再次提交内部布局或启动第二次原生拖动。
- 同一次原生拖动返回来源海报，可由原生结束回调提交有效内部落点；不重新要求按下。
- 外部接受只进入 AwaitingResult；不能按 effect/DragEnd 直接删卡或统计成功。
- Esc/隐藏/锁定/真正失效取消预览；原生阶段只发取消请求并等待结果。接管造成的本地释放不当成真正失效。
- 旧 capture/代次、乱序/重复序列和结束后的回调无效；各海报会话隔离。
- 关闭控制中心不影响海报会话。退出先拒绝新手势，再取消/排空或留下持久恢复任务；不依赖中心 UI 存活。

需要后续原生门证明：一次按住内外切换、返回、Esc、多选、收展并发、隐藏/退出、Explorer 完成语义和拖动数据寿命。纯测试不能替代这些验收。

## 6. 代码与验证

新增代码：

- [ContentDropIntent](../../../../src/DesktopUp.Domain/ContentDropIntent.cs)：根目录资格、载荷分类、动作/能力。
- [ContentTransferPlanning](../../../../src/DesktopUp.Application/ContentTransferPlanning.cs)：冻结提案、共享影响、完整读集、指纹确认、关系差量。
- [ContentTransferRecoveryPolicy](../../../../src/DesktopUp.Application/ContentTransferRecoveryPolicy.cs)：逐项恢复决策、执行/提交门、受管注册共存。
- [ContentTransferGesture](../../../../src/DesktopUp.Application/ContentTransferGesture.cs)：连续内外拖边界、单次原生所有权。
- [ManagedStorageDropContractTests](../../../../tests/DesktopUp.Contracts.Tests/ManagedStorageDropContractTests.cs)、[ContentTransferContinuityTests](../../../../tests/DesktopUp.Contracts.Tests/ContentTransferContinuityTests.cs)：70 项新增纯测试。

最终针对性结果：`out/managed-drop-contract/targeted03/targeted.trx`，70/70 通过。完整合同：`out/managed-drop-contract/full02/contracts.trx`，2724/2724 通过，0 失败/跳过。此前 targeted01 的 23 项失败来自测试夹具使用了不合法的图标资产 ID，修为现 ContentIconAsset 工厂生成；原失败证据保留。targeted02/full01 是追加边界测试前的 63/2717 结果，不冒充最终证据。

生产项目独立构建输出为 `out/managed-drop-contract/build01/`，Release x64为0警告/错误；源码集合、测试程序集、App及TRX哈希保存在 `out/managed-drop-contract/proof.json`（588个源码/脚本/配置文件指纹）。没有启动人工候选，没有真实磁盘拖放；schema15、旧引用、V1资产底层、便携v5拒绝真实引用以及 R7/C2 携带项不变。未提交/推送 Git。

## 7. 第一批交出的下一步

进入 **05-08 持久账本与恢复＋05-12 最小保护**：

1. 追加可恢复的根目录/集合注册、逐项操作账本、占用与幂等收据；旧库备份/未来版本拒绝。
2. 实现本机提供方事实解析和目录/别名影响收集；先验证安全边界再允许执行。
3. 只在自建隔离目录进行同卷/跨卷、同名、源变更、权限/磁盘不足、逐阶段崩溃、部分完成、数据库提交失败的故障验证。
4. 保证未完成任务可独立于海报恢复，找回入口能定位唯一副本；通过后再接原生拖入，随后拖出与人工闭环。

图标更换 UI/构造卡片仍在双向基础交互稳定之后，不因第一批纯合同通过而提前。


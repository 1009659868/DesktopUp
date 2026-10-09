# V3-05-01：真实内容实体、能力与打开结果合同

后续接续（2026-10-09）：05-04已获用户确认；当前[05-05／06](REAL_CONTENT_DISPLAY_AND_ACTIONS.md)已接SQLite14持久打开收据/事实、真实图标/打开/定位，自动通过、人工待验。本页以下是05-01纯模型交付时的冻结范围和2506测试证据，“生产未接线/SQLite12”不覆盖后续实现。

状态：**PURE MODEL / CONTRACT PASS；PRODUCTION NOT WIRED**。2026-10-09，按用户授权完成01-A～D。任务正文见[工作流§6.1](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#61-v3-05-01实施准备实体能力与打开结果)，产品成功门归[V2-DEC-034](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md#31-已明确的近期优先级)。

本步新增纯模型、拒绝策略、关系命令准备、异步结果归约和兼容夹具，不扫描、打开或移动用户文件。生产仍使用DEMO；SQLite12、portable5、content-layout3、presentation6不变。新接口未注册到App，不把全量合同通过当成原生Shell或实际迁移通过。

## 1. DeskBox如何处理 → 本项目如何适配 → 对应测试

参考根为 `G:\A_\Tools\reference\DeskBox`，commit `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`，GPL-3.0-only；已有THIRD_PARTY_NOTICES登记。本次只读参考、独立实现合同，不复制其实现片段或运行其测试。详细来源见[通用合同§8.1](../../20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md#81-v3-05-01参考入口复核2026-10-09)。下表参考路径相对该项目的src/DeskBox。

| DeskBox实现事实 | DesktopUp适配 | 本项目可执行反例 |
| --- | --- | --- |
| Models/WidgetItem.cs混合路径、目标、图像、顺序和选择；WidgetConfig维护映射与受管目录 | Domain拆成全局内容、集合、成员、海报关系；UI/Bitmap/排列不进内容实体 | RealContentModelTests：一项双海报、独立布局ID、重命名及暂失效保留身份 |
| FileDropIntentPolicy按来源能力/目标区分Reference、Copy、Move、Shortcut；虚拟流不能稳定引用 | 能力是必要条件，不等于授权；虚拟流先Materialize，应用/Shell不能伪装可搬运文件 | 应用变更拒绝、虚拟来源拒绝、未知类型/能力/操作拒绝 |
| FileTransferSessionRegistry区分复制源、移动源、目标与目标目录 | 复制源可读；移动源/未完成目标拒绝Open等读操作；活动来源/目标拒绝变更 | TransferReadAndMutationProtectionAreSeparate；来源不可用保留Reference而拒绝Open |
| FileOpenRequestGate按路径去重；FileService.OpenItem.cs保留Shell语义 | 去重包含提供方、完整目标、参数及工作目录；ActivationId防重放，同路径不同参数不合并 | ArgumentsWorkingDirectoryAndOpaqueIdentityAreNotNormalizedOrMerged；ActiveOpenSemanticGateIncludesArgumentsAndWorkingDirectory |
| BoundedStaOperationRunner排队有界；已开始原生调用不能假装被取消并释放槽位 | 队列前可取消；Running取消/等候到期仅标意图，保留占用直到真实终态；并发数暂由宿主传入，不照抄2/6/2秒 | CancellationBeforeDispatchPreventsStartingAndLateSuccess；CancellationOrWaitExpiryAfterDispatchDoesNotPretendToAbortNativeCall |
| FileSurfaceContent.Opening.cs隔离过期UI；OpenedOrHandled可能仅打开失效快捷方式修复界面 | Handled不等于成功；DEC-034明确交接证据才生事实；迟到成功归原身份，不复活旧关系/UI | QueueAndRepairDialogAreNotSuccessfulOpening；LateActualHandoffIsAttributedWithoutRecreatingDeletedRelation |
| 参考项目有策略单测及源码字符串接线断言 | 本步验证实际纯函数输出/状态，不以字符串断言或参考测试代替本项目证据 | RealContentModelTests、RealContentOpenTests、RealContentCompatibilityTests共82例 |

## 2. 实体、归属与身份

| 模型/权威字段 | 责任与不变量 |
| --- | --- |
| ContentItem：Guid Id、Kind、Source、Target、能力、可用性、Version | Id稳定；Source=(ProviderId,Key)是提供方范围的不透明标识；目标/名称可变不自动换Id。不同来源或不同参数只提示重复，不自动合并 |
| ContentOpenTarget：Kind、Locator、Arguments、WorkingDirectory | 原样保留语义；不在纯模型中大小写归一、解析路径、剥离参数或把快捷方式替换成exe |
| Collection：Guid Id、Kind、StorageLocator、Ownership、Version | Reference无物理存储所有权；Mapped显式目录/Borrowed；Managed显式目录/Managed。标记本身不是删文件权限 |
| CollectionMembership：Id、CollectionId、ContentItemId、Origin、Version | 集合内同Item唯一；Reference成员由用户命令维护，Mapped/Managed由来源事实维护 |
| ContentPosterBinding：PosterId、CollectionId、Version | 一张海报一个主集合；可共享集合，不共享本地构图 |
| PosterItem：Guid Id、PosterId、MembershipId、Version | 指向本海报主集合成员；同海报同成员唯一。本地布局键为 `{PosterId}:content-{Id:N}`，不替代全局ContentItemId |

既有ContentLayoutDocument继续持有顺位、跨度、手动位置与图钉。PosterItem不再保存另一份order/pin，未删除既有Poster前缀隔离校验。新模型以不可变快照读入并校验外键、唯一性、正版本、容量及字段边界；现夹具最多每类10,000项，不是生产容量承诺。

生命周期：
- 暂失效、拒绝访问或离线保留内容与关系，不当作空集合清理；明确的来源变更由后续命令处理。
- “移除本海报关联”只准备删除PosterItem和对应本地placement；保留ContentItem、Collection、Membership、其他PosterItem和源文件。
- “从共享集合移除成员”是另一命令，可能影响多个海报，不能复用本地detach冒充。05-04须明确UI措辞/重启可见性，不能由元数据刷新自动重建已移除关联。
- 集合最后一个海报解除不触发磁盘删除；物理回收/迁回归05-08/10/12安全链。

## 3. 能力与命令边界

能力位包括Reference/Open/Reveal/Copy/Move/Link/Rename/Recycle/Enumerate/Materialize。CapabilityPolicy还检查类型、可用性、传输角色；未知值拒绝。协议白名单、实际权限和来源真实性归可信适配器，不能由导入JSON自报能力取得系统权限。

文件/目录/快捷方式沿各自提供方能力判定；Application禁止Copy/Move/Rename/Recycle，普通exe若作为文件则是File而不是安装应用对象。ShellItem在首批同样禁止这些磁盘操作，后续05-13逐提供方扩展，不能默认此电脑/回收站可搬运。VirtualStream只允许声明Materialize，未落地前不得作为持久可打开引用。

关系读集包含PosterItem、Membership、Collection、ContentItem、PosterBinding的全部ID/版本。任一变化使旧计划失效；无关海报更新不造成A冲突。PrepareDetach只产出计划，**没有执行/持久化**。生产接回时必须同一事务内复核完整读集、同步修改本地布局并只提交一个revision；纯检查后另行写库不满足合同。

## 4. 打开请求与成功事实

沿用DEC-023手势和既有ContentActivationSession：单击选择；双击或有效Enter产生一次ActivationId；按住移动拖动不追加打开。Prepare先复核当前呈现见证，再由局部布局键解析到正式身份；不得直接用显示名称或AssetKey启动。

流程：`Prepare → 准入/去重 → Queued → 派发前版本/能力复核 → Running → Completed`。队列可省略；拒绝、不存在、忙或取消不分发。相同ActivationId+相同请求是Duplicate，不再次执行；同token不同载荷是Conflict。真实重试使用新ActivationId。活跃的相同提供方+完整目标返回Busy，不丢失参数差异。

| 结果/阶段 | 事实与资源语义 |
| --- | --- |
| Prepared/Queued、界面反馈 | 不计数；未派发取消/超时直接终结 |
| Running收到取消请求/等待到期 | 仅CancellationRequested/WaitExpired；不能宣称原生已终止、提前释放槽位或再分发 |
| Succeeded + ShellAccepted/ProcessCreated/ProviderConfirmed | DEC-034：提供方明确证明成功交接，产出一次ContentOpenFact；不证明外部程序已经加载完成 |
| Handled/Rejected/Busy/Cancelled/Unavailable/Failed/TimedOut | 不产成功事实；修复/选择对话框属于Handled而非Succeeded |
| Running取消后仍收到真实成功交接 | 原操作已经发生，保留该事实，不以取消意图篡改为失败；界面/关系可能已销毁，不复活它们 |
| 外来提供方/ActivationId、不匹配证据、终结后重复回调 | 拒绝/忽略；不改已确定终态，不再产生事实 |

ContentOpenFact携带ActivationId、ContentItemId、来源PosterId/PosterItemId、源身份/版本、交接证据及观察时间。幂等键为ActivationId：同事实重投Duplicate，不同载荷Conflict。未来单权威存储以唯一索引和同一事务保存事实/统计，不能靠会淘汰的点击缓存或UI回调累加；统计消费归06-05，当前未更新热度。

调度接口只接可信宿主，不让主题/AI/导入包执行任意Shell。运行实例代次、UI代次、超时隔离、崩溃后的不确定交接、事实持久去重及worker退出排空仍须05-06实现/验证；本步纯模型不承诺外部副作用跨崩溃恰好一次。

## 5. 序列化与迁移合同

ContentCatalogCodec的formatVersion=1仅是独立合同夹具，**不是新生产数据库或便携格式**。UTF-8上限4 MiB、深度32；写入也遵守读取上限。未知版本/属性、缺少关键语义字段、重复JSON属性、伪造计算布局ID、无效图关系均不给可写Catalog；保留Original供诊断。成功往返保留ID、参数、工作目录、来源和关系，不忽略未知字段后重写。

ContentLegacyCompatibility只对SQLite1～12的旧海报描述做纯评估；保留ContentLayoutJson原文/null，未知布局只读，正式目录为空。没有运行SQL或验证所有旧SQL迁移，测试样本为1/3/8/12；未来版本13及以上拒绝猜测。

05-04持久接线前必须补：
1. 在现SQLite中增加实体/成员/海报绑定关系和唯一索引；具体migration数字在执行时核对，不改旧migration/checksum，不新增第二数据库。
2. 升级前备份、旧数据完整保留、失败回退/显式恢复、重复升级与原子提交夹具；各版本域独立升级。
3. 旧DEMO布局不自动推导文件路径、不灌入正式Item；从DEMO转真实源是显式命令和可解释映射，不将null当已添加内容。
4. 新增正式关系、placement及来源绑定同事务；0/1/多引用同库重启、跨海报共享、删除关系不复生。
5. 便携携带真实来源还涉及路径隐私/跨机失效，不把本夹具JSON直接当可执行导入包。05-01未修改portable5的支持范围。

## 6. 代码与验证记录

| 层 | 代码入口 |
| --- | --- |
| Domain | [RealContent.cs](../../../../src/DesktopUp.Domain/RealContent.cs)、[ContentCapabilityPolicy.cs](../../../../src/DesktopUp.Domain/ContentCapabilityPolicy.cs) |
| Application | [ContentRelations.cs](../../../../src/DesktopUp.Application/ContentRelations.cs)、[ContentOpenContract.cs](../../../../src/DesktopUp.Application/ContentOpenContract.cs) |
| 兼容夹具 | [ContentCatalogCodec.cs](../../../../src/DesktopUp.Application/ContentCatalogCodec.cs)、[ContentLegacyCompatibility.cs](../../../../src/DesktopUp.Application/ContentLegacyCompatibility.cs) |
| 测试 | [模型](../../../../tests/DesktopUp.Contracts.Tests/RealContentModelTests.cs)、[打开结果](../../../../tests/DesktopUp.Contracts.Tests/RealContentOpenTests.cs)、[兼容](../../../../tests/DesktopUp.Contracts.Tests/RealContentCompatibilityTests.cs)、[公共夹具](../../../../tests/DesktopUp.Contracts.Tests/RealContentFixtures.cs) |

实际工作区G:\A_\Tools\DesktopUp_V2，分支impl/poster-redesign-V3，起始HEAD b76eb1a1e212b81065db526fb8e21d64038b4086＋既有未提交修改；后者完整保留。当前机器安装SDK10.0.103，非旧机器的out/toolchains/10.0.112。命令均Release/x64/--no-restore：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release --no-restore -p:Platform=x64 --logger 'trx;LogFileName=full-02.trx' --results-directory out/v3-05-01/contracts
dotnet build DesktopUp.sln -c Release --no-restore -p:Platform=x64
```

- 最终全量 **2506/2506通过、0失败、0跳过**，其中本步新增82例；full-02.trx。早期targeted-01=77、targeted-02=80、full-01=2504为收紧字段/字节预算前的历史运行，不混用数量。
- 最终solution构建 **0警告、0错误**。没有启动新GUI候选、执行Shell、升级日常库、触碰用户文件或提交推送Git。
- 本地证据清单为 `out/v3-05-01/verification.json`，绑定本步10个源码/测试文件及报告、测试程序集、App程序集哈希；它不是完整原生候选seal。最终TRX SHA256：`94EFF50325DF6297083DCE9A0650DFA28BEB7A5BF2657F631339BB9D61641682`。
- 原R7/R8封存包及1/3/6假源门保持原证据范围；新增源码不继承旧seal。当前结果不代表原生输入、系统扩展或真实文件操作通过。
- R7完整验收仍OPEN，D3/其他未验与BUG-001/004/006/026/031/035等按原[携带清单](../V3-04/LR_MILESTONE_EXECUTION.md#12-r7出口审查与运行差异收清2026-10-09)处理；C2按DEC-033后置。

## 7. 当前交接与下一步

01-A/B/C/D按本步纯合同范围完成。下一步为 **V3-05-04：非破坏性引用添加**，先落同一SQLite的正式身份/关系和安全迁移夹具，再接添加解析、逐项预览、明确复用/独立/跳过/修复与重启保留。普通文件、快捷方式、应用身份分别判定，原文件不动；真正打开适配归05-06，图标提取归05-05，复制/移动/回收仍等待05-08。

宿主端口接线顺序：现ContentSourcePorts/ContentSourceProjection的局部ItemId → PosterItem映射 → 可信内容目录快照；现void IContentOpenHandler → 05-06异步调度适配 → 本合同请求/终态 → 持久事实消费。不得为赶接入把旧void返回当成功，或把新目录当第二状态权威。

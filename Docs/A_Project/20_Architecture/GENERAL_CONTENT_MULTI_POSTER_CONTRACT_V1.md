# 通用内容与多海报架构合同 V1

2026-10-13合同覆盖（DEC-047）：单个有效对象只允许一个可见海报引用；旧关系表/实体保留用于迁移、历史凭据，不再代表可新增共享能力。拖入继续使用已验收的预写账本及防覆盖执行器；拖出使用原生Shell数据对象与DoDragDrop，由Shell决定目标/冲突/落点，应用只核实逐项源状态后调用公共关系移除，绝不二次搬文件。旧有效引用无额外确认；原本失效的引用只在外部松手后确认移除卡片，离线/拒绝访问不推定删除。当前实现与限定范围见[§11](../40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#11-dec-047单一引用与直接原生拖放)；本文旧共享设计被本段覆盖。

状态：DESIGN BASELINE；2026-10-12生产映射：真实引用／打开／独立图标、schema16～19账本与归属保护保留；DEC-041同卷基础拖入／拖回桌面已有接线及自动门，普通权限人工未验，旧恢复任务UI退役。DEC-042新增[全区域拖放与默认存储迁移方案](DROP_SURFACE_AND_STORAGE_PROPOSAL.md)，D0空内容/D1本机探针已过，D2默认位置与整库迁移自动/人工限定门已过；D3接收基础隔离实现，生产全区域/实际落点/逐项跨卷尚未开放；不能沿用旧“原生未接／四任务待验”判断当前状态。完整Shell目标、映射目录及真实热度消费未完成，证据归C层。
日期：2026-09-07
适用：impl/poster-redesign-V3；不替代七份需求。当前工程/人工范围查[05-04实施](../40_Implementation/V3-05/NON_DESTRUCTIVE_REFERENCE_ADD.md)，R7/R8原出口与携带项保留[执行§12～14](../40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md#12-r7出口审查与运行差异收清2026-10-09)，不沿用M03初次待验状态判断全库。

## 1. 输入与明确不变项

用户确认：通用桌面内容收纳、多海报、一个控制中心、一个托盘、自定义独立风格化主题、热度排版、AI 辅助定制。来源：画像 RP-GEN-01/02、RP-MP-01..05、RP-STYLE-01、RP-ACT-01、RP-AI-01；Item §20；运行表面 §17。

保留已验证的 WinUI/Win32 宿主、DefView owner、no-activate、desktop-bottom 保护、视觉与输入岛分离、几何预备提交、SQLite 备份和命令投影基础。不得因支持多实例重新引入整屏透明输入 HWND、永久 topmost、Win+D 隐藏后恢复或旧 Qt/WebView 代码。

以下具体数据/端口为本轮设计基线，需在对应实现项用测试收敛；不能把设计选择写成用户逐项确认或实现已经完成。未决产品默认值见风险台账。

## 2. 实例与共享服务

| 范围 | 所有者与责任 | 不得混入 |
| --- | --- | --- |
| 应用唯一 | AppBootstrap、SingleInstance、ControlCenterService、TrayService、StateStore、CommandCoordinator | 单张海报的几何、草稿或主题覆盖 |
| 环境共享 | DesktopEnvironmentCoordinator、ShellLocator、DisplayTopology、恢复 generation | 将一张海报恢复成功等同全部成功 |
| 空间共享（已接线） | PosterSpatialRuntime＋纯几何策略、完整障碍读集/推让/条件归位 | 另建状态权威；以抬层容许重叠；改变邻居主题/内容/尺寸 |
| 海报会话 | PosterSessionRegistry[PosterId] → PosterSession → WindowCluster、输入、布局、主题呈现 | 全局可写 currentPoster、跨实例输入缓存 |
| 内容共享（本机引用已接） | ContentSourcePorts/可见缓存＋ReferenceContentRuntime；单应用LocalContentWorkerClient池；SQLite14收据/成功事实，元数据不重排构图 | 将共享身份当每张相同排序；将排队当打开成功；将Shell交接当外部应用加载完成 |
| 每海报持久化 | PosterDefinition/参数/ContentLayoutDocument；05-04已持久化PosterItem映射，与正式集合/内容共用原事务 | HWND/捕获句柄；将布局局部ID当全局文件身份 |
| 每海报暂态 | capture/gesture、选中项、scroll offset、候选帧、草稿、恢复状态、取消 token | 每次 pointer move 都写库 |
| 高风险边界 | 当前DesktopUp.ContentWorker负责固定Shell动作；完整Shell提供方与受限ThemeHost仍为后续 | 把进程拆分本身当成权限沙箱；向helper传任意脚本/verb |

App 只组合服务和生命周期入口。逐步把当前 App 内的单海报恢复逻辑提取为环境协调器，把 PosterVisualWindow 内的数据构造/布局策略移出；不为“类图好看”重写已验证的底层算法。移植时保留基线用例，新增多实例上下文。

## 3. 数据关系与身份

一个 SQLite 数据库保持单一权威，不为每张创建数据库。设计实体：

| 实体 | 最小职责/字段 | 关系与删除 |
| --- | --- | --- |
| AppProfile/Settings | profileInitialized、全局设置、catalog version | 区分首次初始化与用户删至零；当前选择属于 UI 状态，不决定业务命令目标 |
| PosterDefinition | PosterId、名称/用途标签、CollectionId、ThemeInstanceId、几何、用户显隐、锁定/收起、内部层序、entity version | 可列出/创建/复制配置/移除；删海报不级联删真实文件 |
| Collection | CollectionId、Reference/Mapped/Managed、来源身份、目录、所有权标志、能力、source revision | 可被多个海报引用；解除最后关联不自动清空存储 |
| ContentItem | ItemId、source identity/type、目标或 Shell identity、能力、元数据、有效性 | 路径不是唯一身份；应用对象不要求有 exe 路径 |
| CollectionMembership | CollectionId、ItemId、来源关系 | 映射清单由来源事实维护；引用集合由用户命令维护 |
| PosterItem | Guid Id、PosterId、MembershipId、Version及可选IconAssetId；派生局部布局键，顺位/图钉/跨度仍仅在ContentLayoutDocument | 图标覆盖只属当前卡片，不携带第二份可变源文件或独立排列权威 |
| ThemeInstance | InstanceId、PosterId、模板版本、参数/素材/结构覆盖、entity version | 克隆配置产生新实例；模板包可共享 |
| ActivityEvent | EventId、ItemId、来源 PosterId、时刻、打开结果 | 幂等事实；排名不能伪造或重复计数 |
| FileOperation/Journal | OperationId、批次/逐项状态、来源、目标、恢复信息 | 必须先于磁盘 mutation；保留未完成操作 |

初版每海报一个主集合；多海报可以共享集合，混合引用集合可容纳不同类型入口。多目录聚合与查询集合留作扩展，不自动等于已承诺首批功能。

迁移不可改写已发布的 migration 或 checksum。新增版本保存全部旧字段；无真实 Item 时不得把六张演示卡片灌入用户正式库。初始化、失败保留、迁移前备份、幂等及旧库恢复测试必须保留。M01 的 [schema v3/目录仓储](../40_Implementation/V3-04/POSTER_CATALOG_SCHEMA_V3.md) 保留原历史范围。V3-05-04已追加schema13正式关系、旧layout显式归档与单事务提交，并验证12升级/失败保留/本机备份恢复；未迁移日常用户库。用户确认DEC-035共享修复全部影响范围，完整读集校验，不允许部分成功；详见[实施合同](../40_Implementation/V3-05/NON_DESTRUCTIVE_REFERENCE_ADD.md)。


### 3.1 卡片独立图标与来源分离补充

2026-10-14 增量呈现约束：真实规范图标是布局无关资源，来源适配器按海报保留、按成员对账；其资源身份与布局/选择/主题版本分离。仅声明 `IContentRasterIdentity` 的可信宿主提供方使用此规则，仍以海报、Item、会话及单项来源代次校验；来源替换、显式刷新、删除与退出拒绝旧结果。`IAtomicContentItemViewSource` 保证锁内一致元数据快照，不以全局观察版本变化抹空整组卡片；通用来源仍执行原严格版本检查。渲染保留未变化节点，像素完成不触发布局/控制中心刷新。实施和验证归[增量呈现记录](../40_Implementation/V3-05/POSTER_INCREMENTAL_RENDERING.md)，不改变布局、打开或文件移动合同。

DEC-037确认作用域：默认图标来自 ContentItem 提供方，替换只覆盖当前 PosterItem 视觉资产，不参与来源重复判断，不修改目标/参数/工作目录或其他海报。恢复默认仅清除覆盖；DEC-047 的单一归属保持。schema15已实现V1存储、隔离解码和生产读取；2026-10-14 V2 在独立功能分支接入选择器、确认预览与恢复默认，状态见[实施记录](../40_Implementation/V3-05/CONTENT_ICON_ASSETS.md)。预览冻结单卡关系/内容版本和海报版本；换选、锁定、来源变化或退出后不提交旧预览。

V1采用内容寻址的应用自有规范化BGRA资产与PosterItem引用，不保存选图绝对路径或将像素塞进布局JSON。原图只读；隔离解码校验格式、尺寸、字节预算，资产与引用同库原子提交，纳入本机备份。只追加迁移，未知客户端拒绝新格式；资产哈希及既有可见代次进入缓存身份，旧图迟到不得覆盖新图。范围和证据见[V1实施](../40_Implementation/V3-05/CONTENT_ICON_ASSETS.md)。

构造卡片是“选择已有真实来源＋可选本地图标＋确认添加引用”，不是新建磁盘文件或编辑共享来源的后门；须复用05-04独立/复用/跳过及完整CAS。失败/取消时海报、旧图、路径保持，选图/预览不能打开目标或产生热度。原05-09的新建真实文件/快捷方式与05-07真实拖放安全门不因此前移。分批出口归[工作流§6.4](../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#64-卡片独立图标与构造入口下一步)。

## 4. Revision、命令与查询

### 4.1 版本职责

- global committed revision：整个数据库提交的单调序列，用于投影一致性、日志与订阅排序。
- entity version：目标 Poster/Theme/Collection/Settings 的并发校验；命令携带期望目标版本与必要读集。
- catalog version：创建/移除及列表级操作需要校验的集合版本。
- 无关 B 的更新不能仅因 global revision 改变就永久阻塞 A；同一实体过期仍返回 Conflict。
- 相关实体命令在一个事务中校验完整读集、执行 mutation、推进实体版本和 global revision；不可在 UI 拼接新旧快照规避冲突。

### 4.2 命令范围

CreatePoster、ClonePosterConfiguration、RenamePoster、SetPosterVisibility、RemovePoster、Move/Resize/Lock/Collapse、UpdateContentMetrics、ApplyTheme 都有明确目标/版本/request ID。Create 的 identity 分配由核心完成且幂等；不存在目标返回 NotFound，删除后的旧回调不得自动 GetOrCreate。

批量 Lock/Visibility 等纯状态变更默认在同一事务中整体校验并提交；不满足时返回各目标原因，不报告部分全部成功。文件批次不承诺跨 SQLite/磁盘原子事务，必须返回逐项完成/失败/取消和可恢复 journal。

### 4.3 投影

DesktopUpProjection 包含同一 committed revision 下的 PostersById、列表顺序、Settings、ControlCenterProjection 和 TrayProjection。局部投影含实体版本、能力和实际运行状态。控制中心有列表与 SelectedPosterId 的呈现，不创造第二权威。

发布带 changed entity IDs；仅 B 变化不能重建 A 的物理窗口/输入岛。UI 订阅释放与草稿按 ID 隔离；设置-only revision 继续保持现有“不重建几何帧”保证。旧投影拒绝、自己提交不重复应用、失败保留仍适用。

## 5. 生命周期、窗口与层序

| 事件 | 处理 |
| --- | --- |
| 首次初始化 | 无既有有效配置且未导入方案时，只创建一张默认海报；正式内容为空；已有或导入的 0..N 配置按集合恢复，不额外插入默认 |
| 普通启动 | 列出 committed 海报，按持久显隐逐张建立会话；中心按需、托盘唯一 |
| 创建 | 控制中心/托盘共用创建命令，提交独立身份/配置后创建会话；呈现失败记录 degraded，不静默删除 committed 数据 |
| 隐藏/收起 | 隐藏取消捕获/禁用输入；收起沿用同窗口高度状态；全局临时隐藏不改每张偏好 |
| 移除 | 控制中心或目标海报“...”先展示后果确认；确认后重校验目标版本及文件任务，提交移除关系，取消回调/释放 cluster；保留真实内容 |
| Explorer 重建 | 一次环境发现，广播新 generation，分别预备/重挂实例并读回，不重复产生托盘 |
| 显示变化 | 共享拓扑，逐实例重投影；恢复期间不持久化暂态像素 |
| 主题/来源失败 | 单实例最近可用主题/失效状态；其他会话继续 |
| 退出 | 禁止新命令 → 结束手势 → drain/记录文件任务 → 持久化 → dispose 全部会话/监听/托盘 |

每个 cluster 以自己的 geometry revision 预备视觉、区域和输入，不能让 A 的按钮命中 B。正常布局海报外框不得重叠；内部 stack order 保留兼容和异常防御，不提供以重叠/前后置替代碰撞的正常方案。全部海报仍在普通程序之后，不能互相 reassert 抖动。只有历史/恢复异常重叠的防御路由才检查较低海报；capture 期间唯一接收者不随换列改变。

零海报是有效状态；一张失败不能提前 return 而漏建其他海报；全局数据库不可用则进入共享恢复门，不启动一套默认配置掩盖问题。

### 5.1 管理入口与确认边界

V2-DEC-006收敛托盘：仅新增、打开中心、临时隐藏/恢复全部、全部锁定/解锁、动效暂停/恢复、退出。托盘不订阅中心选择来生成单张动作；单张管理仍在中心/海报内，共享PosterId/CAS端口保留。全局临时显隐走共享运行期原因，不用批量SetVisible覆盖每张持久意图；摘要不是另一份状态权威。

CreatePoster 的入口是控制中心和托盘；RemovePoster 的入口是控制中心和每张海报的更多菜单。中心选择变化不能替换已经打开确认框的 PosterId；确认前准备移除影响摘要，确认后按原目标和当前版本校验。

共用确认服务显示海报名称、将移除的配置、保留的文件/集合/主题包、受管内容找回路径和最后一张海报的启动影响。取消不提交；进行中文件任务有阻止/安全等待语义；不能把窗口 Close 当作删除持久配置。对话框焦点不允许破坏海报桌面层级。

### 5.2 空间协同补充（设计，待 M04 实现）

[空间协同与展开合同](POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) 承接 RP-SP-01..07：锁定者是固定障碍；展开保留原尺寸，按可用空间向下、贴底向上或先下后上；可推让合法邻居，源收起后未再调整者安全条件归位。主动协调变更不违反实体独立性，但只能改变列明的受影响几何；无可行解保留原状态并解释。

推让不是多个单海报命令循环调用：完整读集包括未移动障碍和目录版本，异构几何在同事务提交至一个 committed revision，沿用现有仓储/回执；物理多窗口仍需预备、批量提交、读回和失败恢复，不能伪称跨数据库/渲染原子。09-08允许完整尺寸候选失败后的临时受限高度，必须区别持久尺寸意图与版本化有效投影，并满足内容最小高度；不能恢复旧的无条件自动压缩/写回高度行为。策略见 [空间设计§5.1～5.4](POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md#51-三档裁决与候选顺序r2)，代码尚待接入。

最小中心列表和SelectedPosterId已由M04-01接入，旧projection.Poster默认别名缺口保留为历史来源，并非当前目录丢失。原04-04继续复用列表和有效投影；DEC-005将旧预览目标改为真实海报定位与简洁管理，不增设第二状态源。

## 6. 内容、文件与原生拖放

能力驱动而非扩展名驱动：能引用/打开/复制/移动/建立链接/重命名/回收/枚举子项分别声明。适配物理路径、快捷方式、打包应用、Shell namespace、网络/云与虚拟流；不支持的提供方有结构化反馈。

顺序固定：解析 payload 和有效期 → 判定能力/目标模式 → 显示意图 → 冲突预演/确认 → journal → 执行 → 校验 → 状态投影 → 完成/补偿。Native drop feedback、源清理与异步任务所有权分别建模。应用执行移动时不让 OLE 源再执行第二次删除；临时数据不得在提供方释放后成为悬空引用。

Watcher 按来源/目录共享引用计数和有界 coalescing；事件更新所有引用者，但不触碰它们的独立布局参数。扫描、图标与扩展调用均可取消、超时、限额。ShellProxy 必须在启用高风险第三方扩展前有实际边界测试，不能推迟到安装阶段才设计。

### 6.1 桌面与海报的剪切式主路径

2026-10-12增量：DEC-042将全区域拖放、真实落点、默认活跃根及有内容迁移作为下一交互目标，详见[专项方案](DROP_SURFACE_AND_STORAGE_PROPOSAL.md)。普通点击的稀疏穿透与文件拖放接收分离；不得以扩大常驻输入岛取代验证。D2默认位置/整库迁移已达到限定出口；原生全区域/落点/逐项跨卷仍按[D3实施](../40_Implementation/V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md)推进，旧同卷拖放支持矩阵在新门通过前保持。默认无布局的海报不再虚拟注入六张测试卡片；显式数据及真实成员不因读取被删除。

**当前用户流程由DEC-041替代下方DEC-040历史设计。** 普通失败自动收尾，下一次拖动建立全新现场、计划和确认，不再暴露继续任务／取消未开始／停止重试操作。启动及操作末尾只核实既有结果，不自动重放文件Move。源保持原样且目标不存在时结束为“未移动”；物理完成且原意图仍合法时自动修复索引；双份、身份不符、独立修复、目标海报删除等结果不明时保留文件并结束调度，仅提示位置。结束调度不是文件成功，也不解除正在执行者的租约。旧记录保留，不能长期占用后来正常操作的路径。

文件系统与数据库不能保证跨卷批量原子性；普通错误尽力恢复用户可理解的稳定状态，不承诺所有失败都能物理搬回原点。自动索引修复沿原目标和共享关系，仍走完整当前CAS，不选择其他海报、不覆盖外部修复。外部更名／移动缺少可信新身份时标失效，用户可重新拖入或修复引用，不要求恢复旧文件路径。

首批入口边界：拖入采用CF_HDROP快照，只接本机物理路径；默认／Shift为Move，Ctrl／Alt明确拒绝。由本应用唯一执行移动，不让Shell再清源。拖出首批复用海报原生鼠标捕获，仅在已验证的真实桌面松手后执行到系统Desktop目录；不把此实现称为任意Shell OLE拖出，不设置Explorer图标坐标。回到海报仍按内部布局，Esc不落文件；落到其他窗口拒绝。标题及Item等既有输入区接收拖入，不新增全桌面拦截层。

以下DEC-040相关用户任务界面为历史方案，后续生产不再采用；目录归属和schema16～18安全合同继续有效。

DEC-040：恢复入口只服务异常，不是日常任务管理。正常收纳不增加账本操作步骤；异常时用“查看文件位置／继续完成收纳／取消尚未开始的收纳”表达意图，技术详情折叠。继续操作先验证现场：尚未移动则重新确认来源/目标；已移动且结果可证则只修复关联，重新确认当前海报及共享影响。旧海报已删除时可选现有海报，不静默重建；目标/来源不确定时保留文件与记录。原移动读集不能静默改写，新确认必须绑定当前状态并与完成记录单事务保存。

不确定结果另有显式“仅停止重试”出口：不读取/删除/移动文件、不修复关联、不伪造成功；独立结束记录绑定原证据和当前配置，解除旧占用但保留原阶段/路径/收据、guard及历史。目录创建中断同样可保留现场并停止，不自动认领未知子目录，已登记存储不能借此解绑。schema17实现此结束记录，停止不替代迁移。

schema18只允许当前配置已登记、一根一集合且根/子文件身份一致的旧无声明目录显式认领：当前revision与有界清单确认→意图持久化→排他凭证→重核→事务激活。凭证单独存在不授权移动，启动不得自动认领；元数据迁移不增加内容revision、不改变文件/引用，不把额外内容自动入海报。未知或其他配置声明、未登记/已停止目录及一根多集合拒绝，实际搬迁需另行合同。复制取消按真实源/目标结果进入恢复，不保证系统必留下部分目标；完整目标收据仍必须先于源处理。

DEC-039明确日常拖入/拖出以移动为目标，05-04非破坏性引用只是已完成的底层能力，不是收纳终态。拖入必须有明确的持久存储目标，不能把原文件放进安装目录、图标BLOB或可清理缓存。旧Reference关系不因新入口上线而批量改为Managed；其拖出按用户确认移动原对象，执行前展示源路径，快捷方式指向的程序不在移动范围内。

手势归同一意图仲裁：海报内拖动沿现布局/图钉合同；离开海报向Shell目标拖出才进入文件传输。一次按住应能完成接管，取消不遗留构图预览或执行文件动作；不得为外部拖放建立全桌面透明拦截层。无存储目标/无移动能力时显式拒绝或请求用户选择其他动作，不把Move静默降为Reference或Copy。

文件系统与SQLite不是一个原子事务。计划需记录源/目标身份、逐项完成、关系读集和恢复动作；执行前持久化意图，实际结果确认后才更新来源与移除本海报关联。共享来源的影响必须预演，不能使其他海报静默断链；外部移动成功而数据库未提交时进入可恢复状态，不能把失败强行回滚到可能已被占用的旧路径。

Shell原生拖出必须区分源与目标的执行/清理责任，不能看到MOVE反馈后再自行删除一次源。参考[Microsoft Shell拖放场景](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)；DesktopUp仍需证明实际完成、异步数据寿命及逐项结果，不能把DragEnd或返回效果单独当作成功。DeskBox的意图集中裁决可参考，但无目录退回Reference、无法Move退回Copy等策略不直接继承为本项目默认。

2026-10-10阶段映射：[第一批纯合同](../40_Implementation/V3-05/MANAGED_STORAGE_AND_DROP_CONTRACT.md)与[第二批账本](../40_Implementation/V3-05/MANAGED_TRANSFER_JOURNAL_AND_RECOVERY.md)保留模型及隔离证据；DEC-041已退役复杂任务UI，当前[直接拖放候选](../40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md)同卷接线及自动门完成／人工待验。[DeskBox参考](DESKBOX_COLLECTION_REFERENCE_V1.md)保留来源；排期与验收仍唯一归[工作流§6.5](../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径)。

## 7. 风格、内容参数、热度与 AI

V3-04-M05 先冻结 ContentMetrics 和 ThemeParameterSchema 最小合同：稳定 key、类型/单位、范围、默认值、作用域、版本、验证/迁移。控制中心仅绑定该合同，不把临时 accent/density 字段不断塞进 PosterDefinition。内置基线样式先提供可用通用 Box，完整主题平台仍在 V3-07。

每个 Poster 有独立模板实例和资源作用域；共享不可变模板/素材缓存，禁止全局替换 Application.Resources 污染所有海报。热更新先预备、验证，再切目标实例；损坏时保留原模板和版本。样式字典不是执行沙箱，受限代码另行安全门。

热度引擎可共享代码与真实打开事实，每海报拥有独立算法参数、模式、锚定和布局快照。游戏风格不是硬编码的六张卡片；通用文件格也不被强制自动排序。最终公式仍待产品门；打开手势按DEC-023、成功交接门按DEC-034，禁止UI临时裁决。

AIChangePlan 明确 targets、expected entity versions、允许操作、diff、确认与撤销。请求期间切换控制中心选择不改变执行目标。只经核心命令更新主题/布局，不赋予通用文件移动或删除权限。

### 7.1 控制中心宏观参数与定位（2026-09-15设计增量）

[V2-DEC-005](../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)由[首版可用性设计](CONTROL_CENTER_USABILITY_DESIGN_V1.md)承接：用户偏好→版本化ContentMetrics映射；每Item热度结果→独立相对层级/排序；二者与视口一起交给布局，不因UI简化永久绑定等尺寸格子。值按PosterId持久化，节点只为UI快捷值，未知参数版本不丢失。

撤掉中心桌面缩略画布；选择/定位提示是目标会话内有期限的视觉状态，不写业务revision、不创建新的窗口层或输入所有者。常用参数预览与自动提交由Application目标会话协调，App调用原呈现/恢复；命令仍走同一CAS/事务。几何查询使用真实有效快照，不将fallback当当前位置；只修改用户明确字段，但完整空间证明和原子提交不减弱。这些UX核心已接实现；各自代码/验证归C层，不从设计文字直接推断原生人工全部通过。

## 8. DeskBox 参考吸收清单

本次只读源码，参考 commit：64f27e1aa0f16867abe20074f35ea05cd7b12ce7。
根目录：G:\A_\Tools\reference\DeskBox。下列“吸收”是计划/设计，不等于已经复制代码或通过 DesktopUp 测试。

| 实际阅读来源 | 可吸收内容 | DesktopUp 必须补强/不照搬 | 对应任务 |
| --- | --- | --- | --- |
| src/DeskBox/Services/WidgetManager.cs | 按 ID 的窗口字典、创建服务、配置恢复；WidgetRemovalAction 区分移除/迁回/删除 | 不复制天气/音乐等无关组件；用 registry＋独立会话，文件删除须单独确认 | V3-04-M03、V3-05-12 |
| src/DeskBox/Services/WidgetSessionManager.cs | 显式交互深度与会话状态 | RaisedSession 不等于允许盖住普通程序；保持 DesktopUp 层级合同 | V3-04-M04 |
| src/DeskBox/Helpers/FileDropIntentPolicy.cs | Reference/Copy/Move/Shortcut/Reorder 意图与源能力、修饰键统一 | 目标海报/集合范围和用户文件 journal，跨海报关联不同于磁盘移动 | V3-05-07/08 |
| src/DeskBox/Helpers/NativeDropEffectPolicy.cs | 反馈与完成/源清理分离，Shell 应用和虚拟数据特殊处理 | 防双重删除、生命周期和失败恢复实测 | V3-05-07/13 |
| src/DeskBox/Services/ManagedStoragePathService.cs | 可预测的初始受管根路径、显式迁移建议、可移动盘/云风险 | 不照搬 DeskBox 用户目录；程序/缓存/用户内容分离，用户选址与断盘恢复 | V3-05-03/12、V3-09 |
| 既有 V3-02/03 provenance ledger | layer、drag/resize、窗口生命周期的已研究来源 | 保留已通过代码与基线；新增多 cluster 证据，不重做技术选型 | V3-04-M03/04 |
| WidgetSnapCalculator、WidgetCoordinatedMoveCalculator、WidgetManager.CoordinatedMove、WidgetWindowBase.CoordinatedMove | 纯吸附、组限界、批量预备/移动/保存、锁定过滤 | 组平移不等于硬碰撞连锁推让；详见空间设计§3，补扫掠与完整CAS | M04-02/03/05 |
| WidgetCompactExpansionCalculator、DirectionPolicy、WidgetWindowBase.Collapse | 候选锚点、严格尺寸拒绝、插值 | 不复制其Auto默认值/持久行为与RaiseForExpandedState抬层；按新合同做有最小阈值的临时高度、保存/有效尺寸分离及方向回退/归位因果 | M04-04/05及07 R2 |

实际移植前补读取来源文件/依赖/测试，登记精确 commit、移植片段、许可证、修改点和新增用例。不以 DeskBox 的测试结果当成本项目 PASS；此次未复制源码。

### 8.1 V3-05-01参考入口复核（2026-10-09）

用户再次明确：真实内容的关系、能力、合同、手势和边界优先参考本地DeskBox，不从零另造文件管理交互。已只读核对同一commit `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`，工作树干净；未运行DeskBox、未复制代码、未执行其测试。本节为具体来源映射，不改变DesktopUp已定产品规则。

以下路径相对 `G:\A_\Tools\reference\DeskBox`：

| 参考范围/已读入口 | 可吸收的实现事实 | DesktopUp适配与验证责任 |
| --- | --- | --- |
| Models/WidgetItem.cs、WidgetConfig.cs | 文件/目录/快捷方式元数据；映射路径、受管目录名和本地顺序 | 来源在src/DeskBox下。WidgetItem同时含BitmapImage/选择状态，不直接作为Domain实体；全局ContentItem、集合成员、海报内关系和临时UI状态仍须分开；不以路径直接替代稳定身份 |
| Models/FileWidgetInteractionState.cs、Helpers/FileDropIntentPolicy.cs | 选择命令状态与Reference/Copy/Move/Shortcut等意图；修饰键、源支持能力、虚拟临时数据强制复制 | 菜单启用不能只按选中数量推定真实文件权限；能力、可用性、集合所有权与运行中保护分别判定。海报内构图调整不等同磁盘移动 |
| Controls/WidgetContents/FileSurfaceContent.xaml及其SelectionAndMenus、KeyboardNavigation、Navigation部分 | 共享ItemClick/DoubleTapped/DragStarting入口、选择/键盘处理、文件夹导航与外部打开分发 | 复用Windows交互经验，保留DEC-023单击选择/双击或Enter打开、按住移动即拖；透明留白穿透、跨格布局与图钉仍走DesktopUp输入/CAS，不整搬其完整矩形控件 |
| Services/FileOpenRequestGate.cs、FileService.OpenItem.cs；Helpers/BoundedStaOperationRunner.cs | 打开去重、失败可重试；请求前快照；2并发/6排队/2秒排队准入；后台STA持有资源直至原生调用结束 | 这些数值是参考实现，不预先冻结为DesktopUp默认。原生调用开始后的取消不代表已终止；排队超时不代表Shell执行超时；ActivationId、启动参数/提供方身份和迟到结果要补合同 |
| Controls/WidgetContents/FileSurfaceContent.Opening.cs | 先显示打开反馈；generation/Dispose保护迟到UI；OpenedOrHandled只保证Windows接受或处理请求 | 失效快捷方式分支也可能返回Handled，不能一概记为真实打开成功/热度事件；01-C分清受理、已处理、成功与失败/取消的证据强度 |
| Services/FileService.cs、FileTransferSessionRegistry.cs | 枚举区分空/部分/不可用/拒绝访问；跨窗口传输状态按来源/目标保护，复制源可读、移动源与未完成目标受保护 | 暂时不可用不能当空目录清成员；内存会话登记不是持久恢复账本，05-08安全门保留 |
| Helpers/NativeDropEffectPolicy.cs | 拖放视觉反馈与完成效果分开；异步接收时不通过MOVE返回值再授权来源删除，避免双重源清理 | 05-07/08需完整验证目标接管、异步结果、源清理责任与故障补偿；不能照搬单一返回值而脱离整个协议 |
| tests/DeskBox.Tests/FileInteractionPolicyTests.cs、FileOpenInteractionContractTests.cs | 拖放策略/去重有可执行单元测试；打开接线部分以源码字符串断言检查 | 可作为反例来源；存在测试不等于已在本机运行，字符串断言不等于OS行为通过，仍需DesktopUp自己的纯合同与后续原生门 |

许可/来源核对：该快照的LICENSE_CHANGE.md和README声明GPL-3.0-only；DesktopUp现THIRD_PARTY_NOTICES.md已登记同一commit及许可，不是新的选型阻断。真实移植仍逐片段更新既有provenance并核对依赖、通知和改动，不以“参考成熟项目”省略登记；本节没有复制实现。

后续制作方式：每个05-01合同先列“DeskBox具体处理 → DesktopUp复用/调整原因 → 新增反例”，优先落最小可用合同，不因名称差异重新设计成熟的通用操作。Reference/Mapped/Managed与文件安全需求保持；跨海报关系、热度事实、单权威事务和恢复账本是必须补齐的本项目差异，不从参考测试自动继承PASS。

## 9. 历史缺口检查点（2026-09-07）

以下列表保留M02/M03当时的缺口与证据，不是当前任务清单。现多实例、空间/CAS/呈现、管理/恢复已按C层推进；05-01正式身份纯模型已完成，生产持久化、Shell打开、真实图标和磁盘执行仍由V3-05后续承接。原审计§14为实施前快照，新增范围见[05-01合同](../40_Implementation/V3-05/CONTENT_ENTITY_AND_OPEN_CONTRACT.md)。

1. M03 已去掉 App 单 _poster，PosterSessionRegistry 按 ID 消费集合投影；动态 Tray count 沿用 M02。
2. M03 已将实际0..N窗口及显隐/删除恢复改为 catalog 驱动，共享 Shell/显示调度；163/163 与真实0/1/3进程通过，短人工门待确认。
3. M02 已改生产命令为实体/必要读集 CAS（147/147 自动验证），保留 global-only 兼容入口；新版实际运行回归待完成，见 [实际合同](../40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md)。
4. 单窗口恢复回调、窗口层序和 input island 的跨实例身份。
5. 演示 Item、硬编码标题/参数和按换列重建 Grid，不能视为生产虚拟化完成。
6. 文件类型、文件归属、drop 完成语义、journal、真实 Shell 能力均未落地。
7. 主题实例/参数与 UI 草稿的范围、最小风格规格和热度/AI目标边界。
8. 实际产物开窗、多实例恢复、普通用户权限、文件失败矩阵与资源容量证据。
9. M04-01已补中心目录/独立选择/目标版本冻结，176/176与Release/实际3/0进程通过；[实际合同](../40_Implementation/V3-04/M04_CONTROL_CENTER_SELECTION.md) 的中心点击门待确认。用户既有基本多海报交互确认不替代新UI的人工结果。
10. 不可重叠、跨海报推让、条件归位和两阶段保尺寸展开均为新增设计；M03数据/会话证据不能替代M04空间矩阵。

验收与逐项推进见 [细化工作流](../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)。旧可视化图仅反映创建时的单海报代码快照，不用作新架构已实现证据。

## 10. 跨主机习惯配置（04-06合同与实现接线）

2026-09-16增量：A/B/C基础已实现，生产整合与D验证仍按[04-06专项](../30_Plans/V3_04_06_PORTABLE_PROFILE_PLAN.md)推进；本节不维护滚动测试数。用户已确认空间不足保留全部配置为待安置，不能以“导入失败”丢弃它们，也不能改尺寸/收展或推动既有海报。

### 10.1 权威与文件职责

保留 SQLite 作为运行时唯一 committed 状态权威。向用户提供一份可导入/导出的 PortableProfile 文件：导出从一致 revision 读取；导入解析为待确认变更，经同一应用命令/事务写入本机，再由普通投影恢复海报。文件在导入后是可再次应用的快照，不与运行数据库双写竞争；不自动监听任意外部文件或扫描当前目录自动覆盖。

日常位置/锁定/收起/主题参数保存仍自动提交本机权威库；用户显式导出时获得最新已提交配置，不包含未保存草稿。跨机目标是复用习惯，不是直接复制 SQLite 文件或将数据库内部 revision 当作远端可接受版本。

### 10.2 逻辑清单与机器差异

- 清单包含自身格式版本、应用兼容信息、海报条目、逐张几何意图/状态/层序、模板 key/version/有效参数、来源依赖以及可迁移的内容/热度偏好。
- 海报数量由清单条目确定。GUID/关联在导入计划中保持或统一映射；与目标机现有身份冲突时禁止静默覆盖。原 HWND、generation、原库 CAS revision、缓存、Key 不进入可应用字段。
- 位置携带逻辑尺寸/相对工作区锚点和显示器匹配提示；目标机重新评估显示环境，重投影并限于可见区域，导入预览显示映射结果。
- 内容源只迁移显式选择的关联/依赖信息，不迁移实际游戏、安装目录或用户文件；新机来源缺失保留待关联状态。路径隐私、符号位置映射在来源合同进入后补齐，不写六张假 Item 充数。
- 内置模板/参数属于 V3-04-06 的基础配置迁移；自定义素材/模板包复用 V3-07-04 的安全导入/打包能力，向用户仍交付一份携带文件。缺失时保留原模板/参数，以最近可用或 minimal-fallback 呈现。

### 10.3 导入和失败合同

格式/容量/版本检查 → 依赖与屏幕映射 → 影响预览 → 用户明确确认 → 目标版本复核及本机备份 → 一次状态提交 → 会话/投影应用与结果摘要。已有布局上的替换/追加策略必须在 04-06-A 冻结并在预览明确，不能由解析器默认清空本机配置。替换若会移除海报也要显示相同删除后果；不包含文件删除。

未知不兼容格式、非法几何/参数、冲突、备份失败均不应用；部分主题/来源依赖缺失可由用户确认以降级方式应用，但完整保留依赖描述。主题资源预备/发布复用主题安装流程，不能以数据库事务冒充文件系统原子回滚。

04-06-A基础文件冻结为DesktopUp.PortableProfile/version1可读JSON：4MiB/128海报/深度32/单参数32KiB/最大逻辑尺寸32768DIP的输入防护上限；默认追加，明确选择替换，所有导入项分配新本机身份。全局偏好另行勾选才应用，自启动注册意图保持本机。未知必需版本拒绝而不忽略；自定义素材容器和真实内容来源仍由07-04/V3-05扩展，不将当前六卡视为内容迁移。

schema9的PlacementPending是本机待安置原因，与IsVisible用户意图分开；投影、空间占位、会话创建/恢复、自动收展均以有效可见状态判断。未能安置仍保留配置并在控制中心列出，专用重试按完整CAS一次提交，普通“显示”不能绕过安全落位。

内部灾难恢复另用StateBackupRecovery：存储关闭/未初始化、唯一实例保护、暂存验证/必要迁移、原DB与sidecar归档、持久journal及原子发布/补偿；不得在线覆盖SQLite文件或在损坏时静默生成默认。此前仅有备份校验，无可用Restore，本轮补齐实现；最终生命周期和用户入口验收以[交付](../40_Implementation/V3-04/PORTABLE_PROFILE_DELIVERY.md)为准。

# V3-06 真实热度：开发准备与实施记录

## 2026-10-26 功能分支检查点（已推送，未合并）

- 用户明确授权同步当前分支，禁止合入`impl/develop`。功能提交`8156b7388b16e61695800745c058eb1d10b0e128`已推送至`origin/impl/06-real-activity`并设置上游，包含真实热度、实时更新、桌面标准尺度及配套源码/测试/文档。当前分支继续保留，未创建PR或发布。
- 直连GitHub两次失败后，使用Windows已启用的本机代理完成fetch、push及`ls-remote`核对；代理只对命令生效，未改Git持久配置。核对时远端功能分支为上述提交，开发线仍`d92266986083fc6f25af1c6acdbc0a6eb774c7e4`、发布线仍`ecb0c04834ef2238252bd89fa77b29837466c2dd`，本地对应分支同样未动。
- 本轮无产品源码变更；复用下文同一源码的构建、定向和原生结果，仅复查暂存差异、源码文本门（1005文件/27冻结迁移）和交接。构建输出、数据库、体验配置及原始日志留在本机，不上传、不清理。
- 此为源码保存检查点，不是V3-06整体验收：实时更新已有正面反馈，新尺度与空间组合集中体验仍待确认；历史未验环境和故障携带。同步后的冷启动/计划以本节为准，下方“未提交/远端未确认”是此前时间点记录。下一步继续本分支体验收口，未经后续授权不集成develop或发布main。

## 当前状态（2026-10-25）

**IN PROGRESS / 待集中人工体验：真实统计、连续策略、空间限幅/扩框及海报级配置已接线；用户认可实时更新效果，随后要求整体内容尺度改为0.4～1.8，1×以桌面图标48 DIP校准，热度独立叠加。该调整已实现并通过定向、原生和UI检查，新候选已打开供体验；不是V3-06整体验收或合并许可。** 当前分支为`impl/06-real-activity`，基线`d92266986083fc6f25af1c6acdbc0a6eb774c7e4`。本功能的详细进度和验证集中在本文件，不另建平行实施计划。

2026-10-25用户要求成功打开后实时入队更新，取代昨日“等下次展开”的发布节奏。源码已接入统计提交通知、按当前内容归属刷新及每海报单计算/合并待更新；相关19项定向测试和原生候选验证通过，用户随后反馈“效果不错”。下方2026-10-24收展验证仅证明旧节奏，不作为实时更新通过证据。最新运行候选及尺度验证见“桌面标准内容尺度”节，旧候选路径与哈希仅供追溯。

- `HEAD`、本地`impl/develop`及现有`origin/impl/develop`跟踪引用均为上述提交；本轮fetch遇到远端连接重置，**未重新确认远端实时HEAD**。按用户指定的当前本地基线创建分支，没有改动develop/main指针。
- 分支切换前后的22份现有文档改动逐文件SHA256一致，未暂存、清空或覆盖。文档审计、热度需求修订随工作区带到新分支；尚未提交/推送，不能认为仅靠本地分支名已把这些文件保存到Git历史。
- 开工基线schema26；本批新增027统计迁移，001～026未改，027登记后同样冻结。schema26升级、回放、事务失败与幂等定向验证通过。用户真实配置尚未启动或迁移；不能把隔离测试等同真实环境体验。

## 权威入口与范围

按[需求§10.4](../../10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md#104-独立倍率合成与空间上限2026-10-24用户确认未实施) → [统一空间合同](../../20_Architecture/CONTENT_LAYOUT_OPERATION_SPACE_REVIEW_V1.md#2026-10-24-增量合同热度限幅与统一空间规划未实施) → [工作流§7.1～7.2](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#71-2026-10-21-真实热度准备未实施)执行；[实施验证规则](../../../Rules/IMPLEMENTATION_AND_VALIDATION.md)和[Git流程](../../../Rules/GIT_STRATEGY.md)继续适用。

已确认的方向不再重复询问：整体内容倍率与Item热度独立合成；手动整体尺度受阻回弹；热度先尝试必要扩框、受阻按现框限幅；用户resize重算显示上限，同一目标不反向撑回；下限沿内容/热度设置及可读底线。手动构图/尺寸覆盖、顺位图钉、单一归属、可信成功事实和文件安全保护保持。

用户随后明确要求按计划开始推进，并确认衰减、身份与可配置低样本平滑。现授权覆盖本功能实施与必要隔离验证；不清理用户数据、不自动发布或合回develop，已有记录不构成推送权限。

## 实现入口与里程碑

| 既有任务 | 实现入口与准备结论 | 里程碑出口 |
| --- | --- | --- |
| 06-05/06-06：规则与统计闭环 | `Application/Features/Content/Open/ContentActionService.cs`、`ContentOpenContract.cs`及`Infrastructure/Persistence/Content/SqliteContentOpenJournal.cs`已有可信成功事实；`Application/Ports/Content/IContentOpenJournal.cs`仅提供打开准入/完成，没有统计消费端口 | 明确统计身份与消费位置，增量幂等聚合可查询；聚合/消费标记原子提交，重启重放不重复计数、不重放真实打开；记录新增读写/等待成本 |
| 06-06/06-07：连续策略与限幅 | `Domain/ContentLayoutProposals.cs`仍有旧五档；复用纯布局与尺寸基础，分开作者基准、热度目标和空间有效尺寸 | 已确认公式样例通过，输出稳定排序/连续目标；限幅保持比例、可读下限和人工覆盖，不累乘；外框增减可恢复/限制目标，无反馈撑框 |
| 06-08/06-10及必要06-09：接入会话 | `Application/Features/Content/Layout/ContentHeatSession.cs`、`PosterContentSession.cs`与`App/Views/Poster/PosterVisualWindow.ContentProposals.cs`已有提案/保护；`ContentScaleExpansion.cs`及`ContentFrameMinimumPolicy.cs`需按新合同扩展，不能直接套用滑块入口或旧有效尺寸下限 | 可信事实→统计→策略→安全时点发布完整连通；扩框/限幅/resize候选及绘制命中同代，旧提案失效，取消/失败恢复；两组集中体验完成后再按授权集成 |

表内省略的源码前缀为`src/DesktopUp.`；详细现有路径见[代码锚点](../V3-04/CODE_ANCHORS.md#2026-10-24-当前源码与热度接入锚点)。应用用例组织消费与发布，Domain保持纯规则，Infrastructure负责事务，窗口只承接交互/呈现；沿现有所有者开发，不另建打开路径、布局引擎或统计数据库。

表格记录开工入口与出口，实际完成情况以下节为准。027建立显式递增消费序列和事务游标，不依赖事实表隐式rowid；策略版本与真实事实分开，不因换算法清空历史。

## 已实现的生产链路

- `ContentActionService`在成功事实持久化后通知`ContentActivityConsumer`，失败/定位/扫描不记次数。`SqliteContentActivityStore`每批最多128条，累计/最近时间/游标/移出待消费队列同一事务，最多保留每对象32条最近记录；重复完成与重启不会重复计数，也不重新打开文件。消费失败不回滚真实成功事实，失败期间到来的新信号不会丢失。
- 身份按`ContentItemId`；跨海报同对象保留，新海报重新比较。`DirectTransferPreparation`只有对象仍有有效卡片归属时才复用旧身份，已经移除的同路径对象重新加入不会冒领旧热度。旧库没有加入时间，027以迁移时刻建立保护窗，原成功事实保留并增量消费。
- `Domain/Content/ContentActivity.cs`实现已确认的衰减、40/35/25评分和可配置平滑；`ContentActivityProducer`从一个已提交成员快照读取统计，输出连续提案v2。旧注入提案v1仍走旧兼容路径，不作为生产热度。
- `ContentHeatSizing`每次从作者基准派生，以共同倍率保持宽高比例；下限取配置/48 DIP可读底线，上限受参数/当前交叉轴限制。`ContentHeatExpansion`复用`ContentScaleExpansion.PlanToMinimum`与已有原生事务，空间允许才在原点扩框，不推动邻居。手动整体尺度按未限幅热度目标检验，受阻仍回弹；用户resize只重投影已接受目标，不反向扩框/不重评排名。作者布局、统计与目标不被限幅结果覆盖。
- `ContentActivityConsumer.StatisticsChanged`只在统计事务提交且内容有变化时通知；App调度到UI线程，`PosterSpatialRuntime.NotifyActivityStatisticsChanged`按ContentItemId的当前归属唤醒相关海报。空批次不通知，通知失败不回滚统计或重放打开。
- `PosterVisualWindow.Activity`在统计变化、首屏后、展开或明确更改热度/模式设置时请求计算；无交互保护时立即异步更新，无鼠标离开/收展前置。每海报最多一个计算在途，合并最新待更新；交互保护和空间动画结束后自动续作。ready携带实体版本和桌面/拓扑代际，旧几何失效时用最新快照重算既有请求，迟到回调不覆盖新意图；无新统计时resize仍只重投影当前目标。
- 控制中心“行为→热度设置”提供当前海报的动态尺寸开关、最小/最大/默认倍率和平滑强度K。初值0.5/3/1.5/20；范围及关系见需求§10.1，K=0关闭平滑。沿现有中心草稿保护与CAS提交；非法值/版本冲突保留输入并报错，取消不写入。已有模式/开关/手工尺寸不按模板重置，图钉只约束顺序。

当前完成的是上述源码和定向验证，不包含临时排名奖励、主动刷新菜单、动态代码插件、在线AI或完整长时性能认证。统计共用现有库，无每海报新增高频timer；实际锁等待/大集合性能尚未量化，不宣称性能优化已验收。

## 产品样例与确认状态

2026-10-24已通过本轮问答补齐前三项，当前按如下决定制作：

1. 已确认：需求§8.5连续保护公式，H=7+21×min(1,ln(1+N)/ln51)²，D=max(0,min(A−30,I−14))，E=N×max(0.1,2^(−D/H))；重新成功打开恢复新的累计次数。
2. 已确认：需求§10.1低样本平滑，默认0.5～3/无数据1.5/K=20；参数可配置，保持海报级作用域，与整体内容尺度分开。2026-10-25整体尺度上限从3改为1.8，不改变热度参数。
3. 已确认：ContentItemId身份跟随同对象转移，在新海报重新比较；移除后重加不按相同路径继承。
4. 各模板自动/手动及动态缩放默认值；已有用户显式设置不静默重置。临时排名奖励、刷新菜单和动态代码加载不自动加入首批。

## 2026-10-25 实时入队更新：结果与候选

- 用户明确成功打开后立即记录并加入更新队列；无收展、鼠标离开或固定延迟前置。统计通知只带真实内容身份，在UI线程按当前归属定位海报；单计算在途＋最新待更新，配置/几何变化淘汰旧结果，受保护操作释放自动续作，无周期轮询。公式、手工模式、图钉、尺寸上下限和空间扩框规则保持。
- 仓库根、项目SDK10.0.112，`dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --no-restore -m:1 -nr:false --filter "FullyQualifiedName~ContentActivityConsumerTests|FullyQualifiedName~ContentActivityPersistenceTests|FullyQualifiedName~ContentActionServiceTests|FullyQualifiedName~PosterContentSessionTests"`：**19/19 PASS**，结果`out/activity-validation/activity-realtime-contracts.trx`。验证提交后通知、消费失败不通知/恢复后通知、空批次不循环、观察者异常不重复统计，以及既有交互保护/释放；与旧211项有重叠，不相加声称新总数。
- App Release/x64 review构建0警告/0错误，文本门1005文件/27冻结迁移通过。候选`out/activity-validation/realtime-candidate`，`DesktopUp.dll` SHA256 `B5EC9E0833A16B751B2895C6C3AD839DEB76943F23A4E47FA6B7F3E5C6373FB1`。
- `out/drop-d3/activity-realtime-20261025-01`首次探针误把进入编辑模式自身的边框/视口变化当作热度重排；统计通知期间无新计算，修正探针为“进入编辑后的几何”作为基线，未因此放宽产品保护。失败日志保留，进程退出资源0。
- `out/drop-d3/activity-realtime-20261025-02/result.json`及profile日志：**原生PASS**，PID39744、退出0、资源0。生产打开服务＋模拟worker成功响应，3次成功/3次重复只调用worker并累计3次；编辑保护期间不重排，结束编辑后无需收展由版本4→8，hot/cold独立排序和连续尺寸；普通状态再成功打开一次直接更新到版本10、累计4次。CanvasMatches/InputMatches/Clipped均true、native handle保持、作者配置SQLite重读一致、自有引用内容哈希不变。通知只导致受影响海报重算。该探针不操作真实鼠标、不启动外部程序，不等同用户点击验收。
- 人工候选沿用`out/drop-d3/activity-20261024-02/profile`，保留此前海报/配置和样例；不切换到本轮自动探针的新数据。只需继续原有两组集中体验：成功打开后自动变化（不必收展）；空间限幅与人工覆盖。公式/配置UI未改，复用上一轮相关结果，不重复全量、打包或实际安装；真实配置/收纳内容未动，未提交/合并/推送。

## 2026-10-25 桌面标准内容尺度：结果与当前候选

- 用户确认：整体内容尺度0.4～1.8；热度关闭时的1×对齐桌面图标，热度继续独立叠加。本机只读桌面视图配置`Shell/Bags/1/Desktop/IconSize=48`，不是`WindowMetrics/ShellIconSize=32`。产品以48 DIP为标准图像区域；实际不透明图案受源图留白影响，不承诺每个图案都填满48。精确校准条件为标准4×4跨度、标准疏密1及无热度/手工尺寸覆盖。
- 实现：`Domain/PosterContentPreferences.cs`的`BaseMetricsScale`按48 DIP图标、标签及内边距校准整个网格；`ContentLayoutViewport.WithPresentation`将其统一传入求解、编辑、热度投影、最小外框和扩框判断，布局快照/重裁切/会话缓存同时比较该因子。作者跨度和基础网格数据保持，绘制与命中使用同代几何；不只把图片缩小而留下旧占位。0.4仍受既有可读底线约束，热度参数与人工覆盖不被重置。
- 兼容：preferences映射升级至2，SQLite仍为27。旧映射1的合法0.4～3值可读，超过新上限时在内存规范到1.8；原始JSON保留至正常用户提交。未知版本或非法值仍只读兜底，既有模板/自定义内容/热度配置不丢失。需求与交接入口已同步，V3-04旧尺度范围明确标为历史。
- 执行目录仓库根，项目SDK10.0.112，Release/x64、`--no-restore -m:1 -nr:false`。`out/activity-validation/content-standard-scale.trx`覆盖`UxContentPreferenceTests|ContentContinuousSizeTests|ContentFrameResizeTests|ContentLayoutPersistenceTests|ContentActivitySizingTests|ContentHeatSessionTests|ContentPresentationLayoutTests|PosterPresentationParameterTests`，初次182通过/2失败；失败为`ContentContinuousSizeTests`仍以240表示旧主轴最小值，已按现行可读下限几何修正断言，未修改生产下限规则迁就测试。保留首次失败记录。
- `out/activity-validation/content-standard-geometry.trx`覆盖`ContentContinuousSizeTests|ContentLayoutSolverTests|ContentLayoutR4EditingTests|ContentDragPolicyTests|ContentManualProjectionTests|ContentSpacePolicyTests|ContentSpacePublicationTests|ContentLayoutTransitionTransactionTests|ContentResourceDemandTests`，179/179通过，包含上述修正用例。两批按测试身份去重、后次覆盖前次结果为357项全部通过；不是额外新增357项，也不与旧211/19相加。未跑全量。
- App Release/x64 review构建0警告/0错误，源码文本门1005文件/27冻结迁移通过。**当前候选**`out/activity-validation/desktop-scale-candidate/DesktopUp.exe`，`DesktopUp.dll` SHA256 `763D51B58BD6F2C7F7A4BBF1E6E21C8908F1970CCEBAC63EABD9E18A0D46DDB3`。
- `out/drop-d3/activity-desktop-scale-20261025-01/result.json`：原生隔离PASS，PID31252、退出0、资源0，实际呈现器读回标准图像区域48 DIP、整体范围0.4～1.8。3次成功/3次重复仅累计3次，编辑保护后版本4→8，无需收展；普通状态再成功打开更新至10。CanvasMatches/InputMatches/Clipped均true，作者配置重读和自有引用文件哈希保持。提供方为模拟worker成功响应，生产打开/统计/布局链路真实；不是实际鼠标或外部程序打开验收。
- 用户退出旧实例后，新候选沿用`out/drop-d3/activity-20261024-02/profile`启动控制中心，PID36920。实际UI将B“普通收纳”整体尺度依次调至1.8、0.4、恢复1.0，读回标签正确并显示“已保存 · 仅当前海报”；B热度关闭，便于对比标准大小，A原有独立尺度/热度设置保留。新窗口继续留供体验，无未完成草稿；用户真实配置与收纳文件未动。
- 本批未新增跨屏DPI、长时负载或打包安装验证；不宣称这些范围通过。下一步只需集中体验标准大小及整体调节与热度的独立性，既有空间/人工覆盖未完成验收仍保留；未提交、合并或推送。

## 验证准备与结果

此前分支准备未改源码、未执行构建。以下环境准备记录保留；本批结果见下方，不把旧准备检查算作产品通过。未操作用户真实配置/收纳文件。

- 分支切换和基线核对已完成；切换前后22份工作区文档哈希一致。
- 在仓库根执行`scripts/setup.ps1 -CheckOnly`：项目SDK存在；`out/toolchains/dotnet-10.0.112/dotnet.exe --version`返回`10.0.112`。此为工具存在/可执行检查，不等于NuGet依赖完整或构建通过。
- 准备时项目NSIS缓存不存在；本轮不打包。缺少的测试NuGet依赖已从正式源恢复至项目缓存，没有安装全局工具。
- 文档检查使用本地链接/章节、严格UTF-8及`git diff --check`；构建、定向测试和原生验证留到实际代码变更。

后续优先复用`tests/DesktopUp.Contracts.Tests`中的`RealContentOpenTests`、`ContentHeatSessionTests`、`ContentContinuousSizeTests`、`ContentFrameResizeTests`，以及`ContentScaleExpansionTests.cs`所含的`ContentLayoutPersistenceTests`。新增测试围绕真实统计幂等/重启、公式边界、两锁定邻居与工作区、热度受阻限幅、主动缩框不撑回、人工覆盖、过期空间和同代命中；不按类机械增测，不默认跑全量。

集中体验依工作流两组：统计与冷热变化；空间限幅/外框变化及模式保护。历史卸载/升级41/P1与R6锁冲突、真实登录和联合长测等仍携带，热度准备不关闭这些问题。

### 2026-10-24 本批自动结果与未验项

执行目录为仓库根；使用`scripts/toolchain.ps1`项目SDK10.0.112，Release/x64、`-m:1 -nr:false`及离线NuGet配置。测试需要本机VSTest IPC，普通沙箱连接测试宿主超时后在授权范围运行隔离测试。

- `out/activity-validation/activity-final-contracts.trx`：**123/123 PASS**。过滤`ContentActivity|ContentHeatSession|ContentLayoutPersistence|ContentFrameMigration|MigrationFingerprint|SqliteStateStore|AutoPostureSettingsMigration`；覆盖公式、极低样本浮点稳定性、参数编辑/持久化、消费失败与重启、身份、schema26→27、连续尺寸/两锁定邻居与过期证明。
- `out/activity-validation/activity-adjacent-contracts.trx`：**88/88 PASS**。过滤`Manual_scale_uses_uncapped_heat_target|RealContentOpenTests|ContentFrameResizeTests|ArchitectureTests`，补验带热度的手动尺度不借限幅吞掉受阻、可信打开边界及模块依赖。未跑全量。
- App review候选和SessionFixture均Release构建成功，0警告/0错误；候选在`out/activity-validation/candidate`。源码文本门1005文件、27份冻结迁移通过。`out/activity-validation/checkout-line-endings.json`记录本地191个原本CRLF检出文件：仅当换为LF后的Git blob与索引一致才处理，无语义改写，不修改历史SQL指纹。
- `scripts/check-architecture.ps1 -Offline`通过：生产111份编译源、0份review源；review增加71份探针，生产源集合不变；预览包禁止review的保护有效。当前热度入口的本地文件链接、严格UTF-8与`git diff --check`通过；代码锚点历史段保留原提交路径和本机不存在的旧out证据，不把旧链接检查当成本批源码缺失。
- 早期定向失败已针对修复：极小对数除零、迁移测试把新增统计表误算旧业务表、数组引用比较、System扩框不能走普通命令入口；以上最终结果包含修正后的相关用例。原失败TRX保留，不删失败证据。
- 用户正常退出安装版后，`--real-activity-probe`首次在`out/drop-d3/activity-20261024-01/profile`执行，保存参数后未唤醒刷新，等待首次生产结果超时；退出资源0。已将`NotifyActivitySafePoint`补到`CommitContentDocument`公共保存入口，覆盖直接编辑/撤销后配置变化，不依赖中心界面释放草稿的偶然通知。保留首次日志，不覆写失败记录。
- 修复后`out/drop-d3/activity-20261024-02/profile/logs/desktopup.jsonl`记录`contract.real_activity_runtime_passed`：3次可信成功事实（提供方确认由journal模拟，无外部程序打开）及3次重复完成仅累计3次；打开后画面不变，普通收起→展开从版本4更新至6，hot/cold连续尺寸独立；CanvasMatches/InputMatches/Clipped均true，保留原native handle，作者尺寸和设置SQLite重读一致，两个自有引用文件哈希不变。进程退出码0、资源0。生产计算完整异步耗时本次1～5项为约8.4～22.3ms（含消费等待/读取/计算，不是纯公式基准或锁等待认证）。
- 同02配置再次正常启动控制中心，通过computer-use读取重启后的0.75/2.5/1.25/K40参数；非法最小倍率9被阻止，取消后重开仍为0.75；通过实际保存按钮把K改为25，显示成功且产生新提案。短窗口内错误文字原位于滚动区下方，已补`ShowError`滚动到错误处；这次正常退出资源0。
- 最终UI修订Release构建0警告/0错误。再次启动同02配置，实际窗口显示A仍为0.75/2.5/1.25/K25；输入K=-1保存被拒，滚动区自动到91，截图确认错误文字可见，取消后不保存。B仍为0.5/3/1.5/K20、动态尺寸关闭，海报间配置不串。两个引用样例再次SHA256核对不变。当前为可供集中体验的隔离候选，不是USER PASS。
- 当前候选`out/activity-validation/candidate/DesktopUp.exe`，最终`DesktopUp.dll` SHA256为`0BD37E93C63AAD61B2BF144985D65BEAA68A3FAED316299FA0541EFE26E36E1A`；数据`out/drop-d3/activity-20261024-02/profile`。原生探针后的最后修改仅错误提示可见性，未变更统计/策略/空间路径，因此复用同批定向和原生结果，补实际UI与重启检查，不重复无关矩阵。体验窗口保留，无未完成参数草稿；测试文件全部位于本批out目录，真实配置/收纳文件未动。
- 待用户集中体验两组：①有效打开后不立即跳位，收起再展开后的冷热变化及参数保存；②开启动态尺寸时缩框/扩框后的限幅与恢复，手工尺寸/手动模式/图钉保持原含义。自动两邻居模型结果不等同真实拖框组合体验；原生失败注入、跨屏DPI和长时负载仍未验。未获集中体验确认不合入develop或发布。

# 架构整理 C：热度接入所需的海报运行边界

2026-10-16；[B批](B_CONTENT_USE_CASES.md)及本批集中人工均USER PASS。本轮用户授权C收口并将架构成果本地合入impl/develop；不推送、不更新impl/main。集成结果见§6。

## 1. 本批最小边界

原PosterVisualWindow.ContentProposals同时持有热度缓冲、已接受热度、派生文档与尺寸档位，并负责Dispatcher/交互保护/绘制。此次抽出Application中的ContentHeatSession作为一个海报可视会话内唯一的热度状态所有者；它不持有窗口、存储、Dispatcher、计时器或图片缓存。

- Session负责：latest-wins缓冲、按原作者布局派生、档位沿用、版本/内容集合失效、空间校验后激活、失败保留上一有效状态、关闭后拒收。复用Domain既有提案和求解算法，不复制规则。
- 窗口负责：既有交互安全点、Dispatcher唤醒及epoch失效；主题提案暂不抽取；把当前作者布局/视口交给Session，将结果沿原RefreshContentSurface路径发布。
- 呈现/输入负责：继续使用已有同一布局快照与ContentSpacePublication、稳定卡片/图标缓存。没有重建另一套Presenter或第二份命中几何。
- 手动/草稿优先次序保持；临时成员变化只试算、不清除原有效热度；真实成员提交后才失效；不把热度派生布局写回作者配置。

本批不制作真实热度统计或消费、不改变“打开后标记待更新、下次展开/明确刷新发布”的未来产品节奏。现有注入提案仍按原安全点应用；安全点不等于未来热度发布时机。新算法、异形模板、AI、完整SessionController/Presenter拆分均不在本批范围。

## 2. 连续实施与精简验证

记录B验收 → 抽运行状态/共享投影校验 → 保留窗口保护/发布接线 → 复用原测试、补会话边界 → 一条隔离同帧/安全点冒烟 → 集中人工。不拆内部步骤验收。

只检查本次提案/运行边界：缓冲最新版本、保护期不重排、作者变更不叠乘、试算失败不污染、成员集失效/关闭、视口不足保留有效状态；原手动尺寸/图钉规则复用原提案测试。集成检查保护期和解除保护后画面/命中同帧、卡片对象保留、作者配置不变。不开全量、安装卸载、打开事实或整套拖放矩阵。

人工只检查一次连续使用：隔离样例内部拖动/框选后，收起再展开，确认无闪烁或误命中；不要求用户验证尚未实现的真实热度变化。旧包与真实配置不动，样例全在out。

## 3. 当前状态

**USER PASS / C批CLOSED**。2026-10-16用户确认“ok,修复好了”，收展闪烁复验通过；此前其他C操作已确认无问题，A/B/C限定范围全部收口。下方初版与修订自动证据保留；本轮只更新记录和本地Git集成，不改行为、不重新累计回归。真实热度统计尚未实现，既有自启登录/长测等未验不补PASS。

### 实施

- [ContentHeatSession](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentHeatSession.cs)：唯一持有热度缓冲、已接受提案、派生文档/档位；Reconcile、ProjectEdit、ApplyPending、Close表达明确的生命周期，不需要WinUI或存储。
- [ContentProjectionValidation](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentProjectionValidation.cs)：原主动投影空间校验提为公共纯方法，窗口预览和Session共用原求解器。
- [窗口提案适配](../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)：只保留宿主安全点/Dispatcher/主题提案，热度结果交回原RefreshContentSurface；关闭仍先使排队epoch失效，再关闭Session。
- PosterVisualWindow.Content.cs仅将原作者文档变更通知接到Session；原卡片绘制、图标缓存、原生输入、拖放、框选、同帧发布与SQLite均未修改。三个原探针读数改读Session，不改变旧验证断言。
- 新增一条显式ReviewTools限定的聚焦集成检查，复用现有真实样例、画面/命中读数及卡片/位图身份校验；不重跑整套内容布局/拖放矩阵。

关于“保持”：新提案空间校验失败会消费该版本并保留原有效投影；已提交作者布局变化但无法派生时，按旧行为清除派生投影/档位、保留已接受提案供后续重算。临时成员预览不重置状态，成员实际提交才失效；关闭是终态，迟到调用不能重新激活。这里没有新增实时评分或发布时机。

### 验证

| 范围 | 结果/证据 |
| --- | --- |
| 定向测试 | 34通过：6个Session边界、25个原热度/主题提案规则、3个结构边界。过滤ContentHeatSessionTests、ContentLayoutProposalTests、HeatRuntimeOwns、CoreProjectsDoNotReference；out/architecture-runtime/tests/heat-runtime.trx |
| 构建 | scripts/build.ps1 -Offline（product01）及同入口-ReviewTools（review01），均0警告/0错误；日志out/architecture-runtime/product-build.log、review-build.log |
| 聚焦集成 | scripts/smoke-content-heat-runtime.ps1，out/drop-d3/architecture-runtime-smoke01/result.json：保护期几何不变、最新版本2生效、绘制/命中一致、卡片/位图及缓存保留、版本/集合拒绝、空提案恢复作者布局、不落库；PID33360，exitCode=0，activeResources=0 |
| 编译边界 | check-architecture.ps1 -Offline：产品99源/0验收源，Review165源/66验收源，生产源集合一致，Preview+Review互斥通过 |
| 候选一致性 | 产品与Review的DesktopUp.Application.dll SHA256相同；App差别仅显式验收编译内容。人工使用不含探针的product01 |
| 环境限制 | 自动集成为高权限工具上下文，nativeMouse/nativeKeyboard=false；不是普通权限原生手势或用户视觉PASS。不运行累计全量、安装/卸载、B打开事实回归或长测 |

最终产品DesktopUp.dll：7EF4FE72CE59C4A0B5B89B3071C0DD667C5A68739140828E13876AEED35D71B0；
DesktopUp.Application.dll：7DAA66CBF05601F85286C1B1D09FA65E5EF34FDA63350D29F84F9C0FF39BAF0D。

### 初版集中人工（历史，PID28440已由用户正常关闭）

入口：[start-content-runtime-review.ps1](../../../../scripts/start-content-runtime-review.ps1)，已在Windows PowerShell 5.1准备并打开普通产品候选，PID28440（仅当次启动身份）。配置/样例全在out/drop-d3/architecture-runtime-review01，旧A/B包及配置保留；无桌面样例、无安装/自启登记，无需收尾用户目录。

只验一次连续操作：在“A · 普通收纳”内部拖动一个卡片、在空白处框选，然后收起再展开。预期其余图标不闪烁/不重新加载，选中/拖动命中正确，内容和手动调整保持。无需再次打开/定位或测试跨海报文件移动，也不要把样例拖到真实桌面。

用户反馈：其他操作没有问题，但展开时真实图标暂变FILE/DIR，此项修复后再复验，不重复其他人工。完成后先展示§5计划，不自动开始下一批。

## 4. 展开图标闪烁修订（2026-10-16）

根因不是热度Session：原SyncContentAssets将IsCollapsed和呈现暂停合并调用ReleaseContentAssets，收起即销毁缓存/解码位图；展开准备树期间又暂停加载，故显示FILE/DIR后才补回图标。此前热度集成没有覆盖收展，不作为此问题已通过的证据。

本次仅改ContentAssets的收起分支：SetVisible([])取消不可见请求，保留同一个有界缓存及最后最多64项解码图像；展开准备树立即复用已接受图像。没有常驻全量加载或新增并发池，字节缓存仍有原16 MiB上限。来源/成员变化继续按RasterKey代次淘汰；真正暂停、换源和窗口销毁仍完整释放。首次打开或确实变化的图标仍正常异步加载，不承诺全场景零加载。

| 范围 | 结果与证据 |
| --- | --- |
| 先复现 | 聚焦检查配旧生产逻辑，首轮收起失败：Posture discarded or re-requested warm rasters；out/drop-d3/architecture-posture-repro01，进程正常退出、activeResources=0 |
| 定向测试 | ContentAssetCacheTests共24项通过，仅新增暖缓存收起/复用/释放用例，其余取消、迟到、并发和容量规则复用；out/architecture-posture/tests/posture-cache.trx |
| 修订集成 | 原smoke-content-heat-runtime.ps1新增-PostureOnly，不重复整组热度检查。真实SetCollapsed命令连续6次收展，276个合成回调（216在过渡期），3个位图身份保持、额外请求0；PID43460，exitCode=0，activeResources=0。out/drop-d3/architecture-posture-smoke01/result.json |
| 构建 | out/architecture-posture/review01及product01均0警告/0错误，product不含验收探针；同目录review-build.log、product-build.log |
| 环境限制 | 自动为高权限工具上下文，检查实际XAML图像绑定与合成回调，不注入鼠标/键盘，不等于用户视觉验收；未跑累计全量、安装卸载、长测 |

当前脚本仍为scripts/start-content-runtime-review.ps1，已改指独立修订包out/architecture-posture/product01；DesktopUp.dll SHA256=A440176EE5E58232B254BF87F4BC8B97A44DAF30B4137DF2495728B16F0872CE；Application.dll保持7DAA66CBF05601F85286C1B1D09FA65E5EF34FDA63350D29F84F9C0FF39BAF0D。独立数据out/drop-d3/architecture-posture-review01，旧C配置/包及用户文件不动，无桌面样例或注册表修改。

2026-10-16人工复验USER PASS：用户确认修订后收起/展开不再闪成FILE/DIR，本批收口。其他已确认操作不重测。验收候选为上述product01，原启动PID52956只作身份记录，旧包/配置/真实文件均保留。

## 5. 后续系列计划（第1项本轮执行，其余未开始）

1. C已收口；本轮按授权执行本地Git集成，将A/B/C与修订合入impl/develop，保留功能分支；本轮不推送或同步发布线。
2. D/V3-06-05～06：补成功打开事实的幂等消费、稳定游标与统计持久化；冻结30天新项保护及最近性/频率公式和样例，再实现默认策略。重启只补算事实，不重放打开或文件操作。
3. V3-06-07～10：真实评分接既有ContentHeatProposal/Session，补尺寸范围与用户上下限；保持手动模式、图钉、手动尺寸优先。打开只标记待更新，在下次展开/明确刷新发布，不在操作中跳位；不重造已存在的视口引擎。
4. 热度策略运行时切换与可解释性：参数/内置策略版本校验、安全点切换、失败沿用旧版；首版不做任意算法DLL热载入。基础能力齐备后安排已延后的联合资源/连续可靠性验证，自启真实登录仍单独携带未验。
5. E/V3-07模板运行时：声明、素材、组件参数、候选切换与回退；先常规模板，再圆形/异形/离散区域，绘制与命中同代，保留既有原生拖放边界。
6. F/AI接入与G/应用更新：AI经受限ChangePlan校验/预览再调用既有用例；二进制更新使用独立更新器、可信版本校验、数据兼容及回退。更新器可在数据/生命周期边界稳定后独立推进，不必等待AI；不承诺EXE/DLL无需重启。

后续每批只选本次影响面的定向验证，集中人工通常1～2个体验点；其余窗口/文件用例抽取按功能需要增量推进，不等待全库重构。

## 6. 收口与本地集成（2026-10-16）

授权范围：登记复验、保存已验收的架构成果、以--no-ff合入impl/develop并停留集成线。fetch后本地develop与origin/develop均为4ae822a，无新增分歧。纯移动按A_MOVES的75个原Git对象独立提交，结构/依赖抽取和闪烁修订另提交，不修改当前工作文件来重建历史。提交与合并身份在交接登记。

验证复用已通过的A/B/C与修订结果，集成前后仅核对变更范围、编译边界与Git树一致性，不重新构建相同源码或启动人工程序。没有安装/卸载、系统启动设置、真实文件操作；out候选和故障证据不进入Git。自启真实登录、长测、真实满盘等原携带项保持未验。

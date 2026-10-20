# 当前架构与流程核对（A/B/C 集成后）

基线：`impl/develop@53b6bec27bbb0f236eded579013ead17c79dea91`，2026-10-16。本次仅核对代码并生成图源/说明，不启动下一批、不修改产品源码、不提交或推送。该提交尚未推送；图内固定提交的 GitHub 来源链接在推送前可能无法打开，下方提供本地代码入口。

## 交付状态

| 内容 | 状态 | 入口 |
| --- | --- | --- |
| 当前运行职责架构 | 已交付：9/9 showcase，0 错误、0 警告；浏览器及截图目视检查通过 | [架构 HTML](../../../../../out/architecture-review-20261016-integrated/current.architecture.html)、[图源](current.architecture.json) |
| 真实内容打开流程 | 图源已按代码更新，但布线校验未通过；未交付 HTML | [图源](current-open.workflow.json)、下方文字流程 |
| 热度运行边界流程 | 同上 | [图源](current-heat.workflow.json)、下方文字流程 |
| 图标收展流程 | 同上 | [图源](current-icons.workflow.json)、下方文字流程 |

[交付回执](delivery-receipt.json)绑定图源与 HTML 的 SHA-256，分别记录结构校验、自动浏览器证据、目视审查；[流程图诊断](workflow-diagnostics.json)保留未通过的原始错误。三张流程图不能算交付通过，不用旧 HTML 替代新结果。

既有[目标架构](../target.architecture.json)、[目标能力热换](../target-extension.workflow.json)、[目标应用更新](../target-release.workflow.json)仍是演进提案，未在本轮实现或重新认证。父目录的旧 current 图固定在 4ae822a，仅用于前后对比。

## 1. 当前职责边界

主图表示主要运行职责和调用，不是完整程序集依赖图。

| 层 | 当前职责 | 尚未完成的边界 |
| --- | --- | --- |
| App 视图与宿主 | WinUI 海报/中心、Dispatcher、交互保护、空间与绘制/命中编排、Bootstrap 组装 | 窗口尚未整体拆分；宿主仍知道具体存储 |
| Application | ContentActionService 执行打开/定位；ContentHeatSession 管理热度提案缓冲与派生状态 | 真实统计消费/评分尚未接入 |
| Domain | 内容能力、关系见证、作者意图、提案和布局纯规则 | 不是文件系统或 Shell 执行器 |
| Infrastructure | SQLite、Worker 适配、文件移动/归还等已有业务 | 部分文件用例仍在本层，未全部迁入 Application |
| Windows / ContentWorker | 原生窗口/输入/拖放适配；独立进程 Shell、图标、菜单 | Windows 适配不是热度会话职责 |
| 内置场景 / tools/review | Lamp/Rain 编译期场景；验收工具与普通产品编译分开 | 不是可动态加载的模板平台 |

## 2. 真实内容打开

卡片激活 → 准备请求与关系见证 → 账本唯一准入 → 再核对关系 → Worker 派发 → 校验协议和成功交接证据 → 原子完成请求/成功事实 → 返回视图反馈。

- 准备失败：不派发。
- 重复或忙碌请求：不重复打开。
- 派发前关系改变：结束为非成功，不打开错误来源。
- 失败、取消、无有效成功证据：不产生有效打开事实。
- 成功交接不等于外部程序已加载完毕；视图已关闭也不能丢失已确认的成功事实。
- 定位（Reveal）不记为打开。成功事实已有持久化，真实热度消费与评分尚未实现。

代码入口：[ContentActionService](../../../../../src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs)（OpenAsync 第12行，完成记录第56行，RevealAsync 第62行）；[SqliteContentOpenJournal](../../../../../src/DesktopUp.Infrastructure/Persistence/Content/SqliteContentOpenJournal.cs)（准入第42行，完成第67行）。

## 3. 热度运行边界

匹配海报/内容集版本的提案 → 保留最新缓冲 → 宿主安全点调度 → 从作者布局派生 → 校验可用空间 → 发布有效布局，沿原绘制/命中路径生效。

- Session 只拥有派生状态，不拥有窗口、文件、SQLite 或调度器，不把热度结果回写成作者意图。
- 拖动、框选、外部拖放、行内编辑、菜单、收展等保护存在时，宿主不发布，提案继续等待安全点。
- 错误身份、旧版本、旧内容集或已关闭会话的提案不接受。
- 空间校验失败消耗该候选版本，保留上一有效状态；内容集变化重置对应派生会话。
- 当前已有提案注入/保护/发布边界，**不代表已有真实使用统计**。未来“打开后只标记待更新，下次展开/明确刷新再发布”的产品节奏仍属下一批。

代码入口：[ContentHeatSession](../../../../../src/DesktopUp.Application/Features/Content/Layout/ContentHeatSession.cs)（Offer 第22行、Reconcile 第24行、ApplyPending 第61行、Close 第81行）；[窗口宿主接线](../../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)（调度第49行、发布第83行）。

## 4. 图标收起与展开

已就绪图标 → 收起取消不可见加载 → 保留有界缓存和已接受位图 → 展开准备树 → 匹配来源的旧位图立即呈现 → 过渡结束后恢复可见加载需求。

- 收起不再等于销毁资源，避免已加载图标重新闪成 FILE/DIR。
- 来源、成员、代次不匹配时仍淘汰旧键；首载和真正变化的图标正常异步补图。
- 最后可见解码图像最多64项，原字节缓存上限16 MiB；不常驻全量加载、不增加并发池。
- 真正暂停呈现、换源和窗口销毁仍释放资源；不承诺所有生命周期都零请求。
- 该修订已经用户复验通过，本轮只是复述源码和原验收结论，没有重新启动验收程序。

代码入口：[ContentAssets](../../../../../src/DesktopUp.App/PosterVisualWindow.ContentAssets.cs)（真正暂停第28行、收起第29行、取消不可见请求第36行、完整释放第162行）。

## 5. 增量开发接点

下一批从已持久化的成功事实接入幂等消费、稳定游标和统计，再把真实评分变成现有 ContentHeatProposal；不把评分写入窗口，也不改文件操作。模板运行时、AI ChangePlan 和应用更新器仍分别推进，不将现有编译期模块称为热加载平台。

后续顺序已按用户要求调整为先完成[基础架构余项及退出标准](../../../40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md)，再决定 D/V3-06 准入。A/B/C已验切片不等于整体目标完成；本图仍固定反映53b6bec现状，不冒充余项已实施。

## 6. 验证与绘图限制

Archify 2.17。整体架构：16个固定提交来源验证；四种视口（1440×900、1600×1000、1920×1080、2048×1320）均无横纵溢出；已实际查看两种端点尺寸的深浅主题4张截图，未发现节点/标签重叠或截断。未把截图检查扩称交互搜索/导出功能测试。

三张 workflow v2 初版的异常边走向形成共享通道；第一轮仅调整被诊断的线路并修正两处节点宽度，第二轮调整同一线路预设，均遇到 `workflow/route-preset-conflict`。最终诊断为 bottom→top 的72px直线，报告需8px端部、16px转折、28px直接间距，未提供可验证的 supportedFixes。依 Archify 两轮修正上限停止，不删除异常分支、不降级质量、不修改技能包，也不认定为 DesktopUp 产品缺陷。

重生成整体架构（仓库根运行；不要覆盖旧审查快照）：

```powershell
$archify = 'C:/Users/allmightwen/.agents/skills/archify/bin/archify.mjs'
$spec = 'Docs/A_Project/20_Architecture/ArchitectureReview/Integrated-53b6bec/current.architecture.json'
$output = 'out/architecture-review-20261016-integrated/current.architecture.html'
node $archify validate architecture $spec --repo-root . --quality showcase --json
node $archify deliver architecture $spec $output --repo-root . --quality showcase --json
$env:ARCHIFY_CHROME = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
node $archify visual-check $output --json
```

每次编辑图源后重新验证；只有 deliver 成功才允许浏览器检查。重新生成后必须重新绑定哈希、浏览器回执和目视记录，不能沿用本次结论。

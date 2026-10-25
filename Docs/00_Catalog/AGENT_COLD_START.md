# DesktopUp：agent 冷启动提示词

2026-10-25最新增量：用户认可实时热度更新效果，整体内容尺度改为0.4～1.8、标准1×图标区域48 DIP（热度关闭/标准疏密），热度独立叠加。偏好mapping v2，合法旧v1大值兼容限幅读取；SQLite仍schema27。最新候选与定向/原生/UI结果以[V3-06唯一记录](../A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md)为准，旧宏观上限3不再作为当前需求。

## 当前基线与接续入口（2026-10-26）

当前工作分支 **`impl/06-real-activity`** 由`impl/develop@d922669`派生，源码检查点`8156b73`已于2026-10-26推送同名远端并设置上游，用户明确要求**不合入develop**；main也未修改。状态为 **IN PROGRESS：实时更新已有正面体验反馈，新尺度与空间组合仍待集中人工确认**。唯一热度C记录：[V3-06开发准备与实施](../A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md)。既有文档及新增源码/测试已保存；此前直连失败，本轮使用系统已启用代理成功核对远端，不能继续按旧“未提交”接手。

当前已有schema27增量聚合、`ContentActivityConsumer`/`ContentActivityProducer`、连续尺寸/空间限幅、会话安全发布及海报级配置。两批定向共211项通过，原生探针与UI保存/取消/重启通过，集中人工体验仍待完成，不能写成USER PASS或已收口。真实内容首版闭环、基础架构 R1～R8/P1～P3、连续尺寸/悬停及单安装/迁移升级已有实现和限定验收记录，不再从架构 R1 或 DEMO 阶段重做。[近期核对记录](../A_Project/40_Implementation/PROJECT_STATUS_AUDIT_2026-10-24.md)保留开工前审计，最新热度进度以C记录为准。

本次核对工作区 `D:\Project\Desktop-up`；文档修改前 `impl/develop@d922669`，本地发布线 `impl/main@ecb0c04`。这是时间点记录，接手时以实际 Git 为准，不按旧机器路径切目录、切分支或回退。开发线保留 Docs/tests/tools，发布线精简，不反向合入其删除。

## 复制给其他 agent

```text
请在用户实际提供的 DesktopUp 仓库完成必要冷启动：先查 Git、适用 agent 指令与当前改动，再按本页阅读顺序核对源码和任务记录，不只复述历史摘要。
项目是 .NET 10 / WinUI 3 的 Windows 多海报桌面收纳工具。开发集成线 impl/develop 的开工基线 d922669，发布线 impl/main 为 ecb0c04；以现场 refs 为准。热度工作树新增SQLite schema27，001～026迁移保持冻结，版本域相互独立；不据源码版本推定用户真实库已迁移。
当前工作分支 impl/06-real-activity，基于 impl/develop@d922669；源码检查点8156b73已推送同名远端，上游已设置，本次用户明确不合入develop，不修改main。详读 Docs/A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md；当前IN PROGRESS，统计consumer/producer、连续热度/空间/配置、实时更新和桌面标准尺度已实现并定向验证。实时更新获正面反馈，新尺度与空间组合仍待体验，不重复创建分支、不提前记整体USER PASS。接手时核对实际Git状态；本地out证据和体验配置不随拉取获得。
真实文件/文件夹/实体快捷方式、原生拖入拖出/跨海报、文件操作、映射观察、归还/卸载保全已形成受支持本地范围的闭环；架构 R1～R8/P1～P3 已收口。连续尺寸、局部悬停及同用户唯一安装/原位和换路径升级已有交付。首次零海报，普通启动无海报开中心、有海报后台；新建基础收纳，不注入测试卡片。普通空白点击穿透按 DEC-043 撤销，单一有效卡片归属按 DEC-047 保持。
V3-06已批准30天加入/14天最近打开双保护、7～28天连续半衰期和10%下限；同ContentItemId转移保留、重加不按路径继承；低样本平滑默认0.5～3/无数据1.5/K=20并可按海报配置。整体尺度与逐Item热度独立，手动整体尺度受阻回弹；热度可扩框/受阻限幅，用户resize重算上限，下限保留可读底线，同一目标不撑回。2026-10-25用户改为成功打开后实时入队：统计提交即通知相关海报，单计算合并最新请求，无交互时异步更新，不等收展/鼠标离开；交互保护释放后自动续作。最新验证和候选见C记录，旧收展探针不证明实时更新通过。不把旧Quantize五档、测试注入或局部测试等同于完成，不重开已批准规则或基础架构。
保留用户改动/配置/真实文件/失败证据；历史卸载、升级41、P1/R6锁冲突与未验环境仍携带。依据当前任务授权行动，不把旧“本轮不合并/可清理”或历史PID当永久指令。若只有理解任务，报告后等待；有明确开发任务则在授权范围连续完成。
```

## 1. 建立实际工作基线

在实际仓库根目录执行：

```powershell
Get-Location
git status --short --branch
git branch --show-current
git rev-parse HEAD
git log -8 --oneline
git remote -v
```

定位仓库及目录级 `AGENTS.md` 或同类文件；本轮未发现，不代表未来没有。未提交/未跟踪文件属于现有工作，不覆盖或清空。检索跳过 `out`、`bin/obj`、依赖/缓存和生成文件。拉取不会带回候选、数据库、安装包和日志；跨机器找不到旧证据时明确记录，旧候选哈希不用于认证新构建。

## 2. 阅读顺序与权威关系

1. [ACTIVE_PATH](ACTIVE_PATH.md) → [当前交接](../A_Project/40_Implementation/V3-04/HANDOFF.md) → [本次状态核对](../A_Project/40_Implementation/PROJECT_STATUS_AUDIT_2026-10-24.md)：确认当前范围、基线、已验与未验。
2. [实施与验证流程](../Rules/IMPLEMENTATION_AND_VALIDATION.md)、[Git策略](../Rules/GIT_STRATEGY.md)：功能分支、验证范围、证据与交付规则。
3. A：[需求索引](REQUIREMENTS_INDEX.md)、[近期决定及未决问题](../A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)。已明确取代的决定优先于旧设计，不仅按时间猜测；代码现状不能自行替代需求。
4. B：[总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)、[源码职责](../../src/README.md)、[通用多海报合同](../A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)，再按任务展开模块合同。
5. C：[总计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md) → [细化工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) → 对应实施/验收记录。当前热度另读[活跃度需求](../A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md)、工作流§7.1～7.2及唯一[实施记录](../A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md)，已批准衰减/身份/平滑不重新列待决。
6. 界面任务实际查看[UI C原型](../A_Project/20_Architecture/Designs/content-layout-controls-c.html)，并结合后续决定；不能只读文件名，也不能把原型演示/测试说明原样放进产品。

A定目标与验收，B定边界，C记实现/证据。历史时间线仅解释当时状态；冻结/归档资料不因本次工作统一改写。一个任务维护一份详细C记录，交接和索引只记摘要及入口。

## 3. 当前代码地图

| 入口 | 职责 / 修改边界 |
| --- | --- |
| `src/DesktopUp.App/Bootstrap` | 显式组合用例、端口、唯一状态库/Worker与启动路径 |
| `src/DesktopUp.Application/Features/Content`、`Application/Ports` | Open/FileActions/Transfers/Observation/Lifecycle/Storage/Layout 用例、版本/取消合同及操作寿命；不放窗口或任意SQL |
| `src/DesktopUp.Domain` | 身份、作者意图、纯布局/尺寸/规则；无I/O |
| `src/DesktopUp.Infrastructure/Persistence/{State,Content,Transfers}` | SQLite事务/CAS、成功事实、转移账本及增量活动统计；工作树schema27，旧001～026不改。迁移SQL及catalog登记后冻结 |
| `src/DesktopUp.App/Runtime/Posters`、`Views/Poster`、`Presentation` | 每海报会话与窗口寿命、输入/同代几何、增量卡片/位图呈现 |
| `src/DesktopUp.Windows`、`src/DesktopUp.ContentWorker` | 原生输入/平台适配；独立有界Shell/图标辅助进程不打开配置库，不等于权限沙箱 |
| `Modules` | 已编译Lamp/Rain声明、场景、适配；不等于任意动态代码/在线AI平台 |
| `src/DesktopUp.Setup`、`packaging/preview` | 独立安装事务、安装身份、原位/迁移升级与恢复；不访问内容数据库，不是在线下载更新器 |

共六个应用/运行时工程，加独立 Setup，当前七个源码 csproj。详细依赖见[src/README](../../src/README.md)；当前可用路径见[代码锚点](../A_Project/40_Implementation/V3-04/CODE_ANCHORS.md#2026-10-24-当前源码与热度接入锚点)。旧设计类名/旧路径不保证仍存在。

核心链路：卡片打开 → `ContentActionService` → Worker/提供方交接 → `ContentOpenContract.Complete`判断可信成功 → `SqliteContentOpenJournal.Complete`原子落事实/排队 → `ContentActivityConsumer`/`SqliteContentActivityStore`有界聚合 → 提交后通知/`RequestActivityRefresh`合并请求 → `ContentActivityProducer`/`ContentActivityPolicy`生成连续提案 → `RefreshActivityAtSafePoint`经共用空间规划和`ContentHeatSession`保护发布 → Canvas/原生命中同代。隔离生产链已验证；`tools/review`模拟提供方确认必须标明，不能冒充真实打开提供方验收。

## 4. 不可误读的约束与携带项

- 默认纵向、循环关闭；首启零海报，用户新增基础收纳。模板专属逻辑不取得内容身份/命中或文件权限，测试卡片/探针/解释文案不进入正常产品。
- DEC-043：普通留白由海报接收输入；不重开已撤销的空白点击穿透。隐藏释放输入、框外不拦截、前台遮挡约束继续有效。
- DEC-047：一个对象一个有效卡片归属；有效拖放表达意图，不恢复额外确认/共享修复。失效引用确认、身份/账本/防覆盖和Windows冲突保护保持。
- 文件夹、文件、快捷方式本体按[首版支持矩阵](../A_Project/40_Implementation/V3-05/CONTENT_COMPATIBILITY.md)执行，不搬快捷方式目标，不推定云/网络/虚拟Shell均已支持。测试只用授权隔离样例，未知文件/配置不自动删除。
- 真实热度不能破坏手动构图/手工尺寸/顺位图钉；成功打开后实时入队，无交互保护时立即异步更新，交互期间合并等待、释放后续作，不恢复旧“等下次展开”。按[2026-10-24确认的Item§10.4](../A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md#104-独立倍率合成与空间上限2026-10-24用户确认未实施)共用空间规划：手动整体尺度受阻回弹，热度尝试必要扩框、受阻按现框限幅，外框变化重算热度显示上限，下限沿内容/热度设置与可读底线。目标/统计不被限幅覆盖；用户缩框时同一目标不能立刻把外框撑回。定向及原生结果见C记录，组合人工仍待确认；旧标题“未实施”属于需求基线锚点。保留现有自动/手动及动态缩放设置，不按模板重置；不恢复已撤刷新菜单或临时奖励。
- [架构§30.4](../A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#304-剩余成本和未定因项的处理计划)、[已知问题](../A_Project/40_Implementation/V3-04/KNOWN_ISSUES.md)继续携带成本、锁冲突及卸载/升级未定因。联合长稳、真实登录、自选多机/DPI等未验，不以新的局部PASS补齐。
- 构造卡片、完整动态模板/热更新、在线AI和在线更新器仍属后续；已有内置模块、参数配方或Setup不代表这些平台已完成。

## 5. 开发与验证

从仓库根目录执行，Windows x64；PowerShell 5.1及以上。`global.json`锁定.NET SDK10.0.112、latestPatch；工具链脚本准备项目内SDK/包缓存，打包另需NSIS。首次准备通常联网；`-Offline`只适用于依赖已完整缓存，不是自动获得离线环境。

```powershell
.\scripts\setup.ps1                 # 工具链与还原；打包准备可加 -ForPackaging
.\scripts\build.ps1 -Configuration Release
.\scripts\run.ps1 -ShowControlCenter
.\scripts\package-preview.ps1
```

上述是常用入口，不代表本批逐项执行。本批热度定向123/123已通过，隔离生产链与UI保存/取消/重启验证已通过，待集中人工体验，详细命令/证据仅记V3-06的C记录；没有据此重新认证打包/安装。普通启动按海报目录决定是否显示中心，`-ShowControlCenter`是显式打开。实际验证先核对旧实例及数据隔离，不凭旧PID关用户实例。

按改动选受影响构建、`dotnet test --filter`及必要原生体验；`scripts/test.ps1`是保留的全量入口，不是每轮必跑。架构改动可用`scripts/check-architecture.ps1`；只改文档检查链接和diff即可，不触发构建/全量测试。原生人工门只列本批新增体验，失败保留首轮证据，不把重试成功改记首轮通过。

## 6. 任务完成与交接

新功能从最新已确认develop派生，职责按现有模块落实；多agent确有安排时先声明文件归属和共享接口，不并发改同一拥有者/合同。实现、定向验证及必要集中人工完成后按授权集成，未经本次适用授权不提交、推送或发布。文档维护可在当前开发线同步状态，不夹带未验功能。

报告区分：目标、代码观察、自动结果、人工结论、推断与未知。交接至少包含实际分支/提交/改动、权威任务入口、完成范围、证据路径与可访问性、未解决问题和下一里程碑。仅冷启动时到此等待任务，不自行选择功能；已明确任务则持续做到其出口。

# 通用内容与多海报架构合同 V1

状态：DESIGN BASELINE — M01/M02 IMPLEMENTED / M03 AUTO PASS / SHORT MANUAL PENDING
日期：2026-09-07
适用：impl/poster-redesign-V3；不替代七份需求权威；M03 已接多会话，后续输入/参数/管理入口仍按计划推进，不声称完整多海报产品已完成。

## 1. 输入与明确不变项

用户确认：通用桌面内容收纳、多海报、一个控制中心、一个托盘、自定义独立风格化主题、热度排版、AI 辅助定制。来源：画像 RP-GEN-01/02、RP-MP-01..05、RP-STYLE-01、RP-ACT-01、RP-AI-01；Item §20；运行表面 §17。

保留已验证的 WinUI/Win32 宿主、DefView owner、no-activate、desktop-bottom 保护、视觉与输入岛分离、几何预备提交、SQLite 备份和命令投影基础。不得因支持多实例重新引入整屏透明输入 HWND、永久 topmost、Win+D 隐藏后恢复或旧 Qt/WebView 代码。

以下具体数据/端口为本轮设计基线，需在对应实现项用测试收敛；不能把设计选择写成用户逐项确认或实现已经完成。未决产品默认值见风险台账。

## 2. 实例与共享服务

| 范围 | 所有者与责任 | 不得混入 |
| --- | --- | --- |
| 应用唯一 | AppBootstrap、SingleInstance、ControlCenterService、TrayService、StateStore、CommandCoordinator | 单张海报的几何、草稿或主题覆盖 |
| 环境共享 | DesktopEnvironmentCoordinator、ShellLocator、DisplayTopology、恢复 generation | 将一张海报恢复成功等同全部成功 |
| 空间共享（设计） | PosterSpatialCoordinator＋纯几何策略，候选占位/碰撞/推让/条件归位，完整障碍读集 | 另建状态权威；靠抬层容许重叠；改变邻居主题/内容/尺寸 |
| 海报会话 | PosterSessionRegistry[PosterId] → PosterSession → WindowCluster、输入、布局、主题呈现 | 全局可写 currentPoster、跨实例输入缓存 |
| 内容共享 | CollectionCatalog、ItemCatalog、文件操作协调、图标缓存、真实打开事实 | 把共享文件身份当作每张相同排序 |
| 每海报持久化 | PosterDefinition、ThemeInstance、ContentMetrics、LayoutPolicy、PosterItem 关系 | HWND、物理像素、捕获句柄 |
| 每海报暂态 | capture/gesture、选中项、scroll offset、候选帧、草稿、恢复状态、取消 token | 每次 pointer move 都写库 |
| 高风险边界 | ShellProxy、将来受限 ThemeHost | 把进程拆分本身当成权限沙箱 |

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
| PosterItem | PosterId、ItemId、order key、锚定、局部显示覆盖 | 仅实例呈现，不携带第二份可变源文件 |
| ThemeInstance | InstanceId、PosterId、模板版本、参数/素材/结构覆盖、entity version | 克隆配置产生新实例；模板包可共享 |
| ActivityEvent | EventId、ItemId、来源 PosterId、时刻、打开结果 | 幂等事实；排名不能伪造或重复计数 |
| FileOperation/Journal | OperationId、批次/逐项状态、来源、目标、恢复信息 | 必须先于磁盘 mutation；保留未完成操作 |

初版每海报一个主集合；多海报可以共享集合，混合引用集合可容纳不同类型入口。多目录聚合与查询集合留作扩展，不自动等于已承诺首批功能。

迁移不可改写已发布的 v1/v2 migration 或 checksum。新增版本保存 poster-main 的全部旧字段，赋予独立实例身份关系；无真实 Item 时不得把六张演示卡片灌入用户正式库。初始化、失败保留、迁移前备份、幂等及旧库恢复测试必须保留。M01 的 [schema v3/目录仓储](../40_Implementation/V3-04/POSTER_CATALOG_SCHEMA_V3.md) 已实现并通过隔离旧库夹具；未迁移用户真实数据库。Collection/ContentItem 的 SQL 留待 V3-05-01。

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

CreatePoster 的入口是控制中心和托盘；RemovePoster 的入口是控制中心和每张海报的更多菜单。中心选择变化不能替换已经打开确认框的 PosterId；确认前准备移除影响摘要，确认后按原目标和当前版本校验。

共用确认服务显示海报名称、将移除的配置、保留的文件/集合/主题包、受管内容找回路径和最后一张海报的启动影响。取消不提交；进行中文件任务有阻止/安全等待语义；不能把窗口 Close 当作删除持久配置。对话框焦点不允许破坏海报桌面层级。

### 5.2 空间协同补充（设计，待 M04 实现）

[空间协同与展开合同](POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) 承接 RP-SP-01..07：锁定者是固定障碍；展开保留原尺寸，按可用空间向下、贴底向上或先下后上；可推让合法邻居，源收起后未再调整者安全条件归位。主动协调变更不违反实体独立性，但只能改变列明的受影响几何；无可行解保留原状态并解释。

推让不是多个单海报命令循环调用：完整读集包括未移动障碍和目录版本，异构几何在同事务提交至一个 committed revision，沿用现有仓储/回执；物理多窗口仍需预备、批量提交、读回和失败恢复，不能伪称跨数据库/渲染原子。旧固定顶边和自动压缩高度策略被新条件规则取代；历史测试保留为旧基线并新增对应反例。

最小中心列表和 SelectedPosterId 前置到 M04-01；现有中心只读 projection.Poster 默认别名是已确认的实现缺口，并非目录丢失。完整管理/预览仍在原04-04复用该入口。

## 6. 内容、文件与原生拖放

能力驱动而非扩展名驱动：能引用/打开/复制/移动/建立链接/重命名/回收/枚举子项分别声明。适配物理路径、快捷方式、打包应用、Shell namespace、网络/云与虚拟流；不支持的提供方有结构化反馈。

顺序固定：解析 payload 和有效期 → 判定能力/目标模式 → 显示意图 → 冲突预演/确认 → journal → 执行 → 校验 → 状态投影 → 完成/补偿。Native drop feedback、源清理与异步任务所有权分别建模。应用执行移动时不让 OLE 源再执行第二次删除；临时数据不得在提供方释放后成为悬空引用。

Watcher 按来源/目录共享引用计数和有界 coalescing；事件更新所有引用者，但不触碰它们的独立布局参数。扫描、图标与扩展调用均可取消、超时、限额。ShellProxy 必须在启用高风险第三方扩展前有实际边界测试，不能推迟到安装阶段才设计。

## 7. 风格、内容参数、热度与 AI

V3-04-M05 先冻结 ContentMetrics 和 ThemeParameterSchema 最小合同：稳定 key、类型/单位、范围、默认值、作用域、版本、验证/迁移。控制中心仅绑定该合同，不把临时 accent/density 字段不断塞进 PosterDefinition。内置基线样式先提供可用通用 Box，完整主题平台仍在 V3-07。

每个 Poster 有独立模板实例和资源作用域；共享不可变模板/素材缓存，禁止全局替换 Application.Resources 污染所有海报。热更新先预备、验证，再切目标实例；损坏时保留原模板和版本。样式字典不是执行沙箱，受限代码另行安全门。

热度引擎可共享代码与真实打开事实，每海报拥有独立算法参数、模式、锚定和布局快照。游戏风格不是硬编码的六张卡片；通用文件格也不被强制自动排序。最终公式与打开手势留在相应产品门，禁止 UI 临时裁决。

AIChangePlan 明确 targets、expected entity versions、允许操作、diff、确认与撤销。请求期间切换控制中心选择不改变执行目标。只经核心命令更新主题/布局，不赋予通用文件移动或删除权限。

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
| WidgetCompactExpansionCalculator、DirectionPolicy、WidgetWindowBase.Collapse | 候选锚点、严格尺寸拒绝、插值 | 不复制Auto缩小和RaiseForExpandedState抬层；新增先下后上轨迹/归位因果 | M04-04/05 |

实际移植前补读取来源文件/依赖/测试，登记精确 commit、移植片段、许可证、修改点和新增用例。不以 DeskBox 的测试结果当成本项目 PASS；此次未复制源码。

## 9. 必须关闭的实现缺口

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

## 10. 跨主机习惯配置（设计，待实现）

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

首个版本冻结结构化逻辑合同；建议基础序列化为可读 JSON，文件扩展名、总容量、素材容器和已有布局导入策略在 04-06-A/07-04 决定。此处不以用户的“配置文件”要求推翻现有单一权威，也不声称包格式已实现。

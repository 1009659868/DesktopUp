# DesktopUp V2 V3 技术路线与总体架构 V1

状态：当前职责基线＋后续目标，2026-10-23对齐。实际进度见[CURRENT_STATUS](../../00_Catalog/CURRENT_STATUS.md)。下文涉及热度、完整模板、AI和独立在线更新器的设计不是已实现能力。

## 0. 当前实现与目标的分界

- 已验基础架构R1～R8、P1～P3已集成；真实内容、原生拖放、生命周期、单一归属及安装升级的职责见[源码导航](../../../src/README.md)和[最终复审](../40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#30-p2p3收口后的最终架构与成本复审2026-10-20)。
- 当前7个生产项目：App、Application、Domain、Infrastructure、Windows、ContentWorker，加后续安装事务Setup。原2026-10-20“六程序集”图是当时业务边界快照，未画后来新增的Setup。
- 热度已有成功打开事实、作者布局与提案会话；尚缺增量聚合、正式评分／连续映射及生产发布时机。旧五档注入路径不是正式热度。
- Lamp／Rain等是编译期内置模块；标题等组件可配置不等于运行时任意风格模板平台。AI和在线更新器尚未实现。
- 普通点击穿透和共享内容引用已撤销；本文正文按现行合同修正，不要求为旧方案重新做P0探针。已有SQL迁移不可改写，文件安全与CAS保持。
- 具体产品行为／验收状态分别归需求与C层；不在此累积每次候选数字。历史架构来源可由Git追溯；旧图保留其日期边界。

## 1. 目的

历史设计来源（2026-09-07，不是当前实施状态）：[多海报空间协同与边界展开](POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) 细化正常布局不可重叠、锁定固定障碍、保原尺寸先下后上展开、连锁推让与条件归位。当时为新增设计，后续M04与连续空间实现已接入；沿用现有Host、M02状态权威和M03会话；推让走完整障碍读集/异构几何事务，物理提交另设失败恢复，不靠抬层。中心最小列表与稳定选择前置M04-01，原04-04复用并扩完整管理/预览。用户已确认基本多海报交互，未逐项报告的核心人工门仍保留。

本文把 DesktopUp V2 的冻结需求投影为一套可实施、可替换、可验证的 Windows 原生架构。V3 不继承旧 Qt、WebView、QPainter、Qt Quick、旧 WorkerW 探针或 Axiom 原型代码，而以 DeskBox 的成熟实现为主要工程参考。

“基于 DeskBox”表示允许直接借鉴窗口生命周期、WinUI 组织方式、文件收纳、Shell 交互、托盘、状态恢复和原生边界，也允许在许可证允许的条件下移植代码；但 DesktopUp 仍必须实现自身的海报层级、全区域输入／拖放与同代命中、活跃度排版、主题热更新和 AI 变更控制。

## 2. 输入与优先级

发生冲突时按以下顺序裁决：

1. 用户最新明确决定；
2. `Docs/A_Project/10_Requirements/` 下冻结需求；
3. 本架构和模块目录；
4. DeskBox 当前源码与测试；
5. DesktopUp 旧 Git 历史。

DeskBox 是实现证据，不是 DesktopUp 的产品需求。DeskBox 已实现某能力，不代表 DesktopUp 自动通过；DeskBox 没有的能力，也不能从 DesktopUp 需求中删除。

## 3. DeskBox 路线事实基线

参考位置：`G:\A_\Tools\reference\DeskBox`

本轮源码审计得到以下事实：

| 领域 | DeskBox 当前做法 | V3 采用方式 |
| --- | --- | --- |
| 主 UI | C#、WinUI 3、Windows App SDK、WinUIEx | 采用 |
| 应用结构 | 单主进程承载格子、设置、托盘和大部分服务 | 首发采用单主进程，危险边界再拆辅助进程 |
| 状态与绑定 | MVVM、依赖注入、服务协调器、显式策略类 | 采用职责分层，不复制超出需求的功能 |
| 桌面层 | 查找 Explorer 已存在的 `SHELLDLL_DefView`，通过 `GWLP_HWNDPARENT` 建立 owner 关系 | 作为 DesktopLayerAttachment 基线 |
| WorkerW | 不主动强制创建；启动时等待桌面图标宿主稳定 | 采用，避免与 Explorer 图标恢复竞争 |
| 层级 | DefView owner、非置顶、`WS_EX_NOACTIVATE`、原子 bottom/show、`WM_MOUSEACTIVATE` guard 和 owner 组内顺序 | 采用并增加 DesktopUp 精确层级验证 |
| 无边框窗口 | `AppWindow` + `OverlappedPresenter.SetBorderAndTitleBar(false, false)` | 采用，禁止系统标题栏和白条 |
| 收起/展开 | 同一窗口、独立expanded/compact bounds、显式状态机、批呈现 | 现SpatialExpansionPolicy先下后上、最小高度/完整障碍读集与条件归位；不是永远顶部锚点不动；复用R4 |
| 拖动/缩放 | Pointer capture、帧节流、结束时一次持久化、DPI/工作区约束 | 采用 |
| 文件格子 | 映射已有文件夹或使用受管文件夹，文件仍由真实文件系统承载 | 采用双模式 |
| 文件变化 | 文件夹 watcher、变更合并、退避和稳定后刷新 | 采用 |
| Shell 操作 | 原生拖放、复制/移动语义、快捷方式、回收站、系统菜单及隔离代理 | 按阶段采用 |
| 原生边界 | C# 主体；Rust DLL/辅助进程处理 AOT 或高风险 Shell 能力 | 保留接口，按需要引入 |
| 发布 | 开发期普通构建，Release 可进入 Native AOT 与安装器流程 | 功能稳定后再进入 AOT，不在骨架期强行优化 |

### 3.1 许可证边界

DeskBox 当前源码为 `GPL-3.0-only`。因此：

- 仅研究其思想和公开 Win32 API，不自动产生代码复制；
- 直接复制、改写或实质派生当前 DeskBox 代码时，必须记录源文件、来源版本和许可证；
- 如果分发含 GPL 派生代码的 DesktopUp，必须按 GPLv3 提供对应源码并保留通知；
- 不复制 DeskBox 名称、图标、品牌图片或让 DesktopUp 冒充官方 DeskBox；
- 在第一段移植代码进入仓库前建立 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和来源清单。

## 4. 总体架构原则

1. **Windows 原生优先**：桌面宿主、Shell 文件操作、拖放、托盘和窗口生命周期使用 Windows 原生能力。
2. **一个权威状态**：UI 不自行持久化业务状态；所有写入经过应用服务和 revision/CAS。
3. **桌面层与视觉解耦**：窗口 owner、Z-order、DPI 和 Explorer 恢复不由主题或 Item 控件决定。
4. **视觉与输入解耦**：半透明视觉可以覆盖桌面，但只有标题栏、按钮、Item 和缩放边界取得输入。
5. **用户文件优先安全**：移动前必须预演并写恢复日志；失败不能伪装成功。
6. **受保护交互不可换肤**：锁定、收起、拖动、缩放、错误恢复和退出入口由宿主控制。
7. **先功能后 AOT**：开发期允许 JIT；待反射、动态资源和主题边界冻结后再推进 Native AOT。
8. **失败保留最近可用状态**：状态、主题和窗口层级变更都使用 prepare/commit/rollback。

## 5. 物理进程拓扑

当前实际拓扑：

- DesktopUp.App：WinUI海报、控制中心、托盘；Bootstrap组装应用用例与适配器；配置／观察生命周期唯一。
- DesktopUp.ContentWorker：独立Shell／图标进程，承接打开、属性、Win菜单等固定协议；不访问配置数据库，不是权限沙箱。
- DesktopUp.Setup：安装器调用的独立安装事务程序；识别固定／历史登记、安全替换及路径迁移、更新自有入口和自启目标。它不读取业务数据库或搬迁收纳文件。不是在线更新器。
- NSIS安装／卸载入口调用相应维护流程；业务归还仍复用Application生命周期用例，不在安装脚本重写。

Domain／Application／Infrastructure／Windows是逻辑程序集，不是每海报一个进程。精确引用见[src/README](../../../src/README.md)。未来如需ThemeHost或在线Updater，另行设计授权／安全／回退，不提前建空进程或声称已具备。

## 6. 逻辑分层

### 6.1 Presentation

- WinUI Poster、Control Center、Tray 和系统对话框；
- 只消费只读投影并发送命令；
- 不直接访问 SQLite、注册表或文件移动 API；
- XAML code-behind 只处理窗口桥接和无法绑定的原生消息。

### 6.2 Application

- 编排用例：创建海报、添加 Item、收纳文件、启动内容、调整布局、切换主题、执行 AI 变更计划；
- 负责事务、revision、幂等、撤销和跨模块顺序；
- 生成 PosterProjection、ControlCenterProjection 和 TrayProjection。

### 6.3 Domain

- Poster、ContentItem、Collection、Activity、Layout、Theme、AIChangePlan、FileTransfer 等纯模型；
- 排序、尺寸映射、锁定权限、收起状态、路径冲突和失败补偿由纯策略表达；
- 不依赖 WinUI、Win32、SQLite 或网络库。

### 6.4 Infrastructure

- SQLite、文件 watcher、日志、主题目录、AI HTTP、图标缓存和安装生命周期；
- 只通过端口接入 Application/Domain；
- 外部异常必须转换为稳定错误，不把 COM/Win32 异常直接抛给 UI。

### 6.5 Native boundary

- HWND owner、Z-order、窗口样式、DPI、显示器、拖放、Shell 文件操作、快捷方式与回收站；
- 每个 handle 都有明确 owner、释放者和退出顺序；
- 辅助进程失败不得结束主海报进程。

## 7. 海报窗口架构

### 7.1 PosterWindowCluster

每张海报由稳定PosterId关联会话、视觉窗口和原生输入宿主。当前PosterVisualWindow负责窗口桥接；PosterContentSession持有内容交互／提案边界，Presenter保留稳定卡片／暖位图；布局与命中使用同一代几何。

按DEC-043，原生输入宿主覆盖**当前可见海报矩形**，同一HWND注册OLE接收。InputIslands等历史命名不再表示常态稀疏点击穿透。逻辑命中区仍区分标题、按钮、Item、空白框选和编辑手柄；不另建全桌面拖放捕获窗口。可见区域、隐藏／暂停／销毁与拖放会话一起受生命周期保护。

### 7.2 DesktopLayerAttachment

首选算法沿用 DeskBox：

1. 枚举顶层窗口，找到 Explorer 已创建的 `SHELLDLL_DefView`；
2. 启动时要求该 handle 连续多个采样保持稳定，不发送消息强制生成 WorkerW；
3. 保存海报窗口原 owner；
4. 通过 `SetWindowLongPtr(GWLP_HWNDPARENT)` 把各海报 HWND 的 owner 设为该 `SHELLDLL_DefView`；
5. 清除 topmost，设置静止态 `WS_EX_NOACTIVATE`；
6. 初次可见提交以一次 `SetWindowPos(HWND_BOTTOM, SWP_SHOWWINDOW | SWP_NOACTIVATE)` 完成，不在其后再次调用独立 Show；
7. 在各 desktop-pinned HWND 上拦截 `WM_MOUSEACTIVATE`，同步 no-show reassert desktop bottom 并返回 `MA_NOACTIVATE`；
8. 在 owner 组内建立一致 sibling 顺序；
9. 读回 owner、样式和 Z-order，不能只信 API 返回成功；owner 正确不能单独代表相对层级正确；
10. Explorer 重建时使 generation 失效，重新发现稳定 host，再一次性重挂全部 HWND；
11. 退出时恢复原 owner、清除样式并销毁全部窗口。

目标层级固定为：

```text
普通窗口 / 文件选择器 / 控制中心 / 全屏程序
                       >
                 DesktopUp 海报
                       >
                Windows 桌面图标
                       >
                     壁纸
```

禁止用永久 topmost、周期性抢 Z-order、整屏透明宿主或 Win+D 后 hide/show 补偿实现该层级。

### 7.3 输入策略

- 常态空白：属于本海报，左键按住拖动框选，右键为海报目录操作；不把输入交给被覆盖桌面。可见矩形支持全区域拖入，海报之间仍遵守空间不重叠规则；
- desktop-pinned HWND 的普通鼠标消息使用 `MA_NOACTIVATE`，不能因为点击而进入普通窗口前方；
- 标题栏：未锁定时拖动，锁定时不移动；
- Item：单击选择/启动规则由需求矩阵决定；
- 按钮：始终可操作；
- 编辑态缩放边：显式 pointer capture，行列重排不得使捕获失效；
- 需要文本输入时，使用显式键盘激活 lease 临时解除 no-activate，并在交互结束恢复；
- 输入岛由同一布局快照生成，禁止 UI 视觉已经变化而 hit region 仍使用旧 revision。

### 7.4 几何与收起

- `FrameGeometry`、`ContentMetrics`、`ResponsiveLayout` 分开存储；
- 同一 DPI 下外框缩放只改变 viewport 和重排，不缩放文字与 Item 参数；
- expanded 与 collapsed 各保存稳定 bounds；
- 收起/展开固定左上角和顶部栏位置，只改变高度；
- 同一个 PosterWindowCluster 完成收起，不创建第二张海报或把按钮移动到旧海报中点；
- AppWindow resize、XAML 状态、输入岛和持久化以一个 geometry transaction 提交；
- 高频 pointer move 只更新内存中候选 bounds，并按合成帧提交；松开后一次持久化。

## 8. 控制中心与托盘

### 8.1 Control Center

- 按需创建的普通 WinUI 顶层窗口；
- 高于海报，参与任务栏/Alt+Tab 的方式由需求决定；
- 读取与真实海报相同 revision 的 projection；
- 编辑操作提交 command，不持有第二份可写 Poster；
- 关闭只释放窗口与可重建 UI，不停止 PosterSessionRegistry。

### 8.2 Tray

- 托盘始终是快速入口和生命周期控制面，不是权威状态；
- 按V2-DEC-006只提供新增、控制中心、全局临时隐藏/恢复、全部锁定/解锁、动效暂停/恢复、退出；不持有单张选择或目标子菜单，启动设置/主题/AI/诊断归控制中心；
- 双击、连续命令和单实例转发串行处理；
- 主进程退出前先禁用新命令，再销毁窗口、停止 watcher、刷盘和移除托盘图标。

## 9. Item 与文件收纳架构

### 9.1 Item 统一模型

`ContentItem` 统一表达文件、文件夹、快捷方式、应用和未来受控内容源，至少包含：身份、来源、目标、显示名、图标引用、有效性、排序字段、固定状态、活跃度统计和最后错误。

视觉卡片不是数据本体；删除卡片不等于删除用户文件。

### 9.2 三种集合与能力模型

1. **Reference Collection**：混合入口关联，添加不改变源文件；
2. **Mapped Collection**：映射已有目录，建立映射不搬文件，后续文件操作明确影响该目录；
3. **Managed Collection**：显式受管目录，用户明确操作后才复制/移动。

按类型声明引用/打开/复制/移动/重命名/回收能力；Shell 虚拟对象不伪造物理路径。同一有效对象仅允许一个可见海报引用；物理受管集合可能共用，不等于同一对象可在多张海报重复展示。跨海报转移沿单一归属与版本事务；删除海报、解除映射与删除实体文件不同。详细数据关系见通用多海报合同。

### 9.3 文件操作事务

任何移动/收纳流程固定为：

```text
扫描 → 规范化路径 → 冲突检查 → 预演计划
→ 写 recovery journal → 执行 Shell 操作
→ 验证结果 → 更新 Item/状态 → 提交 journal
```

取消、部分失败、来源改变、目标断开、跨盘操作和崩溃必须保留可核对结果。普通拖入／出没有应用额外确认；Windows必要冲突／回收提示保留，禁止未授权覆盖、永久删除或清空桌面。Shell原生拖出由目标执行，应用观察后移除关系，不能再执行一次搬移。

### 9.4 Shell 能力

- 原生 IDropTarget/DataPackage 解析；
- 跟随 Windows 的复制、移动、链接及修饰键语义；
- 使用 IFileOperation 或等价系统机制显示进度与撤销能力；
- 快捷方式解析/创建、在资源管理器中显示、属性、回收站和系统菜单通过独立端口提供；
- 现有Shell／图标协议隔离在ContentWorker；只支持已声明能力，不把进程隔离当权限沙箱或全部扩展兼容证明。

## 10. Item 排版与活跃度

当前ContentLayoutDocument／纯布局帧／PosterContentSession及同代Presenter已接真实内容，已验连续视口、手动让位、图钉、小数尺寸与增量呈现。成功打开经ContentActionService／IContentOpenJournal持久化；正式聚合／评分仍未实现。ContentHeatSession和Domain提案路径存在旧五档量化适配，真实热度接入时要替换生产映射并保留必要历史兼容，不得将其当作用户要求的连续热度。

后续链路：事实 → Application幂等消费与协调 → Domain纯策略 → 版本化连续提案 → 会话安全时点 → Presenter／同代命中。具体待裁决和性能约束见[工作流§7.1](../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#71-2026-10-21-真实热度准备未实施)。以下策略名表示职责，不要求另建同名类。

- `ActivityScoringPolicy` 根据需求公式计算分数，不直接操作 XAML；
- `ItemLayoutPolicy` 接收 viewport、内容参数、固定项和排序结果，输出稳定布局快照；
- WinUI 视图使用虚拟化集合控件或自定义 Layout，而不是为每次缩放销毁全部控件；
- 列数变化、连续裁剪和滚动只改变布局快照，Item identity 和 pointer capture 不变；
- 所有 Item 必须可达；零项、单项、多项、失效项和重复来源都有独立夹具；
- 热度自动（可含顺位图钉）与纯手动两模式共享同一模型；图钉不是第三模式，手工尺寸优先，自动热度不主动扩外框。

## 11. 主题与热更新

主题系统分三层：

1. `ProtectedChrome`：锁定、收起、拖动、缩放、错误回退和退出入口，主题不可删除；
2. `ThemeTokens`：颜色、字体、圆角、透明度、间距、图片和动画参数；
3. `ThemeTemplate`：受限的海报和 Item 视觉模板。

内置主题首先使用 WinUI ResourceDictionary、Styles、ControlTemplate 和受控 XAML 组件。热更新采用两阶段提交：读取候选包 → 校验 manifest/schema/资源 → 在隔离预览中构建 → 生成 snapshot → 原子切换；失败继续显示最近可用主题。

普通用户主题不得执行任意本机代码。未来确需用户代码时，放入独立 `ThemeHost`，使用版本化消息、资源配额、路径白名单和网络默认关闭；ThemeHost 崩溃只触发 minimal fallback，不结束主进程。

## 12. AI 控制平面

- BYOK，支持 OpenAI、DeepSeek 和 OpenAI-compatible adapter；
- 无 Key 时不初始化 provider、不发送网络请求；
- AI 只能读取经过裁剪的主题/布局上下文并产出 `AIChangePlan`；
- ChangePlan 经过 schema 校验、权限分级、diff 预览和用户确认后，才转为普通应用命令；
- AI 不直接获得 Shell、进程、注册表、任意文件系统或主题宿主执行权；
- R0–R5 风险等级决定是否可自动预览、必须逐项确认或完全拒绝。

## 13. 状态、事件与一致性

### 13.1 权威状态

- 结构化权威状态使用 SQLite；
- 文件传输恢复日志使用追加式 journal，并与数据库事务通过 operation id 关联；
- 每次生产写入携带目标实体版本与必要读集；事务推进全局 committed revision；冲突返回同事务 projection，不由 UI 偷偷覆盖。M02 已迁移生产链，global-only 仅保留旧仓储/命令兼容入口；
- JSON 只允许用于导入/导出、诊断和主题 manifest，不作为第二业务权威。

### 13.2 投影

同一 committed revision 生成：

- PosterProjection；
- ControlCenterProjection；
- TrayProjection；
- LayoutSnapshot；
- InputRegionSnapshot。

窗口不得分别读取数据库拼出互相矛盾的状态。

### 13.3 事件

应用内部使用类型化事件通知投影更新；事件只携带身份、revision 和必要差量。文件 watcher、Explorer、显示器、DPI、主题目录和 AI 网络回调先进入各自 adapter，再由 Application 层串行化。

## 14. 生命周期与恢复

### 14.1 启动

```text
进程入口 → 单实例分流 → 数据目录/日志
→ schema/迁移/最近可用状态 → 托盘
→ 创建海报但先不显示 → 等待 Explorer 图标宿主稳定
→ attach + 首帧就绪 → 一次显示 → 后台恢复非关键缓存
```

### 14.2 Explorer 重建

监听 `TaskbarCreated`、旧 HWND/PID 失效和 owner read-back 不一致等 Shell 信号，不轮询抢层级。V3 吸收 DeskBox 的 DefView 缓存失效、只发现既有 DefView、稳定采样、owner 设置/读回/失败回滚；补强为独立 `DesktopShellGeneration` 和显式单航班重挂事务。

旧 generation 失效后冻结位置写入，发现新的稳定 `SHELLDLL_DefView`，对现有同一 PosterWindowCluster 原地重挂，验证 owner、样式、可见性和相对层级后提交 `attachedGeneration`。迟到 generation 必须作废；禁止依赖未来交互顺便修复，也禁止新建第二海报、hide/show、激活或临时 topmost。

### 14.3 显示变化

V3 吸收 DeskBox 的窗口原生消息、低频全局拓扑兜底、稳定签名、generation、应用后验证、逻辑尺寸和四角锚点；补强为与 Shell 身份分离的 `DisplayTopologyGeneration`。拓扑、DPI、工作区变化进入去抖稳定窗口，先从同一稳定快照完整计算候选物理 bounds，再按显示器身份、DIP 尺寸、归一化锚点和逻辑 margin 一次提交并夹入可见工作区。

交互期间抑制恢复，恢复事务期间不持久化系统暂态 bounds；`WM_DPICHANGED` suggested rect 只能作为候选输入。提交后必须读回实际 bounds、DPI 与工作区验证，迟到 topology generation 不得覆盖新状态，临时断屏或任务栏中间态不得覆盖已保存的稳定布局。

当 Explorer 与显示环境同时变化时，恢复事务捕获 `(DesktopShellGeneration, DisplayTopologyGeneration)`，等待两者分别稳定后再提交；任一版本在提交前后变化均废弃本轮结果。完整参考与补强合同见 `../40_Implementation/V3-02/DESKBOX_RECOVERY_REFERENCE_AND_V3_STRENGTHENING.md`。

### 14.4 退出

停止接收命令 → 结束主题/AI/文件任务 → 刷新 journal/SQLite → 释放输入岛 → 恢复 owner/样式 → 销毁海报与控制中心 → 移除托盘 → 释放 native handles。退出后不得残留窗口、托盘或后台进程。

## 15. 性能策略

- 常驻静态海报不维持动画计时器；
- XAML 列表必须虚拟化，图标/缩略图有容量上限；
- 文件 watcher 合并突发事件并退避；
- 控制中心关闭后释放大 UI 树；
- 拖动/缩放按合成帧批处理，不对每个 PointerMoved 同步刷数据库；
- 日志、诊断和缓存都有上限；
- 功能完整后重新执行 CPU、Working Set、GPU、启动时间和窗口资源基线；
- Native AOT 是后期优化/发布项目，不能以删减热更新与错误恢复换取数字通过。

## 16. 技术依赖方向

历史技术候选（不等于当前已引用依赖；准确版本与工程方向以项目文件及src/README为准）：

- .NET Windows Desktop，目标版本与 DeskBox 当前工具链对齐后锁定；
- WinUI 3 / Windows App SDK；
- WinUIEx；
- CommunityToolkit.Mvvm；
- Microsoft.Extensions.DependencyInjection；
- H.NotifyIcon.WinUI 或经过验证的等价托盘实现；
- Microsoft.Data.Sqlite；
- Rust stable + C ABI，仅在 native adapter 阶段引入。

依赖版本必须写入 lock 文件；不使用浮动 `latest`。DeskBox 的大型功能依赖不整包导入，只在 DesktopUp 某项需求确实需要时加入。

## 17. 首发范围与延期

当前正式范围是主屏多海报、一个控制中心与一个托盘；单海报证据作为回归而非数量上限。多显示器/混合 DPI 完整认证、云同步、主题市场、任意插件和无限权限脚本仍不自动进入当前交付。多海报需独立实例、主题、内容、持久化和实际窗口门，不以数据数组冒充完成。

## 18. 架构退出门

本架构进入生产实现前必须满足：

- 每项冻结需求至少映射到一个模块和一个计划阶段；
- DeskBox 直接复用点有许可证和来源策略；
- 可见海报全区域输入／OLE与同代命中有验证；不恢复已取消的普通点击穿透；
- owner attach、无白条、Win+D、Explorer 重建和普通窗口层级在最小海报上先通过；
- 文件移动前恢复 journal 和 dry-run 已存在；
- 主题和 AI 不进入桌面层 P0 的关键路径。

模块的详细职责和依赖规则见 [V3 模块分类与端口目录](DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)。

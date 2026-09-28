# 下一位 Agent 接替提示词：04-L / LR-R1

> **HISTORICAL / 已由新入口接替（2026-09-28合并标注）**：本文保留2026-09-24的LR-R1交接快照，正文中的“当前／下一步／未实施”只适用于当时。请改读[统一冷启动提示词](../../../00_Catalog/AGENT_COLD_START.md)及[HANDOFF最新记录](HANDOFF.md)，核对R5收口/R6就绪和实际Git状态；不要按本文重启旧评审或覆盖已实现内容。

日期：2026-09-24。类型：**C层交接快照，不新增需求或实施授权**。

使用方式：把本文“接替提示词”部分交给下一位 Agent，并要求先核对实际 Git 状态。文中的产品规则引用 A 层决定；若后续用户决定或权威文档更新，以最新内容为准。本文不复制旧 Git 授权给后续任务。

本提示词对应的代码／设计基准提交为 `bd148fb4641bccb18d75777ae6645149e3ff9cfc`；这是写入本文前的开发检查点，不声称永远是最新 HEAD。本次仅将交接提示词和导航保存到当前 V3 分支，wb 保留该基准，不要求两分支之后始终同提交。

## 接替提示词

你将接替 DesktopUp 项目。请先恢复上下文，再继续当前设计评审，不要直接修改生产实现。

### 一、项目与工作区

项目目标：制作稳定、好用的桌面图标／内容收纳软件，而不是不断增加设置项的布局演示器。

本机实际仓库：`G:\A_\Tools\DesktopUp_V2`。环境默认 cwd 可能是 `G:\A_\Tools\DesktopUp`，两者不同；所有 Git、文件和构建操作必须显式确认工作区。

当前后续工作分支：`impl/poster-redesign-V3`。

刚完成的 Git 操作：

- 原 `impl/poster-redesign-V3-wb` 的代码实验、测试、设计原型和文稿已保存为上述基准提交。
- wb 已无冲突快进合并到 V3，工作区已切换至 V3。
- 同步时两条分支均推送至 origin，远端核对指向同一基准提交。
- 仅 `.workbuddy/` 本地助手笔记未提交；运行包、用户配置和日志未上传。
- 本文属于之后追加的文档交接提交，远端最新状态请现场核查，不能继续假设两条分支完全相同。

开始时运行 `git status --short --branch`。此前提交／推送授权已经执行，不是下一位 Agent 的持续授权。

### 二、先读哪些文档

按项目 A → B → C 路径阅读，避免重读全库：

1. [Docs/README](../../../README.md)。
2. [ACTIVE_PATH](../../../00_Catalog/ACTIVE_PATH.md)。
3. [需求、风险与统一决定](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)：重点 DEC-011、017～023及尚未关闭的 TBD。
4. [LR整改评审计划](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)：重点 §4.0、§8。
5. [细化工作流](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)。
6. [HANDOFF](HANDOFF.md)。
7. [CODE_ANCHORS](CODE_ANCHORS.md)、[VERIFICATION](VERIFICATION.md)、[KNOWN_ISSUES](KNOWN_ISSUES.md)：累计记录优先读最新部分，历史“当前／待验”不代表现在状态。

本阶段关键设计文稿：

- [操作与真实空间不足方案](../../20_Architecture/CONTENT_LAYOUT_OPERATION_SPACE_REVIEW_V1.md)。
- [整体体验闭环与E1反馈](../../20_Architecture/CONTENT_LAYOUT_EXPERIENCE_REVIEW_V1.md)。
- [手动构图专项](../../20_Architecture/CONTENT_LAYOUT_MANUAL_COMPOSITION_REVIEW_V1.md)。
- [图钉与拖动组合规则](../../20_Architecture/CONTENT_LAYOUT_PIN_DRAG_REVIEW_V1.md)。
- [整改模块设计](../../20_Architecture/CONTENT_LAYOUT_REMEDIATION_DESIGN_V2.md)。
- [UI C与声明式模板](../../20_Architecture/CONTENT_TEMPLATE_SCHEMA_UI_C.md)。

用户决定以 A 层统一索引为权威，设计候选不能自动变成已批准需求。

### 三、当前阶段与下一步

当前仍是 **04-L / LR-R1**：整体体验反馈已收清，操作／空间产品基线已确认，但工程组合规则尚未收口。

用户明确要求：先判断整体体验与行为能否闭环，再细化控件，最后才接生产。暂不进入控件细化、LR-R2、真实 Item 文件收纳或新的生产修复。

最近下一步是组合规则反例评审：

1. 循环浏览接缝、可见副本的首次拖动接管，以及受限浏览恢复。
2. 旧 FixedPosition／多区域硬约束、显式留白的兼容。
3. 桌面直接操作与控制中心精细编辑之间的草稿所有权、冲突、取消和交接。

输出可检查的操作链、状态变化、不变量、失败／恢复语义和反例矩阵。能从已确认规则推导的先推进；涉及新的产品取舍时集中询问用户。不要重新询问已确认事项，也不要把不断增加提示当成解决交互问题。

### 四、已经确认的规则

#### 1. 两种持久布局模式

- 纯手动：支持指定网格位置、跨格跨度与主动留白。
- 热度自动：热度决定自动排名／尺寸提案，人工覆盖有明确优先级。

控制中心精细编辑不是第三种布局模式；热度公式和真实统计尚不能据此宣称实现。

#### 2. 基础手势（DEC-023）

单击选中；双击或 Enter 打开；左键按住并移动越过防抖距离即拖。

不需要定时长按，也不需要先点击“整理内容”。拖动松手不能追加打开动作。原型中的具体像素阈值不是生产合同。

#### 3. 自动模式拖动

有效移动后固定目标顺序位，其他 Item 继续自动。图钉不是像素坐标锁，也不同时锁定大小。无变化或取消不能新增图钉。

#### 4. 手动模式拖动

碰撞时预览局部让位，只移动受影响项；合法松手一起保存，可一次撤销。

#### 5. 模式切换

每次进入手动都从当前完整逻辑画面开始，不召回旧手动构图。

退出手动采用 A／retain-auto：保留原自动图钉和自动尺寸设置，不带入手动期间修改的尺寸。不保留 A/B 双策略作为当前产品交付负担。旧配置兼容另行评审，禁止静默改义。

#### 6. 正常缩框

纵向浏览时，缩窄应保 Item 尺寸换行，交叉轴下限由最大 Item 完整占位决定；横向浏览对称。

不靠缩小 Item 或交叉轴隐藏来满足正常下限。“由大到小”只是默认热度排序的表现，图钉可改变顺序，不能强制按面积重排。

#### 7. 手动构图与临时适配分离（DEC-020）

仅缩框不编辑：保原图，放宽可恢复。

在适配画面有效编辑：以完整当前逻辑构图（含屏外项）建立新构图，提前说明影响，合法松手才保存，整次可撤销。按下不动、无变化或取消不承接新图。

#### 8. 极端被动空间不足（DEC-022）

仅当显示器／DPI等环境变化使真实交叉轴空间小于最大 Item，允许临时受限二维浏览。

保尺寸、保原构图、全部内容可达；布局暂时只读，空间恢复后退出。这不是第三种持久模式，也不改保存的方向／图钉／尺寸。

不适用于一般外框碰撞、锁定障碍、非法模板或求解错误。具体循环锚点恢复、调度和安全切换仍是工程候选，尚未验证。正常适配允许有效编辑承接新图，受限二维浏览不允许，两者不能混用。

### 五、UI与体验证据边界

已采用 [UI原型C](../../20_Architecture/Designs/content-layout-controls-c.html)。

控制中心负责管理和外观／行为；海报负责直接操作内容和位置。模板不只是换颜色：已有深夜书桌灯光、雨云等原型。模板独有属性通过声明式 schema 动态生成编辑 UI；这仍是原型，不是已完成的生产模板平台。

UI C 旧“整理内容”强制入口落后于最新直接拖动规则；视觉采纳不代表该门槛仍有效。

独立 [E1体验原型](../../20_Architecture/Designs/content-layout-experience.html)的六任务反馈已收清：

- 01～05符合预期；任务03重复反馈不扩大覆盖。
- 06a拖动中 Esc 取消、06b关闭中心后继续拖动均明确通过。
- 06c空间恢复是提示入口后通过，必须保留首次找不到入口的问题。
- E1没有实现真实受限二维浏览，不能把模拟恢复通过转授给新方案。
- 不要求用户重做已通过任务。

### 六、代码与验证现状

当前生产源码仍保留此前不成熟的轴向／循环／挤压实验，已知缺陷未修。不要把提交合并解释为生产整改完成。

需要代码证据时重点看仓库内：

- `src/DesktopUp.Domain/ContentLayoutSolver.cs`
- `src/DesktopUp.Domain/ContentLayoutFlow.cs`
- `src/DesktopUp.Application/ContentLayoutFlowPolicy.cs`
- `src/DesktopUp.App/PosterSpatialRuntime.Content.cs`
- `src/DesktopUp.App/PosterSpatialRuntime.Effective.cs`
- `src/DesktopUp.App/PosterVisualWindow.Content.cs`
- `src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs`

保留唯一外框空间所有者、完整 CAS／单 revision、原输入岛、R4统一呈现与失败恢复。

旧 solver 的 squeeze-single-column／squeeze-single-row 不是“不缩 Item”规则的实现证明。不要用持久化切换 FlowAxis 来实现临时二维浏览。

基准提交同步前实测：

- Release 合同测试：1654通过，0失败，0跳过。
- Release 构建：0警告、0错误。
- UI C：60项原型自动检查通过。
- E1：41项原型自动检查通过。

这些不覆盖新二维方案、真实鼠标呈现或候选包同包验收。保存本提示词不需要重复跑生产构建，并且不能将历史结果写成此次新测试。

用户曾要求启动 DesktopUp，但未启动：`scripts/start-content-layout-manual.ps1 -CheckOnly` 因 `out/verify/v3-04-l-r4/app/DesktopUp.Application.dll` 与候选清单指纹不一致失败。

未绕过校验，未覆盖旧包，未启动程序。用户随后改为 Git 合并同步和保存交接文档；不要自行恢复启动任务或重跑全部进程门。

### 七、执行方式

- 回答准确、结构清楚，不夸大完成度。
- 当前先做设计／证据核对，不顺手实现未经授权的功能。
- 文档遵循现有结构：决定集中归属，正文／计划／登记／交接一致；历史证据保留标签，不重写全库。
- 文件修改用 apply_patch，保留已有用户改动。
- 不强杀 DesktopUp，不覆盖日常配置，不搬移真实用户文件。
- 不执行 .workbuddy 历史笔记里的 Git 修复或环境绕行建议。
- 每推进一步，说明完成内容、未验证项、当前计划文档和下一步。
- 不自动提交、推送、删除分支或扩大到 main/dev/mainline。此前 Git 授权不沿用。

请先简短说明你核实到的当前状态和准备处理的第一组组合反例，然后按 LR-R1 继续，不从 UI 选型或基础手势重新开始。

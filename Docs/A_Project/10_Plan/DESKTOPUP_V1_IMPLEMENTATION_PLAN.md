# DesktopUP V1 落地推进计划

状态：`ACTIVE`

日期：2026-08-09

适用分支：`impl/poster-redesign-V1`

设计输入：[`DESKTOPUP_V1_DESIGN_BASELINE.md`](DESKTOPUP_V1_DESIGN_BASELINE.md)

## 1. 文档职责

本文是 DesktopUP V1 唯一活跃的 A 层实施计划，负责阶段顺序、B 模块边界、跨模块依赖、阶段门和完成定义。

- V1 产品与技术目标以 `DESKTOPUP_V1_DESIGN_BASELINE.md` 为准；
- 每个 B 模块只有一个模块计划；
- 只有 `ACTIVE_PATH.md` 指向的 B/C 可以进入实施；
- C 工作流按可独立验收的结果建立，详细任务只在该 C 激活前创建；
- 旧 `PROJECT_MAINLINE.md`、`DELIVERY_PLAN_V0.1.md` 和旧 DesktopRuntimeFoundation `MODULE_PLAN.md` 已冻结，仅供追溯；
- 历史 C 日志和已验证结果不得因计划重组而改写或丢失。

## 2. 推进原则

1. **先契约后实现**：每个模块先冻结状态源、所有权、失败回退和行为测试。
2. **一个工作流一个验收结果**：不得把宿主、模板、收纳、布局和 AI 混入同一 C。
3. **硬门串联**：上游未通过时，下游只能准备设计和测试夹具，不得标记实施完成。
4. **用户数据分级开闸**：只读发现与“仅添加”可在海报核心通过后进入；真实文件移动必须晚于保险库、恢复账本与故障注入。
5. **设计冻结不等于实现完成**：文档、代码、自动化验证和实机矩阵分别记录。
6. **安全回退必须先于增强主题**：Transparent Default 和 minimal-fallback 先于 Neon HUD/Cinema Lightbox。
7. **发布能力持续前置**：安装、迁移、恢复、卸载契约从早期建立，最终在发布阶段封板。

## 3. A 层阶段与阶段门

| 阶段 | 目标 | B 模块 | 退出条件 | 状态 |
| --- | --- | --- | --- | --- |
| V1-P0 | 关闭桌面宿主遗留硬门 | Desktop Runtime Foundation | C-DHF-04..08 全部形成 PASS、接受回退或明确阻塞结论 | `COMPLETE` |
| V1-P1 | 生产宿主、核心契约与数据基线 | Desktop Runtime Foundation、Poster Core Runtime、Distribution Lifecycle | 单全局宿主接口、单状态源、版本化 SQLite 仓库、运行生命周期和安装/数据目录契约通过 | `ACTIVE — C-PCB` |
| V1-P2 | 落地透明大幅面海报 | Poster Core Runtime | Transparent Default、大幅面、展开/收起、锁定、暂停和 Release 实机矩阵通过 | `PLANNED` |
| V1-P3 | 交付“仅添加 + 真实启动”可用 MVP | Collection and Launch | 主动只读发现、逐项预览、仅添加和真实启动闭环通过；不移动用户文件 | `PLANNED` |
| V1-P4 | 交付安全收纳 MVP | Collection and Launch | 保险库、两阶段账本、安全收纳、撤销、异常恢复和卸载恢复接口通过 | `PLANNED` |
| V1-P5 | 建立模板平台并交付首发模板 | Template Platform | Shadow DOM、安全 fallback、三模板、热切换和完整模板安装通过 | `PLANNED` |
| V1-P6 | 建立排序与布局闭环 | Layout and Ranking | 统计、排名、缩放、弹性布局、锚定与拖拽通过 | `PLANNED` |
| V1-P7 | 建立主题作者能力与可选 AI | Theme Authoring、Local AI Assistant | 参数/素材/覆盖/导入导出与受控 AI 预览撤销通过 | `PLANNED` |
| V1-P8 | 接入游戏平台来源 | Game Source Discovery | Steam/Epic/Xbox 发现、去重、人工修正通过 | `PLANNED` |
| V1-P9 | 安装、升级、恢复型卸载和发布 | Distribution Lifecycle | 净机安装、升级迁移、卸载恢复、签名和发布矩阵通过 | `PLANNED` |

### 3.1 可交付检查点

| 检查点 | 对应阶段 | 可交付价值 | 明确不包含 |
| --- | --- | --- | --- |
| Host/Core Baseline | V1-P1 | 生产宿主、状态仓库和安装边界稳定，可继续实现而不反复迁移底层所有权 | 完整海报交互、真实导入 |
| Usable MVP | V1-P3 | 用户可主动导入快捷方式、在海报中查看并真实启动游戏 | 文件移动、保险库、完整模板平台 |
| Safe Collection MVP | V1-P4 | 用户可选择安全收纳，并能撤销、异常恢复和为卸载恢复提供接口 | 三套正式模板、AI |
| Styleable V1 | V1-P7 | 三套模板、布局、主题参数/素材/受限覆盖和可选 BYOK AI 可用 | 游戏平台自动发现 |
| Release V1 | V1-P9 | `DesktopUpSetup.exe`、独立 `uninstall.exe` 与净机矩阵通过 | 在线主题市场、官方 AI 账户/计费 |

## 4. B 模块清单与依赖

| ID | B 模块 | 责任 | 直接依赖 | 状态 |
| --- | --- | --- | --- | --- |
| B-DRF | Desktop Runtime Foundation | 单全局桌面宿主、Shell/显示恢复、暂停、托盘、单实例、自启和运行时性能 | 无 | `INACTIVE — C-DRP COMPLETE / C-RLC PLANNED` |
| B-PCR | Poster Core Runtime | 海报状态仓库、几何、透明大幅面、自制标题栏、交互命中和宿主同步 | B-DRF | `ACTIVE — C-PCB` |
| B-TPL | Template Platform | Shadow DOM 运行时、fallback、三套内置模板、热切换、完整模板包生命周期 | B-PCR | `PLANNED` |
| B-COL | Collection and Launch | 快捷方式发现、预览、仅添加、保险库事务、启动、撤销与恢复 | B-PCR | `PLANNED` |
| B-LAR | Layout and Ranking | 启动统计、排序、衰减、对数缩放、弹性布局、锚定与拖拽 | B-COL, B-PCR | `PLANNED` |
| B-THA | Theme Authoring | schema 参数、素材净化、受限覆盖、预览和导入导出 | B-TPL | `PLANNED` |
| B-AIA | Local AI Assistant | BYOK、凭据、受控命令、ChangePlan、预览、确认与撤销 | B-THA, B-COL | `PLANNED` |
| B-GSD | Game Source Discovery | Steam、Epic、Xbox/Store 本地发现、去重与人工修正 | B-COL | `PLANNED` |
| B-DLC | Distribution Lifecycle | 安装、升级、迁移、恢复型卸载、签名和净机发布 | 所有发布范围模块 | `PLANNED` |

## 5. C 工作流路线图

### B-DRF：Desktop Runtime Foundation

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-DHF Desktop Host Feasibility | 关闭 Win+D、Explorer、多屏/DPI、暂停、性能、托盘和宿主决策硬门 | 无 | `COMPLETE` |
| C-DRP Desktop Runtime Productionization | 将验证原型收敛为单全局生产宿主、分辨率/DPI 响应显示拓扑与稳定接口 | C-DHF | `COMPLETE — PRIMARY-SCREEN SINGLE-POSTER CERTIFIED` |
| C-RLC Runtime Lifecycle and Quick Access | 单实例、控制中心、托盘、自启、退出与 TaskbarCreated 恢复 | C-DRP | `PLANNED` |

### B-PCR：Poster Core Runtime

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-PCB Poster Contract Baseline | 冻结状态 schema、宿主/海报边界、窗口几何所有权、默认快照和失败回退行为测试 | C-DRP | `ACTIVE` |
| C-PSS Poster State Store | 版本化 SQLite schema、迁移、仓库接口和过渡 JSON 桥接替换 | C-PCB | `PLANNED` |
| C-LST Large Surface and Transparency | 主屏右侧大幅面透明海报、稳定透明背景与位置恢复 | C-PSS, C-DRP | `PLANNED` |
| C-PSI Poster State and Interaction | EXPANDED/COLLAPSED、USING/EDITING、锁定、标题栏拖动、海报框拉伸（含最小/最大尺寸限制）、命中区域和暂停同步 | C-LST | `PLANNED` |
| C-PCRG Poster Core Release Gate | Debug/Release、真实 Windows 矩阵和核心性能封板 | C-PSI | `PLANNED` |

### B-TPL：Template Platform

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-SRF Shadow Runtime and Fallback | Shadow DOM 渲染、Transparent Default 与独立 minimal-fallback | C-PCB | `PLANNED` |
| C-BTS Built-in Template Suite | Neon HUD、Cinema Lightbox 与三模板一致性矩阵 | C-SRF, C-PSI | `PLANNED` |
| C-TPS Template Package Security | 完整模板包净化、权限预览、原子安装/注册和失败回滚 | C-SRF | `PLANNED` |
| C-THP Template Hot-switch Performance | 热切换、资源释放、暂停和低于 200ms 目标验证 | C-BTS, C-TPS | `PLANNED` |

### B-COL：Collection and Launch

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-ROD Read-only Discovery | 用户主动触发的 `.lnk/.url/.exe` 只读解析、标准化、图标提取与去重 | C-PCRG | `PLANNED` |
| C-PAA Preview and Add-only | 逐项预览、冲突提示和不移动文件的仅添加闭环 | C-ROD | `PLANNED` |
| C-LCH Real Launch | 真实启动、失效入口、错误反馈和启动结果记录 | C-PAA | `PLANNED` |
| C-SVT Safe Vault Transaction | 保险库、两阶段账本、安全收纳、撤销和故障注入 | C-PAA | `PLANNED` |
| C-RCV Recovery and Uninstall Interface | 异常事务恢复、卸载恢复接口和失败保留保险库 | C-SVT, C-LCH | `PLANNED` |

### B-LAR：Layout and Ranking

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-LTM Launch Telemetry Model | 启动次数、最近使用、衰减和数据迁移 | C-LCH | `PLANNED` |
| C-RNK Ranking Engine | 可复现排序得分、稳定排序和跳变衰减 | C-LTM | `PLANNED` |
| C-ELF Elastic Layout Flow | 行式弹性流、子列填充、响应式宽度和布局快照 | C-RNK, C-PSI | `PLANNED` |
| C-PDS Pin Drag and Scaling | 三层控制、锚定、拖拽、对数缩放和动画 | C-ELF | `PLANNED` |

### B-THA：Theme Authoring

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-SPA Schema Parameter Authoring | schema 驱动参数面板、预设和撤销 | C-THP | `PLANNED` |
| C-ASN Asset Sanitization | 图片/SVG/字体净化、限额和素材槽位 | C-TPS | `PLANNED` |
| C-ROC Restricted Overrides | Shadow Root 内 CSS/HTML 覆盖和信任边界 | C-SPA, C-ASN | `PLANNED` |
| C-TIE Theme Import Export | 参数包、素材包、完整模板包导入导出闭环 | C-ROC | `PLANNED` |

### B-AIA：Local AI Assistant

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-CCP Command and ChangePlan | 手动 UI 与 AI 共用命令、预览、确认、撤销和风险分级 | C-TIE, C-LCH | `PLANNED` |
| C-PCC Provider and Credential | BYOK Provider、Credential Manager、无 Key 不联网 | C-CCP | `PLANNED` |
| C-PUA Preview and Undo Agent | 参数/主题草稿受控调用、越权拒绝和失败回退 | C-PCC | `PLANNED` |

### B-GSD：Game Source Discovery

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-SED Steam and Epic Discovery | Steam/Epic 只读来源适配和证据链 | C-ROD | `PLANNED` |
| C-XSD Xbox and Store Discovery | Xbox/Store 来源适配和不可解析项处理 | C-ROD | `PLANNED` |
| C-SDM Source Dedup and Manual Correction | 多来源去重、来源优先级和人工修正 | C-SED, C-XSD | `PLANNED` |

### B-DLC：Distribution Lifecycle

| C 工作流 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-IBC Install Baseline Contract | 安装目录、数据目录、权限、自启和运行时依赖契约 | C-DRP | `PLANNED EARLY` |
| C-UGM Upgrade and Migration | 应用、数据库、模板和用户配置升级迁移与回滚 | C-PSS 与相关 schema 稳定 | `PLANNED` |
| C-URV Uninstall and Recovery | 独立 uninstall.exe、快捷方式恢复和失败保留保险库 | C-RCV, C-UGM | `PLANNED` |
| C-RCG Release Candidate Gate | 净机、签名、许可证、性能、安全和安装卸载矩阵 | 所有 V1 必需工作流 | `PLANNED` |

## 6. 当前唯一活跃链

```text
A: DESKTOPUP_V1_IMPLEMENTATION_PLAN / V1-P1
└─ B: Poster Core Runtime / B-PCR
   └─ C: Poster Contract Baseline / C-PCB ACTIVE
```

当前推进顺序：

```text
C-DRP 单全局生产宿主
→ C-PCB 海报契约
→ C-PSS 版本化状态仓库
→ C-IBC 安装/数据目录早期契约
→ C-RLC 运行生命周期
→ C-LST / C-PSI / C-PCRG 海报核心
→ C-ROD / C-PAA / C-LCH 可用 MVP
→ C-SVT / C-RCV 安全收纳 MVP
```

`C-DHF-01..08` 与 `C-DRP-01..06` 已关闭；C-DRP 的发布认证范围明确限制为主屏单海报，多显示器/DPI/热插拔未认证。当前只激活 `C-PCB`；C-PSS 是唯一后继工作流，不得与 C-PCB 同时标记 `ACTIVE`，也不得将 V1-P1 或 M1 标记为实施通过。

## 7. 性能预算与迁移门

以下数字是 Release 测试预算，不是尚未测量的产品承诺；超出预算时必须阻塞对应 C 的完成，或记录用户接受的例外：

- 静态与暂停状态：前端不得维持持续 `requestAnimationFrame`，目标持续渲染帧率为 `0 FPS`；
- 活跃动画：默认不高于 `30 FPS`，模板可进一步降级；
- A0 基准机上，单宿主 + 单海报静态/暂停 10 秒窗口的进程 CPU 时间增量目标不高于 `0.10s`；
- A0 基准机上，单宿主 + 单海报 Release 工作集目标不高于 `50 MiB`；
- 事件恢复不得依赖高频轮询，重复事件必须合并；
- SQLite schema 必须版本化；每次 schema 变化同时提交向前迁移、失败回滚/保留策略和幂等测试；
- 过渡 `poster-snapshot-bridge-v1.json` 只有在 C-PSS 完成迁移验证后才能退出权威状态路径。

## 8. 任务状态规则

- `PLANNED`：已进入路线图但未满足激活条件；
- `PLANNED NEXT`：当前 C 完成后优先激活；
- `ACTIVE`：由 `ACTIVE_PATH.md` 唯一选中；
- `IN PROGRESS`：活跃 C 内已开始但尚未通过；
- `BLOCKED`：存在明确外部阻塞；
- `COMPLETE`：完成标准、自动化证据和所需实机矩阵均满足；
- `FAIL WITH ACCEPTED FALLBACK`：主方案失败，但回退被明确接受并形成验证证据；
- `SUPERSEDED`：被新计划替代，仅供追溯，不能继续新增任务。

## 9. 阶段验收证据

每个 C 完成至少提交：

1. 对应 `TASK_INDEX.md` 状态；
2. 实施/验证日志；
3. 自动化测试结果；
4. 需要 Windows 真实操作时的人工矩阵；
5. 代码锚点和当前 handoff；
6. 失败路径、降级与恢复结论；
7. 对上游 B 阶段门的影响。

## 10. V1 发布完成定义

- 桌面宿主在支持环境稳定运行且有明确回退；
- 三套首发模板、Shadow DOM、热切换和完整模板安装通过；
- 导入、仅添加、安全收纳、启动、撤销、恢复和卸载形成闭环；
- 排名、缩放、弹性布局和人工控制可验证；
- 可选 AI 无 Key 不联网，越权命令无法执行；
- `DesktopUpSetup.exe` 与独立 `uninstall.exe` 通过净机矩阵；
- 用户快捷方式在故障注入、升级和卸载中不丢失、不覆盖；
- A/B/C 状态、文档、代码与验证证据一致。

## 11. 明确待定但不阻塞当前阶段

- Transparent Default 正式名称与完整视觉规格；
- AI 低风险免确认范围；
- `DesktopUP` 正式产品名和品牌图标；
- 受限 JavaScript 主题是否进入后续版本；
- 安装自启选项的最终界面。

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-09 | V1.1 | 唯一活跃链切换到 V1-P1/C-DRP；新增 C-PSS，拆分真实启动与恢复；将“仅添加 + 真实启动”提前为可用 MVP，并在完整模板平台前建立安全收纳 MVP；补充交付检查点、性能预算和 schema 迁移门 |
| 2026-08-04 | V1 | 建立 V1 唯一 A 层落地计划；拆分 9 个 B 模块和 33 个 C 工作流；冻结旧制作计划；保留当前 C-DHF 活跃链和未完成硬门 |

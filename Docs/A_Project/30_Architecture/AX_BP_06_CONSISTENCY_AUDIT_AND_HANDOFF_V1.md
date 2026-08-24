# AX-BP-06：一致性审计、蓝图冻结与后续交接

状态：COMPLETE / FROZEN — AX-BP-01～06 CLOSED / AXIOM EXECUTION EXITED

建立日期：2026-08-24

## 1. 任务目的

本文关闭 AX-BP-01～06。

AX-BP 的职责不是实现 DesktopUp V2，而是仅使用 Axiom Core 的系统描述方法，把冻结需求转换为可追溯、框架中立的程序蓝图，并在退出前确认：

1. 没有重复且相互竞争的职责；
2. 没有与冻结需求或 DesktopHost 合同冲突的关系；
3. 没有遗漏正式运行表面、领域能力、外部边界或失败路径；
4. 没有把未选择的 renderer、UI、StateStore、进程、IPC、安装或调度技术写成事实；
5. 没有依赖、推荐或借用 unstable axiom-runtime；
6. 后续技术选型和完整架构能够接收一套稳定输入。

本文不新增第八份需求，不关闭产品 TBD，不批准技术候选，也不启动生产代码。

## 2. 审计输入

### 2.1 冻结产品输入

1. [完整需求画像](../10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)
2. [海报视觉与交互](../10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md)
3. [运行表面、托盘与生命周期](../10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md)
4. [内容 Item、活跃度与动态排版](../10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md)
5. [主题平台与 AI](../10_Requirements/THEME_PLATFORM_AND_AI_V1.md)
6. [风险、开放问题与延期项](../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)
7. [V1 → V2 迁移追溯](../10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md)
8. [需求一致性审计证据](../10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md)

### 2.2 已冻结技术合同输入

- [DesktopHost 原型接口](../20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md)
- [E2 合同审计](../20_Technical_Selection/TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md)
- [E3 生命周期合同](../20_Technical_Selection/TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md)
- [E4 几何、命中与渲染原子合同](../20_Technical_Selection/TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md)
- [E5 诊断与一致性夹具](../20_Technical_Selection/TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md)
- [E6 候选交接](../20_Technical_Selection/TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md)

### 2.3 AX-BP 产物输入

1. [Axiom Core 程序蓝图](AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md)
2. [AX-BP-01 Core 语义映射](AX_BP_01_AXIOM_CORE_SEMANTIC_MAPPING_V1.md)
3. [AX-BP-02 整体程序拓扑](AX_BP_02_PROGRAM_TOPOLOGY_V1.md)
4. [AX-BP-03 模块与端口冻结](AX_BP_03_MODULE_PORT_FREEZE_V1.md)
5. [AX-BP-04 调用、数据与组合关系冻结](AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md)
6. [AX-BP-05 物理执行边界冻结](AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md)
7. [非生产 Axiom Core 类型夹具](../../../prototype/axiom-desktopup-blueprint/README.md)

## 3. 审计方法

审计使用五次独立检查：

| 检查 | 问题 | 判定方式 |
| --- | --- | --- |
| Inventory | 节点、模块、端口、流和物理边界是否齐全 | 稳定 ID 枚举与数量核对 |
| Traceability | 七份需求、12 TBD、4 DEFER 是否都有落点 | 需求到拓扑/端口/流/边界交叉表 |
| Authority | 是否存在第二条业务写入、投影或 AI 旁路 | CAS 闭环和禁止边审计 |
| Neutrality | 是否暗中选定生产技术或 runtime | 术语、依赖和实现承诺扫描 |
| Consistency | 文档间职责、状态、错误与关系是否矛盾 | AX-BP-01 → 05 顺序对照 |

编译成功只证明 Core 类型组合有效；它不替代 Windows 层级、Win+D、Explorer 恢复、输入、DPI、性能或 Release 人工验证。

## 4. Core-only 决策复核

| 决策 | 结果 |
| --- | --- |
| Axiom 位于架构方法与技术研究层 | PASS |
| 只使用 Axiom Core 描述系统 | PASS |
| PortCell/组合/Slot/Conforms/Blueprint/Static 语义无越界 | PASS |
| Core 类型夹具是非生产证据 | PASS |
| axiom-runtime 不进入依赖 | PASS |
| axiom-runtime 不作为测试证据 | PASS |
| axiom-runtime 不决定线程、队列、进程、IPC 或调度 | PASS |
| AX-BP-06 后 Axiom 退出执行主线 | PASS |

## 5. 蓝图清单审计

### 5.1 逻辑系统与模块

AX-BP-02 的 SYS-01～SYS-20 与 AX-BP-03 的 MOD-01～MOD-20 一一对应。每个逻辑角色都有：

- 唯一主职责；
- 显式输入和输出；
- 自有状态或明确的无状态声明；
- 错误族；
- 禁止访问；
- 对应外部 Slot 或纯逻辑边界。

没有发现两个模块共同拥有正式业务状态，也没有发现 surface、renderer、host、AI 或 diagnostics 自行成为写入权威。

### 5.2 流与组合

AX-BP-04 冻结九类关系：

1. Command flow；
2. State flow；
3. Projection flow；
4. Effect flow；
5. Observation flow；
6. Diagnostic flow；
7. Recovery flow；
8. Draft flow；
9. Confirmation flow。

顶层 F-001～F-010 和各领域子流共同覆盖启动、状态写入、海报呈现、控制中心、托盘、Item、Theme、AI、恢复、退出、发布和诊断。

### 5.3 物理执行边界

AX-BP-05 冻结 PEB-01～PEB-15：

- surface/system；
- StateStore；
- DesktopHost；
- Renderer；
- gesture/input；
- discovery/file；
- launch；
- theme package；
- AI provider；
- tray/system；
- diagnostics；
- distribution；
- single-instance；
- time；
- power/session。

这些 PEB 只冻结 I/O 类型、资源所有权、顺序、终态、取消、deadline、信任区和故障隔离要求；没有选择进程数、线程数、IPC、数据库、GPU、UI 或安装器。

## 6. 冻结需求覆盖审计

| 需求域 | 拓扑落点 | 端口/流/边界落点 | 结果 |
| --- | --- | --- | --- |
| 单海报视觉、位置、尺寸、锁定、收起 | PosterDomain、ProjectionGraph、PosterSurface | geometry/revision、gesture、Renderer/DesktopHost PEB | PASS |
| 桌面层级、Win+D、Explorer、命中与透明空白 | DesktopHost | Host events、hit-map、presentability、PEB-03/05 | PASS |
| 控制中心、托盘、单实例与生命周期 | ControlCenter、TraySurface、Lifecycle | draft/commit、tray commands、start/exit flow、PEB-01/10/13 | PASS |
| StateStore 唯一权威与恢复 | StateAuthority、Recovery | expected-revision/CAS、accepted snapshot、PEB-02 | PASS |
| Item 发现、启动、活跃度与动态排版 | ItemDomain、ActivityPolicy、LayoutPolicy | discovery/launch/success gate/layout flows、PEB-06/07/14 | PASS |
| 用户文件安全与安全收纳 | FileSafety | confirmation、recovery ledger、PEB-06 | PASS |
| 主题包、能力、预算与 last-known-good | ThemeDomain | validate/compile/apply/error flow、PEB-08 | PASS |
| AI provider、ChangePlan、确认与审计 | AIDomain、Policy | provider → plan → confirmation → ordinary command、PEB-09 | PASS |
| 诊断、呈现事实与故障定位 | Diagnostics | observation-only events、PEB-11 | PASS |
| 安装、自启、卸载和个人自用边界 | Distribution | explicit opt-in、cleanup contract、PEB-12 | PASS |

## 7. 开放项与延期项保留审计

### 7.1 12 个开放项

V2-TBD-001～012 均在 AX-BP-02 有显式占位：

- 技术边界、进程和模块绑定；
- 活跃度公式；
- 排版阈值；
- 正常默认视觉；
- 主题扩展安全细节；
- AI 免确认策略；
- EDITING 双击；
- 品牌；
- 无障碍发布门；
- 多海报/多屏重新激活；
- LaunchSuccessGate；
- 主题包表达。

AX-BP 没有替用户关闭这些决定。已经冻结的安全底线继续有效。

### 7.2 4 个延期项

| ID | 保留方式 | 审计结果 |
| --- | --- | --- |
| V2-DEFER-001 多海报 | PosterCollection 作为 inactive capability；首发只激活单海报 | PASS |
| V2-DEFER-002 完整多屏认证 | DisplayTopology 接口保留；发布认证延期 | PASS |
| V2-DEFER-003 在线市场/云/共享 | ExternalThemeServices 无激活边和网络资源 | PASS |
| V2-DEFER-004 未确认自动整理 | 无后台自动效果边；只保留用户主动确认的安全收纳 | PASS |

延期能力不得因为“预留”而启动资源、产生网络连接、移动文件或改变正式状态。

## 8. 单一权威与因果闭环审计

唯一合法正式写入闭环为：

~~~text
Intent
  -> Policy / Validation
  -> CommandEnvelope(expected_revision)
  -> StateStore CAS
  -> AcceptedSnapshot(new_revision)
  -> ProjectionGraph
  -> Poster / Control Center / Tray / Diagnostics
~~~

以下规则全部成立：

- UI draft 不是 accepted state；
- Poster/Control Center/Tray 不直接互写；
- DesktopHost 只报告平台事实，不改业务快照；
- Renderer 只呈现同 revision 投影，不写几何或层级状态；
- AI 只产生 ChangePlan，不拥有第二条执行路径；
- Item launch 必须经过 LaunchSuccessGate 后才可提交统计；
- Theme 失败保留 last-known-good；
- Diagnostics 只观察，不修复；
- stale revision 只返回冲突，不静默覆盖。

结果：PASS。

## 9. Host、Renderer 与 Surface 边界审计

| 责任 | 唯一所有者 | 禁止旁路 |
| --- | --- | --- |
| 桌面层级、attach/recover/detach | DesktopHost adapter | Renderer/UI 不得改 z-order |
| 图形内容、材质、文本和帧 | Renderer adapter | Host 不实现视觉主题 |
| 命中岛、透明空白与 gesture session | Gesture/Input + Host contract | DOM/UI 默认命中不得扩张 |
| 业务位置、尺寸、锁定和收起 | StateStore accepted state | HWND 临时几何不得成为权威 |
| surface 显示/关闭 | Lifecycle + surface adapter | 托盘不得直接操作私有 HWND |

结果：PASS。

## 10. 发现并修复的问题

| ID | 发现 | 处理 | 状态 |
| --- | --- | --- | --- |
| AX-AUD-001 | 早期文档曾把 Axiom 写成可能的生产 runtime | 统一改为架构方法/技术研究层 | RESOLVED |
| AX-AUD-002 | 早期 AX-BP-02～06 曾包含实现协调器和候选接入 | 重写为拓扑、端口、关系、物理边界和审计 | RESOLVED |
| AX-AUD-003 | AX-BP-02 声称保留 4 个 DEFER，但未逐项列出 | 补入 V2-DEFER-001～004 及 inactive 规则 | RESOLVED |
| AX-AUD-004 | 主蓝图“可安全开发范围”越过 Axiom 研究边界 | 改为“可冻结范围”，删除生产开发授权 | RESOLVED |
| AX-AUD-005 | 主蓝图残留 Carrier/SlotDrive 实现词汇 | 改为框架中立的物理 adapter/扩展钩子 | RESOLVED |
| AX-AUD-006 | 模块和 adapter 容易被误读为同一层 | MOD 定义逻辑职责；PEB/Slot 定义待选物理边界 | RESOLVED |
| AX-AUD-007 | 动态 Item 集合可能误用有限静态 Rep | 明确静态 Core 图不等于动态业务集合 | RESOLVED |
| AX-AUD-008 | 控制中心 draft 可能成为第二状态源 | 冻结 draft → validate → CAS → accepted 投影 | RESOLVED |
| AX-AUD-009 | Host 与 Renderer 容易重新耦合 | 冻结两个独立端口及禁止边 | RESOLVED |
| AX-AUD-010 | AI/主题扩展可能形成非受控执行通道 | 冻结 ChangePlan、capability、确认与普通命令闭环 | RESOLVED |
| AX-AUD-011 | 进程/线程/IPC 约束可能被误写为方案 | PEB 只给候选约束，不给部署答案 | RESOLVED |
| AX-AUD-012 | 入口、目录和阶段状态仍停在 AX-BP-02 | 已统一更新为 FROZEN/COMPLETE | RESOLVED |

## 11. 重复、矛盾、遗漏与实现假设结论

### 11.1 重复

没有发现相互竞争的正式业务权威、层级所有者、渲染所有者、文件安全所有者或 AI 执行路径。

不同文档重复出现的不变量属于追溯性重复，不构成第二定义源；发生冲突时仍以冻结需求和最新用户决定优先。

### 11.2 矛盾

没有发现 AX-BP-01～05 与七份冻结需求或 DesktopHost E2～E6 的未解释矛盾。

“海报高于图标和壁纸、低于普通/系统/全屏程序”在需求、Host 合同、拓扑和 PEB 中一致。

### 11.3 遗漏

20 个逻辑角色、九类关系、15 个 PEB、12 个 TBD、4 个 DEFER、正常/降级/失败/恢复/退出路径均已登记。未发现会阻止后续技术选型的未解释遗漏。

### 11.4 实现假设

以下仍为 UNDECIDED，不得由 AX-BP 推导：

- renderer 与 UI 框架；
- DesktopHost 正式 adapter；
- StateStore 产品技术与数据格式；
- 单进程或多进程；
- 线程、队列、scheduler、IPC 与序列化；
- GPU、合成、文本与动画技术；
- 主题容器、扩展名与受限脚本机制；
- AI provider、模型与传输协议；
- 安装、自启、升级、签名和卸载工具；
- Axiom Core 的长期依赖交付方式。

## 12. 冻结输出

AX-BP 冻结以下架构方法层事实：

1. Axiom Core 语义映射和适用边界；
2. DesktopUpProgram 整体逻辑拓扑；
3. SYS-01～20 / MOD-01～20 稳定职责；
4. 模块输入、输出、状态、错误和禁止访问；
5. 11 类外部 adapter Slot；
6. 九类调用/数据/组合关系及 F-* 因果流；
7. StateStore expected-revision/CAS 单一权威闭环；
8. accepted snapshot 的同 revision 投影；
9. Host/Renderer/Surface/Input 分离；
10. PEB-01～15 的 I/O、资源、顺序、终态和信任边界；
11. 12 TBD 与 4 DEFER 的显式保留；
12. 禁止旁路和错误保留规则。

冻结不等于产品实现完成，也不等于任何候选技术通过。

## 13. 后续技术选型输入

TS-C01～TS-C07 及 TS-03 支撑域必须接收：

- DesktopHost 与 Renderer 独立绑定；
- 同 revision geometry/hit/render 原子要求；
- 不通过瞬时置顶、隐藏/恢复补丁实现 Win+D；
- 透明空白保持桌面行为；
- 资源所有者、终态事件和故障隔离要求；
- StateStore CAS 与 projection consistency；
- draft、accepted、runtime observed facts 的严格分离；
- 主题/AI/文件/启动的安全边界；
- 候选不得以改变冻结需求换取通过。

候选 P0 仍是 NOT RUN；AX-BP 不能把某候选标为 SELECTED。

## 14. 后续完整架构输入

技术选型产生赢家后，完整架构阶段必须：

1. 把每个 MOD 绑定到获选组件；
2. 把每个 Slot 绑定到正式 adapter；
3. 把每个 PEB 落到明确进程、线程、队列、IPC 或同进程调用；
4. 冻结产品 DTO、序列化、存储 schema 与迁移；
5. 将平台错误映射到冻结错误族；
6. 给出启动、恢复、退出和卸载的可执行时序；
7. 建立性能、故障、安全和发布验证计划；
8. 再据此建立总体制作计划和 B/C 工作流。

完整架构不得修改本蓝图的不变量；如必须修改，先执行变更控制。

## 15. 变更控制

AX-BP 冻结后允许三类变更：

| 变更 | 处理 |
| --- | --- |
| 用户修改产品需求 | 更新需求、追溯表，并重新审计受影响 AX-BP |
| 技术候选证明某约束不可实现 | 保留失败证据；不得静默降低需求，提交决策 |
| 发现蓝图内部错误 | 记录 AX-AUD 新条目，修正文档并重跑 Core/链接/一致性验证 |

单纯的框架偏好、实现方便或历史代码存在，不构成修改冻结蓝图的理由。

## 16. Axiom 退出声明

AX-BP-06 已完成，当前状态为：

- Axiom Core 保留为已冻结的系统描述方法；
- 类型夹具保留为非生产结构证据；
- axiom-runtime 继续明确排除；
- 不再建立 Axiom 生产 runtime、调度、线程或 adapter 工作流；
- 下一主动作返回技术选型，而不是继续扩张 AX-BP；
- 生产代码必须等待对应候选和完整架构获得批准。

## 17. AX-BP 总体退出门

| 检查 | 结果 |
| --- | --- |
| AX-BP-01～05 产物齐全 | PASS |
| 七份冻结需求均有蓝图落点 | PASS |
| 20 个逻辑角色与模块一一对应 | PASS |
| 模块职责与端口无未解释重复 | PASS |
| 九类关系和因果闭环完整 | PASS |
| StateStore 单一业务权威无旁路 | PASS |
| Host/Renderer/Input/Surface 边界明确 | PASS |
| 15 个物理执行边界和资源所有者明确 | PASS |
| 12 TBD 与 4 DEFER 完整保留 | PASS |
| 未暗中选择 renderer/UI/store/process/IPC/installer | PASS |
| axiom-runtime 依赖、推荐与证据为零 | PASS |
| 重复、矛盾、遗漏和实现假设已审计 | PASS |
| 可交给技术选型和完整架构 | PASS |
| Core、夹具、当前权威链接与 Git 文档检查 | PASS — Core 21+5+6、夹具 6、91 文档/108 本地链接/0 断链；V1 只读历史快照 7 个旧断链不属于当前门 |

## 18. 下一动作

AX-BP-01～06 已标记为 FROZEN/COMPLETE，Axiom 已退出执行主线。

后续主线恢复为：

~~~text
TS-C01～TS-C07 renderer adapter / P0 evidence
  -> TS-03 supporting technology domains
  -> TS-04 unified rerun
  -> TS-05 selection and user approval
  -> full architecture
  -> production plan and B/C workflows
~~~

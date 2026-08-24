# Desktop-Up V2 文档结构与阶段治理

## 1. 当前结构

当前仓库已完成需求冻结，技术选型仍在执行；与 renderer/UI 候选正交的 Axiom Core 系统描述蓝图已经完成并冻结。当前权威链为：

```text
Docs/00_Catalog/REQUIREMENTS_INDEX.md
├─ Docs/A_Project/10_Requirements/                 [FROZEN]
├─ Docs/A_Project/20_Technical_Selection/          [ACTIVE / NO RENDERER WINNER]
└─ Docs/A_Project/30_Architecture/                 [AXIOM CORE BLUEPRINT FROZEN]
```

目录职责：

| 路径 | 职责 | 当前状态 |
| --- | --- | --- |
| `00_Catalog/` | 需求入口、当前阶段与文档登记 | `ACTIVE` |
| `A_Project/10_Requirements/` | 完整、自包含、技术中立的 V2 产品需求 | `FROZEN` |
| `A_Project/20_Technical_Selection/` | 候选事实、统一证据矩阵、选型与淘汰记录 | `ACTIVE / NO RENDERER WINNER` |
| `A_Project/30_Architecture/` | Axiom Core 系统描述、状态权威、模块/端口、关系与物理边界 | `AX-BP-01～06 COMPLETE / FROZEN` |
| `Reference/V1_Design_Source/` | V1 原始设计快照与迁移来源 | `HISTORICAL / READ-ONLY` |
| `Rules/` | 文档、Git、版本和发布治理底线 | `ACTIVE` |

目前没有活跃的 B 模块、C 工作流、总体制作计划、交接或正式实现日志。Axiom 已在 AX-BP-06 后退出执行主线；renderer/UI、StateStore、生产 runtime、进程与安装架构仍未冻结。

## 2. 阶段顺序

文档只能随阶段逐步建立：

1. **需求**：冻结行为、范围、不变量、风险和验收，不指定实现技术；
2. **技术选型**：从需求反推候选，用同一证据矩阵比较，不先写结论；
3. **架构设计**：框架中立的程序定义可由用户显式提前激活；进程、窗口、渲染、状态存储、IPC 和安装的完整架构仍须等待对应选型证据；
4. **制作计划**：架构冻结后安排阶段、依赖、闸口、交付物和退出条件；
5. **工作流程**：把计划拆为可验证的 B 模块与 C 工作流；
6. **代码落地**：只执行已激活工作流，并同步验证证据。

不得为了提前开工而伪造未选择 adapter 的结论。Axiom Core 的受控研究阶段已经关闭；冻结蓝图不授权生产 UI、renderer、runtime、存储或安装实现。

## 3. 权威规则

- `10_Requirements/` 中的内容描述“必须实现什么”，不描述“准备怎样实现”。
- 技术候选、架构草案和原型证据必须存入独立阶段目录，不能回写为需求事实。
- `30_Architecture/` 当前只冻结 Axiom Core 系统描述蓝图，不能被解释为完整生产架构已经冻结。
- 失败原型是选型证据，不是产品需求，也不能成为下一轮默认架构。
- 任何从历史实现提取的结论都必须重新接受当前需求矩阵验证。
- 若用户修改需求，先更新需求与追踪表，再判断下游文档是否失效。

## 4. 状态枚举

`DRAFT / PROPOSED / ACTIVE / AUDITED / FREEZE CANDIDATE / FROZEN / BLOCKED / COMPLETE / SUPERSEDED / HISTORICAL`

技术选型为 `ACTIVE / NO RENDERER WINNER`；Axiom Core 蓝图为 `AX-BP-01～06 COMPLETE / FROZEN`；完整生产架构、制作计划和 B/C 工作流仍为 `NOT STARTED`。

# Desktop-Up V2 文档结构与阶段治理

## 1. 当前结构

当前仓库已完成需求冻结并进入技术选型阶段。当前权威链为：

```text
Docs/00_Catalog/REQUIREMENTS_INDEX.md
├─ Docs/A_Project/10_Requirements/                 [FROZEN]
└─ Docs/A_Project/20_Technical_Selection/          [ACTIVE]
```

目录职责：

| 路径 | 职责 | 当前状态 |
| --- | --- | --- |
| `00_Catalog/` | 需求入口、当前阶段与文档登记 | `ACTIVE` |
| `A_Project/10_Requirements/` | 完整、自包含、技术中立的 V2 产品需求 | `FROZEN` |
| `A_Project/20_Technical_Selection/` | 候选事实、统一证据矩阵、选型与淘汰记录 | `ACTIVE / NO WINNER` |
| `Reference/V1_Design_Source/` | V1 原始设计快照与迁移来源 | `HISTORICAL / READ-ONLY` |
| `Rules/` | 文档、Git、版本和发布治理底线 | `ACTIVE` |

目前没有活跃的 B 模块、C 工作流、架构设计、制作计划、交接或实现日志；技术选型是唯一活跃的下游阶段。

## 2. 阶段顺序

文档只能随阶段逐步建立：

1. **需求**：冻结行为、范围、不变量、风险和验收，不指定实现技术；
2. **技术选型**：从需求反推候选，用同一证据矩阵比较，不先写结论；
3. **架构设计**：在选型证据通过后定义进程、窗口、渲染、状态、IPC、安全和恢复契约；
4. **制作计划**：架构冻结后安排阶段、依赖、闸口、交付物和退出条件；
5. **工作流程**：把计划拆为可验证的 B 模块与 C 工作流；
6. **代码落地**：只执行已激活工作流，并同步验证证据。

不得为了提前开工而并行伪造后续阶段结论。

## 3. 权威规则

- `10_Requirements/` 中的内容描述“必须实现什么”，不描述“准备怎样实现”。
- 技术候选、架构草案和原型证据必须存入以后新建的独立阶段目录，不能回写为需求事实。
- 失败原型是选型证据，不是产品需求，也不能成为下一轮默认架构。
- 任何从历史实现提取的结论都必须重新接受当前需求矩阵验证。
- 若用户修改需求，先更新需求与追踪表，再判断下游文档是否失效。

## 4. 状态枚举

`DRAFT / PROPOSED / ACTIVE / AUDITED / FREEZE CANDIDATE / FROZEN / BLOCKED / COMPLETE / SUPERSEDED / HISTORICAL`

技术选型现为 `ACTIVE`；在用户批准选型结论并激活下一阶段前，架构、计划和工作流状态统一为 `NOT STARTED`。

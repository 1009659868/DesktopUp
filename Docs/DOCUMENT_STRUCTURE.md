# Desktop-Up 文档结构与阅读规则

## 1. 结构目标

文档使用三层计划模型，把项目方向、能力建设和具体实施分开，避免设计稿、任务表、过程日志和当前状态互相覆盖。

| 层级 | 正式名称 | 回答的问题 | 更新粒度 |
| --- | --- | --- | --- |
| A | Project Mainline | 产品当前处于什么阶段，为什么优先做这一阶段 | 产品阶段、范围或发布目标变化 |
| B | Capability Module | 一项能力由谁负责，边界、依赖和模块里程碑是什么 | 能力里程碑、接口或所有权变化 |
| C | Workstream | 当前具体做什么，如何验证，证据和下一步是什么 | 每个实现或验收任务 |

A/B/C 是决策与执行层级，不等同于源码目录。一个 B 能力可以横跨 Rust、前端和 Windows 适配层，但必须只有一个权威计划。

## 2. 每层固定分区

| 分区 | 保存内容 | 不保存内容 |
| --- | --- | --- |
| `00_Catalog/` | 下一级索引、任务索引、决策索引 | 设计正文、过程叙述 |
| `10_Plan/` | 未来工作、依赖、验收标准、设计方案 | 冗长命令输出、已完成过程 |
| `20_Log/` | 已完成事实、决策结果、验证证据 | 持续变化的“下一步” |
| `30_Sync/` | 当前状态、阻塞、下一动作、接手入口 | 长期技术说明和历史大全 |

同一事实只设置一个权威归属：未来在 Plan，过去在 Log，现在在 Sync，导航在 Catalog。

## 3. 根目录职责

| 路径 | 职责 |
| --- | --- |
| `README.md` | 唯一人工阅读入口 |
| `DOCUMENT_STRUCTURE.md` | A/B/C 规则与阅读方法 |
| `00_Catalog/` | 活跃路径、B 模块索引、文档登记 |
| `A_Project/` | 项目主线、设计、交付计划、项目级日志与交接 |
| `B_Modules/` | 能力计划、模块日志和模块交接 |
| `C_Workstreams/` | 执行计划、任务索引、实施日志和精确交接 |
| `Rules/` | 文档治理、版本、更迭与归档规则 |
| `Reference/` | 外部资料和非权威调研 |

## 4. 标准阅读路径

### 新开发者或新 Agent

1. `README.md`
2. `00_Catalog/ACTIVE_PATH.md`
3. `A_Project/30_Sync/COLD_START.md`
4. 活跃 B 的 `30_Sync/COLD_START.md`
5. 活跃 C 的 `30_Sync/COLD_START.md`
6. C 的 `WORKSTREAM_PLAN.md`、`TASK_INDEX.md` 和 `CURRENT_HANDOFF.md`

### 查看项目方向

1. `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`
2. `A_Project/10_Plan/DESKTOPUP_V1_DESIGN_BASELINE.md`
3. `A_Project/00_Catalog/DESIGN_INDEX.md`
4. 按当前 B/C 活跃链读取对应模块和工作流计划

### 执行开发任务

1. 当前 C `10_Plan/WORKSTREAM_PLAN.md`
2. 当前 C `00_Catalog/TASK_INDEX.md`
3. 当前 C `30_Sync/CURRENT_HANDOFF.md`
4. 最近一份 C 实施日志

## 5. 同步触发器

- C：任务完成、阻塞改变、验证完成或结论被推翻时更新。
- B：模块里程碑、接口边界、依赖或主要工作流改变时更新。
- A：产品范围、阶段、优先级、发布目标或关键技术方向改变时更新。
- C 的实现细节不得逐条复制到 B/A，只上卷已验证结论和影响。
- `COLD_START.md` 只描述阅读顺序，不复制完整计划。
- `CURRENT_HANDOFF.md` 可以更新；`20_Log` 历史记录原则上不回写。

## 6. 状态枚举

文档和任务统一使用：

- `DRAFT`：内容正在形成，尚未成为执行依据。
- `PROPOSED`：方案已完整，等待确认。
- `ACTIVE`：当前权威执行项。
- `BLOCKED`：存在明确阻塞，无法继续执行。
- `COMPLETE`：完成标准和验证均已满足。
- `SUPERSEDED`：被新文档替代，保留用于追溯。

## 7. 归档原则

- 不删除仍可能解释历史决策的文档。
- 被替代的设计改为 `SUPERSEDED`，并在顶部指向新版本。
- C 日志按 `20_Log/YYYY-MM/YYYY-MM-DD_topic.md` 保存。
- 大规模归档阶段再建立 `_ArchiveVersions/`；当前项目初期不创建空归档树。

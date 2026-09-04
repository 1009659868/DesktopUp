# V3-04 SQLite 状态底座合同

状态：`FROZEN FOR V3-04-01 — SCHEMA VERSION 1`

最后更新：2026-09-04

## 1. 职责与边界

SQLite 是 DesktopUp 结构化业务状态的唯一持久化权威。海报、控制中心和托盘只能通过 Application 端口读取快照或提交命令，不能各自保存一份可写状态，也不能直接持有 SQLite 连接。

V3-04-01 只建立版本化状态底座，不提前定义 Poster、Settings、CollectionItem 或活跃度业务表：

- schema/migration 身份；
- 全局 committed revision；
- expected revision compare-and-swap；
- operation id 幂等重放；
- 迁移前备份和手动在线备份；
- 完整性校验、失败保留和启动顺序门。

Poster/Settings 业务表和 repository 属于 V3-04-02；真实 Item 属于 V3-05；启动统计、热度和动态排版属于 V3-06。

## 2. 文件与身份

| 项 | 合同 |
| --- | --- |
| 数据库 | `%LOCALAPPDATA%\DesktopUp\V3\state\desktopup.sqlite3` |
| 备份目录 | `%LOCALAPPDATA%\DesktopUp\V3\state\backups` |
| application id | `0x44555033`，ASCII `DUP3` |
| 当前 schema | `PRAGMA user_version = 1` |
| journal | WAL |
| durability | `synchronous=FULL` |
| foreign keys | 每连接启用 |
| busy timeout | 5000 ms |

JSON 继续只用于结构日志、资源账本、诊断导出和未来显式导入/导出，不构成第二数据库。

## 3. Schema v1

### `schema_migrations`

记录每个迁移的版本、稳定名称、SQL SHA-256 和应用时间。启动时重新计算当前代码内迁移校验值；版本存在但名称或校验值不一致时视为状态身份损坏，禁止继续启动。

### `state_head`

只有 `singleton_id = 1` 一行，保存当前 `schema_version`、全局 `revision`、最近 operation id 与更新时间。revision 从 0 开始，只允许事务提交后单调加一。

### `state_operations`

保存 `operation_id`、expected revision、committed revision 和提交时间。`committed_revision = expected_revision + 1`，operation id 唯一，committed revision 唯一。

它是轻量幂等账本，不是 UI 撤销历史。完整命令/撤销语义在后续 Application command 层建立。

## 4. CAS 提交

一次写入在同一个 immediate SQLite transaction 中执行：

1. 读取 `state_head`；
2. 若 operation id 已以相同 expected revision 提交，返回 `Replayed`，不重复执行业务 mutation；
3. 若当前 revision 与 expected revision 不同，返回 `Conflict` 和最新 revision，不覆盖新状态；
4. 执行业务 mutation；
5. 以 `WHERE revision = expectedRevision` 更新 head；
6. 写入 operation journal；
7. 一次 commit。

业务 mutation、head 和 operation journal 任一步失败，整个事务回滚；同一 operation id 可在修复失败原因后重新执行。

## 5. 迁移与备份

- 新空库直接应用 v1，不制造无意义的“迁移前空备份”；
- 有内容且版本低于当前代码的数据库必须先通过 `quick_check` 和 application id 检查；
- 迁移前使用 SQLite online backup API 复制一致快照；
- 备份先写同目录 `.tmp`，通过完整性、身份、schema/revision 校验后再原子改名；
- 正式文件名包含 UTC 时间、revision/legacy 和受限 reason，不覆盖旧备份；
- 备份失败则迁移失败，不能先迁移后补备份；
- 发现高于当前程序支持版本的数据库时拒绝降级打开。

## 6. 失败保留

无法打开、`quick_check` 失败、application id 不符、迁移账本被篡改、head 缺失或备份失败时：

- 不删除、不改名、不重建原数据库；
- 不以空白默认海报继续运行；
- 记录结构化失败原因；
- 在托盘、控制中心和海报创建前终止本次启动。

用户可理解的恢复 UI 与最近可用状态属于 V3-04-06；在它完成前，失败策略仍以“不破坏原始证据、不伪装成功”为优先。

## 7. 后续 schema 规则

- 每个版本只能新增一个不可变迁移；已发布迁移不得原地编辑；
- schema 变化必须先更新上层领域/仓储合同，再追加迁移和升级测试；
- 任何海报/设置写入均通过同一全局 revision，不能各自建立独立、无法比较的版本号；
- 活跃度原始数据可以在 V3-05/06 追加业务表，但排序结果和布局快照优先视为可重建 projection，不复制为 UI 私有权威。

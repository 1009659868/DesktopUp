# Collection Item Model and Projection 工作流计划

日期：2026-08-14

状态：`ADVANCE PLAN — AFTER C-PSI`

## 1. 目的

建立正式 `CollectionItem`/`PosterItem` 数据契约和应用自有 SQLite 仓库，以隔离夹具驱动海报的零/一/多/失效 Item 状态，并让硬编码 `GAME_CATALOG` 退出正式运行路径。

## 2. 范围

本工作流负责：

- `CollectionItem` 的稳定身份、显示、图标来源、启动目标元数据与有效性状态；
- `PosterItem` 的海报关联、顺序和展示状态；
- 版本化 SQLite schema、迁移、事务、revision/expected-revision/CAS；
- 仓库 load/list/create/update/delete/replace 接口与结构化错误；
- 隔离夹具下的零、一、多、重复和失效 Item；
- 原生海报与控制中心消费同一 Item 快照；
- `GAME_CATALOG` 从正式运行路径退出。

本工作流明确不扫描桌面、不解析真实快捷方式、不启动目标、不移动任何用户文件，也不实现保险库。

## 3. 提前冻结的边界

1. 所有测试使用应用自有 SQLite 和隔离夹具，禁止使用用户桌面作为夹具。
2. `CollectionItem` 与 `PosterItem` 使用稳定 ID；绝对路径不能作为唯一身份。
3. 失效 Item 必须可见、可诊断、可删除或等待后继修复，不能静默丢弃。
4. 正式海报只消费版本化快照，不直接读取数据库文件。
5. 本工作流只建立数据与投影；`.lnk/.url/.exe` 发现归 C-ROD，用户确认导入归 C-PAA，真实启动归 C-LCH。
6. 结构可为未来多海报保留关联能力，但本轮产品和 Release 认证仍只显示单海报。

## 4. 预定任务

| 任务 | 可验证结果 | 状态 |
| --- | --- | --- |
| C-CIM-01 Item 契约与 schema 基线 | 冻结字段、状态、身份、schema version 和失败边界 | `PLANNED` |
| C-CIM-02 SQLite 仓库与 CAS | CRUD、事务、revision/expected-revision、冲突和失败保留 | `PLANNED` |
| C-CIM-03 隔离夹具与状态矩阵 | 零/一/多/重复/失效 Item 均有可重复仓库证据 | `PLANNED` |
| C-CIM-04 数据驱动海报投影 | 海报和控制中心消费同一 Item 快照，演示目录退出正式路径 | `PLANNED` |
| C-CIM-05 Release 投影门与 C-ROD 交接 | 主屏单海报回归通过，输出只读发现候选写入边界 | `PLANNED` |

## 5. 完成门

- `GAME_CATALOG` 不再作为正式 Item 数据源；
- 零/一/多/失效 Item 均来自仓库快照；
- CRUD、CAS 冲突、损坏数据和未来 schema 有自动化证据；
- 海报空白穿透、标题栏、锁定、收起和暂停不因数据投影回退；
- 整轮未扫描或移动用户桌面文件；
- C-ROD 获得明确的候选数据输入和只读边界。

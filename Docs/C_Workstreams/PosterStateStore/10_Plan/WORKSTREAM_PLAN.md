# Poster State Store 工作流计划

日期：2026-08-12

状态：`COMPLETE — C-PSS-01..05 PASS / WORKSTREAM CLOSED`

## 1. 目的

把 C-PCB 冻结的 `poster-contract-v1` 持久快照落入版本化 SQLite 仓库，提供可诊断加载、原子 mutation、revision/CAS、legacy 幂等迁移、失败保留和升级恢复；最终替换过渡 JSON 的正式权威职责。

## 2. 范围

本工作流负责：

- SQLite 独立 schema version、application id、连接和事务策略；
- 正式快照加载、创建、替换、revision/CAS 与结构化错误；
- A0 JSON/localStorage 迁移、原始数据备份与重复迁移；
- schema 向前迁移、失败回滚/保留和应用接线；
- 当前主屏单海报 Release 的持久化回归。

本工作流不负责：多海报创建 UI、模板安装/Shadow DOM、Windows 桌面宿主、快捷方式真实导入、物理多显示器认证或用户文件移动。

## 3. C-PSS-01 冻结决策

1. SQLite schema version 使用 `PRAGMA user_version`，当前为 `1`；它与 `contractVersion=1` 独立演进。
2. 数据库设置固定 application id `0x44555031`（`DUP1`）；非零且不匹配的数据库明确拒绝，不尝试改写。
3. schema v1 使用一个 `STRICT poster_state` 单例表。正式 envelope 以 JSON TEXT 保存，`contract_version/revision/selected_poster_id` 作为可核对元数据重复列；不把 Poster 字段拆表，避免形成第二套 schema/校验器。
4. 表只允许 `id=1`，要求 revision 非负、selectedPosterId 非空且 JSON 有效；runtime、interaction、宿主和 fallback 派生字段没有列。
5. 持久连接使用 WAL、`synchronous=FULL`、5 秒 busy timeout；schema 创建使用 immediate transaction。未来 schema 只允许显式向前迁移。
6. mutation 使用 expected-revision/CAS：空库只允许创建 revision `0`；既有数据每次成功 mutation 只推进 `+1`，冲突和事务失败必须保留旧值。
7. A0 迁移先通过唯一 `adaptLegacyPosterSnapshot`；当前产品只迁移 Release 实际投影的第一张海报。完整原始 legacy envelope 保留为隔离备份，不创建隐藏第二海报，也不宣称多海报能力。
8. 正式数据库路径为应用配置目录下 `poster-state-v1.sqlite3`；迁移前备份放入同目录的 `poster-state-backups/`。备份成功前不得改写来源或数据库。
9. C-PSS-04 切换时 SQLite 才成为正式权威，JSON/localStorage 只读迁移入口停止写入；C-PSS-05 Release 门通过前不删除 legacy 读取能力或原始来源。

## 4. 任务

### C-PSS-01：存储决策与 schema 基线

结果：上述九项决策冻结；Rust 可以创建/验证 schema v1，拒绝未来 schema 与外部数据库，并证明表中没有运行时字段。

状态：`COMPLETE — SCHEMA TESTS PASS`

### C-PSS-02：加载、原子替换与 CAS

结果：仓库区分无数据、合法快照、内容损坏、schema/数据库失败；所有写入先经 Rust v1 校验并以 expected revision 原子提交。

状态：`COMPLETE — STRUCTURED LOAD / VALIDATION / CAS PASS`

### C-PSS-03：Legacy 迁移与原始数据保留

结果：只迁移当前可见第一海报，完整原始输入备份；重复执行幂等，adapter/写入失败不创建默认值。

状态：`COMPLETE — SOURCE RETENTION / FIRST-POSTER MIGRATION / FAILURE SAFETY PASS`

### C-PSS-04：应用切换与过渡桥退出

结果：Tauri 正式命令接入 SQLite，旧 bridge 停止正式写入；现有 Release 投影和桌面交互不回退。

状态：`COMPLETE — USER RELEASE CORE REGRESSION PASS`

### C-PSS-05：Release 恢复门

结果：schema 升级、损坏、冲突、重启、备份恢复与主屏单海报 Release 矩阵通过。

状态：`COMPLETE — AUTOMATED MATRIX AND USER RELEASE REGRESSION PASS`

## 5. 完成门

- schema 版本独立、可迁移且不静默降级；
- load/replace/create/migrate/backup 都返回结构化结果；
- 损坏、未知版本和迁移失败不被默认快照掩盖；
- CAS 冲突与事务失败不覆盖既有数据；
- legacy 重复迁移幂等且原始数据可诊断保留；
- SQLite 成为正式唯一权威后，过渡 bridge 按既定门退出；
- 当前单海报 Release 的重启、视觉、交互与桌面层级无回退。

上述完成门均已通过。控制中心 legacy `accent/opacity/density` 调整回弹不记为通过，作为后继 Template Platform 参数权威缺陷保留，不扩张 C-PSS schema。

## 6. 已知限制

发布认证仍只覆盖主屏单海报。多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启、热插拔和多海报产品流程未认证。

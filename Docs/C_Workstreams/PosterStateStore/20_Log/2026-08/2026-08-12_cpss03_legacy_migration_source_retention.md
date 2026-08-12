# C-PSS-03 Legacy 迁移与原始数据保留

日期：2026-08-12

状态：`C-PSS-03 COMPLETE / C-PSS-04 ACTIVE`

## 1. 本轮结果

- `poster-legacy-bridge.js` 新增 `prepareLegacyPosterMigration`，只把当前 Release 实际投影的第一张 A0 海报交给唯一 `adaptLegacyPosterSnapshot`；
- 迁移请求同时携带完整原始 legacy envelope、来源 kind/key、正式单海报候选、adapter migrations 与 adapter errors；
- Rust `PosterStateRepository::migrate_legacy` 只接受无 `contractVersion` 且包含 `posters` 数组的 A0 输入，并再次强制正式候选只能包含原始第一海报；
- SQLite 空库迁移固定创建 revision `0`；已有合法正式状态返回 `skippedExistingState`，不覆盖、不递增；已有损坏正式状态明确拒绝；
- adapter、备份或数据库写入失败均不创建正式默认快照，也不修改或删除 legacy 来源。

## 2. 原始数据保留

- 备份目录固定为数据库同级 `poster-state-backups/`；
- 来源 identity 由 source kind、source key 与完整 legacy envelope 的 SHA-256 共同产生；
- 文件名固定为 `legacy-{sourceFingerprint}.json`；备份记录包含 `backupVersion=1`、来源、指纹和完整原始 envelope，因此第二张 A0 演示海报仍可诊断保留；
- 新备份先写唯一临时文件、`sync_all`，再原子 rename；相同来源重复执行复用并核对既有备份，不产生第二份；
- 备份冲突、损坏或 I/O 故障返回 typed store error，不允许继续写 SQLite。

## 3. 失败与幂等证据

Rust 专项覆盖：

- 完整两海报原始输入备份，正式状态只含第一张；
- 相同来源重复迁移跳过，revision 保持 `0`，备份数量保持 `1`；
- adapter 失败先保留原始输入，正式状态仍为 `NoData`；
- 多海报正式候选被 scope guard 拒绝；
- 备份目录创建失败时 SQLite 不写入；
- SQLite trigger 注入写入失败时事务回滚，但已完成备份保留；
- 已有合法或损坏正式状态都不被 legacy 覆盖；
- 迁移结果保持稳定 camelCase 序列化。

前端专项覆盖：

- 完整 A0 输入不被修改；
- 只投影第一张海报给 adapter，第二张只存在于 `legacySnapshot`；
- adapter 失败携带诊断且不产生默认候选；
- formal snapshot 和未知来源在准备阶段拒绝。

## 4. 门禁

- C-PSS 专项 Rust：`20/20` 通过；
- 全量 Rust：`86/86` 通过；
- JS：`92/92` 通过；
- `121` 份 Markdown 链接有效；
- `cargo fmt --check` 与 `git diff --check` 通过。

## 5. 边界与下一步

C-PSS-03 没有管理正式仓库实例，没有注册新 Tauri 命令，也没有改变 `load_poster_snapshot/save_poster_snapshot`、localStorage 写入或当前 Release 投影。

下一步 C-PSS-04 才把 SQLite 接成正式唯一权威：应用启动先加载/迁移 SQLite，JSON/localStorage 降为只读迁移输入并停止正式写入；C-PSS-05 Release 恢复门通过前仍不删除 legacy 读取能力或原始来源。

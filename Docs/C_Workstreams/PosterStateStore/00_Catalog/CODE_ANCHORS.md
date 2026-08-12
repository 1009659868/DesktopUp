# Poster State Store 代码锚点

## 冻结输入

- [`C_PSS_INPUT_CONTRACT_V1.md`](../../PosterContractBaseline/10_Plan/C_PSS_INPUT_CONTRACT_V1.md)：C-PCB 交付的正式快照、禁止持久字段、legacy 与失败边界。
- [`app/src/poster-contract.js`](../../../../app/src/poster-contract.js)：前端正式契约、默认创建和唯一 legacy adapter。
- [`src-tauri/src/poster_contract.rs`](../../../../src-tauri/src/poster_contract.rs)：Rust 正式 v1 严格校验。

## C-PSS-01 schema 基线

- [`src-tauri/src/poster_state_store.rs`](../../../../src-tauri/src/poster_state_store.rs)：SQLite application id、独立 schema version、连接策略、单例 JSON envelope 表、未来版本/外部数据库拒绝。
- [`src-tauri/Cargo.toml`](../../../../src-tauri/Cargo.toml)：`rusqlite` bundled SQLite 与来源 SHA-256 依赖。
- [`src-tauri/Cargo.lock`](../../../../src-tauri/Cargo.lock)：锁定的 SQLite/哈希依赖图。

## C-PSS-02 仓库行为

- [`src-tauri/src/poster_state_store.rs`](../../../../src-tauri/src/poster_state_store.rs)：`NoData/Loaded/Rejected`、typed database errors、Rust v1 校验、元数据核对、revision、expected-revision/CAS 与事务回滚。
- [`2026-08-12_cpss02_load_replace_cas.md`](../20_Log/2026-08/2026-08-12_cpss02_load_replace_cas.md)：C-PSS-02 实施与验证记录。

## C-PSS-03 legacy 迁移

- [`app/src/poster-legacy-bridge.js`](../../../../app/src/poster-legacy-bridge.js)：完整 legacy envelope + 第一海报 adapter 候选 + 来源/诊断的迁移准备；仍是 localStorage/JSON 过渡入口。
- [`tests/poster-legacy-migration.test.mjs`](../../../../tests/poster-legacy-migration.test.mjs)：第一海报范围、完整原始输入、adapter 失败与来源拒绝证据。
- [`src-tauri/src/poster_state_store.rs`](../../../../src-tauri/src/poster_state_store.rs)：来源指纹、原子备份、scope guard、幂等 `migrate_legacy` 与备份/写入故障测试。
- [`2026-08-12_cpss03_legacy_migration_source_retention.md`](../20_Log/2026-08/2026-08-12_cpss03_legacy_migration_source_retention.md)：C-PSS-03 实施与验证记录。

## C-PSS-04 过渡接线

- [`src-tauri/src/poster_snapshot.rs`](../../../../src-tauri/src/poster_snapshot.rs)：只读 JSON bridge 检查器，区分无数据、A0、formal、损坏和非法来源；不再暴露写入能力。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：应用配置目录 SQLite 仓库实例、结构化 load/replace/migrate/context 命令、CAS 参数和统一正式状态事件。
- [`app/src/poster-state-store.js`](../../../../app/src/poster-state-store.js)：唯一正式前端状态网关；SQLite-first 启动、只读迁移、默认创建门、CAS 重试和 runtime/formal 投影。
- [`app/src/poster-legacy-bridge.js`](../../../../app/src/poster-legacy-bridge.js)：shared JSON/localStorage 只读来源发现与迁移准备；不包含 legacy mutation。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：原生海报通过正式网关加载、保存和接收更新。
- [`app/src/main.js`](../../../../app/src/main.js)：控制中心通过同一正式网关加载、重置和同步状态。
- [`tests/poster-state-store.test.mjs`](../../../../tests/poster-state-store.test.mjs)：启动顺序、拒绝保留、正式导入、CAS 冲突和持久字段边界。
- [`tests/poster-contract-seal.test.mjs`](../../../../tests/poster-contract-seal.test.mjs)：正式命令单一所有者、legacy 写令牌退出与 localStorage 禁写封条。
- [`2026-08-12_cpss04_application_authority_switch.md`](../20_Log/2026-08/2026-08-12_cpss04_application_authority_switch.md)：应用权威切换、真实 Release 迁移和待人工回归证据。

## C-PSS-05 Release 恢复门

- [`src-tauri/src/poster_state_store.rs`](../../../../src-tauri/src/poster_state_store.rs)：损坏 JSON/契约/元数据、未来 schema、外部数据库、只读/写入失败、事务回滚、CAS 冲突、备份失败与幂等恢复测试主体。
- [`tests/poster-state-store.test.mjs`](../../../../tests/poster-state-store.test.mjs)：SQLite-first 重载、冲突重试、Tauri 写错误保留、内置模板重启恢复与拒绝不默认重置。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：主屏单海报 Release 表面与 SQLite 权威/legacy 只读运行时封条。
- [`2026-08-12_cpss05_release_recovery_gate_activation.md`](../20_Log/2026-08/2026-08-12_cpss05_release_recovery_gate_activation.md)：恢复矩阵、隔离策略与激活输入基线。

## 禁止依赖

C-PSS 不访问 WorkerW、Progman、HWND、DPI、命中区域或运行时暂停状态，也不复制 C-PCB 的字段白名单和校验器。

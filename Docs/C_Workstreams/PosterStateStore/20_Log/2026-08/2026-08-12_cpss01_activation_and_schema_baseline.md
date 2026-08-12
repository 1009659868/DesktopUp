# C-PSS 激活与 C-PSS-01 schema 基线

日期：2026-08-12

状态：`C-PSS-01 COMPLETE / C-PSS-02 ACTIVE`

## 1. 激活

C-PCB-01..05 已关闭，五项主屏单海报 Release 核心回归由用户确认通过。C-PSS 以冻结的 `C_PSS_INPUT_CONTRACT_V1.md` 为输入成为唯一活跃 C；本轮不把 V1-P1、M1 或多显示器认证标记为完成。

## 2. 决策

- schema v1 使用 application id + `PRAGMA user_version`，与海报 `contractVersion` 分离；
- 单例 `STRICT poster_state` 表保存正式 JSON envelope，仅重复 CAS/诊断所需顶层元数据；
- mutation 强制 expected revision，成功时仅递增一；
- legacy 完整原始输入先备份，只迁移当前 Release 实际读取的第一海报；
- SQLite 切换和 legacy bridge 退出分别归 C-PSS-04 与 C-PSS-05 门控制。

## 3. 实施

- 添加 bundled `rusqlite 0.40.2`；
- 新增 `poster_state_store.rs`，提供持久/内存仓库打开、连接加固、schema 初始化与版本核对；
- schema 拒绝非 Desktop-Up application id、未来 user_version、第二条状态行和非法 JSON；
- C-PSS-01 尚未接管 `load_poster_snapshot/save_poster_snapshot`，所以当前 Release 状态路径没有改变。

## 4. 验证

- `cargo test --manifest-path src-tauri/Cargo.toml poster_state_store`：`4/4` 通过；
- 全量 JS `89/89`、Rust `70/70` 通过；`119` 份 Markdown 链接有效，Rust fmt 通过。

## 5. 下一步

C-PSS-02 实现结构化 load、Rust v1 校验、原子 replace 和 expected-revision/CAS；在这些行为测试通过前不做应用命令切换。

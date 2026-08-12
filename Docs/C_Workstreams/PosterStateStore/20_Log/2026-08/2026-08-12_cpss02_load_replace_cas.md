# C-PSS-02 结构化加载、原子替换与 CAS

日期：2026-08-12

状态：`C-PSS-02 COMPLETE / C-PSS-03 ACTIVE`

## 1. 结果

`PosterStateRepository` 已形成不接入应用命令的仓库层闭环：

- `load` 区分 `NoData`、`Loaded`、内容 `Rejected` 与带稳定 code/message 的数据库错误；
- 内容拒绝细分 `CorruptJson`、`InvalidContract`、`UnsupportedContract` 与 `MetadataMismatch`；
- 加载同时核对 JSON envelope、SQLite 顶层元数据列和 Rust `poster-contract-v1`；
- mutation 结果区分 `Stored`、`Conflict` 与 `Rejected`，并使用 camelCase 序列化形状。

## 2. Revision 与 CAS

- 空库只接受 `expectedRevision=None`，由仓库写入 revision `0`；
- 已有状态必须提交匹配的 `expectedRevision`，成功后只递增 `+1`；
- 调用方传入的 snapshot revision 不具权威性，写入时由仓库覆盖；
- revision 达到 JavaScript safe integer 上限后明确拒绝；
- immediate transaction 与 `UPDATE ... WHERE revision = ?` 共同保护 CAS。

## 3. 失败保留

- 非法正式快照、未知契约、CAS 冲突和 revision 耗尽均不写库；
- 当前数据库内容已损坏或元数据不一致时拒绝 mutation，不用默认值覆盖；
- 注入 SQLite trigger 使 UPDATE 失败后，事务回滚且旧 revision/快照保持不变；
- 查询表损坏作为 `queryDatabase` 基础设施错误返回，不与内容拒绝混合。

## 4. 验证

- C-PSS 专项 Rust：`13/13` 通过；
- 全量 Rust：`79/79` 通过；
- 全量 JS：`89/89` 通过；
- Rust fmt 与差异空白检查通过；
- `120` 份 Markdown 链接有效。

## 5. 边界与下一步

C-PSS-02 没有管理正式仓库实例，没有注册新 Tauri 命令，也没有改变 `load_poster_snapshot/save_poster_snapshot` 或当前 Release 投影。

下一步 C-PSS-03 只迁移当前 Release 实际投影的第一张 A0 海报：先保留完整原始 legacy envelope，再经唯一 `adaptLegacyPosterSnapshot` 产生正式输入并执行幂等迁移；任何备份、adapter 或写入失败都不能创建默认快照。

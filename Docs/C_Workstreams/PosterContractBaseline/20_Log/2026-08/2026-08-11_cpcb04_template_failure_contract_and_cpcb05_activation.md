# C-PCB-04 模板失败契约与 C-PCB-05 激活

日期：2026-08-11

状态：`C-PCB-04 COMPLETE / C-PCB-05 ACTIVE`

## 冻结语义

- `transparent-default` 是普通默认模板；`minimal-fallback` 是内部临时渲染回退，两者拥有不同常量和不同选择语义。
- 正常快照请求可用模板时直接渲染请求模板；缺失、损坏或未知模板以结构化原因选择临时 fallback。
- 默认模板自身缺失或损坏使用独立的 `default_template_*` 原因，不能与用户模板故障混淆。
- 快照无效与快照恢复失败分别报告 `snapshot_invalid` 与 `snapshot_recovery_failed`，不能静默重置为默认快照。
- fallback 只影响本次渲染：始终保留原始 `requestedTemplateKey`，`preserveRequestedTemplateKey=true`，`persistenceMutation=none`。
- 如果 `minimal-fallback` 自身缺失或损坏，结果为 `selection=unavailable` 并附带 `minimal_fallback_*` 原因，不能假装恢复成功。
- 持久快照中的 `minimal-fallback` 仍属于非法用户选择；解析器只提供可诊断的临时渲染结果，不把它提升为普通模板。

## 实施

- 新增 JS 权威解析器 `app/src/poster-template-resolution.js`，集中拥有正常默认、内部 fallback、输入状态、结构化错误和无写回解析结果。
- `app/src/poster-contract.js` 的旧 `resolvePosterTemplate` 保持返回形状和兼容原因，但其选择决策委托给新解析器，避免出现第二套 fallback 逻辑。
- 新增 Rust 同构解析器 `src-tauri/src/poster_template_resolution.rs`；`poster_contract.rs` 复用同一内部 fallback 常量，不再重复定义。
- JS/Rust 共用 `tests/fixtures/poster-template-resolution-v1.json` 的 16 组样例，覆盖正常默认、用户模板、缺失/损坏/未知模板、非法持久 fallback、无效快照、恢复失败、fallback 自身失败和非法输入。
- `package.json` 全量检查入口已包含新 JS 模块语法检查。

## 验证

- C-PCB-04 JS 专项：5/5 PASS；与旧模板接口合并专项：16/16 PASS。
- `pnpm run check`：83/83 PASS；111 个 Markdown 文件链接有效。
- `cargo test --manifest-path src-tauri/Cargo.toml`：65/65 PASS。
- Rust 仅保留既有 MSVC import-library linker message，没有新增编译告警。

## 边界

- 新解析器尚未接管当前 A0/Release 模板加载或快照恢复路径，因此本切片没有改变海报视觉、桌面穿透、卡片命中、标题栏拖动、锁定或收起/展开，不需要新增 Release 人工验收。
- 本任务没有实现 Shadow DOM、模板包安装、SQLite、模板视觉、多海报创建或多显示器认证；这些能力仍归后续工作流。
- 当前 A0 无版本数据继续走显式 legacy 分流，不在 C-PCB-04 自动重写。

## 下一步

C-PCB-05 对完整 C-PCB 公共边界做封板：审计并收敛唯一 A0 bridge 隔离入口，确认 JS/Rust 公共契约与 canonical 宿主接口没有新增兼容旁路，执行当前主屏单海报 Release 核心回归，并形成 C-PSS 可直接消费的 schema、错误与迁移输入清单。C-PCB-05 不提前实现 SQLite、Shadow DOM 或模板安装。

# C-PCB 激活与契约审计

日期：2026-08-11

状态：`COMPLETE — SUPERSEDED BY CONTRACT PARITY LOG`

## 激活依据

用户接受当前发布限制：当前发布基线只认证主屏单海报；第二显示器、混合 DPI、负坐标副屏、主屏切换和热插拔未认证。C-DRP-06 因此具备关闭条件，活动链切换到 B-PCR / C-PCB。

## 代码审计结论

- `app/src/poster-core.js` 的默认快照包含两个演示海报，与当前单海报范围和设计默认不一致。
- A0 持久字段仍为 `theme/accent/opacity/density/x/y/collapsed/locked`，未表达 monitor、anchor、版本或呈现/运行态所有权。
- `src-tauri/src/poster_snapshot.rs` 只检查 `posters` 是否为数组，尚不能阻止未知版本、重复 ID、悬空选择、非法枚举或 fallback 污染。
- 当前原生表面实际上只投影第一张海报；控制中心仍保留两海报演示数据。C-PCB 首轮先建立正式契约与 legacy adapter，不直接替换已验收 Release 投影。
- 设计基线要求 Transparent Default 是正常默认，minimal-fallback 是独立内部回退；现有 A0 主题字段不能直接承担该语义。

## 首个实施切片

新增纯 JS `poster-contract`：

- 契约版本、默认枚举与唯一默认快照；
- 严格、无副作用的快照校验；
- A0 legacy adapter 与转换诊断；
- 临时 fallback 决策，确保不写回持久快照；
- 行为测试覆盖合法默认值和主要失败边界。

首切片不接管现有读写路径，因此不会改变当前 Release 视觉、卡片、穿透、拖动、锁定或收起/展开行为。

## 自动验证

- `node --test tests/poster-contract.test.mjs`：9/9 PASS；
- `pnpm run check`：67/67 PASS，107 份 Markdown 链接有效；
- `cargo test --manifest-path src-tauri/Cargo.toml`：52/52 PASS；链接器仅报告既有 MSVC import library 信息性 warning。

该段是首切片时点结论。后续 Rust 同构校验、正式/legacy 分流和共享矩阵已经完成，最终证据见同日 `cpcb01_contract_parity_and_cpcb02_geometry_audit` 日志。

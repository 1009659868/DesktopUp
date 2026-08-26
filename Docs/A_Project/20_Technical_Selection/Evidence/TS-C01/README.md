# TS-C01 Candidate Evidence Index

状态：`ACTIVE — R5-03 COMPLETE / 2 PASS / 1 FAIL / 34 UNKNOWN / 4 NOT RUN / P0 PASS INELIGIBLE`

日期：2026-08-26

## 1. 候选身份

- Candidate：`TS-C01`；
- renderer：Qt 6.8.3 / Qt Quick；
- host adapter：native DesktopHost，`OFFSCREEN_FRAME_SOURCE / HOST`；
- branch：`impl/Refactor`；
- parent Git HEAD：`83962bf2f0786b68951ef5bf5f020666a1ea1c46`；
- source identity：`UNFROZEN WORKTREE`；
- P0：`NOT PASS / FAIL RECOMMENDATION PENDING R5-04 AND USER DECISION`；
- selected：`NO`。

## 2. 权威结论链

1. [R5-01 原始证据盘点](../../TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md)；
2. [R5-02 统一矩阵映射](../../TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md)；
3. [完整性清单](INTEGRITY_MANIFEST_V1.md)；
4. [源码身份与保留策略](SOURCE_IDENTITY_AND_RETENTION_V1.md)；
5. 本目录中按 Matrix ID 建立的正式记录。

## 3. 已形成确定状态的矩阵项

| Matrix ID | Result | Record |
| --- | --- | --- |
| HG-PERF-01 | `PASS` | [静态 CPU 记录](HG-PERF-01/2026-08-25_r4d3-static-cpu/record.md) |
| HG-PERF-02 | `FAIL` | [工作集记录](HG-PERF-02/2026-08-25_r4d3-working-set/record.md) |
| HG-PERF-04 | `PASS` | [200 Item 与主题切换记录](HG-PERF-04/2026-08-25_r4d3-item-theme/record.md) |

其余 38 项没有预生成 PASS 目录。34 个 `UNKNOWN` 与 4 个 `NOT RUN` 的逐项原因保存在 R5-02，避免用空目录冒充运行。

## 4. 原始材料边界

大型部署树、EXE 和 JSONL 保留在本地 Git 忽略目录 `prototype/renderer-adapters/ts-c01-qt-quick/out/`，本索引不复制或修改原始文件。所有被结论引用的关键文件均在完整性清单中记录相对路径、字节数和 SHA-256。

若 `out/` 被清理，Git 中仍保留结论、哈希和复现入口，但原始 E3 细节将不可直接复核。因此在作出最终技术选择前不得删除这些本地材料；若需跨机器评审，应单独建立受控归档，不直接提交 17 MiB 安装器或部署树。

## 5. 完整性结论

- 原始 JSON/JSONL 格式校验通过；
- 三个确定性能项均可追溯到同一 D3 Release 哈希 `4F1D76FCEC9C5271C1199D2CBAE48C748CA4EC0D8CA8C4D0AE50D325095CA673`；
- R4 其他功能观察跨多个 EXE 哈希，因此只保留为 `UNKNOWN/PARTIAL` 输入；
- 正式包建立不改变 P0 状态，也不表示 Qt 获胜。
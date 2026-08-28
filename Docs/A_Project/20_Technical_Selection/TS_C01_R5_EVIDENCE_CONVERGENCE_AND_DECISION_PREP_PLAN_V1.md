# TS-C01 R5 候选证据收敛与 Qt 裁决准备计划 V1

状态：`COMPLETE HISTORICAL R5 WORKFLOW / FAIL RECOMMENDATION RETAINED / R6 QT ROUTE DECISION RECORDED`

日期：2026-08-26

## 1. 目的

R5 不继续增加候选功能，也不把 R4 的用户接受结论直接改写为 P0 PASS。它负责把 TS-C01 从 R0～R4 产生的自动日志、人工会话、失败记录、计数例外、未执行项和正式安装生命周期证据收敛到统一候选证据矩阵，准备一个可审计的 Qt 技术候选处置建议。

本阶段只回答三件事：

1. 哪些材料真实存在、可解析、可追溯且可用于哪个矩阵项；
2. 哪些结论只能记为计数例外、失败、未知、延期、跳过或未实现；
3. TS-C01 是否具备进入 R6 候选交接的条件，以及建议的退出状态应是什么。

Qt 获胜、P0 PASS、进入加权评分或冻结生产架构均不属于 R5-01 的输出。

## 2. 权威输入

1. [统一候选证据矩阵](UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md)；
2. [TS-02 Renderer Adapter / P0 执行计划](TS_02_RENDERER_ADAPTER_P0_EXECUTION_PLAN_V1.md)；
3. [E6 候选交接与退出规则](TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md)；
4. [TS-C01 R4 人工 P0 手册](TS_C01_R4_MANUAL_P0_RUNBOOK_V1.md)；
5. [R4-D4 当前用户生命周期计划](TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md)；
6. [证据目录规则](Evidence/README.md)；
7. `prototype/renderer-adapters/ts-c01-qt-quick/out/` 中真实生成且未被覆盖的原始文件。

发生冲突时，统一矩阵和 E6 的状态定义优先；用户接受可以决定停止重复测试或带例外继续，但不能把未达到冻结次数的材料重命名为完整 P0 PASS。

## 3. 固定处置词汇

| 处置 | 含义 |
| --- | --- |
| `ADMISSIBLE` | 原始文件存在、格式有效、候选/版本/哈希/动作可追溯，可映射到指定矩阵项 |
| `PARTIAL` | 场景真实执行且观察通过，但次数、环境、媒体或同构范围不足 |
| `FAIL` | 原始结果明确违反冻结硬门，不因用户允许继续而消失 |
| `UNKNOWN` | 无足够观察、设备或原始材料，不能判断 |
| `DEFERRED BY USER` | 用户明确暂缓，本轮未执行；不是 PASS |
| `SKIPPED BY USER` | 用户明确跳过，本轮未执行；不是 PASS |
| `NOT IMPLEMENTED` | 当前候选壳未实现矩阵要求的产品能力 |
| `SUPERSEDED` | 旧构建材料保留供诊断，但不能代表最终候选构建 |
| `DIAGNOSTIC RETAINED` | 失败/中断材料永久保留，用于说明修订原因 |

这些是 R5 证据处置标签；最终矩阵仍只使用 `NOT RUN / UNKNOWN / PASS / FAIL / CONDITIONAL / NOT APPLICABLE`。

## 4. 工作分解

| 阶段 | 工作 | 输出 | 当前状态 |
| --- | --- | --- | --- |
| R5-01 | 原始证据盘点、格式校验、哈希与构建血缘 | [R5-01 证据盘点](TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md) | `COMPLETE` |
| R5-02 | 将每项材料映射到统一矩阵，标记适用性、最低证据等级和实际次数 | [统一矩阵映射](TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md) | `COMPLETE` |
| R5-03 | 收敛缺口、失败、例外、未实现和跨构建污染；形成正式 Candidate Evidence 索引 | [Candidate Evidence](Evidence/TS-C01/README.md) | `COMPLETE` |
| R5-04 | 按 E6 规则形成候选处置草案：PASS、FAIL、CONDITIONAL、WITHDRAWN 或保持 NOT RUN | [候选处置审计](TS_C01_R5_04_CANDIDATE_DISPOSITION_AUDIT_V1.md) | `COMPLETE — FAIL RECOMMENDED` |
| R5-05 | 将经审计结果交给 R6；更新候选登记、决策记录和后续候选顺序 | [R6 交接输入](TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md) | `COMPLETE` |

## 5. 不可拼接规则

1. 不同 EXE SHA-256 的会话不能伪装为同一最终构建的一次完整 P0；
2. 修复后的会话可以使旧失败变为 `SUPERSEDED / DIAGNOSTIC RETAINED`，但不能自动覆盖其他矩阵项；
3. 用户减少轮数形成 `PARTIAL` 或明确例外，不形成冻结矩阵的完整重复次数；
4. `out/` 是本地忽略目录。R5-03 必须保存路径、大小、哈希和保留策略，不能仅引用易失路径；
5. 当前工作树若未冻结为提交或源码包哈希，则来源只能记为 `UNFROZEN WORKTREE`；
6. Qt 版本一致不等于 adapter 源码一致；构建血缘按 EXE 哈希和源码身份判断；
7. 自动 validator PASS 只证明结构化一致性，不代替白条、闪烁、Explorer 输入和视觉人工观察；
8. 工作集超过 50 MiB 是 `HG-PERF-02 FAIL` 候选事实，用户接受继续只表示允许完成取证；
9. 自启延期、设备重启跳过、触控板无设备、多显示器延期必须原样保留；
10. 当前控制中心、StateStore、主题包平台和内容 Item 若只是角色探针或合成夹具，不得描述为完整产品实现。

## 6. R5 关闭门

R5 只有在以下条件同时满足时才能交给 R6：

- 每个纳入材料都有路径、大小、SHA-256、候选版本和构建身份；
- 所有统一矩阵项均有明确映射或明确“不适用/未执行”理由；
- 不同构建、计数例外、延期、跳过、未知、未实现和失败没有被隐藏；
- 正式 Candidate Evidence 索引能从结论回到原始文件；
- 候选处置草案严格符合 E6，不以加权评分挽救硬门失败；
- 用户在 R6 或后续决策阶段明确批准候选处置。

R5 完成仍不自动选择 Qt。若 TS-C01 未达到 P0 PASS，按 E6 应转入其他候选或一次事先冻结的有界修正，而不是把门槛降级。
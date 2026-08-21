# TS-H01-D2 User Acceptance Decision

状态：`USER-ACCEPTED PASS — TELEMETRY EXCEPTION RETAINED`

日期：2026-08-21

用户明确决定认为 TS-H01-D2 验证通过，并要求直接正式执行 TS-H01-D3-1 四档 DPI 矩阵。本决策按最高权威执行，D3 因此由 READY 改为 ACTIVE。

该决策不改写技术事实：上一份 600 秒自动日志仍为 `recovery_contract_fail`，仅观测到 1/10 次 Explorer 恢复。失败 Evidence run、原始日志路径、哈希和技术风险继续保留，不能在后续摘要中表述为自动 10/10 PASS。

正式状态的准确含义是：

- 人工验收：用户接受；
- 自动恢复次数合同：存在已登记例外；
- 流程决策：允许进入 D3；
- 残余风险：Explorer 多代连续恢复尚无 10/10 自动证据。

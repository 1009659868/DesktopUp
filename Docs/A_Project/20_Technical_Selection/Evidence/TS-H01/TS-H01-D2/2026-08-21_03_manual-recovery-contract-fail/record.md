# TS-H01-D2 Manual Recovery Attempt — Contract FAIL

状态：`FAIL — 1 / 10 RECOVERIES`

日期：2026-08-21

## 结果

本次 600 秒人工运行的最终画面能够恢复，但原始遥测只记录到一次 Explorer 代际重建，而冻结门要求恰好十次。探针按预期以退出码 4 和 `recovery_contract_fail` 拒绝放行。

- 样本：72,148 / 72,148 样本内结构合同通过；
- generation loss：1；
- degraded cleanup：1；
- safe target rediscovered：1；
- unique reattach/recovery：1；
- 初始 Explorer PID：17652；恢复后 PID：14900；
- 最终 cleanup 后自有窗口：0；自有顶层窗口：0；
- 期望恢复次数：10；实际恢复次数：1。

“最终又看见卡片”不能替代十代恢复证据。本 run 永久保留为失败证据，不得重命名为 PASS，也不得被后续成功日志覆盖。

## 下一动作

使用同一最终 D2 Release 建立新的日志，在 600 秒内从任务管理器依次重启 Windows Explorer 恰好 10 次；每次必须等待唯一琥珀色卡片恢复后再开始下一次。只有 recovery/recreate/generation-loss cleanup/degraded-entry 全部为 10 且最终结果为 pass，D2 才能关闭。

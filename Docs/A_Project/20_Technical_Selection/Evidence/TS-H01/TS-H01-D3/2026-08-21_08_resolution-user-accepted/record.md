# TS-H01-D3-2 Resolution Change Matrix

状态：`USER-ACCEPTED PASS / AUTOMATED TELEMETRY EXCEPTION`

日期：2026-08-21

用户报告已完成分辨率改变/恢复人工验证并确认通过，随后要求更新文档并直接进入 D3-3。在告知自动运行仍未闭合后，用户再次要求继续未完成任务，因此工作流按用户决定进入 D3-3，但完整保留自动差异。

## 人工结果

用户确认 D3-2 视觉与行为验证通过。该结论只代表人工观察，不改写自动日志。

## 自动日志事实

- x64 Release SHA-256：`4039C93329014023F64FB72FFD84F1A83C8BB1AABE19059B37505337148008AD`；
- 34,987 / 34,987 个完整 sample 的逐样本边界合同 PASS；
- 只记录 8 / 10 个 `display_geometry_changed` 事件；
- 最终动作计数为 drag=2、resize=2、collapse-cycle=1，与本 run 预期的全零交互不符；
- 为进入下一阶段，已结束仍在等待的探针，运行器记录 exit `-1`、`invalid_final_json`；
- JSONL 末尾有 1 行因进程结束而截断，34,987 个完整 sample 不受影响；
- 原始日志 SHA-256：`259D3C368FBDCBDDFA391F6730E56B4EEDD5BA67BCCE5520ABEAED742E6AE9B9`；
- 原运行器摘要 SHA-256：`43EE9BCF7D79485D1232D88FD397F7D0FE61F967C54ADD8D058B600A98A9F432`。

## 裁决

本 run 不能标记为自动门 PASS，也不能声称探针证明了 10/10 次变化。用户人工验收与推进决定记为 `USER-ACCEPTED PASS`，8/10 变化计数、额外交互和非零退出作为显式遥测例外继续进入最终风险汇总。

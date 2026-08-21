# TS-H01-D3-3 Work-Area and Taskbar Matrix

状态：`PASS — AUTOMATED + USER MANUAL`

日期：2026-08-21

最新版 x64 Release run 在 100% / 96 DPI、2560 × 1440 主屏下完成任务栏自动隐藏工作区变化门。自动进程 exit 0，用户确认人工视觉 PASS。

## 自动结果

- 3,802 / 3,802 个 sample PASS；
- 0 个无效 JSON 行；
- 11 个 `display_geometry_changed` 事件，满足至少 10 次变化合同；
- 观察到工作区 `0,0,2560,1392` 与 `0,0,2560,1440`；
- drag=0、resize=0、collapse-cycle=0，无诊断卡交互污染；
- 最终结果 `boundary_telemetry_pass_manual_visual_pending`；
- 原始日志 SHA-256：`C90DE301CACCC730C3830602268E064648C0B9F46D290FCDDB598269A247538C`；
- 运行器摘要 SHA-256：`ADAACF10D633088930EA9ACA22889E61B2AC1FFFAE09AEC5B34230A817040348`；
- 二进制 SHA-256：`4039C93329014023F64FB72FFD84F1A83C8BB1AABE19059B37505337148008AD`。

最终采样工作区为全屏 `0,0,2560,1440`，表示记录结束时任务栏处于自动隐藏状态；该系统偏好不影响已覆盖两种工作区的门结果，用户可按个人偏好恢复。

## 前一失败尝试

前一运行器摘要为 `boundary_contract_fail`、exit 4；其 102,750,109 字节原始日志保留在 `prototype/desktop-host-win32/ts-h01-d3/out/d3-3-work-area-20260821-132003/work-area-change.jsonl`，SHA-256 为 `97CD0B41C2A1C274ED03B36D3A4895353C4C44C22F4D30B5BE48A38EC42FE357`。为避免把 98 MiB 失败日志复制进文档证据，证据目录只保存其运行器摘要；失败事实未删除或改写。

## 人工结果

用户明确回复 `pass` 并要求进入下一步。人工观察与自动边界门共同构成本 run 的 PASS。

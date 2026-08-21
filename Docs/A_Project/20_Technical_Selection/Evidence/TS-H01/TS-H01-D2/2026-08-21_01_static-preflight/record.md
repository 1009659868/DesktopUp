# TS-H01-D2 Static Preflight

状态：`STATIC PREFLIGHT PASS / MANUAL 10 RECOVERIES PENDING`

日期：2026-08-21

## 结果

TS-H01-D2 x64 Release 在不重启 Explorer 的 5 秒静态门中得到 `587 / 587` 样本 PASS，退出码为 0。

所有样本均保持：

- 当前拓扑为唯一 `RAISED_PROGMAN`；
- 恰好一组 visual + 3 interaction islands，共 4 个自有 HWND；
- 4 个 HWND 都是安全桌面父窗口的 `WS_CHILD`，没有普通顶层与 TopMost；
- visual、islands、DefView 的兄弟顺序持续满足海报高于图标；
- 空白命中原生桌面，三个交互岛分别命中自身；
- attach=1，generation-loss cleanup/recreate/recovery/degraded-entry 均为 0；
- 正常退出后自有窗口总数与顶层窗口数均为 0。

## 状态机边界

探针只实现以下有界恢复：

```text
ATTACHED_SAFE
-> SHELL_GENERATION_LOST
-> DEGRADED_NO_DESKTOP_HOST
-> SAFE_TARGET_REDISCOVERED
-> ATTACHED_SAFE
```

安全目标必须连续 32 个 8 ms 目标采样保持同一 Explorer PID、icon host、DefView 和 icon view，才允许创建下一代唯一表面组。已使用过的 Shell 签名不得再次附着。探针不创建普通顶层回退、不使用 TopMost、不启动或终止 Explorer，也不轮询 `SetWindowPos` 修复层级。

## 采样披露

采样目标为 8 ms；平均 8.504 ms，P95 9.799 ms，P99 12.790 ms，最大 132.580 ms。原始日志中的调度长间隔保留披露，不将静态预检扩大解释为 Explorer 重建 PASS。

## 证据边界

本记录只通过无 Shell 代际变化时的 D2 静态门。HG-Z-07 仍需用户使用任务管理器完成 10 个独立 Windows Explorer 重启循环，并确认无普通顶层/TopMost 闪现、每代仅恢复一个诊断卡、普通程序恢复后覆盖诊断卡，以及最终输入合同未回退。

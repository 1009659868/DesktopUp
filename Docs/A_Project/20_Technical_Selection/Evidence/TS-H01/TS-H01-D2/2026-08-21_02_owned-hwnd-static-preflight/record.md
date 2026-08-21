# TS-H01-D2 Owned-HWND Static Preflight

状态：`STATIC PREFLIGHT PASS / MANUAL 10 RECOVERIES PENDING`

日期：2026-08-21

## 变更原因

初版静态预检后增加了进程所有权校验：在销毁或确认自有 HWND 前，同时验证窗口类名与 `GetWindowThreadProcessId` 返回的进程 ID，避免 Shell 代际切换期间失效句柄被系统复用后误触碰非本进程窗口。源码变化后未覆盖旧证据，而是重新构建并建立本 Evidence run。

## 最终静态结果

最终 x64 Release 在 5 秒静态门中退出码为 0，得到 `604 / 604` 样本 PASS：

- 当前 `RAISED_PROGMAN` 目标唯一且持续稳定；
- attached 期间恰好存在 4 个本进程自有窗口，顶层窗口数始终为 0；
- visual/islands/DefView 层级、可见性、非 iconic、非 cloaked、非 active、非 TopMost 全部满足合同；
- 空白与三个交互岛命中合同持续成立；
- attach=1，其余恢复计数均为 0；
- 正常退出后自有窗口总数和顶层数都为 0。

采样平均 8.260 ms，P95 9.565 ms，P99 10.548 ms，最大 11.667 ms。

## 当前门

本 run 是 TS-H01-D2 当前权威静态证据，但不能代替 Explorer 重建。下一门仍是使用本二进制完成 10 次 Windows Explorer 重启，并由遥测得到 generation-loss cleanup/recreate/recovery 各 10 次，同时完成人工层级、唯一卡片和最终输入检查。

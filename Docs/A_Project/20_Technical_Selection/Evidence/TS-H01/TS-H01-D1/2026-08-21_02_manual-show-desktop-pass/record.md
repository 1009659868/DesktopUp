# TS-H01-D1 Manual Show-Desktop Pass

状态：`PASS — KEYBOARD 10/10 + TASKBAR 10/10`

日期：2026-08-21

## 结论

用户分别完成了两个互不混用的人工入口：

- 键盘 `Win+D`：10 个完整“显示桌面 / 恢复普通程序”循环；
- 任务栏最右侧“显示桌面”：10 个完整“显示桌面 / 恢复普通程序”循环。

用户确认两轮均满足：诊断卡在桌面暴露期间连续可见且无肉眼闪烁；恢复普通程序后普通程序立即覆盖诊断卡；没有短暂 TopMost、错误抬层或残留表面；探针超时后所有自有表面清理完成。

自动遥测同时得到：

- 键盘轮 `18,198 / 18,198` 样本通过；
- 任务栏轮 `18,184 / 18,184` 样本通过；
- 两轮所有样本均保持 parent/owner、DefView 相对兄弟顺序、visible、非 iconic、非 cloaked、非 active、非 TopMost；
- 两轮均保持 `attach=1`，`detach/recreate/hide/show=0`；
- visual 未收到意外指针消息；
- 完成事件均为 `continuity_telemetry_pass_manual_cycles_pending`，其中 `manual_cycles_pending` 是探针无法自行判断人工循环数的既定输出；本记录以用户的两组 10/10 确认为人工门证据。

## 采样披露

两轮 8 ms 高分辨率计时目标的 P95 分别为 8.548 ms 和 8.537 ms，P99 分别为 8.792 ms 和 8.766 ms。原始日志中仍存在极少数用户态调度长间隔：键盘轮最大 28.692 ms，任务栏轮最大 145.049 ms。该事实不从证据中删除；人工连续观察用于覆盖这些稀疏采样空隙。

## 证据边界

- 本结论只关闭 HG-Z-03 与 HG-Z-04 在当前设备、当前 `RAISED_PROGMAN` 拓扑和当前 D1 二进制上的 D1 门。
- 触控板显示桌面因设备能力未确认，继续保持 `UNKNOWN_DEVICE_AVAILABILITY`，按章程不阻断当前单机开发。
- Explorer 重建、DEGRADED 与唯一恢复属于 TS-H01-D2，未由本证据覆盖。
- 分辨率、DPI、工作区和边界属于 TS-H01-D3，未由本证据覆盖。
- 两份约 60 MB 的原始运行日志保留在本地忽略目录，不提交仓库；文件名、字节数和 SHA-256 固化在 `summary.json`。

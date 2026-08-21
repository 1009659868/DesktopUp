# TS-H01-D1 Static Preflight

状态：`STATIC PREFLIGHT PASS / MANUAL SHOW-DESKTOP CYCLES PENDING`

## 结果

最终 Release 在无显示桌面操作的 3 秒静态门中得到 364/364 样本 PASS。采样由 8 ms 高分辨率 waitable timer 驱动；最大实测间隔 9.882 ms，消除了默认 15.6 ms 计时量化和显式文件 flush 造成的探针假空洞。

所有样本均保持：

- visual 与三个 interaction island 持续 visible、非 iconic、非 cloaked；
- parent/owner 与相对 DefView sibling order 不变；
- TopMost 与 active 均为 false；
- attach=1，detach/recreate/hide/show=0；
- window/client/DWM extended frame bounds 与 DPI 可观测；
- 自动完成状态为 `continuity_telemetry_pass_manual_cycles_pending`。

## 证据边界

本记录只通过 D1 静态门。还没有执行键盘 `Win+D` 10 个完整循环，也没有执行任务栏“显示桌面”10 个完整循环，因此 TS-H01-D1 尚未 PASS。

探针没有发送 Win+D、没有 hide/show 修复、没有运行期 `SetWindowPos`、没有 TopMost，也没有 Explorer 重建恢复代码。

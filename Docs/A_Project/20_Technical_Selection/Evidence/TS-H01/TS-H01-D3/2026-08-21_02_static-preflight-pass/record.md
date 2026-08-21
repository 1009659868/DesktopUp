# TS-H01-D3 Static Preflight

状态：`D3-0 STATIC PASS / FORMAL D3 BLOCKED BY D2`

日期：2026-08-21

修正初次 sibling 顺序后，D3 x64 Release 在 100% / 96 DPI 下退出码为 0，得到 604 / 604 样本 PASS：

- expected/window/client-screen/DWM extended frame 四类矩形逐样本一致；
- render target 与客户区逐样本一致；
- geometry、hit-region、render revision 均为同一提交；
- visual 与四个 interaction islands 均为 icon host 子窗口、无 caption/thickframe/owner/TopMost/激活；
- islands > visual > DefView 的 sibling 顺序持续成立；
- 逻辑几何、归一化锚点、monitor/work area 和物理投影均写入日志；
- 正常退出后探针窗口全部销毁。

采样间隔最小 7.804 ms，平均 8.268 ms，P95 8.562 ms，P99 8.719 ms，最大 9.010 ms。

本 run 只证明 D3 探针已就绪和 D3-0 静态预检通过。D2 当前正式人工 run 为 1/10 FAIL，因此不能据此把 D3 标记为 ACTIVE，也不能执行 D3-1 至 D3-3 的正式证据矩阵。

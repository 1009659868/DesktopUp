# TS-H01-D3-1 User-Directed Early Exit

状态：`PARTIAL ACCEPTANCE / REMAINING DPI BANDS USER-SKIPPED`

日期：2026-08-21

用户明确决定不再继续 DPI 测试，并要求直接进入下一阶段。本决策关闭 D3-1 的继续执行并激活 D3-2，但不把未执行项改写为 PASS。

当前事实：

- 125% / 120 DPI：当前 DPI 权威构建 5/5 自动与人工 PASS；
- 100% / 96 DPI：旧构建曾 5/5 PASS，当前构建只有 D3-2 前静态回归 PASS，没有当前构建五冷启动交互门；
- 150% / 144 DPI：USER-SKIPPED / UNKNOWN；
- 200% / 192 DPI：USER-SKIPPED / UNKNOWN；
- D3-1 总体：不是完整四档 PASS，状态为用户接受的部分覆盖。

D3-2 在用户结束 DPI 操作后检测到主屏已回到 100% / 96 DPI，因此仍按原矩阵的 100% 固定基线执行，无需登记 125% 基线偏差。

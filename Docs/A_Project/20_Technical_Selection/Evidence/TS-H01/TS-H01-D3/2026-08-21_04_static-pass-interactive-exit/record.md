# TS-H01-D3 Static Regression After Interactive Exit Support

状态：`PASS — CURRENT RELEASE STATIC BASELINE`

日期：2026-08-21

使用同一新二进制和新日志复跑无操作静态门，退出码为 0，609/609 样本 PASS。四类边界、DPI、revision、样式、层级和拓扑合同持续成立，所有动作计数为 0。该 run 证明新增“精确动作计数满足后继续 1.5 秒稳定观察并提前结束”的交互控制没有破坏静态路径。

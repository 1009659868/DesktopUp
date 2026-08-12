# Poster Contract Baseline 任务索引

| 任务 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-PCB-01 版本化海报契约 | 契约版本、唯一默认快照、持久态白名单、legacy adapter 与失败结果有行为测试 | C-DRP | `COMPLETE — JS/RUST SHARED MATRIX PASS` |
| C-PCB-02 几何与呈现所有权 | 默认几何算法、尺寸上下限、锚点和收起标题栏常量唯一且可测试 | C-PCB-01 | `COMPLETE — JS/RUST GEOMETRY MATRIX PASS` |
| C-PCB-03 标题栏与权限矩阵 | 拖动、锁定、收起及可交互区域由状态矩阵约束 | C-PCB-02 | `COMPLETE — JS/RUST PERMISSION MATRIX PASS` |
| C-PCB-04 默认模板与失败回退 | Transparent Default 与临时 minimal-fallback 分离，无效数据不污染持久态 | C-PCB-01..03 | `COMPLETE — JS/RUST FAILURE MATRIX PASS` |
| C-PCB-05 契约封板与 C-PSS 交接 | JS/Rust 同构、legacy 边界、Release 回归和后继接口通过 | C-PCB-01..04 | `COMPLETE — AUTO GATES AND USER RELEASE REGRESSION PASS` |

C-PCB-01..05 已全部完成，C-PCB 工作流关闭。C-PSS 已满足依赖但尚未激活；任务状态的权威解释在 [`WORKSTREAM_PLAN.md`](../10_Plan/WORKSTREAM_PLAN.md)。

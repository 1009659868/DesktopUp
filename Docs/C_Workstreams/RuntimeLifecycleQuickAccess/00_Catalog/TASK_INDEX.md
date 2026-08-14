# Runtime Lifecycle and Quick Access 任务索引

| 任务 | 可验证结果 | 状态 |
| --- | --- | --- |
| C-RLC-01 工作流激活与生命周期矩阵 | 建立 C-RLC 权威文档，冻结启动、单实例、控制中心、托盘、自启、退出和 TaskbarCreated 行为 | `COMPLETE` |
| C-RLC-02 启动意图与单实例分流 | 普通启动、精确 `--autostart` 和非法参数形成单一解析；第二实例按意图分流且不产生重复表面 | `COMPLETE` |
| C-RLC-03 自启显式 opt-in 与状态同步 | 正式合同保持不变；专用临时测试 Release 已通过幂等 HKCU、单实例与清理门 | `COMPLETE — TEMPORARY TEST ROOT GATE PASS` |
| C-RLC-04 控制中心与托盘快捷入口收敛 | 控制中心懒创建/释放/重开、显式 ready 队列、托盘原生状态与仓库快捷动作保持一致 | `COMPLETE` |
| C-RLC-05 TaskbarCreated 恢复与退出顺序 | Explorer 重启后有界恢复托盘和桌面宿主；退出阶段不重建表面或接收迟到恢复 | `COMPLETE` |
| C-RLC-06 Release 生命周期矩阵与封板 | 自动门、Release/NSIS 烟测和用户人工矩阵覆盖冻结行为，关闭 C-RLC | `COMPLETE` |

详细边界、完成信号和验证面以 [`WORKSTREAM_PLAN.md`](../10_Plan/WORKSTREAM_PLAN.md) 与 [`RUNTIME_LIFECYCLE_MATRIX_V1.md`](../10_Plan/RUNTIME_LIFECYCLE_MATRIX_V1.md) 为准。

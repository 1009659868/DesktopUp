# 2026-08-06 C-DHF-08 决策封板

状态：`COMPLETE`

## 目标

汇总 C-DHF-01..07 的验证结论，形成宿主技术决策、风险清单和下一工作流移交；关闭桌面宿主可行性硬门。

## 已关闭硬门

| 硬门 | 结论 |
| --- | --- |
| C-DHF-01 工程基线 | `PASS` |
| C-DHF-02 桌面宿主与回退 | `PASS — ACCEPTED TOOLWINDOW FALLBACK` |
| C-DHF-03 命中测试 | `PASS` |
| C-DHF-04 Shell/显示器/DPI 恢复 | `PASS` |
| C-DHF-05 事件驱动暂停 | `PASS` |
| C-DHF-06 性能基线 | `PASS` |
| C-DHF-07 托盘与 Explorer 恢复 | `PASS` |
| C-DHF-08 决策封板 | `PASS` |

## 技术决策

- 宿主拓扑：V1 目标采用单全局透明宿主；当前可行性阶段接受 `ToolWindow` 回退作为已观察可恢复的宿主模式，WorkerW 仅作为显式 opt-in 适配层；
- 宿主拓扑不再作为二选一待定项；生产单全局宿主收敛到 `C-DRP`；
- 托盘生命周期：单击不改变海报，双击切换主页，右键菜单和显隐控制已通过；
- 暂停策略：全屏/最大化、锁屏、显示器关闭和手动暂停统一进入 `effectivePaused`，不使用高频轮询维持；
- 性能基线：四种模式真实采样已保存到 `performance-baseline-v1.json`；
- 下一工作流：`C-DRP Desktop Runtime Productionization` 收敛生产宿主，`C-PCB Poster Contract Baseline` 冻结海报核心契约；两者由 `ACTIVE_PATH.md` 在下一次接手时唯一选中。

## 风险清单

- 当前 A0 独立海报窗体不是生产单全局透明宿主，仍需 `C-DRP` 收敛；
- WebView2 全透明背景稳定路径尚未在 V1 重新实现，属于 `C-LST/C-PCB` 后续工作；
- 多海报共享同一宿主的实现与命中区域合并仍未验证；
- 物理矩阵依赖用户环境，后续变更显示拓扑时仍需回归；
- 海报框拉伸与最小/最大尺寸限制已进入后续 `C-PSI` 计划，不在 C-DHF 阶段实现。

## 证据

- [`C-DHF-04/05/07 第二轮矩阵记录`](2026-08-06_cdhf04_05_07_08_round2.md)
- [`C-DHF-06 性能基线实现记录`](2026-08-06_cdhf06_performance_baseline.md)
- 用户手动确认：C-DHF-04/05 剩余物理矩阵与 C-DHF-07 托盘恢复矩阵通过
- Node 31 项、Rust 16 项测试和 Release 构建通过

## 结论

`C-DHF-08` 关闭。`V1-P0` 宿主硬门当前具备关闭所需的 `PASS` 或已接受回退结论；下一阶段不直接进入 M1 海报实现封板，而是进入 `C-DRP` 与 `C-PCB` 的后续工作流。

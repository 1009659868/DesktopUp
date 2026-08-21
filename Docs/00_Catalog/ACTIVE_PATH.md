# 当前活跃文档路径

最后检查：2026-08-21

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | Desktop-Up V2 完整需求基线 | `REQUIREMENTS_INDEX.md` | `FROZEN — 2026-08-20` |
| A | Desktop-Up V2 技术选型 | `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md` | `ACTIVE — TS-H01-E COMPLETE / CANDIDATE ADAPTERS NEXT / NO WINNER` |
| B | 无 | 无 | `NOT STARTED` |
| C | 无 | 无 | `NOT STARTED` |

## 当前接手链

1. `REQUIREMENTS_INDEX.md`（冻结输入）；
2. `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md`；
3. `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_CHARTER_V1.md`；
4. `../A_Project/20_Technical_Selection/TS01_CANDIDATE_FACT_REVIEW_2026-08-20.md`；
5. `../A_Project/20_Technical_Selection/CANDIDATE_REGISTER_V1.md`；
6. `../A_Project/20_Technical_Selection/DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md`；
7. `../A_Project/20_Technical_Selection/TS_H01_D_EXECUTION_PLAN_V1.md`；
8. `../A_Project/20_Technical_Selection/TS_H01_D3_MATRIX_V1.md`；
9. `../A_Project/20_Technical_Selection/TS_H01_E_DESKTOP_HOST_INTERFACE_FREEZE_PLAN_V1.md`；
10. `../A_Project/20_Technical_Selection/TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md`；
11. `../A_Project/20_Technical_Selection/TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md`；
12. `../A_Project/20_Technical_Selection/TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md`；
13. `../A_Project/20_Technical_Selection/TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md`；
14. `../A_Project/20_Technical_Selection/TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md`；
15. `../A_Project/20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md`；
16. `../A_Project/20_Technical_Selection/TS_H01_E_COMPLETION_AUDIT_2026-08-21.md`；
17. `../A_Project/20_Technical_Selection/UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md`；
18. `../A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md`。

## 当前冻结边界

- 七份需求已由用户确认冻结；12 项显式 TBD/DEFERRED 继续按登记状态存在；
- Qt、原生 Win32、WinUI、WPF、Avalonia、WebView2、Slint 和组合候选已完成静态事实审查，但均未执行 P0、没有赢家；
- 公共宿主目标固定为“普通/全屏程序 > 海报 > 桌面图标 > 壁纸”；Lively 的图标下方壁纸路径不满足本项目；
- 旧技术选型、架构尝试和原型不具有默认优先级，只能作为历史风险输入；
- 决策记录当前是 `NO WINNER / DO NOT IMPLEMENT`；
- 架构、制作计划、B/C 工作流和代码落地仍未激活。

## 下一动作

TS-H01-E 已完整封闭：E2 数据、E3 生命周期、E4 原子投影、E5 诊断夹具和 E6 候选交接均已冻结。D2/D3 例外、未测 DPI、多屏、traditional WorkerW 与 renderer adapter 继续保持原证据状态。下一动作按 TS-C01 至 TS-C07 稳定 ID 建立候选 adapter 与独立 P0 Evidence；仍无框架赢家。

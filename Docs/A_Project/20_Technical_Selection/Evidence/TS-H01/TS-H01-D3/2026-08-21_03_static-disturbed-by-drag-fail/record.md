# TS-H01-D3 Static Regression — Disturbed Run

状态：`FAIL — STATIC COUNT CONTRACT DISTURBED BY ONE DRAG`

日期：2026-08-21

加入交互动作完成后 1.5 秒稳定退出逻辑并重新构建后，第一次静态回归期间标题岛收到一次真实拖动。608/608 结构样本均保持边界合同，但最终 `drag_count=1` 与静态期望 0 不符，故探针正确返回 `boundary_contract_fail` 和退出码 4。该 run 不作为无操作静态证据，也不覆盖后续复跑。

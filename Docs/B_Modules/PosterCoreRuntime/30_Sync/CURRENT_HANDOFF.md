# Poster Core Runtime 当前交接

最后更新：2026-08-14

状态：`ACTIVE — C-PSI-02 AUTHORITY`

## 已建立

- C-DRP-01..06 已关闭，生产宿主通过版本化契约提供稳定边界；
- C-PCB-01..05 已关闭，正式快照、几何、标题栏权限、模板失败和 C-PSS 输入已冻结；
- C-PSS-01..05 已关闭：SQLite 唯一权威、revision/CAS、legacy 备份迁移、故障恢复和最终单海报 Release 核心回归通过；
- C-LST-01..03 已关闭：七项主屏目标由既有实现、Node 159/159、Rust 132/132 与用户人工 Release 证据覆盖，未新增生产代码；
- 当前 Release 只认证主屏单海报，多显示器、混合 DPI、负坐标、主屏切换、跨屏重启和热插拔未认证。

## 已知缺陷

- 控制中心 legacy `accent/opacity/density` 视觉参数调整回弹，用户决定后续修复；
- 该组字段不属于 C-PSS 正式 schema，归后继 Template Platform/schema 参数面板收敛，不能表述为已通过。

## 后继路线

- C-IBC、C-RLC、C-LST 与 C-PSI-01 已关闭；审计矩阵已冻结 7 组 PASS 和 7 个真实 GAP，当前执行 C-PSI-02；
- C-PSI-02..04 只补矩阵登记缺口；通过后转入 B-COL/C-CIM，让正式 Item 契约、仓库和数据驱动投影替代演示卡片，再推进 C-ROD/C-PAA/C-LCH；
- C-LCH 通过后返回 C-PCRG，用正式 Item 与真实启动链封板；
- C-PSS 已完成并同步；当前不得重新打开 Poster 状态 authority 或扩大多海报范围。

## 工作区注意事项

- legacy 只读来源和既有备份继续保留，不因 C-PSS 关闭而删除；
- 不扩大主屏单海报认证范围；
- 不把结构上的多 poster 数组解释为已经具备多海报产品能力。

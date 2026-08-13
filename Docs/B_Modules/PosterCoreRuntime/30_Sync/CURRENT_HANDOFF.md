# Poster Core Runtime 关闭阶段交接

最后更新：2026-08-12

状态：`INACTIVE AT B-PCR — C-PCB/C-PSS COMPLETE / C-LST PLANNED`

## 已建立

- C-DRP-01..06 已关闭，生产宿主通过版本化契约提供稳定边界；
- C-PCB-01..05 已关闭，正式快照、几何、标题栏权限、模板失败和 C-PSS 输入已冻结；
- C-PSS-01..05 已关闭：SQLite 唯一权威、revision/CAS、legacy 备份迁移、故障恢复和最终单海报 Release 核心回归通过；
- 当前 Release 只认证主屏单海报，多显示器、混合 DPI、负坐标、主屏切换、跨屏重启和热插拔未认证。

## 已知缺陷

- 控制中心 legacy `accent/opacity/density` 视觉参数调整回弹，用户决定后续修复；
- 该组字段不属于 C-PSS 正式 schema，归后继 Template Platform/schema 参数面板收敛，不能表述为已通过。

## 后继路线

- 全局路线先进入 C-IBC，再进入 C-RLC；二者当前待用户激活；
- 完成 C-IBC/C-RLC 后回到 B-PCR 激活 C-LST；
- 当前 C-PSS 实现尚未提交或推送，最近远端同步基点为 `629248c`。

## 工作区注意事项

- legacy 只读来源和既有备份继续保留，不因 C-PSS 关闭而删除；
- 不扩大主屏单海报认证范围；
- 不把结构上的多 poster 数组解释为已经具备多海报产品能力。

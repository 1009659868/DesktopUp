# Poster Core Runtime 当前交接

最后更新：2026-08-12

状态：`ACTIVE AT B-PCR / C-PSS-05 RELEASE RECOVERY GATE`

## 已建立

- C-DRP-01..06 已关闭；生产宿主通过 `host-contract-v1`、`sync_host_surface` 与 `runtime-lifecycle-v1` 向海报核心提供稳定边界。
- 当前 Release 只认证主屏单海报。多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔没有被表述为已验证。
- 用户已验收当前单海报的空白穿透、卡片命中、标题栏拖动/锁定、收起/展开、无顶部白条、Win+D 稳定性、生命周期以及控制中心按需创建。
- C-PCB-01..05 与 C-PSS-01..03 已完成；状态仓库具备 schema、结构化 load、Rust v1 校验、原子 replace/CAS、完整 legacy 备份和第一海报幂等迁移。
- C-PSS-04 已由用户验收关闭：SQLite 是唯一正式状态权威，视觉、桌面交互、退出重启恢复和控制中心往返无回退。

## 当前边界

- 当前默认快照只创建一个海报，但契约本身不通过数组长度封死未来多海报。
- `interaction` 与 `runtime` 是运行时派生状态，不进入海报持久快照。
- `transparent-default` 是正常默认模板；`minimal-fallback` 是内部临时渲染选择，不能持久化为用户模板。
- A0 第二演示海报只留在隔离原始备份，不进入正式可见状态。
- 窗口物理坐标、DPI、WorkerW/Progman、输入钩子继续归宿主。

## 精确下一步

当前进入 [C-PSS 当前交接](../../../C_Workstreams/PosterStateStore/30_Sync/CURRENT_HANDOFF.md) 的 C-PSS-05：Git 同步检查点 `b447f1f` 已创建，本轮推送后用隔离数据库副本和故障注入完成损坏、未来 schema、事务失败、CAS 冲突、备份恢复与 Release 核心回归。

## 工作区注意事项

- 当前分支：`impl/poster-redesign-V1`；本地与远端同步基点无分叉，实施检查点为 `b447f1f`，本轮完成相邻交接提交后推送。
- C-PSS-05 关闭前不删除 legacy 读取能力、原始来源或已生成备份；不得在无恢复副本时损坏真实用户数据库。

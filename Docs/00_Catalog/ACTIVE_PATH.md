# 当前活跃文档路径

最后检查：2026-08-13

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P1 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE — V1-P1` |
| B | 下一候选：Distribution Lifecycle | `B_Modules/DistributionLifecycle/10_Plan/MODULE_PLAN.md` | `READY — C-IBC ACTIVATION PENDING` |
| C | C-PSS 已关闭；C-DRP-04 维护复测 | `C_Workstreams/DesktopRuntimeProductionization/30_Sync/CURRENT_HANDOFF.md` | `NO ACTIVE C — MAINTENANCE USER MATRIX PENDING` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. C-DRP-04 维护复测：`C_Workstreams/DesktopRuntimeProductionization/30_Sync/CURRENT_HANDOFF.md`
3. C-PSS 关闭追溯：`C_Workstreams/PosterStateStore/30_Sync/COLD_START.md`
4. 下一候选模块：`B_Modules/DistributionLifecycle/10_Plan/MODULE_PLAN.md`

## 当前决策门

V1-P0、C-DHF-01..08、C-DRP-01..06、C-PCB-01..05 与 C-PSS-01..05 已关闭。

- 运行时只有一个全局透明宿主 WebView；
- 宿主拥有原生窗口几何、显示/DPI 映射、桌面挂载、命中、暂停和恢复；
- 海报核心只通过版本化接口提交逻辑表面，不访问 Windows 私有实现；
- SQLite 是海报正式状态唯一权威，legacy JSON/localStorage 仅为只读迁移来源；
- Release 隔离恢复矩阵、CAS/事务/备份恢复与主屏单海报核心回归已经通过。

2026-08-10 用户确认 C-DRP-02 的新版 Release 冷启动无顶部白条，收起/展开无裁切且按钮命中正常，并决定将第二显示器/混合 DPI 等物理矩阵延期；该延期不得被表述为已经完成多屏实机验证。随后用户在修复后的 Release 上确认空白框选/右键、原生快捷方式、六张卡片、标题栏拖动/锁定、无白条及收起/展开全部通过；C-DRP-03 以单海报范围关闭。

2026-08-11 用户明确接受“当前发布基线只认证主屏单海报”的平台限制；第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔仍未认证。C-DRP 与 C-PCB 随后关闭。

2026-08-12 C-PSS-01..04 完成 schema/CAS、结构化加载、Rust v1 校验、原子替换、完整 legacy 原始备份、第一海报幂等迁移和 SQLite 唯一权威切换。C-PSS-05 Release 隔离恢复矩阵 `8/8` 通过，正式配置目录哈希不变；用户确认最终 Release 的视觉、桌面/卡片/标题栏交互、退出重启持久化和控制中心往返四项核心回归通过，C-PSS 关闭。

同轮用户报告控制中心 legacy `accent/opacity/density` 视觉参数调整回弹并决定后续修复。该缺陷不记为通过，归后继 Template Platform/schema 参数面板处理；不得通过扩张 C-PSS schema 掩盖。

2026-08-13 用户报告旧 C-DRP-04 的 48ms TOPMOST 防护在桌面右键关闭、刷新和 Windows 文件选择器出现时造成海报瞬时覆盖普通程序。维护版已删除通用 `HWND_TOPMOST/NOTOPMOST`，改为真实桌面 Shell 过滤和有界 Progman 相对重锚；自动检查与 Release/NSIS 构建通过，当前先完成用户人工矩阵。该维护不重新激活 C-DRP，也不改变 C-IBC 仍需用户明确激活的规则。

当前推进顺序（2026-08-13）：

```text
C-DRP（COMPLETE）
→ C-PCB（COMPLETE）
→ C-PSS（COMPLETE）
→ C-IBC（NEXT — ACTIVATION PENDING）
→ C-RLC
→ C-LST / C-PSI / C-PCRG
→ C-ROD / C-PAA / C-LCH（Usable MVP）
→ C-SVT / C-RCV（Safe Collection MVP）
```

当前没有活跃 C；先完成 C-DRP-04 维护 Release 人工矩阵，下一候选仍是 C-IBC 安装/数据目录早期契约并等待用户明确激活。主屏单海报仍是唯一发布认证范围；不得把 C-PSS 关闭解释为 V1-P1、M1、真实导入或最终安装/卸载认证完成。

2026-08-08 在 `impl/poster-redesign-V1-fix` 完成关闭后维护修复并合入 `impl/poster-redesign-V1`：统一 Dev/Release 海报快照，修正锁定与尺寸包络、托盘快捷入口、任务栏契约、桌面 Z 序和顶部亮带；这些结论作为 C-DRP 输入，不把 M1 标记为完成。

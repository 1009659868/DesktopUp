# 当前活跃文档路径

最后检查：2026-08-12

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P1 生产宿主、核心契约与数据基线 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE — V1-P1` |
| B | Poster Core Runtime / 海报核心运行时 | `B_Modules/PosterCoreRuntime/10_Plan/MODULE_PLAN.md` | `ACTIVE — C-PSS` |
| C | Poster State Store / 海报状态仓库 | `C_Workstreams/PosterStateStore/10_Plan/WORKSTREAM_PLAN.md` | `ACTIVE — C-PSS-05 RELEASE RECOVERY GATE` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `B_Modules/PosterCoreRuntime/30_Sync/COLD_START.md`
3. `C_Workstreams/PosterStateStore/30_Sync/COLD_START.md`

## 当前决策门

V1-P0 与 C-DHF-01..08 已关闭。当前 C-DRP 必须把验证原型固化为生产宿主：

- 运行时只有一个全局透明宿主 WebView；
- 宿主拥有原生窗口几何、显示/DPI 映射、桌面挂载、命中、暂停和恢复；
- 海报核心只通过版本化接口提交逻辑表面，不访问 Windows 私有实现；
- 多区域命中、空白穿透、不可激活和桌面 Z 序不回退；
- Release 静态/暂停性能满足量化预算。

2026-08-10 用户确认 C-DRP-02 的新版 Release 冷启动无顶部白条，收起/展开无裁切且按钮命中正常，并决定将第二显示器/混合 DPI 等物理矩阵延期至 C-DRP-06 发布门；该延期不得被表述为已经完成多屏实机验证。
随后用户先确认旧 C-DRP-03 的三项桌面输入失败，再在修复后的最新 Release 上确认空白框选/右键、原生快捷方式、六张卡片、标题栏拖动/锁定、无白条及收起/展开全部通过；C-DRP-03 以单海报范围关闭。
用户确认最终 Release 的连续 Win+D 已较稳定并接受 C-DRP-04 通过；C-DRP-05 的生命周期人工矩阵随后 5/5 通过并关闭。
2026-08-11 用户明确接受“当前发布基线只认证主屏单海报”的平台限制；第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔仍未认证。C-DRP-06 与 C-DRP 因此关闭，活动链切换到 B-PCR / C-PCB。C-PCB-01 完成正式 v1 契约与 legacy 分流，C-PCB-02 冻结工作区派生默认几何，C-PCB-03 冻结标题栏权限矩阵，C-PCB-04 完成正常默认、用户模板与内部 fallback 的 JS/Rust 共享失败矩阵。
2026-08-12 用户确认 C-PCB-05 的五项主屏单海报 Release 核心回归全部通过并要求激活 C-PSS。C-PSS-01..03 已完成 schema/CAS、结构化加载、Rust v1 校验、原子替换、完整 legacy 原始备份和第一海报幂等迁移。
同日用户确认 C-PSS-04 最新 Release 的视觉、桌面交互、SQLite 重启恢复和控制中心往返全部通过；C-PSS-04 关闭，当前激活 C-PSS-05 Release 恢复门。

当前推进顺序（2026-08-12）：

```text
C-DRP（COMPLETE）
→ C-PCB（COMPLETE）
→ C-PSS（ACTIVE）
→ C-IBC / C-RLC
→ C-LST / C-PSI / C-PCRG
→ C-ROD / C-PAA / C-LCH（Usable MVP）
→ C-SVT / C-RCV（Safe Collection MVP）
```

`C-PSS` 是当前唯一活跃 C；`C-PCB` 已完成。SQLite 已是正式唯一权威，当前只推进 C-PSS-05 隔离恢复矩阵；门关闭前不删除 legacy 读取能力、来源或备份。主屏单海报仍是唯一发布认证范围；本次激活不把 V1-P1、M1、真实导入或安装包标记为实现通过。

2026-08-08 在 `impl/poster-redesign-V1-fix` 完成关闭后维护修复并合入 `impl/poster-redesign-V1`：统一 Dev/Release 海报快照，修正锁定与尺寸包络、托盘快捷入口、任务栏契约、桌面 Z 序和顶部亮带；这些结论作为 C-DRP 输入，不把 M1 标记为完成。

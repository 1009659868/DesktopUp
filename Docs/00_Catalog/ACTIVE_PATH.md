# 当前活跃文档路径

最后检查：2026-08-13

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P1 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE — V1-P1` |
| B | Desktop Runtime Foundation | `B_Modules/DesktopRuntimeFoundation/30_Sync/CURRENT_HANDOFF.md` | `NEXT — C-RLC READY TO ACTIVATE` |
| C | Runtime Lifecycle and Quick Access / C-RLC | `B_Modules/DesktopRuntimeFoundation/10_Plan/MODULE_PLAN_V1.md` | `READY TO ACTIVATE` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `B_Modules/DesktopRuntimeFoundation/30_Sync/COLD_START.md`
3. C-IBC 冻结输入：`C_Workstreams/InstallBaselineContract/10_Plan/C_IBC_HANDOFF_INPUTS_V1.md`
4. C-DRP-04 维护通过追溯：`C_Workstreams/DesktopRuntimeProductionization/30_Sync/CURRENT_HANDOFF.md`

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

2026-08-13 用户报告旧 C-DRP-04 的 48ms TOPMOST 防护在桌面右键关闭、刷新和 Windows 文件选择器出现时造成海报瞬时覆盖普通程序。维护版已删除通用 `HWND_TOPMOST/NOTOPMOST`，改为真实桌面 Shell 过滤和有界 Progman 相对重锚；用户随后确认桌面右键关闭、刷新、文件选择器、连续 Win+D 与核心交互矩阵全部通过。C-DRP 仍保持关闭，主线已按用户指令激活 C-IBC。

同日 C-IBC-01 完成：`install-baseline-v1` 冻结当前用户无提权安装、程序/数据分离、当前 roaming authority 保留到 C-UGM、自启默认关闭以及 WebView2/VC/SQLite 依赖基线；下一步为 C-IBC-02 运行时路径解析与兼容诊断。

C-IBC-02 随后完成：`install-path-diagnostics-v1` 统一解析安装/数据/当前 authority 和七个目标角色，setup 保持 SQLite 位于 roaming 根；Release 启停前后 SQLite 哈希一致且目标 LocalAppData 根未创建。

C-IBC-03 已完成双层只读探针：运行时核对目标安装根与最低内容，构建侧审计 current-user/无提权、NSIS 卸载入口和产物哈希；五项发布差距已归 C-RCG，未执行安装/卸载/提权，下一任务为 C-IBC-04 自启边界与清理契约。

C-IBC-05 后续维护已按契约收敛原十项：安装根、主程序名、资源、最终安装器和自启清理五项实际修复，五项许可证相关项按个人自用范围判定不适用；当前组合门为 `contractPackagingReady`、`gaps=[]`。净机、升级、恢复型卸载和签名矩阵仍未认证。C-IBC-01..05 保持关闭，下一候选为 C-RLC。

当前推进顺序（2026-08-13）：

```text
C-DRP（COMPLETE）
→ C-PCB（COMPLETE）
→ C-PSS（COMPLETE）
→ C-IBC（COMPLETE）
→ C-RLC（READY TO ACTIVATE）
→ C-LST / C-PSI / C-PCRG
→ C-ROD / C-PAA / C-LCH（Usable MVP）
→ C-SVT / C-RCV（Safe Collection MVP）
```

当前没有正在实施的 C；C-IBC-01..05 已关闭，C-RLC 等待用户明确激活。主屏单海报仍是唯一发布认证范围；不得把依赖证据通过或带已分配缺口的交接解释为 V1-P1、M1、真实导入或最终安装/卸载认证完成。

2026-08-08 在 `impl/poster-redesign-V1-fix` 完成关闭后维护修复并合入 `impl/poster-redesign-V1`：统一 Dev/Release 海报快照，修正锁定与尺寸包络、托盘快捷入口、任务栏契约、桌面 Z 序和顶部亮带；这些结论作为 C-DRP 输入，不把 M1 标记为完成。

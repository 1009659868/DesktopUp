# 当前活跃文档路径

最后检查：2026-08-14

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P2 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE — V1-P2` |
| B | Poster Core Runtime | `B_Modules/PosterCoreRuntime/30_Sync/CURRENT_HANDOFF.md` | `ACTIVE — C-PSI` |
| C | Poster State and Interaction / C-PSI | `C_Workstreams/PosterStateInteraction/30_Sync/CURRENT_HANDOFF.md` | `ACTIVE — C-PSI-03 GEOMETRY` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `B_Modules/PosterCoreRuntime/30_Sync/COLD_START.md`
3. `B_Modules/PosterCoreRuntime/30_Sync/CURRENT_HANDOFF.md`
4. C-LST 已关闭输入：`C_Workstreams/LargeSurfaceTransparency/30_Sync/CURRENT_HANDOFF.md`
5. C-PSI 当前入口：`C_Workstreams/PosterStateInteraction/30_Sync/COLD_START.md`
6. C-CIM 提前计划：`C_Workstreams/CollectionItemModelProjection/30_Sync/COLD_START.md`

## 当前决策门

V1-P0、V1-P1、C-DHF-01..08、C-DRP-01..06、C-PCB-01..05、C-PSS-01..05、C-IBC-01..05、C-RLC-01..06 与 C-LST-01..03 已关闭。

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
同日用户明确激活 C-RLC。C-RLC-01 已建立权威工作流文档并冻结 `runtime-lifecycle-matrix-v1`，区分普通/自启启动、第二实例、非法参数、控制中心/托盘、自启注册、TaskbarCreated 恢复和退出；本切片没有修改运行时代码或注册表。C-RLC-02 成为唯一下一任务。

C-RLC-02 随后完成 `launch-intent-v1`：普通第二实例只呼回唯一控制中心，精确 `--autostart` 第二实例保持 UI 不变，非法组合以 64 退出；既有性能门被明确为仅首实例内部诊断意图。全量 Node 135/135、Rust 117/117、Release 多进程矩阵和构建通过，下一任务为 C-RLC-03 自启显式 opt-in 与状态同步。

C-RLC-03 已实现 `autostart-change-v1`、正式路径门、串行写后读回和统一原生快照；拖动白条、控制中心投影与默认身份回归均已通过。2026-08-14 用户明确授权 feature-scoped `G:\A_\Tools\Test\DesktopUp.exe` 作为临时 eligible 测试目标，真实 enable/noChange/`--autostart`/普通二次启动/disable/readback 与其他 Run 值不变全部通过，C-RLC-03 关闭；正式安装合同仍保持 `%LOCALAPPDATA%\Programs\DesktopUp`。

同日后续截图暴露控制中心空画布：根因是桌面坐标未经投影直接进入较小编辑画布。独立可逆编辑视口完成后，用户确认控制中心可见、窗口缩放可见、编辑关闭重开可见和真实海报拖动层级回归全部通过。随后干净启动默认恢复为 `NIGHT OPS / TACTICAL ARCHIVE / 01`；当前 revision 38 编辑状态按用户明确授权只恢复身份并通过正式 CAS 推进至 39，尺寸与其他字段保持。


当前推进顺序（2026-08-13）：

```text
C-DRP（COMPLETE）
→ C-PCB（COMPLETE）
→ C-PSS（COMPLETE）
→ C-IBC（COMPLETE）
→ C-RLC（COMPLETE）
→ C-LST（COMPLETE — ABSORBED AND AUDITED）→ C-PSI-01..02（COMPLETE）→ C-PSI-03（ACTIVE）
→ C-CIM（ADVANCE PLAN READY；正式 Item 契约、仓库与数据驱动投影）
→ C-ROD / C-PAA / C-LCH（Usable MVP）
→ C-PCRG（Poster + Item 整合 Release 门）
→ C-SVT / C-RCV（Safe Collection MVP）
```

C-LST 已完成既有实现吸收审计并关闭。C-PSI-01 已冻结 PASS/GAP 矩阵；C-PSI-02 已关闭权限接入、pending/retained mutation 保留和暂停原因分流缺口。当前 C-PSI-03 只处理移动、缩放、动态尺寸边界与非法编辑组合 fail closed。C-PSI 关闭后立即进入已提前规划的 C-CIM，让硬编码演示卡片退出正式路径。主屏单海报仍是唯一发布认证范围；不得把 C-PSI 激活解释为多显示器、真实 Item、真实导入或最终安装/卸载认证完成。

2026-08-08 在 `impl/poster-redesign-V1-fix` 完成关闭后维护修复并合入 `impl/poster-redesign-V1`：统一 Dev/Release 海报快照，修正锁定与尺寸包络、托盘快捷入口、任务栏契约、桌面 Z 序和顶部亮带；这些结论作为 C-DRP 输入，不把 M1 标记为完成。

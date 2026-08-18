# 当前活跃文档路径

最后检查：2026-08-18

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V2 重构 / V2-P0 | `A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md` | `ACTIVE — V2-P0 渲染宿主选定` |
| B | 未建立 | `B_Modules/README.md` | `PENDING` |
| C | 未建立 | `C_Workstreams/README.md` | `PENDING` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md`（§7 架构级需求、§24 分期）
3. `A_Project/30_Sync/CURRENT_HANDOFF.md`
4. `A_Project/30_Sync/KNOWN_ISSUES.md`（V1 继承风险）

## 当前决策门

- V2 首个工作流：渲染宿主选定实验（V2-P0），候选与最小验证矩阵见需求画像 §7.2；
- 任何上层功能不得先于渲染选定启动（RP-ARC-01）；
- V1 结论：透明 WebView2 窗口模式宿主（tao blur-behind + wry + DComp + 动态样式切换）在 Win11 上不可根治白条，已作为架构不继承（需求画像 §2）。

## 前置证据（只读）

- V1 需求画像与问题台账：`G:\A_\Tools\DesktopUp\Docs\...`（旧仓库，不复制进本仓库）。

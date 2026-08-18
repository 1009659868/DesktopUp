# DesktopUP V2 当前问题与继承风险台账

状态：`ACTIVE — V2 INHERITED RISK REGISTER`

最后更新：2026-08-18

适用范围：DesktopUP V2、分支 `impl/Refactor`

## 1. 文档职责

本台账记录 V2 从 V1 继承的已知风险、被否定结论与防回归门，以及 V2 自身的新问题。V1 的完整排查证据保存在旧仓库 `G:\A_\Tools\DesktopUp\Docs\A_Project\30_Sync\KNOWN_ISSUES.md`（只读追溯），本台账只保留结论与 V2 处置。

分类与生命周期规则沿用 V1 台账 §2（BUG/BLOCKER/FUNCTION GAP/VALIDATION GAP/SCOPE DEFERRED/REGRESSION WATCH/ARCHITECTURE RISK；P0..P3；REPORTED→CLOSED 与 DISPROVEN/WITHDRAWN/REOPENED）。

## 2. 继承风险

### V2-IH-001：透明 WebView2 窗口模式宿主的白条根因类别

| 字段 | 内容 |
| --- | --- |
| 类型 | `ARCHITECTURE RISK`（V1 为 P0 BUG；V2 不继承该架构，故降为防回归风险） |
| 状态 | `ACTIVE — 以 V2-P0 渲染选定关闭` |
| V1 结论 | 白条 = 宿主窗口 DWM 呈现层对表面顶部约 24 行的不透明白色坏合成；屏幕合成多级劣化（白条/分段/缺席）；`PrintWindow` 为过期帧；重绘与禁 GPU 均不能修复 |
| V1 否定清单 | 贴边、壁纸、caption、子窗口偏移、copy-bits、bounds 重同步、HRGN、整屏宿主、owner/坐标、幂等同步、WS_EX_LAYERED、blur-behind/擦除（Accent 替换）、--disable-gpu、强制重绘 |
| V2 处置 | 架构不继承（需求画像 §2.3）；V2-P0 候选矩阵 §7.2 全部含“零白条”与“呈现自检”门 |
| 关闭标准 | V2 选定渲染器通过 §7.2 全矩阵，且白条/分段/缺席状态在实机不可复现 |

### V2-IH-002：控制中心打不开（V1 诊断构建回归）

| 字段 | 内容 |
| --- | --- |
| 类型 | `BUG`（V1 诊断构建期出现，静默失败，根因未解） |
| 严重度 | `P1` |
| 状态 | `ACTIVE — 归 V2-P2 控制中心重建` |
| 已知表现 | 双击托盘/第二实例唤回均无法创建控制中心窗口；stderr 无任何错误输出 |
| 约束 | V2 不修复 V1 的该缺陷；V2 控制中心必须以全新路径重建并自带可诊断失败输出 |
| 关闭标准 | V2 控制中心按需打开/关闭矩阵通过，创建失败必须产生结构化错误与日志 |

### V2-IH-003：legacy 视觉参数回弹（V1 DU-BUG-002）

| 字段 | 内容 |
| --- | --- |
| 类型 | `BUG` |
| 严重度 | `P1` |
| 状态 | `DEFERRED — 归模板平台参数权威` |
| 约束 | 不得通过扩张 Poster schema 修复；V2 模板平台以 schema 参数面板重建该能力 |

### V2-IH-004：PrintWindow 过期帧陷阱

`REGRESSION WATCH`。PrintWindow/GDI 后备缓冲区在 V1 中持有过期帧（深色旧主题）而 CDP 实时帧为浅色玻璃。V2 任何验证不得把 PrintWindow 当作渲染真相（RP-ARC-05）。

### V2-IH-005：0 FPS 静态渲染的呈现冻结

`REGRESSION WATCH`。静态 0 FPS 策略下呈现层劣化不会自愈。V2 必须把强制重绘/呈现恢复与触发类操作绑定（RP-ARC-04）。

## 3. 当前优先顺序

```text
1. 建立 V2-P0 渲染宿主选定实验工作流（候选清单与矩阵已冻结于需求画像 §7.2）
2. 逐候选执行矩阵 → 选定决策 → 冻结渲染架构
3. 以选定渲染器重建桌面宿主（V2-P1）→ 控制中心（V2-P2）→ 上层路线
```

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-18 | V1 | 建立 V2 继承风险台账：V1 白条结论与否定清单、控制中心静默失败、参数回弹、PrintWindow 陷阱、0 FPS 冻结 |

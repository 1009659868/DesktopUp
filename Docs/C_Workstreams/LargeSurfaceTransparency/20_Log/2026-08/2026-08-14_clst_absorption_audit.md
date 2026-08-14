# C-LST 既有实现吸收审计

日期：2026-08-14

状态：`COMPLETE — ABSORBED AND AUDITED`

## 1. 目标

核对 Large Surface and Transparency 的计划目标是否已被 C-DRP、C-PCB、C-PSS 与 C-RLC 的实现和验收吸收；没有证据的项目不得通过路线整理标记为完成。

## 2. 初步证据矩阵

| 验收面 | 实现/契约证据 | 人工或历史证据 | 初步结论 |
| --- | --- | --- | --- |
| 主屏右上大幅面默认几何 | JS/Rust `poster-geometry-v1` 与共享矩阵；22% 宽、近满高、24px 边距 | 用户多轮 Release 截图与启动确认 | `PASS — EXISTING EVIDENCE REUSED` |
| 显式尺寸边界 | 240px 最小宽/展开高、45% 最大宽、工作区最大高 | 控制中心缩放和恢复人工通过 | `PASS — EXISTING EVIDENCE REUSED` |
| 收起顶部/左右位置保持 | 展开/收起共用 host offset，收起只变高度 | 用户确认按钮原位、无裁切 | `PASS — EXISTING EVIDENCE REUSED` |
| 透明背景与无顶部白条 | Tauri transparent/backgroundColor、无 decorations/shadow、Windows style 不变量 | 冷启动、拖动、收起/展开均由用户确认无白条 | `PASS — EXISTING EVIDENCE REUSED` |
| 透明空白与桌面原生输入 | 完整 visualRegion 与独立 interactionRegions | 框选、右键、原生快捷方式人工通过 | `PASS — EXISTING EVIDENCE REUSED` |
| 位置/尺寸/锁定/收起恢复 | SQLite-first、CAS、placement 与 HostRecoveryCoordinator | 退出重启、控制中心往返人工通过 | `PASS — EXISTING EVIDENCE REUSED` |
| 普通程序层级 | ToolWindow/WorkerW 与 Progman 相对重锚；无通用 TOPMOST | 右键、刷新、文件选择器、Win+D 和普通程序人工通过 | `PASS — EXISTING EVIDENCE REUSED` |
| 多显示器物理矩阵 | 仅有契约和部分模拟测试 | 用户明确暂不测试 | `DEFERRED — NOT CERTIFIED` |

## 3. 当前自动化

- Node/源码契约：`159/159`；
- Rust：`132/132`；
- 文档链接：修正本轮新计划中的两处预备链接后通过；
- 本轮生产代码变化：`0`。

结论：C-LST 没有独立的生产实现缺口，以 `COMPLETE — ABSORBED AND AUDITED` 关闭。已有多屏延期继续保留，不影响主屏单海报结论。

## 4. 边界

本审计不把 C-PSI 的完整状态/编辑权限、不把 Item 数据、不把模板平台，也不把多显示器认证吸收到 C-LST。

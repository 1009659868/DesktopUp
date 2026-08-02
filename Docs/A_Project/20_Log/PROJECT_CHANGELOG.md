# Desktop-Up 项目更迭记录

本文件只记录项目级已发生事实。局部实现过程进入对应 C 工作流日志。

## 2026-08-02 — 项目文档与设计基线建立

状态：`COMPLETE`

- 接受 A/B/C 三层文档模型：A 项目主线、B 能力模块、C 可执行工作流。
- 建立 Catalog、Plan、Log、Sync 固定分区和权威阅读路径。
- 建立产品设计 V0.1、技术架构 V0.1 和制作交付计划 V0.1。
- 当前项目阶段定为 `A0 可行性与体验定型`。
- 当前活跃 B 模块定为 `Desktop Runtime Foundation`。
- 当前活跃 C 工作流定为 `Desktop Host Feasibility`。
- 最终 Windows 安装入口暂定为 `DesktopUpSetup.exe`。
- 确立安装目录必须包含独立的 `uninstall.exe`。
- 确立卸载前恢复托管快捷方式、失败时保留保险库的安全底线。

关联文档：

- [`PROJECT_MAINLINE.md`](../10_Plan/PROJECT_MAINLINE.md)
- [`PRODUCT_DESIGN_V0.1.md`](../10_Plan/PRODUCT_DESIGN_V0.1.md)
- [`TECHNICAL_ARCHITECTURE_V0.1.md`](../10_Plan/TECHNICAL_ARCHITECTURE_V0.1.md)
- [`DELIVERY_PLAN_V0.1.md`](../10_Plan/DELIVERY_PLAN_V0.1.md)


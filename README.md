# Desktop-Up V2

Desktop-Up V2 是面向 Windows 11 的桌面海报、内容入口与个性化主题平台重构项目。

七份完整需求已于 2026-08-20 冻结，仓库当前处于全新的技术选型阶段。TS-01、TS-H01-A 以及当前 `RAISED_PROGMAN` 路径的 TS-H01-B/C/D1 已通过；ordinary GDI FAIL 和 traditional WorkerW UNKNOWN 证据继续保留。当前执行 TS-H01-D2 Explorer 重建安全退化/恢复人工门；没有框架赢家。

文档入口：[Docs/README.md](Docs/README.md)

当前本地分支：`impl/Refactor`

## 当前权威边界

- 已保留：完整需求画像、海报视觉与交互、控制中心/托盘/海报关系、内容 Item 活跃度动态排版、主题与 AI、安全与发布底线、历史风险和来源追溯。
- 尚未选定：UI 框架、渲染器、桌面宿主方式、编程语言、进程拓扑、IPC、状态存储和安装技术。
- 已建立但未得出结论：技术候选登记、统一 P0 硬门、加权评分和决策记录；当前没有赢家。
- `Docs/Reference/V1_Design_Source/` 是只读历史来源；V2 当前需求以 `Docs/A_Project/10_Requirements/` 为准。

## 后续阶段顺序

```text
技术候选事实审查（已完成）
→ 公共 Desktop Overlay Host 原生探针
→ 渲染候选同构 P0 可证伪原型与统一证据矩阵
→ 架构设计与公共契约
→ 总体制作计划与阶段门
→ 模块/工作流计划
→ 代码落地与验证
```

任何阶段都不能把上一轮实现曾经采用的技术自动视为默认方案。

# Desktop-Up V2

Desktop-Up V2 是面向 Windows 11 的桌面海报、内容入口与个性化主题平台重构项目。

七份完整需求已于 2026-08-20 冻结。DesktopHost TS-H01-E 已完成；仅使用 Axiom Core 的 AX-BP-01～06 系统蓝图已经完成并冻结，Axiom 已退出执行主线。renderer、控制中心 UI、存储、生产 runtime 和安装技术仍没有赢家。

文档入口：[Docs/README.md](Docs/README.md)

当前本地分支：`impl/Refactor`

## 当前权威边界

- 已保留：完整需求画像、海报视觉与交互、控制中心/托盘/海报关系、内容 Item 活跃度动态排版、主题与 AI、安全与发布底线、历史风险和来源追溯。
- 已冻结：[AX-BP 蓝图索引](Docs/A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md)及 [Axiom Core 程序蓝图](Docs/A_Project/30_Architecture/AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md)，用于描述模块端口、调用、组合和物理执行边界；只使用 Core，明确排除 unstable axiom-runtime。
- 已验证：[Axiom 蓝图类型夹具](prototype/axiom-desktopup-blueprint/README.md)锁定无伪造默认状态、CAS 权威闭环和同 revision 投影。
- 尚未选定：UI 框架、renderer、DesktopHost 正式 adapter、进程拓扑、IPC、状态存储、生产 runtime 和安装技术。
- 已建立但未得出结论：技术候选登记、统一 P0 硬门、加权评分和决策记录；当前没有赢家。
- 当前执行：TS-C01 Qt Quick 的 R2 人工门与 R3 自动预检已 PASS，下一步为 R4 人工 P0；P0 仍未运行完成，不能据此选择 renderer。
- `Docs/Reference/V1_Design_Source/` 是只读历史来源；V2 当前需求以 `Docs/A_Project/10_Requirements/` 为准。

## 后续阶段顺序

```text
DesktopHost 原生探针与接口冻结（已完成）
→ Axiom Core 系统蓝图 AX-BP-01～06（已完成并冻结；不实现 runtime）
→ 渲染候选 P0 与其他技术选型 → 获选 adapter 的完整架构集成
→ 总体制作计划与阶段门
→ 模块/工作流计划
→ 代码落地与验证
```

任何阶段都不能把上一轮实现曾经采用的技术自动视为默认方案。

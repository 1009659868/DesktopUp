# Desktop Runtime Productionization 任务索引

| 任务 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-DRP-01 宿主接口与所有权契约 | 版本化 DTO、单几何所有权和兼容测试 | C-DHF | `COMPLETE` |
| C-DRP-02 单全局宿主拓扑 | 单 WebView、统一坐标及分辨率/DPI 响应缩放 | C-DRP-01 | `IN PROGRESS` |
| C-DRP-03 表面登记、合并命中与空白穿透 | 多区域命中和桌面空白穿透 | C-DRP-02 | `PLANNED` |
| C-DRP-04 桌面适配器与恢复协调器 | WorkerW/ToolWindow、Win+D、Shell/显示恢复幂等 | C-DRP-02..03 | `PLANNED` |
| C-DRP-05 暂停、可见性与生命周期接口 | 确定性状态、无持锁 UI 调用和无高频轮询 | C-DRP-01,04 | `PLANNED` |
| C-DRP-06 Release、性能与后继接口门 | Release/实机/预算通过并可交接 C-PCB | C-DRP-01..05 | `PLANNED` |

详细范围、验证面和回退见 [`WORKSTREAM_PLAN.md`](../10_Plan/WORKSTREAM_PLAN.md)。Catalog 只索引状态，不复制实施计划。

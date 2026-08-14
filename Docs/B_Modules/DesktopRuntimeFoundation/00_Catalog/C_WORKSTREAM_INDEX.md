# Desktop Runtime Foundation 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`DesktopHostFeasibility`](../../../C_Workstreams/DesktopHostFeasibility/10_Plan/WORKSTREAM_PLAN.md) (`C-DHF`) | 验证桌面挂载、恢复、暂停、性能和托盘硬门 | 无 | `COMPLETE` |
| [`DesktopRuntimeProductionization`](../../../C_Workstreams/DesktopRuntimeProductionization/10_Plan/WORKSTREAM_PLAN.md) (`C-DRP`) | 将验证结果固化为单全局生产宿主和稳定接口 | C-DHF | `COMPLETE — PRIMARY-SCREEN SINGLE-POSTER CERTIFIED` |
| [Runtime Lifecycle and Quick Access](../../../C_Workstreams/RuntimeLifecycleQuickAccess/10_Plan/WORKSTREAM_PLAN.md) (`C-RLC`) | 托盘、单实例、控制中心、自启、退出和 TaskbarCreated 恢复 | C-DRP, C-IBC | `COMPLETE` |

C-DHF、C-DRP、前置 C-IBC 与 C-RLC 均已关闭；后继 C-LST 也已完成吸收审计，B-DRF 保持关闭，下一候选为 B-PCR/C-PSI。

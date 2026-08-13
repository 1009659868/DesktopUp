# Desktop Runtime Foundation 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`DesktopHostFeasibility`](../../../C_Workstreams/DesktopHostFeasibility/10_Plan/WORKSTREAM_PLAN.md) (`C-DHF`) | 验证桌面挂载、恢复、暂停、性能和托盘硬门 | 无 | `COMPLETE` |
| [`DesktopRuntimeProductionization`](../../../C_Workstreams/DesktopRuntimeProductionization/10_Plan/WORKSTREAM_PLAN.md) (`C-DRP`) | 将验证结果固化为单全局生产宿主和稳定接口 | C-DHF | `COMPLETE — PRIMARY-SCREEN SINGLE-POSTER CERTIFIED` |
| Runtime Lifecycle and Quick Access (`C-RLC`) | 托盘、单实例、控制中心、自启、退出和 TaskbarCreated 恢复 | C-DRP, C-IBC | `READY TO ACTIVATE` |

C-DHF-08、C-DRP 与前置 C-IBC 已关闭；B-DRF 是下一模块，C-RLC 等待用户明确激活。

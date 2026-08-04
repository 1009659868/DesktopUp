# Desktop Runtime Foundation 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`DesktopHostFeasibility`](../../../C_Workstreams/DesktopHostFeasibility/10_Plan/WORKSTREAM_PLAN.md) (`C-DHF`) | 验证桌面挂载、恢复、暂停、性能和托盘硬门 | 无 | `ACTIVE` |
| Desktop Runtime Productionization (`C-DRP`) | 将验证结果固化为单全局生产宿主和稳定接口 | C-DHF | `PLANNED` |
| Runtime Lifecycle and Quick Access (`C-RLC`) | 托盘、单实例、控制中心、自启、退出和 TaskbarCreated 恢复 | C-DRP | `PLANNED` |

只有 C-DHF-08 关闭后才能激活 C-DRP；未激活工作流暂不创建完整 C 目录。


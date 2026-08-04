# Local AI Assistant 模块计划

最后检查：2026-08-04

状态：`PLANNED`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有 BYOK Provider、Credential Manager、受控 Tool Calls、DesktopCommand、ChangePlan、预览、风险分级、确认、撤销和主题草稿。

不执行 Shell、任意原生代码或未经允许的文件系统操作。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Command and ChangePlan (`C-CCP`) | 手动 UI 与 AI 共用命令、预览、确认、撤销和风险分级 | C-TIE, C-LRC | `PLANNED` |
| Provider and Credential (`C-PCC`) | BYOK Provider、凭据安全和无 Key 不联网 | C-CCP | `PLANNED` |
| Preview and Undo Agent (`C-PUA`) | 受控参数/主题草稿、越权拒绝和失败回退 | C-PCC | `PLANNED` |

## 3. 模块完成信号

- Key 不进入前端、数据库、配置或日志；
- 无 Key 不初始化 Provider、不联网；
- 越权 Tool Calls 被本地校验拒绝；
- 文件系统、模板信任、真实收纳和解锁移动始终显式确认；
- 低风险免确认范围在实现前另行冻结。

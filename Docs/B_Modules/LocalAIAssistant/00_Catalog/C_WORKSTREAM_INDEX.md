# Local AI Assistant 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Command and ChangePlan (`C-CCP`) | 统一命令、预览、确认、撤销和风险分级 | C-TIE, C-LCH | `PLANNED` |
| Provider and Credential (`C-PCC`) | BYOK Provider、凭据和无 Key 不联网 | C-CCP | `PLANNED` |
| Preview and Undo Agent (`C-PUA`) | 受控工具调用、越权拒绝和失败回退 | C-PCC | `PLANNED` |

AI 低风险免确认范围必须在 C-CCP 实施前由用户另行冻结。

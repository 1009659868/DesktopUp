# Collection and Launch 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Read-only Discovery (`C-ROD`) | 只读解析、标准化、图标和去重 | C-PCRG | `PLANNED` |
| Preview and Add-only (`C-PAA`) | 预览、冲突提示和仅添加 | C-ROD | `PLANNED` |
| Safe Vault Transaction (`C-SVT`) | 保险库、账本、安全收纳、撤销和故障注入 | C-PAA | `PLANNED` |
| Launch and Recovery (`C-LRC`) | 启动、失效入口、异常和卸载恢复接口 | C-SVT | `PLANNED` |

真实文件移动不得早于 C-SVT，并必须逐批确认。
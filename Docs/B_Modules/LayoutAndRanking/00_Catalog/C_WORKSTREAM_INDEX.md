# Layout and Ranking 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Launch Telemetry Model (`C-LTM`) | 统计、最近使用、衰减和迁移 | C-LCH | `PLANNED` |
| Ranking Engine (`C-RNK`) | 得分、稳定排序和跳变衰减 | C-LTM | `PLANNED` |
| Elastic Layout Flow (`C-ELF`) | 弹性流、子列和响应式布局 | C-RNK, C-PSI | `PLANNED` |
| Pin Drag and Scaling (`C-PDS`) | 控制层、锚定、拖拽、缩放和动画 | C-ELF | `PLANNED` |

只在上游数据和交互契约稳定后激活。

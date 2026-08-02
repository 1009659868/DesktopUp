# Desktop Host Feasibility 任务索引

| ID | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-DHF-01 | 最小 Rust/Tauri/Svelte 工程可在目标机运行并记录版本基线 | 无 | `READY` |
| C-DHF-02 | 透明无边框宿主可进入桌面层，失败时进入明确回退模式 | C-DHF-01 | `PLANNED` |
| C-DHF-03 | 海报区域可点击/拖动，空白区域不阻塞桌面操作 | C-DHF-02 | `PLANNED` |
| C-DHF-04 | Win+D、Explorer 重启、多屏和 DPI 变化后恢复可见合理位置 | C-DHF-02 | `PLANNED` |
| C-DHF-05 | 全屏/最大化、锁屏和显示器关闭触发事件驱动暂停/恢复 | C-DHF-01 | `PLANNED` |
| C-DHF-06 | 静态、动态、暂停和停止模式完成性能基线 | C-DHF-02, C-DHF-05 | `PLANNED` |
| C-DHF-07 | 形成宿主技术决策、风险清单和下一工作流移交 | C-DHF-01..06 | `PLANNED` |


# Desktop Host Feasibility 任务索引

| ID | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-DHF-01 | 最小 Rust/Tauri 前端工程、单实例和托盘骨架可运行并记录版本基线 | 无 | `COMPLETE` |
| C-DHF-02 | 默认只显示透明桌面收纳层，控制中心按需独立；宿主失败时进入明确回退模式 | C-DHF-01 | `COMPLETE — ACCEPTED TOOLWINDOW FALLBACK` |
| C-DHF-03 | 海报区域可点击/拖动，空白区域不阻塞桌面操作 | C-DHF-02 | `COMPLETE — USER MANUAL MATRIX PASS` |
| C-DHF-04 | Win+D、Explorer 重启、多屏和 DPI 变化后恢复可见合理位置 | C-DHF-02 | `BLOCKED — PHYSICAL ENVIRONMENT UNAVAILABLE / AUTOMATED PASS` |
| C-DHF-05 | 全屏/最大化、锁屏和显示器关闭触发事件驱动暂停/恢复 | C-DHF-01 | `IN PROGRESS — USER PARTIAL PASS / REMAINING PHYSICAL ITEMS DEFERRED` |
| C-DHF-06 | 静态、动态、暂停和停止模式完成性能基线，并在 Desktop-Up 内提供简单性能数值可视化 | C-DHF-02, C-DHF-05 | `IN PROGRESS — IMPLEMENTATION READY / BASELINE SAMPLING PENDING` |
| C-DHF-07 | 通知区域单击/双击/右键、主页/设置/快速主题和 Explorer 恢复通过 | C-DHF-01, C-DHF-04 | `PLANNED` |
| C-DHF-08 | 形成宿主技术决策、风险清单和下一工作流移交 | C-DHF-01..07 | `PLANNED` |

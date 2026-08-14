# Large Surface and Transparency 代码锚点

## 几何与默认值

- [`app/src/poster-geometry.js`](../../../../app/src/poster-geometry.js)：`poster-geometry-v1` 的 22% 默认宽、240px 最小宽/展开高度、45% 最大宽、24px 安全边距、60px 收起内容和顶部保持。
- [`src-tauri/src/poster_geometry.rs`](../../../../src-tauri/src/poster_geometry.rs)：与 JS 同构的 Rust 几何决策和共享矩阵测试。
- [`tests/fixtures/poster-geometry-v1.json`](../../../../tests/fixtures/poster-geometry-v1.json)：主屏工作区样本与期望包络。
- [`app/src/poster-contract.js`](../../../../app/src/poster-contract.js)：正式默认快照使用 primary/top-right 与解析后的宽高。

## 透明、表面与层级

- [`src-tauri/tauri.conf.json`](../../../../src-tauri/tauri.conf.json)：无 decorations/shadow、透明 backgroundColor、非 taskbar/非 focusable 海报窗口。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：非客户区 style 不变量、DWM 抑制、桌面层挂载、完整视觉区与独立输入命中、无通用 TOPMOST 恢复。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js)：展开/收起目标包络、视觉区与交互区注册。
- [`app/poster.html`](../../../../app/poster.html) 与 [`app/src/poster-surface.css`](../../../../app/src/poster-surface.css)：透明 DOM 根和海报视觉表面。

## 状态与恢复

- [`app/src/poster-state-store.js`](../../../../app/src/poster-state-store.js)：SQLite-first 正式状态网关、默认创建和 CAS 更新。
- [`src-tauri/src/poster_state_store.rs`](../../../../src-tauri/src/poster_state_store.rs)：版本化 SQLite、revision/CAS、迁移和失败保留。
- [`src-tauri/src/placement.rs`](../../../../src-tauri/src/placement.rs)：显示器归一化位置与绝对坐标回退。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：主工作区解析、正式快照投影、宿主同步和恢复协调。

## 自动化与人工证据

- [`tests/poster-geometry.test.mjs`](../../../../tests/poster-geometry.test.mjs)：JS 默认几何与收起包络矩阵。
- [`tests/poster-contract.test.mjs`](../../../../tests/poster-contract.test.mjs)：正式默认快照和迁移几何。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：透明窗口、单表面、命中、无标题栏、无 TOPMOST 和恢复源码契约。
- [`tests/poster-state-store.test.mjs`](../../../../tests/poster-state-store.test.mjs)：正式状态重载、CAS 和恢复边界。
- [`../20_Log/2026-08/2026-08-14_clst_absorption_audit.md`](../20_Log/2026-08/2026-08-14_clst_absorption_audit.md)：C-LST 最终 PASS/GAP/DEFERRED 证据矩阵。

# Collection Item Model and Projection 预备代码锚点

## 设计输入

- [`Docs/A_Project/10_Plan/SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md`](../../../A_Project/10_Plan/SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md)：Item、发现、仅添加、安全收纳与恢复的阶段边界。
- [`Docs/A_Project/10_Plan/POSTER_DATA_MODEL_V2.md`](../../../A_Project/10_Plan/POSTER_DATA_MODEL_V2.md)：`CollectionItem`/`PosterItem` 候选字段和关系；激活时必须复核，不能直接当作已冻结 schema。
- [`Docs/A_Project/10_Plan/POSTER_ERROR_AND_EMPTY_STATE_V0.1.md`](../../../A_Project/10_Plan/POSTER_ERROR_AND_EMPTY_STATE_V0.1.md)：空集合、失效图标和损坏状态设计输入。

## 预计接入边界

- [`src-tauri/src/poster_state_store.rs`](../../../../src-tauri/src/poster_state_store.rs)：现有 SQLite/revision/CAS 实现模式；C-CIM 应复用策略但不得把 Item 强塞进海报单例 JSON。
- [`app/src/poster-state-store.js`](../../../../app/src/poster-state-store.js)：现有正式状态网关模式。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：当前海报卡片渲染与待替换的演示数据消费点。
- [`app/src/model.js`](../../../../app/src/model.js)：当前 `GAME_CATALOG` 演示目录和控制中心模型输入。

本文件仅登记预备锚点。C-CIM 激活后必须先确认实际源码位置，再建立正式代码与测试锚点。

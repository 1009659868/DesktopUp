# Poster Contract Baseline 代码锚点

## 当前持久快照桥

- [`app/src/poster-legacy-bridge.js`](../../../../app/src/poster-legacy-bridge.js)：A0 localStorage、过渡 JSON 命令、两海报演示默认值和第一海报投影辅助函数的唯一前端入口；只能作为迁移输入。
- [`app/src/poster-core.js`](../../../../app/src/poster-core.js)：当前海报标记渲染与 HTML 转义；不再拥有 A0 存储或默认数据。
- [`app/src/model.js`](../../../../app/src/model.js)：A0 `theme/collapsed/locked` 归一化逻辑；不是正式 C-PCB schema。
- [`src-tauri/src/poster_snapshot.rs`](../../../../src-tauri/src/poster_snapshot.rs)：正式 v1 严格校验与无版本 A0 legacy 分流；正式 SQLite 仓库仍归 C-PSS。
- [`tests/poster-legacy-bridge.test.mjs`](../../../../tests/poster-legacy-bridge.test.mjs)：legacy/formal/非法分类、迁移优先级、正式快照不读不写和第一海报投影证据。

## C-PCB 契约

- [`app/src/poster-contract.js`](../../../../app/src/poster-contract.js)：版本、枚举、默认快照、严格校验、显式 legacy adapter 与旧模板解析兼容包装。
- [`src-tauri/src/poster_contract.rs`](../../../../src-tauri/src/poster_contract.rs)：Rust 同构类型、严格裁决与结构化错误。
- [`tests/poster-contract.test.mjs`](../../../../tests/poster-contract.test.mjs)：默认、拒绝、回退与不持久化行为证据。
- [`tests/fixtures/poster-contract-v1.json`](../../../../tests/fixtures/poster-contract-v1.json)：JS/Rust 共用合法/非法判定矩阵。

## C-PCB 封板与 C-PSS 输入

- [`tests/poster-contract-seal.test.mjs`](../../../../tests/poster-contract-seal.test.mjs)：公共字段白名单冻结、legacy 唯一所有者、canonical 宿主调用和默认/fallback 常量唯一性。
- [`C_PSS_INPUT_CONTRACT_V1.md`](../10_Plan/C_PSS_INPUT_CONTRACT_V1.md)：C-PSS 可以依赖的 schema、默认创建、错误、legacy 和宿主边界；不激活 SQLite 实施。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：Release 回归维护证据；整组枚举 Tauri 外壳与可见内容 ToolWindow 并锚定到桌面层。该文件不是 C-PCB 公共契约依赖。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：有限启动收敛、整组表面筛选和 canonical 运行时路径的静态回归保护。

## C-PCB 几何

- [`app/src/poster-geometry.js`](../../../../app/src/poster-geometry.js)：主工作区到默认逻辑矩形、宿主包络、初始锚点与 `preserve` 呈现变更的 JS 权威实现。
- [`src-tauri/src/poster_geometry.rs`](../../../../src-tauri/src/poster_geometry.rs)：Rust 公共同构几何契约；不访问 Windows 私有 API。
- [`tests/poster-geometry.test.mjs`](../../../../tests/poster-geometry.test.mjs)：产品常量、共用矩阵与收起/展开 top-left 不变量。
- [`tests/fixtures/poster-geometry-v1.json`](../../../../tests/fixtures/poster-geometry-v1.json)：JS/Rust 共用的 1366×768、1080p、4K 和非法工作区判定矩阵。

## C-PCB 标题栏与权限

- [`app/src/poster-permissions.js`](../../../../app/src/poster-permissions.js)：标题栏角色、拖动排除项和跨呈现/交互/锁定/运行时权限的 JS 权威解析。
- [`src-tauri/src/poster_permissions.rs`](../../../../src-tauri/src/poster_permissions.rs)：Rust 公共同构权限契约与结构化错误。
- [`tests/poster-permissions.test.mjs`](../../../../tests/poster-permissions.test.mjs)：锁定、显式启动、编辑能力、暂停覆盖和非法输入证据。
- [`tests/fixtures/poster-permissions-v1.json`](../../../../tests/fixtures/poster-permissions-v1.json)：JS/Rust 共用的权限组合与非法枚举判定矩阵。

## C-PCB 模板解析与失败回退

- [`app/src/poster-template-resolution.js`](../../../../app/src/poster-template-resolution.js)：正常默认、用户请求模板与内部临时 fallback 的 JS 权威解析器。
- [`src-tauri/src/poster_template_resolution.rs`](../../../../src-tauri/src/poster_template_resolution.rs)：Rust 同构解析结果、结构化错误与唯一 fallback 常量源。
- [`tests/poster-template-resolution.test.mjs`](../../../../tests/poster-template-resolution.test.mjs)：默认/fallback 分离、无写回、快照失败和 fallback 自身失败证据。
- [`tests/fixtures/poster-template-resolution-v1.json`](../../../../tests/fixtures/poster-template-resolution-v1.json)：JS/Rust 共用的 16 场景模板失败判定矩阵。

## 稳定宿主输入

- [`app/src/host-contract.js`](../../../../app/src/host-contract.js)：前端 `host-contract-v1` 边界。
- [`src-tauri/src/host_contract.rs`](../../../../src-tauri/src/host_contract.rs)：原生同构宿主契约。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：当前单海报表面投影；后续只能通过 canonical `sync_host_surface` 提交。
- [`src-tauri/src/runtime_lifecycle.rs`](../../../../src-tauri/src/runtime_lifecycle.rs)：运行态权威源；运行态不得复制进 Poster 持久实体。

## 禁止依赖

C-PCB 不直接访问 WorkerW、Progman、HWND、DPI API 或低级输入钩子；也不新增 `resize_poster_surface`、`set_poster_interaction_region`、`runtime-pause-state` 兼容调用。

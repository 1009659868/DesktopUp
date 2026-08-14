# Poster State and Interaction 代码锚点

## 已有输入

- [`C-PSI 审计矩阵 V1`](../10_Plan/C_PSI_AUDIT_MATRIX_V1.md)：PASS、真实缺口、下游边界和剩余 owner 的冻结裁决。

- [`C-PCB-03 权限契约日志`](../../PosterContractBaseline/20_Log/2026-08/2026-08-11_cpcb03_permission_contract_and_cpcb04_activation.md)：标题栏与状态权限冻结输入。
- [`app/src/poster-permissions.js`](../../../../app/src/poster-permissions.js) 与 [`src-tauri/src/poster_permissions.rs`](../../../../src-tauri/src/poster_permissions.rs)：现有 JS/Rust 纯权限解析；C-PSI-02 负责接入真实 UI 并补暂停原因分流。
- [`app/src/poster-contract.js`](../../../../app/src/poster-contract.js) 与 [`src-tauri/src/poster_contract.rs`](../../../../src-tauri/src/poster_contract.rs)：正式海报状态契约。
- [`app/src/poster-state-store.js`](../../../../app/src/poster-state-store.js)：业务状态 CAS 权威入口。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js)：表面包络、命中区与宿主同步。
- [`app/src/poster-editor-geometry.js`](../../../../app/src/poster-editor-geometry.js)：控制中心预览投影、移动/缩放逆投影与工作区约束。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：原生桌面层拖动、命中和窗口几何应用。
- [`src-tauri/src/runtime_lifecycle.rs`](../../../../src-tauri/src/runtime_lifecycle.rs)：暂停、显隐和退出运行时权威。

## 预备验证

- [`tests/poster-permissions.test.mjs`](../../../../tests/poster-permissions.test.mjs)
- [`tests/poster-editor-geometry.test.mjs`](../../../../tests/poster-editor-geometry.test.mjs)
- [`tests/poster-editor-surface.test.mjs`](../../../../tests/poster-editor-surface.test.mjs)
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)
- [`tests/poster-state-store.test.mjs`](../../../../tests/poster-state-store.test.mjs)

## 已确认真实缺口锚点

- [`app/src/main.js`](../../../../app/src/main.js)：控制中心仍使用零散 edit/lock 判断，缺少 pending mutation overlay。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：原生标题文字展开、5px 拖动阈值、权限接入和海报菜单入口尚未实现。
- [`app/src/poster-core.js`](../../../../app/src/poster-core.js)：demo 缩放手柄尚未由权限结果控制。
- [`app/src/poster-editor-geometry.js`](../../../../app/src/poster-editor-geometry.js)：尚未执行工作区 45% 最大宽、安全最大高和右下角固定约束。
- [`app/src/poster-state-store.js`](../../../../app/src/poster-state-store.js)：CAS 仓库已通过；C-PSI-02 只在 UI 层补本地 pending mutation 保留，不重写仓库。

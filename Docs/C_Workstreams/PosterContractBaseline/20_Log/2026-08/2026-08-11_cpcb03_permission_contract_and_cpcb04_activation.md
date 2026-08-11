# C-PCB-03 权限契约与 C-PCB-04 激活

日期：2026-08-11

状态：`C-PCB-03 COMPLETE / C-PCB-04 ACTIVE`

## 设计与 A0 审计

- 权威设计要求自制 DOM 标题栏同行包含标题、模板标识、项目数量、呈现切换和锁定切换；当前 A0 标题与数量仍在第二行，只能作为迁移证据，不能提升为目标结构。
- 当前 Release 已验证未锁定标题栏拖动、锁定阻止拖动、锁定/收起按钮和卡片命中；这些事实与目标权限一致，但不代表 Shadow DOM 标题栏或完整编辑态已经实现。
- 设计文档一处概括“EDITING 不启动”，交互映射又明确双击/Enter/菜单可以启动。本任务将其裁决为：编辑态单击只选中，明确启动意图仍允许。
- 锁定海报在全局 EDITING 中使用 USING 权限；锁定只禁止位置、尺寸、锚点和排序变更，不禁止解锁、收起/展开、上下文菜单或项目启动。

## 冻结契约

- 标题栏拖动起点：背景、标题、模板标识和项目数量；拖动排除项：呈现/锁定控件、按钮、链接、输入控件和项目卡片。
- 单击与拖动由 `5px` 指针阈值区分；COLLAPSED 标题文字可以在未达到拖动阈值时触发展开。
- 未锁定 active 海报在 USING/EDITING、EXPANDED/COLLAPSED 下均可从标题栏拖动；仅未锁定 `EDITING + EXPANDED` 可从内容区拖动并调整尺寸。
- 项目仅在 EXPANDED 可操作：USING 单击启动；EDITING 单击选中、明确启动仍可用；排序和尺寸变更仅 `EDITING + EXPANDED + unlocked` 可用。
- `ACTIVE_STATIC` 与 `ACTIVE_ANIMATED` 输入权限相同。`PAUSED` 与 `STOPPED` 把有效交互态降为 USING，并拒绝全部用户动作。
- 编辑态不运行 auto-collapse；锁定的 EDITING 海报因有效交互态为 USING，继续服从其持久化 auto-collapse 设置。

## 实施

- 新增 `app/src/poster-permissions.js`：严格输入校验、标题栏角色常量和纯权限解析。
- 新增公开 Rust 同构模块 `src-tauri/src/poster_permissions.rs`。
- 新增 `tests/fixtures/poster-permissions-v1.json`：覆盖 8 个 active 基础组合、activeAnimated、PAUSED、STOPPED 与非法枚举。
- 新增 `tests/poster-permissions.test.mjs`：冻结标题栏结构、锁定降级、单击/明确启动分流、编辑能力和暂停 fail-closed。
- `package.json` 全量检查入口已包含新模块语法检查。

## 验证

- C-PCB-03 JS 专项：5/5 PASS。
- `pnpm run check`：77/77 PASS；110 个 Markdown 文件链接有效。
- `cargo test --manifest-path src-tauri/Cargo.toml`：63/63 PASS。
- Rust 仅保留既有 MSVC import-library linker message，没有新增编译告警。

## 边界

- 新权限契约尚未接管现有 A0/Release 事件监听，因此本切片没有改变海报视觉、桌面穿透、卡片命中、拖动、锁定或收起/展开，不需要新增 Release 人工验收。
- 本任务没有实现右键菜单、真实项目启动、图标排序、Shadow DOM、模板安装或多海报 UI；这些能力分别属于后续 C-PSI、C-LCH、C-LAR、C-SRF/C-TPS。

## 下一步

C-PCB-04 冻结正常 `transparent-default`、用户请求模板和内部临时 `minimal-fallback` 的失败矩阵。模板缺失、损坏、快照无效和恢复失败必须给出结构化原因；fallback 不写回用户 `templateKey`，也不静默覆盖原始选择。

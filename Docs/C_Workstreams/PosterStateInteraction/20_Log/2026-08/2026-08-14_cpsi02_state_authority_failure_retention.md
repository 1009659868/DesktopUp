# C-PSI-02 状态权威与失败保留

日期：2026-08-14

状态：`COMPLETE — C-PSI-03 ACTIVE`

## 1. 完成内容

1. 在 `poster-runtime.js` 增加运行时权限适配层，继续以冻结的 `poster-permissions-v1` 纯函数作为唯一权限裁决：
   - 手动“暂停动态效果”只停止动态绘制，海报输入按 `activeStatic` 保持可用；
   - 自动暂停、页面不可见、海报隐藏、宿主未就绪、退出和显式停止均 fail closed；
   - locked + EDITING 仍降级为 USING，但保留解锁、收起/展开及合法 Item 明确启动入口。
2. 控制中心与真实海报表面已消费同一权限结果。点击、双击、标题栏拖动、缩放、锁定和收起/展开不再各自维护零散布尔判断。
3. 原生海报输入区域随运行时权限重新同步：系统级不可用时提交零交互区域；恢复或仅手动暂停动态效果时重建合法标题栏与 Item 区域。
4. `PosterStateClient` 增加正式字段级 pending/retained mutation overlay：
   - SQLite 已接受快照仍是唯一正式权威；overlay 只保留尚未完成或保存失败的本地正式字段，不成为第二份持久化状态；
   - CAS 冲突重载后，只把本地实际修改字段叠加到最新权威快照，因此不会覆盖并发更新的其他正式字段；
   - 较旧 revision 和同 revision 不同内容的事件均被拒绝；
   - 无正式字段变化时不发起原生写入；调用/写入失败时保留最后接受快照，并继续显示本地正式编辑以避免 UI 回弹。
5. 两端暴露 `pending`、`rejected-retained` 与已接受状态诊断，权威事件到达时均通过 overlay 重建运行时投影。

## 2. 自动化证据

- `node --test tests/poster-runtime.test.mjs tests/poster-state-store.test.mjs tests/poster-editor-surface.test.mjs tests/runtime-surface.test.mjs`：`48/48 PASS`；
- `pnpm run check`：`163/163 PASS`，JavaScript 语法与 Markdown 文档链接检查通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：`132/132 PASS`；
- `pnpm run build`：通过，静态产物生成到 `dist/`；
- `git diff --check`：通过。

## 3. 边界

- 未修改 SQLite schema、Rust 状态仓库、legacy 只读迁移来源或主屏单海报发布范围；
- 未实现 C-PSI-03 的动态尺寸约束、内容区拖动或非法编辑组合收口；
- 未实现 C-PSI-04 的标题文字展开、5px 拖动阈值或海报右键菜单；
- 未引入正式 Item 数据、真实启动、排序或 Item 菜单命令，这些仍由 C-CIM/C-LCH/C-LAR 拥有；
- 控制中心 legacy `accent/opacity/density` 回弹仍归后继 Template Platform 参数权威，不纳入本任务。

## 4. 结论与交接

C-PSI 审计矩阵中的 GAP-01..03 已关闭。状态权威仍为 SQLite + expected-revision/CAS，UI 在 pending、冲突重载和写入失败期间不会被旧快照覆盖；渲染暂停与输入不可用也已分流。

唯一下一步为 `C-PSI-03：移动、缩放与尺寸边界`，只处理 GAP-04..06。

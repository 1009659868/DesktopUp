# C-DRP-01 收起尺寸同步缺陷修复

日期：2026-08-09

状态：`FIX VERIFIED`

## 现象

海报业务状态能够切换为 `collapsed = true`，内容也会隐藏，但原生宿主仍保持展开高度。结果是收起后留下大块透明/玻璃背景，标题被 CSS 网格推到旧窗口底部。

## 根因

- 收起后的目标宿主逻辑高度为 `116px + 2 × 10px gutter = 136px`；
- 原生表面 CSS 使用 `height: 100%`，宿主缩小前 DOM 仍测得展开高度 `392px`；
- `buildHostSurfaceSyncArgs` 把这个旧测量值作为 interaction region 高度写进只有 `136px` 高的目标宿主；
- JS 契约校验以 `interactionRegion.rect.height must be ... 136` 拒绝请求，IPC 尚未发生，原生窗口因此保留旧高度；
- 参数构造发生在原生调用的异常边界之外，形成未处理的异步拒绝。

## 修复

- `buildHostSurfaceSyncArgs` 继续从 DOM 读取表面偏移，但 interaction region 的宽高改为业务目标尺寸：展开为海报 `width/height`，收起为 `width/116px`；
- 原生宿主逻辑尺寸与 interaction region 现在来自同一目标状态，不再依赖旧宿主先完成 resize；
- `poster-surface.js` 显式提交 `collapsedHeight = 116`；
- `syncNativeSurface` 覆盖参数构造和 IPC 的完整异常边界，失败时显示可恢复提示，不再产生未处理拒绝；
- 增加“旧展开测量 → 收起目标”和“收起 → 展开恢复”契约回归测试。

## 验证

- `pnpm run check`：39 项 Node/契约测试通过；
- `pnpm run build`：通过；
- `cargo fmt --manifest-path src-tauri/Cargo.toml --check`：通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：24 项 Rust 测试通过；
- `cargo tauri build --no-bundle`：Release 重建通过；
- Release PID `53700` 冷启动读取持久化收起状态后 `Responding=True`；
- Win32 探针确认收起宿主为 `612×163`，与 `510×136 @1.20x` 一致；
- `GetWindowRgnBox` 为 `588×139`、原点 `(12,12)`，与 `490×116` interaction region、10px gutter 和 1.20x DPI 一致；
- 自动化回归确认展开请求恢复 `510×412` 逻辑宿主和 `490×392` interaction region。

## 后继

C-DRP-01 保持 `COMPLETE`，当前主任务仍为 C-DRP-02。后继单全局宿主拓扑必须继续以目标逻辑尺寸构造原生请求，不得重新从 resize 前的 WebView 尺寸推导业务表面大小。

---
name: desktopup-architecture-boundaries
description: Critical architecture boundaries that caused repeated bugs when violated
metadata:
  type: project
---

DesktopUp 项目反复出现画面无法渲染（404）、Windows 原生 title 栏持续可见、启动死锁等问题的根因并非技术复杂度，而是之前的代码修改没有识别正确的基线分支和修改边界。

## 关键错误模式与边界清单

### 1. 分支基线混淆 —— 导致 404 / 渲染空白

之前"审计修复"在 `impl/poster-redesign-a0` 分支上工作，但代码基线实际来自 `impl/desktop-runtime-foundation-a0`（A0 原型分支）。当 V1 分支 `impl/poster-redesign-V1` 建立时，作者做了一个 `chore: reset poster redesign V1 baseline`，将 V1 重置回了 A0 原型代码。（关键：重置后的 V1 代码中 `lib.rs` 已经没有 `webview2` 和 `template_loader` 模块声明、没有 `get_template_bundle` 和 `list_templates` 这两个 Tauri 命令注册、没有 `clear_compositor_background` 调用。）

这意味着 V1 选择的是"从 A0 最简起点重新演进"——不把 M1 修复分支的所有代码直接合并过来。在 V1 上工作时引用其他分支的代码（如 `webview2.rs`、`template_loader.rs`、`poster-surface.js` 的 template 版本）是**无效操作**——这些文件在当前基线中根本不存在。

**正确边界**：每次修改前先确认当前基线是什么。V1 的 poster-surface.js 是 A0 时代的 228 行版本（`innerHTML` 直接渲染海报 HTML，无模板引擎），lib.rs 的 `resize_poster_surface` 尺寸范围仍是 A0 的 `[330,640]×[240,560]`。

### 2. poster.html 的 CSS 加载链 —— 导致游戏卡片不可见

`poster.html` 的 CSS 引用是全局唯一的样式来源。A0 基线加载了两个文件：
- `styles.css`（971行）— 游戏卡片 `.game-tile/.game-cover/.game-name/.game-grid/.hue-*` 等所有样式
- `poster-surface.css` — 仅透明背景和少量补充样式

如果移除 `styles.css` 而不把游戏卡片样式迁移到 `poster-surface.css`，所有游戏卡片完全不可见。必须遵守：**poster.html 加载两者、或者把缺失的样式迁移到仍然加载的文件中**。

### 3. DWM 框架抑制时机 —— 导致标题栏反复出现

Windows 11 的 DWM 在窗口首次绘制时根据窗口 flag 渲染标题栏和边框。如果 DWM 抑制（`DWMNCRP_DISABLED` + `DWMWA_BORDER_COLOR`）只在 `PageLoadEvent::Finished` 后的 `configure()` 中调用，则窗口首帧可能已经渲染了标题栏。

必须在 `setup()` 闭包中**立即**调用抑制，不等待页面加载完成。

### 4. 状态写入保护 —— 导致启动死锁和位置污染

启动时 Tauri 的初始窗体位置会产生 `WindowEvent::Moved`，如果 placement 恢复之前就允许写入，会覆盖已保存的位置。修复需要三层保护：
- `POSTER_PLACEMENT_CAPTURE_ENABLED`：原子 bool，启动/configure 期间禁止写回
- `PLACEMENT_RECOVERY_ACTIVE`：原子 bool，Shell 恢复期间禁止写回
- `PLACEMENT_RECOVERY_GENERATION`：原子计数器，确保事件稳定

### 5. 模板/非模板分支的 JS 差异

V1 的 `poster-surface.js`（228行）和 V1-fix 分支的模板版本（560行）是**两个完全不同的代码**。V1 版本直接在 `render()` 中用 `innerHTML` 构建整个海报 DOM（含 lock/collapse 按钮、游戏卡片网格）。V1-fix 版本改用模板引擎加载 HTML 文件并在运行时注入游戏卡片。

**修改 poster-surface.js 前务必确认当前分支使用的是哪个架构版本。**

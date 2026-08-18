# Desktop-Up 海报错误降级与空状态设计 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 概述

本文档覆盖 GAP-09（错误降级）和 GAP-10（空状态与首次使用）。定义了海报在异常情况和空数据场景下应呈现的视觉和交互行为。跨文档默认值与关键决策以 [`DESKTOPUP_V1_DESIGN_BASELINE.md`](DESKTOPUP_V1_DESIGN_BASELINE.md) 为准。

---

## 2. 错误降级

`minimal-fallback` 是宿主级安全回退，不是用户正常创建海报时使用的默认模板；正常默认模板为 `transparent-default`（暂名）。进入 fallback 不得覆盖用户保存的模板选择。

### 2.1 模板 CSS 加载/解析失败

**触发条件**：模板 `template.css` 文件缺失、包含语法错误、或被安全扫描拦截超过阈值。

**降级行为**：
- 跳过所有模板 CSS 规则
- 应用内置 `minimal-fallback` 样式——纯黑半透明背景 `rgba(0,0,0,0.15)`、白色文字、1px 白色边框、12px 圆角、等大图标网格
- 标题条显示海报标题 + 模板名后加 `[!]` 警告标记
- 控制中心该模板卡片显示橙色警告条："模板样式加载异常，已使用回退样式"

**恢复**：下次模板加载时重试（非持久降级）。

### 2.2 模板 HTML 损坏

**触发条件**：`template.html` 缺失或解析后无有效 `data-slot` 元素。

**降级行为**：
- 使用 `minimal-fallback` HTML 结构：
  ```html
  <div class="poster tpl-minimal-fallback">
    <header class="tpl-title-area" data-slot="title">
      <h1>{{poster.title}}</h1>
    </header>
    <main class="tpl-icons-area" data-slot="icons">
      <div id="poster-icon-container"></div>
    </main>
  </div>
  ```
- 标题条警告标记同上

### 2.3 游戏图标缺失

**触发条件**：`CollectionItem.icon_source` 指向的文件不存在或无法加载。

**降级行为**：
- 显示占位图标：48×48px 圆角方块，背景色 `var(--tpl-accent-dim)` 不透明度 20%，居中对齐游戏名首字母（大写）
- 占位图 CSS：
  ```css
  .tpl-item-icon-fallback {
    display: flex;
    align-items: center;
    justify-content: center;
    background: color-mix(in srgb, var(--tpl-accent-dim) 20%, transparent);
    font-size: 24px;
    font-weight: 700;
    color: var(--tpl-text-secondary);
  }
  ```
- 控制中心该游戏显示警告标记

### 2.4 海报数据损坏

**触发条件**：`Poster` 记录中关键字段（`id`、`theme_instance_id`、`collapsed_state`）缺失或类型错误。

**降级行为**：
- 尝试从有效字段重建海报
- 无法重建时：显示 `minimal-fallback` 空海报 + 标题"海报数据异常"
- 托盘 tooltip 显示"需要处理"
- 控制中心显示红色错误卡片："海报数据损坏 · 点击重置"

**恢复**：用户可通过控制中心"重置海报"恢复为默认配置。重置保留 `CollectionItem` 关联但清除定制（模板、参数、排序、锚定）。

### 2.5 WebView 渲染异常

**触发条件**：WebView2 进程崩溃、OOM、或 JavaScript 未捕获异常导致渲染停止。

**降级行为**：
- Rust 侧检测 WebView 进程状态
- 第一次崩溃：自动重建 WebView，重新加载海报（静默恢复，不通知用户）
- 24 小时内第二次崩溃：重建 + 托盘通知"桌面海报已自动恢复"
- 24 小时内第三次崩溃：不再自动重建，托盘显示"桌面海报渲染异常 · 点击重新连接"
- 用户点击"重新连接桌面"或重启应用后重置计数器

### 2.6 降级优先级链

```text
完整模板渲染
  ↓ CSS/HTML 异常
minimal-fallback 模板
  ↓ 海报数据损坏
空海报 + 错误提示
  ↓ WebView 崩溃
重建 + 托盘通知
  ↓ 连续崩溃
手动重连
```

---

## 3. 空状态设计

### 3.1 零游戏海报（COLLAPSED）

```
┌─ AFTERLIGHT  ·  0 GAMES ─── [展开▼] ─┐
```

- 显示"0 GAMES"而非空白——告知用户这里是空的
- 展开按钮仍然可用

### 3.2 零游戏海报（EXPANDED）

```
┌─ AFTERLIGHT ─────────────────── [收起▲] ─┐
│                                            │
│                                            │
│         ┌──────────────────┐               │
│         │                  │               │
│         │   🎮 + 添加游戏   │               │
│         │                  │               │
│         │  拖入快捷方式      │               │
│         │  或扫描桌面        │               │
│         │                  │               │
│         └──────────────────┘               │
│                                            │
│        [扫描桌面]  [打开控制中心]           │
│                                            │
└────────────────────────────────────────────┘
```

- 中央引导卡片：模板强调色边框，包含游戏手柄图标
- 引导文案："拖入快捷方式或扫描桌面"
- 两个操作按钮：`[扫描桌面]`（打开导入扫描）和 `[打开控制中心]`
- 引导卡是非侵入式的——不是模态框，不影响桌面使用
- 首次添加游戏后引导卡消失，永不再显示（`poster.has_seen_empty_guide: bool`）

### 3.3 启动数据未积累（新海报 / 新模板）

当游戏有零启动次数时（`total_launches = 0, days_since_added = 0`）：

- 图标大小 = `default_scale`（控制中心可调，默认 150%）
  - 注意：`min_scale`（50%）只用于衰减后的长期不玩游戏，新游戏不应该一上来就 50%
  - `default_scale` 是"没有历史数据时的初始缩放"
- 排名中 `recency_norm = 0`（从未启动过）
- 排名中 `newness_norm ≈ 1.0`（刚添加）
- 排名中 `frequency_norm = 0`（零启动 → `log(1+0) = 0`）
- 综合 `rank_score = 0.40×0 + 0.35×0 + 0.25×1.0 = 0.25`

这意味着新添加但从未启动的游戏排在海报中段——在真正活跃的游戏之下，但在衰减后垫底的游戏之上。合理。

---

## 4. 占位图规格

### 4.1 游戏图标占位图

```css
.tpl-item-icon-placeholder {
  width: var(--tpl-icon-base-size, 64px);
  height: var(--tpl-icon-base-size, 64px);
  border-radius: 8px;
  background: color-mix(in srgb, var(--tpl-accent-dim) 15%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
}
.tpl-item-icon-placeholder::after {
  content: attr(data-initial);        /* 游戏名首字母 */
  font-size: 24px;
  font-weight: 700;
  color: var(--tpl-text-secondary);
  font-family: var(--tpl-font-family);
}
```

### 4.2 模板缩略图缺失

`PosterTemplate.preview_thumbnail_path` 为 null 或文件不存在时，控制中心模板选择器显示一个自动生成的缩略图：模板背景色 + 模板名居中显示。

---

## 5. 首次启动引导

这里的"首次启动"指的是新用户的**首次使用流程**（来自 `PRODUCT_DESIGN_V0.1` §6），与海报重构的关联：

1. 安装完成 → 在主显示器右侧创建默认海报（Transparent Default 暂名，空游戏，unlocked）→ 以 EXPANDED 态显示
2. 海报显示空状态引导（§3.2）
3. 用户拖入第一个快捷方式 → 引导消失 → 游戏以 `default_scale`（150%）显示
4. 首次启动完成后，`is_first_launch = false`，后续冷启动以 COLLAPSED 或 EXPANDED 取决于上次状态

**首次启动不显示空状态引导的场景**：
- 用户从备份导入了数据 → 海报已有游戏 → 不显示引导
- 用户选择了"导入主题"作为首次操作 → 应用主题后显示该模板的空状态

---

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立错误降级（CSS/HTML/图标/数据/WebView）+ 空状态设计 + 首次引导 |
| 2026-08-04 | V1 对齐 | 区分 Transparent Default 正常默认模板与 minimal-fallback 安全回退，并同步默认右侧/解锁/展开 |

# Desktop-Up 海报视觉设计规格 V0.1 —— Cinema Lightbox

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 设计方向

电影灯箱（Lightbox）风格——想象电影院走廊里发光的电影海报框，胶片齿孔边框、柔和暖白轮廓光、衬线标题字。适合浅色或暖色壁纸用户，与 Neon HUD 的冷峻形成互补。它是 V1 首发可替换主题之一；新海报默认使用低焦点的 `transparent-default`，本模板不承担默认模板或安全 fallback 职责。

## 2. 色板

### 2.1 主色调

| 角色 | CSS 变量 | 值 | 用途 |
|------|----------|-----|------|
| 强调色（暖白） | `--tpl-accent` | `#f0e6d3` | 边框发光、标题色 |
| 强调色-暗 | `--tpl-accent-dim` | `#8a7e6b` | 非活跃边框、分割线 |
| 强调色-辉光 | `--tpl-accent-glow` | `#f0e6d380` | box-shadow 暖白辉光 |

### 2.2 表面色

| 角色 | CSS 变量 | 值 | 用途 |
|------|----------|-----|------|
| 背景暗度 | `--tpl-bg-darkness` | `0.08` | 比 Neon HUD 更亮（`0.08` vs `0.10`） |
| 表面色 | `--tpl-surface` | `rgba(20, 18, 14, 0.80)` | 标题条和弹出菜单 —— 暖黑 |
| 表面色-悬浮 | `--tpl-surface-hover` | `rgba(32, 28, 20, 0.88)` | 图标悬浮 —— 暖黑提亮 |

### 2.3 文字色

| 角色 | CSS 变量 | 值 | 用途 |
|------|----------|-----|------|
| 主文字 | `--tpl-text-primary` | `#f5f0e8` | 标题、游戏名 —— 暖白 |
| 次文字 | `--tpl-text-secondary` | `#9a9284` | 游戏次数、副标题 —— 暖灰 |
| 强调文字 | `--tpl-text-accent` | `#f0e6d3` | 高亮数值 |

### 2.4 预设配色方案

| 预设 ID | 强调色 | 名称 | 感觉 |
|----------|--------|------|------|
| `lightbox-warm` | `#f0e6d3` | 暖白灯箱 | 默认 —— 经典电影院暖光 |
| `lightbox-cool` | `#d0dce8` | 冷白展厅 | 现代画廊 / 美术馆冷光 |
| `lightbox-gold` | `#d4a853` | 金色大厅 | 颁奖典礼 / 红毯金 |

## 3. 排版

### 3.1 字体族

```css
--tpl-font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
--tpl-font-display: 'Georgia', 'Times New Roman', serif;
--tpl-font-mono: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
```

海报标题使用衬线体（Georgia），模拟电影标题排版。游戏名仍用无衬线体保证小字号可读性。

### 3.2 字号阶梯（与 Neon HUD 一致）

| 角色 | CSS 变量 | 默认值 | 可调范围 |
|------|----------|--------|----------|
| 海报标题 | `--tpl-title-size` | `32px` | 24–48px（默认比 Neon 大 4px，衬线体需要更大视觉体积） |
| 海报副标题 | `--tpl-subtitle-size` | `14px` | 12–18px |
| 游戏名称 | `--tpl-item-name-size` | `13px` | 11–16px |
| 启动次数 | `--tpl-item-stat-size` | `11px` | 10–13px |
| 状态标签 | `--tpl-label-size` | `10px` | 9–12px |

### 3.3 字重

| 角色 | 默认值 |
|------|--------|
| 海报标题 | `400`（衬线体不粗体，靠字号和字间距体现质感） |
| 游戏名称 | `600` |
| 启动次数 | `400` |

## 4. 间距与圆角

### 4.1 间距 Token（与 Neon HUD 一致）

| 角色 | CSS 变量 | 默认值 | 可调范围 |
|------|----------|--------|----------|
| 图标间距 | `--tpl-icon-gap` | `14px` | 8–32px（默认比 Neon 大 2px，灯箱需要呼吸感） |
| 海报内边距 | `--tpl-padding` | `20px` | 12–28px |
| 标题条内边距 | `--tpl-header-padding` | `14px 20px` | — |
| 区块间距 | `--tpl-section-gap` | `18px` | 12–24px |

### 4.2 圆角

| 角色 | CSS 变量 | 默认值 | 可调范围 |
|------|----------|--------|----------|
| 海报外框 | `--tpl-corner-radius` | `12px` | 0–36px（比 Neon 小，灯箱边缘更利落） |
| 图标卡片 | `--tpl-item-radius` | `8px` | 4–16px |

## 5. 边框与发光

### 5.1 胶片齿孔边框

这是 Cinema Lightbox 最具辨识度的元素——左右两侧垂直排列的圆点，模拟电影胶片齿孔：

```css
.tpl-film-perforations {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 6px;
  /* 垂直重复的齿孔圆点 */
  background:
    radial-gradient(
      circle 2px,
      var(--tpl-accent-dim) 30%,
      transparent 30%
    );
  background-size: 6px 12px;  /* 每 12px 一个齿孔 */
  opacity: calc(var(--tpl-border-strength, 30) / 100);
}

.tpl-film-perforations.left  { left: 3px; }
.tpl-film-perforations.right { right: 3px; }
```

### 5.2 柔和轮廓光

单层 box-shadow，没有 Neon HUD 的激进双层辉光：

```css
box-shadow:
  0 0 calc(var(--tpl-glow-spread, 16px) * var(--tpl-border-strength, 30) / 100)
      var(--tpl-accent-glow);
```

`--tpl-border-strength`：0–100%，默认 30。`--tpl-glow-spread`：8–24px 可调。

### 5.3 边框

```css
border: 1px solid color-mix(
  in srgb,
  var(--tpl-accent) calc(var(--tpl-border-strength, 30) * 1%),
  transparent
);
```

比 Neon HUD 更粗的视觉重量（通过辉光扩散更大来体现），但边框本身仍是 1px。

## 6. 背景处理

```css
.poster.tpl-cinema-lightbox {
  background:
    /* 暖暗色叠加 —— 偏棕，比 Neon 的纯黑更暖 */
    rgba(12, 10, 6, var(--tpl-bg-darkness, 0.08)),
    /* 柔和渐隐光晕 —— 模拟灯箱边缘漏光 */
    radial-gradient(
      ellipse at 50% 0%,
      rgba(240, 230, 211, calc(var(--tpl-border-strength, 30) / 1000)) 0%,
      transparent 70%
    );

  backdrop-filter: blur(var(--tpl-blur-radius, 32px));
  -webkit-backdrop-filter: blur(var(--tpl-blur-radius, 32px));
}
```

默认模糊半径 32px（比 Neon 的 24px 更大），灯箱感需要壁纸在背后柔化更多。

## 7. 自制标题栏

本节标题栏是模板 Shadow Root 内的自制 DOM，不是 Windows 原生窗口标题栏。海报标题/模板标识/项目数量与收起、锁定按钮保持同一行；未锁定时控件之外的标题栏空白区和非交互标题/标签文字可启动海报拖动，按钮、链接和输入控件必须排除。

### 7.1 展开态标题

```text
┌─ Cinema Lightbox ───────────── [收起▲] [锁◈] ─┐
│  AFTERLIGHT                                                  │
│  6 GAMES                                                     │
└──────────────────────────────────────────────────────────────┘
```

- 左侧：模板小图标 + 模板名；
- 中间：衬线体标题与小号无衬线游戏数，`letter-spacing: 0.04em`；
- 右侧：收起按钮 + 锁定/解锁按钮，USING 与 EDITING 均可见且不触发拖动。

### 7.2 收起态标题（60px）

```text
┌─ AFTERLIGHT  ·  6 GAMES ─── [展开▼] ─┐
```

- 显示海报标题（用户命名的）+ 游戏数 + 展开按钮 + 锁定/解锁按钮，保持同行

### 7.3 CSS

```css
.tpl-title-area {
  display: flex;
  flex-direction: column;  /* 与 Neon 的 row 不同——标题和统计上下排列 */
  gap: 4px;
  padding: var(--tpl-header-padding);
  border-bottom: 1px solid color-mix(in srgb, var(--tpl-accent-dim) 30%, transparent);
}

.tpl-title-text {
  font-family: var(--tpl-font-display);
  font-size: var(--tpl-title-size);
  font-weight: 400;
  color: var(--tpl-text-primary);
  letter-spacing: 0.04em;
}

.tpl-title-count {
  font-size: var(--tpl-subtitle-size);
  color: var(--tpl-text-secondary);
  font-family: var(--tpl-font-mono);
}
```

## 8. 游戏图标卡片

与 Neon HUD 结构相同，区别：

- 卡片背景更暖：`--tpl-surface` 偏棕而不是偏蓝
- 悬浮时辉光色温暖白而不是青蓝
- 图标卡片 `border-radius: 8px`（比 Neon 的 10px 略方）

```css
.tpl-item-card {
  background: var(--tpl-surface);
  border: 1px solid color-mix(in srgb, var(--tpl-accent-dim) 30%, transparent);
  border-radius: var(--tpl-item-radius);
}

.tpl-item-card:hover {
  border-color: var(--tpl-accent);
  box-shadow: 0 0 12px var(--tpl-accent-glow);
  background: var(--tpl-surface-hover);
}
```

## 9. 装饰元素

### 9.1 底部渐变光条

标题条下方一条水平渐变光条，模拟灯箱底部漏光：

```css
.tpl-lightbox-glow-bar {
  position: absolute;
  top: 0;
  left: calc(var(--tpl-corner-radius) + 12px);
  right: calc(var(--tpl-corner-radius) + 12px);
  height: 1px;
  background: linear-gradient(
    90deg,
    transparent 0%,
    var(--tpl-accent) 15%,
    var(--tpl-accent) 85%,
    transparent 100%
  );
  opacity: calc(var(--tpl-border-strength, 30) / 200);
}
```

### 9.2 角落装饰

四角不是 L 形角标，而是圆角区域内的小小发光点（3px × 3px），模拟灯箱框架的固定螺丝/铆钉：

```css
.tpl-corner-rivet {
  position: absolute;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--tpl-accent);
  opacity: calc(var(--tpl-border-strength, 30) / 150);
  box-shadow: 0 0 3px var(--tpl-accent-glow);
}
/* 左上 */ { top: 8px; left: 8px; }
/* 右上 */ { top: 8px; right: 8px; }
/* 左下 */ { bottom: 8px; left: 8px; }
/* 右下 */ { bottom: 8px; right: 8px; }
```

## 10. 响应式断点

与 Neon HUD 一致：

| 宽度范围 | 行为 |
|----------|------|
| ≥ 360px（标准 ~420px） | 完整：胶片齿孔 + 铆钉 + 光条 |
| 280–359px | 齿孔隐藏，光条保留，标题字号缩小至 24px |
| < 280px | 不适用 |

## 11. CSS 变量完整列表

```text
// === 颜色 ===
--tpl-accent            #f0e6d3    强调色（暖白）
--tpl-accent-dim        #8a7e6b    强调色暗
--tpl-accent-glow       #f0e6d380  辉光色
--tpl-bg-darkness       0.08       背景不透明度 (0.00–0.30)
--tpl-surface           rgba(20,18,14,0.80)
--tpl-surface-hover     rgba(32,28,20,0.88)
--tpl-text-primary      #f5f0e8
--tpl-text-secondary    #9a9284
--tpl-text-accent       #f0e6d3

// === 排版 ===
--tpl-font-family       'Segoe UI Variable', ...
--tpl-font-display      'Georgia', 'Times New Roman', serif
--tpl-font-mono         'Cascadia Code', ...
--tpl-title-size        32px
--tpl-subtitle-size     14px
--tpl-item-name-size    13px
--tpl-item-stat-size    11px
--tpl-label-size        10px

// === 间距 ===
--tpl-icon-gap          14px
--tpl-padding           20px
--tpl-header-padding    14px 20px
--tpl-section-gap       18px

// === 圆角 ===
--tpl-corner-radius     12px
--tpl-item-radius       8px

// === 边框与发光 ===
--tpl-border-strength   30          (0–100)
--tpl-glow-spread       16px        (8–24)

// === 背景 ===
--tpl-blur-radius       32px        (0–48)

// === 图标 ===
--tpl-icon-base-size    64px        (48–96)
```

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立 Cinema Lightbox 完整视觉规格：暖白色板、衬线标题、胶片齿孔、铆钉角标、柔光背景 |
| 2026-08-04 | V1 对齐 | 明确自制 DOM 标题栏、同行锁定/收起按钮及未锁定标题栏空白区拖动契约 |

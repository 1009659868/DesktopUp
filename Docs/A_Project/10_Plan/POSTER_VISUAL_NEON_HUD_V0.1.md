# Desktop-Up 海报视觉设计规格 V0.1 —— Neon HUD

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 设计方向

赛博朋克霓虹风格的 HUD（Heads-Up Display）海报。视觉关键词：霓虹发光、扫描线、数据终端、暗夜中浮现的信息。适合桌面深色壁纸用户。

背景阅读：[Cyberpunk UI design patterns](https://www.uxpin.com/studio/blog/cyberpunk-ui-design/)

## 2. 色板

### 2.1 主色调

| 角色 | CSS 变量 | 值 | 用途 |
|------|----------|-----|------|
| 强调色 | `--tpl-accent` | `#72e4ff` | 边框发光、高亮文字、选中态 |
| 强调色-暗 | `--tpl-accent-dim` | `#3a8fa8` | 非活跃边框、分割线 |
| 强调色-辉光 | `--tpl-accent-glow` | `#72e4ff80` | box-shadow 辉光层 |

### 2.2 表面色

| 角色 | CSS 变量 | 值 | 用途 |
|------|----------|-----|------|
| 背景暗度 | `--tpl-bg-darkness` | `0.10` | 覆盖在壁纸上的黑色不透明度（0.0–0.30 可调） |
| 表面色 | `--tpl-surface` | `rgba(8, 12, 18, 0.85)` | 标题条和弹出菜单背景 |
| 表面色-悬浮 | `--tpl-surface-hover` | `rgba(16, 24, 36, 0.90)` | 图标悬浮背景 |

### 2.3 文字色

| 角色 | CSS 变量 | 值 | 用途 |
|------|----------|-----|------|
| 主文字 | `--tpl-text-primary` | `#f4f6f8` | 标题、游戏名 |
| 次文字 | `--tpl-text-secondary` | `#8a949e` | 游戏次数、副标题、状态文字 |
| 强调文字 | `--tpl-text-accent` | `#72e4ff` | 高亮数值、选中游戏名 |

### 2.4 预设配色方案

| 预设 ID | 强调色 | 名称 | 感觉 |
|----------|--------|------|------|
| `neon-cyan` | `#72e4ff` | 霓虹青 | 默认 —— 经典赛博蓝，冷峻 |
| `neon-magenta` | `#ff4da6` | 霓虹洋红 | 合成波 / 80 年代复古 |
| `neon-amber` | `#ffb347` | 霓虹琥珀 | 温暖的系统终端 / 辐射风 |

## 3. 排版

### 3.1 字体族

```css
--tpl-font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
--tpl-font-mono: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
```

标题使用系统无衬线体（保持低资源原则，不引入 Web 字体）。等宽字体用于数据标签和启动次数显示。

### 3.2 字号阶梯

| 角色 | CSS 变量 | 默认值 | 可调范围 |
|------|----------|--------|----------|
| 海报标题 | `--tpl-title-size` | `28px` | 20–48px |
| 海报副标题 | `--tpl-subtitle-size` | `14px` | 12–18px |
| 游戏名称 | `--tpl-item-name-size` | `13px` | 11–16px |
| 启动次数 | `--tpl-item-stat-size` | `11px` | 10–13px |
| 状态标签 | `--tpl-label-size` | `10px` | 9–12px |

### 3.3 字重

| 角色 | 默认值 |
|------|--------|
| 海报标题 | `700` |
| 游戏名称 | `600` |
| 启动次数 | `400` |
| 状态标签 | `600`，`letter-spacing: 0.05em`，全大写 |

## 4. 间距与圆角

### 4.1 间距 Token

| 角色 | CSS 变量 | 默认值 | 可调范围 |
|------|----------|--------|----------|
| 图标间距 | `--tpl-icon-gap` | `12px` | 8–32px |
| 海报内边距 | `--tpl-padding` | `16px` | 12–24px |
| 标题条内边距 | `--tpl-header-padding` | `12px 16px` | — |
| 区块间距 | `--tpl-section-gap` | `16px` | 12–24px |

### 4.2 圆角

| 角色 | CSS 变量 | 默认值 | 可调范围 |
|------|----------|--------|----------|
| 海报外框 | `--tpl-corner-radius` | `18px` | 0–48px |
| 图标卡片 | `--tpl-item-radius` | `10px` | 4–20px |
| 标题条 | `--tpl-header-radius` | `18px 18px 0 0` | 跟随海报外框（仅上圆角） |

## 5. 边框与发光

### 5.1 主边框

```css
border: 1px solid var(--tpl-accent, #72e4ff);
```

边框强度由 `--tpl-neon-intensity`（0–100%）控制：

```css
border-color: color-mix(
  in srgb,
  var(--tpl-accent) calc(var(--tpl-neon-intensity, 30) * 1%),
  transparent
);
```

### 5.2 辉光

双层 box-shadow：

```css
box-shadow:
  /* 外辉光 */
  0 0 calc(var(--tpl-glow-spread, 12px) * var(--tpl-neon-intensity, 30) / 100)
      var(--tpl-accent-glow),
  /* 内辉光 —— 模拟 HUD 屏幕边缘发光 */
  inset 0 0 calc(var(--tpl-glow-spread, 12px) * var(--tpl-neon-intensity, 30) / 200)
      rgba(114, 228, 255, 0.12);
```

`--tpl-glow-spread`：8–24px 可调。

## 6. 背景处理

```css
.poster.tpl-neon-hud {
  background:
    /* 暗色叠加 */
    rgba(0, 0, 0, var(--tpl-bg-darkness, 0.10)),
    /* 扫描线叠加（固定在暗色之上） */
    repeating-linear-gradient(
      0deg,
      transparent,
      transparent 2px,
      rgba(114, 228, 255, calc(var(--tpl-scanline-opacity, 0.15) * 0.3)) 2px,
      rgba(114, 228, 255, calc(var(--tpl-scanline-opacity, 0.15) * 0.3)) 3px
    );

  backdrop-filter: blur(var(--tpl-blur-radius, 24px));
  -webkit-backdrop-filter: blur(var(--tpl-blur-radius, 24px));
}
```

扫描线密度固定 2px 透明 + 1px 淡色，不透明度 `--tpl-scanline-opacity`（0–40%）可调。

## 7. 标题条

### 7.1 展开态标题

```text
┌─ Neon HUD ─────────────────────── [收起▲] [锁◈] ─┐
│  AFTERLIGHT  ·  6 GAMES                            │
└────────────────────────────────────────────────────┘
```

- 左侧：模板图标（12px）+ 模板名
- 中间：海报标题（用户可重命名）+ 游戏总数
- 右侧：收起按钮 + 锁按钮（如在编辑态显示）

### 7.2 收起态标题（60px）

```text
┌─ Neon HUD  ·  6 GAMES ─── [展开▼] ─┐
```

- 仅标题 + 游戏数 + 展开按钮
- 标题条高度 60px，保持原宽度
- 内部垂直居中

### 7.3 标题条 CSS

```css
.tpl-title-area {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 48px;  /* 展开态标题条高度 */
  padding: var(--tpl-header-padding);
  border-bottom: 1px solid color-mix(in srgb, var(--tpl-accent) 20%, transparent);
}

.tpl-title-text {
  font-size: var(--tpl-title-size);
  font-weight: 700;
  color: var(--tpl-text-primary);
  letter-spacing: 0.02em;
}

.tpl-title-count {
  font-size: var(--tpl-subtitle-size);
  color: var(--tpl-text-secondary);
  font-family: var(--tpl-font-mono);
}
```

## 8. 游戏图标卡片

### 8.1 结构

```text
┌──────────────────┐
│ [游戏图标 64px]   │
│                  │
│ HOLLOW CROWN     │  ← 13px/600
│ 80 次启动         │  ← 11px/400 等宽
└──────────────────┘
```

### 8.2 CSS

```css
.tpl-item-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 8px;
  background: var(--tpl-surface);
  border: 1px solid color-mix(in srgb, var(--tpl-accent-dim) 40%, transparent);
  border-radius: var(--tpl-item-radius);
  transition: border-color 150ms ease, box-shadow 150ms ease;
  cursor: pointer;
}

.tpl-item-card:hover {
  border-color: var(--tpl-accent);
  box-shadow: 0 0 8px var(--tpl-accent-glow);
  background: var(--tpl-surface-hover);
}

.tpl-item-icon {
  width: var(--tpl-icon-base-size, 64px);
  height: var(--tpl-icon-base-size, 64px);
  border-radius: 8px;
  object-fit: cover;
}

.tpl-item-name {
  font-size: var(--tpl-item-name-size);
  font-weight: 600;
  color: var(--tpl-text-primary);
  text-align: center;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tpl-item-stat {
  font-size: var(--tpl-item-stat-size);
  font-weight: 400;
  color: var(--tpl-text-secondary);
  font-family: var(--tpl-font-mono);
}
```

## 9. 装饰元素

### 9.1 数据流装饰线

标题条下方 2 条水平渐变细线，模拟数据流指示器：

```css
.tpl-data-stream {
  position: absolute;
  left: var(--tpl-corner-radius);
  right: var(--tpl-corner-radius);
  height: 1px;
  background: linear-gradient(
    90deg,
    transparent 0%,
    var(--tpl-accent) 20%,
    var(--tpl-accent) 80%,
    transparent 100%
  );
  opacity: calc(var(--tpl-neon-intensity, 30) / 200);
}
```

两条线间距 3px，上线不透明度高于下线。

### 9.2 角落 HUD 标记

四角各有一个 L 形角标（8px × 8px），只在展开态可见：

```css
.tpl-corner-mark {
  position: absolute;
  width: 8px;
  height: 8px;
  border-color: var(--tpl-accent);
  border-style: solid;
  opacity: calc(var(--tpl-neon-intensity, 30) / 200);
}
/* 左上 */ { top: 4px; left: 4px; border-width: 1px 0 0 1px; }
/* 右上 */ { top: 4px; right: 4px; border-width: 1px 1px 0 0; }
/* 左下 */ { bottom: 4px; left: 4px; border-width: 0 0 1px 1px; }
/* 右下 */ { bottom: 4px; right: 4px; border-width: 0 1px 1px 0; }
```

## 10. 响应式断点

Neon HUD 在三种海报宽度下的适配：

| 宽度范围 | 行为 |
|----------|------|
| ≥ 360px（标准 ~420px） | 完整渲染：标题条 + 角标 + 数据流线 + 扫描线 |
| 280–359px（最小 ~280px） | 角标隐藏，数据流线隐藏，标题字号缩小至 20px |
| < 280px | 不出现（海报最小宽度 240px，但排版无法容纳） |

响应式通过 CSS 容器查询实现：

```css
@container poster (max-width: 359px) {
  .tpl-corner-mark { display: none; }
  .tpl-data-stream { display: none; }
  .tpl-title-text { font-size: 20px; }
}
```

## 11. 图标缩放与卡片自适应

图标卡片的 `tpl-item-card` 本身不缩放——缩放的是 CSS `transform: scale()` 应用在卡片上。卡片内容（图标 + 文字）等比例缩放，保证像素精度：

```css
.tpl-item-card[data-scale="2.5"] {
  transform: scale(2.5);
  transform-origin: center center;
}
```

卡片间距由布局引擎控制在 `gap` 参数内，缩放后的卡片之间保持 `--tpl-icon-gap` 间距。

## 12. CSS 变量完整列表

```text
// === 颜色 ===
--tpl-accent            #72e4ff    强调色
--tpl-accent-dim        #3a8fa8    强调色暗
--tpl-accent-glow       #72e4ff80  辉光色
--tpl-bg-darkness       0.10       背景不透明度 (0.00–0.30)
--tpl-surface           rgba(8,12,18,0.85)
--tpl-surface-hover     rgba(16,24,36,0.90)
--tpl-text-primary      #f4f6f8
--tpl-text-secondary    #8a949e
--tpl-text-accent       #72e4ff

// === 排版 ===
--tpl-font-family       'Segoe UI Variable', ...
--tpl-font-mono         'Cascadia Code', ...
--tpl-title-size        28px
--tpl-subtitle-size     14px
--tpl-item-name-size    13px
--tpl-item-stat-size    11px
--tpl-label-size        10px

// === 间距 ===
--tpl-icon-gap          12px
--tpl-padding           16px
--tpl-header-padding    12px 16px
--tpl-section-gap       16px

// === 圆角 ===
--tpl-corner-radius     18px
--tpl-item-radius       10px

// === 边框与发光 ===
--tpl-neon-intensity    30          (0–100)
--tpl-glow-spread       12px        (8–24)

// === 扫描线与装饰 ===
--tpl-scanline-opacity  0.15        (0.00–0.40)

// === 背景 ===
--tpl-blur-radius       24px        (0–48)

// === 图标 ===
--tpl-icon-base-size    64px        (48–96)
```

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立 Neon HUD 完整视觉规格：色板、排版、间距、边框发光、扫描线、角标、响应式断点 |

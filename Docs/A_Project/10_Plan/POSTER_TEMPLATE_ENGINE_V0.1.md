# Desktop-Up 海报模板引擎技术设计 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 设计目标

为大幅面海报提供可切换、可定制的模板渲染系统。模板引擎负责：加载模板文件 → 解析 CSS 变量注入点 → 渲染 HTML 结构 → 注入用户参数覆盖 → 隔离模板间样式。

## 2. 实现位置决策

### 2.1 候选方案

| 方案 | 描述 | 优点 | 缺点 |
|------|------|------|------|
| **A. Rust 侧预渲染** | Rust 读取模板文件，拼接 HTML 字符串，注入 CSS 变量，通过 Tauri IPC 推送到 WebView | 模板安全校验在 Rust 侧完成，类型安全 | 模板变更需要重建 WebView 内容；CSS/HTML 拼接在 Rust 中不自然 |
| **B. WebView 内 JS 渲染** | 模板文件通过 Tauri IPC 传给前端，前端 JS 模板引擎负责渲染和热切换 | 自然的前端模板渲染；支持热切换；可利用浏览器 CSS 能力 | 安全校验需要在两个层面都做 |
| **C. 混合：Rust 负责安全校验 + WebView JS 负责渲染** | 模板导入时在 Rust 侧完成安全扫描和净化，运行时 JS 做渲染和热切换 | 两头优势结合 | 稍复杂 |

**选择方案 C**。理由：
- 海报本身就是 WebView 内容——用 JS 渲染模板是最自然的路径
- 安全校验（路径穿越、压缩炸弹、脚本检测）必须在 Rust 侧完成，WebView 沙箱不足以防护文件系统层攻击
- 运行时 CSS 注入和热切换在 JS 侧效率最高，无需重建 WebView
- 模板不是每帧都换——加载开销可接受

### 2.2 架构

```text
模板导入路径：
  用户选择 .theme.zip / .full.zip
    → Rust: 校验链 (解压、schema、路径穿越、类型、净化)
    → Rust: 写入应用数据目录 templates/
    → Rust: 注册到 PosterTemplate 表
    → 通知前端：新模板可用

运行时渲染路径：
  海报创建/模板切换
    → 前端请求模板数据 (Tauri invoke: get_template_bundle)
    → Rust: 读取模板文件
    → Rust: 安全扫描 CSS/HTML (每次加载都扫，不信任磁盘)
    → Rust: 返回 { html, css, schema, assets } 给前端
    → 前端 JS: 解析 HTML 插槽 → 注入 CSS 变量 → 应用用户覆盖 → 渲染
```

## 3. 模板渲染引擎（前端 JS 侧）

### 3.1 模板数据结构

Rust 传给前端的 bundle：

```typescript
interface TemplateBundle {
  template_id: string
  template_key: string
  version: string
  html: string              // 原始 template.html 内容
  css: string               // 原始 template.css 内容
  manifest: TemplateManifest
  params_schema: ParamGroup[]  // params-schema.json 解析后
  assets: Record<string, string>  // slot_name → base64 (小素材内联) 或 file:// 路径
}
```

### 3.2 HTML 插槽语法

使用 `{{ }}` 模板语法，仅支持变量插值和 `data-slot` 标记：

```html
<!-- template.html 示例 (Neon HUD) -->
<div class="poster tpl-neon-hud" data-template="neon-hud">
  <!-- 标题插槽 -->
  <header class="tpl-title-area" data-slot="title">
    <h1 class="tpl-title-text">{{poster.title}}</h1>
    <span class="tpl-title-count">{{poster.item_count}} GAMES</span>
  </header>

  <!-- 图标网格插槽 -->
  <main class="tpl-icons-area" data-slot="icons">
    <div class="tpl-icon-grid">
      <!-- 由布局引擎动态填充，不是模板语法 -->
      <div id="poster-icon-container"></div>
    </div>
  </main>

  <!-- 状态栏插槽 -->
  <footer class="tpl-status-bar" data-slot="status">
    <span class="tpl-status-text">{{poster.collapsed_state === 'expanded' ? 'EXPANDED' : 'COLLAPSED'}}</span>
  </footer>

  <!-- 模板专属装饰层 (不被用户覆盖) -->
  <div class="tpl-decor-layer" data-slot="decor">
    <canvas id="tpl-scanlines"></canvas>
    <div class="tpl-glow-top"></div>
    <div class="tpl-glow-bottom"></div>
  </div>
</div>
```

插槽规则：
- `data-slot="name"` 标记可被用户深度 3 覆盖的区域
- 没有 `data-slot` 的元素始终使用模板默认值——用户无法替换
- 用户覆盖时：替换整个 `data-slot` 元素的 innerHTML，保留元素的 `data-slot` 属性和 class
- `{{ }}` 内的表达式只支持简单变量访问（`.title`、`.item_count`）和三元表达式（`a ? b : c`），不支持函数调用、循环或任意 JS

### 3.3 CSS 变量注入点

模板 CSS 使用 `--tpl-*` 命名空间。渲染时注入两级变量：

```text
第一级：模板默认值 (来自 params-schema.json 的 default)
第二级：用户覆盖值 (来自 ThemeInstance.parameter_overrides)
第二级覆盖第一级，以 CSS custom property 形式注入到 :root
```

```css
/* template.css 示例 (Neon HUD) */
.poster.tpl-neon-hud {
  /* 由注入的 CSS 变量驱动 */
  background: rgba(0, 0, 0, var(--tpl-bg-darkness, 0.1));

  /* 边框 */
  border: 1px solid var(--tpl-accent, #72e4ff);
  box-shadow:
    0 0 calc(var(--tpl-neon-intensity, 30) * 0.267px) var(--tpl-accent, #72e4ff),
    inset 0 0 calc(var(--tpl-neon-intensity, 30) * 0.133px) rgba(114, 228, 255, 0.15);

  /* 排版 */
  font-family: var(--tpl-font-family, 'Segoe UI Variable', sans-serif);
  border-radius: var(--tpl-corner-radius, 18px);

  /* 背景模糊（壁纸可见） */
  backdrop-filter: blur(var(--tpl-blur-radius, 24px));
  -webkit-backdrop-filter: blur(var(--tpl-blur-radius, 24px));
}

/* 扫描线装饰 */
.tpl-decor-layer .tpl-scanlines {
  opacity: var(--tpl-scanline-opacity, 0.15);
}

/* 图标间距 */
.tpl-icon-grid {
  gap: var(--tpl-icon-gap, 12px);
}
```

用户在 `params-schema.json` 中声明的 `cssProperty` 字段自动映射——参数面板的滑块值经过 `cssTransform` 计算后写入对应 CSS 变量。

### 3.4 模板间样式隔离

不使用 Shadow DOM（会增加复杂性和性能开销）。采用**严格的 CSS 命名空间约定 + 前缀系统**：

```text
1. 模板 CSS 所有选择器以 .tpl-{key} 为根（如 .tpl-neon-hud）
2. 海报容器只挂一个模板类（如 class="poster tpl-neon-hud"）
3. 用户 CSS 覆盖注入在 .tpl-{key} 命名空间内
4. 热切换时：移除旧的模板类，添加新的模板类 → CSS 自动切换
5. 全局海报样式 (poster-surface.css) 不包含任何模板相关规则
```

切换模板时的清理顺序：
```text
1. 移除旧模板类的所有注入 CSS 变量（从 :root 或容器上）
2. 移除旧模板类
3. 添加新模板类
4. 注入新模板的 CSS 变量（默认值 → 用户覆盖）
5. 如果用户对该模板有保存的参数覆盖，应用它们
```

这种方式比 Shadow DOM 更快（无需创建 shadow root 和重建样式计算），且在海报这个上下文中——只有一个模板在运行时活跃——没有跨模板样式泄漏的实际风险。

### 3.5 热切换生命周期

```text
用户选择新模板
  → 前端检查：该模板是否已加载过？（有缓存）
      是 → 直接使用缓存的 TemplateBundle
      否 → invoke('get_template_bundle', { template_id })
            → Rust 返回 bundle
  → 前端：保存当前 TemplateInstance 的参数覆盖快照（用于撤销）
  → 前端：清理旧模板 CSS 变量
  → 前端：切换 CSS 类
  → 前端：注入新模板 CSS 变量
  → 前端：渲染新模板 HTML 插槽（仅替换变化的部分）
  → 布局引擎：重新计算图标网格（因为容器尺寸可能变化）
  → 控制中心预览画布同步更新
  → 用户点击保存 → invoke('set_theme_instance', { poster_id, theme_instance })
     → Rust 持久化 → 桌面海报同步
  或 用户点击撤销 → 恢复旧模板 + 旧参数
```

热切换性能目标：< 200ms 完成 CSS 类切换和重绘（不含图标重新排布）。

### 3.6 `manifest.json` 完整 schema

```json
{
  "$schema": "https://desktop-up.app/schemas/template-manifest-v1.json",
  "template_key": "neon-hud",
  "name": "Neon HUD",
  "version": "1.0.0",
  "author": "Desktop-Up Built-in",
  "description": "赛博朋克霓虹风格的 HUD 海报",
  "trust_level": "builtin",

  "depth0": {
    "presets": [
      { "id": "neon-cyan", "name": "霓虹青", "thumbnail": "presets/neon-cyan.png" },
      { "id": "neon-magenta", "name": "霓虹洋红", "thumbnail": "presets/neon-magenta.png" },
      { "id": "neon-amber", "name": "霓虹琥珀", "thumbnail": "presets/neon-amber.png" }
    ]
  },

  "depth2": {
    "slots": [
      {
        "id": "bg",
        "label": "背景",
        "type": "image",
        "accept": ["png", "jpg", "webp"],
        "max_size_bytes": 2097152,
        "default_asset": "assets/bg.png"
      },
      {
        "id": "border-overlay",
        "label": "海报装饰",
        "type": "svg",
        "accept": ["svg"],
        "max_size_bytes": 204800,
        "default_asset": "assets/border-overlay.svg"
      }
    ]
  },

  "depth3": {
    "css_allowed": true,
    "html_slots": ["title", "status"],
    "script_allowed": false,
    "description": "Neon HUD 不支持代码脚本。导入 full 包时此字段为 true。"
  }
}
```

## 4. 模板安全校验链（Rust 侧）

### 4.1 导入时校验

```
1. 解压到隔离临时目录 (%TEMP%/desktop-up-theme-validate-{uuid}/)
2. 目录遍历防护：所有路径归一化后必须在临时目录内
3. 符号链接检测：拒绝任何 symlink/junction
4. 压缩炸弹检测：解压后总大小 ≤ 20MB，单文件 ≤ 5MB
5. manifest.json 存在且 JSON 解析成功
6. params-schema.json 存在且 JSON 解析成功
7. 文件类型白名单：仅允许 .html .css .json .png .jpg .webp .svg .ttf .otf .woff2
8. 素材净化：
   - PNG/JPG/WebP: 重编码去除 EXIF/元数据
   - SVG: 移除 <script>、<foreignObject>、事件处理器 (on*)
   - 字体: 去除嵌入指令
9. 校验通过 → 原子复制到 app/templates/{template_key}/
10. 注册到 PosterTemplate 表
```

### 4.2 运行时加载校验

每次加载模板到 WebView 前（`get_template_bundle`）：

```
1. CSS 安全扫描：
   - 拦截 url() 含 http/https → 替换为 url(about:invalid)
   - 拦截 @import → 移除整条规则
   - 不修改其他内容

2. HTML 安全扫描：
   - 拦截 <script> → 移除
   - 拦截 <iframe> → 移除
   - 拦截 <object> / <embed> → 移除
   - 拦截 on* 事件属性 → 移除
```

运行时扫描是防御性的——导入时已经净化过，但防止磁盘上的模板文件被手动修改或损坏后注入恶意内容。

## 5. 性能边界

| 操作 | 目标 | 说明 |
|------|------|------|
| 模板首次加载 | < 50ms | 从磁盘读取 + JSON 解析 + 安全扫描 |
| CSS 变量注入 | < 5ms | 设置 CSS custom properties |
| 模板热切换 | < 200ms | CSS 类切换 + 变量注入 + 图标容器重建 |
| 海报收起/展开过渡 | 250ms | CSS transition，不由模板引擎控制 |
| 模板文件缓存 | 首次加载后常驻 | 前端保持 TemplateBundle 在内存中 |

模板切换时不重建 WebView——只替换 CSS 类和 DOM 内容。

## 6. 文件系统布局

```text
app/templates/                          ← 内置模板，随应用打包
├── neon-hud/
│   ├── manifest.json
│   ├── params-schema.json
│   ├── template.html
│   ├── template.css
│   ├── thumbnail.png
│   ├── presets/
│   │   ├── neon-cyan.json
│   │   ├── neon-magenta.json
│   │   └── neon-amber.json
│   └── assets/
│       ├── bg.png
│       └── border-overlay.svg
├── cinema-lightbox/
│   └── ...（同上结构）
└── _builtin_index.json                ← 内置模板注册表

%LOCALAPPDATA%/DesktopUp/themes/       ← 用户导入的模板
└── imported-{uuid}/
    └── ...（同结构 + trust_level 标记）
```

内置模板随应用打包到 `app/templates/`，不进入用户数据目录。用户导入的模板存到 `%LOCALAPPDATA%/DesktopUp/themes/`。

## 7. 错误处理

| 错误场景 | 处理 |
|----------|------|
| 模板文件缺失 | 降级到内置 "minimal-fallback" 模板（纯黑背景 + 白色文字 + 等大图标网格） |
| CSS 解析错误 | 跳过错误规则，其余 CSS 正常生效。控制台记录错误行 |
| HTML 插槽解析错误 | 使用模板默认 HTML，不应用用户覆盖 |
| params-schema.json 格式错误 | 不渲染参数面板，在控制中心显示"参数配置不可用" |
| 热切换中用户快速连续切换 | 去抖 300ms，取最后选择的模板 |
| 模板目录磁盘满 | 导入失败并提示用户释放空间 |

## 8. 实施顺序

| 切片 | 内容 | 依赖 |
|------|------|------|
| TE-1 | `manifest.json` 和 `params-schema.json` schema 冻结 | 无 |
| TE-2 | Rust 侧模板导入校验链 | TE-1 |
| TE-3 | 前端 JS 模板渲染引擎（插槽 + CSS 变量注入） | TE-1 |
| TE-4 | 热切换生命周期 | TE-2 + TE-3 |
| TE-5 | Neon HUD 模板完整 CSS/HTML | TE-3 + GAP-05 视觉设计 |
| TE-6 | Cinema Lightbox 模板完整 CSS/HTML | TE-3 + GAP-05 视觉设计 |

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立模板引擎技术设计：方案 C（Rust 校验 + JS 渲染）、插槽语法、CSS 变量注入、命名空间隔离、热切换、manifest schema、安全校验链 |

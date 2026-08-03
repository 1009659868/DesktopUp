# Desktop-Up 海报风格化定制体系 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 目标

让用户从"一键切换预设"到"导入完整代码主题"都能自由选择控制深度，每一层建立在上一层之上，不强制升级。同时每一层的控制能力通过统一 API 暴露给内置 Agent，让 AI 助手与手动 UI 共用同一套参数读写路径。

## 2. 四级控制深度

```
深度 0：开关与预设   ← 零参数，一键切换
深度 1：结构化参数   ← 滑块、色板、枚举
深度 2：素材替换     ← 图片、字体、SVG 资源
深度 3：代码覆盖     ← CSS 片段、HTML 插槽替换
```

### 2.1 深度 0：开关与预设

用户看到的是即时生效的开关和下拉选项，不需要理解任何参数含义：

```text
海报模板    [Neon HUD ▾]
  ├─ 动态缩放  [开]
  ├─ 背景模糊  [中 ▾]        ← 关 / 低 / 中 / 高
  ├─ 海报圆角  [大 ▾]        ← 无 / 小 / 中 / 大
  ├─ 图标间距  [标准 ▾]      ← 紧凑 / 标准 / 宽松
  └─ 配色方案  [默认 ▾]      ← 每套模板 3–5 个预设配色
```

每个枚举值背后对应一组深度 1 的具体参数。这是 80% 用户永远停留的层级。

### 2.2 深度 1：结构化参数（已确认：参数面板自动生成）

用户展开某个开关的"自定义"选项后进入。参数面板由模板的 `params-schema.json` 自动生成，新增模板不需要改写控制中心前端代码。

```text
海报模板：Neon HUD
━━━━━━━━━━━━━━━━━━━━
边框
  霓虹强度      [30%] ────●──── 100%
  发光扩散      [8px] ──●────── 24px
  扫描线不透明度 [15%] ──●────── 40%

背景
  暗度          [10%] ────●──── 30%
  模糊半径      [24px] ──●───── 48px

排版
  标题字号      [28px] ──●───── 48px
  标题字重      [700] ──●────── 900
  图标间距      [12px] ──●───── 32px
  海报圆角      [18px] ──●───── 48px

颜色
  强调色        [#72e4ff] → 色板
  边框色        [#72e4ff] → 色板（默认跟随强调色）
  文字色        [#f4f6f8] → 色板

图标
  基准大小      [64px] ──●───── 96px
  最小缩放      [50%] ──●────── 300%
  最大缩放      [50%] ──────●── 300%
  默认缩放      [150%] ──●───── 300%

━━━━━━━━━━━━━━━━━━━━
[重置为模板默认值]  [导出为参数主题]
```

参数 schema 定义（`params-schema.json`）：

```json
{
  "groups": [
    {
      "id": "colors",
      "label": "颜色",
      "params": [
        {
          "key": "--tpl-accent",
          "label": "强调色",
          "type": "color",
          "default": "#72e4ff"
        }
      ]
    },
    {
      "id": "border",
      "label": "边框",
      "params": [
        {
          "key": "--tpl-neon-intensity",
          "label": "霓虹强度",
          "type": "percent",
          "min": 0,
          "max": 100,
          "default": 30,
          "cssProperty": "--tpl-glow-opacity",
          "cssTransform": "value => value / 100"
        }
      ]
    }
  ]
}
```

参数类型：`color | percent | pixel | enum | number`。`cssTransform` 是纯函数表达式，由前端 `new Function()` 构造并缓存，只在导入模板时评估，不执行用户编写的动态代码。

用户导入自定义模板时，控制中心读取其 `params-schema.json` 自动渲染参数面板。这使得第三方模板无需任何前端开发即可获得完整的参数编辑 UI。

### 2.3 深度 2：素材替换

用户上传自己的图片、字体、SVG 替换模板中预设的素材槽位：

```text
素材管理
━━━━━━━━━━━━━━━━━━━━
背景
  当前：template-default-bg.png
  [替换背景图片]  [重置]

海报装饰
  当前：neon-rim-overlay.svg
  [替换装饰 SVG]  [重置]

图标字体
  当前：Segoe UI Variable
  [导入字体文件]  [重置]

角标/Logo
  当前：无
  [导入自定义角标]  [移除]
```

素材槽位由模板 `manifest.json` 的 `depth2.slots` 数组声明：

| 素材类型 | 允许格式 | 大小限制 | 处理方式 |
|----------|---------|---------|---------|
| 背景图片 | PNG, JPG, WebP | ≤ 2MB | 重编码去除元数据，复制到主题缓存目录 |
| SVG 装饰 | SVG（净化后） | ≤ 200KB | 移除 script/foreignObject/事件处理器 |
| 字体 | TTF, OTF, WOFF2 | ≤ 5MB | 去除嵌入指令，仅保留字形数据 |
| 角标/Logo | PNG, SVG | ≤ 500KB | 同上 |

### 2.4 深度 3：代码覆盖（已确认：纯代码编辑，不做可视化编辑器）

用户通过控制中心内置的代码编辑器修改 CSS 片段或 HTML 插槽。

**CSS 覆盖**：

```css
/* 用户自定义 CSS —— 语法高亮 + 即时错误提示 */
/* ❌ 不允许：url() 外链、@import、外部字体 */

.poster {
  --tpl-accent: #ff6b6b;
}

.game-tile:hover {
  filter: drop-shadow(0 0 16px var(--tpl-accent));
}
```

约束：
- CSP 拦截一切外部网络请求
- CSS 解析器在注入前过滤 `url()` 含 `http/https` 的规则和 `@import`
- 解析失败不崩溃——坏掉的规则被跳过，其余生效

**HTML 插槽覆盖**：

模板的核心结构是具名插槽。用户可以替换单个插槽的 HTML，不影响其他插槽：

```html
<header class="tpl-title-area" data-slot="title">
  <div class="my-custom-title">
    <span>🎮 {{poster.title}}</span>
    <small>{{itemCount}} GAMES</small>
  </div>
</header>
```

模板引擎渲染时：加载默认 HTML → 检查用户覆盖 → 有则用覆盖版 → 注入 CSS 变量。

## 3. 导入/导出体系（已确认：首版只做文件导入导出）

### 3.1 导出格式

| 格式 | 内容 | 文件 | 可分享 |
|------|------|------|--------|
| 参数快照 | 深度 1 的所有参数值 | `.params.json` (≈2KB) | ✅ |
| 素材包 | 参数 + 素材文件 + 目录清单 | `.theme.zip` | ✅ |
| 完整主题 | 参数 + 素材 + CSS 片段 + 插槽 HTML | `.full.zip` | ✅ |

### 3.2 导入流程

```text
选择文件
  → 解压到隔离临时目录
  → 校验 schema 版本
  → 路径穿越 / 符号链接 / 压缩炸弹扫描
  → 文件类型白名单检查
  → 素材净化（图片重编码、SVG 去脚本）
  → CSS 安全扫描（拦截 url()、@import）
  → HTML 插槽扫描（拦截 script、iframe、object）
  → full.zip 含脚本时弹出信任确认

全部通过 → 原子安装
任一失败 → 列出失败项，不部分安装
```

### 3.3 代码主题信任确认

导入 `full.zip`（含受限 JavaScript）时弹出明确警告：

```text
┌─────────────────────────────────────────────┐
│  ⚠ 此主题包含可执行脚本                       │
│                                             │
│  脚本能力：                                   │
│  ✅ Canvas 2D 绘制                          │
│  ✅ 读取海报数据和主题参数                     │
│  ✅ requestAnimationFrame (≤30fps 可中断)    │
│                                             │
│  脚本不能：                                   │
│  ❌ 访问文件系统 / 网络 / 注册表 / 进程        │
│  ❌ 读写其他海报或系统设置                     │
│  ❌ 创建 iframe / web worker                │
│                                             │
│  [查看完整脚本清单]                           │
│  [信任并安装]  [拒绝]                        │
└─────────────────────────────────────────────┘
```

## 4. AI Agent 控制 API

所有四级深度的读写操作通过统一 API 暴露给内置 Agent，AI 助手与手动 UI 共用同一条命令链。

### 4.1 API 设计原则

- **与手动 UI 共用路径**：Agent 发出的修改与用户拖滑块产生相同的 `DesktopCommand`
- **始终先预览后确认**：Agent 生成 `ChangePlan`，控制中心展示差异，用户点击确认后才写入
- **全部进入撤销栈**：Agent 的修改与手动修改共用同一个撤销历史
- **Key 未配置时 API 不初始化**：无 Key 时不影响手动 UI，手动参数编辑完全独立

### 4.2 读取 API

```text
get_poster(poster_id)                 → 海报当前完整状态（模板、参数、素材引用）
get_available_themes()                → 已安装模板列表（id、名称、预览图、版本）
get_theme_parameter_schema(theme_id)  → 模板的 params-schema.json 内容
get_theme_presets(theme_id)           → 模板的深度 0 预设列表
get_asset_slots(theme_id)             → 模板的素材槽位及当前填充状态
get_css_override(theme_id)            → 用户当前 CSS 覆盖片段
get_html_slot(theme_id, slot_name)    → 用户当前插槽覆盖内容（如有）
```

### 4.3 写入 API

所有写入操作先返回 `ChangePlan` 预览，用户确认后执行：

```text
apply_theme_preset(theme_id, preset_id)                    → ChangePlan
set_theme_parameters(theme_id, { key: value, ... })        → ChangePlan
replace_asset(theme_id, slot_id, file_path)                → ChangePlan
set_css_override(theme_id, css_text)                       → ChangePlan
set_html_slot(theme_id, slot_name, html_text)              → ChangePlan
import_theme(file_path, trust_level)                       → ChangePlan
export_theme(theme_id, format)                             → 返回文件路径
reset_theme_to_default(theme_id)                           → ChangePlan
```

### 4.4 Agent 能力边界

| 操作 | Agent 可否执行 | 约束 |
|------|---------------|------|
| 切换预设 | ✅ | 无需确认，可直接执行 |
| 修改参数 | ✅ | 返回 ChangePlan，预览后确认 |
| 替换素材 | ✅ | 只能引用已通过校验的素材文件 |
| 修改 CSS 片段 | ✅ | CSS 经过安全扫描后才写入 |
| 修改 HTML 插槽 | ✅ | HTML 经过 script/iframe/object 扫描 |
| 导入主题文件 | ✅ | 完整安全校验链后生成预览 |
| 导出主题 | ✅ | 只写应用数据目录内路径 |
| 信任代码主题 | ❌ | 必须用户手动确认，Agent 不可代替信任 |
| 修改其他海报的参数 | ❌ | Agent 只操作当前选中海报 |
| 文件系统 / 网络访问 | ❌ | 不在 Agent 工具白名单中 |

### 4.5 AI 辅助主题草稿生成

Agent 可以帮助用户生成主题草稿，流程见 [AI_ASSISTANT_DESIGN_V0.1.md](AI_ASSISTANT_DESIGN_V0.1.md) 第 6 节：

```text
用户描述 → Agent 生成 JSON 参数 / CSS 片段 / 模板草稿
  → 写入临时主题目录
  → 路径/大小/类型/CSP 检查
  → 隔离预览
  → 显示代码差异与视觉对比
  → 用户确认
  → 安装为新的主题版本
```

## 5. 首版 CSS 方案决策（已确认：不使用原子 CSS）

当前阶段不引入 Tailwind、UnoCSS 或其他原子 CSS 框架。理由：

- 保持零前端依赖原则——海报页面是独立 HTML，当前不需要 npm 构建管线
- 模板开发者目前只有我们自己，每套模板 CSS 规模可控（约 200–400 行）
- CSS 变量体系已经提供了参数化能力，原子 CSS 在这个阶段是锦上添花
- 等模板市场开放后，可为主题开发者提供 UnoCSS preset 工具包作为可选增强

当前方案：**纯 CSS 变量 + 结构化参数 schema** 足够覆盖全部需求。

## 6. 模板文件结构

每套模板的完整目录结构：

```text
app/templates/neon-hud/
├── manifest.json          ← 模板元数据、深度 0–3 声明
├── params-schema.json     ← 深度 1 参数定义（供控制中心自动生成面板）
├── template.html          ← 默认 HTML 结构 + 具名插槽
├── template.css           ← 默认 CSS + CSS 变量引用
├── thumbnail.png          ← 模板预览图（控制中心展示）
├── presets/
│   ├── neon-cyan.json     ← 深度 0 配色预设
│   ├── neon-magenta.json
│   └── neon-amber.json
└── assets/
    ├── bg.png             ← 默认背景素材
    ├── border-overlay.svg ← 默认装饰 SVG
    └── font.ttf           ← 默认字体（如有）
```

## 7. 实时预览与撤销

- 控制中心右侧为预览画布，参数拖拽时即时反映变化
- 修改参数时显示高亮提示（"你正在修改：霓虹强度"）和修改前后对比
- 点击保存后才写入正式主题实例，同步到桌面海报
- 保存后进入全局撤销栈，与 AI 助手共用，支持多步撤销

## 8. Agent 共用约束

Agent 的所有修改必须遵循与手动 UI 相同的规则：
- 锁定海报不能由 Agent 解锁后改变位置（必须用户手动解锁）
- Agent 修改的参数不得超出 `params-schema.json` 声明的 min/max
- Agent 生成的 CSS 必须通过安全扫描
- Agent 不可代替用户信任代码主题

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立四级风格化定制体系、参数面板自动生成、AI Agent 共用 API、导入导出安全链 |

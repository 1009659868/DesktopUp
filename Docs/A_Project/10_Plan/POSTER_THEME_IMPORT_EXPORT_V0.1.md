# Desktop-Up 主题导入导出格式规范 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 目的

定义 `POSTER_THEME_CUSTOMIZATION_V0.1` §3 中三种导出格式的完整文件结构、JSON Schema 和安全约束。模板引擎设计见 [`POSTER_TEMPLATE_ENGINE_V0.1`](POSTER_TEMPLATE_ENGINE_V0.1.md)。

## 2. 三种导出格式

| 格式 | 内容 | 文件名示例 | 用途 |
|------|------|-----------|------|
| 参数快照 | 深度 1 全部参数值 | `neon-hud-params-2026-08-03.params.json` | 分享配色方案，≈2KB |
| 素材包 | 参数快照 + 素材文件 | `my-neon-hud.theme.zip` | 含自定义背景/字体的主题 |
| 完整主题 | 素材包 + CSS 片段 + HTML 插槽覆盖 | `my-neon-hud.full.zip` | 含代码的完整主题 |

---

## 3. `.params.json` 格式

### 3.1 JSON Schema

```json
{
  "$schema": "https://desktop-up.app/schemas/params-snapshot-v1.json",
  "format_version": "1.0",
  "type": "params_snapshot",

  "meta": {
    "exported_at": "2026-08-03T12:00:00Z",
    "exported_by": "desktop-up",
    "source_template_key": "neon-hud",
    "source_template_version": "1.0.0",
    "preset_id": null,
    "label": "我的霓虹青配色"
  },

  "parameters": {
    "--tpl-accent": "#ff6b6b",
    "--tpl-neon-intensity": 45,
    "--tpl-glow-spread": 16,
    "--tpl-scanline-opacity": 0.20,
    "--tpl-bg-darkness": 0.12,
    "--tpl-blur-radius": 24,
    "--tpl-corner-radius": 18,
    "--tpl-icon-gap": 12,
    "--tpl-title-size": 28,
    "--tpl-icon-base-size": 64
  }
}
```

### 3.2 规则

- `parameters` 包含模板 `params-schema.json` 声明的**全部**参数（不仅是用户覆盖值），便于独立于模板版本恢复
- `meta.label` 可选，用户自定义名称
- `meta.preset_id`：如果是基于某个预设的，记录预设 ID；`null` 表示全手动
- `source_template_version`：记录导出时模板版本，导入时用于迁移判断
- 不包含任何二进制内容——纯 JSON

---

## 4. `.theme.zip` 格式

### 4.1 内部目录结构

```text
my-neon-hud.theme.zip
├── manifest.json
├── params.json              ← 同 §3 的参数快照
└── assets/
    ├── bg.png               ← 用户替换的背景图
    ├── border-overlay.svg   ← 用户替换的 SVG
    └── font.ttf             ← 用户导入的字体（可选）
```

### 4.2 manifest.json

```json
{
  "$schema": "https://desktop-up.app/schemas/theme-bundle-v1.json",
  "format_version": "1.0",
  "type": "theme_bundle",

  "meta": {
    "exported_at": "2026-08-03T12:00:00Z",
    "exported_by": "desktop-up",
    "source_template_key": "neon-hud",
    "source_template_version": "1.0.0",
    "label": "我的霓虹主题"
  },

  "asset_mappings": {
    "bg": "assets/bg.png",
    "border-overlay": "assets/border-overlay.svg",
    "font": "assets/font.ttf"
  },

  "asset_checksums": {
    "assets/bg.png": "sha256:abc123...",
    "assets/border-overlay.svg": "sha256:def456...",
    "assets/font.ttf": "sha256:ghi789..."
  }
}
```

### 4.3 规则

- `asset_mappings` 的 key 必须匹配源模板 `manifest.json` 中 `depth2.slots[].id`
- 每个映射到的文件必须存在于 zip 内
- `asset_checksums` 用于导入后验证完整性
- zip 内所有文件路径使用正斜杠，根目录无 `../` 前缀
- 不接受空目录或零字节文件
- 总大小 ≤ 20MB

---

## 5. `.full.zip` 格式

### 5.1 内部目录结构

```text
my-neon-hud.full.zip
├── manifest.json             ← 完整模板元数据与文件清单（必需）
├── params-schema.json        ← 结构化参数 schema（必需）
├── template.html             ← 完整模板 DOM（必需）
├── template.css              ← 完整模板样式（必需）
├── params.json               ← 当前参数快照（可选）
├── user.css                  ← 深度 3 CSS 覆盖片段（可选）
├── slots/                    ← HTML 插槽覆盖（可选）
│   ├── title.html
│   └── status.html
├── presets/                  ← 至少包含完整预设目录（必需，可为空清单但目录必须声明）
│   ├── neon-cyan.json
│   └── neon-magenta.json
└── assets/                   ← 完整模板依赖素材（必需，可为空清单但目录必须声明）
    ├── bg.png
    └── border-overlay.svg
```

### 5.2 manifest.json

```json
{
  "$schema": "https://desktop-up.app/schemas/full-theme-v1.json",
  "format_version": "1.0",
  "type": "full_theme",

  "meta": {
    "exported_at": "2026-08-03T12:00:00Z",
    "exported_by": "desktop-up",
    "source_template_key": "neon-hud",
    "source_template_version": "1.0.0",
    "label": "我的完整主题",
    "trust_required": true
  },

  "template": {
    "schema": "params-schema.json",
    "html": "template.html",
    "css": "template.css",
    "presets_directory": "presets/",
    "assets_directory": "assets/"
  },

  "depth3": {
    "css_override": "user.css",
    "html_slot_overrides": {
      "title": "slots/title.html",
      "status": "slots/status.html"
    },
    "scripts": []
  },

  "asset_mappings": {
    "bg": "assets/bg.png",
    "border-overlay": "assets/border-overlay.svg"
  },

  "asset_checksums": {
    "params-schema.json": "sha256:000...",
    "template.html": "sha256:001...",
    "template.css": "sha256:002...",
    "presets/neon-cyan.json": "sha256:003...",
    "user.css": "sha256:111...",
    "slots/title.html": "sha256:222...",
    "slots/status.html": "sha256:333...",
    "assets/bg.png": "sha256:444...",
    "assets/border-overlay.svg": "sha256:555..."
  }
}
```

### 5.3 规则

- 完整主题包必须同时携带并校验 `manifest.json`、`params-schema.json`、`template.html`、`template.css`、`presets/`、`assets/`；缺少任一必需组成、清单引用不闭合或校验和不匹配都拒绝安装
- `template.html` 与 `template.css` 只允许挂载到每张海报的 Shadow Root；不得注入宿主 document、控制中心或其他海报
- `meta.trust_required` 由导出时判断：如果模板 `manifest.json` 中 `depth3.script_allowed = true` 且包含脚本，则为 `true`
- `depth3.scripts` 数组列出脚本文件路径。当前 Ne​on HUD 和 Cinema Lightbox 均为空数组（内置模板不含脚本）
- `params.json`、`user.css` 和 `slots/` 是实例覆盖，可选；它们不能替代完整模板必需文件

---

## 6. 版本兼容性

### 6.1 格式版本

`format_version` 使用语义版本。导入器检查 MAJOR 版本：

| 格式版本 | 导入行为 |
|----------|---------|
| `1.x` | 正常导入，MINOR 版本差异不影响解析 |
| `2.x`（将来） | 拒绝导入，提示用户升级 Desktop-Up |
| 无 `format_version` | 尝试解析为 `format_version: "1.0"`（向后兼容），失败则拒绝 |

### 6.2 模板版本迁移

当导入的 `source_template_version` 与已安装模板版本不一致时：

```
1. 目标模板比源模板新 (installed > source)：
   - 导入成功
   - 新参数使用目标模板默认值
   - 已删除的参数从 params 中丢弃
   - 重命名的参数：目标模板 params-schema.json 可通过 "migrate_from" 字段声明兼容映射，否则丢弃

2. 目标模板比源模板旧 (installed < source)：
   - 警告用户版本不匹配
   - 允许导入，但在源模板中新增的参数被丢弃
   - 建议用户先更新模板

3. 目标模板不存在：
   - 导入纯参数快照 (`.params.json`) 时：提示用户需先安装对应模板
   - 导入素材包 (`.theme.zip`) 时：拒绝导入并提示其依赖的模板未安装
   - 导入完整主题包 (`.full.zip`) 时：校验包内完整模板后，将模板、索引和可选实例覆盖作为同一原子事务安装
```

### 6.3 参数迁移 schema 扩展

在 `params-schema.json` 中新增可选的 `migrations` 字段：

```json
{
  "migrations": [
    {
      "from_version": "1.0.0",
      "to_version": "1.1.0",
      "renamed": {
        "--tpl-old-key": "--tpl-new-key"
      },
      "removed": ["--tpl-deprecated-key"],
      "added": {
        "--tpl-new-feature": { "use_default": true }
      }
    }
  ]
}
```

---

## 7. 导入安全校验流程

### 7.1 解压前

1. 文件扩展名白名单：仅 `.params.json`、`.theme.zip`、`.full.zip`
2. 文件大小：`.params.json` ≤ 64KB，`.theme.zip` ≤ 20MB，`.full.zip` ≤ 20MB

### 7.2 解压与目录安全

```
1. 解压到 %TEMP%/desktop-up-import-{uuid}/
2. 所有路径归一化 → 必须在临时目录内（防路径穿越）
3. 拒绝任何 symlink/junction
4. 解压后总文件数 ≤ 100，总大小 ≤ 20MB
5. 单文件 ≤ 5MB
```

### 7.3 类型与内容安全

| 文件类型 | 校验 |
|----------|------|
| `.json` | JSON 解析成功，schema 验证通过 |
| `.css` | 扫描拦截 `url()` 含 `http/https` 和 `@import` |
| `.html` | 扫描拦截 `<script>`、`<iframe>`、`<object>`、`<embed>`、`on*` 属性 |
| `.png/.jpg/.webp` | 重编码去除 EXIF/元数据，限制输出 ≤ 2MB |
| `.svg` | 解析 XML → 移除 `<script>`、`<foreignObject>`、`on*` 事件属性 → 重新序列化 |
| `.ttf/.otf/.woff2` | 去除嵌入指令，限制 ≤ 5MB |
| 其他 | 拒绝 |

### 7.4 原子安装

```
全部文件校验通过
  → 在隔离暂存目录构造完整模板目录与可选 ThemeInstance
  → 生成 ChangePlan 预览 → 用户确认
  → 开启安装事务并记录旧模板索引、目录和引用快照
  → 原子发布 manifest / schema / template HTML / template CSS / presets / assets
  → 同一事务注册模板索引、素材、参数和 CSS/HTML 覆盖
  → 提交后清理暂存目录与旧快照

校验、复制、注册或提交任一步失败
  → 回滚模板目录、模板索引、素材注册、ThemeInstance 和海报引用
  → 清除临时目录
  → 列出全部失败项
  → 不留下半安装或半注册状态
```

---

## 8. 完整模板卸载

卸载完整模板前必须先枚举所有 `ThemeInstance`、海报和预设引用：

1. 生成迁移预览，将引用迁移到默认模板；默认模板不可用时迁移到 `minimal-fallback`；
2. 用户确认后，在同一事务中提交引用迁移并移除模板索引与模板目录；
3. 任一引用无法迁移、文件删除失败或事务提交失败时，恢复原引用和模板注册并拒绝卸载；
4. 禁止先删除模板文件再修复引用，禁止留下悬空 `package_id/package_version`。

## 9. 导出实现路径

导出由前端发起 → Rust 侧执行（文件系统操作不经过 WebView）：

```
前端: invoke('export_theme', { theme_instance_id, format })
  → Rust:
      1. 读取 ThemeInstance + PosterTemplate
      2. 构建 params.json（合并 default + overrides）
      3. 构建 manifest.json
      4. 复制素材文件引用到临时目录
      5. [.full.zip] 导出 params-schema.json、template.html、template.css、完整 presets/ 与 assets/，再附加可选 CSS/HTML 覆盖
      6. 校验完整文件清单与校验和后打包 zip
      7. 写入用户选择的路径（通过系统文件保存对话框）
      8. 返回文件路径给前端
```

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立三种导出格式的完整规范、版本兼容策略、参数迁移 schema、安全校验链 |
| 2026-08-04 | V1 对齐 | 完整主题包改为携带模板本体；增加 Shadow DOM 运行边界、原子安装/注册、失败回滚和模板卸载迁移 |

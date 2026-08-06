# Desktop-Up 海报视觉与交互重设计 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 重设计目标

V1 跨文档默认值与关键决策统一见 [`DESKTOPUP_V1_DESIGN_BASELINE.md`](DESKTOPUP_V1_DESIGN_BASELINE.md)。本文继续负责大幅面海报视觉与交互专项规格。

将海报从"小型可收纳悬浮窗"升级为"桌面级大幅面风格化展示面板"。核心改变：

1. 从等大图标网格 → 基于启动频率的对数动态缩放系统
2. 从 490×392 固定小窗 → 默认上下顶满、左右占比约 1/4 工作区的大型海报
3. 从单一深色视觉 → 三套首发模板：默认 `transparent-default`（暂名）以及两套可替换主题 Neon HUD、Cinema Lightbox
4. 根除反复出现的窗体顶部亮带

## 2. 图标动态缩放系统

### 2.1 启动记录

每项 `CollectionItem` 追踪：

```text
GameLaunchRecord
├─ item_id            ← 关联 CollectionItem
├─ total_launches     ← 累计启动次数
├─ last_launched_at   ← 最近一次启动时间戳
└─ launch_history[]   ← 最近 N 次启动时间戳（用于热度衰减）
```

### 2.2 对数映射算法（已确认）

为避免极端热门游戏把其他图标压到不可见，使用对数压缩。输入为海报内所有游戏的启动次数列表，输出为每个游戏的缩放比例：

```text
ratio = log(1 + game_launches) / log(1 + max_launches_in_poster)
scale = min_scale + (max_scale - min_scale) × ratio
```

示例（6 款游戏，min=50%, max=300%）：

| 游戏 | 启动次数 | log(1+n) | ratio | scale |
|------|----------|----------|-------|-------|
| AFTERLIGHT | 0 | 0.00 | 0% | 50% |
| GHOST SIGNAL | 2 | 1.10 | 25% | 112% |
| IRON VEIL | 5 | 1.79 | 41% | 152% |
| LAST ORBIT | 5 | 1.79 | 41% | 152% |
| SABLE RUN | 15 | 2.77 | 63% | 207% |
| HOLLOW CROWN | 80 | 4.39 | 100% | 300% |

备选模式：线性 (`game_launches / max_launches`) 和排名分布，用户在参数面板切换。

### 2.3 热度衰减（已确认）

`effective_launches` 的衰减计算以 [`POSTER_LAYOUT_AND_SORT_V0.1` §2.1](POSTER_LAYOUT_AND_SORT_V0.1.md) 的三因子衰减模型为权威来源：

```text
effective_launches = total_launches × decay_multiplier

decay_multiplier = clamp(
  existence_bonus × frequency_resistance × inactivity_penalty,
  0.1,
  1.0
)
```

其中：
- `existence_bonus`：新加入游戏的衰减免疫（30 天内不衰减）
- `frequency_resistance`：启动次数越多抵抗力越强（50 次满抗）
- `inactivity_penalty`：14 天缓冲期后每 7 天衰减 15%，下限 0.1

同一套 `effective_launches` 用于两个消费端，但缩放端加一层 sqrt 压缩以降低对衰减的敏感度：

```text
// 排名计算（直接用 effective_launches）
frequency_norm = log(1 + effective_launches) / log(1 + max_effective_in_poster)

// 图标缩放（套 sqrt 压缩，让大小变化比排名变化温和）
scale_input = sqrt(effective_launches × max_launches_in_poster)
ratio = log(1 + scale_input) / log(1 + max_launches_in_poster)
scale = min_scale + (max_scale - min_scale) × ratio
```

这确保：排名对衰减更敏感（精确排序），图标大小对衰减更温和（肉眼只需区分 3–5 档）。

### 2.4 动画过渡（已确认）

图标缩放变化在启动游戏后或展开海报时触发。过渡使用轻微弹性曲线：

```css
transition: transform 400ms cubic-bezier(0.34, 1.56, 0.64, 1);
```

海报展开时所有图标从收起态逐帧过渡到目标大小，分页或滚动中的离屏图标延迟到进入视口后再应用。

### 2.5 用户可调范围（已确认）

控制中心"自动排版"面板提供：

```text
图标动态缩放
├─ 最小比例    50% ────●───── 300%
├─ 最大比例    50% ──────●─── 300%
├─ 默认比例   150% ────●───── 300%
├─ 缩放曲线   [对数] | 线性 | 排名
└─ 热度衰减   [7天] | 14天 | 30天 | 关闭
```

修改即时预览，点保存后写入持久配置。所有自动调整均在撤销栈内。

## 3. 大幅面海报窗体

### 3.1 尺寸模型

从小型悬浮卡改为桌面级展示面板。默认约束：

```text
┌─────────────────────────────────────────────┐
│  桌面工作区 (例 2560 × 1392)                   │
│                                             │
│                               ┌──────────┐  │
│                     默认模式：  │          │  │
│  高度 = 工作区高 - 上下安全边距  │  海报    │  │
│          宽度 = 工作区宽的 22%  │          │  │
│                               │  游戏    │  │
│                     其他模式：  │  图标    │  │
│                    - 1/3 宽模式 │  网格    │  │
│                  - 自适应内容宽度│          │  │
│                - 用户手动拉伸宽高│          │  │
│                               │          │  │
│                               └──────────┘  │
│           桌面壁纸可见区                       │
└─────────────────────────────────────────────┘
```

| 参数 | 默认值 | 说明 |
|------|--------|------|
| 左右位置 | 主显示器右侧，`top-right` 锚定 | 右侧及上下保留工作区安全间距 |
| 宽度 | `max(工作区宽 × 0.22, 280px)` | 持久保存，拖动可调 |
| 高度 | 工作区高 - 上下各 12px | 默认近满高 |
| 最小宽度 | 240px | 至少 1 列图标 |
| 最大宽度 | 工作区宽 × 0.45 | 不超半屏，保留壁纸视野 |
| 最小高度 | 待 C-PCB/C-LST 契约冻结 | 展开态至少保留可读内容；收起态由标题条高度接管 |
| 最大高度 | 工作区高 - 上下各 12px | 不越出工作区 |
| 背景 | 模板决定：模糊 + 低不透明度叠加 | 壁纸可见但不抢夺图标焦点 |

海报框在 `EDITING + EXPANDED + 未锁定` 状态支持拉伸宽度与高度，并受上表最小/最大限制；锁定或收起态禁止拉伸。宽度与高度调整后均持久保存，最小/最大尺寸是显式契约，不作为 CSS 或窗口隐式行为兜底。

### 3.2 位置与锚点

与现有 `PosterPlacement` 兼容：锚点仍以显示器 + 归一化坐标存储。新增宽度持久化字段，重启后恢复海报宽度。

### 3.3 收起行为

大幅面下收起从"缩小为 392px 高小卡片"改为"折叠为标题条"（高度 ~60px），保持左右位置和宽度不变，展开后恢复。收起时不缩放图标，只隐藏图标区域。

## 4. 海报模板系统

### 4.1 架构

每张海报绑定一个模板。模板由具名插槽的 HTML 结构 + CSS 变量注入点组成，未来向主题开发者开放之前冻结模板 API 契约。当前阶段只实现内置模板。

```text
poster-template/
├─ transparent-default/  (默认透明低焦点模板，暂名)
│  ├─ template.html
│  ├─ template.css
│  └─ manifest.json
├─ neon-hud/
├─ cinema-lightbox/
├─ glass-gallery/        (后续)
├─ blueprint/            (后续)
├─ comic-panel/          (后续)
└─ minimal-shelf/        (后续)
```

### 4.2 首批模板

| 模板 | 角色 | 视觉特征 | 边框 | 背景 |
|------|------|---------|------|------|
| **Transparent Default**（暂名） | 默认模板 | 高透明、低装饰、低视觉焦点 | 极弱或无装饰边框 | 优先与壁纸融合 |
| **Neon HUD** | 可替换主题 | 赛博霓虹框、扫描线、数据流装饰、HUD 风格标题 | 霓虹发光 + 数据线 | 暗色 10% + 扫描线叠加 |
| **Cinema Lightbox** | 可替换主题 | 胶片齿孔边框、柔和发光轮廓线、电影排版标题 | 发光细线 + 胶片齿孔 | 暗色 8% + 柔光 |

### 4.3 模板参数面板

每个模板暴露可调参数在控制中心预览。通用参数：

```text
当前海报模板：[当前模板名]
├─ 强调色       [#72e4ff]  → 色板选择器
├─ 边框强度     [30%]  ──●───  100%
├─ 背景暗度     [8%]   ───●──  30%
├─ 背景模糊度   [24px] ──●───  48px
├─ 标题字号     [28px] ──●───  48px
├─ 图标间距     [12px] ──●───  32px
├─ 圆角大小     [18px] ──●───  48px
└─ 重置为模板默认值
```

模板专属参数由 `manifest.json` 声明。

## 5. 窗体顶部亮带根因与根治方案

### 5.1 诊断

经过三轮 Windows API 修复（已清除 `WS_CAPTION`/`WS_THICKFRAME`/`WS_SYSMENU`/`WS_MINIMIZEBOX`/`WS_MAXIMIZEBOX`、已禁用 `DWMNCRP_DISABLED`/`DWMWA_BORDER_COLOR`/`DWMWCP_DONOTROUND`、标题已空），仍残留约 10px 亮带。探针显示 `Style=0x94000000, HasCaption=false, DwmNc=0`，确认亮带来源不是 Windows 窗口管理器，而是 **WebView2 底层合成器的默认白色背景**。

### 5.2 根治方案（已确认）

在 WebView2 创建时显式设置 `DefaultBackgroundColor` 为全透明 `COLORREF(0,0,0,0)`。

实施路径：

1. **Rust 层**：在 Tauri `setup` 钩子中通过 WebView2 控制器调用 `ICoreWebView2Controller::put_DefaultBackgroundColor`，传入 `COREWEBVIEW2_COLOR { A: 0, R: 0, G: 0, B: 0 }`
2. **tauri.conf.json**：海报窗口配置继续保留 `"transparent": true`
3. **CSS 层**：`html, body, .poster-surface { background: transparent; }`

这三层必须全部正确才能彻底消除亮带。在大幅面海报重构时同步实施和验证。

## 6. 延后设计项

以下在基础功能稳定后再展开：

- **模板插槽完整 API 契约**（供主题开发者使用）：等基本样式和基础功能稳定后冻结
- **大小不等图标的网格排布算法**：已解决，见 [`POSTER_LAYOUT_AND_SORT_V0.1.md`](POSTER_LAYOUT_AND_SORT_V0.1.md)
- **海报从小型悬浮到大幅面的代码迁移**：之后直接重构，重新调整交付计划中的任务切片
- **参数面板自动生成与导入导出体系**：已解决，见 [`POSTER_THEME_CUSTOMIZATION_V0.1.md`](POSTER_THEME_CUSTOMIZATION_V0.1.md)

## 7. 对现有代码的影响

本次设计决定不会立即改变代码。以下方面将受影响：

- `poster-surface.js` 中硬编码的 `fallbackPoster(width: 490, height: 392)` 在大幅面重构中改为工作区感知尺寸
- `tauri.conf.json` 海报窗口 `width/height` 改为基于显示器工作区计算
- `placement.rs` 新增宽度持久化字段
- `styles.css` 海报视觉全部模板化
- 新增 `GameLaunchRecord` 数据模型和缩放计算引擎
- `desktop_host.rs` 中 `configure()` 增加 WebView2 背景色初始化逻辑

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 接受对数映射算法、热度衰减、动画过渡、50%-300% 用户可调范围、霓虹 HUD 和影院灯箱为首批模板、WebView2 DefaultBackgroundColor 为亮带根治方案 |
| 2026-08-03 | V0.1 | 确认四级风格化定制体系、参数面板自动生成、纯代码编辑器、文件导入导出、无原子 CSS 框架、Agent 共用 API；排版算法和锚定跳变已独立成文 |
| 2026-08-04 | V1 对齐 | 默认位置改为主屏右侧；冻结 Transparent Default + Neon HUD + Cinema Lightbox 三模板、默认解锁展开和总基线引用 |

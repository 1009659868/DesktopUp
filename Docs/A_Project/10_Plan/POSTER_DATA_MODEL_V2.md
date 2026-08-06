# Desktop-Up 海报数据模型 V2

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 目的

汇总三份重设计文档与现有 `TECHNICAL_ARCHITECTURE_V0.1` §5 中零散提到的数据字段，形成统一的 V2 数据模型。本模型是海报重构的数据层权威来源，后续实现和迁移均以此为准。

## 2. 模型概览

```text
PosterTemplate ──→ ThemeInstance ──→ Poster ──→ PosterItem ←── CollectionItem
                                         │                          │
                                    PosterPlacement            GameLaunchRecord
                                                              PosterItemPinned
```

| 模型 | 职责 | 新增/变更 |
|------|------|-----------|
| `PosterTemplate` | 内置或导入的模板定义 | **新增** |
| `ThemeInstance` | 模板 + 用户参数的具体实例 | **重写** |
| `Poster` | 一张海报的完整配置 | **扩大字段** |
| `PosterPlacement` | 海报的原生窗口位置持久化 | **扩大字段** |
| `PosterItem` | 海报与游戏的关联 | **扩大字段** |
| `CollectionItem` | 游戏/应用的启动和元数据 | **扩大字段** |
| `GameLaunchRecord` | 启动历史统计 | **新增** |
| `PosterItemPinned` | 锚定记录 | **新增** |

---

## 3. 模型定义

### 3.1 PosterTemplate（新增）

模板本身是文件系统中的目录，数据库只记录已安装模板的索引：

```text
PosterTemplate
├── id: String (UUID)                    ← 内部标识
├── template_key: String                 ← 目录名，如 "transparent-default", "neon-hud", "cinema-lightbox"
├── name: String                         ← 显示名，如 "Neon HUD"
├── version: String                      ← 语义版本，如 "1.0.0"
├── trust_level: "builtin" | "imported" | "trusted_script"
├── manifest_path: String                ← manifest.json 的模板目录内路径
├── params_schema_path: String           ← params-schema.json 路径
├── preview_thumbnail_path: String?      ← 预览图路径
├── installed_at: Timestamp
└── updated_at: Timestamp
```

V1 首发内置模板为 `transparent-default`、`neon-hud`、`cinema-lightbox`，三者 `trust_level = "builtin"`，不经过用户导入确认链，但运行时仍执行完整性与安全扫描。新海报的 `ThemeInstance` 默认绑定 `transparent-default`。

`minimal-fallback` 是宿主内置的最小安全回退渲染，不是默认模板、首发可选模板或普通 `PosterTemplate`。它不得与 `transparent-default` 合并，也不出现在常规模板选择器中。

### 3.2 ThemeInstance（重写）

原 `TECHNICAL_ARCHITECTURE_V0.1` §5 中 `ThemeInstance` 字段过于简略。V2 重写：

```text
ThemeInstance
├── id: String (UUID)                    ← 实例 ID
├── poster_id: String (UUID)             ← 所属海报（每海报一个实例）
├── template_id: String (UUID)           ← → PosterTemplate.id
├── parameter_overrides: JSON            ← 深度 1 参数覆盖，扁平 key-value
│     // {"--tpl-accent": "#ff6b6b", "--tpl-neon-intensity": 45, ...}
├── asset_overrides: JSON                ← 深度 2 素材替换槽位
│     // {"bg": "user-bg-abc123.png", "border-overlay": null, ...}
├── css_override: String?                ← 深度 3 用户 CSS 片段（原始文本）
├── html_slot_overrides: JSON?           ← 深度 3 插槽覆盖 {"title": "<html>...", ...}
├── active_preset_id: String?            ← 当前选中的深度 0 预设 ID（如有）
├── created_at: Timestamp
└── updated_at: Timestamp
```

约束：
- `parameter_overrides` 只存储用户**修改过**的参数；未修改的参数使用 `params-schema.json` 的 `default`
- `--tpl-*` 是模板 Shadow Root 内部参数命名约定，不是跨模板隔离手段；参数覆盖只注入所属海报的 Shadow Root
- `css_override` 在写入前经过 CSS 安全扫描（拦截 `url()` 含 `http/https`、`@import`），并且只能进入所属模板的 Shadow Root
- `html_slot_overrides` 在写入前经过 HTML 安全扫描（拦截 script、iframe、object），并且只能替换所属 Shadow Root 内允许的插槽

### 3.3 Poster（扩大）

原模型来自 `TECHNICAL_ARCHITECTURE_V0.1` §5，V2 新增字段：

```text
Poster
├── id: String (UUID)
├── title: String
├── monitor_identity: String             ← 所属显示器设备名；新海报默认 "primary"
├── anchor: "top-left" | "top-right" | "bottom-left" | "bottom-right" | "free"
│                                             新海报默认 "top-right"
├── normalized_rect: { x: f64, y: f64 }  ← 归一化坐标 (0.0–1.0)，由右上安全边距计算
├── width: u32                           ← [新增] 海报宽度 (px)，默认 max(工作区宽 × 0.22, 280px)
├── height: u32                          ← [新增] 海报高度 (px)，默认工作区高 - 上下安全边距
├── collapsed_state: "expanded" | "collapsed"  ← [变更] 呈现模式，默认 "expanded"
├── auto_collapse_enabled: bool          ← [新增] 自动收起开关，默认 false（即 auto_collapse=false）
├── auto_collapse_delay_seconds: u32     ← [新增] 自动收起延迟秒数，默认 3
├── is_locked: bool                      ← [新增] 锁定标记，默认 false（unlocked）
├── layout_kind: "grid" | "shelf" | "cards"  ← [暂未使用] A1 排列风格
├── theme_instance_id: String (UUID)     ← → ThemeInstance.id
├── z_group: u32                         ← [暂未使用] 多海报 Z 序组
├── created_at: Timestamp
└── updated_at: Timestamp
```

新建事务必须生成同一份 V1 默认快照：

```text
monitor_identity = "primary"
anchor = "top-right"
collapsed_state = "expanded"       // EXPANDED
interaction_mode = "using"         // USING，仅运行时，不持久化
runtime_mode = "active_static"     // ACTIVE_STATIC，仅运行时快照
is_locked = false                   // unlocked
auto_collapse_enabled = false       // auto_collapse=false
template_key = "transparent-default"
```

默认模板与 `minimal-fallback` 必须分离：正常创建始终绑定 `transparent-default`；只有模板缺失、损坏或无法安全渲染时才由宿主临时进入 `minimal-fallback`，且不得把 fallback 回写为用户选择。

字段变更说明：
- `collapsed_state`：原为枚举，现映射到状态机的呈现模式维度
- `width` / `height`：新增。海报框支持拉伸，尺寸均持久化；显示器变化时在最小/最大尺寸限制内恢复并避免越出工作区
- 最小/最大尺寸是 C-PCB/C-LST 冻结的显式契约常量，不作为单海报业务字段重复存储
- `is_locked`：新增。替代原来隐含在交互模式中的锁定概念
- `auto_collapse_*`：新增。替代原来 "自动隐藏" 伪状态
- 交互模式（USING/EDITING）不持久化——新建与启动总是 USING
- 运行时模式由宿主快照提供，不写入 `Poster`——新建默认 ACTIVE_STATIC，后续按主题与暂停策略转换

PosterPlacement（独立持久化文件，非数据库）：

```text
PosterPlacement (poster-placement-v1.json)
├── poster_id: String (UUID)
├── monitor_device_name: String
├── work_area_normalized: { x: f64, y: f64 }  ← 工作区归一化锚点
├── absolute_fallback: { x: i32, y: i32 }     ← 绝对像素回退
├── width: u32                                 ← [新增] 海报宽度
├── height: u32                                ← [新增] 海报高度
└── updated_at: Timestamp
```

### 3.4 PosterItem（扩大）

```text
PosterItem
├── poster_id: String (UUID)             → Poster.id
├── item_id: String (UUID)               → CollectionItem.id
├── order_key: u32                       ← [变更] 受排序引擎管理
├── pinned: bool                         ← [变更] 是否有锚定记录
├── page: u32?                           ← [暂未使用]
├── local_position: { x: u32, y: u32 }?  ← [暂未使用]
├── created_at: Timestamp
└── updated_at: Timestamp
```

当 `pinned = true` 时，`order_key` 仍然保留自动排序值，但渲染时锚定项插入 `PosterItemPinned.pinned_index` 指定位置。

### 3.5 CollectionItem（扩大）

原模型来自 `TECHNICAL_ARCHITECTURE_V0.1` §5。V2 新增字段：

```text
CollectionItem
├── id: String (UUID)
├── kind: "game" | "application" | "shortcut"
├── display_name: String
├── source_kind: "lnk" | "url" | "exe" | "manual"
├── source_identity: String              ← 目标路径或 URL
├── launch_target: String                ← 实际启动目标
├── launch_arguments: String?
├── working_directory: String?
├── icon_source: String?                 ← 图标路径或 Base64 引用
├── tags: JSON?                          ← 用户标签数组
├── managed_shortcut_id: String?         ← → ManagedShortcut.id（A1 启用）
├── ──────── V2 新增 ────────
├── total_launches: u32                  ← 累计启动次数，默认 0
├── last_launched_at: Timestamp?         ← 最近一次启动时间
├── launch_history: JSON                 ← 最近 N 次启动时间戳数组
├── added_at: Timestamp                  ← 加入海报的时间（用于 newness_norm）
├── burst_rank_score: f64?               ← 跳变后的 rank_score，None = 无跳变
├── burst_activated_at: Timestamp?       ← 跳变触发时间
├── burst_source_index: u32?             ← 跳变参照的邻居序号（诊断用）
├── burst_normal_launches_since: u32     ← 跳变后正常启动次数，默认 0
├── created_at: Timestamp
└── updated_at: Timestamp
```

`total_launches` 和 `last_launched_at` 由启动事件更新。`effective_launches` 是计算列，不持久化——由 `total_launches × decay_multiplier` 实时计算，公式见 `POSTER_LAYOUT_AND_SORT_V0.1` §2.1。

### 3.6 GameLaunchRecord（新增）

并非独立持久化模型——它是 `CollectionItem` 的启动统计视图，由 `total_launches` + `launch_history[]` + `last_launched_at` 三个字段承载。`launch_history` 保留最近 N 次启动时间戳（默认保留 50 条），用于未来的热度趋势可视化。

### 3.7 PosterItemPinned（新增）

```text
PosterItemPinned
├── id: String (UUID)
├── poster_id: String (UUID)             → Poster.id
├── item_id: String (UUID)               → CollectionItem.id
├── pinned_index: u32                    ← 1-based 绝对序号
├── pinned_at: Timestamp
└── pinned_by: "user" | "agent"
```

`pinned_index` 发生冲突时（两个锚定项争同一位），后固定的项自动偏移到 `pinned_index + 1`。对应游戏卸载后锚定记录保留 1 个月，之后自动清理。

---

## 4. 计算列（不持久化）

以下值由引擎在读取时实时计算，不进入数据库：

| 计算列 | 输入 | 公式 |
|--------|------|------|
| `effective_launches` | `total_launches`, `days_since_added`, `days_since_last_launch` | `total × clamp(existence × resistance × inactivity, 0.1, 1.0)` |
| `recency_norm` | `hours_since_last_launch` | `1 - min(hours / (7×24), 1.0)` |
| `frequency_norm` | `effective_launches`, `max_effective_in_poster` | `log(1+effective) / log(1+max_effective)` |
| `newness_norm` | `days_since_added`, `max_days_in_poster` | `1 - min(days / max(1, max_days), 1.0)` |
| `rank_score` | 以上三项 + 可能的 `burst_rank_score` | `0.40×recency + 0.35×frequency + 0.25×newness` |
| `burst_bonus` | `burst_rank_score - original_rank_score` | 每日衰减 50%，每次正常启动衰减 30% |
| `icon_scale` | `effective_launches`, `max_launches_in_poster`, `min_scale`, `max_scale` | `min + (max-min) × log(1+sqrt_input)/log(1+N)` |

---

## 5. 实体关系

```text
PosterTemplate  1 ──── N  ThemeInstance  1 ──── 1  Poster
                                                    │
                                                    │ 1
                                                    │
                                                    │ N
PosterItemPinned N ──── 1  PosterItem      N ──── 1  CollectionItem
                                     │                    │
                                     │ N                  │ 1 (A1)
                                     │                    │
                                   Poster (via poster_id) ManagedShortcut
```

关键约束：
- 一张海报有一个 `ThemeInstance`（模板 + 参数覆盖）
- 一张海报有 N 个 `PosterItem`（关联游戏）
- 一个 `CollectionItem` 可以出现在多张海报中（将来多海报场景）
- 一个 `CollectionItem` 在每张海报中可以有独立的锚定记录（`PosterItemPinned`）
- `GameLaunchRecord` 不是独立表——是 `CollectionItem` 的统计字段
- A0 阶段 `ManagedShortcut` 表存在但不使用（A1 才启用真实收纳）

---

## 6. 迁移路径（从当前 A0 状态）

当前 A0 使用 `localStorage` 中的 `GAME_CATALOG` 假数据和 `poster-surface.js` 中的硬编码状态。没有 SQLite 数据库或持久化 schema。

迁移策略：
1. **V2 schema 是全新创建**——不迁移任何现有持久化数据
2. 当前 `poster-placement-v1.json`（`placement.rs` 输出）保留，新增 `width` 字段向后兼容
3. 假数据 `GAME_CATALOG` 在 V2 中用 `CollectionItem` 的 seed 数据替代
4. `localStorage` 同步在 V2 中替换为 Rust 侧 SQLite + 前端通过 Tauri `invoke` 读写

无数据迁移风险——A0 没有真实用户数据。

---

## 7. 与现有设计文档的对应

| 文档 | 涉及模型 | 本模型对应 |
|------|---------|-----------|
| `TECHNICAL_ARCHITECTURE_V0.1` §5 | CollectionItem, ManagedShortcut, Poster, PosterItem, ThemeInstance | V2 扩大/重写 |
| `POSTER_REDESIGN_V0.1` §2.1 | GameLaunchRecord | 归入 CollectionItem 统计字段 |
| `POSTER_REDESIGN_V0.1` §3 | 海报尺寸、宽度持久化 | Poster.width, PosterPlacement.width |
| `POSTER_REDESIGN_V0.1` §4 | 模板系统 | PosterTemplate, ThemeInstance |
| `POSTER_LAYOUT_AND_SORT_V0.1` §1 | rank_score 因子 | 计算列 |
| `POSTER_LAYOUT_AND_SORT_V0.1` §2 | effective_launches 衰减 | 计算列 |
| `POSTER_LAYOUT_AND_SORT_V0.1` §4 | 锚定 | PosterItemPinned, PosterItem.pinned |
| `POSTER_LAYOUT_AND_SORT_V0.1` §5 | 跳变 | CollectionItem.burst_* |
| `POSTER_STATE_MACHINE_V0.1` | 状态字段 | Poster.collapsed_state, is_locked, auto_collapse_* |
| `POSTER_THEME_CUSTOMIZATION_V0.1` §2 | 参数/素材/代码覆盖 | ThemeInstance |
| `POSTER_THEME_CUSTOMIZATION_V0.1` §6 | 模板文件结构 | PosterTemplate |

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 汇总 V2 数据模型：8 个模型 + 计算列 + ER 关系 + 迁移路径 |
| 2026-08-04 | V1 修订 | 同步 `primary/top-right/EXPANDED/USING/ACTIVE_STATIC/unlocked`、`auto_collapse=false` 与 `transparent-default` 默认值；明确三套首发模板、独立安全 fallback 和 Shadow Root 内部参数边界 |

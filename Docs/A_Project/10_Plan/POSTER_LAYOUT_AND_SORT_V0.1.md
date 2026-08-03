# Desktop-Up 海报图标排布与排序系统 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-03

## 1. 复合排序得分模型

### 1.1 得分公式

三项因子加权合成排名分数，决定图标在海报中的显示顺序和缩放比例：

```text
rank_score = 0.40 × recency_norm + 0.35 × frequency_norm + 0.25 × newness_norm
               ↑ 近期活跃              ↑ 总启动次数有效值        ↑ 新增时间
```

| 权重 | 含义 | 设计意图 |
|------|------|---------|
| 0.40 | 近期活跃 | 昨天玩过的游戏最可能今天继续玩——这是"启动器"的核心价值 |
| 0.35 | 总启动有效值 | 沉淀热度，不会被短期波动推翻 |
| 0.25 | 新增时间 | 保证新游戏在一段时间内获得曝光，不会沉底 |

### 1.2 因子归一化

每项归一化到 0–1：

```text
recency_norm   = 1 - min(hours_since_last_launch / (7 × 24), 1.0)
                 最近 7 天内活跃即得高分，超过 7 天线性降为 0

frequency_norm = log(1 + effective_launches) / log(1 + max_effective_in_poster)
                 使用有效启动次数而非原始次数，已考虑衰减

newness_norm   = 1 - min(days_since_added / max(1, max_days_since_added_in_poster), 1.0)
                 与该海报中最老项的年龄对比
```

新增项天然获得 `newness_norm ≈ 1.0`，在顶部停留约 1–2 周，随海报中最老项的时间跨度自然回落。

### 1.3 刷新节奏

| 触发时机 | 行为 |
|----------|------|
| 应用冷启动 | 启动后 3 秒内完成首次评估，不阻塞桌面显示 |
| 游戏启动后 | 不立即重排（避免"刚点的游戏跳到前面"）。标记下次展开时重排 |
| 海报展开 | 如有待重排标记则重新计算排序 |
| 手动刷新 | 托盘"重新评估活跃度"或控制中心一键刷新 |
| 定时刷新 | 不设后台定时器——静态桌面原则不变 |

---

## 2. 活跃度衰减系统

衰减作用于 `effective_launches`，是该项计算的**权威来源**。排名计算和图标缩放都从同一个 `effective_launches` 取值，但消费端的敏感度不同（见 `POSTER_REDESIGN_V0.1` §2.3）。

### 2.1 衰减公式（权威定义）

```text
effective_launches = total_launches × decay_multiplier

decay_multiplier = clamp(
  existence_bonus × frequency_resistance × inactivity_penalty,
  0.1,                                          ← 下限：不会衰减到原始启动次数的 10% 以下
  1.0                                           ← 上限：不衰减时保持原值
)
```

三个子因子各自独立变化：

```text
existence_bonus (存在时间保护):
  新加入的游戏对衰减有免疫力
  bonus = min(1.0, 30 / max(days_since_added, 1))

  1 天: 1.0  |  7 天: 1.0  |  30 天: 1.0  |  60 天: 0.5  |  180 天: 0.17

frequency_resistance (总启动次数抵抗力):
  启动次数越多，衰减越慢——忠实玩家的常玩游戏不掉队
  resistance = min(1.0, log(1 + total_launches) / log(1 + 50))

  0 次: 0.0  |  3 次: 0.29  |  10 次: 0.61  |  50 次: 1.0

inactivity_penalty (长期不启动惩罚):
  14 天缓冲期，之后开始加速衰减
  penalty = 0.85^(max(0, days_since_last_launch - 14) / 7)

  14 天内: 1.0  |  21 天: 0.85  |  28 天: 0.72  |  42 天: 0.52  |  60 天: 0.37
```

### 2.2 典型场景衰减示例

| 场景 | existence | resistance | inactivity | 最终 multiplier | 说明 |
|------|-----------|------------|------------|-----------------|------|
| 刚加 3 天，启动 2 次，昨天玩过 | 1.0 | 0.38 | 1.0 | **1.0** | 新房保护 + 无衰减 |
| 存在 90 天，启动 80 次，昨天玩过 | 0.33 | 1.0 | 1.0 | **1.0** | frequency 完全抵抗 |
| 存在 90 天，启动 80 次，50 天没玩 | 0.33 | 1.0 | 0.37 | **0.37** | 长期不玩开始滑坡 |
| 存在 90 天，启动 3 次，50 天没玩 | 0.33 | 0.29 | 0.37 | **0.10** | 触及下限，垫底 |
| 用户手动刷新该项 | 1.0 | 不变 | 1.0 | **max(原值, resistance)** | 回到抗衰减水准 |

### 2.3 手动刷新

用户可以单独对某个项目刷新活跃度（右键 → "刷新状态"），或通过托盘"重新评估全部活跃度"批量刷新。

刷新效果：
- `existence_bonus` 重置为 1.0
- `inactivity_penalty` 重置为 1.0
- `frequency_resistance` 不变（基于真实启动数据，刷新不会伪造）
- 下次排序中该项排名回升，但图标大小不会超过其真实启动次数对应的缩放比例

---

## 3. 排布算法：行式弹性流 + 子列填充

### 3.1 基础算法

按排序顺序从高到低放置图标。每行从左到右流式填充，超出容器宽度的图标换行进入下一行。

```text
输入：
  items: [{id, scale, rank_score, pinned_index?}]
  container_width: number
  base_icon_size: number    ← 100% 对应的 CSS 像素（默认 64px）
  gap: number               ← 图标间距（默认 12px）

过程：
  rows = []
  current_row = []

  for each item in items (按 rank_score 降序，锚定项插入指定序号):
    icon_w = base_icon_size × item.scale
    icon_h = base_icon_size × item.scale

    if current_row 为空:
      current_row.push(item)
      current_row.used_width = icon_w
      current_row.max_height = icon_h
    else if current_row.used_width + gap + icon_w <= container_width:
      current_row.push(item)
      current_row.used_width += gap + icon_w
      current_row.max_height = max(current_row.max_height, icon_h)
    else:
      // 尝试子列填充，见 3.2
      commit_and_fill_sub_columns(current_row, rows, remaining_items, container_width)
      current_row = [item]
      current_row.used_width = icon_w
      current_row.max_height = icon_h

  commit_and_fill_sub_columns(current_row, rows, [], container_width)
```

### 3.2 子列填充

当一行无法容纳下一个图标时，并非立即换行——先检查该行右侧剩余空间是否足够容纳小图标：

```text
commit_and_fill_sub_columns(row, rows, remaining, container_width):
  remaining_w = container_width - row.used_width - gap

  if remaining_w >= base_icon_size × min_scale (至少 32px):
    sub_column_w = remaining_w
    sub_y = 0

    for each small_item in remaining（按排序顺序，scale ≤ 100% 优先）:
      small_w = base_icon_size × small_item.scale
      small_h = base_icon_size × small_item.scale

      if sub_y + small_h <= row.max_height:
        将 small_item 放入子列，位置 = (row.used_width + gap, sub_y)
        sub_y += small_h + gap
        从 remaining 中移除 small_item
      else if sub_y + gap + small_h <= row.max_height + 20%（允许轻微超出行高）:
        放入并轻微增加行高

  row 的高度 = max(row.max_height, sub_y)
  将 row 加入 rows
```

子列中的小图标按排序顺序从上到下堆叠。如果该行右侧有 204px 剩余宽度，可以垂直放置 2–3 个 50%–80% 的图标。

### 3.3 视觉效果

在约 420px 宽的默认海报中：

```
┌──────────────────────────┐
│ ┌──────────┐ ┌──┐ ┌──┐  │  ← 300% (192px) + 子列 50% (32px) × 3
│ │ HOLLOW   │ │AL│ │GS│  │     行高 = 192px
│ │ CROWN    │ ├──┤ ├──┤  │     子列内部间距 8px
│ │   80次   │ │SR│ │LO│  │
│ │          │ ├──┤ │  │  │
│ │          │ │IV│ │  │  │
│ └──────────┘ └──┘ └──┘  │
│                          │
│ ┌──────┐ ┌──────┐ ┌───┐  │  ← 152% (97px) × 2 + 60% (38px)
│ │SABLE │ │IRON  │ │HC │  │     60px 图标行内底部对齐
│ └──────┘ └──────┘ └───┘  │
│                          │
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ │  ← 底部密集小图标区 (50%–80%)
│ └───┘ └───┘ └───┘ └───┘ │
└──────────────────────────┘
```

上半部 1–3 行有热门大图标 + 子列小图标，下半部是密集小图标网格。这是"海报"而非"数据表"的视觉语言。

---

## 4. 三层排序控制

### 4.1 层级定义

```text
第 1 层：全自动（默认）
  所有图标按 rank_score 降序排列
  用户不干预
  → 新安装默认此层，80% 用户停留在此

第 2 层：锚定 + 自动
  用户对某项执行"固定到此位置"后进入此层
  锚定项在指定序号不动
  非锚定项在锚定之间的空隙中按 rank_score 自动排序
  → 用户偶尔固定几个关键项即可

第 3 层：全手动
  用户显式进入"手动整理模式"
  此时自动排序完全暂停
  通过拖拽自由排列
  → 完美主义用户偶尔使用
```

层级切换：第 1 层 → 第 2 层（用户首次锚定某项时自动进入）。第 2 层 → 第 3 层（用户在控制中心点击"进入手动整理模式"）。第 3 层 → 第 1 层（用户点击"恢复自动排序"，清除所有锚定）。

### 4.2 锚定机制

锚定项持久记录：

```text
PosterItemPinned
├─ poster_id
├─ item_id
├─ pinned_index: number      ← 锚定的绝对序号，1-based
├─ pinned_at: timestamp      ← 锚定时间
└─ pinned_by: "user" | "agent"
```

锚定行为：

| 操作 | 结果 |
|------|------|
| 用户右键图标 → "固定到当前位置" | 该项在此序号锚定 |
| 用户拖拽锚定项到新位置 | 更新 `pinned_index` 为新序号 |
| 用户右键锚定项 → "解除固定" | 删除锚定，该项恢复自动排序 |
| 锚定项对应的游戏已卸载 | 保留锚定记录但跳过（空洞由自动项填充），1 个月后自动清理 |
| 两个锚定项争同一序号 | 后固定的项偏移到序号 + 1 |
| 重置全部排序 | 清除所有锚定，回到全自动 |

### 4.3 锚定视觉标记

锚定项在角标处显示**浅金色小图钉**（区别于自动排序项的无标记状态），悬停时显示"已固定在第 N 位 · 点击解除"。

图钉标记设计：

```
┌──────────┐
│ HOLLOW   │◈  ← 浅金色图钉图标，12px，右上角
│ CROWN    │    不透明度 60%，不抢夺游戏图标焦点
│          │    悬停时变为 100% + tooltip
└──────────┘

┌──────┐
│SABLE │     ← 无图钉 = 自动排序项
└──────┘
```

"解除固定"的交互入口：
1. 右键锚定项 → "解除固定"
2. 点击图钉图标 → 弹出确认 tooltip → 点击"解除"
3. 控制中心排序面板 → 锚定项列表 → 逐项移除

### 4.4 第 2 层排序示例

用户固定 HOLLOW CROWN 在 1 号位，GHOST SIGNAL 在 4 号位：

```text
位 1: HOLLOW CROWN       ← 锚定（图钉标记）
位 2: [rank_score 最高]  ← 自动
位 3: [rank_score 次高]  ← 自动
位 4: GHOST SIGNAL       ← 锚定（图钉标记）
位 5: [rank_score 第 4]  ← 自动
...
```

---

## 5. 拖拽触发式活跃度跳变

### 5.1 触发条件

当用户**同时满足**以下三个条件时，触发跳变：

1. 该项当前排在自动排序结果的下半区（底部 50%）
2. 用户手动将该图标拖拽到上半区（顶部 50%）并锚定
3. 用户在拖拽后的 **5 分钟内** 点击启动了该游戏

三个条件缺一不可——跳变不是"拖了就变"，而是"拖了 + 玩了"才变。这确保跳变是由用户的实际使用行为触发的，而非纯 UI 操作。

### 5.2 跳变计算

以目标位置（新锚定位置）为参照，将该游戏的活跃度临时提升到与**被挤下去的相邻位置**的项持平：

```text
burst_target_index = pinned_index + 1    ← 即被挤下去的下一项（原本的锚定位置邻居）

burst_recency   = items[burst_target_index].recency_norm
burst_frequency = items[burst_target_index].frequency_norm

该游戏的 burst_rank_score = 0.40 × burst_recency + 0.35 × burst_frequency + 0.25 × 1.0
                              ↑ 取邻居值                         ↑ newness 保持 1.0（刚被"发现"）
```

举例：用户把垫底的 AFTERLIGHT（启动 0 次，存在 90 天未启动）拖到第 3 位并立即启动：

```text
原来第 4 位（burst_target）的 SABLE RUN：
  recency = 0.85, frequency = 0.82
  → burst_rank_score = 0.40 × 0.85 + 0.35 × 0.82 + 0.25 × 1.0 = 0.877

AFTERLIGHT 的原始 rank_score ≈ 0.01
AFTERLIGHT 的 burst_rank_score = 0.877   ← 跳变后排名升至与 SABLE RUN 相当
```

跳变只影响 `rank_score`（排序用），不改变 `total_launches`（真实启动次数）。图标缩放仍然基于真实启动次数的对数映射——**不会因为一次跳变就让一个 0 次启动的游戏图标突然变成 300%**。

### 5.3 跳变衰减

跳变获得的额外分数按以下速率衰减，比正常衰减快得多：

```text
burst_bonus = burst_rank_score - original_rank_score          ← 跳变额外分

effective_rank_score = original_rank_score + burst_bonus × burst_multiplier

burst_multiplier = 0.5^(days_since_burst / 1)                 ← 每天衰减 50%

第 0 天: 1.0  → 第 1 天: 0.5  → 第 2 天: 0.25  → 第 3 天: 0.125  → 第 7 天: ≈ 0
```

跳变在约 3 天内基本消退，5 天后残留分数趋近于零，该项回到其真实活跃度对应的排名。

### 5.4 跳变归一化

每次**正常启动**（非跳变触发的那次）都会减少对跳变的依赖，加速回归真实热度：

```text
每次正常启动：
  burst_multiplier = burst_multiplier × 0.7     ← 每次正常启动衰减 30%

同时：
  total_launches += 1                           ← 真实数据积累
  recency 更新为当前时间                          ← 近期活跃因子上升

当 original_rank_score ≥ burst_rank_score 时：  ← 真实热度已追上跳变值
  burst_bonus = 0                                ← 跳变结束，完全由真实数据驱动
```

这意味着：
- 用户拖拽 + 启动 → 跳变给游戏曝光机会
- 如果用户继续频繁玩 → 真实数据快速追上，跳变自动消失
- 如果用户拖了但不再玩 → 跳变 3 天内消退，回归垫底

### 5.5 跳变可见性

跳变期间，被跳变的图标在角标处显示一个**小型上升箭头动画**（单次播放 600ms，不循环），与锚定的图钉标记共存时位于图钉左侧。悬停 tooltip 显示"活跃度临时提升 · 剩余约 N 天"。

不显示具体跳变天数——避免制造"再过几天就要掉了"的焦虑。只在此项真正回到自动排名后出现一次静默提示"已回归自然排序"。

### 5.6 数据模型补充

```text
CollectionItem 新增字段：
├─ burst_rank_score: Option<f64>        ← 跳变后的 rank_score，None 表示无跳变
├─ burst_activated_at: Option<Timestamp>
├─ burst_source_index: Option<usize>    ← 跳变参照的邻居序号（用于诊断）
├─ burst_normal_launches_since: usize   ← 跳变后的正常启动次数（用于加速归一化）
└─ pinned_index: Option<usize>          ← 锚定序号
```

---

## 6. 边界情况

| 情况 | 处理 |
|------|------|
| 子列放不下任何图标（剩余宽度 < 32px） | 换行，不放子列 |
| 子列图标总高度超过行高 20% | 截断子列，剩余图标继续在下一行主流中放置 |
| 所有图标等大且满行 | 退化为标准网格，子列逻辑不激活 |
| 锚定项 + 跳变项争同一位 | 锚定项优先，跳变项偏移到下一个自动位 |
| 海报宽度调整 | 重新流式排布，锚定序号不变，子列重新计算 |
| 收起态（折叠 60px 标题条） | 图标区域隐藏，排序和跳变数据照常维护 |
| 多个项同时被拖拽跳变 | 每个跳变独立计算 burst_target，互不干扰 |
| 跳变期间用户手动再次拖拽 | 取消上次跳变（视为用户重新排序意图），新位置启动新的跳变评估 |

---

## 7. 实施顺序建议

| 切片 | 内容 | 依赖 |
|------|------|------|
| SORT-1 | `GameLaunchRecord` 数据模型、rank_score 计算、有效启动次数 | A1 数据层就绪后 |
| SORT-2 | 行式弹性流 + 子列填充布局引擎 | SORT-1 + 大幅面重构完成 |
| SORT-3 | 三层排序控制 + 锚定机制 + 图钉标记 | SORT-2 |
| SORT-4 | 拖拽触发跳变 + 衰减 + 归一化 | SORT-3 |

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-03 | V0.1 | 建立排序得分、活跃度衰减、行式弹性流子列填充、三层控制、锚定视觉标记和拖拽跳变机制 |

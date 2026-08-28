# QTS-04-04 R4：Item 连续视口与独立滚动入口

状态：`HISTORICAL R4 / USER FOUND FOOTER CLIP LEAK + REFLOW FLASH / R4.1 ACTIVE`

日期：2026-08-28

## 1. 用户反馈与结论

R3 人工观察确认两个相互关联的缺陷：

1. 当 Item 只有不足一排高度落入 viewport 时，整排会突然消失并留下大块空位，滚动呈按行跳变；
2. 滚轮只在可见 Item island 上生效。一旦错误可见性计算使全部 Item 消失，就不存在任何滚轮入口，用户无法把内容滚回。

这不是滚动步长调优问题，而是 R3 对 V1 overflow 语义的实现错误。

## 2. 根因

### 2.1 整排跳失

R3 使用 `itemViewportBounds.contains(completeItemBounds)` 决定可见性。卡片只要有一个像素越过 viewport，就同时失去 visual 与 hit geometry；因此连续位移被量化成“整张出现/整张消失”。

### 2.2 滚动死锁

R3 仅给可见 Item HWND 派发 `WM_MOUSEWHEEL`。当所有 Item 被完整包含条件排除时，滚动入口也随之归零；视觉状态和恢复入口形成循环依赖。

## 3. R4 正式模型

### 3.1 连续 viewport 裁切

- 全部 Item 的完整布局几何继续保留；
- 可见性改为 Item 与 viewport 的矩形交集；只要存在非空交集，就绘制该交集并发布对应的裁切命中区；
- QPainter 继续以 item viewport 作为 clip，卡片从上下边界逐像素进入或退出；
- 部分可见是正常滚动状态，不等于卡片布局损坏；
- Item 不得越过 viewport 绘制到标题或页脚，也不得在不可见像素上接收输入。

### 3.2 连续滚动

- 标准滚轮一格移动 48 DIP；高分辨率 wheel delta 按比例产生子步长；
- offset 在 `0..maxItemScrollOffset` 内连续钳制，不再对齐完整行；
- resize 后 accepted layout 的钳制 offset 回写 Runtime scene，避免旧的越界 offset 在后续尺寸变化时复活。

### 3.3 独立且最小的滚动入口

- 新增稳定 `ItemScroll` 语义角色和固定 RegionId 150；
- overflow 存在时，仅在右侧滚动条周围发布 12 DIP 宽的纵向 input island；
- 即使当前没有完整 Item，这个 island 仍能接收滚轮并恢复内容；
- island 在无 overflow 时只隐藏，不改变 region 身份集合，因此不得中断进行中的 resize capture；
- 不创建覆盖整个内容区的输入包络；除 Item 可见交集、标题/按钮、resize 边缘和细滚动条外，海报空白继续穿透 Explorer。

## 4. 自动证据

Release 构建通过；全量 CTest `13/13 PASS`。

新增/修订断言：

1. 40 DIP 中间 offset 下，首行只显示与 viewport 相交的部分，不整排消失；
2. 任一中间 offset 均至少保留可见 Item 交集，overflow 时滚动条 visual/input bounds 始终存在；
3. ItemScroll island 能独立派发带 delta 的 Wheel 事件；
4. Item island 仍能派发 Wheel，最后一行仍可到达；
5. disabled/enabled Item 变化不替换已捕获 resize HWND；
6. 空白穿透、透明边界、Host/Shell/display、StateStore、IPC 与退出夹具无回退。

## 5. 与 QTS-06 的边界

R4 只冻结当前单海报 fixture 的视口、offset 和输入承载方式。真实 Item Repository、零/一/N/200 Item、活跃度、动态尺寸、排序/锚定、滚动状态持久化、触控/键盘/无障碍入口仍属于 QTS-06。

## 6. 用户 R4 门

1. 缩成两列并逐渐降低高度：临界处卡片应从 viewport 边缘连续裁切，不得整排瞬间消失或留下异常大空位；
2. 在任意可见卡片上滚动，内容应连续移动并可到达第 1～6 个 Item；
3. 将鼠标放在右侧细滚动条的任意位置滚动，即使画面中没有完整卡片，也必须能恢复 Item；
4. 滚动中 Item 不得越过标题区或页脚；只允许 viewport 边缘裁切，不得出现布局重叠或不可见区域点击；
5. 除细滚动条自身外，卡片之间和下方空白仍应支持 Windows 桌面框选与右键菜单；
6. 按住右边缘跨越 3 列/2 列边界不丢捕获，锁定、收起/展开和标题拖动无回退。

已通过的 Win+D、Explorer 重建、桌面刷新、文件选择器、显示变化和长时间标题拖动矩阵无需重复。
## 7. R4 人工失败记录

用户复测发现：部分 Item 的彩色斜带穿过 viewport 底边，以及跨列/行重排时 visual 闪烁。前者已由 R4.1 的 `IntersectClip` 和像素回归修复；后者仍为 OPEN。当前权威入口移至 `QTS_04_04_R4_1_CLIP_AND_COMPONENT_AUDIT_2026-08-28.md`。

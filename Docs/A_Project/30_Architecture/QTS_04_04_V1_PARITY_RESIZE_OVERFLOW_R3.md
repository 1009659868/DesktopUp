# QTS-04-04 R3：V1 连续缩放与 Item 溢出对齐

状态：`HISTORICAL R3 / CAPTURE FIX RETAINED / ROW-SNAPPED SCROLL SUPERSEDED BY R4`

日期：2026-08-28

> R3 的稳定 RegionId、island HWND 身份和连续 resize capture 修复继续有效；第 3.2 节“仅完整 Item、按整行滚动、Item-only wheel”已被 `QTS_04_04_CONTINUOUS_ITEM_VIEWPORT_R4.md` 取代。

## 1. 用户反馈

R2 后出现两个真实回归：

1. 按住右边缘左右缩放时，只要响应布局从三列切为两列或反向切换，当前拖动立即失效，必须松开并重新按住；
2. 外框高度不足时，R2 直接删除未完整落入 viewport 的 Item visual/hit geometry，导致六个 fixture Item 中一部分没有任何可达入口。

用户要求继续采用已冻结的 FrameGeometry / ContentMetrics 分权，但把缩放事务和内容溢出完整对齐 V1，而不是再建立新逻辑。

## 2. 根因

### 2.1 捕获中断

R2 的 `inputProjection` 只提交当前可见 Item。列数/行数变化会改变启用的 Item region 数量；`Win32InputIslandSet` 因集合身份不同执行整组 replace，销毁了仍持有 `SetCapture` 的 resize island HWND。Windows 随即发送 `WM_CAPTURECHANGED`，`GeometryGestureSession` 被取消。

因此根因不是 QPainter 重排速度，也不是 `GeometryGestureSession` 的宽度公式，而是一次连续手势中错误重建了输入所有者。

### 2.2 Item 丢失

R2 把“当前不完整可见”错误实现成“无布局几何、无后续访问路径”。V1 的实际语义是所有 Item 始终保留在 grid 中，内容视口负责溢出；外框 resize 不删除 Item。

## 3. R3 正式映射

### 3.1 连续几何事务

- 一个 `Pressed` 到 `Released/CaptureLost` 是不可分割的 geometry gesture；
- Item 启用/隐藏变化不得替换当前 resize/title HWND；
- 每个已声明 RegionId 保留稳定 island HWND；不可见 Item 的 island 只隐藏，不接收输入；
- 同一 region 身份集合只原地更新 bounds、enabled 和 z-order；
- 当前鼠标捕获必须跨三列/两列边界持续到用户松键。

### 3.2 完整 Item 流与溢出

- 六个 NIGHT OPS fixture Item 的 card/label 布局几何始终计算并保留；
- 当前 viewport 只绘制和命中完整落入视口的 Item，禁止半卡、页脚遮挡和不可见命中；
- 内容高度不足时产生纵向 overflow range 和可见滚动条；
- 滚轮只由光标下的可见 Item island 接收，空白覆盖区继续穿透到桌面；
- 每个滚轮步长按完整 Item 行推进；顶部、行边界和底部均钳制，最后一行必须可达；
- resize 只改变列/行/overflow，不改写 deviceScale、contentScale 或 Item 基准尺寸。

## 4. 与 QTS-06 的边界

R3 只补齐当前六项 fixture 的最小 V1 溢出承载，防止 QTS-04 交互门建立在“删除 Item”上。以下能力仍属于 QTS-06：

- 真实 Item Repository、零/一/N/200 Item；
- 活跃度、动态尺寸、排序、锚定和人工布局；
- 正式分页器、页码、滚动状态持久化及控制中心布局参数；
- 键盘、触控和无障碍滚动入口。

## 5. 自动证据

Release 构建通过；全量 CTest `13/13 PASS`。

新增/修订断言：

1. 320×420 仍计算六个 Item 的 2×3 完整流，并产生 overflow range；
2. 滚到底后第 5、6 个 Item 完整可见，不删除其布局几何；
3. visible Item island 能派发带 delta 的 `Wheel` 语义；
4. Item 从 disabled 变 enabled 的同一帧，已捕获的 resize HWND 身份与 `GetCapture()` 均保持；
5. 透明边界、空白穿透、Host、Shell/display lifecycle、StateStore、IPC 和退出夹具无回退。

## 6. 用户 R3 门

1. 按住右边缘来回跨越三列/两列边界至少 10 次，全程不能松键，外框必须持续跟随；
2. 缩窄并保持高度不足时，六个 Item 不得消失：在任意可见卡片上向下滚轮，应能看到最后一行，再向上回到第一行；
3. 任一滚动位置不得出现半张卡片、Item/页脚重叠或不可见 Item 点击；
4. 海报空白处左拖框选、右键桌面菜单继续通过；
5. 松开后再次缩放、标题拖动、锁定和收起/展开仍正常。

已通过的 Win+D、Explorer 重建、桌面刷新、文件选择器、显示变化和长时间标题拖动矩阵不要求重复。
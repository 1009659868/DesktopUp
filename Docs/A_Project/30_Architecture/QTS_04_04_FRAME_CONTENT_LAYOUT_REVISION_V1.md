# QTS-04-04 R2 外框与内容布局分离修订 V1

状态：`IMPLEMENTED / HISTORICAL R2 / SUPERSEDED BY R3 CAPTURE + R4 VIEWPORT`

日期：2026-08-28

> 当前权威修订为 `QTS_04_04_CONTINUOUS_ITEM_VIEWPORT_R4.md`。R2 的 frame/content 分权仍有效；R3 保留稳定 capture 修复，隐藏未完整 Item、按整行滚动和 Item-only wheel 已被 R4 替换。

## 1. 用户决定与被替换假设

用户确认正式采用“外框几何、内容参数、响应式排版分离”模型。本修订替换 QTS-04-02 中“以画布宽度计算整体响应式比例”的实现假设，但不撤销 QTS-04-02 已通过的透明像素、圆角、白条/灰框和 buffer 生命周期证据。

旧实现使用 `unit = frameWidth / 420`，因此横向放大且纵向压缩时，标题、间距、卡片高度和页脚尺度同时增大，最终造成 Item 遮挡、半卡裁切和命中范围错误。问题不是 Qt 自身能力，也不是 DesktopHost 层级，而是布局比例的所有权错误。

## 2. 正式模型

| 层 | 权威输入 | resize 时允许变化 | resize 时禁止变化 |
| --- | --- | --- | --- |
| FrameGeometry | left/top/width/height、anchor、locked、presentation | frame viewport、工作区约束 | ContentMetrics、Item scale |
| ContentMetrics | deviceScale、contentScale、标题/卡片/图标/gap/density 参数 | 仅 DPI 或显式控制中心/主题命令 | 从 frame width/height 反推 |
| ResponsiveLayout | viewport + ContentMetrics + Item 顺序 | columns、rows、page/scroll、完整可见容量 | Item 重叠、半卡、越出页脚 |

`deviceScale` 只来自 Windows effective DPI；`contentScale` 是独立内容参数，当前 fixture 默认为 1.0，后续由 QTS-05/QTS-07 的状态与主题参数接管。同一 DPI 下改变海报外框，不得改变二者。

## 3. 当前 P0 行为

1. NIGHT OPS 标题、字体、卡片、图标、间距和页脚使用固定 ContentMetrics；
2. 背景、圆角材料范围、标题栏横线、右上按钮锚点和页脚锚点可随 frame 伸展；
3. 固定卡片根据可用 viewport 换列/换行，最多使用六列；
4. 只发布完整落入 Item viewport 的卡片和标签；不完整项的 visual rect、label rect 和 HitRegion 均为空/缺席；
5. 当前六项仍是视觉夹具。空间不足时未显示项保留在 scene 数据中；正式分页/滚动、页码和 0/1/N Item 属于 QTS-06，不在本修订伪造；
6. 收起高度固定 60 DIP，只随 DPI 投影，不再随 frame width 改变。

## 4. DPI 与逻辑尺寸

- 96 DPI：默认 ContentMetrics scale = 1.0；
- 120 DPI：deviceScale = 1.25，420×620 DIP frame 投影为 525×775 px，内容也投影 1.25；
- 在 120 DPI 下继续 resize frame，deviceScale 保持 1.25，卡片和字体不再次放大；
- display change 更新 deviceScale 和物理投影，但不写回 contentScale。

## 5. 命中一致性

Runtime 继续从 renderer 的同一 `NightOpsLayout` 生成 split input islands。R2 只为 `itemVisible=true` 的 Item 发布命中区，因此不存在“视觉已隐藏但透明 HWND 仍拦截桌面”的分叉。标题、锁定、收起和编辑 resize islands 继续沿用 QTS-04-03/QTS-04-04 revision fence。

## 6. 自动证据

Release 全量 CTest：`13/13 PASS`。

新增断言覆盖：

- 420×620：3×2，六项完整；
- 820×440：6×1，卡片尺寸与参考 frame 相同；
- 320×620：2×3，卡片尺寸不变；
- 320×420：只发布两张完整 Item，其他 Item 无 visual/hit geometry；
- 125% DPI 默认与 resize：只由 deviceScale 缩放一次；
- 改变 expanded width 后 collapsed height 仍为 60 DIP；
- 透明外圈、白/灰条带、Host、input islands、Shell/display lifecycle、StateStore、IPC 与退出夹具均未回退。

## 7. 用户 R2 门

最新版 Release 需要确认：

1. 默认尺寸六张卡片正常，无白条、灰框和页脚遮挡；
2. 拉成宽扁形，标题/卡片/图标不随宽度放大，空间足够时卡片换为更多列；
3. 拉成窄高形，卡片尺寸不变并换为更少列；
4. 拉到最小宽高，只显示完整卡片，不能出现半张卡片、Item/页脚重叠或隐藏 Item 命中；
5. 收起/展开前后顶边和按钮位置稳定；改变展开宽度后收起条高度不跟着变；
6. 连续拖动约 20 秒，复测 R1 是否消除规律性闪烁。

已通过的 Win+D、Explorer 重建、桌面刷新、文件选择器和显示变化 1～6 项不要求重做。

## 8. 证据边界

本轮冻结并实现 P0 的几何/内容分权与完整可见容量；它没有提前实现 QTS-06 的真实 Item Repository、活动度大小、分页器、滚动、人工排版和 200 Item 算法，也没有把 `contentScale` 提前接入 QTS-05 控制中心。
# QTS-04-04 实施日志 — 2026-08-28

状态：`USER MATRIX 1～6 PASS / R4.2 AUTO 14 OF 14 PASS / MANUAL REFLOW FLASH GATE PENDING`

## 本轮完成

1. 关闭“所有窗口 CREATE/DESTROY 都立即枚举 Shell”的路径：
   - 无关窗口事件只计数，不进入拓扑发现；
   - Explorer 相关通知合并到 120 ms 稳定窗口；
   - 同目标 no-op，不 hide/show、不 SetWindowPos 修复；
2. 修正重复 degraded/recovery：
   - 连续无安全目标不重复换 lifecycle；
   - 仅真实新安全目标或 attachment 资源丢失创建新 surface；
   - 无普通顶层/TopMost fallback；
3. 增加独立 display maintenance：
   - `DisplayEnvironmentRevision` 与 Shell generation 分离；
   - 主屏系统 DPI、monitor、work area 实际改变才回调；
   - Runtime 250 ms 采样，沿用 TS-C01 R4-D 已验证方式；
4. 新增 Core 逻辑几何：
   - DIP 宽度、expanded height 和 normalized anchor；
   - 96/120 DPI、收起态与负坐标投影；
   - 不从上一次 physical rect 连续缩放；
5. Runtime 接入：
   - attach 后取 Host display snapshot；
   - display change 原子更新 geometry/frame/hit；
   - recovery 发布匹配新 SurfaceEpoch 的首帧；
   - 退出前记录 relevant/ignored/probe/recovery/display 计数；
6. 自动夹具：
   - 独立无界面进程制造普通窗口 CREATE/DESTROY；
   - 验证 ignored 增加而 topology validation、HWND、generation、epoch、visible 不变；
   - 验证无变化 display poll 为 no-op。

## 自动结果

- Release 构建：PASS；
- 全量 CTest：13/13 PASS；
- `qts04.geometry-gesture`：包含 96/120 DPI、收起态与负坐标；
- `qts04.host-attach-reconnect`：包含无关外部窗口事件、display no-op、显式 generation replacement；
- 既有 renderer/input/exit/process-role 回归：PASS。

## 人工矩阵结果

- 用户确认 Win+D、桌面右键/刷新、文件选择器、Explorer 重建、显示变化和输入回归 1～6 项通过；
- 同轮发现标题拖动过程中存在有规律的间歇性闪烁，因此 QTS-04-04 暂不关闭。

## 拖动闪烁 R1 修订

1. 根因路径是每个 `WM_MOUSEMOVE` 都重复 QPainter render、窗口 region、`SWP_SHOWWINDOW/HWND_TOP` 和 islands 重排；
2. 新增同尺寸 `IDesktopHost::relocate`，使用一次 `DeferWindowPos` 同批移动 visual 与已有 islands，不重绘、不换 region、不改变 z-order 或 HWND；
3. 手势计算、5px 阈值、工作区钳制、capture、revision 和失败回滚继续复用原 `GeometryGestureSession`，没有建立第四套拖动逻辑；
4. 真实 HWND 夹具增加 24 次连续 relocation，验证 capture、visual/island HWND、Shell generation、lifecycle/surface epoch 均保持稳定；
5. 修订后 Release 构建 PASS，全量 CTest 13/13 PASS。

## 外框与内容布局 R2 修订

1. 用户正式采用 FrameGeometry、ContentMetrics 与 ResponsiveLayout 分离模型；
2. 移除 `unit = width / 420`，renderer 使用独立 deviceScale × contentScale；同一 DPI 下 resize 不再改变标题、字体、卡片或间距；
3. 固定 Item metrics 按 viewport 换列/换行，只为完整可见项绘制并发布 HitRegion；
4. 页脚锚定底边并与 Item viewport 物理隔离，Item 不再遮挡或裁成半张；
5. 收起高度固定 60 DIP，不再由 expanded width 推导；
6. 新增 420×620、820×440、320×620、320×420、125% DPI 与变宽收起断言；
7. 修订后 Release 构建 PASS，全量 CTest 13/13 PASS。

详细合同与证据见 `QTS_04_04_FRAME_CONTENT_LAYOUT_REVISION_V1.md`。

## V1 连续缩放与 Item 溢出 R3 修订

1. 根因确认：R2 列/行变化会改变 enabled Item region 数量，`Win32InputIslandSet` 整组 replace 并销毁当前持有 capture 的 resize HWND，触发 `WM_CAPTURECHANGED` 取消手势；
2. 命中岛集合现保留全部 RegionId 的 HWND 身份，不可见 Item 仅隐藏/禁用；显隐变化只原地更新，不再替换捕获所有者；
3. renderer 始终计算六个 fixture Item 的完整流；viewport 只绘制/命中完整 Item，空间不足时形成按整行推进的 V1 式纵向 overflow 与滚动条；
4. visible Item island 新增 Wheel 语义，空白区域没有新增包络窗口，继续穿透桌面；
5. 新增“disabled Item 变 enabled 时 resize HWND/capture 不变”、滚轮派发和最后一行可达断言；
6. 修订后 Release 构建 PASS，全量 CTest 13/13 PASS。

详细根因、合同和人工门见 `QTS_04_04_V1_PARITY_RESIZE_OVERFLOW_R3.md`。

## 连续 Item viewport R4 修订

1. 用户截图确认 R3 的 `contains(completeItemBounds)` 会把部分相交行整体隐藏，造成大空位和按行跳变；
2. Item visible/hit 改为与 viewport 的矩形交集，QPainter clip 保证卡片逐像素进入/退出且不越过标题/页脚；
3. Wheel 步长改为连续 48 DIP，并按实际 delta 比例推进，不再吸附完整行；
4. 新增稳定 `ItemScroll` RegionId 150，只在右侧 12 DIP 带接收滚轮；无完整 Item 时仍可恢复，其他海报空白不新增包络；
5. accepted layout 将钳制后的 offset 回写 Runtime scene，避免 resize 后旧越界 offset 复活；
6. 新增部分交集、独立 scrollbar wheel、无空 viewport 与原 capture 回归断言；Release 构建 PASS，全量 CTest 13/13 PASS。

详细根因、合同和人工门见 `QTS_04_04_CONTINUOUS_ITEM_VIEWPORT_R4.md`。

## 尚未完成

R4 人工门未通过：footer clip 泄漏已由 R4.1 自动修复，行列重排闪烁仍为 OPEN。下一步先完成 renderer adapter 与 Host resize transaction 决策，不再沿用本段作为关闭门。
## R4 用户失败与 R4.1 修订

1. 用户截图确认 card-local `setClipPath(cardPath)` 默认 ReplaceClip 覆盖了外层 item viewport clip，导致 accent band 穿越页脚；
2. 改为 `Qt::IntersectClip`，并增加 viewport 下方 accent 差异像素必须为 0 的回归；Release 全量 CTest 13/13 PASS；
3. resize/reflow 闪烁定位到 Host 先 resize/show/z-order、后 StretchDIBits 的非原子提交间隔，保持 OPEN；
4. 审计确认 QPainter Runtime 的 grid/scroll/hit/reflow 当前为手写；Qt Quick GridView/Flickable + QAbstractListModel + QQuickRenderControl 是推荐的有界 adapter 探针，Qt Widgets QListView 为备选；
5. 组件替换不替代 Host transaction 修复，两条证据必须独立建立；
6. 详细见 `QTS_04_04_R4_1_CLIP_AND_COMPONENT_AUDIT_2026-08-28.md`。

## R4.2 Qt Quick adapter 与 Host staged-present

1. 用户正式选择 Qt Quick/QML + GridView/Flickable + QAbstractListModel + QQuickRenderControl；停止扩展手写 QPainter layout/scroll/reflow；
2. 新增 `desktopup_renderer_qtquick`，用软件 Scene Graph 离屏渲染到 premultiplied QImage，并从 QML delegate 的真实几何输出既有 hit-region 合同；
3. GridView 已覆盖 2/3/6 列、连续 contentY、部分 Item clip、末行可达、collapsed、透明外圈和重复 render；QPainter renderer 保留为历史像素/fallback 回归；
4. Runtime 默认 renderer 切换至 Qt Quick adapter；架构门受控允许 Runtime 链接 QtQuick/QtQml，但继续禁止 QuickControls、WebView 与外部 QML/JS；
5. Host `present` 先完成整帧像素转换和 region 创建，再用一次 DeferWindowPos 批量提交 visual/island geometry，随后同步绘制；稳定 resize 不再每帧 SHOW/HIDE/HWND_TOP；
6. input-island 自动门补充 resize capture 中变更 frame 尺寸，确认 visual/island HWND identity 与 capture 保持；
7. Windows x64 Release 构建 PASS，全量 CTest 14/14 PASS；真实 Runtime 短程启动退出码 0，ledger 为 exited 且 ownedResourceCount 0；
8. 行列重排闪烁尚未由用户真实桌面确认，因此保持 `MANUAL GATE PENDING`，不提前关闭 QTS-04-04。

详细实现、边界和人工门见 `QTS_04_04_R4_2_QTQUICK_ADAPTER_AND_HOST_TRANSACTION_2026-08-28.md`。

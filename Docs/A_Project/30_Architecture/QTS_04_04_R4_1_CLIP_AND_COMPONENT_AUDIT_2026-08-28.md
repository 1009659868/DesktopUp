# QTS-04-04 R4.1：底部裁切修复与 Renderer 组件审计

状态：`SUPERSEDED BY R4.2 / BOTTOM CLIP FIX RETAINED / COMPONENT DECISION CONSUMED`

日期：2026-08-28

## 1. 本轮用户观察

1. viewport 底部被裁切的 Item 中，卡片主体已停止绘制，但彩色斜带继续穿过页脚甚至海报外框；
2. resize 跨越 Item 列数/行数边界时，海报出现一次明显闪烁；
3. 用户质疑当前布局、滚动和重排是否全部手写，并要求评估现成组件，避免持续踩即时绘制与交互状态坑。

## 2. 底部泄漏根因与修复

QPainter 的 `setClipPath()` 默认操作是 `Qt::ReplaceClip`。renderer 先建立 Item viewport clip，随后为卡片内部彩色斜带调用 `setClipPath(cardPath)`；后者替换而非叠加外层 clip，导致彩色斜带在完整 card path 内继续绘制。

修复：卡片局部裁切改为 `setClipPath(cardPath, Qt::IntersectClip)`，确保最终 clip 始终是 `item viewport ∩ card path`。

新增像素回归：构造末行跨越 viewport 底边的场景，将正常 accent 帧与透明 accent 帧在 viewport 下方逐像素比较；差异像素必须为 0。Release 构建和全量 CTest 13/13 PASS。

## 3. 行列重排闪烁的代码级根因

当前 Win32 Host 的变尺寸 `present()` 顺序是：

1. 对当前可见 visual HWND 调用 `SetWindowPos`，先提交新位置与新尺寸；
2. CPU 复制并反预乘新 frame；
3. `SetWindowRgn` 更新圆角区域；
4. 每帧再次 `SetWindowPos(HWND_TOP, SWP_SHOWWINDOW)`；
5. 最后才通过 `StretchDIBits` 绘制新像素；
6. input islands 再执行 show/hide 与 z-order 更新。

因此 Windows 在新 HWND 尺寸已经可见而新像素尚未写入的间隔内，可以合成旧内容或未完成区域。普通连续 resize 也存在该窗口，只是行列重排时新旧帧差异最大，闪烁更明显。

`UpdateLayeredWindow` 从 API 语义上可以一次更新 layered window 的位置、尺寸、内容和透明度，但本项目此前已记录：在当前 raised-Progman child 拓扑中它可能返回成功却不可见，因此不能直接恢复为生产方案，必须重新建立可见性证据。

## 4. 当前哪些部分是手写的

当前常驻 Runtime 只链接 QtCore + QtGui。QPainter 是即时绘制 API，不提供 Item model/view、Grid、viewport、滚动、delegate 生命周期或重排 transition。因此以下内容目前是手写：

- 固定卡片网格与列数计算；
- viewport/scroll offset/scrollbar geometry；
- Item visual 与 hit rectangle 投影；
- wheel 状态、边界钳制和 resize 重排；
- QPainter save/restore/clip 栈管理。

DesktopHost 的 Explorer 层级、split input islands 和 revision fence 属于项目特有 Windows 集成，无现成 Qt 控件可直接替代；但 Item 布局/滚动不应继续全部建立在裸 QPainter 上。

## 5. 可用组件路线

### A. Qt Quick GridView/Flickable + QAbstractListModel（推荐进入适配器探针）

- GridView 原生使用 model/delegate，继承 Flickable，提供像素滚动、delegate 按需实例化、固定 cell、选择和 displaced transitions；
- 显式 `clip: true` 后由 scene graph 保证 viewport 裁切；
- QAbstractListModel 保持数据与 visual delegate 分离；
- QQuickRenderControl 可把无原生窗口的 QQuickWindow 离屏渲染到自定义 render target，再通过现有 IRenderer/Host 边界输出；
- 更符合后续主题、动画、热更新和控制中心预览，但会让常驻 Runtime 引入 Qt Quick/QML/Scene Graph，必须重新测量工作集、GPU 资源和退出清理。

### B. Qt Widgets QListView/QAbstractItemView + delegate（低风险备选）

- 提供 model/view、像素滚动、wrap/grid、selection 和 delegate 绘制；
- 可以离屏 render 到 QImage，继续复用 QPainter 卡片 delegate；
- 会引入 QtWidgets/QApplication，主题热更新和动画能力弱于 Qt Quick；未来动态活跃度布局仍可能需要自定义 view。

### C. QGraphicsScene/QGraphicsGridLayout（不推荐作为主路线）

- 可减少部分几何与 clip 手工代码；
- 但 viewport scrolling、model/delegate、虚拟化和 split-hit 同步仍需较多自定义，收益不足以抵消新抽象。

## 6. 推荐决策

1. 保留已验证 DesktopHost、input islands、StateStore/IPC 和 IRenderer 合同；
2. 停止继续扩展当前手写 QPainter layout/scroll；当前实现只作为视觉参考与 fallback；
3. 新建一个有界 Qt Quick offscreen renderer adapter 探针，验证 NIGHT OPS 等价画面、GridView 连续滚动/重排、可见 Item hit map、工作集和零残留；
4. 并行但独立地修订 Host resize transaction，先消除“可见 resize → 后绘制”的提交顺序；不得把 renderer transition 当作 Host 闪烁修复；
5. 两项证据均通过后再替换生产 adapter，未通过则回退到 Qt Widgets 方案。

## 7. 当前门

- 底部 accent 泄漏：自动关闭，等待一轮用户视觉确认；
- 行列重排闪烁：OPEN；
- QTS-04-04：不得关闭；
- renderer adapter 路线：等待用户确认是否按第 6 节启动有界探针。

## 8. 参考

- Qt 6.8 GridView：<https://doc.qt.io/qt-6.8/qml-qtquick-gridview.html>
- Qt 6.8 Flickable：<https://doc.qt.io/qt-6.8/qml-qtquick-flickable.html>
- Qt 6.8 QQuickRenderControl：<https://doc.qt.io/qt-6.8/qquickrendercontrol.html>
- Qt 6.8 QAbstractItemModel：<https://doc.qt.io/qt-6.8/qabstractitemmodel.html>
- Qt 6.8 QGraphicsLayout：<https://doc.qt.io/qt-6.8/qgraphicslayout.html>
- Win32 UpdateLayeredWindow：<https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-updatelayeredwindow>

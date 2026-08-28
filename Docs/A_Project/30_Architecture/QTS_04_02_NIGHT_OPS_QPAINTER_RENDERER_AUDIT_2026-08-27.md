# QTS-04-02 NIGHT OPS QPainter renderer 审计（2026-08-27）

状态：`CLOSED — RELEASE AUTO 11 OF 11 PASS / USER DESKTOP VISUAL GATE ACCEPTED 2026-08-27`
## 1. 本轮完成范围

QTS-04-02 已在 QTS-04-01 的真实 DesktopHost 上替换最小测试帧，形成单海报 NIGHT OPS QPainter renderer：

1. 将参考海报定义为独立 `NightOpsScene`，六张 Item 仍是视觉夹具，不冒充正式 Item 领域模型；
2. 建立 expanded/collapsed、locked/unlocked 的纯视觉输入；
3. 以 420×620 为冻结参考尺寸，以画布宽度计算响应式布局；420px 宽度的收起态固定为 60px 高；
4. 绘制圆角玻璃背景、标题区、六张卡片、状态、页脚、锁定与收起图形；
5. renderer 只输出完整 `QImage::Format_ARGB32_Premultiplied` frame，不创建 HWND、不访问 Explorer、不改变 z-order；
6. Runtime 默认生产帧从 320×480 改为冻结参考 420×620；
7. 建立离屏像素、几何、状态和 buffer 生命周期自动夹具；
8. 保持 QTS-04-01 Host 与全部 QTS-03 回归绿色。

本轮没有实现 input islands、鼠标命中、拖动/缩放、Win+D、Explorer 人工重启、托盘、正式 Item、主题热更新或 AI。

## 2. 白条与灰框的实现根因及关闭方式

旧最小 renderer 先对整张透明 QImage 执行半透明矩形 `fillRect`，再绘制圆角边线。圆角线并不会裁掉已填充的矩形背景，因此四角和外包络天然带 alpha，后续 Host 只会忠实呈现该矩形像素。

当前实现改为：

~~~text
透明 ARGB32 premultiplied 画布
  -> 仅填充圆角 QPainterPath
  -> 所有光晕、斜线和纹理 clip 到同一路径
  -> 边框绘制在路径内
  -> 外部像素始终保持 alpha=0
~~~

renderer 不再绘制非客户区、窗口背景或整幅矩形蒙层。白条/灰框由自动夹具按最终像素检测，而不是只靠代码审阅或屏幕截图判断。

## 3. 冻结 scene 与布局

`NightOpsScene` 包含：

- `DESKTOP COLLECTION`；
- `TACTICAL ARCHIVE / 01`；
- `NIGHT OPS`；
- `06 TITLES · READY`；
- `AL / GS / IV / LO / SR / HC` 六张视觉夹具及各自标题、强调色；
- `LOCAL LIBRARY / DRAG TO MOVE`；
- `presentation` 与 `locked` 视觉状态。

scene 与 paint routine 分离，测试或后续 State Projection 可替换 scene；当前没有把这六条 fixture 写入 StateStore，也没有提前建立 Item 业务接口。

`NightOpsLayout` 保存 poster、header、content、footer、六张 card 和 label 的实际像素矩形。展开和收起共享 header 的 x/y/width；收起只改变 frame height 并隐藏 content/card/footer。

## 4. 自动像素门

新增 `qts04.night-ops-renderer`，检查：

| 自动条件 | 结果 |
| --- | --- |
| ARGB32 premultiplied 与精确 stride | PASS |
| FrameDescriptor、FrameView 与请求 bounds 一致 | PASS |
| 最外一圈像素 alpha 全为 0 | PASS |
| 四个圆角 contour 外像素 alpha 为 0 | PASS |
| 顶部/底部扫描区无大面积白色或灰色不透明条带 | PASS |
| 海报内部存在已初始化半透明像素 | PASS |
| 六张 card/label 均在 poster 内且位于 footer 上方 | PASS |
| 相同 scene/bounds 重绘像素完全一致 | PASS |
| 新 buffer 替换后旧 handle 立即失效 | PASS |
| locked/unlocked 像素不同但 header 几何不动 | PASS |
| 420×620 展开、420×60 收起及 header x/y/width 不变 | PASS |
| release/beginExit 后 buffer 不可复用 | PASS |

导出证据：`out/build/windows-x64-release/qts04-night-ops.png`。

## 5. Release 回归

环境：Qt 6.8.3 `msvc2022_64`、Visual Studio generator + v143、Windows SDK 10.0.26100.0、Release。

~~~powershell
cmake --build --preset windows-x64-release --parallel
ctest --preset windows-x64-release
~~~

结果：`100% tests passed, 11 tests, 0 failed`。其中 QTS-04-01 的真实 Host attach/reconnect、Runtime exit、资源账本、角色边界、StateStore、IPC 和 Control Center smoke 均未回退。

## 6. 证据边界

自动像素 PASS 证明 renderer 输出本身没有矩形外包络和条带，并证明 frame/buffer 合同正确；它不能替代实际桌面合成后的视觉确认，也不能证明输入或生命周期 P0。

用户已确认最新版 Release 实际可见且满足无白条/灰框、普通窗口高于海报和退出无残留；QTS-04-02 已关闭，QTS-04-03 已激活。
## 7. 用户桌面视觉门

使用最新版 Release 有界运行，确认：

1. 桌面中央显示 420×620 NIGHT OPS，六张卡片和页脚清晰；
2. 四角外是桌面本身，没有矩形暗灰/黑色包络；
3. 顶部和底部没有白条、灰条或额外边框；
4. 普通窗口仍高于海报；
5. 运行结束后海报消失且无残留。

本门只核对渲染与基本 Host 合成；空白框选、右键、原生图标、卡片和按钮命中属于 QTS-04-03，不应在本轮判失败。

用户结果：`PASS — 2026-08-27`。
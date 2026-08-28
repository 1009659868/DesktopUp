# QTS-04-02 代码锚点 V1

状态：`HISTORICAL — QTS-04-02 CLOSED / SUPERSEDED FOR INPUT DETAILS`
## Renderer 与 scene

| 路径 | 作用 |
| --- | --- |
| `src/adapters/renderer-qpainter/qpainter_renderer.h` | `NightOpsScene`、六项视觉 fixture、expanded/collapsed、locked、layout diagnostics 与 IRenderer adapter |
| `src/adapters/renderer-qpainter/qpainter_renderer.cpp` | 透明 premultiplied 画布、圆角路径、NIGHT OPS 绘制、响应式布局、buffer replacement/release |
| `src/apps/runtime/main.cpp` | 使用 renderer 冻结参考尺寸 420×620 发布真实 Host frame |

## 自动证据

| 路径 | 作用 |
| --- | --- |
| `src/tests/renderer/night_ops_renderer_tests.cpp` | 透明外圈、圆角、白/灰条带、六卡布局、展开/收起、锁定、确定性和 buffer 生命周期 |
| `src/CMakeLists.txt` | `qts04.night-ops-renderer` Release CTest 注册 |
| `out/build/windows-x64-release/qts04-night-ops.png` | 当前自动夹具导出的 expanded 参考像素；构建产物，不作为源码提交输入 |

## 保持不变的 QTS-04-01 边界

| 路径 | 作用 |
| --- | --- |
| `src/adapters/desktop-host-win32/win32_desktop_host.*` | HWND、Explorer target、层级、首帧、generation/reconnect 的唯一所有者 |
| `src/platform/windows/desktop_host.manifest` | layered child 所需 Windows compatibility 与 PerMonitorV2 |
| `src/tests/desktop-host/attach_reconnect_tests.cpp` | 真实 Host/replacement 回归，不冒充 renderer 视觉门 |

## QTS-04-03 修改纪律

- 以 `NightOpsLayout` 的 header/card/button 几何为输入建立 split input islands，不把 input window 画进 renderer frame；
- 空白区域必须继续由 Explorer 命中，不能用一个覆盖整张海报的可交互 HWND；
- 不允许修改 QPainter 像素来伪装输入成功；
- 不修改 DesktopHost 的 raised-Progman z-order 和首帧门；
- QTS-04-02 用户视觉门已于 2026-08-27 通过；后续输入细节以 QTS-04-03 代码锚点为准。
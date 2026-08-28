# QTS-04-03 代码锚点 V1

状态：`ACTIVE — R2 AUTO 13 OF 13 PASS / USER RETEST BASELINE`

## 合同与投影

| 路径 | 作用 |
| --- | --- |
| `src/contracts/types.h` | HitRegion role/set、priority、revision-fenced pointer event 与 Host input observation |
| `src/contracts/ports.h` | screen bounds + frame + pixels + hit regions 单次 present 和 input callback |
| `src/adapters/renderer-qpainter/qpainter_renderer.h` | 可供输入投影复用的标题、按钮、卡片、footer 和 resize corner 实际布局 |
| `src/adapters/renderer-qpainter/qpainter_renderer.cpp` | 响应式几何、选中卡片视觉反馈及既有透明像素输出 |
| `src/core/geometry_gesture.h/.cpp` | E4 有界手势会话、5px 阈值、固定 resize 边、320×420/工作区约束与 cancel/end |

## DesktopHost 与 split islands

| 路径 | 作用 |
| --- | --- |
| `src/adapters/desktop-host-win32/win32_input_islands.h` | input-island set 的发布、callback、清理和观测接口 |
| `src/adapters/desktop-host-win32/win32_input_islands.cpp` | sibling HWND、zero/one/multi/overlap、priority、replacement、capture 和语义事件 |
| `src/adapters/desktop-host-win32/win32_desktop_host.h` | 生产 Host 的 input callback 与原子 present 入口 |
| `src/adapters/desktop-host-win32/win32_desktop_host.cpp` | visual + islands 所有权、screen bounds 原子投影、主动 DC repeated present、恢复与同步销毁 |

## Runtime

| 路径 | 作用 |
| --- | --- |
| `src/apps/runtime/main.cpp` | `--editing`、layout→HitRegionSet、GeometryGestureSession 协调、失败回滚、锁定/收起/卡片选择与恢复重发 |

当前 Runtime 交互只修改进程内 scene/geometry；正式状态持久化和控制中心协作属于 QTS-05。

## 自动夹具

| 路径 | 作用 |
| --- | --- |
| `src/tests/core/geometry_gesture_tests.cpp` | 连续会话、阈值、移动、越顶部最小尺寸、最大尺寸、负坐标工作区和取消 |
| `src/tests/desktop-host/input_islands_tests.cpp` | zero/one/multi/overlap、Explorer blank、真实 visual/island 位移、capture 稳定和 exit cleanup |
| `src/tests/desktop-host/attach_reconnect_tests.cpp` | 真实 Host attach/reconnect 回归 |
| `src/tests/lifecycle/lifecycle_tests.cpp` | 更新后的 frame/pixels/hit regions 生命周期合同 |
| `src/CMakeLists.txt` | `qts04.geometry-gesture`、`qts04.input-islands` 与生产 Core/Host 构建注册 |

## QTS-04-04 修改纪律

- 不把 Win+D 或 Explorer 恢复修成 TopMost、隐藏再显示或前台轮询；
- 不把 blank area 改成全矩形 input HWND；
- 不通过向 Explorer 转发鼠标消息冒充原生命中；
- 不让生命周期恢复绕过当前 SurfaceEpoch 和 projection revision；
- 不把 QTS-04-03 人工门未确认的项目提前写成 PASS。

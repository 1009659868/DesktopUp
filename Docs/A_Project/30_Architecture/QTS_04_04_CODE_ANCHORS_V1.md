# QTS-04-04 代码锚点 V1

状态：`R4.2 QT QUICK + HOST STAGED PRESENT AUTO PASS / MANUAL REFLOW FLASH GATE PENDING`

日期：2026-08-28

| 合同/行为 | 代码锚点 |
| --- | --- |
| display revision 与生命周期诊断类型 | `src/contracts/types.h` — `DisplayEnvironment`、`HostMaintenanceResult`、`HostObservation` |
| Host display maintenance 端口 | `src/contracts/ports.h` — `IDesktopHost::refreshDisplayEnvironment` |
| 主屏系统 DPI/monitor/work-area 采样 | `src/adapters/desktop-host-win32/win32_desktop_host.cpp` — `queryPrimaryDisplayEnvironmentSnapshot` |
| 无关窗口事件过滤 | 同上 — `isShellTopologyWindow`、`scheduleTopologyValidation` |
| 120 ms Shell 稳定窗口 | 同上 — `kTopologyValidationTimer`、`coordinatorWindowProcedure` |
| generation/degraded/唯一恢复 | 同上 — `handleTopologyEvent` |
| Host diagnostics | 同上 — `Win32DesktopHost::Impl::observe` |
| DIP/归一化锚点 authority | `src/core/display_geometry.h/.cpp` |
| 250 ms display monitor 与原子重投影 | `src/apps/runtime/main.cpp` — `displayEnvironmentTimer`、`applyDisplayEnvironment` |
| Explorer 新代首帧回调 | 同上 — `setRecoveryCallback` |
| 无关窗口子进程 | `src/tests/desktop-host/unrelated_window_event_source.cpp` |
| 外部事件/无变化 display 自动断言 | `src/tests/desktop-host/attach_reconnect_tests.cpp` |
| DPI/工作区/负坐标投影断言 | `src/tests/core/geometry_gesture_tests.cpp` |
| 同尺寸低延迟 relocation 端口 | `src/contracts/ports.h`、`win32_desktop_host.h/.cpp` — `IDesktopHost::relocate` |
| visual/islands 单批次位移 | `src/adapters/desktop-host-win32/win32_input_islands.cpp` — `Win32InputIslandSet::Impl::relocate` |
| 标题拖动热路径 | `src/apps/runtime/main.cpp` — `relocateCurrentSurface`；继续复用 `GeometryGestureSession` |
| 24 次连续 relocation/capture/身份断言 | `src/tests/desktop-host/input_islands_tests.cpp` |
| FrameGeometry/ContentMetrics/ResponsiveLayout 分离 | src/adapters/renderer-qpainter/qpainter_renderer.h/.cpp — deviceScale、contentScale、itemViewportBounds、itemVisible |
| DPI 与外框 resize 解耦 | src/apps/runtime/main.cpp — deviceScale；src/core/display_geometry.cpp — 固定 60 DIP collapsed projection |
| 宽扁/窄高/最小 frame 自动断言 | src/tests/renderer/night_ops_renderer_tests.cpp；src/tests/core/geometry_gesture_tests.cpp |
| R3 稳定 RegionId/disabled island HWND | src/adapters/desktop-host-win32/win32_input_islands.cpp — `orderedRegions`、`placeAndOrder` |
| R3 完整 Item 流、按行 overflow 与滚动条 | src/adapters/renderer-qpainter/qpainter_renderer.h/.cpp — `itemScrollOffsetDip`、`maxItemScrollOffset`、`itemHitBounds` |
| R3 visible Item Wheel 入口 | src/contracts/types.h、src/apps/runtime/main.cpp、src/adapters/desktop-host-win32/win32_input_islands.cpp |
| R3 capture/滚轮/最后一行可达断言 | src/tests/desktop-host/input_islands_tests.cpp；src/tests/renderer/night_ops_renderer_tests.cpp |
| R4 连续 viewport 交集与 scroll geometry | src/adapters/renderer-qpainter/qpainter_renderer.h/.cpp — `itemHitBounds`、`itemScrollTrackBounds`、`itemScrollInputBounds` |
| R4 独立滚动角色与最小 island | src/contracts/types.h、src/apps/runtime/main.cpp、src/adapters/desktop-host-win32/win32_input_islands.cpp — `ItemScroll`、RegionId 150 |
| R4 部分行、独立 wheel 与无死锁断言 | src/tests/renderer/night_ops_renderer_tests.cpp；src/tests/desktop-host/input_islands_tests.cpp |
| R4.1 card/viewport 组合裁切 | `src/adapters/renderer-qpainter/qpainter_renderer.cpp` — `setClipPath(cardPath, Qt::IntersectClip)` |
| R4.1 footer accent 泄漏像素门 | `src/tests/renderer/night_ops_renderer_tests.cpp` — `leakedAccentPixels == 0` |
| Qt Quick renderer adapter 端口 | `src/adapters/renderer-qtquick/qtquick_renderer.h/.cpp` — `QtQuickRenderer`、`NightOpsItemModel`、QQuickRenderControl software target |
| GridView/Flickable 视觉与 delegate 几何 | `src/adapters/renderer-qtquick/NightOpsPoster.qml` — `GridView`、`contentY`、`delegateAt`、动态 columns |
| Qt Quick 动态列/连续滚动/透明边界回归 | `src/tests/renderer/qtquick_renderer_tests.cpp` |
| 完整新帧预备与 staged present | `src/adapters/desktop-host-win32/win32_desktop_host.cpp` — `Win32DesktopHost::present` |
| visual/islands 同批 geometry 与稳定 z-order | `src/adapters/desktop-host-win32/win32_input_islands.cpp` — `placeAndOrder`、`establishZOrder`、`publish` |
| resize 中 HWND/capture 稳定断言 | `src/tests/desktop-host/input_islands_tests.cpp` |
| Runtime 默认 Qt Quick adapter | `src/apps/runtime/main.cpp` |
| Release 测试装配 | `src/CMakeLists.txt` |

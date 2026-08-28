# QTS-04-01 代码锚点 V1

状态：`ACTIVE — QTS-04-01 COMPLETE / QTS-04-02 BASELINE`

## 构建与进程能力

| 路径 | 作用 |
| --- | --- |
| `CMakeLists.txt` | 根工程启用 C++ 与 Windows RC 资源编译 |
| `src/CMakeLists.txt` | 真实 Host adapter、Runtime/测试 manifest 资源和 `qts04.host-attach-reconnect` |
| `src/platform/windows/desktop_host.manifest` | Windows 8+/8.1/10/11、PerMonitorV2、asInvoker、long-path 声明 |
| `src/platform/windows/desktop_host.rc` | 将 Host-capable manifest 嵌入 executable 的资源 1 |

## Host/Renderer 合同

| 路径 | 作用 |
| --- | --- |
| `src/contracts/types.h` | PixelRect、premultiplied frame view、Host topology/state/maintenance/observation |
| `src/contracts/ports.h` | `IRenderer::frameView` 与真实 `IDesktopHost attach/present/reconnect/observe` |
| `src/adapters/desktop-host-win32/win32_desktop_host.h` | 生产 Win32 DesktopHost adapter 和 Runtime recovery callback |
| `src/adapters/desktop-host-win32/win32_desktop_host.cpp` | raised-Progman discovery、layered child、首帧门、WinEvent generation 恢复、退出清理 |
| `src/adapters/desktop-host-win32/desktop_host_fixture.*` | QTS-03 纯合同夹具；保留给 framework-free 生命周期测试，不是生产 Host |
| `src/adapters/renderer-qpainter/qpainter_renderer.*` | QImage ARGB32 premultiplied frame 与像素视图；QTS-04-02 在此实现 NIGHT OPS |

## Runtime 与自动验证

| 路径 | 作用 |
| --- | --- |
| `src/apps/runtime/main.cpp` | 真实 Host composition、首帧发布、recovery callback 重发当前 surface、退出顺序 |
| `src/tests/desktop-host/attach_reconnect_tests.cpp` | 真实拓扑、manifest、Z 序、首帧、bounds、replacement、callback、清理夹具 |
| `src/tests/lifecycle/lifecycle_tests.cpp` | QTS-03 fake Host 的 epoch/frame/exit 回归，不冒充真实桌面 P0 |

## QTS-04-02 修改纪律

- 保留 `win32_desktop_host.cpp` 对 z-order、HWND 和 first-frame reveal 的唯一所有权；
- QPainter renderer 只产生完整 premultiplied frame，不创建窗口、不访问 Explorer；
- NIGHT OPS 视觉必须在 `renderer-qpainter` 内实现，并以 frame bounds 为唯一像素尺寸输入；
- 本阶段不添加 split input islands；它们属于 QTS-04-03；
- 不把 Qt Quick R2/QML surface 复制进常驻 Runtime；
- 保留 Host-capable manifest，不通过启动延迟、TopMost 或隐藏/恢复替代；
- 每次修改后至少运行 `qts04.host-attach-reconnect` 和全量 Release CTest。

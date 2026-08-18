# C-QTP-01/02 两进程骨架、stdio IPC 与参考构图

日期：2026-08-18

状态：`IMPLEMENTED — C-QTP-01 COMPLETE / C-QTP-02 FRAME VERIFIED`

## 1. 交付内容

- `prototype/qt-quick/core/`：Rust 核心桩——启动/重启渲染侧车（上限 3 次）、stdio 帧传输（4 字节 LE 长度前缀 + UTF-8 JSON）、看门狗、健康轮询（3s/轮）与渲染器自抓帧触发（9s/轮）；
- `prototype/qt-quick/renderer/`：C++/Qt 6.8 Quick 渲染侧车——无边框透明圆角海报窗口（NIGHT OPS 参考构图：标题栏、3×2 占位卡片、底部状态栏、洋红健康标记）、stdio 读线程、get_health/set_geometry/set_visible/set_params/capture/shutdown 命令；
- `prototype/qt-quick/build.ps1`：vcvars + CMake/Ninja + windeployqt + cargo 一键构建。

## 2. 排障记录（重要教训）

- 命名管道 message 模式 + `PIPE_NOWAIT` 混用导致：连接事件提前触发、读线程在客户端到达前读到 `ERROR_PIPE_LISTENING` 被误判断管、`WriteFile` 阻塞死锁——连接即断管、双向数据消失；
- 最终方案：**放弃命名管道，改用父子进程 stdio**（子进程 stdin 下行命令 / stdout 上行回复）。1:1 核心↔渲染器拓扑下更简单、无并发死锁类问题；崩溃隔离由进程重启天然提供；
- 教训入 V2 问题台账：`PIPE_NOWAIT` + 一次性 `ConnectNamedPipe` 是已知陷阱，后续正式 IPC 选型（多表面拓扑）时单独评估，原型阶段不复用该模式。

## 3. 验证证据

- 冒烟测试：核心桩启动渲染器 → `hello` 握手 → 每 3s `get_health` 往返（`exposed=true visible=true geometry=1700,200,450,703`）→ `capture` 命令返回 `ok=true size=450x703`；
- 渲染器自抓帧 `capture.png`（450×703，19KB）像素分析：洋红健康标记 30/35250 采样点、深色玻璃背景 3046/35250 采样点——参考构图在帧内正确渲染；
- 进程隔离：手动结束渲染器进程后核心桩自动重启（3 次上限后放弃）。

## 4. 下一步

C-QTP-03：桌面层与命中——`WS_EX_TRANSPARENT` 穿透切换、命中区域注册、标题栏拖动、5px 阈值、几何命令接入；C-QTP-04：呈现自检（核心侧对 capture 帧的标记比对 + 0 FPS 静态验证）。

## 5. 产物路径

`G:\A_\Tools\DesktopUp_V2\prototype\qt-quick\`（core/renderer 源码、build.ps1、core.log/renderer.log/capture.png 运行时产物，后者不入库）

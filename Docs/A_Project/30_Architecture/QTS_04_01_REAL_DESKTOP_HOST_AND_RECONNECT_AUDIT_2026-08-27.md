# QTS-04-01 真实 DesktopHost 与重连审计（2026-08-27）

状态：`COMPLETE — REAL RAISED-PROGMAN HOST / RELEASE 10 OF 10 CTEST PASS / QTS-04-02 NEXT`

关联输入：

- [Qt 方案总执行计划](QT_SOLUTION_EXECUTION_PLAN_V1.md)
- [QTS-02 生产物理架构](QTS_02_PRODUCTION_PHYSICAL_ARCHITECTURE_V1.md)
- [QTS-03 → QTS-04 交接](QTS_03_HANDOFF_TO_QTS_04_2026-08-27.md)
- [TS-H01-E3 生命周期合同](../20_Technical_Selection/TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md)
- [DesktopHost 原型接口](../20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md)

## 1. 本轮完成范围

QTS-04-01 已把 QTS-03 的 Host fixture 替换为真实 Win32 DesktopHost adapter，并保持 renderer、Host 与 Runtime composition root 的端口隔离。已完成：

1. 枚举唯一、可见且未 cloaked 的 `SHELLDLL_DefView`；
2. 解析 `Progman -> SHELLDLL_DefView -> SysListView32/FolderView` 的当前 raised-Progman 拓扑；
3. 校验桌面 Host、DefView、图标视图和任务栏属于同一 Explorer generation；
4. 以 `WS_CHILD | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE` 创建 Host-owned visual；
5. 在不显示内容的情况下先把 visual 放到 DefView 上方，再以完整 premultiplied frame 触发首次显示；
6. 监听窗口级 WinEvent create/destroy，发现 shell generation 变化时重新发现并重建 visual；
7. 重建时递增 shell generation、lifecycle epoch 和 surface epoch，并通知 Runtime 重新发布当前首帧；
8. `BeginExit` 同步释放 visual、WinEvent hook 和 message-only coordinator；
9. 建立真实 attach、present、reconnect、回调、detach 和零资源自动夹具；
10. 为所有 Host-capable 可执行角色嵌入 Windows 8+/PerMonitorV2 manifest。

本轮仍使用最小 QPainter frame；没有实现 NIGHT OPS 视觉、split input islands、托盘、真实 Item 或完整生命周期 P0。

## 2. 实际层级与失败关闭

当前已验证结构为：

~~~text
普通窗口 / 文件选择器 / 全屏程序
                >
DesktopUp layered child（Progman 的 child，DefView 的前序 sibling）
                >
SHELLDLL_DefView / SysListView32 桌面图标
                >
壁纸
~~~

adapter 明确禁止：

- `WS_EX_TOPMOST`；
- 激活或抢焦点；
- ordinary top-level fallback；
- 隐藏/恢复窗口补救；
- 轮询 `SetWindowPos` 保层级；
- 未验证的 traditional WorkerW 路径。

代码能识别 traditional WorkerW，但在该路径单独获得证据前保持 fail-closed。本轮只关闭当前机器已经通过 TS-H01 的 raised-Progman 类别。

## 3. 首帧与边界合同

attach 只建立 1×1 隐藏 layered child 和正确 sibling Z 序。只有满足以下全部条件，visual 才会显示：

- frame revision/SurfaceEpoch/bounds 通过既有 gate；
- renderer 暴露有效 ARGB32 premultiplied 像素；
- screen rect 成功映射到 Explorer parent client coordinates；
- `UpdateLayeredWindow` 成功提交完整 frame；
- 最终无激活 `SetWindowPos(HWND_TOP, ... SWP_SHOWWINDOW)` 成功。

自动夹具核对 requested bounds、window bounds、client-to-screen bounds 与 DWM extended frame bounds 一致。首次 frame 前和每次 generation replacement 后 visual 都保持隐藏，避免暴露未完成内容。

## 4. manifest 根因与冻结合同

实现初轮能够发现正确 Progman/DefView/IconView，却在 `CreateWindowExW` 进入 `WM_NCCREATE` 前失败。屏幕位置、Qt 启动延迟、初始显示方式和 `WS_EX_TRANSPARENT` 均不是根因。

与同机可通过的 TS-H01-B 和 Qt R2 比对后确认：旧探针都嵌入 Windows 8+ compatibility manifest，新生产骨架没有。Windows 的 layered child 支持要求进程声明相应兼容性；缺失时系统在窗口过程执行前拒绝创建。

因此 QTS-04-01 冻结以下构建合同：

- 任何实例化真实 DesktopHost 的 executable 必须嵌入 `desktop_host.manifest`；
- manifest 至少声明 Windows 8、8.1、10/11 compatibility，使用 `asInvoker`、`uiAccess=false` 和 PerMonitorV2；
- adapter 在创建前检查当前 executable 的 manifest 资源和 Windows 8 compatibility GUID，缺失时以 `0xD002` fail-closed；
- Runtime 和真实 Host 自动夹具使用该 manifest；不拥有真实 Host 的 Control Center 与纯合同夹具不继承此资源。

## 5. generation/reconnect 合同

自动重连夹具验证：

1. 旧 visual 在 replacement 前被销毁；
2. 新 visual HWND 与旧 HWND 不同；
3. shell generation、lifecycle epoch、surface epoch 单调递增；
4. 旧 epoch frame 不能成为新 generation 的首帧；
5. replacement 首帧前保持隐藏；
6. Runtime recovery callback 恰好收到一次 `ReattachedNeedsFrame`；
7. 新 frame 发布后重新处于 DefView 上方；
8. 退出后 visual、hook、coordinator 均为 0。

WinEvent hook 只处理 `OBJID_WINDOW/CHILDID_SELF` 的 create/destroy，并跳过本进程，避免把任意 UI 子对象变化当成 Explorer generation 事件。

## 6. Release 自动证据

构建环境：Qt 6.8.3 `msvc2022_64`、Visual Studio 2026 generator + v143、Windows SDK 10.0.26100.0、CMake Release。

~~~powershell
cmake --preset windows-x64-release
cmake --build --preset windows-x64-release --parallel 8
ctest --preset windows-x64-release
~~~

结果：`100% tests passed, 10 tests, 0 failed`。

新增 `qts04.host-attach-reconnect` 覆盖：真实拓扑发现、manifest、非 TopMost、非 top-level fallback、首帧隐藏门、完整 frame、四类 bounds、generation replacement、Runtime callback 和零资源退出。

独立有界原生运行：

~~~text
desktopup-host-attach-reconnect-tests.exe --duration-ms 1200
QTS04_NATIVE_FIXTURE_EXIT=0
QTS04_NATIVE_FIXTURE_REMAINING=0
~~~

## 7. 退出门

| QTS-04-01 退出条件 | 结果 |
| --- | --- |
| 真实 raised-Progman target 发现与唯一性校验 | PASS |
| visual 为 Explorer Host child、位于 DefView 上方 | PASS |
| 无 TopMost、激活或 ordinary top-level fallback | PASS |
| 完整首帧前保持隐藏 | PASS |
| premultiplied frame 成功发布 | PASS |
| requested/window/client/extended bounds 一致 | PASS |
| generation/epoch 单调 replacement | PASS |
| Runtime 恢复通知一次且要求新首帧 | PASS |
| detach/BeginExit 资源归零 | PASS |
| QTS-03 全量回归 | PASS，10/10 |

## 8. 尚未取得的证据

以下项目本轮明确为 `NOT RUN`，不能由自动 attach/reconnect 结果推导：

- 真实 Explorer 进程重启后的人工恢复；
- Win+D 不隐藏、不恢复、不闪烁；
- 桌面刷新、文件选择器、普通窗口和全屏矩阵；
- NIGHT OPS 无白条、灰框、暗边或透明污染；
- 桌面空白框选/右键/原生图标与应用 input islands；
- 多显示器、DPI、分辨率、工作区和热插拔；
- 单海报完整 P0。

这些分别留在 QTS-04-02～05。QTS-04-01 没有重启 Explorer，也没有借用 Qt Quick candidate 的人工 PASS。

## 9. 结论

QTS-04-01 可以关闭。生产 Runtime 已从假 Host 切换到真实 DesktopHost，且层级、首帧、重建通知和清理都有可重复自动证据。下一步进入 QTS-04-02，只实现 NIGHT OPS 单海报 QPainter renderer，不提前混入输入岛或生命周期人工 P0。

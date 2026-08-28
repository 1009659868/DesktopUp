# TS-C01 R4 人工 P0 执行手册 V1

状态：CLOSED HISTORICAL RUNBOOK — ALL R4/R5 EXCEPTIONS RETAINED / USER LATER SELECTED QT IMPLEMENTATION ROUTE

建立日期：2026-08-24

## 1. 目的

本文把 TS-C01 的 R4 人工 P0 门拆成可重复、可停止、可追踪的四个子段。拆分只改变执行批次，不删除、合并或降低统一候选证据矩阵中的任何硬门。

R4 开始后，候选总状态仍保持 `P0 NOT RUN`，直到全部适用矩阵执行完、用户确认、原始材料完整且进入 R5 裁决。单一截图、一次运行、R2 已通过项目或 R3 自动结果都不能替代 R4。

## 2. 冻结输入

- Candidate：TS-C01 / Qt 6.8.3 / Qt Quick + native DesktopHost；
- configuration：Release；
- transport / ownership：OFFSCREEN_FRAME_SOURCE / HOST；
- R3 deployed executable SHA-256：`8FF9F292B31A92CFDCDB44D81F10B647AD3D3DC6750D6F003F35A7F44E87D86D`；
- R3 summary：`prototype/renderer-adapters/ts-c01-qt-quick/out/r3-preflight.json`；
- 权威矩阵：`UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md`；
- fail-fast：`TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md`。

R4 启动器每次都重新读取 R3 summary、验证 `result=PASS`、验证 `p0Result=NOT_RUN`，并核对部署版 EXE 的完整 SHA-256。哈希不一致时拒绝启动。

## 3. 会话入口

在 PowerShell 中运行：

~~~powershell
& '.\prototype\renderer-adapters\ts-c01-qt-quick\start-r4-manual.ps1' -DurationMs 60000 -SessionLabel 'r4a-01'
~~~

每次运行都会在被 Git 忽略的 `out/r4-manual-<timestamp>-<label>/` 下生成：

- `session.json`：候选、PID、哈希、时长和范围声明；
- `matrix-ready.txt`：自动矩阵和 stale-frame 门已完成，可以开始人工观察；
- `events.jsonl`：本次独立 E5 事件流。

启动器只建立本地人工会话，不创建正式 Candidate Evidence，不写 P0 PASS，也不修改业务状态。

会话自然结束后，用同一会话 ID 执行收尾；人工结果必须由实际观察者明确给出，未给出时保持 `NOT_REPORTED`：

~~~powershell
& '.\prototype\renderer-adapters\ts-c01-qt-quick\finish-r4-manual.ps1' -SessionId 'r4-manual-<timestamp>-r4a-01' -UserObservation 'PASS' -Note '本轮七项均通过'
~~~

收尾器会用冻结 E5 validator 验证事件流、确认进程退出和 adapter/host/quarantine 资源为 0，再把自动结果与人工观察分别写回 `session.json`。

## 4. R4 子段

| 子段 | 冻结范围 | 当前入口状态 | 关闭条件 |
| --- | --- | --- | --- |
| R4-A | 冷启动边界、Explorer 空白/图标、普通窗口基础层级 | USER-ACCEPTED PASS WITH 1/20 EXCEPTION | 2026-08-24 用户确认单次人工会话全部通过，并指示进入下一项；原定 20 次仅完成 1 次，例外永久保留 |
| R4-B | 真实拖动、缩放、收起/展开、锁定、USING↔EDITING、Item 命中 | USER-ACCEPTED PASS WITH COUNT EXCEPTION | 新哈希会话由用户确认稳定；事件仅记录 6 个 geometry gesture 与 16 个 command，未形成冻结 20 轮的完整计数证据，例外永久保留 |
| R4-C | 右键/刷新/文件选择器/焦点、键盘与任务栏 Win+D、控制中心、全屏、Explorer 重建/degraded | USER-ACCEPTED PASS WITH COUNT EXCEPTIONS — C2 7/6 / C3 1/10 | C1、C2、C3 均由用户确认通过；C2 和 C3 的实际计数例外永久保留 |
| R4-D | DPI/分辨率/工作区、主题/fallback、renderer fault、性能、清理、安装与重启 | CLOSED — D1 + D2 USER-ACCEPTED / D3 USER-ACCEPTED WITH WORKING-SET EXCEPTION / D4 USER-ACCEPTED EXECUTED SCOPE PASS | 工作集差异永久保留；自启延期；设备重启跳过；进入 R5 |

R4-B 的真实输入入口已经补齐。2026-08-25 会话 `r4-manual-20260825-104942-r4b-02` 的功能检查 1～7 通过，3813 条事件 E5 合规且退出零资源，但用户观察到拖动偶发卡顿/闪烁以及锁定/收起存在可感延迟，因此永久保留为 `SESSION_FAIL_RETAIN_FOR_DIAGNOSIS`。根因证据显示原始实现每个指针事件同步刷盘、逐移动同步重建圆角/强制重绘/逐个移动 islands，并由同步 `grabWindow()` 多次造成约 1000ms 呈现等待。修订版改为 16ms 合帧、批量 HWND 更新、250ms 日志批刷、异步 Qt Quick 抓帧和无动画原子状态切换；当前 D1 Release 的 10 个投影样本最大总延迟 44.548ms。
稳定化复测会话 `r4-manual-20260825-112934-r4b-stability-01` 共 583 条事件，E5 PASS、退出零资源；用户确认无拖动闪烁、无按钮可感延迟，也无白条/灰框、层级或输入回退，因此允许进入 R4-C。该会话只记录 6 个 geometry gesture 与 16 个 command，不能伪写为冻结矩阵的 20/20，故 R4-B 状态为 `USER-ACCEPTED PASS WITH COUNT EXCEPTION`，总体 P0 仍为 `NOT RUN`。

R4-D2 新构建由 `out/r3-run-20260825-160324/` 产生，双清洁构建可复现，Build ID 为 `ts-c01-qt-6.8.3-r4d2-20260825-02`，EXE SHA-256 为 `8FF9F292B31A92CFDCDB44D81F10B647AD3D3DC6750D6F003F35A7F44E87D86D`。正常/故障/R4-C/R4-D1/R4-D2 静态事件为 61/62/66/64/111 条并全部 E5 PASS。D2 自动门验证三个正常主题、5 次原子呈现、4 类拒绝故障、3 次 LKG 保留、1 次独立 fallback、2 次快速请求淘汰、唯一几何键与零资源；它不冒充完整主题包平台。

首个人工入口只提供全局组合键；用户报告肉眼无反应。日志证明控制器已收到默认主题 Ctrl+Shift+1 和保留当前 LKG 的 Ctrl+Shift+F，两者按契约都不产生可见变化，同时其余组合键未形成可靠输入证据。该入口因此判定为人工验证设计缺陷，不判定为主题渲染失败。修订构建 -02 增加可见原生按钮面板和状态文本；会话 r4-manual-20260825-160909-r4d2-control-panel-r2b 已由用户确认通过，137 条事件 E5 PASS、零资源。

R4-D3 新构建由 out/r3-run-20260825-170513/ 产生，Build ID 为 ts-c01-qt-6.8.3-r4d3-20260825-01，双 Clean Release 与部署后 EXE SHA-256 均为 4F1D76FCEC9C5271C1199D2CBAE48C748CA4EC0D8CA8C4D0AE50D325095CA673。新增 D3 静态日志 111 条事件并通过 E5：同一 attachment/shell/lifecycle 的 surface-only 恢复、surface epoch 仅 +1、旧 revision/旧 surface frame/重复当前 Shell 事件拒绝、200 Item ×64 轮排名布局、6 次主题切换和最终零资源均 PASS。

同一轮 10.223 秒静态采样得到 CPU 时间增量 62ms、0 新帧，均通过；工作集范围 135.648～136.332MiB，超过 HG-PERF-02 的 50MiB 目标。机器摘要因此正确记录 result=FAIL、P0=NOT_RUN。start-r4-manual.ps1 会拒绝该摘要，防止在工作集预算未解决时误启动 D3 人工门。

## 5. R4-A 每轮动作

每轮使用新的 `SessionLabel`，例如 `r4a-01` 至 `r4a-20`。看到海报后依次检查：

1. 首次出现没有顶部白条、底部灰框、暗灰透明包络、未绘制区或裁切；
2. 海报位于主屏中部，圆角和视觉边界与内容一致；
3. 从卡片下方或卡片之间的空白左拖，出现 Windows 桌面框选；
4. 同一空白右键，出现 Windows 桌面菜单，不出现框架或自定义错误菜单；
5. 位于海报空白覆盖范围内的原生快捷方式可单击选中、双击启动；
6. 将普通窗口移到海报上方，普通窗口始终覆盖海报，海报没有瞬时抬为 TopMost；
7. 运行自然结束后，海报及交互 islands 全部消失，无残留窗口或输入遮挡。

任何一轮失败立即停止累积次数，保留该会话目录、截图/录屏和复现动作；修正后使用新哈希、新会话重新开始受影响矩阵，不覆盖失败记录。

## 6. 后续子段的最低矩阵

### 6.1 R4-B

- 主屏中部拖动并松开 20 次；
- 最小、默认、最大宽度及连续缩放 20 次；
- 收起/展开 20 次，顶部栏位置固定；
- USING↔EDITING 与锁定↔解锁各 20 次；
- 标题、锁定、收起、缩放柄和六个 Item 命中正确；
- 操作一次后仍可继续操作；geometry、hit regions、render frame 使用同一 HostProjectionRevision。

### 6.2 R4-C

使用独立 R4-C 入口：

~~~powershell
& '.\prototype\renderer-adapters\ts-c01-qt-quick\start-r4-manual.ps1' -Phase R4C -DurationMs 900000 -SessionLabel 'r4c-01'
~~~

R4-C 分三组执行，不能把控制中心角色探针描述成已完成的产品控制中心：

1. **C1 外部 Windows 层级与显示入口**
   - 桌面右键后左键、桌面“刷新”、文件选择器和普通窗口焦点切换各 20 次；
   - 键盘 Win+D 10 轮、任务栏“显示桌面”10 轮；无触控板设备时触控板入口保持 `UNKNOWN`；
   - 普通全屏与无边框全屏进入/退出各 10 轮；
   - 全程海报不得隐藏再恢复、闪烁、覆盖普通/全屏窗口或改变用户显隐意图。
2. **C2 控制中心窗口角色**
   - 按 `Ctrl+Shift+C` 打开/关闭 `R4-C CONTROL CENTER ROLE PROBE` 20 轮；
   - 探针是普通非 TopMost 窗口，必须覆盖海报并正常获得焦点；
   - 关闭后再次拖动真实海报，拖动、锁定、收起/展开与空白穿透不得回退；
   - 本项只验证控制中心的层级/焦点角色，`product_control_center_implemented=false`，不宣称产品控制中心已经实现。
3. **C3 Explorer 重建与 degraded**
   - 从任务管理器依次“重新启动 Windows 资源管理器”10 轮；每轮等待海报恢复稳定后再开始下一轮；
   - 连续 2 个 250ms 样本确认旧父树失效后，先清除 visual/islands/renderer 并进入 `DEGRADED_NO_DESKTOP_HOST`；
   - 新 raised-Progman 安全目标连续 4 个 250ms 样本稳定后，才以新 attachment/surface epoch 恢复原位置、尺寸、锁定和收起状态；
   - 每轮只能恢复一组海报表面；30 秒恢复预算耗尽时明确 FAIL 并保持 degraded，绝不创建普通顶层或 TopMost 海报 fallback；
   - 收尾日志中的 `shellRecoveryCount`、`shellDegradedCount` 必须各为 10，`shellDegradedAtFinish=false`，且退出零资源。

### 6.3 R4-D

使用独立 R4-D 入口（从仓库根执行）：

~~~powershell
& '.\prototype\renderer-adapters\ts-c01-qt-quick\start-r4-manual.ps1' -Phase R4D -DurationMs 900000 -SessionLabel 'r4d-d1-01'
~~~


R4-D 按 D1～D4 顺序执行。TS-H01 的宿主结果只作为结构输入，不能借给 Qt 候选；R3 自动预检也不能替代人工 P0。

#### D1 — 候选显示环境与几何重投影

- 在当前单主显示器范围建立候选专用入口；多显示器、混合 DPI、负坐标、主屏切换和热插拔继续 `DEFERRED`；
- 100/125/150/200% 分别冷启、拖动、缩放和收起/展开；用户豁免重复次数时必须保留实际计数例外；
- 分辨率降低/恢复与任务栏自动隐藏/恢复分别形成独立会话；
- 记录逻辑几何、normalized anchor、monitor/work-area、有效 DPI、window/client/frame/render bounds 及同一 projection revision；
- 显示环境变化后按锚点重投影并迁回可见工作区，不写回物理像素，不产生白条、越界、旧尺寸帧、错误层级或命中回退。

#### D2 — 三主题、fallback 与隔离边界

- 使用三个结构等价的正常主题和一个独立 fallback；主题内容只验证 renderer 能力，不冒充完整主题平台已经实现；
- 快速连续切换按最后意图生效，无半模板、旧样式残留、空白帧或窗口重建；
- 注入未知主题、资源失败和无效参数，验证 last-known-good → fallback 的有限回退；
- 切换不得改变位置、尺寸、锁定、收起、层级、命中或 Item 身份；
- 验证主题载荷不能取得文件、网络、进程、注册表、凭据或通用 IPC 权限。

D2 使用独立入口（从仓库根执行）：

~~~powershell
& '.\prototype\renderer-adapters\ts-c01-qt-quick\start-r4-manual.ps1' -Phase R4D2 -DurationMs 900000 -SessionLabel 'r4d2-01'
~~~

人工会话会显示普通、非 TopMost 的 `R4-D2 THEME / FALLBACK MANUAL PROBE` 原生按钮面板。依次点击 `1 Transparent Default`、`2 Neon HUD`、`3 Cinema Lightbox`，再点击 `F/R/P` 三个 LKG 故障按钮；点击 `0 No Last-Known-Good` 后等待 fallback 显示，最后用 `1` 恢复。每次点击间隔至少 0.5 秒；300ms 内的连续请求按最后意图胜出。`Ctrl+Shift+1/2/3/F/R/P/0` 只作为可选辅助入口，不再承担唯一人工入口。

#### D3 — renderer 故障、旧事件与性能资源

- 注入 renderer/device loss、迟到 frame、旧 revision、重复 Shell 事件和控制中心释放；
- 状态不分叉，不接受旧 frame/hit，不无限重连，不创建普通顶层或 TopMost fallback；
- Release 单海报静态采样 10 秒：CPU 时间增量目标 ≤0.10s、工作集目标 ≤50 MiB；
- 静态/暂停目标 0 FPS，默认动态上限 30 FPS，隐藏或释放后非必要资源归零；
- 使用合成 200 Item 排名/布局和主题切换压力，核心交互不阻塞、主题切换目标 <200ms（不含完整布局重算）。

D3 自动夹具功能合同已通过。用户已明确接受以 136.332MiB 工作集峰值继续，50MiB 原目标不被改写并作为永久例外进入裁决；D3 因此关闭为 USER-ACCEPTED WITH WORKING-SET EXCEPTION，不再阻断 D4。

#### D4 — 当前用户安装、重启与清理

详细冻结合同、自动证据和正式人工顺序见 [R4-D4 当前用户生命周期计划](TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md)。稳定安装器为 prototype/renderer-adapters/ts-c01-qt-quick/out/r4d4-package/DesktopUpSetup.exe。

- 建立当前用户、无管理员的候选部署包，枚举 Qt 运行依赖和可分发材料；
- 自启必须显式 opt-in，禁用/卸载只删除 DesktopUp 自有入口；
- 验证普通启动、自启、第二实例、应用重启、设备重启、独立卸载和用户状态保留/恢复边界；
- 卸载后不得残留运行进程、托盘、桌面 HWND、输入岛或自启项。

D1 会话 `r4-manual-20260825-153036-r4d-d1-01` 已由用户确认通过；自然收尾 412 条事件、E5 PASS、零资源，实际捕获 2 次显示环境变化且无失败。用户豁免四档 DPI 与重复轮次，多显示器仍为 `DEFERRED`。D2 当前实现为宿主校验的内置结构化主题枚举，不加载第三方 QML/脚本；三个正常主题共享受保护结构，`minimal-fallback` 独立且不写回用户选择。

## 7. Fail-fast

出现以下任一现象即停止当前 run：

- 白条、灰框、暗灰透明边框、裁切或未绘制区；
- 海报覆盖普通程序、文件选择器、控制中心或全屏程序；
- Win+D 可见隐藏、最小化、cloak、detach 或 re-attach 闪帧；
- 空白被整窗截获，Explorer 框选、右键或原生图标失效；
- 操作一次后拖动/缩放/按钮失效；
- 旧 frame、旧 hit region、revision 分叉；
- Explorer 重建时出现重复海报、顶层 fallback 或无界重连；
- 退出后 HWND、surface、island、renderer 或输入残留。

## 8. 当前下一动作

R4-A 与 R4-B 均已由用户接受，但两段冻结循环数没有形成完整机器计数，因此例外永久保留。R4-C/C1 会话 `r4-manual-20260825-120817-r4c-c1-windows-01` 已由用户确认通过：桌面右键/刷新、文件选择器、焦点、键盘与任务栏 Show Desktop、全屏及最终交互回归均无闪烁、越级或输入回退。会话自然退出后收尾得到 520 条事件 E5 PASS、host/adapter/quarantine 与 visual/islands 最终均为 0；0 次 Shell loss、0 次 lifecycle failure、0 次 recovery/degraded，证明 C1 的 Win+D/刷新/焦点操作没有误触发 Shell 恢复。用户人工结论与自动收尾均为 PASS，但总体 P0 仍为 NOT RUN。

R4-C/C2 会话 `r4-manual-20260825-124220-r4c-c2-control-center-02` 已由用户确认层级、焦点和关闭后海报交互通过。会话收尾共 234 条事件，E5 PASS、退出资源为零、Shell recovery/degraded 均为 0；角色探针实际记录 7 次打开、6 次关闭。该结果按用户人工判定记为通过，但不得写成冻结计划的 20/20 完整证据，C2 计数例外永久保留。

R4-C/C3 会话 `r4-manual-20260825-143817-r4c-c3-explorer-04` 已由用户确认恢复成功。自然收尾共 183 条事件，E5 PASS、退出资源为零，实际记录 1 次 Shell degraded 与 1 次恢复，恢复耗时 1714ms，结束时不在 degraded；新 attachment/surface epoch 已建立。用户明确取消其余重复轮次，因此 C3 以实际 1/1 人工通过、冻结矩阵 1/10 计数例外归档，不能写成 10/10。

R4-D2 会话 r4-manual-20260825-160909-r4d2-control-panel-r2b 已由用户确认通过；自然收尾 137 条事件、E5 PASS、零资源。R4-D3 已按用户决定以工作集例外关闭。R4-D4 正式自定义目录安装、普通第二实例、应用重启、精确卸载与托盘清理已经用户确认；自启延期，设备重启跳过。R4 已关闭。R5-01～05 已完成并形成正式 Candidate Evidence；HG-PERF-02 明确失败。R6 用户后来选择 Qt 实施路线，但本手册中的例外不变。

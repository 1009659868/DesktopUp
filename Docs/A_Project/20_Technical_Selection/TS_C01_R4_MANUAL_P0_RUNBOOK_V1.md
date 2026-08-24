# TS-C01 R4 人工 P0 执行手册 V1

状态：ACTIVE — R4-A USER-ACCEPTED PASS WITH 1/20 EXCEPTION / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / OVERALL P0 NOT RUN / NO WINNER

建立日期：2026-08-24

## 1. 目的

本文把 TS-C01 的 R4 人工 P0 门拆成可重复、可停止、可追踪的四个子段。拆分只改变执行批次，不删除、合并或降低统一候选证据矩阵中的任何硬门。

R4 开始后，候选总状态仍保持 `P0 NOT RUN`，直到全部适用矩阵执行完、用户确认、原始材料完整且进入 R5 裁决。单一截图、一次运行、R2 已通过项目或 R3 自动结果都不能替代 R4。

## 2. 冻结输入

- Candidate：TS-C01 / Qt 6.8.3 / Qt Quick + native DesktopHost；
- configuration：Release；
- transport / ownership：OFFSCREEN_FRAME_SOURCE / HOST；
- R3 deployed executable SHA-256：`FA36CD18B46A466F413A08A3DB21F5412610F028080F7FFF203333EFF7548965`；
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
| R4-B | 真实拖动、缩放、收起/展开、锁定、USING↔EDITING、Item 命中 | AUTOMATED LOG PASS / AWAITING USER CONFIRMATION | 本轮 53 条事件和零资源清理通过；人工交互观察尚未回报，不得写成 PASS |
| R4-C | 右键/刷新/文件选择器/焦点、键盘与任务栏 Win+D、控制中心、全屏、Explorer 重建/degraded | PENDING | 对应 HG-Z/HG-LIFE 全部完成；Explorer 重建 10 轮 |
| R4-D | DPI/分辨率/工作区、主题/fallback、renderer fault、性能、清理、安装与重启 | PENDING | 对应 HG-RND/GEO/FAULT/PERF/PACK 全部完成 |

R4-B 的真实输入入口已经补齐：输入 island 提供 down/move/up/double/right 事件，拖动时保持兼容 HWND，视觉与命中区域原位预览，pointer up 时原子提交，并覆盖锁定、收起和 USING↔EDITING。2026-08-24 本轮会话 `r4-manual-20260824-174456-r4b-01` 产生 53 条合规事件，E5 与零资源清理通过；由于用户人工观察尚未回报，状态保持 `AWAITING USER CONFIRMATION`。

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

- 桌面右键后左键、刷新、文件选择器、焦点切换各 20 次；
- 键盘 Win+D 10 轮、任务栏“显示桌面”10 轮；无触控板设备时该入口保持 UNKNOWN；
- 控制中心打开/关闭 20 轮后再拖动真实海报；
- 普通全屏与无边框全屏进入/退出 10 轮；
- Explorer 重启 10 轮；每轮只有一组恢复表面，失败时进入明确 degraded，绝不回退普通顶层或 TopMost。

### 6.3 R4-D

- 已声明的 100/125/150/200% DPI、分辨率和工作区变化；
- 三个正常主题与 fallback 快速切换；
- renderer/device loss、主题失败、旧消息与重复 Shell 事件；
- 静态 CPU、工作集、帧率、暂停/隐藏/控制中心关闭和资源释放；
- 当前用户安装、自启 opt-in、卸载、应用重启和设备重启复测。

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

R4-A 第 1 个受控会话已由用户确认全部通过；E5 为 51/51 PASS，退出资源为零。用户明确指示进入下一项，因此 R4-A 记录为 `USER-ACCEPTED PASS WITH 1/20 EXCEPTION`，不把单次结果写成 20/20。R4-B 真实交互入口及受影响的 R3 回归均已完成；本轮自动日志与清理校验通过，下一动作是取得用户人工观察结果。未取得确认前不得进入 R4-C，也不得填写 P0 PASS。
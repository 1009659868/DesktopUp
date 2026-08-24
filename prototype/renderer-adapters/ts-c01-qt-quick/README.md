# TS-C01 Qt Quick Renderer Adapter

状态：R0 + R1 + R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN

本目录是 TS-C01（Qt 6.8.3 / Qt Quick）的独立 renderer candidate 工作区。R0 固定并验证入口工具链；R1 已把 Qt Quick surface 接入冻结的 DesktopHost 生命周期合同；R2 已完成 Night Ops 同构夹具并由用户通过六项真实 Explorer 人工门；R3 自动预检已通过。当前进入 R4 人工 P0；候选仍是 P0 NOT RUN，也没有创建 Candidate Evidence run。

## 固定边界

- Qt：6.8.3 / MSVC 2022 x64；
- generator：Visual Studio 18 2026；
- platform：x64；
- toolset：v143 / MSVC 14.44 / host=x64；
- Windows SDK：10.0.26100.0；
- configuration：Release；
- renderer：Qt Quick / Direct3D11；
- R1 transport：ADAPTER_OWNED_NATIVE_CHILD / ADAPTER；
- R2 transport：OFFSCREEN_FRAME_SOURCE / HOST-owned layered visual；
- Qt prefix：<QtInstallerRoot>\msvc2022_64。

禁止隐式退回其他 Qt 版本、默认 v145 工具集、未协商 transport、普通顶层窗口、TopMost 或 traditional WorkerW fallback。

## R0：入口预检

~~~powershell
& '.\preflight-ts-c01.ps1' -QtInstallerRoot '<QtInstallerRoot>'
~~~

通过项包括固定工具链、Release build、windeployqt、部署后 Qt Quick/QML 自检，以及 E5 validator 正反例自测。机器可读结果位于被 Git 忽略的 out/entry-preflight.json。

## R1：adapter-contract 预检

~~~powershell
& '.\preflight-r1.ps1' -QtInstallerRoot '<QtInstallerRoot>' -DurationMs 1200
~~~

R1 实现并映射：

- QueryCapabilities；
- NegotiateSurface；
- Attach；
- CommitProjection；
- NotifySurfaceReady；
- NotifyFramePresented；
- NotifySurfaceFault；
- ObserveHostState；
- RequestReconnect；
- Detach；
- BeginExit。

DesktopHost 只接受唯一可见 SHELLDLL_DefView 所在的 RAISED_PROGMAN icon host；Qt adapter 只接收已经核准的 native parent，不自行发现或重排 Explorer 窗口。绑定完成后必须满足 WS_CHILD、非 WS_POPUP、非 TopMost、WS_EX_NOACTIVATE、WS_EX_TRANSPARENT、owner=null，并在首帧前保持不可见。

preflight 会执行固定工具链核对、E5 validator 自测、Release build、全新 windeployqt 部署、真实 attach/首帧/Detach/BeginExit，然后离线验证 JSONL。成功结果必须同时具备：

- Direct3D11 surface_ready；
- 至少一个 frame_presented；
- DETACHED / RELEASED；
- 重复 Detach 成功；
- host、adapter、quarantine 资源计数全为 0；
- E5 validator PASS；
- ordinary_top_level_fallback=false；
- topmost=false。

机器可读结果位于被 Git 忽略的 out/r1-preflight.json；每轮事件流使用 out/r1-events-<run-id>.jsonl。它们是本地 R1 诊断，不是 P0 Candidate Evidence。

## R2：Night Ops 自动同构夹具

运行命令：

~~~powershell
& '.\preflight-r2.ps1' -QtInstallerRoot '<QtInstallerRoot>' -DurationMs 500
~~~

R2 固定覆盖单海报、六张 Item、展开/收起、锁定/解锁、USING/EDITING、标题移动、编辑缩放、零/一/多/重叠命中区域，以及 geometry、hit regions、render frame 共用 HostProjectionRevision。

### 首轮人工门失败记录

2026-08-24 首轮使用 ADAPTER_OWNED_NATIVE_CHILD 路线时，用户确认：

- FAIL：底部存在 footer 灰框；
- FAIL：空白不能拖出 Explorer 框选；
- FAIL：空白不能弹出 Windows 桌面菜单；
- FAIL：空白覆盖范围内的原生快捷方式不能操作；
- PASS：普通窗口、文件选择器和全屏程序高于海报；
- PASS：运行结束无残留。

根因是完整 Qt visual HWND 覆盖了整张海报。自动 ChildWindowFromPointEx 使用 CWP_SKIPTRANSPARENT 主动跳过该 HWND，因此 parent-internal PASS 不能代表真实跨进程鼠标分发；WS_EX_TRANSPARENT 单独加在普通 Qt 子窗口上也不等于 Explorer 穿透。

### 修订路线与自动结果

R2 已切换为冻结接口允许的 OFFSCREEN_FRAME_SOURCE / HOST ownership：

1. Qt 6.8.3 / Direct3D11 只生成离屏 Night Ops frame；
2. Host-owned layered transparent child 显示该 frame；
3. Qt backing child 位于 SHELLDLL_DefView 后方；
4. Host layered sibling interaction islands 承载明确命中；
5. footer 半透明底框已移除。

2026-08-24T15:44:48+08:00 修订版 Release 自动 preflight PASS：10 个矩阵样本、11 次投影、11 个对应 frame、49 条 E5 合规事件，Host layered visual 建立/释放各一次，unexpected visual pointer 为 0，最终 visual/island/host/adapter 资源均为 0。部署后二进制 SHA-256：2B5278AAEAF954DD60F0DF54CE85E9B6AEDF4F40CD6A9A0E3BA321C8022DC666。

## R2 人工门结果

用户已确认修订版 Release 的六项复测全部 PASS：无 footer 灰框/白条/额外外框；空白左拖、右键和覆盖范围内原生快捷方式保持 Explorer 原生行为；普通窗口、文件选择器与全屏程序高于海报；退出无残留。`out/r2-manual-retest-user.jsonl` 共 52 条 E5 合规事件，SHA-256 为 `0D2C60867F13B6DF5C3E0651C82ECBA8A3730CFBD4D73BD3C5A4EACA469F5E73`。

## R3：自动预检

运行命令：

~~~powershell
& '.\preflight-r3.ps1' -QtInstallerRoot '<QtInstallerRoot>' -StaticSampleDurationMs 3000
~~~

R3 覆盖双清洁 Release 可复现构建、windeployqt 与依赖枚举、E5 validator 自测、正常/故障 JSONL、attach/first-frame、stale/late frame、故障/detach/exit、零资源和静态性能。Qt rcc 通过 `SOURCE_DATE_EPOCH=1704067200` 固定自动资源时间戳；两次构建与部署后 EXE SHA-256 均为 `FA36CD18B46A466F413A08A3DB21F5412610F028080F7FFF203333EFF7548965`。

2026-08-24T17:43:53.1415382+08:00 本机结果：PASS。正常/故障事件为 51/52 条且均通过 E5；stale/late frame 各拒绝一次；注入 renderer fault、detach、exit 和 cleanup 通过；最终所有资源为 0。静态样本 3179.125 ms，single-core CPU 0.503%，working set 133.375～133.773 MiB；事件驱动静态 hold 期间无新 frame。

机器摘要位于 `out/r3-preflight.json`，原始运行位于 `out/r3-run-20260824-174241/`。Qt 二进制安装树未发现顶层 license payload，分发前仍需许可证材料审查；这不改变当前本地 R3 PASS，也不构成 P0 PASS。

## 当前下一动作：R4 人工 P0 门

完整分段与 fail-fast 见 [TS-C01 R4 人工 P0 手册](../../../Docs/A_Project/20_Technical_Selection/TS_C01_R4_MANUAL_P0_RUNBOOK_V1.md)。R4-A 单次受控会话命令：

~~~powershell
& '.\start-r4-manual.ps1' -DurationMs 60000 -SessionLabel 'r4a-01'
~~~

启动器核对 R3 PASS、P0 NOT_RUN 和完整 EXE SHA-256 后才启动，并为每轮创建独立本地 session/log。当前程序已经提供 R4-A 冷启动、边界、Explorer 空白/图标和基础层级，以及 R4-B 的真实拖动、连续缩放、收起/展开、锁定、USING↔EDITING 和 Item 命中入口。

会话退出后运行 `finish-r4-manual.ps1 -SessionId '<本轮 ID>'`；它验证 E5 与零资源清理。人工结果未报告时保持 `AUTOMATED_LOG_PASS_AWAITING_USER_OBSERVATION`。

R4 按冻结统一矩阵执行：

1. 20 次冷启动边界检查；
2. 20 次拖动、缩放、收起/展开和模式切换；
3. Explorer 空白左键/框选、右键与原生图标；
4. 普通窗口、文件选择器、焦点与全屏层级；
5. 键盘、任务栏和 Win+D；
6. 控制中心开关；
7. Explorer 重建与 degraded；
8. 已声明 DPI/分辨率/工作区；
9. 主题/renderer 故障；
10. 性能、清理、安装和重启复测。

任何白条、灰框、瞬时 TopMost、隐藏再恢复、整窗输入接管、旧 frame/hit 或资源残留都立即 fail-fast。R4-A 已由用户接受并永久保留 1/20 例外；R4-B 本轮自动事件与清理校验通过，等待用户人工观察确认。R4-C、R4-D 尚未执行，因此不得建立最终 Candidate Evidence、不得填写 P0 PASS，也没有 renderer 赢家。
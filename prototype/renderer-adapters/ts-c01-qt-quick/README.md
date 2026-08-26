# TS-C01 Qt Quick Renderer Adapter

状态：R0 + R1 + R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B USER-ACCEPTED WITH COUNT EXCEPTION / R4-C USER-ACCEPTED WITH COUNT EXCEPTIONS C2 7/6 + C3 1/10 / R4-D D1 USER-ACCEPTED / D2 USER-ACCEPTED / D3 USER-ACCEPTED WITH WORKING-SET EXCEPTION (136.332 MiB; 50 MiB TARGET RETAINED) / D4 AUTO PACKAGE PASS / FORMAL LIFECYCLE ACTIVE / P0 NOT RUN

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
& '.\preflight-r3.ps1' -QtInstallerRoot '<QtInstallerRoot>' -StaticSampleDurationMs 10000
~~~

当前自动预检覆盖双 Clean Release、部署、E5、R3 生命周期、R4-C、R4-D1、R4-D2 与 R4-D3。D3 Build ID 为 ts-c01-qt-6.8.3-r4d3-20260825-01，两次构建与部署后 EXE SHA-256 均为 4F1D76FCEC9C5271C1199D2CBAE48C748CA4EC0D8CA8C4D0AE50D325095CA673。

2026-08-25T17:05 本机结果：D3 FUNCTIONAL PASS / HG-PERF-02 FAIL。正常/故障/R4-C/R4-D1/R4-D2/R4-D3 静态事件为 61/62/66/64/111/111 条并全部 E5 PASS；D3 surface-only 恢复、旧事件拒绝、200 Item ×64 轮 0.180ms、6 次主题切换最大 27.106ms 和零资源均通过。10.223 秒采样 CPU 62ms、静态帧 0 PASS；工作集 135.648～136.332MiB，超过 50MiB 目标。

机器摘要位于 out/r3-preflight.json，原始运行位于 out/r3-run-20260825-170513/。摘要 result=FAIL、P0=NOT_RUN；该失败是工作集硬门的预期 fail-fast，不得改写为候选 P0 PASS。

## 当前下一动作：R4 人工 P0 门

完整分段与 fail-fast 见 [TS-C01 R4 人工 P0 手册](../../../Docs/A_Project/20_Technical_Selection/TS_C01_R4_MANUAL_P0_RUNBOOK_V1.md)。R4-A 单次受控会话命令：

~~~powershell
& '.\start-r4-manual.ps1' -DurationMs 60000 -SessionLabel 'r4a-01'
~~~

启动器核对当前自动摘要 PASS、P0 NOT_RUN 和完整 EXE SHA-256 后才启动，并为每轮创建独立本地 session/log。当前 D3 摘要因工作集硬门为 FAIL，因此启动器会有意拒绝新人工会话，避免绕过 HG-PERF-02。

R4-C 启动命令：

~~~powershell
& '.\start-r4-manual.ps1' -Phase R4C -DurationMs 900000 -SessionLabel 'r4c-01'
~~~

海报显示后按 `Ctrl+Shift+C` 打开/关闭控制中心角色探针。该窗口明确记录 `product_control_center_implemented=false`，只验证层级和焦点；Explorer 重建则由 250ms 有界监控在旧代清理/degraded 后恢复新 attachment，30 秒预算耗尽时 fail-fast，绝不创建顶层海报 fallback。

R4-D1 启动命令：

~~~powershell
& '.\start-r4-manual.ps1' -Phase R4D -DurationMs 900000 -SessionLabel 'r4d-d1-01'
~~~

D1 只验证当前主屏：触发一次分辨率或工作区变化后，海报应按逻辑 DIP 尺寸和 normalized anchor 重投影并留在可见工作区；不得出现白条、旧帧、层级或命中回退。多显示器仍为 `DEFERRED`。

R4-D2 启动命令：

~~~powershell
& '.\start-r4-manual.ps1' -Phase R4D2 -DurationMs 900000 -SessionLabel 'r4d2-01'
~~~

D2 人工入口会显示普通、非 TopMost 的 `R4-D2 THEME / FALLBACK MANUAL PROBE` 原生按钮面板。依次点击三个主题按钮、三个 LKG 故障按钮和独立 fallback 按钮，每次间隔至少 0.5 秒；最后点击 `1 Transparent Default` 恢复。`Ctrl+Shift+1/2/3/F/R/P/0` 仅保留为可选辅助入口，不再作为唯一人工入口。

R4-D3 自动功能夹具已完成；用户已接受 136.332MiB 对 50MiB 原目标的永久工作集例外。Phase R4D3 入口保留用于复现，当前主线已进入 D4 正式生命周期门。

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

任何白条、灰框、瞬时 TopMost、隐藏再恢复、整窗输入接管、旧 frame/hit 或资源残留都立即 fail-fast。R4-A～C 已接受并保留既有计数例外；R4-D1、D2 已通过；D3 以用户接受的 136.332MiB 对 50MiB 原目标例外关闭。D4 自动包门 PASS，正式生命周期 ACTIVE；不得建立 Candidate Evidence、填写 P0 PASS 或宣布 renderer 赢家。


## R4-D4 current-user package

构建与隔离预检：

~~~powershell
& '.\build-r4d4-installer.ps1' -QtPrefix '<QtPrefix>'
~~~

稳定产物：`out/r4d4-package/DesktopUpSetup.exe`。Build ID `ts-c01-qt-6.8.3-r4d4-20260826-02`；自动包门 PASS，安装器提供目录选择页，默认 `%LOCALAPPDATA%\Programs\DesktopUp`；无部署路径冲突的既有非空目录允许安装并已通过用户哨兵保留探针。正式运行资格由相邻所有者标记与 HKCU `InstallLocation` 共同判定，因此默认目录和用户所选目录均受支持，解压/复制运行仍被拒绝；卸载按构建期精确清单删除部署文件，不递归删除整个所选目录。自定义目录正式生命周期、设备重启和正式卸载仍未完成，候选数据隔离到 `%LOCALAPPDATA%\DesktopUp\Candidate\TS-C01`。

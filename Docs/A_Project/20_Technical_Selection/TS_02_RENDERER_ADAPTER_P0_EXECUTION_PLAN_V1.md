# TS-02 Renderer Adapter / P0 执行计划 V1

状态：ACTIVE — TS-C01 R0 + R1 + R2 PASS / R3 AUTOMATED PREFLIGHT PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / NO CANDIDATE P0 RESULT

建立日期：2026-08-24

## 1. 计划目的

本文把已经完成的公共 DesktopHost 探针与 E1～E6 合同交给真实 renderer 候选。

TS-H01 只证明公共宿主结构在当前证据范围内可行；它没有证明 Qt、Direct2D/DirectComposition、WinUI、WPF、Avalonia、WebView2 或 Slint 中任何一个 renderer 能满足 DesktopUp V2。

TS-02 renderer adapter/P0 的目标是让每个候选在相同条件下：

1. 接入同一个 DesktopHost 合同；
2. 呈现同一个 Night Ops 单海报夹具；
3. 输出同一个 E5 诊断事件 schema；
4. 执行同一个 P0 硬门子矩阵；
5. 形成独立、不可借用、可复现的证据；
6. 得出 PASS、FAIL、CONDITIONAL 或 WITHDRAWN，而不是提前选择赢家。

## 2. 权威输入

1. [统一候选证据矩阵](UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md)
2. [技术候选登记表](CANDIDATE_REGISTER_V1.md)
3. [DesktopHost 原型接口](DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md)
4. [E3 生命周期合同](TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md)
5. [E4 几何、命中与 render 原子合同](TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md)
6. [E5 诊断与一致性夹具](TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md)
7. [E6 候选交接规则](TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md)
8. [AX-BP 冻结蓝图](../30_Architecture/AXIOM_BLUEPRINT_INDEX.md)
9. [AX-BP-06 交接](../30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)

发生冲突时，用户最新明确决定、冻结需求、DesktopHost E2～E6、AX-BP 冻结不变量、本文依次生效。候选私有 API 不能降低公共合同。

## 3. 候选顺序与身份

按照 E6 的稳定 ID 顺序建立独立 adapter：

| 顺序 | ID | 候选 | 初始状态 |
| ---: | --- | --- | --- |
| 1 | TS-C01 | Qt 6.8.3 / Qt Quick + native DesktopHost | R0 + R1 + R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN |
| 2 | TS-C02 | Win32 + Direct2D/DirectComposition | P0 NOT RUN |
| 3 | TS-C03 | WinUI 3 / Composition | P0 NOT RUN |
| 4 | TS-C04 | WPF + native DesktopHost | P0 NOT RUN |
| 5 | TS-C05 | Avalonia + native DesktopHost | P0 NOT RUN |
| 6 | TS-C06 | WebView2 + native DesktopHost | P0 NOT RUN / HISTORICAL RISK |
| 7 | TS-C07 | Slint + native DesktopHost | P0 NOT RUN |
| — | TS-C08 | native poster + separate high-level control center | DEFERRED UNTIL ONE POSTER CANDIDATE PASSES P0 |

编号不代表排名。先完成也不代表更优。公共 TS-H01 结果不能借给 TS-C02 或其他候选。

## 4. 每个候选的工作包

### TS-02-R0：进入门

必须同时完成：

- 固定候选、SDK、编译器、构建器和依赖版本；
- 保存官方来源、许可证与部署事实；
- 声明 SurfaceOffer、transport、ownership、线程/事件循环和销毁边界；
- 明确 Night Ops 同构范围；
- E5 validator 正反例自测通过；
- 确认证据目录没有预填 PASS；
- 明确公共宿主证据只作前置输入。

R0 未通过时，不编写 candidate adapter 产品逻辑，不创建候选 Evidence run。

### TS-02-R1：最小 adapter 边界

建立独立目录：

~~~text
prototype/renderer-adapters/ts-cxx-<name>/
  README.md
  CMake/project files
  adapter/
  fixture/
  diagnostics/
  fault-hooks/
~~~

adapter 必须显式映射：

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

不允许候选绕过 DesktopHost 自行修改桌面 parent、owner、z-order、Win+D 或 Explorer recovery。

### TS-02-R2：Night Ops 同构夹具

最低内容必须包含：

- 单张 Night Ops 海报；
- 六张基线 Item；
- 默认透明视觉；
- 展开/收起；
- 锁定/解锁；
- USING/EDITING；
- 标题拖动与编辑缩放；
- 零/一/多/重叠命中区域；
- 相同逻辑尺寸、圆角、按钮和卡片位置；
- 同 revision geometry/hit/render 投影。

不得为了让候选通过而删除透明、圆角、文本、命中或动态尺寸要求。

### TS-02-R3：自动预检

候选至少执行：

1. Release 构建；
2. 依赖与许可证枚举；
3. E5 正反例 validator 自测；
4. candidate JSONL 离线校验；
5. attach/first-frame；
6. stale revision/late frame；
7. fault、detach、exit；
8. 最终零资源；
9. 静态 CPU、工作集和帧率采样；
10. 两次干净构建可复现性检查。

自动预检通过不等于 P0 PASS。

### TS-02-R4：人工 P0 门

按统一矩阵执行：

- 20 次冷启动边界检查；
- 20 次拖动、缩放、收起/展开和模式切换；
- 桌面空白左键/框选、右键与原生图标；
- 普通窗口、文件选择器、焦点与全屏层级；
- 键盘、任务栏和可用时触控板 Win+D；
- 控制中心开关；
- Explorer 重建与 degraded；
- 已声明 DPI/分辨率/工作区；
- 主题/renderer 故障；
- 性能、清理、安装和重启复测。

任何白条、灰框、瞬时 TopMost、隐藏再恢复、整窗输入接管、旧帧/旧命中或资源残留均按 E6 fail-fast 处理。

### TS-02-R5：证据与裁决

只有真实运行后才创建：

~~~text
Docs/A_Project/20_Technical_Selection/Evidence/
  TS-Cxx/<matrix-id>/<run-id>/
~~~

每个 run 保存 environment、reproduce、record、JSONL、媒体、指标和哈希。修改代码后新建 run，失败或中断记录不得覆盖。

### TS-02-R6：候选交接

- PASS：进入 full matrix/短名单；
- FAIL：保留证据并退出当前路线；
- CONDITIONAL：只允许一个事先登记的有界修正和一次全量重跑；
- WITHDRAWN：记录工具链、许可、维护或事实原因；
- NOT RUN：不得评分。

全部适用 P0 硬门通过后才进入加权评分。

## 5. 公共冻结不变量

所有候选必须满足：

- 普通/系统/全屏程序 > 海报 > 桌面图标 > 壁纸；
- Win+D 不产生可见隐藏、最小化、cloak、detach 或 re-attach frame；
- DesktopHost 与 Renderer 是独立端口；
- StateStore accepted snapshot 是唯一业务权威；
- geometry、hit regions 和 render frame 必须关联同一 HostProjectionRevision；
- 空白保持 Explorer 原生行为；
- Host 拥有桌面层、parent/owner/z-order 和交互岛语义；
- Renderer 不写业务状态；
- 失败保留 last-known-good/accepted state；
- 禁止 TopMost、普通顶层 fallback、Explorer 注入、固定 HWND 和轮询抬层。

## 6. 证据范围声明

以下不构成 candidate PASS：

- TS-H01 公共宿主人工通过；
- E5 validator 自测通过；
- 框架官方文档；
- 单张截图；
- Debug 构建；
- 一次显示成功；
- “可以增加 native workaround”但没有实现和证据；
- AX-BP 类型夹具通过。

D2/D3 已登记的 USER-ACCEPTED、PARTIAL、UNKNOWN 和 DEFERRED 也不会自动传播给候选。

## 7. 首个候选：TS-C01

TS-C01 按登记顺序首先进入 R0。

当前固定候选为 Qt 6.8.3 / Qt Quick / Windows x64 / MSVC 2022-compatible ABI。6.11.2 安装失败后已按受控修订切换精确版本；详细进入审计见 [TS-C01 Qt Quick 进入门审计](TS_C01_QT_QUICK_ENTRY_AUDIT_2026-08-24.md)。

当前结论：

- 官方版本和组件名已确认；
- Windows SDK、CMake 和 MSVC 19.44 可用；
- E5 validator 正反例自测通过；
- `<QtInstallerRoot>\msvc2022_64` 的 qmake、qtpaths、windeployqt、Qt Quick/QML 已核验；
- R0 Release build、windeployqt 部署、部署后 QML 自检与 E5 validator 均通过；
- R1 已实现独立 DesktopHost adapter 端口、ADAPTER_OWNED_NATIVE_CHILD surface 和有界生命周期；
- R1 Release 全新部署运行报告 Direct3D11 surface_ready、首帧、幂等 Detach、BeginExit 与零资源清理；
- R1 JSONL 共 10 个事件并通过冻结 E5 validator，未使用 TopMost 或普通顶层 fallback；
- 未创建 TS-C01 Evidence run；
- R2 修订版 OFFSCREEN_FRAME_SOURCE / HOST 路线的六项真实 Explorer 人工门已由用户确认 PASS；`r2-manual-retest-user.jsonl` 共 52 条 E5 合规事件；
- R3 自动预检已完成双清洁可复现构建、依赖枚举、正常/故障 E5、stale/late frame、故障注入、静态性能和零资源清理并 PASS；
- 当前状态为 R0 + R1 + R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN。

## 8. R2 Night Ops 同构夹具记录

R2 已实现六张 Item、展开/收起、锁定/解锁、USING/EDITING、标题移动、编辑缩放及零/一/多/重叠命中区域。每个场景都以同一 HostProjectionRevision 原子提交 geometry、hit regions 和 render frame。

### 8.1 首轮人工失败与根因

首轮 ADAPTER_OWNED_NATIVE_CHILD 自动 preflight 曾通过，但 2026-08-24 人工门得到 2 PASS / 4 FAIL：

| 项目 | 结果 |
| --- | --- |
| 白条/灰框 | 无白条；footer 灰框 FAIL |
| Explorer 空白左拖 | FAIL |
| Explorer 空白右键 | FAIL |
| 空白覆盖下原生快捷方式 | FAIL |
| 普通/文件选择/全屏程序层级 | PASS |
| 退出清理 | PASS |

自动检查通过的原因是 ChildWindowFromPointEx 显式使用 CWP_SKIPTRANSPARENT，主动跳过覆盖整张海报的 Qt HWND；这不能证明真实跨进程鼠标分发。完整 Qt visual HWND 才是三项 Explorer 输入同时失败的直接阻断。

### 8.2 有界修订

修订不改变 DesktopHost 层级和命中合同，只把 R2 surface plan 切换到已冻结的 OFFSCREEN_FRAME_SOURCE / HOST ownership：

- Qt Direct3D11 backing child 位于 SHELLDLL_DefView 后方，只负责生成 frame；
- Host-owned WS_EX_LAYERED | WS_EX_TRANSPARENT child 显示 frame；
- 独立 layered sibling islands 恢复明确交互；
- footer 半透明底框已移除；
- 禁止 TopMost、普通顶层 fallback、HTTRANSPARENT 和向 Explorer 转发消息。

修订版 Release 自动 preflight：

| 字段 | 结果 |
| --- | --- |
| Timestamp | 2026-08-24T15:44:48+08:00 |
| Qt / backend | 6.8.3 / Direct3D11 |
| Transport / ownership | OFFSCREEN_FRAME_SOURCE / HOST |
| Matrix samples | 10 |
| Projection / frame | 11 / 11 |
| E5 events | 49 / PASS |
| Host layered visual | ready=1 / released=1 |
| Unexpected visual pointer | 0 |
| Cleanup | visual=0 / island=0 / host=0 / adapter=0 |
| Executable SHA-256 | 2B5278AAEAF954DD60F0DF54CE85E9B6AEDF4F40CD6A9A0E3BA321C8022DC666 |
| R1 regression | PASS |
| Candidate Evidence | 未创建；P0 NOT RUN |

## 9. R2 真实 Explorer 人工门关闭

用户已在修订版 Release 上确认六项全部 PASS：

1. footer 灰框消失，海报无白条或额外外框；
2. 海报空白左拖可出现 Windows 桌面框选；
3. 同一空白右键可出现 Windows 桌面菜单；
4. 空白覆盖范围内的原生快捷方式可单击和双击；
5. 普通窗口、文件选择器与全屏程序高于海报；
6. 运行结束后所有表面与 islands 消失，无残留。

对应用户复测日志为本地忽略文件 `out/r2-manual-retest-user.jsonl`，包含 52 条 E5 合规事件；SHA-256 为 `0D2C60867F13B6DF5C3E0651C82ECBA8A3730CFBD4D73BD3C5A4EACA469F5E73`。R2 因此关闭为 PASS，但不等于候选 P0 PASS。

## 10. R3 自动预检记录

执行时间：2026-08-24T17:43:53.1415382+08:00。

| 字段 | 结果 |
| --- | --- |
| Qt / toolchain | Qt 6.8.3；MSVC 19.44 / v143 14.44；Windows SDK 10.0.26100.0 |
| Deterministic resource input | `SOURCE_DATE_EPOCH=1704067200`；用于固定 Qt rcc 资源时间戳 |
| Clean builds | 2；同一受校验规范路径，每轮删除并全新配置/构建 |
| Reproducible executable | PASS；两轮与部署后 SHA-256 均为 `FA36CD18B46A466F413A08A3DB21F5412610F028080F7FFF203333EFF7548965` |
| Deployment inventory | 51 files / 39 runtime binaries |
| E5 validator self-test | PASS：known-good accepted / known-bad rejected |
| Normal diagnostics | PASS：51 events conform |
| Fault diagnostics | PASS：52 events conform |
| Attach / first frame | PASS / Direct3D11 |
| Stale / late frame | 各 1 次拒绝；PASS |
| Injected renderer fault | `R3_INJECTED_RENDERER_DEVICE_LOSS`；detach/exit/cleanup PASS |
| Final resources | visual=0 / island=0 / host=0 / adapter=0 / quarantine=0 |
| Static sample | 3194.157 ms；single-core CPU 0.501%；working set 133.254～133.590 MiB |
| Static frame result | 0 new frames；当前夹具为事件驱动静态海报，无持续动画，不把 0 解释为渲染失败 |
| Qt license payload | 安装树未发现顶层 license payload；分发前需单独补做 Qt 许可证材料审查，不阻断本次本地技术预检 |
| Candidate Evidence | 未创建；P0 NOT RUN |

机器可读摘要位于被 Git 忽略的 `out/r3-preflight.json`；本轮原始运行目录为 `out/r3-run-20260824-174241/`。R3 自动 PASS 只允许进入 R4，不能填写候选 P0 PASS。

## 11. 当前下一动作：R4 人工 P0 门

按冻结统一矩阵执行 R4：20 次冷启动；20 次拖动/缩放/收起展开/模式切换；Explorer 空白与原生图标；普通窗口、文件选择器、焦点与全屏；键盘、任务栏和 Win+D；控制中心；Explorer 重建/degraded；已声明 DPI/分辨率/工作区；主题/renderer 故障；性能、清理、安装和重启复测。

执行细则见 [TS-C01 R4 人工 P0 手册](TS_C01_R4_MANUAL_P0_RUNBOOK_V1.md)。R4 被拆成 A～D 四个可追踪批次；完整矩阵不变。R4-A 已由用户接受并保留 1/20 例外；R4-B 真实交互入口已经补齐，本轮 53 条自动事件与零资源清理均通过，当前等待用户人工观察确认。

任何白条、灰框、瞬时 TopMost、Win+D 隐藏再恢复、整窗输入接管、旧帧/旧命中或资源残留均按 E6 fail-fast。R4 未经用户完成并确认前，状态保持 `P0 NOT RUN / NO WINNER`，不得创建最终裁决或进入 R5。
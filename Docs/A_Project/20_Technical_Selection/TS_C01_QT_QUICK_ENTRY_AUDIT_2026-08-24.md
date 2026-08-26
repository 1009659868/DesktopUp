# TS-C01 Qt 6 / Qt Quick 进入门审计

状态：R5-01～05 COMPLETE / 2 PASS + 1 FAIL + 34 UNKNOWN + 4 NOT RUN / FAIL RECOMMENDED / R6 USER DECISION PENDING / NO WINNER

日期：2026-08-24

## 1. 审计目的

本文执行 E6 第 3 节的候选进入门，只回答 TS-C01 是否具备开始真实 adapter/P0 的条件。

它不是候选证据，不产生 P0 PASS，不创建 Evidence run，也不表示 Qt 被推荐或获选。

## 2. 候选身份冻结

| 字段 | 冻结值 |
| --- | --- |
| Candidate ID | TS-C01 |
| Framework | Qt 6 / Qt Quick |
| Exact version | 6.8.3 |
| Release date | 2025-03-27 |
| Target | Windows 11 x64 |
| Qt binary ABI | MSVC 2022 x64 |
| Official component | qt.qt6.683.win64_msvc2022_64 |
| Build system | CMake |
| Prototype configuration | Release |
| Desktop host | frozen native DesktopHost contract |
| Current disposition | R5-01～05 COMPLETE / 2 PASS + 1 FAIL + 34 UNKNOWN + 4 NOT RUN / FAIL RECOMMENDED / R6 USER DECISION PENDING |

Qt 6.11.2 在线安装失败后，用户提供了本机 Qt 6.8.3。该版本作为 TS-C01 的受控版本修订：Candidate ID、框架类别和 P0 状态不变，但从本次修订起所有构建与证据必须精确标注 6.8.3，禁止借用先前 6.11.2 的进入事实。

官方来源：

- [Qt 6.8.3 Released](https://www.qt.io/blog/qt-6.8.3-released)
- [Qt 6.8 Supported Platforms](https://doc.qt.io/qt-6.8/supported-platforms.html)
- [Qt installer command line](https://doc.qt.io/qt-6/get-and-install-qt-cli.html)
- [Qt for Windows deployment](https://doc.qt.io/qt-6/windows-deployment.html)
- [QWindow](https://doc.qt.io/qt-6/qwindow.html)

## 3. 本机工具链事实

| 项目 | 结果 |
| --- | --- |
| OS build | Windows build 26100 |
| Architecture | x64 |
| Visual Studio | Visual Studio Community 2026 18.3.2 |
| MSVC 2022-compatible compiler | 14.44.35207 / cl 19.44.35223 |
| Additional compiler | 14.42.34433 present |
| Windows SDK | 10.0.26100.0 and 10.0.22621.0 |
| CMake | 4.4.2 |
| Python | CPython 3.12 |
| Qt commands | qmake/qtpaths/windeployqt all present and report/target 6.8.3 |
| Qt prefix | <QtInstallerRoot>\msvc2022_64 |
| Qt modules | Qt Quick and QML CMake packages present |
| Qt6Core.dll | ProductVersion/FileVersion 6.8.3.0 |

Visual Studio 18 的默认新工具集不能冒充 Qt 官方支持配置。TS-C01 构建必须显式使用 MSVC 14.44/19.44 这一 MSVC 2022 系列工具集，并在 build record 保存实际 cl/link 版本。

## 4. SurfaceOffer 与物理边界草案

TS-C01 首轮登记的 bounded route 为：

~~~text
SurfaceTransportKind = ADAPTER_OWNED_NATIVE_CHILD
SurfaceOwnership     = ADAPTER
Host owns            = desktop attachment, parent/owner/z-order,
                       hit islands, Shell recovery, geometry commit
Qt adapter owns      = QGuiApplication, QQmlEngine, QQuickWindow,
                       visible Qt child surface and Qt resources
~~~

该路线通过了自动 parent-internal 检查，但随后在真实 Explorer 人工门中失败：完整 Qt child 覆盖 visual bounds，导致空白框选、右键和原生快捷方式全部被阻断。根据 fail-fast 规则，旧路线保留为失败记录，不再作为当前实现。

首轮失败后登记一次有界 transport 修订：

~~~text
SurfaceTransportKind = OFFSCREEN_FRAME_SOURCE
SurfaceOwnership     = HOST
Host owns            = layered transparent visual, desktop attachment,
                       parent/owner/z-order, hit islands and geometry commit
Qt adapter owns      = QGuiApplication, QQmlEngine, QQuickWindow backing,
                       Direct3D11 frame generation and Qt resources
~~~

当前约束：

1. Qt backing child 位于 SHELLDLL_DefView 后方，不作为用户可见桌面表面；
2. Host layered visual 是唯一可见 visual HWND，并保持 WS_EX_LAYERED | WS_EX_TRANSPARENT；
3. Host 提供并验证唯一允许的 native parent；
4. adapter 不得自行附着 Progman/WorkerW，不得设置 TopMost；
5. HostProjectionRevision 改变时，Qt frame、Host visual 与 hit regions 必须同修订发布；
6. adapter 不得创建可见普通顶层 fallback；
7. Qt frame source release 必须在 host-owned visual 销毁后有界完成；
8. 修订版必须重新执行 R0/R1/R2 和完整 P0，旧自动 PASS 不得借用。

该修订是首轮失败后的显式、有界 correction，不是静默 fallback，也没有提前选择 QQuickRenderControl 或生产 transport。

## 5. 线程与事件循环草案

P0 原型采用单进程：

- Qt GUI 主线程拥有 QGuiApplication、QQmlEngine 和 QQuickWindow；
- DesktopHost 的窗口与 Shell 消息也在同一 GUI 消息线程协调；
- Qt Quick 内部 render thread 由 Qt 管理，但必须输出首帧、render revision、surface fault 和释放事件；
- 外部请求只能 marshaling 到 GUI owner thread；
- Detach/BeginExit 必须 fence 新请求，再释放 Qt surface，最后释放 host-owned resources；
- 不把该单进程映射写成生产架构结论。

## 6. Night Ops 同构范围

TS-C01 不允许简化以下内容：

- 默认透明海报背景与圆角；
- 顶部标题、锁定与收起按钮；
- NIGHT OPS 标题；
- 六张 Item 卡片及标签；
- 展开与收起高度；
- 标题拖动；
- EDITING resize islands；
- USING/EDITING、lock、runtime mode；
- 零/一/多/重叠 hit regions；
- 透明空白和原生桌面输入；
- E5 identity/revision/bounds/resource fields。

主题系统、AI、真实 StateStore、安装器和 200 Item 算法不在首个 renderer adapter 的 R1 范围，但其接口和失败边界不得被封死。

## 7. E6 进入门核对

| 进入条件 | 状态 | 证据/缺口 |
| --- | --- | --- |
| 引用 E2～E5 冻结合同 | PASS | TS-02 execution plan |
| 固定候选版本、SDK/toolchain | PASS | 6.8.3 / MSVC 2022 x64；固定 Release build 已通过 |
| SurfaceOffer/ownership/event loop/destruction | PASS / R1 VERIFIED | ADAPTER_OWNED_NATIVE_CHILD；Qt adapter owns surface；DesktopHost owns parent/z-order policy |
| 同构 Night Ops 范围明确 | PASS AS ENTRY DESIGN | 本文第 6 节 |
| E5 validator 正反例自测 | PASS | 2026-08-24：positive accepted, stale-frame rejected |
| 证据目录为空且无预填 PASS | PASS | 没有 TS-C01 run |
| 公共宿主结果不借给候选 | PASS | 状态仍 P0 NOT RUN |
| Qt SDK 可构建 | PASS | Release build、windeployqt 部署和部署后 QML 自检均通过 |

TS-C01 R0、R1 与 R2 自动 preflight 已通过；R2 的真实 Explorer 跨进程输入/层级人工门仍未执行。R1/R2 只证明真实 adapter/native-child surface、Night Ops 同构投影、首帧与清理合同，不等于 P0 PASS。

## 8. 安装边界

已验证的 Qt prefix 位于仓库之外：

~~~text
<QtInstallerRoot>\msvc2022_64
~~~

需要的官方组件：

~~~text
qt.qt6.683.win64_msvc2022_64
~~~

原则：

- 不把 Qt SDK、installer 或账户缓存提交进 V2 仓库；
- 不读取、记录或复制 Qt Account token；
- 不安装 Qt Creator、Android、WebEngine、sources 或 debug symbols，除非后续硬门证明需要；
- 下载后记录 installer hash、Qt package version 和安装路径；
- 构建后用 windeployqt 生成最小运行依赖，再单独形成许可证/归属清单；
- 安装和接受许可必须由用户授权。

2026-08-24 本机实测：qmake 与 qtpaths 均报告 6.8.3，Qt6Core.dll 为 6.8.3.0，Qt Quick/QML 与 windeployqt 齐全。6.11.2 安装失败只作为进入历史记录，不再是当前阻断。

## 9. 进入 R1 的通过条件

必须全部满足：

1. qmake/qtpaths/windeployqt 指向 6.8.3；
2. Qt6Config.cmake 可由固定 CMAKE_PREFIX_PATH 找到；
3. Qt Quick 最小 Release 程序可构建；
4. 运行时插件/QML 模块可枚举；
5. MSVC 14.44 和 Windows SDK 10.0.26100 被写入 build record；
6. E5 validator 继续通过；
7. adapter 目录开始前记录 prototype hash；
8. 没有候选 Evidence run 被提前创建。

## 10. R0 build record

| 字段 | 结果 |
| --- | --- |
| Run timestamp | 2026-08-24T13:30:58+08:00 |
| Qt prefix/version | <QtInstallerRoot>\msvc2022_64 / 6.8.3 |
| CMake | 4.4.2 / Visual Studio 18 2026 |
| Compiler/toolset | MSVC 19.44.35223 / v143,version=14.44,host=x64 |
| Windows SDK | 10.0.26100.0 |
| Configuration | Release |
| Qt Quick build | PASS |
| E5 validator | PASS：known-good accepted；known-bad stale-frame rejected |
| windeployqt | PASS：Qt Quick/QML/Windows platform dependencies deployed to fresh local directory |
| Deployed QML self-check | PASS / exit 0 |
| Entry source package SHA-256 | 0A6BD0020E110FC6206AEB24400C41583F78539A3BBD8B7E22C921B02F957333 |
| Deployed executable | 45,568 bytes / SHA-256 961FFEFAD537793E05B971AC798EB05180752F4DD1DB199A3E3B18DCCF347147 |
| Optional warnings | Vulkan headers、dxcompiler/dxil 未发现；当前 R0 未选择这些后端，部署后自检成功；R1 已确认 Direct3D11；R2/P0 仍须验证真实产品夹具和完整矩阵 |
| Candidate Evidence | 未创建；P0 NOT RUN |

本地机器可读摘要位于被 Git 忽略的 `prototype/renderer-adapters/ts-c01-qt-quick/out/entry-preflight.json`。

## 11. R1 adapter-contract build record

| 字段 | 结果 |
| --- | --- |
| Run timestamp | 2026-08-24T14:19:17+08:00 |
| Qt / toolchain | Qt 6.8.3；MSVC 19.44 / v143 14.44；Windows SDK 10.0.26100.0 |
| Configuration / deploy | Release / fresh windeployqt directory |
| Surface transport / owner | ADAPTER_OWNED_NATIVE_CHILD / ADAPTER |
| Desktop parent route | 唯一可见 SHELLDLL_DefView 的 RAISED_PROGMAN icon host；无 traditional WorkerW fallback |
| Window contract | WS_CHILD；非 WS_POPUP；非 TopMost；WS_EX_NOACTIVATE；WS_EX_TRANSPARENT；owner=null |
| Render backend | Direct3D11 surface_ready + 1 frame_presented |
| Lifecycle | ATTACHING → ATTACHED_SAFE → DETACHING → DETACHED；重复 Detach 成功；BeginExit 成功 |
| E5 validation | PASS：10 events conform to desktopup.desktop-host.event.v1 |
| Cleanup | host=0 / adapter=0 / quarantine=0 |
| Deployed executable SHA-256 | CB258A8E952C85DDA26D6C0DE34AD21F4EE6C9127CE58D9A415BC0EA986F6658 |
| Candidate Evidence | 未创建；P0 NOT RUN |

机器可读摘要与事件流位于被 Git 忽略的 out/r1-preflight.json 和 out/r1-events-20260824-141913.jsonl。R1 preflight 可由 preflight-r1.ps1 重新生成。

## 12. R2 首轮人工失败记录

首轮人工门结果：

- FAIL：footer 灰框；
- FAIL：空白左拖不能出现 Explorer 框选；
- FAIL：空白右键不能出现 Windows 桌面菜单；
- FAIL：空白覆盖范围内的原生快捷方式不能操作；
- PASS：普通窗口、文件选择器和全屏程序高于海报；
- PASS：结束后无残留。

首轮自动检查使用 ChildWindowFromPointEx 和 CWP_SKIPTRANSPARENT，因此只能说明同一 parent 内的显式查找结果，不能证明 Explorer 跨进程输入。完整 Qt HWND 覆盖 visual bounds 是三项输入同时失败的根因；WS_EX_TRANSPARENT 单独用于普通 Qt 子窗口并不能满足本项目空白穿透门。

## 13. R2 有界修订与自动记录

修订路线：OFFSCREEN_FRAME_SOURCE / HOST ownership。Qt 只生成离屏 Direct3D11 frame；Host-owned layered transparent child 显示该 frame；Qt backing child 位于 SHELLDLL_DefView 后方；Host layered sibling islands 承载命中。footer 半透明底框已删除。

| 字段 | 结果 |
| --- | --- |
| Run timestamp | 2026-08-24T15:44:48+08:00 |
| Qt / toolchain | Qt 6.8.3；MSVC 19.44 / v143 14.44；Windows SDK 10.0.26100.0 |
| Configuration / deploy | Release / fresh windeployqt directory |
| Surface transport / ownership | OFFSCREEN_FRAME_SOURCE / HOST |
| Render backend | Direct3D11 |
| Matrix / projection / frame | 10 / 11 / 11 |
| E5 validation | PASS：49 events conform |
| Host layered visual | ready=1 / released=1 |
| Unexpected visual pointer | 0 |
| Cleanup | visual=0 / island=0 / host=0 / adapter=0 |
| R1 regression | PASS |
| Deployed executable SHA-256 | 2B5278AAEAF954DD60F0DF54CE85E9B6AEDF4F40CD6A9A0E3BA321C8022DC666 |
| Explorer cross-process input | MANUAL RETEST PENDING |
| Candidate Evidence | 未创建；P0 NOT RUN |

机器可读摘要位于被 Git 忽略的 out/r2-preflight.json。旧自动 PASS 已被本次人工失败和 transport 修订取代，不得继续引用旧二进制作为当前结果。

## 14. R2 人工复测关闭记录

用户确认修订版 R2 Release 的六项复测全部 PASS：无 footer 灰框/白条/额外外框；空白左拖为 Windows 桌面框选；空白右键为 Windows 桌面菜单；覆盖范围内原生快捷方式可操作；普通窗口/文件选择器/全屏程序高于海报；退出无残留。

本地复测日志 `out/r2-manual-retest-user.jsonl` 共 52 条事件并通过 E5 validator，unexpected visual pointer 为 0，最终 visual/island/host/adapter/quarantine 均为 0；日志 SHA-256 为 `0D2C60867F13B6DF5C3E0651C82ECBA8A3730CFBD4D73BD3C5A4EACA469F5E73`。R2 结论更新为 PASS。

## 15. R3 自动预检审计

R3 为候选增加并验证：

- revision-aware frame API，明确拒绝 stale revision；
- detach/exit 后 late frame 拒绝；
- 可恢复 renderer device-loss 故障注入；
- 独立矩阵就绪标记，不依赖 Windows 上运行中 QFile 的共享读取；
- Qt rcc 官方 `SOURCE_DATE_EPOCH=1704067200` 确定性资源时间戳；
- MSVC `/Brepro`、`/experimental:deterministic`、`/pathmap` 与受校验规范构建路径；
- 两次全新 Release 构建、全新 windeployqt、依赖与归属材料枚举、静态性能和零资源检查。

| 字段 | 结果 |
| --- | --- |
| Timestamp | 2026-08-25T16:04:53.5367056+08:00 |
| Build ID | `ts-c01-qt-6.8.3-r4d2-20260825-02` |
| Reproducibility | PASS；2 clean builds；SHA-256 `8FF9F292B31A92CFDCDB44D81F10B647AD3D3DC6750D6F003F35A7F44E87D86D` |
| Deploy | PASS；51 files / 39 runtime binaries；部署未改变 EXE |
| Normal / fault / R4-C / R4-D1 / R4-D2 static JSONL | 61 / 62 / 66 / 64 / 111 events；E5 全部 PASS |
| R4-C static lifecycle | monitor/probe ready；0 false Shell loss；0 recovery/degraded；角色探针不冒充产品控制中心；最终零资源 |
| R4-D1 static display environment | PRIMARY_DISPLAY_ONLY；observer ready；0 false change；0 reprojection failure；multi-display DEFERRED；最终零资源 |
| R4-D2 static theme/fallback/isolation | 3 normal themes；5 atomic projections；4 rejected faults；3 LKG retains；1 fallback；2 superseded requests；1 geometry key；structured enum isolation；最终零资源 |
| R4-D3 fault/stale/performance | FUNCTIONAL PASS / HG-PERF-02 FAIL；surface-only same-attachment recovery；旧 revision/surface frame 与重复当前 Shell 事件拒绝；200 Item ×64 轮 0.180ms；6 次主题最大 27.106ms；CPU 62ms/10.223s、静态帧 0 PASS；工作集峰值 136.332MiB > 50MiB；最终零资源 |
| Stale / late rejection | PASS / PASS |
| Injected fault lifecycle | PASS |
| Cleanup | visual=0 / island=0 / host=0 / adapter=0 / quarantine=0 |
| Interaction latency | PASS：10 samples / 100ms budget；max total 42.919ms |
| Performance | 3152.792ms；0.0% single-core CPU；131.406～131.422MiB working set |
| Frame behavior | 静态 hold 期间 0 新 frame；事件驱动静态夹具的预期结果 |
| License material | 本机 Qt 二进制安装未带顶层 license payload；分发前审查待办 |
| Candidate Evidence | 未创建；P0 NOT RUN |

机器摘要：out/r3-preflight.json。当前原始 run：out/r3-run-20260825-170513/；Release SHA-256 4F1D76FCEC9C5271C1199D2CBAE48C748CA4EC0D8CA8C4D0AE50D325095CA673。D1、D2 已由用户确认；D3 自动功能门 PASS，但工作集硬门 FAIL，不等于候选 P0 PASS。

## 16. 当前下一动作

R5-01～05 已完成并形成正式 Candidate Evidence。矩阵结果为 2 PASS、1 FAIL、34 UNKNOWN、4 NOT RUN；HG-PERF-02 明确失败，R5 按 E6 建议 FAIL。当前等待 R6 用户处置决定，仍无 winner。

详细执行历史见 [TS-C01 R4 人工 P0 手册](TS_C01_R4_MANUAL_P0_RUNBOOK_V1.md) 与 [R4-D4 生命周期计划](TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md)。R5 结论见 [统一矩阵映射](TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md)、[Candidate Evidence](Evidence/TS-C01/README.md) 和 [R6 交接输入](TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md)。

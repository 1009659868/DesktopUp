# Desktop-Up V2 Qt 方案总执行计划 V1

状态：`ACTIVE — QTS-04-04 R4.2 QT QUICK + HOST STAGED PRESENT AUTO PASS / MANUAL FLASH GATE PENDING`
日期：2026-08-28

实施分支：`impl/qt-solution`

## 1. 总体目标

把冻结的七份需求、DesktopHost 合同和 Axiom Core 系统蓝图转换为可发布的 Qt 6.8.3 + 原生 DesktopHost Desktop-Up V2；Qt Quick 承担受限离屏海报 renderer adapter，并继续承担按需控制中心、编辑和预览。计划必须覆盖海报、控制中心、托盘、真实 Item、活跃度动态排版、主题热更新、用户自定义主题、AI、状态权威、生命周期、安装和发布，而不是只继续扩展候选探针。

## 2. 冻结输入

1. 七份需求及 2026-08-20 一致性审计；
2. TS-H01-E2～E6 与 `DesktopHost` 原型接口；
3. TS-C01 R4/R5 原始证据、已通过观察和全部保留例外；
4. [R6 Qt 实施路线决定](../20_Technical_Selection/TS_C01_R6_QT_IMPLEMENTATION_DECISION_2026-08-26.md)；
5. AX-BP-01～06 的模块、端口、关系和物理边界；
6. 仅使用 Axiom Core，axiom-runtime 使用量保持为零。
7. [QTS-01 混合 Qt 优化方向](QTS_01_HYBRID_QT_OPTIMIZATION_DIRECTION_2026-08-26.md)；
8. [QTS-02 生产物理架构](QTS_02_PRODUCTION_PHYSICAL_ARCHITECTURE_V1.md)；
9. [QTS-02 一致性审计与 QTS-03 交接](QTS_02_CONSISTENCY_AUDIT_AND_QTS_03_HANDOFF_2026-08-26.md)；
10. [QTS-03 生产骨架与合同夹具审计](QTS_03_PRODUCTION_SKELETON_AND_CONTRACT_FIXTURE_AUDIT_2026-08-27.md)；
11. [QTS-03 代码锚点](QTS_03_CODE_ANCHORS_V1.md)；
12. [QTS-03 → QTS-04 交接](QTS_03_HANDOFF_TO_QTS_04_2026-08-27.md)。
13. [QTS-04-01 真实 DesktopHost 与重连审计](QTS_04_01_REAL_DESKTOP_HOST_AND_RECONNECT_AUDIT_2026-08-27.md)；
14. [QTS-04-01 代码锚点](QTS_04_01_CODE_ANCHORS_V1.md)；
15. [QTS-04-01 实施日志](QTS_04_01_IMPLEMENTATION_LOG_2026-08-27.md)；
16. [QTS-04-01 → QTS-04-02 交接](QTS_04_01_HANDOFF_TO_QTS_04_02_2026-08-27.md)；
17. [QTS-04-02 NIGHT OPS renderer 审计](QTS_04_02_NIGHT_OPS_QPAINTER_RENDERER_AUDIT_2026-08-27.md)；
18. [QTS-04-02 代码锚点](QTS_04_02_CODE_ANCHORS_V1.md)；
19. [QTS-04-02 实施日志](QTS_04_02_IMPLEMENTATION_LOG_2026-08-27.md)；
20. [QTS-04-02 用户桌面视觉门](QTS_04_02_MANUAL_VISUAL_GATE_V1.md)；
21. [QTS-04-02 → QTS-04-03 交接](QTS_04_02_HANDOFF_TO_QTS_04_03_2026-08-27.md)；
22. [QTS-04-03 split input islands 审计](QTS_04_03_SPLIT_INPUT_ISLANDS_AUDIT_2026-08-27.md)；
23. [QTS-04-03 代码锚点](QTS_04_03_CODE_ANCHORS_V1.md)；
24. [QTS-04-03 实施日志](QTS_04_03_IMPLEMENTATION_LOG_2026-08-27.md)；
25. [QTS-04-03 用户单海报输入门](QTS_04_03_MANUAL_INPUT_GATE_V1.md)；
26. [QTS-04-04 生命周期矩阵计划](QTS_04_04_LIFECYCLE_MATRIX_PLAN_V1.md)；
27. [QTS-04-04 代码锚点](QTS_04_04_CODE_ANCHORS_V1.md)；
28. [QTS-04-04 实施日志](QTS_04_04_IMPLEMENTATION_LOG_2026-08-28.md)；
29. [QTS-04-04 用户生命周期矩阵](QTS_04_04_MANUAL_LIFECYCLE_GATE_V1.md)；
30. [QTS-04-04 R2 外框与内容布局分离修订](QTS_04_04_FRAME_CONTENT_LAYOUT_REVISION_V1.md)；
31. [QTS-04-04 R3 V1 连续缩放与 Item 溢出对齐](QTS_04_04_V1_PARITY_RESIZE_OVERFLOW_R3.md)；
32. [QTS-04-04 R4 连续 Item viewport](QTS_04_04_CONTINUOUS_ITEM_VIEWPORT_R4.md)；
33. [QTS-04-04 R4.1 底部裁切与 Renderer 组件审计](QTS_04_04_R4_1_CLIP_AND_COMPONENT_AUDIT_2026-08-28.md)。

## 3. 实施原则

- 候选原型只提供证据和可复用算法，不能整体复制为生产架构；
- `StateStore` 是唯一业务状态权威，海报、控制中心、托盘和诊断只消费 accepted snapshot；
- 海报 renderer 与原生 DesktopHost 继续保持端口隔离和同 revision 原子投影；
- Qt Quick 仅以受限 software offscreen renderer adapter 进入常驻 Runtime；Quick Controls 和控制中心仍在独立按需进程，外部主题 QML/JS 不得直接进入 Runtime；
- 透明空白继续交还 Explorer，Item、标题栏和按钮才由应用命中；
- 普通/全屏程序 > 海报 > 桌面图标 > 壁纸，禁止 TopMost 或隐藏再恢复补救；
- 排名、活跃度、启动统计和布局是核心业务逻辑，不放进主题 QML；
- 普通主题只使用结构化 schema、资源和受限 DSL；不可信 QML/JS 不进入主进程；
- AI 只生成 `ChangePlan`，必须走普通校验、预览、确认、撤销和 CAS 路径；
- 每个阶段都要更新权威计划、代码锚点、日志和交接，不把人工观察冒充自动证据。

## 4. 工作流

| ID | 工作项 | 主要产出 | 退出门 | 状态 |
| --- | --- | --- | --- | --- |
| QTS-00 | 路线激活与证据封存 | R6 决定、`impl/qt-solution`、R5 历史边界 | Qt 路线与历史矩阵同时可追溯 | `COMPLETE` |
| QTS-01 | 工作集口径、归因与优化基线 | 分层进程/模块采样、内存贡献图、第一轮优化、预算建议 | 同一 Release/设备可复现；达到 50 MiB 或形成用户批准的正式新预算 | `01-01 COMPLETE / 01-02～04 DEFERRED` |
| QTS-02 | Qt 生产物理架构冻结 | 常驻 QPainter 海报、按需独立 Qt Quick 控制中心、进程/线程、端口、SQLite、named pipe、资源所有权和故障隔离 | 与 AX-BP、DesktopHost、混合 Qt 方向和七份需求无冲突 | `COMPLETE` |
| QTS-03 | 生产工程骨架与合同夹具 | 正式源码树、构建、依赖、命令总线、snapshot/CAS、IPC、诊断、CI 入口 | 最小空产品可构建、启动、退出并零残留 | `COMPLETE — 9/9 CTEST PASS` |
| QTS-04 | 单海报生产运行时 | 海报 renderer、原生 DesktopHost、命中、拖动/缩放/锁定/收起、Shell 恢复 | 白条/灰框/越级/Win+D/Explorer/输入/显示矩阵通过 | `04-01～03 IMPLEMENTED / 04-04 R4.2 QT QUICK + HOST AUTO PASS / MANUAL FLASH GATE PENDING` |
| QTS-05 | StateStore、控制中心与托盘 | 唯一状态源、草稿/预览/CAS、控制中心、托盘、单实例与状态恢复 | 两端同 revision、无回弹、生命周期与恢复通过 | `PENDING` |
| QTS-06 | 真实内容 Item 与动态排版 | Repository、发现/启动、历史统计、活跃度、锚定、自动/手动布局 | 零/一/多/重复/异常与 200 Item 非阻塞矩阵通过 | `PENDING` |
| QTS-07 | 主题平台与热更新 | 包格式、schema、资源、DSL、导入/迁移、预加载、原子切换、LKG/fallback | 恶意包、损坏包、并发切换和回滚隔离通过 | `PENDING` |
| QTS-08 | AI 控制平面 | BYOK Provider、凭据、ChangePlan、权限、预览/确认/撤销、错误隔离 | 无 Key 零网络；AI 不绕过普通命令和 CAS | `PENDING` |
| QTS-09 | 发布生命周期与核心回归 | 安装目录、自启、升级、卸载、依赖、许可证材料、性能、安全、发布包 | 安装/重启/清理和完整需求矩阵通过 | `PENDING` |

## 5. QTS-01 已完成基线与延期任务

### QTS-01-01 测量合同

状态：`COMPLETE — BASELINE CAPTURED / 50 MiB WORKING SET GATE STILL FAILS`

- 合同：[QTS-01-01 内存测量合同](QTS_01_01_MEMORY_MEASUREMENT_CONTRACT_V1.md)；
- 结果：[QTS-01-01 内存基线审计](QTS_01_01_MEMORY_BASELINE_AUDIT_2026-08-26.md)；
- 五角色 × 3 轮 × 10 样本已完成；当前单海报 Working Set 中位平均 136.228 MiB、最大包络 137.141 MiB；
- 控制中心继续单列，Working Set trim 保持禁止，50 MiB 门没有被 Private Working Set 偷换。
- 用户接受[路线三混合 Qt 方向](QTS_01_HYBRID_QT_OPTIMIZATION_DIRECTION_2026-08-26.md)，性能深化延期，功能主线恢复。

### QTS-01-02 分层基线

状态：`DEFERRED PERFORMANCE CHECKPOINT — RESUME NO LATER THAN QTS-09 ENTRY`

依次测量：

1. 补一个 `renderer + 原生投影` 独立层，隔离原生位图/分层窗口复制；
2. 控制中心关闭、打开稳定和再次关闭；
3. 6 Item 与 200 Item；
4. 将 QTS-01-01 五角色与新增角色合并为贡献图，作为 QTS-01-03 优化输入。

### QTS-01-03 归因与第一轮优化

状态：`DEFERRED PERFORMANCE CHECKPOINT`

重点检查 QML 对象/绑定、Scene Graph 与纹理、离屏 render target、CPU readback、原生位图、重复缓存、图片解码尺寸、控制中心销毁、未使用模块/插件和日志缓冲。优化必须降低真实驻留或提交量，不能只改变任务管理器显示。

### QTS-01-04 复测与决定

状态：`DEFERRED PERFORMANCE CHECKPOINT`

在相同构建和环境复测并输出：

- 基线到优化后的绝对值和差值；
- 固定 Qt 成本与可优化项目成本；
- 海报常驻和控制中心临时预算；
- 是否继续保持 50 MiB，或提交适用于产品生命周期的正式预算变更。

## 6. 阶段边界

QTS-01-02～04 已按用户决定延期，但 50 MiB FAIL 和原始证据继续有效，最迟须在 QTS-09 入口前恢复。QTS-04-04 生命周期矩阵已由用户确认通过。R1～R4 的 relocation、frame/content、capture 与连续 viewport 证据保留；R4 用户复测又发现 footer clip 泄漏和 reflow 闪烁。R4.1 已自动关闭 clip 泄漏。R4.2 已按用户决定实现 Qt Quick GridView/Flickable adapter 与 Host staged-present，Release 14/14 和真实进程 smoke PASS；reflow 闪烁只剩真实桌面人工关闭门，完整单海报 P0 尚未关闭。
## 7. QTS-02 冻结输出

- 一个常驻 Runtime 主进程拥有 Core、SQLite StateStore、托盘、DesktopHost 和 QPainter renderer；
- 一个独立按需 Qt Quick Control Center 进程只持有短命 UI draft 和预览；
- Runtime/Control Center 通过当前用户、当前会话受限的 named pipe 交换版本化 DTO；
- Runtime 基础依赖为 QtCore/QtGui；renderer adapter 受控允许 QtQuick/Qml software offscreen，仍禁止 QuickControls/WebView/外部 QML/JS；
- QPainter 产生 premultiplied QImage 离屏帧，Host-owned layered visual 和 split input islands 保持 z-order/命中所有权；
- SQLite direct C API、专用单写者线程和 expected-revision/CAS 组成唯一正式状态权威；
- 详细合同和 QTS-03 交接见 QTS-02 两份权威产物。

## 8. 当前下一动作

执行 QTS-04-04 R4.2 人工闪烁关闭门：跨 2/3/4/6 列连续 resize、连续滚动、输入穿透与层级回归。通过后关闭 QTS-04-04 并进入 QTS-05 StateStore、控制中心与托盘；失败则只审计 Host present timing/paint synchronization，不再新增 layout/scroll 实现。

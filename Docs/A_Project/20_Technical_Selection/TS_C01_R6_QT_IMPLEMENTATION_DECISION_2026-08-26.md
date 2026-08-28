# TS-C01 R6：Qt 实施路线决定

状态：`APPROVED — QT IMPLEMENTATION ROUTE SELECTED / HISTORICAL MATRIX RETAINED / HG-PERF-02 OPEN`

日期：2026-08-26

## 1. 决定

用户明确接受 Qt 方案的总体可行性与当前接受度，并决定在 `impl/qt-solution` 分支继续推进 Desktop-Up V2。

被选定的实施方向是：

- 海报呈现与控制中心 UI 方向：Qt 6.8.3 / Qt Quick；
- 桌面宿主、窗口层级、透明命中、Shell 生命周期与输入边界：冻结的原生 Win32 `DesktopHost`；
- 架构描述方法：仅使用 Axiom Core 的冻结蓝图与端口关系；
- 明确排除：axiom-runtime、WebView 海报回退、顶层/TopMost 海报补救和主进程内执行不可信主题代码。

TS-C02～TS-C08 转为 `DEFERRED ALTERNATIVES`。它们没有被证明失败，也不因本决定获得或失去 P0 结果；只有 Qt 路线以后出现不可接受的技术阻断或用户重新开启横向比较时才恢复。

## 2. 证据与程序例外

本决定是用户作出的产品与实施路线选择，不改写历史候选证据：

- TS-C01 正式矩阵仍是 2 PASS、1 FAIL、34 UNKNOWN、4 NOT RUN；
- `HG-PERF-02` 仍是明确 FAIL：稳定工作集峰值 136.332 MiB，高于冻结的 50 MiB；
- R4 的循环次数不足、自启延期、设备重启跳过和产品功能未实现仍保留；
- 不把控制中心角色探针、合成 Item 或三套内建主题枚举称为完整产品；
- 不把本次选择回填成候选 P0 PASS，也不删除 R5 的 FAIL 建议。

该例外的含义是：历史矩阵决定“候选证据是否达到原章程硬门”，用户决定“是否仍值得作为正式实施路线推进”。两者同时保留，不互相伪造。

## 3. 工作集处理

50 MiB 目前继续作为冻结目标，而不是被静默改成 137 MiB。Qt 实施线首先建立可复现的内存归因与优化工作流，至少分离：

1. 原生 DesktopHost 基线；
2. Qt Core/Gui 基线；
3. 最小 Qt Quick/QML/Scene Graph 基线；
4. 离屏渲染、帧读回与原生投影增量；
5. 单海报、控制中心打开/关闭和 200 Item 场景；
6. 总 Working Set、Private Working Set、Commit 与 GPU 资源。

如果有界优化后仍无法达到 50 MiB，必须形成正式预算复核，说明设备范围、测量口径、用户体验收益和其他候选的同口径基线，再由用户接受新预算。不得使用主动修剪 Working Set 等方式美化数字。

## 4. 已确认可承载的产品能力

Qt 路线按以下边界承载冻结需求：

- 主题热更新：候选包后台校验、预加载、同 revision 原子切换、LKG 与 fallback；
- AI：Provider Adapter → ChangePlan → 校验/预览/确认/撤销 → StateStore CAS；AI 不直接写渲染状态；
- 用户主题：普通主题使用参数 schema、资源槽与受限 DSL；不可信 QML/JavaScript 不在主进程执行；代码主题如启用，必须使用独立受限进程；
- 内容 Item：真实 Repository、启动语义和历史统计位于核心层，活跃度/排序/布局由确定性引擎计算，QML 只消费只读投影并负责视觉；
- 海报、控制中心、托盘和诊断：消费同一 accepted snapshot，不能各自形成状态权威。

## 5. 尚未冻结的生产决定

选择 Qt 不等于完整生产架构已经完成。以下仍需在 Qt 方案计划中冻结：

- StateStore 的具体实现、schema、迁移、CAS、备份与恢复；
- 核心运行时与 Qt/UI 的进程和线程边界；
- 是否需要 IPC，以及版本、错误和超时合同；
- 控制中心、海报 renderer 与主题代码的故障隔离边界；
- 完整安装、自启、升级、卸载和依赖交付；
- 真实 Item 扫描、启动和文件收纳的权限与恢复台账；
- AI 凭据存储、Provider 网络权限和本地端点规则。

## 6. 后续执行入口

后续唯一入口是 [Qt 方案总执行计划](../30_Architecture/QT_SOLUTION_EXECUTION_PLAN_V1.md)。第一项为 `QTS-01` 工作集口径、归因与优化基线；随后冻结 Qt 生产物理架构，再进入正式代码骨架和产品纵向切片。

## 7. 批准

| 角色 | 状态 | 日期 | 说明 |
| --- | --- | --- | --- |
| Product/user | `APPROVED` | 2026-08-26 | 接受 Qt 的可行性与接受度，决定继续推进 |
| Evidence history | `RETAINED` | 2026-08-26 | 不覆盖 R5 FAIL、UNKNOWN、NOT RUN 和例外 |
| Qt solution activation | `ACTIVE` | 2026-08-26 | 分支 `impl/qt-solution` |

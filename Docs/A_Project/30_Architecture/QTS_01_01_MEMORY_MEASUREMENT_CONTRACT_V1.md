# QTS-01-01 内存测量合同 V1

状态：`FROZEN — EXECUTABLE BASELINE CONTRACT`

日期：2026-08-26

适用分支：`impl/qt-solution`

## 1. 目的与阶段边界

本合同用于回答当前约 136 MiB Working Set 是由原生 DesktopHost、Qt GUI 基础层、Qt Quick、离屏帧源还是完整单海报共同贡献。QTS-01-01 只冻结测量方式并建立首轮五角色基线，不修改 50 MiB 目标、不实施 Working Set trim，也不把候选探针直接提升为生产代码。

QTS-01-01 的 `BASELINE_CAPTURED` 仅表示同口径数据已取得；它既不等于性能门 `PASS`，也不等于 Qt 内存预算已获调整。预算裁决属于 QTS-01-04。

## 2. 被测对象边界

五个角色必须分别构建、分别启动、顺序采样，禁止放在同一进程内以免 DLL、QML 引擎、Scene Graph 和纹理资源相互污染。

| 角色 ID | 角色定义 | 必须包含 | 明确不含 |
| --- | --- | --- | --- |
| `desktop_host` | 已通过层级验证的 TS-H01-B 原生宿主空表面 | Win32 消息循环、WorkerW/Progman 发现、桌面子窗口、GDI 诊断卡 | Qt DLL、QML、离屏帧、真实海报 |
| `qt_gui_no_quick` | Qt GUI 平台层下限 | `QGuiApplication`、Windows QPA、一个已创建但不可见的原生 `QWindow` | Qt Quick/QML、Scene Graph、海报、DesktopHost 投影 |
| `minimal_qt_quick` | Qt Quick 固定成本下限 | Qt GUI、QML 引擎、D3D11 Scene Graph、单 Rectangle 首帧 | 离屏读回、Night Ops、DesktopHost 投影 |
| `offscreen_renderer` | 当前候选所用离屏帧源的独立成本 | 420×620 六卡片代表场景、D3D11、`QQuickItem::grabToImage`、保留一张 CPU `QImage` | 原生分层窗口复制、命中岛、Shell 恢复、完整 Night Ops |
| `current_single_poster` | 当前可验证的完整 Night Ops 单海报 | TS-C01 R2 fixture、真实 QML、离屏捕获、原生投影、命中岛、DesktopHost 合同 | 控制中心、真实 200 Item、未来 StateStore/AI/主题沙箱 |

`offscreen_renderer` 名称描述的是当前候选的 offscreen frame-source 路径，不得在证据中误写为 `QQuickRenderControl`。当前实际 API 是 `QQuickItem::grabToImage`。

## 3. 编译和运行口径

- 操作系统、CPU、GPU、驱动、物理内存和当前 Git commit 必须写入 `machine.json`；
- Qt 固定为 6.8.3 msvc2022_64；外部安装根通过 `-QtRoot` 显式传入，不写入持久证据；
- CMake generator 固定为本机已验证的 Visual Studio 18 2026、v143 toolset、x64、Release；
- 五角色均使用同一次基线运行中的本地 Release 构建，不混入 Debug；
- 默认每角色独立重复 3 次；每次 ready 后预热 2 秒，再采 10 个 1 秒间隔样本；
- 角色按 `desktop_host → qt_gui_no_quick → minimal_qt_quick → offscreen_renderer → current_single_poster` 顺序串行运行；
- 单次异常退出、ready 超时、样本不足、PID 计数器丢失或残留进程均使该次运行无效，不能用已有历史数字补齐；
- 采样时控制中心关闭。控制中心打开/关闭基线属于 QTS-01-02，不能并入后台单海报预算。

## 4. 指标定义

| 指标 | 权威来源 | 单位 | 解释 |
| --- | --- | --- | --- |
| Working Set | Windows `Process(*)\\Working Set` | MiB | 当前驻留在物理内存中的全部进程页，包含可共享页 |
| Private Working Set | Windows `Process(*)\\Working Set - Private` | MiB | 当前驻留且不能由其他进程共享的页；作为用户看到的常驻私有内存主指标 |
| Commit | Windows `Process(*)\\Private Bytes` | MiB | 该进程已提交、不能由其他进程共享的虚拟内存；本合同不把它与 Working Set 混称 |
| CPU | 进程 `TotalProcessorTime` 起止差 | ms、单核百分比 | ready、预热完成后的静态采样窗口 CPU 消耗 |
| Static frames | 角色事件日志 | 帧数 | ready 后没有输入和状态变化时的额外提交帧；未装帧调度器的纯原生宿主必须记为 `NOT_INSTRUMENTED`，不能伪造为 0 |
| GPU Dedicated / Shared / Total Committed | Windows `GPU Process Memory(*)`，按 PID 汇总所有 adapter/physical 实例 | MiB | 每进程 GPU 资源；没有匹配 PID 实例时记 0，计数器不可用时应使运行失败而非静默省略 |

每次运行保存原始 10 样本、min/avg/max、CPU 差值、静态帧和可执行文件 SHA-256。角色汇总使用三次运行的 `avg` 中位数，同时保留三次运行的最大包络；禁止只挑最低数字。

## 5. 禁止项

以下行为不能作为 QTS-01 优化证据：

- `EmptyWorkingSet`、`SetProcessWorkingSetSize(..., -1, -1)` 或等价 trim；
- 在采样前关闭或释放产品正常常驻所需资源；
- 把隐藏窗口、未初始化 Scene Graph 或未产生首帧的进程称为 renderer；
- 把控制中心内存算入后台海报后又另行重复计入；
- 用任务管理器截图代替机器可读原始样本；
- 用 Debug、不同 Qt 版本、不同屏幕会话或不同采样时长拼接贡献差值；
- 将 Working Set 的层间差值直接断言为严格可加的独占成本。DLL 共享、缓存和驱动资源使差值只能作为本机归因证据。

## 6. 产物与可追溯性

统一入口：

```powershell
$repoRoot = (git rev-parse --show-toplevel).Trim()
& (Join-Path $repoRoot 'prototype\memory-baselines\qts-01-01\run-qts-01-01.ps1') `
  -QtRoot $env:QT_ROOT
```

默认产物根：

`Docs/A_Project/30_Architecture/Evidence/QTS-01-01/<timestamp>/`

每次正式基线至少包含：

- `machine.json`：机器、工具链、Git 状态和采样参数；
- `baseline.json`：完整角色/重复汇总；
- `baseline.csv`：角色对比表；
- `<role>/run-XX/counter-samples.json`：原始采样；
- `<role>/run-XX/run-summary.json`：单次汇总；
- `<role>/run-XX/events.jsonl`、stdout/stderr 和 ready marker；
- 执行结果审计文档，解释归因边界和下一步，不只粘贴数字。

## 7. QTS-01-01 退出门

同时满足以下条件才可关闭：

1. 五角色独立 Release 可执行文件均构建成功；
2. 每角色 3 次有效运行，每次 10 个同口径样本；
3. Working Set、Private Working Set、Commit、CPU、静态帧和 GPU 指标均有值或有合同允许的显式 `NOT_INSTRUMENTED`；
4. 原始样本、机器信息、可执行文件哈希和汇总可互相追溯；
5. 没有调用 Working Set trim，完整海报退出后无残留；
6. 文档明确区分“已经测量”和“已经达到 50 MiB”。

关闭后进入 QTS-01-02：补齐 `renderer + 原生投影` 独立层、控制中心关闭/打开/再次关闭、6 Item/200 Item，并基于本基线形成贡献图。

# QTS-01-01 内存基线审计（2026-08-26）

状态：`COMPLETE — BASELINE CAPTURED / 50 MiB WORKING SET GATE STILL FAILS`

合同：[QTS-01-01 内存测量合同](QTS_01_01_MEMORY_MEASUREMENT_CONTRACT_V1.md)

正式证据：[`20260826-162129`](Evidence/QTS-01-01/20260826-162129/)

## 1. 执行结论

QTS-01-01 已建立可重复运行的五角色 Release 基线。5 个角色各完成 3 次独立进程运行，每次 ready 后预热 2 秒并采集 10 个 1 秒间隔样本，共 15 次有效运行、150 个进程计数器样本。所有进程正常退出，审计时残留进程数为 0；没有调用 Working Set trim。

本节点只完成“合同冻结 + 首轮分层测量”，没有通过 50 MiB Working Set 门。完整 Night Ops 单海报的 Working Set 中位平均为 **136.228 MiB**，三轮最大包络为 **137.141 MiB**。Private Working Set 为 53.115 MiB 不能替代原 Working Set 门，Commit 126.648 MiB 也说明不能把 136 MiB 简单解释为可忽略的共享 DLL 显示问题。

## 2. 基准环境

| 条件 | 值 |
| --- | --- |
| Windows | Windows 11 专业版 10.0.26100（build 26100） |
| CPU | Intel Core i7-14700，28 logical processors |
| 物理内存 | 32426 MiB |
| GPU | Intel UHD Graphics 770 / driver 32.0.101.5972；NVIDIA GeForce RTX 5060 / driver 32.0.15.7652 |
| Qt | 6.8.3 msvc2022_64；安装根未持久化 |
| 构建 | Visual Studio 18 2026，v143，x64，Release |
| Git 基线 | `b043366f16bc6401ce9aa3490b3e478a989e4348`；工作树包含已披露的 Qt 路线文档与本节点未提交改动 |
| 采样 | 每角色 3 次；每次预热 2 秒；10 × 1 秒 |

可执行文件 SHA-256、机器完整 Git 状态和每次 PID 位于 `baseline.json`、`machine.json` 及各 `run-summary.json`，因此 dirty worktree 不妨碍本次可执行文件级追溯，但它不能被称为已提交发布基线。

## 3. 五角色结果

下表内存列使用三次运行各自 `avg` 的中位数；括号为三轮所有样本的最大包络。GPU 列是按 PID 汇总所有 `GPU Process Memory` 实例的 Total Committed。CPU 是 10 秒静态窗口中值。

| 角色 | Working Set MiB | Private WS MiB | Commit MiB | GPU Commit MiB | CPU ms / 单核 % | 静态帧 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| 原生 DesktopHost | 14.566（14.570） | 1.941（1.945） | 2.820（2.824） | 0.000（0.000） | 203.125 / 2.031% | `NOT_INSTRUMENTED` |
| Qt GUI，无 Quick | 26.453（26.504） | 3.371（3.422） | 4.715（4.773） | 0.000（0.000） | 0.000 / 0.000% | 0 |
| 最小 Qt Quick | 63.454（63.484） | 14.939（14.984） | 72.564（73.023） | 12.895（12.910） | 0.000 / 0.000% | 0 |
| 独立离屏 renderer | 71.881（72.906） | 20.081（20.410） | 82.191（83.535） | 20.418（20.418） | 15.625 / 0.156% | 0 |
| 当前 Night Ops 单海报 | **136.228（137.141）** | **53.115（54.074）** | **126.648（129.328）** | **33.262（34.664）** | 15.625 / 0.156% | 0 |

原生 DesktopHost 的 2.031% 不是生产空宿主 CPU 下限：TS-H01-B 为层级诊断探针，会周期性枚举/记录窗口样本；它被选用于内存宿主基线是因为其 DesktopHost 层级已经人工验证。该 CPU 值只能描述探针本身，不能拿来指控未来生产 DesktopHost 常驻 CPU。后续若要比较 CPU，须补一个关闭诊断枚举的生产形态宿主角色。

## 4. 观察性层间差值

以下差值用于定位优先级，不是严格可加的独占成本。不同进程之间存在 DLL 共享、QPA/Scene Graph 延迟初始化、驱动缓存和页面共享差异。

| 从 → 到 | Working Set Δ MiB | Private WS Δ MiB | Commit Δ MiB | GPU Commit Δ MiB | 可支持的判断 |
| --- | ---: | ---: | ---: | ---: | --- |
| 原生宿主 → Qt GUI | +11.887 | +1.430 | +1.895 | +0.000 | Qt GUI/QPA 的 Working Set 显示成本大于其私有驻留和 Commit 增量 |
| Qt GUI → 最小 Quick | **+37.001** | **+11.568** | **+67.849** | **+12.895** | Qt Quick/QML/Scene Graph 是首个主要固定台阶 |
| 最小 Quick → 离屏 renderer | +8.427 | +5.142 | +9.627 | +7.523 | 420×620 场景、读回与保留 CPU 帧有可见但非最大成本 |
| 离屏 renderer → 完整单海报 | **+64.347** | **+33.034** | **+44.457** | **+12.844** | 完整 fixture、真实 QML、原生投影/位图、命中与合同设施是当前最大剩余台阶，不能把 136 MiB 全归因于 Qt 固定成本 |

## 5. 与历史 R4-D3 的一致性

历史 HG-PERF-02 Working Set：min 135.648、avg 135.976、max 136.332 MiB。本次完整单海报为中位平均 136.228、最大包络 137.141 MiB：

- 平均差 +0.252 MiB，约 +0.185%；
- 最大值差 +0.809 MiB，约 +0.593%。

两个独立采样入口得出近似结果，支持“约 136 MiB 是当前实现的稳定基线”，不支持“旧失败只是任务管理器瞬时波动”。

## 6. 已知边界

1. 当前 `offscreen_renderer` 忠实隔离候选实际使用的 `QQuickItem::grabToImage`，不是 `QQuickRenderControl`；若未来生产设计改用 render control，必须新增角色，不能覆盖本基线。
2. 当前完整单海报仍是 TS-C01 候选 fixture，不是 QTS-03 之后的生产进程；它给出优化起点，不冻结未来物理架构。
3. 屏幕、驱动缓存和系统其他负载会影响 Working Set/GPU 数字；本证据只对记录机器和合同条件有效，跨机器比较须重跑全部角色。
4. 控制中心未打开，6/200 Item 未纳入；它们属于 QTS-01-02。
5. 静态帧为 0 表示 ready 后没有额外 `frame_presented/frameSwapped` 事件，不证明所有驱动后台活动为零；GPU/CPU 计数器仍是资源判断依据。

## 7. 下一步

进入 QTS-01-02，保持本合同不变并补齐：

1. `renderer + 原生投影` 独立层，区分原生位图/分层窗口复制与完整交互设施；
2. 控制中心关闭 → 打开稳定 → 再次关闭，验证临时内存能否回收及是否残留 QML/纹理；
3. 6 Item → 200 Item，区分真实内容规模与 Qt 固定成本；
4. 基于上述角色形成贡献图，再进入 QTS-01-03 的有证据优化，优先审计完整 fixture 相对离屏 renderer 的 +64.347 MiB Working Set / +33.034 MiB Private WS 台阶。

QTS-01-02/QTS-01-03 完成前，不提交 50 MiB 预算变更；预算继续保持 FAIL。

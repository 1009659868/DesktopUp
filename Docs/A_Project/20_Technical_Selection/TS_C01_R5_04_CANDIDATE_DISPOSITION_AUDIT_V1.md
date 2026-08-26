# TS-C01 R5-04 Qt 候选处置审计 V1

状态：`COMPLETE — E6 DISPOSITION RECOMMENDATION = FAIL / USER DECISION PENDING / NO WINNER`

日期：2026-08-26

## 1. 输入

- [R5-02 统一矩阵映射](TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md)：2 PASS / 1 FAIL / 34 UNKNOWN / 4 NOT RUN；
- [TS-C01 Candidate Evidence](Evidence/TS-C01/README.md)；
- [E6 候选退出规则](TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md)；
- 冻结性能目标：Release 单海报稳定工作集 ≤50 MiB。

## 2. E6 五种状态审计

| 状态 | 是否允许 | 理由 |
| --- | --- | --- |
| `PASS` | NO | 存在明确 `HG-PERF-02 FAIL`，同时 34 UNKNOWN、4 NOT RUN；不满足“全部 P0 必需自动+人工门达到规定次数” |
| `FAIL` | YES — RECOMMENDED | 一个硬门失败已经足以退出当前路线；工作集 136.332 MiB 明确超过 50 MiB，原始摘要和 JSONL 可复核 |
| `CONDITIONAL` | NO | E6 只允许单一、明确、有界且不改合同的缺口；当前除工作集失败外还有跨构建、计数不足、许可、自启、设备重启、StateStore、安全和产品 Item 等多个缺口 |
| `WITHDRAWN` | NO | 工具链可构建、候选已实测；当前不是因无法获得 SDK 或主动停止而撤回 |
| `NOT RUN` | NO 作为总体最终建议 | 候选已真实运行且已产生一个明确硬门失败；继续把总体状态写成纯 NOT RUN 会隐藏已知失败。但用户正式裁决前仍不得把登记表写成最终 REJECTED |

## 3. 关键失败

### HG-PERF-02

- 冻结目标：≤50 MiB；
- 实测：135.648～136.332 MiB，平均 135.976 MiB；
- 超出峰值：86.332 MiB；
- 峰值约为目标的 2.727 倍；
- 证据：`TS-C01-HG-PERF-02-2026-08-25-R4D3`；
- 置信度：HIGH。

统一矩阵明确规定：超出工作集目标必须作为需求预算变更，不能靠加权评分抵消。用户此前允许带例外继续，仅授权完成后续取证，没有修改冻结需求，因此本项仍是 FAIL。

## 4. 为什么不能使用 CONDITIONAL

将 TS-C01 记为 CONDITIONAL 至少违反两条 E6 规则：

1. 缺口不止一个。除工作集外，仍有 34 UNKNOWN 和 4 NOT RUN；
2. 把 50 MiB 改为约 137 MiB 属于需求预算变化，不是“不改合同的有界 adapter 修正”。

若未来优化 Qt 候选，必须先写明唯一修正范围和成功判据，再以冻结源码对完整 P0 全量重跑。当前证据不能支持“一次小修后直接通过”。

## 5. 已验证且可复用的正面发现

即使建议 FAIL，下列工程发现仍然有效：

- Qt Quick 能通过 `OFFSCREEN_FRAME_SOURCE / HOST` 接入原生 DesktopHost；
- 宿主分离后可实现海报高于图标、低于普通窗口的目标层级；
- 空白输入可交给 Explorer，交互岛保留卡片、标题和按钮命中；
- 受控会话中 Win+D、普通窗口、全屏、Explorer 单次重建和主题切换未复现 V1 WebView 顶部白条根因；
- 静态 CPU、静态帧率和合成 200 Item/主题切换性能通过对应硬门；
- 当前用户安装、普通第二实例、应用重启和精确卸载的候选合同可行。

这些发现可作为其他候选的设计输入，但不能借给 TS-C02～C07 的 P0 结果。

## 6. 残余风险

- 证据跨至少七个 EXE 哈希；最终 D4 构建没有完整回归前面人工门；
- 当前源码工作树未冻结；
- Qt 分发许可证与依赖材料未闭合；
- 开机自启延期、设备重启跳过、触控板和多显示器延期；
- 产品 StateStore/CAS、真实 Item 启动统计、主题包安全和收纳恢复未执行；
- 控制中心仍主要是窗口角色探针。

## 7. 处置建议

R5-04 建议：

```text
TS-C01
P0 disposition recommendation: FAIL
Primary failed hard gate: HG-PERF-02
Eligible for weighted scoring: NO
Selected: NO
Final rejection: PENDING USER DECISION IN R6
```

这是一项证据审计建议，不代替用户最终裁决。R5-05 应把它和两条合法后续路径交给 R6：接受 FAIL 并进入 TS-C02；或先发起正式需求预算变更，再让所有受影响候选按新基线重跑。
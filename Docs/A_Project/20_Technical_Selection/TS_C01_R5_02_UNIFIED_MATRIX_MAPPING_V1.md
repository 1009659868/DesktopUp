# TS-C01 R5-02 统一候选证据矩阵映射 V1

状态：`COMPLETE — 2 PASS / 1 FAIL / 34 UNKNOWN / 4 NOT RUN / 0 CONDITIONAL / P0 PASS INELIGIBLE`

日期：2026-08-26

## 1. 判定规则

本表使用 [统一候选证据矩阵](UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md) 的原始通过条件和 E6 退出规则。`用户确认通过`只证明实际观察结果；若规定次数、最低证据等级、同一构建可追溯性或完整场景不足，正式矩阵状态仍为 `UNKNOWN`。已知未执行场景记为 `NOT RUN`，明确超过硬门预算记为 `FAIL`。

本表不把 R4 的少轮次接受、D3 工作集例外、自启延期或设备重启跳过改写为 PASS。

## 2. 证据组

| Evidence ID | 材料 | 主要用途 |
| --- | --- | --- |
| `TS-C01-E01` | `entry-preflight.json`、`r1-preflight.json`、`r2-preflight.json` | 工具链、adapter、Night Ops 与原子投影自动门 |
| `TS-C01-E02` | `r2-manual-retest-user.jsonl`，52 条 | Explorer 输入、边界、层级和退出六项复测 |
| `TS-C01-E03` | `r3-preflight.json`、`r3-run-20260825-170513/` | 双 Clean、依赖、故障、性能、200 Item 与零资源 |
| `TS-C01-E04` | `r4-manual-20260824-171814-r4a-01` | R4-A 单次冷启、输入和基础层级 |
| `TS-C01-E05` | `r4-manual-20260825-112934-r4b-stability-01` | 交互稳定化复测 |
| `TS-C01-E06` | `r4-manual-20260825-120817-r4c-c1-windows-01` | 外部窗口、Show Desktop、全屏与焦点 |
| `TS-C01-E07` | `r4-manual-20260825-124220-r4c-c2-control-center-02` | 控制中心窗口角色 7 次打开/6 次关闭 |
| `TS-C01-E08` | `r4-manual-20260825-143817-r4c-c3-explorer-04` | Explorer 1 次 degraded/恢复 |
| `TS-C01-E09` | `r4-manual-20260825-153036-r4d-d1-01` | 减少后的显示环境和几何重投影 |
| `TS-C01-E10` | `r4-manual-20260825-160909-r4d2-control-panel-r2b` | 三主题、fallback 与 LKG 人工观察 |
| `TS-C01-E11` | `r4d4-package/r4d4-preflight.json`、`DesktopUpSetup.exe` | 当前用户安装包自动门 |
| `TS-C01-E12` | `TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md` | 正式安装、单实例、应用重启与卸载；自启/设备重启例外 |

完整哈希见 [R5-01 原始证据盘点](TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md)。

## 3. 构建、来源与同构夹具

| Matrix ID | 正式结果 | 已观察材料 | 不能升级为 PASS 的原因 |
| --- | --- | --- | --- |
| HG-BLD-01 | `UNKNOWN` | E01/E03/E11：Qt 6.8.3、MSVC/SDK、双 Clean 与 EXE 哈希存在 | Qt 许可材料审查未闭合；当前源码为未冻结工作树，无法由单一 source identity 重建全部会话 |
| HG-BLD-02 | `UNKNOWN` | E03/E11：Release、依赖枚举、windeployqt 与当前用户包可运行 | 合法分发材料未闭合；`dxcompiler/dxil` 和 VC 环境警告仍未形成正式分发裁决 |
| HG-BLD-03 | `UNKNOWN` | E01/E02：Night Ops、零/一/多/重叠、锁定/收起/编辑命中存在 | 失效 Item、缺图标、超长名称与完整跨候选同构夹具没有 E2 完整清单 |

## 4. 透明呈现、边界与稳定绘制

| Matrix ID | 正式结果 | 已观察材料 | 不能升级为 PASS 的原因 |
| --- | --- | --- | --- |
| HG-RND-01 | `UNKNOWN` | E04 用户观察无白条/灰框 | 冷启动实际 1/20；不是最终 D4 构建完整回归 |
| HG-RND-02 | `UNKNOWN` | E05 稳定化拖动无闪烁 | 真实 gesture 总数不足冻结 20 次；会话为独立旧构建 |
| HG-RND-03 | `UNKNOWN` | E05 缩放可继续操作 | 未形成最小/默认/最大各 20 次的 E3 证据 |
| HG-RND-04 | `UNKNOWN` | E09 显示环境变化 2 次且用户观察通过 | 100/125/150/200% 四档完整矩阵被用户停止；多轮与最终构建回归缺失 |
| HG-RND-05 | `UNKNOWN` | E05 收起/展开稳定 | 未达到 20 次并缺少逐帧媒体 |
| HG-RND-06 | `UNKNOWN` | E10 三主题、fallback/LKG 人工通过 | 只有 6 次提交且不是完整主题包平台；无最终 D4 构建快速切换回归 |

## 5. 层级与 Show Desktop

| Matrix ID | 正式结果 | 已观察材料 | 不能升级为 PASS 的原因 |
| --- | --- | --- | --- |
| HG-Z-01 | `UNKNOWN` | E02/E04/E06/E07/E12 多次观察普通窗口高于海报 | 材料跨构建，最终 D4 构建未执行完整普通窗口/系统对话框/全屏矩阵 |
| HG-Z-02 | `UNKNOWN` | E06 桌面右键、刷新、文件选择器和焦点观察通过 | 各动作 20 次的机器计数和媒体未保存 |
| HG-Z-03 | `UNKNOWN` | E06 键盘 Win+D 观察通过 | 未形成冻结 10 轮可计数媒体，最终构建未复测 |
| HG-Z-04 | `UNKNOWN` | E06 任务栏显示桌面观察通过 | 未形成冻结 10 轮可计数媒体，最终构建未复测 |
| HG-Z-05 | `UNKNOWN` | 无独立触控板证据 | 不能借用键盘或任务栏结果 |
| HG-Z-06 | `UNKNOWN` | E07 控制中心角色层级与关闭后拖动通过 | 只有 7 次打开/6 次关闭；角色探针不是产品控制中心；非 20 轮 |
| HG-Z-07 | `UNKNOWN` | E08 一次 Explorer degraded/恢复通过 | 仅 1/10；用户取消剩余轮次 |
| HG-Z-08 | `UNKNOWN` | E06 普通/无边框全屏观察通过 | 进入退出 10 轮的计数和最终构建回归缺失 |

## 6. 桌面输入与海报命中

| Matrix ID | 正式结果 | 已观察材料 | 不能升级为 PASS 的原因 |
| --- | --- | --- | --- |
| HG-IN-01 | `UNKNOWN` | E02/E04 空白框选通过 | 无长期保存的 E3 媒体，且最终 D4 构建未复测 |
| HG-IN-02 | `UNKNOWN` | E02/E04 桌面右键菜单通过 | 同上 |
| HG-IN-03 | `UNKNOWN` | E02/E04 原生快捷方式单击/双击通过 | 同上 |
| HG-IN-04 | `UNKNOWN` | E05 Item、按钮、标题和缩放柄可操作 | 会话动作次数不足且不是最终构建；未保存完整命中媒体 |
| HG-IN-05 | `UNKNOWN` | E05 USING/EDITING、锁定切换后仍可操作 | 未达到各 20 次 |
| HG-IN-06 | `NOT RUN` | 当前 Item 主要是视觉/命中夹具 | 未执行真实 Item 启动与一次性统计合同；EDITING 下“不启动”也无产品级证据 |

## 7. 几何、编辑与状态

| Matrix ID | 正式结果 | 已观察材料 | 不能升级为 PASS 的原因 |
| --- | --- | --- | --- |
| HG-GEO-01 | `UNKNOWN` | E01 自动投影场景、E09 显示变化可重投影 | 冻结几何公式、最小/最大/工作区全部边界未在最终构建完整验证 |
| HG-GEO-02 | `UNKNOWN` | E05 连续交互稳定化通过 | 冻结 3 次拖动/2 次缩放/取消/提交与预览产品态没有完整记录 |
| HG-GEO-03 | `UNKNOWN` | E05 锁定拒绝几何且按钮仍可用 | “启动”仍是 Item 夹具而非真实产品启动合同 |
| HG-GEO-04 | `UNKNOWN` | E09 两次显示环境变化通过 | 分辨率、工作区、四档 DPI 与最终构建矩阵不完整 |
| HG-STATE-01 | `NOT RUN` | 无产品 StateStore/CAS 场景 | 当前候选壳未实现预览 expected-revision 冲突与草稿保留 |
| HG-STATE-02 | `UNKNOWN` | E12 普通退出/重启的位置、锁定、收起恢复通过 | 损坏、I/O、无数据三类没有完整故障注入；当前并非正式产品 StateStore |

## 8. 生命周期、故障、安全与安装

| Matrix ID | 正式结果 | 已观察材料 | 不能升级为 PASS 的原因 |
| --- | --- | --- | --- |
| HG-LIFE-01 | `UNKNOWN` | E12 普通启动、普通第二实例、显式退出和零残留通过 | 自启由用户延期，完整启动意图矩阵未执行 |
| HG-LIFE-02 | `UNKNOWN` | E07/E08/E12 唯一托盘、角色窗口和 Explorer 恢复部分通过 | 托盘单击/双击/右键完整产品菜单与状态同步未执行 |
| HG-FAULT-01 | `UNKNOWN` | E03/E10 renderer/theme 故障、LKG/fallback 自动与人工材料存在 | 产品核心状态、真实 Item 启动和完整主题平台尚未实现 |
| HG-FAULT-02 | `UNKNOWN` | E03/E06/E08 旧 frame、重复 Shell 事件和恢复边界部分通过 | 产品控制中心崩溃/释放、旧消息与完整状态不分叉未执行 |
| HG-SEC-01 | `NOT RUN` | 无恶意主题包/路径/链接/脚本测试 | 当前只是内建结构化枚举夹具，不是主题包安全边界 |
| HG-SEC-02 | `NOT RUN` | 无真实快捷方式发现/收纳故障注入 | 当前候选不实现正式内容收纳与恢复台账 |
| HG-PACK-01 | `UNKNOWN` | E11/E12 当前用户安装、无 UAC、精确卸载、未知文件保留通过 | 自启 opt-in/禁用由用户延期；Qt 合法分发材料未闭合 |

## 9. 性能与诊断

| Matrix ID | 正式结果 | 已观察材料 | 判定 |
| --- | --- | --- | --- |
| HG-PERF-01 | `PASS` | E03：10.223 秒、CPU 时间增量 62ms、静态 0 新帧 | 满足 ≤0.10s；原始 D3 JSONL 与摘要哈希已记录 |
| HG-PERF-02 | `FAIL` | E03：工作集 135.648～136.332 MiB | 明确超过冻结 50 MiB；用户允许继续不构成需求预算变更 |
| HG-PERF-03 | `UNKNOWN` | E03 静态 0 FPS、退出零资源 | 动画、暂停、隐藏、控制中心关闭后的全部资源/FPS 矩阵未执行 |
| HG-PERF-04 | `PASS` | E03：200 Item ×64 轮，布局 0.18ms；主题切换最大 27.106ms | 满足不阻塞与 <200ms 目标 |
| HG-DIAG-01 | `UNKNOWN` | E01～E10 的 E5 validator 与资源账本通过 | 未运行的 StateStore、安全、自启、产品控制中心场景没有候选诊断证据；validator 不能替代这些场景 |

## 10. 汇总与含义

| Result | 数量 |
| --- | ---: |
| PASS | 2 |
| FAIL | 1 |
| UNKNOWN | 34 |
| NOT RUN | 4 |
| CONDITIONAL | 0 |
| 合计 | 41 |

结论：TS-C01 当前不具备 P0 PASS 或加权评分资格。`HG-PERF-02` 已形成明确硬门 FAIL；即使忽略该失败，仍有 34 个 UNKNOWN 和 4 个 NOT RUN。用户接受的少轮次结果保留为有效观察，但不能提升正式矩阵状态。

下一步进入 R5-03：建立 `Evidence/TS-C01/` 正式索引、完整性清单和已确定性能结果记录，不复制或篡改原始 `out/` 文件。
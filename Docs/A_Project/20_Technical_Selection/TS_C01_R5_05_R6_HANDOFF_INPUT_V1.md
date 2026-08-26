# TS-C01 R5-05 R6 交接输入 V1

状态：`COMPLETE — R6 READY / USER DECISION REQUIRED / NO WINNER`

日期：2026-08-26

## 1. R5 交付完成情况

| R5 步骤 | 输出 | 状态 |
| --- | --- | --- |
| R5-01 | 原始材料、SHA-256 与构建血缘盘点 | COMPLETE |
| R5-02 | 41 项统一矩阵映射 | COMPLETE |
| R5-03 | 正式 Candidate Evidence 索引与确定结果记录 | COMPLETE |
| R5-04 | E6 候选处置审计 | COMPLETE — FAIL RECOMMENDED |
| R5-05 | 登记、决策准备和 R6 输入 | COMPLETE |

## 2. 候选快照

| 字段 | 值 |
| --- | --- |
| Candidate | TS-C01 |
| Renderer | Qt 6.8.3 / Qt Quick |
| Host boundary | native DesktopHost / OFFSCREEN_FRAME_SOURCE / HOST |
| Matrix | 2 PASS / 1 FAIL / 34 UNKNOWN / 4 NOT RUN / 0 CONDITIONAL |
| Primary failure | HG-PERF-02：136.332 MiB > 50 MiB |
| Score eligibility | NO |
| R5 recommendation | FAIL |
| Final status | USER DECISION PENDING |
| Winner | NONE |

## 3. R6 必须作出的决定

### 路径 A：接受 R5 的 FAIL 建议

- 将 TS-C01 正式登记为 `P0 FAIL / REJECTED FROM CURRENT ROUTE`；
- 保留全部 Evidence 和 Qt 可复用工程发现；
- 不进行加权评分；
- 按候选编号进入 TS-C02：原生 Win32 + Direct2D/DirectComposition 的独立 adapter/P0；
- TS-C02 不得借用 Qt 的通过观察，只复用公共 DesktopHost 合同和实现中立接口。

### 路径 B：先修改冻结的资源预算

- 暂停候选裁决；
- 建立正式需求变更，明确新的工作集预算、适用设备和为什么原 50 MiB 不再成立；
- 该变化同时作用于所有候选，不能只为 Qt 放宽；
- 冻结一个可重现源码身份；
- 补齐许可、自启、设备重启、StateStore、Item、安全、计数和最终构建回归；
- 重新执行完整 P0。旧 FAIL 证据永久保留，不覆盖。

路径 B 不是当前 E6 的 `CONDITIONAL`，因为它改变需求合同且缺口不止一个。

## 4. 禁止的路径

- 直接把“用户认为 Qt 基本通过”写成 P0 PASS；
- 用加权评分抵消 HG-PERF-02；
- 只把 50 MiB 改成 137 MiB 而不重新评估其他候选；
- 删除失败/少轮次/旧构建证据；
- 把控制中心角色探针、合成 Item 或内建主题枚举描述为完整产品；
- 在没有用户决定时把 Qt 或其他技术写成 SELECTED。

## 5. R6 输入文件

1. [R5 工作流](TS_C01_R5_EVIDENCE_CONVERGENCE_AND_DECISION_PREP_PLAN_V1.md)；
2. [R5-01 原始证据盘点](TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md)；
3. [R5-02 统一矩阵映射](TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md)；
4. [TS-C01 Candidate Evidence](Evidence/TS-C01/README.md)；
5. [R5-04 候选处置审计](TS_C01_R5_04_CANDIDATE_DISPOSITION_AUDIT_V1.md)；
6. [统一候选证据矩阵](UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md)；
7. [选型决策记录](SELECTION_DECISION_RECORD_V1.md)。

## 6. 后续主线边界

R6 只处理 TS-C01 的处置与下一候选入口，不提前启动 TS-03 支撑域，也不冻结生产架构。只有至少一个海报呈现候选真正达到 P0 PASS，控制中心 UI、StateStore、IPC、主题平台和安装技术的组合裁决才有合格输入。
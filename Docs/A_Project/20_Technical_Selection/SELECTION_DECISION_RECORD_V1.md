# Desktop-Up V2 技术选型决策记录 V1

状态：`NOT READY — TS-C01 FAIL RECOMMENDED / R6 USER DECISION PENDING / NO RENDERER-UI WINNER / AXIOM CORE BLUEPRINT FROZEN INPUT`

建立日期：2026-08-20

## 1. 使用规则

本文件仍未形成最终技术组合。TS-01 至 TS-04 完成前禁止把 renderer/UI 候选填写为 `SELECTED`；用户明确批准的正交部分决策可以单独记录，但不能冒充完整组合或跳过候选证据。

## 2. 决策信息

| 字段 | 当前值 |
| --- | --- |
| Decision ID | `TS-DEC-001` |
| Frozen requirements revision | 2026-08-20 frozen baseline |
| Evidence matrix | `UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md` |
| Decision status | `R6 USER DECISION PENDING` |
| Selected combination | `NONE` |
| Controlled fallback | `NONE` |
| User approval | `PENDING` |

### 2.1 已批准的部分架构输入

- 决策：仅采用 Axiom Core 0.3.0 作为 DesktopUp V2 架构方法与技术研究层的系统描述方法；
- 固定版本：本轮审计 commit `890cc75680e150d98cbbe4133976dfcf35e8e871`；
- 范围：PortCell、类型化组合、Blueprint、Static、Slot/Conforms 及框架中立状态权威图；
- 排除项：axiom-runtime 处于 unstable 状态，不采用、不推荐、不依赖，也不作为验证证据；renderer、UI、StateStore、IPC、进程或安装技术仍未选择；
- 用户批准：2026-08-24，并进一步明确“只用 Axiom Core；不推荐使用 unstable runtime”；
- 冻结入口：[AXIOM_BLUEPRINT_INDEX](../30_Architecture/AXIOM_BLUEPRINT_INDEX.md)；最终审计：[AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1](../30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)。

## 3. 硬门结果

TS-C01 已在 R5 建立正式 Candidate Evidence 和候选级映射；其他候选仍未运行。下表是 R5 证据准备状态，不是最终用户批准，不得删除失败、未知或未运行项。

| Candidate | P0 Pass | P0 Fail | Unknown | Not Run | Conditional | Result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| TS-C01 | 2 | 1 | 34 | 4 | 0 | `FAIL RECOMMENDED / USER DECISION PENDING` |
| TS-C02 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C03 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C04 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C05 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C06 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C07 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C08 | 0 | 0 | 0 | ALL | 0 | `NOT RUN` |

### 3.1 TS-C01 R5 处置准备

- 矩阵：2 PASS / 1 FAIL / 34 UNKNOWN / 4 NOT RUN；
- 明确失败：`HG-PERF-02`，工作集峰值 136.332 MiB > 50 MiB；
- 加权评分资格：NO；
- R5 建议：FAIL；
- 最终 rejected：等待 R6 用户决定；
- 合法后续：接受 FAIL 并进入 TS-C02，或先正式修改对所有候选一致生效的资源预算并全量重跑；
- 证据入口：[TS-C01 Candidate Evidence](Evidence/TS-C01/README.md) 与 [R5-05 R6 交接](TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md)。

## 4. 评分与敏感性

待全部适用 P0 硬门通过后填写：

- 原始权重结果：`NOT AVAILABLE`；
- ±20% 权重扰动：`NOT AVAILABLE`；
- 置信度分析：`NOT AVAILABLE`；
- 排名反转项：`NOT AVAILABLE`。

## 5. 被淘汰候选

每项必须填写：

```text
Candidate:
Elimination date:
Failed hard gate or decision criterion:
Evidence IDs and paths:
Official limitation or reproduction steps:
Bounded adapter attempt, if any:
Why another retest is or is not justified:
Residual reusable findings:
```

当前：`NONE FINALIZED`。TS-C01 已形成 `FAIL RECOMMENDED`，但在 R6 用户批准前不写入最终淘汰名单。

## 6. 最终组合

以下字段只在用户批准后填写：

- 海报呈现技术；
- Windows 桌面宿主技术；
- 控制中心 UI；
- 核心运行时；
- 权威存储；
- 进程通信；
- 主题表达与隔离；
- 安装、自启、升级和卸载；
- 测试与诊断工具链。

以上生产技术项仍全部为 `UNDECIDED`。唯一已决定的是“Axiom Core 作为系统描述方法”；它不等同于“核心运行时”已经选定。

## 7. 架构阶段输入模板

技术选型完成后至少交付：

- 已验证能力和适用版本；
- 必须保留的原生适配层边界；
- 不允许使用的窗口/合成/输入模式；
- 资源和性能预算；
- 故障隔离与降级条件；
- 状态、消息和主题技术约束；
- 安装与依赖交付约束；
- 未解决风险和架构阶段必须关闭的实验；
- 原型中不得继承的临时代码。

## 8. 批准

| 角色 | 状态 | 日期 | 说明 |
| --- | --- | --- | --- |
| Evidence reviewer | `PENDING` | — | — |
| Product/user | `PENDING` | — | — |
| Architecture stage activation | `BLOCKED UNTIL APPROVAL` | — | — |

# Desktop-Up V2 技术选型决策记录 V1

状态：`USER APPROVED — QT IMPLEMENTATION ROUTE SELECTED / HISTORICAL P0 FAILURE RETAINED / SUPPORT DOMAINS PENDING`

建立日期：2026-08-20

## 1. 使用规则

用户于 2026-08-26 明确选择 Qt 实施路线，形成对原选型程序的产品决策例外。该例外允许进入生产架构准备，但不能把历史 FAIL、UNKNOWN、NOT RUN、少轮次或跳过项改写为 PASS；完整支撑技术组合仍待 Qt 方案计划冻结。

## 2. 决策信息

| 字段 | 当前值 |
| --- | --- |
| Decision ID | `TS-DEC-001` |
| Frozen requirements revision | 2026-08-20 frozen baseline |
| Evidence matrix | `UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md` |
| Decision status | `QT IMPLEMENTATION ROUTE SELECTED BY USER` |
| Selected combination | `Qt 6.8.3 / Qt Quick + native DesktopHost` |
| Controlled fallback | `Other renderer candidates deferred; no automatic runtime fallback` |
| User approval | `APPROVED — 2026-08-26` |

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
| TS-C01 | 2 | 1 | 34 | 4 | 0 | `SELECTED FOR IMPLEMENTATION BY USER / HISTORICAL MATRIX NOT PASS` |
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
- 最终 rejected：NO；用户选择进入 Qt 实施路线，但不改变历史 FAIL；
- R6 用户决定：不删除上述结论，仍选择 Qt 作为实施路线；50 MiB 保持目标并进入专项归因/优化，必要时再形成正式预算变更；
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

当前：`NONE FINALIZED`。TS-C01 未被淘汰，而是以保留历史矩阵失败的用户例外进入实施；TS-C02～TS-C08 是延期替代项，不得写成已失败。

## 6. 最终组合

当前已批准的组合边界：

- 海报呈现：Qt 6.8.3 / Qt Quick；
- Windows 桌面宿主：原生 Win32 DesktopHost；
- 控制中心 UI 方向：Qt Quick，正式窗口和资源生命周期待 QTS-02/QTS-05 冻结；
- 核心运行时、StateStore 与 IPC：未冻结；
- 主题：结构化 schema/资源/受限 DSL；不可信代码不得在主进程 QML 引擎执行；
- AI：Provider Adapter + ChangePlan + 普通命令/CAS 路径；
- 安装、自启、升级和卸载：候选 D4 仅作证据，生产合同待 QTS-09；
- 测试与诊断：复用 DesktopHost E5 合同并建立产品级矩阵；
- 架构方法：仅 Axiom Core，axiom-runtime 排除。

该组合是实施方向，不声称完整生产架构或全部支撑域已经冻结。

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
| Evidence reviewer | `RETAINED WITH EXCEPTIONS` | 2026-08-26 | R5 原始矩阵不改写 |
| Product/user | `APPROVED QT ROUTE` | 2026-08-26 | 接受可行性和当前接受度，授权推进 |
| Architecture stage activation | `ACTIVE — QTS-01 NEXT` | 2026-08-26 | 入口为 Qt 方案总执行计划 |

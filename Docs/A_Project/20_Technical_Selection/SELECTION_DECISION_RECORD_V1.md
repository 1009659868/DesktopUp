# Desktop-Up V2 技术选型决策记录 V1

状态：`NOT READY — NO RENDERER-UI WINNER / AXIOM CORE BLUEPRINT FROZEN INPUT`

建立日期：2026-08-20

## 1. 使用规则

本文件仍未形成最终技术组合。TS-01 至 TS-04 完成前禁止把 renderer/UI 候选填写为 `SELECTED`；用户明确批准的正交部分决策可以单独记录，但不能冒充完整组合或跳过候选证据。

## 2. 决策信息

| 字段 | 当前值 |
| --- | --- |
| Decision ID | `TS-DEC-001` |
| Frozen requirements revision | 2026-08-20 frozen baseline |
| Evidence matrix | `UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md` |
| Decision status | `NOT READY` |
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

待 TS-04 从证据目录生成。不得手工删除失败项。

| Candidate | P0 Pass | P0 Fail | Unknown | Conditional | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| TS-C01 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C02 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C03 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C04 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C05 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C06 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C07 | 0 | 0 | ALL | 0 | `NOT RUN` |
| TS-C08 | 0 | 0 | ALL | 0 | `NOT RUN` |

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

当前：`NONE — NO CANDIDATE HAS BEEN EVALUATED`。

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

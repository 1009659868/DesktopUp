# Desktop-Up V2 技术选型决策记录 V1

状态：`NOT READY — NO WINNER / DO NOT IMPLEMENT`

建立日期：2026-08-20

## 1. 使用规则

本文件是最终技术选择的预留记录，不是当前结论。完成 TS-01 至 TS-04 前禁止填写 `SELECTED`，禁止把候选登记顺序或历史原型写成推荐。

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

当前全部为 `UNDECIDED`。

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

# Desktop-Up V2 文档入口

Desktop-Up V2 是对 V1 的重构:保留产品目标与已验证资产,推倒 V1 的桌面海报渲染宿主架构(透明 WebView2 窗口模式,白条根因无法在架构内根治),以渲染宿主选定实验先行。

本目录沿用 V1 的 A/B/C 三层索引与更迭结构。

## 当前阅读路径

1. 阅读 [文档结构](DOCUMENT_STRUCTURE.md)
2. 阅读 [当前活跃路径](00_Catalog/ACTIVE_PATH.md)
3. 阅读 [A 层冷启动](A_Project/30_Sync/COLD_START.md)

当前路线位置:

```text
A: DesktopUP V2 重构 / V2-P0 ACTIVE — 渲染宿主选定实验先行
```

## 核心文档

- [DesktopUP V2 项目需求画像 V1](A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)(V2 权威需求聚合:V1 结论、继承/不继承资产、架构级需求、渲染候选矩阵、分期)
- [A 层当前交接](A_Project/30_Sync/CURRENT_HANDOFF.md)
- [继承风险台账](A_Project/30_Sync/KNOWN_ISSUES.md)

## V1 证据(只读,不复制进本仓库)

- V1 需求画像与问题台账:`G:\A_\Tools\DesktopUp\Docs\A_Project\...`

## 目录职责

| 路径 | 职责 |
| --- | --- |
| `00_Catalog/` | 当前路径、模块索引、文档登记 |
| `A_Project/` | 产品目标、项目阶段、顶层设计、里程碑与交接 |
| `B_Modules/` | 能力模块边界、依赖、模块里程碑与交接 |
| `C_Workstreams/` | 当前可执行任务、验收标准、实施日志与精确交接 |
| `Rules/` | 文档治理、版本和更迭规则 |
| `Reference/` | 外部项目、调研与非权威参考资料 |

## 权威顺序

1. 已运行代码与可重复验证结果;
2. 当前 C 计划与 C 交接;
3. 当前 B 计划与 B 交接;
4. A 项目主线与 A 交接;
5. 已登记的设计、日志和参考资料(含 V1 只读证据)。

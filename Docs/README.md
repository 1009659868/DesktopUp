# Desktop-Up 文档入口

Desktop-Up（暂定名）是一个面向 Windows 11 的低资源桌面海报启动器。第一阶段聚合游戏快捷方式，后续可扩展到应用、文件夹和其他桌面对象。

本目录采用 A/B/C 三层索引与更迭结构。不要通过文件名或修改时间猜测当前状态，请沿活跃路径阅读。

## 当前阅读路径

1. 阅读 [文档结构](DOCUMENT_STRUCTURE.md)，理解 A/B/C 与 Catalog/Plan/Log/Sync。
2. 阅读 [当前活跃路径](00_Catalog/ACTIVE_PATH.md)。
3. 阅读 [A 层冷启动](A_Project/30_Sync/COLD_START.md)。
4. 按 A 层指引进入当前 B 模块，再进入当前 C 工作流。

当前活跃链：

```text
A: Desktop-Up 产品主线 / A0 可行性与体验定型
  -> B: Desktop Runtime Foundation / 桌面运行时基础
     -> C: Desktop Host Feasibility / 桌面宿主可行性验证
```

## 设计与计划入口

- [产品设计稿 V0.1](A_Project/10_Plan/PRODUCT_DESIGN_V0.1.md)
- [技术架构设计 V0.1](A_Project/10_Plan/TECHNICAL_ARCHITECTURE_V0.1.md)
- [项目主线](A_Project/10_Plan/PROJECT_MAINLINE.md)
- [制作与交付计划 V0.1](A_Project/10_Plan/DELIVERY_PLAN_V0.1.md)
- [项目更迭记录](A_Project/20_Log/PROJECT_CHANGELOG.md)
- [发布与一键卸载规则](Rules/RELEASE_AND_UNINSTALL_POLICY.md)

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

发生冲突时按以下顺序判断：

1. 已运行代码与可重复验证结果；
2. 当前 C 计划与 C 交接；
3. 当前 B 计划与 B 交接；
4. A 项目主线与 A 交接；
5. 已登记的设计、日志和参考资料。

## 维护约定

- 未完成工作写入 `10_Plan`。
- 已发生事实和验证证据写入 `20_Log`。
- 当前接手位置、阻塞和下一动作写入 `30_Sync`。
- `00_Catalog` 只做导航，不产生第二份计划。
- 结构变化、产品决策和版本更迭遵循 [文档治理](Rules/DOCUMENT_GOVERNANCE.md) 与 [版本和更迭规则](Rules/VERSIONING_AND_CHANGELOG.md)。

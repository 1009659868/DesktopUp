# AX-BP：Axiom Core 程序蓝图索引

状态：COMPLETE / FROZEN — AX-BP-01～06 CLOSED / AXIOM EXECUTION EXITED

建立日期：2026-08-24

## 1. 计划定位

AX-BP 是 DesktopUp V2 的架构方法与技术研究计划。它只使用 Axiom Core 描述系统，冻结：

- V2 整体程序拓扑；
- 模块职责与端口；
- 调用、数据和组合关系；
- 纯定义与物理执行的边界；
- 需求到蓝图的追溯；
- 后续技术选型和完整架构的输入。

AX-BP 不实现产品，不选择 renderer/UI/StateStore/runtime/IPC/安装技术，不创建线程、队列、进程、窗口、数据库或网络连接。

axiom-runtime 处于 unstable 状态，明确排除：不采用、不推荐、不依赖、不进入候选、不作为验证证据。

## 2. 权威输入

1. 七份冻结需求及一致性审计；
2. TS-H01-E2 至 E6 DesktopHost 合同；
3. Axiom Core foundations、core API 与闭边界实现；
4. 用户关于 Axiom 正确定位和 Core-only 的最新明确决定；
5. 本目录 AX-BP-01 至 AX-BP-06 产物。

发生冲突时，用户最新明确决定和冻结需求优先。

## 3. 工作项

| ID | 任务 | 产物 | 状态 |
| --- | --- | --- | --- |
| AX-BP-01 | Axiom Core 语义审计与 V2 映射 | AX_BP_01_AXIOM_CORE_SEMANTIC_MAPPING_V1.md | FROZEN |
| AX-BP-02 | V2 整体程序拓扑 | AX_BP_02_PROGRAM_TOPOLOGY_V1.md | FROZEN |
| AX-BP-03 | 模块与端口冻结 | AX_BP_03_MODULE_PORT_FREEZE_V1.md | FROZEN |
| AX-BP-04 | 调用、数据与组合关系冻结 | AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md | FROZEN |
| AX-BP-05 | 物理执行边界冻结 | AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md | FROZEN |
| AX-BP-06 | 一致性审计、冻结与交接 | AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md | COMPLETE / FROZEN |

## 4. Axiom 在此计划中的生命周期

~~~text
Read Core
  -> map concepts
  -> describe whole program
  -> freeze ports and relations
  -> mark physical boundaries
  -> audit and freeze
  -> exit AX-BP execution
~~~

AX-BP-06 已完成；Axiom 只作为冻结蓝图的描述方法保留，不进入生产实现主线。

## 5. 产物关系

~~~text
AX-BP-01 semantic method
            |
            v
AX-BP-02 whole-program topology
            |
            v
AX-BP-03 module and port freeze
            |
            v
AX-BP-04 causal flow and composition freeze
            |
            v
AX-BP-05 physical execution boundary freeze
            |
            v
AX-BP-06 consistency audit and handoff
~~~

每个后续产物必须引用前一产物，不允许重新定义一套平行术语。

## 6. 总体不变量

- StateStore 是唯一正式业务状态权威；
- Poster、Control Center、Tray 和 Diagnostics 只消费同源 accepted snapshot 投影；
- DesktopHost 与 Renderer 是两个独立端口；
- AI 只产生 ChangePlan，最终进入普通命令/CAS 路径；
- Item、Theme、文件收纳和启动不能绕过安全策略与状态提交；
- 透明空白必须保留 Windows 桌面行为；
- 海报层级固定为普通/系统/全屏程序之下、图标和壁纸之上；
- 显示桌面、Explorer 恢复和控制中心开关不得通过瞬时置顶或隐藏补救；
- axiom-runtime 使用量必须为零；
- 未决项和延期项必须保留，AX-BP 不替用户作产品决策。

## 7. 完成标准

AX-BP 只有同时满足以下条件才能关闭：

1. 七份需求均有拓扑落点；
2. 每个模块有唯一职责和显式端口；
3. 所有正式写入均回到 StateStore CAS 闭环；
4. 调用、数据、投影、效果与反馈关系可追溯；
5. 每个纯/物理边界和资源所有者明确；
6. 没有暗中选择未完成证据的技术；
7. axiom-runtime 不存在依赖、推荐或验证证据；
8. AX-BP-06 审计不存在未解释的重复、矛盾、遗漏或实现假设；
9. 输出可直接作为技术选型和完整架构阶段输入。

## 8. 当前下一动作

AX-BP 已关闭。下一主动作返回 TS-C01～TS-C07 renderer adapter/P0 证据；Axiom Core 仅作为冻结的系统描述与追溯方法保留。

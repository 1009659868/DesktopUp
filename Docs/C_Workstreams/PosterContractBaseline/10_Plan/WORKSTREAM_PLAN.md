# Poster Contract Baseline 工作流计划

日期：2026-08-12

状态：`COMPLETE — C-PCB-01..05 PASS / C-PSS INPUT FROZEN`

## 1. 目的

在继续做 SQLite、模板运行时和大幅面布局前，先冻结“海报是什么、谁拥有哪些状态、无效状态如何失败”。本工作流交付可由 JS 与 Rust 同构实现的版本化契约和行为测试，不以当前 A0 演示对象作为权威模型。

## 2. 输入与范围

稳定宿主输入：`host-contract-v1`、canonical `sync_host_surface`、`runtime-lifecycle-v1` / `runtime-state-changed`，以及 RuntimeSnapshot 中的 HostSurface、DisplayTopology、HostTopology、DesktopHost diagnostics。

本工作流负责：

- Poster 持久实体、快照 envelope、枚举、版本与校验结果；
- 当前唯一默认快照以及工作区相关默认几何算法；
- 几何、呈现、锁定、交互态、运行态的所有权边界；
- 自制标题栏权限矩阵和失败回退行为；
- A0 legacy 字段的显式隔离与移除窗口。

本工作流不负责：SQLite 仓库与迁移、模板 DOM/Shadow Runtime、真实快捷方式导入、多海报创建 UI、Windows 私有宿主实现或完整多显示器认证。

## 3. 已冻结的首轮决策

1. 契约使用独立整数版本；未知版本必须明确失败，不能静默猜测。
2. 当前产品范围与默认快照只创建一个海报；数组模型保留未来扩展能力，但本轮不实现多海报创建或多海报人工验证。
3. Poster 只持久化业务状态。`interaction`（USING/EDITING）和 `runtime`（ACTIVE_STATIC/ACTIVE_ANIMATED/PAUSED/STOPPED）由运行时派生，不进入持久快照。
4. 默认值为 `primary`、`top-right`、`expanded`、`unlocked`、`autoCollapseEnabled=false`、`transparent-default`。
5. `minimal-fallback` 是内部临时渲染回退，不能持久化为用户 `templateKey`，也不能在读取失败时覆盖原始用户选择。
6. API 使用 camelCase；后续 C-PSS 可将其映射到 SQLite 的 snake_case 列名，不能让存储命名反向污染 API。
7. A0 `theme/accent/opacity/density/x/y/collapsed/locked` 只能通过显式 legacy adapter 转换；未转换对象不得通过正式校验。
8. C-PCB-02 默认几何只接受主显示器工作区证据：top/right/bottom 安全边距均为 `24px`；默认宽度为 `round(workArea.width × 0.22)`，默认下限 `280px`、绝对下限 `240px`、上限 `floor(workArea.width × 45%)`；expanded 高度为 `floor(workArea.height - 48px)`，最小 `240px`。
9. 目标收起内容高度为 `60px`；现有宿主四周 `10px` gutter 继续归宿主包络，因此 collapsed host 高度为 `80px`。gutter 不计入 Poster 持久宽高。
10. 默认创建使用宿主提供的不透明 `displayId` 和 `top-right` 初始锚点；收起、展开和后续呈现变化统一使用 `preserve`，保证宿主 top-left 不移动。C-PCB-02 只建立契约，不立即替换当前 Release 投影。
11. 自制标题栏同一行必须提供标题、模板标识、项目数量、呈现切换和锁定切换角色；背景与非交互文字可作为拖动起点，按钮、链接、输入控件和项目卡片始终排除拖动。拖动与单击由 `5px` 阈值区分。
12. 锁定海报在全局 `EDITING` 下解析为 `USING` 权限：禁止位置、尺寸和排序变更，但保留解锁、收起/展开、上下文菜单和项目启动。编辑态单击项目只选中；双击、Enter 或菜单等明确意图仍可启动。
13. `ACTIVE_STATIC` 与 `ACTIVE_ANIMATED` 输入权限相同；`PAUSED` 与 `STOPPED` 是最高优先级禁用覆盖，全部用户动作 fail closed，并把有效交互态解析为 `USING`。
14. `transparent-default` 与内部 `minimal-fallback` 分离；缺失、损坏、未知模板、无效快照和恢复失败均产生结构化原因。fallback 只用于本次渲染，始终保留请求键且不执行持久化写回；fallback 自身不可用时明确返回不可渲染。

## 4. 任务

### C-PCB-01：版本化海报契约

结果：纯函数契约模块能够生成唯一默认快照，严格验证版本、revision、ID、枚举、模板及持久态边界；legacy 输入有显式转换结果和诊断。

验证：

- 默认快照只有一个海报，并具有设计基线默认状态；
- 重复 ID、悬空选择、未知版本、非法枚举、非法尺寸和持久化 runtime/interaction 均失败；
- `minimal-fallback` 不能作为持久模板；
- legacy adapter 不改变原输入，并明确报告转换字段；
- 当前 Release 仍使用原投影，首切片不改变视觉与交互。

状态：`COMPLETE — JS/RUST SHARED MATRIX PASS`

### C-PCB-02：几何与呈现所有权

结果：工作区输入到默认逻辑矩形、锚点、宽高上下限、收起标题栏高度的算法只有一个实现；宿主只拥有物理窗体实现，Poster 只拥有逻辑业务几何。

特别审计：设计稿记录的 60px 收起标题栏与当前 Release 约 116 逻辑像素表面不同，不得静默选边；须用当前视觉约束、命中区与 Release 实测形成明确决策。

状态：`COMPLETE — JS/RUST GEOMETRY MATRIX PASS`

### C-PCB-03：标题栏与权限矩阵

结果：标题栏空白拖动、控件命中、锁定、收起/展开和 USING/EDITING 权限由纯状态矩阵约束；锁定不妨碍解锁、收起与启动项目。

状态：`COMPLETE — JS/RUST PERMISSION MATRIX PASS`

### C-PCB-04：默认模板与失败回退

结果：缺失/损坏模板、无效快照和恢复失败得到结构化原因；正常默认与 minimal-fallback 严格分离，回退不写回用户选择。

状态：`COMPLETE — JS/RUST FAILURE MATRIX PASS`

### C-PCB-05：契约封板与 C-PSS 交接

结果：JS/Rust 同构测试通过，A0 bridge 只有一个隔离入口，宿主 canonical 接口无新增兼容调用，当前单海报 Release 核心回归通过并形成 C-PSS 输入。

状态：`COMPLETE — AUTO GATES AND USER RELEASE REGRESSION PASS`

## 5. 完成门

- JS 与 Rust 对同一合法/非法样本给出一致裁决；
- 当前唯一默认快照、几何算法和状态所有权都有行为测试；
- 无效数据不会导致全白、静默重置或把 minimal-fallback 持久化；
- 当前已验收的单海报 Release 行为无回退；
- C-PSS 可以只依赖 C-PCB 公共契约，不读取 A0 内部字段。

## 6. 已知限制

当前发布基线只认证主屏单海报。多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未认证；该限制随 C-PCB 继承，但不应被写成契约不支持未来拓扑。

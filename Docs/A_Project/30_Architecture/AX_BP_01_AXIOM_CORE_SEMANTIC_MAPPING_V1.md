# AX-BP-01：Axiom Core 语义审计与 DesktopUp V2 映射

状态：FROZEN — AX-BP-06 PASS

完成日期：2026-08-24

## 1. 目标

确认 Axiom Core 在 DesktopUp V2 中的准确用途，避免把系统描述方法误写成生产 runtime、框架或平台能力。

本工作项只回答：

- Axiom Core 能表达什么；
- V2 哪些架构事实适合用这些概念描述；
- 哪些事实不能由 Axiom Core 证明；
- 哪些 Axiom 能力禁止进入本项目；
- AX-BP 后续文档应遵循怎样的统一词汇。

## 2. 审计输入

| 输入 | 用途 |
| --- | --- |
| axiom README 与中文 foundations | 理论边界与五概念闭包 |
| axiom core 文档 | PortCell、Wire、组合、Static、Blueprint、Slot |
| axiom src/cell_core.rs | 核对实际 trait、约束和组合实现 |
| closed_boundary tests | 核对封闭概念边界 |
| topology_blueprint tests | 核对类型拓扑与零大小蓝图 |
| DesktopUp 七份冻结需求 | 决定产品语义，优先于方法 |
| DesktopHost E2–E6 | 冻结平台宿主接口事实 |
| 用户 Core-only 决策 | 排除 unstable axiom-runtime |

审计基线：

| 字段 | 值 |
| --- | --- |
| Axiom Core version | 0.3.0 |
| audited commit | 890cc75680e150d98cbbe4133976dfcf35e8e871 |
| Core tests | 21 unit + 5 closed-boundary + 6 topology PASS |
| runtime | OUT OF SCOPE / UNSTABLE / EXCLUDED |
| DesktopUp fixture dependency | axiom Core only |

## 3. 正确定位

~~~text
Axiom Core
  architecture method
  + typed system description
  + compile-time topology
  + composition vocabulary
  + adapter slot description
  - production runtime
  - OS integration
  - rendering
  - persistence
  - process/thread implementation
~~~

Axiom Core 位于架构方法和技术研究层。它用于绘制 V2 整体程序蓝图，帮助冻结模块端口、调用关系、组合关系和物理执行边界。

## 4. Core 概念到 V2 的映射

| Axiom Core 概念 | 准确含义 | V2 使用方式 | 不得推断 |
| --- | --- | --- | --- |
| PortCell | 类型化输入、输出、状态和 step 的开放系统 | 表达纯转换、归一化、验证、投影和架构端口单元 | step 自动纯、无阻塞或线程安全 |
| Wire | A.out 与 B.in 的类型对偶关系 | 表达一个有方向的因果调用或数据关系 | 业务语义、时序、可靠性或平台正确 |
| Chain | 串行组合后仍为 Cell | 表达 ingress → reducer → projector 等顺序 | 自动产生异步流水线或线程 |
| Broadcast | 一个输出进入多个兼容分支 | accepted snapshot 投影到 Poster/Control/Tray/Diagnostics | 具体复制、共享或调度机制 |
| Merge | 多个源进入同一接收边界 | 外部事件经各自归一化后进入 RuntimeIngress | 并发仲裁、优先级和到达顺序 |
| Diamond | 分流后汇合 | 同一 snapshot 生成多投影并在 revision 上一致 | UI 或 renderer 已同步显示 |
| Feedback | 输出再次成为后续输入的闭合形状 | adapter result 作为新的 RuntimeInput 回到 Core | 无延迟即时递归或生产事件循环 |
| Choice | 类型化分支 | 可选命令/事件族的静态分类 | 运行时插件发现机制 |
| Opt | 零或一次的类型化可选作用 | 可选 AI 建议、可选主题覆盖的纯描述 | provider 生命周期或容错 |
| Rep/Repeat | 编译期固定次数的自组合 | 仅适合固定验证步骤或测试结构 | 动态 Item 数、多海报数或显示器数 |
| Slot | 固定输入/输出的可装载型位 | 描述 DesktopHost、Renderer、StateStore 等 adapter 端口 | adapter 已实现、可热换或通过 P0 |
| Conforms | 占据者类型满足 Slot 接口 | 编译期检查 DTO 类型兼容 | 行为、性能、安全或资源清理合格 |
| Blueprint | 零大小类型层拓扑定义 | 表达 DesktopUpProgram 的系统蓝图 | 应用已激活或资源已存在 |
| Static | 对某子图的静态性声明 | 标记纯、框架中立、可单态化的算法子图 | 整个桌面程序零成本 |

## 5. V2 中适合成为 Cell 的内容

- RuntimeInput 归一化；
- CommandEnvelope 结构验证；
- expected revision 前置判断；
- MutationPlan 计算；
- accepted snapshot 到多个表面投影；
- Item 有效性分类；
- 活跃度计算与稳定排序；
- 布局约束与逻辑布局计算；
- ThemeSpec schema/capability 验证；
- AI ChangePlan schema、权限和能力验证；
- 生命周期事件归类；
- 诊断事件脱敏和分类。

这些内容必须保持无 OS、数据库、网络、GPU、线程和文件 I/O。

## 6. 不适合成为 Core Cell 的内容

- 创建或操作 HWND；
- WorkerW/Progman 发现与恢复；
- GPU surface、frame present 和设备恢复；
- SQLite/文件 commit；
- 进程启动、协议调度和文件选择器；
- 托盘图标创建和系统菜单；
- 主题包解压、文件安装和保险库移动；
- AI provider 网络调用；
- 线程、队列、调度、IPC 和取消执行；
- 安装、自启、升级和卸载。

这些能力只能在蓝图中表现为端口和 PhysicalExecutionBoundary。

## 7. 定义、装载、激活

| 阶段 | 内容 | 允许的资源 |
| --- | --- | --- |
| Definition | Cell、Wire、组合、Slot、Blueprint | 零外部资源 |
| Binding description | 指定某类 adapter 应占据哪个 Slot | 仍为文档/类型定义 |
| Installation | 后续完整架构选择并装配实现 | 不属于 AX-BP |
| Activation | 程序接收启动意图并创建物理资源 | 不属于 AX-BP |

AX-BP 只覆盖 Definition 和 binding description，不执行 Installation 或 Activation。

## 8. 物理执行边界的表达规则

每个跨出纯图的关系必须记录：

1. 起点模块和输出端口；
2. 终点外部能力或 adapter 端口；
3. 请求值；
4. 返回事件；
5. 顺序与 revision/epoch 约束；
6. 可靠性、取消、超时和迟到事件要求；
7. 资源所有者；
8. 后续由哪个技术选型域决定。

这些字段只是约束，不对应 axiom-runtime 类型。

## 9. 明确禁止

- 在 Cargo.toml 中加入 axiom-runtime；
- 将 axiom/runtime 源码复制或 vendor 到 DesktopUp；
- 推荐 spawned_flow、SlotDrive 或其他 runtime API；
- 把 Axiom 测试当作 Win+D、层级、透明或性能证据；
- 把 Rep<N> 用作动态产品实例模型；
- 把 Conforms 当作 adapter 通过验收；
- 让 Axiom 类型渗入主题包、持久化 schema 或跨进程公共格式；
- 在 AX-BP 内实现线程、队列、IPC、数据库或窗口。

## 10. 类型夹具职责

prototype/axiom-desktopup-blueprint 只证明：

- 顶层定义可表达为 Axiom Core 类型；
- Blueprint 和 Static 声明为零大小；
- CoreState 默认不伪造业务 snapshot；
- mutation 在 StateStore 接受前不改变 accepted snapshot；
- stale revision 被拒绝；
- 多表面投影共享一个 revision；
- adapter dispatch/event 端口可由 Slot 描述。

它不证明生产架构、runtime 或平台能力。

## 11. AX-BP-01 退出门

| 检查 | 结果 |
| --- | --- |
| Core 文档与实现已读 | PASS |
| Core 测试通过 | PASS |
| V2 概念映射完整 | PASS |
| 能力与证明边界明确 | PASS |
| axiom-runtime 排除 | PASS |
| 类型夹具仅依赖 Core | PASS |
| 动态实例没有误用 Rep | PASS |
| Slot/Conforms 没有被当作行为证明 | PASS |
| 物理能力留在边界外 | PASS |

## 12. 交接 AX-BP-02

AX-BP-02 必须使用本文件词汇建立 DesktopUpProgram 整体拓扑，并确保七份冻结需求全部有节点和关系落点。不得重新引入 runtime、Carrier 实现或候选框架类型。

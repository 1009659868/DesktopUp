# Desktop-Up V2 技术候选登记表 V1

状态：`ACTIVE REGISTER — TS-C01 R0 + R1 + R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN / NO WINNER`

日期：2026-08-20

## 1. 登记规则

本表只定义要调查和实测的候选空间，不代表推荐、排序或兼容性结论。候选名称不附带“轻量”“稳定”“容易”等未经证据支持的评价。

每个候选已在 TS-01 补齐事实卡；结果见 [TS-01 候选事实审查](TS01_CANDIDATE_FACT_REVIEW_2026-08-20.md)。公共 `TS-H01 Desktop Overlay Host` 探针和 E1-E6 接口冻结已经完成；候选下一步必须按 [E6 交接规则](TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md) 独立接入同一 DesktopHost、Night Ops 夹具和 E5 诊断 schema，再执行完整 P0 子矩阵。公共结论不等于候选通过。

## 2. 首要候选：海报呈现与 Windows 桌面宿主

| ID | 候选家族 | 统一评估边界 | 当前状态 |
| --- | --- | --- | --- |
| TS-C01 | Qt 6.8.3 / Qt Quick + Windows 原生宿主适配 | Qt 负责海报绘制/布局；有界原生层负责窗口、桌面层、命中和 Shell 事件 | `R0 + R1 + R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN` |
| TS-C02 | 原生 Win32 + Direct2D/DirectComposition | 原生窗口、绘制、合成、命中和桌面适配作为一个候选栈 | `FACT CHECKED / COMMON HOST USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS / P0 NOT RUN / NOT A WINNER` |
| TS-C03 | WinUI 3 / Windows App SDK + Windows Composition/原生适配 | WinUI/Composition 负责视觉；有界原生层补充特殊桌面窗口能力 | `FACT CHECKED / P0 NOT RUN` |
| TS-C04 | WPF + Windows 原生宿主适配 | WPF 负责海报 UI；有界 Win32 层负责桌面宿主和窗口语义 | `FACT CHECKED / P0 NOT RUN` |
| TS-C05 | Avalonia + Windows 原生宿主适配 | Avalonia 负责海报 UI；有界原生层负责桌面宿主和窗口语义 | `FACT CHECKED / P0 NOT RUN` |
| TS-C06 | WebView2 + Windows 原生宿主适配 | Web 内容负责海报视觉；有界原生层负责窗口、桌面宿主、命中和进程/渲染故障边界 | `FACT CHECKED / P0 NOT RUN / HISTORICAL RISK` |
| TS-C07 | Slint + Windows 原生宿主适配 | Slint 负责海报 UI；有界原生层负责桌面宿主和窗口语义 | `FACT CHECKED / P0 NOT RUN` |
| TS-C08 | 分离式组合：原生海报 + 独立高层控制中心 | 海报使用通过 P0 的原生呈现栈，控制中心另选 UI 技术；必须计入双栈和状态投影成本 | `FACT CHECKED / DEFERRED UNTIL ONE POSTER CANDIDATE PASSES P0` |

候选登记顺序固定按编号，不表示优先级。TS-01 可新增候选，但必须说明与现有候选的实质差异，不能用换语言绑定的方式重复登记同一渲染栈。

### 2.1 公共前置条件 TS-H01（不是候选）

`TS-H01 Desktop Overlay Host` 使用最小原生 Win32 探针，先独立证明：

- `普通程序/全屏程序 > 海报 > 桌面图标 > 壁纸`；
- Win+D 三种入口全程连续可见；
- Explorer/WorkerW 重建不会把海报提升为普通顶层或 TopMost；
- 空白输入原生落到桌面和图标，Item/标题/按钮仍由海报命中；
- DPI、尺寸、非客户区和渲染表面边界一致。

TS-H01 通过不表示 TS-C02 获选；它只冻结所有渲染候选共用的 `DesktopHost` 原型边界。接口、原子投影、诊断与交接见 [DesktopHost 原型接口](DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md)、[E4 合同](TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md)、[E5 夹具](TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md) 和 [E6 规则](TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md)。所有候选当前仍为 P0 NOT RUN。

## 3. 支撑技术决策域

海报候选通过 P0 后，再组合以下支撑域。支撑域不能反向放宽海报硬门。

### TS-D03 控制中心 UI

待调查候选家族：

- 与海报复用同一 UI 技术；
- Qt Quick/Qt Widgets；
- WinUI 3；
- WPF；
- Avalonia；
- WebView2；
- 其他满足普通窗口、预览、表单、键盘和无障碍要求的候选。

必须比较：预览投影、表单/滑杆防回弹、窗口缩放、资源释放、键盘/无障碍、打包体积、双栈维护成本。

### TS-D04 核心运行时

首轮事实候选：Rust、现代 C++、.NET/C#。允许组合，但组合必须量化 FFI、调试、部署、崩溃边界和维护成本。

必须承载或协调：单一实例、托盘、Shell 启动、生命周期、权威状态、主题包校验、安全收纳、日志与故障恢复。

### TS-D05 权威存储

候选类别：

- 嵌入式事务型关系存储；
- 支持事务/版本的嵌入式文档或 KV 存储；
- 应用自有日志化结构存储。

必须实证：事务、revision/CAS、迁移、原子备份、损坏识别、last-known-good、并发写入和卸载恢复。普通配置文件单写或 UI local storage 不能直接视为合格候选。

### TS-D06 IPC/消息

候选类别：同进程版本化命令总线、Windows 本地命名管道、受限本地 RPC/消息协议。是否需要 IPC 依赖后续进程边界；不能为“以后可能多进程”提前引入无证据复杂度。

### TS-D07 主题表达与隔离

候选类别：

- 框架原生声明式组件的受限子集；
- 数据驱动场景图/布局描述；
- 自定义受限声明语言；
- 隔离进程中的受限 HTML/CSS 类表达；
- 参数/素材层与高级结构层分离的组合。

必须证明：三正常模板、fallback、受保护结构、参数 schema/契约、素材净化、无任意文件/网络/进程权限、原子切换和崩溃隔离。

### TS-D08 安装、自启与升级

待调查候选包括 NSIS、WiX/MSI/Burn、MSIX 及满足冻结契约的其他当前用户部署技术。必须验证独立卸载器、无管理员安装、依赖交付、自启 opt-in、升级回滚和用户文件恢复；仅能打包开发机运行时不算通过。

### TS-D09 验证与诊断

至少需要：窗口树/owner/parent/z-order/minimize/cloak 观测，非客户区/透明合成诊断，屏幕录制，输入命中探针，Explorer 重建，性能采样，故障注入和净机安装记录。具体工具在 TS-01/TS-02 决定。

## 4. 候选事实卡模板

每个候选建立同样字段：

| 字段 | 要求 |
| --- | --- |
| Candidate ID | 唯一编号，不复用 |
| 被测试版本 | 精确版本、发布日期和支持周期 |
| 官方来源 | 文档、仓库、许可证、支持矩阵的直接链接及访问日期 |
| 目标平台 | Windows 11 x64、所需最低系统组件 |
| 工具链 | 编译器、SDK、构建系统、IDE 非强制依赖 |
| 运行依赖 | 动态库、框架运行时、系统组件、GPU/驱动要求 |
| 许可证/分发 | 应用与依赖的许可、归属和再分发义务 |
| 窗口模型 | 原生窗口、子窗口、离屏、合成和事件循环能力 |
| 透明/圆角/阴影 | 能力来源及是否产生非客户区/合成风险 |
| 输入模型 | 命中测试、穿透、拖动、非激活、键鼠/触摸 |
| Desktop/Shell 边界 | Win+D、Explorer、任务栏、桌面层适配点 |
| 主题表达 | 参数、素材、结构、样式和受限扩展能力 |
| 故障隔离 | 渲染失控、崩溃、资源泄漏的终止/恢复能力 |
| 诊断能力 | 窗口/渲染/性能/输入可观测性 |
| 部署 | 当前用户安装、体积、启动、自启、升级、卸载 |
| 已知事实限制 | 仅列官方资料或本机实测，不写传闻 |
| 待验证假设 | 每项必须映射矩阵 ID |
| 适配层边界 | 入口、职责、预计复杂度和失败面 |
| 当前结论 | `UNASSESSED / SCREENED / PROTOTYPE / PASS / FAIL / WITHDRAWN` |

## 5. 候选状态机

```text
REGISTERED
→ FACT CHECKED
→ STATIC SCREEN PASS/FAIL
→ P0 PROTOTYPE
→ P0 PASS/FAIL/CONDITIONAL
→ FULL MATRIX
→ FINALIST/REJECTED
→ SELECTED（最多一个组合）
```

任何状态变化都必须带证据路径和日期。`CONDITIONAL` 必须说明有界修正；不能无限保留“稍后修”。

## 6. 初始风险假设（不是结论）

| 假设 | 适用候选 | 必须由什么证据回答 |
| --- | --- | --- |
| 高层 UI 框架的窗口模型可能不足以直接表达桌面层角色 | TS-C01/03/04/05/06/07 | 原生窗口树与层级遥测、Win+D 和输入矩阵 |
| 全原生栈可能增加视觉/主题开发和维护成本 | TS-C02 | Night Ops/三主题同构实现、代码与构建复杂度、迭代时间 |
| 双栈可能改善故障隔离但增加状态/打包/维护成本 | TS-C08 | 生命周期、状态投影、资源和部署证据 |
| Web 内容可能满足主题自由度但引入合成、输入和渲染故障风险 | TS-C06 或含 Web 组合 | 顶部条带、非客户区、进程崩溃、内存和权限硬门 |
| 框架运行时和许可可能影响安装体积与交付 | 全部非纯系统候选 | 固定版本包、净机安装、许可证事实卡 |

这些假设不得预填为扣分。只有统一矩阵证据才能改变候选状态。

## 7. 组合爆炸控制

- 先选择可通过 P0 的海报呈现+宿主短名单；
- 控制中心只对短名单比较“复用”与“分离”两类策略；
- 核心/存储/IPC/主题/安装按依赖顺序筛选，不枚举所有笛卡尔积；
- 任一组合必须说明接口边界和新增硬门；
- 架构阶段只接收一个已选技术组合及最多一个受控备选，不把选型争议下放到实现。

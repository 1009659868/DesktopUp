# TS-H01-E DesktopHost 原型接口冻结计划 V1

状态：`COMPLETE — E1-E6 FROZEN / CANDIDATE ADAPTERS NEXT / NO WINNER`

日期：2026-08-21

## 1. 目标

将 TS-H01-A 至 D 的公共宿主证据转换为实现中立、可由不同渲染候选共同使用的 `DesktopHost` 原型接口和一致性夹具。该步骤不选择 Qt、WinUI、WPF、Avalonia、WebView2、Slint 或 Direct2D，也不把 Win32 探针内部技巧冻结为生产架构。

## 2. 权威输入

1. 七份冻结需求；
2. Desktop Overlay Host 探针章程；
3. TS-H01-B/C/D1 的自动与人工 PASS 证据；
4. D2 的用户接受及 1/10 恢复遥测例外；
5. D3-1 的部分 DPI 覆盖与未测档；
6. D3-2 的人工通过及 8/10 变化遥测例外；
7. D3-3 的 3,802/3,802 样本与人工 PASS；
8. 统一候选证据矩阵。

用户验收可允许工作流继续，但不会把 UNKNOWN、跳过项或未闭合自动门改写成 PASS。

## 3. 工作包

| ID | 内容 | 产出 | 状态 |
| --- | --- | --- | --- |
| E1 | 汇总 H0–H5 和适用范围 | TS-H01-D 汇总结论、例外与残余风险 | `COMPLETE` |
| E2 | 冻结能力与数据合同 | `DesktopHost` 方法、类型、修订和错误语义 | `COMPLETE` |
| E3 | 冻结生命周期与恢复合同 | negotiate/attach/degraded/recover/detach/device-loss 状态机和幂等规则 | `COMPLETE` |
| E4 | 冻结几何、命中与原子更新合同 | DIP/anchor、视觉/命中同修订提交、收起与锁定边界 | `COMPLETE` |
| E5 | 冻结诊断和一致性夹具 | 结构化事件、采样字段、候选必须复跑的最小门 | `COMPLETE` |
| E6 | 候选交接 | TS-C01 至 TS-C07 接入顺序、证据目录和退出规则 | `COMPLETE` |

## 4. 非协商接口不变量

- 普通/全屏程序 > 海报 > 桌面图标 > 壁纸；
- 不允许 TopMost、激活、普通顶层回退、隐藏后恢复或轮询抬层；
- 海报空白保持 Explorer 原生输入，只有显式交互岛由宿主命中；
- 逻辑几何使用 DIP 与归一化锚点，物理像素只作投影和诊断；
- visual geometry、hit regions 与 render target 在同一 revision 提交；
- Explorer 世代丢失时先进入 degraded，未确认安全宿主前不显示普通顶层替代物；
- 所有能力均携带 `PASS / USER-ACCEPTED / UNKNOWN / DEFERRED` 的证据状态和环境范围；
- 候选不得绕过接口直接修改 parent、owner、z-order 或 Win+D 行为。

## 5. 明确不冻结

- 生产进程边界、语言 ABI、线程库和 IPC 技术；
- 控制中心 UI、核心语言、数据库、主题 DSL 和安装器；
- Win32 探针的固定 HWND、类名、枚举序号或临时代码组织；
- traditional WorkerW、多显示器、混合 DPI、负坐标、热插拔和未测 DPI 档的支持结论；
- 任一 UI/渲染候选的赢家状态。

## 6. E 阶段退出门

只有同时满足以下条件才允许首个候选适配器进入 P0：

1. `DesktopHost` 类型、方法、错误和状态机无隐含 Win32 探针对象泄漏；
2. 每项能力注明证据状态、环境范围和例外；
3. attach/recover/detach 幂等与所有权明确；
4. geometry/hit/render revision 的原子性可测试；
5. 诊断字段覆盖层级、可见性、DPI、边界、命中、Shell 世代和恢复原因；
6. 一致性夹具能被 TS-C01 至 TS-C07 原样复用；
7. 文档明确 TS-H01 的用户接受例外不会借给候选 P0。

## 7. 候选进入顺序

接口冻结后按登记编号建立候选适配器证据：TS-C01 至 TS-C07；编号只用于稳定标识，不表示推荐顺序。每个候选只接入同一 Night Ops 最小夹具和同一 P0 子矩阵。TS-C08 仅在至少一个海报候选通过 P0 后评估。

## 8. 完成结论与下一动作

- E2 冻结能力、数据、surface 协商、状态权威和修订分层；
- E3 冻结资源所有权、首帧门、Shell/device loss、恢复、detach 与粘滞退出；
- E4 冻结 DIP/anchor/constraints、gesture、收起/锁定和 geometry/hit/render 同 revision 发布；
- E5 冻结公共 JSONL envelope、场景 manifest、validator 与正反例自测；
- E6 冻结 TS-C01 至 TS-C07 的 adapter 包、证据隔离、fail-fast 和退出规则。

TS-H01-E 已完成。下一动作是按稳定 ID 为 TS-C01 至 TS-C07 建立真实 adapter/P0 证据。D2 的 1/10、D3-2 的 8/10、未测 DPI、多屏、traditional WorkerW、renderer/device-loss 和跨进程适配继续保持原来的例外、`UNKNOWN/DEFERRED/NOT_RUN`；公共宿主结论不得借给候选，也仍无框架赢家。

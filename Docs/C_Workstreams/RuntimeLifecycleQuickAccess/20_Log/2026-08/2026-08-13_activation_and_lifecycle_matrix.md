# C-RLC 激活与生命周期矩阵冻结

日期：2026-08-13

状态：`C-RLC-01 COMPLETE / C-RLC-02 NEXT`

## 目标

在 C-IBC 关闭并完成 Git 同步后，激活 Runtime Lifecycle and Quick Access，先建立工作流治理和可执行生命周期矩阵，不提前修改注册表、进程参数或 Windows 消息处理。

## 检查事实

- 普通冷启动只创建海报，控制中心首次请求创建；托盘单击无动作、双击切换控制中心，关闭控制中心不影响海报；
- 当前单实例回调丢弃 `_args`，所有第二实例都会调用 `show_main_window`，尚未实现精确 `--autostart` 分流；
- `autostart-contract-v1` 和只读 `autostart-diagnostics-v1` 已存在，但托盘自启项仍禁用，生产代码没有注册表写入；
- `runtime-lifecycle-v1` 已提供 Starting/Running/Exiting、运行模式和退出粘滞状态；
- 桌面宿主已有 Shell 恢复协调器和手动重连，但没有明确的 `TaskbarCreated` 托盘重建入口；
- 当前发布认证仍只覆盖主屏单海报。

## 冻结决策

- 新建 [`RUNTIME_LIFECYCLE_MATRIX_V1.md`](../../10_Plan/RUNTIME_LIFECYCLE_MATRIX_V1.md)，统一启动、表面、托盘、自启、恢复和退出状态；
- 有效启动意图只允许普通空参数与精确单参数 `--autostart`；第二自启实例和非法参数均不得呼回控制中心；
- 自启启停只在正式安装路径门后由用户显式触发，并采用写后读回；开发/便携 Release 保持零写入；
- `TaskbarCreated` 采用去抖、单飞、有界恢复，Exiting 阶段不重建任何表面；
- 将实施拆为 C-RLC-02..06，C-RLC-02 先解决启动意图和单实例分流。

## 验证

- C-RLC-01 只产生文档变化，没有写注册表、启动额外进程、改变用户数据或修改运行时代码；
- 全量 Node/契约 130/130、149 份 Markdown 链接和 `git diff --check` 通过；差异仅包含文档。

## 遗留问题

- 自启真实启停需要正式安装路径矩阵，不能用开发 Release 假装通过；
- Explorer 真重启和 TaskbarCreated 仍需 C-RLC-05 的自动/人工证据；
- 多显示器/DPI/热插拔继续延期，不随 C-RLC 激活扩张认证。

## 关联文档与代码

- [`WORKSTREAM_PLAN.md`](../../10_Plan/WORKSTREAM_PLAN.md)
- [`RUNTIME_LIFECYCLE_MATRIX_V1.md`](../../10_Plan/RUNTIME_LIFECYCLE_MATRIX_V1.md)
- [`AUTOSTART_CLEANUP_CONTRACT_V1.md`](../../../InstallBaselineContract/10_Plan/AUTOSTART_CLEANUP_CONTRACT_V1.md)
- [`src-tauri/src/lib.rs`](../../../../../src-tauri/src/lib.rs)
- [`src-tauri/src/runtime_lifecycle.rs`](../../../../../src-tauri/src/runtime_lifecycle.rs)
- [`src-tauri/src/autostart_contract.rs`](../../../../../src-tauri/src/autostart_contract.rs)

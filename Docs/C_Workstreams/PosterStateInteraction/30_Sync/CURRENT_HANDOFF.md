# Poster State and Interaction 当前交接

最后更新：2026-08-14

状态：`ACTIVE — C-PSI-03 GEOMETRY`

## 已完成

- C-PSI-01 已完成，冻结 [`C_PSI_AUDIT_MATRIX_V1.md`](../10_Plan/C_PSI_AUDIT_MATRIX_V1.md)；
- 7 组既有能力直接吸收，包括 SQLite/CAS、正式投影、控制中心几何投影、原生顶部/层级、命中、生命周期与重建；
- 确认 7 个真实缺口，分配给 C-PSI-02..04；审计切片未修改生产代码。
- C-PSI-02 已完成，详见 [`状态权威与失败保留日志`](../20_Log/2026-08/2026-08-14_cpsi02_state_authority_failure_retention.md)：两端已消费共享权限解析，效果暂停与系统输入不可用已分流，pending/retained 正式字段覆盖和 revision 拒绝已自动化通过；
- 全量证据为 Node `163/163`、Rust `132/132`、文档链接 `177` 个和前端构建通过。

## 当前工作

C-PSI-03 统一移动/缩放入口与动态工作区约束，只允许 `EDITING + EXPANDED + unlocked + runtimeInteractive` 的合法组合，并补非 Item 内容区拖动。

## 下一步

只实现审计矩阵中的 GAP-04..06：工作区 `45%` 最大宽度、安全最大高度、右下角固定缩放、权限驱动手柄/事件入口和编辑态非 Item 内容拖动。不得提前实现标题文字展开、5px 阈值、海报右键菜单或正式 Item。

## 完成后的唯一主线

进入 C-PSI-04 命中、标题栏与运行时同步；C-PSI-05 关闭后再交接 B-COL/C-CIM。

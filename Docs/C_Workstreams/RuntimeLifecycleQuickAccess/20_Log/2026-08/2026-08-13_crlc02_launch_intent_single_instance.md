# C-RLC-02 启动意图与单实例分流

日期：2026-08-13

状态：`C-RLC-02 COMPLETE / C-RLC-03 NEXT`

## 目标

把首实例和第二实例的启动参数收敛为同一版本化决策表，保证普通第二实例只呼回控制中心，精确 `--autostart` 不打开控制中心，非法组合不改变已有 UI。

## 实施

- 新增 [`launch_intent.rs`](../../../../../src-tauri/src/launch_intent.rs)，建立 `launch-intent-v1`、首/次实例来源、intent kind、disposition、拒绝码和退出码 64；
- 产品启动只接受空参数与精确 `--autostart`；既有 `--cdrp-performance-gate` 明确为仅首实例可用的内部诊断意图；
- 单实例回调不再丢弃参数：interactive 呼回唯一控制中心，autostart 保持现有 UI，非法或诊断转发只记录结构化拒绝；
- 恢复探针继续先于 launch intent、Tauri 和单实例初始化，保持隔离恢复路径不受影响；
- 新增只读 `launch_intent_diagnostics`，只公开参数数量、分流结果和拒绝码，不保存未知参数原文；
- CDP 探针增加 launch-intent 快照字段；本轮本机 9333 端口未开放，因此 Release 结论使用进程、窗口标题与退出码证据，不将 CDP 连接计为通过。

## 自动验证

- `pnpm run check`：135/135 通过；
- Rust 全量测试：117/117 通过，其中 C-RLC-02 表驱动单元测试 6/6；
- `pnpm run build` 与 `pnpm run tauri:build` 通过；
- 150 份 Markdown 链接检查通过；
- `git diff --check` 通过。

## Release 多进程证据

- 首实例精确 `--autostart` 后仅运行一个项目进程，窗口标题为空，控制中心未创建；
- 第二精确 `--autostart` 退出码 0，首实例仍响应且控制中心保持 absent；
- 第二普通启动退出码 0，首实例窗口标题变为 `Desktop-Up · A0 Demo`，证明唯一控制中心被呼回；
- 非法 `--autostart --extra` 退出码 64，不打开控制中心；无首实例时同样以 64 退出且不残留项目进程；
- 探针前后正式 SQLite SHA-256 均为 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`；本轮未写 HKCU；
- 最新 Release `DesktopUp.exe` SHA-256 为 `2E1F9DDFAC476073C0246DB16B0AE814EA0568D37A60D236B17EECB7B4FB891B`；收口时已恢复普通 Release 运行。

## 结论与下一步

生命周期矩阵 L02..L06 已具备自动化和真实 Release 多进程证据，C-RLC-02 关闭。下一步 C-RLC-03 只在正式安装路径门后实现 HKCU Run `DesktopUp` 的显式 enable/repair/disable 和统一写后读回快照；开发/便携 Release 必须继续零写入。

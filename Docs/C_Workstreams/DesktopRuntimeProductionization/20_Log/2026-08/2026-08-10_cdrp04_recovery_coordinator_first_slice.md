# C-DRP-04 恢复协调器第一实现切片

日期：2026-08-10

状态：`COMPLETE — USER ACCEPTED / PHYSICAL MATRIX DEFERRED TO C-DRP-06`

## 1. 本轮目标

把散落在原生窗口消息、Shell 事件、Tauri DPI 事件和手动重连中的恢复逻辑收敛为一条可合并、可串行、可诊断的恢复路径。当前仍只验证单全局宿主和单海报；第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启与热插拔物理矩阵继续延期至 C-DRP-06。

## 2. 审计结论

实施前存在三条相互独立的恢复路径：

1. `desktop_host.rs` 的 WinEvent/窗口消息工作器只重新锚定 ToolWindow，并读取一份原生内存位置缓存；
2. Tauri `ScaleFactorChanged` 分支独立读取 `PlacementStore`，再单独重同步 HostSurface；
3. 命令与托盘“重新连接桌面宿主”直接调用宿主配置。

此外，原生 `LAST_POSTER_PLACEMENT` 与应用层 `PlacementStore` 同时持有位置状态，恢复所有权重复；旧工作器在同一事件风暴中可能执行不止一轮恢复。

## 3. 已实施

- 新增 `HostRecoveryTrigger` 与 `HostRecoveryRequest`，稳定记录 Shell 前台、Shell 生命周期、显示变化、DPI、工作区、Tauri scale-factor 和手动重连原因；
- 原生事件进入 140ms 尾沿合并窗口；代数、真实事件数和原因由同一互斥累加器生成快照，唤醒令牌与快照在同一临界区处理，避免漏唤醒、重复恢复或原因漂移；
- 新增应用层 `HostRecoveryCoordinator`，所有恢复串行执行：
  1. 重配 WorkerW/ToolWindow 宿主；
  2. 从唯一 `PlacementStore` 恢复位置；
  3. 以新 revision 重同步 HostSurface；
- Shell WinEvent、`WM_DISPLAYCHANGE`、`WM_DPICHANGED`、`WM_SETTINGCHANGE`、Tauri `ScaleFactorChanged`、命令与托盘手动重连均已进入上述路径；
- 删除原生 `LAST_POSTER_PLACEMENT` 和已无调用的窗口级独立恢复 API，位置恢复只保留应用层持久状态所有权；
- `DesktopHostDiagnostics` 新增恢复次数、代数、合并事件数、最近触发原因与最近错误；普通宿主重配不会清空这些诊断。

## 4. 自动验证

- `pnpm run check`：53/53 通过，文档链接检查通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：39/39 通过；
- 新增 Rust 测试覆盖恢复原因合并、真实事件计数和稳定原因顺序；
- 新增源码契约测试，约束原生/Tauri/手动入口只能进入同一应用层协调器；
- `pnpm run build` 与 `pnpm run tauri:build` 通过；
- Release：`src-tauri/target/release/desktop-up.exe`；
- NSIS：`src-tauri/target/release/bundle/nsis/Desktop-Up_0.0.1_x64-setup.exe`；
- 最新 Release 已启动，等待当前单显示器恢复矩阵。

## 5. 尚未关闭的验证

C-DRP-04 仍保持 `IN PROGRESS`。下一步在当前单显示器环境验证：

1. 默认 ToolWindow 回退下连续执行 Win+D，确认无消失、无重复窗口、无 Z 序漂移；
2. 重启 Explorer，确认桌面恢复后海报重新归位且交互不回退；
3. 连续执行托盘手动重连，确认位置稳定且无恢复风暴；
4. 分别覆盖强制 ToolWindow 回退和可用时的 WorkerW opt-in 路径；
5. 观察恢复诊断的次数、合并事件数、触发原因与错误。

第二显示器相关物理矩阵不在本轮人工清单中，不得据此标记已通过。
## 6. Win+D 闪烁复测与修正

用户在第一版 Release 中确认：连续按 Win+D 时海报会闪烁。该项判定为未通过，C-DRP-04 不得关闭。

根因是 Shell 前台/生命周期事件虽然已合并，但协调器仍对每轮 Win+D 执行完整宿主重配、位置恢复和 HostSurface 重同步；其中 `SWP_FRAMECHANGED`、resize 与窗口 region 重写会造成可见闪烁。

修正后恢复协调器按请求类型选择最小动作：

- 仅含 Shell 前台/生命周期原因时进入 `shellRepair`；
- ToolWindow 回退只调用无移动、无 resize、无 `SWP_FRAMECHANGED` 的桌面 Z 序重锚；
- WorkerW 路径只校验当前父窗口仍为 `WorkerW`；
- 只有父挂载失效，或触发原因为显示/DPI/工作区/手动重连时，才执行 `fullRecovery`；
- 新增 `lastRecoveryAction` 诊断，用于区分 `shellRepair` 与 `fullRecovery`。

修正后 `pnpm run check` 53/53、Rust 39/39、Release/NSIS 重建通过。修复版 Release 已于 2026-08-10 16:39 启动（当次 PID 50264），等待用户重新执行连续 Win+D 复测。
## 7. 即时闪烁的二次定位与有界 Z 序防护

用户对 PID 50264 的第二次复测确认仍会闪烁，且发生在按下 Win+D 后立即出现。这证明第 6 节识别的完整重配只是延迟闪烁因素，不是剩余即时闪烁的完整根因。

原生 5ms Z 序探针得到以下证据：

- 闪烁版在 `show+5ms` 到 `show+20ms` 期间保持 `IsWindowVisible=true`，但海报位于 Progman 下方；
- 单次同步重锚版在 `show+5ms` 已位于 Progman 上方，Shell 随后仍在 `show+10ms` 到 `show+45ms` 进行第二次 Z 序重排，把海报重新压到 Progman 下方，约 `show+95ms` 才恢复；
- WinEvent 探针只收到 Progman 的 `EVENT_SYSTEM_FOREGROUND`，未收到可用于第二次重排的 `EVENT_OBJECT_REORDER`，因此不能依赖另一个 Shell 事件精确收尾；
- 未采用 Progman owned-popup 方案，因为 Explorer/Progman 生命周期会让 owner 销毁语义进入海报存活边界，扩大 Explorer 重启风险。

最终实现采用事件触发、时间有界的 ToolWindow 防护：

1. Progman/WorkerW 成为前台时，同步设置 `HWND_TOPMOST`，同时保留 `SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE`；
2. 防护只覆盖 48ms Shell 二次重排窗口；
3. 任意非 Shell 窗口恢复前台时立即 `HWND_NOTOPMOST`；否则恢复工作器在 48ms 后解除；
4. 解除后同步重锚到 Progman 上方，再继续原有 140ms 合并和轻量 `shellRepair`；
5. 用户隐藏海报时也强制解除，避免 topmost 状态残留。

最终 Release（PID 20320）原生对照结果：

- 单轮 5ms 探针所有采样点 `delta < 0`；`show+10ms/+20ms` 防护生效，`show+45ms` 已恢复 `topmost=false` 且海报仍位于 Progman 上方；
- 连续 10 轮中 `showPositive=false`、`showMaxDelta=-1`、`showSettledTopmost=false`、`backAppTopmost=false` 全部成立；
- `pnpm run check` 53/53、Rust 39/39、Release 与 NSIS 构建通过。

该证据关闭了原生 Z 序窗口，但不能代替人的视觉观察。C-DRP-04 继续保持 `IN PROGRESS`，等待用户在 PID 20320 上连续按 Win+D，确认肉眼无闪烁。

## 8. 用户验收关闭

用户在最终 Release 上确认连续 Win+D 已较稳定，并接受 C-DRP-04 通过。C-DRP-04 因此关闭；第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔物理矩阵仍延期至 C-DRP-06，不把这些项目表述为已验证。

# Desktop-Up 启动与单实例回呼挂起修复

日期：2026-08-03

状态：`FIX VERIFIED / USER VISUAL CONFIRMATION PENDING`

## 现象与证据

- 用户报告启动 Desktop-Up 后持续“等待响应”，进程卡死；
- Windows `Application` 日志存在真实 `Application Hang` / Event ID `1002`：发生时间 `2026-08-02 23:58:52`，可执行文件为当前工作区 Release，事件进程 ID `0x12218`，即旧 Release PID `74264`；
- `HangType` 为 `Unknown`，没有可用调用栈，因此不能把具体锁循环当作系统转储已经证明的事实。

## 根因定位

新加入的自动暂停后台线程会进入 `apply_runtime_visibility`。旧实现持有 `RuntimeState` 互斥锁时直接调用 `tray.sync`，而托盘菜单属于 Tauri UI 资源；同时主 UI 线程的页面加载、宿主配置或单实例回呼也会请求同一状态锁。该顺序具备以下死锁条件：

1. 后台线程持有状态锁，并等待托盘/UI 操作在主线程执行；
2. 主线程正等待同一状态锁；
3. 两个线程互相等待，Windows 最终记录 Application Hang。

这与 Event 1002 和 C-DHF-05 加入后的时序相符，是当前最强的软件层根因；因缺少 hang dump，结论表述为高置信修复定位，而非调用栈级证明。

## 修复

- 新增 `schedule_runtime_outputs`，统一通过 `AppHandle::run_on_main_thread` 执行托盘同步和运行状态事件派发；
- `apply_runtime_visibility`、手动暂停和前端状态更新均先克隆快照并释放 `RuntimeState`，之后才安排 UI 输出；
- 宿主诊断、交互区域与海报显隐更新同样不再持锁调用托盘 API；
- 新增契约测试，限制 `tray.sync(&snapshot)` 只能出现在主线程调度器内，并要求自动暂停后台路径先 `drop(snapshot)`。

## 验证

```text
pnpm run check
  15 tests passed
  41 Markdown files verified（补写本日志前）

cargo test --locked --manifest-path src-tauri/Cargo.toml
  6 tests passed

cargo check --locked --manifest-path src-tauri/Cargo.toml
  PASS

cargo tauri build --no-bundle
  PASS
```

- 首轮修复 Release PID：`63628`；冷启动后连续采样至约 54 秒，四次均为 `Responding=True`；
- 静置后 CPU 累计值停止增长，工作集稳定在约 `34.3 MB`；
- 再次启动同一可执行文件后仍只有 PID `63628`，单实例回呼后保持 `Responding=True`，没有创建第二个 Desktop-Up 进程；
- 清理无用命令注入参数后完成最终 Release 对齐重建，当前 PID `70600`；启动后 19–27 秒三次采样均为 `Responding=True`，工作集约 `34.3 MB`，CPU 累计值基本不再增长；
- 当前 Release PID `70600` 已留在运行状态，供用户确认海报和主页实际交互。

## 结论与遗留

启动/单实例回呼的锁顺序缺陷已修复，并通过自动化、Release 冷启动和第二实例回呼验证。仍需用户确认当前桌面上不再出现“等待响应”；C-DHF-05 的真实全屏、锁屏、熄屏和跨屏暂停矩阵状态不因此提前完成。

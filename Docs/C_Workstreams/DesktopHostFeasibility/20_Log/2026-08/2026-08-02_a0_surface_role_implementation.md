# A0 运行表面角色代码对齐记录

日期：2026-08-02

状态：`COMPLETE — SURFACE ROLE SLICE`

对应任务：`C-DHF-02` 的默认可见表面切片；WorkerW 挂载仍在该任务后续范围内。

## 目标

- 普通冷启动只显示桌面海报；
- 控制中心保持隐藏，只有托盘命令、托盘双击或第二实例才显式打开；
- 关闭控制中心只隐藏窗口，不结束运行时或隐藏海报。

## 实现

- 在 `src-tauri/tauri.conf.json` 将 `main.visible` 固定为 `false`；
- 保持 `poster.visible` 为 `true`，并继续应用 Windows bottom-most `ToolWindow` 回退配置；
- 保留 `show_main_window` 作为托盘和第二实例的统一显式入口；
- 保留主页 `CloseRequested` 的 `prevent_close + hide` 生命周期；
- 新增 `tests/runtime-surface.test.mjs`，锁定冷启动和显式呼出契约。

## 自动化验证

```text
pnpm run check
  9 tests passed
  37 Markdown files verified

cargo check --locked --manifest-path src-tauri/Cargo.toml
  PASS

cargo tauri build --no-bundle
  PASS
```

## Release 生命周期探针

目标进程：`desktop-up.exe`，PID `78276`（仅用于本次记录）。

| 时点 | 控制中心 | 桌面海报 | 结论 |
| --- | --- | --- | --- |
| 冷启动 | 窗口存在但 `IsWindowVisible=False` | `Desktop-Up Poster=True` | 只有海报默认可见 |
| 启动第二实例 | `Desktop-Up · A0 Demo=True` | `Desktop-Up Poster=True` | 第二实例按需呼出主页 |
| 关闭主页 | `Desktop-Up · A0 Demo=False` | `Desktop-Up Poster=True` | 主页收纳且海报继续运行 |

测试结束后新版 Release 继续运行，并停留在“控制中心隐藏、海报可见”状态。

## 结论与遗留

运行表面角色切片通过，原先“启动即显示主页”的已知偏差已消除。`C-DHF-02` 尚不能整体标记完成，因为 WorkerW 挂载、可观察回退诊断和退出无残留验证仍待实施。

当前深色视觉仅为 A0 功能原型。用户要求的米白清新风已经登记为功能硬门完成后的独立视觉任务，本切片不修改主题样式。

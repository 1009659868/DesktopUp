# V3-01 代码锚点

状态：`CURRENT`

## 工程边界

| 项目 | 职责 | 允许依赖 |
| --- | --- | --- |
| `DesktopUp.Domain` | 启动意图等纯领域值 | BCL |
| `DesktopUp.Application` | 平台无关端口、意图解析 | Domain |
| `DesktopUp.Infrastructure` | 当前用户路径、JSONL 日志、resource ledger | Application、Domain |
| `DesktopUp.Windows` | 当前用户单实例 Mutex 与 named pipe | Application、Domain |
| `DesktopUp.App` | WinUI composition root、托盘、控制中心 | 全部内层项目；WinUI/H.NotifyIcon |

## 关键锚点

- `src/DesktopUp.Windows/SingleInstanceCoordinator.cs`：主实例 Mutex、当前用户 pipe、第二实例 JSON 意图转发；
- `src/DesktopUp.Infrastructure/UserDataPaths.cs`：`%LOCALAPPDATA%\DesktopUp\V3`；
- `src/DesktopUp.Infrastructure/JsonLineLog.cs`：结构化 JSONL；
- `src/DesktopUp.Infrastructure/ResourceLedger.cs`：原子资源快照与幂等 lease；
- `src/DesktopUp.App/App.xaml.cs`：唯一 composition root、意图分流和退出顺序；
- `src/DesktopUp.App/TrayService.cs`：H.NotifyIcon、隐藏 1×1 owner window、双击和右键入口；
- `src/DesktopUp.App/ControlCenterService.cs`：唯一按需控制中心和关闭后释放；
- `src/DesktopUp.App/app.manifest`：asInvoker、PerMonitorV2、UTF-8；
- `tests/DesktopUp.Contracts.Tests/`：V3-01 新合同测试；
- `scripts/build.ps1`、`test.ps1`、`run.ps1`、`smoke-exit.ps1`：统一入口。

V3-01 不包含 PosterWindow、Explorer owner attach、WorkerW、透明输入岛、SQLite 或 Item。这些能力不能从当前骨架的通过结论中推导。

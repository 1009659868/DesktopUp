# DeskBox 来源登记 — V3-01

来源仓库：`https://github.com/Tianyu199509/DeskBox.git`

来源分支：`main`

来源 commit：`64f27e1aa0f16867abe20074f35ea05cd7b12ce7`

许可证：`GPL-3.0-only`

## 本阶段参考范围

| DeskBox 锚点 | DesktopUp 用途 | 处理 |
| --- | --- | --- |
| `src/DeskBox/DeskBox.csproj` | WinUI/.NET/托盘依赖组合 | 只采用版本与平台组合 |
| `src/DeskBox/App.xaml.cs` | 后台常驻、单实例、激活入口 | 采用职责拆分；DesktopUp 重新实现最小 Mutex + 当前用户 named pipe |
| `src/DeskBox/App.Tray.cs` | 隐藏 owner window + H.NotifyIcon 托盘 | 采用构造模式；删除 DeskBox 菜单、品牌、设置与 widget 功能 |
| `src/DeskBox/app.manifest` | asInvoker、PerMonitorV2、UTF-8 | 采用平台声明模式 |

V3-01 没有复制 DeskBox 品牌、图标、文案或大段源文件。DesktopUp 仍以 GPL-3.0-only 作为本探索分支的许可证基线；以后直接移植的每个源文件必须继续追加登记。

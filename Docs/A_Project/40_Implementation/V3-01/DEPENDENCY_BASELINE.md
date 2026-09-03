# V3-01 依赖与工具链冻结

状态：`FROZEN — RELEASE BUILD VERIFIED`

日期：2026-09-03

## 固定组合

| 组件 | 固定版本 | 说明 |
| --- | --- | --- |
| .NET SDK | 10.0.103 | 本机已安装版本；`global.json` 禁止静默跨 feature band |
| TFM | net10.0-windows10.0.22621.0 | 与 DeskBox 路线一致 |
| Windows 最低版本 | 10.0.19044.0 | Windows 10 21H2 |
| 架构 | x64 | V3 首个正式目标；ARM64 不在 V3-01 假实现 |
| Windows App SDK | 2.4.0 | DeskBox 当前依赖组合 |
| Windows SDK BuildTools | 10.0.28000.2270 | DeskBox 当前依赖组合 |
| WinUIEx | 2.9.3 | 后续窗口帮助层；V3-01 只冻结依赖 |
| H.NotifyIcon.WinUI | 2.5.0-beta.1 | 托盘；预发布风险登记，不向 Domain/Application 泄漏 |
| CommunityToolkit.Mvvm | 8.4.2 | 命令与 ViewModel 基础 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | Composition root |
| xUnit / runner | 2.5.3 / 2.5.3 | 合同测试 |
| Microsoft.NET.Test.Sdk | 17.8.0 | 测试宿主 |

## 本机证据

- Visual Studio：`C:\Program Files\Microsoft Visual Studio\18\Community`；
- Windows SDK include：10.0.22621.0、10.0.26100.0；
- DeskBox 的 `global.json` 要求 10.0.303，但本机不存在，因此 DesktopUp 没有照抄该锁定；
- V3-01 不启用 Native AOT。AOT 只在功能冻结后的 V3-10 评估。

## 边界

`DesktopUp.Domain` 与 `DesktopUp.Application` 禁止引用 WinUI、H.NotifyIcon、Windows App SDK 和 `DesktopUp.Windows`。预发布托盘包只能存在于 `DesktopUp.App` 组合层，未来可替换而不修改领域合同。

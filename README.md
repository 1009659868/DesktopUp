# DesktopUp

Windows 桌面内容收纳工具。此 `impl/main` 分支只保留发布相关源码、内置模块、配置、构建及打包入口和许可证。完整开发资料与测试保留在 `impl/poster-redesign-V3`，不从本分支反向合并精简删除。

当前仍是开发预览，不是已经完成全部人工验收的正式版本。基础拖放支持同卷本机文件、文件夹、快捷方式本体；跨卷原生、任意资源管理器拖出目标等尚未开放。拖放会移动原对象，先用自行创建的无价值文件验证。原有“添加内容”仍为非破坏性引用。

## 构建与运行

构建机使用 Windows x64、`global.json` 指定的 .NET SDK 和 Windows 构建依赖。目标预览系统为 Windows 11 x64。

```powershell
& .\scripts\build.ps1 -Configuration Release
& .\scripts\run.ps1 -ShowControlCenter
```

启动脚本会先构建当前源码，避免切换分支后误启动旧输出。关闭控制中心不退出程序；使用托盘菜单退出。运行配置默认位于当前用户的 `%LOCALAPPDATA%\DesktopUp\V3`。

自定义输出目录请用 `scripts/build.ps1 -OutputDirectory`，直接传 WinUI 的 `-o` 路径必须保留末尾分隔符。不要删除 `Modules`、`config` 或 `content-worker` 运行依赖。

## 预览安装包

安装 NSIS 后，运行根目录 `Package-Preview.cmd` 或：

```powershell
& .\scripts\package-preview.ps1
```

参数来自 `packaging/preview/package-settings.json`，可通过脚本参数覆盖版本、默认模板等；默认“基础收纳”。生成的自包含 x64 安装包位于 `out/releases`，允许自选安装路径，使用独立的 Preview 用户配置。保留源指纹、产物清单和 SHA256，输出不会上传 Git。包仍未数字签名，不要关闭系统防护。

`scripts/package-preview.ps1 -CheckOnly` 只检查打包设置，不生成安装包。发布源码整理和构建通过不代表安装、拖放、显示器或长时场景全部验收通过。

## 许可证

本项目使用 GPL-3.0-only，见 [LICENSE](LICENSE)；依赖与参考项目归属见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。分发时保留相应通知、许可证和对应源码。安装器随包包含用户说明及依赖许可证。

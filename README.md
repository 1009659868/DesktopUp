# DesktopUp

Windows 桌面内容收纳工具。此 `impl/main` 分支只保留发布相关源码、内置模块、配置、构建及打包入口和许可证。完整开发资料与测试保留在功能开发线 `impl/poster-redesign-V3` 和开发集成线 `impl/develop`。后续功能分支汇入 `impl/develop`，里程碑完成后再同步本分支；不反向合并发布精简删除。

2026-10-14 增量：图标优先提取原始资源中的最大有效帧，保守回退 Shell 填充画布，不裁正常留白；安装目录页之后提供“创建桌面快捷方式”选项，默认勾选、可取消。图标肉眼复验及安装向导人工往返仍独立记录，不以 Git 同步替代验收。

当前为开发预览。已接入全区域拖入、同/跨卷收纳、空白框选、外部移出同步、单一归属与原生 Shell 拖出。拖放会移动真实文件；先用自行创建的无价值内容体验。普通点击穿透已撤销，日常添加使用拖入，不再显示测试添加向导。

新配置首次启动为零海报、无演示卡片；新增默认“基础收纳”。每次普通启动（含重复启动），零海报时打开控制中心，有海报时仅后台运行；托盘或 `--show-control-center` 可主动打开控制中心。已有配置不会重置。跨海报直接OLE、任意第三方目标、干净电脑/普通权限、多DPI、满盘与长时场景不代表已全面验证。

## 构建与运行

使用 Windows 11 x64 和 64 位 Windows PowerShell 5.1 或 PowerShell 7。脚本会自动准备项目内工具链，不要求系统已安装匹配版本的 .NET SDK 或 NSIS，也不修改系统 PATH。

首次使用需要联网。可以先一次性准备构建、运行和打包所需依赖：

```powershell
& .\scripts\setup.ps1 -ForPackaging
```

也可以直接执行下面的命令；缺少工具时会自动下载。命令可在项目根目录执行，相对输出路径以项目根目录为基准：

```powershell
& .\scripts\build.ps1 -Configuration Release
& .\scripts\run.ps1 -ShowControlCenter
```

启动脚本会先构建当前源码，避免切换分支后误启动旧输出。Windows App SDK 随运行输出部署，.NET 运行时来自项目内 SDK。关闭控制中心不退出程序；使用托盘菜单退出。运行配置默认位于当前用户的 `%LOCALAPPDATA%\DesktopUp\V3`。

默认收纳使用当前用户的应用数据目录：`%LOCALAPPDATA%\DesktopUp\Storage\daily\<配置身份>`；安装预览版使用`preview`通道。通常位于C盘，但遵循系统配置、不硬编码盘符。收纳原文件不放在安装目录；通过“控制中心 → 全局 → 收纳位置”修改和迁移，重装不会覆盖已有位置。

自定义输出目录请用 `scripts/build.ps1 -OutputDirectory`，直接传 WinUI 的 `-o` 路径必须保留末尾分隔符。不要删除 `Modules`、`config` 或 `content-worker` 运行依赖。

### 工具链、换机与离线使用

- 固定版本及下载校验值：`scripts/toolchain.json`；SDK 版本与 `global.json` 一起维护。目前使用 .NET SDK 10.0.112、NSIS 3.12。
- SDK、NSIS、下载归档：`out/toolchains/`。SDK 从微软下载；NSIS 优先从 SourceForge 下载，失败时尝试 MacPorts 缓存；所有归档均校验固定哈希后解压。
- NuGet 包缓存：`out/nuget/packages/`；源定义见 `NuGet.Config`。临时文件、HTTP 缓存与 CLI 状态也留在 `out`。脚本结束后恢复调用进程的工具链环境变量。
- 构建期间统一按 UTF-8 读取外部工具输出，结束后恢复原控制台编码。Windows PowerShell 5.1 不需要手动执行 `chcp` 或修改系统区域设置；打包日志编码随 PowerShell 版本而定：5.1 为带 BOM 的 UTF-16LE，7 为 UTF-8。
- `out` 不进入 Git。普通换机只需拉取源码后运行 `setup.ps1 -ForPackaging`。若要离线换机，应先在联网的 Windows x64 机器完成该命令，再将**同一版源码**、`out/toolchains/` 与 `out/nuget/packages/` 一起复制到新机器；不需要复制 `bin`、`obj` 或用户配置。

已准备缓存后可显式使用离线模式（只使用本地包源，不向 NuGet 请求缺失依赖）：

```powershell
& .\scripts\setup.ps1 -ForPackaging -Offline
& .\scripts\build.ps1 -Configuration Release -Offline
& .\scripts\run.ps1 -ShowControlCenter -Offline
& .\scripts\package-preview.ps1 -Offline
```

`setup.ps1 -CheckOnly` 只显示工具位置及是否存在，不下载或还原包。下载失败或校验不通过会保留诊断文件并停止使用该下载结果；可检查网络/代理后重试。若提示缓存归档错误或工具目录不完整，先将提示的那个文件/目录移走再重试，不需要删除所有缓存。更改 SDK 版本时同时更新 `global.json`、工具链清单的版本、URL 和哈希；更改打包运行时版本见 `packaging/preview/package-settings.json`，然后重新联网准备依赖。

不要用系统 `dotnet` 命令替代上述入口来判断项目工具链是否可用：`global.json` 的 `latestPatch` 不跨 SDK 功能带，因此系统 10.0.301 不会满足项目锁定的 10.0.1xx。[SDK 选择规则](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)

本方案隔离可下载工具及包依赖，仍依赖 Windows 桌面系统。WinUI 界面、Explorer/Shell 拖放与托盘验收需要交互桌面；[Windows 容器不支持桌面 GUI](https://learn.microsoft.com/en-us/virtualization/windowscontainers/quick-start/lift-shift-to-containers)。本分支暂不提供 Docker 构建流程；需要完整隔离时可使用 Windows 虚拟机，并在其中执行同样的脚本。全新 Windows 机器的端到端验收仍需单独完成。

## 预览安装包

运行根目录 `Package-Preview.cmd` 或以下命令，脚本会自动使用项目内 SDK 和 NSIS：

```powershell
& .\scripts\package-preview.ps1
```

参数来自 `packaging/preview/package-settings.json`，可通过脚本参数覆盖版本、默认模板等；默认“基础收纳”。生成的自包含 x64 安装包位于 `out/releases`，允许自选安装路径，使用独立的 Preview 用户配置。保留源指纹、产物清单和 SHA256，输出不会上传 Git。包仍未数字签名，不要关闭系统防护。

安装至当前用户可写的本地目录（支持其他本地盘），可以包含其他文件。安装器保留已有内容；若 `DesktopUp.exe`、`Uninstall.exe`、`PREVIEW-README.txt`、`app` 或 `licenses` 已存在，则停止并要求换目录，不覆盖同名内容。磁盘根、系统/用户配置根、联接路径和过长路径仍不接受。

安装目录页之后可选择“创建桌面快捷方式”（默认勾选）。取消后仍可从开始菜单启动；同名桌面文件不会被覆盖。静默安装可传 `/DESKTOPSHORTCUT=0` 或 `1`，放在最后的 `/D` 参数之前。卸载只移除本次创建且仍指向本安装目录的快捷方式，用户改过目标的入口保留。

安装根目录仅放启动入口 `DesktopUp.exe`、卸载入口 `Uninstall.exe`、预览说明、`app/` 和 `licenses/`。程序 DLL、运行库、语言资源、内置模块和配置集中在 `app/`；保留完整运行依赖，无需逐个管理。构建信息和文件清单位于 `app/preview-build.json`、`app/payload-manifest.json`。

卸载使用安装目录的`Uninstall.exe`，或Windows设置“应用 → 已安装的应用 → DesktopUp Preview → 卸载”；调用同一登记入口。先从托盘退出。卸载仅删除安装清单文件，保留配置、收纳内容和未知用户文件。各预览版本共享`%LOCALAPPDATA%\DesktopUp\Preview`配置；重装保留配置，不等于重置，不会删除原有海报。

`scripts/package-preview.ps1 -CheckOnly` 只检查打包设置并显示工具是否存在，不下载工具或生成安装包。发布源码整理和构建通过不代表安装、拖放、显示器或长时场景全部验收通过。

2026-10-13 本机验证：Windows PowerShell 5.1 离线 Release 打包通过；非空中文/空格目录安装、全量文件哈希、同名冲突/联接目录拒绝、空白首次及再次启动、有海报后台启动、重复启动与主动打开、运行中禁止卸载、退出后卸载并保留未知文件均通过。实际程序以普通权限加载安装目录 `app/` 内的运行库。原 Preview 配置已恢复并核对文件哈希；另一台干净电脑和完整拖放回归尚未在此轮验证。

2026-10-14 发布增量验证：精简分支 Release 构建与完整 `20261014.2` 打包通过；中文含空格自选目录安装、813 项载荷哈希、桌面快捷方式创建/卸载清理、Windows 卸载注册和未知文件保留通过。本轮未启动安装后的 App，未人工点击向导；原图标肉眼复验及跨机等未验项继续保留。安装产物只留在本地 `out`，不随源码推送。

## 许可证

本项目使用 GPL-3.0-only，见 [LICENSE](LICENSE)；依赖与参考项目归属见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。分发时保留相应通知、许可证和对应源码。安装器随包包含用户说明及依赖许可证。

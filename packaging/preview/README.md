# 一键预览打包

构建电脑使用Windows 11 x64与Windows PowerShell 5.1 / PowerShell 7。脚本自动准备项目锁定的.NET SDK 10.0.112、NuGet包与NSIS 3.12；版本和下载校验值见`global.json`及`scripts/toolchain.json`。缓存分别位于`out/toolchains/`、`out/nuget/packages/`，不替换系统SDK、不永久修改PATH或执行策略。接收安装器的电脑不需要开发环境；打包不自动安装/启动成品。

## 最简单的使用方式

在项目根双击 `Package-Preview.cmd`。默认读取本目录的 `package-settings.json`，自动生成唯一的日期＋当天秒数版本号，并输出到 `out/releases/preview-<编号>/`。已有输出目录不会覆盖；同一秒重复运行应等待一秒或手动指定ReleaseId。

主要交付物是 `DesktopUp-Preview-<编号>-win-x64-Setup.exe` 和 `SHA256SUMS.txt`。其余payload/build/evidence是本机构建/追溯资料，不必发给测试电脑。安装向导中可自选安装目录；安装与配置目录相互独立。

选择安装目录后，“附加选项”页提供“创建桌面快捷方式”（默认勾选），可取消；返回上一页再前进会保留选择。快捷方式带版本号，指向安装根启动器；取消勾选不影响开始菜单入口。不覆盖已有同名桌面文件，卸载仅清理本次创建且仍指向本安装位置的快捷方式。

脚本先按自包含发布参数完整重建，再发布同一输出，避免普通构建后复用缺少WinUI主题资源的旧PRI索引。保留`evidence/rebuild.log`，不要为了提速删除这一步。项目及依赖许可证随包位于`licenses`。

## 按需修改

修改 `package-settings.json`：

| 字段 | 含义 |
| --- | --- |
| version / fileVersion | 应用版本 / 四段Windows文件版本 |
| runtimeVersion | 随包.NET运行时版本；调整前核对官方安全补丁，构建时严格检查实际发布版本 |
| defaultTemplate | 当前为`basic-box`基础收纳；也支持`lamp`深夜书桌、`night-ops`夜航 |
| backgroundOpacityPercent | 当前`null`；使用深夜书桌时可填0～100（0全透明/100不透明），仅影响背景；其他两种模板必须填`null` |

命令行覆盖不修改设置文件：

```powershell
# 首次联网准备；构建与打包入口也会自动调用工具链准备。
./scripts/setup.ps1 -ForPackaging

# 只检查配置、依赖入口及预计输出，不构建/安装。
./scripts/package-preview.ps1 -CheckOnly

# 常规一键构建；无需填写机器专属路径。
./scripts/package-preview.ps1

# 工具和依赖已完整缓存后离线打包；缺失项会明确报错。
./scripts/package-preview.ps1 -Offline

# 本次改用深夜书桌并指定背景不透明度，不改变默认配置文件。
./scripts/package-preview.ps1 -DefaultTemplate lamp -BackgroundOpacityPercent 40

# 切换无灯光模板时显式清空其不支持的透明度。
./scripts/package-preview.ps1 -DefaultTemplate basic-box -BackgroundOpacityPercent $null

# 指定另一份设置文件、编译器或全新的输出目录（输出必须在项目out下）。
./scripts/package-preview.ps1 -SettingsPath ./packaging/preview/package-settings.json `
  -MakeNsis 'C:\Program Files (x86)\NSIS\makensis.exe' `
  -OutputRoot ./out/preview-custom
```

安装根目录只有`DesktopUp.exe`启动器、`Uninstall.exe`、`PREVIEW-README.txt`、`app/`和`licenses/`。WinUI程序、DLL、本地化资源、模块与配置完整保留在`app/`，不按名称猜测并删除运行依赖。启动器转发参数至`app/DesktopUp.exe`；构建日志临时使用UTF-8输出，结束恢复原编码。

生成的 `app/config/preview-defaults.json` 是启动时读取的声明式默认值，不执行脚本。新配置零海报、无演示内容；每次普通启动（含第二实例转发）均按当前目录判断：无海报显示控制中心，有海报后台启动；`--show-control-center`始终显示中心。托盘/控制中心新增海报共用默认入口。复制、导入、已有配置和模板重置不被它改写。仅Preview通道读取它。缺失/非法默认文件会记录警告并退回原新建行为，不阻止启动或覆盖保存数据。

## 安装/卸载与验证边界

- 安装路径支持目录页及静默`/D=<完整路径>`（放最后，不加引号）；允许本地、当前用户可写且含无关文件的目录。遇到`DesktopUp.exe`、`Uninstall.exe`、`PREVIEW-README.txt`、`app`或`licenses`已有同名文件/目录则拒绝；仍拒绝已有同版注册记录、配置/系统根、联接/符号链接及超长路径。
- 静默安装同样默认创建桌面快捷方式，可在`/D`之前传`/DESKTOPSHORTCUT=0`关闭或`/DESKTOPSHORTCUT=1`开启；其他值在安装前拒绝。选项属于安装时用户选择，不是打包模板配置。仅完整开发分支（`impl/poster-redesign-V3` / `impl/develop`）提供安装器选项隔离回归：`./tests/Packaging/Test-DesktopShortcut.ps1`，用惰性假载荷验证，不启动应用或触碰配置/收纳数据；不是成品安装验收，精简`impl/main`不携带此测试入口。
- 卸载同时核对HKCU安装位置、`app/preview-install.ini`的版本/路径/通道；检查子目录未被替换为联接，再删除编译时的精确清单。运行中拒绝卸载，不强杀。未知文件保留，Preview配置始终保留。
- 开始菜单及安装完成页使用普通启动规则；安装目录生成`Uninstall.exe`，Windows“已安装的应用”调用同一卸载器。默认收纳使用`%LOCALAPPDATA%\DesktopUp\Storage\preview\<配置身份>`，不放安装目录，不随卸载删除；普通源码运行使用`daily`通道。既有收纳位置不改写，通过“全局 → 收纳位置”修改/迁移。
- `evidence/resolved-settings.json`、源码指纹、原始发布/NSIS日志、payload清单和Setup SHA256用于绑定；`Verified:false`只是尚未执行运行验收，编译成功不等于验收通过。
- 成品默认未签名；不要为了测试关闭系统防护。跨机/普通权限与人工向导须另验。

历史20260928.1固定路径安装器不再是当前交付入口，记录和配置保留。默认模板为基础收纳；发布验证与未验边界以开发分支的交接记录为准。

# 一键预览打包

构建电脑需要项目锁定的.NET SDK、Windows构建依赖、可恢复的NuGet包以及NSIS。接收安装器的Windows 11 x64电脑不需要开发环境。脚本支持Windows PowerShell 5.1 / PowerShell 7，不自动安装工具、不更改执行策略、不提交Git、不自动安装/启动成品。

## 最简单的使用方式

在项目根双击 `Package-Preview.cmd`。默认读取本目录的 `package-settings.json`，自动生成唯一的日期＋当天秒数版本号，并输出到 `out/releases/preview-<编号>/`。已有输出目录不会覆盖；同一秒重复运行应等待一秒或手动指定ReleaseId。

主要交付物是 `DesktopUp-Preview-<编号>-win-x64-Setup.exe` 和 `SHA256SUMS.txt`。其余payload/build/evidence是本机构建/追溯资料，不必发给测试电脑。安装向导中可自选安装目录；安装与配置目录相互独立。

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
# 只检查配置、依赖入口及预计输出，不构建/安装。
./scripts/package-preview.ps1 -CheckOnly

# 常规一键构建；无需填写机器专属路径。
./scripts/package-preview.ps1

# 本次改用深夜书桌并指定背景不透明度，不改变默认配置文件。
./scripts/package-preview.ps1 -DefaultTemplate lamp -BackgroundOpacityPercent 40

# 切换无灯光模板时显式清空其不支持的透明度。
./scripts/package-preview.ps1 -DefaultTemplate basic-box -BackgroundOpacityPercent $null

# 指定另一份设置文件、编译器或全新的输出目录（输出必须在项目out下）。
./scripts/package-preview.ps1 -SettingsPath ./packaging/preview/package-settings.json `
  -MakeNsis 'C:\Program Files (x86)\NSIS\makensis.exe' `
  -OutputRoot ./out/preview-custom
```

生成的 `config/preview-defaults.json` 是启动时读取的声明式默认值，不执行脚本。初始海报与托盘/控制中心新增海报共用同一入口；复制、导入、已有配置和模板重置不被它改写。仅Preview通道读取它。缺失/非法默认文件会记录警告并退回原新建行为，不阻止启动或覆盖保存数据。

## 安装/卸载与验证边界

- 安装路径支持目录页及静默`/D=<完整路径>`（放最后，不加引号）；选择本地、当前用户可写的新目录/空目录。拒绝非空目标、已有同版注册记录、配置/系统根、联接/符号链接及超长路径。
- 卸载同时核对HKCU安装位置、`preview-install.ini`的版本/路径/通道；检查子目录未被替换为联接，再删除编译时的精确清单。运行中拒绝卸载，不强杀。非空未知文件保留，Preview配置始终保留。
- `evidence/resolved-settings.json`、源码指纹、原始发布/NSIS日志、payload清单和Setup SHA256用于绑定；`Verified:false`只是尚未执行运行验收，编译成功不等于验收通过。
- 成品默认未签名；不要为了测试关闭系统防护。跨机/普通权限与人工向导须另验。

历史20260928.1固定路径安装器已按用户要求撤回到回收站，不再是当前下载入口；记录和配置保留。2026-10-09默认模板统一为基础收纳，本轮未重新生成安装器；当前主线见[V3-05记录](../../Docs/A_Project/40_Implementation/V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md)。

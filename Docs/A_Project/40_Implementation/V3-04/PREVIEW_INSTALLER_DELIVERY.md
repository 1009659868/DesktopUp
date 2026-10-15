# Windows 11 x64 独立预览安装包

## 2026-10-15 develop里程碑与外观UI发布同步

用户确认外观UI集中1～3项均通过，按授权保存功能分支 `0a75494`、非快进合入 `impl/develop@d66265f`，再合并到精简 `impl/main@f80765e`；三条分支已分别推送。最终工作区返回develop，本节和交接作为后续文档提交同步；不将发布精简反向合入开发。

本轮发布一并纳入此前已接受的单卡图标更换/恢复、增量呈现/外部悬停让位、真实文件操作/Win菜单/目录映射及统一外观设置。修改/删除冲突按文件职责处理：开发资料、测试和验收探针仅在发布线移除；UI、文件业务、模块与安全保护保留。新增Shell准备/测试选择入口与行内命名测试回调也已剔除。发布线384个跟踪文件；381项打包源绑定复核通过，334个产品文件与develop逐字节Git内容一致，32个差异文件为发布专用的探针/诊断裁剪与调用名调整。未新增需要反向回写的产品修复。

### 成品验证

证据根 `out/git-sync-20261015`，不上传Git。`source-parity.json`记录来源及精简差异；`verify-install.ps1`、`profile-check.py`、`install-result.json`记录实际安装和运行检查。

| 范围 | 本轮结果 |
| --- | --- |
| 精简线构建 | `scripts/build.ps1 -Configuration Release -Offline -OutputDirectory out/git-sync-20261015/release-app`，0警告/错误 |
| 自包含打包 | `scripts/package-preview.ps1 -Offline -ReleaseId 20261015.1 -OutputRoot out/git-sync-20261015/preview-20261015.1`；重建/发布、启动器和NSIS `/WX` 通过，814项payload，无测试载荷/用户数据/PDB |
| 安装及入口 | 独立中文含空格目录，813项安装文件哈希一致；桌面快捷方式开启/关闭均遵从，开始菜单保留，HKCU64安装位置及两种卸载命令正确 |
| 首次体验 | 原Preview临时保全后，实际安装程序首启/重启都是零海报、无演示卡片，打开控制中心、默认基础收纳；第二实例请求复用已有实例 |
| 已验配置一致性 | 只复制人工候选的两张空海报数据库（内容/成员/绑定均0），安装版启动/重启成功；只读SQLite逐项比较标题、模板、完整布局参数、内容布局、主题等配置保持一致 |
| 运行与退出 | 最终四进程PID7296/35012/49124/29544均正常退出、资源0、无error；.NET和WinUI均从实际安装app目录加载 |
| 卸载与保全 | 调用Windows登记的同一Uninstall.exe，清除程序/自身/登记/快捷方式；保留未知文件。原Preview和日常V3逐文件哈希未变，收纳文件不动 |

首轮四次启动也通过，但验证脚本误把NSIS引导进程退出等同卸载完成，提前报“Registration/shortcuts left behind”；随后读取确认清理已完成。保留 `verify-install-first-wait-diagnostic.ps1`及首轮配置证据；仅修验证脚本等待真实目标全部消失，最终上述结果通过，未改产品/安装器来绕过检查。四份生成的验证配置已从AppData迁存至本证据根的 `profile-evidence/`，原Preview恢复原位；临时安装已卸载，未知文件仍保留。

成品：`out/git-sync-20261015/preview-20261015.1/DesktopUp-Preview-20261015.1-win-x64-Setup.exe`，**67,295,034字节**，SHA256 **`621445A6B50EFE44E3962354743DDBDE26FA10654ACE3006A260872D286EADE9`**。App DLL SHA256 `F56764C263D8426DA7648F25AFB937FA2385150485EAA069E77E1F7E7A3D3B79`。按先验证再提交发布合并的流程，包内SourceCommit为合并前HEAD `484fc02`、IncludesUncommittedChanges=true；确切验证源码以381项source-manifest及发布提交 `f80765e` 对照，不能将旧HEAD误当完整来源。随后仅补README验证说明，不改变源绑定范围。

边界：此为本机高完整性自动验证，没有Windows界面自动化入口，未人工点击向导/设置卸载页，也未在此包重做全部鼠标文件操作；开发人工结论不改写为成品全矩阵人工PASS。普通权限、干净另一台电脑、多DPI、真实满盘、联合长时及第三方扩展仍按原记录携带，安装包未签名，打包元数据整体Verified=false保持。默认存储仍为 `%LOCALAPPDATA%\DesktopUp\Storage\preview\<配置身份>`，不放安装目录，不随卸载删除。

## 2026-10-14 开发集成线与发布同步

本轮按用户授权保存开发产品修订 `f6a8199`，合入精简发布线 `484fc02`，再从含交接的最终 V3 提交派生 `impl/develop`。集成线完整保留开发文档/测试，发布线继续只有产品、模块、配置、构建/启动/打包和许可证。发布合并的修改/删除冲突均为已退役资料/测试及 README，逐项处理；ContentWorker 与安装器产品代码和开发源一致，没有将发布精简反向带入开发线。最终推送结果以现场三条远端 refs 为准。

验证只覆盖本次增量及发布边界，未机械重复拖放矩阵：

- 沿用当前产品源的 3320/3320 合同、实际 iFt 像素取证和安装器五组隔离回归；本轮不把这些写成新增人工 PASS。
- 精简线 `scripts/build.ps1 -Configuration Release -OutputDirectory out/git-sync-20261014/release-app`：0 警告/错误；没有 Docs/tests 工程依赖，354 个发布跟踪文件。
- `scripts/package-preview.ps1 -ReleaseId 20261014.2 -OutputRoot out/git-sync-20261014/preview-20261014.2`：自包含重建/发布、启动器和安装器 `/WX` 编译、源指纹不变检查通过。载荷 814 项（813 项清单哈希，清单自身除外），无测试载荷/用户数据/PDB；实际产品源码与最终发布提交一致。
- 完整安装包在独立中文含空格目录静默安装；813 项安装后哈希、桌面入口指向根启动器、Windows 卸载注册及 `Uninstall.exe` 验证通过。卸载正常清除程序、登记和本次快捷方式，保留未知文件 `user-kept.txt`。脚本与结果在 `out/git-sync-20261014/verify-install.ps1`、`install-result.json`；本次测试安装已卸载，可重新从保留的安装包恢复，不涉及现有用户配置或收纳文件。

成品：`out/git-sync-20261014/preview-20261014.2/DesktopUp-Preview-20261014.2-win-x64-Setup.exe`，67,256,663 字节，SHA256 `1F667FE985A704809BB89BB6FBFADB3CC1F8D8064058E9E7944307F2DDEFE931`。源/产物指纹和日志在同目录 `evidence/`。遵循先验证再合并提交的流程，包内 `SourceCommit` 为合并前 HEAD `fde1860` 且 `IncludesUncommittedChanges=true`；确切已验证产品源以 `source-manifest.json` 与发布提交 `484fc02` 对照，不能把旧 HEAD 误认为全部源版本。

边界：本轮未启动安装后的 App，未人工点击向导往返/Windows 设置页面，iFt 肉眼复验仍待用户反馈；跨机/普通权限/多 DPI/满盘/长时不新增结论。`package.json` 的整体 `Verified=false` 保持，不能因编译和定向安装成功就宣称全部验收完成。安装产物/证据不入 Git；下一主线仍 05-05-V2。

## 2026-10-14 可选桌面快捷方式

用户要求安装过程中可选择是否添加桌面快捷方式。本轮修改`packaging/preview/installer.nsi`及随包/打包说明，保留已有未提交图标修复；不改应用、安装位置、数据目录、开始菜单和开机启动规则，不进行Git同步。

- 安装顺序：欢迎 → 安装目录 → **附加选项** → 安装 → 完成。“创建桌面快捷方式”默认勾选，可取消；点击时与离页时都保存选择，返回目录页再前进不重置。页面使用NSIS自带[nsDialogs](https://nsis.sourceforge.io/Docs/nsDialogs/Readme.html)，不增加外部插件。
- 入口命名`DesktopUp Preview <版本>.lnk`，位于当前用户桌面，指向安装根的`DesktopUp.exe`启动器。未勾选仍有开始菜单入口；静默安装使用同一默认值，`/DESKTOPSHORTCUT=0|1`可覆盖（放在最后的`/D`之前），非法值安装前拒绝17。
- 先在安装器临时目录生成快捷方式，再用禁止覆盖的CopyFile创建桌面入口；同名文件或桌面不可写时跳过该可选入口、保留开始菜单并记录安装详情，不覆盖既有用户文件。
- 安装标识只登记实际成功创建的快捷方式。卸载读取链接保存的目标、不Resolve/搜索或修复；仅当本次创建且仍指向当前安装根时删除。未创建、同名碰撞、已改指向其他程序、无法验证归属的文件均保留。原精确清单、配置/收纳数据保留和目录保护不变。

验证：`tests/Packaging/Test-DesktopShortcut.ps1`编译真实安装脚本，用不执行的惰性载荷、独立版本`20991014.112516`和`out/installer-shortcut/check01`中的中文含空格路径运行安装/卸载。NSIS3.12 `/WX`零警告通过，**默认创建、关闭选项、同名不覆盖/不认领、改目标后卸载保留、非法选项拒绝**五组通过；原卸载安全合同定点1/1通过（`installer-safety.trx`），脚本语法通过。结果在`results.json`、`compile.log`。测试安装注册项、开始菜单和本次创建的桌面入口已清理，惰性载荷/编译证据留在out；没有操作用户图标或配置，运行中图标修订版PID33400未停止。

这是安装器定向验证，不是完整成品的发布/跨机或人工向导认证；本轮**没有重新生成可分发的完整安装包**。下次执行一键打包即包含该选项，已有Setup.exe不会自动更新。UI页面往返的人工点击仍待新完整包验收；未重跑无关App全套/拖放矩阵，主线仍05-05-V2，前轮iFt肉眼复验仍待用户反馈。

## 2026-10-14 便携工具链与安装体验同步

本节取代下方历史记录中关于系统SDK、首次自动海报、普通启动强制显示中心、仅空目录安装和根目录平铺依赖的描述。本轮公共修复先在`impl/main`形成源码提交`e7943af4f9caebfabaeb74859e80d93f31d92d8e`，选择性回写开发线，保留开发探针/测试/资料，再按Git策略合并开发来源到发布线；远端最终提交以Git refs为准。

- 项目本地SDK 10.0.112 / NSIS 3.12由`scripts/toolchain.json`锁定并校验下载，NuGet源由`NuGet.Config`声明。构建/运行/打包/开发测试入口共用工具链；PS5.1路径兼容、UTF-8原生命令输出及结束恢复编码已接入。第一次需联网，缓存完整后支持`-Offline`。
- 初始化持久化空目录，不自动创建默认海报；显式测试种子仍支持，已有目录不重置。普通首启/重复启动都根据当前海报数决定是否显示中心；显式显示与退出意图保持。新建默认基础收纳、无演示卡片。
- 安装根只保留启动器、卸载器、说明、`app/`、`licenses/`；依赖和元数据在`app/`。根启动器转发参数，真实WinUI程序保持与资源相邻。
- 允许含无关文件的安装目标；五个应用专属根名称冲突会拒绝。保留系统/配置根、联接、归属、运行中和精确清单保护；卸载不删除未知文件、配置或收纳数据。

### 已完成验证与未验边界

以下是清理前已执行的本机验证记录，不是本次同步重新运行的结果：

| 范围 | 结果 |
| --- | --- |
| 工具链及中文 | SDK下载/哈希/解压；PS5.1离线Release构建与打包通过，0警告/错误；CP936启动的中文输出正常并恢复原编码，PS7检查通过 |
| 状态与启动合同 | 隔离状态检查23项通过：空初始化/不补种、基础收纳、保留已有内容、删除最后一张保持空、意图与目录组合 |
| 最终安装器 | `20261013.90006`，67,238,550字节；814项payload，813项清单哈希（清单自身除外） |
| 安装保护 | 中文含空格、已有无关文件目录安装成功；五个保留名称冲突拒绝16，联接目标拒绝14 |
| 原生普通权限 | 已安装`app/`加载运行库；全新/重复零海报均显示中心；有海报普通启动与第二实例保持后台，显式显示成功；退出资源0 |
| 卸载 | 运行中拒绝21；退出后清除程序、登记及快捷方式，根/嵌套/`app/`的三份无关文件哈希保留；原Preview配置恢复且哈希一致 |

安装器SHA256：`DEDAB0243949832548CFFED4F12583C5B05312312B6DD5DB70B2812D922B16FA`；App SHA256：`185111948D64CD64A5D0C0BCF66461D7F5ED6A90E3CF132BB46A29B3D5D2F59E`。早期根apphost直接绑定`app/`方案发生WinUI激活失败，已由最终原生启动器替换；目录联接隔离引入的写入失败已改普通目录验证，不记为产品通过证据。

2026-10-14用户要求清除构建历史、release、`out`、工具链/依赖缓存及中间产物，并明确同意删除五张历史实验截图；清理已执行。**上述候选与原始本机日志现已删除，哈希仅作历史追溯，旧文档中的out路径不可再作为可访问证据。** 本次同步六个PowerShell入口语法检查、真实启动策略源码内存编译的15项检查与差异检查通过；CheckOnly确认本地工具链尚未恢复，没有重新生成产物。开发线新增/调整的完整xUnit合同套件未重跑，下方3304 PASS属于此前版本。后续从源码重新构建会下载工具及依赖；跨机、多DPI、满盘、长时与完整拖放矩阵未补验。

## 2026-10-13 发布同步

状态：本机构建、首启及安装/卸载验证完成，按用户授权同步开发线和`impl/main`。远端写入排在验证之后，最后返回`impl/poster-redesign-V3`；不从开发线删除资料和测试，不将本轮交付升级为正式发布认证。

- `scripts/run.ps1`每次构建当前源码至`out/run/app`，避免切分支后复用旧exe；打包源清单不再强依赖`tests`目录。
- 新配置首次直接启动打开控制中心，零海报、无DEMO卡片。开始菜单和安装完成页也显式打开中心；默认新海报为基础收纳。既有配置、复制、导入不重置。
- 安装仍支持自选当前用户可写的新/空目录；`Uninstall.exe`与HKCU64的Windows卸载项使用同一入口。保留现有精确文件清单、运行中拒绝、目录归属/联接保护；不递归删除收纳目录。
- 默认收纳采用用户允许的应用数据回退：`%LOCALAPPDATA%\DesktopUp\Storage\<preview|daily>\<配置身份>`。不硬编码C盘，也不使用安装目录：目录权限及卸载不能危及用户原文件。配置与收纳分离；已有选定位置不覆盖，变更/迁移仍从“全局 → 收纳位置”执行。
- 安装文案、随包说明、metadata不再声称DEMO/同卷限定/拖出额外确认。发布README单独同步，保留实际未验边界。

验证证据统一放`out/release-sync-20261013`，不上传Git。初轮合同3303通过/1失败：卸载安全测试错误地禁止在任何文案中出现Storage路径；已改为检查实际Delete/RMDir语句，保留禁止递归删除和禁止用户数据删除守卫。

### 分支与公共修复

- 开发检查点`5f9616d`保留全部D4-N、05-14及此前未提交资料；发布合并`90a6d8b`（父节点为原发布线与开发检查点），发布修复`ede03da`。远端提交以实际Git refs复核。
- 发布线只保留产品源码、Modules、config、packaging、三个构建/运行/打包脚本及根构建声明/README/许可证；撤测试友元、探针分发和验收夹具，保留产品账本、安全校验、必要运行诊断。发布裁剪不反向合并。
- 公共修复已回写开发线：打包前按一致的自包含参数完整重建，再`publish --no-build`；随包加入项目LICENSE与THIRD_PARTY_NOTICES。首启中心、运行入口、安装文案和收纳路径规则两线一致。

### 失败定位与最终验证

首包`20261013.1`安装成功但首启失败，不交付。原生异常：找不到`ms-appx:///Microsoft.UI.Xaml/Themes/themeresources.xaml`。普通构建后自包含发布复用旧PRI索引（约80KB，无WinUI主题）；补复制XBF/改PRI别名无效。按发布参数完整重建后索引约2.25MB，成功启动。证据`startup-details.txt`、`installed-pri.xml`、`winui-pri.xml`与失败输出保留。

| 项目 | 本轮结果 |
| --- | --- |
| 开发最终合同 | 3304 PASS、0失败/跳过，`dev-final/contracts.trx` |
| 公共打包修复回写后 | CheckOnly通过，脚本与发布线零差异；默认存储/卸载保护定点48 PASS，`final-safety/packaging-safety.trx`，不重复全量矩阵 |
| 开发Release/隔离运行 | 0警告/错误；Created/schema21、零海报、中心打开、退出资源0 |
| 精简源码构建与打包 | 从`ede03da`全新导出`final-source`，无测试/旧obj；先普通build，再脚本打包，均0警告/错误 |
| 最终安装器 | 20261013.2，67,214,700字节；813项payload（安装核对812项，不含清单自身） |
| 自定义路径 | 安装至隔离目录`Installed Preview 2`，退出0、文件hash匹配；重复安装拒绝（13） |
| 全新首启/重启 | PID5248/40628，Created→Ready、零海报、默认basic-box、中心显示/投影正常；.NET/WinUI均从安装目录加载，退出资源0 |
| 卸载集成 | HKCU64的UninstallString/QuietUninstallString、安装位置及快捷方式正确；调用登记Uninstall.exe /S后程序/卸载器/快捷方式/注册项移除 |
| 保护与保留 | 运行时卸载未删除活跃安装；退出后卸载保留Preview配置/自建未知文件，日常V3配置hash不变 |

最终安装器：`out/release-sync-20261013/final-source/out/releases/preview-20261013.2/DesktopUp-Preview-20261013.2-win-x64-Setup.exe`。SHA256：`D590E6940323155612D932BD285318E8BEB577603056A339EB8BC7687FD677BD`；App SHA256：`1867A09DA4BD5390BA58D4A9B2FAEE5DC8A48745DE2AFFC475F87FD97CD61DF1`。

具体断言见`install-verification.json`及`verify-install.ps1`；源码指纹、重建/发布/NSIS日志在最终包的`evidence`。打包器生成的`Verified:false`只是生成时未运行验收，不能自动改为全范围认证；以后续独立报告的限定结果为准。

本轮为高完整性自动验证。桌面自动化不可用，未点击Windows设置向导，验证的是其登记的同一卸载程序和静默流程；普通权限、全新另一电脑、人工目录页未验。没有移动用户原文件；默认收纳/卸载不删除存储由合同与代码校验覆盖，不把配置保留测试扩大为真实搬移验证。预览未签名，旧BUG/长时/多DPI/满盘等携带项不关闭。

下方为历史交付，不覆盖本节。

最新更新：2026-10-09，用户要求默认模板改为**基础收纳**。普通首次创建/新增与Preview打包配置现已统一为`basic-box`，不携带灯光透明度覆盖；已有海报、复制和导入不改写。本轮仅更新默认值及说明，未生成新安装器。验证及当前主线见[V3-05§10](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md#10-默认基础收纳2026-10-09)。

以下为2026-09-28可配置打包流程的历史交付与证据，当时“深夜书桌25%”和R6进度不再代表当前默认值/主线；旧包撤回记录保留。未提交、推送或改分支。

## 0. 历史：旧包撤回／可选安装路径／深夜书桌25%默认值

用户明确确认“背景不透明度25%（能明显透出桌面）”，不是透明度25%。仅背景使用25%，文字与图标不整体淡化。只改变Preview首次创建与后续新增海报的默认值；已有配置、复制、导入和模板重置不被覆盖，普通构建保持原行为。

- 一键入口：[Package-Preview.cmd](../../../../Package-Preview.cmd)，支持PS5.1/7，不改执行策略、不自动安装/启动、不自动Git操作。
- 可调配置：[package-settings.json](../../../../packaging/preview/package-settings.json)：模板默认lamp、背景不透明度25；应用版本、文件版本和自包含.NET版本集中管理，支持命令行覆盖。
- [使用说明与参数示例](../../../../packaging/preview/README.md)；[主脚本](../../../../scripts/package-preview.ps1)提供CheckOnly、新out目录、485源码/测试/脚本等指纹绑定、许可证/产物清单和Setup SHA256。
- 安装向导新增目录页，静默/D亦有效；默认仍为当前用户版本化目录。只接受专用新目录/空目录，不覆盖旧文件，配置仍在独立Preview目录。卸载核对注册位置＋安装标识，预检目录链接，按精确清单删除，未知文件和配置保留。
- [原正式包及试装包撤回记录](../../../../out/preview-package-checks/withdrawn-installers.json)：均移入回收站，可恢复；旧payload/源码/日志/配置保留。此次验证用20260928.90/91安装器也已[移入回收站](../../../../out/preview-script-checks/withdrawn-validation-installers.json)，不是新发布下载入口。

本轮验证绑定20260928.91，App SHA256为874F602034C3363E9EAEBCAD84CD19F9C51CA7941DFFD89B51C2836C52A8C1DC；[485项源码清单](../../../../out/preview-script-checks/package-r2/evidence/source-manifest.json)复核零漂移。Setup历史SHA为648CDD4E1149258BB8A002706BB7D627B40FC23174F16C5410BD8BA3F0BAEEE2，删除前记录保留，不表示现有可分发文件。

| 验证 | 结果 / 证据 |
| --- | --- |
| 纯合同 | [2175通过，0失败/跳过](../../../../out/preview-script-checks/tests/all-contracts.trx)，含17项新增默认值/格式/边界验证 |
| 打包兼容 | Windows PowerShell5.1完整自包含发布＋NSIS警告即错误通过；普通非Preview构建0警告/错误；[CMD入口CheckOnly](../../../../out/preview-script-checks/cmd-check.json)与[PS7覆盖/非法值拒绝](../../../../out/preview-script-checks/parameter-checks.json)通过 |
| 自选路径 | [中文＋空格新目录安装exit0](../../../../out/preview-script-checks/custom-install.json)，612清单文件hash一致（另含manifest自身，共613文件）；非空目录拒绝exit12、哨兵不变，同版本另一位置拒绝exit13 |
| 首次创建 | [安装目录实际运行及SQLite只读读回](../../../../out/preview-script-checks/fresh-run.json)：lamp/opacity25；exit0/资源0；运行中卸载拒绝exit21 |
| 新增与重启 | [实际托盘/中心命令12场景](../../../../out/preview-script-checks/tray-run.json)通过，隐藏中新增仍为lamp25；[同库重启](../../../../out/preview-script-checks/restart-run.json)保留已修改的night-ops与其他海报参数，exit0/资源0 |
| 卸载与数据隔离 | [缺标识拒绝20、子目录联接拒绝23、正常卸载0](../../../../out/preview-script-checks/uninstall.json)；未知哨兵保留，验证后只清理哨兵与空安装叶目录；日常V3及已有Preview共5文件hash不变，独立测试库保留 |

首次验证发现NSIS GetFullPathName对未创建目录返回空，导致合法路径被守卫拒绝；改为Win32 GetFullPathNameW仅作路径规范化后，在全新构建复验通过。失败稿及诊断记录保留，不用绕过守卫或提前创建目标目录来掩盖问题。

上述为本机提升权限下的自动/托管验证；不是人工点击向导、普通权限、另一台电脑或原生鼠标认证。未签名、DEMO、R6余项边界不变。以下§1～5保留第一次固定路径安装包的历史证据，不能当作当前行为或下载入口。

## 1. 历史交付文件（已撤回）

- 原安装器：DesktopUp-Preview-20260928.1-win-x64-Setup.exe，67,252,469 bytes，约64.14 MiB；已移入回收站，不再提供下载链接。
- SHA256：`9DD8680677BC76B663EF26B85161254DF2A80E11CCF9A5B3208FD22B0E55739E`；[校验文件](../../../../out/preview-installer-20260928.1-final/SHA256SUMS.txt)。哈希用于核对传输完整性，不代替发布者数字签名。
- 当时说明随包安装；当前[用户说明](../../../../packaging/preview/PREVIEW-README.txt)已更新为自选路径/可配置默认值。以后重新打包可复制新安装器及其对应SHA256SUMS，勿沿用旧哈希。
- 已安装/回归App SHA256：`A001F62BD0DBA750D2905D5048634380F8FF9BE825C3529D4C837D55CCFD0F21`。
- 构建基于`impl/poster-redesign-V3@98e0204`加当时未提交的R6与打包修改；[328项源码/配置/脚本/打包文件指纹](../../../../out/preview-installer-20260928.1-final/evidence/source-manifest.json)绑定实际工作树，而非假称仅由HEAD构建。该清单不包含Docs/tests，2158合同证据单列如下。
- [原始打包记录](../../../../out/preview-installer-20260928.1-final/evidence/package.json)中的`Verified:false`表示打包器本身不做验收；后续证据在第3节，不能用编译成功替代安装/运行测试。

## 2. 安装与隔离合同

| 项目 | 本包行为 |
| --- | --- |
| 应用/CPU | DesktopUp Preview；应用为win-x64，NSIS引导器为x86 Unicode，仅允许原生AMD64 Windows 11 |
| 安装目录 | 当前用户`%LOCALAPPDATA%\Programs\DesktopUp Preview\20260928.1`；固定版本路径，忽略`/D`；目录已存在即拒绝，不覆盖 |
| 数据目录 | 默认`%LOCALAPPDATA%\DesktopUp\Preview`，测试仍可显式指定隔离root；不自动读取或迁移日常`DesktopUp\V3` |
| 单实例 | Preview使用`DesktopUp.V3.Preview`，普通构建保留`DesktopUp.V3`；预览版本之间共享预览身份和配置，不应并行运行 |
| 入口 | 当前用户开始菜单；控制中心标题和托盘带Preview标识；安装结束的启动勾选默认关闭，不设置开机启动 |
| 权限 | 安装器`RequestExecutionLevel user`、App原有`asInvoker`；不主动请求UAC，继承启动者权限 |
| 运行库 | .NET 10.0.12、Windows App SDK 2.4.0自包含、多文件payload；不需要另外安装开发SDK或全局.NET；仍依赖Windows系统组件 |
| 卸载 | 运行中或不可写时拒绝；只按编译时的文件清单Delete，再非递归移除空目录；不强杀进程、不递归删安装目录、不删除预览配置或未知文件 |
| 分发 | 未数字签名、无自动更新；含原始NuGet许可证/声明、nuspec归属及补充上游许可证；不带PDB、数据库、个人配置或测试夹具 |

当前仍为DEMO内容。R6的被动空间不足/二维恢复、第三批资源门、原生人工未完成；BUG-026、OPT-MOTION-02等已知项仍保留。安装包不是完成真实文件收纳的承诺，也不把HTML原型当产品已实现。

## 3. 已取得的证据及边界

本机系统为Windows 11 24H2 / build26100.8875。所有运行测试继承了工具的提升权限，启动日志`isElevated:true`；**未完成干净电脑、普通权限、目标电脑或人工点击安装向导的认证**。没有禁用安全软件或系统防护。

| 验证 | 结果 / 证据 |
| --- | --- |
| 发布及普通构建 | 最终publish无警告/错误；NSIS以`/WX /INPUTCHARSET UTF8`编译通过；关闭Preview开关的普通构建0警告/错误 |
| 合同测试 | [2158通过/0失败/0跳过](../../../../out/preview-20260928.1-r3/tests/preview-contracts.trx) |
| 最终安装 | [installer exit0；612个payload文件全部hash一致](../../../../out/preview-package-checks/final-install.json) |
| 首次启动/单实例 | [默认Preview目录、CLR/hostfxr/WinUI核心模块从安装目录加载；重复启动exit0](../../../../out/preview-package-checks/default-profile-smoke.json)；PID22176正常exit0/资源0。此测试在试装包进行，App SHA与最终包完全相同，最终包另行安装并验证所有文件 |
| 安装/卸载安全 | [重复安装拒绝exit12，退出后卸载exit0；配置和人为添加的未知文件保留](../../../../out/preview-package-checks/trial-uninstall.json)；运行中卸载拒绝exit21，见首次启动记录。测试哨兵验证后单独清理，未删用户数据 |
| 最终包浏览组合 | [16场景通过](../../../../out/preview-package-checks/installed-browse/observation.json)，包含两轴640循环拖动及56主动缩框检查、102滚动事件；PID52740 exit0/资源0，产物稳定 |
| 最终包构图/恢复 | [构图/R4/单次撤销/同库重启通过](../../../../out/preview-package-checks/installed-composition/observation.json)，PID55204/45288 exit0/资源0 |
| 最终包有限协议 | [Protocol及重启通过](../../../../out/preview-package-checks/installed-protocol/observation.json)，PID53400/42808 exit0/资源0 |
| 普通/预览共存 | [新普通构建PID40220与最终预览PID55664同时独立启动](../../../../out/preview-package-checks/coexistence.json)，分别报告V3/Preview身份，均正常exit0/资源0；普通构建只用隔离测试root |
| 最终正常卸载 | [标准Uninstall.exe入口退出0、安装目录/注册项/快捷方式移除，Preview配置保留，日常V3全部文件hash不变](../../../../out/preview-package-checks/final-uninstall.json) |

托管App回归不是原生鼠标/动画像素或人工验收。测试安装已卸载；`%LOCALAPPDATA%\DesktopUp\Preview`中本轮新生成的测试配置/日志按保留规则仍在，不自动删除。交付文件和全部隔离证据保留在`out/`，不随Git提交二进制。

## 4. 制作入口及失败记录

- [DistributionBuild.cs](../../../../src/DesktopUp.App/DistributionBuild.cs)：仅编译通道身份、默认root、标题/托盘；普通构建保持原行为。
- [发布配置](../../../../src/DesktopUp.App/Properties/PublishProfiles/Preview-x64.pubxml)：.NET＋Windows App SDK self-contained，保留多文件、不裁剪、不单文件打包；运行时补丁限定在`Microsoft.NETCore.App`的FrameworkReference，不污染Windows SDK引用。
- [打包脚本](../../../../scripts/package-preview.ps1)：新out目录、源码漂移检查、运行时/资源必需项、禁止PDB/数据库/夹具、许可证/相对文件hash、安装/卸载精确清单、最终Setup哈希。脚本不会执行安装或Git操作。
- [安装脚本](../../../../packaging/preview/installer.nsi)：版本化当前用户目录、平台守卫、重复安装拒绝、运行中卸载守卫和非递归卸载。

重建命令（需要项目SDK、可恢复的NuGet依赖、已安装NSIS；OutputRoot必须全新）：

```powershell
./scripts/package-preview.ps1 -ReleaseId 20260928.1 -OutputRoot G:\A_\Tools\DesktopUp_V2\out\preview-new-build
```

草稿失败保留：最早发布默认取SDK带的.NET10.0.3，不分发；全局设置RuntimeFrameworkVersion误作用于Windows SDK引用产生NU1505/NU1102，改为限定FrameworkReference；随后守卫拦截ProjectReference生成的4个PDB，CLI补齐DebugType/DebugSymbols；NSIS首次遇到中文编码和卸载命令引号警告，明确UTF8及修正引号后以警告即错误重新通过。最终包不沿用失败稿，试装/正式安装均另有绑定记录。

运行时依据：[Microsoft自包含部署文档](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)；补丁版本核对：[.NET10官方发布页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)（2026-09-28读取，10.0.12）。后续分发须重新核对安全补丁，不将此次版本永久视为最新。

## 5. 目标电脑验收与回归主线

目标电脑先核对SHA256，再由用户决定是否信任未签名包。若被SmartScreen/组织策略阻止，保留提示并反馈，不要求关闭Defender或整体防护。

最小检查：安装/启动→新增DEMO海报→拖动/收展→控制中心修改→退出/重启保留→托盘正常退出→卸载保留配置。异常反馈提供winver、截图、操作顺序及必要日志（先检查本机路径隐私），不要发送整个个人配置库。

交付结束后等待用户下一条任务。主线顺序未变：R6第二批2B真实被动空间准入/有限只读二维/最新状态恢复→第三批资源与完整连续门→模块解耦→Rain→R7/R8；本轮没有提前执行这些任务。

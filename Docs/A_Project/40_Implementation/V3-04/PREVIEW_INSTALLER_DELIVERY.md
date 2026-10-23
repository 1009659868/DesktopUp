# Windows 11 x64 独立预览安装包

## 2026-10-23 唯一安装与迁移升级发布同步

### 已验集成与精简发布

- 用户确认原位升级与换路径迁移两组集中人工均通过，本批USER PASS / CLOSED；[验收记录](SINGLE_INSTALL_MIGRATION.md#2026-10-23-人工通过与收口)。复用42项定向，不重跑历史业务矩阵。
- 功能 `impl/09-single-install-migration@f6a09c293ba8b6dc9391578b6593895f78500031` 已提交推送；非快进集成 `impl/develop@876293d220c97fe6f246d46b18f6c5ca59157531` 已推送。精简发布合并 `impl/main@ecb0c04834ef2238252bd89fa77b29837466c2dd` 已推送。发布树不含Docs/tests/tools/out；仅保留原有发布裁剪并移除Setup测试Checkpoint，不改变正常业务事务。README与包内用户说明同步为唯一安装/迁移规则；完整资料仍在develop，不反向合并发布删除。
- 独立Release构建与合并提交前打包预检通过，再从干净main提交重建最终包；构建/NSIS 0警告0错误，统一文本498文件/26冻结迁移通过。最终507项源输入、810项载荷摘要核对，`IncludesUncommittedChanges=false`，不带用户配置、测试载荷或PDB。
- 最终包：`out/releases/main-single-final01/DesktopUp-Preview-20261023.5-win-x64-Setup.exe`，67,384,679字节，SHA256 `538FC52E9D3011EA2554DA755F96228A4F041D5D76F4E8E9B46C8A5043B7B51D`。SHA256SUMS.txt文件SHA256 `0A125D1BB7D246BA6147AE4D76710E1690710E903D1C085C707ED854AFF414D6`。`package.json` Verified:false仍表示打包器不自行验收，不伪改该字段；本次证据为下述独立结果。

### 限定安装验证与清理

- 为避免修改日常安装，使用精简main源码和最终payload制作独立登记身份副本（测试编译仅改身份/入口名称与profile，不增加产品运行期开关）。实际安装、默认发现旧版、运行中拒绝、原位升级、同版G→C迁移、唯一固定登记、旧程序移除、Run目标更新、数据/未知文件保留及810项安装摘要通过。新旧App均实际启动/正常退出，error日志0、退出资源0。此为隔离身份验证，不冒充使用正式身份升级了日常安装。
- 首次自动清理被测试脚本“Exit fixture normally first”进程守卫停止，**尚未调用卸载器**。保留原始`installed-validation.log`；后续只读核对已正常退出且无测试进程，再单独核对迁移终态并显式卸载，退出0，清理完成。未改产品、未强杀、未增加自动重试；不将该次脚本中断归因为历史卸载或数据库锁问题。最终限定结果为`out/releases/single-install-validation/installed-result.json`。
- 日常配置、Run/StartupApproved、入口和登记快照前后相同，测试安装/固定登记/启动值/快捷方式/进程、C盘临时目标及测试profile均已清理。测试配置和样例逐项摘要归档：`out/single-install/review/closed-20261023-161718.zip`，SHA256 `A358C56186325300476D949D2B5607ECA237B4FE45B7632CE41E3BB6355B0767`。
- 预提交包、重复构建与本轮测试夹具等4294项可重建文件已按精确路径清理，46项日志/生成源摘要归档：`out/releases/single-install-validation/build-evidence-20261023-161740.zip`，SHA256 `75FB2817B6568EE135A94230BB5239ED2C05629C24DC2FE06F2C80B30DF0B73F`。清理脚本末尾展示曾读错结果路径；归档和删除已完成，修正展示行后只读复核，未重复删除或覆盖原记录。最终包、必要证据和已验候选保留。

### 更新现有预发布

- 用户明确选择沿用 [preview-20261023.1](https://github.com/1009659868/DesktopUp/releases/tag/preview-20261023.1)，不新建tag。Release ID仍为421411390、Pre-release保持，标题及包内版本为20261023.5；标签名与包版本不同，以附件/标题为准。
- 保全原发布与旧附件本地摘要后，暂存新附件；仅此标签按旧对象显式force-with-lease更新。旧对象 `cf50d1401b0c07c6d81c13742254cd59ce58830b` → 新对象 `e483cfcbc21e3d4eb3e9787feebe0f4ddac3d24e`，指向上述干净main提交ecb0c04。无分支强推。
- 同一页面替换为安装包和SHA256SUMS.txt两项附件，服务端及实际下载流摘要均匹配。旧附件移出页面但本地仍可恢复；preview-20261021.5的标签、说明、附件核对不变。完整产品功能说明保留，新增唯一安装/原位与迁移升级，不加入开发测试或故障内容。
- 过程与回执在`out/releases/single-install-validation/{before,stage-receipt,publish-receipt,package-audit}.json`。本轮最终回impl/develop，交接随文档提交推送。

既有首次卸载24/Windows32持有者未知、历史卸载首因、升级41首因、P1/R6锁冲突及剩余成本继续保留；真实登录、其他权限/跨机环境不补PASS，真实热度未推进。

## 2026-10-23 覆盖升级收口、发布同步与现有预发布更新

### 验收、集成与源码绑定

- 用户确认取消保护、原位升级后内容/设置/入口保持两组均通过；覆盖升级USER PASS / CLOSED，复用18项定向结果，不扩跑历史业务矩阵。[功能验收记录](INSTALLER_UPGRADE_DELIVERY.md#2026-10-23-人工通过与本轮收口)。
- 功能 `impl/09-installer-upgrade@eee8e56bbc9024eba39aebaa10ab9b02826d996a` 提交推送，非快进合入并推送 `impl/develop@51419c4c4ecef4405fa9f62956c143264113a938`。精简发布非快进合并为 `impl/main@e3bb8c89c2bbd096ca51ced0b8956daa38d064f9`，已推送，README同步。完整开发资料和测试保留在develop；main不保留Docs/tests/tools/out或验收脚本，Setup中的测试友元与内存故障检查点仅在发布树裁剪，不改变正常事务流程。未反向合入发布删除。
- 最终包由干净main@e3bb8c8构建，`IncludesUncommittedChanges=false`；独立Release和自包含打包均0警告/0错误，文本门497文件/26冻结迁移通过。506项产品输入摘要与构建源一致，载荷811文件，不携带测试、探针、用户配置或调试符号。
- 成品 `out/releases/main-upgrade-final01/DesktopUp-Preview-20261023.3-win-x64-Setup.exe`，67,398,393字节，SHA256 `9516BD5049813274EF75A324D5729ADCC839B71253ADF5EDDF24A69A2F98FCB7`。SHA256SUMS.txt 114字节，文件SHA256 `E11119310137F19962E4BC3D1DCF3BD2EDFC517460CE6952EF7A14D30142E0FE`。

### 最小发布包验证与收尾

- 隔离中文空格安装目录，用旧公开20261023.1包实际安装，再用本轮精简main包升级为20261023.3。旧版运行中升级返回41并保持登记，正常退出后升级0；数据库、未知文件与自建收纳文件摘要保持，810项安装文件摘要匹配，旧登记、自有菜单入口及升级备份清理。
- 新旧程序均经安装根启动器与包内运行库正常启动、退出资源0，无error日志；新包正常卸载0。仅这轮直接风险路径，不追加历史拖放/架构/卸载故障矩阵。原目录更新保持Run/StartupApproved快照，不宣称再次验证真实登录；无/D自动选择本轮不操作日常安装，仍为源码核对。
- 日常Preview/V3配置、Run与禁用状态、桌面/开始菜单入口、既有安装登记前后相同。本机唯一日常登记为 `F:\DesktopUp\DesktopUp Preview\20261021.5`，未更新或卸载。测试登记/进程归零。
- 清理精确范围：`out/installer-upgrade/main-upgrade-smoke01`、`out/releases/main-upgrade-final01/build` 与 `setup-build`。18项记录/配置/样例逐项摘要归档，932项可重建重复输出移除。归档 `out/releases/upgrade-main-validation/closed-package-test.zip` SHA256 `B226FB725539E1DE0FFA82646F0E9CED693EE7724EA911E114E89C2E34049ABE`。原始记录与清理清单在同目录，最终安装器/payload/构建证据保留。

### 现有预发布更新（明确授权，不新增tag）

- 沿用 [preview-20261023.1](https://github.com/1009659868/DesktopUp/releases/tag/preview-20261023.1)，Release ID仍为421411390，Pre-release保持。包内版本递增到20261023.3，让旧公开版/20261023.2验收版识别为较新版本；标签名称不再等于包内版本，下载以附件及发布标题为准。
- 标签由旧对象 `f073e5093aa8c571bbad6fff7d8c02fee91c3d46` / 源码7160078改为对象 `cf50d1401b0c07c6d81c13742254cd59ce58830b` / 源码e3bb8c8。仅此标签按旧对象显式force-with-lease更新，不强推分支、不覆盖未经核对的远端变化。
- 更新前保存原release/tag元数据，核对旧附件与本地旧包一致。先暂存并校验新附件，再切换标签，替换旧安装包/校验文件并恢复公开。GitHub暂存时将发布暂挂untagged草稿，校验及时停止，后续明确绑定原tag完成；Release ID不变，未创建额外Git标签，旧公开安装器仍有本地可恢复副本。
- 发布说明保留完整功能介绍，加入原位更新、配置/收纳/自启保留及原目录升级操作，不纳入开发测试内容。最终仅安装器和SHA256SUMS.txt两项附件；服务端摘要及实际下载流摘要与本地均匹配。`preview-20261021.5` 的标签、说明与附件核对未变。
- `out/releases/upgrade-main-validation/{before,stage-receipt,inspect-receipt,publish-receipt}.json` 记录过程与结果。已经拉取旧tag的开发环境如需更新，应核对新对象后显式更新该tag，不把本次移动描述为新增版本标签。

最终回到impl/develop，交接文档随独立提交同步；历史自然卸载首因、上一轮升级41首因、P1/R6锁冲突与剩余读取/观察成本继续保留，不因本次成功销项。真实热度不推进。

## 2026-10-23 两项海报修复与新预发布

### 集成与发布绑定

- 已验功能 `impl/poster-create-enter@52529a1`、开发集成 `508d7ec` 均推送。精简 main 非快进合入该集成点，冲突仅开发文档/测试保留删除及 README 人工协调；相对前版只改变 README、`ControlCenterWindow.Editing.cs`、`ContentFrameMinimumPolicy.cs`。普通业务源码与已验修复相同，冻结迁移、循环滚动与发布裁剪不改。
- 干净 `impl/main@71600786c7068345db265affc7723b64c3430ec2` 制作 `20261023.1`，`IncludesUncommittedChanges=false`；该提交与新附注标签 `preview-20261023.1` 已推送。标签对象 `f073e5093aa8c571bbad6fff7d8c02fee91c3d46`。不移动旧标签，不将新包挂在旧源码下。
- [GitHub Pre-release](https://github.com/1009659868/DesktopUp/releases/tag/preview-20261023.1)，Release ID `421411390`，发布于北京时间 2026-10-23 11:10:10。附件为安装包和 SHA256SUMS.txt；服务端摘要与实际下载流摘要均相同。旧版 `preview-20261021.5` 标签、说明及两项附件核对未变。发布说明包含完整功能介绍及两项新增改进，不包含开发测试说明。
- 本地包：`out/releases/main-poster-fixes-final01/DesktopUp-Preview-20261023.1-win-x64-Setup.exe`，67,341,106 字节，SHA256 `6285A0713D8179444A7FFECB7BECDD5629612339BFBAE4E12748C899EE8DB48A`；SHA256SUMS.txt 为 114 字节，文件本身 SHA256 `A81BA7BF9A9C890AD7BFF9D3DDDAFDAE046E10AC67B58D7BB6B2AA2555E75EA6`。Git 不提交二进制包，GitHub 发行附件按授权上传。

### 精简验证与边界

- main 独立 Release 预检及自包含打包均 0 警告/0 错误，文本门491文件/26项冻结迁移，500项输入逐项摘要一致；807项载荷（806项清单声明文件及清单自身），不含测试夹具、探针、调试符号或用户配置。
- Windows PowerShell5.1 64位、本机高完整性：以自选中文空格目录和独立 profile 实际安装，关闭创建桌面快捷方式；806项安装文件摘要逐项一致。通过安装根目录启动器使用包内运行库，首次新schema26、basic-box默认、控制中心显示、无error日志；正常退出资源0。原安装卸载器的隔离执行副本直接执行普通卸载，实际执行退出0，程序清单/临时登记移除，配置与自建未知文件摘要保持。没有故障注入或产品自动重试。
- 两次测试脚本问题保留：smoke01在安装后遇PS5.1 JSON数组嵌套，未启动产品；修正脚本后由原卸载器清理该自建安装。smoke02产品安装/运行/卸载全部完成，但汇总对单结果 `.Count` 判断不兼容。改为明确集合计数，并对已保存的执行、日志、源输入与前后快照只读复核，不再次安装。最终 `out/releases/poster-fixes-validation/package-audit.json` 为 PASS；不得将夹具问题归为产品故障，原始失败文件未丢弃。
- 首次测试前状态与最终状态一致：Preview/V3配置、Run登记、DesktopUp桌面/开始菜单快捷方式及原安装登记均相同；无遗留DesktopUp进程。没有移动真实内容，不覆盖现有安装或已验候选。两项交互复用此前42项定向及集中人工USER PASS，不扩跑历史矩阵。
- 此为本机限定发布验证，不替代普通权限GUI、真实登录自启、多DPI、跨机及长测。历史自然卸载首因、P1/R6锁冲突与剩余读取/观察成本继续保留，真实热度未实施。

### 测试收尾

- 确认登记卸载完成后，清理精确目录 `out/releases/poster-fixes-smoke01`、`poster-fixes-smoke02`、`poster-fixes-preflight01` 以及 `main-poster-fixes-final01/build`，共1125文件、550,119,670字节。检查绝对路径范围、重解析点、进程并逐项复核摘要后清理，没有清空out或修改其他候选。
- 两次测试中的配置、日志、执行器、样例及原始失败证据归档至 `out/releases/poster-fixes-validation/closed-test-evidence.zip`，逐项归档流哈希核对通过；SHA256 `08A37EFE08DC513FCBC4B6FBF8976A31A03AB991438EBAC618FEAED92104AB05`。预检/重复build为可重建产物，保留日志与逐项manifest，不保留重复副本。最终安装包、payload、生成清单及构建证据保留。
- `poster-fixes-validation/{cleanup-manifest,cleanup-result,package-audit}.json` 记录明细；`github-release-preview-20261023.1/{stage,publish,verify}-receipt.json` 记录远端资产与旧版保留核对。原smoke路径已归档，不作为仍存在的安装入口。最终返回 `impl/develop` 补交接并推送，不反向合并发布精简删除。

## 2026-10-21 即时取证集成与发布同步

- 功能9b0f112已推送，非快进合入develop80d8d0b并推送。发布合并78e5ff7、README记录ed5146d已推送；最终返回develop补交接。main跟踪内容无Docs/tests/tools/out，开发资料与测试只在开发线；发布相对前版仅README、installer.nsi及uninstall-evidence.nsh变化。既有STARTUP_REVIEW裁剪保持，不改变普通业务。
- 先验证合并工作区预检包，再从干净`78e5ff70f64c149dfe8b801c431c98aa01b9ea6c`生成最终20261021.5（IncludesUncommittedChanges=false）。Release/自包含打包0警告0错误，文本门491文件/26项冻结迁移，500项编译/载荷输入摘要核对一致；807个载荷文件，无调试符号、探针/夹具或用户数据。后续README不改变这些构建输入。
- 最终包：`out/releases/main-immediate-final01/DesktopUp-Preview-20261021.5-win-x64-Setup.exe`；67,337,513字节，SHA256 `C65C216163E28B6BC90181A761E67FE72664906D17AB2463C61FE91F0BE75A94`。包和大体积证据仅保留本地out，不上传Git。
- Windows PowerShell5.1、本机高完整性定向验证：在独立中文空格目录实际安装，包内运行库启动（新schema26、默认basic-box）、无error、正常退出activeResources=0。复用已验Test-UninstallFailure的隔离副本，仅调整repo/辅助脚本路径；不搬动真实内容、不开桌面快捷方式，不重跑历史业务矩阵。
- 三段PASS：受控占用app/wuceffectsi.dll时DeleteFileW/Windows32、实际执行PID48196/退出24、保留后续文件和卸载入口，报告`cleanup-134370467806088350-48196.ini`；释放自建句柄后另一次显式卸载0，配置/未知样例/首次报告保持；重装后普通首轮卸载0。没有产品自动重试，没有自然故障复现，HistoricalCauseEstablished=false。
- 隔离前后Preview/V3配置、Run、DesktopUp桌面/开始菜单入口及原有安装登记完全相同；无剩余DesktopUp进程。证据摘要`out/releases/immediate-main-validation/main-immediate-smoke01/{package-audit,isolation-audit,before,after}.json`；未操作真实文件、旧候选或历史stash。
- 授权清理：确认临时安装已正常卸载且登记消失后，仅移除`main-immediate-smoke01`、`main-immediate-final01/build`和`main-immediate-preflight01`三个精确目录，共2311文件、1,135,130,405字节。删除前确认绝对路径在out/releases内、无重解析点/在用进程，重新核对逐项摘要。2247项与保留的最终payload文件完全一致，64项独有证据/测试样例/预检产物归档并逐项解压流校验；不是清空整个out。
- 归档：`out/releases/immediate-main-validation/closed-release-tests.zip`，SHA256 `DE9BA67452EF3CB4BA467C4DB5396187309E5D5DD2F8B28C58AC3171E19E9159`；同目录cleanup-manifest.json列出每项恢复来源，cleanup-result.json确认3目标均已移除、ProtectedStateUnchanged=true。上述原始smoke路径从归档读取，不能再当活跃安装；正式包、最终payload和构建日志保留。

限定结论：验证的是当前机器的构建/打包/实际安装运行及卸载安全边界，不替代普通权限GUI、真实登录自启、跨机、多DPI、满盘或长测；包仍未签名。历史自然卸载首因和P1/R6锁冲突继续独立OPEN；剩余读取/观察成本按原计划先测量，成功测试不销项。真实热度仅进入设计准备，本次不实现。

## 2026-10-21 海报优化集成与发布同步

- 两轮集中人工已验收口：功能5dcf4fb推送 → 非快进develop9e793f8推送 → 精简发布合并472e8ff及README6cec0b8推送，最终回develop补本次交接。main无Docs/tests/tools/out跟踪文件；完整资料/测试留在develop，历史stash不动。本次30个生产路径与已验develop比较，差异仅为原有发布诊断裁剪，未修改业务逻辑。
- 预检包通过后，从干净提交 `472e8ffa072b69f716716dbdcd38c34c667d1166` 生成最终 `20261021.3`（IncludesUncommittedChanges=false）。两次构建0警告0错误，main文本门490文件/26迁移通过，001～025未修改。499项编译/载荷输入摘要一致，807个载荷文件，无开发探针、测试数据或调试符号。
- 最终包：`out/releases/main-scale-final01/DesktopUp-Preview-20261021.3-win-x64-Setup.exe`，67,344,227字节，SHA256 `642BAF5C301DD907EFF13FE07F1B4461A6284401C6561DBD76AF3ADBF1AA609C`。main后续6cec0b8只补README，不改变这些输入；包和运行证据留本地out，不上传Git。
- Windows PowerShell5.1隔离验证：自选中文空格目录安装；安装目录自带运行库启动，新库schema26、默认basic-box，退出activeResources=0。复用已验卸载脚本，不移动真实内容；smoke02的受控占用退出24/释放后显式重试0/重装后普通首轮卸载0均通过。证据 `out/releases/main-scale-smoke02/results.json`、`out/releases/scale-main-validation/main-scale-smoke02/package-audit.json`。
- 保留失败：smoke01重装后的普通卸载在 `app/DesktopUp.dll` 停止，marker记录24、566项声明文件仍在。稍后DeleteSharingError=0、Restart Manager空，不足以解释失败时状态；未把“暂时无占用”当作首因。现场marker/ACL/剩余状态和时间保存在 `scale-main-validation/ordinary-stop-*`，原卸载器显式重试0后清理完成。与既有卸载中断首因未定事项关联保留，不称根治，不覆盖首次证据。
- 审计夹具修正也独立记录：初次PS5.1 ConvertFrom-Json数组套层导致源摘要审计在安装前停止；另一次finally的隔离断言遮盖上游错误，已补错误保存再重抛。只修改out验证脚本，不改产品源码/包。首次after.json保留当时测试登记，最终 `final-audit.json` 对最初before与smoke02结束状态重新比对一致。
- 最终测试安装/登记和进程均为0；日常Preview/V3配置、Run登记、DesktopUp桌面及开始菜单入口前后相同。无桌面样例遗留、未知文件/测试配置按合同留在out。包未签名；仅本机高完整性定向验证，不替代普通权限GUI、真实登录自启、多屏DPI、跨机、满盘或长测。用户已验的是对应开发候选，不冒称本包重新人工验收。

历史卸载首因、P1/R6锁冲突及剩余读取/观察成本继续保留，真实热度HOLD。本轮不追加根因修复或下一功能；自然再现时先取证，再按授权定向处理。

## 2026-10-21 已验卸载诊断集成与发布同步

- 功能提交 `c8a6714` 已推送；以 `--no-ff` 合入 `impl/develop@940ff96` 并推送。发布合并 `9e84ad2` 保留来源，仅改README、安装器和打包入口；`impl/main@4e79035` 补发布验证记录后已推送。最终返回develop，另补本次交接；未将发布精简反向合回。
- main仍无Docs/tests/tools/out跟踪文件，旧发布stash `0d082b90b9ef500b519162444bce869a28b49757` 未动。统一文本门/25项冻结迁移、运行依赖及许可证完整保留；完整开发资料/已验测试仍在develop。正常发布通道没有增加验收入口。
- 预检包验证合并后的精简构建/打包，随后从干净发布提交 `9e84ad201ab2c3f051b1a297a421d7d94cbe17a7` 生成最终 `20261021.2` 包（IncludesUncommittedChanges=false）。两次构建均0警告0错误；main文本门488文件/25项迁移通过。只验证本轮影响，没有再跑历史业务全量。
- 最终包：`out/releases/main-uninstall-final01/DesktopUp-Preview-20261021.2-win-x64-Setup.exe`，67,333,895字节，SHA256 `7CBCA6CB91B320CBA8346B9C2360EB094492BBE31AD928341368913C18F69787`。807个载荷文件，无探针/夹具/用户数据库/调试符号；497项编译/载荷输入与源码匹配。main后续4e79035只补根README，不改变这些输入。
- 最终包在Windows PowerShell5.1完成定向隔离验证：自选中文空格目录安装；安装目录内自带运行库实际启动，schema25/默认basic-box，正常退出activeResources=0；受控禁止删除共享返回24，首个失败路径准确、后续删除停止；释放自己的句柄后原卸载器显式再次执行返回0；重装后的普通首轮卸载返回0。三项结果见 `out/releases/main-uninstall-smoke01/results.json`，日志/快照保留，不把受控占用当作历史归因。
- 测试复用已验脚本的out副本，仅调整仓库/辅助脚本定位，不将脚本加入发布树。源为功能提交c8a6714的 `tests/Packaging/Test-UninstallFailure.ps1`；副本和只读审计在 `out/releases/uninstall-main-validation`。最终安装/登记已由原卸载器清理，测试内容仅留out；日常Preview/V3摘要、Run登记及桌面快捷方式前后相同，DesktopUp进程0。
- 限定：本机高完整性自动验证，不替代普通权限GUI、系统设置卸载入口点击、真实登录自启、跨机、多DPI、满盘或长测。历史卸载中断首因仍OPEN，与P1/R6锁冲突分开保留；剩余读取/观察成本不销项，不推进真实热度。开发候选的集中人工通过继续保留，但不冒称本发布包重新进行了GUI人工。

本次只同步已验诊断补强；自然复现时仍先保留失败文件、实际执行码和当时占用/权限证据，再决定定向修复，不用强删或自动重试掩盖首因。后续顺序见[建议系列计划](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#2026-10-21-发布同步后的系列计划建议未开工)。

## 2026-10-20 架构已验成果集成与发布同步

按本次明确授权恢复发布同步，不增加真实热度或其他功能，不重跑已验业务的历史全量矩阵。R1～R8/P1～P3保持 USER ACCEPTED / CLOSED；剩余成本、P1 final02 / R6 probe02锁冲突未定因继续保留。

### Git 与发布边界

- `impl/architecture-foundation@3c2417e`：已验源码、移位、测试、收口资料/图源提交推送；`impl/develop@bbbe417`：非快进合入并推送。
- `impl/main@3b39231`：从已验 develop 进行正常合并并单独裁剪开发资料/测试/工具；不是将发布删除反向合入开发线。保留六个产品工程、内置模块、运行配置、许可证、构建/打包入口、统一文本门和25项冻结迁移资源。
- main `74e5088`：README/随包自启动和卸载说明同步；最终安装包基于该干净提交。main `fe5741d`：仅补 README 的最终验证与未定因中断记录，497项编译/载荷输入与最终包清单一致，已推送。最终返回 develop，并同步相同的随包说明，不带入发布裁剪。
- 旧发布 stash `0d082b90b9ef500b519162444bce869a28b49757`、旧失败包/安装/配置均保留，未 pop/apply/drop。未改日常配置、真实收纳文件、Run登记或桌面快捷方式。

### 最终交付与限定验证

- 成品：`out/releases/main-20261020-foundation02/DesktopUp-Preview-20261020.1-win-x64-Setup.exe`，67,393,219字节，SHA256 `855E6984FBDBC8F5067B0E16CDC71E52F1AF35B297F577DD0BC733C1F1567A92`。`preview-build.json` SourceCommit=`74e50885941a8ed724b00fe14f1a993ec7511400`、IncludesUncommittedChanges=false。包及原始证据只在本地 out，不上传Git。
- 精简Release源码构建、发布预检包和最终自包含构建均通过，最终0警告0错误；文本门488文件/25项冻结迁移；Windows PowerShell5.1的打包CheckOnly通过。开发提交前文本门964文件、架构生产108/Review177、四门PASS、diff-check通过，未重复全量合同测试。
- `main-20261020-foundation-smoke03/results.json`：同包七个自动检查通过；中文含空格自选目录、HKCU Registry64安装/卸载登记、806项清单声明文件摘要（另有清单自身，共807项载荷）、桌面快捷方式关闭、全新schema25零海报默认基础收纳、安装目录自带.NET加载、`--autostart`静默/重复单实例、显式中心、两次退出资源0、登记卸载、未知文件及配置摘要保留；旧Preview/V3、Run登记和桌面快捷方式前后相同。临时安装/登记已正常卸载，样例仅在out。
- `foundation-package-migration02/result.json`：仅复制保留的旧schema21数据库，用最终包内Infrastructure组件初始化到25、再次初始化Ready；旧21项账本逐字段相同，原始数据库SHA256仍`72A5B4ECC0B35ABA9E2526A688784E8F2BC19261E10501C30DFD8E8F08E09DA3`。未在原配置启动或改写旧记录。最终Infrastructure DLL SHA256=`317C9A04425A935CB28A0D8EDD000493D13950D36CBF942F6E4A21638C118797`。
- 过程证据：`foundation-main-build01/02.log`保留发布裁剪的残留诊断引用编译失败，修订仅涉及发布入口；`foundation-main-build03.log`为0警告0错误。`foundation-package-preflight.log`、`foundation-package-final.log`、`foundation-package-final02.log`及smoke01/02/03均保留，未用后续成功覆盖失败。preflight/01不是最终分发路径。

### 一次卸载中断：保留未定因

最终包smoke02首次卸载在部分程序文件清理后停止：806项声明文件中730项已移除，首个剩余为`app/wuceffectsi.dll`；日志显示内容保留准备完成且App正常退出，但首次卸载子进程返回码未捕获。后续该文件可独占只读打开，不能据此断言曾由谁持锁，也不能归咎于历史P1/R6锁冲突。

通过同一登记的原卸载器 `/S /CONTENT=keep` 重试完成，数据库与未知文件摘要未变，没有手动删除程序文件/改登记；`main-20261020-foundation-smoke02/uninstall-retry.json`保留事实。之后同一包在新隔离目录smoke03完整首轮通过。**此异常首因仍未知，不标记已修，不宣称所有卸载首轮可靠性已经验证。** 后续再现时优先捕获实际卸载子进程退出码、第一失败文件及占用者；不预先改为强删、终止进程、跳过失败文件或循环重试。

本轮运行于高完整性环境；不替代普通权限原生拖放/安装向导人工点击/真实登录自启/跨机/多DPI/满盘/长时验收。未签名预览状态不变。架构图浏览器/视觉证据限制和第30.4节剩余成本照旧携带，真实热度HOLD。

## 2026-10-19 迁移指纹与统一编码修复

**CODE / 定向验证完成；发布同步仍 PAUSED，真实登录自启仍未验。** 用户随后授权按补全方案处理，本轮仅在 impl/architecture-foundation 修复并验证，不恢复发布合并、安装或自启操作。下方“暂停修复/问题未修”是此前现场快照。

### 根因与实现

- 旧迁移在多个 C# raw string 中声明，SHA256 包含编译时换行。单独锁定 SqliteStateStore.cs 的 LF，不能约束其他声明文件。除最先阻断的 schema13 外，保留库 schema17 也具有已核实的 CRLF 指纹差异。
- 迁移1～25全部抽到 src/DesktopUp.Infrastructure/Persistence/Migrations/001.sql～025.sql，UTF-8无BOM、LF、不追加末尾换行；catalog.xml 固定版本、名称、文件、SHA256及每项历史指纹证据。删除原业务类中的重复SQL声明，由 SqliteMigrationCatalog 读取嵌入资源，严格解码、检查原始字节哈希后，把同一文本交给SQLite执行。schema仍为25，不增加表结构或改写旧账本。
- 修订前架构 product01 和失败发布包20261016.101的25项编译SQL逐项相同。当前资源仅把已核实的CRLF转为LF，除此之外逐字不变。保留这两个产物实际生成的历史指纹；另加入原schema21库实际记录且能仅以换行差异精确复现的v13/v17指纹。**不是运行时生成/接受任意LF、CRLF、空白变体**；未知指纹、错误名称、跨版本挪用指纹及实际schema破坏仍拒绝。旧记录只读不重写，新记录只写固定资源的指纹。
- .editorconfig 给出编辑规则，.gitattributes 固定检出换行：普通源码/SQL/构建配置为UTF-8无BOM+LF；PowerShell 5.1为UTF-8 BOM+CRLF；cmd为UTF-8无BOM+CRLF。scripts/validate-source-text.ps1 使用严格UTF-8解码、字节级BOM检查、NUL/EOL检查及固定SQL哈希检查；只报错，不自动修文件。
- Directory.Build.targets 把检查接入普通dotnet/VS构建和Publish入口，package-preview.ps1也在CheckOnly/打包前检查，并把文本规则加入源码绑定。无需依赖开发者全局core.autocrlf。UTF-8校验不能判断本来已是合法UTF-8的文字是否“语义乱码”，但拒绝不合法字节、UTF-16、错误BOM及换行，避免默认编码静默替换。
- 机械格式整理与逻辑修改分开核对：108份已有PowerShell脚本只有BOM/换行变化，解码后的文本与HEAD一致；已有打包脚本另有上述入口接线。C#业务差异仅迁移声明提取及迁移身份检查，没有改动海报、手势、文件搬移或热度逻辑。未使用git renormalize/提交/分支切换。

### 精简验证与证据

影响面是迁移身份、旧库升级/备份及构建文本来源，不重跑UI/拖放人工或全量合同。

- 正常产品 Release 构建：out/migration-identity-20261019/product01，0警告/0错误，不含Review执行器；Infrastructure SHA256为 A013FE98CEDCA0F6B3FFC5CA9F1FFD768A92D8F159FAF732C5D1D3794581C4D1。
- 选用 MigrationFingerprintTests、SqliteStateStoreTests、StateBackupRecoveryTests、ContentReferencePersistenceTests，共58个唯一测试最终通过。初轮57通过、1项新测试误用只校验当前schema的ValidateBackup接口；改为正式旧备份候选验证路径后，新增组9/9通过，49个未改的既有通过结果复用。构建时发现的XML注释/PS宿主模块依赖错误亦已修正，失败日志保留，不补写初轮PASS。
- 新增覆盖：25资源与预修订指纹、schema21/25旧账本逐项保留、升级/重开/备份、全新库固定指纹、错误名称/指纹/真实schema及SQL字节损坏拒绝。证据 out/migration-identity-20261019/tests 与 out/migration-identity-tests-build*.log。
- 当时保留的真实旧库仅复制到 out/migration-identity-20261019/retained-copy01，使用新产品Infrastructure实际初始化：21→25成功，再次Ready；原21条账本及升级前备份全部保持。源备份与真实Preview库SHA256仍是 72A5B4ECC0B35ABA9E2526A688784E8F2BC19261E10501C30DFD8E8F08E09DA3。结果 retained-copy-result.json；未启动App、未搬动收纳文件。
- 文本门通过891份源码/构建相关文件及25份SQL；9组编码/换行/SQL反例均拒绝且不改写输入，直接MSBuild的构建与不重编译发布入口也拒绝非法字节。证据 text-gate02；Windows PowerShell 5.1下原卸载/工具链/启动辅助三脚本解析通过；core.autocrlf=true/false得到相同Git eol规则。
- package-preview.ps1 -CheckOnly -Offline通过，未创建新安装包。完整安装/运行/真实登录自启没有在本轮复验，不将“库初始化成功”写成“安装版自启通过”。

### 后续边界

当前修复尚未提交/推送/合入develop或main。main仍f80765e，发布草稿stash及旧安装包/配置全部保留。后续获准继续发布时，应把本次修复作为独立变更同步到发布来源，不顺带合入未完成R1；impl/main必须保留.editorconfig、.gitattributes、共同构建目标、validate-source-text.ps1和嵌入迁移资源，它们是发布构建必需文件，不是可删除的测试资料。随后重新构建独立安装候选并继续尚未通过的登录自启验收。

这是向新修订版升级的兼容，不承诺旧候选能反读新版本创建/新增迁移的规范指纹。回退使用保留的旧配置/迁移前备份，不让旧验收包直接打开已升级的同一数据库。旧记录未被改写不等于所有历史二进制具有双向兼容能力。

本次属于持久化/构建一致性修复，不关闭R1，也不提前进入真实热度。架构现状和下一步仍以[基础架构余项](../Architecture/FOUNDATION_REMAINDER_PLAN.md)为准。

### 之前的暂停决定（保留历史）

2026-10-19 用户明确决定：**发布同步 PAUSED，保留现场**。不实施下方提出的迁移换行兼容修复，不继续打包/安装/自启动验证，待用户重新要求继续。问题保持 OPEN，草稿/失败包/原配置备份不动；本次仅本地登记决定，未额外提交或推送。

## 2026-10-16 发布同步阻断与现场保全

**状态：发布同步未完成；构建/安装载荷校验通过，启动与真实登录未验通过；兼容问题未修。** 本轮用户转为要求同步架构进度，未授权或实施此修复。历史自启动“关闭/卸载”人工通过仍保留，但不覆盖此次新包失败。

来源为本地已验 `impl/develop@53b6bec`，发布基线 `impl/main@f80765e`；不含 `impl/architecture-foundation@b3bfdc6` 的 R1 文件动作/拖放余项。合并按发布边界排除了 Docs/tests/tools、独立 Review 源及专用 StartupReview 通道/测试环境覆盖；服务注册保留在 Bootstrap，当前窗口反馈使用既有 CurrentWindow。README/包内说明同步已验能力、自启及卸载边界；没有业务功能改造。

### 实际结果

- 精简源码普通 Release 构建及正常 Preview 自包含构建均 0 警告/0 错误；403项源码清单哈希在打包后仍一致，索引406个跟踪路径，不含开发目录/验收脚本。
- 包：`out/releases/main-20261016-startup01/DesktopUp-Preview-20261016.101-win-x64-Setup.exe`；SHA256 `3F2F62D35AFDAF5C663FDAAE43567FA80420DCB1E62D11CCA97A8C4C528C16AF`。这是未提交合并树生成的失败候选，元数据 SourceCommit=f80765e、IncludesUncommittedChanges=true；不是已发布的 main 成品。
- 安装到 `out/releases/main-20261016-startup-check01/installed space`，未创建桌面快捷方式；813项安装载荷清单验证通过。旧安装登记未改，新安装仍保留以供定位，不运行。
- 启动前把原 `%LOCALAPPDATA%/DesktopUp/Preview` 完整复制并逐项核验到同证据目录 `profile-before`，未重置配置。随后 `--autostart` 启动 PID52312，在状态库初始化前置校验处失败并显示保护窗口；已经正常 `--exit` 退出，没有强杀。
- 原数据库 schema=21，失败点为 `Migration ledger mismatch at schema version 13`。只读核对：原记录 `217BFBF8017E342B013D34777FECB68B54A3C28713A28293280D58346F19FA79`；新包编译 SQL 的 LF 哈希 `E94E6680BA64CCDB8A679EC088F5532DEFC4E7305E0B6066DE7F2C0A4CA4D57A`。仅转为 CRLF 即精确复现原记录。迁移校验直接散列 SQL 原始字符串字节，换行差异会被误判；不能据此认定用户数据损坏。
- 原数据库尝试前后 SHA256 均为 `72A5B4ECC0B35ABA9E2526A688784E8F2BC19261E10501C30DFD8E8F08E09DA3`。未搬文件、未写 Run 自启项、未注销/重启；静默启动、重复启动等后续检查没有完成，不能写 PASS。

### 保全与后续处理

本机草稿 stash：`0d082b90b9ef500b519162444bce869a28b49757`，描述 `release-sync-f80765e-from-53b6bec-blocked-sql-line-endings-20261016`；保存树 `1f09dacfb86c880ff7b2ede2dad1b802eb12fe68` 与合并结束前索引一致。原合并来源为53b6bec；merge元状态已经结束以便切回架构分支，没有形成发布 merge commit。main/origin/main仍f80765e。不得把stash当正式发布提交，或反向应用到架构/develop。

stash、失败包、备份、results/app-events和BLOCKER均仅本机out/Git暂存，不随架构拉取；上面保留了另机重新定位所需来源与证据摘要。继续发布时应先核对实际refs；在干净发布工作区重新按Git策略合并来源，或审查本机stash后恢复等价树并正确保留合并父提交，不能直接把没有merge元状态的暂存当普通提交发布。

建议的窄修复范围仍待后续授权：在不重置库、不改表结构、不改写历史迁移记录、不跳过schema/身份校验的前提下，兼容**同一 SQL 的 LF/CRLF 等价字节形式**。先用原库副本和两种换行生成的记录验证兼容，同时验证任意改动哈希仍拒绝，再重建新编号正常安装包继续自启动验证。可能影响其他版本迁移及备份校验的范围仍需核清，不把它只限定为 schema13，也不借机扩大到 R1 架构改造。真实登录需用户准备后进行；手动 --autostart 不替代登录验收。

## 2026-10-16 V3-09-03开机自启动前移（计划）

用户要求在V3-06真实热度之前插入该发布能力。已验05-13/05-14源提交31c11b5、develop集成3c33a68；功能分支impl/09-03-startup从此派生。现为 **AUTO PASS / 部分USER PASS / 真实登录暂缓未验**：用户确认“关闭与卸载”通过，暂不验收真实登录自启，并明确授权提交、推送及合入impl/develop。此次允许携带该未验项集成，不把V3-09-03整体或真实登录标为PASS；impl/main不在同步范围。Git结果见[当前交接](HANDOFF.md)。

### 目标和既有边界

- “开机自启动”指当前用户登录Windows后自动运行，不是登录前系统服务。默认关闭，只能由用户显式开启；首版入口放控制中心“全局”。安装过程不默认开启，不另造一套独立偏好。
- 沿[A需求§24.2/§8.4](../../10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md#242-自启)与[B模块H08](../../20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)：当前用户、可查询/可逆、固定应用标识DesktopUp；只认有效安装身份和实际安装路径，携带精确--autostart。开发/便携/环境变量临时配置入口不能登记。集中验收改用安装器正式登记的独立StartupReview安装，须由用户主动开启，不将裸out候选登记成日常入口。
- 登录后恢复当前启用海报与托盘，不弹/聚焦控制中心；零海报也不打扰。已有实例时自启请求静默结束、不重复托盘或唤起中心。普通启动、显式打开中心及现有动效/显隐偏好不因本批改变。
- 开关展示实际登记/可识别的系统状态，不只展示数据库意图；写后读回，失败明确提示、不得假显示成功。系统侧关闭后不自动重启用；无法可靠确认的状态显示为未知/需系统设置确认，不伪造“已启用”。不改第三方或其他版本的启动项。

### 实施结果

1. Application/StartupRegistration隔离系统操作和偏好，WindowsStartupRegistration核对安装构建、标记、HKCU64安装位置及文件，拒绝重解析/不完整/临时入口。采用官方支持的[当前用户Run登记](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)，固定DesktopUp，命令为双引号实际根启动器＋精确--autostart，长度最多260字符。不引入需要包清单身份的[StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-26100)。
2. 控制中心“全局 → 开机自启动”按系统登记读回显示；用户修改成功并读回后才保存现有LaunchAtStartupRequested偏好。启动/导入/刷新均不据数据库偏好补写系统项。外部删除登记后显示未登记；其他安装占用不抢占。Windows私有StartupApproved格式不读写，因此UI明确显示“已登记/未登记”，系统是否允许启动由“Windows启动应用”入口查看，不把已登记伪装成系统已允许。
3. AutoStart独立启动意图：零海报也不打开中心；已有实例时静默结束、不转发唤醒。显式打开中心仍使用既有逻辑，普通首启不变。卸载在可取消的内容准备成功后、程序清理之前，仅删除仍精确指向自身的启动项；删除失败或删除后仍读回自身命令则停止卸载，外部命令保留。内容归还实现未改。
4. **升级边界**：当前安装器本来就拒绝原地覆盖，本批没有新增升级器/跨版本自动接管。多版本只实施归属保护；旧安装先关闭或卸载，再由用户在新安装启用。原计划的升级路径维护仍留待真正升级能力，不能记为完成。

### 定向验证与候选

- 启动意图/登记服务/单实例共25项通过：out/startup/targeted01/startup.trx。使用替身验证显式写入、读回失败、外部变化、外来命令、开发入口与长路径；没有写真实Run项。
- tests/Packaging/Test-StartupCleanup.ps1从生产NSIS提取清理块，在独立私有测试注册表键验证无登记/本安装/其他安装3组通过；测试键已清理。
- 最终package02：Release自包含构建0警告/0错误，启动器及NSIS /WX通过；same-package smoke02安装→静默自启参数→重复启动→显式中心→正常退出→保留内容卸载六步通过。真实Run登记前后相同，测试安装已正常卸载，无桌面快捷方式/桌面文件遗留。
- smoke01首次在进程退出码读取处因夹具未保留进程句柄失败；应用日志为正常退出。修正测试夹具后smoke02通过；旧失败记录保留，smoke01安装也已正常卸载。package01元数据的Review存储基路径错误已修订，人工只使用package02。
- 最终安装包：out/startup/package02/DesktopUp-Preview-20261016.903-win-x64-Setup.exe，67,604,153字节，SHA256 `30CBB64D6059F4E5E1C883CD8705BF8BC2409EB88771FB79168D874704010A11`。814项载荷，源绑定见evidence/source-manifest.json；SourceCommit为3c33a68加未提交修改，Verified=false不变。
- scripts/start-startup-review.ps1已在Windows PowerShell 5.1执行只读Status通过。此包使用独立ApplicationId及 `%LOCALAPPDATA%\DesktopUp\StartupReview` 配置、`StartupReview.storage` 收纳根；旧日常配置/文件不变。不开默认自启、不自动注销，不重复已验拖放/菜单/归还矩阵。

### 集中人工记录（2026-10-16）

- **关闭与卸载：USER PASS**，依据用户本轮明确确认，沿用下方第2组范围；不扩展成全部Windows环境认证。
- **开启后真实登录：暂缓／未验**，按用户决定停止本轮验收，不自动安排重启或注销，不用手动参数启动代替真实登录PASS。
- 按用户明确授权，带此未验项同步功能分支并合入开发集成线。这是本次集成边界，不放宽其他功能的默认人工门，也不更新精简发布线。之后可回主线计划，实际实施另按授权。

以下两组定义和入口保留用于追溯，第2组不重复要求验收，第1组待用户方便时再恢复：

入口：`powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\scripts\start-startup-review.ps1" -Action Open`。未安装时打开向导（可自选位置，默认不建桌面快捷方式），安装后重跑同一条打开中心。建议从用户自己的普通PowerShell执行，避免混用安装登记上下文。Status只读；Uninstall只打开本次已核验安装的卸载器，保留配置。

1. **开启与登录启动**：全局中默认未登记，用户开启后显示已登记当前安装；Windows启动应用确认未被系统禁用。可新建一张空海报便于观察，不需测试文件。方便时自行注销/登录或重启，应恢复海报与托盘，不弹控制中心、不重复实例。尚未真实登录则保留未验，手动--autostart只算参数分支测试。
2. **关闭与卸载**：关闭后显示未登记，重开全局仍未登记；再显式开启一次并退出应用，以同一脚本 `-Action Uninstall` 卸载，确认本安装启动项移除、程序卸载。仅检查启动生命周期，不重做文件归还；不动其他安装项。系统禁用只在Windows设置操作，不要求重复注销验证。

整体验收退出条件仍包含真实登录行为闭环；此次用户允许提前集成开发线不等于该条件已满足。关闭与卸载已验，真实登录暂缓未验；原地升级未实现，普通权限全范围、长测/跨机等继续携带。下一主线V3-06真实热度，本轮不实施。

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

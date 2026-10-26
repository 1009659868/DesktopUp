# 热度与拖动已验成果：发布同步

2026-10-26。用户授权同步develop至精简main、打包验证、新建tag与GitHub Pre-release，结束回develop；另明确“先发布已验版本，下一批补可插拔协议”。本轮不增加新模型行为，也不重做已通过的人工验收。

## 源码与范围

- 已验整合：develop@9d3d4d4；209项定向、组合冒烟与人工结果继承[整合记录](ACTIVITY_DRAG_INTEGRATION.md)，不记为本轮重新测试。
- 下一批协议准备：纯文档提交`0aae1ee0f6d2ae34bb26f26218329784a66f3de8`，修正现有模型仍固定调用的描述，补[统一协议计划](../../30_Plans/V3_ACTIVITY_MODEL_PROTOCOL_PLAN.md)。排序／尺寸／目标位置／过渡建议由模型提案，宿主验证、调度与执行；无本轮可插拔已完成结论。
- main非快进合并：`f259bbb2fcac69d8b4b857db958a817facb77ae5`，父提交ecb0c04与0aae1ee。main只保留发布源码、模块、配置、构建打包入口、许可证和README；开发资料／测试／review入口留在develop，不反向合并精简删除。
- 排除Markdown后，新旧main相对对应develop的产品精简差量逐行一致：715行、差异0。Application／Domain／持久化以及本轮拖动调度与热度接线与已验开发线一致；未修改安装事务和卸载安全逻辑。
- main README原位更新当前功能／安装规则与协议边界，历史验证记录继续在开发资料中可查。

## 构建、载荷与安装验证

- 项目SDK10.0.112、NSIS3.12，离线Release/x64普通构建与自包含Preview重建均0警告／0错误；文本门510文件／27项冻结迁移通过。
- 包版本20261026.1，`preview-build.json`准确绑定干净f259bbb，IncludesUncommittedChanges=false；519项编译／载荷输入哈希核对，810项清单声明（含清单自身共811个载荷文件）核对。无开发测试、探针、PDB或用户数据库。
- 最终分发包：`out/releases/main-activity-final01/DesktopUp-Preview-20261026.1-win-x64-Setup.exe`，67,392,961字节。
- Installer SHA256：`B5D1F35A48BD40BBA3C961ACB61C48DD186283CD690A416067E4AF613C48EEE8`。
- SHA256SUMS.txt SHA256：`484E05891F30964C63643AAE08CED7E982D4A5498E439CD77AB847D5AF036452`。
- 安装检查使用相同应用载荷及安装事务源码的独立登记夹具`DesktopUp.ActivityReleaseReview`（20991026.1），只替换登记／入口名称及子进程配置路径，未覆盖日常安装。夹具不是对外发布的二进制安装包，不冒充直接升级了正式身份。
- 安装到独立中文空格路径，810项安装文件摘要通过；安装根启动器实际启动App（PID24732），零海报首次控制中心、schema27初始化通过；coreclr与WinUI从安装目录加载，--exit正常退出，activeResources=0。
- 不注入鼠标、不打开真实用户文件、不新增长时／多屏／真实登录／人工GUI认证。原功能人工通过与本轮安装自动检查分开登记。

## 首次卸载异常与独立复验

**首次卸载未通过，不记全程首轮PASS。**

- 普通卸载第一次返回24；即时证据为DeleteFileW／Windows32，首个失败文件`app/System.Transactions.Local.dll`，PID32372／TID64356，失败后持有者快照Count=0。121项声明文件、安装登记及卸载入口保留，符合停止清理保护。
- 已保存首次报告、marker、日志与文件状态；没有依据认定具体占用者，不能直接与P1/R6锁冲突、历史卸载或升级41判为同因。
- 只读核对后单独执行原卸载器，返回0；同包再次安装后普通卸载首次返回0。没有修改产品、强杀进程、自动重试、跳过失败文件或绕过保护。
- 显式清理前后配置数据库和自建未知文件哈希保持；日常Preview/V3配置、安装登记、Run／StartupApproved与桌面／开始菜单快捷方式前后相同，无剩余DesktopUp／Worker进程。
- 完整限定结果：`out/releases/activity-drag-validation/installed-result.json`明确FirstUninstallPassed=false、ExplicitLaterUninstallExit=0及ReinstallOrdinaryUninstallExit=0；不可只引用顶层Passed掩盖首次异常。
- 首因继续OPEN；成本、P1/R6、历史卸载／升级41和环境余验不因这次发布关闭。

## 清理与证据

- 临时安装已正常卸载，测试登记和仅本轮InstallerActivityReleaseReview锁文件已清理。
- 29份独有配置／报告／夹具来源等逐项ZIP哈希复核后，移除整个`out/releases/activity-drag-install01`；另移除本轮prebuild、最终包build及setup-build可重建输出。
- 可恢复证据：`out/releases/activity-drag-validation/fixture-evidence.zip`，SHA256 `247A3A8ECB2E636B07B25FA8599B6463464F8472D92DA572CD497550663E612B`。分发安装包、载荷、源指纹及构建日志保留；没有桌面样例遗留。
- 本轮审计统一在`out/releases/activity-drag-validation`：package-audit、production-parity、first-uninstall-failure、explicit-cleanup、ordinary-uninstall、installed-result、cleanup-result、daily-before/after、发布三个receipt及日志。out不提交Git。

## Git与预发布回执

- main f259bbb、develop协议文档0aae1ee已推送并ls-remote回读一致；本记录作为后续纯文档提交，不改变已验或打包源码。
- 新增注释标签`preview-20261026.1`，tag对象`55164521fe394bd285f9dc2675cf9eaa368da1fb`，剥离后指向f259bbb，已推送。不移动旧tag。
- [GitHub Pre-release](https://github.com/1009659868/DesktopUp/releases/tag/preview-20261026.1)，ID423869529，发布于2026-10-26T04:52:26Z，draft=false／prerelease=true。
- 仅上传安装包与SHA256SUMS.txt。发布说明只写本次拖动优化、内置热度初步接入及尺度协同；不重复完整功能介绍，不宣称模型可插拔已实现。
- 两个附件服务端摘要及实际下载流SHA256均与本地产物一致；旧preview-20261023.1的tag、说明和附件身份检查未变。
- 最终停留impl/develop；本回执与当前状态同步后由Git日志定位最终文档提交，不将其作为包内SourceCommit。

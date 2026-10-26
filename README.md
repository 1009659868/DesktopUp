# DesktopUp

Windows原生桌面内容收纳工具：多海报、真实文件与快捷方式、原生拖放、目录映射、内容布局与独立外观。当前使用.NET／WinUI，非旧Tauri原型。

## 当前进度与文档

[当前状态与后续工作](Docs/00_Catalog/CURRENT_STATUS.md)统一维护已验功能、分支／发布基线和未完成事项。[文档入口](Docs/README.md)按A → B → C导航；[源码职责](src/README.md)区分现有项目与未来目标。

真实内容基础、架构整理、连续尺度与外框修订、唯一安装／迁移升级、拖动呈现优化，以及真实热度与桌面标准尺度均已验。热度与拖动的209项定向、组合冒烟及普通构建通过，按授权集成至develop；[整合记录](Docs/A_Project/40_Implementation/V3-06/ACTIVITY_DRAG_INTEGRATION.md)记录来源、证据及Git回执。本轮不更新main／安装包／tag。完整模板平台和AI后续推进；历史卸载首因、锁冲突及剩余成本继续保留。旧README时间线见[历史快照](README_HISTORY_20261023.md)，不作为当前任务指令。

开发集成线为impl/develop，后续功能从它派生；impl/main仅保留发布源码／内置模块／配置／构建打包入口与许可证。开发资料和测试不得被发布精简反向删除。Git操作与发布需用户授权，见[分支策略](Docs/Rules/GIT_STRATEGY.md)。

## 安装与数据

同一用户维持一个受管安装和固定卸载登记。新安装器支持原位升级或选择新路径迁移程序，保留配置、收纳内容和自启动设置；无法确认归属或有未完成事务时停止，不猜测删除。安装向导可选择桌面快捷方式。

程序、配置和真实收纳数据分离。默认收纳位置为`%LOCALAPPDATA%\DesktopUp\Storage\<通道>\<配置身份>`，可在中心全局设置迁移；卸载可保留内容或先按原路径归还，未知／不可用来源回系统桌面。新配置零海报、无测试卡片；新增默认基础收纳。

## 构建与运行

在Windows x64实际项目根目录使用PowerShell 5.1或7：

```powershell
& .\scripts\setup.ps1 -ForPackaging
& .\scripts\build.ps1 -Configuration Release
& .\scripts\run.ps1
```

版本与哈希由global.json及scripts/toolchain.json锁定，工具链和NuGet缓存放out，不替换系统SDK。首次准备需联网，准备完成后支持脚本的Offline选项。打包见[打包入口](packaging/preview/README.md)，不直接用缺资源约束的自定义dotnet输出目录。

测试按[影响面规则](Docs/Rules/IMPLEMENTATION_AND_VALIDATION.md)选择；scripts/test.ps1保留全量入口，但不是每次文档／局部变更必跑。运行会触及当前配置，测试应使用隔离配置与样例，不能把旧out路径当作仍存在的产物。

## 参考与许可证

参考DeskBox的Windows／Shell实现实践；直接衍生代码需保留来源并履行GPLv3对应源码义务，不复制DeskBox品牌资产。本项目许可证见[LICENSE](LICENSE)，依赖通知见[THIRD_PARTY_NOTICES](THIRD_PARTY_NOTICES.md)。

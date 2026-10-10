# DeskBox 收纳设计与 DesktopUp 适配记录

日期：2026-10-10。参考快照：`G:/A_/Tools/reference/DeskBox@64f27e1aa0f16867abe20074f35ea05cd7b12ce7`。本记录归 A 层架构参考，不替代 DEC-039 或 C 层任务状态。源码审查不等于运行了 DeskBox 测试；本轮按概念独立实现合同，不复制其实现代码。该参考项目为 GPL-3.0-only；后续若移植代码，须继续维护 THIRD_PARTY_NOTICES 和片段来源记录。

## 1. 收纳首先是文件存储，再是图标呈现

DeskBox 的文件组件通常绑定真实目录。`WidgetViewModel.ItemHydration.cs:14` 的 EnsureFolderBackedConfig 在没有目录时建立受管子目录；`ManagedStoragePathService.cs:33` 提供用户目录下的默认存储与迁移建议。普通拖入落到目录、刷新目录内容，不只是添加一条指向桌面的引用。无目录时的 Reference 策略分支不能概括最终文件组件的正常行为。

DesktopUp 保留稳定 ContentItem、CollectionMembership 和每海报 PosterItem。文件只有一个实际位置，多个海报可以共享它；位置变化不能覆盖各卡片的图标、顺序、图钉和跨度。安装位置、图标资产库、缓存均不是用户收纳文件的存储目录。

## 2. 载荷分类，而非按图标长相分类

| DeskBox 入口 | 实际对象和执行方式 | DesktopUp 首批适配 |
| --- | --- | --- |
| CF_HDROP | 优先采用物理路径；文件、文件夹、已有 .lnk/.url 转入同一文件管线 | 本机文件/目录/快捷方式 Move；.lnk 只移动本体，不移动指向的程序 |
| 文件系统中的 .exe | 本身是物理文件，不因显示应用图标就自动改为应用链接 | 由文件提供方分类和声明能力；不得对已建模的 Application 实体强行搬安装目录 |
| Shell IDList 中受支持的 AppsFolder 应用 | 识别合法 AUMID，生成临时 .lnk，再复制入目录 | 独立能力分支；首批明确未开放，不伪报“应用已剪切” |
| FILEGROUPDESCRIPTOR/FILECONTENTS | 实体化临时文件，再强制 Copy；临时数据须活过提供方 | 后续独立物化预算/寿命合同；首批拒绝，不引用临时路径 |
| 此电脑、回收站等虚拟 Shell 对象 | 不属于合法 AppsFolder AUMID 通路 | 明确不支持物理 Move，不能冒充普通文件 |

锚点（均相对参考仓库）：`src/DeskBox/Helpers/NativeDropTarget.cs:464` 检查载荷，`:1059` AUMID 识别，`:1096` 应用链接，`:1173` 虚拟文件；`Helpers/ShortcutHelper.cs:550` 使用 AppsFolder Shell 身份构造快捷方式。优先物理路径避免同一对象被两种格式重复导入。

## 3. 手势与意图集中裁决

`Helpers/FileDropIntentPolicy.cs:20` 将默认、Ctrl、Shift、Alt/Ctrl+Shift、临时来源和源能力集中裁决；默认设置 Move，也支持 FollowWindows 的同卷 Move/跨卷 Copy。`Views/ContentWidgetWindow.NativeDragDrop.cs:948` 提供右键拖放菜单。`Controls/WidgetContents/FileSurfaceContent.ShortcutDrop.cs:11` 与原生入口复用意图规则，而不是各写一套猜测。

DesktopUp 的差异：

- 默认/Shift 表示 Move，跨卷不偷偷改 Copy；Ctrl 表示 Copy，Alt/Ctrl+Shift 表示 Link。首批只开放 Move，另两种意图显示“尚未支持”，不换成 Move。
- 海报内部仍是已有排布/图钉操作。按住移动即拖，不等待长按计时、不要求“整理模式”。Ctrl/Shift 选择集合与跨外部边界后的修饰键反馈是不同阶段。
- 一次物理手势只交接一次原生所有权；交接后本地松键不能再提交布局。回到来源海报由原生会话完成内部落点，不靠第二次按住。
- 右键菜单、跨海报转移、应用链接及虚拟文件属于后续能力，不借默认回退混入首批。

## 4. 文件执行和结果发布

DeskBox 主链为：

载荷快照 → 意图/落点 → 同名及目标预演 → OrganizerService → FileService → 逐项完成结果 → 历史与目录/UI 更新。

`FileSurfaceContent.xaml.cs:3136` 区分普通源与临时源，`:3255` 协调原生入口、插入位置及冲突；`WidgetViewModel.Operations.cs:57` 归一到 ImportPathsAsync；`OrganizerService.cs:58` 准备并执行计划，`:201` 为同名目标分配可用名称。`FileService.cs:1139` 统一执行；`FileService.ShellTransfer.cs:22` 封装 Shell IFileOperation，`:344` 后检查取消/实际结果。NativeAOT 与普通构建存在不同执行后端，不能把所有路径统称同一个 IFileOperation。

`FileService.TransferProgress.cs:329` 的托管分支同卷优先改名，跨卷复制后清源。`FileTransferSessionRegistry.cs:62` 登记进行中的路径，阻止有冲突的修改/读取；该登记是内存态，不是持久账本。部分完成只更新已完成项，不虚构批次全有或全无。

DesktopUp 应保留单一执行者、受影响路径占用、逐项结果和不覆盖策略；跨卷源清理必须在完整性验证之后。提供方负责解析真实身份、重解析点/目录自包含及权限，纯合同中的路径字符串不能代替这些安全检查。

## 5. 恢复机制不能混为一谈

普通拖入的 `OrganizerService.cs:97` 先执行，`:119` 后写历史。另一个“桌面整理”通道 `DesktopOrganizationTransaction.cs:80` 先保存恢复日志，`:100` 再执行，`:202` 恢复未完成事务；`DesktopOrganizationRecoveryStore.cs:44` 持久化日志。

因此不是 DeskBox 没有恢复，而是普通拖入不能直接借用另一通道的恢复保证。DesktopUp 要将同一预写账本真正接到日常拖入/拖出：先保存源/目标身份、计划、关系读集和执行所有者，再动文件；文件成功而数据库未提交时，以观测事实恢复索引，不盲目搬回或删副本。

拖动取消只取消未发生的动作；若磁盘已经完成，取消不能把事实改为“未移动”。两份都在、身份不符、唯一副本不确定时保留并提示处理；删除海报不删除文件或未完成账本。

## 6. 原生反馈不等于文件完成

`Helpers/NativeDropEffectPolicy.cs:115` 区分视觉反馈与完成 effect：DeskBox 已接管异步搬移时避免再通过 MOVE 让源重复清理。`ContentWidgetWindow.NativeDragDrop.cs:1046` 用入口去重避免 WinUI 与原生 fallback 重复导入。

DesktopUp 不能只复制某个 effect 返回值。必须同时验证源/目标执行者、异步数据寿命、实际落点、逐项完成与清理责任；DragEnd 或排队成功不是文件成功。[Microsoft 的 Shell 拖放场景](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)说明了不同数据格式和传输协议；[IFileOperation 文档](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-performoperations)明确要求另查操作中止状态。

## 7. 适配和测试对应

| DeskBox 可吸收部分 | DesktopUp 必须补齐 | 本轮纯测试方向 |
| --- | --- | --- |
| 目录背书与所有权 | 用户显式选址，Reference 不自动转换；受管实体与视图分离 | 未配置/未知版本/非持久目录拒绝；旧关系保持 |
| 类型及能力分类 | 不把应用身份、虚拟流当物理源；不静默降级 | 物理优先、Move/Copy/Link反馈、能力拒绝 |
| 中央意图 | 沿现手势一次接管，内外拖不能双提交 | 内→外→内、Esc、重复/旧代次回调、隐藏/退出 |
| 同名预演及路径会话 | 全量共享影响、相同物理对象的独立身份、后代引用 | 同路径无操作、同名拒绝、源/目标/关系变化重预演 |
| 逐项执行与恢复日志 | 普通拖放预写；SQLite和磁盘分开；失败不删唯一副本 | 持久意图门、部分完成、提交失败、取消后晚到成功 |
| 模块共享文件列表 | ContentItem 稳定身份、每卡图标和布局独立 | 只移除成功取出的当前卡片，其余引用更新源而不改布局 |

实施与验证以 [第一批合同记录](../40_Implementation/V3-05/MANAGED_STORAGE_AND_DROP_CONTRACT.md) 和 [主工作流 §6.5](../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径) 为准。DeskBox 测试源码只作为反例来源，不转记为本项目 PASS。


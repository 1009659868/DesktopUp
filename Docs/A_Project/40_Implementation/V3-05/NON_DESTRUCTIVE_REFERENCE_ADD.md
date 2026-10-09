# V3-05-04：非破坏性引用添加

日期：2026-10-09。状态：**LOCAL REFERENCE AUTO PASS / USER CONFIRMED**，不是V3-05整体关闭。用户已确认添加可用，以及复用／共享修复、重启保留和选择器退出通过；随后授权05-05／05-06。当前实现入口转[真实显示和使用](REAL_CONTENT_DISPLAY_AND_ACTIONS.md)。下述SQLite13与未接真实打开描述保留05-04交付时的范围。

任务归属：[主工作流 V3-05-04](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#62-v3-05-04-当前实施与验收)。前置：[05-01实体与打开结果合同](CONTENT_ENTITY_AND_OPEN_CONTRACT.md)。产品决定归需求索引 DEC-035，不在本页另建裁决。

## 1. 本轮边界

目标是“引用进来、看得见、关系明确、重启保留”，不是物理收纳或真实打开完成。首次可用范围：本机固定/可移动盘的文件、文件夹、`.lnk`/`.url`本体。通过文件入口选择的`.exe`仍是File，不凭扩展名冒认已安装Application身份。Shell应用、网络、云占位、重解析点及虚拟流此入口明确跳过；对应提供方归05-13，目录映射/枚举归05-02。

不执行Shell、不读取快捷方式目标、不递归扫描目录、不复制/移动/重命名/删除原文件、不写Explorer、不新增第二套布局或状态仓库。真实图标/状态提取归05-05，打开/定位/移除关联归05-06，磁盘执行仍须05-08安全账本。

## 2. DeskBox处理 → DesktopUp适配 → 测试

只读参考仍为 `G:\A_\Tools\reference\DeskBox@64f27e1aa0f16867abe20074f35ea05cd7b12ce7`。本轮复查 `src/DeskBox/Helpers/FileDropIntentPolicy.cs` 和 `Services/FileService.cs` 的快照/存在性检查段；吸收思想，没有复制实现，不继承参考项目PASS。

| DeskBox处理 | 本项目适配 | 对应验证 |
| --- | --- | --- |
| 无映射目标时先判定Reference，与Copy/Move分开 | 明确引用入口；源访问只有元数据读取；实体和海报内布局身份分离 | sentinel内容前后相同；虚拟流即使声明Reference能力也拒绝 |
| 文件快照区分文件夹/快捷方式；目标解析与显示信息分层 | 添加时保留快捷方式本体，不执行/解析其启动参数；名称/类型占位与后续图标分开 | txt/lnk/url/exe/目录/失效路径矩阵 |
| 源枚举有不可用/异常处理 | 本轮不枚举，将逐项失败显示为跳过，不把异常当空集合或清掉旧成员 | 混合输入预览；取消无写入；原关系保留 |
| 参考项目以路径/WidgetItem承载多种状态 | GUID全局内容＋CollectionMembership＋PosterItem；路径仅提供方保守重复提示，不等同原生file ID | 独立重复/显式复用/一海报重复复用no-op/跨海报共享 |
| 单项来源变化需保持操作边界 | 按DEC-035共享修复，确认全部海报，完整读集及一个事务 | 漏读集拒绝、过期拒绝、单revision、重放不重复写 |

## 3. 持久化和迁移

- SQLite **12 → 13**，仅追加 `non-destructive-content-references`，不改旧12条SQL或checksum。
- 新表：`content_head`、`content_items`、`content_collections`、`content_memberships`、`content_poster_bindings`、`content_poster_items`、`content_demo_archive`。关系FK、唯一约束及JSON身份检查；读取另验集合归属和layout成员完全匹配。
- 正式内容与海报布局共用 `SqliteStateCommandRepository` 的连接/事务/receipt/单revision。新增 `StateReadSet.ContentVersion`，不改变旧命令省略该字段时的指纹。
- 迁移前沿用已验证备份机制。升级不生成假ContentItem，不重写DEMO/未知layout。未知未来SQLite仍拒绝，原库保留。
- 首次添加必须单独确认：替换这张海报的DEMO构图，新引用从自动布局开始。旧layout原文（含null）在同事务归档；宏观浏览/网格偏好保留，不把假卡片灌入内容库。未知layout或已有未知CollectionId拒绝转换。
- 已绑定海报追加内容不重建旧placement。手动模式新项排到已有构图/留白之后，保留旧项位置、图钉、尺寸及自动返回意图；复杂约束无法通过验证则整批不应用，不擅自重排旧项。
- 删除海报级联删除其展示关系，不删除共享内容、集合成员或源文件；清理孤儿不在本轮自动执行。

## 4. 预览与修复操作链

### 添加

控制中心“外观”下的“＋ 添加内容…” → 选择一个文件/文件夹后可继续累积，或粘贴多行绝对路径 → 后台元数据检查 → 逐项预览 → 明确独立添加、复用或跳过 → 确认DEMO替换（仅第一次） → 重新检查来源 → CAS单事务提交 → 现有内容端口呈现 → 重启读回。

2026-10-09入口定位补充：用户明确后续主要通过桌面图标拖入、右键新建等直接操作添加内容。上述控制中心按钮当前为生产代码中的阶段性接入/验收入口，不是仅测试模式可用，也不冻结为最终常驻布局。05-07拖入应复用本轮关系、重复处理及事务规则；05-09右键新建另须具备存储目标，不能把“建立引用”误当“创建磁盘文件”。本轮不提前实现或移除这些入口。共享修复能力继续保留；最终更适合放在失效Item的就地提示/上下文操作，具体呈现待相关阶段收敛，不能要求日常添加先进入修复流程。

最多100条输入/批，单张仍受现1000项容量门限制。未命中重复提示的可用项默认独立添加；发现同来源/同打开语义则默认跳过，用户明确选择后再处理。同批引用前面候选时，若前项最终未创建，整批拒绝并要求重新预览，不猜测复用对象。锁定海报不接受新增；先解锁。

无效输入逐项给原因，不产生虚假实体。元数据复查不是文件内容哈希/原生file-ID锁定，不能声称防止所有同路径文件替换；本阶段没有文件执行或破坏性操作。外部变化的主动监听/失效呈现仍归05-05/11。

### 修复共享内容

选择真实Item → “修复所选引用…” → 选择/填写新来源 → 预览所有受影响海报名和ID → 确认统一修复 → 全部海报版本＋ContentVersion校验 → 全部更新或全部不更新。

ContentItem ID保持，Version前进一次，来源/名称/能力更新；PosterItem与placement不换ID、不重排，不改变图钉/尺寸。每张受影响海报推进entityVersion，使可见/隐藏后重建的窗口正确刷新。修复也可显式更换来源，不要求伪造一条“已失效”的扫描事实。确认后新增引用者、删除或修改受影响海报均使旧票据失效。

### 生命周期

复用现原生文件选择器的单STA取消/退出通道和控制中心异步交互所有权；选`.lnk`使用不解引用选项，返回快捷方式本体。依据[Windows文件选择器选项](https://learn.microsoft.com/zh-cn/windows/win32/api/shobjidl_core/ne-shobjidl_core-_fileopendialogoptions)。弹窗/选择器取消不提交；正在退出不接纳迟到结果。此设计仍需本轮原生人工验证，旧BUG-007的PASS不自动转授。

## 5. 边界与暂时限制

- 正式引用走 `ReferenceContentViewSource`，展示真实文件名和类型占位，复用已有构图/滚动/拖动/图钉通道。未提供真实图标；打开端口明确未接，不调用DEMO成功计数或伪造成功事实。
- 已绑定真实引用的海报禁止DEMO换样片，避免只换layout造成孤儿关系。
- 便携v5不携带真实来源/关系。存在绑定时阻止导出及替换导入，并提示使用本机备份；追加旧配置仍可用。本机备份/恢复包含SQLite13全部关系，已有隔离测试。跨机引用隐私/失效说明和新格式另行补齐，不能静默输出假完整包。
- 真实引用海报的“复制配置”暂时拒绝，提示新建后显式复用；未裁决自动复制哪些关联，不能把旧Remap结果冒充真实内容。DEMO复制路径保持。
- 路径提示采用保守精确比较；不自动合并大小写/硬链接/短路径别名，不做源文件重命名追踪。首批弱来源键不替代内容GUID。
- R7未验/延期项、C2联合长测、BUG-001/004/006/026/031/035等沿原携带清单，不在本轮顺便关闭。

## 6. 代码锚点

| 层 | 入口 |
| --- | --- |
| 命令/校验 | [ContentReferenceCommand.cs](../../../../src/DesktopUp.Application/ContentReferenceCommand.cs)、[ContentReferenceIntegrity.cs](../../../../src/DesktopUp.Application/ContentReferenceIntegrity.cs) |
| 数据库 | [SqliteContentRepository.cs](../../../../src/DesktopUp.Infrastructure/SqliteContentRepository.cs)、[SqliteStateStore.cs](../../../../src/DesktopUp.Infrastructure/SqliteStateStore.cs)、[SqliteStateCommandRepository.cs](../../../../src/DesktopUp.Infrastructure/SqliteStateCommandRepository.cs) |
| 来源/显示 | [LocalContentReferenceProbe.cs](../../../../src/DesktopUp.Infrastructure/LocalContentReferenceProbe.cs)、[ReferenceContentViewSource.cs](../../../../src/DesktopUp.Application/ReferenceContentViewSource.cs)、[PosterSpatialRuntime.ContentSource.cs](../../../../src/DesktopUp.App/PosterSpatialRuntime.ContentSource.cs) |
| UI/选择器 | [ControlCenterWindow.References.cs](../../../../src/DesktopUp.App/ControlCenterWindow.References.cs)、[NativeProfilePicker.cs](../../../../src/DesktopUp.Windows/NativeProfilePicker.cs) |
| 测试 | [持久与事务](../../../../tests/DesktopUp.Contracts.Tests/ContentReferencePersistenceTests.cs)、[来源与兼容](../../../../tests/DesktopUp.Contracts.Tests/LocalContentReferenceTests.cs)、[应用托管探针](../../../../src/DesktopUp.App/App.ContentReferenceProbe.cs) |
| 验证/人工入口 | [verify-content-references.ps1](../../../../scripts/verify-content-references.ps1)、[start-content-reference-review.ps1](../../../../scripts/start-content-reference-review.ps1) |

## 7. 验证记录

工作区`G:\A_\Tools\DesktopUp_V2`，分支`impl/poster-redesign-V3`，HEAD `b76eb1a1e212b81065db526fb8e21d64038b4086`加既有和本轮未提交修改；未提交/推送、未修改日常库或用户源文件。SDK10.0.103，Release/x64。

- 35项增量覆盖schema13空库、12升级/失败保留、旧原文/未知layout、DEMO显式确认、跳过no-op、去重/复用、共享修复读集、冲突、SQL故障回滚、重放、布局成员护栏、移除海报、元数据探测、虚拟能力拒绝、手动追加、备份恢复和不可携带格式保护。
- `full-01`/旧TestResults记录保留最初版本假设失败；`full-02`为剩一处旧断言，`full-03`为2538通过的中途快照；不把中途数量写成最终数量。
- candidate01应用探针误路由到旧探针，两进程存在error，不计整体PASS，原包/日志保留。candidate02/final02的2541合同和两应用进程通过；此后修正exe仅文件身份，最终依据candidate03/final03。
- 最终同包 **candidate03 / final03：2541/2541、0失败/跳过，35增量；App及fixture Release/x64构建零警告错误**。App程序集SHA256 `1F2366A607C4720CAEA85A58918973C01C0176593B2257D8D57B32EAEE2891B6`；所有运行产物及src/tests/scripts哈希绑定见`out/v3-05-04/final03/{references,artifacts,sources}.json`，不能只比App DLL而忽略Infrastructure变化。
- 应用添加/复用/修复进程PID45836（12断言），同库重启PID37516（4断言），两者error0、退出码0、退出资源0。原文件sentinel前后相同，B更新、C不改；没有Shell调用。不将托管调用当作原生鼠标/选择器人工通过，不继承R7旧seal。

## 8. 人工验收与最近下一步

收口补充（2026-10-09）：用户明确确认“复用／共享修复、重启保留和选择器退出验证”通过，后确认PID42288正常退出。结合此前添加通过，05-04本机引用体验已确认，继续05-05／05-06。保留candidate03/review03/final03；不修改其自动seal中HumanAccepted=false，也不把本轮确认扩大为真实图标/Shell打开人工通过。以下启动命令和待验措辞是当时历史记录，当前源码已前进，不能绕过指纹校验强行使用旧启动脚本。

2026-10-09已通过严格入口CheckOnly并启动candidate03的独立review03（初始PID42288）；进程有响应、控制中心启动日志正常。数据目录`out/v3-05-04/review03`，样本在其`samples`。用户随后反馈“目前添加内容确实能够添加”：记录基础添加人工通过；不据此扩大为混合失败项、复用/共享修复、同库重启或原生选择器退出全部通过。整体人工验收仍未收清，不修改final03的HumanAccepted=false自动证据。

正常退出后，复用同一目录重启（会先核对全部产物/源码指纹，不覆盖已有验收配置）：

```powershell
pwsh -File ./scripts/start-content-reference-review.ps1 -EvidenceRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-04/final03 -DataRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-04/review03
```

只在独立review目录操作其samples，不动日常内容：

1. A添加note.txt、folder和一个不存在路径：失败项有原因，可用项明确确认后显示文件名；原文件还在。第一次明确提示DEMO替换。
2. B再次添加note.txt，选择复用；A修复该Item为repaired.txt，确认前应列A、B。确认后两张都更新；取消则两张都不变。
3. 调整引用Item布局，正常退出并用同一入口重启；内容、位置/图钉/尺寸和引用不变，C未受影响。打开尚未接入，不把无法启动文件报成已实现失败。
4. 打开“选择文件/文件夹”，不先关闭选择器，从托盘退出；选择器取消且应用正常退出。新入口的人工体验与原生取消记为待验，非旧结论继承。

本步人工通过后，再按主工作流衔接**V3-05-05真图标/名称/类型/失效与缓存 → V3-05-06真实打开与定位/移除关联**。Shell扩展隔离/超时先行，成功统计遵DEC-034。不得提前开展真实移动或把引用闭环当Managed/Mapped完成。

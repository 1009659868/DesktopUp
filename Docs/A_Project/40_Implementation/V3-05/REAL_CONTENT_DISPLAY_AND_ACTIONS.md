# V3-05-05 与 05-06 真实内容显示和使用

日期：2026-10-09。最新为**默认基础收纳CODE / AUTO PASS**（§10）；§8图标效果已获用户正向反馈，§9介绍文字控件仍待人工确认。独立图标上传与卡片构造尚未实现，下一步见工作流§6.4。当前只交付本机非破坏性引用的显示、打开、定位和移除关联，不代表物理收纳、Mapped/Managed集合或全部V3-05完成。

任务归属：[主工作流§6.3](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#63-v3-05-0506-当前实施与验收)。前置 [05-04](NON_DESTRUCTIVE_REFERENCE_ADD.md) 的添加、复用、共享修复、重启保留、选择器退出已获用户确认通过，PID42288已由用户正常退出；其旧包和自动证据不改写。DEC-023手势、DEC-034成功交接计一次、DEC-035共享修复不重新裁决。

## 1. 参考与适配

只读参考 DeskBox `64f27e1` 的 `FileSurfaceContent.Opening.cs` 和 `IconHelper.cs`；采用去重、异步结果、生命周期归属与真实 Shell 图标的思路，没有复制其实现或继承其测试结果。

| DeskBox处理 | DesktopUp适配 | 对应测试 |
| --- | --- | --- |
| 后台打开、正在打开的目标去重、失败反馈 | 同库持久请求收据，目标并发去重，显式交接证据，结果返回到原海报代次 | RealContentUsageTests 的请求重放、目标忙、过期修复、迟到结果、故障原子性 |
| 实际 Shell 图标及缓存 | 单应用受限辅助进程池；现可见集合缓存；解码后有界 BGRA，UI 不运行提取扩展 | RealContentNativeTests 原生图标、异常输出、挂起取消；原 ContentAssetCache/需求与代次测试 |
| 文件打开与文件系统操作分开 | 只接默认 open、资源管理器定位、移除 PosterItem 引用；没有 copy/move/delete | 共享引用移除、原文件 sentinel、单 revision、SQL故障回滚 |
| 表面选择与操作入口 | 沿用单击/双击/Enter、直接单项拖动；Ctrl/Shift多选单独保持，不混成图钉或布局 | ContentSelectionTests，已有激活合同；原生手感本轮人工验收 |

Windows接口依据：[SHGetFileInfoW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)、[ShellExecuteExW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecuteexw)、[SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems)。定位使用完整项目 PIDL、cidl=0，不拼资源管理器命令字符串；COM对象/图标/位图/DC/句柄均按所有权释放。

## 2. 显示与隔离边界

本机固定/可移动盘的文件、目录、快捷方式本体沿用05-04能力准入。不递归枚举，不读取目标内容生成缩略图；网络、云占位、重解析点、虚拟流及 Shell 应用身份仍未接。`.exe`从文件入口加入时仍为 File，不冒认已安装应用身份。

`ReferenceContentRuntime`将正式关系映射到原布局 ItemId，真实名称、类型及可用状态只作显示观察。图标失败保留类型占位；长名在海报内省略、控制中心所选项目悬停显示名称/类型。失效不删关联，可刷新或经原共享修复恢复。当前在首次加载、显式刷新、重新出现及实际操作时重新探测，不冒称外部文件 watcher 已完成。

`DesktopUp.ContentWorker`只接受 Inspect/Icon/Open/Reveal 四类有界 stdio 请求，不读取数据库、不接受任意 verb/参数/DLL。它是抗挂起隔离，不是低权限安全沙箱；Windows Shell自身可能调用已安装扩展，仍在该辅助进程/系统提供方中处理。保留系统安全提示，不使用 runas 或 NOZONECHECKS。打开快捷方式的语义交给 Windows 默认提供方。

| 预算 | 当前实现 |
| --- | --- |
| 单应用辅助进程并行数 / 含排队请求上限 | 4 / 64 |
| Inspect、Icon超时 / Open、Reveal超时 | 5秒 / 30秒，含队列等待 |
| stdio请求 / 响应上限 | 65536 / 400000字符 |
| 图标返回 / 协议允许最大尺寸 | ICONONLY优先请求256×256，保留实际返回尺寸，不开SCALEUP；失败沿旧64×64栅格回退。协议仍最大256×256，严格width×height×4字节 |
| 单海报缓存 | 沿用64条、16MiB、4加载及可见/预取集合限制；解码位图仅保留当前需求 |

辅助进程在完成、失败、超时、取消及应用退出时回收。仅可终止本客户端创建的辅助进程，禁止递归终止进程树，不能终止已启动的用户应用。解码结果再次核对海报会话、来源版本、主题/DPI与可见需求；迟到结果不复活窗口/成员。普通 build 携带 helper；publish 按主包 SelfContained 策略独立发布 helper，不能只在某个验收启动脚本中补文件。

## 3. 操作链

- 单击选择，不打开；双击或控制中心 Enter 打开当前项，键重复不重复发起。按住移动继续原单项布局拖动，不因拖动打开。
- 在海报中Ctrl+单击切换集合，Shift+单击按当前阅读顺序选连续范围，Ctrl+Shift追加范围。按DEC-037，只有修饰键/显式全选形成的集合在日常描边，集合一项也描边；松开修饰键后保留至普通点击/清除，不监听全局键盘。普通点击、双击启动仅保留操作目标，不描边。缩放锚点仍仅编辑模式；多选不顺便启动拖动/双击，普通拖动回到单项，不声称群组拖出/跨海报搬运已完成。
- 右键已选项保留多选集合；“打开当前项”“定位”仅作用于右键/当前项，避免不经确认批量启动外部程序。“从此海报移除 N 项引用”明确批量范围，确认后一起提交。控制中心键盘操作区 Ctrl+A全选，Delete进入同一移除确认，Esc清除选择；原方向键微调、Shift+方向键调整跨度不改义，多选时要求先选单项精调。桌面海报不通过全局键盘钩子截获其他应用按键。
- 资源管理器定位不计有效打开。移除关联只删本海报 PosterItem 与对应 placement，共享 ContentItem、集合成员、其他海报和原文件保持。空海报保留绑定。减少成员后尾部顺序图钉压回合法范围、相对顺序不变，存活项尺寸/手动位置保持，休眠自动配置一并校验。
- 对话框确认捕获目标海报和完整内容/海报版本，来源变化或并发修改拒绝旧提交；不会把新的选择偷偷替换进已确认批次。控制中心自身确认弹窗持有交互保护，但不会将自己的合法提交再次拦住。

## 4. 打开事实与数据库迁移

SQLite **13→14**只追加 `content-opening-receipts`；保留旧迁移 SQL/checksum、备份和未知未来版本拒绝。`content_open_requests`记录 ActivationId、不可变请求、目标去重和终态；`content_open_facts`以相同 ActivationId唯一保存成功事实。它们复用原库/锁/事务，不另建数据库，不推进海报配置 revision。

操作顺序：验证当前关系 → 持久认领一次请求 → 再核对关系版本 → 辅助进程默认 open → 验证提供方明确证据 → 原子保存终态和最多一条事实 → 原窗口代次仍有效才更新反馈。失败、Handled、取消、超时不记成功。成功表示 Windows/提供方已接收，不代表目标程序已经加载完成，也尚未接入 V3-06 的真实热度统计消费者。

进程异常时不能把外部启动与 SQLite 组成分布式原子事务。若 Shell 已启动但回执未取得/落盘，状态是“是否交接未知”；不自动重试、不捏造成功。重启仅将中断请求退休为已处理，不重放外部动作；已持久成功事实不会重复写。用户日后新的主动操作使用新 ActivationId，不被旧墓碑永久禁用。

## 5. 代码和验证入口

- 模型/命令：`ContentSelection.cs`、`LocalContentProtocol.cs`、`ReferenceContentRuntime.cs`、`ContentDetachCommand.cs`、原 `ContentOpenContract.cs`。
- 基础设施：`DesktopUp.ContentWorker/Program.cs`、`LocalContentWorkerClient.cs`、`SqliteContentOpenJournal.cs`、`SqliteContentRepository.cs`及schema14。
- UI接线：`PosterVisualWindow.RealContent/ContentSelection/ContentAssets/ContentMenu`、`PosterSpatialRuntime.RealContent/ContentSource`、`ControlCenterWindow.References/ContentLayout/UiCWorkspace`。
- 修订策略：`ContentReferencePreviewPolicy`只列活跃关系/本批前项；`ContentCardPresentationPolicy`统一真实图片区和单/多选反馈，原UI负责绘制，不改变布局求解器。
- 测试：RealContentUsageTests、RealContentNativeTests、ContentSelectionTests、ContentUsageRefinementTests；自建 `DesktopUp.ContentFixture`仅测试使用，不能随app分发。
- 同包自动验证：`scripts/verify-content-usage.ps1`；独立人工入口：`scripts/start-content-usage-review.ps1`。不重新解释05-04旧seal里的 ShellExecuted=false。

## 6. 验证记录与人工出口

本节为candidate02当时证据与人工清单，保留不改写；当前修订入口转§7。旧严格启动命令不应用于修订后的源码。

工作区 `G:\A_\Tools\DesktopUp_V2`，分支 `impl/poster-redesign-V3`，HEAD `b76eb1a1`加既有与本轮修改；无Git提交/推送。05-04 candidate03、review03、final03及本轮中间结果保持原样。

- 最终同包 **out/v3-05-05-06/candidate02 + final02**：2573/2573，零失败/跳过；本轮32增量（20使用/持久化、8原生worker、4选择）。App/fixture Release x64构建零警告错误。
- 应用进程48284完成17断言（真实图标到达两个窗口、追加/全选/单revision移除、共享修复、源sentinel不变、中心打开），同库重启33696完成5断言；错误0、退出码0、退出资源0。两者均是托管探针，不是原生鼠标人工通过。
- 原生测试包括真实 Shell 图标、中文名称、失效来源拒绝、自建无UI程序的真实成功交接、崩溃/超量输出/非法JSON/8请求挂起退出。另有模拟提供方成功回执先到达、取消与helper退出竞态的专门测试，不将该模拟回执说成真实Shell启动。
- `final02/{content-usage,artifacts,sources}.json`绑定全产物及src/tests/scripts指纹。App DLL为`172B68D7697F8E4680C81B43F81019A420863EBD2984E37438D6EF4483EDA9AA`；candidate01的App DLL恰好相同，但Infrastructure不同，不能只比主程序集。candidate01/final01的2572只是前一检查点。
- 常规 framework-dependent publish 已成功，发布目录 helper 可启动且拒绝非法协议（退出码2）。本轮未新制作/验收自包含安装器，不继承旧安装器的兼容结论。
- `NativeInput=false`、`HumanAccepted=false`、`UserFiles=false`。`ShellExecuted=true`仅指原生测试中自建无UI程序；App探针不打开文件，也未自动弹资源管理器。未知真实显示器/DPI等仍携带，未以自动证据补记人工PASS。

独立人工入口（新目录，不覆盖05-04旧库；验证源/全产物指纹后准备引用样本再打开）：

```powershell
pwsh -File ./scripts/start-content-usage-review.ps1 -EvidenceRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/final02 -DataRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/review02
```

需要同库重启时使用同一命令。已存在review必须绑定该候选，源码变化则先重建/重新验证，不跳过守卫。

人工场景已于2026-10-09通过严格CheckOnly并打开：review02，初始PID28060，三海报Ready、中心显示，启动无error。A/B预置共享`reference-sentinels/repaired.txt`，额外可添加样本在`samples`；准备过程仍为本候选托管探针，未计为用户通过。当前等待下列人工反馈，暂不推进05-02。

人工清单：

1. 文件/目录/快捷方式显示真实图标和名称；长名不撑破布局，来源缺失仍保留卡片、刷新/修复后恢复。选择/双击/单项拖动不混淆，Enter不因长按重复打开。
2. Ctrl/Shift选择可见，右键保留集合；只移除指定海报所选引用，另一张共享海报和原文件仍在。清空后可继续添加，重启保持关系。
3. 定位打开资源管理器并选中来源，不把定位当打开次数；失效来源不导致海报/中心退出。只用本次独立夹具，不要求操作用户文件。
4. 连续刷新/隐藏/显示、中心关闭/重开；操作未完成时从托盘退出，应用与自有worker可退出，已启动的外部程序不被结束。真实多显示器/长时资源仍按R7携带项，不补填本轮PASS。

最近下一步先收本轮人工结果；通过后按原顺序进入 **05-02目录映射 → 05-03受管集合 → 05-08文件账本/恢复 → 05-07真实拖放执行**。05-07无变更的拖放解析/预览可以先准备，但不能以引用闭环越过文件安全门。原控制中心添加/修复按钮仍是阶段性入口；用户指定的桌面图标拖入及右键新建分别归05-07/09。

## 7. 人工反馈后三项修订（2026-10-09）

历史检查点：candidate05反馈后的选择范围澄清和无底板/大图提取转§8；本节原自动证据不改写，也不作为当前规则。

用户在review02反馈：图标过小、完全移除后仍列复用、单选没有描边；其余已测试项通过且符合预期。PID28060已由用户正常退出，candidate02/final02/review02及验收配置保留。该反馈不要求重做已通过项，也不将未逐项说明的同库重启、在途动作退出补记为新增人工证明。当前状态：**修订CODE / AUTO PASS，针对性USER PENDING**。

| 问题与原因 | 修订及边界 |
| --- | --- |
| 真实图标仍受旧DEMO的IconSizeDip小图标上限约束 | 真实图像/加载占位按卡片可用图片区最大比例显示，名称区及固定选择边框余量不占用；默认Contain不变形不裁切，已有显式Cover保持。无需改网格、宏观尺度或热度模型。当前Shell图源仍64×64，高清提取不冒称已完成。 |
| 预览扫描全部ContentItems，连无活跃PosterItem的历史记录也列入复用 | 保留底层记录、成员与打开事实；仅活跃海报关系提供日常复用候选，并显示海报名。本批重复路径仍给显式复用前项选项，须先添加该前项。无候选默认独立添加，有候选仍保守默认跳过；不自动合并身份，不垃圾回收。 |
| 日常只对多选描边，单选沿旧DEC-026隐藏 | 按DEC-036，DEMO/真实Item单选与多选均描边；缩放锚点仍仅编辑模式，选择不改卡片外框/命中/拖动手势。Ctrl切回一个Item仍描边，清除后消失。 |

新的独立添加创建新ContentItem身份，不继承已无引用的旧身份/打开历史；仍被另一海报使用时允许用户明确复用共享实体，原共享修复合同不变。这里只修正常预览的提示策略，底层显式Reuse命令及完整CAS没有放松。

验证与留痕：

- 8项`ContentUsageRefinementTests`增量：最后引用移除/重加、其他海报仍引用、本批重复、不同来源/预览无写入、三个图片区尺寸、单选/切换/清除反馈。
- 最终 **out/v3-05-05-06/candidate05 + final05**：**2581/2581**，无失败/跳过；App与fixture Release/x64构建零警告错误。添加进程44172共23断言、同库重启49284共11断言，错误0、退出码0、跟踪资源0。
- 新托管窗口探针验证真实Image布局尺寸、单选边框和另一海报选择隔离；添加/移除后无引用历史默认独立、共享候选列A/B，重启重复验证。不是原生鼠标或像素外观验收。原生测试仍仅打开自建无UI程序，原文件sentinel未改。
- `final05/{content-usage,artifacts,sources}.json`绑定源与完整候选；App DLL：`DCD01314109EF54B679247A6233F7500E89B962FAB042B8FBA01E46EADBA91EE`。`NativeInput=false`、`HumanAccepted=false`、`UserFiles=false`；Git未提交/推送。
- `final03`保留2581合同通过和窗口检查失败，不发PASS seal。`diagnostic04`读回：请求110×87，实际87×87，Uniform，scale1；原探针错误要求正方形图像拉伸占满矩形。修正为按源宽高比计算期望，加半物理像素布局取整容差，且仍检查请求框尺寸；未为迎合测试更改生产缩放策略。candidate03/04与失败日志保持原样。

独立人工修订入口（与旧review02分开）：

严格CheckOnly及准备探针通过，已打开review05：**PID18492**，三个海报Ready、控制中心显示、启动无error。准备进程37612已正常退出；此启动不计人工通过。

```powershell
pwsh -File ./scripts/start-content-usage-review.ps1 -EvidenceRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/final05 -DataRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/review05
```

针对性复验：①真实文件/快捷方式图标按比例占满可用图片区，名称不被盖住；②单选有描边，Ctrl/Shift多选与清除一致；③将仅在一张海报出现的样本添加→移除→再添加，默认独立添加且无历史复用项。A/B预置的是共享repaired.txt，只移除A仍应提示来自B的复用；不能用此共享样本当“全部引用移除”的反例。其余已确认项不强制重做；同库重启可用以上同一命令。

最近下一步：收三项复验及必要的重启/生命周期尾项，再收口05-05／06；之后按工作流05-02→05-03→05-08→05-07，不在本轮自动推进目录映射或物理收纳。

## 8. 修饰键选择与无底板高清图标（2026-10-09）

用户澄清普通点击/双击不要描边，只在Ctrl/Shift选择中显示；要求真实Item没有背景板，并指出放大后模糊。另要求单卡上传图标和从图标/真实路径构造卡片，明确只作用于当前海报卡片。原review05/PID18492已正常退出，原包与配置保留。DEC-037为本轮需求权威，不能继续按§7把日常单击画成选中边框。

实现：

- `ContentSelection.ShowHighlight`区分选择身份与显式集合反馈。Ctrl/Shift形成的单项/多项、Ctrl回到一项、Prune仍保留描边；普通点击/双击目标、取消及最后一项移除无日常描边。编辑态反馈保留；中心Ctrl+A全选走同一显式反馈。瞬态字段不写数据库。
- 真实Item卡片背景为null，未描边时边框为0。`ApplyTemplateCardPaint`拒绝场景再次绘制真实Item底板，模板动画/刷新不把它画回。名称、图标原生透明度、逻辑占位/拖动命中及辅助标记保留；不把全部透明像素改为点击穿透。DEMO实验卡片样式保留，与正式对象分开。
- `ContentWorker/LargeShellIcon.cs`在现有受限STA辅助进程调用`IShellItemImageFactory.GetImage`，ICONONLY请求最大256×256，不取文档缩略图、不加背景、不启SCALEUP。校验实际位图尺寸后读取有界BGRA并释放COM/bitmap/DC；失败回原SHGetFileInfo/遮罩透明度栅格。应用并行4/队列64/图标5秒/256最大维度/400000响应字符预算未扩大。
- 根因是原流程常规Shell图标再Rasterize为64后放大，64像素缓存不等于64像素真实细节。新流程取已有较大资源，不能让只有低清图标的来源自动变高清；较大跨度/高DPI仍受256上限。自定义高清素材属于下一步，不冒称已可上传。

接口依据：[GetImage尺寸及ICONONLY/RESIZETOFIT](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage)、[GetDIBits读回和释放边界](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-getdibits)。接口IID/签名还与本机Windows SDK头文件核对。

验证：

- `icon-refinement/icons-targeted.trx`20项通过：新选择7项、原选择4项、原生worker9项。其中新ICO夹具有16/32/48红色小帧和256青色单像素细节大帧；真实Shell返回256大帧、透明边缘及单像素差异，源文件字节未变，证明不是只调大输出画布。
- 最终`out/v3-05-05-06/candidate06 + final06`：**2589/2589**，0失败/跳过；App/fixture构建0警告错误。应用2256（添加）26断言、38008（重启）14断言，error/退出码/退出资源均0。托管窗口检查普通单击无描边、Ctrl单项有描边、底板为null、图像等比与另一海报选择隔离；不替代肉眼/原生输入验收。
- App DLL `35ADC911546AD500FA117F14FE98EB3CB821FFAA47625AB63844139545B53614`，完整源/产物指纹在final06绑定。旧candidate05/final05与更早包均保留；未Git提交/推送，未改用户来源或安装器。

独立人工入口：

严格CheckOnly和独立准备通过，review06已打开：**PID41336**，三海报Ready、控制中心已显示、启动无error；未计为用户通过。原review05不覆盖。

```powershell
pwsh -File ./scripts/start-content-usage-review.ps1 -EvidenceRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/final06 -DataRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/review06
```

复验普通点击/双击无描边，Ctrl/Shift单项和多项有描边；真实图标无底板，名称仍可读；默认文件与原快捷方式的清晰度。源图自身没有大尺寸版本时仍可能模糊，不据此捏造无损增强。原移除后默认独立添加逻辑不变。

最近下一步：按[工作流§6.4](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#64-卡片独立图标与构造入口下一步)先05-05-V1资产/迁移，再V2当前卡片更换/恢复默认、05-04-V3构造入口与重启/备份验证；这里的设计记录不是三个步骤已经实施。完成后再回05-02/03。

## 9. 模板内容介绍区（2026-10-09）

用户对candidate06效果给出正向反馈，追加夜航/深夜书桌的副标题、大标题与数量区如何修改/关闭。DEC-038确认基础收纳加可关闭装饰的方向。本轮以文字区为小范围修订，不进入整套模板重构或05-05-V1。开工时未发现DesktopUp进程，旧candidate06/review06保留。

### 操作与边界

- 控制中心 → 外观 → 海报模板样片夹 → **内容介绍文字**。一个“显示内容介绍”开关控制整区，关闭后顶部占位也收回；按海报与模板分别保存，切换回来保持，旧配置默认不变。
- 副标题可以改写或清空，Enter/离开输入框保存，Esc取消当前草稿；大标题使用“修改海报名称…”入口，名称仍不能留空。数量由真实内容自动生成，不设可造成不一致的手填数字。
- 顶部标题栏、收展/更多按钮、图标名称、内容排布和真实引用不由此开关删除；原“显示标题”仍保持其既有含义。Rain云属于唯一标题，没有额外文字区，本次不暴露无效开关。
- “留一份自己的”把当前介绍区选择作为新模板的Heading默认值；不复制海报名称/副标题/计数。介绍文字的内容属于海报，显隐偏好属于当前模板实例。
- 本轮关闭介绍区可恢复基础内容视口，不承诺把灯具、云形、材质/边距等所有装饰同时还原为基础收纳。完整可关闭装饰及基础等价需后续模板评审，不把关闭动效误称删除场景。

### 实现与验证

`Domain/TemplateHeadingVisibility`保存有界不可变逐模板覆盖，`PosterPresentationPolicy`仅在显式编辑时写presentation v7（SQLite仍14）；旧v1～6不自动重写，未知/错误v7只读。旧调用者无关修改保留覆盖；显式删除已有模板键拒绝。`ResolvedPosterPresentation`共用介绍区高度、视口度量/最小尺寸；结构变化不走纯重涂快捷通道。`PortableTemplateMerge`在同ID异定义导入时同步重映射介绍区偏好，不能作用到本机原模板。

`ControlCenterWindow.ContentHeading`复用原设置草稿/冲突/CAS/自动保存，XAML入口收纳在模板夹中。副标题复用原海报字段和验证。`ContentHeadingTests`新增16例（含同文件事务部分）：默认/按模板隔离、占位、旧写者保留、非法/未来版本、云标题分离、同ID导入映射、取消/单revision/重放/重启/SQL失败和冲突。App真实控件事件驱动开关，读回Grid行、实际Canvas位置和已发布Item命中，连续关/开/关并重启，未合成原生鼠标。

- 唯一新证据：`out/v3-05-05-06/candidate07`＋`final07`；App/fixture Release x64构建0警告错误，**2605/2605合同通过**，无失败/跳过。
- 添加进程46624，33断言；重启进程46692，19断言；error/退出码/ActiveResources均0。A的文字开关不改B、内容排布或真实关系；正常/隐藏状态实际绘制及输入坐标一致。
- App DLL SHA256 `4DADCAE5E5C6B3B848B195CE7322869FB5116192C5DF223BEEEDA9039CC8FEB7`；final07绑定源码/脚本与全部候选文件。旧证据不覆盖，不提交推送Git。
- 严格CheckOnly与独立准备通过后打开`review07`，**PID42348**。人工尚待确认；A预置介绍区隐藏，可在上述入口开/关、编辑副标题、切换夜航/书桌后查看独立记忆。不会迁移或覆盖review06中的用户测试文件/配置。

```powershell
pwsh -File ./scripts/start-content-usage-review.ps1 -EvidenceRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/final07 -DataRoot G:/A_/Tools/DesktopUp_V2/out/v3-05-05-06/review07
```

人工仅复验本次入口、文字/留白与切模板记忆；不要求重做已通过的引用流程。下一步仍为[05-05-V1：独立图标资产与安全迁移](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#64-卡片独立图标与构造入口下一步)，该资产/UI/构造链当前未实施。

## 10. 默认基础收纳（2026-10-09）

用户明确要求默认海报模板为“基础收纳”，并允许本轮主动关闭DesktopUp。已向旧candidate07/PID42348发送程序自身`--exit`请求，正常退出且资源归零；旧包/验收配置保留，未强制终止。下方证据不构成§9人工验收。

- 空配置首次创建改用`PosterConfigurationFactory.NewBasicBox`，与托盘/控制中心新增共用基础收纳默认；初始名称为“我的收纳”、副标题为空，原初始位置/尺寸计算不变。无内容介绍区或场景灯光覆盖。
- `packaging/preview/package-settings.json`同步为`defaultTemplate: basic-box`、`backgroundOpacityPercent: null`；仍允许显式打包覆盖（如lamp/25）。旧安装包不会被追溯修改，本轮未重打安装器。
- 只作用于新建入口，已有海报及重启、复制、导入不强制换模板；不迁移数据库，不修改用户文件。未Git提交/推送。下一步仍为工作流§6.4的05-05-V1，未提前接入自定义图标。

实现锚点：`PosterVisualWindow.CreateDefaultPoster`、既有`PosterSpatialRuntime.ControlCenter`新建入口和`DistributionBuild.ApplyCreationDefaults`；新增`PosterCreationDefaultsTests`两例，验证基础收纳无介绍区/场景覆盖、Preview配置同源且ApplyToNew保真。

验证：

- 独立App `out/default-basic/candidate01/app` Release x64构建0警告/错误，DLL SHA256 `D9D9A6212EEE16BEA735505F8861FDCC87B61AB3AA75A8AA9A4F3EC387D22C1E`。
- 全量复跑`out/default-basic/final02/contracts/contracts.trx`：**2607/2607**，0失败/跳过。首跑final01为2606通过/1失败：既有Shell成功交接测试在清理临时helper时遇到文件占用（只等待100ms后删除的时序竞争），不是默认值断言失败；原失败证据保留，未扩大范围改动该测试。
- 全新隔离root `out/default-basic/fresh01`：托管真实托盘命令PID46908通过12场景；同库重启PID37112通过全目录/设置指纹保持。两个进程均正常退出，错误日志0、退出资源0。只读SQLite断言首次`poster-main`为basic-box/空副标题，新建后主动改为night-ops的海报重启仍为night-ops，隐藏期间新增仍为basic-box。日志/数据库/重启指纹均保留在此root。
- 打包脚本CheckOnly默认basic-box/null与显式lamp/25覆盖均通过；未执行安装器构建/安装或跨机验收，托管命令验证不冒称人工原生输入通过。当前无DesktopUp驻留进程。

## 11. Git同步检查点（2026-10-09）

按本次用户授权，源码`d8a1fda`与相邻文档提交保存当前状态，推送目标仅`origin/impl/poster-redesign-V3`。同步准备清理12个新增文件末尾空白，未改功能；最终2607合同及Release x64构建通过，绑定与保留范围见[交接](../V3-04/HANDOFF.md#2026-10-09-git同步检查点)和[验证](../V3-04/VERIFICATION.md#2026-10-09-git提交前复验)。远端结果以实际Git核对，不把文档描述作为推送成功证据。

§10及更早“未提交/未推送”保留为实施阶段历史。本轮不运行新人工包、不上传配置/日志，不新增人工PASS；下一步仍05-05-V1独立图标资产与安全迁移。

## 12. 独立图标资产接续（2026-10-10）

05-05-V1底层已完成并通过自动门，记录见[独立图标资产与安全迁移](CONTENT_ICON_ASSETS.md)。SQLite15、单卡引用与同库像素、隔离静态图片解码、原缓存和生产显示已接，2654合同及真实应用添加/重启通过。按已确认DEC-039，下一步先[工作流§6.5剪切式拖放核心](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径)合同/恢复与双向稳定验收；V2上传/预览/恢复默认控件和V3构造在该出口后继续，尚未实现。本页旧“资产未实现/SQLite14/下一步V2”等仅限原日期范围，介绍文字人工待验和其他携带项不变。无Git操作，不覆盖旧包/库或失败记录。

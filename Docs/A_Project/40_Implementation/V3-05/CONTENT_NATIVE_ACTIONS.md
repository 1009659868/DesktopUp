# V3-05 真实内容基础操作与目录映射

2026-10-14 Git检查点：用户授权提交并推送当前功能分支，源码/测试/入口 `8ded3aa`，本文件与交接随相邻文档提交保存。仅目标 `origin/impl/05-content-native-actions`，**暂不合入impl/develop**；不上传out/配置/用户内容。N4仍USER PENDING，Git同步不改变下列验收与失败记录。本次仅复核已有证据及差异格式，不新增实现、不重复跑历史全量；下文“无Git提交/推送”为各次实施时的状态。

**当前：N4反馈修订后待人工复验**。candidate01的属性/原生Item/目录菜单被用户报告全部失败，且确认式新建/独立改名窗口不符合预期。下方N1～N3自动记录保留，不是人工PASS。最新修订入口为candidate02/review02，详情见末节。

2026-10-14，功能分支 `impl/05-content-native-actions`，来源 `impl/develop@475d8ba`。05-04-V3构造卡片延期，不制作。用户授权补齐内容右键及空白区右键的真实文件能力；既有拖入/拖出、增量呈现与外部悬停让位结果保留。

## 用户结果与顺序

1. 内容右键去掉“刷新来源状态与图标”“从此海报移除X项引用”“重做调整”（用户称“重新调整”）；保留打开/定位及原有必要编排入口，增加重命名、属性和Windows原生菜单。
2. 空白区右键新增“新建”与当前目录的属性/原生菜单；先支持文件夹、文本文件、快捷方式，其他注册类型通过Windows目录原生菜单提供。不是重新制作自定义卡片。
3. 目录映射：选择已有本机目录，展示直接子项并同步外部变化；不搬文件。用户已确认**新建独立映射海报**，不清空或替换当前海报。解绑/删除海报只删索引，不删目录。
4. 本分支集中构建、定向/必要安全验证和一次人工交付；不按内部方法逐项验收，不自动合回/推送。

## DeskBox参考 → 本项目适配

- `Controls/FileItemMenuBuilder.cs`：按对象能力构造菜单。本项目复用已发布Item身份/选择和菜单版本门，不以显示名称猜目标。
- `Helpers/ShellContextMenuProxy.cs`、`native/deskbox-thumbnail-proxy/src/main.rs`：独立STA进程、隐藏owner、IContextMenu2/3消息转发、原生命令执行。本项目沿独立内容辅助进程边界，不把第三方Shell扩展加载到WinUI主进程。
- `Helpers/ShellContextMenuHelper.cs`：使用系统属性。本项目属性和原生菜单不记为有效打开事实；打开统计继续原合同。
- 目录映射不等于受管收纳。Borrowed目录不写所有权声明、不进入全局收纳位置迁移、不因为删除海报删除文件；保留本项目单一有效卡片引用，重叠目录/已收纳对象冲突须明确拒绝或跳过，不静默抢占。

## 必要边界

- 新建/重命名不覆盖同名目标；空名、保留名、路径分隔、设备/网络路径与未开放重解析点按能力拒绝。改名保持卡片身份、图标覆盖和布局，不整组重载。
- 菜单持有明确海报/内容版本，操作开始前重验；成功后按实际文件状态对账，Shell取消/失败不假记成功，不自动重放任意Shell命令。
- 原生菜单选择可能由Windows/扩展执行移动、删除等；应用不能二次执行。保留系统冲突/权限提示，原生菜单不是绕过底层单一归属或执行不受限导入脚本的接口。
- 属性/菜单退出、取消、辅助进程故障需释放租约，不影响其他海报；文件操作只使用隔离自建数据验证。
- 尚未验证的网络/云/虚拟Shell提供方及多目录多选菜单不冒称支持；普通桌面点击穿透不恢复。

## 当前进度

用户已确认旧hover02候选（初始PID36880）正常退出；本轮进程检查未发现DesktopUp。旧包/配置未覆盖，未操作用户桌面文件。

## 分批与当前实施日志（2026-10-14）

| 批次 | 内容 | 当前状态 |
| --- | --- | --- |
| N1 | 内容菜单减法、重命名、属性、隔离Shell菜单；空白区右键基础路由与目录菜单 | 源码/Release构建通过；定向自动证据通过，实际原生界面未验 |
| N2 | 空白区新建文件夹／文本文件／快捷方式，复用选定收纳位置、目录归属和单一引用 | 源码与定向100项通过；独立Release开发构建，实际界面/退出组合留集中验收 |
| N3 | 独立Borrowed海报、直接子项同步、目录目标拖入/落点、失联/恢复 | CODE/AUTO完成；保留同名替代拒绝与不完整扫描不清理边界 |
| N4 | 汇总候选集中验收：菜单/新建/映射/拖放与退出组合 | candidate01收到失败反馈，修订candidate02待人工复验；不逐小步验收 |

N1实现：`ContentRenameService`使用schema22追加式操作收据，原21及之前的迁移定义/校验和不改。保存改名意图后执行同目录、文件身份匹配、禁止覆盖的句柄改名；同步所有已知子项路径，保留卡片ID/自定义图标/布局。普通失败按确切身份安全回退，崩溃后只观察路径和身份更新索引，不重放改名、不增加用户“恢复任务”。名称窗口期间对象被替换则拒绝。真实磁盘无法核实的结果保留，不宣称文件系统与SQLite具备跨资源原子事务。

`ShellUiClient`独立进程、15秒准备/10分钟交互预算、有界结果协议；仅终止本次辅助进程，绝不结束Explorer或被打开的应用。Item/目录IContextMenu2/3和属性页留在worker；尚未覆盖的第三方扩展/系统“新建”宿主行为属于待验，不承接DeskBox的测试结论。

保留既有输入/原生拖放：右键复用已发布命中快照、按下/松开区域与版本检查，单独开放ContentViewport；左键框选不改。文件操作仅保护当前海报和实际受影响的收展组，不把所有海报全局锁住。菜单后最多刷新所选卡片，不再整组刷新图标。

本轮Windows界面验证受工具可用性限制：已阅读computer-use技能，但本会话没有其必需的node_repl入口；没有绕过该入口自动点击用户桌面。实际菜单弹出/属性/取消/托盘退出为**未验**。当前目录 `out/native-actions-dev/app/` 只是构建开发包，不是集中人工验收交付；不要覆盖旧验收目录或将它标记通过。

### 本轮自动证据与缺口

- Release App/worker构建成功，最终0 warning/0 error；输出 `out/native-actions-dev/app/`。没有启动覆盖旧验收配置，不是安装包验证。
- `out/native-actions-dev/results/native-actions-targeted.trx`：首次49项中3项失败（改名目录句柄共享冲突），已修正，不删除失败证据。
- `native-actions-targeted-02.trx`：78/78通过。
- `native-actions-final.trx`：受schema升级影响的19类测试共416项，415通过/1失败；失败是旧测试把22当作未来版本，改为23，安全断言保留。
- `native-actions-final-fix.trx`：最终针对新增功能、该持久化类、增量渲染及右键路由复跑104/104通过。未重跑其他未变测试类，不宣称执行了历史全量。覆盖受管/旧引用/已知子项改名、冲突不覆盖、数据库失败回退、崩溃观察不重放、大小写、对象替换、schema21→22、worker身份不符拒绝及租约清理、单卡刷新不影响邻居。
- N1批次结束时的限制：原生系统菜单/属性的实际弹出、原生New扩展、第三方扩展、取消退出、显示器/DPI及长测仍未验；当时N2/N3未制作。N2最新状态见下节，原生界面未验不因代码构建关闭。

## N2：空白区新建（2026-10-14）

### DeskBox如何处理 → 本项目适配 → 测试

- DeskBox `Controls/WidgetContents/FileSurfaceContent.SelectionAndMenus.cs` 的 `CreateFolderInMappedLocationAsync` 在确定目录中新建、选择可用名称、刷新并改名；`FileSurfaceContent.ShortcutDrop.cs` 区分文件/目录、产生不冲突的 `.lnk`，不把目标本体当快捷方式搬移。本项目不复制其整组刷新实现，也不把“只在映射目录新建”限制套到普通受管海报。
- 普通海报空白右键 → 新建 → 文件夹／文本文档／快捷方式。名称窗口在海报附近；快捷方式窗口可选择文件或文件夹。默认名称自动避让，用户明确输入的同名名称拒绝覆盖。使用全局当前收纳位置；不恢复控制中心“添加引用”预览，不进入构造卡片。
- `ContentCreationService` 先核对海报/内容版本、收纳声明和目录身份；在收纳根下唯一 `.desktopup-new-<id>` 目录生成空目录、空文本或真实Shell Link，再复用 `DirectTransferPreparation` → `SqliteTransferJournal` → `AutomaticTransferCompletion`，同时接入受管关系和当前海报卡片。没有第二套布局写入或全组图标刷新，新卡片按已有新增布局规则加入；本批不新增“在右键像素点精确落位”的合同。
- `.lnk` 由系统Shell Link在专用STA上序列化至内存，再以 `CreateNew` 写入；不调用Resolve、打开目标或移动原文件。仅已有普通本机文件/目录，未开放网络、虚拟Shell、参数化命令或任意脚本卡片。
- 生成物与暂存目录清理都核对身份、字节/空目录；不递归删除、不覆盖未知文件。账本写入后中断交给既有观察收尾，不重放新建。退出取消选择器并等待它释放租约；实际GUI退出仍须集中验。

### 自动证据与保留边界

- `out/native-actions-n2/results/new-content-01.trx`：首次新增19项全部通过。
- `out/native-actions-n2/results/new-content-final.trx`：追加默认名称避让及数据库失败用例后，新建、N1文件操作、增量呈现、右键路由、账本收尾5个直接影响类共 **99/99通过**（其中N2为21项）。未重跑历史全量。
- `out/native-actions-n2/results/new-content-final-02.trx`：补“生成后、入库前被外部改动不得收纳”一例后，同一影响面最终 **100/100通过**（N2为22项）；前两次证据保留。
- 覆盖新建目录/空文本、文件与目录快捷方式真实目标回读、原文件身份/字节不变、重启、既有卡片身份保留、同名不覆盖、非法名称、过期窗口、取消、账本前故障精确清理、已改变暂存内容保留、账本后中断不重放、索引失败不造假卡片。
- 最终Release App/worker构建成功，0 warning / 0 error。独立开发构建位置 `out/native-actions-n2/app/`，不是完整人工候选/安装包；不覆盖 `out/native-actions-dev` 或旧人工包。schema保持22，无本批数据库迁移。
- 文件系统与SQLite不具备跨资源原子事务：极端中断早于账本写入可能留下本次生成的暂存物；不会涉及目标文件本体，不自动扫描认领或重新创建。账本后索引故障保留确切路径、普通失败收尾，不增加用户“恢复任务”。未知身份/被外部改过的暂存文件不自动删除。
- 本会话仍无可用原生GUI控制入口，没有冒充点击/菜单/选择器退出验收。N3独立映射尚未实现：Borrowed目录的新建、实时直接子项同步等需在N3接线，当前不开放“映射到原海报”。原生扩展New宿主行为仍单列未验。

### N2结束时的下一批（历史）

N3：选择目录 → 创建独立Borrowed映射海报 → 首次直接子项读取/稳定身份 → 有界外部变化与重启 → 映射目录新建复用。原海报和原文件不搬、不替换，删除映射海报不删目录。随后N4一次集中验证N1/N2/N3、既有拖放及退出，不分别要求用户验每个内部步骤；当前分支不自动提交、合回或推送。

## N3：独立目录映射基础（2026-10-14）

### DeskBox如何处理 → 本项目适配 → 测试

- 实际参考目录为 `G:\A_\Tools\reference\DeskBox\src\DeskBox`。`Controls/WidgetContents/FileSurfaceContent.SelectionAndMenus.cs` 的目录选择、`CreateFolderInMappedLocationAsync` 均以明确的映射目录为操作目标。本项目采用用户已确认的“另建独立映射海报”，不替换原海报、不自动搬入文件；映射目录新建直接写该目录，普通海报仍走N2受管收纳流程。
- 空白右键“目录映射…” → 原生目录选择器 → 复用现有新海报空间占位校验 → 同一SQLite事务创建海报、Mapped/Borrowed集合、来源成员、卡片及观察记录。支持空目录；无空间时沿用现有待安置规则，不推挤已有海报。删除映射海报不删除目录、不自动重建海报。
- 追加schema23 `mapped_directory_observations`，只在本机库保存根目录和子项的文件身份，不在借用目录写所有权文件，不加入全局收纳位置迁移。此前迁移SQL/校验和不改，升级前仍备份。跨机导入或缺本机身份记录的旧映射不静默认领；需删除映射海报后重新选择目录，本批未设计跨机重新绑定UI。
- 复用原有单worker观察器；映射目录只看直接子项，不递归。事件合并后2秒检查，变化稳定至少2秒、提交前复查；无事件时约30秒兜底。一次最多1000个直接子项（含隐藏项计预算），隐藏/系统项不展示；普通固定本机卷，网络、云占位、重解析点等未开放。3秒为逐项检查的软预算，不宣称能强制中断卡在内核的IO。
- 按身份而非显示名对账：改名保留卡片ID、布局、图标覆盖；内容变化只递增受影响实体；新增走已有新增落位；删除复用 `ContentRelationRemoval`。归属和成员匹配用索引查找；不改变现有增量呈现，不对整海报重取图标。目录失联、被替换、扫描不完整/超限、归属冲突均保留旧关联，不以空扫描清理卡片；恢复原目录后继续观察。
- 映射新建支持目录、文本、真实.lnk，名称碰撞不覆盖；失败仅按确切身份/字节或空目录清理本次生成物。被外部改过的结果保留；极端退出早于入库可能留下已生成对象，后续目录观察可发现它，不重放新建。原文件不涉及搬移，因而不伪造转移账本。
- 映射打开、重命名、原生菜单及拖出增加根/对象身份检查，防止旧卡片操作同名替代物。完整根目录内已缺失的对象仍保留原生失效拖动协议，不改成拖动前确认。原生拖出仍由Shell执行；结果观察后复用公共关联移除，映射观察再清理失去的来源成员。

### 自动证据

- `out/native-actions-n3/results/mapping-03.trx` 首次14项中8通过/6失败：空映射没有显式空布局；另一个测试把全局revision增长误判为原海报修改。修正为空集合保存合法布局、检查原海报定义/实体版本后，`mapping-04.trx` **14/14通过**。
- 增加schema22→23、建海报/身份记录原子入库、超限/取消、提交竞争、原生拖出清理、同名替换/失效拖动、新建失败精确清理及改名稳定性。`mapping-targeted-02.trx` **126/126通过**，其中目录映射22项。
- `mapping-final.trx` 185项中184通过/1失败，是旧迁移断言仍期待22；保留旧备份与原迁移校验断言，仅更新当前版本预期。`mapping-final-02.trx` 最终直接影响面（含存储/海报目录）**212/212通过**。未跑历史全量；编译失败记录和旧失败TRX未删除。
- 最后将归属/成员匹配的多重遍历改为预建索引，行为范围不变；`mapping-indexed-lookup.trx` 映射22项＋增量呈现8项 **30/30通过**，未重复跑无关历史全量。
- 最终独立Release App/worker构建成功，**0 warning / 0 error**，位于 `out/native-actions-n3/app/`；保留N1/N2及旧人工候选和配置。不启动日常配置、不操作用户文件，不提交/合回/推送。`git diff --check`通过（仅既有换行提示）。

### N3基础批次结束时的未完成项（历史，现状见下节）

1. **映射海报原生拖入尚未接线**。旧接收器固定把文件搬入全局受管位置，不能直接套用Borrowed目录；当前在UI接收能力及后端准备两层明确拒绝，避免“文件已移动却没有映射卡片”。普通海报的全区域拖入、悬停让位和原生拖出保持原逻辑。N3收尾需以映射目录为真实目标接Windows原生拖入、保留系统冲突提示/持续图像/逐项结果，并核对单一归属与实际落点；不能把本批结果称为完整双向映射拖放通过。
2. 观察异常目前保留旧卡片并记诊断，主动文件操作失败会给出原因。需在集中候选前补足非打扰的映射不可用提示/恢复反馈，避免只有日志可见；不恢复“收纳恢复任务”流程。
3. 本轮未验证实际Shell菜单/属性/原生New扩展、目录选择器取消及托盘退出组合；没有冒充GUI或人工PASS。N3收尾后再制作独立N4候选，一次集中验收菜单、新建、映射、普通/映射拖放与退出重启，不要求逐小步人工。
4. 网络/云/虚拟Shell、超大目录、跨机重绑定、真实显示器/DPI/满盘及长测继续保留未验。构造卡片仍延期，分支仍 `impl/05-content-native-actions`。

## N3收尾与N4集中人工（2026-10-14）

### 本轮接线及必要边界

- 参考DeskBox `Helpers/NativeDropEffectPolicy.cs` 和 `Services/FileService.ShellTransfer.cs`：原生接收/图像与文件执行分离；映射目标默认MOVE、Ctrl COPY，Windows负责冲突交互，完成不向来源返回MOVE要求再清理。本项目复用原有全区域OLE接收及悬停让位，不加控制中心确认；普通海报仍沿已验受管执行器，不能把Borrowed目录伪装成全局收纳位置。
- `MappedDirectoryIngress`：松手后核对海报版本、根/来源身份、锁定与单一归属；schema24先保存意图，随后专用STA中的 `IFileOperation` 执行，每项按Shell回调真实目标记结果。普通文件、目录、.lnk；一次≤100个根对象，每棵目录≤4096项，映射直接子项≤1000；固定本机卷，不遍历重解析点/离线对象/受保护路径。相同目录直接子项拖回不复制或搬自己；Alt/链接、虚拟Shell、网络/云不开放。
- 系统决定同名跳过/取消/替换；本项目不预先删除目标、不删除不确定副本、不重放Shell动作。重启只关闭中断的执行意图并观察真实目录；已完成部分保留、未完成项由用户重新拖。SQLite与文件系统不是跨资源原子事务，崩溃可能丢失未提交的精确落点，但不重复搬文件。原有已知来源卡片通过 `NativeShellExitJournal` 和公共移除核对，再给目标映射建唯一卡片，不恢复共享引用。
- 实际结果复用 `IncomingContentPlacement.Apply/Advance` 保存顺位/网格落点，保留邻居节点、布局与图像；额外外部目录变更同样由观察器增量对账。目标在松手后变化则拒绝执行；落位提交前版本变化则不强行覆盖旧布局。应用内拖至映射海报在OLE释放阶段标记交接，等源原生会话退出再执行，不要求用户确认后二次拖动。实际连续手势留N4。
- `PerformOperations` 的返回并不足以证明全部完成；核对 `GetAnyOperationsAborted` 与逐项回调。首次真实同卷测试暴露成功HRESULT可能非零，已改为按HRESULT成功语义判断，不只接受S_OK；子项回调不能覆盖根对象结果。参考 [Microsoft PerformOperations](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-performoperations)、[PostMoveItem](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-postmoveitem)。
- 退出请求通过取消令牌/进度回调及仅本操作STA的窗口关闭请求收尾，不强杀Explorer或外部程序；系统冲突/权限窗口实际取消退出组合尚待N4，不将代码检查视作已验。
- 失联/不完整/替代根保留旧卡片，轻量标题标记由 `↔` 变为 `!`；空白右键可查看状态/路径，拖入和新建不可用，其他操作也重验身份。恢复原根后自动继续，标记 `✓`；不弹恢复任务、不整组刷新。普通变化约2～4秒，根改名等漏事件最迟约30秒兜底；发现失联后约2秒重查。更换成同名新目录不当恢复。未解决的操作错误可由下一次成功操作清除。

### 自动证据与候选

- `out/native-actions-n4/results/mapped-ingress-01.trx`：61通过/4失败；两项是受管目录原有所有权文件导致“空目录”断言错误，两项是上述同卷移动成功码遗漏。第二次63/65；第三次 `mapped-ingress-03.trx` **65/65通过**。失败证据保留。
- `n4-targeted.trx`：178/179，旧schema预期漏更新；修改当前版本预期、不改历史迁移校验后 `n4-targeted-02.trx` **179/179通过**。包括N1/N2、目录映射、拖放意图、单项增量、外部移除及持久化的直接影响面。
- 最终产品代码 `n4-final-all.trx` **3431/3431通过**。之后仅补两例真实跨卷文件/非空目录移动，`n4-shell-cross-volume.trx` **2/2通过**，产品代码未变；没有把这2项冒记进此前全量计数。真实同卷MOVE/COPY与跨卷MOVE均只用夹具自建数据；系统冲突弹窗/视觉手势不在自动通过范围。
- Release App/worker及fixture均构建成功，0 warning / 0 error；独立候选 `out/native-actions-n4/candidate01`。入口 `scripts/start-content-native-review.ps1` 首次创建 `out/drop-d3/content-native-review01`，调用同包incoming探针生成4个自建对象并正常退出，再启动人工进程初始PID40988；日志已记录poster session ready。重跑脚本保留既有验收配置，不覆盖旧N1/N2/N3包或日常数据。
- 构建和持久化PASS不代表真实菜单、鼠标跟手、系统冲突、选择器与托盘退出通过；N4仍 **USER PENDING**。网络/云/虚拟Shell、超大目录、跨机映射重新绑定、DPI/满盘/长时、第三方扩展广泛兼容仍未验/未开放。当前分支不Git提交、合回或推送。
- 当前环境只读核查 `environment-current/environment.json` 为 `HighIntegrityMatched`（本进程与Explorer同用户/会话/High）；未改变UAC或token，只作为本机权限匹配证据，不代替普通Medium权限验收。第一次误用旧probe04读token失败的报告保留于 `environment/environment.json`，已用现行fullframe-native01核查，不把旧探针故障当产品失败。

### N4一次集中验收：四组

启动入口：`scripts/start-content-native-review.ps1`。本轮资料根是 `out/drop-d3/content-native-review01`；只用这里的自建文件，不用桌面真实快捷方式。已创建 `mapped-directory`（keep.txt / collision.txt）、`incoming-files`（move.txt / copy.txt / collision.txt / folder/child.txt）及空的 `outgoing-target`。人工退出后可用同一脚本重开，保留本轮结果。

1. **内容菜单＋新建**：普通海报右键一个自建Item，确认无刷新来源/移除引用/重做项；重命名，打开属性与Windows菜单并取消，原对象/邻居不丢失。空白右键新建文件夹、文本文档、快捷方式；真实文件出现在当前收纳位置，快捷方式指向本轮自建文件，双击可正常交接。
2. **独立映射＋拖入**：普通海报空白右键“目录映射…”选资料根的 `mapped-directory`，应新增独立海报且原海报/目录不被搬走。拖入 `move.txt` 和folder到新海报留白/Item之间，悬停让位、跟手图像、松手落点有效，来源消失且文件实际在mapped-directory；Ctrl拖 `copy.txt` 应复制并保留来源。再拖同名collision.txt，Windows冲突窗口选“跳过”或“取消”，两边原文件保留，不造重复卡片；无需应用额外确认。
3. **外部变化＋失联恢复**：Explorer中给mapped-directory的keep.txt改名，再新建/移出一个测试对象，海报应增量同步且邻居不闪回占位。把整个 `mapped-directory` 暂改名为 `mapped-directory-away`，等状态检查（必要时约30秒）：旧卡片保留、`!`提示，空白右键能看原因，不误操作替代路径；把同一目录改回原名，应自动恢复`✓`且可再次拖入/新建。不要用一个新空目录替代恢复。
4. **连续操作＋退出重启**：映射Item拖至本轮 `outgoing-target`，在Explorer指定落点松手，原生移动后卡片移除；再拖回，试一次Esc取消，文件不重复/丢失。普通海报既有拖放/框选仍可用；关闭中心仍可操作海报。打开本轮目录选择器/属性/菜单后从托盘退出，应取消相关界面并退出；同一脚本重开，映射、改名、新建与现存文件保留。可将本轮自建文件拖到桌面验证系统落点，但不要动真实用户文件。

四组统一回报即可，有问题给出组号、动作和观察；未具备条件的系统/权限分支明确留未验，不因为其他组通过自动关闭。

## N4反馈修订：菜单启动与行内命名（2026-10-14）

### 用户反馈与修订范围

用户截图报告Item属性/Windows原生菜单、目录属性/Windows目录菜单无法执行；新建文件夹/文件和改名不应另开名称确认窗，新建成功立即进入Item下方命名。**本批没有USER PASS，不收口N4**。旧candidate01与review01保留，不操作其内容或用户桌面文件。

1. **Shell根因**：`PosterVisualWindow.FileActions`使用App根目录的worker.exe；实际完整辅助程序由build/publish放在 `content-worker` 子目录。旧candidate根下只有exe/deps/runtimeconfig而没有worker DLL，故进入Shell准备协议前即退出；图标入口正确所以没有一起失效。新增 `ContentWorkerLocation`，图标与Shell均走同一完整子目录；准备失败带有界stderr诊断。未改COM能力边界、没将第三方扩展搬入主进程。
2. **直接新建**：文件夹/文本文档选菜单即选用不冲突默认名，通过原有身份/收据/逐项安全服务创建；提交完成后按真实路径找卡片，立即行内命名。不会先弹名称/确认窗。快捷方式仍需指定目标文件或文件夹，选择后创建，名称在Item上改；不执行或搬目标，不先弹命名确认。
3. **统一行内改名**：`PosterVisualWindow.InlineRename`将TextBox放在图标下原标签位置，隐藏原文字，不新增带标题的Window。文件默认选主干、保留扩展名；文件夹全选。Enter/离开焦点提交，Esc取消；取消新建对象的命名只保留默认名，不回删对象。重名/非法名称不覆盖，Enter时留在编辑器说明错误；真实文件执行失败仍允许必要错误反馈。`ContentRenameService`继续核对文件身份与关系版本，保留卡片/图标/布局ID。
4. **输入/刷新边界**：海报是视觉窗＋输入岛，不能直接把可输入TextBox塞进原本不接收输入的Canvas。复用 `DesktopMenuInteraction` 的Popup输入/层级租约，显式使用窗口化Popup；根据 [Microsoft Popup说明](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.primitives.popup.shouldconstraintorootbounds)设置不限制到根HWND，但编辑器位置仍贴合卡片标签。初次隔离验证发现异步来源刷新会调用输入重发布关闭编辑器；现编辑期间暂缓这类刷新并暂停内容滚动，结束后补增量刷新。真实布局/生命周期变化仍可取消，不修改原文件。
5. **DeskBox对应**：`FileSurfaceContent.SelectionAndMenus.cs/CreateFolderInMappedLocationAsync`先默认创建、再`StartItemRenameAsync`；`FileSurfaceContent.xaml.cs`隐藏标签、定位编辑器、主干选择、Enter/Esc/LostFocus。本项目适配分离输入岛，不复制整组刷新；旧 `RequestFileName`名称窗口删除。

### 验证记录与限制

- Release App/worker、fixture构建0警告/0错误；输出 `out/native-actions-revision/candidate02`，非安装包/发布线验证。schema仍24，本轮没有迁移。
- `results/shell-prepare-01.trx`：3/3，真实Shell解析/身份/Item及目录IContextMenu准备（不弹菜单、不执行命令）。`results/inline-shell-targeted.trx`：N1/N2/映射及新增主干选择/worker路径 **78/78通过**。候选内完整 `app/content-worker` 对三类操作的准备协议均ready/exit0；属性准备测试没有调用实际属性页API，不记为属性窗口已验。一次手工准备脚本将DirectoryMenu枚举误套文件，按协议拒绝；修正枚举后通过，不改产品来容忍错误目标。
- `out/drop-d3/inline-probe01`失败：Popup受限根HWND，没有独立桥；显式窗口化后 `inline-probe02`检出异步刷新取消名称框。修正来源刷新安全点后，`inline-probe03` **退出0 / contract.inline_naming_passed**，真实TextBox有焦点、直接新建目录后改名、文本取消后保留默认文件、既有Item改名与重名留编辑器通过。测试调用生产回调，不模拟鼠标/键盘，不据此宣称中文输入法/真实Enter/Esc已人工通过。失败数据/日志全部保留。
- 最终合同 `results/n4-revision-final-all.trx`：**3440通过/2失败/0跳过，共3442**。首次失败：`ManagedContentSyncTests.ForegroundExecutorWaitsForObserverButStillRejectsAnotherFileExecutor`（涉及5秒观察租约时限，失败原因尚未复现确认）与 `StorageRelocationFilesTests.IndexFailureKeepsOriginalFilesAndRollsBackEveryLocator(point:2)`（故障注入清理结果断言）。未修改这两处测试/产品实现，同源码相关两类定向 `n4-revision-failures-recheck.trx` **65/65通过**；保留初次失败及“全量稳定性未证明”的携带项，不冒称全量0失败。
- 补充离开名称框提交验证：`inline-probe04`发现原生Popup销毁通知可能先于XAML Closed触发，误按取消结束；改为区分自然关闭（提交）与仍打开的失效界面（取消）。最终 `inline-probe05` 四次真实Popup会话及直接创建/改名/取消/同名/自然关闭提交均通过，退出0、`activeResources=0`。该日志中的通用incoming标记仍沿原夹具固定写items=4，不能作为本轮追加两项后总数；新命名检查以 `contract.inline_naming_passed` 和真实文件断言为证据。最后一次修改仅App编辑器/探针，未改合同工程产品实现；Release再次0警告/0错误。
- computer-use技能要求的node_repl本会话不可用，未绕过技能自动操作桌面。原生系统菜单/属性页、实际键鼠/IME、第三方扩展及原N4未确认组仍需人工；High权限环境结果不代替Medium、DPI、满盘与长测。无Git提交/合回/推送。

### 本次集中复验

入口仍 `scripts/start-content-native-review.ps1`，默认候选改为 `out/native-actions-revision/candidate02`，隔离资料改为 `out/drop-d3/content-native-review02`。旧候选/资料不覆盖；只用本次自建内容。

1. 普通Item的**属性 / Windows原生菜单**及空白区**目录属性 / Windows目录菜单**应实际打开；取消后海报仍可操作。
2. **新建文件夹 / 文本文档**不再弹确认，立即出现卡片并在图标下命名；输入中文后Enter，或点别处，应落盘并改名。已有Item右键“重命名”也在同一位置编辑。文件扩展名默认不被选中；图标、邻居不整组闪烁。
3. 新建后Esc应保留默认名对象；已有项Esc不改名；同名不覆盖且可继续修改；连续新建/改名后关闭并重开仍保留。快捷方式先选目标，再在卡片命名，不搬/打开目标。未完成的原N4映射/拖放/退出组合仍一起验，不自动套用旧里程碑PASS。

修订候选已由上述脚本启动，初始PID **6824**，同包自建incoming种子退出0。人工进程使用review02，旧review01未覆盖；新入口增加完整worker exe/DLL/runtimeconfig校验。实际原生菜单/属性和键鼠/IME仍USER PENDING，不将程序已启动写为人工通过。

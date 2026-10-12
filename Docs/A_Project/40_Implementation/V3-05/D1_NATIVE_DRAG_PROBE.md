# D1 全区域命中与跟手图像验证

2026-10-12。归属DEC-042及[工作流§6.5](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径)。**D1本机A模式探针第1～8项人工通过，机制候选采用A；普通权限兼容及生产窗口复验携带，不能称生产全区域已完成。** 下一步[D2默认位置与迁移](D2_STORAGE_LOCATION_AND_MIGRATION.md)。未改变产品输入岛或拖出执行器，未操作用户文件、提交或推送Git。

## 1 验证边界

### 当前人工反馈 2026-10-12

用户先确认下表结果并指定后续使用A，随后明确“4～8项均验证通过，符合预期”。当前为 **LOCAL_PROBE_USER_PASS / CARRY_TO_PRODUCTION**；本机联合探针验收收清，不扩大为生产或跨环境验收。

| 模式 | 普通点击穿透 | 独立源图标直接进入留白 | Explorer文件直接进入留白 |
| --- | --- | --- | --- |
| A 全矩形＋HTTRANSPARENT | USER PASS | USER PASS | USER PASS |
| B 全矩形＋WS_EX_TRANSPARENT | USER PASS | USER FAIL | USER FAIL |

A作为后续生产机制候选，B保留为本机不满足联合要求的对照；不由此推断所有环境中B都不可用。

| 人工项 | 结果及范围 |
| --- | --- |
| 4 持续跟手 | USER PASS；单／多图标及Explorer自建文件，拖入→拖出→拖回，图标持续可见 |
| 5 拒绝反馈 | USER PASS；拒绝落下时图像仍保留，不以隐藏图像表示拒绝 |
| 6 取消及重拖 | USER PASS；Esc清理后可重新拖动，无残影或残留拦截 |
| 7 收起范围 | USER PASS；收起仅接标题高度，不接不可见展开范围 |
| 8 前台遮挡 | USER PASS；被前台覆盖时不抢接收，解除覆盖后可继续 |

以上按用户本次明确反馈记录，没有新增自动鼠标录像或逐帧日志取证。源／目标在拖动中关闭、普通Medium权限、跨屏／DPI／锁屏、生产窗口图像接力及实际文件移动不在此次反馈范围，仍需对应后续门；尤其不能把Esc通过等同所有生命周期通过。

只读核对当前活动进程32120指向probe06，review-b24846006a82436b9baececc0b5f218e环境报告为HighIntegrityMatched。该目录已有A的hdrop回调及A/B的底层点击记录；本次未见私有源完整日志链，独立源接收结论按用户明确反馈记录，不冒充已关联的源进程日志。未修改或关闭正在运行的候选。

目标是同时证明：从海报透明留白首次进入能被OLE接收；普通点击仍穿透；对象图标跨边界持续跟手，不被目标描边代替。不能以鼠标进入卡片后才扩大区域，或永久扩大点击区域作为通过。

探针由独立源进程和接收进程组成，不是产品海报。源只有自建图像和私有探针载荷，不提供CF_HDROP、虚拟文件或真实路径；落到Explorer不会搬出文件。接收端可读取CF_HDROP，但仅在内存比较快照、记录落点，不调用文件执行器，Drop始终返回NONE。外部来源路径不写日志。

该边界允许先验证真实跨进程OLE和图像显示，不冒充真实剪切收纳成功。用户只需用探针图标及启动器生成的D1-test.txt；无须使用桌面有价值文件。

## 2 代码与对照

| 部件 | 位置与行为 |
| --- | --- |
| 原生验证窗口 | tests/DesktopUp.NativeDragProbe；不引用App，不注入Explorer，不创建全屏窗口，不设Topmost |
| 命中对照 | A全矩形＋HTTRANSPARENT（用户选择的后续候选）；B全矩形＋WS_EX_TRANSPARENT（本机接收失败对照）；C稀疏HRGN；D全矩形不穿透。生产窗口尚未接线 |
| 图像通道 | tests/Shared/DragProbe/ShellProbeImages.cs；接收端四回调转发给IDropTargetHelper；源由IDragSourceHelper初始化单图标／多图标叠影和抓取偏移 |
| 数据与生命周期 | ProbeDataObject.cs支持HGLOBAL与IStream私有格式，按COM规则保留／释放；ProbeProtocol.cs验证载荷一致、重复Drop、拒绝／取消及异常清理 |
| 拖出协议 | ProbeSource.cs使用真实DoDragDrop、Esc／退出取消和IDropSourceNotify记录目标HWND；不把effect或目标HWND当作文件完成收据 |
| 自动测试 | tests/DesktopUp.Contracts.Tests/NativeDragProbeTests.cs；探针与测试链接同一份协议实现，生产工程不引用这些测试文件 |
| 运行环境 | tests/Shared/DragProbe/ProbeEnvironment.cs；定位实际桌面Explorer，只读核对用户SID／会话／完整性级别；查询失败或不一致拒绝，High匹配只作本机隔离证据 |
| 严格入口 | scripts/start-native-drag-probe.ps1；校验源码／产物并调用探针预检，源／目标实际启动再次核对；每次新建独立review目录，不覆盖旧证据 |

UI还提供图像辅助开关、目标拒绝、收起区域、前台覆盖对照及底层点击计数。没有可靠跨源会话发现之前，不加入“检测全局左键后临时扩区”的猜测方案。

## 3 自动证据

### 当前权限兼容修订

本机实际桌面Explorer与探针同用户、同会话，完整性RID均为12288（High）。旧入口在脚本和程序两处按管理员身份一律拒绝，误挡了这个匹配环境；换工作目录或新开同权限终端不能解决。现已移除这一绝对限制，但没有关闭UAC、修改令牌、提升进程或绕过UIPI。

- 只有实际桌面Shell确认为Windows Explorer、双方用户／会话／完整性一致且为Medium或High时才允许。未知／缺失信息、Shell切换、UIAccess或其他完整性级别均拒绝。High记录为HighIntegrityMatched，不代表普通权限验收通过；令牌匹配也不等于OLE命中／视觉通过。
- 新增22项，完整 **2995通过，0失败／跳过**：out/drop-d1/permission-full02/contracts.trx。覆盖双向完整性差异、异用户／会话、查询失败、Shell变化、High匹配及实际自身令牌读取。
- 新候选out/drop-d1/probe06构建0警告／错误；out/drop-d1/proof-permission01.json绑定186输入／16产物及新TRX。旧probe03／proof.json保留，不把旧证据重新绑定到新源码。
- 实机只读预检out/drop-d1/permission-inspect03通过；双方High、同会话且UIAccess关闭。permission-smoke02进程37408退出0，四种窗口创建／注册／撤销／销毁通过。仍无真实鼠标／视觉结论，WindowFromPoint采样不是有效命中证据。
- Windows PowerShell 5.1执行启动器CheckOnly通过，review-3803d82e40814ee684ad72a57f9eb5bf保留环境报告；PrepareOnly也通过。未留下交互探针进程。
- permission-inspect01／02保留首次读取失败：固定DWORD字段TokenElevation的空缓冲长度探测返回ERROR_BAD_LENGTH，而非变长字段的ERROR_INSUFFICIENT_BUFFER。现按字段固定大小分配；新增原生读取测试后复跑完整门。probe04／05、permission-full01等仅作修订过程证据，不是当前候选。

### 此前probe03证据

- 完整合同 **2973通过，0失败／跳过**，新增26项。证据：out/drop-d1/full01/contracts.trx。原D0及既有真实引用／传输合同同时复跑。
- 探针Release x64构建0警告／错误，绑定包out/drop-d1/probe03；proof.json绑定184个输入和16个产物。启动器PrepareOnly校验通过。没有更改旧candidate03或其proof。
- 原生Shell图像管理器初始化及私有格式取回通过。首轮真实接口返回DV_E_TYMED：它同时使用HGLOBAL和IStream，而非只有内存块；补齐流保留／释放后通过。targeted01／02失败记录保留，targeted03为修正后26项通过。
- 独立生命周期进程PID50640退出0，完成四种窗口的创建、RegisterDragDrop、撤销和销毁。out/drop-d1/smoke01保留日志。该运行是提升权限的构造／释放检查，不是普通权限拖动；WindowFromPoint返回非探针窗口，命中采样无效，**不据此判断A/B可行或不可行**。

上述证据不包含真实鼠标、画面连续性、Explorer实际移动、跨DPI、锁屏或生产内容捕获到OLE的接力。桌面自动操作工具本轮没有可用node_repl入口，因此保留人工门，不用直接调用回调代替真实拖动。

## 4 运行环境核对与联合验证

在PowerShell运行同一入口，脚本会检查探针与当前桌面Explorer是否匹配：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-native-drag-probe.ps1'
```

通常应使用普通终端匹配普通Explorer；本机双方均为High时也可做隔离测试，脚本警告、窗口标题与日志都会单独标注。不要为了启动探针改变UAC、进程权限或UIPI；出现不同用户／会话／完整性或查询失败时，先查看本次environment.json，不删除保护。普通Medium环境的真实拖动兼容仍需另验。

可加`-CheckOnly`只校验指纹、准备独立目录及核对令牌，不打开交互窗口；`-PrepareOnly`仅校验和准备，不检查运行环境。正常启动输出本次review目录和自建测试文件位置；窗口关闭即可退出，没有托盘常驻服务。目标窗口里的“打开独立源窗口”会打开可拖动图标，两个窗口可分别正常关闭。源窗口不提供可被Explorer接收的文件，这是故意的安全边界。

### 第一组 命中与穿透同时成立

1. 先用默认A，在蓝色留白普通点击，检查“穿透点击”增加。
2. 打开独立源窗口，按住图标移动，**从四边直接进入远离模拟Item／标题的留白**。看Enter／Over计数，松手看记录Drop。按住途中穿过Item与留白，不能只在Item上才有回调。
3. 如果A不接收，记录事实再切B；C和D仅作反例对照。C预计留白不是接收区域；D即使全域接收，吞普通点击也不能准入。
4. 在Explorer打开脚本输出的external-source文件夹，用其中自建的D1-test.txt重复直接进入留白。松手后文件必须仍在原目录。不能只以自有源通过推断Explorer通过。

任一候选须同时具备普通穿透和外部OLE首次留白接收。两者不能分属不同模式后拼成PASS。A/B均失败时记录失败，返回命中机制设计，不接真实文件移动。

### 第二组 图标连续与清理

1. 在可接收模式中，独立源单图标／三图标分别拖入、越界、再拖回；跨卡片和留白，图标应持续可见、抓取点不跳、没有两份跟手图像。再用Explorer自建文件检查外部来源图像。
2. 关闭“允许探针落下”，图像仍应可见，仅附加禁止反馈；不能靠隐藏图像表示拒绝。关闭／开启“Shell跟手图像”只作A/B定位，不把关闭后的表现当目标体验。
3. 拖动中Esc、源窗口关闭、目标关闭，结束后无残影、无残留吞点击区域。文件不变。切换收起态后，只接标题高度，不接不可见的展开范围。

来源未提供图像时的缓存图标兜底、真实自定义卡片图标、App内捕获→OLE→内排布的单一所有者接力尚未接线，不能以此自建源验证代替。

### 第三组 层级与环境

用“覆盖目标”打开普通前台窗口，拖到覆盖区时海报目标不能抢接收；关闭后可继续。多个真实海报、桌面壳层、实际自动展开、跨屏／DPI与锁屏恢复须后续在生产候选复验；本探针只先排除窗口策略反例。截图或肉眼反馈用于判别图像，日志只证明回调。

## 5 拖出执行责任结论

Shell拖动图像辅助接口只负责视觉。微软的[Shell传输场景](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)区分目标负责的优化移动与源负责清理的普通移动；返回effect不是实际文件路径。IDropSourceNotify给出的目标HWND也不是目标目录和逐项结果。

因此本轮没有把DoDragDrop返回MOVE映射为删除源或解除共享引用。不同返回effect／Performed DropEffect组合都保持“不清理源”的探针策略。该策略只适用于D1安全探针；不声称完成生产Shell协议。

**通用Explorer剪切式拖出仍未准入。** 后续需提供可核实的实际目标、逐项文件身份和完成收据，或限定为本应用能证明目标并自行记账执行的类型。未知目标、异步结果、同名改名和外部移动不能按鼠标坐标或源消失猜测目的地。既有“真实桌面识别＋自有账本执行”仍是产品已实现的受限路径，不被本探针替换。

## 6 出口与下一步

当前为 **PROBE_AUTO_PASS / LOCAL_PROBE_USER_PASS / CARRY_TO_PRODUCTION**。用户确认A第1～8项通过，B仅穿透通过、两种来源接收未通过；不要求重做本机已过项。下一步[D2](D2_STORAGE_LOCATION_AND_MIGRATION.md)默认根／跨卷／迁移安全；D2-01纯模型及完整3081合同已通过，尚无持久化／文件迁移接线。D1携带普通Medium、桌面壳层／多海报／跨DPI／生命周期、生产捕获与图像接力门；通用Explorer取出仍缺实际目标与逐项完成收据。D3实际落点、D4连续拖出、D5同包闭环顺序不变，不提前开放生产直收。

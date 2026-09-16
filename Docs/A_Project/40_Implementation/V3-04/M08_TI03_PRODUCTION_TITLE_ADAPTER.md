# M08-07-TI-03：原生产输入、单一空间所有者与 R4 组呈现接线

日期：2026-09-14。状态：**PASS（代码接线、适配器自动化、隔离 SQLite、构建）／真实鼠标与画面未验**。
规则以[V2-DEC-003标题合同](M08_TITLE_INTERACTION_CONTRACT.md)为准；顺序以[M08计划§4.1](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#41-标题交互修订的最近顺序)为准。下一项TI-04，不跳到正式人工包或真实Item。

## 1. 接线与边界

| 单元 | 责任 |
| --- | --- |
| Windows/PosterInputIslandManager + TitleNativeInput | 原输入岛、原Router capture，在按下发出标题仲裁；领取后Move/Release/Cancel不再走旧拖动/Invoked/Item |
| PosterVisualWindow | 仅自动启用且Using模式的标题空白领取；按钮、菜单、编辑模式、Item及外部点击不进入新标题模型 |
| PosterSpatialRuntime.Title | 在原运行器移交同源收展所有者；保存原Initial和成功AcceptedFrames，重放出的按下画面必须等于实际读回 |
| TitlePresentationDriver | 合并移动但保留第一次阈值；释放排在已收到移动及在途呈现后；物理中间帧与模型完整接受点分开 |
| TitleRuntimeAuthority + coordinator | 一次安装可信宿主，发放限定操作/起点/capture/环境/读集的一次性租约；事务内再核验，普通命令无此旁路 |
| 原R4 Transition/CommitSpatialFrames/RestorePresentedGroup | 保留容量预布局、可见HRGN、稳定内容/底栏裁切、输入岛、两次准备回调、组提交及明确结束恢复 |

没有新全局鼠标钩子、替代输入窗口、第二个空间求解器、逐帧数据库写入或第二份持久权威。窗口实际读回是宿主证据，**不是GPU已经显示该帧的证明**。

## 2. 所有者与顺序

1. 原生DOWN取得原capture后，先发TitleInput，再通知自动观察；避免在标题有机会接管之前把原自动收展取消回滚。
2. 若同源原收展仍在进行，使用其初始快照、原operation和**成功批提交数**建立TI-02会话；不是Cursor尝试次数。原组最后一帧已经接受时先完成原事务，再把按下分类为实际终态。
3. 退订旧Rendering/watchdog并清空旧posture所有者，然后安装title所有者。旧自动回执只关闭旧请求，不触发物理回滚；不同源的在途组保持原所有权，新标题手势被消费但不抢占它。
4. 标题按住Pending不增长、不收拢、没有长按定时动作。拖动按下阶段固定，绝对抓取点和实际占位仍由TI-02与原求解器计算。
5. 快展仍有完整扫掠证明，但只用一次准备好的终态批呈现；拖动释放后的收拢则继续逐帧。未完成整份proposal之前不能Freeze/commit。
6. 完整终态接受后，经原Run/repository/full-CAS提交单revision；设置、中心、托盘仍读原projection。失败或冲突按最新committed状态恢复整组，布局释放/最终读回失败则暂停并发起原宿主恢复。
7. capture结束不等于空间所有者结束。释放退出计数通过AutoPostureEvidence回到原手动抑制；失败不清除已有抑制，成功快展才清理相应屏障。

源码中prepare/submit/accept/precommit均保留当前session、capture（释放后换为终结租约）、环境epoch、shell/display/runtime代次和完整catalog/entity读集验证。只把旧已失效回调挡在原operation之外，没有削弱真正失效恢复。

## 3. 可信起点

普通TitleSpatialCommand仍只接受普通投影或当前continuity的合法几何。生产恢复/断屏回退场景由已绑定的原宿主提供见证：

- BeginTitle核对工作区每个当前可见海报的原生读回、当前有效布局缓存/意图/metrics、完整可见占位，以及全部catalog实体版本（含隐藏和异屏读实体）。
- 收展接管以原PostureRun持有的可信Initial为起点；当前物理前缀必须与TI-02确定性重放吻合。
- 租约构造器不对命令调用方开放，限定issuer/start/read set；关闭、异issuer、异operation、篡改起点或运行代次失效不能写。
- 见证只授权实际起点。最终写集仍由原TI-02重放推导，继续检查实体元数据、目标终态、因果关系、catalog与完整CAS；不能授权任意邻居更新或System写位置。
- 这是可信应用宿主与命令数据的边界，不宣称隔离同进程恶意原生代码。

## 4. 验证与产物

新增 **37/37**：

- TitlePresentationDriverTests：24项。顶/中/底快展、同起点拖缩、释放后持有、快速越阈值/回移/释放、10000次静止Pending、静止拖动收拢、0/1/3个成功帧后失败、prepare/submit/accept失效、5类旧输入、锁定、重入、异常和预算。
- TitleRuntimeAuthorityTests：13项。回退monitor/临时有效高度、未绑定、过期/关闭/issuer/operation/起点/读集/System、重放伪造、实体CAS、重复绑定与读回异常。

最终完整 **1080/1080，0 skipped**；WinUI Release **0警告、0错误**。驱动测试调用生产使用的TitlePresentationDriver和假呈现器；见证测试用隔离真实SQLite和假宿主读回。它们不代替Win32、DWM或视觉验收。

文档检查：82份Markdown、539个本地链接，缺失0；git diff --check通过。任务正文、登记表、入口、模块职责、代码锚点、验证日志和交接已对齐，历史TI-01/02结果保留。

产物：
- `out/verify/v3-04-m08-07-ti03/tests/ti03-driver.trx`
- `out/verify/v3-04-m08-07-ti03/tests/ti03-authority.trx`
- `out/verify/v3-04-m08-07-ti03/tests/ti03-full-final.trx`（最终）
- `out/verify/v3-04-m08-07-ti03/app/`（独立包，未启动/绑定）
- 中间全量TRX和旧TI-01/02包保留，不作最终指纹依据。

最终SHA256：
- `DesktopUp.dll`：`C67AC2D44FD2D0D1ED35B3FD5362A84AEA69199F422145ADF3A73B9F2B65E317`
- `DesktopUp.Application.dll`：`65E9556B054D6E6DD98885981CC092FAFC47AA6D04AA145B87A4A7E5DE1BCA61`
- `DesktopUp.Windows.dll`：`23298C4E832933FC01B047E7FB9DEF7D10B06B8B5D06CA307401E447743FC1D1`

构建/测试命令：
```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --no-restore --logger 'trx;LogFileName=ti03-full-final.trx' --results-directory out/verify/v3-04-m08-07-ti03/tests
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 --no-restore -o 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m08-07-ti03\app\'
```

## 5. 未关闭的门／下一步

**TI-04：隔离进程、故障和生命周期门**，然后才TI-05真人体验/增量资源。具体必验：

- 原native capture消息与Rendering真实换序；旧回调、按住不动、快点、拖动释放、锁定/编辑切换；R4过程与终态不出现白条/错位/留白。
- R4准备、部分提交、结束恢复失败；原读集冲突/隐藏/删除；同组恢复，无残留capture/busy/Rendering/watchdog/布局租约。
- shell/display/session重建、正常退出/同库重启；R2-A外部连续点击单列复验。
- **长拖压力不能跳过**：TI-02已有最多256语义采样（按参与者规模进一步限额）。TI-03目前达到预算会明确失败并恢复，日志为`motion-budget-exhausted`；这不是正常长拖的体验验收结果。换人工包前须评估并处理这一边界，不能静默丢移动或无界放开记录。
- 当前R1绑定、M07固定包、06真实恢复余门、G1～G5及有效资源0/36均未改；没有启动App、人工PASS或资源新样本。

没有修改原空间Domain、Infrastructure schema/仓储、启动配置、日常库或真实文件。未commit/push；保留此前TI-01/02未提交工作、退役快照和旧包/日志。当前六卡仍为DEMO。

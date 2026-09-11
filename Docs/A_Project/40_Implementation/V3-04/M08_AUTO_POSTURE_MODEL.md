# M08-01：自动收展行为合同与纯模型

日期：2026-09-11。范围：Application纯模型及虚拟输入验证；用户确认手动抑制后M08-01 PASS，不代表原生自动功能已接入产品。
需求裁决：[A层统一索引](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md) V2-DEC-001；手动抑制V2-TBD-017已RESOLVED。当前任务状态归[工作流](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)，后续设置已进入[M08-02合同](M08_AUTO_POSTURE_SETTINGS.md)。

## 1. 交付与未交付

- 新增 [AutoPostureModel](../../../../src/DesktopUp.Application/AutoPostureModel.cs)：输入完整目录观察、虚拟单调时间、交互保护和完成回执，输出新的不可变状态、至多一个自动请求或取消请求。
- 不读真实时钟、不 sleep、不创建 timer/线程，不调用 HWND、SQLite、文件系统或 UI。测试复用现有 PosterInputSessionKey 和纯空间求解器，不重造几何策略。
- M08-01交付时未修改schema6、AutoCollapseEnabled持久化、空间/CAS事务、输入岛、R4动画、稳定包或真实Item。设置/迁移归M08-02，真实观察归03，调度/事务接线归04。
- 默认关闭，离开延迟整数1～300秒、默认3秒来自已有需求；M08-01仅实现纯模型输入，后续设置实现以02合同为准。
- 2026-09-11用户明确确认“手动收起且鼠标仍停留时保持收起，必须真正离开再进入才自动展开”。原测试草案转为正式行为合同，测试名移除Proposed；M08-01冻结门关闭，不把此确认扩称原生鼠标验收。

## 2. 观察合同：什么才是有效指针证据

M08-07 [R1补充](M08_ANIMATION_INPUT_R1.md)：PointerButton/PointerCapture仅阻止新自动请求；不单独取消InFlight。Gesture只表示真实海报空间手势，不再由原始capture推定。语义保护、Unknown、策略/生命周期失效仍取消；原始M08-01测试数按当时产物保留。

M08-04兼容补充：[共享调度合同](M08_SHARED_SCHEDULER.md)新增可选`ManualCollapseExitVersion`。它记录手动收起意图时的真实退出计数，防止标题随收起移动后完成时presence已Outside而丢失抑制；真实退出才清理，会话/恢复不复用。没有改变V2-TBD-017或写入持久化状态。下方原69项证据按M08-01产物保留，新增组合验证归M08-04。

| 输入 | 约束 / 所属层 |
| --- | --- |
| Frame.Sequence / NowMs | 一个共享 adapter 串行提交全目录快照；Sequence严格递增，NowMs为单调毫秒，可同毫秒多帧。不使用UTC绝对截止点，不跨进程保存计时状态 |
| Session | PosterId＋当前session epoch，沿用输入路由身份；删除后重建必须新epoch，不能复用旧会话 |
| EntityRevision | 当前已提交实体版本；出队时重新取最新值，不将首次倒计时的版本缓存成执行版本 |
| EnvironmentEpoch | adapter把共享Shell/显示generation及就绪生命周期映射为递增失效代次；不是数据库revision。映射在03/04接入 |
| Policy / IsCollapsed | 当前有效策略和committed姿态；动画中的视觉高度不是另一个“自动姿态”权威 |
| Presence | Unknown=无法取得可靠归属；Outside=不在保护区；Inside=在保护区但非展开热区；ExpansionZone=在可展开热区。不能由整个R4容量窗口的矩形直接推出 |
| EnterVersion / ExitVersion | 分别记录真实进入展开热区、真实离开整个保护区的证据。只因窗口运动/推让跨过静止鼠标不增计数；两次观察间完整离开/重入仍保留两个计数 |
| ManualIntentVersion | 显式手动姿态或几何介入的序号，不是任意重命名、日志或实体revision。手动事件与快照需有明确先后顺序；手动动作在模型中被观察后，之后的exit证据才释放抑制 |
| SpaceVersion | 独立、有效的障碍/拓扑条件变化。自身这次自动批次及其邻居动画帧、无效日志、普通revision增长不能冒充重试触发 |
| Local / SharedProtection | 编辑、菜单、确认、按键、拖放、手势/capture、恢复保护。菜单/确认等可能影响邻居落点的活动由适配器提升为共享保护；仅中心打开不是保护或姿态命令 |
| SpatialBusy / Completion | busy是既有空间运行器的事实，不解析错误文案。完成回执必须对应完整请求身份；不能用另一会话、另一请求的完成信号释放当前请求 |

热区语义固定为：收起态可展开的标题范围，排除现有 CollapseButton、MoreButton、LockButton及其他动作/拖缩区域；展开态保护范围使用当前逻辑可见圆角轮廓（包括可见内容空白），不能把空白区扩成可拦截桌面点击的输入岛。来源为现有 [Contracts](../../../../src/DesktopUp.Application/Contracts.cs) / [PosterInputRouter](../../../../src/DesktopUp.Application/PosterInputRouter.cs) 的区域与布局版本；最终物理映射和原生遮挡证据在M08-03验证，不在本步手写第二份像素常量。

普通窗口遮住海报时，其上的鼠标不能被认作有效展开悬停；能够可靠确定不属于保护区时为Outside，证据不确定时为Unknown并暂停。初次同步、解除遮挡/恢复、开启策略本身都不制造进入事件。被动观察不改变窗口层级、输入穿透或桌面选择。

完整目录是信任边界：模型清除不存在的海报，但不保存无限增长的已删ID墓碑。迟到的旧目录须保留旧Sequence并被拒绝，适配器不能给旧目录伪造新Sequence；真实进程内旧回调不得跨模型实例重用请求ID。上述是后续adapter必须验证的前提，不冒称本步已取得原生证据。

## 3. 行为与转移合同

| 当前情形 / 新证据 | 模型行为 |
| --- | --- |
| 自动OFF | Disabled；没有截止时间或自动请求；姿态不变 |
| 收起＋初始同步或几何移入 | AwaitingEntry；不把当前“在里面”当作新进入 |
| 收起＋真实进入展开热区 | Eligible；未被保护且空间通道空闲时请求展开一次 |
| 展开＋离开保护区 | CountingDown；从此次观察起计完整延迟，到期进入Eligible |
| 到期前重入 | 清除截止时间；再离开从新起点计完整延迟。采样间的重入/再离开同样重置 |
| 手动收起且鼠标仍在内 | ManualSuppressed；静止鼠标不反弹，布局变化不能解除。真实离开后重新进入才展开（V2-TBD-017用户已确认） |
| 手动展开或手动几何介入 | 丢弃旧自动等待；按当前保护和指针重新判定，在外重新计完整延迟 |
| 编辑/菜单/确认/按键/拖放/手势保护 | Protected；清除待执行资格与旧截止时间；解除后在外重新计完整延迟，在内不回放被保护期间的进入 |
| 多源竞争 / SpatialBusy | 每海报保留当前资格，按首次合格时间、PosterId ordinal排序；空闲时重验快照，只交付一个请求。等待中重入、手动或失效可撤销 |
| 事务已提交出去 | InFlight；不抢占执行另一请求。不因鼠标离开或原始按键/临时capture单独取消；语义保护仍可取消；新姿态以完成时committed观察为准 |
| 失败或无空间 | Attempted；同一次触发不随timer tick、同值设置或普通revision变化反复求解。真实新进入/离开或独立SpaceVersion可重新获得一次机会 |
| 无空间的自动请求 | 仍由原求解器裁决；保持原姿态，不能推一半邻居或抖按钮。模型本身不计算“是否有空间” |
| 隐藏、未Ready、Unknown、关闭策略、环境/会话失效 | 清除旧等待；已交出的请求发一次Cancel，保持占用，直到精确完成回执 |
| 恢复 / 重启 | 读取当前状态，不补播；展开态在外重新计完整延迟，收起态等待新的真实进入 |
| 删除 / 零海报 | 移除对应节点；若有已交付请求，仅保留一个待取消回执的请求身份，确认后为空。没有永久计时器或已删除节点集合 |

锁定没有被解释成固定展开/禁止自身自动行为；它仍禁止拖缩与作为邻居被推动。锁定障碍、先下后上、有效高度、链式推动和不能完整归位就原地不动，全部由现有空间合同处理。

## 4. 请求、取消与完成协议

1. `Advance`不执行副作用。输出Request绑定请求ID、Session、EnvironmentEpoch、Policy、ManualIntentVersion、最新ExpectedEntityRevision和目标Collapse。它不是新StateCommand，更不是一次已完成提交。
2. M08-04 adapter收到请求后必须再次核对身份/指针/保护/资格，取得完整空间读集，并走现有System SetCollapsed → 求解 → R4动画 → CAS提交/恢复。不能只靠模型里一个实体版本替代邻居读集。
3. 等待执行与已经交付分开。尚未交付的资格可直接清除；已经交付的操作失效仅发Cancel一次，不能先释放名额再让另一个事务开始。
4. 只有与当前完整Request相等的Completion释放名额；重复/旧回执不释放新请求。取消有竞态：Commit可能先完成，模型仍只采信同步观察到的committed姿态，不私自反向补偿。
5. Committed回执必须与观察姿态吻合；不吻合按本触发已消费处理，不伪造姿态。旧环境/策略/手动代次的失败回执不能污染新的代次。
6. Conflict、InsufficientSpace、Cancelled、HostUnavailable均结束当前尝试，不在同触发内无限重试。失败原因由adapter记录；之后发生新的有效条件才重评。
7. 持续busy或取消永不确认时，模型不会自行突破单航班限制；终止/最终可执行性依赖运行器确实报告结束。M08-04/06须补正常结束、失败/取消、删除和退出的真实回执与资源释放证据。

每次Advance只扫描/排序当前目录，无等待循环；空间规模N时O(N log N)、状态O(N)＋一个在途请求。Sequence/时间回退或当前会话证据计数回退时整帧忽略（包括该帧回执）；重复ID、非法策略/枚举拒绝，不部分更新。恢复时需提交新的完整有效帧。

## 5. 验证矩阵与证据边界

| 范围 | 新夹具 / 实际覆盖 |
| --- | --- |
| 默认/计时/重入/手动优先 | [AutoPostureModelTests](../../../../tests/DesktopUp.Contracts.Tests/AutoPostureModelTests.cs)：OFF、0/1/3/300/301、期限前/到期、重入取消、采样间边界对、动作区不展开、已确认的手动抑制 |
| 保护与竞争 | 同文件：7种本地保护＋7种共享保护；旧计时取消、解除重计、不回放旧hover、busy候选撤销、当前实体版本、确定性排序 |
| 失败/取消/恢复 | [AutoPostureLifetimeTests](../../../../tests/DesktopUp.Contracts.Tests/AutoPostureLifetimeTests.cs)：4种失败限次、9种在途失效、精确取消确认、旧回执、0目录、重启不补执行、版本回退、不可变输入/状态 |
| 无振荡 / 有界 / 确定性 | [AutoPostureSequenceTests](../../../../tests/DesktopUp.Contracts.Tests/AutoPostureSequenceTests.cs)：1000次静止指针布局跨越无自动请求；3个固定seed各5000步，反转目录顺序结果一致，检查单航班、计数有界和快照不变 |
| 与既有空间纯模型组合 | 同文件：C展开推动B/A再收起，共100轮；直接复用SpatialContinuityTests.Model，校验既有每帧安全断言与每轮初始几何恢复。不是新求解器、原生动画或SQLite接线证明 |

M08-01原始自动证据：新AutoPosture测试69/69，全量合同577/577（原508保留），0失败、0跳过。以下原产物/哈希保留；规则确认与后续字段映射后的最新全量回归见M08-02合同。原命令：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~AutoPosture --logger 'trx;LogFileName=m08-01-pure.trx' --results-directory out/verify/v3-04-m08-01
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --logger 'trx;LogFileName=m08-01-full.trx' --results-directory out/verify/v3-04-m08-01
```

TRX保留于上述独立目录，SHA256：

- pure：`3171DF9685E4DA3D2F532E3F93980FAC89EE3F508688E5581255B90E7EA43A74`
- full：`517838F3CE23090D6F2AC429E1E4014A4CE065AA1EC0E1AA29B30CB3C7021AD1`

Windows/x64、.NET SDK10.0.103，受审查的项目外工作目录读写与dotnet测试。合同工程不引用WinUI App，未构建/运行新产品；无新的桌面人工、Shell/DPI、GPU呈现或资源性能认证。没有修改用户库/文件，测试自己的SQLite临时夹具不等于迁移日常配置。

## 6. DeskBox参考吸收与后续出口

重新阅读本地DeskBox commit `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`：

- `src/DeskBox/Services/WidgetCompactInteractionPolicy.cs`：纯快照判定、交互阻挡、原生指针归属/路由证据与手动收起hover guard。
- `src/DeskBox/Views/WidgetWindowBase.Collapse.cs`的ScheduleSmartCollapse流程：计时回调时重取指针状态、到期重验，交互进行中不盲目收起。

本模型为独立实现，不复制其方法体或原生计时代码。吸收判定/执行分离、取消旧计时、用户介入保护；补强逐海报版本、完整目录单航班、精确取消确认、失败限次、虚拟长序列。未照搬Smart/Pinned、窗口抬层、独立配置权威，也不因有参考就宣称Windows事件路径已正确。

TBD-017已经用户确认并关闭，M08-01合同冻结。之后按M08-02设置实体/追加迁移推进，03/04才接原生观察与空间运行器。无需为纯模型重复M07人工，F03/F06按需优化不在本任务展开。

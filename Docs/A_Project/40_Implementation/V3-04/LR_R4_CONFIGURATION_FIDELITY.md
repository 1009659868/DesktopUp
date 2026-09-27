# LR-R4 配置语义保真与样板验证

2026-09-26，R4 SAVE CONTRACT + SAMPLE READY。本文保留R4当时证据，不替代[主计划§4.3](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#43-配置保真可靠性与item接入出口)或R7同包验收。长期goal、基线和文件归属见[执行记录](LR_MILESTONE_EXECUTION.md)。

2026-09-27版本对照：当前SQLite为schema12（`SqliteStateStore.CurrentSchemaVersion`），R5已接模板目录/中心主题持久化、Lamp10参数及跟随/闲置，见[R5审计](LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)。下方schema11、六参数和静态样板是R4历史范围，不是当前限制。Portable v4仍有明确边界：`PortableProfileCodec.Export`遇自定义模板目录、非light中心主题或RecipeInstances时拒绝导出，避免静默丢字段；不能把下方R4字段往返证据泛化为当前所有配置可便携，需保留完整数据库备份，R7另验支持范围及恢复。

## 1. 格式与能力边界

- R4时SQLite为schema11，没有修改旧迁移；当前schema12见上方版本对照。内容JSON支持v1～3；v3新增手动模式的完整自动返回态及人工尺寸的原始自动跨度。只有实际有语义的编辑升级；读取、无操作不写库。
- Portable现版v4支持新字段；旧v1～3仍可读，但不得冒充支持schema3。身份重映射属于明确新建操作：成员后缀/海报ID、语义与实体版本按新实例初始化；来源配置不改。
- `ContentLayoutCapabilities.Inspect`区分可存档与可实例化/编辑。已知、有限、可解但只读的Legacy冲突可以无损导出；应用/克隆/普通写入须满足能力校验。未知、损坏或不可解格式不冒充可移植格式；原数据库备份保原文。
- `ContentLayoutCommandPolicy.Prepare/ValidateTransition`同时检查源、目标、完整CAS、版本及字段语义。普通布局操作保留区域、成员区域归属和全部留白字段；本轮不开放区域/留白转换编辑器。旧硬位置不自动变图钉。旧Automatic的休眠pin不得被激活、改写或在保留数字时失效。
- 新的普通自动拖动可将无pin的旧schema升级，建立顺序图钉；撤销可精确恢复无pin旧schema。不能以一律禁止版本降级替代语义检查。
- 旧Manual无返回来源仍可执行可证明安全的同模式编辑，但不能退出A；旧人工尺寸无原始跨度不能“恢复”成猜测值。两者均保留来源并说明拒绝。
- DEMO替换只允许两端普通单区域、无硬位置、无保留区的组成；多区域Landscape不开放新建，以免前向允许后撤销被兼容门拒绝。已有多区域数据继续按兼容能力保留，此限制不删除配置。

## 2. 用户意图字段对照

| 用户意图 | 作者数据与实现入口 | 当前证据 |
| --- | --- | --- |
| 海报/成员身份与集合 | `PosterId/ItemId/ItemSetVersion`；`ContentLayoutCommandPolicy`、`PortableContentLayoutCodec` | SQLite追加/替换、执行重规划、重放、身份重映射；非目标海报不变 |
| 模式、方向、循环、辅助开关 | `Mode/FlowAxis/LoopScroll/ShowBaselineGrid/ShowSpanLabels` | 两模式往返、切轴不改模式/loop、序列撤销重做；新建纵向/循环关，旧显式值保留 |
| 顺序与位置 | `Order/PinnedIndex/Column/Row/FixedPosition` | 自动拖动仅顺序图钉；普通手动整图承接+局部推让；旧pin语义与硬位置转换反例拒绝 |
| 区域和普通留白 | `Regions`、`RegionId`、Manual坐标 | 区域身份/偏移/宽度/归属不静默改动；普通手动空隙从原意图投影，不持久化临时缩框结果 |
| 显式与装饰留白 | `ReservedSpaces`全部位置/跨度/Kind/Name | 非空双种类往返、损坏拒绝、数组快照；Explicit碰撞，Decorative不阻挡；普通编辑不清除字段 |
| 尺寸覆盖与恢复 | `ColumnSpan/RowSpan/ManualSize/AutomaticSizeBasis` | 连续覆盖保原来源、退出手动后再恢复尺寸、旧无来源拒绝；热度尺寸关时跨度不变，开时尊重覆盖 |
| 手动返回信息 | `SavedAutomaticMode`、全成员`SavedAutomaticState` | 进入仅保存原作者意图，当前呈现可含热度但不成为返回来源；伪造/清除拒绝；退出A、9次真实撤销重做事务 |
| 图像适配、网格基准及热度开关 | `ImageFit/BaseCellDip/BaseGapDip/MaximumColumns/AllowHeatDrivenSize` | Portable字段对照、确定性模型及热度两侧回归；实际统计不在本轮 |
| 外观、宏观参数、模板覆盖 | 既有presentation参数；schema3 `templates.lamp`六字段 | v1/v2兼容/legacy语义、六字段边界、旧调用不得删覆盖、双海报CAS+切模板+SQLite重开；原生绑定单独登记 |

## 3. 已执行合同与数据库证据

- 最新完整合同：`out/lr-r4-evidence/contracts-reviewed-final/contracts-reviewed-final.trx`，1868通过、0失败、0跳过。执行目录项目根，命令为`out/lr-tools/run-check.ps1 -Stage r4 -Evidence contracts-reviewed-final`，使用隔离SDK10.0.112及项目依赖缓存。
- 失败证据保留：草稿4条新失败、保存源/返回态2条、兼容字段10条、追加来源6条、热度尺寸开关1条，以及收紧检查误挡合法首次图钉1条。各自实际红测目录见执行记录，最终回归包括合法旧格式Undo/Redo的正例，未用全拒绝换通过。
- [内容事务回归](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutPersistenceTests.cs)、[模式/返回态事务](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutTransitionTransactionTests.cs)、[兼容操作反例](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutCompatibilityTransitionTests.cs)：真实SQLite的源/目标、完整CAS、失败无receipt/无revision、SQL回滚、重复请求、模式/尺寸恢复与内存历史确认。
- [Portable回归](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutPortableTests.cs)、[能力与归档回归](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutArchiveCapabilityTests.cs)：字段对照、外部旧格式载荷、v4返回态与SizeBasis、存档可读但不应用、伪造计划执行时重规划、store销毁后重开与再次导出。
- [Lamp参数回归](../../../../tests/DesktopUp.Contracts.Tests/LampTemplateParameterTests.cs)：六字段独立、边界/未知/重复/缺失拒绝；模板往返、双海报、真实CAS、SQLite重开。颜色不使用任意资源/表达式。
- [连续序列](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutStressTests.cs)迁移至两模式、自动读序拖动及热度尺寸两侧；每seed要求实际语义变化、Move/Resize变化和拒绝路径均发生，再验证重放一致。不是原生帧率或鼠标证明。

SQLite store重开与App进程重启是不同证据。目前R4合同证明前者；UI C新操作的同库进程重启、完整配置全链和故障矩阵仍由R5/R7固定包完成，不能据此声称配置永不丢失。

## 4. UI C代表模板范围

本轮样板为独立`lamp`“深夜书桌”：固定灯具、光束、光池及卡片渐变；灯色/柔和度/强度、底色/卡片色/背景不透明度共六参数。后者仅控制背景alpha，文字/卡片根不整体变透明。装饰层不命中，不另建窗口、输入所有者、计时器或业务状态；纯Lamp预览更新已有画刷，不重建卡片或求解器。

已完成标准解决方案和SessionFixture构建（0警告/0错误）。原生probe入口`--lamp-parameter-probe`限定隔离数据及单probe进程；检查真实控件、Scene/Backdrop/Card、单revision、取消、模板往返、双海报、中心重开、无效草稿与队列末值，输出PNG与资源归零。最终`candidate3-app-local`与`lamp-native-03`已通过这一级观测；App.dll SHA256=`DFCE3BCF4FB22147FADD4D3C7C2A1D15C9E5AA171E977A37D99A64AAEAB38716`，PID19140/exit0/错误0/资源0、6张PNG实际查看。首次失败及TextChanging修复见执行记录§7；未改旧候选。

截图发现：光池存在亮黄环状边缘，实际桌面合成尚待核对；此视觉问题进入R5/R7，不把控件绑定通过等同视觉通过。

限制：静态样板尚无指针跟随、雨云效果和完整模板平台；RenderTargetBitmap不含SystemBackdrop的桌面合成，因此截图不证明真实透底像素。鼠标/键盘/滚轮、UI C最终视觉及高DPI/多屏、人工作业仍分别归R5～R7。已采纳方向没有被重新选型或宣告删减。

# 可编辑内容布局设计 V1

日期：2026-09-17。状态：**L01～06 CODE/AUTO PASS；L07 MANUAL PENDING**。产品裁决归DEC-007，排期归DEC-008及[04-L计划](../30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)。[实现合同](../40_Implementation/V3-04/CONTENT_LAYOUT_CONTRACT.md)记录真实类型；[交付](../40_Implementation/V3-04/CONTENT_LAYOUT_DELIVERY.md)记录R4同包35门/资源与人工边界，设计稿和自动门不替代真实输入验收。

## 1. 要解决什么

adaptive-grid是历史等尺寸呈现参数基线，不是永久“一图标一格”的产品限制。BUG-005统一合法标识的修复保留；本轮L在独立ContentLayoutDocument中接span-grid，不改写旧presentation v1/v2语义。在真实Item之前先提供异尺寸卡片、跨格占位和安全编辑；新增、默认、复制、导入共用这一内容协议。

用户参考：
- 竖版插画图标墙：大主卡＋两张中卡＋小图标矩阵，背景有留白、弧线/装饰，图标完整contain。
- 竖版封面墙：大横幅＋双卡＋三卡＋四卡梯度，封面cover裁切、标题叠层，不等同把小图标拉伸。
- 附带1600×900 SVG：左侧标题说明、中间大竖卡和不等大小卡、右侧紧凑区域；需要横版阅读结构。
设计稿对应三种组合，示意素材不是游戏封面提取或AI生成能力。原图艺术风格、真实封面资源、字体许可由主题/素材阶段交付，不能用“跨格”冒充完整图像效果。

## 2. 三层分离

| 层 | 所有权与输入 | 输出与限制 |
| --- | --- | --- |
| 内容事实 | 未来ContentItem/Collection/Membership；L阶段用稳定DEMO ID | 来源、名称、图标/封面候选、能力；不含屏幕坐标或主题逻辑 |
| 排版意图 | 每海报的ContentLayoutDocument、ContentPlacement、自动/锚定/手动策略 | 顺序、手动跨度/位置、相对尺寸、区域关联；与真实文件位置无关 |
| 几何与皮肤 | 纯ContentLayoutSolver＋呈现适配器；主题提供有界声明 | 同一LayoutSnapshot驱动卡片、图像、标签、裁切、命中与滚动；主题不能另排一套几何 |

海报外框的碰撞/推动仍由PosterSpatialRuntime负责；卡片内部排版不能触发海报间推动或改写外框保存尺寸。现有宿主、输入岛、R4组呈现、SQLite单权威和实体CAS复用，不另建窗口/渲染服务。

## 3. 当前合同及有界数据

- ContentLayoutDocument：SchemaVersion=1、EngineId=span-grid、EngineVersion=1、PosterId、DocumentVersion/ItemSetVersion、Mode、BaseCellDip/BaseGapDip、MaximumColumns、Regions/ReservedSpaces、Placements和两显示偏好；不存在另一个持久化全局revision。
- ContentPlacement：稳定ItemId、Version、Order、RegionId、可选区域内Column/Row、ColumnSpan/RowSpan、FixedPosition、ManualSize、全海报1-based PinnedIndex、Contain/Cover。未实施aspectIntent或通用主题脚本字段。
- LogicalPlacement保存用户意图；ResolvedPlacement保存本次有效格子/矩形及降级原因。窄屏、缺素材、碰撞恢复不反写手动意图。
- ContentLayoutSnapshot：PosterId、DocumentVersion、ItemSetVersion、ViewportGeneration、ThemeMetricsVersion、cell/gap/columns、每项稳定ID/ReadOrder/Bounds/Clip及区域原点、extent/scroll/diagnostics；Anchor()派生稳定ID＋项内偏移。几何单位DIP，原生投影才换成物理像素。
- ItemViewSource：稳定身份与展示数据提供口。DEMO→真实Item只换来源，不换几何协议；同一ContentItem在不同海报用独立posterItemId。
- ContentLayoutCommand：命令ID、目标ID、完整目标海报读集、ExpectedDocumentVersion/ExpectedItemSetVersion、完整DesiredDocument及显式ReplaceItemSet；一次拖动/跨度调整/模式操作为一次提交，不能每帧写库。只有改变的placement version +1；新增成员从1开始，成员集合变化才推进ItemSetVersion。
- 未知engine/schema保留原文、只读安全兜底，禁止当成adaptive-grid强行覆盖。engineId与basic-box/night-ops等皮肤ID分开。

基线网格是精细定位单位，1格不是固定的一张图标。设计稿/三样例使用12列便于表达结构，不代表所有窗口恒定12列。当前硬界：每海报0～1000项、1～8区域、最多32留白矩形、跨度1～32、最多64逻辑列、行起点0～65535、JSON UTF-8≤1MiB。BaseCell为16～96 DIP、BaseGap为0～40 DIP，求解最多4,194,304次探测，超限明确拒绝。实际列数由固定pitch和viewport计算；卡片最小可读边长48 DIP，以有效span向上量化，preferred span不改。最小读宽与MaximumColumns不兼容时返回领域拒绝，不静默缩字。

## 4. 尺寸、跨度与重排

### 4.1 几何公式

cellRect宽度 = columnSpan × cellWidth + (columnSpan−1) × gap；高度同理。cellWidth=BaseCellDip×宏观Scale，gap=BaseGapDip×Spacing；不随列数把cell拉伸铺满。hitRect取卡片矩形与内容viewport交集；绘制/命中共用快照和物理边界取整，不能重新独立排版。当前DEMO用Contain或Cover居中裁切，尚无可编辑焦点；标签有独立受约束区域，阴影/装饰不得扩大命中。

示意跨度：

| 样例 | 主卡 | 次级 | 小卡 | 要证明的能力 |
| --- | --- | --- | --- | --- |
| 插画图标墙 | 8×8（可保留侧边留白） | 6×6 | 4×4 | 非铺满也合法；图标居中、背景不抢输入 |
| 封面层级墙 | 12×6 | 6×5 | 4×4、3×3 | 大横幅/竖卡共存，封面焦点与文本不变形 |
| 横版混排 | 5×8 | 4×4、3×4 | 2×2、3×2 | 大卡旁填充、区域阅读顺序与留白 |

这些是样例，不把所有卡片限制成三个按钮档位。编辑器支持整数跨度、比例预设和逐格微调，卡片可比1格大很多；模板可声明可用跨度范围和最小内容尺寸，不能声明任意执行脚本。

### 4.2 自动、固定与全手动

- 自动：上游稳定排名/层级→偏好尺寸→合法跨度→确定性排版；首次无热度用中性层级。布局不计算启动热度。
- 锚定+自动：原有1-based pinned index仍是顺序锚，不偷换成屏幕像素。新“固定位置/尺寸”用独立字段；出现冲突显示原因，不能悄悄取消固定或删除Item。
- 全手动：拖卡片改变区域内位置、把手改变跨度；业务Order与位置分离，不把主题region顺序偷换成业务排名。热度不改位置/排序/尺寸。退出未选保持Manual：KeepOrder转Automatic并固化当前业务顺序、清位置/固定/pin而保留尺寸覆盖；ConvertToAnchors转Anchored并写全局顺序锚；RestoreAutomatic清手动覆盖、回中性4×4。
- 手动尺寸覆盖优先于热度尺寸；解除覆盖才恢复热度。所有动作保留撤销信息，不把预览帧或统计刷新各算一次操作。
- 当前求解器用稳定逐行扫描＋共享跨度占位图；所有固定/手动位置先占位，之后流动项维护行游标、不回填已越过的行。region只组织几何，阅读顺序仍按全海报Order/pins。显式ReservedSpaces参与同一占位约束且计入extent；无Item的留白仍有内容范围，不建第二个行流模型。

固定障碍/留白冲突拒绝整次编辑；Manual中的非固定邻项确定性向下推让。逐海报RAM历史上限64：成功CAS后Record；Undo/Redo先提案，按当前成员/语义重新归一化版本，CAS成功再Confirm移动栈，失败不吞记录。重启不保留撤销栈。

### 4.3 窄窗口与尺寸边界

外框resize只改变viewport/列数/有效投影，不修改baseCell、宏观尺度、手动跨度、热度层级或文字字号。Automatic/非固定内容超出可用列宽时临时clamp有效宽跨度，窄区域可向下折叠，保留首选span及原区域；Manual/Fixed保持逻辑位置和宽度，使用水平滚动，不静默夹窄。低于可读最小宽仍保留可滚动占位或明确拒绝；固定位置与最小可读扩张冲突不偷偷重叠。宏观参数及便携输入也须验证组合后的可布局性。

竖向始终可连续滚动；内容可以部分进入/退出viewport，不能要求完整一行可见才渲染/命中。滚动入口独立于是否存在完整卡片。锚点为稳定ID＋项内偏移，重排和热度刷新不无故跳回顶部。

## 5. 热度模型的接口，不提前偷做公式

现有50%～300%、无数据默认值、30天保护、衰减待决、交互期间延后重排继续有效。三层分别为基准度量、当前海报宏观偏好、Item相对层级。

ContentHeatProposal包含PosterId/ItemSetVersion/Version、稳定rank、RelativeScale与理由；当前量化版本1，五档长度倍率0.5/0.75/1/1.5/3，相邻中点±0.05迟滞。无数据中性1.0只乘一次，始终从authored intent推导有效span，不将上轮投影反复相乘；面积自然派生。ManualSize阻止热度改尺寸，Anchored中的全局pin保护阅读序位。提案仍需通过空间求解；固定项增长冲突时保留last-good，不持久化无效投影。

“内容大小”改共同基准，“疏密”改间距/留白，二者不改打开次数、排名或手动顺序。编辑/拖动/菜单/选择进行时冻结本次布局；新的热度提案只保留最新待用版本，手势结束再统一应用。L阶段用注入层级测试，实际统计/衰减仍在V3-06。

## 6. 编辑体验与输入仲裁

2026-09-16用户确认必须采纳两项可视辅助，作为**两个独立开关**：

- 基线网格：独立显示/隐藏网格线；关闭可视网格不关闭底层网格定位或改变布局。
- Item跨度提示：独立显示/隐藏各Item左上角的“列跨度 × 行跨度”（例如4 × 3），不依赖网格是否显示。
- 四种开关组合已由两个独立布尔字段与UI入口支持，辅助层只在编辑态绘制，不占内容空间、不抢点击/拖动、不改变排序、跨度或热度。当前实现假设为**默认均on、逐海报持久化**；用户确认的是必须独立开关，默认值/保存范围不能写成已获确认。有效跨度与首选不同时提示两者。
- HTML设计稿的旧联动开关是原型历史，不是产品状态；生产两开关仍须L07四组合人工验收。

- 海报内容区为主编辑场所；标题栏仍移动整张海报，边缘仍缩放外框。卡片选中后才出现跨度把手，不把所有区域都变成拖动热区。
- 自动模式拖卡片须明确转入手动或固定操作，不能一次按下既启动文件又改布局。取消恢复本次可信起点，不覆盖外部新revision。
- 拖动预览：捕获与阈值沿现有输入门，保持抓取点；显示合法占位影子和受影响卡片；松手一次CAS。碰到固定卡片拒绝该落点；未固定卡片以稳定推让/重排提案调整，不修改内容事实。
- 快速移动只消费最新位置，现接入单手势16ms合并，释放前补算最新点；边缘滚动计入相对初始快照的累计scroll差，手势期间禁止普通重排锚覆盖该offset。键盘方向键逐格移动、Shift调整跨度由控制中心明确焦点区域发同一命令；海报输入岛继续NOACTIVATE，不宣称原生海报已拥有键盘焦点。
- 滚动边缘自动滚动是输入适配行为，不向热度制造活动事件；现有候选近边24 DIP以8 DIP/16ms推进，距相应边界超过64 DIP停止该方向，参数属本轮实现常量、尚待真实手感验证。原生capture中Esc由33ms临时timer观察，退出即释放；失焦/删除/锁屏/显示代次变化取消旧提案。结合BUG-001保留原有失败恢复门，不在本阶段声称该旧缺陷已修复。
- 常用操作即时预览、完成后保存，无额外逐项应用。失败显示原因并恢复权威布局；撤销/重做也走版本校验。

## 7. 呈现、动画与热更新

ContentLayoutSolver→不可变LayoutSnapshot→呈现/裁切/命中三者同帧代次；过期snapshot不能被输入端采用。动画期间预布局、容量和内容租约稳定，沿R4提交/结束恢复经验；卡片内部重排不能自己绕过窗口提交门。最小阶段先无动画或短静态预览，稳定后再动画，不以动画优化阻塞基础能力。

虚拟化按viewport交集和有界overscan；不为全部Item创建永久控件、窗口、timer或每项全尺寸封面。ContentAssetCache端口已实现：key含海报/成员/会话/文档/集合/placement/素材/像素尺寸/DPI/主题版本，默认64项/16MiB、单载荷4MiB、并发4；取消但不配合的provider仍占并发槽直到真实完成，晚到旧代次不能重挂。当前只有Null provider及DEMO内建绘制，真实文件/网络/解码与位图呈现不冒充已接通。

后续完整主题的ThemeComposition需要声明标题/介绍/内容/底栏/装饰槽、图像fit、可读字号与裁切；本轮不是已交付通用ThemeComposition解释器。现有主题端口区分纯颜色与度量：前者不重排，后者生成新snapshot并在编辑保护结束后应用；失败保留last-good。新布局不依赖任意XAML注入、脚本或本机代码。左右分栏先由显式regions表达，不把SVG当可执行主题直接加载。

## 8. 持久化和04-06-A衔接

- schema8修BUG-005、schema9增加04-06待安置；本轮追加schema10 nullable content_layout_json，保留全部旧迁移校验和与备份流程，不改presentation v1/v2语义。null旧值虚拟读取Equal六项，不在查询时写库；明确空内容保存合法空Placements。
- span-grid使用独立布局schema/engine版本1，保存在PosterDefinition聚合；不是单独SQL placement权威，不复用旧grid歧义别名。窗口不读一套、便携不另排一套。
- PortableProfile本轮扩展为v2携带布局逻辑载荷，仍接受无新载荷的v1；v1夹带新布局拒绝。portable载荷不携内部文档/集合/成员CAS版本，导入和克隆重映射owner/成员ID并从1开始。未知必需engine/schema拒绝应用并保留源文件；本地未知原文只读保留不是允许导入未知版本。
- PortableProfile存逻辑布局、作用域和稳定关系；不存HWND、捕获、动画帧、滚动中的暂态、内部CAS/revision。真实Item还未存在时布局可空；导入后ID映射必须覆盖placement引用，未知/缺失素材用占位，不下载或搬文件。
- 内部数据库灾难备份≠便携用户配置。04-06未验余项和04-07/08专项收口已按DEC-008延期；L阶段只补新增布局的迁移、往返/追加克隆ID映射、未知版本、事务与资源验证，不预先补完整套旧计划。再接真实Item仍受原文件安全合同约束。

## 9. 最小出口与明确非目标

L阶段出口：一套纯求解/编辑/持久化合同，DEMO可表现三类参考的结构，等尺寸网格退化可用；多海报互不改写；可滚动、可选中、可调整跨格、可撤销、重启保持；生产新增和旧数据统一。

本阶段不实现：实际文件拖入/移动/缩略图提取、正式热度公式、AI生成封面、完整主题商店、无约束像素自由画布、卡片相互遮挡和任意旋转。后者不等于永不支持；本次高自由度先以可编辑跨格和受约束组合实现，防止Item地基变成无限画布项目。

## 10. 当前验证界线

本批自动合同门1630/1630 PASS、0skip；连续属性使用240条固定种子40步序列并双重放，检查每步安全/确定性/版本/拒绝不变/宽窄无漂移，另有真实SQLite撤销重做、便携/参数组合与异步缓存生命周期测试。证据为`out/verify/v3-04-l/results/content-layout-full-r2.trx`，后续输入接线变动须重新绑定最终构建证据。

生产隔离进程与1/3/6海报、0/100/1000内容资源门已在最终R4通过，退出资源0。L07真实鼠标、把手、滚轮、键盘、四辅助组合、R4像素过程/终态及交互独立性仍待人工；Canvas/命中读回不是原生输入或视觉PASS。具体证据与限制见当前C交付。

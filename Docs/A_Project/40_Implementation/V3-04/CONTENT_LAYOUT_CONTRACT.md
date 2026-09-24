# V3-04-L 内容布局合同

**最新DEC-020（设计决定，非实现修订）**：手动局部让位、每次进入手动取当前完整画面、适配编辑合法提交才承接新图且可撤销。普通投影及退出覆盖候选见[手动专项](../../20_Architecture/CONTENT_LAYOUT_MANUAL_COMPOSITION_REVIEW_V1.md)；下方旧Mode清坐标/Y向推让不作为新合同已实现证据。

**2026-09-24 DEC-019优先**：两种模式（纯手动/热度自动）均可桌面按住移动即拖，不先点整理；手动位置/留白与中心精调已确认。原IsEditing前置/统一顺序拖动方案不再是新目标。[需求§13](../../10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md#13-两种布局模式与直接操作)与[组合稿](../../20_Architecture/CONTENT_LAYOUT_PIN_DRAG_REVIEW_V1.md)维护新要求；下方是旧实现事实，未修订生产/载荷。旧数据保真不变。

**前一轮DEC-017/018记录**：UI C已采用；当时确认整理拖动固定顺序位。如今由DEC-019取消入口前置并将该规则限定于自动模式。下方Move/FixedPosition/Manual仍是旧产物事实，不是新接线；待LR-R1组合收口冻结新命令/载荷，旧数据保真。

**2026-09-23适用范围**：本文保留旧实现合同及其产物语义，不是当前dirty工作区的完整现状。轴/循环、三模式/位置覆盖、最小边界和数据保真已有分歧，见[审查](CONTENT_LAYOUT_REVIEW_2026-09-23.md)。[整改设计V2](../../20_Architecture/CONTENT_LAYOUT_REMEDIATION_DESIGN_V2.md)仍DRAFT；待产品评审后再逐条修订本合同，不能先实现或以新提案覆盖旧数据含义。

日期：2026-09-17。状态：**L01～06代码与自动门通过；L07人工待验**。验证范围与候选按交付/日志，不将原生输入或视觉预填PASS。

对应[04-L计划](../../30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)、[设计V1](../../20_Architecture/CONTENT_LAYOUT_FOUNDATION_V1.md)。本文记录本轮真实类型和接线边界，不将 HTML 设计稿、旧候选验收或单次纯模型结果替代当前产品验证。最终构建、测试数量、候选指纹和人工状态由当前 VERIFICATION/HANDOFF 统一登记。

## 1. 目标与范围

在真实 Item 接入前，以稳定 DEMO 内容交付多格卡片布局、三参考结构、连续滚动和安全编辑。文件/Shell 身份、真实拖入与移动、启动统计、热度公式、网络封面、AI 及主题商店不在 L 阶段实现。

- 海报外框由现有 PosterSpatialRuntime 管理碰撞、收展、推动和恢复。
- 内部卡片只改内容布局，不能为了排列卡片自动扩大海报或推动其他海报。
- 复用现有 PosterVisualWindow、输入岛、组呈现 R4、SQLite committed revision 与命令通道。
- 视觉辅助、标题和图像不得独立计算另一份可点击几何。

## 2. 身份、版本与保存位置

代码入口：Domain/ContentLayout.cs、Domain/AuthoritativeState.cs、Application/ContentLayoutCommand.cs。

| 类型/字段 | 含义与约束 |
| --- | --- |
| ContentLayoutDocument.PosterId | 唯一所属海报，非标题或列表索引；不允许跨海报引用 |
| ContentPlacement.ItemId | 当前 L 阶段稳定 membership ID，必须以前缀 PosterId + `:` 开始；不等同未来真实文件路径 |
| ContentPlacement.Version | authored placement 语义版本；只有发生变化的项 +1，新项从1开始 |
| DocumentVersion | 每次有意义的布局操作 +1；预览帧不推进，纯版本变化为no-op |
| ItemSetVersion | 只有成员 ID 集合变化才 +1；普通排序、位置、跨度和辅助开关不改变它 |
| ContentLayoutJson | 保存在 PosterDefinition / posters 聚合的独立 nullable JSON 列；不是改写原 presentation 参数含义 |
| SchemaVersion / EngineId / EngineVersion | 当前为1 / span-grid / 1；与 basic-box / night-ops 皮肤标识分开 |
| ContentItemView / IContentItemViewSource | 展示数据端口；当前 DemoContentItemViewSource 只返回名称/缩写/颜色，不读取或执行用户文件 |

null ContentLayoutJson 按虚拟 Equal 六项读取，查询不会将其物化写回，也不推进 revision。明确空内容必须保存一个 Placements 为空的有效文档；**空内容不等于零海报**。

克隆/创建/便携导入分配新海报 ID 后，必须重映射全部成员 ID，文档/集合/成员版本从1开始，不沿用旧海报命中身份或 CAS 版本。新建配置中的临时 PosterId 与其文档 owner 必须一致。

当前存储方案是**逐海报聚合 CAS**，不是独立 placement SQL 行或逐项数据库事务。placement 版本表达文档内语义变更，不形成第二个持久化权威。

## 3. 有界文档与基线网格

当前实现的硬边界：

| 项 | 上限/范围 |
| --- | --- |
| 每海报成员 | 0～1000 |
| 每项行/列跨度 | 1～32整数 |
| 逻辑列 | 最多64；文档 MaximumColumns 为2～64 |
| 逻辑行起点 | 0～65535，最终跨度仍须通过求解边界检查 |
| 内容区域 | 1～8 |
| 显式留白 | 最多32个矩形 |
| 单布局 JSON | UTF-8不超过1 MiB，深度不超过24 |
| BaseCellDip | 16～96 DIP |
| BaseGapDip | 0～40 DIP |
| 求解探测预算 | 最多4,194,304次，超出明确拒绝而非无限循环 |
| viewport | 宽/高各不超过1,000,000 DIP；有限非负数 |

宏观大小 Scale（0.8～1.5）和疏密 Spacing（0.5～1.5）是已有整体偏好投影。网格几何：

- cell = BaseCellDip × Scale。
- gap = BaseGapDip × Spacing。
- 跨度 n 的长度 = n × cell + (n − 1) × gap。
- 坐标保存在逻辑格；DIP仅属于求解快照，原生投影最后统一换算为物理像素。
- 最小可读卡片48 DIP，由有效跨度投影保障；不得通过缩小字体来掩盖过窄区域。读宽与列上限无法兼容的文档必须明确拒绝或保持可滚动的有效占位，不能遗漏内容。

未知字段、重复 JSON 成员、非有限值、重复/外来 ID、非法 enum、缺少区域、非法跨度和位置明确拒绝。格式验证不等于逻辑位置已可放置：固定/手动占位、区域与留白的冲突还必须通过求解校验。

## 4. 区域与显式留白

ContentLayoutRegion 使用 Id、Order、Column、Columns、Row。Placement 的 Column/Row 是所属 region 内的位置，求解结果保存 RegionColumn/RegionRow 以供编辑坐标转换。

- 自动排版按区域 Order、Id 和项的稳定顺序求解。
- region.Columns=0 表示使用可用范围；非零表示显式区域列宽。
- 自动区域在窄宽下可以折叠到后续行，产生诊断；手动或固定位置保留用户意图，不偷偷搬迁。
- ContentReservedSpace 是有意留白，不是漏排项；参与同一个占位图，不能被自动卡片填补。
- 保留区和卡片都进入内容 extent；留白必须随持久化和便携往返保留。
- 自动流维护阅读行游标，选择稳定首个合法位置，不回填已越过的行。
- 求解顺序不得依赖无序集合枚举、主题控件创建顺序或原生 HWND 层级。

## 5. 三模式、固定与手动覆盖

| 模式 | 排序/位置规则 |
| --- | --- |
| Automatic | 按稳定 Order/ItemId流式排布；拖动前需明确进入手动或执行固定，不把单击同时当启动和编辑 |
| Anchored | PinnedIndex是全海报1-based阅读序号，不是像素位置；未锚定项填其余序号，固定位置是独立字段 |
| Manual | 保存region内位置和手动跨度；热度不能自动改位置或排序，冲突不得静默取消固定或删除项 |

FixedPosition 与 ManualSize 是不同维度：前者固定逻辑位置，后者阻止热度覆盖尺寸。解除其中一个不隐式解除另一个。

编辑提案按 ContentLayoutEditing 的同一入口形成；手动移动中，固定障碍导致目标拒绝，非固定邻项可作确定性向下推让。手动模式退出未作选择时保持 Manual。KeepOrder 转 Automatic，固化当前业务顺序、清理手动位置/固定/序号锚，保留显式 ManualSize；ConvertToAnchors 转 Anchored，写入全局顺序锚；RestoreAutomatic 转 Automatic，清除全部手动覆盖并恢复中性4×4。独立 SizeOverride.Enabled=false 也恢复4×4且清除 ManualSize。

撤销/重做是逐海报内存历史，最多64项；仅成功提交后记录。先 ProposeUndo/Redo，再执行同一 CAS，成功后 Confirm 移动历史指针，失败不能吞掉一次撤销。重启不承诺保留撤销栈。

## 6. 窄宽、裁切和连续滚动

ContentLayoutSolver.Resolve 输出 ContentLayoutSnapshot：

- owner、DocumentVersion、ItemSetVersion、ViewportGeneration、ThemeMetricsVersion；
- columns、cell/gap、extent、scroll；
- 每项稳定身份、阅读顺序、完整 Bounds、与 viewport 的 Clip、响应诊断。

Automatic/非固定内容的首选宽超过可用区域时，可压缩**有效跨度**并重排，保留原 ColumnSpan/RowSpan；Manual/固定逻辑坐标保留并允许水平溢出滚动。外框 resize、DPI、滚动和受限投影不写回用户跨度、基准字号或宏观偏好。

滚动使用 extent − viewport 的连续 DIP 范围，不按完整行可见性决定内容是否存在。锚由稳定 ItemId＋项内偏移＋原阅读序号组成；原项消失时选择确定性后继，必要时最后一项，无项才归零。部分卡片仍可见/可命中，viewport中没有完整卡片时也必须可滚动。

Reclip 复用完整几何，只更新 clip/滚动和viewport代次；R4动画不能每帧改写文档或重排整段内容。呈现应只实例化可见项和有界 overscan，不为1000项创建常驻窗口、计时器或全量重型控件。

## 7. 两个独立显示偏好：默认值待确认

用户已要求必须有两个互不联动的开关：

1. ShowBaselineGrid：显示基线网格。
2. ShowSpanLabels：在 Item 左上角显示跨度。

**当前采用默认均true、逐海报持久化，作为实现假设交L07体验；这不是用户明确确认的默认值。**若反馈调整，统一修改默认、迁移/便携和夹具，不在各界面各自解释。

两开关四种组合必须支持。关闭网格不改变网格吸附；关闭提示不改变 Item 身份、位置、跨度或热度。辅助绘制不占排版空间、不新增命中目标。提示需明确使用 authored span 与受限有效 span 的区别，不能把响应性压缩误报成已修改用户设置。

## 8. 热度与主题端口

既有50%～300%采用**长度倍率语义**：作用于相对目标宽/高，面积自然派生，不再平方一次。无数据中性倍率1.0只应用一次；宏观 Scale 与 Item 相对倍率分别拥有各自输入，不重复乘入默认尺寸。

当前纯适配端口已实现 ResolveHeat 与 latest proposal buffer，五档为0.5/0.75/1/1.5/3，按相邻中点量化、±0.05迟滞，策略版本1；测试证据须在本轮交付中核对，不把端口实现写成真实热度产品已验收。Manual不自动改rank或size；ManualSize保持显式覆盖。真实计数/衰减/启动行为仍归V3-06。

主题仅供受约束的图像fit、颜色、槽位与度量。图标Contain保持完整，封面Cover按内容框裁切；L阶段示意素材不构成真实封面/网络下载能力。纯颜色更新只改绘制，度量变化生成新snapshot；交互期间保留最新待用提案，安全点再应用。失败保留last-good，不执行任意主题脚本或XAML代码。

## 9. 单聚合命令与未知数据

ContentLayoutCommand 输入：RequestId、Source、ExpectedRevision、TargetPosterId、StateReadSet、ExpectedDocumentVersion、ExpectedItemSetVersion、DesiredDocument、ReplaceItemSet。

- 仅海报/控制中心来源，读集必须含目标海报完整 entity version。
- 文档版本恰好 +1；只有改变的 placement version +1；成员变化显式 ReplaceItemSet 且 ItemSetVersion +1。
- 一次拖动、跨度调整、模式切换或辅助显示修改是一次语义提交；鼠标预览与滚动不写库。
- 通过现有 StateProjectionCoordinator/SqliteStateCommandRepository 原子提交，一次 committed revision，三端仍读同一状态。
- 查询未知本地 engine/schema/非法文档：保留原文，内容只读安全回退，禁止当作空白布局覆盖。
- 便携配置遇未知必需布局版本：拒绝应用，保留源文件；这与本地只读保留不是同一行为。
- 便携载荷使用文件内成员key，不能携带本机CAS/revision、捕获、滚动暂态、HWND；导入/追加/克隆重新映射 owner/membership。
- L03追加schema10的内容JSON列，不重写历史迁移，也不改现有presentation v1/v2参数语义。失败、备份、source hash与原子导入复用04-06边界。

## 10. 原生输入与生命周期

当前输入扩展复用 PosterInputIslandManager：ContentItem、ContentSpanHandle、ContentViewport 三种目标；旧Item/标题/外框语义保留。Pressed仅在原生SetCapture成功后发送，适配器同步Handled=true接管，未接管则取消，不回落旧Item或外框路径。

事件携带原捕获basis、稳定ItemId、原ContentSnapshotGeneration、最新屏幕点、Sequence和取消原因。LayoutRevision指当前呈现输入代次，不能替换原文档读集。五物理像素阈值保持；释放补取最新位置，最后Move未送达不能误判成单击。

- 卡片/把手优先于内容空白；标题移动海报，内容不能误入外框移动。
- wheel沿同一region命中；Shift+wheel可水平滚动，不全局拦截键盘。
- Esc仅持有本应用内容capture期间用原HWND的33ms临时timer检查，释放/取消/销毁即移除并释放ledger。
- 岛保留NOACTIVATE；不以新增窗口或全局键盘hook获取输入。
- 当前键盘精调由现有控制中心的明确焦点编辑区域发送同一布局命令，不宣称海报自身已有原生键盘焦点。
- 内容编辑进入既有共享交互保护；自动收展、主题度量变化、外部提交不能夺走本次手势。
- 失焦/捕获转移、删除、切海报、隐藏、锁屏、桌面/显示代次变化必须取消或拒绝旧提案，不能重定向到另一海报。
- R4仍保持内容预布局、整段稳定、统一窗口提交、结束恢复；不另建呈现宿主。

## 11. 三组可检验几何与候选

以下来自 ContentLayoutSamples，不是艺术资源完成声明。三样例均为Manual，MaximumColumns=12，gap=6 DIP：

| 样例 | 基线cell | 逻辑构图 | 图像fit |
| --- | --- | --- | --- |
| Illustration | 32 DIP | 主卡(0,0,8,8)，中卡(0,8,6,6)/(6,8,6,6)，两行4×4小卡；保留(8,0,4,8)留白 | Contain |
| Covers | 32 DIP | 12×6横幅；两张6×5；三张4×4；四张3×3 | Cover |
| Landscape | 40 DIP | main区域列0..4的一张5×8；side区域从列5起宽7，4×4/3×4/2×2/3×2混排 | Cover |

Equal是每项4×4的自动基线；0/100/1000项仍使用同一DTO、求解器、DEMO source和命令，不启用第二套特殊网格。所有稳定ID归属当前海报；示例切换也需完整身份/CAS处理。

已实现的隔离fixture语法：

```powershell
DesktopUp.SessionFixture.exe <NEW_ROOT> --content-layout <equal|illustration|covers|landscape|three|empty|hundred|thousand|resource-one|resource-three|resource-six>
```

这里只允许新独立目录，不覆盖日常profile或旧人工配置；empty是一张空内容海报，three是三个参考分别持久化到不同海报，thousand是独立1000项profile。具体启动包、哈希和人工入口由构建后的候选manifest绑定，本合同不提供虚构PASS。

资源对比单独使用 resource-one / resource-three / resource-six：每张均为 basic-box、300×360 DIP、展开、未锁定、自动收展关闭、六项4×4 Equal 自动网格、相同外观参数；只改变数量、身份及必要摆放位置。复用 MeasurementFixtureData 的位置/尺寸，不复制旧夹具的混合主题。three 参考构图及编辑完成后的状态仅作功能门，不代替三张同负载基线。

2026-09-17最终候选为`out/verify/v3-04-l-r4`；r1/r2/r3及失败日志保留。7场景×初跑/重启＝14布局门、相关21回归门已通过；25文件指纹与35门绑定人工入口。每次12个idle样本、时长≥10秒、退出账本0；测量与限制见[交付](CONTENT_LAYOUT_DELIVERY.md)，不称GPU/FPS或真实鼠标验收。

## 12. 本轮验证范围（自动证据见交付，原生/视觉待L07）

- 纯门：0/1/100/1000、窄宽、区域/留白冲突、确定性/终止性/连续操作无漂移、版本与回放、portable关系不丢失。
- 生产门：draw/hit同snapshot、真实控件修改/保存/重启、viewport部分卡片、输入岛捕获/取消、R4收展过程与终态。
- 资源门：1/3/6海报，0/100/1000项增量；不能把纯模型耗时称为窗口渲染帧率。
- 人工门：真实首次按下、拖动/跨度把手、滚轮/Shift滚轮、明确焦点键盘、两个辅助开关四组合、三构图及跨海报独立。
- BUG-001及其它延期问题不因本合同新增而关闭。旧阶段DEFERRED不补填PASS，不扩大本轮认证范围。

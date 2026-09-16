# V3 当前实现与后续计划对照审查（2026-09-15）

性质：代码静态审查 / 开发入口盘点，不是新一轮功能验收。审查当前 `impl/poster-redesign-V3` 工作树（含前轮未提交改动），不是只看 HEAD，也不保证旧启动包包含当前源码。未构建、未运行测试或应用；下列“已有”只表示代码/调用路径存在，历史验证范围查各阶段证据。

## 1. 结论

本审查为04-04-01实施前快照；后续新增纯编辑器与验证见[编辑合同](CONTROL_CENTER_EDIT_CONTRACT.md)。旧盘点不代表后续仍未实现，01也不等于界面已接入。

**有部分能力早于原阶段编号完成，但主要是 M01/M02/M04/M05/M08 为多海报与自动收展提前交付的基础，不是 V3-04-04 或之后阶段整体完成。** 总计划已经多处写明“复用”；仍有“新增汇总”等措辞落后于实现，容易造成重复制作。

后续应按“已有可操作入口 → 已有底层能力 → 真正缺口”实施，不能只看任务编号，也不能把命令存在、测试专用入口或单独字段当成完整产品能力。[V3-BUG-001](KNOWN_ISSUES.md)仍未解决，按用户要求暂缓修复。

## 2. 实现与计划矩阵

| 范围 | 当前实际代码 | 与原阶段的关系 / 后续缺口 |
| --- | --- | --- |
| 控制中心目录、选择、锁定/收展/显隐、删除确认 | [ControlCenterWindow](../../../../src/DesktopUp.App/ControlCenterWindow.xaml)、[选择模型](../../../../src/DesktopUp.Application/ControlCenterSelection.cs)、[Service](../../../../src/DesktopUp.App/ControlCenterService.cs)已有真实控件、目标化命令与共用删除确认 | M04-01/06等已提前接入；04-04复用，不再从零建列表、删除弹窗或唯一窗口 |
| 自动收展设置草稿 | [AutoPosture控件](../../../../src/DesktopUp.App/ControlCenterWindow.AutoPosture.cs)、[专用Editor](../../../../src/DesktopUp.Application/ControlCenterAutoPostureEditor.cs)已有开关/秒数/应用取消、冻结目标版本、过期反馈与交互保护 | M08-05已有；仅自动设置，不等于位置/尺寸/主题的通用编辑器 |
| 内容/主题参数 | [参数策略](../../../../src/DesktopUp.Domain/PosterPresentationParameters.cs)、[参数草稿](../../../../src/DesktopUp.Application/PosterPresentationDraft.cs)、SetPresentation已有验证/序列化/实体CAS，renderer已有两种内置模板与独立度量 | M05基础已接；中心仍显示“编辑面板后续接入”，写入示例在PresentationSmoke/MultiInstanceSmoke；缺日常编辑入口，不是完整主题平台 |
| 新增、复制配置、重命名 | [StateCommandPolicy](../../../../src/DesktopUp.Application/StateCommandPolicy.cs)与[Coordinator](../../../../src/DesktopUp.Application/StateProjectionCoordinator.cs)已有实现；Create/Clone生成新PosterId/ThemeInstanceId，清CollectionId；有仓储/命令测试 | M01/M02底层已做；中心和托盘均无新增/复制入口，中心无重命名控件；尚缺合法落位、无空间反馈与用户闭环 |
| 位置/尺寸 | 已有Move/Resize命令、M04真实海报拖缩、有效几何与完整空间事务；中心只有尺寸/受限高度文本 | 缺控制中心数值草稿、应用/取消和空间提案适配；不能把真实海报可拖缩等同于中心已能编辑 |
| 托盘汇总/批量锁定/基础入口 | [TrayService](../../../../src/DesktopUp.App/TrayService.cs)已有开中心/双击/编辑/全部锁定/暂停/退出；Coordinator已生成总数、可见数、锁定数，ApplyProjection更新tooltip与“部分已锁定” | M02及此前阶段已有；04-05不是从零做汇总。仍缺新增、明确单张目标入口、全局隐藏/恢复等完整作用域；当前编辑依赖中心选择 |
| 备份/配置 | [SqliteStateStore](../../../../src/DesktopUp.Infrastructure/SqliteStateStore.cs)已有迁移前备份、在线备份及ValidateBackup；[IStateStore](../../../../src/DesktopUp.Application/StateStoreContracts.cs)无恢复/便携导入导出入口 | 原04-01已有基础，不表示04-06完成；损坏初始化返回Failed，不自动恢复last-good；单文件跨机映射/预览/确认导入仍缺 |
| 中心生命周期、多海报退出恢复 | Service已有窗口释放/重开，App退出释放会话、托盘、订阅等；M03/M06/M07/M08有相应基础与历史分项验证 | 04-07/08应覆盖新增预览/编辑资源和剩余场景，不再重写普通开关窗/退出；也不能用旧验证代替新增功能验证 |
| 真实Item、热度、AI、完整主题 | [PosterVisualWindow](../../../../src/DesktopUp.App/PosterVisualWindow.cs)仍持有静态六张PosterItem，文案DEMO ITEMS；当前Domain/Infrastructure未见真实Item仓储/文件执行/热度评分/AI或主题包加载闭环 | 不属于“已超前做完”；图标尺寸、颜色和模板字段只是参数基础 |

## 3. 三个不能直接当成“已实现”的边界

### 3.1 新增/复制尚无安全落位闭环

StateCommandPolicy.Create/Clone主要复制配置并分配身份；Clone沿用源配置几何。SqliteStateCommandRepository.ValidatePlan验证字段、身份与读集覆盖，不在这里计算真实工作区内的空位。合同测试证明身份/CAS/重放，不证明正常桌面入口能无重叠创建。

因此04-04-04需复用现有身份/事务端口，补空间提案及完整依赖验证，不能只加一个按钮直调Clone就宣称完成。本项为接线前缺口，不是在本轮复现了用户界面的重叠Bug。

### 3.2 中心Move/Resize分支不等于完整几何编辑器

App.ExecuteControlCenterPosterCommand对收展调用既有空间运行器；Move/Resize目前调用BeforeManualIntent后执行命令。SpatialGestureCommitPolicy.Validate只在带SpatialGesture时验证轨迹/完整读集，未带该字段会返回无该项错误。现有中心CreatePosterCommand仅产生锁定/收展/显隐，不产生数值几何提案。

04-04-01/03必须先明确草稿与空间提案/呈现/单revision提交边界，再接字段；不能依赖“已有Move枚举”推定用户入口已安全完成。

### 3.3 “暂停动态效果”不能等同于“全局暂停自动收展”

按钮已写入DynamicEffectsPaused，投影/tooltip已同步；[PosterVisualWindow.Feedback](../../../../src/DesktopUp.App/PosterVisualWindow.Feedback.cs)用该值关闭空间不足按钮抖动。然而[ReadPassivePointer](../../../../src/DesktopUp.App/App.PassivePointer.cs)及[自动采样](../../../../src/DesktopUp.App/App.AutoPosture.cs)未消费该值为自动收展暂停保护。

所以既有按钮/持久化不是完整全局自动暂停，也不代表整段收展动画均遵守减少动效。04-05应核定作用域与实际接线；按需动效优化仍按既有延期决定，不在本轮扩项或修改。

## 4. 现有测试如何复用

只读核对以下测试源码，不声称本轮运行通过：

- ControlCenterSelectionTests：目录/隐藏目标、固定ID、切换后不串写、过期投影和零目录。
- ControlCenterAutoPostureEditorTests：脏草稿、目标变化、迟到回复、取消、实体冲突和同步重入。
- PosterPresentationParameterTests：参数独立/校验、草稿原目标/CAS、受限几何与保存意图。
- ScopedStateCommandTests：Create/Clone身份、批量单revision、完整读集、冲突/故障回滚、删除及重放。
- SqliteStateStoreTests：备份校验、迁移前备份、损坏库不被替换；不证明恢复流程/跨机导入可用。

新增验证重点是这些基础之间的产品接线和安全边界；不原样重建测试或沿用旧测试总数宣称新功能通过。

## 5. V3-04-04的整体目的

**把已有“能操作几项状态的控制中心”，补成用户可管理多张海报布局与基本样式的日常管理入口。**

用户应能在一个窗口中看清所有海报及选中目标，管理创建/复制/重命名/删除，编辑位置、尺寸与最小内容/主题参数；理解哪些是未应用草稿、保存意图、临时空间受限结果，并得到明确应用/取消/冲突反馈。所有操作使用现有权威状态和空间服务，关闭中心不影响桌面海报运行。

本阶段不是重建宿主、另做复杂设计软件或立即实现真实文件收纳/热度/AI。首个预览增量是概览与选择，不把它冒充完整交互预览需求已经验收；后续范围与验收按需求和活动计划继续细化。

当前[04-04计划](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)仍处准备阶段；下一项04-04-01应以本矩阵为输入，只补通用编辑合同与纯草稿/命令验证。审查不改变M08未关闭、Bug延期或发布前安全要求。

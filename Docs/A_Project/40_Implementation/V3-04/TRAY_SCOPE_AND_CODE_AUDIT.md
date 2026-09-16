# 托盘范围收敛与当前代码／计划审查

日期：2026-09-15。类型：HISTORICAL STATIC AUDIT / 实施前范围快照。下文“待退役/未接线/TBD-020未决”是当时事实；后续用户已确认隐藏期间新增保持隐藏，接线及验证见[TRAY_DELIVERY](TRAY_DELIVERY.md)，不以本快照覆盖当前状态。

## 1. 结论与分类

托盘只提供新增、中心、临时全局隐藏/恢复、全部锁定/解锁、动效暂停/恢复、退出；双击唯一中心。存在少量不再需要的单张托盘实现及准备代码，也有合理的跨阶段底层复用。没有依据将所有“早于总计划编号”的代码判为无用或宣称未来阶段已经完成。

| 分类 | 实际代码与调用证据 | 处置／未完成边界 |
| --- | --- | --- |
| 新范围下多余的生产入口 | `TrayService.Create`创建“进入海报编辑”；`App.TogglePosterEditing`读取中心SelectedPosterId并切换对应session；`OnPosterEditingChanged/OnControlCenterSelectionChanged`回写托盘状态 | 05-02退役托盘专用入口与选择耦合；同时更新`TrayService.VerifyProjection`的旧编辑可用性断言。不是取消海报自身编辑能力 |
| 新范围下多余的准备代码 | `TrayManagement.Poster`、`TargetCurrent`只有`TrayManagementTests`调用，没有生产UI接线；原05-03目标子菜单未实现 | 05-03取消；05-02移除这两项及仅服务其目标菜单的4例夹具。不将测试过的辅助方法当作菜单已交付 |
| 应复用但尚未接托盘 | `TrayManagement.Create`、`ControlCenterCatalogRequest.Source`及`PosterSpatialRuntime.ApplyControlCenterCatalog`贯通来源到现有创建/安全落位命令；工厂当前只有测试调用 | 正常创建逻辑在控制中心已使用；托盘新增按钮尚不存在。保留完整目录/实体读集、合法占位及空间失败反馈，不重写创建模型 |
| 应复用但尚未接生产 | `PosterSessionRegistry.GloballyHidden/SetGloballyHidden/Wants`处理运行期隐藏原因；SetGloballyHidden只有测试调用 | 不能称全局隐藏已可用。05-04需补App调用、原手势/自动观察保护、有效可见摘要和恢复/退出；不改Poster.IsVisible持久偏好 |
| 已有核心，不再重复开发 | `App.TogglePosterLocked`的Tray分支已读全目录/实体并提交AllPosters批量命令；`TrayService.ApplyProjection`已有汇总。`App.ShowControlCenter`经Dispatcher延后开窗，双击命令与退出已有 | 复用并做新菜单回归；保留此前托盘双击崩溃修复。不建立第二套选择或全局状态库 |
| 有开关但实际覆盖不足 | `DynamicEffectsPaused`已存储/投影/有菜单与中心命令；直接视觉消费者是`PosterVisualWindow.Feedback.cs`的空间不足按钮反馈。R4与自动收展未消费该字段控制整段动画 | 不等于全局暂停动画，更不等于关闭自动收展。如实标注作用域，完整减少动效映射仍按V2-DEFER-005，不静默扩张本步 |
| 早于后续计划的必要基础 | `SqliteStateStore.CreateBackup/ValidateBackup`和迁移前备份已在；StateStoreContracts仅Initialize/ReadHead/CreateBackup/ValidateBackup | 属04-01安全前置，不是04-06便携配置或完整恢复入口已完成。复用备份，后续仍需格式、导入导出、确认/回滚、跨机映射 |
| 早于后续计划的必要基础 | ControlCenterService唯一窗口生命周期，App.SystemLifecycle共享暂停/恢复、退出释放等已在M03/M08接入 | 04-07/08应补余项并验新增资源，不重新造唯一中心/退出链；代码存在不补齐缺失人工/资源门 |
| 按用户要求保留的未来接口 | `PosterContentPreferences`分离宏观Scale/Spacing与`PosterItemVisualInput(RelativeScale,Order,Pinned)`；ResolveItems只接收已确定的顺序/尺度，当前只有测试调用 | 是热度排版接入基础，不含统计、衰减、排序决策。保留，不能宣称已实现热度模型 |
| 仅数据预留 | Settings中的PerformanceMode、LaunchAtStartupRequested有状态/schema/命令基础；本次未发现对应完整性能调度或自启注册闭环 | 不因为字段存在而验收未来能力，不随意删迁移或改库兼容 |
| 尚未成为真实核心内容 | PosterVisualWindow仍静态六张DEMO；检查Domain/Application/Infrastructure及生产源未发现完整Item仓储、PortableProfile、ThemeHost/ThemePackage或AI调用闭环 | 真实收纳、完整主题平台、热度引擎、AI仍按主线进入；不是被托盘范围变更取消 |

## 2. 下一步退役边界

在[05-02](../../30_Plans/V3_04_05_TRAY_PLAN.md#实施顺序)实施，不在本轮动代码：

1. 去掉托盘edit菜单、回调、状态同步及专用验证；解除对控制中心当前选择的依赖。回调若兼有自动观察等其他职责，只拆托盘分支，不能整段误删。
2. 删除TrayManagement.Poster/TargetCurrent；对应`TrayTargetFreezesIdentityAndSurvivesUnrelatedWrite`理论3例和`TrayStaleTargetCannotFallBackToNeighbor`1例列为退役。其余7例新增/全局隐藏基础仍有用；若测试还承担共享安全证明，先迁回对应合同后再删专用包装。
3. 保留中心/海报`IPosterSession.SetEditing`、PosterStateCommand、ControlCenterSelection、输入岛、空间/R4、事务/CAS及恢复能力；托盘批量锁定继续用这些公共通道。
4. 生产文件内的smoke入口受隔离配置与启动互斥保护，是现阶段回归设施；本次没有证明它们是死代码，不因“测试代码在App中”批量删除。

## 3. 证据锚点与检索范围

- [TrayService](../../../../src/DesktopUp.App/TrayService.cs)：Create、SetPosterEditing、VerifyProjection、ApplyProjection。
- [App](../../../../src/DesktopUp.App/App.xaml.cs)：托盘构造、ShowControlCenter、TogglePosterEditing、OnPosterEditingChanged、OnControlCenterSelectionChanged、TogglePosterLocked、ToggleDynamicEffectsPaused。
- [托盘工厂](../../../../src/DesktopUp.Application/TrayManagement.cs)、[对应夹具](../../../../tests/DesktopUp.Contracts.Tests/TrayManagementTests.cs)、[会话注册表](../../../../src/DesktopUp.Application/PosterSessionRegistry.cs)：rg核对src/tests调用方，区分接口与生产接线。
- [目录请求](../../../../src/DesktopUp.Application/ControlCenterCatalogRequest.cs)、[生产落位](../../../../src/DesktopUp.App/PosterSpatialRuntime.ControlCenter.cs)、[动效反馈](../../../../src/DesktopUp.App/PosterVisualWindow.Feedback.cs)。
- [状态仓储](../../../../src/DesktopUp.Infrastructure/SqliteStateStore.cs)、[备份端口](../../../../src/DesktopUp.Application/StateStoreContracts.cs)、[系统生命周期](../../../../src/DesktopUp.App/App.SystemLifecycle.cs)、[宏观／Item尺度](../../../../src/DesktopUp.Domain/PosterContentPreferences.cs)。

静态审查能证明当前文件和调用关系，不能证明功能在真实鼠标/系统恢复/GPU下已经通过。未来模块的“未发现完整闭环”仅针对本次源文件/符号和调用检查，不是形式化的不存在证明。原[04-04前代码盘点](V3_04_IMPLEMENTATION_PLAN_AUDIT_2026-09-15.md)保留为历史阶段快照，不作为当前05范围依据。

## 4. 未决与验证边界

全局隐藏期间新增是先恢复还是保持隐藏，仍归V2-TBD-020，用户尚未选择；不阻止先清理多余入口或接正常新增。暂停动效与自动收展分离，不能把本次“保留动效入口”理解为用户已激活延期优化。

本轮只改A/B/C文档并检查一致性；未修改源码/测试、未构建/运行DesktopUp、未更新候选或配置、未提交/推送。05-01原1294/1294与独立构建只作历史基础证据；新菜单及退役后的测试总数须下一实现轮重新取得。BUG-001/004与既有未验范围不自动关闭。

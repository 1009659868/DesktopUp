# M08-07 R2：外部点击与标题拖动仲裁

日期：2026-09-11；整理：2026-09-14。状态：**R2-A诊断/源码回归证据保留，实机待验；R2-B旧方案已撤销**。当前标题目标只看[TI合同](M08_TITLE_INTERACTION_CONTRACT.md)。历史880项及后续921项不是新TI验收；人工绑定仍R1。

## 1. 本轮用户反馈

- 当前R1/PID25524：自动收起途中普通点击仍继续收起、自动展开途中普通点击仍继续展开，用户均确认通过。
- 新失败A：离开已展开海报后，在桌面连续点击会无限推迟自动收起。
- 新失败B：收起海报启用自动后，进入标题长按准备拖动，自动展开先发生；真正拖动时回滚收起，但拖动未成功。

R1只解决InFlight阶段原始点击误取消，没有涵盖这两项。不能把两项PASS写成整个G2/G3通过。

## 2. 证据与根因边界

日志：`out/manual/v3-04-m08-07/basic/logs/desktopup.jsonl`，PID25524。

### A：保护作用域及计时重置

09:06:14～17 UTC连续出现CountingDown/Protected交替。`NativeDesktopPointerPlatform.TryReadProtection`读取全局按键/当前及前台capture；`PassivePosterPointerObserver.Sample`无条件并入共享保护，未区分该按下是否属于某张海报。

`AutoPostureModel.Observe`在尚未开始动画时，任何保护进入Inactive；Inactive调用ClearWork清空DeadlineMs。释放后使用now＋delay重新计时。因此即使鼠标一直在海报外，反复点击也会反复刷新截止时间。这一机制可由现有日志与代码直接确认。

只把清空改成暂停仍不充分：若全局原始按下继续阻塞发起，连续点击的采样可能始终命中按下态。需要以实际归属区分海报交互与无关桌面/前台点击，不能把收起延迟变成全桌面鼠标空闲时间。

### B：自动展开与手动拖动的接管断点

请求17：09:07:36.591 UTC自动展开；36.844记录手动意图变化后的失效（ManualIntentVersion=10，原始PointerButton/PointerCapture），36.849恢复成功、回执Cancelled；区间内没有spatial.gesture_started。请求20在09:07:40.956～41.322重复同类过程。未见相反姿态命令，仍是自动事务回滚。

相关代码链：

1. `PosterInputIslandManager.BeginResolvedGesture`在按下时保存`_pressFrame=hit.Frame`并取得capture；此时可能处于自动动画中。
2. 只有移动达到5px后才发送GesturePreview，随后`PosterVisualWindow.OnInput`调用空间Begin。
3. `PosterSpatialRuntime.Begin`先调用BeforeManualIntent，取消自动动画并恢复committed几何；随后强制要求快照Bounds等于旧pressFrame。
4. 若pressFrame来自中间动画高度/位置，恢复后两者不相等，Begin返回null。窗口随后CancelSpatialGesture释放输入capture；因为尚无gesture对象，这个失败分支目前没有记录两份矩形或取消原因。

**已证实**：自动展开被手动介入取消并恢复，但未成功建立拖动；**代码明确存在且与现象吻合的失败路径**：动画中按下几何与恢复后几何校验冲突。现有日志没有press/current成对矩形，尚不能宣称逐次实测确认了具体矩形差值或排除了其他Begin早退。实施前应补有界拒绝原因及成对几何取证，不持续记录全局鼠标轨迹。

## 3. DeskBox参考

本地参考 `G:\A_\Tools\reference\DeskBox`，沿用项目登记的参考版本；本轮阅读，没有复制方法体或改许可证。

- `WidgetWindowBase.Collapse.cs`：CompactPointerPressed取消待展开计时；TryScheduleCompactHoverExpansion使用配置延迟，到期重新读取资格和输入归属。
- `WidgetCompactInteractionPolicy.cs`：CanHoverExpand排除已有交互；普通热区和移动把手/动作区域分开。交互区域停留下限为620ms，不等于本项目应照搬620ms。
- CaptureCompactInteractionSnapshot使用本widget的边界交互、拖缩、弹层等状态；其CanAutoCollapse不是以全桌面鼠标停止活动为条件。

吸收意图待定与按下抑制、归属明确、到期重验。保留DesktopUp单调计时、共享调度、事务恢复、输入岛、空间约束及手动抑制；不复制其窗口层级或独立持久化。

## 4. 保留的R2-A修订证据

### R2-A：无关点击不能变成全桌面空闲计时

- 先新增连续点击/前台遮挡夹具，原观察器4/4失败：原deadline=1000被清成null；前台覆盖下仍误报共享按键保护。原始证据`out/verify/v3-04-m08-07-r2/tests/outside-click-before.trx`。
- `Windows/PassivePosterPointerObserver.cs`将原始PointerButton/PointerCapture按实际可接收的海报归属过滤。包括auto关闭的邻居；已有海报交互的所有者保留至释放，真正拖出范围仍保护共享空间。无关桌面/前台按下不重置、也不阻塞离开计时。
- 原生菜单、确认、编辑、真实Gesture、未知/恢复保护不剥离；路由capture仍保留。零需求、环境代次变化、所有者隐藏/移除清理旧归属。
- 新9项归属/连续输入测试全部通过；定向89项为补最后零需求用例前的结果，最终完整**880/880、0 skipped**包含新9项及组合模型28项。`r2-full.trx`为最终合同证据。
- 独立`out/verify/v3-04-m08-07-r2/app`构建0警告/0错误；未启动，也未替换`Get-M08Build.ps1`/25项R1绑定。没有实际鼠标认证或新增资源样本。
- 末次观察R1/PID25524已正常退出，资源0；09:52:30 UTC PS5.1原生前置读取input desktop/cursor error5，未启动R2隔离smoke。错误码只作为实机前置不可读证据，不据此判断用户锁屏/管理员或更改权限。

## 5. R2-B旧方案撤销与现行承接

删除原200ms停留提议、先恢复再拖动步骤、旧“继续增长”接线计划及重复历史实施建议；这些均不再是待确认选项。
V2-DEC-003改为按下暂停、释放快展、按下阶段固定的收起/展开态拖动，见[唯一活动合同](M08_TITLE_INTERACTION_CONTRACT.md)。§2的失败证据和取证边界仍保留：不能把符合代码的路径写成逐次矩形实测结论。

[旧组合实验报告](M08_COMBINED_VALIDATION_R3.md)只记录当时测试与反例，不指示生产接线。实际旧Begin取消→恢复→按下起点校验路径尚未被新TI替换；下一步按[M08计划](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#41-标题交互修订的最近顺序)补新模型与真实接管，不通过放宽校验掩盖断点。R2-A继续单列，不用标题修订的PASS代填外部连点体验。

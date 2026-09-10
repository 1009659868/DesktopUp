# M07：DeskBox 收展呈现链路对照与取证计划

2026-09-10后续：已得到用户原位重绘无变化反馈及日志，独立确认收起态未隐藏meta/footer行；具体动画白色像素来源仍未直接证实。用户继续修复授权后，按本文成套机制完成[R3](M07_ANIMATION_R3.md)，不再是“未实施”；505项/原生生命周期通过但实际画面仍待人工。下文保留研究阶段原始判断与边界。

日期：2026-09-10。状态：SOURCE REVIEW COMPLETE / PIXEL ROOT CAUSE OPEN。

用户要求先重新阅读 DeskBox、定位问题、制定方案，最后才实现。本轮只研究与记录：未修改生产代码、构建、启动或停止应用；未改变动画参数、数据库、空间策略或 Git 提交。R2 白条人工 FAIL、M07 OPEN 保留。

## 1. 证据边界

- DeskBox 本地参考：`G:\A_\Tools\reference\DeskBox`，提交 `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`，工作区干净。本轮未运行它来验证同机像素效果。
- V3 对照：`impl/poster-redesign-V3`，HEAD `29473b6d99acef84ad9914ecf020495009ce2f36` 加已有未提交 R2 改动。R2 运行指纹、日志及截图测量沿用 [R2 记录](M07_ANIMATION_R2.md)，本轮未重新采集进程证据。
- R1/R2 动画截图均出现约 10px 纯白带。用户明确确认 `7e055a5c` 是动画完全停止后的截图：C 底部仍有白色残留，A 底部有异常带。因此不能只解释成“动画中一帧露底，结束自动恢复”。
- 裁切截图未与屏幕坐标、客户区和 HRGN 同步标定，不能断言多保存了 10px 高度，也不能仅凭颜色确定是哪一层绘出了白色。

## 2. DeskBox 的完整实现链

下列源文件路径均相对参考仓库，行号针对上述提交。

### 2.1 内容预布局与就绪

`Views/WidgetWindowBase.Collapse.cs` 的 `TryRunCompactExpansionWarmupSlice`（约 994）先分片实现控件模板，再调用 `Controls/WidgetShell.xaml.cs:1645` 的 `WarmCompactExpansionLayout`。

预热不扩大原生窗口：收起层保持不透明，展开标题和真实内容在其后按目标展开尺寸 Measure/Arrange。`finally` 恢复响应式布局并返回收起视觉状态。正式动画开始时会再次冻结内容尺寸，而不是靠预热永久保留固定宽高。

重要限制：函数明确以 Measure/Arrange 无异常返回作为就绪判据，不是 GPU/屏幕像素呈现完成。`WidgetCompactWarmupPolicy` 还校验内存清理 epoch；请求带 generation 和取消处理。`Models/WidgetCompactExpansionReadinessPolicy.cs` 设置 96ms 截止，超时允许实时布局降级，避免预热永远阻塞展开。因此不能称其所有路径都有“完整新帧已经上屏”的保证。

### 2.2 动画期间内容与窗口分工

`Views/WidgetWindowBase.Collapse.cs:3114` 的 `StartBoundsTransition` 调用 Shell 的 `BeginResponsiveLayoutTransition`（`WidgetShell.xaml.cs:1513`）：

- 展开时，以目标展开尺寸冻结内容；收起时，以起点展开尺寸冻结内容。
- 冻结对象是 `ShellContentPresenter`，不是整个 HWND 或全部 XAML。锚点决定左/右、上/下对齐。
- 外层 `ContentTransitionViewport` 负责裁切；过渡中不逐帧通知内容视口尺寸变化，结束后再通知。
- `PrepareCompactTransition` 保留真实展开内容，并准备收起覆盖层；同一 eased progress 驱动边界、透明度、位移和圆角。普通 Box 收展不是另一个内容截图切换流程。

`CollapseAnimationRendering`（约 3214）调用 `MoveWindowWithoutPersisting(bounds, suppressRedraw:true)`，其窗口标志增加 `NOREDRAW | NOCOPYBITS`；随后更新 Shell 视觉进度。

这里的 Move 在协调器回调内只是入队：`Services/WidgetCompactAnimationCoordinator.cs:100` 按 HWND 保存最后一次请求；`OnRendering`（约 326）先执行所有有效回调，再在 `finally` 中通过 `FlushPendingBoundsMoves`（373）批量应用原生位置。因此实际顺序是先准备各窗口视觉状态，再提交整组边界，而非每调用一次 Move 就立即改变 HWND。

### 2.3 正常末帧与恢复

进度到 1 时，DeskBox 停止本次动画注册，再用不带 `suppressRedraw` 的 `MoveWindowWithoutPersisting(to)` 请求最终边界，并调用 `CompleteBoundsTransition`（3307）。

同一 HWND 的最终普通请求会覆盖本次回调里已入队的动画请求。结束视觉恢复也发生在协调器最终 Flush 之前；不能把函数调用顺序误读为原生窗口已经提前提交。

结束恢复包括：复位过渡透明度/位移/组合动画，确定收起或展开状态，释放固定内容宽高和锚点对齐，恢复 Stretch，通知最终内容视口尺寸，应用最终圆角/背景偏好并处理状态回调。V3 可以参考这种明确的过渡生命周期，但不能据此声称该流程具有 GPU 呈现屏障。

`ScheduleExpandedLayerRestoreAfterFrameCommit`（4117）还会在后续协调器回调或超时后恢复某些展开抬层状态，并检查 generation。这里的“frame commit”是回调计数策略，不是读取每个窗口的实际呈现回执；V3 不应引入 DeskBox 的展开抬层策略来修白条。

### 2.4 超时、取消和关闭

- 动画 watchdog 走普通最终边界提交和相同的 `CompleteBoundsTransition`。
- `StopCollapseAnimation`（3912）释放帧注册/计时器，并按调用参数取消 Shell 过渡。
- 立即应用路径、关闭清理还调用 `CancelResponsiveLayoutTransition`，释放内容固定尺寸；挂起预热、延迟回调和层级租约一并取消。
- `SimplifyBackdropForInteraction` / Restore 的背景降级有旧版 Acrylic/性能条件，不是所有 Windows 11 动画必走的抗白条方案。

## 3. 对照 V3：事实与风险

| 阶段 | DeskBox | V3 当前代码 | 结论 |
| --- | --- | --- | --- |
| 内容准备 | 隐藏展开内容预热；整段过渡冻结内容尺寸 | `PosterVisualWindow.CommitSpatialFrames:111` 每步安排当前目标尺寸，再在 finally 恢复原宽高 | 已确认缺少同等的整段内容布局生命周期；是同步风险，不是单独的像素根因证明 |
| 组内协调 | 各回调准备视觉，最后统一 Flush HWND 请求 | 所有参与者先 Measure/Arrange，随后原生批次，再更新各自 HRGN/输入岛 | V3 已有批次机制，不能说完全没有协调；它并未证明 XAML、裁切与屏幕像素同帧完成 |
| 中间/末帧策略 | 动画 NOREDRAW/NOCOPYBITS；末帧改普通提交 | `DesktopZOrderPolicy.SpatialBatchFlags:62` 中间与恢复均含 NOCOPYBITS、不含 NOREDRAW | 阶段策略确实不同；禁止单独增加 NOREDRAW 作为推测性补丁 |
| 结束恢复 | 解除内容冻结、恢复全部过渡视觉与最终窗口策略 | `FinishPosture:147` 重新读取权威投影，复用 `CommitSpatialFrames` | V3 有结束恢复；缺口是没有独立的呈现收尾与像素稳定验证，不是“完全没有恢复” |
| 裁切/圆角 | 所查窗口圆角路径使用 DWM 偏好与 XAML 外观 | V3 逐帧 HRGN，`SetWindowRgn(redraw:false)`；相同尺寸/圆角命中缓存 | 需要单独检查最终区域/脏区；不能凭源码把 false 或缓存认定为唯一原因 |
| 验证 | 参考实现，无本机同场景像素测试 | ActualSize、HWND 几何、背景连接读回通过；用户画面失败 | 自动门没有覆盖实际失败条件，不能把连接成功等价于白条修复 |

V3 另有分离输入岛、视觉 HWND 的 layered/transparent 配置、桌面层级守护和实体读集 CAS。DeskBox 的原生提交失败后逐窗口 fallback，不能直接移植成 V3 部分失败仍成功；必须保留整组检查和 committed revision 恢复。

平台语义核对：[UpdateLayout](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.updatelayout?view=windows-app-sdk-1.8)是布局更新；[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) 的 NOREDRAW 要求调用方负责需要的失效/重绘；[SetWindowRgn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn) 的 false 不请求设置区域后的重绘，但不代表其他路径也停止绘制。不要混淆这些语义。

## 4. 当前根因判断

1. **动画中白条：较强嫌疑是可见窗口/裁切边界与内容呈现不同步。** 白条厚度与样本每步增长吻合，几何读回正常；尚未拿到同帧边界与像素证据来确定具体白色来源。
2. **停止后异常带：终态问题已确认，具体成因仍开放。** 必须区分底栏/根布局未覆盖、HRGN/脏区未刷新和旧表面残留。前一项“短暂延迟”不能独立解释它。
3. **R2 外观变化：R2 确实改变背景来源。** 这未修复白条，也可能影响外观，但不能把所有异常带都归因于背景画刷。不要再以换色掩盖几何/呈现问题。
4. **已确认的工程缺口：** 临时布局、原生边界、裁切及结束恢复缺少一份跨整段过渡的明确呈现合同；目前验证不足以分辨上述失效。这是方案要补齐的结构性问题，不等于已证明唯一直接触发点。

## 5. 先取证，再冻结解决方案

### D1：同帧坐标归属

固定现有配置和同一 C/B/A 连续序列，记录 request/frame/generation、目标边界、实际 window/client/region 边界、XamlRoot 缩放、root/content viewport/footer 实际尺寸及位置、实际屏幕像素。覆盖展开、收起、中间帧和停止后若干渲染周期；截图必须保留可标定的屏幕坐标。

先回答：异常位于客户区内还是旧窗口区域？底栏 bottom 是否与客户区 bottom 一致？HRGN 是否与预期一致？若布局已经超出/不足，先修布局；若几何一致但像素残留，再验证重绘路径。不得只增加字符串断言或再次读取背景连接就宣布取证完成。

### D2：隔离结束残留

在已停止的同一几何下，以可恢复的重绘/遮挡后露出对照检查异常是否消失，不修改状态库。消失只能加强脏区/表面残留假设，不能独自证明具体 API。再针对命中的嫌疑分别测试最终区域刷新、布局恢复时序；不要一次同时替换多种窗口标志和背景。

### D3：配套机制对照

确认 D1/D2 结果后，才在隔离实现中比较“现路径”与“内容预布局 + 整段内容尺寸冻结 + 共享视觉进度/边界提交 + 明确结束恢复”。NOREDRAW 如需试验，必须连同正常结束/取消时的绘制责任验证，不能孤立加到生产。

本轮未执行 D1/D2/D3。若工具无法获得实际画面，就明确保留集中人工像素门，不能以原生尺寸测试替代。无须在定位前重跑全部 Explorer/DPI 矩阵。

## 6. 候选解决方案及不变项（尚未实施/冻结）

- 在现有 renderer/窗口适配层建立每次过渡的独立上下文，包含 generation、参与者、展开内容尺寸、锚点、临时视觉状态和最终恢复责任；开始预布局，动画期间保持内容布局稳定，正常/取消/关闭都幂等释放。
- 将内容布局尺寸与当前可见视口尺寸分开。V3 底栏仍必须贴当前可见底部，不能直接冻结整个根窗口造成底栏跑出视口；保留部分行连续裁切和滚动。自适应展开采用求解后的有效高度，不能偷偷改用户原展开高度。
- 所有参与者先准备视觉，再沿现有批次通道提交窗口；HRGN、输入岛必须对应当前可见区域。最终帧和取消恢复按取证结果明确处理布局、裁切缓存及重绘，不保留动画临时属性。
- 不改已获认可的空间策略、连锁推动/完整归位规则、CAS、数据 schema、像素步长与帧间隔配置；不引入 DeskBox 的展开抬层、时间跳帧或逐窗口降级成功语义。
- 验收同时看动画中无白条、停止后无残留/多余区域、嵌套收展同步贴合，并检查菜单输入与正常退出无回归；自动几何门保留，但不足以关闭视觉门。

下一实施条件：完成 D1/D2 的异常归属和区分性证据，据此确定最小修改面，再实施和验证。不能因为 DeskBox 有同名组件或相似注释就跳过根因确认。

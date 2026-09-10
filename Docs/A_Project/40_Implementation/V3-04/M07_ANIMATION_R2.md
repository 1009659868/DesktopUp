# M07 动画 R2：展开新增区域白条修订

> 历史记录：本页命令、PID、测试数和“当前/待验”均属于当时轮次，不再是活动入口。R1/R2/R3未解决白条，诊断轮不是修复。最终用户确认、清理及稳定入口见 [R4](M07_ANIMATION_R4.md)；已移除脚本/源码本地保留于 out/archive/presentation-cleanup-20260910，不再推荐执行本页旧命令。

日期：2026-09-10。状态：IMPLEMENTED / BUILD & NATIVE PASS / WHITE-STRIP VISUAL FAIL。M07整体仍OPEN。

## R2人工失败与后续诊断（优先于下文）

2026-09-10 已按用户要求重新阅读 DeskBox 的预布局、共享帧提交、正常结束/取消恢复，完整对照及取证顺序见 [DeskBox 呈现研究](M07_DESKBOX_PRESENTATION_RESEARCH.md)。本次只研究/记录，未再改生产实现；明确区分源码缺口与尚未取得同帧像素证据的直接根因。

最新补图`7e055a5c`由用户明确确认为动画完全停止后：C底部仍有白色残留，A底部有异常带，不能再仅解释为动画期间一帧露底后自动恢复正常。无需再次索要同类停止截图。当前PID仍44360。只凭裁切截图不能将条带精确映射到HWND客户区/旧窗口残留，不能断言状态库多保存了10px高度。

收尾代码新发现：`FinishPosture`先解除动画Rendering订阅，随后经`PreparePostureRestore`复用`CommitSpatialFrames`；原生视觉区域一直使用`SetWindowRgn(..., redraw:false)`，相同尺寸的`ApplyVisualRegion`还会命中缓存直接返回。当前收尾没有单独的最终区域重绘/呈现稳定验证。解除动画订阅不等于停止WinUI自身渲染；以上是需隔离验证的收尾缺口，不足以单凭源码认定它是唯一根因。下一修订应同时覆盖中间帧同步和结束后残留清理，保留原空间策略，不再只换背景色。[SetWindowRgn官方语义](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn)明确false不请求区域设置后的重绘。

用户复测：白条仍存在，动画结束后似乎融入海报背景；同时截图背景外观相较R1有变化，不能把该效果变化当作修复。当前实际PID44360、`out/manual/v3-04-m07-animation-r2/20260910-110413-ac3e50/`，App SHA256仍为下文A59E…，排除误开旧包。新截图`ae175fd7`尺寸301×347，中央x150、y278～287仍为10px纯白，和R1同厚；当前动画参数未改变。日志没有相应布局/批次错误。

R2只替换了背景来源，并没有消除HWND扩展/HRGN更新与WinUI像素呈现之间的时间窗口。`backdropsReady`只是实际画刷已连接及颜色配置正确，不证明新区域已合成，因此240/400帧检查通过与人工白条FAIL并不矛盾。停止用该检查作为白条修复的充分条件。

进一步参考DeskBox完整收展实现，而非只参考Backdrop：

- `WidgetShell.xaml.cs`约1669～1720：在收起层后准备展开尺寸的内容；注释明确区分宿主裁切后的ActualSize与预布局，不以ActualSize等于目标尺寸证明像素就绪。
- `WidgetWindowBase.Collapse.cs`约3180～3257、3868～3906：PrepareCompactTransition配合逐帧视觉进度；动画窗口提交使用NOREDRAW/NOCOPYBITS，结束时恢复普通提交。
- `WidgetCompactAnimationCoordinator.cs`：共享Rendering回调收集窗口组提交。这是一整套内容准备/动画绘制/结束恢复，不是只添加NOREDRAW标志即可复用。

V3仍有独立输入岛、WS_EX_LAYERED/透明输入和逐帧HRGN约束，与DeskBox不能视为完全相同宿主。尚未通过隔离对照证明哪个具体Win32/WinUI表面贡献白像素，也未证明R2窗口背景是视觉变化的唯一来源；不得直接宣布新根因已完全确认。

当前仅重新诊断/登记失败，没有改生产代码、重建包、停止PID44360或提交Git。已询问用户停止后是恢复正常、底部多出一截，还是仍有色差，以区分呈现短暂露底与终态布局异常。下一实现前应做内容预布局/稳定画面与窗口提交的配套验证，不能继续堆背景补丁或以源码字符串断言替代画面证据。

## 现象、证据与范围

用户认可R1动画顺畅度，但两张展开过程截图出现全宽白条。读取原图中央像素：`30555ef5`在y266～275、`1d1f2ba1`在y178～187均为10px纯白；R1实际参数12px/16ms/240ms，60→360的30帧路径每步增长10px。运行PID40740、R1 App指纹E57D…、无布局/批次失败。证据高度指向新客户区可见尺寸领先XAML画面，而非主题边框或空间求解留缝；未取得逐帧合成追踪，不声称最终区分了外层HWND与WinUI内部表面的白色来源。

R1的Measure/Arrange/UpdateLayout与ActualHeight检查只能证明布局，不保证新像素已呈现。保留该准备步骤、整体HWND提交后更新HRGN的顺序；不回退到提前裁切，不加Sleep/DwmFlush假屏障，不改步长、时钟、连续关系、CAS/schema、层级或输入区域。

## 修订实现与参考

DeskBox的`WidgetWindowBase.Backdrop.cs`中`ApplySolidColorBackdrop`使用`WinUIEx.TransparentTintBackdrop`作为窗口级背景，主题变化更新TintColor。V3已经依赖WinUIEx 2.9.3，本次直接使用现有包，不新增依赖、复制DeskBox代码或自建渲染器。

- `PosterVisualWindow.Backdrop.cs`：每个海报一份独立背景，在显示前连接Window.SystemBackdrop；颜色取本海报已解析主题，保留原245/255透明度。背景生命周期覆盖整个窗口，不随每帧布局重建。
- XAML根背景改为透明，避免底色仍绑定旧内容尺寸，也避免与窗口背景叠加两次透明度。背景由窗口合成层提供，内容更新未跟上时仍有对应底色；不是宣称跨HWND原子像素呈现已经实现。
- `ApplyPresentation`同步更新本实例TintColor，再更新内容；保留同HWND/同几何。窗口退出前断开SystemBackdrop，交由组件释放背景画刷和消息监听。
- `DesktopEnvironmentWatcher`只在没有活动窗口背景时执行原深色擦底；活动时让消息继续进入组件处理，避免两个WM_ERASEBKGND实现竞争。窗口层级、激活和环境信号保护保留；共享环境锚点没有接入海报背景。
- 原生动画探针新增`backdropsReady`：检查背景实例身份、主题色、透明XAML根，以及实际ICompositionSupportsSystemBackdrop上非空画刷。只在隔离计时探针逐帧读取，不增加正常空闲轮询。这是连接/配置检查，不是屏幕像素检测。

参考：[WinUIEx背景说明](https://dotmorten.github.io/WinUIEx/concepts/CustomBackdrops.html)、[组件源代码](https://github.com/dotMorten/WinUIEx/blob/main/src/WinUIEx/TransparentTintBackdrop.cs)、[UpdateLayout的布局语义](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.updatelayout?view=windows-app-sdk-1.8)。开源参考不替代本项目运行证据。

## 自动验证

完整490/490、0失败0跳过；新增两项结构合同检查窗口级背景/主题接线、擦底所有权和断开顺序。TRX：`out/verify/v3-04-m07-animation-r2/results/animation-r2-full.trx`。这些源码约束检查不替代实际运行或视觉测试。

App/SessionFixture Release均0警告0错误。独立包`out/verify/v3-04-m07-animation-r2/`，24项指纹（含WinUIEx.dll）见`scripts/m07-animation-r2-build.json`；App SHA256：`A59E71B7352C502E49A850FC396BD42576219D2FF8F4C55CF699CE235A347CCD`。

| 样本 | 结果 |
| --- | --- |
| PS5.1默认，PID46520，`20260910-104955-a5f93b` | 两种关闭顺序8命令；240帧提交与背景连接全通过；无警告/错误，exit0/clean/台账资源0 |
| PS5.1自定义，PID6312，`20260910-105236-b9147b` | 带空格配置路径；6px/8ms/320ms/linear；8命令400帧提交与背景连接通过，正常退出清理 |
| 三海报参数更新，PID13644，`presentation-e6fb82` | 同HWND、同Bounds、独立实例、实际XAML和窗口背景读回通过；非法草稿拒绝；exit0/台账资源0 |
| 动画中正常退出，PID42988，`exit-during-c37f54` | 首次动画18帧后取消，exit0/clean/台账资源0；原8步探针报surface-removed是预期中断，不当作8步PASS |

运行目录均在`out/manual/v3-04-m07-animation-r2/`。未覆盖R1、连续性/菜单旧包或日常配置，未操作桌面文件，未commit/push。

## 人工复测与入口

先从托盘正常退出已有DesktopUp，再在当前Windows PowerShell5.1运行：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-animation-r2.ps1'
```

默认仍读取`config/poster-animation.json`，支持`-AnimationConfig`，参数合同不变；每次新建隔离C/B/A数据。`-CheckOnly`校验24项指纹/参数；`-Smoke`运行原生8命令并正常退出。

只需集中复测本次反馈：C/B分别展开时底部白条是否消失；嵌套收展仍顺畅、没有新增空隙；海报色彩/圆角和菜单/拖缩是否无回归。没有用户视觉反馈或实际逐帧像素证据前，不记录白条已通过，不关闭M07其他门。屏幕捕获未执行；自动读回不是像素验收。

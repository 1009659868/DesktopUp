# DeskBox 空间计算吸收记录

日期：2026-09-07
状态：源码/测试阅读与适配边界已完成；空间算法尚未接入生产，按 M04-02..05 推进。
来源：`G:\A_\Tools\reference\DeskBox`，commit `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`，GPL-3.0-only。本次不修改该仓库、不复制代码/素材、不运行 DeskBox，不以其测试结果作为 DesktopUp PASS。

## 1. 吸附：复用纯计算和迟滞，不混入捕获/提交

阅读 `src/DeskBox/Services/WidgetSnapCalculator.cs` 的 ResolveMove、ResolveResizeEdge、ResolveStickyMoveMatch、EvaluateEdgeCandidates、IsBetter，以及 `tests/DeskBox.Tests/WidgetSnapCalculatorTests.cs` 的边间距、工作区零间距和sticky测试。

- 一个手势内统一物理坐标；用户阈值在进入手势时转换一次。横/纵轴分别求解，移动保持尺寸，resize只调整活动边。
- 进入与脱离阈值分开，脱离阈值不小于进入阈值；已吸附时先评估sticky，再查新候选。参考测试以8/12和间距5举例，不能自动当作DesktopUp产品默认值。
- 候选优先比较距离；相同距离再比较工作区/间距/同边对齐优先级，之后比较垂直方向间隔和中心距离。最后完全相同仍依赖候选遍历顺序，DesktopUp需稳定ID排序消除偶然性。
- 参考sticky计算只比较原点和旧吸附位置，不能替我们验证目标仍有效。DesktopUp按PosterId/目标几何版本/拓扑代次使过期sticky失效，不能把旧HWND作为持久身份。
- 吸附是候选修正，后面仍须硬碰撞/工作区验证；不允许吸附将目标直接塞进另一个海报。硬碰撞优先，无法吸附时保持合法自由位置，不丢capture。

预定落点：M04-02纯空间合同，M04-03在既有手势候选阶段接入；不另建全局鼠标hook或手工输入系统。保留当前最新采样、最后帧flush和结束一次提交。

## 2. 组移动：复用整组预备与完成边界，不当作连锁推动

阅读 `WidgetCoordinatedMoveCalculator.cs` 全文、`WidgetManager.CoordinatedMove.cs` 的begin/update/complete/batch路径；与 `WidgetCoordinatedMoveCalculatorTests.cs` 的union、正负clamp、已越界和超大组算例对照。

- 以组起始包围盒限制共同delta，所有成员从各自StartBounds计算，避免逐帧累积误差。
- Begin冻结参与者；Update先准备全部目标，再DeferWindowPos，保留NOZORDER/NOACTIVATE/NOOWNERZORDER；Complete最后批量保存。参考实现批量失败还有逐窗fallback。
- 这条路径收集同屏可移动窗口并共同平移，不是“碰到B才推动B”；锁定过滤和同屏过滤可参考，但DesktopUp推让成员由碰撞求解产生。
- 不照搬超大组直接允许请求delta的宽松策略；不照搬“一个窗口失败其余继续并视作整体成功”。DesktopUp有完整读集CAS、预备失败拒绝、物理部分失败账本和恢复；数据库事务不等于多个HWND/DWM原子。

预定落点：M04-05候选变更集与物理批量提交；沿用M03会话注册、epoch/lifetime、共享恢复与退出，沿用M02事务/回执。没有新增“整屏所有海报一起拖”的产品手势。

## 3. 展开：复用尺寸意图/可行性/锚点分离，替换轨迹

阅读 `WidgetCompactExpansionCalculator.cs` 的Resolve/CreateCandidate/CreateBoundsFromPivot/InterpolateAnchoredBounds、`WidgetCompactExpansionDirectionPolicy.cs`，对照扩展计算测试的“优先可容纳方向”“约束大小”“严格拒绝”和固定pivot插值用例。

- 保留请求尺寸和结果尺寸的区分，以及CanExpand/IsSizeConstrained等显式结果；不把“得到一个较小矩形”当成功恢复原尺寸。
- DeskBox四角候选和固定pivot插值适合其模式；非严格Auto会缩小，高度轨迹不是本产品的先下后上。DesktopUp按[空间设计§5](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md#5-无障碍时的保尺寸两阶段展开)明确两段并保持原H。
- 工作区不足时参考其严格拒绝思想，返回原因、保留原状态；不复制RaiseForExpandedState的抬层分支，不通过覆盖邻居完成展开。
- 参考没有提供本次条件归位所需的完整因果证明。DesktopUp需额外处理二次移动、锁定、再次推动、占位变化、版本冲突和跨重启证明。

预定落点：M04-04保尺寸轨迹/标题命中同步；M04-05加入锁定障碍、连锁传播与条件归位。

## 4. 文件指纹与复用门

| 源文件（Services/） | SHA-256 |
| --- | --- |
| WidgetSnapCalculator.cs | DAE3A7BE28AB336B5C8CE05F4FFCB228073AD9EB5FCC700FA4C0C170A559E067 |
| WidgetCoordinatedMoveCalculator.cs | FCD4147C30B521756C751FAD9C12A2BE2A3E59548FCD539C0BD3BB9E4C806263 |
| WidgetCompactExpansionCalculator.cs | 1DD18474F63006317B08149461701422E5CEDD673CE9A4A480416ADEED572000 |

实际移植时新增来源头/NOTICE和修改说明，若源commit变化重新比较，不能依赖活动main。先移植最小纯策略与对应行为测试，再连接既有适配器；不复制依赖庞大的WidgetManager/SettingsService/WindowBase整类。M04-01为自主中心选择实现，不使用DeskBox源码；上述“已吸收”指已明确参考取舍与落点，不表示吸附/推动已在桌面生效。

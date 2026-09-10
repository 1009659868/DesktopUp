# M07 动画呈现与启动配置 R1

> 历史记录：本页命令、PID、测试数和“当前/待验”均属于当时轮次，不再是活动入口。R1/R2/R3未解决白条，诊断轮不是修复。最终用户确认、清理及稳定入口见 [R4](M07_ANIMATION_R4.md)；已移除脚本/源码本地保留于 out/archive/presentation-cleanup-20260910，不再推荐执行本页旧命令。

日期：2026-09-09。状态：IMPLEMENTED / CONTRACT & NATIVE PASS / VISUAL MANUAL PENDING；M07整体仍OPEN。

2026-09-10后续：用户认可顺畅度，但展开10px白条FAIL；已接[窗口背景R2修订](M07_ANIMATION_R2.md)，当前复测入口改为`scripts/start-m07-animation-r2.ps1`。本文R1产物和证据保持历史，不用于验R2。

## 用户反馈与边界

用户反馈当前收展、推让、复原操作已经较好，但截图在动画中出现约30像素空隙。本轮授权优化顺畅度，并将像素跳变和动画帧节奏暴露为启动配置；暂不在控制中心编辑或热更新。

上一轮只读诊断用现有Domain程序集核算10步链式序列：各计划帧C/B与B/A间隙均为0，每步边缘变化30像素。代码发现`PrepareSpatialFrame`提前对真实HWND应用HRGN，随后才执行整个窗口组的批次；WinUI内容也没有同帧布局准备。该证据支持呈现不同步判断，但没有证明裁切和合成延迟各占多少。不能以80次HWND提交成功声称屏幕像素已经同步。

本轮不改`SpatialContinuitySolver`、展开/归位资格、下优先策略、锁定固定、整返或不动、同revision/CAS；没有新schema，也不改窗口层级策略。

## 实现

1. `PosterAnimationOptions`＋`PosterAnimationConfiguration`：启动一次读取不可变参数；严格版本、范围、重复/未知字段、文件大小校验。正常应用支持用户文件/包内文件与显式CLI；错误配置记录critical并正常结束。没有文件监听，也没有第二份布局权威状态。
2. `SpatialAnimationSchedule`：只细分已接受的安全关键帧，最终端点不变。以共同绝对边缘插值，包括负坐标，保持接触边缘一致；检查每段连续扫掠、最大步长及工作预算。任一参与者的边缘变化都受上限约束，不只限制收展源。
3. `SpatialAnimationCursor`：按单调时钟/目标时间读取下一帧；每个渲染回调最多提交一帧，滞后不跳过轨迹、不爆发式追赶。默认12px/16ms/240ms/ease-in-out；目标时间受安全步长/最小间隔限制，当前300px场景名义约492ms，而非承诺240ms完成。
4. `PosterSpatialRuntime.Posture`：动画由`CompositionTarget.Rendering`驱动，原timer改为100ms的环境/停滞看门狗；结束、取消和销毁移除Rendering订阅。只在活动动画期间订阅，不在空闲桌面永久逐帧唤醒。
5. `PosterVisualWindow`：准备帧不再修改可见裁切或内容；提交时先以目标尺寸完成参与者XAML布局，再批次修改HWND位置/尺寸，之后更新各HRGN及输入布局。临时显式根尺寸在finally恢复为原布局模式，避免拖缩继承错误固定尺寸。新增XAML实际宽高和原生读回失败检查。
6. 独立新包与入口`scripts/start-m07-animation.ps1`，固定23项二进制清单；配置不被哈希锁死。SessionFixture新增只读`--validate-animation-config`，与生产共用解析器；PS5.1预检不再另写一套参数规则。

这协调了准备/布局/几何/裁切的顺序，并缩小每次可见跳变，不宣称独立HWND和WinUI合成已构成跨窗口原子呈现事务。屏幕留缝是否消除仍需用户观看。

## 自动验证

新增27项，完整 **488/488，0失败0跳过**；`out/verify/v3-04-m07-animation/results/animation-full.trx`。覆盖1/4/8/12/32/64像素上限、两种嵌套关闭顺序逐帧贴边、负坐标上扩/两段、16种子混合宽度、延迟/重复渲染回调、无效路径/预算、配置默认/覆写/冻结/非法输入，以及准备无副作用与Rendering订阅成对的架构检查。

App与独立fixture Release均0警告0错误。构建输出`out/verify/v3-04-m07-animation/app/`、`fixture/`；指纹见[清单](../../../../scripts/m07-animation-build.json)。App DLL SHA256：`E57D1A5896E0177CB898A16D31F7A09E7CDDBC30E971447EBEEAE5920E207A3B`。

| 原生样本 | 参数与结果 |
| --- | --- |
| 默认，PID32568，`20260909-174355-0bfa4a` | PS5.1；12px/16ms/240ms，8命令、240计划/240实际成功帧，最大边缘变化10px、0归位尾帧、0警告/错误；两种关闭顺序恢复原位；exit0/clean/资源0 |
| 自定义，PID40764，`20260909-174617-d08b4a` | PS5.1＋带空格配置路径；6px/8ms/320ms/linear，8命令、400计划/400实际帧，最大变化6px、0尾帧、0警告/错误；exit0/clean/资源0 |
| 动画中正常退出，PID47036，`exit-during-5200b8` | 首次动画提交19帧后正常限时退出；记录surface-disposed取消，exit0/clean/资源0。探针明确报surface-removed/未完成8步，这是预期中断证据，不算完整Smoke PASS |

目录均在`out/manual/v3-04-m07-animation/`。默认样本准备/提交平均约6.66ms仅供该样本参考，不当性能基线或屏幕FPS。0警告不等于没有视觉留缝。

原连续性包、原菜单R1包均保留，PS5.1各自CheckOnly继续通过；没有覆盖旧输出、修改日常配置、操作桌面文件或commit/push。

## 用户配置与人工验收

参数文件：[config/poster-animation.json](../../../../config/poster-animation.json)。完整范围、优先级、调整权衡和命令见[配置说明](../../../../config/README.md)。

启动：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-animation.ps1'
```

人工重点：两种C/B嵌套关闭顺序中相邻边缘是否仍短暂分离；B收展推动A是否顺畅；正常拖缩/滚动、收起后的标题栏和菜单是否仍正常；不新增白条/灰框/残留。配置变化需要退出重启；人工反馈前不关闭本视觉门或其他M07剩余项目。

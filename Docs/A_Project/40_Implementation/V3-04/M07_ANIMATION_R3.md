# M07 动画 R3：整段稳定布局与分阶段呈现恢复

> 历史记录：本页命令、PID、测试数和“当前/待验”均属于当时轮次，不再是活动入口。R1/R2/R3未解决白条，诊断轮不是修复。最终用户确认、清理及稳定入口见 [R4](M07_ANIMATION_R4.md)；已移除脚本/源码本地保留于 out/archive/presentation-cleanup-20260910，不再推荐执行本页旧命令。

2026-09-10新反馈：**R3白条人工FAIL**，PID40500已正常退出。两图白条均10px，真实日志显示原生220/root210，而R3原检查只查内部structure。后续已修订[R4](M07_ANIMATION_R4.md)预分配视觉容量及覆盖断言；以下R3自动结果不能抵消用户失败，旧包保留。

日期：2026-09-10。状态：IMPLEMENTED / CONTRACT & NATIVE PASS / VISUAL PENDING。M07 不关闭。

## 1. 已取得的证据与结论边界

用户对诊断版 PID41252 提供四张新截图，确认动画白条仍在，并明确“诊断：原位重绘对照”没有改变画面；随后已正常退出。实际日志在 `out/manual/v3-04-m07-presentation-diagnostic/20260910-120558-57f5c2/logs/desktopup.jsonl`。

- C 在12:12:10、12:12:15，A在12:13:56的三次原位重绘均成功请求，前后几何和revision不变。不能继续把重复RedrawWindow当作已证实的解决方案。
- 对应静止样本，展开根/内容结构与客户区高度均360，底栏和客户区底边一致。普通静止布局读回不能解释或否定用户动画截图。
- **独立确认的收起态布局缺陷**：原实现只隐藏列表，仍保留46标题、42页脚和模板meta行。60px收起窗口中，basic-box结构为88px，页脚从46开始，有14px仍处于窗口内；night-ops还保留130px meta，结构218px。该缺陷解释收起后的分隔线/尾部区域，不能冒充动画纯白条的唯一原因。
- **动画白条的具体像素来源仍未直接证实**：现有证据指向窗口扩大与内容呈现之间的协调风险。已确认V3缺少DeskBox整段内容冻结及中间/末帧区分机制；R3是在用户“继续修复”授权下对该机制缺口的成套修订，不声称已完成逐像素归因。

## 2. 本轮实现

1. `PosterVisualWindow.Transition.cs`建立每次request独立的临时布局。取过渡两端较大的有效高度（含自适应结果），在原生窗口扩大前测量展开内容；不修改持久首选高度。
2. 将内容结构暂放入Canvas，使固定内容的DesiredSize不强迫当前根窗口变大。整段固定内容宽高，只改变可见裁切；没有每帧根Width/Height固定→NaN的反复切换。列表不随每个中间高度重新布局/重算列数。
3. 底栏独立跟随当前可见底边，内容裁切止于底栏顶部；紧凑60高度不露出半截页脚。纯`PosterTransitionViewport`覆盖连续增长、收缩、缩放和非法尺寸。
4. 明确`Regular / Animation / FinalRestore`提交阶段。已登记视觉HWND的中间帧使用NOREDRAW + NOCOPYBITS；输入岛保持原标志。所有视觉准备仍先于同一次原生批次；不改变NOACTIVATE/NOZORDER/NOOWNERZORDER。
5. 结束/取消先解除临时树、固定尺寸和Clip，按committed projection恢复，执行最终普通绘制标志的整组提交、强制重设最终HRGN和输入区域，再请求最终失效重绘。此请求承担NOREDRAW的恢复责任，**不作为GPU呈现屏障或白条消失证明**。
6. 启动预布局异常、取消、窗口删除/关闭、主题树替换均释放临时布局；参与者恢复独立try/catch，失败仍走现有整组恢复，不将逐窗口部分成功当作事务成功。
7. 正常收起状态同时将meta、页脚隐藏并将对应行归零；60px空间合同、标题输入坐标及原展开高度不变。

参考机制见[DeskBox代码研究](M07_DESKBOX_PRESENTATION_RESEARCH.md)。未复制其展开抬层、逐窗口降级成功或旧版Acrylic专用策略；没有引入新组件依赖，没有更改空间求解、关系归位、SQLite/CAS和动画配置节奏。保留R2窗口背景，不再用另一种底色掩盖问题。

## 3. 验证

- 全部合同 **505/505**，0失败/0跳过：`out/verify/v3-04-m07-animation-r3/results/r3.trx`。
- App及SessionFixture Release：0警告/0错误。
- Windows PowerShell **5.1.26100.8875**入口预检通过；支持带空格配置路径。
- 24项产物指纹：`scripts/m07-animation-r3-build.json`。App DLL SHA256：`18F5B587089D013243B7B2B9F35CBE6EEDE3460DCFBCF8F92B8CE4079C9B4C0D`。

| 隔离样本 | 实际结果 |
| --- | --- |
| PID37836，`native-20260910-142825` | 默认12px/16ms，8次命令、240批次帧，applied/layoutsStable全部true；8次最终恢复成功；停止后紧凑root/structure均60且footerCollapsed、Clip和固定布局已释放；exit0/clean/资源0 |
| PID24724，`20260910-143045-f4c268` | PS5.1，6px/8ms/320ms/linear，8次命令、400帧；20次内容准备/释放配对、8次恢复成功；无warning/error；exit0/clean/资源0 |
| PID15832，`cancel-20260910-143238` | 首次动画第18帧正常退出；取消后整组恢复和布局释放成功；exit0/clean/资源0。8命令探针报surface-removed是预期中断，不记成完整序列PASS |
| PID10140，`presentation-20260910-143335` | 三海报真实主题/内容参数更新、XAML/同窗口读回通过；exit0/clean/资源0 |

以上数据目录均位于`out/manual/v3-04-m07-animation-r3/`。computer-use两次初始化失败，最终错误`CreateProcessWithLogonW failed: 1385`；没有取得修订后的屏幕帧。自动验证证明布局生命周期及原生提交/恢复工作，**不能证明白条消失或动画视觉无回归**。未以脚本自建UI自动化绕过工具限制。

## 4. 人工复测与启动

已启动人工修订包PID40500（与指纹一致），数据目录`out/manual/v3-04-m07-animation-r3/20260910-143712-131d21/`；启用PresentationDiagnostic供本次复测取证。三张紧凑海报排列就绪，画面结论仍等待用户。

先从托盘正常退出已有DesktopUp，然后执行：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-animation-r3.ps1'
```

入口每次建立独立C/B/A配置，旧R1/R2/诊断包和日常配置均保留。可选`-AnimationConfig '绝对路径'`、`-CheckOnly`、`-Smoke`；需要逐阶段日志时加`-PresentationDiagnostic`，否则默认不启用高频布局观测。

集中复测：C/B/A展开中底部无白条；停止后不出现额外底带/半截页脚；两种嵌套关闭顺序仍贴合顺畅；菜单、编辑拖缩、滚动和锁定无回归。视觉门等待用户反馈，M07其他未完成门保留。不自动推进真实Item阶段。

本轮无commit/push，没有修改用户文件或日常数据。

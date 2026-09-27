# LR-R5 核心界面与直接操作记录

2026-09-27最新（DEC-029）：用户确认滚动大致可用、停顿续滚BUG-026延期，批准恢复推进、最小解耦及其后的Rain。R5核心有限路径已收口，证据与遗留项见[收口审计](LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)。下方“四页签/未对齐/R5不能收口”等为早期事实，后续实现已取代；运行中的循环直接拖动仍归R6，不与普通浏览修复混算。

当前：**CORE CLOSED / 明确携带遗留项**，不是R7可靠性验收。阶段出口以[主计划§4](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)及收口报告为准；下述历史代码、协议、控件、系统输入与人工体验分开记录，无真实文件执行能力。

2026-09-27最新反馈：用户对早期基础操作组回复“人工体验通过”，随后明确UI/操作意图与已采纳C差距较远，并提醒先完整对齐文档中已确认的E1测试。**R5不能收口；R6新增实现暂缓**。六任务逐项源码差异、C结构偏差和纠偏边界见[对齐记录](LR_R5_UI_C_INTENT_ALIGNMENT.md)，实施顺序归主计划§4.2.1。历史测试结果不撤销，也不扩大为UI/意图通过。

## 1. 当前实现入口

- `src/DesktopUp.Domain/ContentDragPolicy.cs / Map`：从按下时作者文档、完整快照、视口和实际 tile 推导总位移；自动移动生成最终读序图钉，手动移动生成区域内坐标，跨度来自有效呈现；返回原点不写，非法落点不夹紧。当前拒绝运行中的循环拖动，其接缝及有界可见集归 R6。
- `src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs / HandleContentInput / FlushContentMotion`：复用原生按下、防抖和捕获；内容操作无需进入外框调整模式；按下快照不追随预览变化，松手重新检查最后落点；无效/取消不提交旧候选。作者草稿与热度派生呈现分离。
- `src/DesktopUp.Application/ContentActivationSession.cs`、`src/DesktopUp.App/PosterVisualWindow.ContentActivation.cs`：单击选择，同身份/画面见证且符合系统时空阈值的双击仅产生一次 DEMO 打开意图；Enter 只从中心真实焦点区进入并抑制按住重复；不创建全局按键监听。
- `src/DesktopUp.App/ControlCenterWindow.ContentFields.cs`：列/行跨度、读序、手动局部位置按组保留原始文本；同步 TextChanging 获得唯一所有权，显式应用/Enter 才提交一次。非法或过期输入保留，冲突入口拒绝，切目标/关闭只取消本中心的草稿。
- `src/DesktopUp.App/ControlCenterWindow.xaml`：中心样板已接两模式、方向/循环独立、选中卡片细调、独立图钉/尺寸恢复和撤销重做；其四页签/大表单尚未对齐C，不称为完整UI C。键盘区需要真实焦点；桌面 NOACTIVATE 宿主保持。

## 2. 已执行证据

执行目录均为项目根，使用 `out/toolchains/dotnet-10.0.112`；App 验证使用独立新 DataRoot 和 app-local Windows App SDK。没有更改日常库。

| 证据 | 已观察事实 | 不证明什么 |
| --- | --- | --- |
| `out/lr-r5-evidence/activation-drag-first/` | 相关合同 105/105 | 不证明 OS 路由或画面 |
| `out/lr-r5-evidence/contracts-first/` | 全量 1921/1921，0 跳过 | 不证明后续 App 接线 |
| `probes-second/`、`fixture-first/`、`app-local-candidate1.log` | 解决方案、fixture、app-local 均 0 警告/0 错误；此前失败日志保留 | 构建不等于运行 |
| `content-protocol-01/observation.json` | 12 组真实窗口处理器协议通过；PID22700 exit0，退出资源0；新进程 PID4124 读同库，配置和版本指纹相同，Canvas/Input/Clip 一致 | 未发送系统鼠标/键盘；非人工操作 |
| `content-fields-01/observation.json` | PID27080 exit0，304 次断言、六次有效正式写入 revision2→8；非法/过期/取消/锁定目标隔离、中心关闭重开通过，资源0 | 使用真实控件属性及共享处理器，不是 OS 输入；此字段门未重启进程 |
| `content-fields-01/data/content-fields-*.png` | 四张实际 XAML 宽/窄×跨度/位置截图已查看，字段及应用/取消可见 | 不含桌面 SystemBackdrop；不是最终视觉验收 |

以上原生证据绑定 `candidate1-app-local/`，391 源码记录、401 产物记录；App.dll SHA256=`78C2044F14ED65E6D4CF51944591C6632E3B193171F8226A020A7100BB2CA5EF`，执行前后产物一致。后续修复未自动继承此包的 PASS。

## 3. 复审与补充验证

- 窄窗口键盘提示被截断：改为按钮内容横向拉伸，候选3新图已复查；去除面向用户的布局内部版本号。
- 卡片→跨度把手点击→同卡片点击可遗留双击配对：把手按下立即断对，新增真实处理器协议反例通过。
- 中心持草稿期间热度/主题被正确延迟，但释放后缺唤醒；派生几何更新也未通知中心有效字段刷新。补单海报安全点唤醒和现有 editor 通知，真实控件与提案端口联合反例通过：保护时不应用、取消后只取最新、字段同步、空闲不重复重排、恢复不写作者。
- 作者4×4硬位置移动合法，但叠加热度6×6后碰撞 Explicit 留白的反例：旧 catch 未清 projected，可能发布非法草稿/留下空只读画面。现清空候选，提交前置拒绝也恢复当前画面；真实 handler 验证预览及 release 零写入、原有效画面/命中保留，随后同库重启通过。
- `content-fields-02`失败保留：提案正常恢复作者/布局并保持浏览锚，但测试错误要求滚动 offset 也回到初始位置。候选3把 canonical layout 指纹与浏览 clip 分开，布局/作者必须相同，当前 Canvas/Input/Clip 仍必须有效；没有删除保护、唤醒或版本断言。
- Native 驱动审查发现鼠标捕获不保证键盘焦点。Escape 仅在目标进程确实拥有前台和焦点时发送，首次输入前和发键前都检查；否则零输入 `NotExecuted`。鼠标门与键盘门分开，不为测验抢前台。
- Native 断言须核对实际目标卡片和精确跨度/位置/图钉，不能只靠 revision+1；脚本/驱动/启动器与 App/fixture 一起绑定证据，已加载旧 driver 的 PowerShell 会话拒绝复用。

## 4. 当前固定候选及人工入口

当前候选 `out/lr-r5-evidence/candidate3-app-local/`，394 源码/401 产物记录，App.dll SHA256=`925DF8591A0C085366671E2983FF4A09E2EDBAA23D9189640B55C4B42BAF40F2`。`app-local-candidate3.log` 构建0警告/错误；与候选2相比只改 probe 观测断言。下列均绑定当前候选，产物稳定、exit0/error0/退出资源0：

| 证据目录（均在 `out/lr-r5-evidence/`） | 当前结果 |
| --- | --- |
| `content-protocol-03` | 13组真实处理器协议通过，PID10432；同库重启PID33524，配置/版本指纹相同 |
| `content-fields-03` | PID35448，336次断言，六次正式写入revision2→8；含提案唤醒/恢复；四张最新PNG均实际复查 |
| `content-rejected-heat-02` | PID36924，合法作者/非法有效投影的预览及release不写、不丢画面；同库重启PID2540 |
| `lamp-regression-01` | PID8792，六参数/仲裁/取消等既有代表模板回归通过；不扩大为最终视觉或指针跟随验收 |

系统鼠标首次尝试使用候选2，`content-native-mouse-01`在第一项返回 `Occluded`；起点(198,334)未命中指定输入窗口，MouseMoves/LeftDowns/LeftUps/EscapeDowns均0。PID21912正常退出、资源0。该记录证明安全停止，不证明鼠标功能通过；没有为重试操作其他应用或抢前台。

早期体验启动器为 `scripts/start-lr-r5-review.ps1`，逐项校验候选全部文件及三组同包协议/字段/反例证据，可用 `-CheckOnly`只读验证。旧 `start-content-layout-manual.ps1`及旧R4 manifest保留，不作为此阶段入口。

已启动当前候选的 `human-review-01/data` 独立配置，启动PID2812；启动时进程响应、日志确认中心投影/海报就绪。用户随后回复基础操作“人工体验通过”，**UI及操作意图另明确要求纠偏**，不据前条关闭R5。既有review配置只能在同一候选绑定下重新打开，不能清空或自动迁移。重开命令（项目根，先正常退出该App）：

```powershell
$env:DOTNET_ROOT=(Resolve-Path out/toolchains/dotnet-10.0.112).Path
./scripts/start-lr-r5-review.ps1 -CandidateDirectory out/lr-r5-evidence/candidate3-app-local -ProtocolEvidence out/lr-r5-evidence/content-protocol-03 -FieldsEvidence out/lr-r5-evidence/content-fields-03 -RejectedHeatEvidence out/lr-r5-evidence/content-rejected-heat-02 -DataRoot out/lr-r5-evidence/human-review-01/data
```

早期人工核对：A首次自动拖动/松手图钉；拖动Esc取消后继续可操作；单击只选择/双击DEMO反馈；手动模式移动及跨度；中心成组应用/取消和失败提示。真实焦点键盘及UI C视觉以实际反馈登记，不能根据启动日志或用户未回复判通过。

## 5. 出口待办与后续边界

早期基础操作组已有用户正向反馈；未取得的自动系统输入/真实焦点矩阵仍按原证据单列，不补写逐格PASS。当前R5前置是已确认E1意图和UI C对齐：桌面最终结果/取消、同项管理、整图影响预告、模式反馈及中心结构。Lamp光池透明截图的亮黄环仍需区分捕获与实际桌面合成。详见[对齐记录](LR_R5_UI_C_INTENT_ALIGNMENT.md)，未进入R6出口。

R6 继续横向/循环接缝、被动受限恢复、完整可见集/成员资源变动；R7 执行同一最终候选的产品与配置全链；R8 交付真实 Item 接入包。现阶段不能宣称真实文件、快捷方式、应用入口已经接入。

## 6. 2026-09-27 等待体验期间的只读核对

- PID2812仍指向候选3，Responding=true；独立体验日志104条，无error/critical/fatal，尚无content/state.command交互事件。这是进程观测，不是用户确认或功能PASS；没有重启/关闭体验实例。
- 重新查看候选3 Lamp 的三张海报PNG，并只读提取像素，记录 `out/lr-r5-evidence/lamp-pixel-readback-01/observation.json`。蓝灯截图左侧无卡片区域（x2～23、y160～284）alpha最大8/255；例如(10,200)为RGBA(255,255,191,4)，按alpha数值合成黑底仅(4,4,3)。透明原图预览中的亮环不能直接当作桌面亮度失败；蓝色参数下仍出现暖色RGBA，原因尚未证实。未修改或重新合成原截图，也未据此修改生产色值。
- 官方[RenderTargetBitmap说明](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.rendertargetbitmap?view=windows-app-sdk-1.8)说明它捕获XAML树、有内容/时序限制；它不提供本项目桌面合成正确性的证明。官方[GradientOrigin属性页](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.radialgradientbrush.gradientorigin?view=windows-app-sdk-1.8)和[类备注](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.radialgradientbrush?view=windows-app-sdk-1.8)对相对原点文字存在差异，不能凭此把当前(.5,.5)改成(0,0)。外部资料用于提出验证方向，不替代当前2.3.6运行时证据。
- 后续最小视觉辨别：在独立测试中对同一光池记录变色前/后的XAML gradient stops、底层composition stops、原始BGRA和明确背景下的捕获，并与实际桌面观测对照。先证明是颜色更新、透明捕获还是实际合成问题，再修改；当前人工包及源码保持不变。
- 已把五份核心架构文档的优先导航从“R3进行中”同步为当前R5人工待反馈；历史设计/需求和阶段门未改。此前R6只读接线地图保留，尚未实施。

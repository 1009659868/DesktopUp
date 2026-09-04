# V3-03 验证记录

状态：`CLOSED — R5 AUTOMATED PASS + USER MANUAL PASS 2026-09-04`

日期：2026-09-04

分支：`impl/poster-redesign-V3`

## 本轮实现

- 以 NIGHT OPS 单海报替换 V3-02 青色诊断卡，保留无系统标题栏、无非客户区和既有桌面 owner/resting-layer 合同；
- `PosterWindowCluster` 由全幅视觉 HWND 与一个原生稀疏 InputIsland HWND 组成，二者使用同一 DefView owner、desktop generation 和物理 bounds；
- 视觉 HWND 始终透明命中；USING 的 InputIsland 只包含顶部标题、锁定/收起按钮和六个 Item，海报背景、卡片间隙及卡片下方不建立输入区域；
- EDITING 通过托盘或更多菜单显式开启，开放整面移动以及四边、四角八向缩放；结束编辑后恢复稀疏输入区域；
- 原生输入岛使用 pointer capture、屏幕坐标 delta 和 5 px 阈值；重排或窗口边界更新不结束本次按下；
- 拖缩 preview 按帧应用，取消回滚至按下时外框，松开只记录一次正式 geometry commit；
- 最小宽度 240 DIP、最小展开高度 240 DIP、收起高度 60 DIP；八向缩放保持对边/对角锚定并受主屏工作区约束；
- 锁定禁止移动与缩放，但锁定、收起按钮仍保留；Item 在展开时仍可触发测试意图；
- 收起/展开保持 top、left、width 和先前展开高度，按钮在屏幕上的位置不因海报高度变化而跳动；
- 每次 visual reflow 后生成单调 layout revision，并一次提交对应输入区域；区域必须完全位于同版本 frame 内；
- 视觉与输入岛共同参加 Explorer generation 重挂、显示拓扑重投影、desktop-bottom reassert 和有序清理。

## R4 标题操作与更多菜单修订

人工反馈指出右上角锁定/收起的视觉位置与实际点击位置不一致。根因是视觉标题栏使用 DeskBox 尺度的 24-DIP 操作按钮、`14,7,12,5` padding 与 2-DIP 间距，而 InputIsland 仍按旧的 34-DIP 按钮、13-DIP 顶边距、10-DIP 间距和 20-DIP 右边距独立计算；两套几何随 DPI 分别换算后必然错位。

R4 新增 `PosterTitleBarLayoutPolicy` 作为唯一几何权威：视觉标题栏与 InputIsland 同时读取 46-DIP 标题栏、24-DIP 操作尺寸、2-DIP 间距和 padding，旧 `LockButton` target 仅保留协议兼容，不再生成直接命中区。右上角交互调整为“收起/展开 + 更多”：

- 收起/展开继续是独立受保护按钮；
- 更多菜单提供锁定/解除锁定、进入/完成编辑、打开控制中心；
- “新建文件”和“新建文件夹”先显示为禁用的路线入口；真实写入必须等待 V3-05 的目标路径、事务、恢复账本和补偿合同，V3-03 不跨阶段写用户文件；
- 菜单关闭后重新断言 poster cluster 的 desktop-bottom 层级，避免 WinUI flyout 生命周期把海报带到普通窗口之上；
- 锁定状态通过菜单文字与海报底部 `LOCKED` 状态显示，不再保留一个不可见但可点击的旧锁定按钮。

## 首轮人工反馈与 R1 修订

首轮人工门确认 Item、标题拖动、锁定、收起、编辑缩放、层级和退出有效，但发现：普通态存在应用绘制的外围亮框；海报空白左拖、右键和原生快捷方式被拦截；缩小后 Item 无法上下滚动。因此首轮候选不通过，未关闭 V3-03。

R1 不再依赖顶层 `HTTRANSPARENT` 猜测穿透。实机 `WindowFromPoint` 证明实际拦截者为 WinUI 自动创建的 `Microsoft.UI.Content.DesktopChildSiteBridge` 子窗口；视觉根 HWND 在 WinUI 完成合成初始化后会清掉启动阶段设置的 `WS_EX_LAYERED`。当前修订在 post-composition 阶段重新施加并持续验证 `WS_EX_LAYERED | WS_EX_TRANSPARENT`，同时保持稀疏 InputIsland 位于 Item、标题和受保护按钮之上。

- USING 取消应用外框；EDITING 只绘制 1 DIP 状态边线；DWM border 继续显式透明；
- Item 区改用 WinUI `ScrollViewer`，采用连续像素裁剪和标准滚动条；原生输入岛只转发可见 Item 与窄滚动轨的滚轮，不将海报空白变成输入面；
- Item hit region 纳入 scroll offset，部分可见卡片仍命中正确 Item；缩放后 offset 自动夹紧；
- 拖动/缩放吸收 DeskBox 的“只保留最新采样、每约 16 ms 提交一帧、松开前刷新最终帧”策略；同列数内不再反复销毁重建 Item 网格。

## R1 人工反馈与 R2 修订

用户确认 R1 的空白框选、桌面右键、原生快捷方式、六 Item、拖动/缩放、锁定、收起和层级均通过，1-DIP 编辑状态细边可接受；唯一剩余视觉问题是四个外圆角露出白色楔形像素。

根因不是 XAML `Border.CornerRadius` 数值，而是内部 Border 只裁剪其自身内容，顶层 WinUI HWND 仍保持完整矩形；视觉窗为跨进程空白穿透使用 uniform-alpha layered composition 后，矩形 HWND 四角不会自动获得 per-pixel alpha，于是透明 XAML 根下暴露窗口默认底色。R2 不再用背景色掩盖：

- `DesktopLayerAttachment` 新增物理圆角窗口区域端口，调用 `CreateRoundRectRgn + SetWindowRgn`，成功后按 Win32 所有权规则把 HRGN 交给系统，失败时自行释放；
- 初次显示前、每次尺寸/DPI/显示重投影以及 post-composition 修复时提交与当前物理尺寸对应的 8-DIP 圆角；纯位置拖动复用既有 region，避免逐帧重绘区域；
- InputIsland 的稀疏命中区域与同 revision 外圆角取交集，四个透明角不仅不可见，也不会留下不可见输入拦截；
- 临时 protected visual 改用 DeskBox 常规 Box 的 8-DIP 外圆角、0.8-DIP 默认细边、46-DIP 标题栏、24-DIP 操作按钮、4-DIP 卡片内圆角和 `#21242A` 深色实色回退。NIGHT OPS 内容语义保留；主题 schema、热更新与自定义材质仍属于 V3-07。

R2 人工截图确认外周仍有连续白色线条。该线条是 R2 主动采用的 DeskBox 默认 0.8-DIP/`0x18` 白色 XAML border，不是 DWM、HRGN 或系统非客户区残留；在 DesktopUp 的桌面海报语境下视觉过强。用户同时判定当前整体样式与 DeskBox 相差过大，因此本记录将 R2 外观标记为“近似未接受”，不再宣称完成 DeskBox 风格替换。

R3 将 USING 的 `BorderBrush` 设为透明且 `BorderThickness=0`，只在 EDITING 保留既已接受的 1-DIP 状态线。没有改成窗外阴影：当前 PosterWindowCluster 的视觉窗与 InputIsland 必须保持精确物理边界，额外阴影窗或扩大区域会重新打开层级、白角和空白命中风险；阴影如有需要应在后续视觉设计中作为 surface 内阴影或受合同约束的独立视觉层评估。

## R5 八向缩放与白底修订

人工复测又暴露两个独立缺口：EDITING 只有右边、底边和右下角可缩放；扩大视觉 HWND 时，右侧或底部会短暂露出白色默认底。前者不是命中偏移，而是 V3-03 原合同只建立了三向 resize target；后者发生在原生窗口边界已经扩大、WinUI 合成树尚未完成新尺寸帧的时间窗，透明 XAML 根层让 HWND 的默认擦除底色直接可见。

R5 收敛为以下边界：

- `PosterGestureKind`、`PosterInputTargetKind`、权限策略、方向光标和 InputIsland 同时扩展为左、右、上、下及四角八向；四角优先于边、受保护按钮和 Item 优先于编辑面；
- `PosterFrameResizePolicy` 成为纯几何权威：左/上缩放会同步改变 `X/Y` 并固定原右/下边，右/下缩放保持原左/上边；所有方向共享最小尺寸、45% 工作区最大宽度和工作区边界；
- `ApplyFrame` 先生成目标列数、滚动状态、输入 layout 与目标 HRGN，再通过 `ApplyPreparedBounds` 一次提交物理边界；原生 `SetWindowPos` 使用 `SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_DEFERERASE`，不触发层级和激活变化；
- 外部透明由物理 HRGN 负责，XAML 根层改为与 surface 相同的 `#21242A`/245 alpha 已绘制底；视觉 HWND 在 `WM_ERASEBKGND` 期间以同 RGB 深色填充尚未被合成帧覆盖的客户区，因此默认白色不再有可见入口；
- 本修订不把原生深色兜底称为最终主题。V3-07 仍可替换 surface/主题；届时必须让主题提供对应的 resize fallback token，不能恢复系统默认白底。

## 自动证据

### Release 合同测试

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\DesktopUp.sln" -c Release --no-restore
```

结果：`PASS — 73/73`。

新增验证八向缩放权限、四边/四角对边锚定、左/上方向最小尺寸与工作区夹紧、attached-only prepared bounds、无白色擦除提交守卫；既有收起、滚动、桌面宿主、Explorer generation、DPI、工作区、激活与 Z-order 合同全部继续通过。

### Release 构建

结果：`PASS — 0 warning / 0 error`。

### 真实桌面 smoke

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\smoke-desktop-locator.ps1"
```

结果：`PASS — R5 PID 14672`。同一进程日志包含 `poster.attached`、`poster.input_island_created`、`poster.shown`，无 error；退出包含 `poster.input_island_disposed`、`poster.disposed`、`application.stopped(activeResources=0)`，resource ledger 为 clean。

### 原生命中探针

显式重建 Release 后，进程 PID 3216 的视觉 HWND extended style 为 `0x080800A0`，同时包含 `WS_EX_LAYERED` 与 `WS_EX_TRANSPARENT`；日志存在成功的 `poster.post_composition_reasserted`。海报下方空白点命中 `SysListView32`，Item 点命中与日志一致的 `DesktopUp.V3.PosterInputIsland`。这证明 1/2/3 的底层跨进程路由已纠正，但真实框选、右键菜单和快捷方式双击仍保留为人工门。

最终 R2 可见候选 PID 9004 的视觉 HWND `0x1207DC` 与 InputIsland `0x360D12` 均返回 `COMPLEXREGION`；两者 `PtInRegion(0,0)=false`，视觉中心点为 `true`，同一桌面角点的 `WindowFromPoint` 命中 Explorer `SysListView32`。该探针证明矩形顶层窗和输入岛的左上角均被物理裁除，但四角实际像素仍由人工观察裁决。

## 人工 P0 门

启动当前 Release 后验证：

1. NIGHT OPS 单海报可见，USING 无应用外框、系统标题栏、顶部白条、灰框、裁切或未绘制矩形；EDITING 仅出现细状态边线；
2. 从卡片间、卡片下方等海报空白左拖，出现 Windows 桌面框选；
3. 在同一空白右键，出现 Windows 桌面菜单；
4. 将原生桌面快捷方式放到海报空白覆盖范围，能够单击选中、双击启动；
5. 六张 Item 均可点击；顶部标题栏可连续拖动海报；
6. 右上角只显示收起/展开与更多，两个 24-DIP 可见位置分别准确命中对应动作，不存在偏移、重叠或不可见热区；
7. 点击更多，菜单可见；锁定后标题拖动和编辑缩放无效，再次从菜单解除锁定后恢复；进入/完成编辑和打开控制中心均只触发一次；
8. 菜单中的“新建文件/新建文件夹”可见但保持禁用，并明确留待 V3-05；关闭菜单后普通窗口仍高于海报；
9. 点击收起后 top、left、width 不变，展开按钮停留原屏幕位置；再次展开恢复原高度；
10. 托盘或更多菜单进入海报编辑，从四边和四角逐一连续缩放；左/上方向保持相反边不动，拖过列数变化阈值不丢失当前按下；缩短高度后，将鼠标放在任意可见 Item 或右侧滚动轨上滚轮上下滚动，六个 Item 均可到达；再完成海报编辑；
11. 普通程序、文件选择器和全屏程序始终高于海报，桌面右键/刷新、Win+D 与 Explorer 重建无 V3-02 回退；
12. 连续拖动/缩放无规律闪烁、白条、灰框或抬层；托盘退出后视觉窗、输入岛、托盘和进程完全消失。

自动测试不能证明 Explorer 跨进程穿透、原生图标可操作、菜单实际定位和视觉无闪烁。以上 12 项必须由人工实机观察；任一失败均停留 V3-03。

## 最终人工裁决

2026-09-04，用户确认 R5 修订验证通过：左、上、右、下及四角缩放可用，对边锚定正确，缩放过程中不再出现白色底色；既有 Item、滚动、锁定、收起、透明空白、桌面图标、层级和退出行为没有回退。V3-03 据此关闭，后续 geometry commit 由 V3-04 的 SQLite/revision/CAS 权威接管。

## 当前未宣称完成

- 几何 commit 目前已有单次提交事件和结构日志，但 SQLite 权威持久化属于 V3-04；
- 六个 Item 目前只产生测试意图，真实文件/程序启动属于 V3-05；
- V3-03 只交付六项候选所需的标准 `ScrollViewer` 与连续 offset；大集合虚拟化、活跃度和完整动态排版属于 V3-06；
- 多海报和多屏不在当前范围。

# V3-03 验证记录

状态：`IN PROGRESS — R1 RELEASE BUILT / MANUAL RECHECK PENDING`

日期：2026-09-03

分支：`impl/poster-redesign-V3`

## 本轮实现

- 以 NIGHT OPS 单海报替换 V3-02 青色诊断卡，保留无系统标题栏、无非客户区和既有桌面 owner/resting-layer 合同；
- `PosterWindowCluster` 由全幅视觉 HWND 与一个原生稀疏 InputIsland HWND 组成，二者使用同一 DefView owner、desktop generation 和物理 bounds；
- 视觉 HWND 始终透明命中；USING 的 InputIsland 只包含顶部标题、锁定/收起按钮和六个 Item，海报背景、卡片间隙及卡片下方不建立输入区域；
- EDITING 通过托盘显式开启，开放整面移动以及右边、底边、右下角缩放；结束编辑后恢复稀疏输入区域；
- 原生输入岛使用 pointer capture、屏幕坐标 delta 和 5 px 阈值；重排或窗口边界更新不结束本次按下；
- 拖缩 preview 按帧应用，取消回滚至按下时外框，松开只记录一次正式 geometry commit；
- 最小宽度 240 DIP、最小展开高度 240 DIP、收起高度 60 DIP；缩放固定左上角，受主屏工作区约束；
- 锁定禁止移动与缩放，但锁定、收起按钮仍保留；Item 在展开时仍可触发测试意图；
- 收起/展开保持 top、left、width 和先前展开高度，按钮在屏幕上的位置不因海报高度变化而跳动；
- 每次 visual reflow 后生成单调 layout revision，并一次提交对应输入区域；区域必须完全位于同版本 frame 内；
- 视觉与输入岛共同参加 Explorer generation 重挂、显示拓扑重投影、desktop-bottom reassert 和有序清理。

## 首轮人工反馈与 R1 修订

首轮人工门确认 Item、标题拖动、锁定、收起、编辑缩放、层级和退出有效，但发现：普通态存在应用绘制的外围亮框；海报空白左拖、右键和原生快捷方式被拦截；缩小后 Item 无法上下滚动。因此首轮候选不通过，未关闭 V3-03。

R1 不再依赖顶层 `HTTRANSPARENT` 猜测穿透。实机 `WindowFromPoint` 证明实际拦截者为 WinUI 自动创建的 `Microsoft.UI.Content.DesktopChildSiteBridge` 子窗口；视觉根 HWND 在 WinUI 完成合成初始化后会清掉启动阶段设置的 `WS_EX_LAYERED`。当前修订在 post-composition 阶段重新施加并持续验证 `WS_EX_LAYERED | WS_EX_TRANSPARENT`，同时保持稀疏 InputIsland 位于 Item、标题和受保护按钮之上。

- USING 取消应用外框；EDITING 只绘制 1 DIP 状态边线；DWM border 继续显式透明；
- Item 区改用 WinUI `ScrollViewer`，采用连续像素裁剪和标准滚动条；原生输入岛只转发可见 Item 与窄滚动轨的滚轮，不将海报空白变成输入面；
- Item hit region 纳入 scroll offset，部分可见卡片仍命中正确 Item；缩放后 offset 自动夹紧；
- 拖动/缩放吸收 DeskBox 的“只保留最新采样、每约 16 ms 提交一帧、松开前刷新最终帧”策略；同列数内不再反复销毁重建 Item 网格。

## 自动证据

### Release 合同测试

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\DesktopUp.sln" -c Release --no-restore
```

结果：`PASS — 52/52`。

新增验证收起几何不变量、锁定权限、EDITING 缩放权限、尺寸下限、WinUI transparent/layered 样式丢失检测与恢复，以及连续滚动 offset/边界；既有桌面宿主、Explorer generation、DPI、工作区、激活与 Z-order 合同全部继续通过。

### Release 构建

结果：`PASS — 0 warning / 0 error`。

### 真实桌面 smoke

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\smoke-desktop-locator.ps1"
```

结果：`PASS — PID 21980`。同一进程日志包含 `poster.attached`、`poster.input_island_created`、`poster.shown`，无 error；退出包含 `poster.input_island_disposed`、`poster.disposed`、`application.stopped(activeResources=0)`，resource ledger 为 clean。

### 原生命中探针

显式重建 Release 后，进程 PID 3216 的视觉 HWND extended style 为 `0x080800A0`，同时包含 `WS_EX_LAYERED` 与 `WS_EX_TRANSPARENT`；日志存在成功的 `poster.post_composition_reasserted`。海报下方空白点命中 `SysListView32`，Item 点命中与日志一致的 `DesktopUp.V3.PosterInputIsland`。这证明 1/2/3 的底层跨进程路由已纠正，但真实框选、右键菜单和快捷方式双击仍保留为人工门。

## 人工 P0 门

启动当前 Release 后验证：

1. NIGHT OPS 单海报可见，USING 无应用外框、系统标题栏、顶部白条、灰框、裁切或未绘制矩形；EDITING 仅出现细状态边线；
2. 从卡片间、卡片下方等海报空白左拖，出现 Windows 桌面框选；
3. 在同一空白右键，出现 Windows 桌面菜单；
4. 将原生桌面快捷方式放到海报空白覆盖范围，能够单击选中、双击启动；
5. 六张 Item 均可点击；顶部标题栏可连续拖动海报；
6. 点击锁定后标题拖动和编辑缩放无效，锁定与收起按钮仍可操作；
7. 点击收起后 top、left、width 不变，展开按钮停留原屏幕位置；再次展开恢复原高度；
8. 托盘右键选择“进入海报编辑”，从右边、底边、右下角分别连续缩放；拖过列数变化阈值不丢失当前按下；缩短高度后，将鼠标放在任意可见 Item 或右侧滚动轨上滚轮上下滚动，六个 Item 均可到达；再选择“完成海报编辑”；
9. 普通程序、文件选择器和全屏程序始终高于海报，桌面右键/刷新、Win+D 与 Explorer 重建无 V3-02 回退；
10. 连续拖动/缩放无规律闪烁、白条、灰框或抬层；托盘退出后视觉窗、输入岛、托盘和进程完全消失。

自动测试不能证明 Explorer 跨进程穿透、原生图标可操作和视觉无闪烁。以上 10 项必须由人工实机观察；任一失败均停留 V3-03。

## 当前未宣称完成

- 几何 commit 目前已有单次提交事件和结构日志，但 SQLite 权威持久化属于 V3-04；
- 六个 Item 目前只产生测试意图，真实文件/程序启动属于 V3-05；
- V3-03 只交付六项候选所需的标准 `ScrollViewer` 与连续 offset；大集合虚拟化、活跃度和完整动态排版属于 V3-06；
- 多海报和多屏不在当前范围。

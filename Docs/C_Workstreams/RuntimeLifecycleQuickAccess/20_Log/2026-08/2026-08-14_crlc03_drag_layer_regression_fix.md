# C-RLC-03 正式验证反馈与桌面拖动层维护

日期：2026-08-14

状态：`C-RLC-03 IN VALIDATION / DRAG FIX READY FOR RELEASE RETEST`

## 用户反馈与证据边界

- 用户报告上一轮验证项均通过，并提供安装测试路径 `G:\A_\Tools\Test\DesktopUp`；
- 当前运行实例为 `G:\A_\Tools\Test\DesktopUp\DesktopUp.exe`，PID 49012；
- 冻结正式安装目标仍是 `%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe`；本机该目标当前不存在，HKCU Run 的 `DesktopUp` 值为 absent；
- 因此 G 盘实例可证明 A05 ineligible/零写入和海报交互行为，但不能单独证明 A02/A03/A04/A07/A08 的 eligible 正式路径写入、读回与禁用矩阵。若用户曾在正式目标完成该矩阵，需要另行确认该验证发生在正式路径。

## 新发现的回归

1. 在编辑状态拖动海报或松手后，顶部非客户区亮条重新出现；
2. 在控制中心编辑/拖动海报框后，再拖动真实海报，ToolWindow 被系统移动循环提升到普通应用之上，覆盖控制中心或其他程序。

截图与当前 Win32 只读探针确认：海报仍是无 `WS_CAPTION` 的 `WS_POPUP + WS_EX_TOOLWINDOW + WS_EX_NOACTIVATE`，窗口区域也仍存在。问题不是静态配置漂移，而是 `WebviewWindow::start_dragging()` 进入 Windows 系统窗口移动循环后产生的瞬时 Z-order 提升和 DWM 非客户区重绘；仅在 `WM_EXITSIZEMOVE` 异步下沉不足以封住拖动中和异常结束路径。

## 修复

- Windows 拖动入口不再调用 Tauri 的系统窗口移动循环；
- 复用既有 `WH_MOUSE_LL` 钩子建立单一 `PosterDragState`，根据全局鼠标坐标移动海报；
- 每次移动通过 `SetWindowPos` 携带冻结桌面相对锚点、`SWP_NOACTIVATE` 和异步位置提交，海报始终位于普通应用之后；
- 移动坐标按当前显示器工作区钳制，并兼容负坐标和大于工作区的窗口；
- 左键松开后向海报窗口线程投递 `WM_POSTER_DRAG_FINISHED`，重新确认无标题栏样式、ToolWindow/NOACTIVATE 扩展样式、DWM 非客户区抑制、桌面层级和当前窗口区域；
- 快速点击在命令到达前已经松手时不保留悬挂拖动事务，窗口销毁也会清理活动事务；
- 非 Windows 平台继续保留 Tauri 默认拖动兼容实现。

## 自动与构建证据

- 拖动/Z-order Node 契约：16/16；
- 桌面宿主定向 Rust：8/8；
- 全量 Node：140/140；
- 全量 Rust：123/123；
- Markdown 链接：152 份通过；
- production UI、Release 与 NSIS 构建通过，运行时依赖门 `gaps=[]`；
- `DesktopUp.exe` SHA-256：`1BA9E6F89690B3C457F73EF7BA6E259CCFEA520CB247A6E191058B5F2051C6AE`；
- `DesktopUpSetup.exe` SHA-256：`4FE1C613A426C63F5432ADE1004533ABFF35D9E61A2263A34D5574F4E26FE709`；
- 没有覆盖或强制结束 G 盘测试安装实例，没有写注册表或执行安装/卸载。

## 待人工复测

先从托盘退出 G 盘旧实例，再运行最新 `src-tauri/target/release/DesktopUp.exe`：

1. 编辑状态连续拖动并松开真实海报，整个过程和结束后均不得出现顶部白条；
2. 打开控制中心，编辑并拖动一次预览海报框，再拖动真实海报经过控制中心和其他程序覆盖区域；海报在拖动中和松手后都必须保持在普通应用之后；
3. 再确认空白穿透、卡片、锁定、收起/展开和位置持久化无回退。

上述回归通过、且 eligible 正式路径自启证据边界澄清后，才能将 C-RLC-03 标记 COMPLETE 并推进 C-RLC-04。

# TS-H01-D3 分辨率、DPI、工作区与边界矩阵 V1

状态：`D3-3 PASS / D3 USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS`

日期：2026-08-21

## 1. 目标

TS-H01-D3 只回答当前公共桌面宿主在单主显示器上的几何和最终呈现边界是否稳定。它不选择 Qt、WinUI、WPF、WebView2、Direct2D 或其他 UI/渲染框架，也不把多显示器、混合 DPI、负坐标、主屏切换或热插拔写成已通过。

本门覆盖：

- HG-RND-01 至 HG-RND-05；
- HG-GEO-01、HG-GEO-04；
- V2-RISK-001 在公共宿主夹具上的边界反例；
- D2 已验证层级/输入/恢复合同在几何操作后的无回退检查。

## 2. 前置门

1. TS-H01-B、C、D1 已 PASS；
2. 默认技术门要求 D2 原始日志证明 10 次 generation loss、degraded cleanup 与唯一 reattach；现有自动日志为 1/10 FAIL，该事实和残余风险继续保留；
3. 用户于 2026-08-21 明确接受 D2 并要求直接正式执行 D3-1。按权威顺序，本次用户验收决定构成显式流程例外，D3 正式 ACTIVE，但不得把例外描述成自动 10/10 PASS。

## 3. 固定范围

- Windows 11 x64；
- 当前设备的主显示器；
- 单张诊断海报；
- Per-Monitor v2 在创建任何 HWND 前生效；
- 当前 `RAISED_PROGMAN` layered visual + split interaction islands 路径；
- 100%、125%、150%、200% 四档缩放分别建立独立 Evidence run；
- 系统缩放、分辨率和任务栏设置只由用户通过 Windows 设置修改，探针不写注册表、不调用私有 API、不自动改变显示模式。

多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔保持 `DEFERRED`。

## 4. 统一坐标模型

探针同时记录并区分：

1. `logical_geometry_dip`：海报自身的技术中立逻辑几何；
2. `normalized_anchor`：相对于主显示器可用工作区的归一化位置；
3. `monitor_rect_px` 与 `work_area_rect_px`；
4. `expected_window_rect_px`：逻辑几何按当前有效 DPI 投影后的期望物理矩形；
5. `window_rect_px`：`GetWindowRect`；
6. `client_rect_px`：客户区尺寸及映射到屏幕后的矩形；
7. `extended_frame_bounds_px`：`DWMWA_EXTENDED_FRAME_BOUNDS`；
8. `render_target_px`：本帧实际绘制目标宽高；
9. parent、owner、相邻 z-order、visible、iconic、cloaked、active、TopMost；
10. process/system/host/self DPI 与 DPI awareness context；
11. geometry revision、hit-region revision、collapse state 和动作计数。

物理像素只用于投影和观测，不写回为逻辑几何权威。不得使用固定顶部补偿、固定边框厚度或特定分辨率坐标修复白条。

## 5. 几何夹具

### 5.1 初始与约束

- 默认锚点：`top-right`；
- 默认宽度：`max(work_area_width_dip × 0.22, 280 dip)`；
- 最小宽度：240 dip；
- 最大宽度：工作区宽度 45%；
- 默认展开高度：工作区高度减去上下安全边距；
- 最小展开高度：240 dip；
- 最大展开高度：工作区高度减去安全边距；
- 收起高度：60 dip；
- 默认安全边距：探针显式记录的逻辑值，不作为修复白条的隐藏补偿。

### 5.2 操作

- 标题交互岛：指针移动达到 5 dip 后开始拖动；
- 右下缩放岛：左上角固定，连续调整宽高并受动态工作区约束；
- 收起岛：只切换底部高度，标题顶部、水平位置、宽度和按钮屏幕位置不变；
- 预设入口：依次验证 240、280、360、默认和最大宽度，以及最小/默认/最大展开高度；
- 空白视觉区域继续命中原生桌面，视觉表面不接收指针输入。

## 6. 执行矩阵

### D3-0 — 构建与静态边界预检

- x64 Release 无 IDE 启动；
- 当前缩放下至少 5 秒连续采样；
- window/client/DWM/render 四类物理边界逐样本一致；
- 自有可见表面均为桌面子窗口、非 TopMost、无 owner；
- 退出后零自有窗口。

### D3-1 — 四档 DPI 独立运行

在 100%、125%、150%、200% 各自完成：

1. 冷启动 5 次，共 20 次；
2. 主屏中部拖动并松开 5 次，共 20 次；
3. 缩放 5 次，共 20 次，五次分别覆盖 240、280、360、默认和最大宽度，并覆盖最小/默认/最大展开高度；
4. 收起/展开完整循环 5 次，共 20 次；
5. 每档结束时验证普通程序覆盖、桌面空白输入和交互岛命中无回退。

每档必须使用新的进程和新日志；不得把 100% 的通过借给其他 DPI。

### D3-2 — 分辨率变化

在 100% 缩放基线下，从原生分辨率切换到一个系统支持的较低分辨率，再恢复原生分辨率，完成 5 个改变/恢复循环。每次确认：

- normalized anchor 投影到新工作区；
- 海报整体保持可见且不覆盖任务栏；
- 逻辑尺寸不被物理像素污染；
- 命中区与视觉边界同帧更新；
- 无白条、灰框、裁切或一帧旧尺寸残留。

### D3-3 — 工作区与任务栏边界
使用 Windows 设置切换任务栏自动隐藏并恢复，完成 5 个改变/恢复循环；若当前 Windows 构建还提供受支持的任务栏位置变化，可作为附加证据，不通过注册表强制实现。

每次确认工作区变化被检测、海报按归一化锚点重算、仍位于任务栏之外，且锁定状态不会让海报永久留在不可见区域。

## 7. 自动 PASS 条件

每个采样必须同时满足：

- `window_rect_px == client_screen_rect_px == extended_frame_bounds_px`；
- `render_target_px` 与客户区宽高完全一致；
- 期望物理尺寸与实测尺寸误差不超过 DPI 投影舍入允许的 1 px；
- attached 期间自有表面数量和相对 DefView 顺序正确；
- 无 WS_CAPTION、WS_THICKFRAME、WS_EX_TOPMOST、owner、iconic、cloak 或激活；
- collapse 前后顶部、左侧和宽度不变，只改变 bottom/height；
- resize 期间左上角不变；
- 工作区变化后归一化锚点与可见约束成立；
- hit-region revision 与 geometry revision 同一次提交更新；
- 没有旧物理像素写回逻辑几何。

## 8. 人工 PASS 条件

- 20 次冷启动均无顶部白条、灰色透明包络、未绘制背景和首帧裁切；
- 20 次拖动全过程及松开后无条带、边框突变或一帧错位；
- 20 次缩放可连续操作，圆角、边框、内容和命中同步；
- 20 次收起/展开中标题栏顶部与按钮屏幕位置保持不动；
- 四档 DPI 均清晰可见且无固定像素补偿痕迹；
- 分辨率/工作区改变时无越界、任务栏覆盖、旧尺寸残留或异常抬层；
- 普通程序始终高于诊断卡，桌面图标与空白原生输入合同无回退。

任何单次白条、灰框、非客户区、裁切、错位、错误 TopMost 或操作一次后失效均使对应 run FAIL；不能以随后恢复正常抵消。

## 9. 探针 CLI 合同

```text
--run-boundary-test
--scenario static | interactive | display-change
--expected-dpi 96 | 120 | 144 | 192
--duration-ms <bounded>
--expected-drags <count>
--expected-resizes <count>
--expected-collapse-cycles <count>
--expected-display-changes <count>
--log <jsonl path>
```

退出码 0 只证明结构化边界、动作计数和清理合同通过；白条、灰框、视觉裁切和一帧错位仍需人工观察或与同一 Evidence ID 对齐的录屏证明。

## 9.1 当前就绪证据

2026-08-21 已完成 D3 x64 Release 探针和 100% / 96 DPI 的 D3-0 静态预检。初版 605/605 样本因 visual 位于 DefView 下方而 FAIL；修正仅初次挂载的 sibling 顺序后建立新 run，604/604 样本 PASS。失败与通过证据均独立保留。

D3-3 的最新版 run 已以 exit 0 完成：3,802/3,802 个 sample PASS，11 个 `display_geometry_changed` 事件，观察到 `0,0,2560,1392` 与 `0,0,2560,1440` 两种工作区，drag/resize/collapse-cycle 全为 0；用户确认人工视觉 PASS。前一 D3-3 `boundary_contract_fail` 尝试继续保留。D3 总体按用户决定收敛，但 D3-1 未测 DPI 档和 D3-2 自动例外仍在，因此状态为 `USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS`，不是严格 H5 自动 PASS。当前进入 TS-H01-E 接口冻结。


## 10. 退出与交接

D3 的全部适用 run PASS 后：

1. Gate H5 标记 PASS；
2. TS-H01 的 H0–H5 状态汇总；
3. 冻结实现中立的 `DesktopHost` 能力与诊断字段；
4. 才允许不同 UI/渲染候选接入同一宿主接口；
5. 触控板显示桌面及 deferred 多屏矩阵继续按已登记状态保留，不能静默写成支持。

# QTS-04-03 split input islands 审计（2026-08-27）

状态：`R2 IMPLEMENTED / RELEASE AUTO 13 OF 13 PASS / INITIAL USER GATE FAILED / RETEST PENDING`

## 1. 完成范围

QTS-04-03 已把单海报从“可见但不交互”推进到结构化 split-input 生产路径：

1. 在公共 contracts 中加入 `HitRegionSet`、region role、priority、action index、pointer phase/button 和 revision-fenced `HostInputEvent`；
2. `IDesktopHost::present` 现在原子接收 screen bounds、frame、premultiplied pixels 和同投影的 hit regions；
3. visual HWND 始终保留 `WS_EX_TRANSPARENT`，不使用覆盖整张海报的 input envelope；
4. 每个启用区域建立独立 sibling child HWND，空白位置直接命中 Explorer；
5. 支持零区、一区、多区、重叠 priority、原子 replacement、同身份原地更新和退出销毁；
6. Runtime 从 renderer 的实际 `NightOpsLayout` 生成标题栏、锁定、收起、六卡片及编辑尺寸边；
7. 实现 5px gesture threshold、标题拖动、工作区夹紧、右/下/右下缩放、锁定、收起/展开和卡片选中反馈；
8. geometry、scene 和 input projection 每次变化都递增 projection/hit revision，旧事件不再驱动当前状态；
9. Explorer generation 恢复后重新发布当前 frame 和当前 input projection；
10. 退出时先撤销 callback，再销毁 input islands、visual、renderer buffer 和其余资源。

本阶段的六张卡片仍是视觉/交互 fixture，不冒充 QTS-06 的正式内容 Item。

## 2. 输入拓扑

`Win32InputIslandSet` 为每个 enabled region 创建一个 `DesktopUpV2QTS04InputIsland` child HWND：

- parent 与 visual 一致；
- `WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`；
- alpha=1，仅提供原生命中表面，不绘制海报像素；
- sibling z-order 按 priority 从低到高排列；
- 不使用 `HTTRANSPARENT` 伪穿透，因为该机制不能可靠跨线程回落到 Explorer；
- 不把鼠标消息转发给 Explorer，空白区域根本不存在 DesktopUp input HWND。

这使“海报可见区域”与“DesktopUp 可交互区域”不再被错误地视为一个矩形。

## 3. 运行时语义

| role | USING | EDITING | locked | collapsed |
| --- | --- | --- | --- | --- |
| TitleDrag | 启用 | 启用 | 禁用 | 启用（未锁定时） |
| Item × 6 | 启用 | 启用 | 启用 | 禁用 |
| LockToggle | 启用 | 启用 | 启用 | 启用 |
| PresentationToggle | 启用 | 启用 | 启用 | 启用 |
| ResizeRight/Bottom/BottomRight | 禁用 | 启用 | 禁用 | 禁用 |

标题拖动与缩放只有移动超过 5px 才形成几何操作。收起只改变高度，left/top/width 不变；展开恢复上次展开高度并重新夹紧主屏工作区。

## 4. repeated present 根因与修正

新增矩阵最初在“一岛 → 多岛”处失败，但 input-islands 的创建、批量定位和 z-order 均未报错。进一步分解 Host 后确认：

- 首帧显示时 `WM_PAINT/StretchDIBits` 有效；
- 第二次 `InvalidateRect` 与 `UpdateWindow` 都返回成功；
- Explorer child layered visual 却不再收到新的 `WM_PAINT`；
- Host 因 `paintSucceeded=false` 拒绝整个 present，后续 island 断言只是连锁失败。

修正后，Host 保留 `WM_PAINT` 处理系统请求的重绘，同时每次 `present` 使用 Host-owned DC 与 `StretchDIBits` 主动提交当前完整像素，并用 `ValidateRect` 关闭当前脏区。input islands 因此可以随拖动、缩放、锁定、收起和卡片选择连续更新，而不依赖 Explorer 是否再次派发 `WM_PAINT`。

## 5. 自动证据

新增 `qts04.input-islands`，验证：

- zero region 保持全海报 pointer-transparent；
- one region 精确路由到对应 island；
- multi region 不退化为矩形 envelope；
- 海报空白由 `ChildWindowFromPointEx` 直接解析为 Explorer desktop target；
- overlap 由显式高 priority region 获得命中；
- island click 产生带 hit/projection revision 的语义事件；
- multi → zero 原子替换后旧 HWND 全部销毁；
- `BeginExit` 后 visual、islands 和 owned resources 均为零。

R2 Release 全量结果：`13/13 PASS`。新增 `qts04.geometry-gesture`，并扩展 `qts04.input-islands` 验证真实 visual/islands 移动、320×420 最小展开尺寸及活动 capture HWND 稳定性。

## 6. 首次人工门失败与 R2 修订

首次人工验证确认两项真实回归，因此原 `12/12` 自动结果不能关闭 QTS-04-03：

1. 标题拖动时 Runtime 更新了 `posterBounds.left/top`，但旧 `IDesktopHost::present` 没有位置参数；Host 继续使用 attach 时的初始坐标；
2. Runtime 允许高度缩到 240 px，而 renderer 在 300 px 以下撤销内容和 resize region；同步 `WM_MOUSEMOVE` 中可能替换持有 capture 的 island，失败路径会隐藏 visual。

R2 没有引入第四套手势语义，而是按 E4 与 Qt R2 候选证据收敛：

- `screenBounds + frame + hit regions` 由同一次 Host present 发布；
- Runtime 临时 `GeometryGesture` 已替换为 Core `GeometryGestureSession`；
- 展开最小尺寸统一为 320×420，右下角越过顶部只钳制，不撤销活动 resize island；
- live present 失败时回滚上一有效几何并重发，禁止把失败留成永久隐藏；
- 自动门新增真实 HWND 位移、连续会话、最小/最大约束和 capture 稳定性断言。

## 7. 证据边界

自动门证明 HWND 拆分、命中解析、revision、事件和资源清理合同成立；它不能替代用户对真实桌面框选、右键、快捷方式、拖动手感、按钮位置及缩放边缘的人工确认。

Win+D、桌面刷新、文件选择器、Explorer 人工重启、显示变化和完整 P0 仍未在 QTS-04-03 中宣称通过。

# QTS-04-04 生命周期与恢复矩阵计划 V1

状态：`USER MATRIX 1～6 PASS / HISTORICAL BASELINE / CURRENT GATE MOVED TO R4`

日期：2026-08-28

实施分支：`impl/qt-solution`

## 1. 目标

把已经在 TS-H01、V1 和 TS-C01 Qt 候选中分别验证过的桌面生命周期经验，落实到当前生产 `Runtime + Win32DesktopHost + QPainter renderer`，覆盖：

1. `Win+D` 与任务栏“显示桌面”；
2. 桌面右键、任意左键收起菜单和桌面刷新；
3. 普通窗口及 Windows 文件选择器覆盖；
4. Explorer generation 丢失、degraded、唯一重挂载和新首帧；
5. 主屏分辨率、工作区及 DPI 环境变化；
6. 所有场景后的单海报输入、层级、几何和退出回归。

本轮不实现控制中心、托盘、状态持久化或多海报；它们属于 QTS-05 以后。

## 2. 复用的既有结论

| 来源 | 已验证结论 | QTS-04-04 用法 |
| --- | --- | --- |
| TS-H01-D1 | 正确 raised-Progman child 在 Win+D 中不需要 hide/show 修复 | 不监听 Win+D，不切换可见性，不在恢复时临时创建顶层海报 |
| V1 层级故障复盘 | 右键、刷新和文件选择器闪烁来自把无关前台/窗口事件误判成宿主恢复并重新抬层 | 无关进程 CREATE/DESTROY 在进入拓扑发现前过滤 |
| TS-H01-D2 / E3 | Shell 恢复必须 generation fencing、稳定发现、唯一 visual、无 TopMost/fallback | Explorer 事件合并到 120 ms 稳定窗口；旧代只清理一次，新安全目标只挂载一次 |
| TS-H01-D3 / E4 | 主屏系统 DPI 是投影权威；Explorer parent DPI 只能诊断 | 使用 PMv2 进程 `GetDpiForSystem()`、逻辑 DIP 和归一化锚点重投影 |
| TS-C01 R4-C/R4-D | 文件选择器和普通窗口只影响覆盖关系；显示变化与 Shell generation 正交 | 两类事件使用不同 maintenance result 和 revision，不互相触发 |

## 3. 冻结不变量

1. 层级始终为“普通/全屏程序 > DesktopUp 海报 > Explorer 图标 > 壁纸”；
2. poster visual 始终是 raised-Progman 下的 `WS_CHILD`，非 `TopMost`，无普通顶层回退；
3. Win+D、焦点、右键、刷新、普通窗口和文件选择器不得改变：
   - visual HWND；
   - `ShellGeneration`；
   - `LifecycleEpoch`；
   - `SurfaceEpoch`；
   - 用户可见性；
4. Explorer 身份或角色树真实变化才允许换代；单次临时空枚举和重复通知不能重复进入 degraded；
5. display change 只增加 `DisplayEnvironmentRevision` 和投影 revision，不 detach/re-attach；
6. 重挂载和显示尺寸变化都经过不可见首帧门，不显示旧尺寸帧；
7. 退出先停 display monitor 和回调，再同步销毁 islands、visual、hook、IPC、renderer 和 store。

## 4. 场景矩阵

| ID | 场景 | 自动证据 | 人工证据 | 判定 |
| --- | --- | --- | --- | --- |
| L1 | 外部普通窗口 CREATE/DESTROY | 独立子进程事件源；ignored 计数增加，topology validation 不增加 | 普通窗口覆盖海报 | HWND/generation/epoch/visible 全不变 |
| L2 | 文件选择器 | L1 证明无关窗口事件分类 | 打开/关闭一次真实文件选择器 | 文件选择器始终高于海报，无瞬时抬层 |
| L3 | Win+D | 结构、样式和零显隐修复检查 | 键盘 Win+D 完整循环 | 无隐藏再恢复闪烁，无 TopMost |
| L4 | 桌面右键/左键/刷新 | Shell 事件稳定窗口和同目标 no-op | 右键后左键、右键刷新 | 不抬层、不换代，桌面输入正常 |
| L5 | Explorer 重建 | 显式 generation replacement、旧 HWND 销毁、新首帧、唯一回调 | 任务管理器重启 Explorer 一次 | 最多一个新海报，无旧帧、fallback 或残留 |
| L6 | 无变化 display poll | `refreshDisplayEnvironment()==Unchanged` | 无 | display revision、Host 身份不变 |
| L7 | DPI/分辨率/工作区 | 96/120 DPI、负坐标、收起态逻辑投影夹具 | 当前主屏执行一种变化并恢复 | 只重投影；无 Shell recovery、白条、灰框或裁切 |
| L8 | 事后输入/退出 | 既有 input-islands 与 exit ledger | 拖动、缩放、锁定、收起/展开后退出 | 无输入回退和资源残留 |

## 5. 实现分解

### 5.1 Shell 事件分类

`SetWinEventHook(EVENT_OBJECT_CREATE..DESTROY)` 继续只传递不可变通知；coordinator owner thread 根据类名、Explorer PID 和当前 degraded 状态分类。无关事件只增加 `ignoredShellEventCount`，不调用 `discoverShellTarget()`。

### 5.2 稳定发现与唯一恢复

相关 Shell 通知以同一个 timer id 合并到 120 ms 稳定窗口。发现结果与当前目标一致时为 no-op；连续无安全目标只保留一次 degraded；新安全目标创建一组新 visual/islands，并等待 Runtime 发布匹配新 `SurfaceEpoch` 的首帧。

### 5.3 显示环境

Runtime 每 250 ms 复用 TS-C01 R4-D 已验证的轻量主屏采样方式。Host 只在 monitor/work-area/effective-DPI 实际改变时增加 `DisplayEnvironmentRevision`。Core 保留 DIP 宽高和归一化锚点，投影到最新物理工作区；不从上一次物理矩形连续缩放。

## 6. 证据边界

自动 13/13 只能证明合同、投影数学、外部普通窗口事件分类、显式 generation replacement、输入和退出夹具。它不能代替真实 Win+D、桌面菜单/刷新、Windows 文件选择器、Explorer 重启和实际显示设置变化的人工观察。


## 7. 执行进展与拖动闪烁 R1

- 用户已确认真实 Win+D、桌面菜单/刷新、文件选择器、Explorer 重建、显示变化和输入回归 1～6 项通过；
- 同轮发现标题拖动存在规律性间歇闪烁；根因是同尺寸移动仍重复 full render、region、show/z-order 与 islands 重排；
- R1 保留原 `GeometryGestureSession`，仅新增 visual/islands 单批次 relocation 热路径；
- Release 全量 13/13 PASS，真实拖动观感复测仍是关闭前唯一剩余门。

## 8. 退出门

QTS-04-04 关闭必须同时满足：

1. Release 构建成功且全量 CTest 通过；
2. L1/L6 自动不变量通过；
3. L2～L5、L7、L8 人工门通过；
4. 无白条、灰框、隐藏再恢复闪烁、瞬时 TopMost、重复海报或普通顶层 fallback；
5. 文档记录真实执行项和未执行项，不把自动结果扩写成人工 PASS。

关闭后进入 QTS-05：StateStore、控制中心与托盘。

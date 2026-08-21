# TS-H01-D 生命周期完整门执行计划 V1

状态：`USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS — HANDOFF TO TS-H01-E`

日期：2026-08-20

## 1. 输入基线

本计划只建立在以下已通过、仍受范围限制的事实之上：

- TS-H01-B：当前 `RAISED_PROGMAN` Windows 8-aware layered child 路径满足“普通程序 > 探针 > 桌面图标 > 壁纸”；
- TS-H01-C：透明视觉表面与三个交互岛并存，Explorer 原生空白/快捷方式输入和海报交互均通过；
- ordinary GDI child 的视觉失败继续保留；
- traditional WorkerW 在当前环境仍为 `UNKNOWN_NOT_PRESENT`；
- 没有 UI/渲染框架因此获选。

## 2. 拆分原则

TS-H01-D 不允许把 Win+D、Explorer 重建和显示边界混成一次“看起来正常”的测试。三个子门分别产生证据与结论；前一子门失败时停止向后一子门扩散。

```text
TS-H01-D1  Win+D / 任务栏显示桌面连续可见
→ TS-H01-D2  Explorer 重建、DEGRADED 与安全恢复
→ TS-H01-D3  分辨率、DPI、工作区和非客户区边界
```

## 3. D1 — 显示桌面连续性

### 3.1 独立场景

1. 键盘 `Win+D`：10 个完整显示/恢复循环；
2. 任务栏“显示桌面”：10 个完整显示/恢复循环；
3. 触控板显示桌面：当前设备不可用则保持 `UNKNOWN`，不伪造 PASS。

键盘与任务栏入口不得混在同一证据运行中。

### 3.2 高频观测

探针在整个运行期以不高于 10 ms 的采样间隔记录：

- visual 与每个 interaction island 的 HWND、parent、owner；
- sibling z-order 与 `SHELLDLL_DefView` 相对位置；
- `visible / iconic / cloaked / active / topmost`；
- window/client/DWM extended frame bounds 与 DPI；
- Explorer PID、DefView/icon-view HWND 与拓扑；
- attach、detach、recreate、hide、show 调用计数；
- 单调时间戳和采样序号。

### 3.3 PASS

- 用户观察不到任何隐藏、闪烁、抬层或重绘空洞；
- 每轮显示与恢复过程中所有自有表面持续 visible、非 iconic、非 cloaked；
- parent、owner 和相对 DefView z-order 不改变；
- 没有 TopMost、激活、hide/show、detach/re-attach 或重建；
- 普通程序恢复后继续覆盖探针；
- 两个入口分别 10/10 通过。

“最后重新出现”不等于连续可见。

## 4. D2 — Explorer 重建与安全恢复

### 4.1 状态机

```text
ATTACHED_SAFE
→ SHELL_GENERATION_LOST
→ DEGRADED_NO_DESKTOP_HOST
→ SAFE_TARGET_REDISCOVERED
→ ATTACHED_SAFE
```

在新一代 `Progman/WorkerW + SHELLDLL_DefView + icon view` 尚未被唯一确认前，不创建普通顶层窗口，也不保留孤立视觉表面。

### 4.2 PASS

- 旧父树失效后自有 HWND 全部清理；
- degraded 期间没有普通顶层、TopMost、重复或孤立表面；
- 每代 Explorer 最多恢复一组 visual/islands；
- 新组仍保持图标上方、普通程序下方与 C 阶段输入合同；
- 10/10 重建循环通过。

## 5. D3 — 显示与边界矩阵

首轮主屏分别验证：

- 100%、125%、150%、200% DPI；
- 分辨率、工作区和任务栏边界改变；
- 中部拖动、最小/默认/最大尺寸、收起/展开、冷启动；
- logical geometry、HWND rect、client rect、DWM bounds、render pixels 一致。

任何顶部白条、灰框、非客户区、裁切、固定像素补偿或一帧错位均为 FAIL。

## 6. 实现边界

- 不发送或拦截 Win+D；只观察用户触发的系统行为；
- 不用 hide/show、最小化恢复、TopMost、前台激活或轮询 `SetWindowPos` 修复结果；
- 不注入/子类化 Explorer，不发送 `0x052C`，不读写桌面图标坐标；
- D1 探针不预先实现 D2 恢复逻辑，防止恢复代码掩盖 Win+D 原始行为；
- 每次源码或二进制变化建立新 Evidence run，不覆盖失败或旧通过证据。

## 7. 当前动作

TS-H01-D1 已完成并 PASS：键盘 `Win+D` 10/10、任务栏“显示桌面”10/10，两轮 36,382/36,382 遥测样本通过，用户确认无闪烁、抬层或清理残留；触控板入口保持 `UNKNOWN_DEVICE_AVAILABILITY`。

TS-H01-D2 已实现独立 x64 Release 恢复探针并完成 604/604 静态预检。首个 600 秒人工 run 的样本内合同为 72,148/72,148 PASS，但只记录一次 Explorer generation loss、degraded cleanup、safe rediscovery 和 unique reattach，最终返回 `recovery_contract_fail`。用户随后明确决定接受 D2 并直接进入 D3；因此流程状态记为 `USER-ACCEPTED PASS`，自动遥测例外和失败证据继续保留。

TS-H01-D3 的完整矩阵、x64 Release 探针和 D3-0 静态预检已完成；D3 现已正式 ACTIVE。当前 Release 在 100% / 96 DPI 的最新无操作静态回归通过，window/client/DWM/render 边界和 geometry/hit/render revision 全部一致。

D3-3 已完成自动与人工 PASS：3,802/3,802 个样本通过，记录 11 次工作区变化，drag/resize/collapse-cycle 均为 0。TS-H01-D 按用户决定以 `USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS` 收敛；D2 1/10、D3-1 未测 DPI 档和 D3-2 8/10 变化例外继续保留，strict H4/H5 不标记完整自动 PASS。当前交接到 TS-H01-E，冻结带能力状态和适用范围的 `DesktopHost` 原型接口。

# 2026-08-04 C-DHF-04 / C-DHF-05 真实环境矩阵第一轮

状态：`IN PROGRESS — AUTOMATED DESKTOP MATRIX PASS / PHYSICAL USER MATRIX PENDING`

## 目标

在当前双屏 Windows 11 会话中复测 C-DHF-04 Shell/显示恢复与 C-DHF-05 自动暂停，使用真实 Release、真实 HWND/前台窗口和运行时属性，不以源码字符串测试或历史 PID 代替实机证据。

## 环境

- 分支：`impl/poster-redesign-V1`
- 代码基线：V1 重置后的 A0 宿主源码，另含本轮缺陷修复
- 主屏：`[0,0,2560,1440]`，工作区 `[0,0,2560,1392]`
- 副屏：`[2560,243,4480,1443]`，工作区 `[2560,243,4480,1395]`
- 海报初始尺寸：`490×392`
- 运行时能力位：`7/7`（前台覆盖、WTS 会话、电源显示状态）
- 观测属性：`DesktopUp.RuntimePauseFlags`、`DesktopUp.ForegroundProbeFlags`、`DesktopUp.RuntimeMonitorCapabilities`

## 已完成矩阵

### C-DHF-04

| 场景 | 轮次 | 结果 | 证据 |
| --- | ---: | --- | --- |
| Win+D 显示桌面 / 返回应用 | 10 | `PASS` | 每轮海报 HWND 保持 visible、非 iconic；桌面与恢复前台切换不激活海报 |
| 副屏位置持久化 | 1 | `PASS` | 移动至 `(3000,400)` 后 placement 写入 `DISPLAY1`、归一化锚点与 fallback 坐标 |
| 副屏位置重启恢复 | 发现缺陷，修复待最终 10 轮复测 | `IN PROGRESS` | 原实现启动 `Moved` 竞态会用初始 `(72,154)` 污染内存快照；WorkerW 还可能裁剪副屏子窗 |

### C-DHF-05

| 场景 | 有效轮次 | 结果 | 证据 |
| --- | ---: | --- | --- |
| 同屏普通窗口 | 10 | `PASS` | `pause=0`，前台普通窗口不误停 |
| 同屏最大化 | 10 | `PASS` | `pause=5`（自动 + 最大化），恢复普通窗口后 `pause=0` |
| 同屏无边框全屏 | 10 | `PASS` | `pause=3`（自动 + 全屏），恢复后 `pause=0`；被其他窗口抢前台的尝试不计有效轮次 |
| 副屏全屏、海报在主屏 | 10 | `PASS` | 有效前台位于副屏时 `pause=0`，未发生跨屏误停 |

## 本轮发现并修复的缺陷

### 1. WorkerW 副屏裁剪风险

原 `select_workerw()` 只取首个可见 WorkerW，未验证父窗口是否覆盖海报屏幕矩形或与海报属于同一显示器。海报成为 `WS_CHILD` 后可能在副屏被父窗口裁剪。

修复：

- WorkerW 必须完整覆盖海报矩形；
- WorkerW 与海报中心必须映射到同一显示器；
- 覆盖检查在 `SetParent` 前执行；
- 不再发送 `0x052C` 强制 Explorer 创建隐藏 WorkerW；
- WorkerW 改为 `DESKTOP_UP_ENABLE_WORKERW` 显式 opt-in，生产默认使用已验证 ToolWindow 回退；
- 无合格候选时安全进入 ToolWindow，不强挂隐藏 Shell 窗口。

### 2. 启动 / 恢复期间位置写回竞态

Tauri 初始窗体 `(72,154)` 会产生 `WindowEvent::Moved`。若该事件在 placement 最终恢复前写回，会覆盖已加载的副屏 placement 内存快照。

修复：

- 页面加载和 host configure 最终恢复完成前禁止 placement 写回；
- 每次 reconnect/configure 都执行“关闭捕获 → 配置/恢复 → 开启捕获”；
- Shell/显示/DPI 恢复期间设置恢复门闩和事件代数；
- 恢复过程产生的临时 `Moved` 不得写盘或覆盖恢复源。

## 自动化回归

- Node / 契约测试：17/17 通过；
- 文档链接：75/75 通过；
- Rust 单元测试新增 WorkerW 覆盖矩形测试；修复前版本 10/10 通过；
- 默认 `target` 后续出现 `.cargo-build-lock` 权限异常，最终 Rust 测试正在隔离 `CARGO_TARGET_DIR` 中全量重编；
- 最终隔离 Release 构建通过，启动后海报恢复到副屏 `(3000,400)`，运行时能力位 `7/7`、暂停状态为 0；
- 由于物理人工场景尚未执行，本轮仍不封板 C-DHF-04/05。

## 尚未完成的真实人工矩阵

以下场景不能由当前自动化会话安全代替，尚不得标记 C-DHF-04/05 完成：

### C-DHF-04

- Explorer 真重启 10 次；
- 物理显示器拔出 / 重接；
- 主屏切换；
- DPI 100% / 125% / 150% / 200%；
- 修复版副屏位置冷启动恢复 10 次；
- 视觉确认无幽灵窗、裁切、亮带或焦点抢占。

### C-DHF-05

- 打开托盘菜单时继续保持最大化/全屏暂停原因；
- 真实游戏无边框全屏与独占全屏；
- Win+L 锁定 / 解锁；
- 物理显示器关闭 / 唤醒；
- 手动暂停与自动暂停叠加；
- 用户视觉确认恢复后动画与交互状态正确。

## 当前结论

- 自动化可覆盖的 Win+D、普通/最大化/全屏、跨屏不误停矩阵通过；
- C-DHF-04 真实矩阵发现了有效的副屏重启恢复缺陷，已完成代码修复但尚需最终 Release 与人工矩阵确认；
- C-DHF-04 和 C-DHF-05 均保持 `IN PROGRESS`，不得进入 C-DHF-06 封板；
- 任何 Explorer 重启、锁屏、熄屏、热插拔和 DPI 修改必须在用户允许并可现场确认时执行。

## 关联代码

- `src-tauri/src/desktop_host.rs`
- `src-tauri/src/lib.rs`
- `tests/runtime-surface.test.mjs`
- `%APPDATA%/com.allmight.desktopup/poster-placement-v1.json`

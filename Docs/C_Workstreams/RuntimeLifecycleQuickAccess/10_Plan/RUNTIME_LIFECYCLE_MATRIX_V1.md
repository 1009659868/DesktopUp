# Runtime Lifecycle Matrix V1

日期：2026-08-13

状态：`SEALED — C-RLC COMPLETE`

稳定标识：`runtime-lifecycle-matrix-v1`

## 1. 权威与状态维度

- Rust 主进程是 launch intent、单实例、托盘、原生表面、恢复和退出的唯一权威。
- SQLite Poster State Store 是海报业务持久状态的唯一权威；本矩阵不新建第二份业务状态。
- 控制中心按需创建，只消费原生/仓库快照；关闭或销毁不能改变海报持久状态。
- 自启有效状态必须来自 HKCU Run 读回和正式路径门，不能来自按钮本地状态。

矩阵使用以下状态：

| 维度 | 值 |
| --- | --- |
| launchIntent | `interactive` / `autostart` / `performanceGate`（仅内部首实例诊断）/ `invalid` |
| runtimePhase | `starting` / `running` / `exiting` |
| posterSurface | `initializing` / `visible` / `hidden` / `recovering` / `destroyed` |
| controlCenter | `absent` / `hidden` / `visible` / `destroyed` |
| tray | `initializing` / `ready` / `recovering` / `removed` |
| autostartRegistration | `absent` / `matching` / `drifted` / `unreadable` / `ineligible` |

## 2. 启动与单实例矩阵

| ID | 事件/前置 | 海报宿主 | 控制中心 | 托盘/进程 | 允许的持久副作用 | 当前基线 |
| --- | --- | --- | --- | --- | --- | --- |
| L01 | 首实例，无额外参数 | 初始化后显示已启用海报 | `absent` | 创建唯一托盘，进程进入 running | 只允许既有状态读取/恢复 | `VERIFIED BASELINE` |
| L02 | 首实例，参数精确为 `--autostart` | 初始化后显示静态海报 | `absent`，不得创建/聚焦 | 创建唯一托盘，进程进入 running | 不写注册表，不改变自启开关 | `VERIFIED C-RLC-02` |
| L03 | 第二实例，无额外参数 | 现有状态不变 | 创建或显示并聚焦唯一控制中心 | 第二实例通知后退出；不得产生第二托盘 | 无 | `VERIFIED C-RLC-02` |
| L04 | 第二实例，参数精确为 `--autostart` | 现有状态不变 | 现有状态不变，不得打开 | 第二实例静默退出 | 无 | `VERIFIED C-RLC-02` |
| L05 | 未知参数、重复 `--autostart` 或 `--autostart` 加其他参数 | 现有/首实例状态不得被改变 | 不得打开 | 结构化拒绝并以 64 退出请求进程 | 无 | `VERIFIED C-RLC-02` |
| L06 | 首实例精确 `--cdrp-performance-gate` / 已有实例时再次请求 | 仅首实例进入既有性能诊断运行；已有实例不变 | 不得打开 | 首实例允许诊断；第二实例结构化拒绝 | 只允许既有诊断报告 | `VERIFIED C-RLC-02` |

产品启动只允许 `[]` 与 `['--autostart']`。精确 `['--cdrp-performance-gate']` 是既有内部、仅首实例可用的诊断意图，不得作为产品参数或转发到已有实例。工作目录、参数大小写、额外空参数或其他开关不得改变 frozen intent；恢复探针保持在 launch intent、Tauri 和单实例初始化之前独立处理。

## 3. 控制中心、托盘与运行状态矩阵

| ID | 事件 | 海报宿主 | 控制中心 | 托盘/运行时 | 不变量 | 当前基线 |
| --- | --- | --- | --- | --- | --- | --- |
| S01 | 托盘左键单击 | 不变 | 不变 | 不变 | 单击永远不是显隐快捷键 | `VERIFIED BASELINE` |
| S02 | 托盘左键双击 | 不变 | absent/hidden → visible；visible → absent（优先释放，失败才 hidden） | 保持唯一托盘 | 首次请求才创建 WebView；关闭后不得保留控制中心 renderer | `VERIFIED — C-RLC-04 COMPLETE` |
| S03 | 菜单“打开主页” | 不变 | absent/hidden → visible；visible 保持 visible 并聚焦 | 不变 | 不切换海报 | `VERIFIED BASELINE` |
| S04 | 关闭控制中心窗口 | 不变 | absent（优先释放；销毁失败才 hidden） | 进程/托盘继续 | 不解释为退出；海报与业务状态不变 | `VERIFIED — C-RLC-04 COMPLETE` |
| S05 | 菜单“显示海报宿主” | visible ↔ hidden | 不变 | hidden 时 lifecycle mode 为 stopped | 不能顺带打开控制中心 | `VERIFIED BASELINE` |
| S06 | 菜单“暂停动态效果” | 可见性不变，running mode 在 paused/active 间切换 | 不变 | 托盘勾选/提示消费同一快照 | 自动暂停理由不得被手动恢复清除 | `VERIFIED BASELINE` |
| S07 | 打开设置/Agent | 不变 | 按需显示对应路由 | 不变 | 无 API Key 不初始化 Provider；显式前端 ready 前命令按序排队且只投递一次 | `VERIFIED — C-RLC-04 COMPLETE` |
| S08 | 重复/乱序控制中心请求 | 不变 | 最终只存在零或一控制中心 | 不阻塞托盘消息循环 | 不丢失 ready 前命令，不重复创建；释放后可重新懒创建 | `VERIFIED — C-RLC-04 COMPLETE` |
| S09 | 显式托盘退出 | 销毁 | 销毁 | phase 粘滞为 exiting，刷新必要状态、移除托盘并退出 | 迟到显隐/暂停/恢复不能逆转 exiting | `VERIFIED — C-RLC-05 USER RELEASE PASS` |

## 4. 自启注册矩阵

冻结目标命令：`"%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe" --autostart`。

| ID | 用户动作与读前状态 | 路径门 | 目标操作 | 写后状态/结果 | 禁止副作用 | 当前基线 |
| --- | --- | --- | --- | --- | --- | --- |
| A01 | 无动作/应用启动 | 任意 | 只读诊断 | 保持原值 | 启动本身不得创建、修复或删除注册值 | `VERIFIED READ-ONLY BASELINE` |
| A02 | 显式启用；`absent` | eligible | 写 `DesktopUp`/`REG_SZ`/精确命令并读回 | `matching` 才成功 | 不创建任务、服务、Startup 副本或 HKLM 值 | `VERIFIED — TEMPORARY TEST ROOT LIVE` |
| A03 | 显式启用；`matching` | eligible | 幂等读回，不重复写 | `matching` 成功 | 不启动额外进程 | `VERIFIED — TEMPORARY TEST ROOT LIVE` |
| A04 | 显式启用；`drifted` | eligible | 仅在本次明确 opt-in 下修复应用自有值并读回 | `matching` 才成功，记录 before/after 诊断 | 不触碰 Run 中其他值 | `DEFERRED — C-RCG FORMAL INSTALL MATRIX` |
| A05 | 显式启用或禁用；任意状态 | ineligible | 拒绝 | 原状态不变并返回正式路径门原因 | 开发/便携 Release 零写入 | `VERIFIED C-RLC-03` |
| A06 | 显式启用；`unreadable` | 任意 | 拒绝 | UI 保持未确认状态并返回错误 | 不盲写覆盖 | `AUTOMATED VERIFIED` |
| A07 | 显式禁用；`matching` 或 `drifted` | eligible | 只删除 HKCU Run 的 `DesktopUp` 值并读回 | `absent` 才成功 | 不删除父键或其他应用值 | `VERIFIED — TEMPORARY TEST ROOT LIVE` |
| A08 | 显式禁用；`absent` | eligible | 幂等读回 | `absent` 成功 | 无 | `AUTOMATED VERIFIED / LIVE FINAL STATE ABSENT` |
| A09 | 显式禁用；`unreadable` | eligible | 返回失败 | 不乐观翻转 UI；保留重试证据 | 不伪装成功 | `AUTOMATED VERIFIED` |

2026-08-14 用户明确授权将本轮测试目标临时调整为精确 `G:\A_\Tools\Test\DesktopUp.exe`。实现采用默认关闭的 Cargo feature `autostart-test-root`，只在当前可执行文件与该路径完全匹配时暴露 `temporaryTest` eligibility；默认 Release 和 NSIS 继续只接受冻结的 `%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe`。该临时门已关闭 C-RLC-03 的真实 HKCU 与单实例证据，但不等于净机正式安装路径认证。

托盘与控制中心必须消费同一写后读回快照。命令执行中可显示 pending，但不得先修改本地勾选再等待异步结果；这条规则用于阻止状态回弹和两端不一致。

## 5. Explorer/TaskbarCreated 与退出矩阵

| ID | 事件/前置 | 托盘 | 海报宿主 | 控制中心 | 恢复/退出规则 | 当前基线 |
| --- | --- | --- | --- | --- | --- | --- |
| R01 | running 收到首次 `TaskbarCreated` | 重建唯一图标与菜单快照 | 请求现有恢复协调器重新核对桌面层 | 保持原 visible/hidden/absent | 去抖并记录 generation/trigger | `VERIFIED — C-RLC-05 USER RELEASE PASS` |
| R02 | 恢复中重复 `TaskbarCreated` | 不重复创建 | 合并到当前恢复请求 | 不变 | 单飞、有界重试 | `VERIFIED — C-RLC-05 USER RELEASE PASS` |
| R03 | Explorer 尚未就绪或连续恢复失败 | 进入可诊断 degraded 状态 | 保留安全 ToolWindow 回退或明确失败状态 | 不得为掩盖失败而打开 | 达到上限后停止自动重试，保留手动重连 | `VERIFIED — AUTOMATED DEGRADED PATH / USER EXPLORER RECOVERY PASS` |
| R04 | exiting 收到 `TaskbarCreated`/Shell/显示迟到事件 | 保持 removed/走向 removed | 不恢复 | 不创建/显示 | 忽略并记录；Exiting 不可逆 | `VERIFIED — C-RLC-05 USER EXIT-RACE PASS` |
| R05 | 用户执行“重新连接桌面宿主” | 不重复创建 | 触发一次 ManualReconnect | 不变 | 只恢复桌面层，不改变海报业务状态 | `VERIFIED BASELINE` |

恢复过程禁止使用通用 `HWND_TOPMOST/NOTOPMOST` 脉冲，不得让海报短暂覆盖普通应用。

## 6. 验证与认证边界

### 自动证据

- launch intent 表驱动单元测试；
- 第一/第二实例多进程探针；
- autostart 缺失/匹配/漂移/不可读/不合格夹具和零越界写入检查；
- 控制中心零/一拓扑、命令排队、重复事件和 Exiting 迟到事件测试；
- TaskbarCreated 去抖、单飞、上限与手动重连测试；
- 全量 Node/Rust、production UI、Release/NSIS 构建。

### Release 人工证据

- 普通启动、自启启动、第二普通实例、第二自启实例；
- 托盘单击/双击/右键、控制中心关闭/重开、海报显隐和暂停；
- 正式安装路径自启启用/重启/禁用；
- Explorer 重启后的托盘/海报恢复和连续显式退出；
- 既有主屏单海报空白穿透、卡片、拖动、锁定、收起/展开和无 TOPMOST 闪烁核心回归。

### 不在本矩阵认证

多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启、热插拔、净机安装、升级回滚、恢复型卸载和签名仍分别由后继平台矩阵或 C-RCG/C-UGM/C-URV 负责。

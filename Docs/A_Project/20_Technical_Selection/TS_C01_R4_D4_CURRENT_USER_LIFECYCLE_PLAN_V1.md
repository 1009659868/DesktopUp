# TS-C01 R4-D4 当前用户安装、重启与清理计划 V1

状态：`CLOSED — USER-ACCEPTED EXECUTED SCOPE PASS / AUTOSTART DEFERRED BY USER / DEVICE RESTART SKIPPED BY USER / P0 NOT RUN`

日期：2026-08-26

## 1. 目的

R4-D4 回答一个候选级问题：已经通过海报宿主、输入、Shell、主题与故障功能门的 Qt 6.8.3 候选，能否被包装为可选择安装目录的当前用户软件，并在普通启动、自启、第二实例、应用重启、设备重启和卸载期间继续遵守公共生命周期合同。

本阶段不把候选探针冒充完整产品。正式内容 Item、完整控制中心、最终托盘信息架构、唯一生产 StateStore、主题包平台和 AI 接入仍未实现；D4 只提供足以验证安装与生命周期边界的候选壳层。

## 2. 进入决定

用户于 2026-08-25 明确认定 Qt 候选基本通过并要求进入 R4-D4。R4-D3 因此记录为 `USER-ACCEPTED WITH WORKING-SET EXCEPTION`：功能合同通过，工作集实测峰值 136.332 MiB；冻结的 50 MiB 原始目标不被改写，差异永久进入候选证据和后续裁决。

用户于 2026-08-26 要求安装器必须允许用户指定安装目录。此前 Build ID `ts-c01-qt-6.8.3-r4d4-20260825-02` 的默认路径正式安装和首次普通启动机器观察保留为历史证据，但不能代替本次可选目录修订的正式验证。

该决定允许继续执行 D4，但不等于 Qt 获胜，也不允许将 P0 改为 PASS。

## 3. 冻结边界

| ID | 场景 | 必须满足 | 当前状态 |
|---|---|---|---|
| D4-A | 包与安装范围 | 单一 `DesktopUpSetup.exe`；`RequestExecutionLevel user`；默认 `%LOCALAPPDATA%\Programs\DesktopUp`；目录页允许选择其他当前用户可写目录；既有非空目录在无部署路径冲突时允许；含 `DesktopUp.exe`、`uninstall.exe`、`resources/` | `PASS — <user-selected-test-root>\DesktopUp` |
| D4-B | 首实例与第二实例 | 普通/自启首实例只有海报+唯一托盘；普通第二实例只激活控制中心角色探针；自启第二实例静默退出；无重复海报/托盘 | `ORDINARY PASS / AUTOSTART DEFERRED` |
| D4-C | 自启所有权 | 默认 OFF；只允许正式安装实例显式 opt-in；HKCU Run 值名精确 `DesktopUp`、命令引用实际安装路径且参数精确 `--autostart`；写后回读；禁用只删除完全匹配的自有值 | `DEFAULT OFF CONFIRMED / REMAINDER DEFERRED BY USER` |
| D4-D | 状态与重启 | 原子保存位置、尺寸、锁定、收起、交互模式和候选主题；应用重启和设备重启恢复；无控制中心抢焦点 | `APPLICATION RESTART PASS / DEVICE RESTART SKIPPED BY USER` |
| D4-E | 卸载与残留 | 请求主实例退出；仅按生成清单删除本安装器文件，并删除自有开始菜单、卸载登记、匹配自启和候选数据；不递归删除用户选择的整个安装目录，不删外部文件、V1 数据或其他候选目录；无进程/托盘/HWND/输入岛残留 | `PASS — MACHINE SCAN + USER TRAY CONFIRMATION` |

## 4. 候选实现合同

### 4.1 安装目录与路径资格

默认安装目录为 `%LOCALAPPDATA%\Programs\DesktopUp`，目录选择页允许用户改为其他当前用户可写的专用目录。安装器不得请求管理员权限；若用户选择当前令牌不可写的位置，应明确失败，而不是隐式提权。

目录选择值就是实际安装目录。既有非空目录不再被整体拒绝：构建期生成的安装冲突清单会逐一检查 `DesktopUp.exe`、`uninstall.exe`、正式标记以及全部 Qt/资源部署路径；不存在同名路径时允许安装，存在冲突时拒绝覆盖并要求更换目录或移走冲突内容。选择磁盘或共享根目录时自动改用其 `DesktopUp` 子目录。若 HKCU 已登记另一安装位置，必须先卸载现有版本再改变目录，避免同一用户出现两个互相争夺单实例、自启和卸载所有权的正式安装。

正式运行资格不再绑定默认绝对路径，而由三项共同判定：

1. 当前文件名为 `DesktopUp.exe`；
2. 相邻 `resources/r4d4-formal-install.flag` 内容为本轮所有者标记；
3. `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp\InstallLocation` 与当前可执行文件目录匹配。

因此默认目录和用户选择目录均可运行；直接解压、复制单个 EXE、复制整目录到未登记位置和自动包探针目录均被拒绝，不创建状态目录、不写 Run。`/R4D4PACKAGEPROBE=1` 只属于构建期自动探针，不是面向用户的便携运行模式。

生产需求的数据根仍冻结为 `%LOCALAPPDATA%\DesktopUp`。为了避免候选测试删除既有 V1 或其他候选数据，本轮只拥有 `%LOCALAPPDATA%\DesktopUp\Candidate\TS-C01` 子树；正式卸载只递归删除该子树，并仅尝试移除已经为空的父目录。

### 4.2 启动意图和单实例

- 首次普通启动：创建海报和唯一托盘，不打开控制中心；
- 首次 `--autostart`：创建海报和唯一托盘，不打开/聚焦控制中心，不修改自启注册；
- 普通第二实例：通过当前用户命名事件通知首实例打开唯一控制中心角色探针，第二实例立即退出；
- `--autostart` 第二实例：不发送激活通知，静默退出；
- `--request-exit`：只向现存首实例发送退出请求；无首实例时幂等成功；
- 其他未知参数：拒绝，不改变现有 UI。

### 4.3 托盘和自启

候选托盘只提供打开控制中心角色探针、显式切换开机启动和退出。Run 项固定为动态引用本次正式安装路径：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
DesktopUp = "<所选安装目录>\DesktopUp.exe" --autostart
```

若同名值存在但内容不完全匹配，候选不得覆盖或删除，必须记录所有权冲突。候选实现使用写后回读，默认不在安装阶段注册自启。

### 4.4 状态原子性

候选状态文件为 `candidate-state-v1.json`，通过 `QSaveFile` 原子替换。结构或范围无效时，原文件改名隔离并使用安全默认，不把损坏状态覆盖成看似成功。恢复几何会迁回最近工作区，不恢复旧物理屏幕的越界位置。

### 4.5 安装与卸载

NSIS 3.12 只作为构建工具。安装器固定 `RequestExecutionLevel user`，不请求管理员权限、不下载运行依赖、不默认启动、不默认自启。Qt 依赖由 `windeployqt 6.8.3` 应用本地部署并写入哈希清单。

构建脚本同时生成精确卸载清单：逐一删除本次部署的文件、正式安装标记和卸载器，再以非递归 `RMDir` 清理已经为空的部署子目录和安装目录。用户后来放入安装目录的未知文件不会被 `RMDir /r` 连带删除。

卸载器先执行实际安装目录中的 `DesktopUp.exe --request-exit`，等待主实例清理，再删除程序。若进程仍占用 `DesktopUp.exe`，卸载中止并保留重试入口。Run 值只有完全匹配本安装路径和精确参数时才删除；外来同名值保留。

## 5. 自动预检证据

2026-08-26 可选目录修订自动预检结果：

- run root：`prototype/renderer-adapters/ts-c01-qt-quick/out/r4d4-run-20260826-110019`；
- Build ID：`ts-c01-qt-6.8.3-r4d4-20260826-02`；
- 双 Clean Release：PASS；
- `DesktopUp.exe` SHA-256：`4C0FAF65F6EB04CA10B342C851BEB93822B329A4F50DC2A616AFE0B72E50C5EE`；
- Qt 部署文件：52；
- 旧 R2 海报回归：PASS；
- NSIS 页面：4，包含目录选择页；
- `DesktopUpSetup.exe`：17,144,583 bytes；
- 安装器 SHA-256：`7807804C836FEEBB088A8DFB3B90144A2D2D686396F80DABE13E9B86C638632D`；
- 含 `keep-user-file.txt` 的非空包探针目录安装/精确清单卸载：PASS；部署文件清除且哨兵文件保留；
- 包探针 Run 与卸载登记前后不变：PASS；
- 可选目录正式当前用户安装、首次普通启动、第二实例、应用重启、设备重启和正式卸载：NOT RUN。

### 5.1 自定义目录正式安装机器证据

2026-08-26 用户确认安装成功并运行，随后机器核对：

- 实际安装位置：`<user-selected-test-root>\DesktopUp`；
- 首实例 PID：`68808`，同路径进程数 1；
- 已安装 EXE SHA-256 与 Build ID `ts-c01-qt-6.8.3-r4d4-20260826-02` 自动产物一致；
- 相邻正式所有者标记内容正确；HKCU `InstallLocation`、`UninstallString`、`DisplayIcon` 均引用实际自定义目录；
- Run 值不存在，默认自启 OFF；本轮按用户要求不测试或修改开机自启；
- `candidate-state-v1.json` 已通过原子替换保存；进程响应正常；
- 普通第二实例退出码 0，运行后仍只有 PID `68808`；首实例仅新增一次 `r4c_control_center_role_probe_opened`，`topmost=false`；
- 日志中的 `LOCKED_OPERATION_REJECTED` 是锁定状态拒绝拖动的预期权限结果，不计为故障；
- 用户确认普通第二实例只打开一个控制中心，没有第二张海报或第二个托盘；关闭控制中心后海报正常，普通第二实例门 PASS；
- 用户确认普通退出后重新启动，位置、锁定和收起状态恢复；新首实例 PID `33620`，同路径进程数 1，日志记录 `r4d4_state_loaded=PASS`、启动意图 `ORDINARY`；应用重启门 PASS；
- 设备重启前基线：`<user-selected-test-root>\DesktopUp\DesktopUp.exe`，状态文件 schema `desktopup-ts-c01-r4d4-state-v1`，当前几何 `x=2000, y=54, width=538, expanded_height=1235`，`presentation=EXPANDED`、`locked=false`、`interaction=USING`、主题 `transparent-default`；Run 值不存在。

稳定自动摘要：`prototype/renderer-adapters/ts-c01-qt-quick/out/r4d4-package/r4d4-preflight.json`。

`windeployqt` 报告缺少 `dxcompiler.dll`/`dxil.dll` 和未设置 `VCINSTALLDIR` 的警告；现有 Direct3D 软件回退和本轮 52 文件部署仍完成，旧 R2 回归 PASS。该警告不改写为错误，但保留在构建日志中供 R5 收敛。

### 5.2 正式卸载与零残留机器证据

2026-08-26 用户要求跳过设备重启并直接进入正式卸载。卸载目标经 HKCU 登记、相邻所有者标记和实际目录三方核对为 `<user-selected-test-root>\DesktopUp`，卸载前唯一目标进程 PID `33620`，Run 值不存在。

正式静默卸载退出码 0，机器结果：

- PID `33620` 正常退出，目标路径 `DesktopUp.exe` 进程数 0；
- `DesktopUp.exe` 与 `uninstall.exe` 删除；
- HKCU 卸载登记、Run 值、开始菜单目录全部不存在；
- `%LOCALAPPDATA%\DesktopUp\Candidate\TS-C01` 删除；
- `%LOCALAPPDATA%\DesktopUp\DesktopUp TS-C01 Qt R1` 与 `R2` 保留；
- 卸载前写入的未知文件 `R4-D4-user-file-preservation-check.txt` 在卸载后独立保留，证明未递归误删所选目录；
- 记录该证据后，只删除由本轮创建的哨兵文件；确认目录无其他内容后移除空安装目录；最终 `<user-selected-test-root>\DesktopUp` 不存在；
- 进程、文件、注册表、开始菜单和候选数据机器残留扫描 PASS；用户随后确认通知区域托盘图标消失，D4-E 已执行范围完整通过。

设备重启：`SKIPPED BY USER`。开机自启：`DEFERRED BY USER`。二者均未执行，不能被解释为 PASS。

## 6. 正式人工顺序

1. 运行新版稳定 `DesktopUpSetup.exe`，确认无 UAC、出现目录选择页；选择一个已有其他文件但不含 DesktopUp 同名部署路径的当前用户可写目录，确认不再出现“必须为空目录”阻断并完成安装；
2. 从开始菜单启动一次，确认只有一张海报和一个托盘，控制中心不自动出现；
3. 再次普通启动，确认只打开一个控制中心角色探针，无重复海报/托盘；
4. 关闭角色探针，海报仍工作；在托盘显式开启自启，确认勾选状态和 Run 命令引用实际所选目录；
5. 改变海报位置、锁定/收起状态，从托盘退出并普通重启，确认状态恢复；
6. 保持自启开启并重启 Windows，确认自启只恢复海报+托盘、不打开/聚焦控制中心，状态恢复；
7. 在托盘关闭自启，确认只删除精确自有 Run 值；
8. 在安装目录加入一个不与部署文件重名的人工保留文件，然后运行 `uninstall.exe`，确认主实例退出并完成卸载；
9. 核对无 `DesktopUp.exe`、托盘、桌面 HWND、输入岛、Run、开始菜单和卸载登记；部署文件与候选子数据被删除，人工保留文件、V1 数据以及既有 `DesktopUp TS-C01 Qt R1/R2` 数据仍保留。

设备重启前后的两半证据必须属于同一次可选目录正式安装。不得用旧固定路径或包探针结果替代。

用户已明确暂缓所有开机自启相关测试，因此本次设备重启不启用、不修改也不评价 `--autostart`：Windows 返回后由用户手动运行已安装 `DesktopUp.exe`，只验证正式安装、状态文件和桌面宿主是否跨设备重启恢复。自启合同继续保持 `DEFERRED`，不得据此关闭 D4。

## 7. Fail-fast

出现 UAC、缺少目录选择页、无冲突的非空目录仍被整体拒绝、磁盘根目录未规范到 `DesktopUp` 子目录、覆盖同名用户路径、默认自启、重复海报/托盘、第二自启实例打开 UI、普通第二实例创建新宿主、状态回弹、Run 所有权越界、卸载递归删除未知文件或非候选数据、残留进程/托盘/HWND/输入岛或任意既有海报回归，D4 立即 FAIL。

## 8. 关闭条件

D4 的已执行范围只有在 D4-A、普通 D4-B、应用重启和 D4-E 完成后才能收敛；设备重启已由用户明确跳过，开机自启仍由用户延期，必须作为未执行例外永久保留，不能改写为 PASS。通知区域托盘消失经用户确认后，可将 D4 关闭为 `USER-ACCEPTED WITH AUTOSTART DEFERRED / DEVICE-RESTART SKIPPED`，随后进入 R5 证据收敛；这不自动创建候选赢家或 P0 PASS。

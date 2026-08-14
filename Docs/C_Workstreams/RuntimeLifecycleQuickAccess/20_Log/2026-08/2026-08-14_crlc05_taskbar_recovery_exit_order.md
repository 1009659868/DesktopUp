# C-RLC-05 TaskbarCreated 恢复与退出顺序

日期：2026-08-14

状态：`COMPLETE — USER RELEASE MATRIX PASS`

## 实施结论

- 新增 `taskbar-recovery-v1`，明确记录 initializing/ready/recovering/degraded/removed、generation、事件/合并/尝试计数、连续失败、最后触发、结果与错误；
- Windows 原生层监听 tray-icon 的 `tray_icon_app` 消息窗口和注册消息 `TaskbarCreated`，180 ms 去抖，最多 3 次恢复尝试，延迟为 0/250/750 ms；
- 同一恢复批次只验证或重建一个 ID 为 `desktop-up-tray` 的托盘；重建后重新挂接原生监听，并同步 runtime/autostart 菜单快照；
- `TaskbarCreated` 通过现有宿主恢复协调器提交 `taskbarCreated` 触发，不使用 TOPMOST/NOTOPMOST 脉冲；控制中心保持原 absent/hidden/visible 状态；
- 监听安装或连续恢复失败进入可诊断 degraded，停止无限自动重试，并保留“重新连接桌面宿主”手动恢复；
- 显式退出统一为：先将 runtime 置为 Exiting，再关闭宿主恢复队列、移除 TaskbarCreated 子类监听、刷新 placement、销毁控制中心与海报、移除托盘，最后结束进程；迟到恢复事件不能逆转退出。

## 自动与 Release 证据

- Node/文档门：`159/159`；Rust：`132/132`；production UI 构建通过；
- 新增 Rust 状态机覆盖：重复事件合并、成功恢复、有界失败与手动恢复、监听安装失败诊断、退出后迟到事件忽略；
- 新增 Node 契约覆盖：原生消息监听、去抖/上限、唯一托盘验证与回退重建、宿主恢复触发、退出清理顺序和禁止 TOPMOST；
- Release/NSIS 构建通过；`DesktopUp.exe` SHA-256：`5CC505193C5A138F157470D5CE201E51435BC7800FEE9A0C8ECCC70F6D1AFF83`；
- `DesktopUpSetup.exe` SHA-256：`484762DC72D594F837570DFD1855DFFFD8C11E0D797F2BB9E8F5EF7E65CE94D0`；安装内容与运行时依赖探针均为 ready、`gaps=[]`；
- 定向真实 Release 探针只向 DesktopUp 自身托盘消息窗口连续发送 5 次 `TaskbarCreated`：前后均为 1 个进程和 1 个 `tray_icon_app` 窗口，PID 与消息窗口句柄保持不变，未创建控制中心。

## 待用户 Release 人工矩阵

1. 控制中心关闭时，在任务管理器中“重新启动 Windows 资源管理器”；托盘图标只恢复一个，海报正常，控制中心不得自行打开；
2. 连续重复资源管理器重启三轮；始终只有一个 DesktopUp 进程、一个托盘入口，不出现重复控制中心；
3. 分别在海报显示/隐藏、暂停/恢复、锁定/解锁状态下重启资源管理器，托盘勾选与海报实际状态一致，不回弹；
4. 重启资源管理器后使用托盘打开/关闭控制中心、切换海报显隐与手动重连，确认入口与状态仍可用；
5. 回归顶部无白条/裁切、空白穿透、卡片、拖动、锁定、收起/展开，海报不得短暂覆盖普通程序；
6. 在资源管理器重启恢复窗口附近执行托盘“退出 Desktop-Up”，确认进程完整退出，托盘无残影，迟到事件不重新创建海报或控制中心。

## 用户人工验收

2026-08-14，用户明确确认六项人工矩阵全部通过：真实 Explorer 重启、连续三轮恢复、跨显隐/暂停/锁定状态一致性、恢复后托盘与控制中心入口、海报核心层级回归，以及恢复窗口附近的显式退出均无异常。

结论：C-RLC-05 标记 `COMPLETE`，C-RLC-06 已执行最终封板。

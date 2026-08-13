# Autostart and Cleanup Contract V1

状态：`FROZEN — C-IBC-04`

稳定标识：`autostart-contract-v1`

## 1. 注册所有权

- 范围：当前用户；
- 机制：`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`；
- 值名：`DesktopUp`；
- 值类型：`REG_SZ`；
- 默认状态：关闭，值不存在；
- 只有用户显式 opt-in 后才允许 C-RLC 注册；
- 应用不得创建计划任务、服务、Startup 文件夹副本或 HKLM 启动项作为替代。

## 2. 正式路径门

固定命令为 `"%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe" --autostart`。

注册前必须同时满足：

- 当前进程就是目标安装根中的 `DesktopUp.exe`；
- 目标是普通文件，不是目录、symlink 或其他类型；
- 值类型为 `REG_SZ`；
- 命令与冻结值完全一致，不允许开发路径、便携路径、额外参数或 `REG_EXPAND_SZ`。

## 3. 单实例启动语义

- 首实例携带 `--autostart`：只启动海报，不打开控制中心；
- 已有实例收到第二个 `--autostart`：保持现有实例和 UI 状态，不显示控制中心；
- 自启入口不接受额外参数；
- 当前运行时状态为 `contractOnlyCrlcPending`，真实参数分流和 UI 开关仍归 C-RLC。

## 4. 清理接口

- owner：C-URV；
- 只删除上述 HKCU Run 中名为 `DesktopUp` 的应用自有值，不删除父键或其他应用值；
- 值不存在视为成功，清理必须幂等；
- 正常卸载、恢复失败和部分恢复失败后都必须执行；
- 不要求管理员权限；
- 清理失败必须返回带重试证据的部分失败，不能伪装成功。

## 5. 当前实现边界

- `autostart-diagnostics-v1` 只读查询 HKCU Run 并报告 absent/matching/drifted/unreadable；
- 托盘和控制中心自启项继续禁用；
- 本阶段不写注册表、不改变自启、不启动额外进程、不执行卸载；
- 实际 enable/disable 归 C-RLC，恢复型删除与验证归 C-URV。

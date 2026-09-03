# V3-01 验证记录

状态：`COMPLETE — AUTOMATED 10/10 + USER MANUAL PASS`

日期：2026-09-03

分支：`impl/poster-redesign-V3`

## 环境

- .NET SDK：10.0.103；
- Visual Studio：18 Community；
- Windows SDK include：10.0.22621.0、10.0.26100.0；
- target：Release / x64 / `net10.0-windows10.0.22621.0`；
- Windows 最低版本声明：10.0.19044.0；
- 权限清单：`asInvoker`、`uiAccess=false`。

## 自动证据

### 构建

```powershell
dotnet build "G:\A_\Tools\DesktopUp_V2\DesktopUp.sln" -c Release -p:Platform=x64 --no-restore
```

结果：PASS，0 warning，0 error。

### 合同测试

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\test.ps1" -Configuration Release
```

结果：PASS，10/10：

- 首次普通启动意图为后台常驻；
- 第二次普通启动转为打开控制中心；
- 显式控制中心意图保持稳定；
- 两个真实 coordinator 之间只转发一个意图；
- 默认数据根不位于程序目录；
- 所有 lease 释放后 ledger 为 clean；
- Domain/Application 两项架构隔离测试；
- manifest 普通权限合同测试。
- 托盘必须使用稳定 `PopupMenu + RightClick` 激活合同，禁止 `MenuActivation=None`。

### 真实进程退出 smoke

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\smoke-exit.ps1"
```

结果：PASS。临时 smoke 进程创建托盘后走普通退出；最终：

```json
{
  "clean": true,
  "resources": []
}
```

日志事件顺序：`tray.created → application.started → activation.received → application.exit_requested → tray.disposed → application.stopped`，停止事件记录 `activeResources=0`。

### 权限证据边界

从最终 `DesktopUp.exe` 提取的嵌入 manifest 已确认：

- `requestedExecutionLevel=asInvoker`；
- `uiAccess=false`；
- `dpiAwareness=PerMonitorV2`；
- `activeCodePage=UTF-8`。

当前测试机的 Codex 与 Explorer Shell 本身均处于管理员 token，故它们启动的子进程日志均为 `isElevated=true`。这不表示 DesktopUp 请求提权，但也不能替代“medium-integrity 标准用户 token”实测；该环境限制保留为人工/后续干净账户门。

## 人工门

先确保没有其他 DesktopUp V3 实例，然后运行：

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\run.ps1"
```

请验证：

1. 首次启动不弹控制中心，只出现一个 `DesktopUp V3` 托盘图标；
2. 双击托盘只出现一个控制中心；连续双击不产生第二个窗口；
3. 关闭控制中心后托盘仍在；再次双击能重新创建控制中心；
4. 托盘右键选择“退出 DesktopUp”后，控制中心、托盘和进程全部消失。
5. 启动时不出现 UAC 提权提示；若测试账户提供标准用户 token，日志中的 `application.started.data.isElevated` 应为 `false`。

本门不验证海报，因为 V3-01 明确没有海报窗口。人工通过后方可进入 V3-02。

## 2026-09-03 首轮人工结果与修订

- 项 1–4：用户确认 PASS；
- 项 5：FAIL，右键无反应；
- 根因：初版组合为 `ContextMenuMode.SecondWindow + MenuActivation=None`，但没有移植 DeskBox 配套的手动 `RightClickCommand`/菜单定位实现，等价于禁用了右键触发；
- 修订：改为 H.NotifyIcon 的稳定原生 `ContextMenuMode.PopupMenu + PopupActivationMode.RightClick`，并把菜单项从 `Click` 事件改为 `ICommand`，确保 PopupMenu 代理可以调用“打开”和“退出”；
- 回归：Release 构建、10 项合同测试、正常退出 smoke 必须重新通过；人工只需重点复测右键菜单及退出清理，同时确认项 1–4 未回退。

## 2026-09-03 关闭结论

- 修订版 Release：0 warning / 0 error；
- 合同测试：10/10 PASS；
- 真实退出 smoke：PASS；
- 用户确认托盘右键菜单和退出恢复正常，前四项无回退；
- V3-01 正式关闭，后续问题归入 V3-02 及对应阶段。

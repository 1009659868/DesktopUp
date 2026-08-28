# QTS-04-02 用户桌面视觉门 V1

状态：`CLOSED — USER ACCEPTED 2026-08-27`

## 启动命令

在 PowerShell 执行：

~~~powershell
$repoRoot = (git rev-parse --show-toplevel).Trim()
$qtRoot = $env:QT_ROOT
if ([string]::IsNullOrWhiteSpace($qtRoot)) { throw 'Set QT_ROOT to the Qt 6.8.3 msvc2022_64 directory.' }
$env:PATH = "$(Join-Path $qtRoot 'bin');$env:PATH"
$testDir = Join-Path $repoRoot 'out\manual\qts04-02'
New-Item -ItemType Directory -Force -Path $testDir | Out-Null
& (Join-Path $repoRoot 'out\build\windows-x64-release\bin\Release\desktopup-runtime.exe') `
  --state-db (Join-Path $testDir 'state.sqlite') `
  --pipe-name "DesktopUp.QTS04.02.Manual" `
  --instance-name "DesktopUp.QTS04.02.Manual" `
  --ledger (Join-Path $testDir 'ledger.json') `
  --exit-after-ms 15000
~~~

运行约 15 秒后自动退出。命令使用独立 StateStore、pipe、instance 和 ledger，不接管日常 Runtime 状态。

## 本轮只确认五项

1. 桌面中央显示 NIGHT OPS，六张卡片与 footer 完整；
2. 顶部、底部没有白条或灰条；
3. 圆角外没有暗灰/黑色矩形包络；
4. 打开普通窗口时，普通窗口高于海报；
5. 15 秒结束后海报完全消失，无残留。

## 本轮不要验证

- 海报空白框选、右键与原生图标；
- 卡片、锁定、收起与标题栏命中；
- 拖动、缩放、Win+D、Explorer 重启；
- 托盘、控制中心、Item、主题和 AI。

这些尚未实现或属于 QTS-04-03～05，不能作为 QTS-04-02 的失败项。

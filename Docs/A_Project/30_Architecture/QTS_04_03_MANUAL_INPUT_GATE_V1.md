# QTS-04-03 用户单海报输入门 V1（R2 修订）

状态：`R2 READY — INITIAL RUN FAILED / RETEST NOT YET USER-CONFIRMED`

首次人工门已确认“标题无法拖动”和“右下角缩过顶部后消失”，因此初版不通过；本门只验证 R2 修订版。

## 1. 启动命令

在 PowerShell 执行：

~~~powershell
$repoRoot = (git rev-parse --show-toplevel).Trim()
$qtRoot = $env:QT_ROOT
if ([string]::IsNullOrWhiteSpace($qtRoot)) { throw 'Set QT_ROOT to the Qt 6.8.3 msvc2022_64 directory.' }
$env:PATH = "$(Join-Path $qtRoot 'bin');$env:PATH"
$testDir = Join-Path $repoRoot 'out\manual\qts04-03'
New-Item -ItemType Directory -Force -Path $testDir | Out-Null

& (Join-Path $repoRoot 'out\build\windows-x64-release\bin\Release\desktopup-runtime.exe') `
  --editing `
  --state-db "$testDir\state.sqlite" `
  --pipe-name "DesktopUp.QTS04.03.Manual" `
  --instance-name "DesktopUp.QTS04.03.Manual" `
  --ledger "$testDir\ledger.json" `
  --exit-after-ms 120000
~~~

该实例使用独立数据库、pipe、instance 和 ledger，运行约 120 秒后自动退出。`--editing` 只为本门启用右边、底边和右下角 resize islands。

## 2. 人工验证项

1. 从六张卡片下方的海报空白左键拖动：出现 Windows 桌面框选，不能选中海报文字；
2. 在同一空白处右键：出现 Windows 桌面菜单，不出现应用或网页菜单；
3. 把一个原生桌面快捷方式放在海报空白覆盖范围内：单击可选中、双击可启动；
4. 依次点击六张卡片：对应卡片出现青色选中边框，其他海报区域不误触发；
5. 未锁定时连续三次拖动顶部 `DESKTOP COLLECTION` 标题区：visual、按钮和命中区域必须同步跟随；移动小于约 5px 时不应跳动，每次释放后都能再次拖动；
6. 从最右约 8px、最下约 8px、右下角分别缩放；再把右下角向左上拖过海报顶部：尺寸必须停在 320×420，海报不能报错或消失；释放后必须能立即再次缩放；
7. 点击锁定按钮后重复标题拖动和边缘缩放：均不生效；卡片、锁定和收起按钮仍可使用；
8. 再次解锁，点击收起：顶部位置和按钮屏幕位置保持不动，只缩短高度；收起后原卡片位置不得继续响应；
9. 点击展开：恢复上次展开高度，顶部与按钮位置仍保持不动；
10. 全程确认无白条/灰框，普通窗口始终高于海报；120 秒结束后海报消失，`ledger.json` 中 `ownedResourceCount` 为 0。

## 3. 本轮失败条件

以下任一出现即不关闭 QTS-04-03：

- 空白被 DesktopUp 截获，无法桌面框选、右键或操作快捷方式；
- 一个矩形 input window 覆盖整张海报；
- 卡片、按钮、标题或尺寸边命中错位；
- lock 未阻止 geometry，或错误禁用了卡片/按钮；
- 收起/展开移动顶部锚点或留下隐藏卡片命中；
- 任一次拖动/缩放后位置不变、不能再次操作、画面不刷新、海报消失、出现条带或越级到普通窗口上方；
- 退出后仍有 visual/input HWND 或资源账本非零。

## 4. 本轮不要验证

Win+D、桌面刷新、文件选择器、Explorer 重启、分辨率/DPI/工作区变化和热插拔属于 QTS-04-04；不要用这些结果判定 QTS-04-03。

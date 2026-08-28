# QTS-04-04 用户生命周期矩阵 V1

状态：`USER MATRIX 1～6 PASS / R4.2 AUTO PASS / MANUAL REFLOW FLASH GATE PENDING`

日期：2026-08-28

## 1. 启动最新版 Release

请先退出其他 DesktopUp V2 测试进程，然后在 PowerShell 执行：

```powershell
$repoRoot = (git rev-parse --show-toplevel).Trim()
$qtRoot = $env:QT_ROOT
if ([string]::IsNullOrWhiteSpace($qtRoot)) { throw 'Set QT_ROOT to the Qt 6.8.3 msvc2022_64 directory.' }
$env:PATH = "$(Join-Path $qtRoot 'bin');$env:PATH"
$runRoot = Join-Path $repoRoot 'out\manual\qts04-04'
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

& (Join-Path $repoRoot 'out\build\windows-x64-release\bin\Release\desktopup-runtime.exe') `
  --state-db "$runRoot\state.sqlite" `
  --pipe-name "DesktopUp.QTS04.04.Manual" `
  --instance-name "DesktopUp.QTS04.04.Manual" `
  --ledger "$runRoot\ledger.json" `
  --editing `
  --exit-after-ms 240000 2>&1 |
  Tee-Object -FilePath "$runRoot\runtime.log"
```

当前海报尺寸按逻辑 DIP 投影：若主屏为 125%，默认 420×620 DIP 会显示为约 525×775 物理像素。这是本轮显示模型的预期变化。

## 2. 人工顺序

1. 基线：确认 NIGHT OPS 可见，无白条、灰框、裁切或暗边；普通窗口高于海报；
2. Win+D：完成 3 个“显示桌面 → 恢复窗口”循环；
3. 桌面菜单：桌面空白右键，再任意左键收起菜单；随后右键点击“刷新”；
4. 文件选择器：从任意普通程序打开一次 Windows 文件选择器，再关闭；
5. Explorer：任务管理器中只重启一次“Windows 资源管理器”，等待唯一海报恢复；
6. 显示变化：在本轮四分钟内选择以下一种并恢复：
   - 修改一次主屏分辨率；或
   - 改变一次任务栏工作区（例如临时打开/关闭自动隐藏）；
7. 回归：标题连续拖动、右下缩放、锁定、收起/展开各一次；确认空白框选、右键和原生图标仍正常；
8. 等待进程自动退出，确认海报、输入 islands 和托盘/窗口残留均不存在。

## 3. 通过标准

- Win+D 不出现隐藏再恢复闪烁；
- 右键、刷新、文件选择器不触发瞬时置顶；
- Explorer 重启后只恢复一张海报，无普通顶层 fallback；
- 显示变化只重投影，海报不换 Shell generation，不出现旧尺寸帧；
- 所有场景后输入、锁定、收起/展开仍正常；
- `runtime.log` 无 `*_FAILED`；
- Explorer 重启应出现一次 `QTS04_RUNTIME_SHELL_RECOVERED`；
- 显示变化应出现 `QTS04_RUNTIME_DISPLAY_REPROJECTED`；
- 最终 `QTS04_RUNTIME_LIFECYCLE_FINAL` 中计数与实际操作一致。

若本轮不执行显示变化，请明确标记 L7 为 `NOT RUN`，不能用自动投影测试代替。

## 4. 执行记录

- 2026-08-28：用户确认上述 1～6 项全部通过；
- 同轮发现标题拖动过程中有规律的间歇性闪烁，登记为 R1 修订门，不将 QTS-04-04 提前关闭。

## 5. R1 唯一复测

1. 使用第 1 节相同命令启动最新 Release；
2. 按住标题区连续拖动约 20 秒，覆盖慢速、快速、水平、垂直和斜向移动；
3. 预期海报连续移动，无周期性消失/闪现，鼠标捕获不中断，释放后仍可再次拖动；
4. 退出日志中的 `geometryRelocations` 应大于 0，且无 `QTS04_RUNTIME_GEOMETRY_PREVIEW_FAILED`。

若未观察到其他回退，不要求重复已通过的 Win+D、Explorer、显示变化等 1～6 项。

## 6. R2 外框/内容分离复测

使用第 1 节相同命令启动最新 Release：

1. 默认尺寸确认六张卡片、标题和页脚正常；
2. 拉成宽扁形：标题、卡片和图标不能随宽度整体放大；空间足够时固定尺寸卡片增加列数；
3. 拉成窄高形：固定尺寸卡片减少列数并增加行数；
4. 拉到最小尺寸：只允许显示完整 Item，不得出现半张卡片、Item/页脚重叠；不可见 Item 区域应继续允许桌面框选/右键；
5. 改变展开宽度后收起/展开：顶边、按钮位置和 60 DIP 收起高度稳定；
6. 连续拖动 20 秒，同时完成 R1 闪烁复测。

本门不要求重做已经通过的 Win+D、Explorer、桌面刷新、文件选择器和显示变化矩阵。
## 7. R3 V1 连续缩放与 Item 溢出复测

使用最新版 R3 Release：

1. 按住右边缘，左右来回跨越 3 列/2 列边界至少 10 次；过程中不能松开左键，resize 必须一直跟随；
2. 缩成 2 列且高度不足以同时容纳三行时，确认出现细滚动条；将鼠标放在任意可见卡片上向下滚动，必须能看到第 5、6 个 Item，再向上恢复第 1、2 个；
3. 任一滚动位置只能出现完整卡片，不得出现半卡、Item/页脚重叠或不可见 Item 点击；
4. 卡片外的海报空白仍应支持 Windows 桌面框选和右键菜单；
5. 松开后重新按住仍可缩放；标题拖动、锁定、收起/展开无回退。

已通过的 Win+D、Explorer、桌面刷新、文件选择器、显示变化和 R1 长时间标题拖动无需重复。
## 8. R4 连续 Item viewport 复测

使用最新版 R4 Release：

1. 缩成两列并逐步降低高度，观察 viewport 临界处：卡片应逐像素进入/退出，不得整排突然消失或留下异常大空位；
2. 鼠标位于任意可见卡片时滚动，应连续访问第 1～6 个 Item；
3. 鼠标位于右侧细滚动条任意位置时滚动，即使没有完整卡片，也应能恢复内容；
4. Item 可以仅在 viewport 上/下边缘部分可见，但不得越过标题区或页脚，viewport 外不可点击；
5. 除右侧 12 DIP 滚动条命中带外，卡片之间和下方空白继续支持 Windows 桌面框选和右键；
6. 按住右边缘跨 3 列/2 列边界仍不得丢捕获；标题拖动、锁定和收起/展开无回退。

R3 第 3 项“只能出现完整卡片”已被本节明确撤销；它是造成整排跳失的错误验收条件。
## 9. R4.1 最小复测

1. 将末行滚到 viewport 底边：彩色斜带、卡片纹理和文字均不得穿过内容区进入页脚或海报外；
2. 行列重排闪烁仍为 OPEN，本节不把它登记为通过；
3. renderer adapter/Host transaction 路线确定后再给出新的闪烁关闭门。

## 10. R4.2 Qt Quick + Host staged-present 最小复测

使用 `QTS_04_04_R4_2_QTQUICK_ADAPTER_AND_HOST_TRANSACTION_2026-08-28.md` 第 6 节命令启动最新版 Release：

1. 按住右边缘连续跨越 2↔3↔4↔6 列阈值，resize capture 全程不得中断；
2. 每次列数/行数变化时，不得出现整张海报消失、露壁纸、旧帧或周期性闪烁；
3. 两列窄视口连续滚动，六张卡片都可到达，滚动必须逐像素而非整排跳变；
4. 无白条、灰框、accent 泄漏和 Item/页脚遮挡；
5. 空白穿透、卡片、锁定、收起/展开及普通窗口高于海报无回退；
6. 退出无 visual/island/进程残留。

本节通过前，R4.2 只能登记为自动证据通过，不能登记为用户视觉通过或关闭 QTS-04-04。

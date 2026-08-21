# TS-H01-D1 Manual Gate Reproduction

从仓库根目录执行。键盘与任务栏入口必须使用两个独立进程和两个独立日志。

## 键盘 Win+D

```powershell
$exe = Join-Path (git rev-parse --show-toplevel) 'prototype/desktop-host-win32/ts-h01-d1/out/build/x64-release/Release/desktop-host-lifecycle-probe.exe'
$log = Join-Path (git rev-parse --show-toplevel) 'prototype/desktop-host-win32/ts-h01-d1/out/d1-win-d-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','win-d','--duration-ms','150000','--log',$log `
  -WindowStyle Hidden -Wait
```

保持一个普通程序可见，完成 10 个 `Win+D` 显示桌面、再次 `Win+D` 恢复普通程序的完整循环。

## 任务栏显示桌面

```powershell
$exe = Join-Path (git rev-parse --show-toplevel) 'prototype/desktop-host-win32/ts-h01-d1/out/build/x64-release/Release/desktop-host-lifecycle-probe.exe'
$log = Join-Path (git rev-parse --show-toplevel) 'prototype/desktop-host-win32/ts-h01-d1/out/d1-taskbar-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','taskbar-show-desktop','--duration-ms','150000','--log',$log `
  -WindowStyle Hidden -Wait
```

只使用任务栏最右侧“显示桌面”区域完成 10 个显示/恢复完整循环。

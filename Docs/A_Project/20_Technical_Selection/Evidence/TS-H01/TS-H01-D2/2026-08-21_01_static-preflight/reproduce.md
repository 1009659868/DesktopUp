# TS-H01-D2 Reproduction

## Build

```powershell
$root = git rev-parse --show-toplevel
Set-Location (Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2')
cmake --preset x64-release
cmake --build --preset x64-release
```

## Static preflight

```powershell
$root = git rev-parse --show-toplevel
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/build/x64-release/Release/desktop-host-recovery-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/d2-static-preflight.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-recovery-test','--scenario','explorer-restart','--expected-recoveries','0','--duration-ms','5000','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

## Manual 10-recovery gate

```powershell
$root = git rev-parse --show-toplevel
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/build/x64-release/Release/desktop-host-recovery-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/d2-explorer-restart-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-recovery-test','--scenario','explorer-restart','--expected-recoveries','10','--duration-ms','600000','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

在任务管理器中对“Windows 资源管理器”执行恰好 10 次“重新启动”。每次等待桌面和唯一琥珀色诊断卡恢复后再开始下一轮。第 10 次恢复并稳定 1.5 秒后探针应自行退出。

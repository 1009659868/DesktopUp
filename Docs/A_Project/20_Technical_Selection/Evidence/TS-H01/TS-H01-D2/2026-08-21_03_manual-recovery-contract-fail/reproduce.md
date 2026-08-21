# TS-H01-D2 Failed Attempt Reproduction

```powershell
$root = git rev-parse --show-toplevel
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/build/x64-release/Release/desktop-host-recovery-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/d2-explorer-restart-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-recovery-test','--scenario','explorer-restart','--expected-recoveries','10','--duration-ms','600000','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

本次运行只在任务管理器中完成了一次 Windows Explorer 重启，故在 600 秒截止时按合同失败。重新验证必须使用新日志名，不覆盖本 run 对应的本地原始日志。

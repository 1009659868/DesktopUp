# TS-H01-D3 Initial Static Reproduction

```powershell
$root = git rev-parse --show-toplevel
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/out/build/x64-release/Release/desktop-host-boundary-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/out/d3-static-preflight.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-boundary-test','--scenario','static','--expected-dpi','96','--duration-ms','5000','--expected-drags','0','--expected-resizes','0','--expected-collapse-cycles','0','--expected-display-changes','0','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

该命令描述失败 run 的输入。当前源码已修正，重新构建不会复现旧二进制的初建顺序，旧日志由其哈希标识。

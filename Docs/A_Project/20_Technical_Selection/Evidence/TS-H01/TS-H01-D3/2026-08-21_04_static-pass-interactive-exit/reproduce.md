# Current D3 Static Regression Reproduction

```powershell
$root = '.'
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/out/build/x64-release/Release/desktop-host-boundary-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/out/d3-static-after-interactive-exit-v2.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-boundary-test','--scenario','static','--expected-dpi','96','--duration-ms','5000','--expected-drags','0','--expected-resizes','0','--expected-collapse-cycles','0','--expected-display-changes','0','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

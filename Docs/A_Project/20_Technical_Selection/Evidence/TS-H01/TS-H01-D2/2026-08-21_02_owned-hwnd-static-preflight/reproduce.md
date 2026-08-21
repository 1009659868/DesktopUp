# TS-H01-D2 Final Static Reproduction

```powershell
$root = git rev-parse --show-toplevel
Set-Location (Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2')
cmake --preset x64-release
cmake --build --preset x64-release

$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/build/x64-release/Release/desktop-host-recovery-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/d2-static-preflight-owned-hwnd.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-recovery-test','--scenario','explorer-restart','--expected-recoveries','0','--duration-ms','5000','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

用于人工 10 轮门时，将 `--expected-recoveries` 改为 `10`、`--duration-ms` 改为 `600000`，并使用新的 `d2-explorer-restart-manual.jsonl` 日志。

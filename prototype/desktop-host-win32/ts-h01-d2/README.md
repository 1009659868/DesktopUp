# TS-H01-D2 Explorer generation recovery probe

This bounded x64 Win32 Release probe verifies the frozen recovery state machine:

```text
ATTACHED_SAFE
-> SHELL_GENERATION_LOST
-> DEGRADED_NO_DESKTOP_HOST
-> SAFE_TARGET_REDISCOVERED
-> ATTACHED_SAFE
```

It reuses the D1 implementation-neutral layered visual and split interaction
fixture, but adds only Explorer-generation loss cleanup and safe rediscovery.
It never creates a normal top-level fallback, never uses TopMost, never starts
or kills Explorer, and never repairs z-order by polling `SetWindowPos`.

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

## Manual 10-generation gate

```powershell
$root = git rev-parse --show-toplevel
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/build/x64-release/Release/desktop-host-recovery-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d2/out/d2-explorer-restart-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-recovery-test','--scenario','explorer-restart','--expected-recoveries','10','--duration-ms','600000','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

While the probe runs, restart **Windows Explorer** from Task Manager exactly 10
times. Wait for the desktop and the single amber diagnostic card to return
before starting the next cycle. Do not terminate `explorer.exe` with the probe
and do not run more than 10 cycles in the evidence run.

For every cycle confirm:

- the old card is removed with the old shell generation;
- there is no ordinary top-level or TopMost fallback while Explorer is absent;
- exactly one card returns after one unambiguous safe target is stable;
- a restored ordinary application covers the card immediately;
- after the final recovery, desktop blank input and the three interaction
  islands still follow the TS-H01-C contract;
- the probe exits by itself after the tenth recovery plus a 1.5 second stable
  observation window, and no self-owned surface remains.

Exit code `0` proves the telemetry contract and exactly 10 unique recoveries.
The no-flash/no-raise observation and post-recovery native input checks remain
mandatory manual evidence.

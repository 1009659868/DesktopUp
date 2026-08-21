# TS-H01-D3 display and boundary probe

Status: `D3-3 PASS / D3 USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS / HANDOFF TO TS-H01-E`

This x64 Win32 Release probe extends the accepted TS-H01-B/C/D1 host fixture
with an implementation-neutral logical geometry model. It measures the same
surface through expected projection, `GetWindowRect`, client-to-screen bounds,
`DWMWA_EXTENDED_FRAME_BOUNDS`, and the actual paint target.

It does not change Windows display settings, create a normal top-level fallback,
use TopMost, activate itself, inject Explorer, or poll `SetWindowPos` to repair
z-order. The only z-order placement occurs during initial safe attachment;
geometry updates use `SWP_NOZORDER`.

The current D2 automatic run ended 1/10 and remains a recorded telemetry
exception. The user explicitly accepted D2 and authorized formal D3-1 execution;
the exception is retained rather than relabeled as an automatic 10/10 pass.

## Build

```powershell
$root = git rev-parse --show-toplevel
Set-Location (Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3')
cmake --preset x64-release
cmake --build --preset x64-release
```

## D3-0 static preflight at the current 100% scale

```powershell
$root = git rev-parse --show-toplevel
$exe = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/out/build/x64-release/Release/desktop-host-boundary-probe.exe'
$log = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/out/d3-static-preflight-local.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-boundary-test','--scenario','static','--expected-dpi','96','--duration-ms','5000','--expected-drags','0','--expected-resizes','0','--expected-collapse-cycles','0','--expected-display-changes','0','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

Use expected DPI 96/120/144/192 for 100/125/150/200% respectively. A mismatch
fails closed; never relabel a 96-DPI run as evidence for another scale.

## D3-1 interactive run

Set Windows scaling manually, then run exactly one matching command. The runner
creates five independent processes and logs, so five successful iterations are
also five cold starts:

```powershell
$root = '.'
$runner = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/run-d3-dpi-gate.ps1'

& $runner -ExpectedDpi 96   # Windows 100%
& $runner -ExpectedDpi 120  # Windows 125%
& $runner -ExpectedDpi 144  # Windows 150%
& $runner -ExpectedDpi 192  # Windows 200%
```

Do not execute all four commands without changing Windows scaling between them.
A mismatched scale fails closed before creating evidence for that band.

During every cold start:

1. drag the title by more than 5 DIP and release exactly once;
2. drag the green bottom-right resize island exactly once;
3. click the amber collapse island to collapse and expand exactly once;
4. optionally click the magenta preset island to inspect width presets;
5. on the fifth run, also put an ordinary program over the card and check native
   desktop blank input outside the islands.

After exact gesture counts are reached, the probe observes another 1.5 seconds
and exits automatically. The runner advances to the next cold start only after
exit code 0. Visual white strips, gray frames, crop, one-frame mismatch, or an
incorrect ordinary-program level remain manual FAIL conditions even when the
telemetry summary passes.

## D3-2 resolution-change run

```powershell
$root = '.'
$runner = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/run-d3-resolution-gate.ps1'
& $runner -ExpectedDpi 96
```

At 100% scaling, change from native resolution to one Windows-supported lower
resolution and restore native resolution for five complete cycles. The probe
expects at least ten display-geometry changes and exits after the contract is
met and the final geometry remains stable for 1.5 seconds. Do not combine this
run with D3-3 work-area/taskbar changes.

## D3-3 work-area/taskbar run

```powershell
$root = '.'
$runner = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/run-d3-work-area-gate.ps1'
& $runner -ExpectedDpi 96
```

Keep 100% scaling and the native resolution. In Windows taskbar settings, turn
"Automatically hide the taskbar" on and back off for five complete cycles.
Wait for the diagnostic card to stabilize after each change. Do not drag,
resize, collapse, or otherwise interact with the card during this run. The
probe expects at least ten work-area geometry changes and exits after the
contract is met and stable for 1.5 seconds. Visual taskbar overlap, stale
geometry, white strips, gray frames, crop, one-frame mismatch, or incorrect
z-order remain manual failures.

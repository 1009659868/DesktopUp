# Reproduction

```powershell
$root = '.'
$runner = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/run-d3-work-area-gate.ps1'
& $runner -ExpectedDpi 96
```

Preconditions: Windows 11 x64, primary display 2560 × 1440, scaling 100% / 96 DPI, native resolution.

1. Open Windows Settings > Personalization > Taskbar > Taskbar behaviors.
2. Enable automatically hide the taskbar and wait for the diagnostic card to stabilize.
3. Disable automatically hide the taskbar and wait again.
4. Repeat for at least five complete cycles.
5. Do not drag, resize, collapse, or click the diagnostic card.
6. Confirm no white strip, gray frame, crop, stale frame, taskbar overlap, topmost elevation, or native desktop input regression.

The probe exits after at least ten detected work-area changes remain contract-valid for 1.5 seconds.

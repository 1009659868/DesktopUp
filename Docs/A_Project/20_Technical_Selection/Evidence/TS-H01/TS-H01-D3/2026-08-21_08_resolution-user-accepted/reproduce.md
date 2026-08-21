# Reproduction

Baseline: Windows 11 x64, primary display 2560 x 1440, work area 2560 x 1392, 100% / 96 DPI.

```powershell
$root = '.'
$runner = Join-Path $root 'prototype/desktop-host-win32/ts-h01-d3/run-d3-resolution-gate.ps1'
& $runner -ExpectedDpi 96
```

Intended operation:

1. Keep scaling at 100%.
2. Change native resolution to one Windows-supported lower resolution and retain the setting.
3. Wait for the diagnostic card to stabilize.
4. Restore native resolution and retain the setting.
5. Repeat for five complete cycles without interacting with the card.

The archived run contains only eight detected geometry changes and extra card interactions; reproduce it as evidence of the exception, not as an automatic PASS reference.

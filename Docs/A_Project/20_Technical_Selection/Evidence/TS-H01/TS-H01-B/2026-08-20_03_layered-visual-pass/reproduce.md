# Reproduce TS-H01-B-20260820-03

## Automated structure run

```powershell
& '.\prototype\desktop-host-win32\ts-h01-b\out\build\x64-release\Release\desktop-host-layer-probe.exe' `
  --run-host-test `
  --duration-ms 4000 `
  --log '.\Docs\A_Project\20_Technical_Selection\Evidence\TS-H01\TS-H01-B\2026-08-20_03_layered-visual-pass\host-samples.jsonl'
```

## Manual visual run

Move or minimize ordinary windows away from the primary-screen center without
using Win+D, then launch the probe hidden so its console cannot cover the card:

```powershell
Start-Process `
  -FilePath '.\prototype\desktop-host-win32\ts-h01-b\out\build\x64-release\Release\desktop-host-layer-probe.exe' `
  -ArgumentList @('--run-host-test', '--duration-ms', '15000') `
  -WindowStyle Hidden
```

Verify:

1. the cyan card is visible at the primary-screen center;
2. an ordinary application moved over it completely covers the card;
3. moving the ordinary application away reveals the still-running card again;
4. the card disappears cleanly after 15 seconds.

Do not test Win+D or blank click-through in this run.

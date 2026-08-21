# TS-H01-D1 reproduction

Run every command block from the repository root.

## Build

```powershell
Push-Location '.\prototype\desktop-host-win32\ts-h01-d1'
cmake --preset x64-release
cmake --build --preset x64-release
Pop-Location
```

## Static preflight

```powershell
$exe='.\prototype\desktop-host-win32\ts-h01-d1\out\build\x64-release\Release\desktop-host-lifecycle-probe.exe'
$log='.\prototype\desktop-host-win32\ts-h01-d1\out\d1-smoke-final.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','win-d','--duration-ms','3000','--log',$log `
  -WindowStyle Hidden -PassThru -Wait
```

## Keyboard Win+D manual gate

```powershell
$exe='.\prototype\desktop-host-win32\ts-h01-d1\out\build\x64-release\Release\desktop-host-lifecycle-probe.exe'
$log='.\prototype\desktop-host-win32\ts-h01-d1\out\d1-win-d-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','win-d','--duration-ms','150000','--log',$log `
  -WindowStyle Hidden -Wait
```

Starting with an ordinary application visible, perform 10 complete cycles.
Each cycle is two key presses: press `Win+D` to expose the desktop, then press
`Win+D` again to restore applications.

## Taskbar show-desktop manual gate

Use a new process and log:

```powershell
$exe='.\prototype\desktop-host-win32\ts-h01-d1\out\build\x64-release\Release\desktop-host-lifecycle-probe.exe'
$log='.\prototype\desktop-host-win32\ts-h01-d1\out\d1-taskbar-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','taskbar-show-desktop','--duration-ms','150000','--log',$log `
  -WindowStyle Hidden -Wait
```

Perform 10 complete expose/restore cycles using only the far-right taskbar
show-desktop control.

For both scenarios confirm: no card flicker/disappearance, no frame above the
restored ordinary application, ordinary application covers the card again,
and all surfaces disappear after timeout.

# TS-H01-C split-input probe reproduction

Run every command block from the repository root.

## Build

```powershell
Push-Location '.\prototype\desktop-host-win32\ts-h01-c'
cmake --preset x64-release
cmake --build --preset x64-release
Pop-Location
```

## Automated preflight

```powershell
& '.\prototype\desktop-host-win32\ts-h01-c\out\build\x64-release\Release\desktop-host-input-probe.exe' `
  --run-input-test `
  --duration-ms 5000 `
  --log '.\Docs\A_Project\20_Technical_Selection\Evidence\TS-H01\TS-H01-C\2026-08-20_01_split-input-preflight\host-input-samples.jsonl'
```

This preflight only checks the verified desktop parent, sibling z-order,
layered/transparent styles and Progman-internal hit routing. It does not inject
clicks and does not prove Explorer-native behavior.

## Manual native-input gate

Run the Release hidden for 45 seconds:

```powershell
$exe='.\prototype\desktop-host-win32\ts-h01-c\out\build\x64-release\Release\desktop-host-input-probe.exe'
$log='.\prototype\desktop-host-win32\ts-h01-c\out\ts-h01-c-manual.jsonl'
Start-Process -FilePath $exe `
  -ArgumentList '--run-input-test','--duration-ms','45000','--log',$log `
  -WindowStyle Hidden `
  -Wait
```

During the run, expose the desktop and verify:

1. Left-drag in the large hatched `BLANK / EXPLORER INPUT` region creates the
   native Windows desktop selection rectangle.
2. Right-click the same region opens the native Windows desktop context menu.
3. Put a harmless shortcut beneath that region and rerun; single click selects
   it and double click launches it.
4. The cyan title, magenta Item and yellow action islands each change color and
   increment their counter when clicked.
5. An ordinary application covers the whole probe and moving it away reveals
   the probe again.
6. All probe surfaces disappear when the 45 seconds end.

Do not press Win+D as a lifecycle test in this gate; Win+D belongs to TS-H01-D.

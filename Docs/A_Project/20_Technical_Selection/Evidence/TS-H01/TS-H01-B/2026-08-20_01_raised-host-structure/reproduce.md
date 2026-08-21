# Reproduce TS-H01-B-20260820-01

## Scope

This run tests only the structural z-order hypothesis on the currently observed
`RAISED_PROGMAN` Explorer topology. It does not test input pass-through, Win+D,
Explorer restart, rendering framework behavior, or final visual quality.

## Build

From `prototype/desktop-host-win32/ts-h01-b`:

```powershell
cmake --preset x64-release
cmake --build --preset x64-release
```

## Formal automated run

```powershell
.\out\build\x64-release\Release\desktop-host-layer-probe.exe `
  --run-host-test `
  --duration-ms 4000 `
  --log ..\..\..\Docs\A_Project\20_Technical_Selection\Evidence\TS-H01\TS-H01-B\2026-08-20_01_raised-host-structure\host-samples.jsonl
```

The executable creates one 360x220 diagnostic child surface near the center of
the primary work area. It auto-closes after four seconds.

## Manual visual gate — superseded

The original manual gate was executed and failed: the ordinary GDI child was
not visible on the final desktop. Evidence, screenshots and diagnosis are in
`../2026-08-20_02_gdi-visual-fail/`. The executable at the path above has since
been rebuilt as a layered-child control variant and no longer matches this
record's SHA-256. Do not use the current binary to relabel this historical run.

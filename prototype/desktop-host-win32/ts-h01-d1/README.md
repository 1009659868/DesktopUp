# TS-H01-D1 show-desktop continuity probe

This bounded x64 Win32 Release probe reuses the B/C raised layered and split
input relationship, then observes it continuously while the user invokes one
show-desktop entry point. It does not implement repair or recovery.

Run the commands below from the repository root.

## Scenarios

Run keyboard and taskbar scenarios separately:

```powershell
$exe='.\prototype\desktop-host-win32\ts-h01-d1\out\build\x64-release\Release\desktop-host-lifecycle-probe.exe'

Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','win-d','--duration-ms','150000','--log','.\prototype\desktop-host-win32\ts-h01-d1\out\win-d.jsonl' `
  -WindowStyle Hidden -Wait

Start-Process -FilePath $exe `
  -ArgumentList '--run-lifecycle-test','--scenario','taskbar-show-desktop','--duration-ms','150000','--log','.\prototype\desktop-host-win32\ts-h01-d1\out\taskbar.jsonl' `
  -WindowStyle Hidden -Wait
```

For each run, execute 10 complete display/restore cycles using only the named
entry point. The probe uses an 8 ms high-resolution timer target so the measured P95 remains below the 10 ms gate.

## PASS gate

- the card remains continuously visible whenever the desktop is exposed;
- no hide/show, minimize, cloak, detach, recreate, activation or TopMost frame;
- parent/owner and relative DefView sibling order remain unchanged;
- restored ordinary applications cover the card immediately;
- every telemetry sample passes and the timed cleanup is complete.

An exit code of `0` means only that telemetry stayed structurally valid. The
user's no-flicker observation and the exact 10-cycle count remain mandatory.

The probe never synthesizes Win+D, never polls `SetWindowPos`, and never
contains Explorer-restart recovery. Explorer recovery is TS-H01-D2.

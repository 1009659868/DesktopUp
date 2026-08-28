# QTS-01-01 memory baseline probes

This directory contains measurement-only Release probes for the Qt implementation
route. It is not production Desktop-Up code.

Run from PowerShell:

```powershell
& .\run-qts-01-01.ps1
```

The runner builds and samples five isolated roles under one contract:

1. the accepted native TS-H01-B DesktopHost layer probe;
2. Qt Gui with a native platform window and no Qt Quick link;
3. a minimal one-rectangle Qt Quick scene;
4. the current `QQuickItem::grabToImage` offscreen frame-source path without
   DesktopHost projection;
5. the current TS-C01 Night Ops single-poster fixture.

Output is written to
`Docs/A_Project/30_Architecture/Evidence/QTS-01-01/<timestamp>/` as JSON,
CSV, process logs and raw counter samples.

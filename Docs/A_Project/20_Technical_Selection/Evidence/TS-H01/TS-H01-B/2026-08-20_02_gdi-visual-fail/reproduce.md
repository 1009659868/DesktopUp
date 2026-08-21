# Reproduce TS-H01-B-20260820-02

Run the original GDI fixture for 15 seconds:

```powershell
& '.\prototype\desktop-host-win32\ts-h01-b\out\build\x64-release\Release\desktop-host-layer-probe.exe' --run-host-test --duration-ms 15000
```

Expected failure on the recorded environment:

1. the process runs for 15 seconds and returns structural success;
2. the diagnostic card does not appear at primary-screen rect `1100,586–1460,806`;
3. an external `PrintWindow(PW_CLIENTONLY)` call can still capture the complete card;
4. a simultaneous screen capture at the same rectangle contains only the desktop image.

This run must not be relabeled as a successful visual gate merely because `IsWindowVisible`, parent and sibling z-order are correct.

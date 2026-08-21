# TS-H01-D3-1 125% Reproduction

After selecting the primary display and setting Windows scaling to 125%:

```powershell
$runner = '.\prototype\desktop-host-win32\ts-h01-d3\run-d3-dpi-gate.ps1'
& $runner -ExpectedDpi 120 -Runs 5
```

The probe fails closed unless its PMv2 primary projection DPI is 120. `window_dpi=96` and `shell_parent_dpi=96` are expected diagnostic observations for the accepted desktop child host and do not override the primary geometry authority.

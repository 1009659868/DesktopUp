# TS-H01-D3-1 DPI Gate Reproduction

```powershell
$runner = '.\prototype\desktop-host-win32\ts-h01-d3\run-d3-dpi-gate.ps1'
& $runner -ExpectedDpi 96 -Runs 5
```

若某轮只因未操作超时，可保留失败 run，并使用 `-Runs 1` 建立新的独立替代 run。合并证据必须同时列出选中与排除日志，不能覆盖失败记录。

# Reproduce

From repository root, on the recorded Windows/Qt toolchain:

```powershell
& '.\prototype\renderer-adapters\ts-c01-qt-quick\preflight-r3.ps1'
```

Use the generated `r3-preflight.json` and `r3-run-<timestamp>/r4d3-static-fault-performance.jsonl`. A new execution creates a new run and must not overwrite the 2026-08-25 evidence. Compare CPU delta, working-set range, static frames, 200 Item ranking/layout and theme switch metrics to the frozen matrix thresholds.
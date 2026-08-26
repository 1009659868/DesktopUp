# Evidence Record — HG-PERF-02

Evidence ID: `TS-C01-HG-PERF-02-2026-08-25-R4D3`

- Candidate: TS-C01 / Qt 6.8.3
- Matrix ID: HG-PERF-02
- Configuration: Release
- Expected: stable working set ≤50 MiB
- Observed: minimum 135.648 MiB; maximum 136.332 MiB; average 135.976 MiB
- Raw log: `prototype/renderer-adapters/ts-c01-qt-quick/out/r3-run-20260825-170513/r4d3-static-fault-performance.jsonl`
- Raw log SHA-256: `177BBD2E313E8824D21365F8AC779B3233AD7F405E23AD15D443F8EE47FDB788`
- Summary SHA-256: `70163D5740A35A227D4D54BBF0EBCB0C846C7B4DD2E24012C779B601D7F4EB30`
- Result: `FAIL`
- Confidence: HIGH
- Decision boundary: user acceptance to continue R4 did not modify the frozen 50 MiB requirement; scoring cannot offset this hard-gate failure
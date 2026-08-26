# Evidence Record — HG-PERF-01

Evidence ID: `TS-C01-HG-PERF-01-2026-08-25-R4D3`

- Candidate: TS-C01 / Qt 6.8.3
- Matrix ID: HG-PERF-01
- Configuration: Release
- Preconditions: single poster static hold; 10-second minimum
- Expected: CPU time delta ≤0.10s; no sustained high-frequency redraw
- Observed: 10.223352s; CPU time delta 62ms; 0 new frames; single-core CPU 0.606%
- Raw log: `prototype/renderer-adapters/ts-c01-qt-quick/out/r3-run-20260825-170513/r4d3-static-fault-performance.jsonl`
- Raw log SHA-256: `177BBD2E313E8824D21365F8AC779B3233AD7F405E23AD15D443F8EE47FDB788`
- Summary SHA-256: `70163D5740A35A227D4D54BBF0EBCB0C846C7B4DD2E24012C779B601D7F4EB30`
- Result: `PASS`
- Confidence: HIGH for this binary/run
- Limitation: does not establish final-build full P0 identity
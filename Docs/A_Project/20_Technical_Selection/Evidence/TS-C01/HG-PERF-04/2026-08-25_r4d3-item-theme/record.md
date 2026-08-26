# Evidence Record — HG-PERF-04

Evidence ID: `TS-C01-HG-PERF-04-2026-08-25-R4D3`

- Candidate: TS-C01 / Qt 6.8.3
- Matrix ID: HG-PERF-04
- Configuration: Release
- Expected: 200 Item ranking/layout does not block; theme switch <200ms excluding full layout recompute
- Observed: 200 synthetic Items ×64 ranking rounds; layout 0.18ms; 6 theme switches; maximum 27.106ms; coreInteractionBlocked=false
- Raw log: `prototype/renderer-adapters/ts-c01-qt-quick/out/r3-run-20260825-170513/r4d3-static-fault-performance.jsonl`
- Raw log SHA-256: `177BBD2E313E8824D21365F8AC779B3233AD7F405E23AD15D443F8EE47FDB788`
- Summary SHA-256: `70163D5740A35A227D4D54BBF0EBCB0C846C7B4DD2E24012C779B601D7F4EB30`
- Result: `PASS`
- Confidence: HIGH for the synthetic fixture
- Limitation: this is not proof that the full product Item pipeline or theme package platform is implemented
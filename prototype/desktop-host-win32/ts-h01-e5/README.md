# TS-H01-E5 DesktopHost Conformance Fixture

This is an implementation-neutral, offline evidence validator for renderer candidates. It does not create a desktop surface and does not certify any candidate by itself.

Run the fixture self-test:

```powershell
pwsh -NoProfile -File .\test-validator.ps1
```

Validate a candidate JSONL event stream:

```powershell
pwsh -NoProfile -File .\validate-desktop-host-events.ps1 -Path <events.jsonl>
```

The validator checks the shared event envelope, monotonic sequence/time, stable session/build/candidate identity, no TopMost or ordinary top-level fallback, current lifecycle/surface/projection fencing, render/hit/bounds alignment, and final zero-resource cleanup. Manual video and interaction gates remain mandatory under the unified evidence matrix.

Files:

- `conformance-manifest.json`: frozen common scenarios and matrix mappings;
- `validate-desktop-host-events.ps1`: offline JSONL validator;
- `test-validator.ps1`: positive/negative fixture self-test;
- `samples/pass-minimal.jsonl`: valid lifecycle sample;
- `samples/fail-late-frame.jsonl`: intentionally invalid stale-frame sample.

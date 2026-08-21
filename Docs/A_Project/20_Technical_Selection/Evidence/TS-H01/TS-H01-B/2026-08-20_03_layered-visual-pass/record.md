# TS-H01-B Raised Layered Visual Gate — PASS

## Conclusion

The `RAISED_PROGMAN` layered child path passed the bounded TS-H01-B visual and structure gate on the current machine.

This result proves only the following relationship for the probe surface:

`ordinary applications > cyan diagnostic card > desktop icons > wallpaper`

It does not yet prove input passthrough, Win+D stability, WorkerW rebuild recovery or production rendering-framework suitability.

## Automated evidence

- Build: `TS-H01-B-20260820-02-LAYERED`
- Surface mode: `layered_gdi_child`
- Topology: `RAISED_PROGMAN`
- Extended style: `0x8080080`
- Samples: `80 / 80 PASS`
- Host child index: `0`
- `SHELLDLL_DefView` child index: `1`
- TopMost used: no
- Host destroyed on completion: yes
- Log: `host-samples.jsonl`
- Log SHA-256: `F6CCF5A54E585D05D47757D7F5F33BA0853792060625D6700258BD10C542152A`

## Manual visual evidence

The user confirmed all four gates:

1. The cyan diagnostic card was visible at the center of the primary desktop.
2. An ordinary application window covered the card.
3. Moving the ordinary window away revealed the card again.
4. The card disappeared cleanly when the probe duration ended.

## Scope boundaries

- The earlier ordinary GDI child visual failure remains preserved in `2026-08-20_02_gdi-visual-fail`; this PASS does not rewrite that result.
- The traditional WorkerW path remains `UNKNOWN_NOT_PRESENT` in this environment.
- TS-H01-B is passed only for the current `RAISED_PROGMAN` layered path.
- TS-H01-C must now verify native desktop blank-area input, shortcut input and explicit interaction islands without message forwarding or Explorer manipulation.

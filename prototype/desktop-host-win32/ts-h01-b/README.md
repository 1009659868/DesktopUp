# TS-H01-B native host layer probe

This is a bounded native Win32 experiment for the Desktop-Up desktop-host
contract. It is not a renderer candidate and it is not production code.

The executable discovers the visible Explorer icon view, creates one Windows 8-aware layered child
surface in the same safe desktop parent, inserts it above `SHELLDLL_DefView`,
samples the relationship, then destroys the surface automatically.

It deliberately does not test input pass-through, Win+D, Explorer recovery,
per-pixel transparency, rounded composition, or any UI framework.

## Safety properties

- explicit opt-in: the probe does nothing unless `--run-host-test` is present;
- rejects ambiguous or unsupported shell topology;
- creates no top-level poster window;
- uses `WS_EX_LAYERED` only to provide the child with a redirection surface;
- never uses `WS_EX_TOPMOST`, activation, ownership, `SetParent`, Explorer
  injection, subclassing, or undocumented `0x052C` messages;
- defaults to 4 seconds and caps the run at 60 seconds;
- destroys its own child HWND before exit.

## Build

```powershell
cmake --preset x64-release
cmake --build --preset x64-release
```

## Run

```powershell
.\out\build\x64-release\Release\desktop-host-layer-probe.exe `
  --run-host-test `
  --duration-ms 4000 `
  --log .\ts-h01-b.jsonl
```

Exit codes:

- `0`: every structural sample passed;
- `2`: arguments or DPI setup failed;
- `3`: no unambiguous safe desktop host was found;
- `4`: child surface creation or structural verification failed.

An exit code of `0` is only automated structural evidence. The ordinary
non-layered GDI baseline failed the visual gate because Progman uses
`WS_EX_NOREDIRECTIONBITMAP`. The current layered build passed its bounded
manual visual gate on 2026-08-20: the card was visible above desktop icons,
ordinary applications covered it, moving them away revealed it, and timed
cleanup removed it. This does not prove input passthrough, Win+D or Explorer
recovery; those remain separate stages.

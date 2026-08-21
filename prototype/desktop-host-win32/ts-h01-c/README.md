# TS-H01-C native input split probe

This is a bounded native Win32 experiment for the Desktop-Up desktop-host
input contract. It is not production code and it does not select a renderer.

The probe uses the TS-H01-B `RAISED_PROGMAN` host relationship and splits the
poster into two window roles:

- a full-card Windows 8-aware layered visual surface with
  `WS_EX_TRANSPARENT`, which must never receive pointer-button input;
- three smaller layered interaction-island sibling windows (title, Item and
  action button), which remain above the visual surface and receive clicks.

The split is intentional. `HTTRANSPARENT` is not used because its documented
search is limited to underlying windows in the same thread. No mouse messages
are forwarded to Explorer.

## Safety properties

- explicit opt-in through `--run-input-test`;
- rejects ambiguous or unsupported Shell topology;
- creates only child windows in the verified desktop parent;
- never uses TopMost, activation, ownership, Explorer injection, subclassing,
  icon-coordinate reads/writes or undocumented Shell messages;
- defaults to 20 seconds, caps the run at 120 seconds and removes every owned
  HWND at the end.

## Build

```powershell
cmake --preset x64-release
cmake --build --preset x64-release
```

## Run

```powershell
Start-Process `
  -FilePath '.\out\build\x64-release\Release\desktop-host-input-probe.exe' `
  -ArgumentList '--run-input-test','--duration-ms','45000','--log','.\ts-h01-c.jsonl' `
  -WindowStyle Hidden `
  -Wait
```

An exit code of `0` proves only the HWND/z-order/style and `WindowFromPoint`
preflight. The following manual Explorer-native gates remain mandatory:

1. Drag from the large hatched `BLANK / EXPLORER INPUT` area: Explorer must
   draw its native desktop selection rectangle.
2. Right-click the same blank area: the native Windows desktop menu must open.
3. Place a harmless desktop shortcut under that blank area, then rerun: single
   click must select it and double click must launch it.
4. Click the cyan title island, magenta Item island and yellow action island:
   each counter/color must change, while the ordinary application layer still
   covers the entire probe.

Do not execute Win+D during this stage; that is TS-H01-D.

# Desktop-Up Agent Entry

## Cold start

1. Read `Docs/README.md`.
2. Follow `Docs/00_Catalog/ACTIVE_PATH.md` through A → B → C.
3. Read the active C plan, task index, current handoff, code anchors and latest log.
4. Run `git status --short --branch` before modifying files.

## Current branch policy

- `main`: verified release baseline; no direct implementation.
- `dev/mainline`: daily integration branch.
- `impl/desktop-runtime-foundation-a0`: current local implementation branch.
- Follow `Docs/Rules/GIT_STRATEGY.md` for sync, commit and merge behavior.

## Project commands

The A0 UI prototype intentionally has no npm dependencies.

```powershell
pnpm run check
pnpm run build
pnpm run dev
```

Tauri commands become available after Rust stable and Visual Studio C++ Build Tools are installed:

```powershell
pnpm run tauri:dev
pnpm run tauri:build
```

## Safety boundaries

- Never delete or overwrite user shortcuts during a demo.
- Shortcut collection must use a recovery ledger before any move operation.
- Do not inject into Explorer or treat Explorer icon coordinates as authoritative data.
- Do not execute imported native code or unrestricted theme scripts.
- Do not commit, push, merge or delete branches unless the user explicitly requests it.
- Update the active C log, code anchors and handoff after verified implementation work.

# DesktopUp Axiom program blueprint fixture

This fixture uses Axiom Core only and makes the framework-neutral DesktopUp topology compile as
an Axiom type. It does not select or implement a renderer, DesktopHost adapter,
control-center UI, database, process topology, or production runtime.

It proves only the first architecture slice:

- the top-level graph is a zero-sized definition;
- core seams are type-compatible at compile time;
- the default core state contains no fabricated business snapshot;
- a command produces a store effect without optimistically changing accepted
  state;
- only a successful, newer store result produces a new accepted snapshot;
- poster, control-center, tray, and diagnostic projections share one source
  revision;
- adapter dispatch seams are explicit Axiom slots.

Run from the DesktopUp V2 repository root:

~~~powershell
cargo test --manifest-path .\prototype\axiom-desktopup-blueprint\Cargo.toml
~~~

The local Axiom source is currently an independent checkout at axiom/ pinned by
the architecture document to commit
890cc75680e150d98cbbe4133976dfcf35e8e871. Its long-term dependency form is
still an explicit architecture decision.

Normative blueprint:

- Docs/A_Project/30_Architecture/AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md

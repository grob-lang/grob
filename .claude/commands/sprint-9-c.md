---
description: "Sprint 9 · Increment C — `fs` + `IFileSystem`. The File plugin type with date-typed properties (modified/created — hence date-first), the full decompose/read/write/copy/move API, IoError through the native-throw seam, the fifth capability interface landed with its OS-backed default and its playground VFS seam (D-319 — in-memory path→bytes). Extends D-343/D-319."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment C — `fs` + `IFileSystem`

`date` landed in Increment B. This increment lands `fs` — the first host-capability
Sprint 9 module — and with it the **fifth** capability interface, `IFileSystem`. The
`File` plugin type carries `date`-typed properties (`modified`/`created`), which is why
`date` shipped first. `fs` never touches the OS directly; all file contact goes through
the injected `IFileSystem` (D-343's proven seam), with the OS-backed default constructed
by `Grob.Cli` and the playground substituting an in-memory virtual filesystem (D-319).

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 9** `fs` scope (the module
   signatures, the `File` properties and methods, the `overwrite: bool = false`
   parameters on `copy`/`move`/`moveTo`/`copyTo`), §3.1.1, the solution-architecture §.
2. `docs/design/grob-stdlib-reference.md` — the **`fs`** section and the **`File`** type.
3. `docs/design/grob-vm-architecture.md` — the `GrobValue`/`Struct` model (D-303) and the
   `ValueDisplay` protocol (D-336).
4. `docs/design/grob-playground-architecture.md` — the `IFileSystem` interface shape and
   the VFS substitution (`VfsFileSystem` — in-memory, upload-fed, zip-exportable) the
   playground injects in place of the OS default (§ capability set).
5. Decisions: **D-343** (the capability seam and its injection mechanism — `IFileSystem`
   is declared here and consumed for the first time), **D-319** (the playground VFS
   substitution), **D-342** (module-namespace resolution), **D-303** (the `Struct`
   discriminator — `File` is `Struct`-discriminated), **D-336** (`ValueDisplay` — `File`
   registers a `toString()`), **D-284** (the `IoError` leaf), **D-308**
   (`ErrorCatalog`). Grep for the next-free D-number for the `fs`/`IFileSystem` landing
   record.

> **Verify before relying on cited decisions and sections.** Grep the `fs`/`File`
> sections and the playground `IFileSystem` shape. If a signature has moved, surface it.
>
> **`fs` + `IFileSystem` — inline reference.**
>
> - **`IFileSystem` is declared in `Grob.Runtime` and consumed by `Grob.Stdlib`.** It is
>   the fifth of D-319's six interfaces, landing into D-343's proven injection seam —
>   `Grob.Cli` constructs the OS-backed default and passes it to the VM registration
>   surface; the playground injects `VfsFileSystem` instead. `Grob.Stdlib` reaches the
>   OS only through this interface — grep for direct `System.IO.File`/`Directory`/`Path`
>   OS calls in `Grob.Stdlib`; any direct call breaks the playground seam and is a
>   should-fix at least. The DAG holds: interface in `Grob.Runtime`, consumed by
>   `Grob.Stdlib`, injected by `Grob.Cli`; no new cross-edge.
> - **The `File` plugin type.** `Struct`-discriminated (D-303, no new `GrobValueKind`),
>   properties `name`/`path`/`directory`/`extension`/`size`/`modified`/`created`/
>   `isDirectory` — `modified` and `created` return the `date` type landed in B —
>   methods `rename`/`moveTo`/`copyTo`/`delete` with `overwrite` where specified. A
>   registered `toString()` so `ValueDisplay` (D-336) renders it canonically, not
>   structurally.
> - **`fs` module natives.** `list`/`exists`/`isFile`/`isDirectory`/`ensureDir`/
>   `createDir`/`delete`/`deleteRecursive`/`readText`/`readLines`/`writeText`/
>   `appendText`/`copy`/`move`, all through `IFileSystem`. `list()` returns `File[]`.
> - **`IoError` through the native-throw seam.** A failed file operation raises a
>   catchable `IoError` (D-284) that unwinds the Sprint-7 handler table (D-334/D-342) —
>   one mechanism, no bespoke path. The code is the existing `IoError` code referenced
>   through its `ErrorCatalog` descriptor.
> - **`path` separators are Windows-native** (Sprint 8 `path` already established this);
>   `fs` composes on `path` where it needs to, it does not re-implement path logic.

> **Sequencing note.** This is Increment C: B (`date`) → **C (`fs`/`IFileSystem`)** → D
> (`json`/`mapAs<T>`). `File.modified`/`created` depend on B's `date` type. Do not pull
> `json` forward.

## What you're building

1. **`IFileSystem` in `Grob.Runtime`**, consumed by `Grob.Stdlib`, OS-backed default
   constructed in `Grob.Cli`, injected into the VM registration surface — the fifth of
   D-319's six.
2. **The `File` plugin type** — `Struct`-discriminated, `date`-typed `modified`/`created`,
   the full property and method surface, a registered `toString()` for `ValueDisplay`.
3. **The `fs` module natives** — the full decompose/read/write/copy/move surface through
   `IFileSystem`.
4. **`IoError` through the native-throw seam** — catchable, one mechanism.
5. **The `fs`/`IFileSystem` landing decision** — recorded at its real next-free number in
   three-location lockstep, extending D-343/D-319.

No new opcode, no new `GrobValueKind` variant. Error codes reuse the existing `IoError`;
any new code is a fold-versus-new call via `allocating-an-error-code` (D-331).

## Out of scope

`json`/`csv`/`process`/`regex`. `IProcessRunner` (Increment F — do not land it here). The
`mapAs<T>` machinery. Do not edit the `OpCode` enum or add a `GrobValueKind` variant.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):** `fs` resolves as a namespace; `fs.list`
  resolves to `(string): File[]`; a `File` value's members resolve; `File.modified` is a
  `date`. §3.1.1 holds.
- **Stdlib tests (`Grob.Stdlib.Tests`):** the read/write/copy/move surface works against
  an in-memory `IFileSystem` test double; `list()` returns `File[]`; `overwrite` is
  honoured; a failed operation throws `IoError`.
- **VM tests (`Grob.Vm.Tests`):** `print(f)` renders `File` through `ValueDisplay`, not
  structural fields; an `fs` failure unhandled produces the quality diagnostic and exit
  1, and caught by `catch (e: IoError)` resumes.
- **Integration / spec-consistency:** D-316 green; the landing decision in lockstep; no
  direct OS file API in `Grob.Stdlib` (grep-asserted); the DAG check green.

## Acceptance

- The full `fs` API works through `IFileSystem`; `File` properties (including
  `date`-typed `modified`/`created`) and methods work; `overwrite` honoured.
- A file failure throws a catchable `IoError` through the handler table.
- `IFileSystem` landed in `Grob.Runtime`, OS default in `Grob.Cli`, no direct OS file API
  in `Grob.Stdlib`; the playground VFS seam intact.
- `print(f)` renders `File` canonically via `ValueDisplay`.
- No new opcode, no new `GrobValueKind` variant; the landing logged in lockstep; D-316
  green; DAG holds; coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High). A type registration plus native surface plus one capability interface
into a proven seam — no Opus carve-out.

## Hand-off

Summarise: how `IFileSystem` landed and is injected; the `File` type and its `date`-typed
properties; the `fs` native surface; the `IoError` path; the playground VFS seam; the
landing decision. Note for the next chat: Increment D is `json` + `mapAs<T>` +
`json.Node` indexer — the load-bearing type-argument-consumption increment and the Opus
carve-out, building on A's array-indexer emission.

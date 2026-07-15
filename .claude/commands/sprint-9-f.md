---
description: "Sprint 9 · Increment F — `process` + `IProcessRunner`. The ProcessResult plugin type, the four run/runShell/runOrFail/runShellOrFail forms with timeout:int=0, ProcessError on timeout expiry through the native-throw seam, the sixth and FINAL capability interface landed — closing D-319's six — with its playground unsupported seam (in-hierarchy host error, no new code) and the command-execution security note."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment F — `process` + `IProcessRunner`

`csv` landed in Increment E. This increment lands `process` — the last host-capability
Sprint 9 module — and with it the **sixth and final** capability interface,
`IProcessRunner`, closing out D-319's six. `process` executes external commands, so it
carries a real security surface and a deliberate playground stance: the playground
injects an `UnsupportedProcessRunner` that raises a clear in-hierarchy host error rather
than shelling out (D-319).

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 9** `process` scope (`run(cmd,
   args[], timeout)`, `runShell(cmd, timeout)`, `runOrFail()`, `runShellOrFail()`, the
   `ProcessResult` fields, `timeout: int = 0` meaning infinite, `ProcessError` on
   timeout), §3.1.1, the solution-architecture §.
2. `docs/design/grob-stdlib-reference.md` — the **`process`** section.
3. `docs/design/grob-playground-architecture.md` — the `IProcessRunner` interface shape
   and the `UnsupportedProcessRunner` substitution (§7 — the in-hierarchy host error, no
   new code).
4. Decisions: **D-343** (the capability seam — `IProcessRunner` is the last of D-319's
   six to land into it), **D-319** (the playground unsupported stance), **D-342**
   (module-namespace resolution), **D-303** (`Struct` discriminator — `ProcessResult`),
   **D-336** (`ValueDisplay` — `ProcessResult` registers a `toString()`), **D-284** (the
   `ProcessError` leaf), and the `process.run()` timeout open-question resolution (no
   silent default timeout; `timeout: int` on all four forms; `ProcessError` on expiry;
   `ProcessResult` needs no `timedOut` property). Grep for the next-free D-number for the
   `process`/`IProcessRunner` landing record.

> **Verify before relying on cited decisions and sections.** Grep the `process` section
> and the playground `IProcessRunner`/`UnsupportedProcessRunner` shape. If a signature
> has moved, surface it.
>
> **`process` + `IProcessRunner` — inline reference.**
>
> - **`IProcessRunner` is the sixth and final capability interface** — declared in
>   `Grob.Runtime`, consumed by `Grob.Stdlib`, OS-backed default constructed by
>   `Grob.Cli`, landing into D-343's proven seam. With it, D-319's six are complete.
>   `Grob.Stdlib` reaches the OS only through it — grep for a direct
>   `System.Diagnostics.Process` in `Grob.Stdlib`; a direct call breaks the playground
>   seam and is a should-fix at least. The DAG holds: interface in `Grob.Runtime`,
>   consumed by `Grob.Stdlib`, injected by `Grob.Cli`; no new cross-edge.
> - **The `ProcessResult` plugin type** — `Struct`-discriminated (D-303, no new
>   `GrobValueKind`), fields `stdout`/`stderr`/`exitCode`, a registered `toString()` for
>   `ValueDisplay` (D-336).
> - **The four forms.** `run(cmd, args[], timeout: int = 0)`, `runShell(cmd, timeout: int
>   = 0)`, `runOrFail()`, `runShellOrFail()`. `timeout: 0` means infinite (no silent
>   default). `runOrFail`/`runShellOrFail` throw on a non-zero exit; the plain forms
>   return the `ProcessResult` for the caller to inspect.
> - **`ProcessError` on timeout through the native-throw seam.** Timeout expiry raises a
>   catchable `ProcessError` (D-284) that unwinds the Sprint-7 handler table
>   (D-334/D-342) — one mechanism, no bespoke path. `ProcessResult` carries no `timedOut`
>   property; timeout is the exception, not a result field.
> - **The playground unsupported seam.** The playground injects
>   `UnsupportedProcessRunner`, which raises a clear in-hierarchy host error (D-319) — no
>   new error code, an existing in-hierarchy host error. Do not special-case the
>   playground in `Grob.Stdlib`; the substitution is at the injection point.
> - **Security note.** `runShell`/`runShellOrFail` run a command through a shell — the
>   command-injection surface. This is a deliberate v1 capability (Azure CLI scripting is
>   a driving use case), executed only through the injected `IProcessRunner`, never
>   directly. The stdlib does not attempt to sanitise or sandbox — that is the script
>   author's responsibility, documented in the module reference.

> **Sequencing note.** This is Increment F: E (`csv`) → **F (`process`/`IProcessRunner`)**
> → G (`regex`). F closes D-319's six interfaces. Do not pull `regex` forward.

## What you're building

1. **`IProcessRunner` in `Grob.Runtime`** — the sixth and final capability interface,
   consumed by `Grob.Stdlib`, OS-backed default in `Grob.Cli`, closing D-319's six.
2. **The `ProcessResult` plugin type** — `Struct`-discriminated, `stdout`/`stderr`/
   `exitCode`, a registered `toString()`.
3. **The four `process` forms** — with `timeout: int = 0` (infinite), the `orFail`
   variants throwing on non-zero exit.
4. **`ProcessError` on timeout** through the native-throw seam.
5. **The playground unsupported seam** — `UnsupportedProcessRunner` raising the
   in-hierarchy host error (no new code).
6. **The `process`/`IProcessRunner` landing decision** — recorded at its real next-free
   number in three-location lockstep, extending D-343/D-319 and noting D-319's six now
   complete.

No new opcode, no new `GrobValueKind` variant; `ProcessError` reuses the existing leaf.

## Out of scope

`regex` (Increment G). The close (Increment H). Sandboxing or command sanitisation. Do
not edit the `OpCode` enum or add a `GrobValueKind` variant.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):** `process` resolves as a namespace; the
  four forms resolve with their signatures; a `ProcessResult` value's fields resolve.
  §3.1.1 holds.
- **Stdlib tests (`Grob.Stdlib.Tests`):** the four forms capture `stdout`/`stderr`/
  `exitCode` against an `IProcessRunner` test double; `timeout` expiry throws
  `ProcessError`; `runOrFail` throws on non-zero exit; `timeout: 0` is infinite.
- **VM tests (`Grob.Vm.Tests`):** a `ProcessError` unhandled produces the quality
  diagnostic and exit 1, caught it resumes; `print(result)` renders via `ValueDisplay`;
  the `UnsupportedProcessRunner` path raises the in-hierarchy host error with no new code.
- **Integration / spec-consistency:** D-316 green; the landing decision in lockstep; no
  direct `System.Diagnostics.Process` in `Grob.Stdlib` (grep-asserted); D-319's six
  interfaces confirmed complete; the DAG check green.

## Acceptance

- The four `process` forms work through `IProcessRunner`; `timeout` expiry throws
  catchable `ProcessError`; `runOrFail`/`runShellOrFail` throw on non-zero exit.
- `IProcessRunner` landed, closing D-319's six; no direct process API in `Grob.Stdlib`;
  the playground unsupported seam raises the in-hierarchy host error with no new code.
- `print(result)` renders `ProcessResult` via `ValueDisplay`.
- No new opcode, no new `GrobValueKind` variant; the landing logged in lockstep; D-316
  green; DAG holds; coverage at or above 90% — the failure and timeout paths especially.

## Model

Sonnet 4.6 (High). A type registration plus native surface plus the last capability
interface into a proven seam — no Opus carve-out.

## Hand-off

Summarise: how `IProcessRunner` landed and closed D-319's six; the `ProcessResult` type;
the four forms and the `timeout`/`orFail` semantics; the `ProcessError` path; the
playground unsupported seam and the security note; the landing decision. Note for the
next chat: Increment G is `regex` — the `Regex`/`Match` types, the module convenience
functions, and the new `regex.compile()` constructor that replaces the deferred literal
(D-350), with the requirements `regex` bullet reconciled.

---
description: "Sprint 8 · Increment C — `env`, `log`, `input()`. The host-capability and CLI-touching modules: env via IEnvironment (require throws LookupError), log four levels to stderr via IStandardStreams with setLevel and the --verbose CLI flag gating debug, input() via stdin with IoError on a closed stream. No new opcode. Consumes the A-declared capability interfaces; touches Grob.Cli for --verbose."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 8 · Increment C — `env`, `log`, `input()`

Increment B added the pure/near-pure breadth. This increment adds the modules that
reach the **host** — `env` (environment variables), `log` (structured stderr logging)
and `input()` (a line from stdin) — through the capability interfaces Increment A
declared on the injection seam (D-340). It is the first increment to touch `Grob.Cli`
beyond registration: `log.debug` visibility is gated by a `--verbose` flag. No new
opcode; no new structural decision.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 8 `env`, `log` and `input`
   bullets (incl. `input(prompt: string = ""): string` and its `IoError`-on-closed-stdin
   rule; `log.debug` suppressed by default, visible with `--verbose`), §3.5.
2. `docs/design/grob-stdlib-reference.md` — the **`env`** section (the five signatures;
   `require` throws `LookupError`; `has` is false for absent **and** empty; `set` is
   process-scoped only). The `log` one-liner (four levels, all stderr, `setLevel`,
   `--verbose`). Note the `log.info("...${myGuid}...")` example implies interpolation
   calls `toString()` — but `guid` is Increment D; C's log tests use scalar/string
   interpolands only.
3. Decisions: **D-339** (the resolution machinery), **D-340** (`IEnvironment` and
   `IStandardStreams` were declared in A; C is their first consumer), **D-274**/**D-284**
   (`LookupError` and `IoError` leaves), **D-334** (the native-throw seam `require` and
   `input` reuse), **D-336** (`ValueDisplay` — `log` message rendering goes through it,
   and credential opacity via a registered `toString()` must hold: an `AuthHeader`
   logged shows `[AuthHeader]`, never the token, incl. under `--verbose`), **D-308**.

> **Verify before relying on cited sections.** Confirm A landed `IEnvironment` and
> `IStandardStreams` on the seam and that the `--verbose` flag has no prior owner in
> `Grob.Cli`. Grep the `LookupError`/`IoError` descriptors in `grob-error-codes.md`.
> Surface any disagreement before building.
>
> **Module rules — inline reference (authoritative source is the `env` section, the
> requirements § and D-336; reproduced here).**
>
> - **`env` goes through `IEnvironment`, never `System.Environment` directly.**
>   `get(key): string?` → nil if unset; `require(key): string` → throws `LookupError`
>   (through the native-throw seam) if absent **or empty**, message naming the variable;
>   `set(key, value): void` → process-scoped only, no registry/profile write;
>   `has(key): bool` → false for absent and empty; `all(): map<string, string>`. The
>   playground substitutes a synthetic map for `IEnvironment` (D-319) — so no direct OS
>   call may leak past the interface.
> - **`log` writes to stderr through `IStandardStreams`, results never to stdout.**
>   `debug`/`info`/`warning`/`error`, `setLevel`. `debug` is suppressed by default and
>   visible only under `--verbose`. Errors to stderr, results to stdout is the standing
>   personality rule — `log` is a stderr citizen. Message values render through
>   `ValueDisplay` (D-336): a logged `AuthHeader` shows `[AuthHeader]`, never the
>   credential, including under `--verbose` — verify this on the real log path.
> - **`--verbose` is a `Grob.Cli` flag with no prior owner.** It sets the initial log
>   level so `log.debug` is emitted; absent it, `debug` is dropped before reaching the
>   stream. `setLevel` overrides at runtime. Keep the flag plumbing in `Grob.Cli`;
>   `Grob.Stdlib` sees only the injected `IStandardStreams` and a level.
> - **`input()` reads one line from stdin through `IStandardStreams`.**
>   `input(prompt: string = ""): string` — writes the prompt to stdout with **no**
>   trailing newline, reads one line, returns it with the newline stripped. Throws
>   `IoError` (native-throw seam) if stdin is closed before a line is read. No namespace
>   — always available, same category as `print` (formalised in A). It is a built-in,
>   not a module member.
>
> **Sequencing note.** A → B → **C** → D → E → F. Do not build the `guid` type (D) or
> `formatAs` (E). This increment is `env`, `log` and `input()` only.

## Branching, planning and commits

Not on `main`. Plan mode → approval → `/start-branch` `feat/stdlib-host` → TDD
(failing first) → `/commit-message` → stop after the local commit.

## What is already done

Sprints 1–7, the interlude, and **Increments A–B**: the infrastructure and resolution
model (D-339), the native-throw and capability-injection seams (D-340) with
`IEnvironment`/`IStandardStreams`/`IClock`/`IRandomSource` declared, `print`/`exit`
formalised, and `math`/`path`/`strings.join` complete. Error-code base as B left it.

## Deliverable for this increment

1. **`env`** — the five functions on `IEnvironment`; `require` throws `LookupError`
   through the native-throw seam; `has` false for absent and empty; `set` process-scoped.
2. **`log`** — four levels to stderr through `IStandardStreams`; `setLevel`; the
   `--verbose` `Grob.Cli` flag gating `debug`; message rendering through `ValueDisplay`
   with credential opacity preserved.
3. **`input()`** — the built-in on stdin; `IoError` on a closed stream; prompt to
   stdout with no trailing newline; newline stripped from the result.
4. **No new opcode, no new structural decision, no new error code** (reused leaves mint
   none) unless an unanticipated diagnostic surfaces — then `allocating-an-error-code`
   (D-331), surfaced not swept.

## Out of scope

`guid` (D). `formatAs` (E). Calibration and close (F). `fs`/`process` and their
capabilities (Sprint 9). Do not edit the `OpCode` enum.

## Tests

Per §3.5.

- **Stdlib tests (`Grob.Stdlib.Tests`), against fake capabilities:** `env.get` returns
  nil for unset and the value for set; `env.require` throws `LookupError` naming the
  variable for absent **and** empty; `env.has` false for absent and empty; `env.set`
  visible to a later `env.get` in the same run; `env.all` returns the map. `log.info`/
  `warning`/`error` write to the fake stderr; `log.debug` is dropped at default level
  and emitted after `--verbose`/`setLevel`. `input()` returns a line from a fake stdin
  with the newline stripped and throws `IoError` on a closed stream.
- **VM tests (`Grob.Vm.Tests`):** `env.require("MISSING")` unhandled produces the
  quality top-level diagnostic and exit 1; caught by `catch (e: LookupError)` the script
  resumes. `input()` on a closed stream is a catchable `IoError`. A logged value carrying
  a registered `toString()` (use a Sprint-6 struct/built-in with one) renders through
  `ValueDisplay`, and an `AuthHeader`-shaped opaque value shows its bracket tag, never
  its fields, including under `--verbose`.
- **CLI tests:** `--verbose` flips `log.debug` visibility; results stay on stdout, log
  on stderr.
- **Consistency:** D-316 green; catalog↔registry agreement; count reconciled (expected
  no change).

## Acceptance

- `dotnet build`/`dotnet test` green. `env`/`log`/`input` work to spec on the injected
  capabilities; `require` and `input` throw catchable leaves through the handler table;
  `--verbose` gates `debug`; log stays on stderr; credential opacity holds under
  `--verbose`.
- No direct OS access in `Grob.Stdlib` (all host contact through the interfaces).
- No new opcode; no new structural decision; the DAG holds; §3.1.1 holds on any new node.
- Coverage at or above 90% line+branch on the affected projects.

## Model

Sonnet 4.6 (High) throughout. Registration and native-body work over settled seams,
plus a small `Grob.Cli` flag — no Opus carve-out.

## Hand-off

Summarise: the `env` surface on `IEnvironment` and the `require`/`has`/`set` semantics;
the `log` levels, `setLevel`, `--verbose` plumbing and the `ValueDisplay`/opacity
behaviour; `input()` on stdin and its `IoError` path; any diagnostic decisions; the
test files added. Note for the next chat: Increment D is the `guid` primitive type and
module — `Struct`-discriminated storage (no new `GrobValueKind`), checker distinctness,
value equality, a registered `toString()` for `ValueDisplay`, generation on
`IRandomSource`/`IClock`, `parse`/`tryParse`, the namespaces, and the D-149 compile-time
literal validation.

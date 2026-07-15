---
description: "Sprint 9 · Increment A — array-indexer emission fix + reconciliation. Add the missing `VisitIndex` emission so `arr[i]` array index read compiles and runs; wire out-of-bounds to the existing E5101 (IndexError) through the Sprint-7 handler table; confirm the IndexError leaf is reached. Land the append-only decisions-log reconciliation for Sprint 8's guid Increment D (E0601, count 117→118, source D-149) that shipped without a log entry. No module code, no new error code. Prerequisite for the JSON/CSV indexer surface."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment A — array-indexer emission fix + reconciliation

You are opening Sprint 9 of the Grob compiler — the core standard library, part two.
Sprint 8 stood up the module machine and nine light modules. Before any Sprint 9 module
work, this increment clears a piece of standing debt that the module work depends on:
`arr[i]` array index **read** has no compiler emission at all, so any script indexing an
array crashes the VM with a stack underflow (D-345). It is release-blocking on its own,
and it is a hard prerequisite for this sprint — `json.Node["key"]` (Increment D) and
`csv`'s `row[name]`/`row[index]` (Increment E) are indexer access, and the array-indexer
emission underneath them does not exist. This increment adds it, wires out-of-bounds to
the existing `E5101` (`IndexError`), and lands the append-only decisions-log
reconciliation for Sprint 8's guid Increment D that shipped without a log entry. It
writes no module code and mints no error code.

Read, in order:

1. `docs/design/grob-vm-architecture.md` — the compiler's expression emission
   (`Compiler.Expressions.cs`, the `Visit*` overrides), the value-stack model, and the
   Sprint-7 handler-table / native-throw path (D-334) a bounds throw re-uses. Confirm
   there is **no** `VisitIndex` override today.
2. `docs/design/grob-language-fundamentals.md` — array indexing (`IndexExpression`,
   D-112 — `arr[n]`, zero-based, multi-dimensional `matrix[r][c]`), and how the parser
   already produces the `IndexExpression` node the checker already resolves.
3. `docs/design/grob-error-codes.md` — `E5101` (array index out of range → `IndexError`)
   and `E5102` (substring bounds → `IndexError`). Confirm both are already registered
   with `IndexError` as the thrown type; A **wires** `E5101`, it does not mint a code.
4. Decisions: **D-112** (array indexing syntax and `IndexExpression`), **D-345** (the
   pre-existing `VisitIndex` gap, surfaced-not-fixed), **D-284** (the ten-leaf hierarchy
   and the `IndexError` leaf), **D-334** (the handler table / native-throw path the
   bounds throw re-uses). For the reconciliation: **D-149** (the guid design decision
   E0601 shipped under), **D-345**/**D-346** (which both state the stale "count unchanged
   at 117"), and `grob-error-codes.md`'s footer (which correctly records 118). Grep the
   log for the **next-free D-number** (this prompt provisionally names the emission fix
   **D-347** and the reconciliation **D-348**; confirm against the live tail — the tail
   closes at D-346, so next-free is D-347).

> **Verify before relying on cited decisions and sections.** Grep
> `grob-vm-architecture.md` and `grob-error-codes.md` to confirm the emission and
> error-code sections read as this prompt assumes. If a section has moved or a decision
> been superseded, surface it rather than proceeding.
>
> **Grammar-first gate (D-331).** `arr[i]` parses to an `IndexExpression` (D-112) and
> type-checks already — the gap is emission, not grammar. Confirm the `IndexExpression`
> node reaches the compiler unmodified before adding the `VisitIndex` override. No parser
> or AST edit is expected here; if one is needed, that is a surfaced finding, not an
> incidental edit.
>
> **The emission — inline reference.**
>
> - **Array index read (`arr[i]`).** Add the `VisitIndex` override to
>   `Compiler.Expressions.cs`. Emit the receiver, emit the index expression, then the
>   index-read for an array — the same value-stack discipline every other expression
>   visitor follows. The existing map indexer read (D-112, `m[key] → V?`) already emits;
>   the array arm is the missing one. Match the existing emission shape rather than
>   inventing a new one.
> - **Bounds → `IndexError` via `E5101`.** An out-of-range array index raises a
>   `GrobError` (`IndexError`) that enters the **same** unwinding path Sprint 7 built
>   (D-334) — the handler table, the `finallyOffset`, the top-level diagnostic — through
>   one mechanism. Do not build a bespoke bounds-error path. `arr[99]` on a length-3
>   array, unhandled, produces the quality top-level diagnostic (`file:line`, D-322) and
>   exit 1; caught by `try`/`catch (e: IndexError)` it is an ordinary catchable error.
>   The code is the **existing** `E5101` referenced through its `ErrorCatalog` descriptor
>   (D-308) — never a literal.
> - **No new opcode.** Array index read emits over existing opcodes. If you reach to add
>   an `OpCode` case, stop — the enum is closed (§3.3, ADR-0013); growing it would be the
>   `adding-an-opcode` procedure (D-331), out of scope here.
> - **Index write is a verify-and-surface item.** This increment fixes array index
>   **read** — the documented crash and the JSON/CSV read-indexer prerequisite. Confirm
>   whether `arr[i] = v` (array element **write**) also lacks emission. If it does, that
>   is a related finding — surface it with a recommendation (fold into this increment
>   under the same `VisitIndex`/assignment-target machinery, or a separate concern) and
>   take the approved path; do not silently expand scope.
>
> **The reconciliation — inline reference.** Sprint 8's guid Increment D landed `E0601`
> (invalid guid string literal), taking the true count 117 → 118, recorded in
> `grob-error-codes.md`'s three internal locations under source decision D-149, but with
> **no decisions-log landing entry** — so D-345 and D-346 both still state "count
> unchanged at 117". Land a short **append-only** decisions-log entry (provisionally
> **D-348**) recording the guid Increment D landing and correcting the running count to
> **118**. D-345 and D-346 stay as written — the log is append-only, so the correction is
> a new entry with a `Refines:`/note pointer, not an in-place edit. This restores the
> authority (the decisions log) to the true state the registry already holds.

> **Sequencing note.** This is Increment A of the agreed Sprint 9 breakdown: **A
> (indexer fix + reconciliation)** → B (`date`) → C (`fs`/`IFileSystem`) → D
> (`json`/`mapAs<T>`/indexer) → E (`csv`) → F (`process`/`IProcessRunner`) → G (`regex`)
> → H (close). A clears the prerequisite the JSON/CSV indexers need. Do not pull module
> work forward.

## What you're building

1. **`VisitIndex` array index read emission.** The missing override in
   `Compiler.Expressions.cs`, emitting array index read over existing opcodes,
   `ResolvedType`/`Declaration` set on the index node (§3.1.1). Map index read is
   unchanged.
2. **Bounds → `IndexError`.** Out-of-range array index raises `IndexError` through the
   existing `E5101` and the Sprint-7 handler table (D-334) — catchable, and unhandled it
   produces the quality top-level diagnostic and exit 1. One mechanism, no bespoke
   bounds path.
3. **Index-write verify-and-surface.** Confirm whether `arr[i] = v` emits; if not,
   surface with a recommendation and take the approved path.
4. **The reconciliation entry (D-348).** Append-only decisions-log entry recording the
   Sprint 8 guid Increment D landing (E0601) and correcting the running count to 118.
5. **The emission decision (D-347).** Append-only entry recording the `VisitIndex`
   array-read emission and the `E5101` wiring, in three-location lockstep.

No new opcodes, no new error code, no module code, no parser/AST edit beyond a
surfaced-and-approved finding.

## Out of scope

Every Sprint 9 module (`date`, `fs`, `json`, `csv`, `process`, `regex`). Substring
bounds behaviour (`E5102`) beyond confirming it is unchanged. Array element **write** if
the verify step finds it also broken and the approved path is a separate increment. Do
not edit the `OpCode` enum or add a `GrobValueKind` variant.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Compiler / disassembler tests (`Grob.Compiler.Tests`):**
  - `arr[0]` on a literal array disassembles to receiver + index + array index-read over
    existing opcodes — verified through the disassembler, not only the VM's answer. No
    new opcode present.
  - `matrix[r][c]` (D-112 multi-dimensional) emits as chained index reads.
  - §3.1.1: the index node carries a non-null `ResolvedType` and `Declaration`
    (`Assert.Same`).
- **VM tests (`Grob.Vm.Tests`):**
  - `arr[1]` on `[10, 20, 30]` returns `20`; the pre-D-345 stack-underflow crash is gone.
  - `arr[99]` on a length-3 array, unhandled, unwinds to the top level and produces the
    quality diagnostic (file, line, `IndexError`, message) and exit 1.
  - `arr[99]` inside `try`/`catch (e: IndexError)` is caught and the script resumes — the
    bounds throw runs through the Sprint-7 handler table.
  - A `finally` around an out-of-bounds index runs exactly once (the D-334 partition
    holds for a bounds throw as for a user throw).
- **Integration / spec-consistency:** the consistency gate (D-316) green;
  catalog↔registry agreement holds at **118**; D-347 and D-348 present in three-location
  lockstep; the running count in the decisions log now reads 118.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `arr[i]` array index read emits and runs; the D-345 stack-underflow crash is fixed.
- Out-of-bounds array index throws a catchable `IndexError` through the Sprint-7 handler
  table via the existing `E5101` and, unhandled, produces the quality top-level
  diagnostic and exit 1.
- No new opcode, no new error code, no new `GrobValueKind` variant, no module code.
- D-347 (emission) and D-348 (reconciliation, count → 118) logged in three-location
  lockstep; D-345/D-346 unedited; D-316 green with the count reconciled at 118.
- The index-write question is answered — either confirmed working, or surfaced with the
  approved path taken.
- Coverage at or above 90% line+branch on the affected projects; the new emission and
  the bounds-throw path are covered, not excluded.

## Model

Sonnet 4.6 (High) drives the increment. This is a single missing emission over settled
value-stack machinery plus an append-only log reconciliation — no Opus carve-out.

## Hand-off

Summarise: the `VisitIndex` emission shape and how it matches the existing index-read
discipline; how the bounds throw re-uses the Sprint-7 handler table via `E5101`; the
answer to the index-write question and the path taken; D-347 and D-348 and their
lockstep entries; the corrected running count. Note for the next chat: Increment B is
`date` — the first type-carrying module, the `date` plugin type with its registered
`toString()` through `ValueDisplay` (D-336), the `IClock` heavy consumer, the foundation
`fs` (Increment C) consumes.

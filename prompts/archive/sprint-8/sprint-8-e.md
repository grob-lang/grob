---
description: "Sprint 8 · Increment E — `formatAs`. table/list/csv terminators; compile-time column derivation from the Sprint-6 field registry; the chained-form compiler rewrite (<expr>.formatAs.table() → formatAs.table(<expr>), the D-320 mechanism, in v1 not deferred); the namespace-misuse compile errors (bare .formatAs; unknown .formatAs.X); cell rendering through ValueDisplay.Inspect/Display (D-336) with culture-pinned floats; the columns: explicit-selection form. No new opcode."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 8 · Increment E — `formatAs`

Increments A–D delivered the function-shaped modules and the `guid` primitive. This
increment adds the last Sprint-8 module and the most compiler-integrated one:
`formatAs`, the collection-to-string terminators. Unlike the other modules its members
are not plain natives — `formatAs.table` derives its columns from the **element type's
field registry at compile time**, and the chained `<expr>.formatAs.table()` form is a
**compile-time rewrite** to the function form. It renders cells through `ValueDisplay`
(D-336). No new opcode; no new structural decision.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 8 `formatAs` bullet (columns
   derived from the type field registry at compile time; named and anonymous structs),
   the scope-cut § confirming the **chained-form rewrite stays in v1** (retired from the
   cut list, simplified via D-282/D-320), §3.5.
2. `docs/design/grob-stdlib-reference.md` — the **`formatAs`** section in full (the four
   signatures; the chained form and its unconditional compile-time rewrite; the bare-
   access compile error and its exact message; the unknown-method compile error naming
   the three valid methods; the `columns:` explicit-selection form; string left / number
   right alignment; auto-sized widths; scalar formatting lives on the value, not here).
3. Decisions: **D-282** (`formatAs` replaces `format`, scalar formatters moved to
   instance methods), **D-320** (`formatAs` reserved identifier, the member-access
   mechanism, E1103 — the chained form already **parses** as member access),
   **D-339** (the namespace machinery; `formatAs`'s namespace-misuse arm folds into
   D-339's error code — see the budget), **D-336** (`ValueDisplay` — cells render via
   `Inspect`/`Display`, floats culture-pinned so `formatAs.csv` is stable on a `de-DE`
   host), the Sprint-6 field-registry decisions (how a named/anonymous struct's field
   order and names are known at compile time), **D-308**. Grep the log.

> **Verify before relying on cited sections.** Confirm the chained `<expr>.formatAs.table()`
> form parses as member access against the merged tree (D-320) and that the Sprint-6
> field registry exposes ordered field names to the checker at a call site. Grep the
> scope-cut § to confirm the rewrite is in v1. Surface any disagreement before building.
>
> **`formatAs` rules — inline reference (authoritative source is the `formatAs` section,
> D-282/D-320 and D-336; reproduced here).**
>
> - **`formatAs` is a compiler-namespace with a closed three-method surface.**
>   `formatAs.table(items: T[]): string`, `formatAs.table(items: T[], columns: string[]):
>   string`, `formatAs.list(item: T): string`, `formatAs.csv(items: T[]): string`. All
>   return `string` — no write-to-stdout side effect; the caller decides
>   (`print`/`log.info`/`fs.writeText`/concatenation).
> - **The chained form is a compile-time rewrite (in v1).** `<expr>.formatAs.table()`,
>   `<expr>.formatAs.table(columns: [...])`, `<expr>.formatAs.list()`,
>   `<expr>.formatAs.csv()` rewrite **unconditionally** to the function form
>   (`formatAs.table(<expr>)` etc.) at compile time — `formatAs` is reserved (D-320) and
>   cannot collide with a user field, so there is no fallback or disambiguation. The
>   checker then validates the receiver is assignable to the wrapped parameter type and
>   otherwise produces a standard argument-type-mismatch error.
> - **Columns derive from the field registry at compile time.** For `formatAs.table`/
>   `csv`/`list` the column names and order come from the element type `T`'s registered
>   fields (named struct or anonymous `#{ }`), known to the checker at the call site
>   (Sprint 6). The `columns: [...]` form selects and orders explicitly. No runtime
>   reflection over the value.
> - **The namespace-misuse compile errors.** Bare `<expr>.formatAs` (no following method
>   call) is a compile error with the exact message *"formatAs is a compiler-namespace,
>   not a property. Use .formatAs.table(), .formatAs.list(), or .formatAs.csv()."*
>   `<expr>.formatAs.X()` where `X` is not `table`/`list`/`csv` is a compile error naming
>   the three valid methods. These fold into the D-339 namespace-misuse code with a
>   `formatAs`-specific message (kickoff budget, lean: fold); confirm the fold-vs-new
>   call in-increment per `allocating-an-error-code` (D-331).
> - **Cells render through `ValueDisplay` (D-336).** A cell value renders via
>   `Display`/`Inspect` — floats under pinned `InvariantCulture` (`1.5`, never `1,5`),
>   round-trippable, so `formatAs.csv` and the gold masters are stable on any host
>   culture. Strings left-aligned, numbers right-aligned; widths auto-sized to content
>   (per-column alignment/width control is post-MVP).
> - **Scalar formatting is not here (D-282).** `total.format("N2")`, `d.format("dd MMM
>   yyyy")` are instance methods on the value types, not `formatAs` functions. Do not add
>   scalar formatters to this module.
>
> **Sequencing note.** A → B → C → D → **E** → F. Do not build the calibration or the
> close (F). This increment is `formatAs` only.

## Branching, planning and commits

Not on `main`. Plan mode → approval → `/start-branch` `feat/formatas` → TDD (failing
first) → `/commit-message` → stop after the local commit. Flag in the plan the
fold-vs-new call for the namespace-misuse code (lean: fold into D-339's code).

## What is already done

Sprints 1–7, the interlude, and **Increments A–D**: the infrastructure and resolution
model (D-339), the throw/capability seams (D-340), `math`/`path`/`strings`/`env`/`log`/
`input` complete, `print`/`exit` formalised, and the `guid` primitive type. `ValueDisplay`
(D-336) renders composites and honours registered `toString()`s. Error-code base as D
left it.

## Deliverable for this increment

1. **The three terminators.** `table` (both overloads), `list`, `csv` — returning
   `string`, columns derived from the field registry at compile time, `columns:`
   selection, alignment and auto-width per spec.
2. **The chained-form rewrite.** `<expr>.formatAs.X(...)` → `formatAs.X(<expr>, ...)`
   at compile time, unconditional, with the receiver-type validation.
3. **The namespace-misuse compile errors.** Bare `.formatAs` and unknown `.formatAs.X`,
   with their exact messages, through the D-339 code (fold confirmed in-increment).
4. **Cell rendering through `ValueDisplay`.** Culture-pinned floats; string/number
   alignment.
5. **Any code work in lockstep.** If the fold-vs-new call lands on a new code, register
   it in three-location lockstep and reconcile the count; else confirm the fold and
   leave the count unchanged. D-316 green.

## Out of scope

Scalar formatters (`.format(...)` — instance methods, D-282, not this module). The
`formatAs.table()` per-column alignment/width control (post-MVP). The calibration and
close (F). Do not edit the `OpCode` enum.

## Tests

Per §3.5.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):** `<expr>.formatAs.table()`
  rewrites to `formatAs.table(<expr>)` — verified at the AST/compile level, not only by
  output; a receiver not assignable to `T[]` is the argument-type-mismatch code; bare
  `.formatAs` and `.formatAs.X` are the namespace-misuse code with the exact messages;
  columns derive from a named struct and from an anonymous `#{ }` at compile time.
  §3.1.1 holds on the new nodes.
- **Stdlib tests (`Grob.Stdlib.Tests`):** `formatAs.table` over a small struct array
  aligns strings left and numbers right with auto-sized widths; `columns: [...]` selects
  and orders; `formatAs.list` renders one field per line; `formatAs.csv` includes a
  header row and comma-delimits; a `float` cell renders `1.5` (pinned culture).
- **VM / integration tests:** `formatAs.csv` output is byte-identical under a `de-DE`
  ambient culture (the D-336 float pinning holds end-to-end).
- **Consistency:** D-316 green; catalog↔registry agreement; count reconciled per the
  fold-vs-new outcome.

## Acceptance

- `dotnet build`/`dotnet test` green. The three terminators work to spec; the chained
  form rewrites to the function form and validates the receiver; the namespace-misuse
  errors fire with their exact messages; cells render through `ValueDisplay` with pinned
  floats; `formatAs.csv` is culture-stable.
- No new opcode; no scalar formatters added; the DAG holds; §3.1.1 holds.
- Code work (if any) in lockstep, count reconciled, D-316 green.
- Coverage at or above 90% line+branch on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The compile-time column derivation reuses the Sprint-6
field registry and the rewrite reuses the D-320 member-access mechanism — bounded work
over settled machinery, no Opus carve-out.

## Hand-off

Summarise: the three terminators and the `columns:` form; the chained-form rewrite and
where it sits in the pipeline; how columns derive from the field registry at compile
time; the namespace-misuse errors and the fold-vs-new outcome; the `ValueDisplay` cell
rendering and the culture-pinning; the test files added. Note for the next chat:
Increment F is the sprint close — the stability-calibration ritual against the
Sprint-8-runnable script set (D-302 addendum + the §6 correction), the stability test's
first passing run, the sixth VM-execution benchmark baseline (D-313/D-333), and
`stdlib.grob`.

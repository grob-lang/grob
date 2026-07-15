---
description: "Sprint 9 · Increment E — `csv`. The csv.Table/CsvRow plugin types, mapAs<T>() reuse over the D foundation (CSV int-field auto-conversion at the boundary), the row[name]/row[index] indexer, RFC 4180 baseline (quoted fields, embedded commas/newlines, \"\" escape). Reuses the Increment D machinery; adds no new structural call."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment E — `csv`

Increment D stood up the `mapAs<T>` machinery over `json.Node`. This increment lands
`csv` — the `csv.Table`/`CsvRow` plugin types and the second consumer of `mapAs<T>`,
over `csv.Table` this time. It adds the `CsvRow` indexer (over A's array-indexer
emission) and RFC 4180 parsing, and reuses the D machinery rather than re-deriving it —
the Sprint 8 nominal-before-structural rhythm.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 9** `csv` scope (`read`/`write`/
   `parse`/`stdin`/`stdout`, the `hasHeaders`/`delimiter` named parameters, the
   `csv.Table` and `CsvRow` types, RFC 4180), §3.1.1, the solution-architecture §.
2. `docs/design/grob-stdlib-reference.md` — the **`csv`** section (the `csv.Table` fields
   `headers`/`rowCount`/`rows`, `mapAs<T>() → T[]`; `CsvRow`'s `get(name)`/`get(index)`
   and `row[name]`/`row[index]` indexer; the RFC 4180 baseline; `csv.parse()` for
   in-memory strings).
3. Decisions: the **D-349** `mapAs<T>` model (Increment D — reused here over
   `csv.Table`), **D-342** (module-namespace resolution), **D-303** (`Struct`
   discriminator — `csv.Table`/`CsvRow` are `Struct`-discriminated), **D-336**
   (`ValueDisplay` — each registers a `toString()`), **D-284** (the `IoError` leaf
   `csv.read` throws on failure), **D-347** (the array-indexer emission the `CsvRow`
   indexer builds on), **D-343** (`IStandardStreams` for `stdin`/`stdout`), **D-308**
   (`ErrorCatalog`). Grep for the next-free D-number for the `csv` landing record.

> **Verify before relying on cited decisions and sections.** Grep the `csv` section and
> confirm the `mapAs<T>` machinery from D reads as this prompt assumes. If a signature
> has moved, surface it.
>
> **`csv` — inline reference.**
>
> - **`csv.Table` and `CsvRow` are `Struct`-discriminated plugin types** (D-303, no new
>   `GrobValueKind`), each with a registered `toString()` for `ValueDisplay` (D-336).
>   `csv.Table` exposes `headers: string[]`, `rowCount: int`, `rows: CsvRow[]` and
>   `mapAs<T>() → T[]`.
> - **`mapAs<T>` is reused, not re-derived.** `csv.Table.mapAs<T>()` uses the Increment D
>   machinery (D-349) — the checker resolves `<T>` and types the result `T[]`; the
>   runtime coercion maps rows to `T`, with **int-field auto-conversion at the boundary**
>   (a CSV cell is text; a target `int` field is parsed). A shape or conversion mismatch
>   throws through the same seam as JSON's (confirm whether it is the shared shape code or
>   a `csv`-specific one against the live registry — fold-versus-new via
>   `allocating-an-error-code`, D-331). Do not stand up a second `mapAs` mechanism.
> - **The `CsvRow` indexer** — `row[name]`/`row[index]` — builds on the array-indexer
>   emission Increment A restored; `get(name)`/`get(index)` are the method forms.
> - **RFC 4180 baseline.** Quoted fields, embedded commas, embedded newlines, `""` escape
>   for an embedded double-quote; `hasHeaders` defaults true; `delimiter` defaults `,`.
>   `csv.read` throws `IoError` on file failure through the native-throw seam; `csv.parse`
>   is the in-memory form; `csv.stdin`/`csv.stdout` go through `IStandardStreams`.

> **Sequencing note.** This is Increment E: D (`json`/`mapAs<T>`) → **E (`csv`)** → F
> (`process`). E is the second `mapAs<T>` consumer; do not re-implement the machinery.

## What you're building

1. **The `csv.Table` and `CsvRow` plugin types** — `Struct`-discriminated, the field and
   member surface, registered `toString()`s. §3.1.1 on every node.
2. **`mapAs<T>()` over `csv.Table`** — reusing the D-349 machinery, with int-field
   auto-conversion and a shape/conversion-mismatch throw through the seam.
3. **The `CsvRow` indexer** — `row[name]`/`row[index]` over A's emission, plus the
   `get(name)`/`get(index)` method forms.
4. **The `csv` module natives** — `read`/`write`/`parse`/`stdin`/`stdout` with
   `hasHeaders`/`delimiter`, RFC 4180 parsing, `IoError` on failure, `stdin`/`stdout`
   through `IStandardStreams`.
5. **The `csv` landing decision** — recorded at its real next-free number in
   three-location lockstep.

No new opcode, no new `GrobValueKind` variant; `mapAs` reused not re-derived.

## Out of scope

`process`/`regex`. Standing up a second `mapAs` mechanism. Do not edit the `OpCode` enum
or add a `GrobValueKind` variant.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):** `csv.read(p)` types as `csv.Table`;
  `.mapAs<Employee>()` types as `Employee[]`; `row["col"]`/`row[0]` type correctly.
  §3.1.1 holds.
- **Stdlib tests (`Grob.Stdlib.Tests`):** RFC 4180 parsing (quoted fields, embedded
  commas/newlines, `""` escape); `hasHeaders`/`delimiter` honoured; `mapAs<T>` maps rows
  with int-field auto-conversion; a conversion/shape mismatch throws; `csv.parse` handles
  an in-memory string.
- **VM tests (`Grob.Vm.Tests`):** a `mapAs<T>` mismatch unhandled produces the quality
  diagnostic and exit 1, caught it resumes; `print(table)`/`print(row)` render via
  `ValueDisplay`.
- **Integration / spec-consistency:** D-316 green; the landing decision in lockstep; the
  count reconciled.

## Acceptance

- `csv.read().mapAs<T>()` deserialises to `T[]` with int-field auto-conversion and rejects
  a mismatch through the handler table.
- `row[name]`/`row[index]` and `get(name)`/`get(index)` work; RFC 4180 cases parse
  correctly; `hasHeaders`/`delimiter` honoured.
- `csv.read` throws `IoError` on failure; `stdin`/`stdout` go through `IStandardStreams`.
- No new opcode, no new `GrobValueKind` variant; the landing logged in lockstep; D-316
  green; coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High). A type registration plus `mapAs<T>` reuse plus RFC 4180 parsing over
settled machinery — no Opus carve-out.

## Hand-off

Summarise: the `csv.Table`/`CsvRow` types; the `mapAs<T>` reuse and the int-field
auto-conversion; the `CsvRow` indexer over A's emission; the RFC 4180 parsing; the
landing decision. Note for the next chat: Increment F is `process` + `IProcessRunner` —
the sixth and final capability interface, the `ProcessResult` type, `ProcessError` on
timeout, and the playground unsupported seam.

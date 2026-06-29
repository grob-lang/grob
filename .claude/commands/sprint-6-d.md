---
description: "Sprint 6 · Increment D — anonymous structs. #{ f: v } (NewAnonStruct), the internal structural type, type-safe structural field access, anon structs in lambdas and .select() projections, nested #{ }, the { } / #{ } / TypeName { } disambiguation (E2101). No #{...} type-annotation form — anon structs are inferred, value-position only."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 6 · Increment D — anonymous structs

Increment C made named-struct fields readable and writable. This increment adds the
**structural variant**: `#{ field: value }` constructs an anonymous struct through the
`NewAnonStruct` opcode, the type checker synthesises an **internal structural type**
from the literal, field access on it is type-safe, and anonymous structs flow through
lambdas and `.select()` projections — the §7 acceptance vehicle. It reuses Increment
C's field-access machinery; the new work is the structural type and the brace
disambiguation.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 6 anonymous-struct bullets
   (`#{ }` literal, internal structural type, type-safe access, anon structs in lambdas
   and `select()` projections, the brace-disambiguation rule), §3.1.1, §3.3 (confirm
   `NewAnonStruct` is present), §3.5. The §7 acceptance line names anonymous structs in
   `select()` projections specifically.
2. `docs/design/grob-language-fundamentals.md` — the anonymous-struct literal grammar,
   the structural-typing rule (field access is type-safe against the synthesised type)
   and the brace-disambiguation rule: a bare `{ }` is **always** a block, `#{ }` is
   **always** an anonymous struct, `TypeName { }` is **always** named construction — no
   parser ambiguity.
3. `docs/design/grob-type-registry.md` — how a structural (anonymous-struct) type is
   represented, structural field resolution, and any structural-compatibility rule (two
   `#{ }` literals with the same field set and types share a structural type).
4. `docs/design/grob-vm-architecture.md` — the `NewAnonStruct` handler sketch (operand:
   field count; the field name/value pairs assembled on the stack).
5. `docs/design/grob-error-codes.md` — confirm **E2101** (bare `{` is a block — the
   disambiguation diagnostic) and **E1002** (undefined member — reused for structural
   field access). Confirm whether `#{ }` field access reuses E1002 or a structural
   variant; if the spec is silent, surface it rather than inventing a code.
6. `docs/design/grob-sample-scripts.md` — grep for `#{` to see the projection idioms the
   acceptance demonstrates (`.select(e => #{ name: e.name, ... })`) and the nested
   `#{ }` cases (the ARM-template body). The implementation must make these resolve.

> **Verify before relying on cited decisions and sections.** Grep the fundamentals for
> the anonymous-struct and brace-disambiguation rules and the type registry for the
> structural-type representation. Confirm E2101 and the structural-access code in the
> registry. If a rule has moved, surface it.
>
> **Anonymous-struct rules — inline reference (authoritative source is the fundamentals,
> `grob-type-registry.md` and `grob-vm-architecture.md`; reproduced here so the
> implementation does not depend on a fetch landing well).**
>
> - **`#{ field: value }` construction (`NewAnonStruct`).** An anonymous-struct literal
>   constructs a value whose type the checker **synthesises** from the literal's field
>   names and value types — there is no prior declaration. The compiler emits the field
>   name/value pairs then `NewAnonStruct` with the field count; the VM builds the
>   anonymous-struct value. `NewAnonStruct` already exists in the closed enum (Sprint
>   2 A) — wire its emission and dispatch; the enum step is a no-op. A genuinely needed
>   new opcode would be `adding-an-opcode`'s warranted path, surfaced first — this
>   increment needs none.
> - **Structural type — type-safe access.** Field access on an anonymous struct resolves
>   against the **synthesised structural type**: `p.name` is type-safe when `name` is a
>   field of the literal; an undefined field is the structural-access code (E1002 or a
>   structural variant — confirm against the registry). Two `#{ }` literals with the
>   same field set and field types share a structural type (structural identity, no
>   declaration required).
> - **Projections — the acceptance vehicle.** Anonymous structs must work in lambdas and
>   `.select()` projections: `employees.select(e => #{ name: e.name, salary: e.salary })`
>   yields an array whose element type is the synthesised structural type, and field
>   access on the result is type-safe. Nested `#{ }` (an anonymous struct as a field
>   value of another) resolves recursively. The sample scripts' projection idioms are the
>   gold standard — make them resolve.
> - **Brace disambiguation — no ambiguity (E2101).** A bare `{ }` is **always** a block;
>   `#{ }` is **always** an anonymous struct; `TypeName { }` is **always** named
>   construction. The parser already distinguishes these from Sprint 1; the checker and
>   the **E2101** diagnostic ("bare `{` is a block; use `#{ }` for an anonymous struct or
>   `TypeName { }` for named construction") handle the case where a user wrote `{ }`
>   expecting a struct. Confirm the parser's existing disambiguation holds and that
>   E2101 fires where the spec says.
> - **No `#{...}` annotation form — do not add one.** An anonymous struct appears in
>   **value position only** and its type is **inferred** from the literal. There is no
>   `field: #{...}` or `x: #{...} := ...` type-reference production, and none is to be
>   added — D-327 settled the §9 type-reference grammar and deliberately excludes a
>   `#{...}` production. This is a decision-backed prohibition, not a general
>   completeness claim: an implementer reaching to admit `#{...}` as a `TypePrimary`
>   **stops and surfaces** — `extending-the-grammar` does not override a settled
>   decision. (Where an anonymous-struct value
>   needs a name, it is bound by inference: `p := #{ ... }`.)
>
> **Sequencing note.** This is Increment D: A (declarations) → B (construction) →
> C (access) → **D (anonymous structs)** → E (close). The structural variant builds on
> C's field-access machinery; this is the last feature increment before the close.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the structural-type synthesis, the
   structural field-access resolution, the `NewAnonStruct` emission and dispatch, the
   projection path through lambdas/`.select()`, the brace-disambiguation/E2101 handling,
   and the tests — and wait for Chris's approval.
2. On approval, run `/start-branch` and propose `feat/anonymous-structs`. Wait for Chris.
3. Implement with TDD — tests confirmed **failing** first. Follow the `tdd-cycle`,
   `defining-a-type` and `adding-an-opcode` skills.
4. Run `/commit-message` against the staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–5** and **Sprint 6 Increments A–C**: the type registry (A), named
  construction (B), field access and assignment via `GetProperty`/`SetProperty` (C),
  and the D-325 closure-in-field escape verified. C's field-access machinery is what
  the structural variant reuses.

## Deliverable for this increment

Anonymous structs across the type checker, the compiler and the VM, working in
projections, with the brace disambiguation handled. **This is the final feature
increment.**

1. **Type checker — structural type synthesis.** Synthesise an internal structural type
   from each `#{ }` literal (field names and value types); resolve field access against
   it type-safely; share a structural type between literals with the same field set and
   types. Set `ResolvedType` and `Declaration`; the §3.1.1 invariant holds.
2. **Type checker — projections.** `.select(e => #{ ... })` yields an array of the
   synthesised structural type; field access on the result is type-safe. Nested `#{ }`
   resolves recursively.
3. **Compiler — `#{ }` construction.** Emit the field name/value pairs then
   `NewAnonStruct` with the field count.
4. **VM — `NewAnonStruct` dispatch.** Build the anonymous-struct value from the stack
   against `grob-vm-architecture.md`.
5. **Brace disambiguation.** Confirm the parser's `{ }` / `#{ }` / `TypeName { }`
   distinction holds; **E2101** fires where a bare `{ }` was written expecting a struct.
6. **Diagnostics via `ErrorCatalog` (D-308).** E2101 and the structural-access code
   raise through catalog descriptors. No literals; no invented codes; no `#{...}`
   annotation form.

This increment wires the closed-enum `NewAnonStruct` and synthesises the structural type
over already-parsed nodes; it adds no grammar. If you find it needs a parser production
or an AST node, stop and propose via `extending-the-grammar` — but note D-327 forbids a
`#{...}` annotation production, so that specific node is off the table.

## Out of scope

The `types.grob` smoke script, the §7 acceptance run and the benchmark baseline
(Increment E). This increment touches neither the grammar nor the enum; if you find it
needs a node or an opcode, stop and propose via `extending-the-grammar` or
`adding-an-opcode` rather than editing silently. A `#{...}` type-annotation production is
forbidden by D-327 — do not add one.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A `#{ }` literal synthesises a structural type with the right fields and types;
    field access on it is type-safe; an undefined field is the structural-access code
    with a source location.
  - Two `#{ }` literals with the same field set and types share a structural type;
    differing literals do not.
  - `.select(e => #{ ... })` yields an array of the synthesised structural type and
    field access on the result is type-safe (the acceptance projection).
  - Nested `#{ }` resolves recursively (the ARM-template-style depth from the sample
    scripts).
  - A `#{ }` literal disassembles to its field pairs followed by `NewAnonStruct` with
    the correct field-count operand (verified through the disassembler).
  - A bare `{ }` written where a struct was expected is **E2101**; `#{ }` and
    `TypeName { }` are never misread.
  - §3.1.1 invariant holds for every node introduced.
  - Layer-invariant `[Theory]` rows: pathological but parseable `#{ }` inputs type-check
    to a result or a diagnostic, never throw.
- **VM tests (`Grob.Vm.Tests`):**
  - A hand-built chunk constructs an anonymous struct with `NewAnonStruct` and reads its
    fields, leaving the stack at the expected depth.
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script projecting an array through `.select(e => #{ ... })` and reading a
    field of the projected elements runs end-to-end via `grob run`.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `#{ }` constructs an anonymous struct via `NewAnonStruct`; the synthesised structural
  type gives type-safe field access; anon structs work in lambdas and `.select()`
  projections; nested `#{ }` resolves; the brace forms disambiguate with E2101 where a
  bare `{ }` was misused; no `#{...}` annotation form exists.
- The §3.1.1 invariant holds; the DAG holds; diagnostics raise through catalog
  descriptors; no literals, no invented codes. Error-code budget: this increment adds no
  codes (E2101 and the structural-access code already exist); confirm the count is
  unchanged against the **live** registry and the D-316 gate is green, rather than
  asserting a fixed number.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. Structural typing and the brace rule are settled by the
fundamentals and the type registry; the `NewAnonStruct` arm is transcription over a
specified machine reusing C's field-access path. No Opus carve-out.

## Hand-off

Summarise: the structural-type synthesis as built; structural field resolution and its
code; the projection path through `.select()`; the `NewAnonStruct` arm; the brace
disambiguation and E2101; and the test files added. Note for the next chat: Increment E
is the sprint close — the `types.grob` smoke script exercising declarations,
construction with defaults, nested construction, field access and assignment, a
recursive type, a `#{ }` projection and a closure-in-field escape; the §7 acceptance;
and the fourth VM-execution benchmark baseline against the two-axis gate (D-313).

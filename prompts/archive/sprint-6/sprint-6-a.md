---
description: "Sprint 6 · Increment A — type declarations, the type registry and cycle detection. Register every `type Name { ... }`, resolve each field type through the full §9 type-reference grammar, run the §17.1 required-non-nullable field-cycle DFS (E0301/E0302), E1102 type-name collision (D-324), E2208 duplicate field. Fix the §17.1 E—cycle drift. The structural increment B–D reuse."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 6 · Increment A — type declarations, the registry and cycle detection

You are opening Sprint 6 of the Grob compiler — user-defined types. Sprint 5 made
the language call, return and close over. This increment makes it **declare a type**:
every `type Name { field: Type, field: Type = default }` is registered in the type
checker's type registry, each field's type is resolved through the full §9
type-reference grammar, the §17.1 required-non-nullable field-cycle check raises
**E0301** / **E0302**, a `type` name colliding with another top-level binding is
**E1102**, and a duplicate field in a declaration is **E2208**. It is the structural
foundation every later Sprint 6 increment reuses — B's construction, C's field access
and D's anonymous structs all resolve against the registry built here, so the
field-type-resolution discipline built here is load-bearing.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 6 — User-Defined Types**
   scope (the `type` declaration, the type registry, type-cycle detection and the
   "accessing undefined fields is a compile error" bullets), §3.1.1 (the day-one LSP
   properties), §3.3 (the closed `OpCode` enum — confirm `NewStruct`, `NewAnonStruct`,
   `GetProperty`, `SetProperty` are present; **none is emitted in this increment**),
   §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — **§9** (the type-reference grammar,
   `TypeRef := TypePrimary TypeSuffix*`, including named user types, `T?`, `T[]`,
   `map<K, V>`, `fn(): T` and grouping), **§10** (type declarations — fields, required
   vs defaulted), and **§17.1** (type cycles — what participates, what terminates, the
   `Unvisited`/`Visiting`/`Visited` DFS). Grep §17.1 for the error-code citation — it
   is the stale `E—cycle` placeholder (see the drift note below).
3. `docs/design/grob-type-registry.md` — how user `type` declarations are represented
   in the registry, the field representation, and the distinction between a registered
   nominal type and the built-in types.
4. `docs/design/grob-error-codes.md` — confirm **E0301**, **E0302**, **E1102** and
   **E2208** exist with their descriptors and their exact titles. Grep; do not take
   their content from this prompt.
5. Decisions: confirm **D-287** (cycle codes E0301/E0302 replace the `E—cycle`
   placeholder), **D-324** (E1102 broadened to all top-level binding forms, reported
   at the second declaration in source order), **D-323** (the three-pass checker and
   the §17.2 value-binding cycle E0303 — the **other** cycle DFS, not this one), and
   **D-166** (pass-1 top-level registration). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep `grob-language-
> fundamentals.md` to confirm §9, §10 and §17.1 read as this prompt assumes, and the
> decisions log for D-287, D-323, D-324 and D-166. If a section has moved or a
> decision been superseded, surface it rather than proceeding.
>
> **Type-declaration rules — inline reference (authoritative source is §9, §10, §17.1
> and `grob-type-registry.md`; reproduced here so the implementation does not depend
> on a fetch landing well).**
>
> - **The registry.** Pass 1 (D-166) already registers every top-level `type` **name**
>   so a forward reference to a type declared later in the file resolves. This
>   increment populates each registered type's **fields** in pass 2: for each field,
>   its name, its resolved type and whether it is required (no default) or defaulted.
>   It does **not** re-build pass-1 name registration.
> - **Field-type resolution through the full §9 grammar.** Every field annotation is a
>   `TypeRef`. Resolve each through the **complete** grammar — a named user type
>   (`address: Address`, including self-reference `children: Tree[]`), a nullable
>   (`next: Node?`), an array (`tags: string[]`, `int[][]`), a map (`meta: map<string,
>   int>`), a function type (`onDone: fn(): int`) and grouped/suffixed combinations
>   (`(fn(): int)[]`). The grammar is complete post-D-326/D-327; this increment wires
>   the **field-annotation resolution path** to the types the checker already owns. If
>   a field-annotation form in the corpus does not resolve, that is a finding — surface
>   it, do not paper over it. There is **no** `#{...}` annotation form; an anonymous
>   struct is value-position only (Increment D).
> - **§17.1 required-non-nullable field-cycle detection.** A `type` declaration cannot
>   contain a cycle of **required non-nullable named-type fields** that would produce
>   an infinitely-sized value. Walk the type-declaration graph with a standard
>   `Unvisited`/`Visiting`/`Visited` DFS. A back-edge to a type on the DFS stack is a
>   cycle. **What participates:** required fields whose type is a named user-defined
>   type (including self). **What terminates (does not participate):** nullable fields
>   (`T?`, nil terminates), array fields (`T[]`, empty terminates), map fields
>   (`map<K, V>`, empty terminates). A multi-type cycle (`A → B → A`) is **E0301**; a
>   trivial single-type self-reference (`type A { a: A }`) is **E0302**. The full path
>   is reported in the diagnostic. `type Tree { children: Tree[] }` and `type Node {
>   next: Node? }` are legitimate and must **not** fire — the array and nullable
>   terminate the walk.
> - **This is the §17.1 walk only — not §17.2.** §17.2's value-binding cycle (E0303,
>   D-323) is a different DFS over top-level initialiser dependencies, run in the
>   checker's phase 1.5. Do not touch it and do not route a field cycle through it.
>   Two walks, two graphs, two codes.
> - **Type-name collision (E1102, D-324).** A `type` name that collides with any other
>   top-level binding — another `type`, an `fn`, a `readonly`/`const`/`:=` value — is
>   **E1102** "name already declared in this scope", reported at the **second**
>   declaration in source order (the D-324 uniform rule; provisional registration in
>   pass 1, finalised in pass 2). Do not re-derive the collision predicate; reuse the
>   uniform one D-324 established.
> - **Duplicate field (E2208).** A `type` declaration listing the same field name more
>   than once is **E2208** "duplicate field name in type declaration".
> - **No construction, no runtime, no opcodes.** This increment registers and validates
>   declarations. It emits no bytecode for construction or access, builds no runtime
>   struct value and emits no `OpCode` arm. If you reach to emit `NewStruct` or
>   `GetProperty`, stop — that is Increment B and C.
>
> **The §17.1 `E—cycle` drift — corrected in this increment.** §17.1 still prints the
> placeholder `E—cycle`, but the registry records that **E0301 already replaced it**
> (D-287), with **E0302** for the trivial self-reference case. This is live drift: the
> registry moved on, the spec prose did not. Correct §17.1 to cite E0301 (multi-type
> cycle) and E0302 (trivial self-reference), and update the error-message-format block
> accordingly. This is a mechanical spec fix, surfaced not swept — the same shape as
> Sprint 5's `map` → `select` §6 correction. No design change, no new error code.
>
> **Sequencing note.** This is Increment A of the agreed Sprint 6 breakdown:
> **A (declarations + registry + cycles)** → B (construction + defaults) → C (access +
> assignment) → D (anonymous structs) → E (close). Do not pull construction, defaults,
> field access, assignment or anonymous structs forward — those are their own
> increments. This increment is declaration registration, field-type resolution and
> cycle detection only.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the registry
   population (pass-2 field resolution), the §9 field-annotation resolution, the §17.1
   DFS, the E1102/E2208 checks, the §17.1 spec-drift fix and the tests — and wait for
   Chris's approval before editing. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/type-declarations`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first. Follow the
   `tdd-cycle` and `defining-a-type` skills.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–2:** the front end (lexer, grammar-complete AST including `type`
  declaration, named-construction, anonymous-struct-literal and member-access nodes,
  the error-recovering parser) and the back end (closed `OpCode` enum **including
  `NewStruct`, `NewAnonStruct`, `GetProperty`, `SetProperty` defined but not yet
  emitted**, `GrobValue` (D-303), `Chunk`, disassembler, VM dispatch loop, the
  multi-pass type checker, compiler, BenchmarkDotNet skeleton). C# 14 / .NET 10
  (D-310), `ErrorCatalog` (D-308), the consistency gate (D-316). All green.
- **Sprint 3:** variables, the scope chain, locals on slots, `const`/`readonly`, the
  nullable runtime, string interpolation, the REPL, the first benchmark baseline.
- **Sprint 4:** control flow and the `calculator.grob` close.
- **Sprint 5:** functions and closures — `fn` declarations, call frames, named and
  default arguments, lambdas, natives, closures with upvalue capture, the top-level
  initialisation state machine and narrowing, and the `functions.grob` close. All
  green, QA and QA-fix landed. The post-close correctness increments and numbered
  interlude (D-320 through D-328) are merged — including the **three-pass type
  checker** (D-323, phase 1.5, E0303), **location-based upvalue closing** (D-325),
  **function types as type-references** (D-326) and **array type-refs** (D-327). The
  §9 type-reference grammar is therefore complete coming into Sprint 6.

## Deliverable for this increment

Type declarations registered and validated across the type checker, with field types
resolved through the full §9 grammar and required-non-nullable field cycles caught.
The parser and AST are stable — every node exists. **No construction, no field access
or assignment, no anonymous structs, no opcodes, no runtime struct value.**

1. **Type registry — field population (pass 2).** For every registered `type`,
   resolve each field's annotation through the §9 grammar and record name, resolved
   type and required/defaulted. Set `ResolvedType` and `Declaration` on every
   identifier node introduced; the §3.1.1 invariant holds. (Pass 1's name
   registration already exists — D-166 — reuse it.)
2. **Field-type resolution.** Each field annotation resolves to the type the checker
   already owns — a registered nominal type, a built-in, a `T?`, a `T[]`, a `map<K,
   V>`, a `fn(...): T`. Self-reference and forward reference to a type declared later
   resolve via the registry. An unresolvable annotation is the appropriate existing
   diagnostic (e.g. E1001 undefined identifier for an unknown type name) — confirm the
   code, do not invent.
3. **§17.1 cycle detection.** The required-non-nullable field-cycle DFS, raising
   **E0301** (multi-type) or **E0302** (trivial self-reference) with the full cycle
   path and a source location. `Tree`/`Node` terminating patterns do not fire.
4. **Collision and duplicate checks.** **E1102** for a `type` name colliding with any
   other top-level binding, at the second declaration in source order (D-324).
   **E2208** for a duplicate field name within a declaration.
5. **§17.1 spec-drift fix.** Correct the §17.1 `E—cycle` placeholder to cite E0301 and
   E0302 (D-287), updating the error-message-format block. Mechanical, sanctioned in
   the kickoff.
6. **Diagnostics via `ErrorCatalog` (D-308).** E0301, E0302, E1102, E2208 all raise
   through their existing catalog descriptors. No `"Exxxx"` literal at a call site; no
   invented codes.

No new opcodes (the enum is closed), no parser or AST edits, no construction or
runtime.

## Out of scope

Named construction, required-field validation at construction, field defaults and the
`NewStruct` runtime value (Increment B). Field access and assignment, `GetProperty`/
`SetProperty` and the closure-in-field escape (Increment C). Anonymous structs and the
brace-disambiguation diagnostic (Increment D). The §17.2 value-binding cycle DFS
(E0303) is **not** this increment — it is the merged D-323 phase-1.5 walk and must not
be touched or conflated with the §17.1 field walk. Do not edit the parser, the AST or
the `OpCode` enum. If you believe you need an opcode in this increment, stop and
surface — A emits none.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - A declared type registers with the correct fields, types and required/defaulted
    flags; a forward field reference to a type declared later in the file resolves
    (pass-1 registration, D-166).
  - Each §9 field-annotation form resolves: a named user type, a self-referential
    `T[]` field, a `T?` field, a `map<K, V>` field, a `fn(...): T` field, and a
    grouped/suffixed combination. A field whose annotation names an unknown type is the
    appropriate existing diagnostic with a source location.
  - **E0301** fires on a multi-type required-non-nullable cycle (`A.b: B`, `B.a: A`)
    with the full path; **E0302** fires on `type A { a: A }`. `type Tree { children:
    Tree[] }` and `type Node { next: Node? }` do **not** fire (array and nullable
    terminate the walk).
  - **E1102** fires at the second declaration when a `type` name collides with another
    `type`, with an `fn`, and with a value binding — including the value-before-`type`
    order (the D-324 reverse-order case).
  - **E2208** fires on a duplicate field name within one declaration.
  - §3.1.1 invariant: every identifier node introduced carries a non-null
    `ResolvedType` and a non-null `Declaration` (sentinels by reference, `Assert.Same`).
  - Layer-invariant `[Theory]` rows: pathological but parseable declarations type-check
    to a result or a diagnostic, never throw.
- **Integration / spec-consistency:** the §17.1 prose now cites E0301/E0302, and the
  consistency gate (D-316) is green (no `E—cycle` placeholder remains; catalog↔registry
  agreement holds; error-code count unchanged at 109 — A registers no new code).

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Types declare and register; every §9 field-annotation form resolves; required-non-
  nullable field cycles are caught (E0301/E0302) and legitimate `Tree`/`Node` patterns
  are not; type-name collisions are E1102 at the second declaration; duplicate fields
  are E2208.
- The §17.1 spec-drift fix is applied; no `E—cycle` placeholder remains.
- The §3.1.1 invariant holds for every identifier node introduced.
- No new opcodes, no parser/AST edits, no construction or runtime; the DAG holds.
- Diagnostics raise through `ErrorCatalog` descriptors; no literals, no invented
  codes; error-code count unchanged at 109.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The registry shape, the §9 grammar and the §17.1 DFS are
settled by the spec, and every code already exists — this is transcription over a
specified machine plus a sanctioned drift fix, not judgement. There is no Opus
carve-out in this increment.

## Hand-off

Summarise: the registry's field representation as built, how each §9 field-annotation
form resolves, the §17.1 DFS and its terminating rules, the E1102/E2208 checks and
their catalog codes, the §17.1 spec-drift fix as applied, and the test files added.
Note for the next chat: Increment B is named construction and field defaults — required-
field validation (E0103), unknown-field-at-construction at the new **E0012** (D-330,
registered in B, count 109 → 110), the construction-site default-expression machinery
(the Sprint 5 B default-argument shape), nested construction, E2102 and the `NewStruct`
runtime value.

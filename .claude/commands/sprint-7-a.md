---
description: "Sprint 7 · Increment A — the exception hierarchy, `throw` and the unhandled top-level path. Register `GrobError` + ten leaves as built-in nominal types with D-274 fields and the first leaf<:GrobError subtyping, resolve `throw` (operand must be a GrobError subtype, new code E0014), emit `Throw`, and give the VM a `Throw` arm that unwinds to the top level and produces the quality diagnostic + exit 1. Verify the try/catch/finally/throw grammar parses first (D-331). The structural increment B–D reuse."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 7 · Increment A — the exception hierarchy, `throw` and the unhandled top-level path

You are opening Sprint 7 of the Grob compiler — error handling. Sprint 6 made the
language declare, construct and access structured data. This increment makes it
**raise an error**: the ten-leaf `GrobError` hierarchy is registered as built-in
nominal types, `throw <expr>` type-checks its operand as a `GrobError` subtype and
emits the `Throw` opcode, and the VM's `Throw` arm — with no handler yet in the
language — unwinds the whole stack to the top level and produces a Grob-quality
diagnostic before exiting with code 1. It is the structural foundation every later
Sprint 7 increment reuses — B's catch matching, C's finally and D's runtime routing
all resolve against the hierarchy registered here, so the exception-type
registration and the subtype-assignability discipline built here are load-bearing.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 7 — Error Handling** scope
   (the exception hierarchy, `throw`, the `Throw` opcode and the unhandled-exception
   top-level bullets), §3.1.1 (the day-one LSP properties), §3.3 (the closed
   `OpCode` enum — confirm `TryBegin`, `TryEnd`, `Throw` are present; **only `Throw`
   is emitted in this increment**), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — **§27** (exception handling — the
   v1 hierarchy, the canonical throw-site table, `throw` syntax and the
   unhandled-exception behaviour). Read the hierarchy diagram and the `throw`
   subsection closely; the `try`/`catch`/`finally` subsections are B and C.
3. `docs/design/grob-type-registry.md` — how built-in nominal types are represented
   in the registry, and how a user `type` (Sprint 6) differs from a runtime-registered
   built-in type. The exception leaves are built-ins, not user declarations.
4. `docs/design/grob-error-codes.md` — confirm the exception-runtime leaves each
   carry a descriptor with a `throws_type`, and grep for the **next free Type-block
   number** for the throw-operand code (this prompt provisionally names it **E0014**;
   confirm against the live registry — E0012 and E0013 are already taken). Do not
   take the number from this prompt.
5. Decisions: confirm **D-284** (the ten-leaf hierarchy, authoritative over D-084),
   **D-274** (`throw` grammar, exception fields, the `ThrowStatement` AST node),
   **D-043** (exceptions constructed via struct construction syntax), **D-110**
   (`exit()`/`ExitSignal`), **D-322** (runtime diagnostics are `file:line`,
   column-less) and **D-308** (`ErrorCatalog`). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep `grob-language-
> fundamentals.md` to confirm §27 reads as this prompt assumes, and the decisions log
> for D-284, D-274, D-043, D-110 and D-322. If a section has moved or a decision been
> superseded, surface it rather than proceeding.
>
> **Grammar-first gate (D-331).** Before any type-checker or emission work, confirm
> the `throw` production actually parses to a `ThrowStatement(expression)` against the
> merged tree, and that `try`/`catch`/`finally` parse to their D-274/D-275 nodes
> (`TryStatement` with `CatchClause`/`FinallyClause`). D-331 recorded that the
> "grammar-complete" premise was false in Sprint 4E and 6B; do not assume it holds
> here. This increment builds only on `throw`, but confirm the whole exception
> grammar parses so B and C inherit a verified tree. If a production is missing or
> malformed, that is a finding — extend it through the `extending-the-grammar` skill,
> surfaced not swept, before continuing.
>
> **Exception-hierarchy rules — inline reference (authoritative source is §27, D-284,
> D-274 and `grob-type-registry.md`; reproduced here so the implementation does not
> depend on a fetch landing well).**
>
> - **The hierarchy is a `Grob.Runtime` concern, registered as built-in nominal
>   types.** `GrobError` is the root; the ten leaves — `IoError`, `NetworkError`,
>   `JsonError`, `ProcessError`, `NilError`, `ArithmeticError`, `IndexError`,
>   `ParseError`, `LookupError`, `RuntimeError` — are direct children (D-284,
>   authoritative over D-084's six-leaf shape). Register them so the type checker can
>   resolve `GrobError` and every leaf by name, exactly as it resolves a built-in
>   scalar. They are **not** user `type` declarations and do **not** go through the
>   Sprint 6 pass-1/pass-2 user-type registry path; they are runtime built-ins,
>   present before any script is checked.
> - **The fields (D-274).** `GrobError` root: `message: string` and `location:
>   SourceLocation?`. `NetworkError` adds `statusCode: int?`. All other leaves carry
>   `message` (and inherited `location`) only in v1. The `location` field is
>   **runtime-set** — the runtime stamps the throw site's location and any
>   user-supplied `location` value in a construction initialiser is ignored. The
>   checker permits `location` in the initialiser (so the field is nameable) but does
>   not treat its omission as a missing required field.
> - **The first subtype relationship.** Each leaf `<: GrobError`. This is the first
>   user-observable nominal subtyping in the type system. Build the assignability
>   check for real — a leaf is assignable to `GrobError`; `GrobError` is **not**
>   assignable to a leaf; a leaf is not assignable to a sibling leaf. In v1 the
>   hierarchy is flat and closed (no user exceptions, D-085), so this is shallow, but
>   it is specified and built polymorphically so user-defined exceptions slot in
>   post-MVP without a matching-engine change (D-274). Do not special-case the flat
>   shape into exact-match; build the subtype check.
> - **`throw <expr>` type-checking.** The operand must evaluate to a `GrobError` or a
>   subtype. `throw 42`, `throw "oops"` and throwing any other non-`GrobError` value
>   are compile errors at the **new throw-operand code** (provisionally **E0014**,
>   confirmed next-free against the live registry, Type block, dedicated over folding
>   into E0001 per D-318/D-330). `throw` is a statement, not an expression (§28); it
>   does not appear in expression position.
> - **Exception construction is Sprint 6 construction (D-043).** `throw IoError {
>   message: "File not found: ${path}" }` is named construction of a `GrobError`
>   subtype — it goes through the **existing** `NewStruct` emission and dispatch path,
>   obeys required-field validation (E0103) and unknown-field (E0012), and evaluates
>   its field defaults at construction time. Do **not** build a second construction
>   path for exceptions. The one exception-specific rule is the `location` override
>   above.
> - **`Throw` emission and the unhandled path.** `throw` compiles the operand (a
>   construction, or an already-bound `GrobError` value) then emits `Throw`. The VM's
>   `Throw` arm pops the exception value and, **with no handler present in this
>   increment**, unwinds the entire call stack — closing open upvalues by location on
>   every frame it tears down (D-325, route-agnostic) — to the VM top level, where it
>   produces the Grob-quality diagnostic (file, line, error type, message,
>   suggestion; `file:line` only, no column, D-322) and terminates with exit code 1.
>   This is the same observable top-level shape a script sees today when a runtime
>   error halts it, now driven by a first-class exception object.
> - **No `try`, no `catch`, no `finally`, no handler table.** This increment raises
>   and propagates-to-top only. It builds no protected-region handling, no catch
>   matching and no `finallyOffset`. If you reach to emit `TryBegin`/`TryEnd` or to
>   build a handler table, stop — that is Increment B and C.
>
> **Sequencing note.** This is Increment A of the agreed Sprint 7 breakdown:
> **A (hierarchy + throw + unhandled top-level)** → B (try/catch) → C (finally) →
> D (runtime-throw catchability + uncatchable exit) → E (close). Do not pull catch
> matching, the handler table, finally or the runtime-site routing forward — those
> are their own increments. This increment is the exception-type registration, `throw`
> type-checking and emission, and the unhandled top-level diagnostic only.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the grammar-first
   verification, the exception-hierarchy registration and subtype-assignability, the
   `throw` type-check (E0014), the `NewStruct`-reuse for construction, the `Throw`
   emission, the VM unwind-to-top arm and its top-level diagnostic, and the tests —
   and wait for Chris's approval before editing. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/exception-hierarchy`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first. Follow the
   `tdd-cycle` skill.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–2:** the front end (lexer, grammar-complete AST including the `throw`,
  `try`/`catch` and `finally` nodes per D-274/D-275, the error-recovering parser) and
  the back end (closed `OpCode` enum **including `TryBegin`, `TryEnd`, `Throw` defined
  but not yet emitted**, `GrobValue` (D-303), `Chunk`, disassembler, VM dispatch loop,
  the multi-pass type checker, compiler, BenchmarkDotNet skeleton). C# 14 / .NET 10
  (D-310), `ErrorCatalog` (D-308), the consistency gate (D-316). All green.
- **Sprint 3:** variables, the scope chain, locals on slots, `const`/`readonly`, the
  nullable runtime, string interpolation, the REPL, the first benchmark baseline.
  `NilError` and `ArithmeticError` runtime sites exist as VM halts.
- **Sprint 4:** control flow and the `calculator.grob` close. Arithmetic error sites
  (div/0, mod/0, overflow) halt the VM.
- **Sprint 5:** functions and closures — call frames, named and default arguments,
  lambdas, natives, closures with upvalue capture, the three-pass checker (D-323),
  location-based upvalue closing (D-325), function types (D-326) and array type-refs
  (D-327). `IndexError` and `RuntimeError` (stack-overflow) sites exist as VM halts.
- **Sprint 6:** user-defined types — `type` declarations and the registry, named
  construction with field defaults (the `NewStruct` path this increment reuses for
  exception construction), field access and assignment, anonymous structs, the
  field-cycle check. E0012 and E0013 registered; the error-code base is 112. All
  green, QA and QA-fix landed.

## Deliverable for this increment

The `GrobError` hierarchy registered as built-in nominal types, `throw` type-checked
and emitting `Throw`, and an unhandled exception producing a quality top-level
diagnostic. **No `try`, no `catch`, no `finally`, no handler table.**

1. **Exception hierarchy registration.** `GrobError` and the ten leaves registered
   as built-in nominal types in `Grob.Runtime`, resolvable by the type checker by
   name, with their D-274 fields and the runtime-set `location` rule. Each leaf
   `<: GrobError` in the assignability check, built polymorphically.
2. **`throw` type-checking.** The operand resolves to a `GrobError` subtype or the
   new throw-operand code fires (provisionally **E0014**, confirmed next-free). Set
   `ResolvedType` and `Declaration` on every identifier node introduced; the §3.1.1
   invariant holds.
3. **Exception construction via `NewStruct` (D-043).** `throw Leaf { … }` reuses the
   Sprint 6 construction path with its E0103/E0012 rules and default emission; the
   `location` override is the only exception-specific behaviour.
4. **`Throw` emission and VM arm.** `throw` emits `Throw`; the VM pops the exception,
   unwinds every frame to the top level (closing upvalues by location, D-325) and
   produces the Grob-quality diagnostic (`file:line`, D-322) with exit code 1.
5. **The new error code, registered in lockstep.** The throw-operand code registered
   in `grob-error-codes.md` and the catalog at the next-free number, three-location
   lockstep (summary row, full entry, footer changelog), count reconciled 112 → 113,
   D-316 gate green. No `"Exxxx"` literal at a call site.
6. **Diagnostics via `ErrorCatalog` (D-308).** The throw-operand code and every reused
   code raise through catalog descriptors. No literals, no invented codes.

No new opcodes (the enum is closed), no parser or AST edits (grammar verified, not
grown), no handler table, no catch, no finally.

## Out of scope

`try`/`catch`, the handler table, catch matching and the catch compile errors
(Increment B). `finally`, the `finallyOffset` and the emitted finally chains
(Increment C). Routing the existing runtime throw sites through the exception model
and the uncatchable-`exit()` verification (Increment D) — this increment leaves the
existing runtime halts as they are; it does not convert them. The `errors.grob` smoke
script and the benchmark baseline (Increment E). Do not edit the `OpCode` enum. If you
believe you need an opcode, stop and surface — A emits only `Throw`, which already
exists.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - `GrobError` and each of the ten leaves resolve by name; a leaf is assignable to
    `GrobError`, `GrobError` is not assignable to a leaf, and a leaf is not
    assignable to a sibling leaf.
  - `throw IoError { message: "x" }` type-checks; `throw 42`, `throw "oops"` and
    `throw someInt` are the throw-operand code with a source location.
  - Exception construction obeys Sprint 6 rules: a missing required field is E0103,
    an unknown field is E0012, and a user-supplied `location` is accepted at the
    checker but documented as runtime-overwritten.
  - §3.1.1 invariant: every identifier node introduced carries a non-null
    `ResolvedType` and a non-null `Declaration` (sentinels by reference,
    `Assert.Same`).
- **Compiler / disassembler tests (`Grob.Compiler.Tests`):**
  - A `throw` of a constructed exception disassembles to the field values then
    `NewStruct` then `Throw`, with the right type index — verified through the
    disassembler, not only the VM's answer.
- **VM tests (`Grob.Vm.Tests`):**
  - An unhandled `throw` unwinds to the top level and produces the quality
    diagnostic (file, line, error type, message) and exit code 1.
  - An unhandled `throw` from inside a called function unwinds the call frames and
    closes any open upvalues by location (D-325) with no value-stack underflow.
  - Layer-invariant `[Theory]` rows: pathological but parseable `throw` operands
    type-check to a result or a diagnostic, never throw a host exception.
- **Integration / spec-consistency:** the consistency gate (D-316) is green;
  catalog↔registry agreement holds; the error-code count is reconciled 112 → 113.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The hierarchy resolves; leaf `<: GrobError` assignability holds both ways; `throw`
  type-checks a `GrobError` subtype and rejects any other operand at the new code.
- Exception construction reuses `NewStruct` with the Sprint 6 rules; the `location`
  override holds.
- An unhandled `throw` produces the Grob-quality top-level diagnostic and exit 1,
  unwinding frames and closing upvalues correctly.
- The §3.1.1 invariant holds for every identifier node introduced.
- No new opcodes, no parser/AST edits, no handler table, no catch, no finally; the
  DAG holds.
- Diagnostics raise through `ErrorCatalog` descriptors; no literals, no invented
  codes; the count is reconciled 112 → 113.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The hierarchy shape, the fields and the `throw`
grammar are settled by §27 and D-274/D-284, exception construction reuses the Sprint 6
`NewStruct` path verbatim, and the unhandled top-level shape mirrors the existing
runtime-halt diagnostic — this is registration, a bounded type-check and a
straight-line emission over a specified machine. There is no Opus carve-out in this
increment.

## Hand-off

Summarise: how the hierarchy is registered and how leaf `<: GrobError` assignability
is checked, how `throw` type-checks its operand and which code rejects a bad one, how
exception construction reuses `NewStruct` and how the `location` override works, the
`Throw` emission shape and the VM unwind-to-top arm with its top-level diagnostic, the
new code and its lockstep registration, and the test files added. Note for the next
chat: Increment B is `try`/`catch` — `TryBegin`/`TryEnd` emission, the handler table,
polymorphic source-order first-match, the catch-all-last rule and the catch binding,
the catch compile errors (E2204, E2205, the new catch-type code and the new
duplicate-catch code), and the VM's unwind-to-nearest-handler arm.

---
description: "Sprint 4 · Increment E — switch expression. value switch { p => r, _ => d } — a value-producing expression with exhaustiveness enforced by the type checker and all arms unifying to one type (reusing Increment A's ternary arm-unification). Different machine from select: expression vs statement, exhaustive vs non-exhaustive. Registers E0505."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 4 · Increment E — switch expression

Increment D delivered the `select`/`case` **statement** — non-exhaustive, no
value. This increment delivers its deliberate counterpart: the switch
**expression** `value switch { pattern => result, _ => default }` — a
value-producing expression with **exhaustiveness enforced by the type checker**
and **all arms unifying to one type**. The two are a designed split (D-301):
different machines underneath. The exhaustiveness check is the structural piece
here — it is type-checker work, not just emission.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 4** switch-expression
   bullet (exhaustiveness enforced by the type checker, all arms same type),
   §3.3 (the comparison and jump opcodes the arms compile to), §3.1.1, §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§3.1** (Conditional
   Expression — Switch Expression) and **§12** (Expressions vs Statements — the
   switch expression is an expression, like the ternary). Grep for the pattern
   grammar (value, relational, catch-all `_`) and the all-arms-same-type rule.
3. Decisions: **D-277** (switch-expression pattern grammar — value, relational,
   catch-all) and **D-301** (exhaustive expression vs non-exhaustive statement).
   Grep and confirm.
4. `docs/design/grob-error-codes.md` — the E05xx sub-block (Increment C
   registered E0501–E0504 there); this increment registers the
   non-exhaustive-switch error at the next free number.

> **Verify before relying on D-277, D-301, §3.1 and the code number.** Grep the
> log and the fundamentals; confirm the E05xx tail before assigning. If C
> registered a different range, use the real next-free number and note it.
>
> **Switch-expression rules — inline reference (authoritative source is §3.1,
> D-277, D-301).**
>
> - **`value switch { p1 => r1, p2 => r2, _ => rd }`.** An **expression**: it
>   produces a value, usable anywhere an expression is (`x := y switch { … }`).
>   Each arm is `pattern => result`.
> - **Pattern grammar (D-277).** Value patterns (`1 => …`), relational patterns
>   (`> 10 => …` if the grammar admits them — confirm in §3.1), and the
>   catch-all `_`. Use only the forms §3.1/D-277 define; do not invent pattern
>   syntax.
> - **Exhaustiveness enforced (the structural piece).** The type checker proves
>   every possible subject value is covered — in practice via a catch-all `_`
>   arm, or (for a finitely-valued subject the type system can enumerate) full
>   coverage. A non-exhaustive switch expression is a **compile error**. This is
>   the hard difference from `select`, which is non-exhaustive by design.
> - **All arms unify to one type.** Every `result` unifies to a single type,
>   which is the `ResolvedType` of the whole switch expression — **reuse
>   Increment A's ternary arm-unification**; it is the same mechanism. Arms that
>   do not unify are a compile error.
> - **Compilation — jump-based.** Evaluate the subject; test each arm's pattern
>   in order; the first match evaluates its result and `Jump`s to the end; the
>   `_` arm is the fall-through tail. Backpatch through `patchJump`. Like a
>   `select` ladder, but every path leaves exactly one value on the stack (it is
>   an expression).
>
> **Sequencing note.** This is Increment E: A → B → C → D → **E (switch
> expression)** → F (close). It reuses A's arm-unification and jump emission and
> D's ladder shape, but adds exhaustiveness (type checker) and the
> leave-a-value discipline. Do not pull the calculator close (F) forward.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the pattern type-checking,
   the **exhaustiveness algorithm** (the piece to get right), the arm
   unification (reused from A), the jump-based value-leaving emission, the
   E0505 registration, and the tests — and wait for approval before editing.
2. On approval, `/start-branch` → propose `feat/switch-expression`. Wait for the
   branch.
3. TDD — tests confirmed **failing** first.
4. `/commit-message` against staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop and surface.

## What is already done

- **Sprints 1–3 and Sprint 4 A–D:** the full pipeline through `select`, the
  ternary arm-unification (A), the conditional jump ladder (A), the loop and
  loop-context model (B), `for...in` (C), the `select` equality ladder (D). All
  green.

## Deliverable for this increment

The switch expression across the type checker and the compiler.

1. **Type checker — patterns, exhaustiveness, unification.** Type-check each arm
   pattern against the subject type (per D-277); **prove exhaustiveness** and
   make a non-exhaustive switch expression a compile error (**E0505** or the real
   next-free E05xx); unify all arm results to one `ResolvedType` (reuse A's
   ternary unification), with non-unifying arms a compile error. The §3.1.1
   invariant holds.
2. **Compiler — jump-based emission.** Subject evaluated; arm patterns tested in
   order; first match leaves its result on the stack and `Jump`s to the end; the
   `_` arm is the tail. Every path leaves exactly one value (it is an
   expression). Backpatch through `patchJump`.
3. **Diagnostics — register the non-exhaustive-switch code** (E0505 or real
   next-free) in `grob-error-codes.md` (index row + full entry, ADR-0014) and the
   `ErrorCatalog`. The `"Exxxx"` string appears exactly once; arm-unification
   mismatches reuse A's confirmed code; nothing invented.

No new opcodes, no parser/AST edits.

## Out of scope

The `select` statement (Increment D — already delivered; the expression is
distinct). The calculator close (Increment F). Pattern forms beyond §3.1/D-277.
Flow narrowing (Sprint 5). Do not edit the parser, AST or `OpCode` enum.

## Tests

Per §3.5.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A non-exhaustive switch expression (no `_`, not full coverage) is a compile
    error (E0505) with a source location.
  - An exhaustive switch expression (with `_`, or full enumerated coverage)
    type-checks; its `ResolvedType` is the unified arm type; non-unifying arms
    are a compile error.
  - The pattern forms of D-277 (value, relational if admitted, catch-all)
    type-check correctly; an invalid pattern type is a compile error.
  - The emission disassembles to the jump ladder, and **every path leaves
    exactly one value** on the stack (assert stack height at the end of each
    arm's path).
  - §3.1.1 invariant holds.
- **VM tests (`Grob.Vm.Tests`):**
  - `x := n switch { 1 => "one", 2 => "two", _ => "many" }` yields the right
    string for each `n`, including the `_` fall-through.
  - The result is usable as an expression (assigned, printed, passed onward).
- **Integration tests (`Grob.Integration.Tests`):**
  - A script binding a variable from a switch expression and printing it runs
    end-to-end and produces the expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green.
- The switch expression produces a value, is exhaustive (non-exhaustive is
  E0505), and all arms unify to its `ResolvedType`.
- Every compiled path leaves exactly one value on the stack (verified by
  disassembly / stack-height assertion).
- E0505 registered in the registry and the catalog; arm-unification reuses A's
  code; nothing invented.
- The §3.1.1 invariant holds; the DAG holds; no parser/AST/enum edits.

## Model

Sonnet 4.6 (High) throughout. The emission reuses A's jump ladder and D's
shape, and the unification is A's. The one piece needing care is the
**exhaustiveness algorithm** — for v1 this is "a `_` arm makes it exhaustive,
otherwise prove full coverage for an enumerable subject type"; confirm §3.1's
exact requirement and match it. Reach for the Opus subagent only if the
coverage proof for a finitely-valued subject turns out to need a judgement call;
the trigger is that specific check, not "exhaustiveness is hard".

## Hand-off

Summarise: the pattern type-checking, the exhaustiveness algorithm as built,
the arm-unification reuse, the jump-based leave-a-value emission (and the
stack-height discipline that keeps it an expression), the E0505 registration,
and the test files added. Note for the next chat: Increment F closes Sprint 4 —
the loop-and-`select` `calculator.grob` smoke script over the Sprint 1–4 surface
(no user functions; functions are Sprint 5), the §5 control-flow acceptance, and
the second VM-execution benchmark baseline via the `benchmark.yml` workflow
(D-309) checked against the two-axis gate (D-313).

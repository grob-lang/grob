# Sprint 9 — Increment A4b: compound assignment and increment/decrement on member (field) targets

**Branch:** `feat/member-target-compound-assign`
**One concern:** emit `obj.field op= v` and `obj.field++` / `obj.field--` (including chained
`a.b.c` targets), closing the field-target silent-drop D-359 confirmed. Nothing else.

Runs against the fresh corpus zip carrying D-356 through D-359. Corpus-first discipline
throughout — read the live decisions log and registry tails; do not rely on this prompt or
memory for D-### numbers or error codes.

---

## Authority and context

- **The spec already mandates this.** `grob-language-fundamentals.md` §28
  ("Assignment — `=` and compound") lists a struct field access (`obj.field`) as a valid
  assignable target and states `x += y` lowers to `x = x + y`; §28 ("Increment and
  decrement") lowers `i++` to `i = i + 1`, int-only, postfix-only, non-`const`. The gap is
  in the compiler, not the spec.
- **D-359 confirmed the gap and corrected the A4 kickoff's error.** Field-target compound
  assignment and increment (`obj.field += v`, `obj.field++`) hit the identical
  `is not IdentifierExpr` guard in `Compiler.Statements.cs` / `TypeChecker.Statements.cs`
  that A4 removed for index targets, and are **silently dropped today** — RHS side effects
  included. D-359 explicitly records that plain `obj.field = v` *does* work and evaluates
  its receiver once, but the compound and increment forms on that same target **do not** —
  so plain assignment is **not** a read-modify-write precedent to mirror. Build by adapting
  A4's index-target read-modify-write shape (D-359), minus the index subexpression.
- **A4 is the direct precedent to reuse.** Its evaluate-once temp-local emission
  (`DeclareLocalSlot` / `EmitGetLocal`, the `for...in` / switch-expression vehicle — **not**
  `_nextSlot`), the shared `EmitCompoundOperatorTypeCheck` (E0002), `FindReadonlyRoot`
  (E0204) and the `++`/`--` → `+= 1` / `-= 1` call-site lowering all apply here. Member
  targets are simpler: only the receiver is an expression to evaluate once; the field name
  is a static operand, so there is no index expression to stash.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Confirm on the live source tree and report:

1. **The deferred early-return** for compound assignment and increment on **member** targets
   (the same `is not IdentifierExpr` guard D-359 names). Confirm it emits nothing today.
2. **The member read/write opcodes and their operand shape.** Plain `obj.field = v` works —
   identify the read opcode (`GetField`/`GetProperty`/…) and write opcode
   (`SetField`/`SetProperty`/…), whether the field name is an inline operand, and the stack
   order the write expects (e.g. `[receiver, value]`).
3. **`GetExprType`'s `MemberAccessExpr` arm.** Does `GetExprType` read
   `MemberAccessExpr.ResolvedFieldType` to pick a typed binary opcode, or does it — as it did
   for `IndexExpr` before A4 — fall through to `Unknown`, which `EmitArithmetic` silently
   defaults to `Int`? If the arm is missing, `obj.floatField + 1.0` is **already** silently
   selecting `AddInt` in the plain binary-op path; adding the arm is **in-scope** (A4b's
   compound path needs the same typed-opcode selection — identical justification to A4's
   `IndexExpr` arm).
4. **`FindReadonlyRoot` on member chains.** Does it walk `MemberAccessExpr` chains so a
   `readonly` struct's field (`ro.field += v`) rejects with E0204? Plain `ro.field = v`
   already rejects — confirm the same root walk covers the compound path, extend it if not
   (in-scope, mirrors D-350).
5. **`ResolvedFieldType` availability** at the assignment site for the type check, and the
   existing non-int increment diagnostic (`float++`) for the int-only rule.
6. **`SetField` (write) leaves a clean stack** as a statement.
7. **Residual `GetExprType` gaps — observe and report only.** While in `GetExprType`, note
   whether `CallExpr` (`floatFn() + 1.0`) or any other arithmetic-operand node kind also
   lacks an arm and hits the `Unknown`→`Int` default. **Do not fix these here** — they are a
   separate concern (the GetExprType completeness audit). List them for scheduling.

Report the emission design, the type-check plan, the E0002/E0204 reuse, the `GetExprType`
member-access decision, any residual `GetExprType` gaps, and the test list. Then STOP.

---

## Emission design — recommended, confirm or adjust in plan

`receiver.field op= rhs` and `receiver.field++` / `--` share one path.
`obj.field++` ≡ `obj.field += 1`; `obj.field--` ≡ `obj.field -= 1` (int literal `1`, applied
at the call site through the same helper A4 used — no dedicated member fast path). Evaluate
the receiver **exactly once**:

1. Emit receiver → stash in a reserved temp local (`Ra`) via `DeclareLocalSlot` /
   `EmitGetLocal` in its own scope (A4's vehicle).
2. `GetLocal Ra` — the eventual write's receiver operand, pushed first so it sits below the
   result.
3. `GetLocal Ra`, `GetField field` — read the current value onto the stack.
4. Emit `rhs` (or the int literal `1` for `++` / `--`).
5. Emit the typed binary op (`Add`/`Sub`/`Mul`/`Div`/`Mod`), selected via `GetExprType` on
   `receiver.field` (step 3 of the gate) — the same typed-opcode selection the plain binary
   operator uses.
6. `SetField field` — pops `[Ra, result]`, writes `receiver.field = result`.

Release `Ra` with the existing `EmitScopeCleanup` (`PopN` — a compiler-synthesised temp name
is never lambda-capturable); this is a **statement** and must leave nothing on the stack.
Chained targets (`a.b.c op= v`) fall out for free — the receiver expression is `a.b`,
evaluated once to `Ra` by step 1's ordinary emission. **No new opcode** (adapt the exact
member read/write opcodes plain assignment already uses; confirm exact operand order in the
gate and adjust steps 2–6 to match).

---

## Type checking

- **Field type** comes from `MemberAccessExpr.ResolvedFieldType` set by the checker's member
  path. `ResolvedFieldType op rhs` type-checks by §28's underlying-binary-operator rules via
  the shared `EmitCompoundOperatorTypeCheck` (**E0002**), the identical rule A4's index and
  the pre-existing identifier paths apply; the result must be assignable to the field type.
- **`++` / `--`** require the field type to be `int` (§28 int-only rule) — `obj.floatField++`
  and `obj.strField++` are compile errors reusing the existing non-int increment diagnostic.
- **`readonly`-rooted target:** reject with **E0204** via `FindReadonlyRoot` (D-350), walking
  the member chain. `const`-rooted is unreachable as D-350 established — add no `const` path.

No new opcode, no new error code; count stays at 118.

---

## Scope boundaries — do NOT

- **No new opcode.** Reuse the member read/write opcodes plain `obj.field = v` uses, plus the
  temp-local and typed-arithmetic opcodes. If plan-mode finds an opcode unavoidable, STOP and
  escalate — do not grow the closed `OpCode` enum without the `adding-an-opcode` procedure and
  an explicit decision.
- **No new error code.** Reuse E0002, E0204 and the non-int increment code. Count stays 118.
- **Do not fix residual `GetExprType` gaps** (`CallExpr` and any others) — report them for the
  separate GetExprType completeness audit. One concern per branch.
- **Do not touch** index-target compound assignment (A4, landed) or the named-type registry
  (C0c).
- Fix the `MemberAccessExpr` `GetExprType` arm **only** because A4b's own typed-opcode
  selection depends on it — that is the single in-scope `GetExprType` change.

---

## Tests — TDD, red first, in the same commit as the feature

At minimum, mirroring D-359's A4 test surface:

- `obj.field op= v` for each of `+= -= *= /= %=` on an `int` field and a `float` field
  (int→float widening where §28 permits).
- `obj.field++` / `obj.field--` on an `int` field; compile error on `float` and `string`
  fields.
- **Evaluate-once (load-bearing):** `getObj().field += 1` evaluates `getObj()` exactly once
  (assert via a side-effect counter function observed to run once, the A4 pattern).
- Chained: `a.b.c += v`, `a.b.c++`.
- `readonly` struct field target → E0204.
- Div/mod by zero via `/=` / `%=` → the inherited runtime error (`E5001`/`E5002` per D-359's
  A4 paths); overflow via `+=` near `int` max → `ArithmeticError`.
- **`GetExprType` member-access regression:** `obj.floatField + 1.0` (a *plain* binary
  expression, not a compound assignment) selects `AddFloat`, not `AddInt` — the member-target
  analogue of A4's `floatArr[i] + 1.0` regression test.
- Compiler-side bytecode-shape and diagnostic tests in `Grob.Compiler.Tests` (hand-built AST
  where a struct literal is inconvenient, per the established convention); end-to-end tests in
  `Grob.Integration.Tests` through the CLI's top-level diagnostic formatting.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and
  pre-push (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never
  "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-360**; confirm, do not assume. The
  entry records: closes the field-target sibling silent-drop D-359 flagged; the evaluate-once
  member emission (receiver stashed once, static field operand, `GetField`/typed-op/`SetField`
  read-modify-write); type checking via `ResolvedFieldType` reusing E0002 and the non-int
  increment code, E0204 readonly reuse; the `GetExprType` `MemberAccessExpr` arm added in-scope
  (if it was missing) and the plain-arithmetic `obj.floatField + 1.0` mis-typing it fixed as
  a side effect; **no new opcode, no new error code**, count 118; and any residual
  `GetExprType` gaps (`CallExpr` etc.) named for the separate completeness audit. Cite
  D-359 / D-350 / D-351 and §28.
- If §28's compound-assignment note benefits from an explicit evaluate-once clarification now
  covering both index and field targets, add it citing the new D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.

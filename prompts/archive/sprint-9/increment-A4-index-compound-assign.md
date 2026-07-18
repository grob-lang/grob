# Sprint 9 — Increment A4: compound assignment and increment/decrement on index targets

**Branch:** `feat/array-index-compound-assign`
**One concern:** emit `arr[i] op= v` and `arr[i]++` / `arr[i]--` (with their map and
chained forms), closing the silent-drop gap D-350 named. Nothing else.

This session runs against the fresh corpus zip that already carries D-356, D-357 and
D-358 from the pre-9C planning session. Corpus-first discipline applies throughout —
read the live decisions log and registry tails, do not rely on this prompt or memory
for D-### numbers or error codes.

---

## Authority and context

- **The spec already mandates this.** `grob-language-fundamentals.md` §28
  ("Assignment — `=` and compound") lists `arr[i]` as a valid assignable target and
  states `x += y` lowers to `x = x + y`; §28 ("Increment and decrement") lowers `i++`
  to `i = i + 1`, int-only, postfix-only, non-`const`. The gap is in the compiler, not
  the spec — this increment brings the implementation in line with a spec that is
  already correct.
- **D-350 named the gap.** The `arr[i] = v` / `m[k] = v` landing record records that
  `arr[i] += v` and `arr[i]++` / `arr[i]--` "both silently drop emission today" via the
  deferred-index-target early-return in `Compiler.Statements.cs` — the statement
  compiles to nothing, RHS side effects included.
- **D-351 unblocks it.** `ArrayTypeDescriptor` gives `arr[i]` a real element type, which
  the read-modify-write needs to type-check `arr[i] op rhs`. D-351's own text records
  "Unblocks A4".
- **D-348 and D-350 supply the halves.** `GetIndex` read emission (D-348) and `SetIndex`
  write emission (D-350) exist and are correct; A4 composes them with evaluate-once
  semantics. No new VM primitive should be needed.
- **Chosen over the tourniquet.** The Sprint 9B principal review
  (`grob-principal-review-sprint9b.md`, F1b) recommended a stopgap diagnostic. That was
  superseded in pre-9C planning: no clean existing error code fits, so a tourniquet
  would burn a permanent code (ADR-0017) for a short-lived band-aid, and A4's
  dependencies are already shipped. Delivering the feature closes F1b permanently. Record
  this in the landing decision.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Confirm on the live source tree and report findings:

1. **The deferred early-return sites** for compound assignment and increment/decrement on
   index targets in `Compiler.Statements.cs` (D-350). Confirm they emit nothing today.
2. **The struct-field compound-assignment precedent.** Does `obj.field += v` /
   `obj.field++` already compile, and does it evaluate its receiver **once**
   (`getObj().field += 1` calls `getObj()` a single time)? If a sound read-modify-write
   pattern already exists for member targets (Sprint 6 struct-field assignment, the
   source of E0206), mirror it for index targets. If field-target compound assignment is
   itself broken (silent-drop or double-evaluation), **surface it as a sibling finding and
   name it for scheduling — do not fix it in this branch** (one concern per branch).
3. **The temp-local slot-reservation mechanism** (D-334's `_nextSlot` reservation pattern
   for `finally`) — the recommended vehicle for evaluate-once if no reusable member-target
   precedent exists.
4. **Element-type availability** at the assignment site via `Symbol.ArrayDescriptor`
   (D-351), for the type check.
5. **The binary-op type-mismatch code(s)** the compound form must reuse — the same code
   `arr[i] + v` already raises — and the existing non-int increment code (`float++`).
   Confirm no new code is required.
6. **`FindReadonlyRoot`'s IndexExpr-chain walk** (D-350) — reuse for E0204 on
   readonly-rooted index targets.
7. **`SetIndex` leaves a clean stack** as a statement (pops its three operands, pushes
   nothing).

Report the emission design (it should be opcode-free — see below), the test list and any
sibling finding, then STOP for approval.

---

## Emission design — recommended, confirm or adjust in plan

`target[index] op= rhs` and `target[index]++` / `--` share one path.
`arr[i]++` ≡ `arr[i] += 1`; `arr[i]--` ≡ `arr[i] -= 1` (int literal `1`). Evaluate the
receiver and index expressions **exactly once**:

1. Emit receiver → stash in a reserved temp local (`Ra`).
2. Emit index → stash in a reserved temp local (`Ia`).
3. `GetLocal Ra`, `GetLocal Ia` — the eventual `SetIndex` operands, pushed first so they
   sit below the result.
4. `GetLocal Ra`, `GetLocal Ia`, `GetIndex` — read the current value onto the stack.
5. Emit `rhs` (or the int literal `1` for `++` / `--`).
6. Emit the typed binary op (`Add`/`Sub`/`Mul`/`Div`/`Mod` per operand types — the same
   selection the plain binary operator uses).
7. `SetIndex` — pops `[Ra, Ia, result]`, writes `target[index] = result`.

Stack trace: after (4) `[Ra, Ia, value]`; after (5) `[Ra, Ia, value, rhs]`; after (6)
`[Ra, Ia, result]`; (7) consumes all three in `SetIndex`'s established `[receiver, index,
value]` order (D-350). Receiver and index run once (steps 1–2); the `GetLocal` re-reads
are side-effect-free. Chained targets (`matrix[r][c] op= v`) fall out for free — the
receiver expression is `matrix[r]`, evaluated once to `Ra` by step 1's ordinary emission.
**No new opcode.**

---

## Type checking

- **Arrays:** `arr[i]` has element type `E` via the descriptor (D-351). `E op rhs`
  type-checks by the underlying binary operator's §28 rules; the result must be assignable
  to `E`. `++` / `--` require `E` to be `int` (§28 int-only rule) — index increment on
  `float[]` / `string[]` is a compile error reusing the existing non-int increment
  diagnostic.
- **Maps:** value type is `Unknown` (MapTypeDescriptor is not built — F5-2 / C0b), so
  `m[k] op= v` and `m[k]++` stay **permissive** at compile time, mirroring D-350's
  permissive map-write RHS. This is the same honest, scoped gap D-350 and D-351 already
  carry — do not build ad-hoc map element typing here.
- **`readonly`-rooted target:** reject with **E0204** via `FindReadonlyRoot` (D-350).
  `const`-rooted is unreachable (D-289 bars collection literals as `const` RHS, as D-350
  established) — add no `const` path.

---

## Scope boundaries — do NOT

- **No new opcode.** The temp-local path is opcode-free. If plan-mode finds an opcode
  genuinely unavoidable, STOP and escalate — do not grow the closed `OpCode` enum without
  the `adding-an-opcode` procedure and an explicit decision.
- **No new error code.** Reuse the binary-op mismatch code, the non-int increment code and
  E0204. Count stays at 118.
- **No map element typing** — F5-2 / C0b owns it.
- **No touching the C0a array-method surface** (`append`/`insert`/`contains`/… — a separate
  increment).
- **Do not fix field-target compound assignment** if plan-mode finds it separately broken —
  surface it, name it, leave it. One concern per branch.

---

## Tests — TDD, red first, in the same commit as the feature

At minimum:

- `arr[i] op= v` for each of `+= -= *= /= %=` on `int[]` and `float[]` (int→float widening
  where §28 permits).
- `arr[i]++` / `arr[i]--` on `int[]`; compile error on `float[]` and `string[]`.
- **Evaluate-once (the load-bearing case):** `arr[sideEffect()] += 1` calls `sideEffect()`
  exactly once (assert via a counter or log); `getArray()[i] += 1` evaluates `getArray()`
  once.
- Chained: `matrix[r][c] += v`, `matrix[r][c]++`.
- Map: `m[k] += v`, `m[k]++` compile and run (permissive element).
- `readonly` array and map index target → E0204.
- Div/mod by zero via `/=` / `%=` → `RuntimeError` (D-278, inherited through the lowering).
- Overflow via `+=` on an `int` near `int` max → `ArithmeticError` (inherited).

Unit tests in the compiler and VM projects; gold-master anything belonging in the
integration smoke surface.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and
  pre-push (`tooling/coverage-gate.ps1`, 80% line floor) both green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never
  "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer
  changelog), D-### taken from the **live registry tail** — next free is **D-359**;
  confirm against the tail, do not assume. The entry records: implements §28's
  already-specified index-target compound and increment emission the compiler dropped
  (D-350 named the gap); the evaluate-once emission design that actually landed; type
  checking via `ArrayTypeDescriptor` (D-351), maps permissive; E0204 readonly reuse;
  **no new opcode, no new error code**; and that A4-as-feature was chosen over the F1b
  tourniquet from the Sprint 9B review, closing that finding by delivery. Cite
  D-348 / D-350 / D-351 and §28. If a field-target sibling gap was found, name it for
  scheduling.
- If §28's compound-assignment lowering note benefits from an explicit **evaluate-once**
  clarification for index (and field) targets, add it citing the new D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this
  prompt under `prompts/archive/sprint-9/`.

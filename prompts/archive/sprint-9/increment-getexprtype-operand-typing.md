# Sprint 9 — Increment: `GetExprType` arithmetic-operand completeness (silent `Int`-default sweep)

**Branch:** `fix/getexprtype-operand-typing`
**One concern:** close the `GetExprType` → `EmitArithmetic` `Unknown`→`Int` silent
wrong-opcode class for arithmetic operands, and harden the default so the class cannot
silently recur. No feature surface.

Runs against the fresh corpus zip carrying D-356 through D-361. Corpus-first discipline
throughout; read the live decisions log and registry tails, do not trust this prompt or
memory for D-### numbers or error codes.

---

## Authority and context

- **D-359** revealed the class: `GetExprType` had no `IndexExpr` arm and fell to `Unknown`,
  which `EmitArithmetic` silently defaults to `Int` — so `floatArr[i] + 1.0` selected
  `AddInt`, skipping the int→float promotion the spec requires. A4 fixed the `IndexExpr` arm
  as a side effect. A4b confirmed the `MemberAccessExpr` field arm already existed.
- **D-360** mapped the residue precisely and left it for this sweep: `CallExpr` only resolves
  a return type through a **direct `IdentifierExpr`-callee to a `FnDecl`**. It still falls to
  `Unknown`→`Int` for a call via **member access** (`obj.method()`), a **function-typed
  variable** (`fnVar()`), a **native/stdlib call** (`math.abs(x)`, `float.max(a, b)`), and
  for `StructConstructionExpr` and `LambdaExpr` operands.
- **These are reachable and produce silent wrong results.** `grob-type-registry.md` shows
  float-returning member and static calls — `float.round(decimals) → float`,
  `float.abs() → float`, `int.toFloat() → float`, `float.max/min/clamp → float`. Used
  directly as an arithmetic operand (`(3.14).round(1) + 1.0`, `someInt.toFloat() * 2.0`),
  each currently selects the int opcode and skips promotion (`grob-language-fundamentals.md`
  §: `int op float` promotes the int to float). The common assign-to-a-variable-first pattern
  resolves via the variable's type and sidesteps this, so blast radius is moderate — but the
  inline case is silently wrong, which is the class this whole sequence has been closing.
- **C0c unblocked the member-access-call half.** `NamedTypeRegistry` (D-361) now carries each
  nominal method's return type, so `someDate.addDays(1)` is resolvable; running this sweep
  after C0c lets it close all three `CallExpr` cases in one pass rather than deferring one.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Map on the live source tree and report:

1. **`GetExprType`'s current arm coverage** against the full set of expression node kinds that
   can be arithmetic operands. For each kind, where is its type resolvable at emission time —
   already resolved on the node, on a symbol, in `NamespaceRegistry` (D-342), in
   `NamedTypeRegistry` (D-361), in the built-in numeric/structural method tables, or genuinely
   not statically knowable?
2. **The four accidental `CallExpr` sub-cases** and their return-type source:
   - native/stdlib call → `NamespaceRegistry` return type;
   - function-typed variable call → the variable's function-type return;
   - nominal member method (`obj.method()` on a `date`/`guid`) → `NamedTypeRegistry` entry's
     method return type;
   - numeric/structural member method (`someFloat.round(2)`, `someInt.toFloat()`) → the
     built-in method table — locate it and confirm `GetExprType` can reach it without a new
     `Compiler`↔`Vm` edge.
3. **The genuinely-Unknown residue** (category 2, do **not** try to statically resolve): map
   elements before `MapTypeDescriptor` (C0b) and fields on an `Unknown`-typed receiver
   (untyped lambda parameter) — the permissive paths D-359/D-360 established. Confirm these are
   the *only* legitimate remaining `Unknown` sources at `EmitArithmetic` after the accidental
   gaps close (error-recovery nodes should never reach emission — confirm).
4. **`StructConstructionExpr` / `LambdaExpr` as arithmetic operands** — confirm the type checker
   already rejects `struct + num` / `lambda + num` (E0002) *before* opcode selection, so these
   never legitimately reach `EmitArithmetic`. If confirmed, they are category-1 (should never
   be `Unknown` here), not category 2.
5. **Whether the accidental-vs-deliberate `Unknown` distinction is cleanly expressible** at the
   `EmitArithmetic` fallback — can the deliberate permissive sites (map element, Unknown-receiver
   field) be identified so the fallback can hard-error on any *other* `Unknown` (a compiler bug)
   while routing the permissive ones through an explicit, documented int-assumption? If the
   separation is invasive, say so and propose the minimal viable hardening.

Report the arm-fill plan (source per case), the default-hardening approach and its feasibility,
the category-2 residue, and the test list. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

1. **Fill the accidental `CallExpr` arms** so each of the four sub-cases resolves its return
   type from the source identified in the gate. After this, a float-returning call used as an
   arithmetic operand selects `AddFloat` and promotes correctly, matching the spec's existing
   rules — this changes opcode *selection* only, never the promotion rules or the opcodes
   themselves.
2. **Harden the `Unknown` default.** Distinguish **accidental** `Unknown` (a node kind whose
   type is statically knowable — after (1), this should never occur; if it does it is a compiler
   defect) from **deliberate** `Unknown` (the identified permissive map-element and
   Unknown-receiver-field sites). Accidental `Unknown` at `EmitArithmetic` becomes a hard
   **internal compiler error** (fail loud, caught in tests) — not a user-facing diagnostic.
   Deliberate `Unknown` routes through an explicit, commented int-assumption that is documented
   as a known limitation closing when its type source lands. If the gate finds the clean
   separation invasive, apply the minimal viable form (fill the arms; assert where `Unknown`
   provably cannot legitimately occur) and record the residue.

---

## Scope boundaries — do NOT

- **Do not build `MapTypeDescriptor`** — that is C0b. The map-element float case is category-2
  residue that closes when C0b lands; document it, do not fix it here.
- **Do not build a runtime-polymorphic `Add`.** Dispatching arithmetic on the runtime value tag
  is an OQ-005-adjacent design decision, out of scope; raise it as a contingent future option
  only if category-2 float residue proves to matter after C0b.
- **Do not change the promotion rules or the typed opcodes.** This corrects opcode *selection*
  to match rules already specified in `grob-language-fundamentals.md` §.
- **No new user-facing error code** — correct opcode selection is not a diagnostic; the
  accidental-`Unknown` hardening is a compiler assert, not an `Exxxx`. Count stays 118.
- **No new opcode.**

---

## Tests — TDD, red first, same commit

- A **bytecode-shape lock plus an end-to-end value test** for each fixed sub-case, mirroring
  A4's `floatArr[i] + 1.0` and A4b's `obj.floatField + 1.0` regression locks:
  - native/stdlib call: `float.max(1.0, 2.0) + 1.0` selects `AddFloat` and yields the correct
    float;
  - function-typed variable call: a `float`-returning lambda in a variable, called inline as an
    operand;
  - nominal member method: a `date`/`guid` method result used in an inherited-numeric context
    (or, if none returns a numeric, a shape test that the arm resolves the nominal return type);
  - numeric member method: `(3.14159).round(2) + 1.0` and `someInt.toFloat() * 2.0` select the
    float path.
- The **hardening test**: a synthetic node with an accidental `Unknown` operand at
  `EmitArithmetic` triggers the internal compiler error (if that mechanism is built); the
  deliberate permissive map-element path still compiles and runs under its documented
  int-assumption.
- Confirm no existing arithmetic gold master changes except where a previously silent-wrong
  `AddInt` is now correctly `AddFloat` — each such change is a **fix**, called out in the PR,
  not a regression.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and
  pre-push (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never
  "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-362**; confirm, do not assume. The
  entry records: the `GetExprType` accidental `CallExpr` gaps closed (the four sub-cases and
  their return-type sources); the `EmitArithmetic` `Unknown` default hardened to fail loud on
  accidental `Unknown` while routing the documented permissive category-2 sites (map element
  pending C0b, Unknown-receiver field) through an explicit int-assumption; `StructConstructionExpr`/
  `LambdaExpr` confirmed type-error-unreachable as operands; the map-element float residue named
  as closing at C0b; and any `AddInt`→`AddFloat` gold-master corrections listed as fixes. No new
  opcode, no new error code, count 118. Cite D-360 (the gap report), D-359, D-356/D-342 (the
  registries consulted) and `grob-language-fundamentals.md` § (the promotion rules matched).
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.

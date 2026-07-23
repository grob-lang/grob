# Consolidation — Increment A1a: `int`/`float`/`bool` instance members

**Branch:** `feat/numeric-instance-methods`
**One concern:** deliver the documented instance-member surfaces for `int`, `float` and
`bool` on the existing primitive-member dispatch mechanism. Instance members only — the
type-static functions (`int.min`, `float.clamp`, …) are increment A1b.

Runs against the fresh corpus zip carrying D-356 through D-367. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust
this prompt or memory for D-### numbers or error codes.

---

## Authority and context

- **This is a release-gate blocker.** The advertised-vs-built corpus audit (July 2026)
  established that `PrimitiveMemberRegistry` registers members for `GrobType.String` **only**
  — `Int` and `Bool` appear there solely as *return* types, and no numeric or boolean
  **receiver** has a dispatch arm anywhere in `TypeChecker.Expressions.cs`. Meanwhile
  `grob-sample-scripts.md`'s release-gate validation scripts call:
  - line 215 — `size_mb: (f.size / 1024.0 / 1024.0).round(2)`
  - line 768 — `used_pct := ((d.used.toFloat() / total.toFloat()) * 100.0).round(1)`
  - line 140 — `year := file.modified.year.toString()` (`.year` is an `int` property;
    `toString` is registered for `string` and `guid` only)

  v1 cannot ship until these dispatch. This is the same class, and the same severity, as the
  `string`-methods gap closed by D-363 — it was simply never scheduled.
- **The mechanism already exists — this is surface population, not a new subsystem.** D-363
  built primitive-member dispatch (`PrimitiveMemberRegistry`, the `ResolveMemberAccessCall`
  primitive route, the `VisitCall` rewrite to a qualified native with the receiver as arg[0]).
  D-364 built the branch-agnostic `NativeDefaultArgumentFill`; D-365 wired it into the
  primitive-member branch and promoted `RequiredArgumentCount` to shared. Every piece this
  increment needs is proven.
- **`float.round` is resolved by decision, not by overloading.** The corpus documents
  `round() → int` **and** `round(decimals: int) → float` — arity-based overloading with a
  return-type change, which D-358 explicitly excludes and which the name-keyed registries
  cannot express. **Ratified resolution: split the names.** `round() → int` (nearest integer,
  preserving Grob's `floor`/`ceil` → `int` convention) and `roundTo(decimals: int) → float`
  (round to N decimal places). No overloading, both signatures unambiguous, no new mechanism.
  The two validation-script call sites and the corpus/wiki rows are updated as part of this
  increment. **[If this resolution was not ratified as described, STOP and escalate rather
  than improvising an alternative.]**

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **`PrimitiveMemberRegistry`'s structure** — confirm how `string`'s entries are keyed by
   receiver `GrobType`, and that adding `Int`, `Float` and `Bool` receiver blocks is purely
   additive (no shape change). Confirm `PrimitiveMemberProperty`/`PrimitiveMemberMethod`
   (including the `ParameterDefaults` field) need no modification.
2. **`StringMethodsPlugin`'s registration shape** — determine the runtime home for the new
   natives. A new `NumericMethodsPlugin` (covering `int`, `float`, `bool`) is the recommended
   split, keeping `StringMethodsPlugin` single-purpose; confirm the composition root wiring and
   that `PrimitiveMemberRegistryAgreementTests` picks it up automatically.
3. **Overlap with the `math` module.** `MathPlugin` registers 16 natives — report whether any
   (`abs`, `floor`, `ceil`, `round`) already implement the same operation as a module function.
   If so, the instance natives should **delegate to or share** the implementation rather than
   duplicating the maths, and any semantic divergence between the two surfaces must be
   reported, not silently resolved.
4. **`float.format(pattern)`** — present in `grob-type-registry.md`'s `float` section, absent
   from `wiki/Type-Registry/float.md`. Confirm, build it (the design corpus is authoritative),
   and note the wiki omission for the pending corpus sweep. Its `.NET` pattern strings (`"N2"`,
   `"F4"`, `"P1"`, `"E3"`) route to `double.ToString(pattern, CultureInfo.InvariantCulture)` —
   confirm the invariant-culture convention the existing natives use and match it.
5. **`toString()` semantics per receiver.** `bool.toString()` → `"true"`/`"false"` (lower-case,
   per the wiki). `int.toString()` and `float.toString()` — confirm the invariant-culture
   formatting the codebase already uses for value display, and match `ValueDisplay`'s rendering
   so `print(x)` and `x.toString()` cannot diverge. Report the existing convention.
6. **`float.toInt()` truncation vs overflow.** Documented as "Truncates — does not round". A
   `float` outside `int` range (`1e300.toInt()`, `NaN.toInt()`, `Infinity.toInt()`) must fault
   through the native seam as a catchable `GrobError`, not wrap silently or escape as a host
   exception — the D-366 "fails well" contract. Report the leaf/code to reuse
   (`ArithmeticError`/the existing checked-arithmetic code is the expected fit); **do not mint a
   new code inline** — if none fits, STOP and escalate via `allocating-an-error-code`.
7. **`CallExpr.ResolvedReturnType` (D-362)** — confirm the primitive-member arm already sets it,
   so a numeric-returning member used as an arithmetic operand (`x.abs() + 1`,
   `f.round() * 2`) selects the correct typed opcode. This is the D-362 machinery; verify it
   covers the new receivers without change.

Report the registry additions, the plugin home, any `math` overlap or divergence, the pinned
`toString`/`toInt` semantics, the fault leaf/code, and the test list. Then STOP.

---

## Surface to build

**`int` instance members** — `toString() → string`, `toFloat() → float` (always succeeds),
`abs() → int`.

**`float` instance members** — `toString() → string`, `toInt() → int` (truncates; faults out of
range), `round() → int` (nearest integer), `roundTo(decimals: int) → float`,
`floor() → int`, `ceil() → int`, `abs() → float`, `format(pattern: string) → string`.

**`bool` instance members** — `toString() → string` (`"true"`/`"false"`).

Semantics per `grob-type-registry.md` (authoritative) and `wiki/Type-Registry/{int,float,bool}.md`.
Where the two diverge, the design corpus wins and the divergence is reported.

**`abs()` on `int.MinValue`** — `-int.MinValue` is not representable. It must fault through the
seam as `ArithmeticError` (consistent with Grob's checked-arithmetic rule), not wrap to a
negative. Pin this in the gate and test it.

---

## Scope boundaries — do NOT

- **Do not build the type-static functions** (`int.min`, `int.max`, `int.clamp`, `float.min`,
  `float.max`, `float.clamp`). They are namespace-receiver calls, belong in `NamespaceRegistry`,
  and raise a distinct unresolved question (a **type name** in receiver position, `int.min(a, b)`
  versus `x: int` in annotation position). That is increment A1b.
- **Do not touch the `string` surface** (D-363/D-365, complete) or the `math` module's own
  functions.
- **Do not build array or map members** — C0a and C0b.
- **Do not add overload resolution.** The `round`/`roundTo` split exists precisely to avoid it.
- **No new opcode.** Reuse the `GetGlobal`+`Call` native rewrite.
- **No new error code** — reuse the checked-arithmetic/`ArithmeticError` code for the overflow
  paths. Count stays 118. If a genuinely new condition has no home, STOP and escalate.

---

## Tests — TDD, red first, same commit

- Each member: a type-checker resolution test, a compiler bytecode-shape test (rewrite to the
  correct qualified native, receiver as arg[0]), and an end-to-end value test through the CLI.
- **The release-gate unblock, asserted directly** — the three script shapes:
  `(f_size / 1024.0 / 1024.0).roundTo(2)`, `(used.toFloat() / total.toFloat() * 100.0).roundTo(1)`,
  and `someInt.toString()` compile and produce correct values.
- `roundTo` with its default omitted is **not** applicable (it has a required parameter) —
  assert `f.roundTo()` raises `E0003`, confirming no accidental default was introduced.
- Rounding semantics pinned: `round()` on `.5` boundaries (state and test the chosen rule —
  away-from-zero or banker's — matching whatever `MathPlugin` already does if it rounds),
  negative values, and `roundTo` at 0 and high decimal counts.
- Fault paths: `1e300.toInt()`, `NaN.toInt()`, `Infinity.toInt()`, `int.MinValue.abs()` each
  raise a **catchable** `GrobError` of the chosen leaf — assert catchable from Grob source via
  `try`/`catch`, not a host exception.
- `bool.toString()` → `"true"`/`"false"`; `float.toString()` and `print()` agree on the same
  value (the `ValueDisplay` consistency check).
- Numeric-return-as-operand: `x.abs() + 1` and `f.round() * 2` select the correct typed opcode
  (D-362 `ResolvedReturnType` wired for the new receivers).
- `PrimitiveMemberRegistryAgreementTests` passes with the new natives registered.
- Every existing `string`-member and `math`-module test unchanged.

---

## Also in scope — the `round` rename's corpus consequences

- `grob-type-registry.md`'s `float` section: `round(decimals: int)` becomes
  `roundTo(decimals: int)`, citing the ratified decision.
- `wiki/Type-Registry/float.md`: same row change, plus add the missing `format(pattern)` row.
- `grob-sample-scripts.md` lines 215 and 768: `.round(2)` → `.roundTo(2)`, `.round(1)` →
  `.roundTo(1)`.
- Grep the corpus for any other `.round(` call site with an argument and update it; report the
  full list rather than assuming these are the only three.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail**; confirm the next free number, do not assume. The entry
  records: the `int`/`float`/`bool` instance surfaces delivered on D-363's mechanism, closing the
  second release-gate blocker the advertised-vs-built audit found; the `round`/`roundTo` split as
  implemented (and that it avoids arity overloading D-358 excludes); the runtime plugin home; any
  `math`-module overlap or divergence found and how it was resolved; the pinned `toString`,
  truncation, rounding-boundary and overflow-fault semantics; `float.format` built and the wiki
  omission noted for the sweep; error codes reused, count 118; no new opcode. Cite D-363, D-364,
  D-365, D-362, D-366, D-284, D-066, and the audit.
- **Update `grob-type-registry.md`** — the `int`, `float` and `bool` build-status notes now read
  as built, citing this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages,
  updated sample scripts). Archive this prompt under `prompts/archive/sprint-9/`.

# Consolidation — Increment C0a-1: array non-mutating member surface

**Branch:** `feat/array-query-members`
**One concern:** deliver the array surface that reads — `length`, `isEmpty`, `first()`,
`last()`, `contains(v)` — and complete `sort`'s advertised `Comparable` key set with its
missing `date` and `guid` arms. The four mutating members (`append`, `insert`, `remove`,
`clear`) and their `const`/`readonly` rejection are increment C0a-2.

Runs against the fresh corpus zip carrying D-356 through D-370. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust
this prompt or memory for D-### numbers or error codes.

---

## Authority and context

- **The gap.** `grob-type-registry.md`'s `T[]` section documents **thirteen** members. Only
  **four** dispatch: `TypeChecker.Expressions.cs`'s `IsArrayHigherOrderMethod` accepts `filter`,
  `select`, `sort` and `each`. The other **nine** are documented as shipped surface and do not
  compile — **five** delivered by this increment (`length`, `isEmpty`, `first`, `last`,
  `contains`) and **four** by C0a-2 (`append`, `insert`, `remove`, `clear`). Found by the
  advertised-vs-built corpus audit. Verify this count against the live registry section before
  building: the surface is the registry's, not this prompt's.
- **`sort`'s key set is incomplete in a way the corpus contradicts.** The registry states
  `sort<U: Comparable>` where *"U must be `int`, `float`, `string`, `date`, `guid`, or `bool`"*.
  `GrobValueComparer` (`Grob.Vm/ArrayNatives.cs`) handles `Int`, `Float`, `String` and `Bool`
  only; every other kind — including the `Struct`-discriminated `date` and `guid` — falls to a
  throw reading *"sort key type Struct does not implement Comparable"*. Sorting by a `date` key,
  the single most likely real-world sort in a file-and-log scripting language, faults at runtime
  with a message that contradicts the documentation.
- **The `date` arm must use D-367's instant basis.** D-367 made `date` equality and ordering
  instant-based (`DateTimeOffset` comparison via `DateNatives.ToDateTimeOffset`), deliberately
  not the round-trip `__value` string. If `sort`'s new `date` arm compared `__value` strings it
  would order dates *differently from `<`* — the same trichotomy incoherence D-367 exists to
  remove, reintroduced through a second comparison path. `guid` stays ordinal on its canonical
  string, consistent with D-357's deliberate decision to leave `guid` field-by-field.
- **Arrays are structural, not nominal.** They carry an `ArrayTypeDescriptor` (D-351) and are
  explicitly excluded from `NamedTypeRegistry` (D-356) and from `PrimitiveMemberRegistry`
  (D-363), because their member signatures are **generic in `T`** — `first() → T?` and
  `contains(v: T) → bool` depend on the receiver's element type. This is the structural reason
  arrays could not simply join the registry the numeric surface used.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **The current array dispatch path** — `IsArrayHigherOrderMethod` and its type-checker arm, and
   the `ArrayNatives` receiver-bound native table (`"sort" => new NativeFunction(...)`). Report
   how a member is resolved, type-checked and emitted today, end to end for one example.
2. **How generic return types are computed.** `first()` returns `T?` and `contains(v: T)` takes a
   `T`, both derived from the receiver's `ArrayTypeDescriptor` (D-351). Confirm the descriptor is
   available at the member-resolution site and report how `select<U>` already computes its
   result element type — that is the existing precedent for a `T`-dependent signature and should
   be mirrored rather than reinvented.
3. **Property access on an array.** `length` and `isEmpty` are **properties**, not methods.
   Report how property access is resolved for a registered receiver (the
   `ValidatePrimitiveMemberCall` property twin, D-369) and how an array receiver resolves a
   property today.
4. **THE KNOWN TRAP — the permissive-`Unknown` property fall-through.** D-369 records that
   `array` is *still unregistered*, and that a pre-existing `CompilerNullableTests` case
   (`PlainDot_DoesNotEmitIsNilOrJumps`) was **moved onto an `array` receiver** precisely because
   it still exercises the generic permissive-`Unknown` property-access fall-through — after
   `int` became registered and started raising `E1002`. Registering array properties breaks that
   test's premise a second time. Report: what the fall-through does for arrays today (does
   `arr.length` currently compile permissively and yield `Unknown`?); which receiver kinds still
   legitimately fall through after this increment (`map`, until C0b); and how the test should be
   re-anchored. **Do not silently retarget it — report the reasoning.** If no receiver kind will
   remain, say so, because that changes the fall-through from a live path to dead code and is a
   finding in its own right (it interacts with D-362's enumerated permissive-`Unknown` sources).
5. **`contains(v: T)` equality semantics.** It must use the *same* equality the `==` operator
   uses, including D-367's instant-based `date` equality and D-169's field-by-field structs.
   Report the existing equality entry point and confirm `contains` can reuse it rather than
   open-coding a comparison. A `date[]` where `contains` disagreed with `==` would be the
   trichotomy bug in a new place.
6. **`first()`/`last()` nullable returns.** They return `T?`. Report how a nullable-returning
   member is typed and how the existing nullable machinery (`??`, `?.`, the nil checks) handles
   it, so an empty array yields nil rather than faulting.
7. **`GrobValueComparer`'s fault code.** It raises `E0004` today for an uncomparable key. Confirm
   whether `E0004` (an argument-type code) remains the right code once `date`/`guid` are valid
   and the remaining uncomparable kinds are arrays, maps and user structs. Report; do not mint a
   new code — if the taxonomy is genuinely wrong, name it as a finding for the pending
   correctness batch rather than fixing it here.

Report the dispatch design, the generic-signature mechanism, the property path, the
fall-through re-anchoring, the equality reuse, and the test list. Then STOP.

---

## Surface to build

| `length` | property | `→ int` |
| `isEmpty` | property | `→ bool` |
| `first()` | method | `→ T?` — nil if empty |
| `last()` | method | `→ T?` — nil if empty |
| `contains(v: T)` | method | `→ bool` |

Plus **`sort`'s missing key arms**: `date` (instant-based, via the same
`DateNatives.ToDateTimeOffset` path D-367's ordering uses) and `guid` (ordinal on the canonical
string). After this, `GrobValueComparer` covers exactly the six kinds the corpus advertises as
`Comparable`, and no more.

---

## Scope boundaries — do NOT

- **Do not build `append`, `insert`, `remove` or `clear`**, and do not build the `const`/
  `readonly` mutation rejection. That is C0a-2 — a genuinely different concern (in-place
  mutation, binding-immutability enforcement, aliasing semantics) with its own design surface.
- **Do not build map members** — C0b.
- **Do not change `filter`, `select`, `each` or `sort`'s existing behaviour** beyond adding the
  two comparer arms. No reordering, no stability change — `sort` is documented **stable** and
  the LINQ `OrderBy` that guarantees it must not be swapped.
- **Do not route arrays through `PrimitiveMemberRegistry` or `NamedTypeRegistry`.** They are
  structural by decision (D-351, D-356, D-363); their generic signatures are exactly why.
- **Do not weaken or retarget any agreement test to go green** — the A1b precedent (D-370)
  stands: reconcile so both directions still catch drift, and report the reconciliation.
- **No new opcode. No new error code** — count stays 118. If a genuinely new condition has no
  home, STOP and escalate via `allocating-an-error-code`.

---

## Tests — TDD, red first, same commit

- Each of the five members: type-checker resolution, compiler bytecode-shape, and end-to-end
  value tests.
- **Generic typing proven**, not assumed: `first()` on an `int[]` types as `int?` and on a
  `string[]` as `string?`; `contains` rejects a wrong-typed argument (`intArray.contains("x")`)
  with the existing argument-type code.
- **Empty-array behaviour**: `first()`/`last()` on `[]` return nil and are safely consumable via
  `??` and `?.`; `length` is 0; `isEmpty` is true.
- **`contains` equality parity — the load-bearing test:** for a `date[]` containing a value `a`,
  `arr.contains(a.toUtc())` is **true** (same instant, different offset), matching D-367's `==`.
  A `contains` that disagreed with `==` would be the trichotomy bug in a new place.
- **`sort` by `date` key**, including values at differing offsets — ordering must match what `<`
  gives for the same pairs. Assert against the `LessDate` semantics directly, so the two
  comparison paths cannot drift.
- **`sort` by `guid` key** — ordinal on the canonical string, stable.
- **`sort` stability preserved** — equal keys retain input order (lock the existing guarantee).
- **`sort` by a still-uncomparable key** (an array, a map, a user struct) raises the existing
  fault with an accurate message.
- The re-anchored `CompilerNullableTests` case still asserts what it was written to assert, on
  whatever receiver the gate justifies.
- Every existing array, `string`, numeric and `math` test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-371**; confirm, do not assume. The
  entry records: the five non-mutating array members delivered and the dispatch mechanism used
  (and why arrays stay structural rather than joining either registry); how generic `T`-dependent
  signatures are computed from `ArrayTypeDescriptor`; `sort`'s `date` and `guid` key arms added,
  with the `date` arm explicitly on D-367's instant basis so `sort` and `<` cannot diverge;
  `contains`'s equality reuse and the `date` parity it guarantees; the permissive-`Unknown`
  property fall-through's status after array registration, which receivers still use it, and how
  the `CompilerNullableTests` case was re-anchored; the `GrobValueComparer` fault-code finding if
  any; and that C0a-2 owns the mutating half. No new opcode, no new error code, count 118. Cite
  D-351, D-356, D-363, D-367, D-357, D-169, D-362, D-369, and the advertised-vs-built audit.
- **Update `grob-type-registry.md`** — the `T[]` build-status note records the non-mutating
  surface and `sort`'s full key set as built, the mutating members as pending C0a-2, citing this
  D-###. Update `wiki/Type-Registry/array.md` to match.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages).
  Archive this prompt under `prompts/archive/sprint-9/`.

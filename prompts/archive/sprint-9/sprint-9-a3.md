---
description: "Sprint 9 · Increment A3 — investigate and fix array element-type tracking. `GrobType.Array` carries no element type, so `int[]` and `string[]` are indistinguishable to the checker (surfaced by A2). Investigate the full reach FIRST (read-only), then thread an element type through GrobType.Array — literal inference, index read/write typing, for-in, mutation methods, signatures, mapAs<T[]> — mirroring the map<K,V> parameterisation precedent. Closes the A2 RHS-write gap and unblocks Increment D's typed-array deserialization. Load-bearing; prerequisite for D. If the reach exceeds one increment, surface a staged plan rather than cram."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment A3 — investigate and fix array element-type tracking

A2 surfaced the root gap behind the array troubles: `GrobType.Array` has **no element-type
tracking**, so `int[]` and `string[]` are the same type to the checker. That is why the
A2 RHS-write check (`arr[0] = "x"` on an `int[]`) could not be enforced, and it reaches
much further than index-write — array literals, index read, `for...in`, mutation methods
and function signatures all depend on an element type the type representation does not
carry. Maps do not have this gap: the checker distinguishes `map<string, int>` from
`map<string, string>` (D-112). Arrays were shipped on their syntax and never given a type
identity. This increment gives them one.

**This is an investigate-and-fix increment. The investigation is read-only and comes
first**, produces a findings report on the true state and the full reach, and gates the
fix behind explicit approval (the plan-mode gate). If the investigation shows the reach
exceeds what one increment can safely carry, **surface a staged plan** — do not force the
whole change into A3.

Read, in order:

1. `docs/design/grob-type-registry.md` — how `GrobType` represents built-in types, how
   `map<K, V>` carries its key/value type parameters (the precedent to mirror), and the
   constrained-generics model (D-080 — arrays and maps as consumed constrained generics).
2. `docs/design/grob-language-fundamentals.md` — array literals and their inference, index
   read/write, `for x in arr`, the array mutation methods (D-140 — `append`/`insert`/…),
   and function-parameter/return typing for `T[]`.
3. `docs/design/grob-vm-architecture.md` — the type checker's expression typing and the
   array index **read** emission (A/D-347) and **write** emission (A2/D-350) whose result
   and RHS typing depend on the element type.
4. Decisions: **D-112** (array/map indexing — the `map<K, V>` distinct-types precedent),
   **D-080** (constrained generics — arrays and maps consumed, not declared), **D-140**
   (array mutation methods), **D-291** (readonly deep immutability — corrected from the
   A2 mis-citation), **D-345**/**D-347**/**D-350** (the array indexer read/write emission
   this types), and any array-type decision the investigation surfaces. Grep the log for
   the next-free D-number (provisionally **D-351** — confirm against the live tail).

## Phase 1 — investigation (read-only, gates the fix)

Produce a findings report answering, with file/line evidence:

- **What does `GrobType.Array` carry today?** Is there an element-`GrobType` field at all,
  is it present but unpopulated for scalars, or absent entirely? Is the gap **scalar-only**
  (struct-element arrays tracked, scalars not) or **total** (no element parameterisation)?
  This is the single most important finding — it decides whether Increment D's
  `mapAs<Config[]>()` typed-array path is safe or undercut.
- **The reach.** Enumerate every site that needs the element type and its current
  behaviour:
  - array literal inference (`[1, 2, 3]` → `int[]`; heterogeneous `[1, "a"]` → error?);
  - index read result type (`arr[i]` → element type or `any`/unknown?);
  - index write RHS check (the A2 gap);
  - `for x in arr` iterator-variable type;
  - mutation-method argument checks (`arr.append(v)` — is `v` checked?);
  - function-parameter/return `T[]` enforcement at call sites;
  - `mapAs<T[]>()` (Increment D) — does it have an element type to target?
  - the **map** analogue — confirm maps do **not** share the gap (they should not, per
    D-112), so the map machinery is a clean precedent.
- **The struct-field twin (report-only — feeds Increment D).** The array gap is
  "shipped on syntax, invariant never carried". Its nearest twin sits directly under
  Increment D: `mapAs<Config>()` coerces a `json.Node` into a struct's **individually
  typed** fields. While in struct-element territory, confirm not just that a struct works
  *as* an array element, but that the struct's own fields are individually typed and
  checkable in the type registry — the exact invariant `mapAs<Config>()` will rely on.
  This is a **report** item, not part of A3's fix (A3 fixes array element typing). If the
  struct-field types are also uncarried or unchecked, that is the same class of gap under
  D — surface it with a recommended owner (its own increment before D, or D's front),
  scheduled not filed-and-forgotten.
- **The break risk.** Do any existing tests or the eleven validation scripts rely on
  loosely-typed or heterogeneous arrays that element typing would newly reject? List them
  — they are candidates for **quarantine with a documented reason** (not test-weakening),
  per the standing discipline.
- **The fix shape and size.** The recommended representation change (mirror `map<K, V>`),
  the threading work, the error-code disposition (reuse an existing mismatch code, or a
  new heterogeneous-array-literal code — a fold-versus-new call), and an honest size
  estimate. **If it exceeds one increment, propose the split** (e.g. representation +
  literal/read typing here; call-site and signature enforcement in a follow-up).

Stop at the end of Phase 1 for approval before any edit.

## Phase 2 — the fix (after approval)

> **The fix — inline reference.**
>
> - **Mirror the `map<K, V>` precedent.** Add an element `GrobType` to `GrobType.Array`
>   so `int[]` and `string[]` are distinct, exactly as `map<string, int>` and
>   `map<string, string>` already are. Do not invent a new mechanism where the map one
>   generalises.
> - **Infer at the literal.** `[1, 2, 3]` infers `int[]`; a heterogeneous literal is a
>   compile error (or the corpus's documented widening rule if one exists — confirm, do
>   not assume). An empty literal takes its type from context/annotation, consistent with
>   §9 (no uninitialised) and the empty-map precedent (D-112).
> - **Thread it through the sites the investigation named** — index read result, index
>   write RHS check (closing the A2 gap), `for...in` iterator type, mutation-method
>   arguments, and `T[]` signature enforcement at call sites — to the scope approved at
>   the end of Phase 1.
> - **Error codes.** Reuse the existing assignment/argument type-mismatch code for a
>   wrong element; if a heterogeneous-array-literal diagnostic needs its own code, that is
>   the `allocating-an-error-code` fold-versus-new call (D-331), at the next-free number,
>   count reconciled. Never a literal where an `ErrorCatalog` descriptor belongs (D-308).
> - **Quarantine, do not weaken.** Any fixture asserting loosely-typed array behaviour
>   that element typing now rejects is quarantined with a documented reason, not rewritten
>   to keep passing.
> - **No opcode growth.** This is type-system work; the read/write emission is unchanged.
>   If the element type changes what the compiler emits, that is a surfaced finding.

> **Dependency and sequencing.** A3 is a **prerequisite for Increment D** —
> `mapAs<Config[]>()` needs a real array element type to deserialize into. Run A3 **before
> D**. It also unblocks the compound-assignment work A2 scheduled (`arr[i] += v`,
> `arr[i]++`/`--`), which needs the element type for a well-typed read-modify-write — note
> it as the natural follow-up (an A4), do not fold it in here.

## What you're building

1. **The Phase-1 findings report** — the true state of `GrobType.Array`, the full reach,
   the break risk, the **struct-field twin** disposition (feeding Increment D) and the
   recommended fix shape and size, gating the fix.
2. **Element-type tracking on `GrobType.Array`** (Phase 2, to the approved scope) —
   mirroring `map<K, V>`, with literal inference and the sites threaded.
3. **The A2 RHS-write check closed** — `arr[0] = "x"` on an `int[]` now caught.
4. **Quarantine entries** for any fixture the change newly rejects, with documented
   reasons.
5. **The decision** — element-type tracking on arrays, at the next-free D-number in
   three-location lockstep, extending the array-type decisions and recording the
   compound-assignment follow-up as now-unblocked.

## Out of scope

The Sprint 9 stdlib modules. Compound assignment (`arr[i] += v`, `arr[i]++`/`--`) —
scheduled as the A4 follow-up now that A3 unblocks it. Any site the Phase-1 approval
places in a follow-up increment. Opcode changes.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - `int[]` and `string[]` are distinct types; a function taking `int[]` rejects a
    `string[]` argument.
  - `[1, 2, 3]` infers `int[]`; a heterogeneous literal is the documented outcome (error
    or widening — per the confirmed rule).
  - `arr[i]` reads at the element type; `arr[0] = "x"` on an `int[]` is the element-type
    mismatch (the A2 gap closed); `arr.append("x")` on an `int[]` is caught.
  - `for x in [1, 2, 3]` types `x` as `int`.
  - `mapAs<Config[]>()` has a concrete element type to target (the D dependency).
  - §3.1.1 holds on the affected nodes.
- **Regression / quarantine:** the eleven validation scripts and the existing suite run,
  with any newly-rejected loose-array fixture quarantined with a reason, not weakened.
- **Integration / spec-consistency:** D-316 green; the decision in lockstep; the count
  reconciled.

## Acceptance

- `GrobType.Array` carries an element type; `int[]` and `string[]` are distinct
  everywhere the approved scope covers.
- The A2 RHS-write check is enforced; array-literal inference, index read/write typing,
  `for...in` and mutation-method argument checks agree on the element type.
- `mapAs<T[]>()` has a concrete element type to deserialize into — Increment D unblocked.
- Any newly-rejected fixture is quarantined with a documented reason, not weakened.
- No opcode growth (or surfaced and approved); error codes reused or minted via the
  fold-versus-new call; the decision logged in lockstep; D-316 green; coverage at or above
  90% on the affected projects.
- If the reach exceeded one increment, the split was surfaced at Phase 1 and the deferred
  scope is scheduled, not silently dropped.

## Model

Sonnet 4.6 (High) drives the investigation and the threading. **If Phase 1 finds the
reach wide** — element typing touching signatures, call sites and `mapAs<T[]>()` together
— the **element-type-representation design** (how the element type is carried and threaded
without destabilising the checker) is the load-bearing sub-problem and a candidate for the
Opus 4.8 subagent, settled **before** Increment D since D depends on it. Make that call at
the end of Phase 1 on the evidence, not up front.

## Hand-off

Summarise: the Phase-1 findings (the true `GrobType.Array` state, the reach, the break
risk); the representation change and how it mirrored `map<K, V>`; the sites threaded and
the A2 gap closed; any scope placed in a follow-up and how it was scheduled; the
quarantine entries; the decision and its lockstep entry. Confirm Increment D is unblocked
— `mapAs<T[]>()` has a real element type — and note A4 (compound assignment) as the next
array follow-up. Arrays now have a type identity; record anything still owed so it is
scheduled, not left to slip again.

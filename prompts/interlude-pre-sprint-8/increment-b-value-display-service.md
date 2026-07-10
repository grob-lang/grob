# Pre-Sprint-8 Interlude — Increment B: `ValueDisplay` service

**Branch:** `feat/value-display-service`
**Authorises:** D-336
**Model:** **Opus** — this is the interlude's one load-bearing sub-problem. The
dispatch precedence is security-critical (wrong order leaks credentials) and cycle
handling must be correct (wrong handling overflows the stack). The consuming wiring
(Increment C) is mechanical and stays on Sonnet.
**Depends on:** none. The service is pure and has no consumer yet — that is deliberate.

## Why

Three artefacts disagree today (D-336): §13 mandates reference types call `.toString()`;
user `type`s have no `.toString()`; and `OpCode.Print` calls no Grob-level `.toString()`
at all. This increment builds the single renderer that resolves the divergence, as a pure
service with no VM wiring. Wiring is Increment C.

## Plan gate

Produce a numbered plan and stop for approval. The plan must report:

1. **The assembly-boundary answer.** `ValueDisplay` lives in `Grob.Runtime`. It needs the
   type registry to resolve a registered `toString()`. Confirm `Grob.Runtime` already owns
   or references the registry, and state the exact call it will use to ask "does this
   value's runtime type have a registered `toString()`, and what does it return". If the
   registry lives such that this creates a bad dependency edge, surface it before editing —
   do not invent a new coupling silently.
2. **The `GrobValue` accessor surface** (D-297): how to read `Kind`, the scalar payload,
   the boxed reference, and how a `GrobStruct` exposes its fields in declaration order,
   how a `GrobArray` exposes elements, how a `GrobMap` exposes entries, and how a
   `GrobFunction` exposes its parameter and return types for the type-signature render.
   Read these from the live code.
3. **Which types carry a registered `toString()`** today (`AuthHeader`, `ProcessResult`,
   `guid`, `date`, `json.Node`, `Response`, and any others in the registry). The security
   test uses at least `AuthHeader`.

Do not edit until approved.

## Scope

**In:** `Grob.Runtime` — new `ValueDisplay` type and its tests.

**Out:** `OpCode.Print`, string interpolation, `GrobValue.ToString()`, any `Grob.Vm`
change, any gold master, the four `Sprint6IncrementBTests`. This service is consumed by
nobody in this increment. If a test needs to invoke it, it invokes `ValueDisplay`
directly, not through `print`.

## The specification (D-336)

Two entry points. `Display(GrobValue)` is public-facing (top-level position). `Inspect`
is internal to `Grob.Runtime` (nested position). `toString()` remains the sole public
*method* on values; `Inspect` is not exposed as one.

### Dispatch precedence — the order is load-bearing

Resolve in this order. **Step 2 must precede step 5.**

1. `nil` → `nil` (in any position).
2. **The value's runtime type has a registered `toString()` → call it and return.
   Terminal.**
3. Scalars: `bool` → `true`/`false`; `int` → digits; `float` → see below.
4. `string` → position-dependent: **unquoted** under `Display`, **quoted and escaped**
   under `Inspect`.
5. `Function` → its type signature: `fn(): int`, `fn(int): int`, `fn(int, string): bool`.
6. Composites (`Struct`, `Array`, `Map`) → structural, source-shaped, recursing into
   elements via **`Inspect`**.

Why step 2 before step 5: per D-297 all plugin types and user `type`s share the `Struct`
discriminator. If the structural arm runs before the registry lookup, an `AuthHeader`
(which is `Kind == Struct`) is rendered field-by-field and its bearer token is printed.
D-159's credential guarantee currently holds only by the accident of the opaque
fall-through; this precedence makes it hold by design.

### Rendering forms

```
Config { host: "example.com", port: 8080 }     named struct type
#{ host: "example.com", port: 8080 }           anonymous struct literal (D-114)
[1, 2, 3]                                      array
{ "a": 1, "b": 2 }                             map
nil                                            nil
```

- Named vs anonymous: a named `type` prints its type name before the brace; an anonymous
  `#{ }` literal prints `#{`. Confirm at the plan gate how a `GrobStruct` records whether
  it is named, and its type name.
- Fields, elements, keys and values inside a composite are rendered via `Inspect`, so
  nested strings are quoted — this is what distinguishes `"8080"` from `8080`.
- Empty forms: `[]`, `{ }`, and a fieldless struct `Name { }`.

### `float` — round-trip, decimal point, invariant culture

- Always render round-trippable and **always carry a decimal point or exponent**:
  `print(1.0)` → `1.0`, never `1`. A `float` that renders as `1` is indistinguishable
  from an `int` in a statically typed language, the same defect class the `Inspect`
  quoting rule prevents.
- `0.1 + 0.2` → `0.30000000000000004` (honest shortest round-trippable, as Go and
  Python).
- **Every numeric conversion pins `CultureInfo.InvariantCulture`.** Unpinned,
  `double.ToString()` emits `1,5` on a `de-DE` or `fr-FR` host — squarely inside Grob's
  Windows sysadmin audience — silently breaking every gold master and writing commas
  into `formatAs.csv` fields, reproducible only on machines the maintainer does not own.
- Pinned spellings for `NaN`, `Infinity`, `-Infinity` (do not accept .NET's culture- or
  symbol-dependent defaults).

### Cycles and depth

- `E0301`/`E0302` reject only *non-terminating* type cycles, so `type Node { value: int,
  next: Node? }` is legal and `a.next = b; b.next = a` is constructible at runtime.
  `Inspect` therefore carries **reference-identity cycle detection** — a revisited object
  renders as `<cycle>`.
- A depth cap renders `...` as a backstop beyond a fixed nesting depth (state the constant
  in the plan; a small value such as 32 is ample for real scripts).
- The visited set is allocated **only when a composite nests**. Scalar and flat-struct
  paths must allocate nothing beyond the output buffer.

## TDD

1. **Red first**, one failing test per arm before implementing that arm. At minimum:
   - `Display(int/bool/nil)` exact forms.
   - `float`: `1.0` → `1.0`; `0.1+0.2` round-trip; `NaN`/`Infinity` pinned.
   - **Culture invariance:** set the ambient culture to `de-DE` inside the test and assert
     `Display(1.5)` is `1.5`, not `1,5`. This is the landmine test — it must exist.
   - `string`: `Display("hi")` → `hi`; `Inspect("hi")` → `"hi"` with escaping of `"` `\`
     `\n`.
   - `Function`: `fn(int): int` for the matching signature.
   - Struct named/anonymous, array, map, nested, empty forms.
   - **Security:** a value of a registered-`toString()` type (`AuthHeader` constructed
     with a known secret) is rendered via its registered method and the output **does not
     contain the secret**, in both `Display` and `Inspect` position. Assert step 2 wins
     over step 5.
   - Cycle: construct a cyclic struct, assert `<cycle>` appears and the call returns
     (no `StackOverflowException`).
   - Depth cap: construct nesting past the cap, assert `...`.
2. **Green.** Implement `ValueDisplay` to the precedence above.
3. **Refactor.** Single dispatch method shared by both entry points, position carried as
   a parameter or two thin public wrappers over one core.

### FsCheck properties

- **Culture invariance:** for any `float` and any culture drawn from a small set
  (`en-US`, `de-DE`, `fr-FR`, invariant), `Display(f)` is identical.
- **Termination and balance:** for any acyclic nested composite generated to bounded
  depth, `Inspect` terminates and produces balanced brackets and braces.
- **Determinism:** for any value, two `Display` calls return equal strings.

## Acceptance

- [ ] Every precedence arm has a red-first test that then passes.
- [ ] The security test proves a registered-`toString()` type is never structurally
      rendered and never leaks its payload.
- [ ] The `de-DE` culture-invariance test passes.
- [ ] The cycle test returns `<cycle>` without overflow; the depth cap renders `...`.
- [ ] FsCheck properties pass.
- [ ] `dotnet test` green at the solution root; the membership gate (Increment A, if
      merged) stays green.
- [ ] 90% line coverage on `ValueDisplay`.
- [ ] Error-code count unchanged at 116. No `OpCode`, `.grobc`, or `GrobValue` shape
      change (D-297 surface untouched — this reads values, it does not alter them).

## Commit

One commit. e.g. `feat(runtime): add ValueDisplay with Display/Inspect and security-ordered
dispatch (D-336)`. Body states the precedence, the credential-ordering constraint and the
culture pinning as the three things a reviewer must check.

## Guardrails

British English, no Oxford comma, never "simply". One concern. No consumer wired in this
increment — if the service is not yet reachable through `print`, that is correct and
Increment C's job. Do not touch the four `Sprint6IncrementBTests`; they are wrong in a way
Increment C fixes, and touching them here mixes concerns.

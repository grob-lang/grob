# Correctness batch — Increment: `map<K, V>?` does not exist

**Branch:** `feat/nullable-map-type`
**One concern:** make a nullable map a usable type. `m: map<string, int>? := nil` currently
fails to compile — `GrobType` has no `NullableMap` variant.

Runs against the fresh corpus zip carrying D-356 through D-400. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm against the live registry.

---

## Authority and the gap

**D-400 found this** while sweeping receiver kinds for the `?.` method-call fix. Trying to
reproduce the crash on a nullable map was impossible: `GrobType` carries `NullableArray`,
`NullableString`, `NullableInt`, `NullableFloat`, `NullableBool`, `NullableStruct`,
`NullableFunction` and `NullableAnonStruct` — **map is the only collection type with no nullable
variant** — so `m: map<string, int>? := nil` fails with `E0001` before any call site is reached.
D-400 correctly scoped this out and asserted no nullable-map behaviour in any of its tests.

**This is advertised surface, not an unbuilt convenience.** `grob-stdlib-reference.md` documents
`Grob.Http`'s entire request surface with a nullable map parameter:

```
http.get(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response
```

— and the same shape on `post`, `put`, `patch` and `delete`, ratified in the decisions log.
`Grob.Http` is Sprint 11, so nothing is broken today, but the documented signature **cannot
compile** as written. This is the fifth advertised-but-unbuilt instance the consolidation phase
has found, and the first in **type-variant coverage** — territory neither the advertised-vs-built
audit (members, stdlib functions, error codes) nor the grammar audit (literal, statement,
declaration and annotation forms) swept. `T?` is documented as a general suffix applying to any
type; the parser accepts `map<K, V>?` (D-326/D-327's suffix grammar is complete); only the type
system lacks the variant.

---

## The design question this increment must answer, not assume

Nullability is currently **enumerated per type** — one `GrobType` variant per nullable form.
Adding `NullableMap` follows that pattern and is the smallest change. But the pattern itself is
worth one paragraph of scrutiny before it grows again:

- **Why is map the only gap?** Report whether it was an oversight or a deliberate deferral
  recorded somewhere. If a decision deferred it, cite it; if nothing did, say so.
- **Does the enumeration scale?** Every future nullable-capable type needs another variant, and
  each new variant needs arms in the type checker, the VM, `ValueDisplay` and every `switch` over
  `GrobType`. Report roughly how many sites a new variant touches — that number is the honest
  cost of the pattern.
- **Is `GrobType` a closed surface?** Check whether it carries the same stability governance as
  `OpCode` (ADR-0013) and `GrobValueKind`. If adding a variant requires a procedure, follow it;
  if not, note that the enum has grown without one.

**Recommended: add `NullableMap`, matching the existing pattern.** A composed
nullable-as-a-wrapper redesign would touch every type-system site at once and is far outside a
correctness-batch increment. But if the gate finds the enumeration is already straining — for
instance, if a variant touches dozens of sites — **report that as a finding for a future
decision** rather than either redesigning here or staying silent.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce empirically first** (the D-366/D-380/D-400 precedent): confirm
   `m: map<string, int>? := nil` fails, and with which code. Confirm the parser accepts the
   annotation and the failure is in the type checker, not the grammar.
2. **Trace `NullableArray` end to end** — declaration, annotation resolution, assignment,
   `??`, `?.`, nil checks, VM dispatch, `ValueDisplay`, equality. **This is the template.**
   Report every site, because that list is both the implementation plan and the honest measure of
   what a variant costs.
3. **Descriptor interaction — the subtle part.** D-374's `MapTypeDescriptor` carries `V`, and
   D-377/D-378 built the member surface on it. Report how `ArrayTypeDescriptor` survives the
   nullable-array path today, and confirm a `map<string, int>?` still carries its `V` descriptor
   through `??`, `?.` and nil-guarded narrowing. **A nullable map that loses its value type would
   silently regress D-374's typing** — `(m ?? emptyMap)["k"]` must still type as `int?`.
4. **`?.` on a nullable map**, now that D-400 fixed the method-call short-circuit. `m?.get("k")`,
   `m?.length`, `m?.keys` must all short-circuit correctly. D-400's emission is
   receiver-type-agnostic, so this should follow for free — **verify rather than assume**, and
   report which of D-400's tests can be extended to cover it.
5. **`for k, v in` a nullable map** — is it a compile error (requiring `??` or a guard), as it
   presumably is for a nullable array? Report the nullable-array behaviour and mirror it.
6. **The `E0001` message** for the current failure. Once `NullableMap` exists, confirm no
   diagnostic still claims a nullable map is not a type.

Report the empirical reproduction, the `NullableArray` site list, the descriptor findings, the
`?.` verification, the `for...in` rule, and the test list. Then STOP.

---

## Scope boundaries — do NOT

- **Do not redesign nullability.** Add the missing variant matching the existing pattern; report
  any strain as a finding for a future decision.
- **Do not implement `Grob.Http`** — Sprint 11. This makes its documented signature *compilable*;
  it builds nothing.
- **Do not add nullable variants for types that do not have one and are not documented as
  nullable.** Only close the map gap. If the sweep finds *another* missing variant, **report it**
  — that would confirm a systematic gap rather than a one-off, and belongs in the corpus sweep.
- **Do not change `?.`, `??` or nil-check semantics** (D-400, and the fundamentals' nullable
  rules). This adds a type; it changes no rule.
- **Do not fix the other correctness-batch findings** — `Synchronise()`'s double diagnostic
  (D-376), `E5102`'s missing throw site (D-382), D-380's diagnostic-quality gaps.
- **No new opcode. No new error code** — count stays **121**. If the gate finds a genuinely new
  diagnostic condition, STOP and escalate via `allocating-an-error-code`.

---

## Tests — TDD, red first

- **The reported case:** `m: map<string, int>? := nil` compiles; assigning a real map to it
  compiles; both round-trip correctly.
- **Descriptor survival — load-bearing:** `(m ?? map<string,int>{})["k"]` types as `int?`, and
  `m?.get("k")` likewise. A nullable map that lost its `V` would silently undo D-374's typing,
  and this is the test that catches it.
- **`?.` on a nullable map**: `m?.length`, `m?.get("k")`, `m?.keys` each short-circuit to nil on
  a nil receiver and dispatch normally on a real one (extending D-400's coverage to the receiver
  kind it could not reach).
- **`??` unwrapping**: `m ?? map<string,int>{}` yields a non-nullable map usable without guards.
- **Nil-guard narrowing** behaves as it does for a nullable array — mirror whatever the gate
  found.
- **Using a nullable map unguarded** where a non-nullable one is required raises the existing
  nullable diagnostic — the same as a nullable array.
- **`for k, v in` a nullable map** behaves per the mirrored rule.
- **`ValueDisplay`**: `print(m)` on a nil nullable map renders as nil, and on a real one renders
  as the map — no new rendering path.
- **The `Grob.Http` signature shape compiles**: a user function declared
  `fn f(headers: map<string,string>? = nil): void` type-checks, since making that shape legal is
  the point of the increment.
- Every existing map, nullable, array, `?.` and `??` test unchanged.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-401**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the gap and that it was
  **advertised** by `Grob.Http`'s documented signature, making this the fifth advertised-but-
  unbuilt instance and the first in type-variant coverage; whether the omission was deliberate or
  an oversight, with a citation if one exists; **the site count a `GrobType` variant touches**,
  and whether the enumeration pattern is straining; `GrobType`'s governance status; the
  descriptor-survival result; the `?.` verification extending D-400; the mirrored `for...in` and
  narrowing rules; and any *other* missing variant found, named for the corpus sweep. No new
  opcode, no new error code, count 121. Cite D-400 (the finding), D-374, D-377, D-378, D-326/
  D-327 (the suffix grammar that already accepts it), and the `Grob.Http` signature decision.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.

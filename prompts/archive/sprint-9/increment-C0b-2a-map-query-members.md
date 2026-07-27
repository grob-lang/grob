# Consolidation — Increment C0b-2a: map query member surface

**Branch:** `feat/map-query-members`
**One concern:** deliver the six non-mutating map members — `length`, `isEmpty`, `keys`,
`values` (properties) and `get(key)`, `contains(key)` (methods). The three mutating members
(`set`, `remove`, `clear`) and their `readonly` rejection are increment C0b-2b.

Runs against the fresh corpus zip carrying D-356 through D-376. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this
prompt or memory for D-### numbers or error codes. **Note the error-code count is now 119**
(D-376 added E0016) — confirm against the live registry, do not assume 118.

---

## Authority and context

- **The gap.** `TypeChecker.Expressions.cs` has **no `GrobType.Map` member dispatch arm at
  all** — confirmed by D-371's investigation and unchanged since. All nine documented members
  are advertised in `grob-type-registry.md` and uncompilable. This is the last collection-surface
  finding from the advertised-vs-built audit (finding B2).
- **The substrate is complete.** D-374 built `MapTypeDescriptor` (carrying `V`; `K` is fixed
  `string` in v1) with `MapDescriptorOf(Expression)` recovery, and D-376 added its third tier so
  a `:=`-inferred binding from a map literal carries a real descriptor. Both the indexer and
  `for k, v in m` already type correctly.
- **Map literals now exist (D-376)** — so unlike D-374, which was forced onto hand-built AST and
  `fn`-parameter annotations, **this increment tests through real Grob source**. Use
  `map<string, int>{"a": 1}` literals in tests throughout; hand-built AST is no longer an
  acceptable substitute.
- **The array member surface is the precedent to mirror.** D-371 (query members: `length`,
  `isEmpty`, `first`, `last`, `contains`) and D-373 (mutating members) established the dispatch
  shape: a name allow-list (`IsArrayMethod`) plus a validator (`ValidateArrayMethodCall`), with
  receiver-bound natives in `ArrayNatives.cs`. Mirror it, do not invent a third pattern.
- **Insertion order is already guaranteed at the runtime layer.** `GrobMap` is backed by
  `OrderedDictionary<string, GrobValue>` and already exposes `InsertionOrderKeys`. The registry
  states `keys` is insertion-ordered and `values` matches `keys`. **Reuse the existing ordering;
  do not reimplement or re-sort it.**

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **The array member dispatch path end to end** — `IsArrayMethod`, `ValidateArrayMethodCall`,
   the property-access twin, and `ArrayNatives.cs`'s receiver-bound native table. Report the
   shape, and where a `GrobType.Map` arm slots into `ResolveMemberAccessCall` alongside the
   existing namespace, nominal, primitive and array routes.
2. **`keys → K[]` and `values → V[]` must construct populated `ArrayTypeDescriptor`s.** This is
   the increment's one genuinely novel piece: the two descriptor systems compose here. Report how
   to build an `ArrayTypeDescriptor` from the map's `K` (always `string` in v1) and `V`, and
   confirm the result flows through D-371/D-373's array member surface so `m.keys.first()`,
   `m.keys.length` and `m.values.contains(x)` all resolve. A bare or missing descriptor here
   silently degrades every downstream array member — verify, do not assume.
3. **`get(key)` must agree with the indexer.** D-374 types `m[k]` as `V?`; the registry documents
   `[key]` as *"sugar for `get(key)`"*. Confirm both produce the same type **and** the same value
   for present and absent keys. Report whether `get` should share the indexer's implementation
   path or be a separate native, and prefer sharing if the seam allows.
4. **THE KNOWN TRAP — `CompilerNullableTests` re-anchoring, third occurrence.** D-371 records
   that `PlainDot_DoesNotEmitIsNilOrJumps` was moved onto an **`env.all()`** receiver — which
   returns `map<string, string>` — precisely because map was the last unregistered receiver kind
   still exercising the permissive-`Unknown` **property-access** fall-through, after `int`
   (D-369) and `array` (D-371) each stopped qualifying. Registering `length`/`isEmpty`/`keys`/
   `values` as map properties breaks that anchor a third time.

   Report: which receiver kinds, if any, still legitimately hit that property fall-through after
   this increment; and how the test should be re-anchored. **If none remain, say so explicitly**
   — that makes the fall-through unreachable for valid input, which is a finding in its own right
   (it is the property-access analogue of D-362's enumerated permissive-`Unknown` operand
   sources) and belongs in the correctness batch, not fixed here. **Do not silently retarget the
   test; report the reasoning.**
5. **`contains(key: K)` is key-membership, not value-membership** — the array's `contains(v: T)`
   checks values (D-371). Same name, different subject, on two collection types. Confirm the map's
   is key-membership per the registry, and report it so the asymmetry is recorded as deliberate.
6. **`length`/`isEmpty`/`keys`/`values` are properties, not methods** — no parentheses. Confirm
   the property-access path (D-371's array-property precedent) handles a map receiver.
7. **Key-argument typing.** `get(key)` and `contains(key)` take `K`, fixed to `string` in v1.
   Report what a non-`string` argument raises (expected: the existing `E0004`) and confirm
   `ResolveMapValueDescriptor`'s deliberate, documented non-validation of the `K` type argument
   (D-374, unchanged by D-376) is not disturbed.

Report the dispatch design, the array-descriptor composition, the `get`/indexer sharing decision,
the fall-through re-anchoring analysis, and the test list. Then STOP.

---

## Surface to build

| `length` | property | `→ int` | Number of entries |
| `isEmpty` | property | `→ bool` | |
| `keys` | property | `→ K[]` | All keys. **Insertion order** — reuse `GrobMap.InsertionOrderKeys` |
| `values` | property | `→ V[]` | All values. **Order matches `keys`** |
| `get(key: K)` | method | `→ V?` | Nil if key absent |
| `contains(key: K)` | method | `→ bool` | True if **key** present |

Semantics per `grob-type-registry.md`'s `map<K, V>` section (authoritative).

---

## Scope boundaries — do NOT

- **Do not build `set`, `remove` or `clear`**, and do not build the `readonly` mutation
  rejection — that is C0b-2b, a separate concern (in-place mutation plus binding-immutability
  enforcement), mirroring the C0a-1/C0a-2 split.
- **Do not change the indexer or `for...in`** (D-350, D-374) or map-literal construction (D-376).
- **Do not support non-`string` keys** — v1 is `string`-only; the registry records non-string keys
  as post-MVP, and D-374's unvalidated `K` type argument stays as it is.
- **Do not reimplement ordering** — `GrobMap`'s `OrderedDictionary` already provides it.
- **Do not silently retarget `CompilerNullableTests`** — report the analysis first.
- **Do not fix correctness-batch findings** — the `E0004` sort-comparator taxonomy (D-371), the
  `readonly` binding-scope gap (D-372), `for...in`'s dynamic bound and unrecognised-member
  fall-through (D-373), the `Synchronise()` double-diagnostic (D-376's branch). Report anything
  new; fix none of them here.
- **No new opcode** — `keys`/`values` allocate their arrays inside the native, as the array
  natives already do for `filter`/`select`. **No new error code** — reuse `E0004` for argument
  types and the existing member/arity codes. Count stays **119**. If a genuinely new condition has
  no home, STOP and escalate via `allocating-an-error-code`.

---

## Tests — TDD, red first, same commit, through real source

- Each of the six members: type-checker resolution, compiler bytecode-shape, and end-to-end value
  tests, written against **real map literals** (D-376) rather than hand-built AST.
- **Descriptor composition — the load-bearing test:** `m := map<string, int>{"a": 1, "b": 2}` then
  `m.keys.first()` types `string?` and `m.values.contains(2)` types `bool` and evaluates true —
  proving `keys`/`values` return arrays with **populated** descriptors, not bare ones. Include
  `m.keys.length`, and a nested `map<string, int[]>` whose `values` is `int[][]`.
- **Ordering:** `keys` returns insertion order for a literal with entries in a known order, and
  `values` is index-aligned with `keys`. Include a map built by successive index assignment
  (`m["z"] = 1` then `m["a"] = 2`) to prove ordering survives the mutation path too.
- **`get`/indexer agreement:** `m.get(k)` and `m[k]` produce the same type and the same value for
  both a present and an absent key.
- **Nullable semantics:** an absent key yields nil from both, consumable via `??` and `?.`; using
  it unguarded in a non-nullable context raises the existing nullable diagnostic.
- **`contains` is key-membership** — an explicit test contrasting `map.contains(key)` with
  `array.contains(value)` on the same underlying data, so the asymmetry cannot be "corrected"
  later by mistake.
- **Empty map:** `length` is 0, `isEmpty` true, `keys`/`values` are empty arrays whose descriptors
  are still populated (`map<string,int>{}.values.contains(1)` must type-check, not degrade).
- **Argument typing:** `m.get(42)` and `m.contains(42)` raise `E0004`; wrong arity raises the
  existing arity code.
- The re-anchored `CompilerNullableTests` case still asserts what it was written to assert, on
  whatever receiver the gate justifies.
- Every existing map-indexer, `for...in`, map-literal, array, string and numeric test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-377**; confirm, do not assume. The
  entry records: the six query members delivered and the dispatch route used (mirroring D-371's
  array shape); the `ArrayTypeDescriptor` composition for `keys`/`values` and the test proving the
  descriptors are populated; `get`'s relationship to the indexer and whether the implementation
  was shared; the insertion-order guarantee reused from `GrobMap.InsertionOrderKeys` rather than
  reimplemented; the map-versus-array `contains` asymmetry recorded as deliberate; the
  property-access fall-through's status after map registration, which receivers still use it, and
  how `CompilerNullableTests` was re-anchored for the third time — **including whether any
  receiver kind remains, and if not, that finding named for the correctness batch**; and that
  C0b-2b owns the mutating half. No new opcode, no new error code, count 119. Cite D-374, D-376,
  D-371, D-373, D-350, D-362, and the advertised-vs-built audit (finding B2).
- **Update `grob-type-registry.md`** — the `map<K, V>` build-status note records construction
  (D-376) and the query surface as built, with the mutating members pending C0b-2b, citing this
  D-###. Update `docs/wiki/Type-Registry/map.md` to match.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages).
  Archive this prompt under `prompts/archive/sprint-9/`.

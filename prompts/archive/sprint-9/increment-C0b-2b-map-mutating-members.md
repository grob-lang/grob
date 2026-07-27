# Consolidation — Increment C0b-2b: map mutating member surface

**Branch:** `feat/map-mutating-members`
**One concern:** deliver the three in-place mutating map members — `set(key, value)`,
`remove(key)`, `clear()` — with compile-time `readonly` rejection. **This completes the
collection surface and the consolidation phase's code work.**

Runs against the fresh corpus zip carrying D-356 through D-377. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this
prompt or memory for D-### numbers or error codes. **The error-code count is 119** (D-376's
E0016) — confirm against the live registry.

---

## Authority and context

- **The gap.** D-377 delivered the six query members and explicitly deferred these three plus
  their `readonly` rejection to this increment, mirroring the C0a-1/C0a-2 split D-371/D-373
  established for arrays. This closes the last open half of the advertised-vs-built audit's
  finding B2.
- **Everything needed already exists.** D-374's `MapTypeDescriptor`, D-376's map literals (real
  source for tests), D-377's `IsMapMethod`/`ValidateMapMethodCall` dispatch and `MapNatives.cs`
  factory. D-373 is the direct behavioural precedent — the array mutating members with
  `FindReadonlyRoot`-based E0204 rejection at a method-call site.
- **The rejection rule is settled — do not redesign it.** D-291 §4 names this case explicitly:
  *"`X["k"] = v` on `readonly map<...>`"* is a compile error. `FindReadonlyRoot`
  (`TypeChecker.Statements.cs`) walks index and member chains to a `readonly` root, and **E0204**
  is the code. D-373 established its use at a **method-call** site rather than an assignment
  target; mirror that.
- **Aliasing is settled — D-372.** Reference semantics: `b := a` binds the same `GrobMap`
  instance, and mutation through either binding is visible from both. `readonly` is
  **binding-scoped**, not object-scoped (D-372's recorded limitation) — a `readonly` map aliased
  to a mutable binding is mutable through that binding, which is a **correctness-batch finding,
  not a defect to fix here**.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **D-373's array mutating increment end to end** — how `append`/`insert`/`remove`/`clear` were
   added to `IsArrayMethod`/`ValidateArrayMethodCall`, where the E0204 check sits at the
   method-call site, and how `ArrayNatives` mutates the receiver in place. Report the shape; this
   increment mirrors it.
2. **`FindReadonlyRoot` for a map receiver.** Confirm it walks map member chains
   (`ro.field.set("k", 1)`, `roMap["k"].set(...)` where a value is itself a map). Report where
   the check belongs so E0204's message stays accurate for a mutating method rather than an
   assignment.
3. **`set` versus the `[key] = value` indexer.** The registry documents the indexer as *"sugar
   for `set(key, value)`"*, and D-350's `SetIndex` opcode already implements the write. D-377
   shared the read path two ways — `MapValueResultType` at the checker layer and
   `GrobMap.TryGetValue` at the VM layer. **Mirror that for the write path**: `MapNatives.Set`
   and `OpCode.SetIndex`'s map arm must both call `GrobMap.Set`, so the two shapes cannot drift.
   Report the seam.
4. **THE ORDERING INTERACTION — verify, do not assume.** D-377 built `keys`/`values` on
   `GrobMap`'s `InsertionOrderKeys`/`InsertionOrderValues`, guaranteeing insertion order. `set`
   must not violate that. `OrderedDictionary<K,V>` semantics: inserting a **new** key appends;
   overwriting an **existing** key keeps its original position. Confirm both against the live
   `GrobMap.Set` implementation and report, because this is a user-visible guarantee D-377 just
   established and tested.
5. **`remove` is a no-op when the key is absent** — per the registry, *"No-op if key absent"*.
   That is the **opposite** of the array's `remove(index)`, which throws `IndexError` out of
   range (D-373). Same member name, different failure behaviour, on two collection types.
   Confirm against the registry and report it as deliberate, alongside D-377's `contains`
   asymmetry, so neither is "corrected" later by mistake.
6. **Mutation during `for k, v in m` iteration.** D-373 found that `for...in` over an array
   re-reads its bound each iteration, so mutating mid-loop shifts it — reported, not fixed, and
   sitting in the correctness batch. Report the map analogue: what happens when a loop body calls
   `set`, `remove` or `clear` on the map being iterated. `OrderedDictionary` may throw on
   modification during enumeration, which would be a *different* behaviour from the array case.
   **Report only** — if it is surprising, name it for the correctness batch, do not fix it here.
7. **Value and key type checking.** `set(key: K, value: V)` — the value checked against `V` via
   D-376's `CheckMapEntryValue` or D-377's argument path (report which is the right reuse), the
   key against `string`. Expect `E0004` for both, `E0003` for arity. **No new code.**

Report the dispatch additions, the E0204 call-site check, the write-path sharing seam, the
confirmed ordering semantics, the two asymmetries, the `for...in` finding, and the test list.
Then STOP.

---

## Surface to build

| `set(key: K, value: V)` | method | `→ void` | Insert or overwrite. Mutates in place |
| `remove(key: K)` | method | `→ void` | **No-op if key absent.** Mutates in place |
| `clear()` | method | `→ void` | Removes all entries. Mutates in place |

Semantics per `grob-type-registry.md`'s `map<K, V>` section (authoritative).

---

## Scope boundaries — do NOT

- **Do not change the query members** (D-377), the indexer (D-350/D-374), map literals (D-376) or
  `for...in` lowering.
- **Do not fix the `readonly` binding-scope gap** (D-372) — aliasing a `readonly` map to a
  mutable binding bypasses E0204 by design of the compile-time mechanism. Correctness batch.
- **Do not fix the `for...in` mutation behaviour** if the gate finds it surprising — report it.
- **Do not add object-level freezing** — D-372 deferred it post-v1.
- **Do not support non-`string` keys** — v1 is `string`-only.
- **No new opcode** — `GetProperty`/`Call` and the existing `SetIndex` are sufficient, as D-377
  confirmed for the query half. If the gate finds one genuinely unavoidable, STOP and escalate
  via `adding-an-opcode` rather than growing the enum.
- **No new error code** — E0204 for immutability, E0004 for argument types, E0003 for arity,
  E1002 for unrecognised members. Count stays **119**.

---

## Tests — TDD, red first, same commit, through real source (D-376 literals)

- Each of the three members: type-checker resolution, compiler bytecode-shape, and end-to-end
  value tests proving in-place mutation.
- **Mutation observed through D-377's query members** — after `set`, `length` increments and
  `contains(key)` is true; after `remove`, both reverse; after `clear`, `length` is 0, `isEmpty`
  is true, and `keys`/`values` are empty arrays with descriptors still populated.
- **THE ORDERING TEST — load-bearing:** a new key appended by `set` appears **last** in `keys`;
  overwriting an existing key leaves its position **unchanged**; `values` stays index-aligned
  with `keys` throughout. This is the guarantee D-377 established, and `set` is the operation
  most likely to break it.
- **`readonly` rejection — load-bearing:** `readonly m := map<string, int>{"a": 1}` then
  `m.set("b", 2)`, `m.remove("a")` and `m.clear()` each raise **E0204** at compile time. Include
  a chained case proving `FindReadonlyRoot`'s walk.
- **Aliasing pinned (D-372):** `a := map<string,int>{"x":1}; b := a; b.set("y",2)` leaves
  `a.length` at 2; plus an argument-passing case where a function mutates its parameter and the
  caller observes it. Mirrors D-373's array aliasing tests.
- **`remove` on an absent key is a no-op** — no throw, no change to `length`. Explicitly
  contrasted with the array's throwing `remove(index)` so the asymmetry is locked.
- **`set`/indexer agreement:** `m.set(k, v)` and `m[k] = v` produce identical state, including
  ordering, for both a new and an existing key.
- **Type checking:** `m.set("k", "wrong")` on a `map<string,int>` raises `E0004`;
  `m.set(42, 1)` and `m.remove(42)` raise `E0004`; wrong arity raises `E0003`.
- Every existing map, array, string and numeric test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-378**; confirm, do not assume. The
  entry records: the three mutating members delivered, **completing the `map<K, V>` surface and
  the collection surface as a whole** (arrays D-371/D-373, maps D-374/D-376/D-377/this, strings
  D-363/D-365, numerics D-369/D-370); the E0204 compile-time rejection at a method-call site
  mirroring D-373; the write-path sharing seam between `set` and `SetIndex`; the confirmed
  insertion-order semantics for new versus overwritten keys and the test locking them; the
  `remove` no-op-versus-throw asymmetry with the array recorded as deliberate alongside D-377's
  `contains` asymmetry; D-372's aliasing pinned by test; and the `for...in`-during-mutation
  finding named for the correctness batch if one emerged. No new opcode, no new error code, count
  119. Cite D-377, D-376, D-374, D-373, D-372, D-350, D-291, and the advertised-vs-built audit
  (finding B2, now fully closed).
- **Update `grob-type-registry.md`** — the `map<K, V>` build-status note records the surface as
  **fully built**, citing this D-###. Update `docs/wiki/Type-Registry/map.md` to match.
- **Update `grob-advertised-vs-built-audit.md`'s status banner** — finding B2 fully closed; note
  that only the documentation findings (A2, B1, C2) remain open.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages).
  Archive this prompt under `prompts/archive/sprint-9/`.

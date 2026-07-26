# Consolidation — Increment C0b-1: `MapTypeDescriptor` and the map query member surface

**Branch:** `feat/map-type-descriptor-and-query-members`
**One concern:** build the map value-type descriptor and deliver the six non-mutating map
members that depend on it — `length`, `isEmpty`, `keys`, `values`, `get(key)`,
`contains(key)`. The three mutating members (`set`, `remove`, `clear`) and their `readonly`
rejection are increment C0b-2.

Runs against the fresh corpus zip carrying D-356 through D-373. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust
this prompt or memory for D-### numbers or error codes.

---

## Authority and context

- **The gap, documented in the corpus itself.** `grob-type-registry.md`'s `map<K, V>` section
  carries a standing build-status note (F5-1): map typing *"is not yet implemented (D-351):
  `TypeRef.TypeArguments` is parsed and not yet consulted, `map` resolves to the flat
  `GrobType.Map` tag everywhere, and a `for k, v in m` loop binds `v` as `Unknown`."* It names
  the intended fix precisely — a `MapTypeDescriptor` mirroring `ArrayTypeDescriptor` (D-351),
  *"with only `V` inferred since v1 keys are `string`-only"*. This increment builds it.
- **No map member dispatch exists at all.** `TypeChecker.Expressions.cs` has no `GrobType.Map`
  member arm — confirmed by D-371's investigation. All nine documented members are advertised
  and uncompilable. This is the last of the advertised-vs-built audit's collection findings.
- **D-351 is the pattern to mirror, not reinvent.** It established the three-tier descriptor
  carriage for arrays (declaration, expression, symbol). Maps are simpler: v1 keys are
  `string`-only, so `K` is always `string` and only `V` needs inference.
- **This closes one of D-362's three permissive-`Unknown` sources.** D-362 enumerated exactly
  three legitimate `Unknown` operands reaching `EmitArithmetic`: the map element (pending this
  increment), the `Unknown`-receiver field, and the void-returning call. When the descriptor
  lands, the map-element source closes and D-362's comment must be updated to two.
- **The indexer forms already work.** `m[key]` and `m[key] = value` were built by D-350; A4
  added `m[k] op= v` permissively. This increment does not rebuild them — it gives them
  **types**. Their runtime behaviour must not change.

---

## THE DESIGN RISK — settle this in plan-mode, before any source edit

`grob-type-registry.md` documents both `m[key]` and `get(key)` as returning **`V?`**, not `V`
— nil when the key is absent.

Today `m[k]` types as `Unknown`, which is permissive, so `m[k] + 1` and
`someString == m[k]` compile. Once the descriptor lands and types the element as `int?` or
`string?`, those become **nullable-misuse compile errors** requiring `??` or a nil guard.

That is the correct semantics — an absent key really can yield nil, and silently treating it
as zero or empty is exactly what static typing exists to prevent — but it is a **breaking
change to code that currently compiles**.

**Required in the gate:**

1. Confirm what `m[k]` and a `for k, v in m` value binding type as today.
2. Confirm the intended post-change typing against the registry (`V?` for the indexer and
   `get`; note that `values` is documented `→ V[]`, **not** `V?[]`, since iterating existing
   entries cannot yield a missing one — confirm that asymmetry is intended and report it).
3. **Enumerate every site in the test suite and the validation scripts that would newly fail**,
   and report the list before changing anything. The validation scripts construct
   `map<string, string>` but appear not to use map elements in arithmetic — verify that.
4. Report whether any existing behaviour would change at **runtime** (it should not — this is
   compile-time typing only).

If the breakage is wider than a handful of sites, **STOP and report** rather than mass-editing
tests to accommodate it. Updating a test to add a `??` is legitimate; deleting or weakening one
to keep the build green is not.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **`ArrayTypeDescriptor`'s three-tier carriage** (D-351) — read it end to end: where the
   descriptor is created, how it is threaded through declaration, expression and symbol, and
   how `ArrayDescriptorOf` recovers it at a use site. `MapTypeDescriptor` mirrors this shape.
2. **`TypeRef.TypeArguments`** — parsed today and not consulted. Report where it is populated
   and what consuming it for `map<K, V>` requires.
3. **Value-type inference from a map literal.** `m := map<string, int>{...}` is explicit; a bare
   `{...}` literal needs `V` inferred from its elements. Report how array literals infer their
   element type (D-351) and mirror it, including the empty-literal and mixed-element cases —
   state what each does and whether it is an error.
4. **`for k, v in m` binding.** The F5-1 note says `v` binds as `Unknown`. Report where the loop
   binds its two variables and confirm `k` binds as `string` (v1 keys) and `v` as `V`.
5. **`keys → K[]` and `values → V[]` return arrays.** These must construct a *populated*
   `ArrayTypeDescriptor` so `m.keys.first()` and `m.values.contains(x)` type correctly — the
   two descriptor systems compose here. Report how to build an `ArrayTypeDescriptor` from the
   map's `K`/`V` and confirm the result flows through D-371's array member surface.
6. **Member dispatch placement.** Report where a `GrobType.Map` member arm slots into
   `ResolveMemberAccessCall`, alongside the existing namespace, nominal, primitive and array
   routes — and whether the array precedent (`IsArrayMethod` name allow-list plus
   `ValidateArrayMethodCall`) is the right shape to mirror for maps.
7. **`length`/`isEmpty`/`keys`/`values` are properties, not methods** — no parentheses. Confirm
   the property-access path (the D-371 array-property precedent) and that D-371's re-anchored
   permissive-`Unknown` property fall-through is not disturbed. Report which receiver kinds
   still use that fall-through after maps register — if none do, say so, because that makes it
   dead code and is a finding in its own right.
8. **`contains(key: K)` semantics differ from the array's.** The map checks **keys**; the array
   checks **values** (D-371). Same name, different subject. Confirm the map's is key-membership
   per the registry and note the asymmetry in the landing entry so it is deliberate, not
   accidental.
9. **SIZING CHECK.** If the descriptor work alone is substantial enough that bundling six
   members would make this increment unreviewable, **STOP and propose splitting the descriptor
   into its own increment**, with the query members following. Do not rush the substrate to fit
   the members in — the descriptor is the foundation `json` (Increment D) will build on.

Report the descriptor design, the breaking-change enumeration, the inference rules, the
member-dispatch placement, the `keys`/`values` array-descriptor composition, and the test list.
Then STOP.

---

## Surface to build

**The descriptor** — `MapTypeDescriptor` carrying `V` (and `K`, fixed to `string` in v1),
threaded on D-351's three-tier pattern, consulted by the indexer, `for...in`, and the members
below.

**Six query members:**

| `length` | property | `→ int` |
| `isEmpty` | property | `→ bool` |
| `keys` | property | `→ K[]` |
| `values` | property | `→ V[]` |
| `get(key: K)` | method | `→ V?` — nil if absent |
| `contains(key: K)` | method | `→ bool` — key membership |

Semantics per `grob-type-registry.md`'s `map<K, V>` section (authoritative). Note the registry's
ordering guarantee for `keys`/`values` — read it and reproduce it exactly rather than assuming
insertion order.

---

## Scope boundaries — do NOT

- **Do not build `set`, `remove` or `clear`**, and do not build the `readonly` mutation
  rejection — that is C0b-2, a separate concern (in-place mutation plus binding-immutability
  enforcement, mirroring the C0a-1/C0a-2 split).
- **Do not change the runtime behaviour of `m[key]` or `m[key] = value`** (D-350) or `m[k] op= v`
  (A4). This increment types them; it does not rebuild them.
- **Do not support non-`string` keys.** v1 is `string`-only by decision; the registry records
  non-string keys as deferred post-MVP.
- **Do not add user-declarable generic types.** D-080's constrained-generics model is unchanged —
  users consume `map<K, V>`, they cannot declare their own generic types.
- **Do not weaken or delete a test to absorb the nullable breaking change** — add the `??` or
  guard the test's author would have written, or STOP and report.
- **Do not fix the correctness-batch findings** — D-371's `E0004` comparator taxonomy, D-372's
  `readonly` binding-scope gap, D-373's `for...in` dynamic-bound behaviour and unrecognised-member
  fall-through. Report anything new; fix none of them here.
- **No new opcode. No new error code** — count stays 118. If a genuinely new condition has no
  home, STOP and escalate via `allocating-an-error-code`.

---

## Tests — TDD, red first, same commit

- **Descriptor typing proven**: `m := map<string, int>{...}` then `m["k"]` types as `int?`;
  `map<string, string>` yields `string?`. A nested `map<string, int[]>` element types as
  `int[]?` and its array members resolve.
- **`for k, v in m`** binds `k` as `string` and `v` as `V` — the F5-1 note's stated defect,
  fixed and locked.
- **Inference from a bare literal**, plus the empty-literal and mixed-element cases behaving as
  the gate pinned them.
- Each of the six members: type-checker resolution, compiler bytecode-shape, and end-to-end
  value tests.
- **`keys`/`values` compose with the array surface**: `m.keys.length`, `m.keys.first()`,
  `m.values.contains(x)` all resolve and run — proving the constructed `ArrayTypeDescriptor` is
  populated, not bare.
- **`get` and the indexer agree**: `m.get(k)` and `m[k]` produce the same value and the same
  type for both a present and an absent key.
- **The nullable semantics**: an absent key yields nil, consumable via `??` and `?.`; using it
  unguarded in a non-nullable context raises the existing nullable diagnostic.
- **`contains` is key-membership**, not value-membership — an explicit test contrasting it with
  the array's `contains`, so the asymmetry cannot be "fixed" later by mistake.
- **D-362's comment updated** to two permissive-`Unknown` sources, with the map-element source
  recorded as closed.
- Every existing map indexer test (D-350, A4) unchanged in runtime behaviour, plus every array,
  string and numeric test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-374**; confirm, do not assume. The
  entry records: `MapTypeDescriptor` built and its three-tier carriage (mirroring D-351), with
  `K` fixed to `string` and only `V` inferred; the value-inference rules including the empty and
  mixed literal cases; `for k, v in m` binding fixed, closing the F5-1 note's stated defect; the
  six query members delivered and the dispatch route used; `keys`/`values` constructing populated
  `ArrayTypeDescriptor`s so the two descriptor systems compose; the `V?` typing change, the sites
  it broke and how each was updated (never weakened); the map-versus-array `contains` asymmetry
  recorded as deliberate; D-362's permissive-`Unknown` sources reduced from three to two; and
  that C0b-2 owns the mutating half. No new opcode, no new error code, count 118. Cite D-351,
  D-350, D-362, D-371, D-373, D-080, F5-1's note, and the advertised-vs-built audit.
- **Update `grob-type-registry.md`** — replace the F5-1 build-status note with the built state,
  recording the query surface as built and the mutating members as pending C0b-2, citing this
  D-###. Update `wiki/Type-Registry/map.md` to match.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages).
  Archive this prompt under `prompts/archive/sprint-9/`.

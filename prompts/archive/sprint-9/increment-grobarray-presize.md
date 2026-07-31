# Increment: `GrobArray` copy pre-sizing (measured optimisation)

**Branch:** `perf/grobarray-presize`
**One concern:** pre-size `GrobArray`'s backing list when the source collection's count is
knowable, eliminating grow-by-doubling waste on every array built from an existing collection.

Runs against the fresh corpus zip carrying D-356 through D-387. Corpus-first discipline
throughout; read the live decisions log tail, do not trust this prompt for D-### numbers.
**Error-code count is 121** — unchanged by this increment.

---

## Authority — this is a *measured* optimisation, which is what makes it permissible

D-313's standing rule forbids optimisation proposals without measurement. That rule is
**satisfied here**, by two independent runs:

- **Phase 1** (`docs/design/bench-allocation-attribution.md` §5) measured snapshot-plus-iteration
  at **80,015 B** against a ~24,576 B prediction for one `GrobValue[1000]` copy — ≈3.3×, root
  cause unidentified at the time.
- **D-387** isolated it with the `attr-snapshot-empty` fixture: the **pure contents-snapshot copy
  is 75,416 B**, with iteration accounting for only ≈4,599 B (≈4.6 B/iteration). That lands inside
  D-386's recorded ≈73–80 KB prediction for a grow-by-doubling copy.

**The hypothesis on record (D-386):** `GrobArray`'s only constructor takes
`IEnumerable<GrobValue>?` and copies via `[.. elements]`. The parameter's *static* type carries no
count, so the copy may grow by doubling — 4 + 8 + 16 + … + 1024 ≈ 2,044 discarded slots (≈49 KB)
on the way to the final 1,000-element array (≈24 KB), totalling ≈73 KB against 75,416 B measured.

**This is consistent with, not proof of, the doubling explanation** — D-387's own wording. This
increment's fix **is the experiment**: if pre-sizing drops the measured figure toward ≈25 KB, the
hypothesis is confirmed. **If it does not, the hypothesis was wrong, and that is the finding** —
report it and stop rather than searching for a different optimisation to justify the branch.

**Why this matters beyond benchmarks.** D-383 made contents-snapshot universal, so **every array
`for...in` in every Grob script** pays this. It is not benchmark-only.

**Why now, before phase 3b.** Phase 3b derives per-category allocation ceilings from canonical
numbers. Setting a ceiling that encodes a known 3× waste would make fixing it later require
*lowering* a committed threshold.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **`GrobArray`'s constructor(s) and backing store** — confirm the `[.. elements]` copy and that
   `_elements` is a `List<GrobValue>`. Report exactly what the collection expression lowers to for
   an `IEnumerable<T>` spread in this C# version; if that cannot be established by reading, say so
   and let the measurement decide.
2. **Enumerate every `new GrobArray(...)` construction site that passes an existing collection** —
   the `for...in` contents snapshot (D-383/D-384), map `keys` and `values` (D-377), `filter`,
   `select` and `sort` results (`ArrayNatives`), and any others. Report the full list; each is a
   site that benefits, and each is a site whose behaviour must be unchanged.
3. **Check for a site that relies on the current behaviour.** Unlikely, but confirm — particularly
   anywhere a caller mutates the source during or after construction.
4. **Confirm the semantics the fix must preserve exactly**: same elements, same order, and an
   **independent list** — mutating the source afterwards must not affect the copy. Note that
   D-372's reference semantics mean the copy is **shallow**: element references are shared, so
   mutating an element's own contents remains visible. That must not change.
5. **`ArrayNatives`' existing comment** notes that "a `[.. result]` spread would add a redundant
   intermediate array" for `filter`. Report whether the sites already avoid a double copy, and
   whether the fix interacts with that.
6. **Plan the before/after measurement.** `attr-snapshot-empty` is the fixture; report how it will
   be run and what figure would confirm or refute the hypothesis. State the expected post-fix
   value (≈25 KB) **before** measuring, so the result cannot be rationalised afterwards.

Report the constructor, the construction-site list, the preserved semantics, and the measurement
plan. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

A runtime fast path when the count is knowable:

```csharp
_elements = elements switch {
    null => [],
    ICollection<GrobValue> c => new List<GrobValue>(c),   // pre-sized, single copy
    _ => [.. elements],                                   // unknown length, unchanged
};
```

`List<T>`'s `ICollection<T>` constructor sizes from `Count` and copies once. A source that is
genuinely length-unknown (a LINQ chain, say) keeps today's behaviour. Note that `Elements` returns
`IReadOnlyList<GrobValue>` whose runtime type is `List<GrobValue>`, which **does** implement
`ICollection<GrobValue>` — so the snapshot path takes the fast branch.

---

## Scope boundaries — do NOT

- **Do not change `GrobArray`'s public surface, semantics or aliasing behaviour.** Shallow copy,
  same order, independent list. D-372 is untouched.
- **Do not optimise anything else.** Not the per-native-call dispatch tax (phase 4), not the
  `NativeFunction` rebinding, not `GrobMap`. Each needs its own measured case.
- **Do not touch `bench/` fixtures or `policy.json`** beyond running the existing
  `attr-snapshot-empty` to measure. Ceilings are phase 3b.
- **Do not commit a baseline** — only `benchmark.yml` produces committable baselines (§8.2).
- **Do not rationalise a null result.** If the measurement does not move, report that the
  hypothesis is refuted and stop.
- **No new opcode. No new error code** — count stays **121**.

---

## Tests — TDD, red first where behaviour is asserted

- **Semantics unchanged** — the load-bearing tests: constructing from a `List`, from an array,
  from a LINQ result and from `null` all produce the same elements in the same order; the result
  is independent (mutating the source afterwards does not change it); the copy is **shallow**
  (an element that is itself a `GrobArray` is the same instance, per D-372).
- **Every existing array, map, `for...in`, `filter`/`select`/`sort` and `keys`/`values` test
  passes unchanged.** This is a pure allocation change; any behavioural diff is a bug.
- **The measurement**, before and after, from the same machine in one sitting: report
  `attr-snapshot-empty`'s `Allocated` both ways and the derived pure-snapshot figure
  (`attr-snapshot-empty` − `attr-build`).

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-388**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the fix and the
  `ICollection` fast path; **the before and after measurements with the derived pure-snapshot
  figure**, stated numerically; whether the doubling hypothesis is **confirmed or refuted** by
  that result; the full list of construction sites that benefit; that semantics, ordering and
  D-372's shallow-copy aliasing are unchanged, with the tests that prove it; and that this
  precedes phase 3b deliberately so ceilings are not derived from a known waste. Cite D-313 (the
  measurement rule this satisfies), D-386 (the recorded hypothesis), D-387 (the isolating
  measurement), D-383, D-372, and `docs/design/bench-allocation-attribution.md` §5.
- If the hypothesis is **refuted**, the entry says so plainly and records the measurement anyway —
  a refuted hypothesis with numbers is a useful entry, and the branch closes without a fix.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt under
  `prompts/archive/sprint-9/`.

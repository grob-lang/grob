# Increment: attribute the `for...in` snapshot allocation residual (measurement only)

**Branch:** `bench/snapshot-residual-attribution`
**One concern:** find out what the ~74,560 B pure-snapshot cost is actually made of.
**Measurement and reading only — no `src/` changes, no optimisation, no fix even if the cause
becomes obvious.**

This is a **blocking prerequisite for phase 3b**: D-388 states that a `Run_ArrayForIn`
allocation ceiling must not be derived until this residual is attributed, or the ceiling
commits ≈50 KB per thousand-element loop as expected behaviour that a later fix would have to
lower a published threshold to remove.

Runs against the fresh corpus zip carrying D-356 through D-388. Corpus-first discipline
throughout. **Error-code count is 121** — unchanged.

---

## What is established, and what is not

**Established by measurement:**

| Quantity | Bytes | Source |
|---|---:|---|
| Pure-snapshot differential (`attr-snapshot-empty` − `attr-build`) | **75,416** (of record) | D-387 |
| Same differential, local re-run | 74,560 | D-388 (≈850 B run-to-run variance) |
| One isolated `GrobArray` copy, 1,000-element `List` source | **24,080** | D-388, `GC.GetAllocatedBytesForCurrentThread` |
| Loop machinery, 1,000 iterations | **1,977** (≈2 B/iteration) | D-387, `attr-range` − `attr-empty` |

**Ruled out:** copy doubling in `GrobArray`'s constructor (D-388 — the compiler already lowers
`[.. elements]` to `new List<T>(elements)`, and `List<T>` fast-paths any runtime-`ICollection<T>`
source). Also ruled out: loop machinery, which is twenty-five times too small to account for it.

**The hypothesis to test.** The residual is ≈3.1× a single isolated copy, and:

```
3 × 24,080  +  1,977  =  74,217      predicted
                          74,560      measured  (within ~350 B)
```

**Three** copies of the thousand-element array on the snapshot path — not two. That would also
explain why D-388's constructor-level fix moved nothing: the fix was in the right function,
applied to the wrong number of calls.

**This is arithmetic, not evidence.** Two copies plus something else unrelated fits almost as
well. The whole point of this increment is to replace a suggestive ratio with an attribution.

---

## Method

1. **Disassemble the minimal case.** Compile `xs := [1, 2, 3]` followed by `for x in xs { }` and
   dump the chunk (the disassembler exists — D-376 wired new opcodes through it). Report the full
   op sequence for the `for...in` lowering, and identify exactly which op or ops construct the
   snapshot.
2. **Trace every allocation on that path.** For each op involved, read its VM handler and record
   every `new GrobArray`, `new List<GrobValue>`, `GrobValue[]`, `ToList()`, `ToArray()` or `[.. ]`
   spread. **Count them.** The answer to "how many copies" should come from this reading, not from
   the ratio.
3. **Check `GrobArray.Elements` specifically.** If it returns `_elements` directly, that is no
   copy; if it returns a defensive copy, that is one — and it would be invisible at the call site.
   Same question for any `Keys`/`Values`/`InsertionOrderKeys`/`InsertionOrderValues` accessor the
   path touches.
4. **Check `EmitArrayForIn`'s post-D-384 lowering** for an intermediate: whether elements are
   materialised into a temporary before being wrapped in the snapshot `GrobArray`, or whether the
   snapshot is built by a native that itself copies before the constructor copies again.
5. **Measure the snapshot construction in isolation**, using D-388's technique —
   `GC.GetAllocatedBytesForCurrentThread` around just the snapshot step of a 1,000-element
   `for...in`, not the whole benchmark differential. **This is the number that settles it.** Report
   it against the 24,080 B single-copy figure and state the multiple.
6. **Reconcile.** State plainly how much of the 74,560 B is now attributed and how much is not. If
   the reading and the isolated measurement disagree, say so — that disagreement is a finding.

---

## Scope boundaries — do NOT

- **Do not change anything under `src/`.** Not one line, however obvious the fix looks. D-313's
  rule cuts both ways: this increment produces the measurement that would *justify* a fix, and
  bundling the fix into the measurement is exactly what that rule prevents.
- **Do not change the `for...in` semantics.** D-383's contents-snapshot guarantee, D-372's shallow
  copy and D-384's implementation are all settled and out of scope.
- **Do not commit a baseline** — only `benchmark.yml` produces committable baselines.
- **Do not add permanent benchmarks.** If a throwaway fixture helps, keep it on this branch and
  say so; the `attribution` category's membership is settled (D-386/D-387).
- **Do not rationalise a partial result.** If the residual attributes to something other than
  repeated copying, or attributes only partly, report exactly that.
- **No new opcode. No new error code** — count stays **121**.

---

## Deliverable

A findings note — `docs/design/bench-snapshot-residual.md`, repo-pathed — containing:

- The disassembled `for...in` op sequence, with the snapshot-constructing ops identified.
- **A counted list of every allocation on the snapshot path**, by site, from reading the handlers.
- The isolated snapshot measurement and its multiple of 24,080 B.
- **How much of the 74,560 B is attributed, and how much is not.** An honest partial attribution
  is a good result; a confident total that the numbers do not support is not.
- Whether the three-copy prediction is **confirmed or refuted**, stated plainly.
- If confirmed: which copies are redundant and which are load-bearing — enough for a later,
  separate increment to act on. **No proposal, no fix, no design.**

**Decisions log:** a landing entry is appropriate here even though no code changes, because this
resolves a blocking question and unblocks phase 3b — the same shape as a measurement result of
record. D-### from the **live registry tail** — next free is **D-389**; confirm, do not assume.
Match the current index-row format (unpadded date cell). The entry records the attribution, the
confirmation or refutation, what remains unexplained, and **explicitly whether phase 3b is now
unblocked**. Cite D-388, D-387, D-386, D-383, D-313, and
`docs/design/bench-allocation-attribution.md`.

Archive this prompt under `prompts/archive/sprint-9/`.

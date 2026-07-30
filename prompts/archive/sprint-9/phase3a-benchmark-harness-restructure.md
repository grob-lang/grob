# Phase 3a — Benchmark harness restructure (D-385, D-386)

**Branch:** `bench/harness-restructure`
**One concern:** apply the ratified harness decisions to `bench/` and the corpus docs, then
capture fresh baselines for the changed measurements and **report the numbers**. The
per-category allocation ceilings those numbers will inform are **phase 3b** — they cannot be
derived before this increment produces the measurements they must be derived from.

Runs against the fresh corpus zip carrying D-356 through D-386, plus
`docs/design/bench-allocation-attribution.md`. Corpus-first discipline throughout; read the live
decisions log tail, do not trust this prompt for D-### numbers. **Error-code count is 121** —
unchanged by this increment.

---

## Authority

**D-385** settled six questions; **D-386** refines Q1 and Q5. Read both in full — D-386 changes
what Q1 and Q5 require, and Q2/Q3/Q4/Q6 stand as D-385 ratified them.

Phase 1's branch `bench/allocation-attribution` holds the `attr-*` fixtures and the
`StringMethodsPlugin` registration. It is uncommitted. Rebase or cherry-pick from it rather than
rewriting that work.

---

## In scope

**1. Rebuild `vm` in place (D-386 Q1').** Hoist compilation out of the measured region — the
measured call becomes `vm.Run(chunk)` alone, against a `Chunk` prepared in `[GlobalSetup]` (or an
equivalent pre-built form). Same nine fixtures, same category, now measuring what
`grob-benchmarking-strategy.md` §4.2 has always specified: *"hand-constructed `Chunk` instances —
the compiler is not involved."* This is drift correction, not redesign.

**2. `endToEnd` stays empty (D-386 Q1').** Do **not** populate it. Do **not** rename anything
into it. F8 is recorded as **open**. If `policy.json` or `benchmark.yml` currently reference an
`endToEnd` category or baseline that does not exist, report what they do today and leave the
category defined-but-empty if that is what they already assume — **do not delete the category**.

**3. New `attribution` category, `gating: false` (D-386 Q5').** Host the `attr-*` fixtures there
permanently, and update their doc comments — they currently say "phase 1 allocation-attribution
fixture (throwaway)", which is no longer true. Add the two fixtures phase 1 identified as missing:
- **`attr-map-build.grob`** — map construction only, no second loop, so `Run_MapForIn`'s 1,000
  `"k${i}"` interpolations can be split from its second snapshot array.
- **An empty-body snapshot fixture** — `for x in xs { }` over a pre-built array, isolating the
  snapshot copy from iteration. This tests the hypothesis D-386 records: that `GrobArray`'s
  `IEnumerable<GrobValue>` constructor copies via `[.. elements]` with no statically-known count,
  so the copy grows by doubling (~49 KB of waste) on the way to the final ~24 KB array. **Report
  the number; do not act on it.** Any fix is a separate, measured decision.

**4. Ratify the composition root (D-385 Q4).** `StringMethodsPlugin` registered uniformly in the
shared run path becomes the permanent, documented approach. Record in the code that array/map
instance-method dispatch (`ArrayNatives`) is structurally distinct and reachable without plugin
registration, per phase 1 §4.

**5. Corpus amendments (D-385 Q3, Q6).** Amend **D-313's text** so a reader of D-313 alone is not
led to believe the compile cumulative axis is enforceable while `compile.origin.json` carries
`Unknown processor`. State the **gating matrix explicitly** — which categories gate, on which
axes, at what thresholds — together with the flip condition D-385 ratified (when `endToEnd`
carries the validation-suite corpus it becomes the gate and `compile`/`vm` drop to informational).
**Shipped decisions are append-only**: D-313 and D-333 are amended by *citing* D-385/D-386 in the
spec documents and in this increment's landing entry, **never by editing their log entries in
place**. `grob-benchmarking-strategy.md` is the spec document to update.

**6. Capture fresh baselines for the changed measurements, and report every number.** `vm` now
measures something different, so its previous baseline no longer describes the same quantity.

---

## The baseline distinction — state it explicitly in the PR and the landing entry

**Establishing a baseline for a changed measurement is legitimate. Loosening a baseline to absorb
a known regression is the ratchet trap D-313 forbids.** This increment does the former. Say so,
and say which numbers are new-measurement baselines rather than revised thresholds.

**Do not touch `compile`'s baselines.** Its measurement is unchanged by this increment, and it
carries a live cumulative-drift question (+65.2% and +37.9% against origin, currently suppressed
as CPU mismatch) that is **not** this increment's to resolve or absorb.

---

## Scope boundaries — do NOT

- **Do not derive or set per-category allocation ceilings** — that is phase 3b, and it needs this
  increment's numbers first. Leave the existing ceiling in place, even if it still fires; note in
  the PR that phase 3b addresses it.
- **Do not change anything under `src/`.** If the restructure appears to require a `src/` change,
  STOP and report — that would be a finding, not a task.
- **Do not build `endToEnd` content.** Its own increment.
- **Do not act on the ~55 KB hypothesis.** Measure and report.
- **Do not resolve the compile cumulative drift.** Report the numbers if they become visible.
- **Do not edit D-313's or D-333's log entries in place.**

---

## Tests and verification

- Every benchmark builds and runs; the `vm` fixtures produce results with compilation outside the
  measured region — **verify by inspection of the generated report, not by assumption**.
- `attr-native.grob` still reaches `string.upper` (the phase 1 regression this guards).
- The `attribution` category is recognised by `BenchCheck` and is **non-gating** — prove it by
  showing a deliberately-large attribution result does not fail the run.
- `BenchCheck` still handles the `new`-benchmark path correctly for the added fixtures (it did
  before — `Time: new`, no comparison; confirm it still does).
- Existing `compile` results unchanged.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-387**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: `vm` rebuilt in place and
  the mechanism used to hoist compilation; `endToEnd` left empty with **F8 explicitly open**; the
  `attribution` category created non-gating with its fixture list and the two new fixtures; the
  composition root ratified; the corpus amendments made and which decisions they cite; **the full
  fresh-baseline table with every number**, marked clearly as new-measurement baselines rather
  than revised thresholds; the empty-body snapshot fixture's measured result against the recorded
  ~55 KB hypothesis, **with no action taken**; and that phase 3b owns the per-category ceilings.
  Cite D-385, D-386, D-313, D-333, ADR-0018, and `docs/design/bench-allocation-attribution.md`.
- **Update `grob-benchmarking-strategy.md`** — the gating matrix, the flip condition, the
  `attribution` category, and the note that `vm` now matches §4.2 as always specified.
- **Deliverable:** repo-pathed zip (bench, tooling if touched, updated design docs, baselines).
  Archive this prompt under `prompts/archive/sprint-9/`.

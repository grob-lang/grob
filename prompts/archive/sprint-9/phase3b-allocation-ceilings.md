# Phase 3b — Allocation ceilings: rename the mechanism, derive the thresholds

**Branch:** `bench/allocation-ceilings`
**One concern:** replace `policy.json`'s single borrowed `lohTripwireBytes` constant with a
correctly-named total-allocation ceiling carrying thresholds derived from measured values, and
document how those thresholds were chosen and how they are revised.

Runs against the fresh corpus zip carrying D-356 through D-389, plus
`docs/design/bench-allocation-attribution.md` and `docs/design/bench-snapshot-residual.md`.
Corpus-first discipline throughout. **Error-code count is 121** — unchanged.

---

## Authority

**D-385 Q2** ratified the shape of this work: `lohTripwireBytes` (85,000 B) is the .NET Large
Object Heap's *single-object* promotion threshold, but `BenchCheck.BreachesLohTripwire` compares
it against BenchmarkDotNet's *total-per-operation* allocation. D-385 ratified **redocumenting the
mechanism as what it already is — a total-allocation ceiling, explicitly not an LOH detector** —
and left phase 3 to derive per-category (or per-fixture-shape) thresholds from measured values.
D-385 explicitly rejected removing the tripwire outright and rejected implementing real
large-object detection.

**D-389 unblocked this increment.** D-388 forbade deriving a `Run_ArrayForIn` ceiling until the
≈75 KB pure-snapshot residual was attributed. D-389 attributed it completely: one `$snapshot`
copy (D-383's guarantee, load-bearing) plus an ≈48 B/call closure-capture tax on **every**
`GetProperty` dispatch against an array receiver — a cost that is **independent of array size**,
scales with iteration count, and applies to any user `.length`/`.isEmpty` read, not just
`for...in`. That tax is a candidate for later removal, and D-389 records the direction this
matters in: removing it would **shrink** a ceiling, never require raising one — the correct side
of D-313's ratchet rule.

**Until this lands, the tripwire keeps firing.** That is the deliberate interim state D-385 chose.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Canonical numbers first.** D-387's figures are local and explicitly non-committable; only
   `benchmark.yml` produces committable baselines (§8.2, D-309: `windows-latest` is canonical).
   Confirm a canonical run exists on `main` **after** the D-387 restructure merged, and use its
   allocation figures. **If no such run exists, STOP and say so** — thresholds must not be derived
   from local numbers.
2. **`BenchCheck.BreachesLohTripwire`** — the comparison, the failure message, and every place the
   name `loh` appears in code, config, output or docs. Report the full rename surface.
3. **`policy.json`'s current shape** — where `lohTripwireBytes` sits, whether it is global or
   per-category, and what changing that costs in `BenchCheck`.
4. **The per-category allocation profile from the canonical run.** Report the measured
   per-operation allocation for every benchmark in every category. The categories now differ by an
   order of magnitude or more — `vm` measures execution only after D-387 (thousands of bytes),
   `compile` measures compilation (tens of thousands), `attribution` measures whole-script
   diagnostic fixtures (hundreds of thousands). One constant cannot serve all three; that is the
   whole finding.
5. **Whether `attribution` should have a ceiling at all.** It is `gating: false` by D-386 —
   instruments, not guards. Report whether a non-gating category should carry a ceiling that can
   only ever produce noise, and recommend.
6. **The `Run_ArrayForIn`/`Run_MapForIn` shape specifically.** D-389 established their allocation
   decomposes into a size-proportional copy plus an iteration-proportional tax. A ceiling for
   these must be set knowing the tax may later be removed. Report the number and state explicitly
   what portion is the removable tax.

Report the canonical figures, the rename surface, the proposed thresholds **with the derivation
for each**, and the `attribution` recommendation. Then STOP.

---

## What the thresholds must satisfy

- **Derived, not guessed.** Every threshold traces to a measured value with stated headroom, and
  the reasoning is written down. "It fires today, so raise it until it doesn't" is not a
  derivation.
- **Documented where a reader will find them** — `grob-benchmarking-strategy.md`, not only in
  `policy.json`.
- **Revisable by a stated rule.** State when a ceiling is legitimately raised (a new benchmark of
  a genuinely different shape; a deliberate feature landing with measured, accepted cost) and when
  it is not (an unexplained regression). Without that rule the ceiling becomes the ratchet trap
  with extra steps.
- **Headroom stated as a policy**, not chosen per number. Pick one convention, justify it, apply
  it uniformly.

---

## The distinction this increment must be explicit about

**Setting a first threshold for a correctly-named mechanism against a newly-correct measurement is
not loosening a gate.** The old constant was borrowed from an unrelated concept and compared
against the wrong quantity; `vm`'s measurement changed entirely under D-387. Both make the old
number meaningless rather than merely wrong.

**But some new ceiling will be numerically higher than 85,000 B**, and that must not be allowed to
read as quiet relaxation. State in the PR and in the landing entry: which thresholds are higher
than the old constant, why each is justified by measurement, and what would now trigger a failure
that does not today. If a threshold cannot be justified that way, it is too high.

---

## Scope boundaries — do NOT

- **Do not change anything under `src/`.** The ≈48 B/call `GetProperty` tax D-389 identified is a
  separate, separately-decided increment. Set ceilings that account for it as it stands today.
- **Do not flip any `gating` flag.** D-385 Q6 settled the matrix and its flip condition.
- **Do not touch `compile`'s baselines.** Its measurement is unchanged and it carries an
  unresolved cumulative-drift question (+65.2%, +37.9% vs origin, CPU-mismatch-suppressed) that is
  not this increment's to absorb.
- **Do not build `endToEnd` content** — F8 remains open by D-386, its own increment.
- **Do not implement large-object detection** — D-385 rejected it.
- **No new opcode. No new error code** — count stays **121**.

---

## Verification

- `BenchCheck` runs green against the canonical figures with the new thresholds — and **prove the
  ceiling still fires**: show a deliberately-inflated value failing, so the gate is demonstrably
  live rather than merely quiet.
- `attribution` behaves per the plan-mode recommendation (no ceiling, or a stated one) and remains
  non-gating.
- Every existing category comparison is otherwise unchanged.
- No `loh` naming survives anywhere — code, config, output messages or docs.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-390**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the mechanism renamed from
  LOH tripwire to total-allocation ceiling, completing D-385 Q2; **every threshold with its
  derivation** — the measured value, the headroom convention and why; which thresholds exceed the
  old 85,000 B constant and the measurement justifying each; the revision rule; the `attribution`
  category's treatment; the canonical run the figures came from; that the `Run_ArrayForIn`/
  `Run_MapForIn` ceilings include a removable ≈48 B/call component per D-389, so a future fix
  shrinks rather than raises them; and the proof that the ceiling still fires. Cite D-385 (Q2),
  D-386, D-387, D-389, D-388, D-333, D-313, D-309, and both findings notes.
- **Update `grob-benchmarking-strategy.md`** — the ceiling mechanism, the thresholds, the
  derivation and the revision rule, so the documented gate and the enforced gate are the same
  thing (D-385 Q3/Q6's through-line).
- **Deliverable:** repo-pathed zip (tooling, policy, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.

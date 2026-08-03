# Decision session — the time axis on shared runners: noise floor and gating

**Type:** planning/decision session. **No source changes, no threshold edits.** The deliverable
is a ratified decision; implementation follows separately.

Runs against the fresh corpus zip carrying D-356 through D-394, plus
`docs/design/bench-allocation-attribution.md` and `docs/design/bench-snapshot-residual.md`.
Corpus-first discipline throughout. **Error-code count is 121.**

---

## Why this decision exists

Benchmark run **30720384069** on `main` failed the gate on exactly one row:

```
compile  Compile_TwoExpressions  Δ time (rolling) +12.0%  →  per-sprint breach
```

Everything else passed. Every allocation delta in the run was **0.0%** — the D-391 ceilings all
cleared.

**The failure is a false positive, and the run's own data proves it.** `Compile_TwoExpressions`
allocated **9,856 B, byte-identical** to the previous run. The compiler was not modified; D-393
and D-394 touched only `Grob.Vm`/`Grob.Core` dispatch. A 12% time move with byte-identical
allocation on unchanged code is variance.

**The same run shows the background it sits in.** Comparing runs 30707325720 → 30720384069, both
on `windows-latest` / AMD EPYC 7763, identical code:

| Benchmark | Δ time | Δ alloc |
|---|---:|---:|
| `Run_DeclAndArith` | **+27.0%** | 0.0% |
| `Run_AttrMapBuild` | **+20.2%** | 0.0% |
| `Run_AttrEmpty` | +16.6% | 0.0% |
| `Run_Interpolation` | +13.5% | 0.0% |
| `Compile_TwoExpressions` | +12.0% | 0.0% |
| `Compile_TenPrints` | +4.5% | 0.0% |

At +12.0%, `Compile_TwoExpressions` is unremarkable against that background. It failed only
because `compile` is the sole category that gates on time.

**Why the ×3σ noise floor did not catch it — the probable mechanism, to be verified.**
`Compile_TwoExpressions` reports StdDev **0.1002 μs** on a **4.958 μs** mean — ≈2% variance
*within* a run. Three sigma of that is ≈6%, below the 12% *between-run* move. If the noise floor
is computed from within-run StdDev, it systematically understates between-run variance on shared
hosted runners, because it cannot observe the thing that actually varies: neighbouring workload,
CPU frequency and Hyper-V scheduling across separate runs. **Confirm this by reading
`BenchCheck` before accepting it** — it is inference from the reported numbers, not yet verified.

**This is not what D-385 Q3 decided.** Q3 addressed **CPU heterogeneity**, correctly, and left
D-333's `SameCpu` guard in place. This run had **matched** hardware on both sides of the
comparison for `compile` and still produced a false breach. No CPU check can reach it — it is a
different failure mode, and the reason to reopen.

**The cumulative axis is drifting too.** `Compile_TwoExpressions` is now **+74.2% vs origin**
(previously +65.2%, then +60.4%); `Compile_TenPrints` **+44.2%** — against a 12% ceiling, both
suppressed as `cpu mismatch` because `compile.origin.json` carries `Unknown processor` (D-333's
logged gap, re-recorded by D-385 Q3). Those figures move run to run on unchanged code, which
indicates they track runner variance rather than real drift — but nobody can currently tell.

**The wider cost.** This is the second time a gate has blocked a PR for a reason unrelated to the
change under test (D-385's context records the first). A gate that fires spuriously trains people
to ignore red gates, which erodes D-313, ADR-0018 and every gate the project actually relies on.

---

## Plan-mode gate — read-only, read before proposing

1. **`BenchCheck`'s noise-floor implementation** — where the ×3σ figure comes from, which
   sigma it uses, and whether it is within-run StdDev as the numbers suggest. **Confirm or
   refute the mechanism above.**
2. **The per-sprint comparison path** — how `Δ time (rolling)` is computed, and how the noise
   floor gates it. Report exactly why 12.0% breached while ×3σ did not suppress it.
3. **What between-run variance data already exists.** Runs 30523454580, 30707325720 and
   30720384069 have all been captured. Report the per-benchmark spread across whatever runs are
   comparable, and state the sample size honestly — three runs is thin, and any threshold
   derived from it inherits that.
4. **`compile.origin.json`'s state** and what re-capturing it would take, including whether the
   +74.2%/+44.2% figures would then become meaningful or would simply move.
5. **The gating matrix** as D-385 Q6 ratified it, and what each option below changes in it.

---

## The questions to settle

**Q1 — Is time gateable at all on shared hosted runners?** The evidence says between-run variance
on identical code reaches ≈27%. Options:

- **A — Time informational everywhere; allocation carries the gate.** Allocation is provably
  deterministic (0.0% across every benchmark, both runs — exactly what D-333 assumed when it made
  allocation the gating axis). Honest, and matches what the data supports. Cost: no automated
  protection against a genuine time regression; a real 30% slowdown would pass silently.
- **B — Re-derive the noise floor from between-run variance**, measured across several runs of
  identical code, replacing within-run StdDev. Keeps a time gate, at the cost of a wide threshold
  and a sample-size problem.
- **C — Pin a stable runner** (self-hosted, or a larger GitHub runner class). D-385 Q3 rejected
  self-hosting as unnecessary scope for the heterogeneity problem; this is a different problem, so
  the rejection does not automatically carry over. Cost and maintenance to be stated.
- **D — Widen the per-sprint threshold** to sit above observed variance. **Consider it, but the
  bar is high:** a threshold set above ≈27% would let a real regression through, and raising a
  threshold to stop a gate firing is the shape D-313's ratchet rule exists to forbid. If chosen,
  the entry must distinguish this from a ratchet explicitly and convincingly.

**Q2 — What happens to `compile`'s cumulative axis?** It has been inert since `compile.origin.json`
was captured with `Unknown processor`, and now emits +74.2% figures nobody can act on. Decide:
re-capture it, drop the axis, or keep it informational with the corpus stating plainly that it is
inert. **The documented gate and the enforced gate must end up the same thing** — D-385 Q3/Q6's
through-line.

**Q3 — Is this run's failure resolved, and how?** It must not be resolved by editing a threshold
to make it pass. State what unblocks `main`: whichever of Q1's options is chosen, whether a
re-run is expected to pass, and what happens if it fails again on a different benchmark.

**Q4 — Does a false-positive gate have a standing rule?** D-390 established that a **methodology**
change requires unconditional baseline re-freezing. This is adjacent: a gate firing on variance
rather than on the change under test. Decide whether a standing rule is warranted — for instance,
that a time breach accompanied by a 0.0% allocation delta on a category the change did not touch
is presumptively variance and must be investigated before being treated as a regression. **Only
if it earns its place**; not every observation needs a rule.

---

## Constraints

- **D-313's ratchet rule is absolute.** No threshold is raised to absorb a known regression.
  Widening a threshold because the *measurement* is too noisy to support the old one is a
  different act — but it must be argued explicitly, not assumed.
- **Append-only.** Anything amending D-313, D-333 or D-385 is done by citation in a new entry,
  never by editing theirs.
- **Do not touch `src/`, `policy.json`, any baseline or any threshold in this session.**
- **State sample sizes honestly.** Three runs is a small basis for a variance threshold; if that
  is the basis, say so and state what would strengthen it.

---

## Deliverable

**A single ratified decision**, three-location lockstep (summary index row, full ADR entry, footer
changelog), D-### from the **live registry tail** — next free is **D-395**; confirm, do not
assume. Match the current index-row format (unpadded date cell).

For each of Q1–Q4: what was decided, what was rejected and why, and what it costs. It must carry
`Refines: D-385` (Q3 and Q6), `Refines: D-313`, and cite D-333, D-390, D-391 and run
30720384069's gate output as evidence.

It must state explicitly **what unblocks the current red `main`**, and whether implementation is
one increment or several.

**No implementation.** If the answer to Q1 is that time cannot be gated on this infrastructure,
say so plainly — recording an honest limitation is a better outcome than a threshold nobody
trusts.

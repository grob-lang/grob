# Phase 2 — Benchmark harness correctness: decision session

**Type:** planning/decision session. **No source changes, no harness changes.** The
deliverable is a ratified decision in the decisions log; phase 3 implements it.

Runs against the fresh corpus zip carrying D-356 through D-384, plus
`docs/design/bench-allocation-attribution.md` (phase 1's findings note — commit it if it
is not yet on `main`; this decision cites it as evidence).

---

## Why this decision exists

The `Benchmarks` CI run on `0125bae` failed. Investigation established that the failure
had nothing to do with the change under test, and that several parts of the gate are
measuring or reporting the wrong things:

- **The `vm` category measures the entire compiler pipeline.** `VmBenchmarks.RunSource`
  runs `Lexer.Scan` → `Parser.Parse` → `TypeChecker.Check` → `Compiler.Compile` →
  `vm.Run`, all inside the measured region. A compiler regression and a VM regression are
  currently indistinguishable in the same number.
- **`Run_ArrayForIn` is 86.4% fixture setup**, 13.6% the loop its own doc comment claims
  to measure (phase 1, measured).
- **The `vm` benchmarks reached no `Grob.Stdlib` native at all** until phase 1's branch —
  `RunSource` constructs a bare `VirtualMachine` and registers no plugin, while the CLI's
  composition root registers the full set. The most common call path in real Grob scripts
  has never been benchmarked.
- **The LOH tripwire compares BenchmarkDotNet's total `Allocated` against 85,000 B**, the
  .NET *single-object* Large Object Heap threshold. It cannot detect a large-object
  allocation from BDN summary output, and it fires on any benchmark that allocates many
  small objects. **This was the sole cause of the CI failure** — both `for...in`
  benchmarks were already 6–12× over it before the change under test existed.
- **The time axis is currently decorative.** The run reported *"CPU mismatch — fresh 'AMD
  EPYC 7763' vs rolling baseline 'Intel Xeon Platinum 8370C'"*, degrading every time
  comparison to `info`. GitHub-hosted runners do not guarantee a CPU, so this is
  structural, not a one-off. **D-313's 5%-per-sprint and 12%-cumulative time thresholds
  are not currently enforced by anything**; only the allocation axis gates.
- **Two compile benchmarks show large cumulative drift nobody can currently measure** —
  `Compile_TwoExpressions` +65.2% and `Compile_TenPrints` +37.9% against origin, against
  a 12% ceiling, both suppressed as `cpu mismatch`. That may be entirely CPU difference,
  or real drift the mismatch has been hiding. It is not currently knowable.
- **`endToEnd` is empty** — *"no fresh benchmarks matched 'Grob.Benchmarks.EndToEnd' —
  nothing to compare"*. This is F8, the never-captured baseline, still open.

**Correctly working, do not "fix":** the new-benchmark path. Both new benchmarks reported
`Time: new` and were not compared — BenchCheck handled them properly. An earlier
hypothesis that the gate fails on missing baselines was **wrong**; the tripwire fired
independently.

**The wider cost.** A gate that fires spuriously trains people to ignore red gates. This
one blocked a PR for a reason unrelated to the change, and the reflex it teaches erodes
D-313, ADR-0018 and every other gate the project actually relies on.

---

## Plan-mode gate — read-only, read the harness before proposing anything

1. **`tooling/Grob.BenchCheck`** in full — the comparison logic, the rolling-versus-origin
   two-axis implementation (D-313), the allocation axis (D-333's 10%), the LOH tripwire,
   the CPU-mismatch detection and what it suppresses, and the `new`-benchmark path.
2. **`policy.json`** — categories, thresholds, and each category's `gating` flag. The CI
   output shows `vm` as `gating: false`; confirm which categories actually gate and report
   the full matrix.
3. **`.github/workflows/benchmark.yml`** — when it runs, on what runner, what it uploads,
   and how baselines are committed and rolled.
4. **The committed baselines** — `baseline/*.json`, including whether `endToEnd.origin.json`
   exists at all (F8), and how the rolling baseline is updated.
5. **`bench/Grob.Benchmarks/`** — the fixture set, `VmBenchmarks.RunSource`, the category
   attributes, and how `CompileBenchmarks`/`StabilityBenchmarks` differ in shape.
6. **The CLI composition root** — `Grob.Cli.PluginRegistration.RegisterAll` and the
   capability interfaces it wires (`IRandomSource`, `IEnvironment`, `IStandardStreams`),
   so the fidelity question below can be answered concretely.

Report what each currently does before proposing changes. **Do not change anything.**

---

## The questions this decision must settle

**Q1 — What does each category measure?** The `vm` category measures the full pipeline;
`endToEnd` is empty. Options include renaming `vm` to `endToEnd` (it is what that name
describes), hoisting compilation into `[GlobalSetup]` so `vm` measures `vm.Run(chunk)`
only, or maintaining both with distinct fixtures. Whatever is chosen, **a category's name
must describe what it measures**, and F8's never-captured `endToEnd` baseline must be
resolved rather than left empty.

**Q2 — The LOH tripwire.** It cannot do what its name says. Decide: remove it; repurpose
it as a documented **total-allocation ceiling** with a threshold derived from observed
values rather than from the unrelated 85,000 B LOH constant; or implement genuine
large-object detection (note the cost — BDN summary output does not carry it). State the
reasoning, and if a ceiling is kept, state how its threshold is chosen and revised.

**Q3 — The time axis under CPU heterogeneity.** D-313's time thresholds are currently
unenforced on GitHub-hosted runners. Options: accept allocation-only gating and **amend
D-313 explicitly** so the corpus stops claiming a guarantee it does not deliver; pin a
self-hosted or larger runner with a stable CPU; or add a CPU-independent time measure —
for instance ratios **between benchmarks within the same run**, which cancel machine
speed. Whichever is chosen, the outcome must be that **the documented gate and the
enforced gate are the same thing**. A threshold that cannot fire should not be documented
as a threshold.

**Q4 — Benchmark fidelity: which composition root?** Benchmarks construct a bare
`VirtualMachine`; the CLI registers the full plugin set. Decide whether benchmarks should
mirror the CLI's composition root (measuring what users actually run, at the cost of
including registration in every measurement) or stay minimal (isolating the VM, at the
cost of never exercising the stdlib path). Phase 1 registered `StringMethodsPlugin`
uniformly as a stopgap; that choice needs ratifying or replacing.

**Q5 — Fixture design.** Whole-script benchmarks cannot build a fixture outside the
measured region, so perfect isolation is unattainable. Decide whether to accept that and
**document each benchmark as whole-script**, or adopt **companion setup-only fixtures** so
a subtraction attributes the code under test — the technique phase 1 used successfully.
Also decide whether phase 1's `attr-*` fixtures become permanent, and whether to add the
two fixtures phase 1 identified as missing: an `attr-map-build` to split `Run_MapForIn`'s
interpolation cost from its second snapshot array, and an empty-body snapshot fixture to
isolate the unexplained ~55 KB in §5.

**Q6 — What gates, and what is informational?** With `vm` currently `gating: false`, the
practical gate is narrower than the corpus implies. State the intended matrix explicitly:
which categories gate, on which axes, at what thresholds — and make ADR-0018-style
documentation match it.

---

## Constraints

- **D-313's ratchet trap is absolute.** No baseline may be updated to absorb a known
  regression. Establishing a baseline for a **changed measurement** is legitimate and
  different — say which is which, explicitly, wherever this decision implies rebaselining.
- **The compile-benchmark drift (+65.2%, +37.9% vs origin) must not be silently
  rebaselined.** If the new design makes those numbers measurable, they must be measured
  and reported. If they are real drift, that is a finding for its own increment.
- **Do not weaken a gate to make it pass.** Removing a gate that measures the wrong thing
  is not weakening; loosening a threshold to accommodate a regression is. Distinguish them
  in the entry.
- **Cite phase 1's findings note for every empirical claim.** Do not restate its numbers
  from memory.

---

## Deliverable

**A single ratified decision**, three-location lockstep (summary index row, full ADR
entry, footer changelog), D-### taken from the **live registry tail** — next free is
**D-385**; confirm, do not assume. Match the current index-row format (unpadded date
cell).

The entry states, for each of Q1–Q6: what was decided, what was rejected and why, and
what it costs. It must be explicit about which parts amend **D-313** (the two-axis gate),
**D-333** (the allocation axis) and **ADR-0018** (coverage-scope conventions, for its
documentation-matching precedent), and must carry `Refines:` headers accordingly —
**shipped decisions are append-only and are never edited in place**.

It must also record F8's status and whether this decision resolves it.

**No implementation.** Phase 3 is the increment that applies this. If the decision turns
out to need more than one increment, say so and propose the split.

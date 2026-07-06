# Pre-Sprint 7 — Interlude 2: Benchmark gate hardening

> **Shape:** Pre-Sprint 7 interlude. Self-contained. One concern, one branch, one PR.
> **Runs after:** Pre-Sprint 7 Interlude 1 (VM operand-stack allocation remediation). The
> allocation baseline this interlude freezes must be taken against the fixed VM. If the VM
> still allocates on the LOH, **stop** — Interlude 1 has not landed.
> **Model routing:** tooling and spec change, no language surface — Sonnet for execution,
> Opus only if the significance model turns out to be contentious.

---

## 1. Why this interlude exists

The Sprint 6 run demonstrated the gate flagging the wrong things:

- **Compile_TenPrints +8.7% vs rolling → "breach".** That benchmark's StdDev is ~3.2% of a
  6.55 μs mean; its sibling Compile_TwoExpressions barely moved (+4.2%, inside tolerance).
  On a 2-physical-core Hyper-V windows-latest runner an 8.7% swing on a single-digit-µs
  microbenchmark is inside the infrastructure noise floor. The gate fired on noise.
- **VM +33.8% uniform → "info".** All three unrelated VM benchmarks moved by the *identical*
  figure — the signature of a whole-baseline shift (runner/hardware or a shared allocation),
  not three coincident code regressions. It read as "info" because `vm` is `gating: false`
  during build-out — correct by policy, but it meant the one genuinely broken thing (the LOH
  allocation, now fixed in Interlude 1) was logged as informational while noise went red.

Two structural corrections follow, both already latent in the strategy doc, which states
that tightening the time gate "has a precondition — a quieter measurement first":

1. **Allocation is a deterministic, runner-independent signal** and `[MemoryDiagnoser]`
   already records it into every baseline JSON (D-302 body). The gate simply never acts on
   it. An allocation axis would have screamed on the VM's 419 KB/op on day one instead of
   filing it under "info".
2. **The time gate must be significance-aware** so a delta inside a benchmark's own
   measurement noise does not trip a flat percentage.

**Proven cross-CPU case (the post-Interlude-1 run).** The verification run landed on an
**Intel Xeon Platinum 8370C**; the baseline was captured on an **AMD EPYC 7763**. Both are
labelled `windows-latest`; the silicon differs. The gate flagged compile +37%/+25% as a
per-sprint breach — yet compile allocation was **byte-identical across both runs** (7.68 KB
and 14.14 KB, unchanged), and Interlude 1 never touched the compiler. The time delta was
pure CPU swing, not a regression. This is exactly the cross-hardware comparison D-309 already
calls invalid, and the existing guard missed it because it keys on runner *type*
(OS + runtime), not on the CPU. The hardening below closes that gap.

---

## 2. Verify-first — do this before proposing anything

Corpus-first. Read live source, the live policy data and the live log tail.

1. **Interlude 1 has landed.** Confirm the recaptured VM baseline shows allocation off the
   LOH. If not, stop — do not build an allocation gate on top of a broken baseline (that is
   the D-313 ratchet trap: you would freeze the bug in as "normal").
2. **Decisions log tail.** Read the live `grob-decisions-log.md` index tail and last full
   entry. Allocate the **next free** `D-###` from the live log — not from this prompt.
3. **Current gate mechanics.** Read the live:
   - `tooling/Grob.BenchCheck` — how it reads `-report-full.json`, computes vs-rolling and
     vs-origin deltas, and applies thresholds; how it guards the runner from
     `HostEnvironmentInfo`. Confirm the current guard keys on runner *type* (OS + runtime)
     and **not** on `ProcessorName` — the post-Interlude-1 run proved this passes an
     EPYC-baseline vs Xeon-run comparison that D-309 declares invalid. Read the
     `HostEnvironmentInfo` fields available (`ProcessorName`, `PhysicalCoreCount`,
     `RuntimeVersion`) so the CPU-identity guard in (C) reads real data.
   - `bench/Grob.Benchmarks/baseline/policy.json` — the current schema: `perSprintPercent`,
     `cumulativePercent`, and the per-category `{ name, namespacePrefix, baseline, gating }`
     rows.
   - `grob-benchmarking-strategy.md` §8 (baseline storage) and §9 (per-sprint policy) — the
     two-axis model (D-313), the rolling-vs-origin distinction, the "compile gates during
     build-out, VM informational" arrangement.
4. **Confirm the allocation data is present.** Verify each benchmark's
   `Memory.BytesAllocatedPerOperation` and `Gen0/1/2Collections` are in the committed
   baseline JSON so the new axis reads existing data with **no benchmark-workload change**.

Report findings from 1–4 before writing the plan.

---

## 3. Objective — three changes

**(A) Add a hard allocation axis** to `policy.json` + `Grob.BenchCheck`. Per gating
category, compare `BytesAllocatedPerOperation` against the rolling baseline with its own
threshold. Recommended model, to argue not assume: a **percent-vs-baseline** allocation
threshold mirroring the time axis, **plus an absolute LOH tripwire** — any gating benchmark
whose single-allocation footprint clears the ~85,000-byte LOH threshold on the run path
fails outright. The tripwire is what would have caught the VM defect on day one; the percent
axis catches ordinary allocation creep. Allocation is deterministic, so its threshold can be
tight where the time threshold cannot.

**(B) Make the time axis significance-aware.** A time delta must clear the benchmark's own
measurement noise before it can trip, not merely the flat percentage. Read `StdDev` (and/or
the CI) already present in `-report-full.json` and require the delta to exceed
`max(perSprintPercent, k · relativeStdDev)` for a breach. The flat percentage remains a
floor; significance is an **additional** filter layered over it, never a replacement — a
genuine acute regression (a value boxed onto the dispatch hot path jumping 30%) must still go
red. Optionally require **N consecutive breaches** across runs before failing, to filter a
one-off noisy runner. State the chosen `k` and defend it.

**(C) CPU-identity guard and axis-gating by hardware.** The `windows-latest` label is not a
hardware pin — the hosted pool serves different CPUs, and the post-Interlude-1 run proved a
25–37% time swing between EPYC and Xeon with allocation unchanged. So:

- The guard keys on the **CPU identity** (`ProcessorName` from `HostEnvironmentInfo`), not the
  runner label. A run whose CPU differs from the baseline's CPU is a cross-hardware time
  comparison.
- **Refusing on CPU mismatch is wrong** — hosted runners cannot be CPU-pinned, so a hard
  refuse would make the gate refuse constantly. The rule is axis-split by hardware:
  **allocation gates always** (it is CPU-independent — 49 KB is 49 KB on any silicon), and
  **the time axis gates only when the run CPU matches the baseline CPU; on a CPU mismatch the
  time comparison drops to informational** and the report says so explicitly. This is the
  concrete mechanism behind "treat hosted-runner time as directional, allocation as the hard
  gate".
- Baseline recapture is still on `windows-latest` (D-309), and each baseline records the CPU
  it was captured on so the guard can compare. Recapture affected categories against the
  Interlude-1 code. The **VM allocation** anchor (≈49/51.6/57.1 KB) can be frozen now — it is
  CPU-independent and verified. A **time** baseline captured on a one-off hosted CPU is only
  meaningful under the CPU-match rule above, so do not let this run's Xeon time overwrite an
  EPYC-anchored compile baseline.

---

## 4. Plan-mode gate — stop for explicit approval

Produce a **numbered** plan. Do not touch tooling, policy data or the strategy doc until it
is approved. The plan must specify:

- The **allocation threshold model** — percent-vs-baseline value, plus the absolute LOH
  tripwire and where it reads single-allocation size from.
- The **significance model** — the exact breach condition for the time axis, the value of
  `k`, and whether consecutive-breach is in scope for this interlude or deferred.
- The **`policy.json` schema delta** — new fields (e.g. `allocPercent`, `lohTripwireBytes`,
  `timeSignificanceK`), kept as *data the maintainer edits*, consistent with the D-313
  principle that thresholds are data not constants. Confirm the schema change is
  backward-readable or migrate the committed file in the same commit.
- Which categories the allocation axis gates. Recommended: gate allocation on the same
  categories that gate time today (compile during build-out), and additionally run the LOH
  tripwire across **all** categories including informational ones — an informational category
  should still be forbidden from silently going onto the LOH.
- The **CPU-identity rule** — where the baseline's captured CPU is stored (baseline JSON
  `HostEnvironmentInfo` or a sidecar field in `policy.json`), how `ProcessorName` is compared,
  and the exact report wording when time drops to informational on a CPU mismatch. Allocation
  and the LOH tripwire are unaffected by CPU and continue to gate.

---

## 5. Closed surfaces — do not touch

- **The benchmark workloads.** Changing a workload invalidates the baseline (strategy §5
  discipline). This interlude changes how results are *judged*, never what is measured.
- **`GrobValue`, `OpCode`, `.grobc`, the language surface.** None are in scope.
- **The two-axis time model's intent** (D-313). Significance refines *how* the per-sprint
  axis fires; it does not remove the rolling/origin structure or the cumulative ceiling.

---

## 6. TDD matrix

`Grob.BenchCheck` is plain tooling with unit tests. Red-first:

| # | Test | Asserts |
|---|------|---------|
| 1 | Allocation regression trips | A synthetic report with allocation above the rolling baseline by more than `allocPercent` → gate red. |
| 2 | Allocation within threshold passes | Allocation drift under `allocPercent` → gate green. |
| 3 | LOH tripwire fires | A benchmark reporting a single-allocation footprint ≥ `lohTripwireBytes` → gate red, even on an informational category. |
| 4 | Time delta inside noise passes | A time delta above the flat `perSprintPercent` but **below** `k · relativeStdDev` → gate green (this is the TenPrints case). |
| 5 | Time delta clearing noise trips | A time delta above both the flat percentage and the noise band → gate red (the genuine acute regression). |
| 6 | CPU match → time gates | Report and baseline share a `ProcessorName` → the time axis is evaluated normally (red on a real regression, green otherwise). |
| 7 | CPU mismatch → time informational, allocation still gates | Report CPU ≠ baseline CPU → the time comparison is reported informational (never red), while the allocation axis and LOH tripwire are still evaluated and can go red. |
| 8 | Proven cross-CPU fixture | The real post-Interlude-1 pair — EPYC-anchored compile baseline vs a Xeon run with identical 7.68/14.14 KB allocation and +25–37% time — must yield: compile time **informational** (not a breach), compile allocation **green** (unchanged). Locks the exact false-positive this run produced. |

Dry-run acceptance (not a unit test, a gate check): run the hardened `Grob.BenchCheck`
against the **Interlude-1 post-fix reports** and confirm (i) the compile time breach is
suppressed to informational under the CPU-mismatch rule, (ii) compile allocation reads green
(unchanged), and (iii) the now-healthy VM allocation reads green under the allocation axis.
Test tooling: xUnit, FsCheck where a property fits. No FluentAssertions.

---

## 7. Stop-and-surface triggers

- Making the time gate significance-aware would let a **real** acute regression through under
  the chosen `k` — tune conservatively and surface the tradeoff rather than shipping a soft
  gate.
- The `policy.json` schema change ripples into anything D-313 documents as a fixed shape —
  log the supersession rather than silently changing it.
- Recapturing baselines shows VM allocation **still** on the LOH — Interlude 1 did not land;
  stop and go back.
- The consistency gate (D-316) has any assertion over `policy.json` or the baseline files
  that the schema change would break — extend the gate in lockstep, do not route around it.

---

## 8. Definition of done

- `Grob.BenchCheck` tests 1–8 green; the dry-run acceptance passes (compile time
  informational under CPU mismatch, compile allocation green, healthy VM allocation green).
- `policy.json` carries the allocation axis, the significance parameters and the
  CPU-identity rule as data.
- `grob-benchmarking-strategy.md` §8–9 updated: the allocation axis, the LOH tripwire, the
  significance-aware time gate and the **CPU-identity axis rule** (allocation always gates;
  time gates only on CPU match, else informational) documented, each citing the new `D-###`.
  The §9 prose that currently says a tighter time gate "has a precondition — a quieter
  measurement first" is updated to record that the precondition is now met. The D-309
  "same runner type" wording is refined to "same CPU identity", with the axis-split rule
  cited as how the hosted-pool CPU churn is handled without the gate refusing constantly.
- **Decision logged, three-location lockstep:** summary index row, full ADR-style entry
  (Area: Tooling — benchmarking; Supersedes: none; note "refines D-302 / D-309 / D-313"),
  footer changelog. Add a "refined by D-###" note to D-313's entry in lockstep.
- Consistency gate (D-316) green.

---

## 9. Deliverable shape

Full files, never patches. One branch, one PR, main protected, Husky.NET pre-push gate green
before push. Ship a single zip: the changed `Grob.BenchCheck` source and tests, the updated
`policy.json`, the recaptured baseline JSON, the updated `grob-benchmarking-strategy.md`, the
`grob-decisions-log.md` update, and this executed command archived under
`prompts/archive/sprint-7/`. British English, no Oxford comma.

# Grob — Benchmarking Strategy

> **Purpose:** This document specifies how Grob is benchmarked. It defines
> the harness, the project structure, the three benchmark categories,
> memory diagnostics, the long-run stability test, baseline storage and
> the per-sprint regression policy.
>
> **Authority:** D-302. Updates to `grob-v1-requirements.md` §12 and
> `grob-solution-architecture.md` cite this document for detail.
>
> **Scope:** Benchmarking infrastructure is implementation-level, not
> language-level. It does not appear at the CLI surface in v1. It is
> built and maintained by the engineer working on Grob; it is not a
> feature shipped to Grob users.

---

## 1. Why Benchmarking Lands Before Optimisation

A bytecode VM has many obvious-looking optimisations that do not move
the needle on real workloads. Whether any specific change is worth its
cost is an empirical question, and the way you answer empirical questions
is with measurement. Without a harness in place from early in the build,
the project loses two things:

1. **The ability to evaluate optimisation candidates honestly.** "Feels
   faster" is how performance regressions get shipped. A harness that
   produces statistically credible numbers is the only way to know
   whether a change earned its complexity.
2. **The ability to catch regressions when they happen.** The bigger
   risk is not unoptimised code — it is a refactor three sprints ago
   that silently slowed everything down by 8% and is now buried under
   subsequent work. A baseline you can compare against turns this from
   archaeology into a diff.

The benchmarking infrastructure therefore lands before any optimisation
work. It is built early, exercised continuously, and used as a quality
gate at the end of every sprint.

---

## 2. Harness

**BenchmarkDotNet.** Standard tool in the .NET ecosystem. Handles
warmup, JIT compilation effects, GC interaction, statistical analysis,
outlier detection and report generation. Rolling a custom harness for
a hobby project would be wasted effort and the results would be less
credible than what BenchmarkDotNet produces by default.

Run mode is Release configuration only. Debug builds produce numbers
that are not comparable to Release numbers and are not useful as a
baseline.

---

## 3. Project Structure

A new sibling to `src/`, `tests/` and `plugins/`:

```
Grob.sln
├── src/
├── plugins/
├── tests/
├── bench/
│   └── Grob.Benchmarks/         ← Console app — BenchmarkDotNet entry point
│       ├── Compile/             ← Lex/parse/typecheck/emit benchmarks
│       ├── Vm/                  ← Hand-constructed chunk execution benchmarks
│       ├── EndToEnd/            ← Validation suite full-pipeline benchmarks
│       ├── Stability/           ← Long-run stability test (separate cadence)
│       └── baseline/            ← Committed baseline JSON results
└── tooling/
```

`Grob.Benchmarks` is a console application referencing
`BenchmarkDotNet`, all four `src/` assemblies it needs to drive
(`Grob.Core`, `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`), and
`Grob.Runtime`. Entry point is `dotnet run -c Release --project bench/Grob.Benchmarks`.

`bench/` is deliberately not under `tests/`. Tests run on every commit
in CI; benchmarks do not. They have different lifecycles and different
audiences. The directory structure reflects that.

---

## 4. Three Benchmark Categories

The three categories map to the three layers of the Grob pipeline. Each
category answers a different question.

### 4.1 Compile-Time Benchmarks (`bench/Grob.Benchmarks/Compile/`)

**Question answered:** how fast is the compiler?

For a scripting language, startup cost matters. The compiler runs every
time a script runs. A 200ms compile time on a 50ms script is a real
quality issue. Compile-time benchmarks measure each stage of the
pipeline in isolation and end-to-end:

- Lexer throughput — tokens per second on a representative source
- Parser throughput — AST nodes per second
- Type checker throughput — declarations checked per second
- Compiler throughput — bytecode emitted per second
- End-to-end compile — source string to `Chunk` ready for the VM

Source corpus for these benchmarks: the thirteen validation suite
scripts, plus a synthetic large script (auto-generated, 1000+ lines)
to surface throughput characteristics the small scripts cannot.

### 4.2 VM Execution Benchmarks (`bench/Grob.Benchmarks/Vm/`)

**Question answered:** how fast is the dispatch loop?

These use hand-constructed `Chunk` instances — the compiler is not
involved. This isolates VM performance from compiler performance.
A regression in VM execution time on a tight arithmetic loop tells
you something very different from a regression in the same workload
when measured end-to-end.

Patterns measured:

- Arithmetic-heavy tight loop — integer and float
- Function call overhead — recursive fibonacci, iterative fibonacci
- String interpolation in a loop
- Map operations — insert, lookup, iterate
- Closure capture — upvalue read/write
- Array operations — append, index, iterate
- Pipeline-style chained method calls

These are the micro-benchmarks. They will not predict real-world
performance on their own, but a regression in any of them flags
something worth understanding.

### 4.3 End-to-End Script Benchmarks (`bench/Grob.Benchmarks/EndToEnd/`)

**Question answered:** does Grob run real scripts fast enough?

The thirteen validation suite scripts, run through the full pipeline:
source file on disk → compiled → executed → exit. This is the workload
that actually matters. If end-to-end numbers regress, something is
wrong even if the micro-benchmarks all look fine.

This category is the primary gate. The other two exist to help diagnose
regressions surfaced here.

---

## 5. Memory Diagnostics

Three categories of memory issue can affect a managed VM. They need
different infrastructure and only the first two get coverage in v1.

### 5.1 Managed-Side Retention (Category 1)

True .NET-side leaks via unintended retention. A static collection that
should release. A registry that grows without bound. A cache that never
evicts. These are diagnosable via the standard tooling because they
appear as growing managed heap.

**Coverage:** `[MemoryDiagnoser]` attribute on every benchmark.
BenchmarkDotNet reports allocations per operation and Gen 0/1/2 collection
counts per operation. Both numbers join timing in the committed baseline
JSON. A benchmark that suddenly allocates 3× more bytes per operation
is a regression flag, same as one that runs 3× slower.

### 5.2 GC Pressure (Category 3)

No leak. Allocation patterns that produce excessive GC work — string
concatenation in tight loops, autoboxing-equivalent issues at the
`GrobValue` boundary, intermediate collections in pipeline operations.
Performance regression masquerading as a memory problem.

**Coverage:** also `[MemoryDiagnoser]`. Gen 0/1/2 collection counts per
op surface this directly. A sprint's changes that triple Gen 0
collections on the validation suite show up at the next end-of-sprint
run.

### 5.3 Grob-Side Logical Retention (Category 2) — Deferred

A Grob script holds a closure that captures a large array, the closure
outlives its useful life, the array stays reachable. From the .NET GC's
perspective everything is correctly rooted — the leak is in the
language semantics, not in the runtime. Diagnosing this requires
Grob-aware introspection: "how many `GrobArray` instances are reachable
right now?", "what is the retention root for this value?", "how deep
is the upvalue chain on this closure?".

**Explicit non-goal in v1.** Building this is a significant piece of
work that solves a problem v1 does not yet have. The v1 architecture
does not preclude it — the `GrobValue` boundary is clean and VM
allocation paths are centralised, both of which keep the door open.
Actual implementation is post-v1, informed by real script behaviour.

---

## 6. Stability Test

A long-run test that catches Category 1 leaks invisible in single-run
timing. Lives at `bench/Grob.Benchmarks/Stability/`.

**Shape:** run each validation suite script in a loop for a fixed
iteration count. After a warmup window, record managed heap size. At
the end of the run, assert that final managed heap size is within a
tolerance of the post-warmup heap size.

**Initial values are placeholders pending calibration.** The numbers
below are starting points, not locked decisions:

- Iteration count: 10,000 (placeholder)
- Warmup window: 100 iterations (placeholder)
- Tolerance: 10% (placeholder)

**Calibration ritual at Sprint 8 close.** Before locking these numbers
in the baseline, run a single-iteration pass against the stdlib-substantial
build that Sprint 8 produces and characterise:

1. Wall-clock time for one full iteration of all thirteen scripts. This
   sets the realistic upper bound on iteration count — if one iteration
   takes 30 seconds, 10,000 is too many for a test that ships.
2. Steady-state managed heap size after the first ten iterations. This
   sets the realistic warmup window — there is no point asserting against
   the heap before it has reached its plateau.
3. Iteration-to-iteration variance in heap size at steady state. This
   sets the realistic tolerance — if normal variance is already 8%, a
   10% tolerance is noise, not a signal.

The output of the calibration run is a short note in the decisions log
(or appended to D-302 as a calibration entry) and the locked numbers
written to `bench/Grob.Benchmarks/baseline/stability.json` with the
calibration date and the iteration/warmup/tolerance trio.

**Tolerance shape:** hard threshold against post-warmup heap size. One
opinion, no configuration — consistent with the rest of Grob's tooling.
No per-script tolerance, no per-category tolerance.

**Cadence:** separate from the per-sprint regression run. The stability
test runs at a longer cadence — once per release, or on demand when
something specific is suspected.

**Failure mode:** if the stability test exceeds the threshold, that is
a release-gate fail. The release does not ship until the leak is
diagnosed and the test passes again.

---

## 7. Test Materials — Setup, Teardown, Storage and Lifecycle

This section is the operational layer. It covers what each benchmark
actually does on every iteration, where its inputs come from, and how
those inputs are kept stable enough that the baseline means something.

### 7.1 The Fixtures Directory

All benchmark inputs that are not C# code live under one root:

```
bench/Grob.Benchmarks/
├── Fixtures/
│   ├── EndToEnd/                  ← Frozen copies of the thirteen validation scripts
│   │   ├── 01-calculator.grob
│   │   ├── 02-file-organiser.grob
│   │   └── … (eleven more)
│   ├── Compile/
│   │   └── synthetic-large.grob   ← Auto-generated, gitignored, deterministic
│   └── Vm/                        ← (Empty — VM benchmarks build chunks in code)
├── Generators/
│   └── SyntheticLargeGenerator.cs ← Generates synthetic-large.grob deterministically
└── …
```

Three classes of test material, three different storage policies.

### 7.2 Hand-Constructed VM Chunks — Code, Not Files

VM execution benchmarks construct their `Chunk` instances directly in
C#. There is no `.chunk` file format and there should not be one — a
hand-constructed chunk is code, and committing it as code keeps it
reviewable, refactorable and visible alongside the benchmark that
consumes it.

```csharp
internal static class VmFixtures
{
    public static Chunk TightArithmeticLoop { get; } = BuildLoop();
    public static Chunk RecursiveFibonacci  { get; } = BuildFib();
    // …

    private static Chunk BuildLoop() { /* emit opcodes */ }
}
```

The static read-only initialisation means construction cost is paid
once at type initialisation, not per iteration. The chunk is reused
across every iteration of the benchmark that consumes it.

### 7.3 End-to-End Scripts — Frozen Copies in Fixtures

End-to-end benchmarks consume `.grob` source files. These are **frozen
copies** of the thirteen validation suite scripts, kept under
`Fixtures/EndToEnd/` and **not** referenced live from `tests/Grob.Integration.Tests/`.

**Why frozen copies, not live references.** Validation suite scripts
evolve. A change to a script in `tests/` for legitimate test-quality
reasons (clearer assertions, expanded coverage) could silently invalidate
the benchmark baseline by changing the workload without anyone
noticing. Frozen copies decouple the two lifecycles: tests evolve for
correctness reasons, benchmarks evolve for measurement reasons.

**Refresh ritual.** When a validation suite script in `tests/` changes
in a way that genuinely should propagate to the benchmark workload,
the engineer:

1. Copies the new script into `Fixtures/EndToEnd/`.
2. Reruns the end-to-end benchmark suite.
3. Commits the new fixture file and the updated baseline together,
   with the rationale in the commit message.

This is the same discipline as updating the baseline after a
deliberate performance trade-off: a baseline change is part of the
diff, not a side effect.

### 7.4 Synthetic Large Script — Deterministic Generation

The compile-time category needs a script larger than any of the
thirteen validation suite scripts, to surface throughput characteristics
that small scripts cannot. Committing a 1000+ line file would bloat
the repo and produce diff noise.

Solution: **deterministic generation**. A C# generator class
(`Generators/SyntheticLargeGenerator.cs`) produces
`Fixtures/Compile/synthetic-large.grob` on demand from a fixed seed.
The generator is committed; the output is gitignored. First benchmark
run generates the file; subsequent runs detect it and reuse it.

The generator outputs a script with a known mix of constructs —
function declarations, expressions, control flow, string interpolation
— in proportions that approximate "a realistic 1000-line Grob script
would look roughly like this". The exact mix is recorded as comments
at the top of the generated file so the workload is inspectable.

If the generator changes, the next benchmark run regenerates the file
with the new mix, and the baseline update is committed in the same
change.

### 7.5 BenchmarkDotNet Setup/Teardown — What Runs When

Getting setup/teardown wrong is the single most common BenchmarkDotNet
mistake. The cost of setup leaks into the measured operation and the
numbers stop meaning anything. The right answer differs by category.

**Compile-time benchmarks:**

- `[GlobalSetup]` reads the source file from disk into a `string` field
  and resolves the file path. This runs **once** per benchmark class.
- The measured method runs the compiler pipeline (lex, parse, type
  check, emit) on the already-loaded `string`. Disk I/O is **not**
  measured.
- `[GlobalCleanup]` is empty — there is nothing to release. Each
  invocation of the measured method produces a fresh `Chunk` that the
  GC will reclaim.

**VM execution benchmarks:**

- `[GlobalSetup]` builds the hand-constructed `Chunk` (via the static
  fixture properties from §7.2) and constructs a `GrobVM` instance.
- `[IterationSetup]` resets the VM's stack, globals and call frame
  array to a clean state. This is necessary because the measured
  method mutates VM state, and a benchmark that runs 1000 iterations
  needs each iteration to start from the same state.
- The measured method runs the chunk on the prepared VM.
- `[GlobalCleanup]` releases the VM.

**End-to-end script benchmarks:**

- `[GlobalSetup]` reads the script source from disk into a `string`
  field. This runs once.
- `[IterationSetup]` constructs a fresh `GrobVM`. This runs every
  iteration because end-to-end benchmarks measure the full lifecycle
  including VM construction. The alternative — reusing a VM — would
  hide construction cost, which is a real part of script startup
  performance.
- The measured method compiles the source and runs the resulting
  `Chunk` on the iteration-fresh VM.
- `[GlobalCleanup]` is empty.

**Stability test:**

- The stability test does **not** use BenchmarkDotNet — it is a
  separate console application loop under `Stability/`. BenchmarkDotNet
  is optimised for measuring small operations many times; the
  stability test runs large operations and inspects steady-state heap.
  Different shape, different tool.
- The stability loop reads each script once, constructs a fresh VM per
  iteration, executes, releases the VM, and records `GC.GetTotalMemory(forceFullCollection: true)`
  at the warmup boundary and at the end. The `forceFullCollection: true`
  ensures the reading is meaningful (no uncollected garbage masquerading
  as retained memory).

### 7.6 Baseline Files — One Per Category

```
bench/Grob.Benchmarks/baseline/
├── compile.json        ← BenchmarkDotNet JSON for compile-time category
├── vm.json             ← BenchmarkDotNet JSON for VM execution category
├── endToEnd.json       ← BenchmarkDotNet JSON for end-to-end category
└── stability.json      ← Calibration values + last passing result
```

`stability.json` is hand-curated, not BenchmarkDotNet output. Shape:

```json
{
    "calibrated": "2026-MM-DD",
    "iterations": 10000,
    "warmup": 100,
    "tolerancePercent": 10,
    "lastPassingHeapBytes": 0,
    "lastRun": "2026-MM-DD"
}
```

The `lastPassingHeapBytes` value is the steady-state heap size the
last passing stability run observed. The next run asserts within
tolerance of this value, not just within tolerance of its own warmup
window — that way, slow growth across releases (sub-threshold each
time but accumulating) shows up against the historical baseline.

---

## 8. Baseline Storage

Baseline results are committed to the repository as JSON, alongside
the benchmarks themselves. Each gating category carries **two** baseline
files — a rolling baseline and a frozen origin baseline — plus a single
`policy.json` holding the thresholds (D-313):

```
bench/Grob.Benchmarks/baseline/
├── policy.json            ← thresholds and gating categories (data, not code)
├── compile.json           ← rolling: updated each sprint
├── compile.origin.json    ← frozen origin: the cumulative anchor
├── vm.json
├── vm.origin.json
├── endToEnd.json
├── endToEnd.origin.json
└── stability.json
```

BenchmarkDotNet produces JSON output natively. Committing it gives:

- Offline comparison (no external service dependency)
- A single source of truth (no drift between repo state and external
  state)
- Visible history through git log on the baseline files themselves
- Reviewable change in pull requests — a baseline update is part of the
  diff, not a side effect

**Rolling versus origin (D-313).** The rolling baseline
(`<category>.json`) is the per-sprint anchor: it is updated at each
sprint close and the next sprint's run is measured against it. The
origin baseline (`<category>.origin.json`) is the cumulative anchor: it
is frozen the first time a category's baseline is established and is
**not** updated each sprint. When a category's baseline is first
committed, the same JSON is written to both files; thereafter only the
rolling file moves. The origin is re-frozen only by a deliberate,
logged event — most obviously after the optimisation sprint pays the
accumulated debt down, at which point the new, better numbers become the
origin for the remainder of the v1 track.

**Baseline updates are deliberate.** A regression-aware sprint that
intentionally trades some performance for correctness, clarity or safety
updates the rolling baseline as part of the same commit, with the
rationale in the commit message and a decisions-log entry. The
regression gate (§9) never updates a baseline on its own — it reads,
compares and reports; the human commits the update.

`policy.json` holds the per-sprint threshold, the cumulative threshold
and the list of categories with their gating flags. It is data so the
cumulative budget is a number the maintainer edits, not a constant
recompiled into the tool. Its shape:

```json
{
  "perSprintPercent": 5.0,
  "cumulativePercent": 12.0,
  "categories": [
    { "name": "compile",  "namespacePrefix": "Grob.Benchmarks.Compile",  "baseline": "compile.json",  "gating": true  },
    { "name": "vm",       "namespacePrefix": "Grob.Benchmarks.Vm",       "baseline": "vm.json",       "gating": false },
    { "name": "endToEnd", "namespacePrefix": "Grob.Benchmarks.EndToEnd", "baseline": "endToEnd.json", "gating": false }
  ]
}
```

### 8.1 Canonical Production Path — GitHub Actions Workflow

The `benchmark.yml` GitHub Actions workflow (`.github/workflows/benchmark.yml`)
is the canonical entry point for producing committed baselines and for
sprint-close regression comparisons. Trigger: manual (`workflow_dispatch`).
No benchmark run intended to update a committed baseline should come from a
local machine.

**Procedure:**

1. Trigger the workflow on GitHub (Actions tab → Benchmarks → Run workflow).
2. Download the `benchmark-results-windows-latest-<run-id>` artifact when
   the run completes (90-day retention).
3. Extract. Locate the `-report-full.json` for the relevant benchmark class
   — not `-report-brief.json`. The full report includes `HostEnvironmentInfo`
   (CPU model, OS, runtime version, GC mode), which makes the baseline
   traceable and comparable.
4. Copy the `-report-full.json` to the appropriate path under
   `bench/Grob.Benchmarks/baseline/` and commit.

**Commit message convention.** Record the workflow run ID, the runner used
(`windows-latest`) and the sprint context. Example:
`bench: first compile-time baseline (windows-latest, run #42) per D-302 / D-309`.
This anchors the file to a specific, reproducible origin.

**Runner consistency.** The canonical runner is `windows-latest` (D-309).
All future baseline production and regression-check runs must use the same
runner type. Cross-runner comparisons are not valid — the absolute numbers
are not comparable across runner types.

**The regression gate runs inside this workflow (D-313).** After the
benchmark run, the workflow invokes `tooling/Grob.BenchCheck` against the
committed baselines and the fresh `-report-full.json`. The tool computes
the two-axis comparison (§9), writes a per-benchmark delta table to the job
summary and exits non-zero on a breach — so the workflow run itself goes
red when a gating category regresses. The check reads only; it never
commits a baseline. Committing an updated baseline remains the deliberate
manual step above. A run triggered on a non-canonical runner
(`ubuntu-latest`) fails the gate by design — the runner guard refuses a
cross-runner comparison rather than producing a meaningless green.

### 8.2 Local Invocation — Debugging and One-Off Exploration

```bash
dotnet run -c Release --project bench/Grob.Benchmarks
```

Local invocation remains supported and is the right tool when:

- A benchmark crashes and you need to reproduce it quickly.
- You want to explore the effect of a JIT or configuration change before
  triggering a workflow run.
- You are developing a new benchmark class and need a fast feedback loop.

Local results are **not** committed as baselines. Hardware, background load,
GC state and runtime configuration vary too much across machines to anchor
a 5% regression gate. Use the workflow (§8.1) for anything intended to become
the committed baseline.

Local runs write to `BenchmarkDotNet.Artifacts/` relative to the working
directory. This path is in `.gitignore` and must never be committed.

---

## 9. Per-Sprint Regression Policy

The end of every sprint runs the full compile-time, VM execution and
end-to-end benchmark suite through the `benchmark.yml` workflow (§8.1).
The stability test runs separately at a longer cadence (per release). The
benchmark run belongs **after** the sprint's correctness QA loop has
landed and the code is final — measuring a state that is about to change
wastes the run.

The policy has **two comparison axes** (D-313). A single axis — comparing
only against the immediately prior baseline and then updating it — ratchets:
a regression below the gate passes, becomes the new normal and a steady
few-percent-per-sprint creep compounds invisibly. The two axes close that.

**Axis 1 — per-sprint gate (noise filter).** New results compared against
the **rolling** baseline (`<category>.json`). Threshold **5%** on a gating
category. This catches an acute regression — the sprint that boxes a value
on the dispatch hot path and jumps 30%. 5% is a noise floor, not a budget:
most sprints touch no hot path and should read near 0%. It is set at 5%
because run-to-run variance on the shared `windows-latest` runner is
genuinely a few percent; a tighter gate fires on infrastructure noise, and
tightening it has a precondition — a quieter measurement first.

**Axis 2 — cumulative ceiling (anti-ratchet).** New results compared
against the **frozen origin** baseline (`<category>.origin.json`).
Threshold **12%** total drift to v1. A slow creep trips this within a few
sprints even when every individual step is inside the 5% per-sprint gate.
Read it against the arc: Grob lands benchmarking before optimisation, so
features add real, correct overhead (checked arithmetic, nil checks, the
extra type-checker passes) through the build sprints, and the dedicated
optimisation pass claws it back. The 12% is sized for "necessary trades
through features, recovered at optimisation", not "never regress".

**Comparison validity.** Both axes are only valid between runs on the same
runner type (`windows-latest` per D-309 / §8.1). The gate tool guards this
from `HostEnvironmentInfo` and refuses a cross-runner comparison.

**Which category gates.** The end-to-end script benchmarks are the primary
gate — they measure the thing that matters. Compile-time and VM execution
are diagnostic, there to localise where an end-to-end regression came from.
**During build-out, before the end-to-end workload exists** (its thirteen
validation scripts need control flow in Sprint 4 and functions in Sprint
5), **compile-time gates cumulatively instead.** For a scripting language
that compiles-and-runs on every invocation with no persistent process,
compile time is real wall-clock time-to-result, not merely diagnostic — a
script that goes from 50 ms to 200 ms to compile is a genuine regression
even with execution unchanged. VM execution stays informational while it
remains a first baseline with no origin to anchor against. When end-to-end
becomes live it takes over as the gate and compile/VM drop to
informational. That flip is a deliberate `gating` edit in `policy.json`,
not an automatic change.

**The gate is mechanical, not eyeballed.** `tooling/Grob.BenchCheck`
performs both comparisons inside the workflow (§8.1) and the run goes red
on a breach. Ownership is split: the **workflow** decides pass/fail; the
**maintainer** adjudicates a failure.

**On a regression flag:**

1. Diagnose. Use the compile-time and VM execution breakdowns to localise
   where the slowdown lives.
2. Either fix it before the sprint closes, or accept it as a deliberate
   trade-off — update the rolling baseline, capture the decision in the
   decisions log and (rarely, for a sanctioned step-change) re-freeze the
   origin.

**On an improvement:** update the rolling baseline. A 15% speedup is
welcome, but if it is not captured the next sprint will not notice when
half of it is lost. Leave the origin frozen — the improvement shows as
headroom against the cumulative ceiling, which is the point.

An hour of automated benchmarking at the close of a two-week sprint is
rounding-error overhead against the cost of catching regressions late.

---

## 10. No CLI Surface

`grob bench` is not a v1 CLI command.

The validation suite scripts are language-level fixtures, exercised
by users implicitly when they run their own Grob scripts. The
benchmarks are implementation-level infrastructure, exercised by the
engineer working on Grob itself. Different audiences, different
lifecycles, different concerns. Conflating them at the CLI would
suggest Grob users are expected to think about VM internals — they
are not.

The canonical production path for committed baselines is the `benchmark.yml`
GitHub Actions workflow (§8.1). One-off debugging and exploration runs use
`dotnet run -c Release --project bench/Grob.Benchmarks` locally (§8.2).
The README documents both paths. The CLI stays focused on running Grob
scripts.

This may change post-v1 if there is a genuine reason for users to
benchmark Grob scripts themselves (a `grob bench myscript.grob` for
profiling user code is a plausible later feature). It is out of scope
for v1.

---

## 11. Implementation Timing

`bench/Grob.Benchmarks` is created as a skeleton at the end of
**Sprint 2**, the first sprint that produces meaningful code to
benchmark. The compile-time category lands first because the compiler
exists earliest. VM execution and end-to-end categories grow alongside
the features they exercise, sprint by sprint.

The baseline JSON files are committed for the first time at the close
of Sprint 2. Each subsequent sprint updates them. Baselines are produced
via the `benchmark.yml` Actions workflow (§8.1 / D-309).

The stability test lands at the close of **Sprint 8** — the first
sprint with a stdlib substantial enough that meaningful long-run leak
detection makes sense.

**Stability test calibration ritual — Sprint 8 close.** Before the
stability test's iteration count, warmup window and tolerance are
locked into `stability.json`, the calibration described in §6 runs.
A single-iteration pass characterises wall-clock time per iteration,
steady-state heap, and iteration-to-iteration variance. The numbers
that ship in `stability.json` are derived from this characterisation,
not from the §6 placeholders. The calibration result is recorded as
an addendum to D-302 in the decisions log.

---

## 12. What This Document Does Not Cover

- **Profiling.** BenchmarkDotNet integrates with the standard .NET
  profilers (dotTrace, PerfView). When profiling is needed, the existing
  tooling is the answer; no Grob-specific infrastructure is built.
- **Per-opcode dispatch latency analysis.** Out of scope for v1.
  BenchmarkDotNet measures patterns, not individual instructions. If
  opcode-level analysis is required post-v1, it gets its own design.
- **Comparative benchmarking against Python, PowerShell, Go.**
  Tempting but a separate exercise — different runtimes, different
  fairness questions, different audiences. Post-v1.
- **CI integration of benchmarks.** Benchmarks do not run in CI on
  every commit. Committed baselines and sprint-close regression
  comparisons are produced via the `benchmark.yml` manual-dispatch
  workflow (§8.1, D-309). The benchmark project also supports local
  invocation for one-off debugging and exploration (§8.2). Per-commit
  automated benchmarking is out of scope for v1.

---

_This document is the authoritative reference for Grob's benchmarking_
_strategy. D-302 records the original decision. D-309 (May 2026) refines_
_the baseline production mechanism: baselines are produced via the_
_`benchmark.yml` GitHub Actions workflow with `windows-latest` as the_
_canonical runner. `grob-v1-requirements.md` §12 and_
_`grob-solution-architecture.md` cite this document for detail._

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
│       ├── Attribution/         ← Differential allocation-attribution fixtures
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

These execute a pre-built `Chunk` — the compiler is not involved **in the
measured region**. This isolates VM performance from compiler performance.
A regression in VM execution time on a tight arithmetic loop tells
you something very different from a regression in the same workload
when measured end-to-end.

**Implementation note (D-385/D-386/D-387).** `VmBenchmarks.RunSource` drifted
from this definition for several sprints, running the full pipeline (lex,
parse, type-check, compile, execute) inside the measured region. D-387
corrected it: compilation now happens once in `[GlobalSetup]`, and each
`[Benchmark]` method is exactly `vm.Run(chunk)`.

**What "pre-built" means, exactly.** Through Sprint 9 this section read
"hand-constructed `Chunk` instances — the compiler is not involved", and the
fixtures are not hand-constructed: they are ordinary `.grob` sources compiled
by the real compiler in `[GlobalSetup]`. D-386 Q1' ratified that model and
D-387 built it, so the wording above is corrected to state the property that
actually holds and that the category depends on — the compiler is outside the
measured region — rather than one particular means of reaching it. Compiling
real sources keeps the fixtures readable and keeps them measuring the bytecode
the compiler actually emits, which a hand-written chunk would drift from.

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

### 4.2a Attribution Benchmarks (`bench/Grob.Benchmarks/Attribution/`)

**Question answered:** where, specifically, does a whole script's allocation
go?

A dedicated, permanent, **non-gating** (`policy.json`: `"allocGating": false`)
category added by D-385/D-386 (phase 1 findings:
`docs/design/bench-allocation-attribution.md`). The `attr-*` fixtures are
**instruments, not guards** — they measure the pipeline floor, loop
machinery and per-native-call overhead by differencing successive
whole-script fixtures, not features anyone should gate on. A regression in
`attr-empty` means "the compiler pipeline allocates more", which is already
`compile`'s job to catch — gating on it here would gate on the wrong
category.

**A difference is only as clean as its pair.** The goal is that each pair
differs by exactly one thing, and the fixture set is built so that a correct
pair exists for every quantity the category claims to attribute — but that is
a property of the *pair*, not of the category, and it does not hold for every
pair one could form. Two rules keep the subtractions honest:

- **Match the dispatch path.** Phase 1 §4 established that Stdlib-plugin
  natives (registered once, `GetGlobal` per call) and array/map instance
  methods (bound fresh by `GetProperty`, paying a `NativeFunction`/delegate/
  display-class triple per call) are structurally different costs. So
  `attr-build` − `attr-array-dispatch` isolates array growth, while
  `attr-build` − `attr-native` spans two paths and yields the **combined**
  dispatch tax plus growth.
- **No unmatched terminal work.** No fixture ends with a `print` or other
  trailing operation its counterpart lacks, so a subtraction never silently
  adds or removes output work. `attr-empty` is statement-free for this
  reason.

Whole-script by design: perfect isolation of a fragment inside its own
measured region is unattainable, so every fixture in this category measures
the full lex/parse/type-check/compile/run pipeline, and the differential
technique does the isolating instead of `[GlobalSetup]`.

Fixture set: `attr-empty` (pipeline + VM setup floor, statement-free),
`attr-range` (range-loop machinery), `attr-native` (Stdlib-plugin native-call
dispatch), `attr-array-dispatch` (array-member dispatch with no growth — the
growth-free control for `attr-build`, and the dedicated fixture phase 1 §4
recorded as missing), `attr-build` (array-member dispatch plus array growth),
`attr-map-build` (map construction only), `attr-snapshot-empty` (the
`attr-build` loop plus an empty-body `for...in`, isolating the D-383
contents-snapshot copy from any iteration-body cost).
`StringMethodsPlugin` is registered uniformly across every fixture in this
category (D-385 Q4) so its one-time registration cost cancels out of every
pairwise subtraction.

### 4.3 End-to-End Script Benchmarks (`bench/Grob.Benchmarks/EndToEnd/`)

**Question answered:** does Grob run real scripts fast enough?

The thirteen validation suite scripts, run through the full pipeline:
source file on disk → compiled → executed → exit. This is the workload
that actually matters. If end-to-end numbers regress, something is
wrong even if the micro-benchmarks all look fine.

This category is the *intended* primary gate, and is not gating yet: the
validation-suite corpus it needs is still F8-open, so it carries
`allocGating: false` and no allocation ceiling today, and `compile` holds the
allocation-percent gate in the meantime (§9.1's matrix, §9.2's flip
condition). It holds only that one: `vm` and `attribution` gate on their
absolute allocation ceilings whatever their `allocGating` flag says (D-333,
D-391). The other categories exist to help diagnose regressions surfaced
here.

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

**Calibration ritual at Sprint 8 close (mechanically corrected by D-346).**
Before locking these numbers in the baseline, run a single-iteration pass
against the stdlib-substantial build that Sprint 8 produces and characterise:

1. Wall-clock time for one full iteration of the **Sprint-8-runnable script
   set** — not all thirteen. `grob-sample-scripts.md`'s validation scripts
   each depend on at least one Sprint-9 module (`fs`, `date`, `csv`, `json`,
   `process`) or an as-yet-unbuilt plugin (`Grob.Http`, `Grob.Crypto`), so
   none of them runs at Sprint 8 close; the runnable set is the five
   sprint-close smoke scripts (`hello`, `calculator`, `functions`, `types`,
   `errors`) plus the Sprint 8 close-gate script (`stdlib.grob`, D-337). The
   full validation-suite stability run is deferred to a v1 release-gate step
   once Sprint 9 lands the remaining modules. This sets the realistic upper
   bound on iteration count — if one iteration takes 30 seconds, 10,000 is
   too many for a test that ships. (D-346 also notes, separately: the
   "thirteen" count itself is stale — `grob-sample-scripts.md` has only ever
   held eleven scripts; see D-346 for the full account. This does not change
   the runnable-set finding above, which is zero either way.)
2. Steady-state managed heap size after the first ten iterations. This
   sets the realistic warmup window — there is no point asserting against
   the heap before it has reached its plateau. **Caution from D-346's actual
   run:** ten iterations was too short a horizon to catch a one-time
   cache/registry warm-up step that only appeared between iterations 1000
   and 2000 — a longer checkpoint sweep is worth the extra runtime before
   locking the warmup window.
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
│   ├── Vm/                        ← Fixtures compiled once in [GlobalSetup] (D-387)
│   └── Attribution/                ← Differential allocation-attribution fixtures
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
├── attribution.json    ← BenchmarkDotNet JSON for the attribution category
├── endToEnd.json       ← BenchmarkDotNet JSON for end-to-end category (declared, not yet committed — F8)
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
├── attribution.json       ← first measurement, canonical run 30707325720 (D-391)
├── attribution.origin.json
├── endToEnd.json          ← declared in policy.json, not yet committed (F8)
├── endToEnd.origin.json
└── stability.json
```

**`vm.json`/`vm.origin.json` were re-established, not merely re-captured.**
D-387 rebuilt the `vm` category to measure `vm.Run(chunk)` alone rather than
the full pipeline (D-385/D-386 Q1'), which also forced `[IterationSetup]`'s
`InvocationCount=1`/`UnrollFactor=1`, removing BenchmarkDotNet's normal
unrolling amortization — a second, independent change to what the numbers
mean, not only which region of code they cover. The previously-committed
files predated both changes; comparing a fresh post-rebuild run against them
produced raw per-sprint deltas of +343.8%/+147.6%/+141.5% that were never
real regressions (D-390's triggering evidence). Per **D-390** — whenever a
benchmark's measurement methodology changes, the rolling and origin
baselines are re-frozen in the same change, unconditionally, and recorded as
a re-establishment rather than a rebaseline — both files were replaced from
the canonical run `30707325720` (`windows-latest`, AMD EPYC 7763,
2026-08-01) in the same change that derived `vm`'s new allocation ceilings
(D-391). `attribution.json`/`attribution.origin.json` are a first
measurement from the same run, not a re-establishment — the category has
never had a committed baseline before.

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

`policy.json` holds the allocation threshold and the list of categories with
their allocation-gating flags and allocation ceilings, plus the historical
per-sprint/cumulative/significance time figures (§9.1) retained for the CLI
report header's context only — time never gates on any category (D-395/
D-396). It is data so the cumulative budget is a number the maintainer edits,
not a constant recompiled into the tool. Its shape (D-333 added `allocPercent`
and `timeSignificanceK`; D-391 replaced the single global `lohTripwireBytes`
constant with a per-category `allocationCeilingBytes` default and optional
per-benchmark `benchmarkAllocationCeilings` overrides — this example was
also missing the `attribution` category added by D-387, folded in here;
D-396 renamed `gating` to `allocGating`, scoping it to the allocation-percent
axis alone):

```json
{
  "perSprintPercent": 5.0,
  "cumulativePercent": 12.0,
  "allocPercent": 10.0,
  "timeSignificanceK": 3.0,
  "categories": [
    {
      "name": "compile",
      "namespacePrefix": "Grob.Benchmarks.Compile",
      "baseline": "compile.json",
      "allocGating": true,
      "allocationCeilingBytes": 20100
    },
    {
      "name": "vm",
      "namespacePrefix": "Grob.Benchmarks.Vm",
      "baseline": "vm.json",
      "allocGating": false,
      "allocationCeilingBytes": 4700,
      "benchmarkAllocationCeilings": {
        "Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn": 637900,
        "Grob.Benchmarks.Vm.VmBenchmarks.Run_MapForIn": 1240000
      }
    },
    {
      "name": "attribution",
      "namespacePrefix": "Grob.Benchmarks.Attribution",
      "baseline": "attribution.json",
      "allocGating": false,
      "allocationCeilingBytes": 55400,
      "benchmarkAllocationCeilings": {
        "Grob.Benchmarks.Attribution.AttributionBenchmarks.Run_AttrNative": 278700,
        "Grob.Benchmarks.Attribution.AttributionBenchmarks.Run_AttrArrayDispatch": 552100,
        "Grob.Benchmarks.Attribution.AttributionBenchmarks.Run_AttrBuild": 610900,
        "Grob.Benchmarks.Attribution.AttributionBenchmarks.Run_AttrSnapshotEmpty": 700300,
        "Grob.Benchmarks.Attribution.AttributionBenchmarks.Run_AttrMapBuild": 1128400
      }
    },
    {
      "name": "endToEnd",
      "namespacePrefix": "Grob.Benchmarks.EndToEnd",
      "baseline": "endToEnd.json",
      "allocGating": false
    }
  ]
}
```

`allocPercent` governs the per-sprint allocation-growth axis (§9), gated
per category by `allocGating`. `allocationCeilingBytes`/
`benchmarkAllocationCeilings` govern the absolute allocation ceiling (§9,
§9.2) — a category with neither configured (`endToEnd`, above, while F8 is
open) has no ceiling to breach yet. `perSprintPercent`/`cumulativePercent`/
`timeSignificanceK` are retained and still rendered in the CLI report header
for a reader's context, but no longer drive any classification — time is
informational on every category regardless of these figures or of
`allocGating` (D-395/D-396). The committed baseline JSON already carries the
data `allocPercent` needs (`Memory.BytesAllocatedPerOperation`,
BenchmarkDotNet's native output), so it required no benchmark-workload
change to add.

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
runner type. `windows-latest` is a label, not a hardware pin — the hosted
pool serves more than one CPU generation, so two runs sharing the label can
still land on different silicon (D-333 confirmed an AMD EPYC 7763 baseline
against an Intel Xeon Platinum 8370C verification run, both labelled
`windows-latest`). The gate's CPU-identity guard (§9) handles that gap by
keying on the CPU itself, not the label.

**The regression gate runs inside this workflow (D-313, hardened by
D-333, D-395/D-396).** After the benchmark run, the workflow invokes
`tooling/Grob.BenchCheck` against the committed baselines and the fresh
`-report-full.json`. The tool computes the time and allocation comparisons
(§9), writes a per-benchmark delta table to the job summary and exits
non-zero on a breach — so the workflow run itself goes red when an
`allocGating` category's allocation-percent regresses, or when any category's
allocation meets or exceeds its allocation ceiling. Time deltas are always
computed and shown but never fail the run. The check reads only; it never
commits a baseline.
Committing an updated baseline remains the deliberate manual step above.
Allocation gates regardless of which CPU produced the run; the time axes do
not gate at all, on a matched CPU or a mismatched one (D-395/D-396). The
CPU-identity guard still runs, and its only remaining job is to annotate the
report — against the rolling baseline for the per-sprint Δ, and against the
origin baseline for the cumulative Δ, which can disagree with the fresh run's
CPU even when the rolling baseline agrees. Hosted runners cannot be
CPU-pinned, so a hardware swing has to be visible in the report rather than
mistaken for a slowdown.

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

The policy has **two time comparison axes** (D-313), both informational on
every category since D-395/D-396 (below), plus, since D-333, an
**allocation axis** that is the sole gating signal. A single axis —
comparing only against the immediately prior baseline and then updating it —
ratchets: a regression below the gate passes, becomes the new normal and a
steady few-percent-per-sprint creep compounds invisibly. The allocation axis
closes that gap the Sprint 6 run exposed — a deterministic, CPU-independent
signal (`[MemoryDiagnoser]`'s `BytesAllocatedPerOperation`) that the gate
previously recorded into every baseline but never acted on, so a defect like
D-332's Large Object Heap allocation read as merely informational instead of
failing outright.

**Axis 1 — per-sprint (noise filter). Informational only, not enforced
(D-395/D-396).** New results compared against the **rolling** baseline
(`<category>.json`). `policy.json` still carries `perSprintPercent` and
`timeSignificanceK`, rendered in the CLI header for context, but neither
drives a classification — D-395 found `BenchCheck`'s ×3σ significance filter
(`max(perSprintPercent, timeSignificanceK × relativeStdDev)`, `relativeStdDev`
a benchmark's own within-run `StandardDeviation` as a percentage of its
`Mean`) is structurally blind to **between-run** variance on hosted,
non-pinned runners: a CPU-matched comparison moved **+12.0%** between two
consecutive runs on unchanged code with a **0.0%** allocation delta, well
past the filter's own ~6% threshold. D-396 retired the enforcement rather
than widen the threshold further (D-313's ratchet rule forbids raising a
threshold specifically because it fired) — Δ time is still computed and
shown every row, so a real slowdown stays visible, it just cannot fail the
build on its own.

**Axis 2 — cumulative (anti-ratchet). Informational only, not enforced
(D-395/D-396).** New results compared against the **frozen origin** baseline
(`<category>.origin.json`). `cumulativePercent` (**12%**) is likewise
retained and rendered for context — the arc it was sized for still matters
when reading the number (Grob lands benchmarking before optimisation, so
features add real, correct overhead through the build sprints and the
dedicated optimisation pass claws it back; 12% was sized for "necessary
trades through features, recovered at optimisation") — but a slow creep no
longer fails the gate on its own; only a paired allocation-percent or
allocation-ceiling breach does.

**Axis 3 — allocation (D-333). The sole gating axis.** New results compared
against the **rolling** baseline's `Memory.BytesAllocatedPerOperation` on two
sub-checks:

- **Percent-vs-baseline** (`allocPercent`, **10%**): an `allocGating: true`
  category's allocation growing by more than this fails the gate, tighter
  than the old time axes because allocation is deterministic (the same code
  path allocates the same bytes run to run), so it only needs to absorb
  legitimate minor variance, not hardware or scheduler noise. On an
  `allocGating: false` category the percentage is reported, never failing.
- **The allocation ceiling** (`allocationCeilingBytes`/
  `benchmarkAllocationCeilings`, §9.2): any benchmark, `allocGating` or not,
  whose fresh allocation meets or exceeds its applicable ceiling fails the
  gate outright — unconditional, exactly as it ignored the old `gating` flag
  before D-396's rename. This is the check that would have caught D-332 on
  day one instead of filing it under "info" — an informational category is
  still forbidden from silently landing on an unbounded allocation.

### 9.2 Allocation Ceiling — Category-Default and Per-Benchmark Thresholds (D-385 Q2, D-391)

**What it was, and why that stopped working.** D-333 introduced
`lohTripwireBytes` as a single global constant (85,000 B) — the .NET Large
Object Heap's *single-object* promotion threshold, borrowed because it was
the nearest available number, not because Grob benchmarks anything about the
LOH specifically. `BenchCheck` always compared it against
`Memory.BytesAllocatedPerOperation`, BenchmarkDotNet's *total-per-operation*
allocation — a total-allocation ceiling in function from day one, mis-named
after an unrelated CLR concept it never measured (D-385 Q2). Once the
`for...in` benchmarks' legitimate allocation (D-383's contents-snapshot) put
them 6–12× over that number, and once `vm`'s post-D-387 rebuild put its five
fixtures anywhere from 2,344 B to 1,033,320 B — a **441×** internal spread —
one constant could no longer serve every category, or even every benchmark
within one category, without either firing constantly on legitimate
allocation or being too loose to catch anything.

**The mechanism, renamed to what it does.** `policy.json`'s
`lohTripwireBytes` is gone. Each category declares its own
`allocationCeilingBytes` — the default ceiling for every benchmark in that
category — and, where a category's own internal spread is too wide for one
number to serve every fixture, a `benchmarkAllocationCeilings` map keyed by
the benchmark's full name, taking precedence over the category default for
the benchmarks it names. A category with neither configured (`endToEnd`,
while F8 leaves it empty) has no ceiling to breach — absence is not a
silent pass on some implicit default, it means the mechanism has nothing to
compare against yet.

**Granularity: per-category default, per-benchmark override where the
spread demands it — not a rigid "one taxonomy" rule.** D-385 Q2 allows
"per-category (or per-fixture-shape)"; the two categories that need more
than a flat default resolve it differently, on the same underlying logic
(does a shared number span too wide a range to mean anything):

- **`compile`** (16,728 B / 9,856 B, both compile-time-shaped) — one
  category default, no overrides.
- **`vm`** — three scalar-dispatch fixtures (`Run_DeclAndArith` 2,344 B,
  `Run_Interpolation` 3,880 B, `Run_ControlFlow` 2,504 B) share a category
  default; `Run_ArrayForIn` (531,616 B) and `Run_MapForIn` (1,033,320 B) —
  themselves ~2× apart — each get their own override rather than a shared
  ceiling between the two, which would just reintroduce a smaller version of
  the same "one number spans too much" problem.
- **`attribution`** — the two floor-shaped fixtures that measure the
  pipeline-plus-harness cost every other fixture is subtracted against
  (`Run_AttrEmpty` 42,849 B, `Run_AttrRange` 46,145 B) share a category
  default; the five larger, structurally distinct diagnostic fixtures
  (`Run_AttrNative`, `Run_AttrArrayDispatch`, `Run_AttrBuild`,
  `Run_AttrSnapshotEmpty`, `Run_AttrMapBuild`) each get their own override,
  for the same reason as `vm`'s two `for...in` fixtures — `attr-native`
  (232,239 B) to `attr-map-build` (940,315 B) is itself a ~4× spread.
  `attr-empty` and `attr-range` are deliberately not folded into "let
  `compile` cover the small end" the way a purely category-wide ceiling
  might assume: `attr-empty` measures the whole pipeline *plus*
  `VirtualMachine` construction *plus* plugin registration, so a regression
  in either of the latter two lands here and nowhere `compile` watches — and
  it is the fixture every other `attr-*` subtraction treats as a stable
  floor, so it needs its own live protection, not a borrowed one.

**Headroom — one convention, stated once, applied uniformly: 20% over the
current canonical measured value.** Double `allocPercent` (10%, the
existing per-sprint *relative* growth gate that already tracks incremental
creep) — sized larger because the ceiling is the coarse, rare-firing
absolute backstop, not the primary signal. A fresh allocation reaching
approximately 1.20 × the canonical measurement breaches the ceiling, so any
growth beyond 20% is caught rather than absorbed. Every ceiling below is
`round_to_nearest_100(1.20 × canonical measured value)`, from the canonical
run `30707325720` (`windows-latest`, AMD EPYC 7763, 2026-08-01):

| Benchmark / group | Measured | Ceiling |
|---|---:|---:|
| `compile` (default; basis `Compile_TenPrints`) | 16,728 B | **20,100 B** |
| `vm` scalar-shape default (basis `Run_Interpolation`) | 3,880 B | **4,700 B** |
| `vm` → `Run_ArrayForIn` | 531,616 B | **637,900 B** |
| `vm` → `Run_MapForIn` | 1,033,320 B | **1,240,000 B** |
| `attribution` floor-shape default (basis `Run_AttrRange`) | 46,145 B | **55,400 B** |
| `attribution` → `Run_AttrNative` | 232,239 B | **278,700 B** |
| `attribution` → `Run_AttrArrayDispatch` | 460,101 B | **552,100 B** |
| `attribution` → `Run_AttrBuild` | 509,095 B | **610,900 B** |
| `attribution` → `Run_AttrSnapshotEmpty` | 583,609 B | **700,300 B** |
| `attribution` → `Run_AttrMapBuild` | 940,315 B | **1,128,400 B** |

Three of these ten ceilings sit below the old 85,000 B constant (`compile`,
the `vm` scalar default and the `attribution` floor default) and seven sit
above it: a misapplied global limit replaced by shape-specific ceilings,
tightening the three small shapes and raising the permitted figure for the
seven larger ones. The seven are higher because the old number was never
derived from any of these fixtures' legitimate allocation in the first
place — every one of them already measured above it, so it was never a
threshold those shapes were passing. The `vm`
`for...in` ceilings specifically **include an ≈48 B/call `GetProperty`
closure-capture tax** D-389 attributed — paid once per loop iteration,
independent of array or map size, a Roslyn closure-capture display-class
allocation on the array/map `GetProperty` dispatch path (D-389's full
mechanism and evidence trail: `docs/design/bench-snapshot-residual.md`). Per
D-313's ratchet rule, removing that tax later must *lower* these ceilings,
never require raising them; it is not chased in this entry.

**The revision rule.** A ceiling is legitimately raised when a new benchmark
of a genuinely different allocation shape lands, or a deliberate feature
adds measured, accepted cost — always as a stated, logged decision citing
the new measurement, following this same 20%-headroom convention. It is
**not** legitimately raised to silently absorb an unexplained regression;
that is D-313's ratchet trap restated at the ceiling's own granularity. A
ceiling may be *lowered* freely once a component contributing to it (like
the `GetProperty` tax above) is fixed — the ratchet only forbids motion in
the direction that hides cost.

**CPU identity (D-333, refining D-309's "same runner type" to "same CPU
identity") — now affects reporting only.** `windows-latest` is a label,
not a hardware pin — the post-Interlude-1 verification run proved a 25–37%
time swing between an AMD EPYC 7763 baseline and an Intel Xeon Platinum
8370C run sharing that label, with allocation byte-identical across both.
The gate's guard keys on `HostEnvironmentInfo.ProcessorName`, not the runner
label, comparing the fresh run's CPU against the rolling baseline's for the
per-sprint axis and against the origin baseline's for the cumulative axis —
each mismatch carries its own note, because the two sides can disagree
independently. **Time
was previously gated only when the fresh run's CPU matched the baseline's**;
since D-395/D-396 that distinction no longer changes the classification —
time is informational unconditionally, CPU-matched or not — but the guard
is still evaluated, and a mismatch still adds an explanatory note to the
report (Δ time may be a CPU artefact rather than a real change, so a reader
does not mistake a hardware swing for a slowdown). **Allocation gates
regardless of CPU** — it is deterministic, hardware-independent data, and
was never suppressed by a CPU mismatch even before this change.

**Which category gates.** The allocation axis is the only gating signal
(D-395/D-396); `compile` is the sole category with `allocGating: true`
today, both its 10% percent-vs-rolling check and its absolute ceiling.
`vm`/`attribution`/`endToEnd` gate only on their absolute allocation
ceilings, informational on the percent axis. Time — per-sprint and
cumulative, every category including `compile` — is informational only,
still computed and shown so a real slowdown stays visible, but incapable of
failing the build (D-395 diagnosed a within-run ×3σ noise filter that cannot
see between-run variance; D-396 is the implementation). This supersedes the
original design's stopgap of having `compile` gate cumulatively until
`endToEnd`'s validation-suite corpus existed (§4.3, F8) — that stopgap is
retired, not merely dormant; reinstating any time gate is a future,
separately decided increment (D-395 Q1's rejected options B/C, revisit
triggers noted there).

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

### 9.1 Gating Matrix (D-385/D-386 Q6, made explicit by D-387; time columns retired by D-395/D-396)

The matrix above is stated here explicitly rather than left for a reader to
reconstruct from `policy.json` plus separate decisions:

| Category      | Time, per-sprint | Time, cumulative | Allocation (%) | Allocation ceiling |
|----------------|-------------------|-------------------|-----------------|--------------|
| `compile`      | Informational     | Informational     | Gates | Gates |
| `vm`           | Informational     | Informational     | Informational   | Gates        |
| `attribution`  | Informational     | Informational     | Informational   | Gates        |
| `endToEnd`     | Informational (empty — F8 open) | Informational | Informational   | Not configured (empty — F8 open) |

No category gates on time — this is the one row-shape the whole matrix now
shares, by construction (`BenchCheck.ClassifyTime` computes no threshold and
takes no category input at all; see §9 Axis 1/2). The allocation ceiling
(§9.2) ignores `allocGating` entirely — it fires on any benchmark, in any
category with one configured, per D-333. `endToEnd` has none configured
today; §9.2 explains why an absent ceiling never breaches rather than
silently passing on some implicit default.

**`compile.origin.json`'s CPU-provenance gap is now moot.** Its
`HostEnvironmentInfo.ProcessorName` still reads `"Unknown processor"` (a
pre-D-333 BenchmarkDotNet 0.14.0 capture) and `BenchCheck.SameCpu` still
never treats that placeholder as a match to anything, but this no longer
matters to the cumulative axis's classification — cumulative is
informational for every category unconditionally now (D-395/D-396), not
only because of this placeholder. It does still show: the placeholder never
matches, so every `compile` run carries an origin-side CPU-mismatch note
saying the cumulative Δ may be a hardware artefact. Re-capturing the file
remains a legitimate future act (it would retire that standing note and make
the cumulative figure trustworthy) but is no longer a precondition for
anything gating.

**Flip condition.** When `endToEnd` carries the full validation-suite corpus
(§4.3, not yet built — F8), it becomes the primary allocation-gating category:
`endToEnd.allocGating` flips to `true` and `compile.allocGating` flips to
`false`. This is a deliberate, logged `policy.json` edit, never an automatic
change; the time columns above are unaffected by this flip — they stay
informational regardless of which category holds `allocGating: true`.

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
_canonical runner. D-385/D-386/D-387 (July 2026) correct implementation_
_drift found by the phase 1 allocation-attribution session: `vm` rebuilt_
_to match §4.2's since-inception hand-off-isolated intent (§4.2), the_
_`attr-*` differential fixtures given a dedicated, permanent, non-gating_
_`attribution` category (§4.2a), and §9's gating matrix and compile's_
_cumulative-axis caveat stated explicitly (§9.1). D-395/D-396 (August 2026)_
_retire time as a gating axis on every category — a within-run ×3σ noise_
_filter proved blind to between-run variance — decoupling the_
_allocation-percent axis from the old shared `gating` flag (renamed_
_`allocGating`, §8/§9/§9.1) so allocation keeps gating `compile` without a_
_time-axis flag flip silencing it too. `grob-v1-requirements.md`_
_§12 and `grob-solution-architecture.md` cite this document for detail._

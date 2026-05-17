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

-----

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

-----

## 2. Harness

**BenchmarkDotNet.** Standard tool in the .NET ecosystem. Handles
warmup, JIT compilation effects, GC interaction, statistical analysis,
outlier detection and report generation. Rolling a custom harness for
a hobby project would be wasted effort and the results would be less
credible than what BenchmarkDotNet produces by default.

Run mode is Release configuration only. Debug builds produce numbers
that are not comparable to Release numbers and are not useful as a
baseline.

-----

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

-----

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

-----

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

-----

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

-----

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

-----

## 8. Baseline Storage

Baseline results are committed to the repository as JSON, alongside
the benchmarks themselves:

```
bench/Grob.Benchmarks/baseline/
├── compile.json
├── vm.json
├── endToEnd.json
└── stability.json
```

BenchmarkDotNet produces JSON output natively. Committing it gives:

- Offline comparison (no external service dependency)
- A single source of truth (no drift between repo state and external
  state)
- Visible history through git log on the baseline files themselves
- Reviewable change in pull requests — a baseline update is part of the
  diff, not a side effect

Baseline updates are deliberate. A regression-aware sprint that
intentionally trades some performance for correctness, clarity or
safety updates the baseline as part of the same commit, with the
rationale in the commit message.

-----

## 9. Per-Sprint Regression Policy

The end of every sprint runs the full compile-time, VM execution and
end-to-end benchmark suite. The stability test runs separately at a
longer cadence (per release).

**Comparison:** new results compared against the committed baseline.

**Threshold:** **5% regression on the end-to-end script benchmarks**
is the gate. Compile-time and VM execution benchmarks are informational
— useful for diagnosing where an end-to-end regression came from, but
not gates on their own. Micro-benchmark noise is real and a 5% regression
on a single tight loop is not by itself a problem.

**On a regression flag:**

1. Diagnose. Use the compile-time and VM execution breakdowns to
   localise where the slowdown lives.
2. Either fix it before the sprint closes, or document the trade-off,
   update the baseline, and capture the decision in the decisions log.

**On an improvement:** update the baseline. A 15% speedup is welcome,
but if it is not captured in the baseline the next sprint will not
notice when half of it is lost.

An hour of automated benchmarking at the close of a two-week sprint
is rounding-error overhead against the cost of catching regressions
late.

-----

## 10. No CLI Surface

`grob bench` is not a v1 CLI command.

The validation suite scripts are language-level fixtures, exercised
by users implicitly when they run their own Grob scripts. The
benchmarks are implementation-level infrastructure, exercised by the
engineer working on Grob itself. Different audiences, different
lifecycles, different concerns. Conflating them at the CLI would
suggest Grob users are expected to think about VM internals — they
are not.

Entry point is `dotnet run -c Release --project bench/Grob.Benchmarks`.
The README documents this. The CLI stays focused on running Grob
scripts.

This may change post-v1 if there is a genuine reason for users to
benchmark Grob scripts themselves (a `grob bench myscript.grob` for
profiling user code is a plausible later feature). It is out of scope
for v1.

-----

## 11. Implementation Timing

`bench/Grob.Benchmarks` is created as a skeleton at the end of
**Sprint 2**, the first sprint that produces meaningful code to
benchmark. The compile-time category lands first because the compiler
exists earliest. VM execution and end-to-end categories grow alongside
the features they exercise, sprint by sprint.

The baseline JSON files are committed for the first time at the close
of Sprint 2. Each subsequent sprint updates them.

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

-----

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
  every commit. They run at sprint close (full suite) and per-release
  (stability test). CI runs unit tests and integration tests; that
  separation is deliberate.

-----

*This document is the authoritative reference for Grob's benchmarking*
*strategy. D-302 records the decision. `grob-v1-requirements.md` §12*
*and `grob-solution-architecture.md` cite this document for detail.*

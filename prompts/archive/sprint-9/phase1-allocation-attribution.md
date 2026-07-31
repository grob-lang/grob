# Phase 1 — Allocation attribution measurement (no source changes)

**Branch:** `bench/allocation-attribution` — throwaway or short-lived; nothing here is
required to merge.
**One concern:** attribute where `Run_ArrayForIn`'s ~584 KB actually goes, and measure the
per-native-call dispatch overhead. **Measurement only. No `src/` changes. No optimisation.**

This discharges D-313's standing rule — *no optimisation proposals without measurement* —
before any decision is taken on the benchmark harness (phase 2) or the native-dispatch shape
(phase 4). The output is **numbers and a findings note**, not a fix.

---

## What prompted this

The `Benchmarks` CI run on `0125bae` failed the LOH tripwire on `Run_ArrayForIn`
(584,265 B) and `Run_MapForIn` (1,088,319 B), both marked `new`. Investigation established:

- `VmBenchmarks.RunSource` runs **the entire pipeline** inside the measured region — `Lexer.Scan`,
  `Parser.Parse`, `TypeChecker.Check`, `Compiler.Compile`, then `vm.Run`. The `vm` category has
  never measured VM execution in isolation.
- The `for...in` fixtures do all their native calls in the **setup** loop
  (`for i in 1..1000 { xs.append(i) }`). The loop actually under test — `total = total + x` — makes
  **no** native calls at all.
- Every native dispatch in `VirtualMachine.cs` allocates a `GrobValue[argCount]` args array **and**
  builds a `VmInvoker` closure (a delegate plus a display class capturing `line`, `column`, the
  cancellation token, the `FinallyContext` and `this`). **95 of 99 natives discard that invoker** —
  only `filter`, `select`, `sort` and `each` use it.

Estimated per-call overhead is ~150–200 B, but that is **inference from reading code, not
measurement**. This task replaces the estimate with a number.

---

## Method — differential fixtures, not a profiler

A profiler would attribute by type; what actually decides the downstream questions is
**bytes per native call** and **bytes attributable to the snapshot**. Successive fixtures that
differ by exactly one thing give both, using the existing `[MemoryDiagnoser]` harness.

Add these to `bench/Grob.Benchmarks/Fixtures/Vm/` with matching `[Benchmark]` methods routed
through the existing `RunSource`:

| Fixture | Source | Isolates |
|---|---|---|
| `attr-empty.grob` | `print(0)` | Pipeline + VM setup floor |
| `attr-range.grob` | `for i in 1..1000 { }` | Range-loop machinery |
| `attr-native.grob` | `for i in 1..1000 { "x".upper() }` | 1,000 native calls, **no collection growth** |
| `attr-build.grob` | `xs: int[] := []` then `for i in 1..1000 { xs.append(i) }` | Native calls **+** array growth |
| (existing) `array-for-in.grob` | unchanged | Build **+** snapshot **+** iteration |

Run all of them plus the existing five, one BenchmarkDotNet invocation, Release, same machine,
back to back:

```
dotnet run --configuration Release --project bench/Grob.Benchmarks -- --filter '*'
```

**Report the full `Allocated` column, not just means.** Allocation is deterministic and immune to
the CPU-mismatch problem that reduced the CI time axis to `info`, so it is the trustworthy signal
here.

---

## The numbers to derive and report

1. **Pipeline floor** = `attr-empty`.
2. **Loop machinery** = `attr-range` − `attr-empty`.
3. **Per-native-call overhead** = (`attr-native` − `attr-range`) ÷ 1000. **This is the headline
   number** — it decides whether option C's allocation argument has force, and it is paid by every
   native call in every Grob script, not just benchmarks.
4. **Array growth cost** = `attr-build` − `attr-native`, with the caveat that `append` takes two
   arguments and `upper` one, so subtract one `GrobValue` slot per call before attributing the
   remainder to `GrobArray` doubling.
5. **Snapshot + iteration cost** = `array-for-in` − `attr-build`. Compare against the predicted
   ~24 KB for a `GrobValue[1000]` copy. **If it is materially larger, that is a finding** — it
   would mean the measured loop allocates per iteration, which reading the lowering does not
   predict.
6. **Map comparison**: `map-for-in` − `array-for-in`, and note how much is attributable to 1,000
   `"k${i}"` interpolations in its setup loop rather than to the second snapshot array.

Also report **what fraction of `Run_ArrayForIn` is setup versus the code the benchmark claims to
measure** — that number is the evidence base for phase 2's harness decision.

---

## Scope boundaries — do NOT

- **Do not change anything under `src/`.** Not a single line. This is measurement.
- **Do not optimise.** Not the invoker, not the args array, not `GrobArray` growth. Findings feed
  phases 2 and 4; acting on them here would be exactly the unmeasured-optimisation pattern D-313's
  rule exists to prevent.
- **Do not commit these fixtures as permanent benchmarks yet.** Adding benchmarks is what tripped
  the missing-baseline path in the first place. Whether any become permanent is phase 2's call.
- **Do not touch any committed baseline JSON** (D-313's ratchet-trap rule).
- **Do not write a decisions-log entry.** No decision is being taken. The deliverable is a findings
  note.

---

## Deliverable

A short findings note — `bench-allocation-attribution.md`, repo-pathed under `docs/design/` —
containing:

- The full `Allocated` table for every benchmark run, and the machine/runtime the run was on.
- The six derived numbers above, with the arithmetic shown.
- **Bytes per native call**, stated plainly, with a note that it is paid by every native call in
  every script.
- Whether the snapshot's measured cost matches the ~24 KB prediction, and if not, what the gap
  suggests.
- Anything the numbers contradict — including anything in this task's framing. The estimates here
  are inference from reading code; if measurement disagrees, **the measurement wins and the
  disagreement is the most valuable line in the note**.

No conclusions about what to change. Phase 2 and phase 4 take those decisions with this note as
evidence.

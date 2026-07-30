# Phase 1 — Allocation Attribution: Findings

**Session:** `prompts/archive/sprint-9/phase1-allocation-attribution.md`. Branch:
`bench/allocation-attribution` (throwaway). **Measurement only — no `src/` changes,
no optimisation, no decisions-log entry, no committed baseline touched.**

Method: differential fixtures under BenchmarkDotNet's `[MemoryDiagnoser]`, run once,
back to back, Release, one machine. Not a profiler run — bytes attributable by
subtracting successive fixtures that differ by exactly one thing, per the source
prompt's method.

---

## 0. A harness gap found before any number could be produced

`VmBenchmarks.RunSource` constructs a bare `new VirtualMachine(...)` and calls
`vm.Run(chunk)` — it registers **no stdlib plugin**. The prompt's own
`attr-native.grob` design (`"x".upper()` × 1,000) assumes `string.upper` is reachable;
it is not, in this harness, as written. The first run of this fixture crashed with
`Grob.Core.GrobRuntimeException: Undefined global 'string.upper'` (full VM stack
trace preserved in this branch's local run log, not committed).

None of the five pre-existing VM benchmarks exercise this path either —
`print`/`exit` are dedicated opcodes (`OpCode.Print`), and `xs.append(i)` /
`m.set(...)` are **not** Stdlib-plugin natives: they are `Grob.Vm`-internal
(`ArrayNatives.GetMethod`), bound directly by `GetProperty`. So the "vm" benchmark
category has, until this branch, never actually reached a `Grob.Stdlib`-registered
native at all.

**Fix applied (bench/ only):** added a `Grob.Stdlib` project reference to
`Grob.Benchmarks.csproj` and registered `new StringMethodsPlugin().Register(vm)`
inside the shared `RunSource`, uniformly, for **every** fixture — including the
five pre-existing ones — so its one-time registration cost cancels out of every
pairwise subtraction below. `StringMethodsPlugin` is pure (no capability injection),
so this needed no `IRandomSource`/`IEnvironment`/`IStandardStreams` wiring; it is not
the full `Grob.Cli.PluginRegistration.RegisterAll` composition-root set.

**Consequence:** the absolute `Allocated` figures below are *not* directly comparable
to the committed `baseline/vm.json` for the five pre-existing benchmarks — every
number here carries a small extra fixed registration cost the committed baseline
does not. The relative deltas this note derives are unaffected (both operands of
every subtraction carry the same fixed cost), but anyone diffing these numbers
against CI history should know why they do not line up exactly.

---

## 1. Machine and run

- BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
- Intel Core i5-8400 CPU @ 2.80GHz (Coffee Lake), 6 physical/6 logical cores
- .NET SDK 10.0.302; Host and Job runtime: .NET 10.0.10, X64 RyuJIT x86-64-v3
- `dotnet run --configuration Release --project bench/Grob.Benchmarks -- --filter '*VmBenchmarks*'`
  (the `vm` category only — `Compile`/`Stability` categories are out of scope for this
  attribution exercise and were not run)

## 2. Full `Allocated` table

| Method | Mean | Allocated | Alloc Ratio |
|---|---:|---:|---:|
| Run_DeclAndArith (baseline) | 16.098 us | 54.37 KB | 1.00 |
| Run_Interpolation | 18.150 us | 58.19 KB | 1.07 |
| Run_ControlFlow | 73.376 us | 63.13 KB | 1.16 |
| Run_ArrayForIn | 588.666 us | 574.54 KB | 10.57 |
| Run_MapForIn | 871.238 us | 1066.81 KB | 19.62 |
| Run_AttrEmpty | 7.072 us | 43.14 KB | 0.79 |
| Run_AttrRange | 76.005 us | 45.07 KB | 0.83 |
| Run_AttrNative | 216.296 us | 226.59 KB | 4.17 |
| Run_AttrBuild | 295.966 us | 496.4 KB | 9.13 |

(`Alloc Ratio` is against `Run_DeclAndArith`, BenchmarkDotNet's baseline column — not
a ratio between the attribution fixtures themselves; the derived numbers below use
the raw KB→byte conversions, at 1 KB = 1024 B, the same convention BenchmarkDotNet's
own legend states.)

## 3. The six derived numbers

**1. Pipeline floor** (`attr-empty`) = **44,175 B** (43.14 KB). This is `Lexer.Scan` →
`Parser.Parse` → `TypeChecker.Check` → `Compiler.Compile` → fresh `VirtualMachine` +
`StringMethodsPlugin` registration → one `print(0)`, all in one measured region.

**2. Loop machinery** = `attr-range` − `attr-empty` = 46,152 − 44,175 = **1,977 B**
over 1,000 empty-body range iterations (≈2 B/iteration). Negligible — consistent with
an int-counter range loop needing no per-iteration heap allocation, since `GrobValue`
holds `int` in its `long` scalar slot (D-303) with no boxing.

**3. Per-native-call overhead** = (`attr-native` − `attr-range`) ÷ 1000 =
(232,028 − 46,152) ÷ 1000 = **≈186 B/call**. This is the headline number the source
prompt asked for, and it lands almost exactly inside the ~150–200 B range the prompt
estimated from reading `VirtualMachine.cs` — **the code-reading inference is
confirmed by measurement** for this call shape (a Stdlib-registered, no-argument,
receiver-injected primitive native, reused as a cached global — see §4 for why that
qualifier matters).

**4. Array growth cost** = `attr-build` − `attr-native` = 508,314 − 232,028 =
**276,286 B** over 1,000 `append` calls (≈276 B/call). **The prompt's stated caveat
does not hold** — see §4, this is the most consequential contradiction this note
found.

**5. Snapshot + iteration cost** = `array-for-in` − `attr-build` = 588,329 − 508,314 =
**80,015 B**, against a ~24,576 B (24 KB) prediction for one `GrobValue[1000]`
snapshot copy. **Materially larger — ≈3.3× the prediction.** This is a genuine
finding; see §5.

**6. Map comparison** = `map-for-in` − `array-for-in` = 1,092,413 − 588,329 =
**504,084 B**. This run's fixture set cannot cleanly split how much of that is the
1,000 `"k${i}"` interpolations in `map-for-in`'s setup loop versus the second
(values) snapshot array `map-for-in` adds over `array-for-in`'s one (keys) snapshot —
no differential fixture isolates a map-only build step the way `attr-build` isolates
the array one. Reporting this as an open gap rather than guessing a split; see §6.

**Setup vs. measured-code fraction for `Run_ArrayForIn`:**
setup (`attr-build`, the 1,000-append build loop) = 508,314 ÷ 588,329 = **86.4%** of
the benchmark's total allocation; the second loop the benchmark's doc comment claims
to measure (snapshot + 1,000 `total = total + x` iterations) is the remaining
**13.6%**. This confirms, and quantifies, the concern that started this whole
exercise: `Run_ArrayForIn` spends most of its allocation building the fixture, not
running the code under test.

---

## 4. Finding: the prompt's "subtract one `GrobValue` slot" caveat is wrong

The prompt frames `attr-native` (`"x".upper()`) and `attr-build` (`xs.append(i)`) as
differing by exactly one runtime argument — "`append` takes two arguments and `upper`
one" — so that array-growth cost could be isolated by subtracting one `GrobValue`
slot per call before attributing the rest to `GrobArray` doubling.

Reading `src/Grob.Vm/ArrayNatives.cs` and `src/Grob.Stdlib/StringMethodsPlugin.cs`
shows this assumption does not hold, because the two natives bind their receiver
through **structurally different mechanisms**:

- **`string.upper`** (D-066): the compiler rewrites `"x".upper()` to a call against
  the qualified native name, **injecting the receiver as `args[0]`** — a runtime
  arity of 1 (the string), captured in a lambda with **no closure state**, so the
  compiler emits it as a cached static delegate. `StringMethodsPlugin.Register`
  builds this `NativeFunction` **once**, ever, at plugin-registration time; every one
  of the 1,000 calls does a `GetGlobal` against the same pre-existing global slot.
- **`array.append`** (`ArrayNatives.GetMethod`): `OpCode.GetProperty` calls
  `GetMethod("append", receiver, invoker)`, which returns `new NativeFunction("append",
  1, (args, _) => Append(args, receiver))` — **freshly, on every single call**. The
  lambda captures `receiver`, so the C# compiler cannot cache it as a static delegate:
  it allocates a display-class instance to hold the captured `GrobArray` reference,
  a delegate bound to that instance, and the `NativeFunction` wrapper around it —
  **three allocations per call**, before the `Append` body (`receiver.Add(args[0])`)
  even runs. Its runtime arity is also 1 (just the value) — **not 2** — since the
  receiver never travels through the `args[]` array at all.

Both natives therefore build a `GrobValue[1]` args array per call — the same size —
so there is no slot-count adjustment to make. The 276 B/call `attr-build` figure is
not "per-native-call overhead plus growth"; it is **per-native-call overhead plus a
fresh `NativeFunction`/delegate/display-class triple allocated on every `GetProperty`
dispatch, plus growth (which fires roughly `log₂(1000) ≈ 10` times total, not per
call, since `GrobArray`'s backing store doubles)**. The ≈90 B/call gap over the
186 B/call baseline (§3.3) is a plausible order of magnitude for that triple-object
allocation, but this note has not isolated it with its own dedicated fixture — stated
as inference, not measured directly.

**Practical implication for phase 4:** "bytes per native call" is not one uniform
number across the ~99 natives. Stdlib-plugin natives (registered once, looked up by
`GetGlobal`) pay only the ≈186 B/call baseline. Array/map instance methods
(`append`/`insert`/`remove`/`clear`/`filter`/`select`/`sort`/`each`/`first`/`last`/
`contains` — bound fresh by `GetProperty` every call) pay that baseline **plus** a
per-call closure-allocation tax the Stdlib natives do not. If phase 4 considers
caching the bound `NativeFunction` per `(receiver, method-name)` pair rather than
rebuilding it on every `GetProperty`, this is the number that argument would need to
cite — but that is a phase 4 decision, not this note's to make.

---

## 5. Finding: the snapshot's measured cost is ≈3.3× the ~24 KB prediction

`array-for-in.grob` minus `attr-build.grob` isolates exactly `total := 0` plus the
second `for x in xs { total = total + x }` loop plus `print(total)`. The loop body is
a pure int accumulation over an already-`int`-typed local — no native calls, no
boxing (ints live in `GrobValue`'s `long` scalar slot, D-303) — so the ~24 KB
prediction assumed the measured delta would be close to one `GrobValue[1000]`
snapshot copy and little else.

The measured delta is 80,015 B, not ~24,576 B. This note has not root-caused the
extra ~55 KB — doing so would need a further differential fixture (e.g. a snapshot
with a truly empty loop body, isolating snapshot-copy cost from iteration cost),
which is out of this phase's prescribed fixture set. Flagging the gap plainly, as
instructed: **the measured loop allocates more than reading the lowering predicts**,
and phase 2's harness design and phase 4's optimisation case should treat "snapshot +
iteration" as a bigger cost than the ~24 KB figure previously assumed, not a smaller
one.

## 6. Map vs. array — the split this run cannot make

`map-for-in` costs 504,084 B more than `array-for-in`. Two things plausibly explain
most of it — 1,000 `"k${i}"` string interpolations in the setup loop (each a fresh
heap-allocated `string`, via `OpCode.BuildString`), and the second (values) array
`GrobValue[1000]` snapshot `map-for-in` takes in addition to the keys snapshot
`array-for-in` already exercises (per the fixture's own doc comment, D-383) — but no
fixture in this set isolates a map-only build step the way `attr-build` isolates the
array one. Providing a confident split here would be exactly the "inference from
reading code, not measurement" the source prompt warns against; recorded as an open
question rather than a guess. A `attr-map-build.grob` fixture (map construction, no
second loop) would resolve it, if phase 2 wants the number — not added here since it
was not in this phase's prescribed table.

---

## 7. Summary for phase 2 / phase 4

- Confirmed: ~186 B/call is a solid, measured, headline figure for the
  Stdlib-plugin native-call path (the majority of the ~99 natives) — close to the
  code-reading estimate.
- New finding: array/map instance methods are a **structurally distinct, more
  expensive** native-call path (fresh bound closure every call) — not covered by the
  ~186 B/call figure, and not something the prompt's original framing distinguished.
- New finding: `Run_ArrayForIn` is 86.4% setup, 13.6% the loop its own doc comment
  says it measures — quantifying, not just qualifying, the concern that opened this
  investigation.
- New finding: the for...in snapshot + iteration cost measures ≈3.3× the predicted
  ~24 KB, root cause not yet isolated.
- Open gap: no fixture isolates map-only build cost, so the interpolation-vs-second-
  snapshot split for `Run_MapForIn` is unresolved.
- Harness gap fixed (bench/ only, not `src/`): `VmBenchmarks.RunSource` reached no
  Stdlib-plugin native before this branch; it now registers `StringMethodsPlugin`
  uniformly so `attr-native.grob` is reachable and every derived delta stays
  apples-to-apples.

No conclusions about what to change follow from this note. Phase 2 (the benchmark
harness redesign) and phase 4 (the native-dispatch shape) take those decisions with
this note as evidence, per the source prompt's scope.

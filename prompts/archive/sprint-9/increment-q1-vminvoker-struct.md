# D-393 Q1 — `VmInvoker` becomes a `readonly struct`

**Branch:** `perf/vminvoker-struct`
**One concern:** convert `VmInvoker` from a delegate to a `readonly struct` carrying the VM
reference and per-call-site context, eliminating the per-native-call closure allocation.
Second of D-393's three fixes, sequenced after Q3 (D-394) by ascending blast radius.

Runs against the fresh corpus zip carrying D-356 through D-396. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — unchanged by this increment.

---

## Authority and the measured case

**D-393 Q1** ratified option **B** — `VmInvoker` as a `readonly struct` — over option C (split
pure and higher-order registration shapes). Read D-393 in full for the reasoning, including why
C was rejected *for now* and its explicit revisit triggers.

**The cost being removed.** Today `VmInvoker` is
`delegate GrobValue VmInvoker(GrobValue callable, GrobValue[] args)`. Each construction captures
`line`, `column`, `ct`, `finallyContext` and `this`, so Roslyn emits a display class **and** a
delegate — on every native call. It sits inside the canonical **186.1 B/call** Stdlib-native
figure, and **95 of 99 natives discard it** — only `filter`, `select`, `sort` and `each` invoke
it.

**D-394 removed one of the three construction sites.** The `GetProperty` array sub-arm's closure
is gone (dead parameter deleted), which is why the canonical run shows `Run_ArrayForIn` at
371,520 B, down 30.1%. **Two sites remain:**

- the `Call` handler's `invoker`, built before every native dispatch;
- `InvokeCallable`'s `nestedInvoker`, built for the nested-native path
  (`nativeFn.Implementation(args, nestedInvoker)`).

**Locate both against the live tree** — any line numbers recalled from earlier sessions predate
D-394 and will have moved.

---

## The honest cost D-393 recorded — do not soften it

`VmInvoker` appears in `NativeFunction.Implementation`'s public signature
(`Func<GrobValue[], VmInvoker, GrobValue>`), so this is **binary-breaking for every out-of-tree
native**, not only higher-order ones. D-393 accepted that because **no out-of-tree consumer
exists yet** and the in-tree source break is four one-token rewrites
(`inv(...)` → `inv.Invoke(...)`). That window is open now and closes when third-party plugins
ship — part of why this is sequenced before Increment C.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Locate both remaining construction sites** on the live tree and confirm D-394 removed the
   third. Report actual line numbers.
2. **Design the struct.** It must carry what the closure captured: the VM instance, `line`,
   `column`, `CancellationToken` and `FinallyContext`. Report the field set, the size, and
   whether `readonly struct` or `readonly record struct` is the right shape. Confirm it exposes
   an `Invoke(GrobValue callable, GrobValue[] args)` method with semantics identical to the
   current delegate call.
3. **THE RECURSION QUESTION — the increment's real risk.** `InvokeCallable`'s nested path builds
   an invoker that calls `InvokeCallable` again. As a delegate this is a closure capturing a
   method; as a struct it becomes a value whose `Invoke` calls back into the VM. Report:
   - whether the struct can be constructed inside `InvokeCallable` without circularity;
   - whether deep nesting (a higher-order native invoking a lambda that calls another
     higher-order native) still works, and how deeply existing tests exercise it;
   - whether passing by value at each level is correct, or whether `in`/`ref` parameters are
     wanted to avoid repeated copying.
4. **`NativeFunction.Implementation`'s signature** — `Func<GrobValue[], VmInvoker, GrobValue>`
   with a struct type argument. Confirm this compiles and **does not box the struct when the
   `Func` is invoked**. A struct boxed at the delegate boundary would defeat the entire change.
   Check explicitly and report the reasoning — a generic type argument on a `Func<>` is exactly
   where a value type can quietly become a heap allocation.
5. **The four higher-order call sites** in `ArrayNatives` — confirm `inv(...)` →
   `inv.Invoke(...)` is the whole in-tree source change, and that nothing else calls a
   `VmInvoker`.
6. **`IPluginRegistrar.RegisterNative` and `IGrobPlugin`** — confirm no in-repo plugin passes or
   stores a `VmInvoker`, and that the Stdlib plugins need nothing beyond recompilation.
7. **Plan the measurement.** `attr-native` (186.1 B/call, the Stdlib path) is the primary fixture
   — it isolates the per-native-call cost this change targets. State the expected post-fix figure
   **before** measuring. `attr-array-dispatch` and `Run_ArrayForIn` will also move, since they
   make native calls too.

Report the struct design, the recursion analysis, the boxing verdict, the call-site list, and
the measurement plan. Then STOP.

---

## Scope boundaries — do NOT

- **Do not implement Q2** (per-receiver `NativeFunction` cache) — its own increment, after this.
- **Do not implement option C.** Registration shapes are unchanged; `IPluginRegistrar` keeps its
  single `RegisterNative`. D-393 rejected C for now with explicit revisit triggers — do not
  pre-empt them.
- **Do not change what the invoker does.** Same re-entrancy semantics, same `line`/`column`
  reporting, same cancellation, same `FinallyContext` propagation. This changes the **shape** of
  the thing passed, nothing about its behaviour.
- **Do not change `NativeFunction`'s arity, name or registration mechanism** beyond the type
  argument.
- **Do not update a benchmark baseline** — a reduction needs none, and per D-313/D-391 this must
  **lower** ceilings if anything, never justify raising them. Ceiling re-derivation is deferred
  until after Q2.
- **No new opcode. No new error code** — count stays **121**.

---

## Tests — TDD, red first

- **The four higher-order natives, end to end and load-bearing:** `filter`, `select`, `sort`,
  `each` all work; a lambda that **throws** (exercising `FinallyContext` propagation and the
  native-throw seam, D-342/D-382); a lambda invoked from inside another higher-order native
  (**nested invocation**, the recursion path); and cancellation if existing tests cover it.
- **Error reporting unchanged**: a fault raised inside an invoked lambda still reports the
  correct `line`/`column` from the original call site. That captured context is the reason the
  closure existed, and a struct conversion is exactly where it could silently regress.
- **No boxing at the delegate boundary** — assert it if practical, using D-388's
  `GC.GetAllocatedBytesForCurrentThread` technique around a single native call. If not practical
  in the unit-test project, **say so and record the gap** rather than writing a test that cannot
  fail; the benchmark figures then become the guard, as D-394 accepted.
- Every existing VM, array, map, string, numeric and plugin test passes unchanged.
- **The measurement**, before and after, same machine, one sitting: `attr-native`,
  `attr-array-dispatch` and `Run_ArrayForIn` `Allocated` figures both ways, with the derived
  per-native-call delta stated against the 186.1 B/call baseline.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-397**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: D-393 Q1 implemented; the
  struct's field set and why that shape; **the boxing verdict at the `Func<>` boundary**, with
  its evidence; the recursion analysis and how nested invocation was verified; the four
  `inv(...)` → `inv.Invoke(...)` rewrites as the whole in-tree break; **the before and after
  measurements with the derived per-native-call delta**; whether the no-boxing assertion was
  practical or recorded as a gap; that the public-signature break is accepted per D-393 with no
  out-of-tree consumers; and that Q2 remains outstanding, with ceiling re-derivation deferred
  until after it. No opcode change, no new error code, count 121. Cite D-393 (Q1, Q4), D-394,
  D-391, D-313, D-342, and `docs/design/bench-allocation-attribution.md`.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.

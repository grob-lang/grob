# D-393 Q3 — Eliminate the `GetProperty` closure-capture tax

**Branch:** `perf/getproperty-closure-tax`
**One concern:** stop the array `GetProperty` sub-arm allocating a display class on every
dispatch. First of D-393's three fixes, chosen first for the smallest blast radius: one
method, no public API surface.

Runs against the fresh corpus zip carrying D-356 through D-393. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — unchanged by this increment.

---

## Authority and the measured case

**D-389** identified the cost by measurement and proved the mechanism with a synthetic C# repro
containing no Grob code: in the `GetProperty` array sub-arm, the closure-declaring statements
(`VirtualMachine.cs` ~894–899) share a lexical scope with the early-return branches above them,
so Roslyn allocates the display class **on entry to the scope**, not gated on which branch
executes. A bare `.length` read — which returns before ever reaching the closure — still pays
for it. Cost: **≈48 B per `GetProperty` dispatch against an array receiver**, independent of
array size, scaling with call count.

This is **not** a `for...in` cost. It is paid by every user `.length` and `.isEmpty` read in
every script, and by every `$snapshot` read.

**D-393 Q3** ratified fixing it, and **D-393 Q4** sequenced it first, ahead of Q1 (`VmInvoker`
struct-ification) and Q2 (per-receiver method cache), because it touches one method and no API.

**D-313's measurement rule is satisfied for the cost** — canonical figures exist. It is **not**
satisfied for this fix's effectiveness, which must be measured after implementation, never
assumed.

---

## A finding that may make this a deletion rather than a restructure — verify first

D-393's Q2 analysis established, and current source confirms, that
**`ArrayNatives.GetMethod`'s `invoker` parameter is dead**:

- It appears only in the signature (`ArrayNatives.cs:34`).
- Every switch arm binds `(args, inv) => Filter(args, inv, receiver)` — where `inv` is the
  **lambda's own parameter**, supplied by the caller at **invocation** time through
  `Func<GrobValue[], VmInvoker, GrobValue>`, not the parameter `GetMethod` was handed at
  **bind** time.
- No arm references the bind-time parameter at all.
- `MapNatives.GetMethod` (`MapNatives.cs:24`) already takes **no** `VmInvoker` parameter, and
  its call site (`VirtualMachine.cs:936`) correspondingly builds no closure and no
  `FinallyContext` — which is exactly why the map path does not pay this tax.

If that holds, the closure passed at `VirtualMachine.cs:899`
(`(callable, args) => InvokeCallable(callable, args, line, column, ct, finallyContext)`) is
constructed for a parameter nothing reads. The fix would then be to **delete the argument and
the parameter**, bringing the array call site into line with the map one — removing the
allocation rather than relocating it, and removing the `CancellationToken ct` and
`FinallyContext` locals that exist only to feed it.

**Verify this before designing a lexical restructure.** A scope restructure is the fallback if
the parameter turns out to be load-bearing in some path this reading missed.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Confirm or refute the dead-parameter finding** across the whole tree, not just
   `ArrayNatives.cs`: is `GetMethod`'s bind-time `VmInvoker` parameter read anywhere, by any
   arm, now or via any other caller? Report the evidence either way.
2. **If dead:** confirm that deleting the parameter, the argument at `:899`, and the `ct` /
   `finallyContext` locals that feed it leaves the four higher-order natives
   (`Filter`/`Select`/`Sort`/`Each`) fully functional — they must still receive a live
   `VmInvoker` at **invocation** time through the `NativeFunction` delegate signature. **This is
   the correctness question of the increment.** Trace one higher-order call end to end
   (`xs.filter(f)`) and report where its `VmInvoker` comes from at invocation.
3. **If not dead:** design the minimal lexical restructure — move the closure-declaring
   statements into a scope the early-return branches do not share, so `$snapshot`, `length` and
   `isEmpty` reads do not allocate. Report the restructure and confirm it changes no behaviour.
4. **Check `ArrayNatives.cs:28`'s doc comment**, which D-393 notes would need correcting if the
   parameter goes. Report what it currently claims.
5. **Confirm no other `GetProperty` sub-arm has the same shape** — the map arm reportedly does
   not, but check the nominal-type and primitive-member arms too. If one does, **report it; do
   not fix it here** unless it is the same lexical scope in the same method.
6. **Plan the measurement.** `attr-array-dispatch` (414.0 B/call) and `Run_ArrayForIn`
   (531,616 B) are the fixtures. State the expected post-fix figures **before** measuring, so
   the result cannot be rationalised afterwards. Note D-389 measured this tax at ≈48 B/call.

Report the dead-parameter verdict, the fix shape, the higher-order trace, the doc-comment
correction, and the measurement plan. Then STOP.

---

## Scope boundaries — do NOT

- **Do not implement Q1 or Q2.** `VmInvoker` stays a delegate in this increment; no
  per-receiver cache. Both are their own increments, sequenced after this one (D-393 Q4).
- **Do not change `NativeFunction`'s delegate signature** — `Func<GrobValue[], VmInvoker,
  GrobValue>` is Q1's territory. Higher-order natives must still receive their invoker at
  invocation time exactly as today.
- **Do not change any semantics.** D-372 reference semantics, D-383's contents-snapshot,
  the native-throw seam (D-342/D-382) and catchability are all out of scope.
- **Do not update a benchmark baseline** — only `benchmark.yml` produces committable baselines,
  and a *reduction* needs no baseline change to pass. Per D-313 and D-391, this fix must
  **lower** the ceilings if anything, never justify raising them.
- **No new opcode. No new error code** — count stays **121**.

---

## Tests — TDD, red first

- **Behaviour unchanged, and this is the load-bearing set:** all four higher-order natives
  (`filter`, `select`, `sort`, `each`) work end to end, including a lambda that throws (so the
  `FinallyContext` and cancellation paths are exercised), and a cancellation case if one is
  already covered. If `ct`/`finallyContext` locals are deleted, **prove by test that nothing
  depended on them at bind time.**
- Every existing array member test — the nine from D-371/D-373 plus `$snapshot` via `for...in`
  — unchanged.
- `.length` and `.isEmpty` still resolve and return correctly (the early-return branches this
  fix targets).
- A **characterisation test** guarding the fix, per D-388's precedent: this is predicated on
  current compiler lowering behaviour, not a guaranteed contract, so something must fail if a
  future change reintroduces the capture. If a direct allocation assertion is impractical in the
  unit-test project, say so and record the gap rather than writing a test that cannot fail.
- **The measurement**, before and after, same machine, one sitting: `attr-array-dispatch` and
  `Run_ArrayForIn` `Allocated` figures both ways, with the derived per-call delta.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-394**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: whether the dead-parameter
  finding was **confirmed or refuted**, and which fix shape followed from it; the higher-order
  invoker trace proving invocation-time supply is untouched; the `ct`/`finallyContext` locals'
  fate; **the before and after measurements with the derived per-call delta**, and whether they
  match D-389's ≈48 B/call; the characterisation test, or the recorded gap if one was not
  practical; the `ArrayNatives.cs:28` doc correction; and that Q1 and Q2 remain outstanding per
  D-393 Q4's ordering. No opcode change, no new error code, count 121. Cite D-393 (Q3 and Q4),
  D-389, D-391, D-313, and `docs/design/bench-snapshot-residual.md`.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.

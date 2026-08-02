# Phase 4 — Dispatch-path allocation: decision session

**Type:** planning/decision session. **No source changes.** The deliverable is a ratified
decision; implementation follows in separate increments, split by blast radius.

Runs against the fresh corpus zip carrying D-356 through D-392, plus
`docs/design/bench-allocation-attribution.md` and `docs/design/bench-snapshot-residual.md`.
Corpus-first discipline throughout. **Error-code count is 121.**

---

## Why this decision exists, and why now

The benchmark investigation (phases 1–3, D-385 through D-392) measured the VM's dispatch path
precisely. Three **distinct** allocation sources sit on it. They have been conflated in earlier
framing — including in the prompts that commissioned this work — and separating them is the first
thing this decision must do.

| # | Source | Who pays | Measured |
|---|---|---|---|
| 1 | `VmInvoker` closure built per native call — `VirtualMachine.cs` ~1023, capturing `line`, `column`, `ct`, `finallyContext`, `this` | **every** native call | inside the 186.1 B/call figure |
| 2 | `ArrayNatives.GetMethod` returning `new NativeFunction(...)` per call, each capturing `receiver` — display class + delegate + wrapper | array/map member calls | part of the 227.9 B/call tax |
| 3 | `GetProperty` display-class allocation from Roslyn lexical scope (D-389) — allocated on entry to a scope shared with early-return branches | **every** array `GetProperty`, including a bare `.length` read | ≈48 B/call |

**Canonical measurements** (run 30707325720, `windows-latest`, D-309):

- Stdlib-plugin native call: **186.1 B/call** — includes source 1.
- Array-member dispatch: **414.0 B/call** — includes sources 1, 2 and 3.
- **The 227.9 B/call difference is sources 2 + 3. It is not source 1**, which both paths pay.

**Why now, before Increment C.** `fs`, `json`, `csv`, `regex` and `process` will add roughly
50–60 natives. If this decision changes the registration shape, those are written in the new shape
rather than migrated afterwards. That window closes when Increment C starts.

**What D-389 established about source 3, which is easy to under-scope:** it is *not* a `for...in`
cost. It is paid by **every `GetProperty` dispatch against an array receiver** — every user
`.length` and `.isEmpty` read in every script. D-389 proved the mechanism with a synthetic C#
repro containing no Grob code at all: the display class is allocated on entry to the shared
lexical scope, not gated on which branch executes, so an early-returning `length` read pays for a
closure it never constructs.

---

## Plan-mode gate — read-only, read before proposing

1. **The three sites in current source** — `VirtualMachine.cs`'s `Call` handler (the invoker
   construction), its `GetProperty` handler (the scope-shared closure, D-389), and
   `ArrayNatives.GetMethod` / `MapNatives.GetMethod`.
2. **Who actually consumes the invoker.** Report the count of natives taking `(args, inv)` versus
   `(args, _)`, across `Grob.Stdlib` and `Grob.Vm`. **Note that `MapNatives.GetMethod` takes no
   `VmInvoker` parameter at all** — no map member is higher-order — so the narrower signature is
   already demonstrated to work on one path. Report whether any *plugin* native (`Grob.Http`,
   `Grob.Crypto`, `Grob.Zip`, or the `IGrobPlugin` surface generally) is higher-order.
3. **`IPluginRegistrar.RegisterNative`'s reach** — this is the third-party plugin API. Report what
   changing its signature would break, and whether anything outside the repo depends on it yet.
4. **The re-entrancy hazard.** A native receiving the invoker can **stash it in a field** and call
   it later, out of turn, carrying that call site's `line`, `column`, cancellation token and
   `FinallyContext` — so a deferred invocation would run VM code with stale context. Confirm
   whether anything prevents this today, and report it as a correctness consideration independent
   of allocation. **Do not overstate it**: the invoker can only invoke a callable already present
   in `args`, so it is not arbitrary VM access.
5. **Whether sources 2 and 3 are separable.** Source 2 is `GetMethod`'s per-call rebinding; source
   3 is the lexical-scope closure at the `GetProperty` case. Report whether fixing one changes the
   other, since the 227.9 B/call figure covers both and a fix addressing only one will produce a
   partial improvement.

---

## The questions to settle

**Q1 — The `VmInvoker` shape (source 1).** Options, from D-385-era analysis:

- **A** — status quo, uniform signature.
- **B** — `VmInvoker` becomes a `readonly struct` carrying VM reference, `line`, `column`, token
  and context. Removes the allocation for **all** calls; the four higher-order users change from
  `inv(...)` to `inv.Invoke(...)`; no native rewritten; **no plugin API change**.
- **C** — two registration shapes: pure `Func<GrobValue[], GrobValue>` and higher-order. Removes
  the allocation, **and** narrows the plugin API so a pure native cannot re-enter the VM at all.
  Costs two dispatch paths and a breaking `IPluginRegistrar` change.
- **D** — a `NeedsInvoker` flag on `NativeFunction`; construct only when true.

Decide, and be explicit about which argument carries the decision: **allocation** (which B
achieves as fully as C, at lower cost) or **capability narrowing and signature legibility** (which
only C achieves). Note the timing asymmetry: C is far cheaper before third-party plugins exist.

**Q2 — Per-call rebinding in `GetMethod` (source 2).** `ArrayNatives.GetMethod` and
`MapNatives.GetMethod` allocate a fresh `NativeFunction`, delegate and display class on **every**
member call, because the receiver is captured. Decide whether to cache — and if so, keyed on what,
with what lifetime, and how the cache is invalidated given `GrobArray`/`GrobMap` are **mutable
reference types** under D-372. A cache keyed on receiver identity that outlives the receiver is a
leak; one keyed on method name alone cannot carry the receiver. **State the invalidation story or
reject caching** — an unstated one is worse than the allocation.

**Q3 — The lexical-scope closure (source 3).** D-389 identified a Roslyn capture allocated on
entry to a scope shared with early-return branches. Decide whether to restructure the case body so
early-returning property reads do not pay it. This is contained — one method, no API change — but
it is a fix predicated on **current compiler lowering behaviour**, so record the same caveat D-388
attached to its own refutation: this is implementation behaviour, not a guaranteed contract, and a
characterisation test is what would guard it.

**Q4 — Sequencing and blast radius.** These are three separable fixes:

- Q3 is contained in one method.
- Q2 is contained in `ArrayNatives`/`MapNatives`.
- Q1 touches `VirtualMachine.cs`, and under option C the **plugin API**.

Decide the order and which land as separate increments. State explicitly whether any part should
land **before Increment C** on the grounds that 50–60 new natives are about to be written.

**Q5 — What is actually worth doing.** Not every measured cost is worth fixing. `Run_ArrayForIn`
at 531,616 B is a thousand-element benchmark; a typical script's arrays are small. Ask plainly
whether each fix earns its complexity, and **be willing to answer "no" for any of them** — D-388's
precedent is that a written, measured, reverted fix is a good outcome. Any fix that lands must
also state that removing this cost **shrinks** the D-391 ceilings, never raises them (D-313's
ratchet direction, already recorded for the ≈48 B/call component).

---

## Constraints

- **D-313's measurement rule is satisfied** for sources 1, 2 and 3 — canonical figures exist. It
  is **not** satisfied for any *proposed fix's* effectiveness: each must be measured after
  implementation, not assumed.
- **No semantics change.** D-372 reference semantics, D-383 contents-snapshot, the native-throw
  seam (D-342/D-382) and catchability are all settled and out of scope.
- **Append-only.** Anything amending D-066, D-342 or D-363 is done by citation in a new entry.
- **No new opcode, no new error code** — count stays **121**.

---

## Deliverable

**A single ratified decision**, three-location lockstep (summary index row, full ADR entry, footer
changelog), D-### from the **live registry tail** — next free is **D-393**; confirm, do not
assume. Match the current index-row format (unpadded date cell).

For each of Q1–Q5: what was decided, what was rejected and why, and what it costs. It must
separate the three allocation sources explicitly — **correcting the earlier conflation is part of
the entry's job**, since prior framing treated the 227.9 B/call tax as an argument for option C
when option C does not address it.

State which fixes become increments, in what order, and which land before Increment C.

**No implementation.** If any answer is "not worth doing", say so and record the reasoning — that
is a legitimate and useful outcome.

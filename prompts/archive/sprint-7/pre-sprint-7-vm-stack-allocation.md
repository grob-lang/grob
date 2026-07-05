# Pre-Sprint 7 — Interlude 1: VM operand-stack allocation remediation

> **Shape:** Pre-Sprint 7 interlude. Self-contained. One concern, one branch, one PR.
> **Runs before:** Pre-Sprint 7 Interlude 2 (benchmark gate hardening). The order is
> load-bearing — the allocation baseline that Interlude 2 freezes must be taken against
> the *fixed* VM, not the buggy one, or the defect ratchets in as the baseline (the exact
> anti-ratchet concern D-313 raises).
> **Model routing:** structural VM change on the hot path — Opus for the plan, Sonnet may
> execute the approved plan.

---

## 1. Why this interlude exists

The Sprint 6 benchmark run (windows-latest, AMD EPYC 7763, .NET 10.0.9) surfaced a real
VM defect that the current gate mislabelled as informational. The three VM benchmarks all
report the same pathological signature:

```
Run_DeclAndArith   alloc ≈ 419 KB/op   Gen0 = Gen1 = Gen2 = 2047
Run_Interpolation  alloc ≈ 421 KB/op   Gen0 = Gen1 = Gen2 = 1023
Run_ControlFlow    alloc ≈ 427 KB/op   Gen0 = Gen1 = Gen2 = 1023
```

Two facts read straight off that:

1. **`Gen0 == Gen1 == Gen2` is a full compacting GC on every collection.** That only
   happens when a single allocation clears the ~85,000-byte Large Object Heap threshold,
   which forces a Gen2. Contrast the compile side — `Gen0=123, Gen1≈1, Gen2=0`, a healthy
   generational pyramid.
2. **Allocation is near-constant (419 → 427 KB) regardless of workload.** ControlFlow does
   ~2.7× the work of DeclAndArith yet allocates ~2% more. That is a fixed per-`Run()` setup
   cost dominating, not per-instruction churn. One allocation to fix, not a pervasive problem.

The prime suspect is the operand stack: `GrobValue` is a 24-byte struct
(`grob-vm-architecture.md`), so a default operand-stack capacity of ~16K slots is
≈ 393 KB — over the LOH threshold — allocated fresh per VM instance, and the VM is
constructed fresh per run (D-300). Note the OQ-005 NaN-boxing future does **not** rescue
this: 16K × 8 B is still 131 KB, still LOH. Right-sizing is the fix regardless of OQ-005.

**This interlude does not touch the gate.** It fixes the VM defect and recaptures the VM
baseline against the fix. Interlude 2 changes the gate policy.

---

## 2. Verify-first — do this before proposing anything

Corpus-first. Read live source and the live log tail. Do not reason from this prompt's
figures or from any project-knowledge snapshot.

1. **Decisions log tail.** Open the live `grob-decisions-log.md`, read the index tail and
   the last full entry. Allocate the **next free** `D-###` for this interlude's decision —
   take the number from the live log, never from this prompt. (At the time of writing the
   tail was D-331; increments 6C–6E and any morale-milestone work may have consumed numbers
   since, so confirm.)
2. **The actual allocation site.** Read the live `Grob.Vm` source. Identify the operand
   stack field, its element type and its **default capacity**, and confirm whether the
   backing array is allocated per VM instance. Confirm `CallFrame[]` size
   (`grob-vm-architecture.md` shows `new CallFrame[256]` — ~6 KB, not the culprit, but
   verify). If the LOH allocation is **not** the operand stack, re-measure and surface
   before proposing a fix — do not fix a site you have not confirmed.
3. **Interacting invariants.** Read the live entries for:
   - **D-325** — location-based upvalue closing. Upvalues capture a **stack location**.
     Any growth of the operand-stack array must not invalidate a captured location or a
     closed-over value.
   - **D-319** — per-instance dispatch step-budget counter (spans the re-entrant bridge).
     Any change to VM lifecycle or array pooling must not disturb it.
   - **D-300** — VM fresh per run. This is the property a pooling approach would revisit.
   - The stack-overflow guard: a fixed operand-stack capacity is currently the implicit
     recursion/depth cap. Find where overflow is detected and which `GrobError` leaf is
     thrown.
4. **Coverage floor.** ADR-0018 / D-328 — the language-implementation denominator holds a
   90% line+branch floor. New VM branches (growth path, overflow-at-cap) must be covered.

Report what you found from 1–4 before writing the plan.

---

## 3. Objective

Get per-`Run()` VM allocation off the Large Object Heap and restore a normal generational
GC profile (`Gen0 ≫ Gen1 ≫ Gen2`), **without changing one bit of observable VM behaviour**.
Same results, same errors, same error codes, same ordering. This is a storage-and-lifecycle
change, not a semantics change.

---

## 4. Plan-mode gate — stop for explicit approval

Produce a **numbered** plan. Do not touch source until it is approved. The plan must choose
and justify one of:

- **(a) Right-size + grow on demand.** Drop the default operand-stack capacity well under
  the LOH threshold (most scripts never exceed a few dozen slots), grow geometrically on
  demand via `Array.Resize`. Must preserve the overflow guard: growth is capped at the old
  ceiling and the existing overflow `GrobError` is thrown at the cap — unbounded growth
  silently removes the depth guard, which is a regression.
- **(b) Pool the backing array across `Run()`.** Reuse a right-sized array between
  invocations rather than allocating per run. Higher blast radius — interacts with D-300
  (fresh-per-run) and D-319 (per-instance counter). If chosen, state exactly how the pooled
  array is reset (recall `default(GrobValue)` is `Nil`, so a length reset needs no per-slot
  clear on the live region, but stale references above `_stackTop` must not pin GC roots).
- **(c) Both** — right-size the default *and* pool.

Recommendation to argue for or against, not to assume: **(a) as the baseline fix**. It is
the lowest-blast-radius change that resolves the LOH pressure, keeps D-300 intact and needs
no lifecycle rework. Pooling (b/c) is a further optimisation that can be justified on its own
numbers later if per-run array allocation still shows up once the LOH pressure is gone.

The plan must explicitly address:
- The new default capacity and the growth policy (factor, cap).
- Overflow-guard preservation — where the cap check lives, which error it throws.
- Whether any code caches a `ref` or `Span<GrobValue>` into the stack array (a resize
  invalidates a cached span/ref — this is the single most likely correctness trap).
- The D-325 interaction — a worked argument that a captured upvalue location survives a
  resize, or the mechanism that makes it survive.

---

## 5. Closed surfaces — do not touch

The fix is confined to operand-stack storage and lifecycle. Do not touch:

- **`OpCode` and the wire format** (ADR-0013) — no opcode added, removed or renumbered.
- **`GrobValue` public factory/accessor surface** — the OQ-005 boundary. If the fix appears
  to need a `GrobValue` layout or surface change, **stop** — that is OQ-005 territory, not
  this interlude.
- **`.grobc` format** (D-298) — no serialisation change; the operand stack is never serialised.
- **Dispatch-loop opcode semantics** — no change to what any instruction computes.
- **Error codes** — no new code; the overflow error already exists. Count unchanged.

If the plan cannot be delivered without crossing one of these, surface it — do not cross it.

---

## 6. TDD matrix

The change is behaviour-preserving, so the **existing VM test suite is the correctness net**
and must stay green throughout. Add these, red-first:

| # | Test | Asserts |
|---|------|---------|
| 1 | Allocation ceiling on the run path | A representative script (e.g. the DeclAndArith / ControlFlow shapes) executed through a fresh VM allocates **no single object ≥ 85,000 bytes** and forces **no Gen2 collection**. Use `GC.CollectionCount(2)` around a warmed run, or assert peak single-allocation size via the same `[MemoryDiagnoser]` data the bench uses. |
| 2 | Deep-stack growth correctness | A script that pushes past the new default capacity executes correctly — results identical to a reference run — proving the growth path preserves values across a resize. |
| 3 | Overflow guard at the cap | A script that exceeds the growth cap throws the **existing** overflow `GrobError` leaf (same code, same category) — the depth guard is preserved, not removed. |
| 4 | Upvalue survives growth (D-325) | Capture an upvalue, force an operand-stack resize after capture, then read the closed-over value — it is correct. Guards the location-based-closing interaction directly. |
| 5 | Fresh-per-run isolation (D-300) | Two sequential runs on the pattern do not leak state between them (guards a pooling approach; trivially green for approach (a) — keep it either way as a regression tripwire). |

Test tooling: xUnit, FsCheck where a property fits (e.g. #2 over random push depths). No
FluentAssertions (v8 commercial licence — excluded). Do not author fixtures against wrong
behaviour — if a test cannot be made to pass without changing semantics, that is a
stop-and-surface, not a fixture to bend.

---

## 7. Stop-and-surface triggers

Halt and report rather than pressing on if:

- The confirmed LOH source is **not** the operand stack — the premise is wrong; re-measure.
- Any code caches a `ref`/`Span<GrobValue>` into the stack array such that growth cannot be
  made safe without a wider change.
- The overflow guard cannot be preserved under the chosen growth policy.
- A pooling approach collides with D-319's per-instance counter or D-300's fresh-per-run
  contract.
- The fix appears to require any change to the `GrobValue` public surface or layout
  (OQ-005) or to any opcode.
- Coverage on the new branches cannot reach the ADR-0018 floor without excluding real code.

---

## 8. Definition of done

- Test 1 (allocation ceiling) green; full VM suite green; coverage floor (ADR-0018) held.
- Benchmark re-run on **windows-latest** via `benchmark.yml` (§8.1 of the strategy — never a
  local run for a committed baseline) shows VM allocation off the LOH and the generational
  pyramid restored (`Gen0 ≫ Gen1 ≫ Gen2`, Gen2 ≈ 0 on these workloads).
- **Baseline update is deliberate and logged.** This is a step-change improvement to a first
  VM baseline. Update the rolling VM baseline; re-freeze the VM **origin** only as a
  sanctioned re-freeze (the origin was frozen against the buggy first capture and is no
  longer a meaningful cumulative anchor). Record the workflow run ID and runner in the
  commit message per §8.1 convention. Per §9, a baseline change is part of the same commit
  as the decision.
- **Decision logged, three-location lockstep:** summary index row, full ADR-style entry
  (Area: VM / runtime; Supersedes/Superseded-by as applicable), footer changelog. Cite the
  benchmark finding as motivation and the recaptured baseline as evidence.
- Consistency gate (D-316) green — error-code count unchanged, log lockstep intact.

---

## 9. Deliverable shape

Full files, never patches. One branch, one PR for this interlude, main protected, Husky.NET
pre-push gate green before push. Ship the session outputs as a single zip: the changed VM
source, the new tests, the recaptured baseline JSON, the `grob-decisions-log.md` update and
this executed command archived under `prompts/archive/sprint-7/`. British English, no Oxford
comma.

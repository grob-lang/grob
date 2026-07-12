# Increment prompt — Compile allocation regression (pre-Sprint-8)

**Concern:** One concern only — the compile-path per-compile allocation
regression surfaced at Sprint 7 close. `Compile_TwoExpressions` **+68.6%**
(→ 13,256 B) and `Compile_TenPrints` **+37.2%** (→ 19,872 B) allocated bytes
per operation versus the rolling baseline. Allocation is deterministic and
CPU-independent, so these are real code-driven deltas, not runner noise. The
time columns on the same run were correctly demoted to informational on a
CPU-identity mismatch (D-333) and are not in scope here.

**Model:** Sonnet 4.6 (High). Not the Opus carve-out.

**Branch:** `fix/compile-alloc-regression` via `/start-branch` — it refuses on
`main`. One branch, one PR, one concern.

---

## 0. Verify first — read live, do not trust this prompt

Before anything else, read the live files and confirm. Do not proceed on the
prompt's word:

- **Decisions log tail.** Read and quote the current bodies of **D-333**
  (three-axis regression gate; axis 3 allocation; allocation gates regardless
  of CPU), **D-313** (ratchet trap — never commit a baseline that freezes a
  known defect), **D-309** (baseline production via `benchmark.yml`) and
  **D-302** (benchmarking-strategy authority). If any number or body differs
  from what this prompt states, **stop and surface** the discrepancy.
- **Rolling baseline.** Open `bench/Grob.Benchmarks/baseline/compile.json`
  (confirm the policy-referenced path in `bench/Grob.Benchmarks/baseline/policy.json`).
  Confirm it records a named CPU and a real BenchmarkDotNet version — not a
  placeholder or `Unknown processor`. Record the commit that last wrote it:
  `git log -1 --format=%H -- <baseline path>`. That commit is the reference
  state; the regression was introduced between it and `HEAD`.

Report the four decision confirmations and the baseline commit SHA before
moving on.

---

## 1. Reproduce locally — read-only

Allocation is deterministic, so the CI figures must reproduce exactly on your
machine. Run the compile category only:

```
dotnet run -c Release --project bench/Grob.Benchmarks -- --filter *CompileBenchmarks* --memory
```

Adjust the filter to the real benchmark type name. Confirm bytes/op for
`Compile_TwoExpressions` and `Compile_TenPrints` match the reported 13,256 B
and 19,872 B within rounding. If they do **not** reproduce, **stop and
surface** — a non-reproducing allocation delta means the baseline or the
fixtures moved, which is a different finding.

---

## 2. Localise — read-only, plan mode, no source edits

The tell from the report: the smaller program took the larger percentage hit
(+68.6% on two expressions vs +37.2% on ten prints). That is the signature of
a **fixed per-compile allocation** added to the always-run path
(lex → parse → typecheck → emit), not a per-node cost that scales with program
size. Localise it without editing source:

1. **Diff the always-run compile path from the baseline commit to HEAD.**
   `git diff <baseline commit>..HEAD -- <compiler / lexer / parser / typechecker / emitter source roots>`.
   The introduced allocation is in this diff. Sprint 7 was error handling, so
   prime suspects are error-recovery scaffolding or diagnostic machinery that
   now allocates unconditionally — even when the source has zero diagnostics
   and no `try`/`catch`.
2. **Attribute with a profiler, no source edits.** Run the two-expression
   compile under PerfView, dotMemory, or `dotnet-trace` with the GC allocation
   provider and read the allocation call-tree. Confirm the site the diff
   implicated is where the bytes actually land.

Localise to a specific allocation site and name the introducing commit. If
read-only localisation is inconclusive, the plan **may propose** temporary
`GC.GetAllocatedBytesForCurrentThread()` brackets around each pipeline stage
on the two-expression fixture to bisect the stage — but that is a source edit,
so it goes in the plan for approval, not done pre-gate.

---

## 3. Classify and PLAN GATE — stop for approval

Classify the root cause into exactly one of:

- **Accidental churn (default expectation).** An eager allocation not needed on
  the common path — a diagnostics collection allocated when empty, an
  unnecessary intermediate collection, boxing at a boundary, a per-compile
  buffer that could be shared or created lazily. Propose the behaviour-preserving
  fix.
- **Load-bearing.** The allocation is genuinely required by Sprint 7
  error-recovery correctness and cannot be removed without regressing
  behaviour. If so, **do not fix.** Surface it as an accept-and-baseline
  decision needing a D-### and Chris's call. Do not silently update the
  baseline (D-313).

**Stop here.** Present: the allocation site, the introducing commit, the
classification, and the proposed action (the specific fix, or the accept
recommendation). Wait for explicit approval before any source change.

---

## 4. Fix — only after approval

If approved for fix:

- Implement the behaviour-preserving fix. It must change **allocation**, not
  **behaviour**.
- **Regression guard.** The existing compiler / type-checker / error-recovery
  tests are the behaviour guard and must stay green. If the fix touches the
  diagnostics or error-recovery path, the error-examples gold-masters must be
  **byte-identical** — no diagnostic output may change.
- Re-run the compile benchmarks. Confirm `Compile_TwoExpressions` and
  `Compile_TenPrints` bytes/op are back **within the axis-3 10% allocPercent
  gate** versus the rolling baseline — ideally back to the pre-regression
  figures, not merely under the threshold.
- Remove any temporary instrumentation. The tree must be clean.

---

## Stop-and-surface triggers

- Any D-### confirmation in §0 mismatches the live log.
- The allocation delta does not reproduce locally (§1).
- The root cause is load-bearing (§3) — surface, do not fix.
- The fix would change any observable behaviour (diagnostics, emitted
  bytecode, output). A perf fix is behaviour-preserving or it is not this
  increment.
- Localisation implicates a shared `Grob.Core` structure or spans beyond the
  compile path — surface the wider blast radius before editing.

---

## Definition of done

- Root cause localised to a named allocation site; introducing commit
  identified.
- **Either** the fix landed with compile-benchmark allocation back within the
  axis-3 gate versus rolling, behaviour unchanged, all tests and gold-masters
  green, no leftover instrumentation; **or** classified load-bearing and
  surfaced for an accept decision with no baseline change.
- Rolling baseline **not** updated to hide the regression. If the fix restores
  allocation, the baseline may need no change; a deliberate accept bumps it
  only under a recorded D-###.
- One branch, one PR, one concern.

---

## Deliverable shape

- The fix PR — or, if load-bearing, the surfaced finding with no code change.
- Return the root cause, introducing commit, classification, and before/after
  bytes/op, so the decisions-log finding entry can be authored in the planning
  seat with a live-tail-verified D-### number and three-location lockstep.

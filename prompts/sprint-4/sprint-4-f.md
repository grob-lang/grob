---
description: "Sprint 4 · Increment F — calculator close. The loop-and-select calculator.grob smoke script over the Sprint 1–4 surface (no user functions — functions are Sprint 5), the §5 control-flow acceptance, and the second VM-execution benchmark baseline via benchmark.yml (D-309) checked against the two-axis regression gate (D-313). Closes Sprint 4."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 4 · Increment F — calculator close

Increments A–E delivered the full control-flow surface: conditionals, `while`,
loop control, `for...in`, `select` and the switch expression. This increment
closes Sprint 4. It writes the `calculator.grob` smoke script that exercises the
sprint surface end-to-end, confirms the §5 acceptance, and lays the second
VM-execution benchmark baseline through the workflow, checked against the
two-axis regression gate. There is little new code here — it is the closing
gate, the way Sprint 3 closed on `grob run hello.grob`.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 4** acceptance block
   (all control-flow constructs work; array `for...in` and map `for k, v`
   iterate; single-ident map iteration is a clear compile error; the calculator
   smoke test runs; nested loops with `break`/`continue` behave), §3.5.
2. `docs/design/grob-benchmarking-strategy.md` — §8/§9: the second VM-execution
   baseline production path (`benchmark.yml`, D-309) and the **two-axis gate**
   (`Grob.BenchCheck`, D-313 — 5% per-sprint against the rolling baseline, 12%
   cumulative against the frozen origin).
3. Decisions: **D-309** (baseline production via the workflow) and **D-313** (the
   two-axis gate and `Grob.BenchCheck`). Grep and confirm.

> **The calculator close-gate — definition (read this carefully).** §5's
> acceptance names "the calculator smoke test script". The calculator is a
> **planning-defined smoke test**, like Sprint 3's `hello.grob` — it is not one
> of the thirteen release-gate scripts (none of them is a calculator). The
> Sprint 3 kickoff's aside that "the calculator needs functions (Sprint 5)" is
> **superseded**: the Sprint 4 `calculator.grob` is **loop-and-`select` based
> and uses no user-defined functions** — functions, lambdas and call frames are
> Sprint 5 and are out of this sprint's surface. A function-using calculator, if
> ever wanted, is a Sprint 5 artifact. The §5 acceptance is met by a calculator
> built from the Sprint 1–4 surface only.
>
> **What `calculator.grob` must exercise** (the §5 control-flow surface,
> end-to-end through `grob run`):
>
> - Declarations, the scope chain, reassignment, `const`/`readonly` (Sprints 3
>   and 4 A).
> - `if`/`else if`/`else`, `&&`/`||`, a ternary (A).
> - A `while` loop with a `break` and a `continue` (B).
> - A `for...in` over an array and a numeric range (with `step`) (C).
> - A `select` on an operator value (e.g. `+ - * /`) driving the computation,
>   with `break`/`continue` inside a case proven to act on the enclosing loop
>   (D).
> - A switch **expression** producing a value (E).
> - String interpolation in the printed output (Sprint 3 E).
> - **No user functions** — accumulate over a sequence of operations with loops,
>   `select` and reassignment. (`print`/`input` are built-ins, not user
>   functions — they are fine.)
>
> A natural shape: a fixed array of `{op, value}` steps (anonymous structs or
> parallel arrays — confirm what the Sprint 1–4 surface admits; structs are
> Sprint 6, so use arrays/ranges and a `select` on a parallel operator array),
> looped over, `select`-dispatched on the operator, accumulating a running
> result, printing each step with interpolation. Keep it strictly within the
> Sprint 1–4 surface — if you find yourself reaching for a function, a struct
> `type` or a stdlib module beyond the built-ins, stop: the gate must not test a
> later sprint's feature early.
>
> **Verify before relying on D-309, D-313 and the acceptance text.** Grep the
> log and re-read §5 and the benchmarking §8/§9 before building. If the
> acceptance text or the gate policy has moved, surface it.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the `calculator.grob` script
   (and exactly which Sprint 1–4 features each part exercises), the close-gate
   test asserting its stdout, any nested-loop `break`/`continue` regression
   tests §5 names, and the benchmark baseline + gate-check steps — and wait for
   approval before editing.
2. On approval, `/start-branch` → propose `feat/control-flow-close`. Wait for the
   branch.
3. TDD where there is code; the script itself is a fixture with a gold-master
   stdout assertion written first.
4. `/commit-message` against staged changes.
5. Stop after the local commit. Push and PR are Chris's. **The benchmark
   baseline is produced by the `benchmark.yml` workflow on the canonical runner
   (D-309), not by a local `dotnet run -c Release`** — so the baseline commit is
   the workflow's, surfaced to Chris, not something this session fabricates
   locally.

If you find yourself on `main` with edits, stop and surface.

## What is already done

- **Sprints 1–3 and Sprint 4 A–E:** the full control-flow surface —
  conditionals, `while` + loop control, `for...in`, `select` and the switch
  expression — over the declaration/scope/`const`/`readonly`/nullable/
  interpolation base. `grob run` and the REPL exist. All green.

## Deliverable for this increment

1. **`calculator.grob`** — the loop-and-`select` smoke script defined above,
   placed where the sprint's end-to-end fixtures live (confirm the path the
   integration tests use). No user functions.
2. **The close-gate test.** `grob run calculator.grob` through the real CLI entry
   point produces the expected stdout (gold-master), in `Grob.Integration.Tests`.
   A wrong answer is the highest-priority finding to fix before close.
3. **The §5 named regressions.** Array `for...in` and map `for k, v` iterate
   correctly; single-identifier `for k in m` is a clear compile error (E0502);
   nested loops with `break`/`continue` behave as specified — each asserted by a
   test if not already covered in A–E.
4. **The second VM-execution benchmark baseline.** Produced via `benchmark.yml`
   (D-309) on the canonical runner; checked through `Grob.BenchCheck` against the
   two-axis gate (D-313) — the per-sprint 5% against the rolling baseline and the
   12% cumulative against the frozen origin. Surface the gate result to Chris;
   a breach is adjudicated (fix, or a deliberate baseline update with a
   decisions-log entry), not silently accepted.

No new opcodes, no parser/AST edits. No new error codes — the close exercises
codes A–E registered.

## Out of scope

User functions, lambdas, call frames, closures (Sprint 5) — and therefore a
function-using calculator. Structs, the `type` keyword (Sprint 6). Flow
narrowing (Sprint 5). The full benchmark suite / stability calibration (Sprint
8). Rewriting §5 to the A–F structure — that documentation-authority call is
deferred to sprint close and is Chris's. Do not edit the parser, AST or `OpCode`
enum.

## Tests

Per §3.5.

- **Integration tests (`Grob.Integration.Tests`):**
  - `grob run calculator.grob` produces the gold-master stdout.
  - The §5 named cases: array and map iteration, the single-ident map compile
    error (E0502), nested `break`/`continue`.
- **Regression sweep.** The Sprint 1–3 surface still holds — `print(2 + 3 * 4)`
  → `14`, `grob run hello.grob` (Sprint 3's gate) still produces its output, the
  parser-recovery acceptance suite still produces its diagnostic counts.
- **Benchmark.** The VM-execution category runs; the new baseline came from the
  workflow; `Grob.BenchCheck` reports the two-axis result.

## Acceptance (Sprint 4 closes here)

- `dotnet build` and `dotnet test` green.
- All control-flow constructs work; array `for...in` and map `for k, v` iterate;
  single-ident map iteration is a clear compile error; nested loops with
  `break`/`continue` behave as specified.
- `grob run calculator.grob` runs and produces the expected stdout, using the
  Sprint 1–4 surface only (no user functions).
- The Sprint 1–3 surface has not regressed.
- The second VM-execution benchmark baseline is workflow-produced (D-309) and
  passes the two-axis gate (D-313), or a breach is adjudicated and logged.
- The DAG holds; no parser/AST/enum edits.

## Model

Sonnet 4.6 (High) throughout — this increment is a script, a gold-master test
and the benchmark/gate plumbing, all settled. No Opus carve-out.

## Hand-off

Summarise: the `calculator.grob` shape and which Sprint 1–4 features each part
exercises (and the confirmation that it uses no user functions), the close-gate
stdout, the §5 named regressions covered, the regression-sweep result, and the
benchmark baseline + two-axis gate outcome. State plainly whether **Sprint 4 is
closeable** — the §5 acceptance green and the gate passed. Note the deferred
documentation-authority call (whether to rewrite §5 to the A–F structure) for
Chris, and that Sprint 5 (functions and closures) is the next sprint — where
call frames, upvalues, the four-category variable resolution and flow narrowing
land.

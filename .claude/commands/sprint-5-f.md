---
description: "Sprint 5 · Increment F — close. The functions.grob smoke script (recursion, named/default args, a makeCounter closure, a filter/select/sort pipeline with a capturing lambda, an if (x != nil) narrowing — Sprint 1–5 surface only, no stdlib modules), the §6 acceptance across the six increments, and the third VM-execution benchmark baseline against the two-axis gate (D-313)."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 5 · Increment F — close

Increments A–E built the function and closure machinery: call frames, named and
default arguments, lambdas and natives, closures with upvalue capture, the
initialisation state machine and narrowing. This increment closes the sprint the
way Increment F closed Sprint 4 — a smoke script that exercises the whole §6
surface end-to-end, the §6 acceptance confirmed across the six increments, and the
third VM-execution benchmark baseline through the two-axis gate.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 5** acceptance block (the
   bar this increment proves), §1 (the calculator/real-program success criteria —
   context), §3.5 (test routing). Confirm the §6 acceptance now reads "filter,
   **select**, sort" (corrected in Increment C).
2. `docs/design/grob-benchmarking-strategy.md` — **§8/§9**: the VM-execution
   baseline production path (`benchmark.yml`, D-309) and the two-axis gate
   (`tooling/Grob.BenchCheck`, the 5% per-sprint rolling and 12% cumulative origin
   axes, D-313). The baseline is produced by the workflow, not a local run.
3. `docs/design/grob-sample-scripts.md` — the idiom the smoke script matches (the
   release-gate scripts are the style reference; `functions.grob` is a smaller
   planning-defined smoke, not a fourteenth release-gate script).
4. Decisions: **D-313** (two-axis benchmark policy), **D-309** (benchmark workflow
   production). Grep the log; do not take their content from this prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-v1-requirements.md` for the §6 acceptance block and confirm the C
> correction landed, and `grob-benchmarking-strategy.md` §8/§9 for the gate. If
> the acceptance or the gate reads differently than this prompt assumes, surface
> it rather than proceeding.
>
> **Close-gate rules — inline reference (authoritative source is
> `grob-v1-requirements.md` §6 and `grob-benchmarking-strategy.md` §8/§9;
> reproduced here so the implementation does not depend on a fetch landing well).**
>
> - **`functions.grob` is a planning-defined smoke test, Sprint 1–5 surface only.**
>   Like Sprint 3's `hello.grob` and Sprint 4's `calculator.grob`, it is **not**
>   one of the thirteen release-gate scripts. It uses **no stdlib modules** (no
>   `fs`, `json`, `process`, `date`, `regex`, …) — none exist until Sprint 8. It
>   exercises, end-to-end:
>   - a **recursive** function (e.g. factorial or Fibonacci);
>   - a function with **named and default** parameters, called both ways;
>   - a **`makeCounter`-shaped closure** producing two independent counters
>     (category-4 upvalue capture);
>   - an **array pipeline** — `filter`/`select`/`sort` — with a **capturing**
>     lambda (so it exercises a category-4 capture through a native), printed with
>     a built-in (`print`/string interpolation), not `formatAs` (that is fine to
>     use too if it is already built, but it is not required here);
>   - an **`if (x != nil)` narrowing** of a nullable to its non-nullable form.
>   It is idiomatic per `writing-grob-source`: Windows paths in backticks (if any),
>   `:=` bindings, `select` not `map`, same-line braces, four-space indent, British
>   English in prose.
> - **The §6 acceptance is the bar.** Functions call and return; recursion works;
>   named and default arguments work with the four call-site cases caught; lambdas
>   work in `filter`/`select`/`sort`; closures capture with per-call independence;
>   the init state machine catches circular initialisation; narrowing narrows `T?`
>   to `T`; the type checker catches arity and argument-type mismatches; `grob run
>   functions.grob` runs. F confirms the bar is met across A–E, it does not re-prove
>   each increment.
> - **The benchmark baseline comes from the workflow.** The third VM-execution
>   baseline is produced by `benchmark.yml` (D-309), not a local `dotnet run`, and
>   must pass the two-axis gate (`Grob.BenchCheck`: 5% per-sprint vs the rolling
>   baseline, 12% cumulative vs the frozen origin — D-313). A flagged regression is
>   either fixed before close or accepted with a baseline update and a decisions-log
>   entry — the maintainer adjudicates; the implementer surfaces, does not decide.
> - **No new feature work.** This increment is fixture, acceptance verification and
>   benchmark-baseline work. No new opcodes, no type-checker or compiler feature, no
>   parser/AST edits. If proving the acceptance reveals a gap in A–E, surface it as
>   a finding for a fix on the relevant branch — do not patch a feature into the
>   close increment.
>
> **The §6-rewrite question.** Whether to rewrite §6 from the prose scope into the
> explicit A–F structure (as was deferred for §5 at Sprint 4 close) is a
> documentation-authority call for Chris at close, not an implementer action. Raise
> it; do not rewrite §6 unprompted.
>
> **Sequencing note.** This is Increment F, the last of Sprint 5: A → B → C → D →
> E → **F (close)**. After F's acceptance is green and the baseline passes the
> gate, Sprint 5 is closeable. Sprint 6 (user-defined types) is the next sprint.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the `functions.grob` fixture
   and its integration test, the §6 acceptance verification (which existing tests
   cover which acceptance clause, and any acceptance clause not yet covered by an
   A–E test getting a close-increment test), and the benchmark-baseline step — and
   wait for Chris's approval.
2. On approval, run `/start-branch` and propose `feat/functions-close`. Wait for
   Chris to create the branch.
3. Implement with TDD where there is logic; the `functions.grob` fixture follows
   `writing-grob-source` and its integration test asserts the expected stdout. Any
   acceptance-coverage gap gets a test first (follow `tdd-cycle`).
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's. The benchmark baseline is
   produced by the workflow on the merged branch; surface the gate result.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–4** and **Sprint 5 A–E**: the full function and closure surface —
  call frames and recursion, named/default arguments and the four call-site codes,
  lambdas and natives and the array higher-order methods, closures with upvalue
  capture, the initialisation state machine (E5902) and flow-sensitive narrowing.
  All green.

## Deliverable for this increment

The sprint close — the smoke script, the acceptance verification and the benchmark
baseline. **No new feature work.**

1. **`functions.grob` smoke script** exercising recursion, named/default arguments,
   a two-independent-counter closure, a `filter`/`select`/`sort` pipeline with a
   capturing lambda, and an `if (x != nil)` narrowing — Sprint 1–5 surface only, no
   stdlib modules — with an integration test asserting `grob run functions.grob`'s
   expected stdout.
2. **§6 acceptance verification.** Confirm each §6 acceptance clause is covered by a
   passing test across A–F; add a close-increment test for any clause not yet
   directly covered.
3. **Third VM-execution benchmark baseline.** Produced by `benchmark.yml` (D-309)
   on the merged branch, passing the two-axis gate (`Grob.BenchCheck`, D-313).
   Surface the gate result; a flagged regression is the maintainer's call.
4. **Raise the §6-rewrite question** for Chris — do not rewrite §6 unprompted.

No new opcodes, no parser/AST edits, no feature work.

## Out of scope

Any new feature (all A–E). User-defined types, struct construction and `#{ }`
(Sprint 6). Rewriting §6 into the A–F structure (a documentation-authority call,
raised not actioned). Do not edit the parser, the AST or the `OpCode` enum. If
proving the acceptance reveals a feature gap, surface it for a fix on the relevant
feature branch — do not patch a feature into the close.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Integration tests (`Grob.Integration.Tests`):**
  - `grob run functions.grob` produces the expected stdout — the headline close
    test, exercising recursion, named/default args, independent closures, the
    `filter`/`select`/`sort` pipeline and narrowing in one script.
- **Acceptance-coverage tests (routed per the clause):** any §6 acceptance clause
  not already covered by an A–E test gets a directed test in the matching project.
- **Benchmark:** the VM-execution category runs through `benchmark.yml`; confirm
  the baseline was produced by the workflow and passed the two-axis gate.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `grob run functions.grob` runs and produces the expected stdout.
- Every §6 acceptance clause is covered by a passing test.
- The third VM-execution benchmark baseline came from the workflow and passed the
  two-axis gate (5% per-sprint, 12% cumulative — D-313).
- No new opcodes, no parser/AST edits; the DAG holds; the consistency gate (D-316)
  stays green.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The close is fixture and verification work over a
built surface — settled, not judgement. No Opus carve-out.

## Hand-off

Summarise: the `functions.grob` script and its asserted output, the §6 acceptance
coverage (clause → test), the benchmark gate result, and the §6-rewrite question
put to Chris. State plainly whether Sprint 5 is closeable — acceptance green and
baseline through the gate — and note that Sprint 6 (user-defined types: `type`,
named and anonymous `#{ }` construction, field access) is the next sprint.

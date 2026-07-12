---
description: "Sprint 7 · Increment E — sprint close. The errors.grob smoke script over the Sprint 1–7 surface (throw, typed + catch-all, finally on normal and exceptional paths, a nested-finally early-exit chain, a caught runtime error, uncatchable exit), the § acceptance, and the fifth VM-execution benchmark baseline against the two-axis gate (D-313). No stdlib modules."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 7 · Increment E — sprint close

> **Correction (2026-07-12).** This archived prompt carries two false citations,
> discovered during the D-335–337 capture. The corpus is correct; only this prompt was
> wrong.
>
> 1. **`finally` model citation.** Below, the `finally` compilation model is cited as
>    D-332. It is **D-334**. D-332 is the `ValueStack` LOH right-sizing / benchmark fix,
>    unrelated.
> 2. **Smoke-script location.** Below, the four prior smoke scripts are said to live in
>    `grob-sample-scripts.md`. They live in `tests/Grob.Integration.Tests` and never
>    lived in that document (D-337). `grob-sample-scripts.md` holds the thirteen
>    release-gate validation scripts, a different family.
>
> The body below is preserved as originally written; read the citations below with
> these corrections in mind.

Increments A–D built error handling end-to-end: raise, recover, clean up, and make
the existing runtime failures catchable. This increment closes the sprint the way
Sprint 6 closed on `types.grob` — a planning-defined smoke script, `errors.grob`,
that exercises the whole § acceptance surface on `grob run`, plus the fifth
VM-execution benchmark baseline against the two-axis regression gate. No feature work;
this rounds off A–D.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 7 — Error Handling**
   acceptance block in full (this is what `errors.grob` must exercise), and §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§27** once more, to confirm the
   smoke script covers each subsection (throw, typed and catch-all matching, finally
   on the normal and exceptional paths, uncatchable exit).
3. `docs/design/grob-benchmarking-strategy.md` — the VM-execution baseline workflow
   (D-309) and the two-axis gate (D-313, 5% per-sprint vs rolling baseline + 12%
   cumulative vs frozen origin). This is the fifth baseline (Sprint 6 was the fourth).
4. `docs/design/grob-sample-scripts.md` — the shape of the prior smoke scripts
   (`hello.grob`, `calculator.grob`, `functions.grob`, `types.grob`) so `errors.grob`
   matches their voice and stays within the Sprint 1–7 surface.
5. Decisions: confirm **D-309** (the benchmark production workflow), **D-313** (the
   two-axis gate), **D-110** (uncatchable `exit()`) and **D-332** (the finally model,
   logged in C). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep to confirm the §
> acceptance block, the benchmark workflow and D-313 read as this prompt assumes. If a
> section has moved or a decision been superseded, surface it rather than proceeding.
>
> **Close rules — inline reference.**
>
> - **`errors.grob` is a smoke script, not a release-gate script.** Like Sprint 4's
>   `calculator.grob`, Sprint 5's `functions.grob` and Sprint 6's `types.grob`, it is
>   planning-defined and lives alongside them — **not** one of the thirteen
>   release-gate scripts. It uses **no** stdlib modules (`fs`, `json`, `process`,
>   `env` and the rest do not exist until Sprint 8). It exercises only the Sprint 1–7
>   surface.
> - **What it must exercise (the § acceptance surface):**
>   - a user `throw` of a constructed exception (`throw IoError { message: "…" }`) and
>     a typed `catch` that catches it;
>   - a second typed catch that does **not** match, proving source-order first-match;
>   - the bare `catch e` catch-all catching something not matched above;
>   - a `finally` that runs on a **normal** completion and a `finally` that runs on an
>     **exceptional** completion (observable via a side effect);
>   - a **nested** `try`/`finally` with an early `return` that runs both finallys,
>     inner then outer, in order (the D-332 chain);
>   - a **caught runtime error** — an int div/0 caught as `ArithmeticError` — proving
>     a runtime failure is catchable and the script resumes;
>   - an `exit()` inside a `try`/`catch`/`finally` proving **neither** the catch nor
>     the finally runs and the process terminates with the code.
> - **Also still runs.** `hello.grob`, `calculator.grob`, `functions.grob` and
>   `types.grob` still run correctly — no Sprint 7 change regressed them.
> - **The benchmark baseline.** Produce the fifth VM-execution baseline from the D-309
>   workflow and confirm it passes the two-axis gate (D-313). If it regresses, that is
>   a finding — surface it, do not adjust the gate to pass.
>
> **Sequencing note.** This is Increment E, the close: A → B → C → D → **E**. It adds
> no language surface. If you find yourself editing the type checker, compiler or VM
> for anything other than a genuine close-blocking defect, stop and surface it — a
> defect is a finding for its own increment or interlude, not a quiet fix at close.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the `errors.grob` script content,
   the acceptance run, the four prior smoke scripts' regression check, and the
   benchmark baseline production and gate check — and wait for Chris's approval before
   editing. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/errors-close`. Wait for Chris to
   create the branch.
3. Author `errors.grob`, confirm `grob run errors.grob` meets the § acceptance, run
   the four prior smoke scripts, produce the baseline and check the gate. Follow the
   `tdd-cycle` skill for any fixture work.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–6** as summarised in the Increment A prompt.
- **Sprint 7 A–D:** the exception hierarchy and `throw` (A); `try`/`catch` with the
  handler table and matching (B); `finally` with the D-332 compilation model (C); the
  existing runtime throw sites routed through the exception model and `exit()`
  confirmed uncatchable (D). All green.

## Deliverable for this increment

1. **`errors.grob`.** A smoke script over the Sprint 1–7 surface exercising every
   bullet above, using no stdlib modules.
2. **Acceptance.** `grob run errors.grob` meets the § acceptance; `hello.grob`,
   `calculator.grob`, `functions.grob` and `types.grob` still run.
3. **The fifth VM-execution benchmark baseline**, produced from the D-309 workflow,
   passing the two-axis gate (D-313).

## Out of scope

Any language-surface change. The stdlib (Sprint 8). The thirteen release-gate scripts
(their gate is v1, not this sprint). Do not edit the `OpCode` enum, the parser, the
AST or the error registry. If closing surfaces a defect, surface it as a finding — do
not fix it quietly at close.

## Tests

Per §3.5:

- **Acceptance run:** `grob run errors.grob` produces the expected output for each
  exercised bullet; a gold master captures it.
- **Regression:** `hello.grob`, `calculator.grob`, `functions.grob`, `types.grob` run
  correctly.
- **Benchmark:** the fifth baseline is produced and passes the two-axis gate.
- **Integration / spec-consistency:** the consistency gate (D-316) is green; the
  error-code count is unchanged (E adds none).

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `grob run errors.grob` meets the § acceptance and exercises every listed bullet.
- The four prior smoke scripts still run.
- The fifth VM-execution benchmark baseline passes the two-axis gate (D-313).
- No language-surface change; the DAG holds; the count is unchanged.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. This is fixture authoring, an acceptance run and a
benchmark baseline over the finished A–D machinery — no structural decision. There is
no Opus carve-out.

## Hand-off

Summarise: the `errors.grob` script as authored and which acceptance bullet each part
exercises, the acceptance-run result, the prior-smoke-script regression result, and
the fifth benchmark baseline and its gate outcome. Note that Sprint 7 is closeable —
or list the blockers that must clear first. Flag that the merged branch is ready for
the GPT-5.3 Codex cold-read (the Sprint 7 QA brief under `prompts/sprint-7/`). Note for
the next chat: Sprint 8 is the core standard library part 1 (`print`, `input`, `math`,
`strings`, `path`, `env`, `log`, `formatAs`, `guid` as `IGrobPlugin` implementations),
which introduces the first stdlib throw sites (`IoError`, `LookupError`) that flow
through the Sprint 7 machinery built here.

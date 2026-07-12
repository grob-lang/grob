---
description: "Sprint 8 · Increment F — sprint close. The stability-calibration ritual (single-iteration characterisation against the Sprint-8-RUNNABLE script set, not all thirteen — a D-302 addendum + a mechanical grob-benchmarking-strategy.md §6 correction) producing locked iteration/warmup/tolerance in stability.json and a first passing stability run; the sixth VM-execution benchmark baseline against the two-axis gate (D-313/D-333); the gold-mastered stdlib.grob close-gate smoke script (D-337 family, no Sprint-9 modules); the § acceptance."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 8 · Increment F — sprint close

Increments A–E delivered the standard library. This increment closes the sprint off
the feature path: the stability-test calibration ritual and its first passing run, the
sixth VM-execution benchmark baseline, the `stdlib.grob` close-gate smoke script, and
the § acceptance. It writes no module code; it characterises, gold-masters and gates.

Read, in order:

1. `docs/design/grob-benchmarking-strategy.md` — **§6** (the stability test and the
   calibration ritual) and **§11**, under **D-302**. Read the calibration steps (wall-
   clock for one full iteration; steady-state heap after warmup; iteration-to-iteration
   variance) and the locked-numbers trio (iteration count, warmup window, tolerance)
   written to `bench/Grob.Benchmarks/baseline/stability.json`.
2. `docs/design/grob-v1-requirements.md` — the Sprint 8 acceptance (the calibration
   ritual and the first passing stability run), §3.5, and the smoke-family home (D-337).
3. `docs/design/grob-sample-scripts.md` — the thirteen release-gate scripts and, for
   each, which modules it needs (to determine the Sprint-8-runnable subset).
4. Decisions: **D-302** (benchmarking authority — the calibration addendum lands here),
   **D-309** (the canonical `windows-latest` benchmark runner — no locally-produced
   committed baseline), **D-313** (the two-axis per-sprint/cumulative gate), **D-333**
   (the hardened gate — allocation axis, LOH tripwire, CPU-identity guard,
   significance-aware time axis), **D-337** (the smoke-script family and its stdout/
   stderr/exit-code contract). Grep the log.

> **Verify before relying on cited sections.** Grep `grob-benchmarking-strategy.md` §6
> for the calibration-ritual wording and `grob-sample-scripts.md` for each script's
> module dependencies. Confirm the five prior smoke scripts still run. Surface any
> disagreement before building.
>
> **Close rules — inline reference (authoritative source is §6/§11 under D-302, D-309/
> D-313/D-333 and D-337; reproduced here).**
>
> - **The calibration runs against the Sprint-8-runnable script set, not all thirteen.**
>   §6 step 1 says "all thirteen scripts", but scripts depending on `fs`/`csv`/`process`/
>   `regex`/`date`/`json` cannot run until Sprint 9. The calibration characterises the
>   trio against the scripts that **do** run at Sprint 8 close — the five smoke scripts
>   (`hello`, `calculator`, `functions`, `types`, `errors`) plus `stdlib.grob` and any
>   validation script whose modules all exist now. Record this as a **D-302 calibration
>   addendum** and a **mechanical correction to `grob-benchmarking-strategy.md` §6**
>   citing D-302 (the full-thirteen stability run becomes a v1 release-gate step) —
>   surfaced, not swept. Confirm the exact runnable set against the live module surface
>   and the script dependencies; do not assume this prompt's list is exhaustive.
> - **Lock the trio, write `stability.json`, run once green.** From the single-iteration
>   characterisation, set the realistic iteration count, warmup window and tolerance;
>   write them to `bench/Grob.Benchmarks/baseline/stability.json` with the calibration
>   date; implement the stability test (a separate console loop under `Stability/`, not
>   BenchmarkDotNet — fresh VM per iteration, `forceFullCollection: true` at the warmup
>   boundary and at the end) and produce a **first passing run** against the locked
>   numbers.
> - **The sixth VM-execution benchmark baseline (D-309/D-313/D-333).** Trigger
>   `benchmark.yml` on `windows-latest` — **do not** commit a locally-produced baseline
>   (D-309). The result passes the two-axis gate (per-sprint 5% / cumulative 12%,
>   significance-aware time axis; the allocation axis and LOH tripwire; the CPU-identity
>   guard) — D-313/D-333. If a category breaches, it is a finding, surfaced not swept;
>   never commit a baseline that freezes a known defect (the D-313 ratchet-trap rule).
> - **`stdlib.grob` — the close-gate smoke script (D-337).** A planning-defined smoke
>   test (not one of the thirteen release-gate scripts), gold-mastered under
>   `Grob.Integration.Tests`, exercising the Sprint-8 surface with **no** Sprint-9
>   module: `print`, a `math` call, a `path.join`, an `env.get`/`require` with a caught
>   `LookupError`, `log.info`/`error` to stderr, a `guid` generation and `parse` with
>   interpolation, and a `formatAs.table()` over a small struct array. Its contract is
>   stdout, stderr **and** exit code (D-337). Confirm `hello`/`calculator`/`functions`/
>   `types`/`errors` still run.
>
> **Sequencing note.** A → B → C → D → E → **F**. This is the close; it writes no module
> code.

## Branching, planning and commits

Not on `main`. Plan mode → approval → `/start-branch` `feat/stdlib-close` → implement
(the stability test and `stdlib.grob` under TDD/gold-master; the calibration is a
characterisation run, not TDD) → `/commit-message` → stop after the local commit. The
`benchmark.yml` trigger and the baseline commit follow the sprint-close sequence
(merge → trigger → BenchCheck → commit baseline → tag) and are Chris's, not this chat's.

## What is already done

Sprints 1–7, the interlude, and **Increments A–E**: the full Sprint-8 module surface —
the infrastructure and resolution model (D-339), the throw/capability seams (D-340),
`math`/`path`/`strings`/`env`/`log`/`input`, the `guid` primitive type, and `formatAs`.
All green.

## Deliverable for this increment

1. **The calibration ritual.** The single-iteration characterisation against the
   Sprint-8-runnable script set; the locked iteration/warmup/tolerance in
   `stability.json` with the calibration date; the **D-302 calibration addendum** and
   the mechanical **§6 correction** (runnable-set scoping), three-location lockstep for
   any decisions-log touch.
2. **The stability test.** Implemented under `Stability/` (console loop, fresh VM per
   iteration, `forceFullCollection` at the warmup boundary and the end) with a first
   passing run against the locked numbers.
3. **The sixth VM-execution benchmark baseline.** Produced from `benchmark.yml` on
   `windows-latest` (not locally), passing the two-axis gate (D-313/D-333). Any breach
   surfaced, not frozen.
4. **`stdlib.grob`.** The gold-mastered close-gate smoke script, its stdout/stderr/exit
   contract, exercising the Sprint-8 surface with no Sprint-9 module; the five prior
   smoke scripts still green.
5. **The § acceptance met** across A–F.

## Out of scope

Any module code (A–E delivered it). The full-thirteen stability run (deferred to a v1
release gate). `fs`/`process` (Sprint 9). Do not commit a locally-produced benchmark
baseline (D-309).

## Tests

Per §3.5.

- **Integration (`Grob.Integration.Tests`):** `grob run stdlib.grob` matches its
  gold-master on stdout, stderr and exit code; `hello`/`calculator`/`functions`/`types`/
  `errors` still match theirs.
- **Stability:** the stability test produces a first passing run against the locked
  `stability.json` numbers.
- **Consistency:** D-316 green; the calibration addendum's three-location lockstep holds
  if the decisions log was touched; the §6 correction cites D-302.

## Acceptance

- `dotnet build`/`dotnet test` green. `stdlib.grob` runs and gold-masters; the five
  prior smoke scripts still run.
- The calibration ritual is complete: `stability.json` carries the locked trio and the
  date; the stability test passes its first run; the runnable-set scoping is recorded as
  a D-302 addendum + the §6 correction.
- The sixth VM-execution baseline is produced on `windows-latest` and passes the
  two-axis gate; no known defect frozen.
- The § acceptance is met; Sprint 8 is closeable.

## Model

Sonnet 4.6 (High) throughout. Characterisation, gold-mastering and gating over settled
tooling — no Opus carve-out.

## Hand-off

Summarise: the Sprint-8-runnable script set the calibration used and the locked trio;
where the D-302 addendum and §6 correction landed; the stability test's first-run
result; the sixth baseline's gate outcome (and any surfaced breach); `stdlib.grob` and
its gold-master; confirmation the prior smoke scripts still run. Note for the QA
cold-read: the merged Sprint-8 branch is ready for the GPT-5.3 Codex adversarial pass
(`grob-sprint-8-qa.md`), with the module-resolution dispatch (D-339), the native-throw
seam, the `guid` display-opacity and the `formatAs` culture-pinning as the highest-value
probes.

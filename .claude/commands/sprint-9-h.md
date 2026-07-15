---
description: "Sprint 9 · Increment H — close. The stability re-characterisation against the now-larger Sprint-9-runnable script set (Sprint 9 unblocks most of the eleven validation scripts, so the full-suite run deferred by D-346 partly activates), the next VM-execution benchmark baseline against the two-axis gate (D-313/D-333), the gold-mastered close-gate smoke script (D-337 family), the file-organiser real-program target, and the regex.compile() release-gate script flagged by D-350."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment H — Sprint close

Increments A–G delivered the fix, the six modules and the two remaining capability
interfaces. This increment closes Sprint 9 off the feature path: the stability
re-characterisation, the next benchmark baseline, the close-gate smoke script and the
release-gate targets Sprint 9 now unblocks. It writes no module code.

Read, in order:

1. `docs/design/grob-benchmarking-strategy.md` — **§6** (the stability test and its
   calibration ritual) and **§11**, and D-346's calibration addendum (the Sprint 8 close
   ran against six Sprint-8-runnable scripts because every validation script depended on
   a Sprint-9 module; the full-suite run was deferred to a v1 release-gate step).
2. `docs/design/grob-v1-requirements.md` — the **Sprint 9** acceptance (the file-organiser
   real-program target runs; JSON/CSV round-trip; regex matching/replacement; process
   execution captures stdout/stderr), the Definition-of-Done, and the smoke-script family
   home (D-337).
3. `docs/design/grob-sample-scripts.md` — the eleven release-gate validation scripts;
   confirm which now run on the live Sprint-9 module surface (`date`/`fs`/`json`/`csv`/
   `process`/`regex`) and which still depend on an unbuilt plugin (`Grob.Http`/
   `Grob.Crypto`).
4. Decisions: **D-302**/**D-346** (the stability ritual and the Sprint 8 calibration
   addendum), **D-309** (baseline production via `benchmark.yml` on the canonical
   `windows-latest` runner — **not** a locally-committed baseline), **D-313**/**D-333**
   (the two-axis regression gate and the CPU-identity guard), **D-337** (the sprint-close
   smoke-script family and its stdout/stderr/exit contract), **D-350** (the
   `regex.compile()` release-gate script required before v1). Grep for the next-free
   D-number for the close addendum.

> **Verify before relying on cited decisions and sections.** Grep §6/§11 and the sample
> scripts. D-346 corrected the validation-script count from "thirteen" to **eleven** —
> confirm the live count and which scripts run at Sprint 9 close.
>
> **The close — inline reference.**
>
> - **The stability re-characterisation runs against the now-larger Sprint-9-runnable
>   set.** Sprint 8 closed against six scripts (five smoke + `stdlib.grob`) because no
>   validation script ran (D-346). Sprint 9 unblocks most of the eleven — check each
>   against the live module surface and characterise against the set that **does** run
>   (the smoke family plus every validation script whose modules and plugins all exist).
>   Scripts still depending on `Grob.Http`/`Grob.Crypto` remain deferred to the v1
>   release gate. Record the enlarged set and any revised iteration/warmup/tolerance as a
>   **D-302/D-346 addendum** and a mechanical §6 correction — surfaced, not swept. If the
>   locked numbers hold, say so; if they move, record why.
> - **The next VM-execution benchmark baseline.** Produced from `benchmark.yml` on
>   `windows-latest` (D-309 — **not** a locally-produced committed baseline), passing the
>   two-axis gate (D-313/D-333 — per-sprint significance-aware time axis, cumulative axis,
>   the allocation axis with the LOH tripwire, the CPU-identity guard). No known defect is
>   frozen into the baseline (the D-313 ratchet trap). If a locally-run number is all that
>   is available in-session, record the recapture as a subsequent `benchmark.yml` act, as
>   D-338/D-341 did — do not commit a local baseline.
> - **The close-gate smoke script (D-337 family).** A gold-mastered script exercising the
>   Sprint-9 surface — a `date` value, an `fs` read/write, a `json.read().mapAs<T>()`, a
>   `csv` round-trip, a `process.run` capture and a `regex.compile()` match — with a
>   stdout, stderr **and** exit-code contract, under `Grob.Integration.Tests`. The five
>   prior smoke scripts still run.
> - **The file-organiser real-program target.** The Sprint 9 § acceptance names it;
>   confirm it runs correctly on the live surface as a release-gate program, not only as a
>   smoke script.
> - **The `regex.compile()` release-gate script (D-350/D-186).** D-186 requires a
>   `regex.compile()`-exercising release-gate script before v1, since no existing
>   validation script uses regex. Add it (or confirm the file-organiser/smoke coverage
>   satisfies it) and record it as a v1 release-gate item — the deferral of the literal
>   made this a standing obligation.

> **Sequencing note.** This is Increment H, the close: A–G green, then H rounds off the
> sprint. It writes no module code.

## What you're building

1. **The stability re-characterisation** against the enlarged Sprint-9-runnable set,
   recorded as a D-302/D-346 addendum and a §6 correction; locked numbers in
   `stability.json`; a passing run.
2. **The next VM-execution benchmark baseline** via `benchmark.yml` on `windows-latest`,
   passing the two-axis gate, no frozen defect.
3. **The close-gate smoke script** (D-337 family) — gold-mastered stdout/stderr/exit,
   exercising the Sprint-9 surface with no unbuilt plugin.
4. **The file-organiser real-program target** confirmed running on the live surface.
5. **The `regex.compile()` release-gate script** (D-350/D-186) added or its coverage
   confirmed, recorded as a v1 release-gate item.
6. **The close addendum decision** — recorded at its real next-free number in
   three-location lockstep.

No module code, no new opcode, no new error code (unless a genuine close-gate gap
surfaces a diagnostic — then via `allocating-an-error-code`, D-331).

## Out of scope

Any module feature (A–G). The full-suite stability run for scripts still blocked on
`Grob.Http`/`Grob.Crypto` (a v1 release-gate step). Committing a locally-produced
baseline (D-309 forbids it).

## Tests

- **Integration / spec-consistency (`Grob.Integration.Tests`, `Grob.Consistency.Tests`):**
  the close-gate smoke script meets its stdout/stderr/exit contract; the five prior smoke
  scripts still pass; the file-organiser target runs; D-316 green; the count reconciled;
  the D-335 membership check green.
- **Stability (`bench/Grob.Benchmarks`):** the stability test produces a passing run
  against the locked calibration numbers over the enlarged runnable set.
- **Benchmark gate:** the new baseline passes the two-axis gate (D-313/D-333) with no
  frozen defect; the CPU-identity guard honoured.

## Acceptance

- The stability re-characterisation is complete against the enlarged runnable set, locked
  numbers in `stability.json`, a passing run, recorded as a D-302/D-346 addendum and a §6
  correction.
- The next VM-execution benchmark baseline passes the two-axis gate, produced on the
  canonical runner, no frozen defect.
- The close-gate smoke script meets its stdout/stderr/exit contract; the five prior smoke
  scripts still pass; the file-organiser real-program target runs.
- The `regex.compile()` release-gate obligation (D-350/D-186) is satisfied or explicitly
  recorded as a v1-gate item.
- D-316 green; the close addendum logged in lockstep; coverage floor held.

## Model

Sonnet 4.6 (High). Calibration, baseline production and gold-mastering over settled
tooling — no Opus carve-out.

## Hand-off

Summarise: the enlarged Sprint-9-runnable set and the stability outcome; the new baseline
and its gate result; the close-gate smoke script and its contract; the file-organiser
target result; the `regex.compile()` release-gate disposition; the close addendum. Note
that Sprint 9 completes the core standard library — Sprint 10 is Script Parameters and
Decorators; the remaining v1 release-gate steps are the full-suite stability run (scripts
still blocked on `Grob.Http`/`Grob.Crypto`) and the regex-literal deferral's v1.1
follow-up (D-350).

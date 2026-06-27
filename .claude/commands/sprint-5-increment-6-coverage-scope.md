# Sprint 5 — Increment 6: Test-coverage scope and floor

Final Sprint 5 interlude increment. Runs **after Increment 5 (D-327)** and **before the
GPT-5.3 Codex cold-read**. Purpose: give test coverage a defined scope and a CI floor, close
the single outstanding maintainability issue, and clear the known board so the cold reader
spends its budget on unknowns — not on gaps already on the SonarCloud dashboard.

This is execution against a logged decision. The decision is **D-328 / ADR-0018**
(coverage scope and floor). The *work* below — tests, exclusion config, the S3776 refactor —
is not itself a decision and earns no further D-number.

---

## 0. Verify-first — STOP before any code

Confirm against the **live** files, not memory and not this prompt:

1. **Decisions-log tail.** Expect `D-327` as the current tail. Allocate `D-328` for the
   coverage scope-and-floor decision only after confirming D-327 is the last full entry and
   D-328 is unused. If the tail is not D-327, stop and report.
2. **ADR tail.** Expect `ADR-0017` as the last published ADR in `docs/wiki/ADR/`. Allocate
   `ADR-0018`. If the tail is not ADR-0017, stop and report.
3. **Error-code count.** This increment adds **zero** error codes. Run the D-316 drift gate
   (`tests/Grob.Consistency.Tests`) and record the current count it agrees on (expected: the
   merged Sprint 5 figure). The same gate must pass green at increment close with the count
   **unchanged**. If any task below tempts you to add a code, that is a stop-and-surface, not
   a quiet addition.
4. **SonarCloud baseline.** Record the current overall figure (observed: **88.9%**) and the
   per-file worklist in §3 so the close can be measured against a known start.

Do not edit `src/` until the plan (§7) is approved.

---

## 1. The decision this implements (D-328 / ADR-0018)

A bare coverage percentage with no defined scope is undefined — it drifts because nobody knows
what it is supposed to mean, and "top up to 90%" invites assertion-free filler that games the
number. D-328 fixes both halves:

- **Scope.** A committed exclusion set defines what is deliberately *out* of the coverage
  denominator. Every exclusion carries a reason. What remains is the language implementation
  proper — lexer, parser, type checker, compiler, VM, runtime, stdlib — the surface where a
  coverage number means something.
- **Floor.** The in-scope denominator carries a mechanical line+branch coverage floor (**90%**),
  enforced in CI, red on breach. It is a tripwire that triggers triage, not a target to fill.

This mirrors the D-313 benchmark gate ("mechanical, not eyeballed") and the D-316 CI drift
regime (self-relative, no frozen baseline).

---

## 2. Load-bearing rules (inline)

- **Pin, then refactor.** For existing behaviour, tests are *characterisation* tests: they pin
  what the code does now and pass on first write. Red-green-refactor in the feature sense does
  not apply — the sequence is **pin behaviour → refactor → tests stay green**.
- **A failing characterisation test is a defect signal, not a fixture to author.** If a test
  written for an uncovered path fails because the code is *wrong*, quarantine it and surface.
  Never assert the wrong output as a gold master. This is the §0 stop-and-surface discipline
  applied to coverage work, where latent defects are most likely to surface.
- **Coverage is triage, not topup.** Classify every uncovered region into one of three buckets
  before writing anything: (a) genuinely untested behaviour → write a real test; (b) defensive
  or structurally-unreachable code → exclude with an annotated reason; (c) dead code → delete.
  Never write an assertion-free test to move the number.
- **Exclusions carry reasons.** Every `[ExcludeFromCodeCoverage]` and every
  `sonar.coverage.exclusions` glob carries a comment naming *why* it is out of scope. An
  unexplained exclusion is rejected in review.
- **User-facing surface is never excluded to hit a number.** Diagnostic formatting and the
  run pipeline are the identity ("a developer can trust"). They are *covered*, not *excluded* —
  see §3.1.

---

## 3. The work, sequenced

### 3.1 Instrumentation first — possibly decisive, definitely cheap

`RunCommand.cs` (0%, 31 lines) and `DiagnosticFormatter.cs` (0%, 10 lines) read as zero while
the thirteen validation scripts and the 40-pair gold-master error-examples library exercise
exactly those paths end-to-end. That contradiction means the suite is not instrumented into the
coverage run — almost certainly an out-of-process invocation the collector cannot see.

- Determine whether the validation-suite and gold-master harnesses invoke Grob in-process or
  shell out.
- If in-process: confirm they are inside the coverage collector's assembly set and fix the
  instrumentation config so their coverage is counted.
- If out-of-process: this is a **stop-and-surface**. Present the options (move to in-process
  invocation for the coverage run vs merge a separate coverage collector's output) with a
  recommendation. **Do not** exclude `RunCommand` or `DiagnosticFormatter` to make the number
  move — excluding user-facing run and diagnostic surface to hit a floor is precisely the
  gaming D-328 forbids.

Expected effect: a meaningful overall-coverage rise with no new test authored. Do this before
anything else so the rest of the worklist is measured against the corrected denominator.

### 3.2 `Compiler.Statements.cs` — the risk centre (49.5%, 53 lines / 43 conditions)

Half-covered statement **bytecode emission** with 43 untested branches is the worst failure
class: it compiles green and emits incorrect behaviour at runtime. This gets real
compile-then-disassemble or compile-then-execute assertions per statement form. Highest
priority of the test work.

- One characterisation test per statement form's emission, asserting the emitted opcode
  sequence (via the D-306 disassembler) or the executed result.
- A statement form that emits **wrong** bytecode → quarantine and surface (§2). Do not encode
  the wrong sequence as the expected.

### 3.3 `TypeChecker.Statements.cs` — cover, then kill S3776 (86.8%, 11 / 22)

The single outstanding maintainability issue (`csharpsquid:S3776`, cognitive complexity) is the
statement-visit method in this file. Coverage and the refactor are the **same** surface and
sequence together:

1. Raise coverage on the current method to pin its behaviour — one characterisation test per
   statement-kind branch it dispatches.
2. **Under green**, refactor: decompose the monster visit into per-statement-kind handlers.
   The refactor is behaviour-preserving — same diagnostics, same codes, same order. Nothing
   observable changes.
3. Confirm the characterisation tests still pass and S3776 clears.

If the decomposition would change any observable diagnostic, that is a **stop-and-surface** —
the refactor is pure structure, not a behaviour change smuggled in under a coverage increment.

### 3.4 REPL — extract the eval core, exclude the loop (`ReplCommand.cs` 0%, 139 lines)

The largest single contributor, and most of it is not a testing problem. A read-eval-print loop
resists unit testing by nature; its eval core does not.

- Extract the eval-one-line core into a testable unit (no console IO) and cover it.
- Leave the read-print IO loop as a thin shell, excluded with an annotated reason.
- Do **not** attempt to unit-test the loop itself.

### 3.5 `GrobValue.cs` — the genuine middle case (85.4%, 4 / 23 conditions)

23 uncovered conditions split two ways. Triage each:

- Variants the compiler does not yet emit → exclude with a comment naming the variant and why
  it is unreachable in v1.
- Live equality/accessor/factory paths → write real tests.

No representation change. Tests target the **public** surface only (factory + accessors); the
internal layout is closed (D-303).

### 3.6 Exclude the noise tier — do not test it

These move out of the denominator with annotated reasons; writing tests to nudge them is waste:

- `tooling/Grob.BenchCheck/Program.cs`, `tooling/Grob.DriftCheck/Program.cs` (0%, 1 line each)
  — `Main` entrypoint wrappers.
- `src/Grob.Cli/DiagnosticFormatter.cs` — **NOT here.** Covered per §3.1.
- Record types with a single uncovered defensive member — `CaseClause.cs` (75%), `SwitchArm.cs`
  (75%), `CatchClause.cs` (80%): these fall out incidentally when §3.2/§3.3 exercise the
  surrounding statements (a catch test exercises `CatchClause`). If a single line remains after
  that, exclude it with a reason. Do not author a bespoke test for the record alone.
- `GrobArithmeticException.cs` (50%, 1 line), `ErrorDescriptor.cs` (85.7%, 1 line),
  `Diagnostic.cs` (91.3%, 1 line) — single defensive/structural lines; exclude with reason or
  let §3.2/§3.3 incidentally cover.

### 3.7 Apply the scope config and wire the floor gate

- Commit the exclusion set as `sonar.coverage.exclusions` (and matching coverlet
  `[ExcludeFromCodeCoverage]` / `.runsettings` for local parity), each glob commented.
- Wire the **90% line+branch floor** on the in-scope denominator into CI, red on breach,
  mechanical (no eyeballing) — the D-313 shape. Read SonarCloud's blended coverage, which
  includes conditions, because branch coverage on the parser and type checker is the number
  that actually protects against silent regressions.

---

## 4. Closed surface — do not touch

- **Opcodes, `.grobc`, `GrobValue` layout.** Tests may target `GrobValue`'s public surface; the
  representation is closed (D-303). No opcode or bytecode-format change.
- **Error codes.** No additions, no edits, no renumbering. Count frozen (§0.3).
- **Language and spec semantics.** No grammar, type-system or runtime behaviour change. The
  §3.3 refactor is decomposition only.
- **Previously merged decisions (D-320–D-327).** Off-limits except as the code under test.
- **Benchmark baselines and `policy.json`.** Untouched — this is the coverage gate, not the
  perf gate.

---

## 5. Stop-and-surface triggers

Pause and report rather than resolve independently if:

- A characterisation test reveals wrong bytecode emission (§3.2) or a wrong diagnostic (§3.3).
- The validation-suite / gold-master harness is out-of-process and cannot be instrumented
  without a structural change (§3.1).
- Any task appears to need a new error code, or the D-316 count would change.
- The §3.3 decomposition would alter any observable diagnostic.
- The live decisions-log tail is not D-327, or the ADR tail is not ADR-0017 (§0).

---

## 6. Plan-mode gate

Produce a numbered plan covering: the §3.1 instrumentation finding (in-process vs out-of-process,
with the chosen path), the per-file test list, the §3.3 refactor decomposition, and the full
exclusion set with reasons. **No `src/` change until that plan is explicitly approved.**

---

## 7. Definition of done

- §3.1 instrumentation resolved; `RunCommand` and `DiagnosticFormatter` covered, not excluded.
- `Compiler.Statements.cs` carries per-statement-form emission tests; no quarantined defect left
  unsurfaced.
- `TypeChecker.Statements.cs` S3776 cleared under green; characterisation tests pass post-refactor.
- REPL eval core extracted and covered; IO loop excluded with reason.
- `GrobValue` middle case triaged; noise tier excluded with reasons.
- Scope config committed (every exclusion commented); 90% line+branch floor wired into CI, red
  on breach.
- Overall in-scope coverage at or above the floor.
- D-316 drift gate green; error-code count unchanged.
- D-328 inserted (index row, full entry, changelog line); ADR-0018 published; `Home.md` ADR
  index updated.
- Full benchmark suite run per D-313 at increment close (coverage work touches no hot path; the
  run confirms it).

# Session hand-off — 23 November 2026

## Where we are

- Branch: `feat/crash-oracle`, HEAD `c5cf812` before this commit
- Working tree: clean (scratch is in gitignored `.grob-session/`)
- Commits on this branch: see `git log --oneline origin/main..HEAD`. The code commits are:
  - group 1 `a0915a7`, the oracle;
  - group 2 `9b3c762`, the CI slice;
  - `476c730`, `7aef72e`, `9424a17`, `8c48f01`, the review fixes, the relock, the newline restore and the cancellation fix;
  - group 3 `c5cf812`, the CLI's E9001 report and the documented exit codes.
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Gate report: `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree
- Picked up from: this branch's own previous hand-off, which this file overwrites. No other path is pending retirement
- Phase reached: **session C, group 3 complete.** Group 4 (the mutator and the minimiser) has not been started. Draft PR #268 is open

## Green state

- Tests: full `dotnet test Grob.slnx` green after group 3 and after restoring the handler following mutation-verify 4. Integration 990, Compiler 2,805, Vm 473, Stdlib 672, Runtime 378, Core 345, Consistency 79, Torture 125, BenchCheck 76.
- Coverage on group 3's changed lines in `Grob.Cli`: 186 of 188 lines, 98.9%, and 48 of 48 branches. Measured from the Integration project's cobertura report, over the diff against `HEAD`. The one uncovered line is `RunCommand.cs:169`, the return after a post-compile `HasErrors`. It was uncovered before this change, because the compiler adds no diagnostics today.
- Error-code count unchanged at 122; the D-316 gate (`Grob.Consistency.Tests`) is green.
- Pre-push gate: runs on this hand-off's push.
- CI on PR #268 for this session's commit: not seen. At session start the Windows build and SonarCloud on the previous head had no conclusion; I did not recheck them.
- Mutation-verify 4 (remove the `catch (Exception)` in `RunCommand.Run`):
  - I predicted 15 failures, each the unhandled exception: 8 × `InvalidOperationException: secret detail`, 6 × `GrobInternalException: GrobValue kind mismatch…`, 1 × `clock failed`. Those 15 failed as predicted.
  - **17 failed.** I missed two: `SampleVerdictTests.Bare_ReportsAnInternalError_Fails` and `Blocked_FailsItsClass_Passes`. Both run the R-42 reproducer through the real CLI, which throws a `GrobInternalException` in the VM, so both now depend on the handler.
  - Restored; the suite is green again.

## Decisions taken, not yet logged

Every item below belongs in the landing entry as a divergence or correction (prompt §5). Items A to I are carried from the previous hand-off, which this file overwrites.

- **Q1 to Q5**, Chris's rulings of 23 November. Their text is in the gate report's §0.
- **A. The first stack frame is not part of a signature.** Tiered JIT inlining makes it unstable between early and late programs, and between Debug and Release. It is kept on `Finding.FirstFrame`.
- **B. Four files are linked from `Grob.Integration.Tests`:** `SampleExtractor`, `SampleBlock`, `ErrorExampleMaster` and `DeferredWorkRegister`. A namespaces-follow-folders exception.
- **C. The findings record store and §4's two-records test move to group 4.**
- **D. A runtime error with an unregistered code is a finding.**
- **E. Correction to the gate report's §1.1: the sentinel rule is one-directional.** `UnresolvedDecl` implies `ResolvedType == Error`. The converse is false, which is cascade suppression (§29.3).
- **F. The slice warms up on every tenth seed**, not a full pass.
- **G. A stale known entry fails the slice.**
- **H. `check` and `corpus` stop after a watchdog timeout.**
- **I. The VM's token is cancelled by the oracle's waiting thread** (`8c48f01`), not by a timed `CancellationTokenSource`, which was starved on the Windows CI runner.
- **J. The gate report's exit-code scan was incomplete: ten tests change from `1` to `2`, not three.**
  - It found three; the suite found seven more. The ten:
    - `Sprint8IncrementATests`: three;
    - `Sprint8IncrementDTests`: two;
    - `Sprint9IncrementBTests`: two;
    - `Sprint9CRegexTests`: three.
  - Each asserts a checker or parser code. The two `CommandLineRunnerTests` changed to `3` and were renamed.
- **K. The REPL catches `GrobRuntimeException` at the iteration level**, with `GrobExitException` and the general clause, and the inner handler in `ProcessEntry` is gone. A runtime error now propagates out of `ProcessEntry` and skips `RegisterNewDeclarations` by unwinding instead of by `return`. Output and behaviour are unchanged.
- **L. `ReplCommand` gained an internal constructor** taking `Func<HostCapabilities>` and the phase hook, which it lacked (gate report §2.5). The public constructor chains to it. The REPL's phase starts at `Lexing`, because `ReadEntry` and `IsBareExpression` lex before the hook runs; `LoadingStandardLibrary` never occurs in the REPL, since the catalogue is built once in the constructor.
- **M. A real CLI E9001 reads as a diagnostic in the public samples verdict.** `SampleVerdictTests.Bare_ReportsAnInternalError_Fails` now asserts `"writes E9001"` where it asserted `"internal error"`. I did not investigate how `CliDiagnosticReader` accepts the `--> file (while running)` line as a location, since the location pattern ends at `:line[:col]`. Worth a look when the error-examples harness next meets an E9001.
- **N. R-64 and R-72 stay open in the register.** A closed row names the D-number that closed it, and that number comes with the landing entry (session E, prompt §5). Group 3's tests are the evidence to cite: `ExitCodeTests`, `InternalErrorReportTests`, `ReplInternalErrorTests`.
- **O. Docs changed:** `Error-Handling.md` §Exit Codes gained `2`, `3` and the E9001 line, and `CLI/Commands.md` gained an exit-code paragraph. Requirements §8 already stated the documented set. The old `WriteInternalError` mentions in the decisions log and the prompt are historical and untouched.

## Error codes and registry state

None allocated. The count is 122. E9001 was already registered and is now raised from `InternalErrorReport` through `ErrorCatalog.E9001`; its code string still appears once, in its descriptor.

## Tried and rejected

- A short watchdog without a warm-up run: a cold catalogue build outlasts it.
- A platform branch for the fake current directory: it left one branch uncoverable.
- Guards in `AstPath` for a visited set, a null constructor and indexers: unreachable, and removed.
- Changing `Environment.CurrentDirectory` in a CLI test: process-global state in a parallel run.
- `for` with the loop variable updated in its body (Sonar S127): replaced by a `while`.
- A default arm that throws in `InternalErrorReport.Words`: unreachable, so it cost a branch. `Running` is the final arm.
- A public `TheoryData<PipelinePhase, …>` in the E9001 tests: `PipelinePhase` is internal and the test class is public (CS0053). The theory passes phase names as strings and parses them.
- A separate inner `catch (GrobRuntimeException)` in the REPL beside the outer one: redundant once the outer clause exists (decision K).
- IDE diagnostics about missing references in `tooling/Grob.DriftCheck/ConsistencyChecks.DefaultedParameters.cs` appeared throughout the session. The build and the suite are clean; I did not chase them.

## Quarantined or skipped tests

None.

## Why this session stopped

Not stopped for a STOP condition. Group 3 is a finished commit, and the prompt asks for a hand-off after each group. Group 4 is next in the same session row (C).

## Next session, first action

If this is a new session, run the prompt's session-start check for session C, group 4 only (Sonnet, high); then start group 4 with the span locator's range-fidelity test, red, per the gate report's §1.6.

## Open for the maintainer

- Draft PR #268 now carries groups 1 to 3 and the PR description needs group 3's commit and the exit-code change. I update it after the push, not in this commit.
- Carried from earlier hand-offs:
  - the benchmark workflow for #261 is still to dispatch;
  - `.grob-session/handoffs/feat-stdlib-regex-replace-fn.md` on `main` is unretired;
  - the fake-capability test covers `env`, `path.resolve`, the clock and the streams until 9C (gate report §4.2);
  - Dependabot will drop the final newline from its lockfiles again on its next bump.

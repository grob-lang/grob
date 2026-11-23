# Session hand-off — 23 November 2026

## Where we are

- Branch: `feat/crash-oracle`, rebased onto `main` at `4800385` (#267), HEAD `7aef72e` before this commit
- Working tree: clean (scratch in gitignored `.grob-session/`)
- Commits on this branch: see `git log --oneline origin/main..HEAD`. The code commits are:
  - group 1 `a0915a7`, the oracle;
  - group 2 `9b3c762`, the CI slice;
  - `476c730`, the local CodeRabbit review fixes;
  - `7aef72e`, the relock after #267's SonarAnalyzer bump.
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Picked up from: this branch's own previous hand-off, which this file overwrites. No other path is pending retirement
- Phase reached: **session B complete.** Change groups 1 and 2 are committed, and a draft PR is open. The gate report is at `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree

## Green state

- Tests: full `dotnet test Grob.slnx` green after the rebase, 5,901 passed. `Grob.Torture.Tests` has 125.
- Coverage: the new `tooling/Grob.Torture/` files are at 99.8% line and 94.9% branch. That measure excludes generated regex code and the four linked Integration files, which are not new code. One branch is left partial: the post-compile `HasErrors` check in `OracleRun`, unreachable because the compiler adds no diagnostics today.
- CI slice runtime here: the measured pass of 1,318 programs takes 4.0 s. The slice adds about 9 s to `dotnet test` with the warm-up and determinism passes.
- CI slice runtime in CI, on PR #268 at `8c48f01`, from log timestamps: the fixture and its first assertion finish about 13 s after the Torture test run starts on Ubuntu, and about 6 s after on Windows. All 12 checks are green. SonarCloud reports 0 issues on new code. R-69's message matches on Linux.
- Pre-push gate: runs on this hand-off's push
- Mutation-verify, each predicted exactly before it ran:
  - 1 (a compiler host exception classified as a finding): the test host crashed with `InvalidOperationException: no`;
  - 2 (fake file system isolation): only the `path.resolve` test failed, actual `<bin dir>\a`;
  - 3 (the known list's row check): exactly two failures, `Problems_EntryWithoutARow_IsReported` and the corpus CLI problem line.

  Number 4 is group 3's.

## Decisions taken, not yet logged

Every item below belongs in the landing entry as a divergence or correction (prompt §5).

- **Q1 to Q5**, Chris's rulings of 23 November. Their text is in the gate report's §0.
- **A. The first stack frame is not part of a signature.** Tiered JIT inlining makes it unstable between early and late programs, and between Debug and Release builds. It is kept on `Finding.FirstFrame` instead.
- **B. Four files are linked from `Grob.Integration.Tests`:** `SampleExtractor`, `SampleBlock`, `ErrorExampleMaster` and `DeferredWorkRegister`. They keep their test-project namespace, a namespaces-follow-folders exception.
- **C. The findings record store and §4's two-records test move to group 4.**
- **D. A runtime error with an unregistered code is a finding.**
- **E. Correction to the gate report's §1.1: the sentinel rule is one-directional.**
  - `UnresolvedDecl` implies `ResolvedType == Error`, per D-311.
  - The converse is false: a name resolved to an `Error`-typed declaration keeps that declaration. That is cascade suppression (§29.3).
  - The "exactly when" rule flagged 11 correct programs in the corpus.
- **F. The slice warms up on every tenth seed**, not a full pass. A full pass doubled the time and added no warmth.
- **G. A stale known entry fails the slice.** An entry is stale when its program no longer makes the finding or is no longer in the corpus. The prompt asked only for an entry whose row is closed to fail it.
- **H. `check` and `corpus` stop after a watchdog timeout**, since the worker cannot be killed. `Cli` takes the oracle through an internal seam for this.
- **I. The VM's token is cancelled by the oracle's waiting thread** (`8c48f01`), not by a timed `CancellationTokenSource`.
  - The gate report's §4.4 proposed the timed source.
  - Its thread-pool timer was starved on the Windows CI runner, so the `CompilerFinallyTests` finally-loop seed showed as `Run|Timeout`.
  - The waiting thread now polls in 10 ms steps and cancels at the VM budget. The VM is unchanged.

## Error codes and registry state

None allocated. The count is 122.

## Tried and rejected

- A short watchdog without a warm-up run: a cold catalogue build outlasts it.
- A platform branch for the fake current directory: it left one branch uncoverable. `Path.GetFullPath("/grob-torture")` is used instead.
- Guards in `AstPath` for a visited set, a null constructor and indexers: unreachable, and removed.
- Changing `Environment.CurrentDirectory` in a CLI test: process-global state in a parallel run. `Cli.Run` takes the working directory as a parameter instead.
- `for` with the loop variable updated in its body (Sonar S127): replaced by a `while`.

## Quarantined or skipped tests

None.

## Why this session stopped

Session B's row (groups 1 and 2) is done. Under the prompt, a session may not cover groups from two rows.

## Next session, first action

Run the prompt's session-start check for session C (change groups 3 and 4, Sonnet, high), reading this file and the draft PR.

## Open for the maintainer

- Draft PR #268 is open. Session C adds groups 3 and 4 to it.
- One extra commit, `9424a17`, restores the final newline that Dependabot's #266 and #267 dropped from 14 files. Without it, the end-of-file pre-push hook blocks any push that carries those commits. Dependabot will drop the newline again on its next bump unless its output is normalised.
- **Register rows R-68 to R-74** were filed for F-1 to F-7. R-66 gained a sentence on the slice's seven seeds, including the property read `s?.length`.
- `ErrorStmt` was never reached by the sentinel walker in tests. It is a two-line override, noted rather than chased.
- Carried from earlier hand-offs:
  - the benchmark workflow for #261 is still to dispatch;
  - `.grob-session/handoffs/feat-stdlib-regex-replace-fn.md` on `main` is unretired;
  - the fake-capability test covers `env`, `path.resolve`, the clock and the streams until 9C (gate report §4.2).

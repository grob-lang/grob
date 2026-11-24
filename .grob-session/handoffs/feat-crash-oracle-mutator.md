# Session hand-off — 24 November 2026

## Where we are

- Branch: `feat/crash-oracle-mutator`, cut from `feat/crash-oracle` at `40ae287`, HEAD `592c679` before this commit. A **stacked branch**: its PR has `feat/crash-oracle` (PR #268) as its base
- Working tree: clean (scratch is in gitignored `.grob-session/`)
- Commits on this branch beyond its parent: `592c679`, change group 4, the mutator, the minimiser and the finding store. The parent holds groups 1 to 3 (`a0915a7`, `9b3c762`, `c5cf812`) and the commits between them; see `git log --oneline origin/main..HEAD`
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Gate report: `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree
- Picked up from: `.grob-session/handoffs/feat-crash-oracle.md`, the parent branch's hand-off, retired here by `git rm`. It stays on the parent branch, in PR #268, until that merges. No other path is pending retirement
- Phase reached: **session C complete.** Groups 3 and 4 are done. Group 5 (the generator, session D) has not started

## Why two PRs

PR #268 changed 87 files against `main` after group 3, and group 4 adds 43, which is 123 to 130. CodeRabbit skips a PR over 100 files. Chris chose a stacked PR on 24 November: #268 stays at groups 1 to 3, and this branch carries group 4 with base `feat/crash-oracle`. Sessions D and E stack further. Each PR must stay under 100 files, so plan group 5 and groups 6 and 7 as their own branches or PRs if they would push this one over.

## Green state

- Tests: full `dotnet test Grob.slnx` green after group 4. Torture 278, Integration 990, Compiler 2,805, Vm 473, Stdlib 672, Runtime 378, Core 345, Consistency 79, BenchCheck 76. `dotnet format Grob.slnx --verify-no-changes` clean.
- Coverage on group 4's new and changed `tooling/Grob.Torture` lines: 639 of 639 lines, 100.0%, and 437 of 474 branches, 92.2%. Measured from the Torture test project's cobertura report against `40ae287`. The partial branches are guard conditions in the operators.
- CI slice, measured here: 1,318 seeds and 536 mutants in 5.8 s for the measured pass; the whole Torture test project takes 8.8 s. **Not measured in CI.** Gate report §1.5 allows 15 s here and 30 s in CI; the previous CI figure, from before the mutants, was about 13 s on Ubuntu for a 4.0 s local pass.
- Error-code count unchanged at 122; the D-316 gate (`Grob.Consistency.Tests`) is green.
- Pre-push gate: runs on this hand-off's push.
- Mutation-verify for group 4, each predicted before it ran:
  - A, `FileMode.CreateNew` to `FileMode.Create` in `FindingStore.Add`: exactly one failure, `Add_SecondRunOverTheSameProgram_LeavesTheFirstRecordUnchanged`, at `Assert.False(written)`, `Expected: False, Actual: True`. My first attempt hit the doc comment, not the code, so nothing failed; I redid it against the real call.
  - B, the minimiser's line pass ignoring its direction: exactly one failure, `Minimise_TwoRootCausesOfDifferentShape_DirectionsDisagree`, at `Assert.True`.
- **TDD was not red-first for group 4.** I wrote the operators, the mutator, the minimiser and the store before their tests. The tests took their expectations from the gate report's operator table, not from the code, and nine failed at first for real reasons (see decision P). A corpus-wide robustness test and the two mutation-verifies above stand in for the red step.

## Decisions taken, not yet logged

Every item below belongs in the landing entry as a divergence or correction (prompt §5). Items A to O are carried from the parent's hand-off, which this branch retires.

- **Q1 to Q5**, Chris's rulings of 23 November. Their text is in the gate report's §0.
- **A.** The first stack frame is not part of a signature (tiered JIT inlining makes it unstable). It is kept on `Finding.FirstFrame`.
- **B.** Four files are linked from `Grob.Integration.Tests`: `SampleExtractor`, `SampleBlock`, `ErrorExampleMaster` and `DeferredWorkRegister`. A namespaces-follow-folders exception.
- **C.** The findings record store and §4's two-records test moved to group 4. Done here.
- **D.** A runtime error with an unregistered code is a finding.
- **E.** Correction to the gate report's §1.1: the sentinel rule is one-directional (`UnresolvedDecl` implies `Error`, not the converse).
- **F.** The slice warms up on every tenth seed, not a full pass.
- **G.** A stale known entry fails the slice.
- **H.** `check` and `corpus` stop after a watchdog timeout.
- **I.** The VM's token is cancelled by the oracle's waiting thread, not by a timed `CancellationTokenSource`.
- **J.** The gate report's exit-code scan found three tests to change; the suite found ten, all asserting a checker or parser code.
- **K.** The REPL catches `GrobRuntimeException` at the iteration level, and its inner handler is gone.
- **L.** `ReplCommand` gained an internal constructor taking `Func<HostCapabilities>` and the phase hook.
- **M.** A real CLI E9001 reads as a diagnostic in the public samples verdict. I did not investigate how `CliDiagnosticReader` accepts the `--> file (while running)` line as a location.
- **N.** R-64 and R-72 stay open until the landing entry names the D-number that closes them. The evidence is `ExitCodeTests`, `InternalErrorReportTests` and `ReplInternalErrorTests`.
- **O.** Wiki exit-code docs changed: `Error-Handling.md` and `CLI/Commands.md`.
- **P. A node's `Range.End` is not its end, and R-74's census was wrong about it.** The register row said binary expressions and calls are exact. Measured: a binary expression and a call end at the start of their last token (`1 + 2` ends at the `2`, `g()` at the `)`), a literal and an identifier have a point range, and `RangeFrom` ends a node at the start of the next token. The gate report's §1.6 plan to read an end from `Range.End` for "exact" kinds could not work. The mutator takes a node's **start** from the tree, which every node of every corpus seed has at a token start, and the **extent** from the token stream. The one place it still reads an end is a call with no arguments, which ends at its `)`; `MutationRangeTests` fails if that changes. R-74's row is corrected in this commit.
- **Q. Operator 6 reads names from the parser's declarations, not the checked AST.** The gate said "from the checked AST's declarations". Running the checker needs a catalogue per mutant and a replacement need not be in scope or of the same type, since a mismatch is a diagnostic the oracle checks. The callee `print` is an identifier, so it is swapped too.
- **R. Operator 7 makes the array and anonymous-struct forms only.** The map form needs `map<string, T>` and `T` is the type the mutator cannot know without the checker.
- **S. Operator 12 reads the receiver's type from its declaration's own syntax**, an annotation or a literal initialiser, and leaves a receiver of any other shape alone.
- **T. The CI slice's mutant sample is about 540, not 3,000, chosen by hash, with two draws per seed and operator.**
  - Why smaller: a mutant costs a few milliseconds and a bounded or regex-heavy one up to a second, and the triage of what a bigger sample finds is a pull request's worth of register rows. Measured at 3,000 the sample found 52 findings in about 25 signatures.
  - Why a hash: a seed's mutants must not depend on any other seed, or adding a fixture would renumber the sample. A program is in when `FNV-1a(campaignSeed|pick|programId)` mod 32 is 0, and mod 4 for `const-edge` and `nullable-receiver`, which have candidates in only 57 and 68 seeds.
  - The campaign seed `20261123` is `CorpusSlice.CampaignSeed`.
- **U. Triage of the slice's mutants.** Fifteen findings, each minimised and compared with the register: R-60 (six programs, four signatures by kind), R-63 (one), R-66 (eight, by kind), R-69 (one) and R-71 (one), plus R-76 (one), which is new. R-75 was found with the larger probe and has no known entry in the current sample. R-67's row gained a sentence: a property read on a function value takes the same path as a member call.
- **V. New register rows R-75 and R-76**, filed unowned with CLI-measured output. R-75: a value that cannot be indexed is accepted and stops the VM (`GetIndex on receiver of kind Int`, `Bool`, `Float`, `Struct`). R-76: a member read on a value of unknown type that lacks the member stops the VM (`GetProperty: guid has no member`, reached by routing a value through an anonymous struct). The landing entry adds both to the register's changelog.
- **W. The determinism test reruns every program that made a finding and every tenth other, not a whole second pass.** A whole pass doubled the slice's time to show the same thing. The fixed-sample property is checked separately by rebuilding the program list.
- **X. `grob-torture corpus` runs the mutants too**, so it judges the same programs as the slice. `Cli.Run` has an internal `programs` seam, and its tests run the seeds alone.
- **Y. The minimiser:** a complement-only line ddmin, then passes that reuse the operators that make a program shorter (delete a statement, edit an argument, replace a value, remove an annotation, remove a guard) and accept only a strictly shorter result. Slip detection is the two directions disagreeing on the set of syntax node kinds, or the original still failing once the reproducer's lines are removed. It does not re-run the original with a named construct removed, as the gate report §4.6 also describes; the line-removal check is the automatable part.
- **Z. A raw string literal in a test project becomes a seed.** My own `MutatorTests` held one, so it became a seed with a known finding, and editing it would have made the entry stale. The test builds its source from ordinary strings. Anyone adding a raw literal to a test that the oracle finds a defect in meets the same staleness: re-triage.

## Error codes and registry state

None allocated. The count is 122. The register gained R-75 and R-76; R-64 and R-72 are still open.

## Tried and rejected

- Reading a node's end from `Range.End` for binary expressions and calls: not exact (decision P).
- Choosing the mutants' seeds by position in the corpus: renumbers the sample whenever a seed is added (decision T).
- One hash rate for every operator: `const-edge` missed the sample entirely at one in 32.
- A whole second pass for the determinism test (decision W).
- A VM bound of 100 ms for mutants: saves about a second, but a slow runner could then tip a program from making a finding to being bounded, which makes a known entry stale.
- Everything from the parent's hand-off: a short watchdog without a warm-up, a platform branch for the fake current directory, guards in `AstPath`, changing `Environment.CurrentDirectory` in a test, a `for` that updates its variable in its body, a default throwing arm in `InternalErrorReport.Words`, a public `TheoryData<PipelinePhase, …>`, an inner REPL handler beside the outer one.
- Python edit scripts written with a heredoc lose their backslashes; use the Write tool.
- IDE diagnostics about missing references in `tooling/Grob.DriftCheck` appear throughout; the build and the suite are clean.

## Quarantined or skipped tests

None.

## Why this session stopped

Groups 3 and 4, the work of session C, are done and pushed. Session D's row is the generator, which needs Opus, and a session may not cover groups from two rows.

## Next session, first action

Run the prompt's session-start check for session D (change group 5, the generator, Opus, high), reading this file and both draft PRs.

## Open for the maintainer

- **Stacked PRs.** This branch's PR has `feat/crash-oracle` as its base. When #268 merges, **do not delete its branch** before the child is retargeted to `main` and rebased; deleting it closes the child, as happened to #263. Tell me when you are about to merge #268 and I will retarget first.
- The CI slice's runtime in CI is not measured. Check the Torture test step in the checks of both PRs; the local figure is 8.8 s for the project.
- R-64 and R-72 stay open until session E's landing entry. R-74's row was corrected here, and R-67 extended.
- The decision log has no entry yet: D-451 authorises the work, and the landing entry in session E takes its D-number from the live tail of the merged log.
- Carried from earlier hand-offs:
  - the benchmark workflow for #261 is still to dispatch;
  - `.grob-session/handoffs/feat-stdlib-regex-replace-fn.md` on `main` is unretired;
  - the fake-capability test covers `env`, `path.resolve`, the clock and the streams until 9C (gate report §4.2);
  - Dependabot will drop the final newline from its lockfiles again on its next bump.

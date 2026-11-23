# Session hand-off — 23 November 2026

## Where we are

- Branch: `feat/crash-oracle`, HEAD `c161f0a` before this commit
- Working tree: clean (scratch in gitignored `.grob-session/`)
- Commits on this branch: 4 (`git log --oneline origin/main..HEAD`): three hand-offs and group 1
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Picked up from: this branch's own previous hand-off, which this file overwrites. No other path is pending retirement
- Phase reached: **session B, change group 1 committed (`c161f0a`).** Group 2, the CI slice, is next in this same session. The gate report is at `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree

## Green state

- Tests: full `dotnet test Grob.slnx` green at `c161f0a`, 5,855 passed (5,776 before, plus 79 in `Grob.Torture.Tests`)
- Coverage: `Grob.Torture`'s hand-written code is 99.6% line and 93.1% branch. Regex-generated code under `obj/` is excluded from that figure. The one partial branch left is the post-compile `HasErrors` check in `OracleRun`, which is unreachable because the compiler adds no diagnostics today
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged

Chris's Q1 to Q5 rulings from the previous hand-off still stand. Their text is in the gate report's §0. All of them belong in the landing entry. Session B's plan was approved on 23 November with four more departures from the gate report. Each one also belongs in the landing entry:

- **A. The first stack frame is not part of a signature.** Tiered JIT inlining changes the frames between early and late programs in one process, and between Debug and Release builds. A known entry could then match locally and fail in CI. The frame is kept on `Finding.FirstFrame` for triage. This departs from the gate report's §4.7 recommendation.
- **B. Four files are linked from `Grob.Integration.Tests` in group 2, not three:** `SampleExtractor`, `SampleBlock`, `ErrorExampleMaster` and `DeferredWorkRegister`.
  - `SampleExtractor` needs `ErrorExampleMaster.SplitLines`.
  - `DeferredWorkRegister` is reused for the known-list row check.
  - `InfoString` is not needed.
  - The linked files keep their test-project namespace. That is a namespaces-follow-folders exception, to be surfaced in the PR.
- **C. The findings record store moves to group 4.** The gate report's `findings/<sig>/<id>.json` is written with CreateNew. Its first users are the minimiser and the campaign, so it moves to group 4 under the YAGNI rule. §4's "two programs leave two records" test moves with it.
- **D. A runtime error must carry a registered code.** This is an added check. The oracle reports an unregistered runtime code as an `Unregistered` finding.

## Error codes and registry state

None allocated. The count is 122.

## Tried and rejected

- **A 250 ms watchdog in the timeout test without a warm-up run.** A cold catalogue build plus JIT outlasted it, and the finding came out as `Catalogue|Timeout`. The test now runs one warm program first. The CI slice needs the same warm-up pass (gate report §4.4).
- **A platform branch for the fake current directory** (`Z:\…` against `/…`). It left one branch uncoverable on any single platform. `Path.GetFullPath("/grob-torture")` is used instead.
- **A visited set and a null-constructor guard in `AstPath`.** The path search follows only constructor-parameter properties, so the tree has no cycles. The guards were removed as unreachable.

## Quarantined or skipped tests

None.

## Why this session stopped

It has not stopped. This hand-off is the per-group checkpoint the prompt requires after group 1.

## Next session, first action

If this session ends before group 2 is committed, run the prompt's session-start check for session B and build group 2. It covers the seed corpus, `known-findings.json`, the slice tests and register rows R-68 onward for F-1 to F-7. Recheck the R- tail on `origin/main` before pushing.

## Open for the maintainer

- `ErrorStmt` was not reached by the sentinel walker in tests. A top-level `try … finally … catch` source did not produce one where the walker visits. It is a two-line override, noted rather than chased.
- Carried from the previous hand-offs:
  - the benchmark workflow for #261 is still to dispatch;
  - `.grob-session/handoffs/feat-stdlib-regex-replace-fn.md` on `main` is still unretired;
  - the fake-capability test covers only `env`, `path.resolve`, the clock and the streams until 9C (gate report §4.2).

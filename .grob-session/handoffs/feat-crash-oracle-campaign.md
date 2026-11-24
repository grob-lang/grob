# Session hand-off — 24 November 2026

## Where we are

- Branch: `feat/crash-oracle-campaign`, cut from `feat/crash-oracle-generator` at `3dd32fb` and rebased onto `b722150` after #268's review fix `9a9ab28`. A **stacked branch** four deep: its PR's base is `feat/crash-oracle-generator` (PR #270), whose base is `feat/crash-oracle-mutator` (PR #269), whose base is `feat/crash-oracle` (PR #268)
- Working tree: clean (scratch, campaign output and probes are in gitignored `.grob-session/`)
- Commits on this branch beyond its parent: `8a18a90` group 6, the first campaign and triage; `b71b3c3` group 7, the record; `9d3a520`, the local review's fixes; then this hand-off
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451). Landing entry: **D-453**
- Gate report: `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree
- Picked up from: `.grob-session/handoffs/feat-crash-oracle-generator.md`, retired here by `git rm` in `8a18a90`. It stays on PR #270's branch until that merges, and the two older hand-offs stay on #268 and #269 likewise
- Phase reached: **session E complete; the increment is complete.** All seven change groups are committed across the four PRs

## Green state

- Tests: full `dotnet test Grob.slnx` green after the rebase onto `9a9ab28`. Torture 476, Integration 994, Compiler 2,805, Vm 473, Stdlib 672, Runtime 378, Core 345, Consistency 79, BenchCheck 76. `dotnet format Grob.slnx --verify-no-changes` clean.
- Coverage on the lines this branch adds under `tooling/Grob.Torture`: 1,010 of 1,014 lines, 99.6%, and 408 of 424 branches, 96.2%, from the Torture project's cobertura report against `3dd32fb`. The process bindings, `ProcessWorkerLauncher` and `ProcessGrobRunner`, are excluded with justifications.
- CI slice, measured here: 1,318 seeds, 536 mutants and 200 generated programs in 5.9 s.
- Error-code count unchanged at 122; the D-316 gate is green.
- TDD: group 6's 64 tests were written against stubs and seen failing first; the review fixes' tests were seen failing first, one of them as the very hang it guards against. One coverage test (`Write_MutantItsSeedNoLongerGives_Throws`) and two store tests passed at once: they guard code that already existed.
- Mutation-verify: removing the worker's `minimise` journal write, predicted to fail exactly `Run_TheJournal_NamesTheProgramAndPhaseThenTheMinimisingThenIsCleared`; that one test failed, as predicted. Restored.
- Local CodeRabbit, `--base origin/feat/crash-oracle-generator`: twelve findings, all fixed in `9d3a520`. A 200-program campaign through real worker processes gave the same record set before and after the fixes.

## The campaign

Seed `20261124`, 200,000 programs, four workers, at `3dd32fb`'s pipeline: 58 minutes of workers, 9 of report and black-box check. 10,174 findings under 86 signatures; R-77 to R-94 filed. Output in `.grob-session/torture/20261124/`, local scratch, with `report.md`. D-453 has the figures.

## Decisions taken this session

All recorded in D-453: Chris lifted §6's twenty-signature STOP for this session, approved the fourth stacked PR and the slice's 200 generated programs. The divergences (no `--resume` flag, exit `75` for a restart, the 25 ms minimising bound, the uneven ratio the corpus forced) are in its Decision 6.

## Error codes and registry state

None allocated. The count is 122. The register: R-64 and R-72 closed by D-453; R-77 to R-94 added; R-57, R-60, R-63, R-69, R-71, R-75 and R-76 extended.

## Quarantined or skipped tests

None.

## Not done, and why

- **The slice's time in CI.** CI, SonarCloud and CodeQL run only on pull requests into `main`, so PRs #269, #270 and this one have never run them. Their first CI, Sonar and CodeQL results come when each is retargeted to `main`.
- **The canonical benchmark run.** The stability loop drives `RunCommand`, which group 3 changed. Run locally it fails on `main` and on the branch alike (447,872 and 409,456 bytes against a 190,456-byte baseline), so the local run says nothing. The canonical job is Chris's to dispatch. No baseline updated.

## Next action

Chris merges in order: #268, #269, #270, then this PR. Before each merge, retarget the child PR to `main` and rebase it, and do not delete a parent's branch first, or the child closes (as #263 did). Each PR, once its base is `main`, gets CI and SonarCloud for the first time: read the new-code issues and coverage then, before it merges.

CodeRabbit reviewed #268, and its six findings are fixed in `9a9ab28`. The stack is rebased onto that fix and force-pushed. #269's CodeRabbit review was requested and has not been triaged yet: run `/review-pr-comments 269` once it is posted, then rebase #270 and this branch onto any fix commit it adds.

## Open for the maintainer

- **Triage priority across R-68 to R-94.** R-77 (a switch expression's slot; it prints wrong answers silently), R-79 (a finally a throw runs) and R-78 (`??` as an operand) account for most of the campaign's findings and are the cheapest wins for the slice's known list. R-69's `replace("", …)` alone is 927 programs.
- **R-87 and R-83 are spec questions**: whether a ternary unifies `nil` with `T`, and whether a typed function must return on every path.
- Carried: the #261 benchmark dispatch, the unretired `feat-stdlib-regex-replace-fn.md` on `main`, the fake-capability hand-on to 9C (D-453) and the Dependabot newline.

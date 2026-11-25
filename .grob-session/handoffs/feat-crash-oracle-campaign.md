# Session hand-off — 24 November 2026

## Where we are

- Branch: `feat/crash-oracle-campaign`, cut from `feat/crash-oracle-generator` at `3dd32fb` and rebased onto `b722150` after #268's review fix `9a9ab28`. #268, #269 and #270 have merged (`c119923`, `bdb1cc7`, `c245dbf`), and PR #271's base is now `main`. The branch took #270's head `ed90e1e` by merge, then `main` with `-s ours`, since `main`'s tree equals `ed90e1e`'s
- Working tree: clean (scratch, campaign output and probes are in gitignored `.grob-session/`)
- Commits on this branch beyond its parent: `8a18a90` group 6, the first campaign and triage; `b71b3c3` group 7, the record; `9d3a520`, the local review's fixes; then this hand-off
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451). Landing entry: **D-453**
- Gate report: `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree
- Picked up from: `.grob-session/handoffs/feat-crash-oracle-generator.md`, retired here by `git rm` in `8a18a90`. It reached `main` with #270 and leaves it when this PR merges
- Phase reached: **session E complete; the increment is complete.** All seven change groups are committed across the four PRs

## Green state

- Tests: full `dotnet test Grob.slnx` green after the merge with `main` at `c245dbf`, which brought #269's and #270's review fixes here: the CI slice still passes against the known list with #269's changed mutants. After the review fixes, Torture 507, Integration 995, Compiler 2,805, Vm 473, Stdlib 672, Runtime 378, Core 345, Consistency 79, BenchCheck 76. `dotnet format Grob.slnx --verify-no-changes` clean.
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

- **The canonical benchmark run.** The stability loop drives `RunCommand`, which group 3 changed. Run locally it fails on `main` and on the branch alike (447,872 and 409,456 bytes against a 190,456-byte baseline), so the local run says nothing. The canonical job is Chris's to dispatch. No baseline updated.

## Next action

This PR is the last of the four. Its first CI, SonarCloud and CodeRabbit pass, on `a04dbe3`, gave one Sonar issue and eight CodeRabbit findings, all fixed in `ab6cba6` and the review-fix commit after it. One was Chris's to rule on: the black-box check runs reproducers unisolated, so `BlackBoxTests` now pins the CLI's namespaces and named types and R-95, owned by 9C, records the isolation `fs` writes need (D-453). Next: read the bots' pass on the fix commit, then Chris merges. The stack's review fixes after the rebase came down by merge, not rebase: #269's in `53ac2d6` and `fc83574`, #270's in `ed90e1e`. `fc83574` brought this branch's atomic `FindingStore.Add` down to #269, since `Triager` calls it there, so this branch adds only `All` and its tests on top.

## Open for the maintainer

- **Triage priority across R-68 to R-94.** R-77 (a switch expression's slot; it prints wrong answers silently), R-79 (a finally a throw runs) and R-78 (`??` as an operand) account for most of the campaign's findings and are the cheapest wins for the slice's known list. R-69's `replace("", …)` alone is 927 programs.
- **R-87 and R-83 are spec questions**: whether a ternary unifies `nil` with `T`, and whether a typed function must return on every path.
- Carried: the #261 benchmark dispatch, the unretired `feat-stdlib-regex-replace-fn.md` on `main`, the fake-capability hand-on to 9C (D-453) and the Dependabot newline.

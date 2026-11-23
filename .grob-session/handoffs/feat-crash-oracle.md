# Session hand-off — 23 November 2026

## Where we are

- Branch: `feat/crash-oracle`, cut from `main` at `bd59e5f` to carry this hand-off. It holds no work yet. The branch name is provisional: confirm it, or rename it, under the prompt's session-start check
- Working tree: clean (scratch in gitignored `.grob-session/`)
- Commits on this branch: this hand-off only (`git log --oneline origin/main..HEAD`)
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Picked up from: `.grob-session/handoffs/feat-native-named-arguments.md`, retired in this commit
- Phase reached: the native named arguments increment is **merged** (PR #261, squash `bd59e5f`, landing entry D-452). The crash oracle increment, the next work, has not started

## Green state

- Tests: `dotnet test Grob.slnx` on `b7856b3`, whose tree is identical to `main` at `bd59e5f`: 5,776 passed, 0 failed, 0 build warnings
- Coverage: SonarCloud new-code coverage on #261 was 99.1%, with 0 new issues and 0.0% duplication. Whole-solution figure not run this session
- Pre-push gate: passed on the last #261 push. Runs again on this hand-off's push

## Decisions taken, not yet logged

None. Everything from #261 is in D-452, including the two rulings Chris gave this session (the E0003 sentence, and R-22 closing with R-20 and R-55).

## Error codes and registry state

None allocated. Count 122.

## Tried and rejected

- An earlier version of the `AssertEmits` constant-pool check read pool indices back out of the rendered text, so a string constant such as `"[999] "` was taken for an index. Indices now come from the decoded operands (`b7856b3`).
- An expected line array for every `ChunkRendering` characterisation test: CodeRabbit asked for it, I pushed back and CodeRabbit accepted. The one line decision bound-argument emission makes, the `IntToFloat` widening, has its own `GetLine` test.

## Quarantined or skipped tests

None.

## Why this session stopped

The increment merged. Chris is clearing the session to start the next one.

## Next session, first action

Run the session-start check in `docs/prompts/crash-oracle.md` (its model and effort row for session A) on this branch before reading further.

## Open for the maintainer

- **The benchmark workflow** for #261 on the canonical runner is still to dispatch. The local figures are in D-452.
- **`.grob-session/handoffs/feat-stdlib-regex-replace-fn.md`** (20 November) is still on `main`. Its work merged as D-443 and nothing has retired it. Retire it in a later hand-off commit, or tell me it should stay.
- **The crash oracle prompt** expects no hand-off file in session A. This one carries only the merge state, so the prompt's order of work stands.
- New register rows from D-452 for later increments: R-65 (owned by `fs`), and R-66 and R-67 (unowned). The crash oracle's campaign may meet R-66 and R-67 as known signatures.

# Session hand-off — 15 November 2026

## Where we are
- Branch: `docs/corpus-sweep`, cut from `main` at `c351fd1`
- Working tree: clean apart from gitignored scratch in `.grob-session/sweep/`
- Commits on this branch: this hand-off only
- Increment prompt: `docs/prompts/corpus-sweep.md`
- Phase reached: session A complete. The §1 investigation gate has run and been reported, and no edits have been made to the tree

## Green state
- Tests: `dotnet test tests/Grob.Integration.Tests --filter FullyQualifiedName~ErrorExample` gave 239 passed, on a scratch copy of `c351fd1`, both before and after the §1.5 transform. The full suite was not run this session
- Coverage: not run this session
- Pre-push gate: runs on this push

## Decisions taken, not yet logged
- **The host-year residual in `date.parse` is accepted as documented** (Chris, 15 November 2026). Given a free-form partial date with no year, such as `"Jul 1"`, the one-argument `date.parse` still takes the host's year. D-437 states this and `src/Grob.Stdlib/DateParse.cs:27-28` records it. It stays as it is: no code change, no rejection of input without a year and no register row. This closes the question D-437 left open. It becomes a line in this increment's landing entry, group 5, crediting D-437.

Six questions wait on Chris (below); their answers become lines in the landing entry.

## Error codes and registry state
None allocated. Count 120.

## Tried and rejected
None. The gate report records the one stale assumption: the 39 `.claude/` files in R-05 were counted at `f7acb83`'s parent, not at `f7acb83`.

## Quarantined or skipped tests
None.

## Why this session stopped
Session A's scope ends with the gate. No STOP condition fired.

## Next session, first action
Read the gate report at `D:\Code\grob-lang\gate-reports\corpus-sweep-gate-report.md` and Chris's answers to its §7 questions, then start session B with change group 1.

## Open for the maintainer
Q1 to Q6 in §7 of the gate report: row 9's widening, the renumbering's note and *Reason* prose, `float.md`'s `fragment` marker against the unchanged-tallies deliverable, the four unnamed living files, row 23's residual and the bare generic-type rendering. Three register rows are proposed in §6 of the report.

Scratch tools for sessions B and D, all in `.grob-session/sweep/`: `scope.py`, `commas.py` (row 9's second query), `fragments.py` and `renumber.py` (the group 4 transform, with `--extended` for Q2).

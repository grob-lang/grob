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

- **Chris's rulings on the gate report's §7 questions** (15 November 2026). Each becomes a line in the landing entry:
  - **Q1, row 9 widened.** Sweep every multi-line Category B list missing its trailing comma that the second query finds in scope (about 29 sites, listed in the report's §3 row 9), not only D-417's seven. Authority: §3.5 as D-421 widened it. The `grob-sample-scripts.md` sites stay out (D-421 and D-422 leave them to `grob fmt` in Sprint 12).
  - **Q2, the renumbering's prose.** Group 4's transform is `renumber.py --extended`. It also shifts the five `note` lines that cite a source line and the fourteen quarantine *Reason* cells, in the same commit. `json-parse-error`'s JSON-input line stays.
  - **Q3, `float.md`.** Follow D-437. When lines 37-38 go, drop the *Literals* block's `fragment` marker and record the tally move in the entry: 45 run to 46, 39 fragments to 38. This departs from the prompt's unchanged-tallies deliverable, and the entry says so.
  - **Q4, the unnamed living files.** Correct `docs/dev/coverage-policy.md:34`, the script count. Record `SECURITY.md` and the two `.github/ISSUE_TEMPLATE/` files as checked and clean.
  - **Q5, row 23.** Group 3's residual is the sentence in root `CLAUDE.md`'s *Model policy* saying that each increment prompt's session table names the models. The entry credits `c5fb5e1` (#241) for de-versioning the policy.
  - **Q6, bare generic types.** A **new register row**, separate from R-23: diagnostics render generic types bare, so E0001 says `'map'` to `'map'` and E0004 says `'array'` to `'array'`. Owner `unowned`. Done when E0001 and E0004 name the full type (`map<string, int>`, `int[]`) and a test pins each.
- **Register rows to open in group 5: four.** Three are proposed in the report's §6 (synthetic large-script generator; map argument against a map parameter; non-unary `sort` key selector), and the fourth is Q6's.

## Error codes and registry state
None allocated. Count 120.

## Tried and rejected
None. The gate report records the one stale assumption: the 39 `.claude/` files in R-05 were counted at `f7acb83`'s parent, not at `f7acb83`.

## Quarantined or skipped tests
None.

## Why this session stopped
Session A's scope ends with the gate. No STOP condition fired.

## Next session, first action
Start session B (Opus 5.5, medium) with change group 1, working from the gate report at `D:\Code\grob-lang\gate-reports\corpus-sweep-gate-report.md` §3 and the rulings above.

## Open for the maintainer
Nothing. Q1 to Q6 are answered above.

Scratch tools for sessions B and D, all in `.grob-session/sweep/`: `scope.py`, `commas.py` (row 9's second query), `fragments.py` and `renumber.py` (the group 4 transform, with `--extended` for Q2).

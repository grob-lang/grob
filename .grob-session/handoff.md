# Session hand-off — 15 November 2026

## Where we are
- Branch: `docs/corpus-sweep`, cut from `main` at `c351fd1`; group 1 at `e9fea06`
- Working tree: clean apart from gitignored scratch in `.grob-session/`
- Commits on this branch: 4 (three earlier hand-offs and `e9fea06`, change group 1)
- Increment prompt: `docs/prompts/corpus-sweep.md`
- Phase reached: session B. **Change group 1 complete** (`e9fea06`). Change group 2 is next
  and has not started

## Green state
- Tests: `dotnet test Grob.slnx` on group 1's tree gave 5,109 passed, 0 failed (Core 320,
  BenchCheck 76, Consistency 55, Runtime 348, Stdlib 611, Compiler 2,344, Vm 473,
  Integration 882)
- Coverage: not run this session (no code changed)
- Pre-push gate: runs on this push

## Decisions taken, not yet logged
All of these become lines in the landing entry in group 5. None is in the log yet.

- **The host-year residual in `date.parse` is accepted as documented** (Chris, 15 November
  2026). The one-argument form still takes the host's year for a partial date with no year.
  No code change, no rejection and no register row. Credit D-437; see
  `src/Grob.Stdlib/DateParse.cs:27-28`.
- **Chris's rulings on the gate report's §7 questions** (15 November 2026):
  - **Q1.** Row 9 is widened to every multi-line Category B list that the second query
    finds in scope without its trailing comma (about 29 sites, gate report §3 row 9).
    Authority: §3.5 as D-421 widened it. The `grob-sample-scripts.md` sites stay out.
  - **Q2.** Group 4 runs `renumber.py --extended`, which also shifts the five `note` lines
    and the fourteen quarantine *Reason* cells. `json-parse-error`'s JSON-input line stays.
  - **Q3.** `float.md`: follow D-437. Drop the *Literals* block's `fragment` marker once
    lines 37-38 go, and record the tally move: 45 run becomes 46 and 39 fragments become
    38. The entry says this departs from the prompt's unchanged-tallies deliverable.
  - **Q4.** Corrected `docs/dev/coverage-policy.md:34` (done in group 1). `SECURITY.md` and
    the two `.github/ISSUE_TEMPLATE/` files were checked and are clean.
  - **Q5.** Group 3's residual is the root `CLAUDE.md` *Model policy* sentence saying that
    each increment prompt's session table names the models. Credit `c5fb5e1` (#241).
  - **Q6.** A new register row, separate from R-23: E0001 and E0004 render generic types
    bare (`'map'` to `'map'`). Owner `unowned`. Done when both name the full type and a
    test pins each.
- **Group 5 opens four register rows:** the three in the gate report's §6 and Q6's.
- **Group 1's outcome, for the entry** (this session):
  - Rows 3 and 20 are swept. §14 rows 12 and 13 are gone, and a sentence names R-08's and
    R-34's scripts.
  - **One site the gate did not list:** `grob-benchmarking-strategy.md` §7.1's fixture
    tree said "… (eleven more)" after two named scripts, which totals thirteen. It now
    reads "nine more".
  - The second query re-run after the commit returns only changelogs, the dated principal
    review and benchmarking line 301's deliberate historical sentence.
  - `README.md:57` also lost an Oxford comma flagged by the prose hook, in the same commit.

## Error codes and registry state
None allocated. Count 120.

## Tried and rejected
None.

## Quarantined or skipped tests
None.

## Why this session stopped
It has not stopped. This is the checkpoint the prompt requires after each change group.

## Next session, first action
Start change group 2 from the gate report at
`D:\Code\grob-lang\gate-reports\corpus-sweep-gate-report.md` §3, covering rows 2 (residual),
9 (widened per Q1, except `SKILL.md`), 14, 15, 19, 24 to 27 and 30.

## Open for the maintainer
Nothing.

Scratch tools for sessions B and D are in `.grob-session/sweep/`: `scope.py`, `commas.py`
(row 9's second query), `fragments.py` and `renumber.py` (group 4, `--extended` for Q2).

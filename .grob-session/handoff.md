# Session hand-off — 15 November 2026

## Where we are

- Branch: `docs/corpus-sweep`, cut from `main` at `c351fd1`
- Working tree: clean apart from gitignored scratch in `.grob-session/`
- Commits on this branch: group 1 at `e9fea06` and group 2 at `6fab31b`, plus hand-offs
- Increment prompt: `docs/prompts/corpus-sweep.md`
- Phase reached: **session B complete.** Change groups 1 and 2 are committed. Group 3
  (`.claude/`, R-05) is next and belongs to session C

## Green state

- Tests: `dotnet test Grob.slnx` on group 2's tree gave 5,109 passed, 0 failed
- Coverage: not run this session (no code changed)
- Pre-push gate: runs on this push

## Decisions taken, not yet logged

All become lines in the landing entry in group 5. None is in the log yet.

- **Carried from session A** (Chris, 15 November 2026):
  - `date.parse`'s host-year residual is accepted as documented. No code change, no
    register row. Credit D-437.
  - Q1: row 9 is widened to every in-scope multi-line Category B list. **Done in group 2:
    29 sites.**
  - Q2: group 4 runs `renumber.py --extended`, which also shifts five `note` lines and
    fourteen *Reason* cells.
  - Q3: `float.md` follows D-437. **Done in group 2.** The marker is dropped, and the
    public samples tallies move by one block from fragment to run (D-437's 45 run and 39
    fragments become 46 and 38). The entry says this departs from the prompt's
    unchanged-tallies deliverable.
  - Q4: `coverage-policy.md:34` was corrected in group 1. `SECURITY.md` and the two
    `.github/ISSUE_TEMPLATE/` files are clean.
  - Q5: group 3's residual is root `CLAUDE.md`'s sentence that the increment prompt's
    session table names the models. Credit `c5fb5e1` (#241).
  - Q6: a new register row for E0001 and E0004 rendering generic types bare. Owner
    `unowned`.
- **Group 5 opens four register rows**: gate report §6's three and Q6's. Group 2's
  `grob-type-registry.md` map note says "tracked in the Deferred Work Register" and
  depends on §6 row 2 landing.
- **Group 1 outcome**: rows 3 and 20 swept. One site the gate missed: benchmarking §7.1's
  fixture tree said "eleven more", now "nine more". The prose hook also removed an Oxford
  comma at `README.md:57`.
- **Group 2 outcome**: rows 2, 9, 14, 15, 19 and 24 to 27 swept, plus row 30's
  documentation sites. Departures from the gate report:
  - Row 19 also moved stdlib-reference's `HH:mm` line onto `t`, since it rendered 14:30
    from a midnight value.
  - Row 26 changed 77 literals on 73 lines; the gate counted lines.
  - Row 30's `Functions.md` comparator became `items.sort(i => i.size, descending: true)`.
    No v1 member takes a two-parameter lambda, so the "Multi-parameter" label went.
  - `Control-Flow.md`'s string case became `case 500`.
- **Mutation 4 is done** (prompt §4). The prediction was that only
  `PublicSampleCorpusTests.Block_PassesItsClass("README.md:22 (Grob)")` would fail, on
  E2005. It did, with the message `writes E2005 ('\R')`, and the change was reverted.
  Mutations 1 to 3 belong to group 4 and mutation 5 to after group 5.

## Error codes and registry state

None allocated. Count 120.

## Tried and rejected

- `1_000.25` as a float literal example in `Types.md`. Dropped unverified, because it is not
  clear the lexer takes underscores in a float.

## Quarantined or skipped tests

None.

## Why this session stopped

Session B's row ends with group 2. A session may not cover groups from two rows.

## Next session, first action

Start session C (Opus 5.5, medium) with change group 3, working from the gate report at
`D:\Code\grob-lang\gate-reports\corpus-sweep-gate-report.md` (rows 21 to 23, R-05's full
read, row 9's `SKILL.md:100` and the three `.claude/` script counts).

## Open for the maintainer

Nothing.

Scratch tools are in `.grob-session/sweep/`: `scope.py`, `commas.py`, `addcommas.py`,
`paths.py`, `runnable_paths.py`, `fragments.py` and `renumber.py`.

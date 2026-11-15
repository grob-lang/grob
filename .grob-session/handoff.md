# Session hand-off — 15 November 2026

## Where we are

- Branch: `docs/corpus-sweep`, cut from `main` at `c351fd1`; PR #245
- Working tree: clean apart from gitignored scratch in `.grob-session/`
- Commits on this branch: group 1 at `e9fea06`, group 2 at `6fab31b`, group 3 at
  `de37b8a`, plus hand-offs
- Increment prompt: `docs/prompts/corpus-sweep.md`
- Phase reached: **session C complete.** Change groups 1 to 3 are committed. Groups 4
  and 5 are next and belong to session D

## Green state

- Tests: `dotnet test Grob.slnx` on group 3's tree gave 5,109 passed, 0 failed
- Coverage: not run this session (no code changed)
- Pre-push gate: runs on this push

## Decisions taken, not yet logged

All become lines in the landing entry in group 5. None is in the log yet.

- **Carried from sessions A and B**: unchanged; see the previous hand-off in
  `git show 48e8e89:.grob-session/handoff.md`. In short: D-437 credit for `date.parse`'s
  host-year residual; Q1 (29 sites, done in group 2); Q2 (group 4 runs
  `renumber.py --extended`); Q3 (`float.md` follows D-437, tallies 46 run and 38
  fragments); Q4 (`coverage-policy.md:34` only); Q5 (credit `c5fb5e1`); Q6 (new register
  row, owner `unowned`). Group 5 opens four register rows: gate report §6's three and Q6's.
- **Group 3 outcome** (`de37b8a`, 26 files): rows 21, 22 and 23 swept, row 9's
  `SKILL.md:100` and the three `.claude/` script counts done, and R-05's full read
  complete: every file in `.claude/` (28), the four `CLAUDE.md` files and `AGENTS.md`.
  R-05 can close with the entry's D-number. The commit body lists every class of
  correction. Ones the entry should name:
  - `src/CLAUDE.md`'s emission rule cited D-315 (`break`/`continue` in `select`); it now
    says no entry states the rule alone and D-354 applies it. Flagged in memory since
    Sprint 4D, never fixed.
  - `sonar-pr-review` read a `sonar-project.properties` that never existed; it now reads
    `/k:` and `/o:` from `sonarcloud.yml`, and falls back to anonymous calls (verified:
    HTTP 200 on PR 243).
  - `tdd-cycle`'s examples used a lexer API that never existed (`new Lexer(s).ScanAll()`)
    and invented codes (E0042 to E0044). Rewritten against `Lexer.Scan` and E2002, E2004
    and E2005, and run in a throwaway test (8 passed, then deleted).
  - `emitting-closures-and-upvalues` said upvalues close only on return; the compiler
    closes them at every scope-exit pop (`EmitScopeCleanup`, D-115).
  - `sprint-plan` and `propose-change` told authors to split `test` and `feat` commits,
    against the rule that `feat` carries its tests.
  - Root `CLAUDE.md` said there is no Claude reviewer subagent (`grob-reviewer` exists),
    that a Fable-pinned agent exists (none does) and that Sonar is not a NuGet analyzer
    (`SonarAnalyzer.CSharp` is a local gate since PR #104).

## Error codes and registry state

None allocated. Count 120.

## Tried and rejected

- Nothing this session.

## Quarantined or skipped tests

None.

## Why this session stopped

Session C's row ends with group 3. A session may not cover groups from two rows.

## Next session, first action

Start session D (Opus 5.5, medium) with change group 4: run
`.grob-session/sweep/renumber.py --extended` on the real tree per the gate report §5.

## Open for the maintainer

Found in group 3, outside its scope. Each is a register row or a question for group 5,
not a sweep edit:

1. `README.md:45` says "Sprint 5 (Functions and Closures) is active". Group 2's row 24
   covered lines 17 and 18 only.
2. `docs/dev/coverage-policy.md:30-33` says the validation scripts drive `grob fmt`,
   `grob check`, `grob install` and `grob restore`, which do not exist yet. Q4 took only
   line 34.
3. `logging-a-decision` and root `CLAUDE.md` say design-doc deliverables are complete
   files, never diffs, while root `CLAUDE.md` also says files are edited with the Edit
   tool. A claude.ai-era rule; which holds is Chris's call.
4. `/commit-message` says never run `git commit`, while `/close-session` step 4 commits
   through it. Process, not interface; left alone.
5. `sonar-pr-review` and `/review-pr-comments` compare new-code coverage against D-328's
   90% floor, while the SonarCloud PR gate fails under 80%. Both are true; neither file
   says which number fails the gate.

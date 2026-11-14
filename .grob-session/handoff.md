# Session hand-off — 14 November 2026

## Where we are
- Branch: `feat/tests-public-samples-gate`, cut from `main` at `04d2609`
- Working tree: clean apart from this hand-off
- Commits on this branch: this hand-off only
- Increment prompt: `docs/prompts/public-samples-compile.md`
- Phase reached: session A (§1 read-only investigation gate) complete and reported; no source,
  test or document edits made. Change groups 1 to 5 not started.
- Gate report: `D:\Code\grob-lang\gate-reports\public-samples-compile-gate-report.md` (outside the
  working tree). Measurement scripts: `.grob-session/measure_corpus.py`, `classify.py`,
  `corrections.py`, `sites.py` (gitignored scratch).

## Green state
- Tests: 4,856 passed, 0 failed, 0 skipped, on `04d2609` with SDK 10.0.303 (Release build)
- Coverage: not run this session
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged
None taken. The gate report lists rulings Chris must make (below); none is decided. Its revised
tallies (F2: 45 run, 29 excused, 39 fragments, 10 blocked) are a measurement, not a decision, and
belong in the landing entry with the D-430 divergences it records.

## Error codes and registry state
None allocated. Count unchanged at 120.

## Tried and rejected
- `git am --abort` to clear a stale `git am` session left in the repository since 6 November
  (applying the D-428 scoping commit, already on `main` as `7671f5b`): rejected, because its
  `abort-safety` is `f747517` and aborting would reset `main` back to it. Cleared with
  `git am --quit` instead, which leaves HEAD and the index untouched.
- Classifying offset-free versus explicit-offset `date.parse` input with `DateTimeStyles` flags
  alone: no combination does it without reading the host zone (report F5). A lexical classifier is
  needed in group 1.

## Quarantined or skipped tests
None.

## Why this session stopped
Session A's work is complete, and a §6 STOP fired: §1.2 found W6's defect in a `grob` block §2.3
does not list (`docs/wiki/Plugins/Grob-Crypto.md:28-29`). Two §1.3 STOPs fire on a literal
reading (report, STOPs 2 and 3). Session B's row also needs a fresh session.

## Next session, first action
Read the gate report's *Before session B* section and get Chris's rulings on its six questions
before starting change group 1.

## Open for the maintainer
The six rulings in the gate report's *Before session B* section: Grob-Crypto.md into W6 or not;
whether `RunCommand.cs:95` and the test-side capability sites are outside the STOPs; the date for
time-only input with an explicit offset; whether W10 covers the `if (…)`; whether guid.md's table
and `date.md:50` fold into W10 and W8; whether to add a near-miss fence guard.

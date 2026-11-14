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
Chris ruled on the gate report's six questions on 14 November. Each departs from the prompt or
D-430. All go in group 5's landing entry (D-number from the live tail), in its record of where the
work diverged.

1. **W6 widens to `Plugins/Grob-Crypto.md` block 2** (fence line 25; lines 28 and 29): both
   double-quoted paths become backtick raw strings, on D-285. Verdict unchanged (`fragment`); no
   tally moves. Reason: the same defect and authority as W6. It compiles silently to a wrong
   path, which is worse than W6's loud E2005.
2. **STOPs 2 and 3 are set aside.** `RunCommand.cs:95`'s relative script-path read is the host
   interpreting its own command line and is outside R-32. The test-side capability assembly
   sites (`RunCommandSeamTests.cs:19-20`, `StdlibCatalogueTests`, the per-plugin constructors in
   `Grob.Stdlib.Tests`) are inside group 1's licence. The landing entry says so in one sentence
   each.
3. **Time-only `date.parse` input with an explicit offset takes today in the input's own offset**
   (.NET's convention), computed from `clock.UtcNow`, never the host clock. `"14:30+02:00"` at
   22:33Z gives the next day's date in `+02:00`. Time-only input with no offset still takes
   today in `clock.LocalZone` (§2.1). Host-free only by lexical handling (report F5), so group 1
   writes a classifier: a trailing `Z` or `±hh[:]mm` for the ISO form, and a `z`/`K` specifier
   outside quotes for the pattern form.
4. **W10 covers parenthesising the bare `if`**: `if (parsed == nil) {`. The rewritten block must
   be legal code, and "rewritten" is read to include it.
5. **W8 and W10 widen to the stale reference sites on their pages.** W8 also removes
   `Standard-Library/date.md:50`'s `minusDays` row. W10 also rewrites `Standard-Library/guid.md`'s
   API table (lines 10-12) and its notes (48-51) against `grob-stdlib-reference.md`'s `guid`
   module (`newV4`, `newV5` variadic over `guid.namespaces.*`, `newV7`, `parse`, `tryParse`).
6. **A near-miss fence guard is added in group 2.** It fails on any fence in the corpus files
   whose info string looks like a `grob` fence but is not opened canonically. **Proposed rule,
   not yet ruled on**: the first info-string word matches `grob` case-insensitively, not
   followed by a letter (so `grob-fragment` and `grob,x` count; `grobparams` does not), and the
   fence is indented, cased otherwise than `grob`, tilde-fenced or opened with four or more
   backticks. Measured green on arrival (0 near-misses). It gets a unit test per shape and a
   tenth mutation verification (remove the guard; predicted: its own test only).

Measured, not decided, and carried into session C: F2's tallies (45 run, 29 excused, 39 fragments,
10 blocked, five of them fragments), which change mutation 2 to five blocked fragments and mutation
4 to 39 unblocked fragments; F4's corrected drive-relative wording; F7's two-digit register pattern
(widen `DeferredWorkRegister` to `R-\d{2,}` in group 2).

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
Session A's work is complete. A §6 STOP fired (§1.2: W6's defect in `Grob-Crypto.md`, unlisted),
and two §1.3 STOPs fired on a literal reading. Chris resolved all three by rulings 1 and 2.
Session B's row needs a fresh session.

## Next session, first action
Run the prompt's session-start check for session B (Opus 5.5, medium), then start change group 1
under rulings 2 and 3 above.

## Open for the maintainer
Only the near-miss guard's exact matching rule (ruling 6, proposed above). Session B may proceed on
the proposal and surface it in group 2's commit if Chris has not answered.

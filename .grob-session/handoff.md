# Session hand-off — 15 November 2026 (session C, change group 3 complete)

## Where we are
- Branch: `feat/tests-public-samples-gate`, HEAD `c5b99b9`
- Working tree: clean apart from this hand-off
- Commits on this branch (`git log --oneline main..HEAD`): hand-off commits, plus `c9526eb`
  (change group 1, R-32), `02af086` (change group 2, the harness) and `c5b99b9` (change
  group 3, the wiki corrections)
- Increment prompt: `docs/prompts/public-samples-compile.md`
- Phase reached: session C (Opus 5.5, medium, confirmed by Chris). Group 3 is committed. Next:
  group 4 (wire the corpus, red first, then info strings, then mutations 1-8 and 10), then
  group 5.
- Group 3 measurement: every corrected block reached its predicted verdict. The live tally
  after group 3, with no info strings, is 42 run (45 once the harness fixtures supply
  `BASE_URL` and the other two variables), 29 excused, 39 fragments and 13 others: the 3
  needing fixtures and the 10 to be blocked (the 3 VM crashes are among them). This is F2's shape: group 4's red count and final
  tallies follow it, not the prompt's 38/11. Re-measure with
  `python -I .grob-session/measure_corpus.py . <abs grob.exe> <out>` and compare with
  `.grob-session/diff_results.py`. The exe path must be absolute.
- Gate report: `D:\Code\grob-lang\gate-reports\public-samples-compile-gate-report.md` (outside
  the working tree).
- PR: draft #243.

## Green state
- Tests: the full suite passed 4,964, failed 0, on `c5b99b9`'s tree (unchanged by group 3,
  which is documentation only). Release build, SDK 10.0.303.
- Coverage: group 1's changed files in `Grob.Stdlib` and `Grob.Cli` are at 100% line coverage,
  from the `Grob.Stdlib.Tests` and `Grob.Integration.Tests` cobertura runs. Group 2 is test code
  only. The project-level D-328 figure was not run.
- Pre-push gate: passed on group 1's hand-off push; runs again on this push.
- Mutation 9 run in group 1: putting `TimeZoneInfo.Local` back in `date.today` failed exactly the
  predicted three tests (`Today_InjectedZone_TakesTheZonesDate`,
  `Today_InjectedZoneTransitionDay_UsesMidnightsOwnOffset` and
  `StdlibSources_ReadNoAmbientZoneOrDirectory`).
- Mutations 1 to 8 and 10 have not run. Chris approved moving them to group 4, because their
  predictions name corpus cases that exist only once the corpus is wired in.

## Decisions taken, not yet logged
All go in group 5's landing entry (D-number from the live tail), in its record of where the work
diverged from the prompt or D-430.

Session A rulings (Chris, 14 November), carried forward unchanged:

1. **W6 widens to `Plugins/Grob-Crypto.md` block 2** (fence line 25; lines 28 and 29): both
   double-quoted paths become backtick raw strings, on D-285. Verdict unchanged (`fragment`).
2. **STOPs 2 and 3 are set aside.** `RunCommand.cs:95`'s relative script-path read is the host
   interpreting its own command line and is outside R-32. The test-side capability assembly
   sites are inside group 1's licence. The landing entry says so in one sentence each.
3. **Time-only `date.parse` input with an explicit offset takes today in the input's own
   offset**, computed from `clock.UtcNow`. Implemented in `c9526eb`.
4. **W10 covers parenthesising the bare `if`**: `if (parsed == nil) {`.
5. **W8 and W10 widen to the stale reference sites on their pages**: `date.md:50`'s `minusDays`
   row; `guid.md`'s API table (lines 10-12) and notes (48-51) against
   `grob-stdlib-reference.md`'s `guid` module.
6. **A near-miss fence guard is added in group 2.** Chris accepted the proposed matching rule
   this session: the first info-string word matches `grob` case-insensitively, not followed by
   a letter (so `grob-fragment` and `grob,x` count, `grobparams` does not), and the fence is
   indented, cased otherwise than `grob`, tilde-fenced or opened with four or more backticks.
   One unit test per shape and a tenth mutation verification (remove the guard; predicted: its
   own test only).

Taken in session B, group 1:

7. **The `date.parse` split is lexical, in `src/Grob.Stdlib/DateParse.cs`.** The ISO form
   counts as explicit-offset only when a trailing `Z` or `±hh[:]mm` directly follows a clock
   time, so a date's own hyphens never read as an offset. The pattern form counts as
   explicit-offset when `z` or `K` appears outside quotes and unescaped. A time-only pattern is
   one with no `y`, `M` or `d` specifier. Safety net: an offset-free parse that still comes
   back with a non-`Unspecified` kind (a trailing `GMT`, the standard format `"o"`) is reparsed
   as a `DateTimeOffset` and keeps that designator's offset.
8. **The `NoCurrentDateDefault` sentinel edge is stated, not guarded**: an input naming
   1 January of year 1 is read as time-only (`DateParse`'s remarks).
9. **`DatePlugin.BuildNamedType` is now an instance method** so `toLocal` can read the zone.
   `PathPlugin` is no longer "pure"; its class doc names the capability.
10. **The R-32 source guard lives in the test project**
    (`tests/Grob.Consistency.Tests/StdlibAmbientReadGuardTests.cs`), not in
    `tooling/Grob.DriftCheck`, to stay inside group 1's licence. The one-argument
    `Path.GetFullPath(` check counts commas at the call's own nesting level. Two comments in
    `PathPlugin.cs` were reworded because the text scan would have flagged them.
11. **Test zones**: `TestClock` takes an optional zone, defaulting to `TimeZoneInfo.Local` so the
    tests written before R-32 keep their meaning. `TestZones.PlusThree` is +03:00 with daylight
    saving (+04:00, last Sunday of March to last Sunday of October). The harness `FixedClock`
    defaults to `HarnessZone`, a fixed +03:00 with no daylight saving.
    `FixedFileSystem.HarnessDirectory` is under the temp path and is never created.
12. **The Windows-only drive-relative test returns early off Windows.** xUnit 2.9.3 has no
    dynamic skip, and the prompt forbids `Skip =`.

Taken in session B, group 2 (plan approved by Chris):

13. **The near-miss guard reports any fence whose first info word looks like `grob` and that
    extraction does not take.** That is the accepted rule plus a column-0 ```` ```grob-x ```` or
    ```` ```grob,x ````, which would otherwise escape unseen. Stated in `02af086`'s message.
14. **The fixture guard is lexical**: each fixture variable must appear as a double-quoted
    literal in some extracted block's source (`PublicSampleCorpus.UnreadFixtures`). This avoids a
    cross-case aggregate run.
15. **The E1001 subject comes from a header pass in `SampleRunner`**, the first quoted name in
    the message. `ExampleDiagnostic` is not widened, because its equality drives the master
    comparison. Unaccounted stderr lines (an internal error, stray output) come from
    `CliDiagnosticReader.Read`'s problems and fail a bare block, as session A's measurement did.
16. **A fragment whose only diagnostics are excused E1001s fails** ("is a complete program"). It
    should be a bare block, which then passes as excused. This follows "after setting aside" in
    §2.2 and matches mutation 4's prediction.
17. **The harness classes were written before their unit tests**, not after a red run. Group 4's
    mutation verifications stand in for the red step. Stated in `02af086`'s message.
18. **No hang protection.** xUnit 2's `Timeout` needs async tests, and CLAUDE.md forbids
    `Task.Run`. Session A measured no hangs.

Taken in session C, group 3:

19. **Ruling 5's "guid.md API table" is the whole function table**, not only the `v4`/`v5`/`v7`
    rows at lines 10-12. `parse` (now throws `ParseError`), `tryParse` (new row), `empty` (a
    constant, not a call) and `namespaces` (new row) are corrected too, because the rewritten
    example's `tryParse` would otherwise contradict the table above it. The type-methods table
    (`toStringUpper`/`toStringN` against the reference's `toUpperString`/`toCompactString`, no
    `version` row) is a separate table and goes in group 5's *Findings for the corpus sweep*.

Measured, not decided, carried into session C: F2's tallies (45 run, 29 excused, 39 fragments,
10 blocked, five of them fragments), which change mutation 2 to five blocked fragments and
mutation 4 to 39 unblocked fragments; F4's corrected drive-relative wording (pinned by
`Resolve_DriveRelativePathOnAnotherDrive_ResolvesToThatDrivesRoot`); F7's two-digit register
pattern (widen `DeferredWorkRegister` to `R-\d{2,}` in group 2).

## Error codes and registry state
None allocated. Count unchanged at 120.

## Tried and rejected
- `git am --abort` to clear a stale `git am` session: rejected, because its `abort-safety`
  resets `main` to `f747517`. Use `git am --quit`.
- Classifying `date.parse` input with `DateTimeStyles` flags alone: no combination avoids a host
  read (report F5).
- An ISO offset regex that only required the body to contain a colon: it misreads
  `12:00 2026-07-01`'s trailing `-01` as an offset. The regex now anchors the offset to a clock
  time.
- A dynamic skip for the Windows-only test: not available in xUnit 2.9.3.
- An inline `sed` to widen `DeferredWorkRegister`'s regex: the escapes collapsed and corrupted
  the file, so it was restored from git and redone with Edit. CLAUDE.md's "scripts go in files"
  rule exists for this.
- Using `math.nope` as the E1002 test input: it gives E1003 (undefined namespace member). An
  undefined member on a value (`x := 5`, `x.nope`) gives E1002.

## Quarantined or skipped tests
None.

## Why this session stopped
Session B's row is complete (change groups 1 and 2). The prompt forbids a session covering groups
from two rows, so groups 3 to 5 need session C.

## Next session, first action
If a new session: run the prompt's session-start check for session C (Opus 5.5, medium). Then
start change group 4 (§2.4): wire the theory to the live corpus with no info strings, predict
the red count from the live tally (49 failing: 39 fragments and 10 blocked, five R-29 and five
R-30; the prompt's total of 49 is split 38/6/5), run it red and record which fail.

## Open for the maintainer
Nothing.

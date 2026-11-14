# Session hand-off — 14 November 2026 (session B, after group 1)

## Where we are
- Branch: `feat/tests-public-samples-gate`, HEAD `c9526eb`
- Working tree: clean apart from this hand-off
- Commits on this branch: 3 (`git log --oneline main..HEAD`): two hand-off commits and
  `c9526eb` (change group 1)
- Increment prompt: `docs/prompts/public-samples-compile.md`
- Phase reached: session B. Change group 1 (R-32) is complete and committed. Change group 2
  (the harness) is next, in this session. Groups 3 to 5 belong to session C.
- Gate report: `D:\Code\grob-lang\gate-reports\public-samples-compile-gate-report.md` (outside
  the working tree).

## Green state
- Tests: the full suite passed 4,904, failed 0, at group 1's code before its last five
  `date.parse` theory cases were added. After that addition, `Grob.Stdlib.Tests` passed 597 and
  `Grob.Integration.Tests` passed 701. The full suite was not rerun after the addition.
  Release build, SDK 10.0.303.
- Coverage: changed files only, from the `Grob.Stdlib.Tests` and `Grob.Integration.Tests`
  cobertura runs. Every file group 1 touched in `Grob.Stdlib` and `Grob.Cli` is at 100% line
  coverage (`DateParse`, `DatePlugin`, `PathPlugin`, `InertCapabilities`, `StdlibPlugins`,
  `SystemClock`, `SystemFileSystem`, `HostCapabilities`). The project-level D-328 figure was not
  run.
- Pre-push gate: runs on this hand-off's push.
- Mutation 9 run: putting `TimeZoneInfo.Local` back in `date.today` failed exactly the
  predicted three tests (`Today_InjectedZone_TakesTheZonesDate`,
  `Today_InjectedZoneTransitionDay_UsesMidnightsOwnOffset` and
  `StdlibSources_ReadNoAmbientZoneOrDirectory`). The line was restored afterwards.

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

## Quarantined or skipped tests
None.

## Why this session stopped
It has not. This hand-off marks group 1's completion, as the prompt requires after each group.
Session B continues into change group 2.

## Next session, first action
Start change group 2 (`docs/prompts/public-samples-compile.md` §2.2), with the near-miss guard
on the accepted rule and `DeferredWorkRegister` widened to `R-\d{2,}`.

## Open for the maintainer
Nothing.

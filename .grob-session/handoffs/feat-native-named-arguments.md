# Session hand-off — 22 November 2026

## Where we are
- Branch: `feat/native-named-arguments`, HEAD `a2a50be` (group 1)
- Working tree: clean (scratch in gitignored `.grob-session/nna/`)
- Commits on this branch: 10 (`git log --oneline origin/main..HEAD`): hand-off commits, the merge of `main` at `567cf08` (#262 to #265) and group 1
- Increment prompt: `docs/prompts/native-named-arguments.md`
- Gate report: `D:\Code\grob-lang\gate-reports\native-named-arguments-gate-report.md`
- Picked up from: this branch's own hand-off (session A), overwritten here
- Phase reached: session B, change group 1 committed (`a2a50be`). Group 2 is next, in this session if context allows. Chris approved the session B plan for groups 1 and 2 (chat, 22 November); its substance is under *Decisions* below.

## Green state
- Tests: `dotnet test` on `a2a50be`: 5,619 passed, 0 failed
- Coverage: not run this session
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged
All go into the landing entry (group 7). Take its D-number from the live tail.

Carried from session A (Chris's rulings, 21 and 22 November; unchanged):
- **S1:** fixed separately, merged as #262 (D-448). Group 5's refusal on function values is already built by #262 (`RejectNamedFunctionValueArgs`); the landing entry cites D-448 rather than claiming it.
- **S2:** `sort` binds `descending` through the shared binder from a one-entry parameter list in its bespoke arm; every other array method, map methods, `print`, `exit` refuse with E0011. Narrows D-447 Decision 2. Group 6's drift check pins `descending` too.
- **S3:** a member call on an `Unknown` receiver refuses a named argument with E0011 in group 5. Extends D-447 Decision 2.
- **S4:** `date-members.errors.expected.txt` pins no named argument and stays byte-identical; a line in the landing entry.
- **`strings.join`:** fix the doc (`grob-type-registry.md:337` loses `separator`'s default) in group 6, with the stdlib-reference signatures for `date.parse`'s `pattern` and `input`'s `prompt`.
- **Function-value refusal wording:** `A function value does not accept named arguments; pass them positionally.` (built by #262).
- **R-58 native widening folds in** (22 November): the emission helper emits `IntToFloat` for an `int` argument bound to a `float` parameter. The rest of R-58 (bindings, returns, struct fields, parameter defaults, calls through function values) stays on its own `fix/` branch; the landing entry narrows R-58.
- **Crash oracle follows this increment** (22 November): the landing commit also changes `docs/prompts/crash-oracle.md`'s *When it runs* line to "after the native named arguments increment lands, and before the 9C `fs` increment".

New this session (in the approved plan):
- **Widening covers direct script-function calls too.** Every checked script call now emits through `EmitBoundArguments`, so group 2's widening flag (`BoundSlot.WidenToFloat`, set by the checker) makes `half(3)` against `x: float` run. Parameter defaults are not widened (they are on R-58's `fix/` list). The landing entry says so.
- **Every checked script call goes through the record**, not only reordered or default-omitting ones; byte identity for complete positional calls is pinned by `CompilerCallEmissionCharacterisationTests`.
- **E0004 on a named native argument** is numbered by the argument's written position (gate report §1.4), so no pinned message moves.

## Error codes and registry state
None allocated. Live count 122.

## Tried and rejected
- `fn:` as a named argument in a test does not parse (keyword). Do not use it.
- Characterisation expectations were captured from the base with a temporary file-dump hook (removed before commit). Do not hand-edit them: they must stay byte-identical through groups 2 to 4.

## Quarantined or skipped tests
None.

## Why this session stopped
Not stopped: hand-off after group 1, as the prompt requires after each group.

## Next session, first action
Build group 2 per the approved plan (namespace natives and `input` bind on `ArgumentBinder`, `RejectNamedNativeArgs` deleted, R-58 widening in `EmitBoundArguments`, probe-master lines 14 to 22 updated per gate report §1.6), starting with its red tests.

## Open for the maintainer
Nothing.

# Session hand-off — 21 November 2026

## Where we are
- Branch: `feat/native-named-arguments`, HEAD `67b268d` (branched from `main` at D-447)
- Working tree: clean (probe scripts sit in gitignored `.grob-session/nna/`)
- Commits on this branch: 0 before this hand-off
- Increment prompt: `docs/prompts/native-named-arguments.md`
- Picked up from: none
- Phase reached: session A complete. The §1 read-only gate has been run and reported, and no source was edited. Report: `D:\Code\grob-lang\gate-reports\native-named-arguments-gate-report.md`

## Green state
- Tests: `dotnet test` on `67b268d` gave 5,398 passed, 0 failed
- Coverage: not run this session
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged
Chris's rulings on the gate's STOP conditions, 21 November. The group 7 landing entry records them. Take its D-number from the live tail.

- **S1: separate branch.** A call through a function-typed parameter is checked against the enclosing function (`TypeChecker.Declarations.cs:43`). This is fixed on its own `fix/` branch, outside this increment. Groups 1 to 4 do not wait for it; group 5's refusal on function-typed parameters does.
- **S2: `sort` binds `descending`.** The bespoke `sort` arm passes a one-entry parameter list (`fn` required, `descending` defaulted) to the shared binder. Every other array method, every map method, `print`, `exit` and function values refuse with E0011. This narrows D-447 Decision 2 for one callable. Group 6's drift check should pin `descending` as well.
- **S3: refuse.** A member call on an `Unknown` receiver refuses a named argument with E0011 in group 5. This extends D-447 Decision 2 to a fifth kind.
- **`strings.join`: fix the doc.** `grob-type-registry.md:337` loses `separator`'s default; the code is unchanged. This lands in group 6 with the other §1.3 document fixes (adding signatures for `date.parse`'s `pattern` and `input`'s `prompt` to the stdlib reference).

The gate report proposes the binder (§1.4), the emission record (§1.5) and the probe-master line list (§1.6). These are not yet approved; the rulings above change none of them.

## Error codes and registry state
None allocated. The live count is 121.

## Tried and rejected
- `r.replace("abc", fn: m => …)` as a probe for the overload's parameter name: it does not parse (`fn` is a keyword). Do not use `fn:` in tests.
- Calling a function-typed parameter to probe kind 9 is broken on `main` even positionally (S1). Use a lambda held in a binding to exercise function-value calls until S1 is fixed.

## Quarantined or skipped tests
None.

## Why this session stopped
Session A ends at the gate by design. Four STOP conditions fired. Chris has ruled on S1 to S3 and on `strings.join` (above). S4 needs only a line in the landing entry. Session B is unblocked.

## Next session, first action
Run the prompt's session-start check for session B (Opus, high), then build group 1 from the gate report's §1.4 and §1.5, starting with the characterisation emission tests §1.5 lists.

## Open for the maintainer

- The new refusal wording for a callee that has no name: `A function value does not accept named arguments; pass them positionally.` (gate report §1.4). Needed by group 5.
- The S1 fix branch is not yet cut. It must merge before group 5 starts.

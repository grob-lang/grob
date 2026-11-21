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
None. The gate report proposes the binder (§1.4), the emission record (§1.5) and the probe-master line list (§1.6). They are proposals for session B and Chris, not decisions.

## Error codes and registry state
None allocated. The live count is 121.

## Tried and rejected
- `r.replace("abc", fn: m => …)` as a probe for the overload's parameter name: it does not parse (`fn` is a keyword). Do not use `fn:` in tests.
- Calling a function-typed parameter to probe kind 9 is broken on `main` even positionally (S1). Use a lambda held in a binding to exercise function-value calls until S1 is fixed.

## Quarantined or skipped tests
None.

## Why this session stopped
Session A ends at the gate by design. Four STOP conditions fired; S1 to S3 need Chris's rulings before session B can start, and S4 needs only a line in the landing entry. Each is set out under "STOP conditions that fired" in the gate report.

## Next session, first action
Read the gate report's STOP section and Chris's rulings on S1, S2 and S3, then run the prompt's session-start check for session B (Opus, high).

## Open for the maintainer
- **S1:** calls through a function-typed parameter are bound against the enclosing function (`TypeChecker.Declarations.cs:43`). Fix on its own branch first, or fold the fix into group 5?
- **S2:** array `sort`'s documented `descending: bool = false` is named throughout the corpus. Bind it (recommended), refuse it, or leave array methods out of group 5?
- **S3:** member calls on an `Unknown` receiver accept any name and emit in source order. Refuse with E0011 in group 5 (recommended)?
- **`strings.join`:** `grob-type-registry.md:337` documents `separator: string = ""`, but the code has no default. Correct the document (recommended), or add the default in code?
- The new refusal wording for a callee that has no name: `A function value does not accept named arguments; pass them positionally.`

# Session hand-off — 17 November 2026

## Where we are

- Branch: `feat/stdlib-regex-module`, HEAD `8c9fee3` before this hand-off commit. Two review-fix commits for PR #253 follow it (`59e946f` and the map-value fix); the second keeps a map value's named type when it is indexed.
- Working tree: clean apart from gitignored scratch in `.grob-session/`.
- Commits on this branch: six change groups plus this hand-off (`git log --oneline origin/main..HEAD`).
- Increment prompt: none. Authorised by D-432 and `grob-v1-requirements.md` (Sprint 9C, `regex` bullet); plan approved in session (`C:\Users\cjgro\.claude\plans\vectorized-dreaming-kay.md`, local to Chris's machine).
- Picked up from: `.grob-session/handoffs/feat-compiler-slash-rule.md`, retired in this commit.
- Phase reached: **PR 2 of 3 complete.** The `regex` module, `Regex` and `Match`, literal emission and E0602 are built and recorded in **D-440**. PR 3 (the function form of `replace`, R-44) is not started.

## Green state

- Tests: `dotnet test Grob.slnx` observed this session at `8c9fee3`'s tree, all eight projects green (Compiler 2,441 after the three caret tests; Integration 916; Stdlib 662; VM 473; Runtime 348; Core 345; BenchCheck 76; Consistency 55). `dotnet build`: 0 warnings.
- Coverage: new `src/` lines from `git diff origin/main` against the union of every project's Cobertura report: 306 of 306 lines, 109 of 114 branch conditions (95.6%), measured before the three caret tests that cover the remaining branches were added.
- Pre-push gate: runs on this push.
- Benchmarks: not run. The lexer change is diagnostic text only.
- CodeRabbit pre-PR pass: **not run**; the CLI is not installed on this machine.

## Decisions taken, not yet logged

None. Every ruling this session is in **D-440**.

## Error codes and registry state

**E0602 minted** and registered in `ErrorCatalog.cs`, the summary row, the full entry, the footer total and `ErrorCodeCountTests` (count 121). D-316 green. No runtime code added: E5702 and E5903 are reused.

## Tried and rejected

All argued in D-440; do not reopen them without Chris:

- A hidden native-function field on `Regex` holding the compiled .NET object: equality becomes reference identity.
- A bespoke checker arm for the function-form `replace`: a third hand-coded exception to the catalogue model. PR 3 grows `SignatureType` instead.
- Clamping a bad `timeout`: fails silently.

## Quarantined or skipped tests

None added. `TypeCheckerDecoratorTests.PatternArgument_IsNotValidatedAsARegex_Deferred` was replaced by `PatternArgument_InvalidRegex_IsE0602`, since the deferral it pinned has ended.

## Why this session stopped

PR 2's work is complete and pushed; PR 3 is a separate branch.

## Next session, first action

Check whether this branch's PR has merged, then cut PR 3's branch from the updated `main` and plan R-44 (the function form of `replace` and `replaceAll`) in plan mode.

Pointers for that plan, all on disk:

- R-44 in `docs/design/grob-open-questions.md`; D-440 Decision 3; D-432 Decision 6.
- `SignatureType.Concrete` rejects function shapes at `src/Grob.Runtime/SignatureType.cs` (D-426 Decision 1); `NamedTypeDefinition.ValidateMethods` rejects a second member of the same name.
- Lambda parameters are typed `Unknown` in `VisitLambda` (`src/Grob.Compiler/TypeChecker.Expressions.cs`); the array higher-order methods are hand-coded in `ValidateArrayMethodCall`.
- `VmInvoker.Invoke` is how a native calls a Grob function (`src/Grob.Vm/ArrayNatives.cs` is the worked example).

## Open for the maintainer

- **Merge this PR.** This hand-off stays in it and merges to `main`.
- The stdlib reference's `regex.compile(pattern, "i", timeout: 30)` sample uses a named argument, which a native call rejects today (E0011, D-426 Decision 4). It describes the intended surface; say if you want it positional until named-argument binding lands.
- `Grob.Stdlib` gained `InternalsVisibleTo` for `Grob.Stdlib.Tests`, so `RegexCache` stays internal and its once-per-value contract is pinned directly.
- A single-line raw string's range now runs to its closing backtick (parser change, not grammar), so a diagnostic can point inside it. No other test or gold master moved.

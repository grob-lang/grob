# Session hand-off — 22 November 2026

## Where we are

- Branch: `feat/native-named-arguments`, HEAD is the hand-off commit on top of `0dfa925` (`git log -1`)
- Working tree: clean (scratch in gitignored `.grob-session/nna/`, including `newcov.py` and `commit-g3.txt` to `commit-g5.txt`)
- Commits on this branch: 20 with this one (`git log --oneline origin/main..HEAD`). Change groups: 1 at `a2a50be`, 2 at `d401715`, 3 at `4f31cf1`, 4 at `f467d50`, 5 at `5938a67`, then `0dfa925` (two cascade tests for group 3, no source change)
- PR: draft #261. Push onto it and update its description through a REST PATCH; do not open a new PR.
- Increment prompt: `docs/prompts/native-named-arguments.md`
- Gate report: `D:\Code\grob-lang\gate-reports\native-named-arguments-gate-report.md`
- Picked up from: this branch's own hand-off (session B, after the review fixes), overwritten here
- Phase reached: session C complete. Groups 3, 4 and 5 are committed and green. Session D is groups 6 and 7 (Sonnet, medium).

## Green state

- Tests: `dotnet test`, whole solution, on `5938a67`: all eight projects passed, 0 failed, 5,736 (Core 345, Consistency 55, BenchCheck 76, Runtime 378, Compiler 2,789, Vm 473, Stdlib 672, Integration 948). `0dfa925` adds two Compiler tests and the Compiler project alone was run on it: 2,791, 0 failed
- Coverage: new code, the `src/` diff against `origin/main` (groups 1 to 5), Compiler and Integration cobertura unioned by `.grob-session/nna/newcov.py`: 258/258 lines, 171/171 branches, on `0dfa925`. Only `Grob.Compiler` changed under `src/`. Whole-solution figure not run
- Benchmark gate: a local run of every benchmark was started in the background at the end of the session, writing to the session scratchpad (`bench/` and `bench-run.log` under it). If it finished, its result is in the PR description; if not, run it before the PR is marked ready. It is a `workflow_dispatch` job on a canonical runner (`.github/workflows/benchmark.yml`), so `BenchCheck` fails the runner guard locally by design: compare the allocation columns to `bench/Grob.Benchmarks/baseline/compile.json` by eye, report against R-24 and update no baseline
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged

All go into the landing entry (group 7). Take its D-number from the live tail.

Carried from sessions A and B (unchanged; the previous hand-off's list stands, in `git show 49e8f3d:.grob-session/handoffs/feat-native-named-arguments.md`): S1 to S4, the `strings.join` ruling, the function-value wording, R-58's native widening folded in, the crash-oracle line change and session B's five (script-function widening, every checked call through `ResolvedBinding`, E0004 numbered by written position, `input`'s signature from the catalogue, variadics bind for diagnostics only).

New in session C (inside the plan Chris approved on 22 November):

- **`sort` binds for its diagnostics and gets no `ResolvedBinding`.** `descending` can only follow a positional `fn`, so a clean call is already in parameter order and its bytes do not change. The view is `Binding/ArraySortParameters.cs` (`fn` required, `descending` defaulted), on the one shared binder.
- **`sort()`, `sort(descending: true)` and a third argument are now E0003** ("'sort' expects between 1 and 2 arguments, but N were supplied"). They compiled before and faulted the VM or ignored the extra argument. Chris approved folding this in. The landing entry records it as a behaviour change beyond the refusal.
- **The S3 refusal reaches any receiver `DispatchMemberAccessCall` has no surface for**, not only `Unknown` (`bool`, for example). Approved. That path also reports no E1002 for an unknown member, which predates this increment: **a finding for the register** (D-420), with the VM fault on `ns?.padLeft(5)`.
- **`ns?.padLeft(5)` emits different bytes** (default filled: `Constant " "`, `Call 2`) because the guarded call now uses the bound list, which gate report §1.4 requires. `OptionalChainCall_PrimitivePadLeft_EmitsPinnedBytes` was updated for it (the one characterisation pin changed in this session) and `OptionalChainCall_PrimitivePadLeftComplete_EmitsPinnedBytes` pins the complete call, byte-identical to the base form. The landing entry says so.
- **Skip and fill on a primitive method is proven by a hand-built call, not a registered member.** No first-party primitive has two defaults and the registrar is internal, sealed by `InternalsVisibleTo_NamesExactlyTheThreeFirstPartyAssemblies` (D-426 Decision 3), which was left alone. Widening on a primitive method is not tested for the same reason (no primitive has a float parameter); the line is shared with the named-type and namespace paths. Named-type default fill is proven in bytecode on `box`, a type the tests register through the public registrar (`MemberNamedArgumentFixtures`), not at run time: no first-party named-type method has a default, and the VM passes the argument array as given.
- **`RejectNamedMemberArgs` is deleted (group 4) and `RefuseNamedArguments` is new (group 5)**: one refusal, with the function-value check calling it too. `MemberArgCountMatches`, now array and map only, refuses first and counts after, so a refusal never has an arity error beside it. `MemberArgCountInRange` is folded into it and `EmitMemberArityError` is the one E0003 wording for a member call.
- **Group 7's fixture header** in `named-arguments.grob` is still stale (it says binding "stays unsupported"). Rewrite it without moving a line.

## Error codes and registry state

None allocated. The live count is 122.

## Tried and rejected

- `fn:` as a named argument in a test: it does not parse, because `fn` is a keyword. So `xs.filter(fn: …)` and `sort(fn: …)` cannot be written, and sort's key can never hit E0009 from source. The refusal tests name the lambda `p:`.
- Adding `Grob.Compiler.Tests` to `Grob.Runtime`'s `InternalsVisibleTo`, to register a two-default primitive method in a test: a guard test pins the grantees to the three first-party assemblies. Reverted; the hand-built call replaced it.
- Mutation checks that leave the diagnostic call in place and drop only the early `return` are weak: they fail only the tests that assert suppression. Both of this session's first attempts did that and the failure counts did not match the predictions. The group 3 one stands as run (predicted wrongly, recorded in its commit body); the group 5 one was redone properly and matched (12 checker tests and two integration cases).
- Do not hand-edit the characterisation expectations except the one named above. They were captured from the base.

## Quarantined or skipped tests

None.

## Why this session stopped

Session C's rows are done. Groups 6 and 7 belong to session D (Sonnet, medium), and the prompt forbids one session covering groups from two rows.

## Next session, first action

Run the prompt's session-start check for session D, then read `git show 4f31cf1 f467d50 5938a67 --stat --format=%B` before building group 6's drift check, which must pin `descending` on `sort` as well as the seven signature defaults.

## Open for the maintainer

Nothing blocking. For awareness:

- The pre-push CodeRabbit review (`coderabbit review --agent --deep --fresh --base main -c .coderabbit.yaml`) was not yet run on groups 3 to 5 when this was written; see the PR for its outcome.
- The E0003 wording finding from session B (a missing required parameter reads as an arity range, not by name) is still open for the register. Groups 3 and 4 inherited it on member calls: `s.padLeft(char: "*")` reads "expects between 1 and 2 arguments, but 1 was supplied". One change to every path fixes it.

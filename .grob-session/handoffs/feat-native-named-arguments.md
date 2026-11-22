# Session hand-off — 22 November 2026

## Where we are

- Branch: `feat/native-named-arguments`, HEAD `b44a6c7` (fixes from the pre-push CodeRabbit review, after group 2)
- Working tree: clean (scratch in gitignored `.grob-session/nna/`, including `newcov.py`, the new-code coverage script)
- Commits on this branch: 14 (`git log --oneline origin/main..HEAD`). Change groups: 1 at `a2a50be`, 2 at `d401715`, review fixes at `b44a6c7`
- PR: draft #261. Push onto it and update its description through a REST PATCH; do not open a new PR.
- Increment prompt: `docs/prompts/native-named-arguments.md`
- Gate report: `D:\Code\grob-lang\gate-reports\native-named-arguments-gate-report.md`
- Picked up from: this branch's own hand-off (session B, after group 1), overwritten here
- Phase reached: session B complete. Groups 1 and 2 are committed and green. Groups 3, 4 and 5 are session C (Sonnet, high).

## Green state

- Tests: `dotnet test` on `b44a6c7`: all eight projects passed, 0 failed (5,659 on `d401715` plus one new test) (the validation scripts run inside `Grob.Compiler.Tests`)
- Coverage: new code in groups 1 and 2 (the `src/` diff against `origin/main`, Compiler and Integration cobertura unioned): 149/149 lines, 87/87 branches. Whole-solution figure not run this session
- Benchmark gate: not run this session (a §5 deliverable; run it before the PR)
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged

All go into the landing entry (group 7). Take its D-number from the live tail.

Carried from session A (Chris's rulings of 21 and 22 November, unchanged):

- **S1:** fixed separately and merged as #262 (D-448). #262 already builds group 5's refusal on function values (`RejectNamedFunctionValueArgs`), so the landing entry cites D-448 rather than claiming it.
- **S2:** `sort` binds `descending` through the shared binder, from a one-entry parameter list in its bespoke arm. Every other array method, every map method, `print` and `exit` refuse with E0011. This narrows D-447 Decision 2. Group 6's drift check pins `descending` as well.
- **S3:** a member call on an `Unknown` receiver refuses a named argument with E0011 in group 5. This extends D-447 Decision 2.
- **S4:** `date-members.errors.expected.txt` pins no named argument and stays byte-identical. The landing entry records it in one line.
- **`strings.join`:** fix the document, not the code. `grob-type-registry.md:337` loses `separator`'s default in group 6, alongside the stdlib-reference signatures for `date.parse`'s `pattern` and `input`'s `prompt`.
- **Function-value refusal wording:** `A function value does not accept named arguments; pass them positionally.`, built by #262.
- **R-58 native widening folds in:** the emission helper widens an `int` argument bound to a `float` parameter. The rest of R-58 (bindings, returns, struct fields, parameter defaults, calls through function values) stays on its own `fix/` branch, and the landing entry narrows R-58 to those.
- **The crash oracle follows this increment:** the landing commit changes `docs/prompts/crash-oracle.md`'s *When it runs* line to "after the native named arguments increment lands, and before the 9C `fs` increment".

New in session B (inside the plan Chris approved on 22 November; the group 2 commit body records each one):

- **Widening covers direct script-function calls too.** It is built in group 2 for namespace natives and script functions. Default slots are never widened.
- **Every checked call goes through `ResolvedBinding`:** script functions, non-variadic namespace natives and `input`, complete positional calls included. Byte identity is pinned by `CompilerCallEmissionCharacterisationTests`.
- **E0004 on a named native argument** is numbered by the argument's written position.
- **`input`'s signature comes from the catalogue.** A catalogue with no `input` built-in throws `GrobInternalException` as a host misconfiguration, which `InputCall_CatalogueWithoutInput_IsAHostMisconfiguration` tests.
- **A variadic native binds only for its diagnostics.** It gets no record and keeps positional emission.

**A finding for the register** (D-420; group 7 files it with the landing entry): when the bound set leaves a required parameter unbound, E0003 counts the supplied arguments against the allowed range. `date.parse(pattern: "dd/MM/yyyy")` therefore reads "expects between 1 and 2 arguments, but 1 was supplied", and `value` is not named. Script functions say the same (`f(b: 1)` against `f(a, b = 1)`). The pre-push CodeRabbit verify run raised it on the native path only. It was not changed here: script-function wording is out of scope, and D-446's single convention rules out changing the native message alone. The fix is one change to both paths.

## Local CodeRabbit review (pre-push, whole branch)

- First run: 2 findings, both fixed in `b44a6c7`: widening recorded on the call's first line, and missing line and column in two negative tests.
- Verify run: 1 finding, the E0003 wording above, recorded rather than fixed.

## Error codes and registry state

None allocated. The live count is 122.

## Tried and rejected

- `fn:` as a named argument in a test: it does not parse, because `fn` is a keyword.
- Do not hand-edit the characterisation expectations. They were captured from the base and must stay byte-identical through groups 3 and 4.

## Quarantined or skipped tests

None.

## Why this session stopped

Session B's rows are done. Groups 3 to 5 belong to session C (Sonnet, high), and the prompt forbids one session covering groups from two rows.

## Next session, first action

Run the prompt's session-start check for session C. Then read the group 1 and group 2 commit bodies (`git show a2a50be d401715 --stat --format=%B`) before building group 3 on `ArgumentBinder`, `TypeChecker.BoundArguments` and `Compiler.EmitBoundArguments`.

## Open for the maintainer

Nothing blocking. For session C's awareness, from the gate report:

- A non-nil nullable primitive `?.` call faults the VM (`ns?.padLeft(5)`). Group 3 can prove its nullable-receiver case in bytecode only.
- `EmitOptionalChainCall` emits arguments in source order and must call `EmitBoundArguments` in its non-nil branch (gate report §1.4).

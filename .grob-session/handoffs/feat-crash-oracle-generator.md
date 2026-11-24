# Session hand-off — 24 November 2026

## Where we are

- Branch: `feat/crash-oracle-generator`, cut from `feat/crash-oracle-mutator` at `d9e517d`. A **stacked branch** three deep: its PR's base is `feat/crash-oracle-mutator` (PR #269), whose base is `feat/crash-oracle` (PR #268)
- Working tree: clean (scratch is in gitignored `.grob-session/`)
- Commits on this branch beyond its parent: `84d901c`, change group 5, the generator; `7db0eb7`, the pre-push review's fixes; then this hand-off. See `git log --oneline origin/feat/crash-oracle-mutator..HEAD`
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Gate report: `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree
- Picked up from: `.grob-session/handoffs/feat-crash-oracle-mutator.md`, retired here by `git rm` in `84d901c`. It stays on the parent branch, in PR #269, until that merges. No other path is pending retirement
- Phase reached: **session D complete.** Group 5 is done. Groups 6 and 7 (session E) have not started

## Green state

- Tests: full `dotnet test Grob.slnx` green after `84d901c`. Torture 380, Integration 990, Compiler 2,805, Vm 473, Stdlib 672, Runtime 378, Core 345, Consistency 79, BenchCheck 76. `dotnet format Grob.slnx --verify-no-changes` clean. After `7db0eb7`, which touched only the generator and its tests, the Torture project alone: 381 green, format clean.
- Coverage on `tooling/Grob.Torture/Generation/`, all new: 770 of 780 lines, 98.7%, and 403 of 418 branches, 96.4%, from the Torture test project's cobertura report.
- Torture test project: 10 s locally, against 8.8 s before; the generator's tests add about 1.5 s. Not measured in CI.
- Error-code count unchanged at 122; the D-316 gate is green.
- Pre-push gate: runs on this hand-off's push.
- CodeRabbit, local, `--base feat/crash-oracle-mutator`: five minor findings on `84d901c`. Four fixed in `7db0eb7`: the typed-catch window never reached its last position (and the switch's string-arm window had the same off-by-one), and three census predicates matched shapes they did not name. One pushed back: the termination test does not fail on findings, since they are the campaign's to triage (D1); the test now says so.
- **TDD:** the type-check, census, determinism, depth, termination and `GenType` tests were written first and seen failing (74 red). The unit tests for the member table, the scope, the random source and the writer were written after the code; they found two real bugs (`Weighted` with all-zero weights did not throw; `GenNative` compared parameter lists by reference).
- Mutation-verify, each predicted before it ran:
  - A, struct bindings no longer always annotated: exactly one failure, `Sample_EveryProgram_TypeChecksAndCompilesWithoutErrors`, with E0001 `Cannot assign value of type 'unknown' to field …`. I predicted the test and the E0001 family; the slot was a field where I predicted a binding.
  - B, the `while` counter's `++` removed: exactly one failure, `Sample_EveryProgram_EndsWithoutReachingItsBound`, listing the bounded programs.

## Decisions taken, not yet logged

Every item belongs in the landing entry as a divergence or a record (prompt §5). Items A to Z are in the parent's hand-off, carried on PR #269's branch; the landing entry needs them too.

- **AA. Plan decisions D1 to D3, approved by Chris on 24 November.** D1: generated programs stay out of the CI slice in group 5; group 6 sets the ratio and what the slice carries. D2: the generator reads primitive members from the catalogue, plus a hand list of seven namespace functions whose signatures it also reads from the catalogue (`MemberTable.NamespaceFunctions`). D3: no `generate` CLI subcommand; group 6's `campaign` consumes the generator.
- **AB. The nesting bound has a definition.** The gate report's "depth bound 8 or less" now counts every statement, declaration and expression a node sits inside, except `BlockStmt`, `ExpressionStmt`, `GroupingExpr` and the helper records (arguments, arms, fields). Statements nest to depth 4 (`StatementGenerator.MaxStatementDepth`) and the rest is kept for expressions. `GeneratedProgramsTests.NestingDepth` measures it; the sample's deepest program sits between 6 and 8.
- **AC. Lambda parameters are never put in scope.** In every position the generator writes a lambda, the checker types its parameters `unknown` and refuses an `unknown` in a typed slot (measured; R-47 is the cause). Half of expression lambdas instead have a body over the first parameter, `p op literal` or `p` alone, the R-61 shape.
- **AD. `filter` and `select` results are read only through `.length`, or as a `for` whose element the body does not read.** Their elements are `unknown` to the checker.
- **AE. The generator works round further checker gaps, listed below as triage candidates.** Ternaries and switch expressions are written for scalars only; struct bindings are always annotated; array elements are scalars or structs; anonymous struct fields are scalars; array fields are built but not read back.
- **AF. `const` operands are small literals.** A large operand reaches R-68's folding crash, which is the mutator's `const-edge` operator's to find.
- **AG. A name budget of 100 per program** (`StatementGenerator.NameBudget`). The top level's global names share its constant pool with its literals under a one-byte operand; at 160 one program of 3,000 passed 255 and stopped the compiler. The budget is a proxy, measured to leave every program of the 3,000 inside the limit.
- **AH. No named function is passed as a function value.** `apply(twice, 4)` with `g: fn(int): int` is E0004, `'unknown' is not assignable to parameter 'g' of type 'fn'`.

## Triage candidates for session E

Not register rows yet: filing is group 6's (prompt §2). Each was measured through `grob run` or the checker on this branch.

- **G-1. Arrays of an annotated and an inferred `int` are judged different.** `a: int := 5`, `b := 0`, then `n > 0 ? [b] : [a]` is E0002, `array element types do not match`, and so is a switch with those arms; `[[v26, 2], [2, v25]]` with `v26: int := v25` is E0001, `must share a nested element type`. A refusal of a well-typed program.
- **G-2. `false ? nil : 0.0` into `float?` is E0001**, `Ternary arms must produce the same type; found 'nil' and 'float'`, as is an array literal mixing `nil` and a value. A spec question first: whether a ternary unifies `nil` with `T` to `T?`.
- **G-3. A struct copied by `b := a` loses its type's name.** `b.f` is `unknown` and `[b]` is not a `T0[]`.
- **G-4. A struct out of a switch expression loses its type's name** the same way.
- **G-5. Arrays read from a struct field or an anonymous struct field iterate with `unknown` elements**, and `filter` and `select` results do too.
- **G-6. AH above**: a named function as a function-typed argument is refused.
- **G-7. More than 255 top-level constants stop the compiler** with `GrobInternalException: Bytecode operand overflow` in the compile phase, an oracle finding with no diagnostic.
- **G-8. Oracle findings from 3,000 generated programs** (campaign seed `20261124`, indexes 300 to 3,299, at `7db0eb7`): 148 programs with findings, **25 distinct signatures**, 1,447 clean, 1,405 with a Grob runtime error, none bounded, and none failing to type-check. The list is in `.grob-session/gen/wide-3000-report.txt`, local scratch; regenerate any program with `ProgramGenerator.Generate(20261124, index)`. Minimised examples:
  - `v9 := math.pow(("xyz" switch { "grob" => 2.25, "xyz" => 10.0, _ => 0.0 }), 1)` stops with `Call target is not a function (kind: Float)`.
  - `v20 := 1` then `an21 := #{ m0: (v20 switch { 0 => 1.5, _ => 2.25 }), m1: 1 }` stops with `kind mismatch: expected String, actual Float`.
  - A `try` that throws with a `finally` holding `for e in ["a", ("k0" ?? "grob")] {}` stops with `GetProperty: struct has no field`.
  - `xs.select(v => v.toString())` then a read of an element stops with `GetProperty 'toString' on receiver of kind Int` (near R-67 and R-76).
  - Index 1562 stops the **compiler** with `InvalidOperationException: Collection was modified`. The minimiser barely shrank it.
  - An `ArgumentException: The value cannot be an empty string` from a native, 20 programs, likely an empty separator.
  - Several of these centre on a switch expression in an argument or field position.

## Error codes and registry state

None allocated. The count is 122. The register is unchanged on this branch.

## Tried and rejected

- Putting lambda parameters in scope with their annotated types: the checker types them `unknown` (AC).
- `filter` and `select` producing typed arrays: their elements are `unknown` (AD).
- Ternaries and switches over arrays, maps, structs and nullables: G-1 to G-4.
- Passing named functions as function values: AH.
- A name budget of 160: one program in 3,000 passed the operand limit (AG).
- Constant operands from `IntLiteral`, which draws `9223372036854775807`: R-68 crashed the type-check test (AF).
- Python edits through a heredoc wrote CRLF on Windows; `dotnet format` caught it. Use `open(p, 'w', newline='')` or the Edit tool.

## Quarantined or skipped tests

None.

## Why this session stopped

Group 5, session D's work, is done and committed. Session E's row covers groups 6 and 7, and a session may not cover groups from two rows.

## Next session, first action

Run the prompt's session-start check for session E (change groups 6 and 7, Opus, high), reading this file, the parent hand-offs on PRs #268 and #269 and all three draft PRs.

## Open for the maintainer

- **The campaign is likely to meet prompt §6's STOP.** 3,000 generated programs alone gave 25 distinct signatures (G-8), past the stop at twenty new ones, before any are matched to known rows. Session E will report the list and wait unless you set the triage scope beforehand.
- **Three stacked PRs.** Merge order #268, #269, then this one. Do not delete a parent's branch before its child is retargeted and rebased, or the child closes (as #263 did). Tell me before each merge.
- G-2 needs a spec answer before it is a defect.
- Carried from the parent hand-offs: the CI slice's runtime in CI is unmeasured; R-64 and R-72 close in the landing entry; the decisions log has no entry yet; the #261 benchmark dispatch, the unretired `feat-stdlib-regex-replace-fn.md` on `main`, the fake-capability scope until 9C and the Dependabot newline all stand.

# Session hand-off — 20 November 2026

## Where we are

- Branch: `feat/stdlib-regex-replace-fn`, HEAD `8f20a3f` before this hand-off commit.
- Working tree: clean apart from gitignored scratch in `.grob-session/`.
- Commits on this branch: seven plus this hand-off (`git log --oneline origin/main..HEAD`).
- Increment prompt: none. Authorised by D-432 Decision 6 and R-44; plan approved in session (`C:\Users\cjgro\.claude\plans\snappy-sleeping-owl.md`, local to Chris's machine).
- Picked up from: `.grob-session/handoffs/feat-stdlib-regex-module.md`, retired in `078ca30`.
- Phase reached: **PR 3 of 3 complete.** The function form of `Regex.replace` and `replaceAll` is built and recorded in **D-443**; R-44 is closed. The `regex` increment is finished once this merges.

## Green state

- Tests: `dotnet test Grob.slnx` observed this session after the stdlib group, all eight projects green (Compiler 2,463; Integration 924; Stdlib 672; VM 473; Runtime 377; Core 345; BenchCheck 76; Consistency 55). The compiler suite grew to 2,464 with the last commit, whose test file was run on its own (17 pass). `dotnet build`: 0 warnings.
- Coverage: new `src/` lines from `git diff origin/main` against the union of every project's Cobertura report: 190 of 190 lines, 124 of 128 branch conditions (96.9%), measured before the last commit, which covers three of the four remaining branches. The one left is `ShapeAt` meeting two distinct shapes at one position, which needs a contrived overload set.
- Pre-push gate: passed on the push of `8f20a3f`.
- Benchmarks: not run. No hot path changed; the VM is untouched.
- CodeRabbit pre-PR pass: **not run**; the CLI is not installed on this machine.

## Decisions taken, not yet logged

None. Every ruling this session is in **D-443**.

## Error codes and registry state

None allocated. Every new diagnostic folds into E0004; the run-time non-string result reuses E5903. Count unchanged at 121.

## Tried and rejected

All argued in D-443; do not reopen them without Chris:

- One binding per overload, with the checker choosing an index for the compiler to emit: the VM binds a method by name before its arguments exist, so this needs a mangled name or a new operand.
- Overloading by arity or by concrete type: nothing needs it, and the ambiguity rule would grow.
- A new error code for a function argument that does not fit its shape.

## Quarantined or skipped tests

None. Two integration tests were worded around the pre-existing bugs below; `ReplaceFunction_ThrowInsideTheFunction_Propagates` asserts the uncaught path on purpose and says why.

## Why this session stopped

The increment is complete and pushed; merging is Chris's action.

## Next session, first action

Check whether this branch's PR has merged, then cut a `fix/` branch from the updated `main` for the first of the three pre-existing bugs below.

## Open for the maintainer

- **Merge this PR.** This hand-off stays in it and merges to `main`.
- Three pre-existing bugs found and left to their own `fix/` branches (Chris, this session):
  - **Compiler:** string `+` with a parenthesised `??` operand. `s: string? := nil` then `print((s ?? "") + ":")` faults the VM (kind mismatch, expected Int); `print("a" + (s ?? "b"))` throws `InvalidOperationException` from `Compiler.GetArithmeticOpCode`. `int` and `float` work, as does binding the `??` result first.
  - **VM:** a `throw` inside a lambda that a native calls back into, caught by an enclosing `try`, prints the caught message and then fails with "value stack underflow". Array `select` reproduces it.
  - **Compiler:** an `int` returned from a function declared `float` is not coerced. `fn g(): float { return 1 }` then `print(g() / 2)` faults the VM (kind mismatch, expected Float); so does `f: fn(): float := () => 1`. Since this PR, a block lambda ending in `return 1` bound to `fn(): float` type-checks and faults the same way (before, it was wrongly rejected with E0001). `IntToFloat` exists, so named fns need only emission at the return; lambdas need typing from their binding's annotation, which is a design decision to log.
- **R-47** is open and `unowned`: a lambda parameter's type annotation is ignored.
- A top-level `fn` reference is typed `Unknown`, so a named function passed for a shape is not checked against it (recorded in D-443, no register row). Say if you want one.

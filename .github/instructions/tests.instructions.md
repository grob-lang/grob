---
name: 'Grob Tests'
description: 'Testing conventions across the five xUnit projects and the integration/error-example fixtures.'
applyTo: 'tests/**/*.cs'
---

# Grob test conventions

Tests are not optional and are never added retroactively. Every change ships with
tests for the behaviour it adds. These rules sit on top of the C# host-code rules.

## The five projects and what each owns

| Project                  | Tests                                                        | Style       |
|--------------------------|-------------------------------------------------------------|-------------|
| `Grob.Core.Tests`        | Value representation, chunk construction, opcode encoding   | Unit        |
| `Grob.Compiler.Tests`    | **Source → assert emitted bytecode.** Highest-priority surface | Unit     |
| `Grob.Vm.Tests`          | Hand-constructed `Chunk` → assert execution result          | Unit        |
| `Grob.Stdlib.Tests`      | Register a plugin into a VM instance → assert function output | Unit      |
| `Grob.Integration.Tests` | `.grob` source → assert stdout, stderr and exit code        | Integration |

Put a test in the project that owns the layer under test. A type-checker bug is a
`Grob.Compiler.Tests` case, not an integration test. Reach for integration tests to
prove the whole pipeline works end-to-end, not to debug a single pass.

## Compiler tests are where the bugs are

The SharpBASIC retrospective established this: bugs live in the compiler, not the VM
loop. Given source text, assert the exact bytecode emitted — opcodes, operands,
constant-pool contents, and the line-number array. Test typed-opcode selection
explicitly: `2 + 3` emits `AddInt`, `2.0 + 3.0` emits `AddFloat`, `"a" + "b"` emits
`Concat`. A regression here is a silent miscompilation, so these tests are dense and
specific.

## Invariants that must have guard tests

- **LSP-enabling fields.** After type checking, every identifier node carries a
  non-null `ResolvedType` and a non-null `Declaration`. Assert this — it stops the
  day-one LSP invariant from silently regressing.
- **Two-mode errors.** A source file with three independent type errors produces three
  diagnostics, not one. The checker never stops at the first.
- **Parser recovery (D-300).** Cover: a malformed expression mid-function still yields
  a complete AST for the surrounding well-formed code with an `ErrorExpr` at the
  failure site; a malformed declaration still lets subsequent declarations parse; a
  multi-error file yields one diagnostic per root cause with no cascade.

## Error-example gold masters

The error-examples library pairs each case: one `*_grob.txt` source and one
`*_expected.txt` gold-master diagnostic. The expected file is the exact stderr output,
byte for byte — file, line, column, message, suggestion. When a diagnostic's wording
changes, regenerate and review every affected `_expected.txt` deliberately; never
hand-edit one to make a test pass. The skill `writing-an-error-test` walks the
procedure.

## Conventions

- xUnit. `[Theory]` with `[InlineData]` for table-driven cases — common given Grob's
  type-rule matrices.
- Test names state the behaviour: `AddsTwoInts_EmitsAddIntOpcode`, not `Test1`.
- Integration-test fixtures use Windows paths in backticks, like real Grob scripts.
- Assert on exit codes as well as output — Grob is a Unix-style tool where exit codes
  carry meaning.
- No flaky timing assertions in the unit suites. Performance lives in
  `bench/Grob.Benchmarks`, gated separately.

# Grob test conventions

These rules apply to `tests/**/*.cs`. Tests are not optional and are never added
retroactively. Every change ships with tests for the behaviour it adds. These rules
sit on top of the C# host-code rules in `src/CLAUDE.md`.

## The five projects and what each owns

| Project                  | Tests                                                          | Style       |
| ------------------------ | -------------------------------------------------------------- | ----------- |
| `Grob.Core.Tests`        | Value representation, chunk construction, opcode encoding      | Unit        |
| `Grob.Compiler.Tests`    | **Source → assert emitted bytecode.** Highest-priority surface | Unit        |
| `Grob.Vm.Tests`          | Hand-constructed `Chunk` → assert execution result             | Unit        |
| `Grob.Stdlib.Tests`      | Register a plugin into a VM instance → assert function output  | Unit        |
| `Grob.Integration.Tests` | `.grob` source → assert stdout, stderr and exit code           | Integration |

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

## Diagnostic assertions

Every test that exercises error output must assert the full diagnostic contract —
not just that _a_ diagnostic exists:

```csharp
Diagnostic diag = Assert.Single(diagnostics.Errors);
Assert.Equal("E2002", diag.Code);          // exact registered code
Assert.Equal(1, diag.Range.Start.Line);    // 1-based line
Assert.Equal(2, diag.Range.Start.Column);  // 1-based column — exact value, not > 1
```

`Assert.NotEmpty(diagnostics.Errors)` alone is never sufficient. Use equality
assertions for column, not inequalities (`>= 1`, `> 0`) — those hide off-by-one
regressions in source-location tracking.

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
hand-edit one to make a test pass. The `writing-an-error-test` skill walks the
procedure.

## Conventions

- xUnit. `[Theory]` with `[InlineData]` for table-driven cases — common given Grob's
  type-rule matrices. Only consolidate into a Theory when all cases share the
  **same assertion shape** (identical number of assertions at fixed indices). If
  expected token-stream length varies per case, or per-case index-based lexeme
  spot-checks differ, keep separate `[Fact]`s — a Theory that obscures intent is
  worse than the duplication.
- Test method names use the shape `Subject_BehaviourClause` — two PascalCase
  segments joined by a single underscore. Match the convention in
  `tests/Grob.Core.Tests/TokenTests.cs`.
- Test names state the behaviour: `PlainString_SegmentsIntoStartPartEnd`, not
  `Plain_string_segments_into_start_part_end` and not `PlainStringSegmentsIntoStartPartEnd`.
- Plain xUnit assertions (`Assert.Equal`, `Assert.True`, `Assert.Single`, …) with a
  descriptive message on the assertions that need one. This is the project
  convention across every test project — `FluentAssertions` and `FsCheck` are
  deliberately not used (not in `Directory.Packages.props`, no `.Should()` anywhere)
  and must not be introduced. Reviewers and tools should not suggest them.
- Integration-test fixtures use Windows paths in backticks, like real Grob scripts.
- Assert on exit codes as well as output — Grob is a Unix-style tool where exit codes
  carry meaning.
- No flaky timing assertions in the unit suites. No `Thread.Sleep`. Performance lives
  in `bench/Grob.Benchmarks`, gated separately.

---
name: tdd-cycle
description: The strict red/green/refactor TDD procedure for Grob, with xUnit specifics. Use when starting a new test, when about to write production code, when finishing a cycle or when uncertain whether you should be writing a test or implementation next.
---

# TDD cycle for Grob

The cycle is non-negotiable. Red, green, refactor, in that order, every
time. The named exceptions are listed at the end of this skill — take them
explicitly, not silently.

A feature is not done after one green cycle. The cycle below covers a
single behaviour; a feature is done when happy path, failure path, and
edge cases are all green and the project is still at or above 90% line
coverage. See `Step 5: Extend coverage` and `Step 6: Coverage check`
below.

## The full procedure

### Step 1: Red

Write a single test that exercises one specific behaviour. The behaviour
should be observable through the public API of the type under test — or
through the internal surface when `InternalsVisibleTo` makes the type
reachable from the test assembly. Visibility itself follows the
internal-by-default rule below.

```csharp
public sealed class LexerTests {
    [Fact]
    public void Scan_EmptyInput_ReturnsEofTokenOnly() {
        // Arrange
        DiagnosticBag bag = new();

        // Act
        IReadOnlyList<Token> tokens = Lexer.Scan("", bag);

        // Assert
        Token token = Assert.Single(tokens);
        Assert.Equal(TokenKind.Eof, token.Kind);
        Assert.Empty(bag.Errors);
    }
}
```

The example is illustrative: the lexer is long built, and its real tests are in
`tests/Grob.Compiler.Tests/`. The shape — one behaviour, the real entry point
(`Lexer.Scan(source, diagnostics)`), explicit types, same-line braces — is the point.

Run it: `dotnet test --filter "FullyQualifiedName~LexerTests.Scan_EmptyInput_ReturnsEofTokenOnly"`.

**Watch it fail.** The failure should be one of:

- **Compilation error.** The type or member doesn't exist yet. Good — that's
  a clear red.
- **Assertion failure.** The implementation produces the wrong answer. Good.
- **Unexpected exception.** The implementation crashed. Acceptable if the
  message points at what's missing.

**The failure should not be:**

- **Test passes on the first run.** The behaviour already exists, or the test
  doesn't actually exercise what its name claims. Investigate before
  continuing.
- **Test errors out for an unrelated reason.** Setup is wrong, dependency
  injection is broken, build is failing. Fix the noise before declaring red.

### Step 2: Green

Write the _minimum_ code that makes the test pass. Nothing more.

If the test expects `EofTokenOnly` on empty input, the implementation might
be as small as:

```csharp
public sealed class Lexer {
    public static IReadOnlyList<Token> Scan(
        string source, DiagnosticBag diagnostics, string file = "<source>") {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(diagnostics);
        return [new Token(TokenKind.Eof, "", new SourceLocation(file, 1, 1), 0)];
    }
}
```

This is obviously not the final lexer. That's fine. The next test will force
the next increment. **Resist** the urge to write the lexer you imagine you'll
end up with. Write only what the current test demands.

Run the test. It should pass. Run the full test suite for the project:
`dotnet test`. Nothing else should break.

### Step 3: Refactor

With the test passing and nothing else broken, look at the code with fresh
eyes:

- **Extract** anything that's getting long or doing too much.
- **Rename** anything that no longer reflects what it does.
- **Remove duplication** between the new code and existing code.
- **Simplify** expressions, conditions, control flow.

After each refactor, re-run the tests. If they go red, the refactor changed
behaviour — revert and try a smaller step.

The refactor step is **not optional**. Skipping it accumulates the design
debt TDD is supposed to prevent.

### Step 4: Invariant coverage (lexer / parser / type checker / VM only)

For any work on the compilation or execution pipeline, cover the layer's
invariant alongside the example-based test: no input — valid, invalid, or
pathological — should ever crash the layer; it must always tokenise, parse,
type-check, or execute to either a result or a diagnostic. Express this with
`[Theory]` rows of boundary and adversarial inputs. (`FsCheck` is not used in
this project — not in `Directory.Packages.props` — so do not reach for
property-based testing here.)

````csharp
[Theory]
[InlineData("")]
[InlineData("```")]      // three backticks, no content
[InlineData("\"unterminated")]
public void Scan_PathologicalInput_DoesNotThrow(string input) {
    var ex = Record.Exception(() => Lexer.Scan(input, new DiagnosticBag()));
    Assert.Null(ex);
}
````

Stdlib and other non-pipeline work doesn't need adversarial rows by default,
but add them whenever the input space is large enough that a few examples
can't cover it meaningfully (JSON, CSV, regex, path operations).

### Step 5: Extend coverage

Happy path is green. The feature is not done. Write the failure-path and
edge-case tests now, each through its own red/green/refactor cycle.

- **Happy path.** Behaviour as designed, valid input. Already covered by
  Steps 1–3.
- **Failure path.** Invalid input produces the correct diagnostic (with
  the right error code, location, and message) or the correct `GrobError`
  subtype at runtime.
- **Edge cases.** Inputs at the boundaries of what's representable or
  expected, for the layer you're working in (lexer, parser, type checker,
  VM, stdlib). `tests/CLAUDE.md` lists the invariants that must have guard
  tests — the §3.1.1 fields, two-mode errors and parser recovery.

Each new test follows the full red/green/refactor sequence. Each failing
adversarial row from Step 4 is paid down here.

### Step 6: Coverage check

Run coverage on the affected project. Confirm at or above 90% line
coverage. If it dropped:

- **Add the missing tests.** Coverage gaps usually point at a path the
  feature exercises that none of the tests reach. Add a test.
- **Exclude with substantive justification, when genuinely unreachable.**
  For defensive guards against impossible states, platform-conditional
  code, or generated code:

    ```csharp
    [ExcludeFromCodeCoverage(Justification =
        "Defensive guard against null after compiler enforces non-nullable. " +
        "Unreachable in normal execution; retained as belt-and-braces.")]
    private static void ThrowUnreachable() => throw new InvalidOperationException();
    ```

    "Helper method" is not a justification. "Defensive branch unreachable
    while X holds" is. If you can't write a substantive justification, the
    exclusion is wrong and a test is needed instead.

Do not commit below the bar.

### Step 7: Commit

When the cycle is complete, all categories are green, and coverage is
at or above 90%:

1. `git status` to see what changed.
2. The commit type:
    - **`feat`** — new behaviour. Always includes the tests in the same
      commit. `feat` without tests in the diff is not a `feat`.
    - **`fix`** — bug fix. Always includes the regression test in the same
      commit. `fix` without a regression test is not a `fix`.
    - **`refactor`** — structural change with no new behaviour. No new
      tests should be required; if new tests are needed, it isn't a
      refactor.
    - **`test`** — genuinely test-only work: regression rows that aren't
      paired with a fix, coverage gap filling on already-shipped code, test
      infrastructure changes.
3. If coverage moved materially — `[ExcludeFromCodeCoverage]` added,
   tested code removed, percentage dropped — note it in the commit body
   with the justification.
4. Stage, then run `/commit-message`, which drafts the message and commits on the
   feature branch.

## Design for testability

The example lexer above takes a string and returns tokens — no ambient
dependencies. Most pipeline code is like that and presents no testability
issues. When the code you're writing touches the clock, the filesystem,
the console, the environment, or a process, **don't reach for the static
API**. Inject an abstraction:

- `DateTime.Now` / `DateTime.UtcNow` → inject `IClock`.
- `File.*` / `Directory.*` → inject `IFileSystem`.
- `Console.*` → inject `IStandardStreams`.
- `Environment.*` → inject `IEnvironment`.
- `Random` → inject `IRandomSource`.
- `Process.Start` → no abstraction yet; propose a new interface.

The interfaces live in `Grob.Runtime`; the concrete implementations live at
the composition root (`Grob.Cli`). Tests substitute fakes. This is the
single largest determinant of whether a project hits the 90% bar — see
`src/CLAUDE.md` for the full set of design-for-testability rules.

## Regression cycles

A bug fix is a TDD cycle with a different shape. The sequence:

1. **Reproduce.** Write a test that triggers the exact failure the bug
   report describes. Run it. Watch it fail — but verify the failure is
   the bug, not a different bug or a setup problem.
2. **Comment.** Add a comment to the test referencing the issue or
   commit:

    ```csharp
    // Regression: see issue #142
    [Fact]
    public void Pop_EmptyStack_ThrowsGrobInternalException()
    ```

3. **Co-locate.** Put the test alongside the related happy-path tests
   for the same type — not in a separate `RegressionTests` file.
   Co-location keeps context for the next person reading the file.
4. **Fix.** Write the minimum code to make the test pass.
5. **Refactor, extend coverage, coverage check, commit** as in the
   standard cycle. The commit type is `fix`, not `feat`. The test is
   in the same commit as the fix.

The regression test asserts the **corrected** behaviour, not the bug's
symptom. "Throws `GrobInternalException` with the instruction offset" is the
assertion. "Doesn't crash the host process" is the symptom that pointed
at the bug.

## xUnit specifics

### `[Fact]` vs `[Theory]`

- `[Fact]` for a single concrete test case.
- `[Theory]` with `[InlineData]` when the same behaviour is exercised across
  multiple inputs. Each row should test the same logical behaviour with
  different inputs, not different behaviours.

```csharp
[Theory]
[InlineData("42")]
[InlineData("0")]
[InlineData("9223372036854775807")]
public void Scan_DecimalIntegerLiteral_ProducesIntToken(string source) {
    Token token = Lexer.Scan(source, new DiagnosticBag())[0];
    Assert.Equal(TokenKind.IntLiteral, token.Kind);
    Assert.Equal(source, token.Lexeme);
}
```

### `[MemberData]` for non-inline cases

When data doesn't fit inline — multi-line strings, generated cases, anything
involving non-primitives:

```csharp
public static TheoryData<string, string> InvalidStringLiterals => new() {
    { "\"unterminated", "E2002" },
    { "`unterminated raw", "E2004" },
    { "\"bad escape \\q\"", "E2005" },
};

[Theory]
[MemberData(nameof(InvalidStringLiterals))]
public void Scan_InvalidStringLiteral_ProducesExpectedDiagnostic(
    string source,
    string expectedCode) {
    DiagnosticBag bag = new();
    Lexer.Scan(source, bag);
    Diagnostic diag = Assert.Single(bag.Errors);
    Assert.Equal(expectedCode, diag.Code);
}
```

The codes are the registered ones (`grob-error-codes.md`); a real test also asserts the
line and column (`tests/CLAUDE.md`, _Diagnostic assertions_).

## Assertion patterns (plain xUnit)

The project uses plain xUnit assertions — `FluentAssertions` is deliberately not
used (it is not in `Directory.Packages.props` and there are no `.Should()` calls
anywhere). Do not introduce it. Add a message to assertions that need one.

```csharp
// Equality
Assert.Equal(expected, result);
Assert.NotEqual(unexpected, result);

// Reference equality (use for the §3.1.1 sentinels)
Assert.Same(reference, result);

// Collections
Assert.Equal(3, tokens.Count);
var only = Assert.Single(tokens);             // exactly one, returns it
Assert.Empty(tokens);
Assert.All(tokens, t => Assert.Equal(TokenKind.IntLiteral, t.Kind));

// Strings
Assert.Contains("expected substring", message);
Assert.StartsWith("error:", message);
Assert.Matches(@"line \d+", message);

// Exceptions — capture and assert the payload, do not assert the type alone
var ex = Assert.Throws<ArgumentNullException>(() => Lexer.Scan(null!, new DiagnosticBag()));
Assert.Equal("source", ex.ParamName);

// Booleans carry a message so a failure explains itself
Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
```

`Assert.Single` returns the element, so the "assert one then inspect it" pattern is:

```csharp
Diagnostic diag = Assert.Single(bag.Errors);
Assert.Equal("E2002", diag.Code);
```

## Named exceptions to TDD

These are the only cases where production code may exist without a prior
failing test. Take them explicitly — say in chat: "This is structural
scaffolding — TDD exception, no test first."

1. **Solution skeleton.** Creating the seven projects, csproj files, the
   `.slnx`, the directory layout. Tests come after.
2. **csproj edits** that change build configuration only — target framework,
   language version, package references. No behaviour to test.
3. **Directory and file scaffolding** that contains no logic — empty
   namespace files, partial class declarations created to be filled in later.
4. **Public API surface declaration** when working through a design proposal —
   interfaces and abstract types declared first so tests can be written
   against them. The implementations still follow strict TDD.

The exception is the scaffolding. Implementation hiding behind a
"scaffolding" label is not an exception.

## What to do when the cycle resists

- **Test is hard to write:** the design is probably wrong. Step back, propose
  a redesign before pushing through.
- **Test passes red without an implementation:** the behaviour exists
  somewhere, or the test doesn't actually exercise what you think. Trace it.
- **Implementation grows beyond a few lines to make one test pass:** the
  test is asking for too much at once. Split it into two tests with two
  smaller cycles.
- **Refactor breaks tests that were passing:** the refactor changed
  behaviour, not structure. Revert and try a smaller step.
- **Two tests fail when only one should:** the second test depends on the
  first or the implementation has unwanted side effects. Investigate before
  proceeding.
- **An adversarial row finds a failure:** good. That's what it's for. Keep
  it as a regression row, fix the bug, re-run. The fix is part of this
  cycle, not a follow-up.
- **Coverage drops below 90% at Step 6:** stop. Add tests, or add
  `[ExcludeFromCodeCoverage]` with a substantive justification — and note
  it in the commit body. Do not commit below the bar.
- **Production code reaches for `DateTime.Now`, `File.*`, `Console.*`:**
  stop. Inject the abstraction (see Design for testability above). Direct
  ambient access blocks the 90% bar and breaks parallel test execution.

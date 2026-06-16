---
name: tdd-cycle
description: The strict red/green/refactor TDD procedure for Grob, with xUnit and FluentAssertions specifics. Use when starting a new test, when about to write production code, when finishing a cycle or when uncertain whether you should be writing a test or implementation next.
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
public class LexerTests
{
    [Fact]
    public void Scan_EmptyInput_ReturnsEofTokenOnly()
    {
        // Arrange
        var lexer = new Lexer("");

        // Act
        var tokens = lexer.ScanAll();

        // Assert
        tokens.Should().ContainSingle()
            .Which.Kind.Should().Be(TokenKind.Eof);
    }
}
```

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
// internal — only Grob.Compiler.Tests needs to construct one. Public
// visibility is reserved for the project's contract; this is implementation.
internal sealed class Lexer
{
    private readonly string _source;

    public Lexer(string source) => _source = source;

    public IReadOnlyList<Token> ScanAll()
    {
        // file path empty in test fixture — production callers pass the
        // real path through SourceLocation.
        return new[] { new Token(TokenKind.Eof, new SourceLocation(1, 1, "")) };
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

### Step 4: Property test (lexer / parser / type checker / VM only)

For any work on the compilation or execution pipeline, write an FsCheck
property test alongside the example-based test. The property asserts the
invariant that no input — valid, invalid, or pathological — should ever
crash the layer; it must always tokenise, parse, type-check, or execute to
either a result or a diagnostic.

```csharp
[Property]
public Property Scan_AnyInput_NeverThrows(string input)
{
    return ((Action)(() => new Lexer(input).ScanAll().ToList()))
        .Should()
        .NotThrow()
        .ToProperty();
}
```

If the property test finds a failing input, **don't fix the bug and move
on.** Add the shrunk input as a regression `[Theory]` row alongside the
property test in the same cycle. The property catches the class; the row
pins the specific case.

````csharp
[Theory]
[InlineData("```")]  // discovered by FsCheck — three backticks, no content
public void Scan_TripleBacktickEmpty_DoesNotThrow(string input)
{
    Action act = () => new Lexer(input).ScanAll().ToList();
    act.Should().NotThrow();
}
````

Stdlib and other non-pipeline work doesn't need a property test by default,
but reach for one whenever input space is large enough that examples can't
cover it meaningfully (JSON, CSV, regex, path operations).

### Step 5: Extend coverage

Happy path is green. The feature is not done. Write the failure-path and
edge-case tests now, each through its own red/green/refactor cycle.

- **Happy path.** Behaviour as designed, valid input. Already covered by
  Steps 1–3.
- **Failure path.** Invalid input produces the correct diagnostic (with
  the right error code, location, and message) or the correct `GrobError`
  subtype at runtime.
- **Edge cases.** Inputs at the boundaries of what's representable or
  expected. The Grob-specific edge categories per layer (lexer, parser,
  type checker, VM, stdlib) live in `tests/CLAUDE.md` — read the
  list for the layer you're working in. Don't guess at categories; the
  list exists because they were chosen deliberately.

Each new test follows the full red/green/refactor sequence. Each property
test failure during Step 4 is paid down here.

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
    - **`test`** — genuinely test-only work: regression rows from
      property-test discoveries that aren't paired with a fix, coverage
      gap filling on already-shipped code, test infrastructure changes.
3. If coverage moved materially — `[ExcludeFromCodeCoverage]` added,
   tested code removed, percentage dropped — note it in the commit body
   with the justification.
4. Use the `/commit-message` prompt to draft the message.
5. Stage and show the message to Chris. Don't auto-commit.

## Design for testability

The example lexer above takes a string and returns tokens — no ambient
dependencies. Most pipeline code is like that and presents no testability
issues. When the code you're writing touches the clock, the filesystem,
the console, the environment, or a process, **don't reach for the static
API**. Inject an abstraction:

- `DateTime.Now` / `DateTime.UtcNow` → inject `IClock`.
- `File.*` / `Directory.*` → inject `IFileSystem`.
- `Console.*` → inject `IConsole`.
- `Environment.*` → inject the appropriate abstraction.
- `Process.Start` → inject `IProcessRunner`.

The interfaces live in `Grob.Core`; the concrete implementations live at
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
    public void Pop_EmptyStack_ThrowsInternalVmError()
    ```

3. **Co-locate.** Put the test alongside the related happy-path tests
   for the same type — not in a separate `RegressionTests` file.
   Co-location keeps context for the next person reading the file.
4. **Fix.** Write the minimum code to make the test pass.
5. **Refactor, extend coverage, coverage check, commit** as in the
   standard cycle. The commit type is `fix`, not `feat`. The test is
   in the same commit as the fix.

The regression test asserts the **corrected** behaviour, not the bug's
symptom. "Throws `InternalVmError` with the instruction offset" is the
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
[InlineData("42", 42)]
[InlineData("0", 0)]
[InlineData("9223372036854775807", long.MaxValue)]
public void Scan_DecimalIntegerLiteral_ProducesIntToken(string source, long expected)
{
    var token = new Lexer(source).ScanAll().First();
    token.Kind.Should().Be(TokenKind.IntLiteral);
    token.IntValue.Should().Be(expected);
}
```

### `[MemberData]` for non-inline cases

When data doesn't fit inline — multi-line strings, generated cases, anything
involving non-primitives:

```csharp
public static IEnumerable<object[]> InvalidStringLiterals =>
    new[]
    {
        new object[] { "\"unterminated", "E0042" },
        new object[] { "\"newline\nin\"", "E0043" },
        new object[] { "\"bad escape \\q\"", "E0044" },
    };

[Theory]
[MemberData(nameof(InvalidStringLiterals))]
public void Scan_InvalidStringLiteral_ProducesExpectedDiagnostic(
    string source,
    string expectedCode)
{
    var diagnostics = new Lexer(source).ScanAll().Diagnostics;
    diagnostics.Should().ContainSingle()
        .Which.Code.Should().Be(expectedCode);
}
```

### `[Property]` for invariants

Property tests live alongside `[Fact]` and `[Theory]` tests in the same
class. Use them for the cross-input invariants that example-based tests
can't reach exhaustively.

```csharp
[Property]
public Property Parse_AnyTokenSequence_NeverInfiniteLoops(string input)
{
    return ((Func<bool>)(() =>
    {
        var tokens = new Lexer(input).ScanAll();
        var parser = new Parser(tokens);
        parser.Parse();
        return true;
    })).Should().NotThrow().And.Subject().ToProperty();
}
```

## FluentAssertions patterns

Always use FluentAssertions. Never raw xUnit assertions.

```csharp
// Equality
result.Should().Be(expected);
result.Should().NotBe(unexpected);

// Reference equality
result.Should().BeSameAs(reference);

// Collections
tokens.Should().HaveCount(3);
tokens.Should().ContainSingle();              // exactly one
tokens.Should().BeEmpty();
tokens.Should().StartWith(expectedToken);
tokens.Should().AllSatisfy(t => t.Kind.Should().Be(TokenKind.IntLiteral));

// Strings
message.Should().Contain("expected substring");
message.Should().StartWith("error:");
message.Should().MatchRegex(@"line \d+");

// Exceptions
Action act = () => new Lexer(null!).ScanAll();
act.Should().Throw<ArgumentNullException>()
    .WithMessage("*source*");

// Object graphs
result.Should().BeEquivalentTo(expected);     // deep equality
```

Avoid `result.Should().NotBeNull()` followed by access — use
`.Should().NotBeNull().And.Subject` or `.Which`:

```csharp
diagnostics.Should().ContainSingle()
    .Which.Code.Should().Be("E0042");
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
- **Property test finds a failure:** good. That's what it's for. Add the
  shrunk input as a regression row, fix the bug, re-run both the row and
  the property. The fix is part of this cycle, not a follow-up.
- **Coverage drops below 90% at Step 6:** stop. Add tests, or add
  `[ExcludeFromCodeCoverage]` with a substantive justification — and note
  it in the commit body. Do not commit below the bar.
- **Production code reaches for `DateTime.Now`, `File.*`, `Console.*`:**
  stop. Inject the abstraction (see Design for testability above). Direct
  ambient access blocks the 90% bar and breaks parallel test execution.

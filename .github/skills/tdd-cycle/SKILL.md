---
name: tdd-cycle
description: The strict red/green/refactor TDD procedure for Grob, with xUnit and FluentAssertions specifics. Use when starting a new test, when about to write production code, when finishing a cycle or when uncertain whether you should be writing a test or implementation next.
---

# TDD cycle for Grob

The cycle is non-negotiable. Red, green, refactor, in that order, every
time. The named exceptions are listed at the end of this skill — take them
explicitly, not silently.

## The full procedure

### Step 1: Red

Write a single test that exercises one specific behaviour. The behaviour
should be observable through the public API of the type under test.

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

Write the *minimum* code that makes the test pass. Nothing more.

If the test expects `EofTokenOnly` on empty input, the implementation might
be as small as:

```csharp
public sealed class Lexer
{
    private readonly string _source;

    public Lexer(string source) => _source = source;

    public IReadOnlyList<Token> ScanAll()
    {
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

### Step 4: Commit

When the cycle is complete and the tests are green:

1. `git status` to see what changed.
2. Decide if this is one commit or two:
   - One: the test and implementation are tightly coupled, the change is
     small, separating them adds no value.
   - Two: the test is genuinely separate (e.g. covering existing behaviour
     that wasn't tested, or testing a different unit). Commit the test first
     as `test(scope):`, then the implementation as `feat(scope):`.
3. Use the `/commit-message` prompt to draft the message.
4. Stage and show the message to Chris. Don't auto-commit.

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

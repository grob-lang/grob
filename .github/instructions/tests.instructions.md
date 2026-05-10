---
applyTo: "**/*Tests*.cs"
---

# TDD discipline for Grob test files

When you are writing or modifying a test file, the rules below apply on top of
the general C# conventions.

## The cycle is non-negotiable

**Red, green, refactor.** In that order. Every time.

1. **Red.** Write the test. Run it. Watch it fail with a clear, specific error
   message. If the test passes on the first run, the test is wrong — either it
   isn't actually exercising the behaviour, or the behaviour already exists and
   the test was redundant. Find out which.
2. **Green.** Write the *minimum* implementation that makes the test pass.
   Nothing more. If you find yourself adding code that isn't required by a
   failing test, stop.
3. **Refactor.** With the test passing, improve the design. Extract methods,
   rename for clarity, remove duplication. Re-run the tests after each change.
   The refactor step is not optional — skipping it accumulates the design debt
   TDD is supposed to prevent.

## Named exceptions to TDD

Some Sprint 1 work is genuinely TDD-awkward. The following are named exceptions
where it is acceptable to write code without a failing test first. Name the
exception explicitly when you take it:

- **Solution skeleton.** Creating the seven projects, their csproj files, the
  `.slnx` file, the directory layout. Tests come after the structure exists.
- **csproj edits** that change build configuration only (target framework,
  language version, package references). No behaviour to test.
- **Directory and file scaffolding** that contains no logic — empty namespace
  files, partial class declarations created to be filled in later.
- **Public API surface declaration** when working through a design proposal —
  interfaces and abstract types declared first so tests can be written against
  them. The implementations still follow strict TDD.

If you take an exception, say so in chat: "This is structural scaffolding — TDD
exception, no test first." Don't smuggle implementation through under the
scaffolding label.

## Test naming

`MethodUnderTest_Scenario_ExpectedBehaviour`. Examples:

- `Scan_EmptyInput_ReturnsEofTokenOnly`
- `Scan_UnterminatedString_ProducesE0042Diagnostic`
- `Parse_MissingClosingBrace_RecoversAtNextDeclaration`

The scenario should describe what's being varied, not just restate the method
name. "Returns expected value" is not a scenario.

## Test structure

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

`// Arrange`, `// Act`, `// Assert` comments are optional but useful when
sections aren't visually distinct. The blank lines between them are not.

## Assertions

- `FluentAssertions` everywhere. No `Assert.Equal`, no `Assert.True`.
- One logical assertion per test. Multiple `.Should()` calls are fine if they
  verify the same outcome from different angles; they're not fine if they're
  testing separate behaviours that warrant separate tests.
- Assertion messages: only when the failure wouldn't be obvious from the
  expression. `value.Should().Be(42)` doesn't need a message; complex equality
  on a large object graph does.

## Parameterised tests

`[Theory]` with `[InlineData]` for small fixed sets. `[MemberData]` when the
data is generated or doesn't fit inline. Each row should test the same
behaviour with different inputs, not different behaviours.

```csharp
[Theory]
[InlineData("42", 42)]
[InlineData("0", 0)]
[InlineData("-1", -1)]
public void Scan_IntegerLiteral_ProducesIntToken(string source, long expected)
{
    var token = new Lexer(source).ScanAll().First();
    token.Kind.Should().Be(TokenKind.IntLiteral);
    token.IntValue.Should().Be(expected);
}
```

## What a test should not do

- **No `Thread.Sleep`.** If timing matters in the test, the design is wrong —
  fix the design.
- **No file system or process side effects** except in tests that are
  explicitly integration-tagged. Unit tests work against in-memory sources.
- **No dependency on test execution order.** Each test sets up its own
  arrangement.
- **No conditionals on assertion outcome.** A test that branches based on what
  it found is two tests pretending to be one.
- **No commented-out tests.** Delete them or fix them.

## Gold-master tests (error-examples library)

The negative-test release gate uses gold-master comparison: a `*_grob.txt`
source file and a `*_expected.txt` diagnostic output. When you add or change
a diagnostic:

1. Update the source file if needed.
2. Run the test, capture the actual output.
3. Read the actual output. Verify it matches what the diagnostic *should* say.
4. Only then update the expected file.

Never auto-update the expected file without reading what changed. The whole
point of gold-master is to catch diagnostic regressions, and silent
acceptance defeats it.

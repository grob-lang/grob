---
applyTo: "**/*Tests*.cs"
---

# TDD discipline for Grob test files

When you are writing or modifying a test file, the rules below apply on top of
the general C# conventions.

Grob is a public-facing language. The test suite is what makes that claim
defensible. A bug in the lexer, parser, type checker, or VM becomes a bug
report from someone's CI pipeline, not a personal embarrassment in a hobby
project. The bar is correspondingly high.

The rules in this file cover *how tests are written and what coverage is
expected*. The design-for-testability rules — ambient dependencies,
`InternalsVisibleTo`, `[ExcludeFromCodeCoverage]` placement — live in
`csharp.instructions.md`. Read both.

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

## Coverage expectations per feature

Every feature gets three categories of test. Missing any one of them is not
"done", regardless of what the happy-path test reports.

### Happy path

The behaviour as designed, exercised with valid, well-formed input. This is
what most developers write first and stop at. It is necessary and insufficient.

### Failure path

The behaviour when input is invalid, malformed, or violates a contract. For
the compiler this means producing the correct diagnostic with the correct
code, location, and message. For the runtime this means throwing the correct
`GrobError` subtype. A failure-path test is not optional just because the
happy path is covered.

### Edge cases

Inputs at the boundaries of what's representable, valid, or expected. These
are where language implementations break. The categories below are not a
checklist to skim — they are the failure modes that come back as bug reports
if not tested up front.

**Lexer:**

- Empty input (zero bytes).
- Single character (each token-starter character in isolation).
- EOF mid-token (unterminated string, unterminated block comment, number with
  trailing decimal point, identifier at exact buffer end).
- Maximum-length identifiers, maximum-length string literals, maximum-magnitude
  numeric literals (boundary of `long.MaxValue`, `long.MinValue`, `double`
  precision limits).
- Line endings: LF, CRLF, CR-only, mixed within one file.
- BOM at start of file.
- Unicode: identifiers using non-ASCII letters (if supported), strings
  containing surrogate pairs, combining characters, RTL marks.
- Escape sequences at buffer boundaries (`\` as final character, `\u` with
  truncated hex digits).
- String interpolation: empty `${}`, nested interpolation inside an
  interpolation expression, `$` followed by non-`{`, escaped `\$`.
- Raw strings: backtick at start of input, triple-backtick spanning the entire
  file, backtick inside a triple-backtick string.
- Whitespace-only input, comment-only input.

**Parser:**

- Empty file, single token, single declaration.
- Deeply nested expressions (1000+ levels) — stack discipline check.
- Deeply nested blocks, deeply nested type expressions.
- Error recovery synchronisation: malformed declaration followed by a valid
  one (next declaration must parse cleanly).
- Cascade suppression: one root cause must not produce a flood of derived
  diagnostics.
- Trailing commas where permitted and where not.
- Missing closing brackets at every nesting level.
- Operator precedence at the edges (unary chained with binary, ternary-like
  constructs, nullable propagation through `?.` chains).
- `param` block edge cases: zero parameters, decorator without argument,
  decorator on wrong target.

**Type checker:**

- Forward references resolved by pass 2 (function calling function declared
  below it in the same file).
- Cyclic type definitions (must be diagnosed, not infinite-loop).
- Nullable propagation: `T?` to `T` without check, `T` to `T?` (implicit ok),
  chained `?.` short-circuit semantics.
- Generic constraint violations (consumer-side, since users can't declare
  generic functions in v1).
- Implicit `int → float` conversion at every site that allows it; explicit
  conversion required everywhere else.
- `const` used in a non-constant context.
- `readonly` rebinding attempts.
- Cascade suppression: an expression whose subexpression is `Error`-typed
  must not produce a second diagnostic.

**VM:**

- Empty stack on every opcode that pops (must throw, not segfault).
- Stack overflow at the configured limit.
- Integer overflow at `long.MaxValue + 1`, `long.MinValue - 1`, `long.MinValue *
  -1`.
- Division by zero (integer and float), modulo by zero.
- Float special values: `NaN`, `+Infinity`, `-Infinity`, `-0.0`.
- Null dereference on every operation that dereferences.
- Array bounds: index `-1`, index `length`, index `long.MaxValue`.
- Map key not found, map key is null.
- Closure capture: capture of a loop variable, capture of a variable that
  goes out of scope, capture across multiple closures sharing one upvalue.
- Try/catch: exception in finally, exception in catch handler, rethrow,
  catch-all ordering.

**Standard library:**

- Empty inputs for every collection-taking function.
- Single-element inputs.
- Inputs containing the type's zero value.
- Path operations: empty path, root path, path with trailing separator,
  path with `..` segments, UNC paths (Windows is the primary target — these
  matter).
- File system operations: file not found, permission denied, path too long,
  reserved Windows filenames (`CON`, `PRN`, `NUL`).
- JSON: empty object, empty array, deeply nested, escaped strings, numbers
  at the edge of double precision, duplicate keys.
- CSV: empty file, single row, quoted fields containing commas, quoted fields
  containing newlines, escaped quotes inside quoted fields.
- Regex: empty pattern, pattern matching empty string, pattern with no match,
  catastrophic backtracking input (must time out, not hang).
- Date: epoch, far-future dates, leap seconds (if exposed), time zones at
  the offset extremes.

If a feature's edge-case category is not represented in the test file, the
feature is not finished.

## Release-gate coverage rules

These are enforceable rules, not aspirations. They gate v1 release.

- **Every error code in the registry must have at least one test that
  produces it.** The error-examples library covers gold-master diagnostic
  output; in-process tests cover the diagnostic being emitted at all. Both
  are required.
- **Every opcode must have a VM test exercising it directly.** Coverage
  through higher-level scripts is necessary but does not replace per-opcode
  tests.
- **Every public method on every built-in type must have happy-path,
  failure-path, and edge-case tests.** The type registry is the checklist;
  the test suite is the proof.
- **Every public function in every stdlib module must have happy-path,
  failure-path, and edge-case tests.** The stdlib reference is the
  checklist.
- **Every validation script in `grob-sample-scripts.md` must run as a test
  that compiles and executes it and asserts the expected output.** The
  thirteen scripts are the release gate; they must be automated, not
  manually verified.

## Coverage metrics

**Minimum line coverage: 90% across every project.** This is a hard floor,
not a target. The CI pipeline enforces it.

When code is genuinely unreachable by tests — defensive branches against
impossible states, platform-conditional code, generated code — exclude it
with `[ExcludeFromCodeCoverage]` and supply a **valid justification** in the
attribute:

```csharp
[ExcludeFromCodeCoverage(Justification =
    "Defensive guard against null after compiler enforces non-nullable. " +
    "Unreachable in normal execution; retained as belt-and-braces.")]
private static void ThrowUnreachable() => throw new InvalidOperationException();
```

The justification must explain **why** the code cannot be tested, not what
the code does. "Helper method" is not a justification. "Unreachable defensive
branch retained for forward compatibility" is. Code review rejects exclusions
without a substantive justification.

Exclusions are reviewed periodically. An exclusion that was valid when added
may become invalid as the code around it changes — when in doubt, remove the
attribute and write the test.

## Property-based testing

`FsCheck` is in from day one. Property-based tests are not a replacement for
example-based tests; they are an additional layer that finds inputs no human
would think to write.

Apply property-based testing where input space is large enough that examples
can't cover it meaningfully:

- **Lexer:** any string of valid characters should either tokenise cleanly
  or produce a diagnostic — it must never throw an unhandled exception.
- **Parser:** any token sequence should either parse to an AST or recover
  with diagnostics — never crash, never infinite-loop. The error-recovering
  parser is exactly the kind of code property testing is for.
- **Type checker:** any well-formed AST should produce a type-checked tree
  or a diagnostic — same invariant as the parser.
- **VM:** any well-formed bytecode should execute to completion, throw a
  `GrobError`, or hit a configured limit (stack depth, instruction count) —
  never segfault, never hang.
- **Numeric and string operations:** algebraic identities where they hold
  (`x + 0 == x`, `s + "" == s`, `parse(format(x)) == x` within precision
  bounds).
- **Round-trip operations:** any value formatted to JSON and parsed back
  should equal the original (within representability). Same for CSV
  with appropriate caveats.

Property-based test naming follows the same convention as example-based
tests, with `Property` as the implicit scenario verb:

```csharp
[Property]
public Property Scan_AnyValidUtf8Input_NeverThrows(string input)
{
    return ((Action)(() => new Lexer(input).ScanAll().ToList()))
        .Should()
        .NotThrow()
        .ToProperty();
}
```

When a property test finds a failing case, **add the shrunk failing input as
a regression `[Theory]` row immediately.** The property test catches the
class of bug; the regression row pins the specific case that was found.

## Regression discipline

Every bug fix gets a test that fails before the fix and passes after. This
rule is what stops the same bug coming back six months later when someone
refactors the area.

The regression test:

1. Reproduces the exact input that caused the bug.
2. Asserts the corrected behaviour, not the bug's symptom.
3. Carries a comment referencing the issue or commit that introduced the
   fix: `// Regression: see issue #142`.
4. Lives next to the related happy-path tests, not in a separate
   "regression" file. Co-location keeps context.

If a bug was found by a property test, the regression row is mandatory in
addition to leaving the property test in place. The property test demonstrates
the property holds; the regression row demonstrates this specific case is
locked down.

## What NOT to test

Equally important as what to test. Tests that don't pull their weight become
a tax on every refactor and erode the suite's credibility.

- **Don't test the framework.** No tests verifying that `List<T>.Add` works,
  that `JsonSerializer` round-trips, that `Dictionary` returns what you put
  in. The framework's own test suite covers this.
- **Don't test trivial getters and setters.** A property that returns a
  backing field doesn't need a test. A property that computes or validates
  does.
- **Don't test private implementation details.** Test through the public
  surface. If a private method is so complex it warrants direct testing,
  it should probably be a public method on a separate type.
- **Don't write tests that restate the implementation.** A test that mirrors
  the production code's structure will pass whatever the production code
  does, including when it's wrong. Tests verify behaviour, not structure.
- **Don't write tests for code that will be deleted next sprint.** If the
  decision is already made, delete the code first.

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
- One **behaviour** per test, not one assertion per test. A test that
  verifies a token has the correct kind, lexeme, value, and source location
  is verifying one behaviour — the lexer produced the right token — and
  four `.Should()` calls on those four facets are correct. A test that
  verifies the lexer produced the right token *and also* the right
  diagnostic is two behaviours; split it.
- Assertion messages: only when the failure wouldn't be obvious from the
  expression. `value.Should().Be(42)` doesn't need a message; complex
  equality on a large object graph does.

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

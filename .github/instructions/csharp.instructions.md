---
applyTo: "**/*.cs"
---

# C# coding conventions for Grob

These rules apply to every `.cs` file in the solution.

## Language and target

- C# 13 on .NET 10 LTS. `<LangVersion>13</LangVersion>`, `<TargetFramework>net10.0</TargetFramework>`.
- `<Nullable>enable</Nullable>` everywhere. Nullable reference types are not optional.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` on every project.
- `<ImplicitUsings>enable</ImplicitUsings>`.

## File layout

- One public type per file. File name matches the type name exactly.
- `using` directives first, then a blank line, then the file-scoped namespace
  declaration (`namespace Grob.Compiler.Lexing;`), then the type.
- `using` directives sit before — and therefore outside — the namespace, sorted
  `System.*` first, then other external packages, then `Grob.*`.

## Naming

- `PascalCase` for types, methods, properties, public fields, constants.
- `camelCase` for locals and parameters.
- `_camelCase` for private fields. No `m_` prefix. No Hungarian.
- Interface names start with `I`. Generic type parameters start with `T`.
- Test class names end with `Tests`. Test methods are
  `MethodUnderTest_Scenario_ExpectedBehaviour`. No spaces. Underscores are
  acceptable in test method names because they substantially improve readability;
  this is the only place underscores appear in identifiers.

## Style

- Allman braces (opening brace on its own line) — the .NET convention, not the
  C-style same-line braces used in Grob source itself.
- Spaces inside string interpolation braces: `$"{name}"` not `$"{ name }"`.
- Explicit access modifiers always. No reliance on default `private`.
- `var` only when the right-hand side makes the type obvious (`new Foo()`,
  literals, factory methods with the type in the name). Otherwise spell out
  the type.
- Expression-bodied members for one-liners that read naturally. Block bodies
  for anything multi-statement.
- Pattern matching and switch expressions over chained `if/else if` when the
  shape fits. Exhaustive switch on enums.
- `is null` and `is not null` over `== null` and `!= null`. Always.
- Readonly fields wherever the value is set once. `readonly struct` and
  `readonly record struct` for value types.
- No regions. No commented-out code. No `TODO` without an issue link.

## Immutability

- Prefer immutable types. Records for data, classes for behaviour with state,
  structs for small value-like things measured to matter.
- `init` accessors instead of `set` for properties that must be settable only
  during construction.
- Collections returned from public APIs are read-only types (`IReadOnlyList<T>`,
  `IReadOnlyDictionary<K, V>`). Never return `List<T>` from a public method.

## Errors and exceptions

- Throw exceptions for programmer errors and genuinely exceptional conditions
  only. Recoverable failures in the compiler pipeline use the diagnostic model
  (error nodes, `Diagnostic` records) not exceptions.
- Throw the most specific exception type that fits. `ArgumentNullException` and
  `ArgumentException` for invalid arguments at boundary methods.
- Never catch `Exception` except at the outermost CLI entry point. Never catch
  and swallow. Never catch and log-then-throw — log at the catch site only if
  you have context the caller cannot reconstruct.

## Async

- `async`/`await` always. No `.Result`, no `.Wait()`, no `Task.Run` to escape
  sync contexts.
- Suffix async method names with `Async` unless they implement an interface
  method that doesn't.
- `CancellationToken` as the last parameter on any async method that could
  take longer than a few milliseconds.
- `ConfigureAwait(false)` is not required — there is no synchronisation context
  to deadlock against in console apps and unit tests, and the rule has been
  relaxed in modern .NET guidance.

## Doc comments

- Public types and members get XML doc comments. `<summary>` is mandatory.
  `<param>`, `<returns>`, `<exception>` where they add information beyond the
  signature.
- Internal types: doc comment when the purpose isn't obvious from the name.
- Private members: comment only when the implementation has non-obvious
  reasoning.

## Performance and allocations

- Don't micro-optimise. Profile first. The compiler and VM are not yet at the
  point where allocation patterns matter.
- However: in tight loops (lexer scanner, VM dispatch), avoid LINQ, avoid
  `string.Format`, prefer `Span<T>` and `ReadOnlySpan<char>` over substring.
- `StringBuilder` for any string assembly that involves more than three
  concatenations or runs in a loop.

## Design for testability

Production code is written expecting to be tested. The minimum line coverage
bar is 90% per project — see `tests.instructions.md` for the bar itself and
the rules around exclusions. The conventions below are what makes that bar
achievable without distortion.

- **No ambient dependencies in business logic.** No direct use of
  `DateTime.Now`, `DateTime.UtcNow`, `Environment.*`, `File.*`, `Directory.*`,
  `Console.*`, or `Process.Start` inside types that contain logic. Inject an
  abstraction (`IClock`, `IFileSystem`, `IConsole`, `IProcessRunner`) and
  pass it through the constructor. The single concrete implementation lives
  at the composition root — `Grob.Cli` — and is wired up there.
- **No static mutable state.** `static readonly` for genuinely constant
  lookup tables and singletons is fine. `static` fields that change at
  runtime are not — they make tests order-dependent and parallel-hostile.
- **Constructor injection over service location.** No
  `ServiceProvider.GetService<T>()` inside business code. Dependencies are
  declared on the constructor; the composition root supplies them.
- **`InternalsVisibleTo` is allowed with discipline.** Each production
  project declares `[assembly: InternalsVisibleTo("Grob.<Project>.Tests")]`
  in an `AssemblyInfo.cs` (or via `<InternalsVisibleTo>` MSBuild item).
  Tests should drive through the public surface where they can. Where the
  public surface would otherwise grow purely to enable testing — exposing
  a type or method that has no consumer outside the test assembly — make
  the type `internal` and use `InternalsVisibleTo` instead. The bar is:
  is this type part of the project's contract, or only its implementation?
  Contract → public. Implementation → internal + `InternalsVisibleTo`.
- **No `sealed` on internal types unless there is a reason.** Internal
  types may need to be subclassed or substituted in tests; don't lock
  them down by default.
- **Avoid `static` methods on types that have any non-trivial branching.**
  A `static` method with three code paths is three things to cover and no
  seam to substitute. If it's pure (input → output, no I/O, no time, no
  randomness) `static` is fine. If it touches anything ambient it should
  be an instance method on a type that takes the ambient dependency
  through its constructor.

## `[ExcludeFromCodeCoverage]`

When code is genuinely unreachable by tests — defensive guards against
states the compiler should have already ruled out, platform-conditional
code, generated code — exclude it with `[ExcludeFromCodeCoverage]` and
supply a substantive `Justification`:

```csharp
[ExcludeFromCodeCoverage(Justification =
    "Defensive guard against null after compiler enforces non-nullable. " +
    "Unreachable in normal execution; retained as belt-and-braces.")]
private static void ThrowUnreachable() => throw new InvalidOperationException();
```

The justification must explain **why** the code cannot or should not be
tested, not what the code does. "Helper method" is not a justification.
"Defensive branch retained for forward compatibility, unreachable while
the type checker enforces X" is. Code review rejects exclusions without
a substantive justification.

Periodically review exclusions. An exclusion that was valid when added may
become invalid as the surrounding code changes. When in doubt, remove the
attribute and write the test.

## Test code conventions

- Tests live in `Grob.<Project>.Tests` alongside the project they exercise.
- One test class per type under test, named `<TypeUnderTest>Tests`.
- AAA (Arrange / Act / Assert) layout. Comments labelling the sections only if
  the boundaries aren't obvious from blank-line grouping.
- `FluentAssertions` for assertions. `xUnit.Theory` with `InlineData` for
  parameterised tests; `MemberData` for anything that doesn't fit on a line.
- `FsCheck` for property-based testing — it is part of the standard test
  stack, not an optional extra. See `tests.instructions.md` for where to
  apply it.
- No test should depend on another test's state. No test ordering.
- No `Thread.Sleep` in tests. If timing matters, the design is wrong.

The full TDD discipline — red/green/refactor, coverage taxonomy, release-gate
rules, regression policy — lives in `tests.instructions.md`. The rules in
this file cover _how C# is written_; the test instructions cover _how tests
are written and what coverage is expected_.

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
- File-scoped namespaces. `namespace Grob.Compiler.Lexing;` followed by a blank
  line, then `using` directives, then the type.
- `using` directives outside the namespace, sorted: `System.*` first, then other
  `System.*`, then external packages, then `Grob.*`.

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

## Test code conventions

- Tests live in `Grob.<Project>.Tests` alongside the project they exercise.
- One test class per type under test, named `<TypeUnderTest>Tests`.
- AAA (Arrange / Act / Assert) layout. Comments labelling the sections only if
  the boundaries aren't obvious from blank-line grouping.
- `FluentAssertions` for assertions. `xUnit.Theory` with `InlineData` for
  parameterised tests; `MemberData` for anything that doesn't fit on a line.
- No test should depend on another test's state. No test ordering.
- No `Thread.Sleep` in tests. If timing matters, the design is wrong.

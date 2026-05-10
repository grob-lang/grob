# Contributing to Grob

Contributions are welcome. Here is how to get involved.

## Process

1. Fork the repository
2. Create a branch from `main`
3. Make your changes
4. Open a pull request

## CLA

By submitting a pull request you confirm that you have the right to contribute
the code and that it may be distributed under the MIT licence. No separate CLA
document — this confirmation is implicit in your PR submission.

## Code Style

Match the existing code. Key conventions:

- C# .NET 10, targeting Windows
- PascalCase types, camelCase locals, _camelCase private fields
- Same-line braces
- `partial class` for compiler file separation
- `struct` for value types, `class` for heap types only
- British English in documentation, comments and error messages
- No Oxford comma

## Tests

Every compiler change requires tests. Test both happy paths and error paths.
Given source, assert correct bytecode. The VM loop can be trusted once verified
on simple cases — bugs live in the compiler.

## Documentation

British English throughout. No Oxford comma. Never use the word "simply." All
path examples use Windows conventions.

The wiki specification in `docs/wiki/` is the language behaviour reference. The
decisions log in `docs/design/grob-decisions-log.md` is the design authority.

## Reporting Issues

Use GitHub Issues. Include:

- What you expected
- What happened
- A minimal Grob script that reproduces the problem
- Grob version (`grob --version`)

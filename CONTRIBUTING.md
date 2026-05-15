# Contributing to Grob

Contributions are welcome. Read this document once before opening a pull request.

## Culture

Grob is developed with AI augmentation, not vibe coding. Every line that lands
in this repository is understood by the human engineer who wrote or reviewed it.
Code generation without comprehension is not how this project works.

The decisions log at `docs/design/grob-decisions-log.md` is the design
authority. When any other document — including this one — conflicts with the
decisions log, the log wins.

British English is used throughout: source comments, doc comments, identifiers,
commit messages and all documentation. No Oxford comma. Never the word "simply".
No emoji anywhere — not in compiler output, CLI output, comments or
documentation.

## Process

All work goes through topic branches and pull requests. Direct commits to `main`
are not permitted; branch protection enforces this.

1. Fork the repository.
2. Create a branch from `main` using the naming scheme below.
3. Make your changes, including tests.
4. Open a pull request against `main`.

### Branch naming

| Prefix          | Use                             |
| --------------- | ------------------------------- |
| `feat/<topic>`  | New language or tooling feature |
| `fix/<topic>`   | Bug fix                         |
| `chore/<topic>` | Maintenance, dependency updates |
| `docs/<topic>`  | Documentation only              |
| `ci/<topic>`    | CI/CD pipeline changes          |

### Commit messages

Follow Conventional Commits format (`feat:`, `fix:`, `chore:` etc.). This is a
convention, not yet enforced by tooling.

### Pull requests

- One coherent chunk per PR.
- PR description: short summary, what changed, why, and references to any
  relevant decisions log entries or ADRs.
- Self-review before requesting merge: read the diff in the GitHub UI as if
  someone else wrote it.
- CI must be green before merge.
- PRs are squashed on merge. The squash commit message is what lands on `main`;
  write it as substantive prose with rationale, not just a ticket reference.

## CLA

By submitting a pull request you confirm that you have the right to contribute
the code and that it may be distributed under the MIT licence. No separate CLA
document — this confirmation is implicit in your PR submission.

## Code conventions

The project targets C# 14 on .NET 10 LTS. `Nullable` and `TreatWarningsAsErrors`
are both enabled.

### Naming

- `PascalCase` for types and public members; `camelCase` for locals and
  parameters.
- Runtime types use the full `Grob` prefix: `GrobType`, `GrobValue` and so on.
  Never the abbreviated `Gro` form (see ADR-0012).

### Structure

- `partial class` is used in `Grob.Compiler` to separate concerns across files.
- The visitor pattern is used for all AST passes: type checker, optimiser and
  compiler.
- Use `struct` for value types and `class` for heap objects only.

## Tests

The project uses xUnit. Test class names match the type under test with a
`Tests` suffix appended.

Strict TDD applies: write the test first, watch it fail, then implement. Do not
open a PR for a compiler change that lacks tests.

Compiler outputs are tested exhaustively. Given source input, assert the correct
bytecode output (per D-040). The VM execution loop can be trusted once verified
on simple cases — bugs live in the compiler, not in dispatch.

## Reporting issues

Use GitHub Issues. Include:

- What you expected.
- What happened.
- A minimal Grob script that reproduces the problem.
- Grob version (`grob --version`).

## Pre-commit Hooks

This repository uses the pre-commit framework. Install once after
cloning:

    pip install pre-commit
    pre-commit install

Hooks run automatically on every commit. To run manually:

    pre-commit run --all-files

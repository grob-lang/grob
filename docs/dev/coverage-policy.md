# Coverage Policy

This document defines which files are excluded from code coverage measurement
in the Grob codebase, and why. It is the single source of truth referenced by
`[ExcludeFromCodeCoverage]` justification strings and by the SonarCloud
configuration in `.github/workflows/sonarcloud.yml`.

The policy is deliberately narrow. Exclusion is a tool for genuinely-untestable
bootstrap code, not a place to hide untested logic.

---

## Coverage target

Grob targets **≥ 90% line coverage** on production code across the solution,
measured by SonarCloud against Cobertura output from `dotnet test`.

The target applies to in-scope production code only. Excluded files (listed
below) are removed from both numerator and denominator before the percentage
is calculated.

---

## Excluded files

### `src/Grob.Cli/Program.cs`

**Reason:** CLI entry point. Parses `argv`, dispatches to subcommands, and wires
the compiler and VM together. Contains no business logic of its own — every
meaningful operation delegates to a class in `Grob.Compiler`, `Grob.Vm`, or
`Grob.Runtime`.

**How it is tested:** End-to-end integration tests drive `grob run`, `grob fmt`,
`grob check`, `grob install`, and `grob restore` against the thirteen validation
suite scripts (`grob-sample-scripts.md`) plus the error-examples library. These
tests exercise the entry point in its real configuration, which is the only
configuration that matters.

**Size constraint:** `Program.cs` must remain thin. If it grows past ~30 lines,
extract argv parsing and subcommand dispatch into a `CommandLineRunner` (or
similar) class inside `Grob.Cli`. That class is in scope for coverage and must
be unit tested.

### `src/Grob.Lsp/Program.cs`

**Reason:** LSP host bootstrap. Sets up JSON-RPC transport (stdio in v1) and
instantiates the language server. Contains no protocol or analysis logic.

**How it is tested:** LSP integration tests connect a test client to the server
process and exercise the protocol surface — `initialize`, `textDocument/didOpen`,
`textDocument/hover`, `textDocument/definition`, and diagnostics push. These
tests cover entry-point wiring in its real configuration.

**Size constraint:** Same as `Grob.Cli/Program.cs`. Anything beyond instantiating
the host must move into `Grob.Lsp` proper and remain in scope for coverage.

---

## What is _not_ excluded

The following are **in scope** for coverage and must meet the 90% target:

- All code in `Grob.Core`, `Grob.Runtime`, `Grob.Compiler`, `Grob.Vm`,
  `Grob.Stdlib`.
- All code in `Grob.Cli` and `Grob.Lsp` **except** the two `Program.cs` files
  listed above.
- All first-party plugins (`Grob.Http`, `Grob.Crypto`, `Grob.Zip`).
- Argv parsing, subcommand dispatch, exit-code mapping, and any other CLI logic
  that is more than pure bootstrap wiring — these must live in testable classes,
  not in `Program.cs`.

Test projects (`*.Tests.csproj`) are excluded by convention — they do not need
their own coverage measured. This is configured via `/d:sonar.test.exclusions`
in the CI workflow.

---

## Implementation

The exclusion is applied in two places (defence in depth — the partial-class
attribute is known to be unreliable in some sonar-dotnet versions with top-level
statements):

### 1. `[ExcludeFromCodeCoverage]` attribute on `Program`

Both `Program.cs` files end with a partial-class declaration carrying the
attribute and a justification pointing at this document:

```csharp
using System.Diagnostics.CodeAnalysis;

// ... top-level statements above ...

[ExcludeFromCodeCoverage(
    Justification = "CLI entry point; covered by integration tests. See docs/dev/coverage-policy.md.")]
internal partial class Program { }
```

### 2. `.github/workflows/sonarcloud.yml`

The SonarScanner for .NET does not use `sonar-project.properties`; exclusion
patterns must be passed as `/d:` arguments to `dotnet sonarscanner begin`:

```yaml
/d:sonar.coverage.exclusions="**/Grob.Cli/Program.cs,**/Grob.Lsp/Program.cs"
```

`sonar.coverage.exclusions` removes the files from coverage measurement only —
they remain in scope for issue detection, code smells, and duplication analysis.

---

## Changing this policy

Adding a new exclusion requires:

1. A documented reason explaining why the code is not meaningfully unit-testable.
2. A description of how the code is otherwise tested (integration tests,
   end-to-end tests, contract tests).
3. A size constraint or scope limit that prevents the exclusion from being used
   as a hiding place for untested logic.
4. An entry in this document plus the corresponding `[ExcludeFromCodeCoverage]`
   attribute and CI workflow update.

Removing an exclusion requires only that the corresponding code be brought under
test and the entries deleted from this document, the source file, and the CI
workflow.

---

_This policy applies from Sprint 1 onwards. Last reviewed: May 2026._

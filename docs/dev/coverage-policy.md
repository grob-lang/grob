# Coverage Policy

This document defines which files are excluded from code coverage measurement
in the Grob codebase, and why. It is the single source of truth referenced by
`[ExcludeFromCodeCoverage]` justification strings and by the SonarCloud
configuration in `.github/workflows/sonarcloud.yml`.

The policy is deliberately narrow. Exclusion is a tool for genuinely-untestable
bootstrap code, not a place to hide untested logic.

---

## Coverage target

Grob targets **≥ 90% line and branch coverage** (D-328) on production code across the solution,
measured by SonarCloud against Cobertura output from `dotnet test`. Two more floors apply at
the same 90%: every PR's new production code (D-438), so a PR cannot lean on solution-wide
headroom, and every project on its own, a working check deliberately stricter than D-328 that
`trunk-flow` and `tdd-cycle` enforce.

The target applies to in-scope production code only. Excluded files (listed
below) are removed from both numerator and denominator before the percentage
is calculated.

---

## Excluded files

### `src/Grob.Cli/Program.cs`

**Reason:** CLI entry point. It binds `Console.In`, `Console.Out` and `Console.Error` to
`CommandLineRunner` and returns its exit code. It has no branch of its own.

**How it is tested:** argv parsing and subcommand dispatch live in `CommandLineRunner`,
which is in scope for coverage. `CommandLineRunnerTests` in `Grob.Integration.Tests`
pins each branch: `run` with and without a path, `repl`, help, `--verbose` anywhere in
`argv`, the unknown-command error and the version string. Below the runner, the
error-examples harness, the public samples gate and the sprint smoke tests call
`RunCommand`, and the REPL tests call `ReplCommand`.

The other subcommands do not exist yet; each lands in `CommandLineRunner` with its tests:
`grob install` and `grob restore` in Sprint 11, `grob fmt` and `grob check` in Sprint 12
(`grob-v1-requirements.md`). The validation scripts (D-346) are lexed and parsed in
`Grob.Compiler.Tests` and type-checked only for ordering and decorator diagnostics; they
are not executed, and not through the CLI.

**Size constraint:** `Program.cs` stays a single statement over the console streams. A
new command, flag or usage message goes into `CommandLineRunner`, never here: the
exclusion is safe only while this file has nothing to test.

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
    Justification = "Entry point only: binds Console.In, Console.Out and Console.Error to CommandLineRunner and returns its exit code. It has no branch; all argv parsing and dispatch is in CommandLineRunner, which is unit tested.")]
internal partial class Program {
    protected Program() { }
}
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

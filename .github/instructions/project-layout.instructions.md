---
applyTo: "**"
---

# Grob project layout

The Grob solution is a strict directed acyclic graph. The rules below
constrain every file you create, every `using` directive, every project
reference.

When in doubt, fetch `docs/design/grob-solution-architecture.md`. It is
the authoritative source.

## The seven projects

```
Grob.Core         <- foundation. Has no dependencies on any other Grob project.
Grob.Runtime      <- runtime value types and plugin interface. Depends on: Core.
Grob.Compiler     <- lexer, parser, type checker, emitter. Depends on: Core.
Grob.Vm           <- bytecode interpreter. Depends on: Core, Runtime.
Grob.Stdlib       <- standard library modules. Depends on: Core, Runtime.
Grob.Cli          <- the `grob` executable. Depends on: Core, Runtime,
                     Compiler, Vm, Stdlib.
Grob.Lsp          <- language server. Depends on: Core, Compiler.
```

## The seven test projects

One test project per production project. Same DAG, same naming:

```
Grob.Core.Tests        <- references Grob.Core
Grob.Runtime.Tests     <- references Grob.Runtime
Grob.Compiler.Tests    <- references Grob.Compiler
Grob.Vm.Tests          <- references Grob.Vm
Grob.Stdlib.Tests      <- references Grob.Stdlib
Grob.Cli.Tests         <- references Grob.Cli
Grob.Lsp.Tests         <- references Grob.Lsp
```

A test project references exactly one production project plus any test
support libraries. If you find yourself wanting to reference two production
projects from one test project, the production design is probably wrong —
surface it rather than papering over it.

Each production project declares
`[assembly: InternalsVisibleTo("Grob.<Project>.Tests")]` so internal types
are reachable from tests. See `csharp.instructions.md` for when to test
through internals vs the public surface.

## Hard rules

1. **`Grob.Compiler` and `Grob.Vm` never reference each other.** This is the
   single most important constraint in the solution. The compiler produces
   `.grobc` files; the VM consumes them. They share only what's in
   `Grob.Core`. If you find yourself wanting to add a reference between them,
   stop. The thing you want belongs in `Grob.Core`.
2. **`Grob.Core` references nothing.** No NuGet packages beyond what's
   included in the .NET 10 BCL. No other Grob projects. This is the shared
   foundation; it must stay neutral.
3. **`Grob.Lsp` does not reference `Grob.Vm`.** The language server is
   compile-time only. Runtime concerns stay out.
4. **No project references upward.** `Grob.Core` cannot reference
   `Grob.Compiler`. `Grob.Runtime` cannot reference `Grob.Stdlib`. The
   dependency arrows point down only.
5. **Test projects mirror the production projects.** `Grob.Core.Tests`
   references `Grob.Core` and no other production project (a test project
   can reference other test-support projects if those exist). If a test
   needs types from two production projects, the production design is
   probably wrong — surface it.

## Central package management

Shared dependencies — both production and test — are version-managed in
`Directory.Packages.props` at the solution root. Individual `.csproj` files
declare `<PackageReference Include="..." />` without a version; the version
is set centrally.

Shared test dependencies that every `*.Tests` project references:

- `xunit` — test framework.
- `xunit.runner.visualstudio` — test runner integration.
- `FluentAssertions` — assertion library.
- `FsCheck.Xunit` — property-based testing, in from day one.
- `coverlet.collector` — coverage measurement for the 90% line-coverage bar.
- `Microsoft.NET.Test.Sdk` — test SDK.

These belong in `Directory.Build.props` (under a condition that targets
`*.Tests` projects only) so every test project picks them up automatically
without per-project boilerplate.

## What goes where

| Concern                                                                                 | Project                                                                            |
| --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `SourceLocation`, `Diagnostic`, `GrobError` hierarchy                                   | `Grob.Core`                                                                        |
| AST node types                                                                          | `Grob.Core`                                                                        |
| `GrobValue` and its variants                                                            | `Grob.Core`                                                                        |
| `GrobType` (the runtime type-tag enum and metadata)                                     | `Grob.Core`                                                                        |
| Lexer, parser, type checker, emitter                                                    | `Grob.Compiler`                                                                    |
| Bytecode interpreter, call stack, value stack                                           | `Grob.Vm`                                                                          |
| `.grobc` binary format reader and writer                                                | Split: writer in `Grob.Compiler`, reader in `Grob.Vm`, format types in `Grob.Core` |
| `IGrobPlugin` interface and plugin host                                                 | `Grob.Runtime`                                                                     |
| `fs`, `strings`, `json`, `process`, etc.                                                | `Grob.Stdlib`                                                                      |
| `grob` command-line entry, argument parsing                                             | `Grob.Cli`                                                                         |
| LSP protocol handling                                                                   | `Grob.Lsp`                                                                         |
| Ambient dependency abstractions (`IClock`, `IFileSystem`, `IConsole`, `IProcessRunner`) | `Grob.Core` (interface) + `Grob.Cli` (concrete)                                    |

## When you create a new file

Before writing the namespace, ask:

1. Which project does this type belong in?
2. Does that project have access to everything this type needs?
3. If not, can the missing thing move to `Grob.Core`, or does the type
   itself belong in a different project?

If you can't answer these confidently, the file isn't ready to write yet —
propose the design first.

## Namespaces follow projects

`Grob.Core/Diagnostics/Diagnostic.cs` declares
`namespace Grob.Core.Diagnostics;`. The namespace matches the project name
and folder path exactly. No exceptions.

This rule includes **every** subfolder, not just the project root. A file at
`Grob.Compiler/Ast/Expressions/BinaryExpr.cs` declares
`namespace Grob.Compiler.Ast.Expressions;` — not `Grob.Compiler.Ast;`.
When you create a new file or move a file into a subfolder, the namespace
on line 1 must mirror the full folder path under `src/` or `tests/`. If
sibling files in the same folder disagree, fix them; never copy the wrong
namespace from a neighbour.

If callers in the parent namespace would otherwise need a `using` for every
new subfolder, add a single `GlobalUsings.cs` at the project root rather
than weakening the per-file rule.

## `using Grob.Vm` in compiler code

This is a smell. If you find yourself adding `using Grob.Vm;` in a
`Grob.Compiler` file (or vice versa), you have almost certainly violated the
DAG. Stop and propose a redesign.

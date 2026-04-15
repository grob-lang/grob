# Grob Wiki

*A statically typed scripting language that a hobbyist can learn and a developer can trust.*

Welcome to the Grob specification and reference documentation. This wiki is the
authoritative source for language behaviour, standard library APIs, VM architecture
and tooling.

## Language Specification

The core language — syntax, types, control flow, operators, functions, error
handling, script parameters and the module system.

- [Syntax](Language-Specification/Syntax.md)
- [Types](Language-Specification/Types.md)
- [Control Flow](Language-Specification/Control-Flow.md)
- [Functions](Language-Specification/Functions.md)
- [Operators](Language-Specification/Operators.md)
- [Expressions](Language-Specification/Expressions.md)
- [Error Handling](Language-Specification/Error-Handling.md)
- [Script Parameters](Language-Specification/Script-Parameters.md)
- [Modules and Imports](Language-Specification/Modules-and-Imports.md)

## Standard Library

Thirteen core modules, auto-available in every script. No `import` required.

- [fs](Standard-Library/fs.md) — file system operations
- [strings](Standard-Library/strings.md) — string utilities
- [json](Standard-Library/json.md) — JSON parse and serialise
- [csv](Standard-Library/csv.md) — CSV parse and serialise
- [env](Standard-Library/env.md) — environment variables
- [process](Standard-Library/process.md) — external command execution
- [date](Standard-Library/date.md) — date and time
- [math](Standard-Library/math.md) — mathematics
- [log](Standard-Library/log.md) — structured logging
- [regex](Standard-Library/regex.md) — regular expressions
- [path](Standard-Library/path.md) — path string manipulation
- [format](Standard-Library/format.md) — output formatting
- [guid](Standard-Library/guid.md) — GUID generation and parsing

## Type Registry

Built-in type members — the compiler and type checker reference for what methods
and properties exist on each type.

- [string](Type-Registry/string.md)
- [int](Type-Registry/int.md)
- [float](Type-Registry/float.md)
- [bool](Type-Registry/bool.md)
- [array](Type-Registry/array.md)
- [map](Type-Registry/map.md)
- [File](Type-Registry/File.md)
- [date](Type-Registry/date.md)
- [Regex](Type-Registry/Regex.md)
- [Match](Type-Registry/Match.md)
- [guid](Type-Registry/guid.md)
- [CsvTable](Type-Registry/CsvTable.md)
- [CsvRow](Type-Registry/CsvRow.md)
- [json.Node](Type-Registry/json-Node.md)
- [ProcessResult](Type-Registry/ProcessResult.md)
- [Response](Type-Registry/Response.md)
- [AuthHeader](Type-Registry/AuthHeader.md)
- [ZipEntry](Type-Registry/ZipEntry.md)

## VM Architecture

The bytecode VM design — instruction set, value representation, call frames
and GC strategy.

- [Overview](VM-Architecture/Overview.md)
- [Instruction Set](VM-Architecture/Instruction-Set.md)
- [Value Representation](VM-Architecture/Value-Representation.md)
- [Call Frames](VM-Architecture/Call-Frames.md)
- [GC Strategy](VM-Architecture/GC-Strategy.md)

## CLI

Commands, install strategy and error message design.

- [Commands](CLI/Commands.md)
- [Install Strategy](CLI/Install-Strategy.md)
- [Error Messages](CLI/Error-Messages.md)

## Plugins

The plugin ecosystem — overview, authoring guide, official plugins and community
registry.

- [Overview](Plugins/Overview.md)
- [Writing Plugins](Plugins/Writing-Plugins.md)
- [Grob.Http](Plugins/Grob-Http.md)
- [Grob.Crypto](Plugins/Grob-Crypto.md)
- [Grob.Zip](Plugins/Grob-Zip.md)
- [Community Registry](Plugins/Community-Registry.md)

## Architecture Decision Records

Key design decisions with full rationale.

- [ADR-0001: Static Typing with Inference](ADR/0001-static-typing-with-inference.md)
- [ADR-0002: Bytecode VM over Tree-Walking](ADR/0002-bytecode-vm-over-tree-walking.md)
- [ADR-0003: Exceptions over Error Values](ADR/0003-exceptions-over-error-values.md)
- [ADR-0004: Constrained Generics](ADR/0004-constrained-generics.md)
- [ADR-0005: C# GC over Custom](ADR/0005-csharp-gc-over-custom.md)
- [ADR-0006: No Pipe Operator](ADR/0006-no-pipe-operator.md)
- [ADR-0007: Solution Structure and Naming](ADR/0007-solution-structure-and-naming.md)
- [ADR-0008: Windows-First Design](ADR/0008-windows-first-design.md)
- [ADR-0009: No Var Keyword](ADR/0009-no-var-keyword.md)
- [ADR-0010: Select Statement vs Switch Expression](ADR/0010-select-statement-vs-switch-expression.md)
- [ADR-0011: Core Modules Auto-Available](ADR/0011-core-modules-auto-available.md)
- [ADR-0012: NuGet as Plugin Registry](ADR/0012-nuget-as-plugin-registry.md)
- [ADR-0013: Three-Tier Install Scope](ADR/0013-three-tier-install-scope.md)
- [ADR-0014: Source Locations from Day One](ADR/0014-source-locations-from-day-one.md)
- [ADR-0015: Bare Brace Rule](ADR/0015-bare-brace-rule.md)
- [ADR-0016: Const Means Fully Immutable](ADR/0016-const-means-fully-immutable.md)

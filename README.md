# Grob

[![CI](https://github.com/grob-lang/grob/actions/workflows/ci.yml/badge.svg)](https://github.com/grob-lang/grob/actions/workflows/ci.yml)
[![Release](https://github.com/grob-lang/grob/actions/workflows/release.yml/badge.svg)](https://github.com/grob-lang/grob/actions/workflows/release.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=grob-lang_grob&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=grob-lang_grob)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=grob-lang_grob&metric=coverage)](https://sonarcloud.io/summary/new_code?id=grob-lang_grob)
![Status](https://img.shields.io/badge/status-pre--release-orange)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

_A statically typed scripting language that a hobbyist can learn and a developer can trust._

---

Grob is a statically typed scripting language with C-style syntax, type inference
and first-class file system operations. Nullable types are explicit. Immutability
is opt-in via `const`. It is designed to be readable by any C# or Go developer
without prior knowledge of Grob.

```grob
param dir:  string = "C:\\Reports"
param from: string
param to:   string

for file in fs.list(dir) {
    if (file.name.contains(from)) {
        new_name := file.name.replace(from, to)
        file.rename(new_name)
        print("Renamed: ${file.name} → ${new_name}")
    }
}
```

## The Gap

Go is too ceremonious for scripts. PowerShell's syntax is hostile. Python is
dynamically typed and increasingly clunky at scale. bash is cryptic and
inconsistent. Nobody has solved statically typed, low-ceremony, genuinely
readable scripting cleanly. Grob is designed to fill that gap.

## Status

Grob is being built in the open. The language design is complete — implementation
has not yet begun. Sprint 1 begins after the solution skeleton is finalised.

What exists today:

- Full language specification covering syntax, types, control flow, operators,
  functions, error handling, script parameters and modules
- Thirteen core standard library modules, fully specified
- Built-in type method registry — compiler and type checker reference
- VM architecture design — stack-based bytecode VM in C# .NET
- Plugin ecosystem model with NuGet as the package registry
- Install strategy — three-tier scope model, `winget` delivery
- CLI personality and error message design
- Mascot (Sparky) — character sheet v1 complete

What comes next: implementation, in the order laid out in the
[VM Architecture](docs/wiki/VM-Architecture/Overview.md) documentation.

**This is a real project with serious design behind it. It is not ready yet.**
Watch the repo if you want to follow along.

## Documentation

The full specification lives in the [wiki](docs/wiki/Home.md):

- [Language Specification](docs/wiki/Language-Specification/Syntax.md) — syntax,
  types, control flow, operators, expressions
- [Standard Library](docs/wiki/Standard-Library/fs.md) — all thirteen core modules
- [Type Registry](docs/wiki/Type-Registry/string.md) — built-in type members
- [VM Architecture](docs/wiki/VM-Architecture/Overview.md) — bytecode VM design
- [CLI](docs/wiki/CLI/Commands.md) — commands, install strategy, error messages
- [Plugins](docs/wiki/Plugins/Overview.md) — ecosystem, authoring, registry
- [ADRs](docs/wiki/ADR/0001-static-typing-with-inference.md) — architecture
  decision records

## Sparky

Sparky is Grob's mascot — a raccoon in a blue hoodie with a utility belt and
wrench. Character sheet v1 is complete. Commissioned illustration to follow when
the project is public.

The `G>` logo mark is shared between Sparky and the REPL prompt. That is not a
coincidence.

## Licence

MIT — see [LICENSE](LICENSE).

---

_Grob is a hobby project but it is not a toy._

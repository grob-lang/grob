# Grob — Decisions & Context Log

> Authoritative record of design decisions, open questions, and context.
> All entries are dated. Nothing undated is a firm decision.
> This document is updated after every design session.
> Source of truth when the brainstorm doc and VM doc conflict.

---

## Project Status

| Milestone                        | Status                                      |
| -------------------------------- | ------------------------------------------- |
| SharpBASIC complete              | ✅ Done                                     |
| SharpBASIC retrospective written | ✅ Done — April 2026                        |
| clox worked through (Ch 14–30)   | ✅ Done — May 2026, NaN boxing included     |
| Grob formal design phase begun   | ✅ Done — this document                     |
| Grob Claude Project created      | ✅ Done — April 2026                        |
| Mascot designed (Sparky)         | ✅ Done — character sheet v1 complete       |
| Personality & identity locked    | ✅ Done — see grob-personality-identity.md  |
| Licensing model decided          | ✅ Done — MIT                               |
| Language fundamentals specified  | ✅ Done — April 2026                        |
| Plugin ecosystem model decided   | ✅ Done — see CONTRIBUTING.md, PLUGINS.md   |
| Solution architecture locked     | ✅ Done — April 2026                        |
| v1 requirements specified        | ✅ Done — April 2026                        |
| Tooling strategy defined         | ✅ Done — April 2026                        |
| MVP defined and scoped           | ✅ Done — see grob-v1-requirements.md       |
| Implementation started           | 🔄 In progress — Sprints 1–4 complete; Sprint 4→5 interlude |

---

## Identity

**Name:** Grob — childhood nickname, no semantic meaning, good name for a language.

**Foundational principle — April 2026, stated explicitly:**

> Grob is a hobby project but it is not a toy. Learning is a byproduct, not the
> primary purpose. Grob is a serious attempt to build a genuine scripting language.
> It should be designed, documented, and built with that in mind.

This principle governs every decision. When there is a choice between the approach
that teaches more and the approach that produces a better language — choose the
better language. The learning will follow.

**The design target:** Grob should be able to stand next to Go, PowerShell, and
Python as a credible answer to the question “what should I use for this scripting
task?” Each has real, well-documented weaknesses in the scripting space. Go is too
ceremonious for scripts. PowerShell’s syntax is hostile. Python is dynamically typed
and increasingly clunky at scale. Nobody has solved this cleanly. That gap is what
Grob is designed to fill. The origin as a hobby project is irrelevant to whether it
can. The design decisions are what matter.

**One-line statement:**

> _A statically typed scripting language that a hobbyist can learn and a developer can trust._

**Identity statement (full):**

> _Grob is a statically typed scripting language with C-style syntax, type inference,
> and first-class file system operations. Nullable types are explicit. Immutability
> is opt-in via `const`. It’s designed to be readable by any C# or Go developer
> without prior knowledge of Grob._

**What Grob is NOT:**

- Not Python — dynamically typed, whitespace-significant, clunky for scripting
- Not PowerShell — powerful but syntactically hostile
- Not bash — cryptic, inconsistent
- Not Rust — too steep a learning curve for hobbyists
- Not a general-purpose application language — scripting first

**The gap Grob fills:**
Nobody has nailed statically typed, low-ceremony, genuinely readable scripting. Go comes
closest but was designed for services. PowerShell and bash own the sysadmin space through
ubiquity not quality. Python owns education but is dynamically typed. Grob targets that gap.

---

## Confirmed Decisions — Summary Index

| D-### | Date                                                              | Area                          | Summary                                                                                                                                                          |
| ----- | ----------------------------------------------------------------- | ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| D-001 | Feb 2026                                                          | Targeting                     | Arduino hardware targeting ruled out                                                                                                                             |
| D-002 | Feb 2026                                                          | Purpose                       | General-purpose scripting chosen over DSL                                                                                                                        |
| D-003 | Feb 2026                                                          | MVP                           | Console calculator as MVP success criterion                                                                                                                      |
| D-004 | Feb 2026                                                          | Modules                       | Module/import system in scope, late phase                                                                                                                        |
| D-005 | Feb 2026                                                          | Philosophy                    | “Build for developers, design for hobbyists”                                                                                                                     |
| D-006 | Feb 2026                                                          | VM strategy                   | Stack-based bytecode VM, informed by clox                                                                                                                        |
| D-007 | Feb 2026                                                          | Implementation                | Written in C# .NET                                                                                                                                               |
| D-008 | Feb 2026                                                          | Syntax                        | Same-line braces                                                                                                                                                 |
| D-009 | Feb 2026                                                          | Syntax                        | No semicolons                                                                                                                                                    |
| D-010 | Feb 2026                                                          | Syntax                        | `//` comments                                                                                                                                                    |
| D-011 | Feb 2026                                                          | Variables                     | `:=` declares; `=` reassigns; no `var`                                                                                                                           |
| D-012 | Feb 2026                                                          | Variables                     | No uninitialised variables                                                                                                                                       |
| D-013 | Feb 2026                                                          | Variables                     | Mutable by default; `const` for immutable _(partially superseded by D-288)_                                                                                      |
| D-014 | Feb 2026                                                          | Types                         | `?` suffix for nullable types                                                                                                                                    |
| D-015 | Feb 2026                                                          | Types                         | `??` nil coalescing; `?.` optional chaining                                                                                                                      |
| D-016 | Feb 2026                                                          | Functions                     | `fn` keyword; typed parameters; explicit return type                                                                                                             |
| D-017 | Feb 2026                                                          | GC                            | Lean on C#’s GC; structs for value types                                                                                                                         |
| D-018 | Feb 2026                                                          | Plugin system                 | Stdlib implemented as `IGrobPlugin`                                                                                                                              |
| D-019 | Feb 2026                                                          | Plugin system                 | Type safety enforced at plugin boundary                                                                                                                          |
| D-020 | Feb 2026                                                          | Bytecode format               | `.grobc` binary format; magic number `GROB`                                                                                                                      |
| D-021 | Feb 2026                                                          | Execution model               | Primary use: compile in-memory and run                                                                                                                           |
| D-022 | Feb 2026                                                          | Fluent syntax                 | Fluent chaining yes — requires collections API first                                                                                                             |
| D-023 | Feb 2026                                                          | Collections                   | C# LINQ as design north star                                                                                                                                     |
| D-024 | Feb 2026                                                          | Release                       | Open source; release when core is solid                                                                                                                          |
| D-025 | Apr 2026                                                          | Plugin loading                | `--plugin` retired from public API; `--dev-plugin` for dev only                                                                                                  |
| D-026 | Apr 2026                                                          | Import statement              | `import` is the single non-core dependency mechanism                                                                                                             |
| D-027 | Apr 2026                                                          | Core modules                  | 13 core modules auto-available, no import required                                                                                                               |
| D-028 | Apr 2026                                                          | Import signal value           | Scripts with no imports are self-contained                                                                                                                       |
| D-029 | Apr 2026                                                          | Plugin import alias           | Default alias is last segment lowercased                                                                                                                         |
| D-030 | Apr 2026                                                          | Explicit alias                | `import X as y` for collision resolution only                                                                                                                    |
| D-031 | Apr 2026                                                          | Import vs requires            | `import` chosen over `requires`                                                                                                                                  |
| D-032 | Apr 2026                                                          | Package install               | `grob install`; never silently downloads at runtime                                                                                                              |
| D-033 | Apr 2026                                                          | Package resolution            | Check `grob.json` then `~/.grob/packages/`; compile error if missing                                                                                             |
| D-034 | Apr 2026                                                          | Project manifest              | `grob.json` for multi-script projects                                                                                                                            |
| D-035 | Apr 2026                                                          | Package registry              | NuGet; tagged `grob-plugin`                                                                                                                                      |
| D-036 | Apr 2026                                                          | grob.json shape               | npm-influenced; semantic versioning; `^` for compatible                                                                                                          |
| D-037 | Apr 2026                                                          | AST pattern                   | Visitor pattern for three-pass AST                                                                                                                               |
| D-038 | Apr 2026                                                          | Scope                         | `:=` declares in current scope; `=` walks parent chain                                                                                                           |
| D-039 | Apr 2026                                                          | Error strategy                | Compiler collects all errors; VM stops on first unhandled runtime error                                                                                          |
| D-040 | Apr 2026                                                          | Compiler tests                | Test compiler outputs exhaustively                                                                                                                               |
| D-041 | Apr 2026                                                          | Partial classes               | Compiler as `partial class` files                                                                                                                                |
| D-042 | Apr 2026                                                          | Real program target           | Real-program target required before implementation begins                                                                                                        |
| D-043 | Apr 2026                                                          | OQ-002 resolved               | User-defined struct types confirmed; `type` keyword                                                                                                              |
| D-044 | Apr 2026                                                          | Semantic analyser             | No empty placeholder; type-checker pass is the semantic analyser                                                                                                 |
| D-045 | Apr 2026                                                          | Use cases                     | Real-world targets: Azure CLI, ADO, agent hooks                                                                                                                  |
| D-046 | Apr 2026                                                          | Pipeline model                | File-read primary; stdin/stdout for pipeline composition                                                                                                         |
| D-047 | Apr 2026                                                          | String interpolation          | `"Hello ${name}"` confirmed load-bearing                                                                                                                         |
| D-048 | Apr 2026                                                          | Licensing                     | MIT licence                                                                                                                                                      |
| D-049 | Apr 2026                                                          | Open source model             | Core in main repo; first-party plugins in `plugins/`                                                                                                             |
| D-050 | Apr 2026                                                          | Community plugins             | Independent repos; registry via `PLUGINS.md` PR                                                                                                                  |
| D-051 | Apr 2026                                                          | Plugin SDK                    | `Grob.Runtime` NuGet package; versioned independently                                                                                                            |
| D-052 | Apr 2026                                                          | Contributions                 | Fork → branch → PR; CLA on first PR                                                                                                                              |
| D-053 | Apr 2026                                                          | Mascot                        | Sparky — raccoon, blue hoodie, utility belt, wrench                                                                                                              |
| D-054 | Apr 2026                                                          | Logo mark                     | `G>` — forward chevron on G                                                                                                                                      |
| D-055 | Apr 2026                                                          | REPL prompt                   | `G>` matches logo mark                                                                                                                                           |
| D-056 | Apr 2026                                                          | Windows Terminal              | Grob ships a Windows Terminal profile                                                                                                                            |
| D-057 | Apr 2026                                                          | Terminal colours              | Denim blue, warm amber, raccoon greys                                                                                                                            |
| D-058 | Apr 2026                                                          | Personality                   | Three modes: seasoned engineer, enthusiastic teacher, scrappy builder                                                                                            |
| D-059 | Apr 2026                                                          | Error messages                | Helpful — what, where, why, suggested fix                                                                                                                        |
| D-060 | Apr 2026                                                          | First run                     | `✦ First script. Nice work.` Celebrated once, never repeated                                                                                                     |
| D-061 | Apr 2026                                                          | Opinions                      | `snake_case` warned not errored; nil safety is non-negotiable _(snake_case warning superseded by D-283)_                                                         |
| D-062 | Apr 2026                                                          | Formatter                     | `grob fmt`; never automatic, always opt-in                                                                                                                       |
| D-063 | Apr 2026                                                          | CLI output                    | Quiet on success; errors to stderr; results to stdout                                                                                                            |
| D-064 | Apr 2026                                                          | Never list                    | No emoji in CLI output; never “simply” in docs                                                                                                                   |
| D-065 | Apr 2026                                                          | AI tutor                      | Deferred idea; parked in grob-personality-identity.md                                                                                                            |
| D-066 | Apr 2026                                                          | Primitive model               | Primitives never boxed; method-call syntax is compile-time sugar                                                                                                 |
| D-067 | Apr 2026                                                          | Method syntax                 | All types support method-call syntax                                                                                                                             |
| D-068 | Apr 2026                                                          | Properties vs methods         | `length`, `isEmpty` etc. are properties — no `()`                                                                                                                |
| D-069 | Apr 2026                                                          | Conversion rule               | Conversions are methods on the source type                                                                                                                       |
| D-070 | Apr 2026                                                          | Static utilities              | Functions with no receiver live on the type namespace                                                                                                            |
| D-071 | Apr 2026                                                          | One rule                      | Conversions on source value; static utilities on type namespace                                                                                                  |
| D-072 | Apr 2026                                                          | Security posture              | Trust script author; document risks; safe path is obvious path                                                                                                   |
| D-073 | Apr 2026                                                          | Plugin security               | Loading a plugin is running arbitrary code; documented prominently                                                                                               |
| D-074 | Apr 2026                                                          | Credential handling           | `env.require()` is the canonical credential pattern                                                                                                              |
| D-075 | Apr 2026                                                          | process module naming         | `process.run()` safe form; `process.runShell()` shell form; supersedes D-076                                                                                     |
| D-076 | Apr 2026                                                          | process.runArgs (retired)     | `process.runArgs()` naming — superseded by D-075                                                                                                                 |
| D-077 | Apr 2026                                                          | Errors — no values            | Error messages show names and types, never values                                                                                                                |
| D-078 | Apr 2026                                                          | Community registry            | PLUGINS.md is not a safety endorsement                                                                                                                           |
| D-079 | Apr 2026                                                          | Type method registry          | Defined method set per type; undefined method = compile error                                                                                                    |
| D-080 | Apr 2026                                                          | OQ-001 resolved               | Constrained generics — users consume, cannot declare                                                                                                             |
| D-081 | Apr 2026                                                          | Generics — plugin boundary    | Generic functions at plugin boundary via `FunctionSignature`                                                                                                     |
| D-082 | Apr 2026                                                          | OQ-004 resolved               | Exceptions as runtime error model; `try/catch`                                                                                                                   |
| D-083 | Apr 2026                                                          | try/catch                     | Multiple typed catches; bare `catch e` must appear last                                                                                                          |
| D-084 | Apr 2026                                                          | Exception hierarchy           | `GrobError` root; `IoError`, `NetworkError`, `JsonError` etc. _(superseded by D-284)_                                                                            |
| D-085 | Apr 2026                                                          | User-defined exceptions       | Post-MVP                                                                                                                                                         |
| D-086 | Apr 2026                                                          | csv module                    | Core stdlib; headers assumed by default; RFC 4180                                                                                                                |
| D-087 | Apr 2026                                                          | Named parameters              | Named parameters confirmed; only specify params differing from defaults                                                                                          |
| D-088 | Apr 2026                                                          | log module                    | Core stdlib; distinct output streams; for unattended scripts                                                                                                     |
| D-089 | Apr 2026                                                          | regex module                  | Core stdlib; regex literals `/pattern/flags`                                                                                                                     |
| D-090 | Apr 2026                                                          | path module                   | Core stdlib; path string manipulation, no I/O                                                                                                                    |
| D-091 | Apr 2026                                                          | strings module                | `strings.join()` on module; all other ops as instance methods                                                                                                    |
| D-092 | Apr 2026                                                          | csv module full API           | Full signatures locked                                                                                                                                           |
| D-093 | Apr 2026                                                          | math module full API          | Constants, trig, random; no duplication of type-level functions                                                                                                  |
| D-094 | Apr 2026                                                          | log module full API           | Four levels; all to stderr; `log.setLevel()`                                                                                                                     |
| D-095 | Apr 2026                                                          | regex module full API         | Regex literals; `Regex` type; `Match` type; module convenience fns                                                                                               |
| D-096 | Apr 2026                                                          | path module full API          | Full function set; `path.separator` constant; no I/O                                                                                                             |
| D-097 | Apr 2026                                                          | First-party plugins           | `Grob.Crypto` and `Grob.Zip` in `plugins/`                                                                                                                       |
| D-098 | Apr 2026                                                          | Script parameters             | `param` block; typed, defaultable; validated at compile time                                                                                                     |
| D-099 | Apr 2026                                                          | Param files                   | `.grobparams` key-value format; committable; readable                                                                                                            |
| D-100 | Apr 2026                                                          | Param override                | CLI overrides param file values                                                                                                                                  |
| D-101 | Apr 2026                                                          | @secure decorator             | Handling instruction, not a type; not echoed or logged                                                                                                           |
| D-102 | Apr 2026                                                          | Param decorators              | V1 set: `@secure`, `@allowed`, `@minLength`, `@maxLength`                                                                                                        |
| D-103 | Apr 2026                                                          | Secure param pattern          | `@secure` params absent from `.grobparams`; supply via CLI or `env`                                                                                              |
| D-104 | Apr 2026                                                          | Pipe operator                 | No `                                                                                                                                                             | ` pipe in Grob scripts; fluent chaining is the idiom |
| D-105 | Apr 2026                                                          | format module                 | Core stdlib; human-readable output; `format.table()`, `format.list()`                                                                                            |
| D-106 | Apr 2026                                                          | select() projection           | `.select()` on collections; typed; PowerShell `Select-Object` equivalent                                                                                         |
| D-107 | Apr 2026                                                          | date module — type            | Single `date` type; no separate `datetime`                                                                                                                       |
| D-108 | Apr 2026                                                          | date module — API             | Full API locked                                                                                                                                                  |
| D-109 | Apr 2026                                                          | fs module API shape           | `fs.list()` returns `File[]`; `File` built-in type; full function set                                                                                            |
| D-110 | Apr 2026                                                          | Script exit                   | `exit(n)` built-in; uncatchable `ExitSignal`                                                                                                                     |
| D-111 | Apr 2026                                                          | Conditional expressions       | Ternary `? :` and switch expression; exhaustiveness enforced                                                                                                     |
| D-112 | Apr 2026                                                          | Array indexing                | `arr[n]`; zero-based; multi-dimensional `matrix[r][c]`                                                                                                           |
| D-113 | Apr 2026                                                          | Named parameter convention    | Positional first; named after; only defaultable params may be named                                                                                              |
| D-114 | Apr 2026                                                          | Anonymous struct literals     | `#{ field: value }` syntax; structurally typed; field access safe                                                                                                |
| D-115 | Apr 2026                                                          | Lambdas and closures          | `x => expr`, `(a,b) => expr`, block form; upvalue mechanism from clox                                                                                            |
| D-116 | Apr 2026                                                          | format module calling conv    | `.format.table()` chained form; compiler namespace rewrite; no boxing                                                                                            |
| D-117 | Apr 2026                                                          | date interval computation     | `daysUntil()` and `daysSince()` added; `Interval` type post-MVP                                                                                                  |
| D-118 | Apr 2026                                                          | Grob.Http API shape           | Full REST; `Response` type; `auth` sub-namespace                                                                                                                 |
| D-119 | Apr 2026                                                          | string left() and right()     | `left(n)` and `right(n)` added; range indexing post-MVP                                                                                                          |
| D-120 | Apr 2026                                                          | Language fundamentals         | Full spec in grob-language-fundamentals.md; decisions log wins on conflict                                                                                       |
| D-121 | Apr 2026                                                          | Install scope model           | Three-tier: user-global, system, project-local                                                                                                                   |
| D-122 | Apr 2026                                                          | grob.json manifest walk       | Walk up from script file location, not CWD                                                                                                                       |
| D-123 | Apr 2026                                                          | grob runtime install          | `winget install Grob.Grob`; `grob restore` idempotent                                                                                                            |
| D-124 | Apr 2026                                                          | Nested struct field access    | Full chain resolution at compile time; undefined field = compile error                                                                                           |
| D-125 | Apr 2026                                                          | Solution structure            | Six `src/` assemblies; three `plugins/`; five `tests/`; DAG dependency                                                                                           |
| D-126 | Apr 2026                                                          | Type naming convention        | `Grob` prefix full — not `Gro`; ADR-0012                                                                                                                         |
| D-127 | Apr 2026                                                          | String literal forms          | Three forms: double-quoted, single backtick, triple backtick                                                                                                     |
| D-128 | Apr 2026                                                          | Raw string newline rule       | Newline inside single backtick string is compile error                                                                                                           |
| D-129 | Apr 2026                                                          | Raw string indentation        | Triple backtick verbatim; no trimming in v1                                                                                                                      |
| D-130 | Apr 2026                                                          | Escape sequence set           | `\n`, `\r`, `\t`, `\\`, `\"`, `\$`; unknown = compile error                                                                                                      |
| D-131 | Apr 2026                                                          | Namespace conventions         | Gerunds or adjectives; never same word as primary class                                                                                                          |
| D-132 | Apr 2026                                                          | Tooling — language-config     | `language-configuration.json` in Phase 1 with TextMate grammar                                                                                                   |
| D-133 | Apr 2026                                                          | Tooling — TextMate grammar    | First tooling deliverable; no compiler dependency                                                                                                                |
| D-134 | Apr 2026                                                          | Tooling — Grob.Lsp            | `Grob.Lsp` in solution; depends on Compiler/Core/Runtime, not Vm                                                                                                 |
| D-135 | Apr 2026                                                          | Tooling — VS Code extension   | `tooling/Grob.VsCode/`; TypeScript; ~30 lines                                                                                                                    |
| D-136 | Apr 2026                                                          | Tooling — LSP handler order   | Diagnostics, completions, hover, go-to-definition; semantic tokens post-MVP                                                                                      |
| D-137 | Apr 2026                                                          | Compiler SourceLocation       | Every AST node carries `SourceLocation`; day-one requirement                                                                                                     |
| D-138 | Apr 2026                                                          | v1 Requirements Spec          | Full build spec in grob-v1-requirements.md                                                                                                                       |
| D-139 | Apr 2026                                                          | input() built-in              | `input(prompt): string`; blocks on stdin; throws `IoError` on EOF                                                                                                |
| D-140 | Apr 2026                                                          | Array mutation methods        | `append`, `insert`, `remove`, `clear`; mutation on `const` = compile error                                                                                       |
| D-141 | Apr 2026                                                          | map<K, V> type                | First-class; string keys in v1; insertion order preserved                                                                                                        |
| D-142 | Apr 2026                                                          | OQ-009 opened                 | `GrobValue` provisional — tagged union, documented as provisional                                                                                                |
| D-143 | Apr 2026                                                          | OQ-010 opened                 | `.grobc` binary format spec needed before implementation                                                                                                         |
| D-144 | Apr 2026                                                          | OQ-011 opened                 | `Grob.Crypto` API shape — defer to Sprint 10 planning                                                                                                            |
| D-145 | Apr 2026                                                          | OQ-012 opened                 | `process.run()` timeout — defer to Sprint 9                                                                                                                      |
| D-146 | Apr 2026                                                          | OQ-007 resolved               | `for...in` special-cased: ranges, arrays, maps; formal protocol post-MVP                                                                                         |
| D-147 | Apr 2026                                                          | OQ-012 resolved               | `process.run()` timeout: `timeout: int = 0`; throws `ProcessError`                                                                                               |
| D-148 | Apr 2026                                                          | OQ-011 resolved               | `Grob.Crypto` API shape resolved; stream-based file hashing                                                                                                      |
| D-149 | Apr 2026                                                          | guid module                   | Core stdlib; `guid` primitive type; `newV4`, `newV7`, `newV5`                                                                                                    |
| D-150 | Apr 2026                                                          | fs.copy/fs.move overwrite     | `overwrite: bool = false` on copy/move functions and instance methods                                                                                            |
| D-151 | Apr 2026                                                          | Script 11 validation          | Azure Resource Provisioning Helper added to validation suite                                                                                                     |
| D-152 | Apr 2026                                                          | Grob.Zip API shape            | Three `zip.create()` overloads; `zip.extract`; `zip.list`; `ZipEntry`                                                                                            |
| D-153 | Apr 2026                                                          | env module full API           | `get`, `require`, `set`, `has`, `all`                                                                                                                            |
| D-154 | Apr 2026                                                          | format module full API        | Returns `string`; `format.table`, `format.list`, `format.csv`; auto-sizing                                                                                       |
| D-155 | Apr 2026                                                          | Grob.Http locked signatures   | Full HTTP verb signatures; `http.download` throws on non-2xx                                                                                                     |
| D-156 | Apr 2026                                                          | json.encode() added           | Serialises any typed value to JSON string                                                                                                                        |
| D-157 | Apr 2026                                                          | json.Node full spec           | `node["key"]` indexer; accessors; type predicates                                                                                                                |
| D-158 | Apr 2026                                                          | Response type full spec       | `statusCode`, `isSuccess`, `headers`, `asText()`, `asJson()`                                                                                                     |
| D-159 | Apr 2026                                                          | AuthHeader type full spec     | Opaque; `toString()` returns `"[AuthHeader]"`                                                                                                                    |
| D-160 | Apr 2026                                                          | ProcessResult type full spec  | `stdout`, `stderr`, `exitCode`; `toString()` returns stdout                                                                                                      |
| D-161 | Apr 2026                                                          | Escape sequence set updated   | `\r` added; full set confirmed                                                                                                                                   |
| D-162 | Apr 2026                                                          | Numeric type precision        | `int` = 64-bit signed; `float` = 64-bit IEEE 754                                                                                                                 |
| D-163 | Apr 2026                                                          | Integer overflow              | Checked arithmetic; overflow throws `RuntimeError`                                                                                                               |
| D-164 | Apr 2026                                                          | Implicit type coercion        | Only `int` → `float`; all else explicit                                                                                                                          |
| D-165 | Apr 2026                                                          | Trailing commas               | Permitted in all comma-separated lists; never required                                                                                                           |
| D-166 | Apr 2026                                                          | Forward references            | Two-pass type checker; forward refs between top-level declarations _(extended by D-286)_                                                                         |
| D-167 | Apr 2026                                                          | Variable shadowing            | Allowed; compiler emits warning                                                                                                                                  |
| D-168 | Apr 2026                                                          | Script structure order        | import → param → type/fn → code; violations are compile errors                                                                                                   |
| D-169 | Apr 2026                                                          | Equality semantics            | Value equality throughout; struct field-by-field; `==` on incompatible types = compile error                                                                     |
| D-170 | Apr 2026                                                          | Nil chain propagation         | `?.` short-circuits entire chain; result type always `T?`                                                                                                        |
| D-171 | Apr 2026                                                          | Script-level return           | `return` at top level is compile error; use `exit()`                                                                                                             |
| D-172 | Apr 2026                                                          | No multiple return values     | Functions return single value; use struct for multiple                                                                                                           |
| D-173 | Apr 2026                                                          | No operator overloading       | User-defined types cannot define custom operators                                                                                                                |
| D-174 | Apr 2026                                                          | No circular imports           | Scripts cannot import other scripts in v1                                                                                                                        |
| D-175 | Apr 2026                                                          | json.write pretty default     | Pretty-printed by default; `compact: bool = false` on all three fns                                                                                              |
| D-176 | Apr 2026                                                          | date constructors local time  | `now()`, `today()`, `of()`, `ofTime()` return local time                                                                                                         |
| D-177 | Apr 2026                                                          | fs.readText UTF-8 default     | UTF-8; BOM auto-detection; `writeText` writes without BOM                                                                                                        |
| D-178 | Apr 2026                                                          | Map literal separator rules   | Entries separated by newlines or commas; keys are string literals in v1                                                                                          |
| D-179 | Apr 2026                                                          | string.toString() identity    | Returns string unchanged; every type now has `toString()`                                                                                                        |
| D-180 | Apr 2026                                                          | Stack overflow behaviour      | `CallFrame[256]`; depth 257 throws `RuntimeError`                                                                                                                |
| D-181 | Apr 2026                                                          | const depth                   | `const` prevents both rebinding and mutation; one rule _(superseded by D-288, D-291)_                                                                            |
| D-182 | Apr 2026                                                          | Nested arrays (T[][])         | Valid; `int[][]`; no rectangular guarantee; arrays-of-arrays                                                                                                     |
| D-183 | Apr 2026                                                          | No tuples                     | Not in v1; use structs; post-MVP if friction observed                                                                                                            |
| D-184 | Apr 2026                                                          | No out parameters             | Not in v1 and not planned; nullable returns cover the use case                                                                                                   |
| D-185 | Apr 2026                                                          | Try-parse pattern             | Nullable return types; `toInt() → int?`; `??` for defaults                                                                                                       |
| D-186 | Apr 2026                                                          | v1 scope                      | v1 scope-cut list: validation decorators and regex literals; activation at Chris's discretion                                                                    |
| D-270 | Apr 2026                                                          | Tokenisation — built-ins      | `print`, `exit`, `input` are built-in functions, not keywords                                                                                                    |
| D-271 | Apr 2026                                                          | Operator precedence           | `??` binds tighter than ternary; corrects §7 ordering                                                                                                            |
| D-272 | Apr 2026                                                          | Operator precedence           | Assignment operators not in precedence table; new §28 Statement Forms                                                                                            |
| D-273 | Apr 2026                                                          | Arithmetic                    | `float % float` supported with fmod semantics; `% 0.0` throws `RuntimeError`                                                                                     |
| D-274 | Apr 2026                                                          | Exception handling            | `try`/`catch`/`throw` grammar; typed catch, polymorphic match, catch-all form                                                                                    |
| D-275 | Apr 2026                                                          | Exception handling            | `finally` block on `try`; runs on all exits except `exit()`; no return/break/continue inside                                                                     |
| D-276 | Apr 2026                                                          | Lambdas                       | Block-body lambda: implicit last expression + `return` for early exit                                                                                            |
| D-277 | Apr 2026                                                          | Expressions                   | Switch expression v1 pattern grammar: value, relational, catch-all                                                                                               |
| D-278 | Apr 2026                                                          | Arithmetic                    | `int / 0` throws `RuntimeError`; no form of division by zero is silent                                                                                           |
| D-279 | Apr 2026                                                          | String literals               | Nullable interpolation is a compile error; resolve with `??` or narrowing `if`                                                                                   |
| D-280 | Apr 2026                                                          | Pipeline methods              | Drop `map()`; `select()` is universal transformation; LINQ-for-scripting identity                                                                                |
| D-281 | Apr 2026                                                          | Pipeline methods              | `sort()` key-selector only; `U: Comparable` constraint; stable; no comparator overload                                                                           |
| D-282 | Apr 2026                                                          | Formatting                    | `formatAs` replaces `format`; scalar formatters move to instance methods on numeric/date                                                                         |
| D-283 | Apr 2026                                                          | Compiler warnings             | Drop `snake_case` compiler warning; naming convention moves to formatter layer                                                                                   |
| D-284 | Apr 2026                                                          | Exception hierarchy           | `RuntimeError` split: four new typed leaves + residual; hierarchy now ten leaves                                                                                 |
| D-285 | Apr 2026                                                          | String literals               | Backtick raw strings canonical idiom for Windows paths and literal backslash content                                                                             |
| D-286 | Apr 2026                                                          | Forward references            | Cross-declaration reference rules; all top-level forward reference forms documented                                                                              |
| D-287 | Apr 2026                                                          | Type system                   | Non-nullable type cycles are a compile error; DFS detection with three visit states                                                                              |
| D-288 | Apr 2026                                                          | Variables                     | Split `const` into `const` (compile-time) and `readonly` (runtime-once)                                                                                          |
| D-289 | Apr 2026                                                          | Type system                   | Definition of "compile-time constant expression"; allowed and disallowed forms                                                                                   |
| D-290 | Apr 2026                                                          | Variables                     | Migration rule for existing `const` bindings; mechanical RHS-kind rule                                                                                           |
| D-291 | Apr 2026                                                          | Variables                     | `readonly` semantics: evaluated at declaration, never reassigned or mutated                                                                                      |
| D-292 | Apr 2026                                                          | Scoping                       | `const` and `readonly` permitted at function-local scope                                                                                                         |
| D-293 | Apr 2026                                                          | Implementation                | Grammar, AST and opcode impact of `readonly`; one new keyword                                                                                                    |
| D-294 | Apr 2026                                                          | Runtime                       | Top-level initialisation order: source order; three-state tag for circular detection                                                                             |
| D-295 | Apr 2026                                                          | User-defined types            | Type field default evaluation at construction time, construction-site scope                                                                                      |
| D-296 | Apr 2026                                                          | Closures                      | Four-category variable resolution in lambdas; `const` inlined, others via globals/upvalues                                                                       |
| D-297 | Apr 2026                                                          | VM — value representation     | `GrobValue` provisional shape; nine-variant tagged-union struct under .NET 10 LTS                                                                                |
| D-298 | Apr 2026                                                          | VM — bytecode file format     | `.grobc` skeleton spec; 40-byte header; `.grob/cache/` side directory; mtime invalidation                                                                        |
| D-299 | Apr 2026                                                          | Sprint plan                   | Sprint 8/9 reordered by dependency weight; `fs → date` is the only hard cross-module link                                                                        |
| D-300 | May 2026                                                          | Compiler — error recovery     | Parser error recovery: synchronisation set, `Error*` nodes, cascade suppression, no cap                                                                          |
| D-301 | May 2026                                                          | Control flow                  | `select` statement is non-exhaustive; switch expression is exhaustive — intentional split                                                                        |
| D-302 | May 2026                                                          | Tooling — benchmarking        | BenchmarkDotNet harness in `bench/Grob.Benchmarks`; three categories + stability; committed baselines; no CLI surface in v1                                      |
| D-303 | May 2026                                                          | VM — value representation     | OQ-005 closed. `GrobValue` is a tagged union — permanent. NaN boxing rejected (moving-GC mismatch, I/O-bound workload, debuggability)                            |
| D-304 | May 2026                                                          | VM — memory management        | OQ-006 closed. Lean on .NET GC; no custom mark-and-sweep in v1; benchmarking provides the surface to revisit                                                     |
| D-305 | May 2026                                                          | Process — implementation gate | clox gate satisfied; Sprint 1 cleared to begin. Core chapters worked through incl. NaN boxing; OQ-005/006 experience banked                                      |
| D-306 | May 2026                                                          | VM — developer diagnostics    | Disassembler (always compiled, Sprint 2) + `#if DEBUG` execution tracing. `grob dump` CLI wrapper deferred to Sprint 12. Release dispatch loop stays branch-free |
| D-307 | May 2026                                                          | Type system — naming          | Built-in scalars are lowercase (`int`/`string`/`bool`/`float`) — canonical, not new. Sprint 1 impl drift to `Int`/`String` corrected in code and tests           |
| D-308 | Diagnostics raised against catalog descriptors, not code literals | Tooling / error model         | —                                                                                                                                                                | —                                                    |
| D-309 | May 2026                                                          | Tooling — benchmarking        | Benchmark execution production mechanism moved to GitHub Actions `benchmark.yml`; D-302 deliverable unchanged, production path refined                           |
| D-310 | May 2026                                                          | Tooling — build               | C# 14 / .NET 10 SDK pinning corrected; `LangVersion 14` canonical; Sprint 2-end QA verified clean Debug and Release builds under corrected pinning               |
| D-311 | May 2026                                                          | Compiler — type checker       | Unresolved-identifier `Declaration` sentinel: `UnresolvedDecl.Instance` satisfies the §3.1.1 non-null invariant at every error path; addendum to D-137           |
| D-312 | June 2026                                                         | CLI — bare invocation         | Bare `grob` (no subcommand) ≡ `grob --help` — prints command listing to stdout, exit 0. Does not launch the REPL; `grob repl` is the sole REPL entrance          |
| D-313 | June 2026                                                         | Tooling — benchmarking        | Two-axis benchmark regression policy: 5% per-sprint vs rolling baseline + 12% cumulative vs frozen origin; `Grob.BenchCheck` makes `benchmark.yml` the gate; compile-time gates until end-to-end is live. Refines D-302/D-309 |
| D-314 | June 2026                                                         | Methodology — harness         | Implementation harness migrated Copilot → Claude Code: durable rules in `CLAUDE.md`, plan mode as the approval gate, increment prompts as `.claude/commands/` slash commands, Opus 4.8 subagent for named sub-problems, GPT-5.3 Codex cold-read via Codex CLI, CodeRabbit retained. Workflow shape unchanged |
| D-315 | June 2026                                                         | Control flow                  | `break`/`continue` in `select`: asymmetric. `break` inside `select` is a compile error (E2211) at any nesting; `continue` passes through to the nearest enclosing loop. `select` is not loop-control-transparent. Resolves Requirements/Fundamentals contradiction; E2212 added for `break`/`continue` outside any loop |
| D-316 | June 2026                                                         | Tooling — quality gate        | Corpus consistency regime: one-time A1 reconciliation (stale error-code total 99→103 and stale Sprint-1 status row corrected) plus a permanent CI-enforced drift gate (`tests/Grob.Consistency.Tests`) asserting error-code count agreement, decisions-log lockstep, ADR-reference integrity and opcode/TokenKind completeness, with D-308's `ErrorCatalog` agreement test referenced as the catalog↔registry guard. Generalises D-308; self-relative checks, no frozen baseline |
| D-317 | June 2026                                                         | Tooling — supply chain        | Project hardening interlude (Sprint 4→5): central package management with exact pins and committed lockfiles under locked-mode restore, NuGet and CodeQL vulnerability gates, deterministic builds with SourceLink, SHA-pinned least-privilege workflows, gitleaks CI secret scanning, and CycloneDX SBOM and build-provenance scaffolding. Additive repository hardening; complements §11 language/runtime security, does not overlap it. Code signing deferred to first public release (OQ-018) |
| D-318 | June 2026                                                         | Type system — named-arg diagnostics | Named-argument call-site errors (D-113) get dedicated codes E0008–E0011 — named-before-positional, naming a required parameter, duplicate named argument, unknown parameter name — rather than folding into E0003. Registered in Sprint 5 Increment B through their `ErrorCatalog` descriptors |
| D-319 | June 2026                                                         | Tooling — playground          | Browser playground is a client-side Blazor WASM embedding host on the existing Azure SWA (Route 1 — nothing sent to a server) wiring an alternate capability set: `fs`→in-memory virtual filesystem, `env`→synthetic map, `process`→unsupported (in-hierarchy host error, no new code). Requires a pure embeddable engine — OS contact behind injected host capabilities (in `Grob.Runtime`, confirmed DAG-clean), `exit` already handled via the existing `ExitSignal` (D-274), VM fresh per run (D-300). Dispatch-loop cancellation/step-budget seam adopted into v1 and folded into Sprint 5 Increment C (counter on the VM instance so it spans the re-entrant bridge; surfaces as `OperationCanceledException`, outside `GrobError`); rest of the playground build deferred post-v1 |
| D-320 | June 2026                                                         | Lexer/parser — name collision | `select` is a reserved identifier, not a hard keyword (the D-282 `formatAs` mechanism): the lexer emits `Identifier` and `select` leaves the keyword set, the statement parser promotes a leading `select (` at statement head, and `expr.select(...)` parses as member access. Resolves the D-280 (`.select()` universal transform) vs D-301 (`select` statement) collision that made `.select(...)` ungrammatical from source though checker/VM/compiler supported it — blocking six release-gate scripts (3, 5, 7, 8, 9, 12). The type checker forbids `select` as a binding name; adds E1103 (reserved identifier used as a binding name), also covering `formatAs`'s previously code-less rule. v1 reserved set `{ formatAs, select }`. Consistency gate (D-316) gains a native-method-name vs reserved-word check. Implemented as a Sprint 5 correctness increment after Increment C; D/E/F unchanged |
| D-321 | June 2026                                                         | Runtime / type checker — init order | Top-level `fn` bindings are runtime-hoisted: the compiler emits every top-level function's `DefineGlobal` in a prologue ahead of the first top-level statement, so each slot is `Initialised` before any top-level code runs. Two-pass checker pass 1 now registers top-level value bindings (`readonly` and mutable `:=`), not only `fn`/`type`, so a function body resolves a later-declared top-level value without E1001. E5902 is thereby narrowed to genuine value-binding initialisation cycles; calling a function declared later in source is no longer an E5902 trigger. The VM composes the E5902 message tracing through the function (binding initialising, function that read, binding read, with lines), via the `ErrorCatalog.E5902` descriptor (D-308). Refines D-294 and D-166; clean because top-level fns capture no upvalues (D-296). Sprint 5 correctness increment after Increment E; does not regress 5E's init-state machine |
| D-322 | June 2026                                                         | VM / diagnostics — source position | Runtime diagnostics carry `file:line` only — the column is omitted, not idealised (no fabricated `5:15`); per-opcode column tracking deferred out of v1. The compiled `Chunk`'s debug info is line-keyed (the D-306 disassembler prints lines for the same reason); compile-time diagnostics are unaffected — lexer/parser carry full `(file, line, column)` (D-137), so §10's `14:12` example is met. Not a D-321 regression: D-321 moved E5902 to a function-body read site and so surfaced the line-only render (`:5`) on `circular-initialisation`, but runtime E5902 was always line-only. Gold masters, harnessed and rich, record the real line-only output so they agree. Known-limitation note for `grob-vm-architecture.md` flagged as a follow-up |
| D-323 | June 2026                                                         | Type checker — value-binding type resolution | Three-pass type checker: new phase 1.5 resolves top-level value-binding types in initialiser-dependency order (DFS, Unvisited/Visiting/Visited) before pass-2 body validation. Forward value reads in function bodies now see the real type, not `GrobType.Unknown`, closing the false-E0005 gap. Unannotated mutual cycles → E0303 (compile-time); annotated cycles → E5902 (runtime). Lifts the `circular-initialisation` gold-master quarantine. New error code E0303. Refines D-321 and D-166 |
| D-324 | June 2026                                                         | Type checker — uniform top-level redeclaration | E1102 is broadened from `:=`-only to all top-level binding-introducing forms (`fn`, `type`, `readonly`, `const`, `:=`). All top-level declarations are registered as provisional in pass 1; each visitor finalises its own provisional entry in pass 2, so the collision is always detected at the second declaration in source order. Corrects the reverse-order bug where a value-before-fn collision erroneously reported at the earlier value binding. E1102 title broadened from "variable already declared in this scope" to "name already declared in this scope". No new error code; count stays at 109 |
| D-325 | June 2026                                                         | VM — closure upvalue lifecycle | Closures close captured locals by stack location, not by inspecting the return value. The VM keeps an open-upvalue list keyed by stack slot; on frame exit every open upvalue at or above the returning frame's base is closed into its heap cell, regardless of which heap object now references the closure. Route-agnostic by construction: array element, map value, struct field, parameter and direct return are handled identically. Fixes the array-escaped-closure value-stack underflow (`return [inc]` then `arr[0]()`) — value-based closing reached closures referenced directly by the return value but missed closures wrapped in a container, leaving an open upvalue pointing at a truncated slot. Concerns category-4 capture only (D-296); categories 1–3 and top-level fn hoisting (D-321, no captures) are untouched. No new error code; count stays at 109. Sprint 5 Increment 3 |
| D-326 | June 2026                                                         | Type system — function types | `fn(ParamTypes): ReturnType` becomes a first-class type reference accepted by `ParseTypeRef` anywhere a type is written. Resolves the standing contradiction — v1 mandates explicit return types on every function AND closures are first-class returnable values, so `makeCounter(): fn(): int` could not be written (the only expressible return type was rejected with E2001). Removed by making function types expressible, not by relaxing explicit return types or dropping returnable closures. Structural identity; invariant assignability (no variance in v1); runtime-erased (the callable is already a `GrobFunction`, so no opcode/grobc/`GrobValue` impact). `?`/`[]` bind to the return type — `(fn(): int)?` and `(fn(): int)[]` need parens. User-written function types are monomorphic; the registry's internal `→` generic notation is unchanged and D-080's no-user-generics rule is untouched. Function-type mismatches reuse the existing assignment/argument mismatch codes; no new error code; count stays at 109. Sprint 5 Increment 4 |
| D-327 | June 2026                                                         | Parser / type system — array type-refs | The `[]` array suffix is a type-reference production: `ParseTypeRef` consumes a postfix `[]`/`?` suffix chain after the primary type, so `int[]`, `int[][]` (D-182), `int[]?` (nullable array) and `int?[]` (array of nullable) parse as type annotations. Closes the D-326 gap — the formalised `TypeRef` grammar omitted the `[]` production its own suffix-precedence prose assumed, so `ParseTypeRef` never consumed `[` in type position even though `int[]` is pervasive across the spec and the `T[]` stdlib signatures (`fs.list(): File[]` etc.). Array type-refs are a built-in constrained generic (D-080, same model as `map<K, V>`), not user-facing generics — no generics-sprint dependency. The checker resolves an array annotation to the existing `T[]` type it already owns; value-position `[` (index, array literal) is unchanged. Runtime-erased — no opcode/grobc/`GrobValue` impact. Malformed suffixes (`int[`, fixed-size `int[5]`) reuse existing parser-error codes via D-300 recovery; no new error code; count stays at 109. Completes D-326; relates to D-182. Sprint 5 Increment 5 |
| D-328 | June 2026                                                         | Tooling — quality gate        | Test coverage gets a defined scope and a CI floor. A committed exclusion set (`sonar.coverage.exclusions` + annotated `[ExcludeFromCodeCoverage]`/`.runsettings`) removes CLI IO shells (the REPL read-print loop after its eval core is extracted), `tooling/` `Main` wrappers and named defensive/unreachable branches from the denominator — each exclusion carries a reason. `RunCommand`/`DiagnosticFormatter` are NOT excluded: their 0% is an instrumentation gap (validation suite and gold masters exercise them out of the collector's sight), fixed by instrumenting, not excluding. The remaining language-implementation denominator (lexer, parser, type checker, compiler, VM, runtime, stdlib) carries a mechanical 90% line+branch floor enforced in CI, red on breach — a tripwire that triggers triage, not a target to fill. Mirrors the D-313 mechanical gate and the D-316 self-relative CI drift regime; promoted to ADR-0018. The `csharpsquid:S3776` cognitive-complexity issue on the type-checker statement-visit method is closed in the same increment: cover to pin behaviour, decompose under green. No new error code; count unchanged. Sprint 5 Increment 6, before the Codex cold-read |
| D-329 | June 2026                                                         | Tooling — versioning          | MinVer (v7.0.0) adopted as the version-management strategy: assembly and package versions are derived from semver git tags with no hardcoded version in any `.csproj`. `MinVerMinimumMajorMinor=0.5` in `Directory.Build.props` gives pre-release builds the version `0.5.0-alpha.0.{height}` until the first semver tag. `v1.0.0` is reserved for post-Sprint-12 release. `ReplCommand` and `Program.cs` banners read `AssemblyInformationalVersionAttribute` (stripping the `+hash` suffix) so the displayed version is always in sync with the assembly. Banner tests assert format only (`Grob \d+\.\d+\.\d+`), not a literal — version-proof forever. |
| D-330 | June 2026                                                         | Type system — construction-site diagnostics | Unknown field name at a named type construction (`TypeName { notAField: v }`) gets a dedicated code **E0012** (Type category), not a fold into E1002 (undefined member, which is member *access* — `obj.field` on an existing value). Mirrors D-318's choice of dedicated call-site codes over folding into E0003: E0012 is to construction what E0011 (unknown parameter name) is to a named call, and sits in the E00xx type block beside E0103 (missing required field at construction) and E0011. Registered in Sprint 6 Increment B through its `ErrorCatalog` descriptor (D-308); error-code count 109 → 110. Resolves the §10 gap where the spec mandated the error but cited no code |
| D-331 | June 2026                                                         | Process / harness — closed-surface growth | Closed surfaces grow through sanctioned, logged procedures rather than hard "nevers", calibrated to whether the surface carries a stability contract. `OpCode`/`TokenKind` carry a wire-format contract (ADR-0013) — grown via `adding-an-opcode`. The parser and AST carry none (compiler-internal, never serialised) — built incrementally, closed *within an increment* as scope discipline, extended via the new `extending-the-grammar` skill; reclassified from architecture invariant to scope discipline in `AGENTS.md` and the compiler-engineer agent. Error codes carry ADR-0017 immutability — each increment declares an error-code budget, with the new `allocating-an-error-code` ladder (surface fold-vs-new, register at the next free number from the live registry, count reconciled and D-316-ratified) for the unanticipated case. Resolves the three-way harness contradiction on codes and the grammar-complete premise that Sprint 4E and 6B falsified. No new error code; count unchanged |
| D-332 | June 2026                                                         | VM — operand-stack allocation | `ValueStack`'s backing array right-sized from a fixed 16,384-slot (393 KB) allocation to a 1,024-slot (24 KB) default that grows geometrically (doubling, capped at the unchanged 16,384-slot ceiling) via `Array.Resize` on `Push`. Fixes the Sprint 6 benchmark finding — all three VM benchmarks showed `Gen0 == Gen1 == Gen2` (a full compacting GC every op) because the fixed array cleared the ~85,000-byte LOH threshold and a fresh `VirtualMachine`/`ValueStack` is constructed per run. Overflow guard (E5903) and effective depth cap unchanged; D-325 open upvalues (stack object + slot index, never a raw reference/span) survive the resize transparently — the sole `Span<GrobValue>` in `Grob.Vm` is the `#if DEBUG` trace hook's per-iteration, never-cached snapshot. No new error code; count unchanged. Baseline recapture on `windows-latest` via `benchmark.yml` pending — not performed in this local session (D-309 forbids a locally-produced committed baseline) |
| D-333 | July 2026                                                         | Tooling — benchmarking | Benchmark gate hardened on three fronts. A hard **allocation axis** reads `[MemoryDiagnoser]`'s already-committed `BytesAllocatedPerOperation`: a percent-vs-rolling threshold (`allocPercent`, 10%) on gating categories, plus an absolute **LOH tripwire** (`lohTripwireBytes`, 85,000 B) that fails outright on any category regardless of gating — the check that would have caught D-332 on day one instead of reading "info". The per-sprint **time axis is significance-aware**: a breach now requires the delta to exceed `max(perSprintPercent, timeSignificanceK × relativeStdDev)`, `timeSignificanceK = 3` (three-sigma), so a delta inside a benchmark's own measurement noise (the Sprint 6 `Compile_TenPrints` false positive) no longer trips it; the cumulative axis is unchanged. `SameRunnerType`/`RunnerMismatch`/`CannotCompare` are replaced by a **CPU-identity guard** (`BenchCheck.SameCpu`, keyed on `HostEnvironmentInfo.ProcessorName`, checked independently against the rolling and origin baseline) after the post-Interlude-1 verification run proved an EPYC-baseline-vs-Xeon-run comparison passed the old OS-family-only guard with a false +25–37% time breach despite byte-identical allocation. Allocation always gates regardless of CPU; time gates only on a CPU match, else informational — refines D-309's "same runner type" to "same CPU identity". `vm.json`/`vm.origin.json` recaptured post-fix (50,265/52,841/58,473 B, off the LOH); `compile.json` (rolling) refreshed to the same Xeon capture after discovering it was three sprints stale; `compile.origin.json` deliberately left with its pre-provenance `"Unknown processor"` host, so the compile cumulative axis reads informational until a separate, logged re-freeze. No new error code; count unchanged |
| D-334 | July 2026                                                         | Exception handling — finally compilation model | `finally` splits on one axis: the exceptional path (an exception unwinding through a region) is VM-run via handler-table `TryRegion.FinallyOffset`; every non-exceptional path (normal try/catch completion, early `return`/`break`/`continue`) is compiler-emitted, re-visiting `node.Finally`'s AST at each crossing site (duplication expected — the closed `OpCode` enum has no `Leave`/`EndFinally`). Compiler: a `TryFinallyContext` stack mirroring `LoopContext`, popped only after catch bodies compile; `return` crosses every context, `break`/`continue` cross only contexts pushed after the target loop (`LoopDepthAtPush`); a `_nextSlot` reservation parks a `return`'s value below the finally bodies' own locals. VM: the construct span for triggering a finally is bounded by `FinallyOffset` (covers catch bodies too, not just `EndOffset`); `PropagateThrow` drives the outward walk, running each region's finally via `RunFinallyExceptional` — a new bounded mode of the existing `RunDispatch` that runs in the current frame (no call, no upvalues) and stops at the region's own closing `TryEnd`; a throw escaping it raises an internal `FinallyEscape` that replaces the in-flight exception. Rejected: a closure-based exceptional-path invocation via the existing reentrant call machinery (needs no new VM primitive but forces upvalue capture for every referenced local, and doesn't unify with the inline copies anyway). Folds in a mechanical fix making E2206 reachable (`ParseTry` previously broke unconditionally on a matched `finally`, so a trailing catch/finally was structurally unrepresentable and undetectable) — not a new decision. No new error code; count unchanged at 116 |
| D-335 | July 2026                                                         | Process / CI — solution membership | `tests/Grob.Integration.Tests` restored to `Grob.slnx`, having been silently dropped by PR #94 (`28eb753`, 2026-06-26) incidental to an unrelated change and never logged. For two sprints neither `dotnet test Grob.slnx` nor CI's bare `dotnet test` executed the project, so the Sprint 5 (`functions.grob`) and Sprint 6 (`types.grob`) close-gate smoke masters were unverified by CI, and `Sprint6IncrementBTests` was authored and merged against behaviour the VM never implemented (see D-336). Root cause is not the dropped line but the absence of any mechanical owner: an unreferenced test project fails no gate and contradicts no document. Durable fix is a **test-project membership check** in `tests/Grob.Consistency.Tests` — enumerate `**/*.Tests.csproj` under `tests/`, assert each is referenced by `Grob.slnx`, drift is a build failure. Generalises D-316's mechanical-agreement regime to project membership. No new error code; count unchanged at 116 |
| D-336 | July 2026                                                         | Runtime — value display protocol | `print()` and string interpolation render composite values through a single `ValueDisplay` service in `Grob.Runtime`, resolving a three-way divergence. §13 mandated "reference types call `.toString()`"; user struct types had no `.toString()` (D-179 gave one to every *built-in* type only); and `OpCode.Print`'s single-arg fast path dispatched through a hardcoded C# switch that called no `.toString()` at all. `GrobValue.ToString()`'s `[TypeName]` fall-through was never a decision — the bracket-tag precedent (D-159 `AuthHeader`) is a deliberate per-type opacity mechanism for credentials, not a display default, and D-101 (`@secure`) is Grob's actual do-not-echo instruction. Two internal entry points: **`Display(v)`** (top-level — strings unquoted, per D-179 identity) and **`Inspect(v)`** (nested — strings quoted, so `"8080"` is distinguishable from `8080`); `toString()` remains the sole public method. Rendering mirrors source syntax: `Config { host: "example.com", port: 8080 }`, `#{ host: "example.com" }` (D-114), `[1, 2, 3]`, `{ "a": 1 }`. Cycles are reachable at runtime because E0301/E0302 reject only *non-terminating* type cycles, so `Inspect` carries reference-identity cycle detection (`<cycle>`) plus a depth cap (`...`). Dispatch is a **numbered precedence**, not a type switch: `nil` → registered `toString()` (terminal) → scalars → position-dependent `string` → structural composite. Step 2 precedes step 5 for a security reason: all plugin types and user `type`s share the `Struct` discriminator (D-297), so `[AuthHeader]`'s credential opacity (D-159) is today produced *accidentally* by the same fall-through arm that emits `[Config]`, and a structural renderer wired ahead of the registry lookup would leak bearer tokens. `Function` values render as their type (`fn(int): int`) — never an address, which would make gold-mastered smoke scripts non-deterministic. `float` always renders round-trippable with a decimal point or exponent (`1.0`, not `1`) under pinned `InvariantCulture`, with pinned `NaN`/`Infinity`/`-Infinity` — an unpinned culture emits `1,5` on a `de-DE` host, silently breaking every gold master and `formatAs.csv`. Supersedes the implicit `[TypeName]`/`[array(N)]`/`[map]` behaviour. The four `Sprint6IncrementBTests` assertions were rewritten mid-interlude to expect `[Config]`; that commit is not a decision and D-336 rewrites them to assert the exact rendered form. Reconciles `print` with `formatAs` (D-282) ahead of Sprint 8. No new error code; count unchanged at 116 |
| D-337 | July 2026                                                         | Process — sprint-close smoke scripts | The sprint-close smoke-script family gets a documented home in `grob-v1-requirements.md`. Five exist (`hello.grob` Sprint 3, `calculator.grob` Sprint 4, `functions.grob` Sprint 5, `types.grob` Sprint 6, `errors.grob` Sprint 7), each added at a sprint close, each gold-mastered under `tests/Grob.Integration.Tests`. Until now the family appeared in no design document, had no Definition-of-Done row and was named by no decision — the ownership vacuum that let D-335's CI gap persist undetected. Distinct from the thirteen release-gate validation scripts of `grob-sample-scripts.md`: the smoke family is per-sprint and cumulative, the validation suite is a v1 release gate. `errors.grob` departs from the prior four by asserting exit code 42 (`exit()` inside `try`/`catch`/`finally`, neither handler running), so the family's contract is stdout, stderr **and** exit code, not exit 0. No new error code; count unchanged at 116 |
| D-338 | July 2026                                                         | Tooling — benchmarking / Compiler — exception hierarchy registration | The Sprint 7-close compile-allocation regression (`Compile_TwoExpressions` +68.6%, `Compile_TenPrints` +37.2% bytes/op, both against the D-333 rolling baseline) is traced to `TypeChecker.RegisterExceptionHierarchy` (introduced in #112): every compile re-synthesised 11 `TypeDecl`/`UserTypeInfo`/`Symbol` objects with content identical on every run, grew the global-scope and `UserTypeRegistry` dictionaries from empty via repeated resize-and-copy, and fed all 11 into `DetectTypeCycles`'s §17.1 DFS even though no hierarchy member carries a required `GrobType.Struct` field and so can never participate in a cycle. Four behaviour-preserving fixes (cache the three object kinds as `static readonly`; pre-size the global-scope dictionary to its known fixed load; pre-size the `UserTypeRegistry` dictionary likewise; exclude hierarchy names from the cycle-detection walk) cut the regression to +14.0%/+7.6%, all tests and gold masters unchanged. The residual ~1,104 B fixed cost is accepted as load-bearing: D-284 requires all 11 `GrobError` hierarchy names resolvable in every compile's global scope, so a permanently larger built-in symbol table (14 entries against the pre-Sprint-7 baseline's 3) costs real bytes with no further avoidable churn to remove — closing it fully would mean lazily registering hierarchy names only when referenced, changing the timing of built-in name resolution, rejected here as a materially bigger redesign out of scope for a behaviour-preserving perf fix. `Compile_TwoExpressions` (+14.0%) therefore remains outside the axis-3 `allocPercent` 10% gate (D-333) by deliberate acceptance, not oversight; the rolling `compile.json` baseline is left un-updated by this entry — D-309 requires baseline production via the `benchmark.yml` workflow on the canonical runner, not a local run, so the recapture is a separate, subsequent act. No new error code; count unchanged at 116 |
| D-341 | July 2026                                                         | Tooling — benchmarking | D-338's deferred baseline recapture is performed: the rolling `compile.json` baseline is replaced with the `-report-full.json` from `benchmark.yml` run 29207744217 (`windows-latest`, AMD EPYC 7763, 2026-07-12), folding in the accepted +14.0%/+7.6% (8,968 B / 15,584 B) figures per D-309's canonical-workflow production requirement. `BenchCheck` now reads 0.0% delta on both compile benchmarks against the recaptured baseline. `compile.origin.json` is deliberately left untouched — the cumulative axis reads informational only under D-333's CPU-identity guard (fresh AMD EPYC 7763 vs origin's Intel Xeon Platinum 8370C), so the now-larger origin drift (+47.1%/+32.8%) does not gate, and an origin re-freeze remains a separate maintainer-judged act. `vm.json`/`endToEnd.json` untouched — vm deltas stay informational (non-gating, D-313) and no fresh end-to-end benchmarks exist yet. No new error code; count unchanged at 116 |
| D-342 | July 2026                                                         | Compiler — module-namespace resolution | Core stdlib modules (`math`, and in later increments `path`/`env`/`log`/`guid`/`formatAs`) are compile-time namespaces — a name category in the global scope that is neither a value nor a type binding. `TypeChecker.RegisterNamespaces` seeds each as a `NamespaceDecl` sentinel (mirroring `BuiltinDecl`); a hand-authored `NamespaceRegistry` table (compile-time twin of the runtime `IGrobPlugin` registration, agreement-tested per the D-308 pattern) maps each namespace's members to a constant type or a native signature. Dispatch precedence at a member-access node is fixed: namespace receiver resolves against `NamespaceRegistry` (unknown member → E1003 "undefined module", reused rather than duplicated); value receiver falls through unchanged to the pre-existing struct-field and array-higher-order-method arms. A namespace referenced in value position (`x := math`, `print(math)`) is E1004 "namespace used as a value" (new), the namespace analogue of the existing `TypeDecl`-as-value arm (E2102). No runtime module object and no new opcode: a namespace constant (`math.pi`) compiles to `GetGlobal`; a namespace-qualified native (`math.sqrt`) compiles to its argument(s) then `GetGlobal` then the existing `Call` — the same `GetGlobal`-by-qualified-name shape a plain top-level function call already uses (D-321's `DefineGlobal` prologue), not a literal embedded function constant, since `Grob.Compiler` cannot reference `Grob.Stdlib` to know a native's C# delegate at compile time. One new error code (E1004); count 116 → 117 |
| D-343 | July 2026                                                         | Runtime — capability-injection seam | Refines D-319's provisional capability-interface sketch into a landed seam. `IGrobPlugin` (`Register(IPluginRegistrar)`) and `IPluginRegistrar` (`RegisterNative`/`RegisterConstant`) are declared in `Grob.Runtime`; `IPluginRegistrar` exists as a narrow interface distinct from the concrete `VirtualMachine` specifically so `Grob.Runtime` never references `Grob.Vm` (the DAG already has `Grob.Vm` → `Grob.Runtime`; the reverse edge would cycle) — `VirtualMachine` implements `IPluginRegistrar` in `Grob.Vm`. `IStandardStreams` (`Out`/`Error`) is the first capability consumed: `OpCode.Print`'s VM handler reads an injected `IStandardStreams` instead of touching `Console` or a bare `TextWriter` field directly; `Grob.Cli`'s composition root constructs the OS-backed default and passes it to `VirtualMachine` via a new constructor overload (the pre-existing single-`TextWriter` constructor is kept unchanged, wrapping its argument in a minimal default, so none of the ~39 existing call sites across the test suite need to change). `print`/`exit` stay on their existing dedicated opcodes (`OpCode.Print`/`OpCode.Exit`) — they are not converted into `Call`-dispatched `NativeFunction`s; a `Grob.Stdlib.IoPlugin : IGrobPlugin` exists to give the I/O seam a uniform place in the plugin-registration pass, but registers no callable, since print/exit are formalised, not rebuilt. `IEnvironment`, `IClock`, `IRandomSource` are declared in `Grob.Runtime` alongside `IStandardStreams` per D-319's sketch, with no consumer yet — Increment B (`math.random*`), C (`env`/`log`) and D (`guid`) are their first real consumers. `IFileSystem`/`IProcessRunner` are not declared; they arrive with `fs`/`process` in Sprint 9. No new error code; count unchanged at 117 |
| D-344 | July 2026                                                         | Runtime / Compiler — stdlib host surfaces | Sprint 8 Increment C lands `env`, `log` and `input()`. `IStandardStreams` gains a third member, `In` (`TextReader`), for `input()` to read from; `SingleWriterStreams` answers it with `TextReader.Null` (closed stream), mirroring its existing `Error` convention. `input()` is a new dispatch category — the no-namespace native: `TypeChecker.Expressions.cs VisitCall` validates it (0–1 args, E0003/E0004) ahead of the permissive `print`/`exit` fallback, and `Compiler.Expressions.cs VisitCall` fills a missing 0-argument call's prompt with the constant `""` before the ordinary `GetGlobal`-then-`Call` shape (the runtime native's own arity is always 1) — a one-off arm, not a general defaulted-native mechanism. `env`/`log` are ordinary `NamespaceRegistry` consumers (D-342 pattern): `env.require` reuses `ErrorCatalog.E5801` with D-284's pinned message template; `input()`'s closed-stdin fault reuses the residual `ErrorCatalog.E5305`. `log.setLevel` recognises exactly its four lowercase level names; any other string is a silent no-op, not a thrown fault — deliberate, since a typo in defensive logging code should not crash a script. `ValueDisplay`/registered-`toString()`-through-`log` wiring is deferred to Increment D (`guid`, its first real consumer) — building the seam now would be untested forward scaffolding. `--verbose` is a new CLI presence flag selecting `LogPlugin`'s initial threshold. Corrects this increment's kickoff prompt, which mis-cited non-existent "D-339"/"D-340" and unrelated D-334 — the governing decisions are D-342 and D-343. No new error code; count unchanged at 117 |
| D-345 | July 2026                                                         | Compiler / Runtime — `formatAs` module | Sprint 8 Increment E lands `formatAs` (`table`/`list`/`csv`). Corrects this increment's kickoff prompt, which again cited non-existent "D-339" (see D-344's identical correction) — the governing decision is D-342, whose E1003/E1004 the two namespace-misuse diagnostics (bare `.formatAs`; unknown `.formatAs.X`) fold into with `formatAs`-specific message text. `formatAs` registers as a namespace **name** only (empty `NamespaceRegistry` member dict); its three members bypass the generic `ConstantMember`/`NativeMember` dispatch entirely for bespoke resolution (`ResolveFormatAsCall`), since compile-time column derivation and the chained-form rewrite don't fit the positional native model. The chained-form rewrite (`items.formatAs.table()` → `formatAs.table(items)`) is shared resolution over a pattern-matched AST shape, not a literal node substitution — `CallExpr`/`MemberAccessExpr` are immutable records, so both the checker's and the (necessarily separately-implemented) compiler's `TryDetectFormatAsChainReceiver` independently re-derive the chain shape, mirroring the existing `math.pi` namespace-receiver precedent. Compile-time column derivation is a bounded, `formatAs`-scoped peek (`GetArrayElementFieldNames`/`GetStructFieldNames`) covering array literals, `.select`/`.filter`/`.sort` chains and indexed elements — not a general array-element-type system; one new `Symbol.ArrayElementStructTypeName` field (parameters only) plugs the one real gap found (`ResolveSignatureType`'s `ArrayTypeRef` arm previously discarded the element's struct name entirely). The compiler always injects the derived columns as a synthesised second constant-array argument (`OpCode.NewArray`, no new opcode), so `FormatAsPlugin`'s three natives keep a fixed arity of 2 regardless of source overload. A new `IPluginRegistrar.RenderValue` capability lets `Grob.Stdlib` (which cannot reference `Grob.Vm`) render cells through the VM's real, registry-backed `ValueDisplay` (D-336) rather than a `NullRegistry`-backed one that would miss `guid`'s registered `toString()`. A real regression was caught and fixed in the same change: `formatAs` is the first reserved identifier (D-320) that is also a pre-registered namespace symbol, so `formatAs := 1` doubled up E1103 with a spurious E1102 until `FinalizeTopLevelBinding`/`VisitVarDecl` were taught to skip the collision check for a reserved name. Array indexing (`arr[i]`) was found to have no compiler emission at all (crashes the VM with or without this change) — confirmed pre-existing and out of scope; `formatAs` on an indexed element is verified at the type-checker level only. No new error code (E0004/E0011/E1003/E1004 all pre-existing; confirmed via `allocating-an-error-code`); count unchanged at 117 |
| D-346 | July 2026                                                         | Tooling — benchmarking        | Sprint 8 close: the stability-test calibration ritual runs against the six Sprint-8-runnable scripts (five smoke scripts + `stdlib.grob`), not "all thirteen" (§6) — every one of `grob-sample-scripts.md`'s scripts depends on a Sprint-9 module or an unbuilt plugin; the full-suite run becomes a v1 release-gate step. Surfaces, rather than silently fixes, that the "thirteen" count itself is stale — the file has only ever held eleven scripts, and D-283 (predating D-302) already called it eleven. A longer checkpoint sweep found a one-time cache/registry warm-up step between iteration 1000–2000, not a leak; locked `iterations: 10000, warmup: 2000, tolerancePercent: 2.0`, first passing run at 0.0% drift (190,456 B both ends). `Grob.Benchmarks` now references `Grob.Cli`, driving the stability loop through `RunCommand` rather than re-implementing capability wiring — a documented deviation from §3's literal assembly list. No new error code; count unchanged at 117 |
| D-347 | July 2026                                                         | Tooling — benchmarking        | Compile-time baseline recapture: the rolling `compile.json` time axis is folded forward to the post-Sprint-8 floor. `RegisterNamespaces` (D-342) unconditionally seeds all seven core-module namespaces into every compile's global scope — a fixed cost every compile now pays, hitting the tiny `Compile_TwoExpressions` benchmark hardest (+5.8% rolling, a genuine same-CPU breach, not `vm`'s CPU-mismatch noise). The allocation half of this same regression was already fixed once (`NamespaceRegistry`'s static object caching, landed in the Sprint 8 QA pass); this entry performs the equivalent time-axis recapture, mirroring D-338→D-341's ship-then-recapture precedent. `benchmark.yml` run 29399169091's report replaces `compile.json` wholesale (no local run ever committed as a baseline, D-309); `BenchCheck` now reads 0.0% delta. `compile.origin.json` untouched — still CPU-mismatched against the frozen Intel Xeon origin per D-333. No new error code; count unchanged at 117 |
| D-348 | July 2026                                                         | Compiler — expression emission | Sprint 9 Increment A lands the missing `VisitIndex` override (D-345's surfaced gap): `arr[i]` compiled to nothing at all (`AstVisitor<T>.VisitIndex`'s default fell through to `Compiler`'s no-op `DefaultVisit`), crashing the VM with a stack underflow. The fix emits the receiver, the index expression, then the existing `OpCode.GetIndex` — the opcode itself, its bounds-checked array arm and nil-on-miss map arm, and the D-334 handler-table routing of an out-of-range read through the existing `E5101`/`IndexError` leaf (D-284) were already implemented at the VM layer, reachable only via `for...in` lowering until now. One emission shape covers array and map reads and chained indexing (`matrix[r][c]`, D-112) via `IndexExpr` nesting — no opcode, `GrobValueKind`, parser or AST change. Array element **write** (`arr[i] = v`) is a confirmed, separately pre-existing gap (`Compiler.Statements.cs`'s deferred-index-target early-return, unchanged), left deferred since Sprint 9's `json`/`csv` indexer consumers are read-only. §3.1.1's `ResolvedType`/`Declaration` invariant scopes to identifier nodes only — `IndexExpr` carries neither property and none is added; an identifier used as an index target still gets both set via the pre-existing `VisitIdentifier` path. No new error code; count unchanged at 118 |
| D-349 | July 2026                                                         | Process — decisions-log reconciliation (append-only) | Append-only landing record for Sprint 8 Increment D's `guid` module, which shipped `E0601` (invalid `guid` string literal, the first E06xx entry, source D-149) without a matching decisions-log entry — so D-345 and D-346, both logged afterwards, still read "count unchanged at 117" against `grob-error-codes.md`'s footer, which already correctly recorded 118. D-345 and D-346 are left unedited (append-only); this entry is the missing landing record and the pointer that reconciles the log's own narrative count to the registry's true, already-live total. No new error code; count unchanged at 118 |
| D-350 | July 2026                                                         | Compiler — statement emission | Sprint 9 Increment A2 lands `arr[i] = v` / `m[k] = v` index-store emission — the write companion to D-348's read-side `VisitIndex`. Wires the existing, previously-unemitted `OpCode.SetIndex` (already in the closed enum, recognised by the disassembler, simply never dispatched by the VM or emitted by the compiler) into both: array writes bounds-check and raise `IndexError` via the existing `E5101`/D-334 handler table exactly as `GetIndex` does; map writes upsert (no bounds error); a nil receiver raises `E5201`. One emission shape covers array and map writes and chained targets (`matrix[r][c] = v`) for free, mirroring D-348's precedent. `readonly` rejection (`E0204`) generalises the existing member-access root-walk (`FindReadonlyRoot`) to also walk `IndexExpr` chains — its deep-immutability precedent is **D-291**, not D-289 as the kickoff prompt cited (D-289 is the compile-time-constant-expression definition; corrected here, not silently). `const`-bound array/map mutation is not a reachable state to test: D-289 already disallows array/map/struct literals as `const` RHS, so a `const`-bound collection fails earlier at `E0205` — no const-rejection path is added, mirroring the pre-existing member-access check, which likewise only ever tests `ReadonlyDecl`. RHS element-type checking stays permissive: `GrobType.Array` carries no scalar element type (element-type tracking awaits generics, per `VisitIndex`'s own unconditional `Unknown` read-side result); this is named here as an open, honestly-scoped gap, not built ad hoc. Two adjacent assignment-target gaps sharing the same early-return shape are confirmed still deferred and named for scheduling rather than left as a code comment: `arr[i] += v` (compound assignment) and `arr[i]++`/`arr[i]--` (increment/decrement) both silently drop emission today. No new opcode, no new error code (E5101/E0204/E5201 all pre-existing); count unchanged at 118 |
| D-351 | July 2026                                                         | Compiler — type representation | Sprint 9 Increment A3 gives `GrobType.Array`/`NullableArray` a real element-type identity via a new `ArrayTypeDescriptor` mirroring `FunctionTypeDescriptor`'s side-channel shape (`Symbol.ArrayDescriptor`, generalising the Sprint 8 Increment E parameter/struct-name-only field; literal-node and call-result dictionaries), never folding element data into the `GrobType` enum itself. Corrects the increment's own premise: `map<K, V>` is not a working precedent — `TypeRef.TypeArguments` is parsed but never consulted, `"map"` resolves to the flat `GrobType.Map` tag everywhere, and `for k, v in m` already binds `v` as `Unknown`; maps share arrays' pre-existing gap and stay out of scope here. Threads the element type through array-literal inference (`[1, "a"]` is `E0001`, int/float widens), index read (`arr[i]`, replacing D-348's unconditional `Unknown`), index write RHS (closing the A2 gap D-350 named), `for...in` item binding (including the item's own struct-name/nested-array identity, so member access and further indexing inside the loop resolve as a `:=`-local's would), function-parameter/return enforcement and struct-field construction — reusing `E0001`/`E0004`/`E0005` throughout, no new error code. Zero quarantines: no fixture in the corpus relied on loosely-typed or heterogeneous arrays. Surfaces, Chris-approved as its own follow-up rather than folded in: the array mutation-method surface (`append`/`insert`/`remove`/`clear`/`contains`/`first`/`last`/`length`/`isEmpty`) has no type-checking, no compiler emission and almost no VM support at all — D-140 documented it but only `filter`/`select`/`sort`/`each` were ever built. Unblocks A4 (`arr[i] += v`, `arr[i]++`/`--`, D-350's named follow-up) and Increment D's `mapAs<T[]>()`. No new opcode; count unchanged at 118 |
| D-352 | July 2026                                                         | Grob.Http — redirect and credential policy | `grob-stdlib-reference.md` and D-155 lock every `Grob.Http` signature but say nothing about redirect behaviour, so the .NET `HttpClientHandler` default (`AllowAutoRedirect=true`, `MaxAutomaticRedirections=50`, and historically `Authorization` forwarded across redirects) would ship silently — an unpinned default lets a `302` from a trusted host walk a live `auth.bearer` token to an attacker-controlled origin, a credential-exfiltration vector in the stdlib itself. Pinned policy: (1) redirects are followed by default but a cross-origin target (differing scheme+host+port) does **not** receive the `AuthHeader` — the request proceeds without it, silent-and-safe by design, not a fault; (2) an https→http downgrade redirect throws `NetworkError` — no silent downgrade; (3) the total redirect chain caps at 10 (tighter than .NET's 50 — a scripting default should be conservative), exceeding it throws `NetworkError`; (4) `download()` follows the identical policy. Cap and downgrade faults reuse the existing `NetworkError` leaf (D-284) — no new error code. Implementation lands in **Sprint 11** with `Grob.Http` (plugins are Sprint 11 in the build plan, not Sprint 9), so the policy is pinned now for Sprint 11 to build against; verified later by Pillar 7 of the adversarial suite (D-353) — a post-Sprint-11 network-hardening pass against a local hostile Kestrel server and the Azure tenant's real cross-host and IMDS-reachable endpoints. Extends D-155. No new error code; count unchanged at 118 |
| D-353 | July 2026                                                         | Process / Tooling — adversarial testing | Authorises `grob-adversarial-testing-strategy.md`. The corpus's existing test families (§12 unit/integration, the D-337 smoke family, the D-346 eleven-script validation suite) are all cooperative — they verify Grob does what it should; the adversarial family verifies Grob fails well under hostile input, a distinct discipline with its own harness and exit criteria. The contract: no input (source, bytecode, CLI args, environment, file-system state, child-process behaviour) may produce an unhandled .NET exception, a host stack trace, a non-responsive hang or an undocumented exit code — every failure is a Grob diagnostic with an E-code on stderr; any violation is P0. Seven pillars: (1) compiler fuzzing (mutation, grammar-based, SharpFuzz), (2) hostile `.grobc` fuzzing, (3) differential/metamorphic + cross-model spec adversaries, (4) stdlib/environment brutality (Windows-specific), (5) resource exhaustion/soak, (6) cold-read usage campaigns, (7) hostile network surface (two-tier: local Kestrel + Azure tenant). Harness: `tooling/Grob.Torture` — a black-box CLI driver outside `tests/` with an in-proc SharpFuzz mode; a stabilised subset graduates to CI as `tests/Grob.Torture.Tests` (D-335 membership gate applies). Timing (split to fit the build plan): Sprint 9 lands the remaining **core** modules (`fs`/`date`/`json`/`csv`/`regex`/`process`) but plugins are **Sprint 11** — so the **Sprint 9/10 hardening interlude** runs Pillars 1–6 in full (the language, VM and core-stdlib surface is complete at Sprint 9 close), and Pillar 7 (hostile network) plus D-352 verification and the Azure work become a smaller **post-Sprint-11 network-hardening pass** once `Grob.Http` exists. Harness skeleton and Pillar 1 layers 1–2 build during/after Sprint 9; each pass carries its own DoD. Four decisions the interlude is expected to force and log: the `.grobc` load-time verifier vs per-instruction bounds checks, the parser recursion-depth guard, the `regex` `matchTimeout` value, the `exit(n)` out-of-range clamp. No new error code; count unchanged at 118 |
| D-354 | July 2026                                                         | date module — API shape       | Amends D-108 ahead of `date`'s first implementation: drops `minusDays` (no per-unit `minus*` — `addDays`/`addMonths`/`addHours`/`addMinutes` all accept a negative `n` uniformly); adds `toDateOnly()`/`toTimeOnly()` (mirrors `date.today()`'s zero-the-other-part convention, `toTimeOnly()` epoch-anchored); authorises `LessDate`/`GreaterDate` — the first `OpCode` enum growth since Sprint 2 — so `d1 < d2`/`>`/`<=`/`>=` work, gated to a nominal date-vs-date pair at the type checker. No new error code; count unchanged at 118 |
| D-355 | July 2026                                                         | Runtime / Compiler / Stdlib / Vm — date module | Sprint 9 Increment B: `date` lands following the `guid` precedent (D-149) — `Struct`-discriminated (D-303, no new `GrobValueKind`), one hidden `__value` field holding a round-trip `DateTimeOffset` string, `NamespaceRegistry` entry for the seven static constructors, type-checker/VM dispatch keyed off the struct name. Diverges from guid: real per-argument arity/type checking (guid's methods are all zero-arity); `date` is the first type besides `guid` recognised in annotation position (`ResolveSignatureType`/`ResolveNamedFieldType`/`TryGetNamedStructTypeName` each gain a `"date"` arm). `now`/`today` read the injected `IClock` (D-343), converted to local time (D-176), never `DateTime.Now` directly. `date.parse` reuses the reserved `E5702` (D-284) through the native-throw seam — no compile-time literal check (unlike guid's E0601). Registers a canonical ISO-8601 `toString()` for `ValueDisplay` (D-336); discovered `DateTimeOffset`'s `K` specifier never renders `Z` (unlike `DateTime`'s), handled with an explicit zero-offset case. No new error code (E5702 reused); no new `GrobValueKind`. The two opcodes are D-354's; this is the landing record, mirroring the D-348/D-349-vs-D-345 split precedent |
| D-356 | July 2026                                                          | Compiler / Type checker / Runtime — named-type dispatch | A single declarative named-type registry consolidates the six hand-rolled, string-matched dispatch surfaces per nominal `Struct` type (the three annotation-position resolvers, the method/property validator, VM dispatch, the `ValueDisplay` `toString()` registration) into one table entry per type. `guid` and `date` migrate onto it as behaviour-preserving proving cases; `File`/`json.Node`/`csv.Table`/`CsvRow`/`Regex`/`Match`/`ProcessResult` (Sprint 9) and `Response`/`AuthHeader`/`ZipEntry` (Sprint 11) then land as data, not as code spread across six sites where one missed arm resolves a valid type as E1001. Extends the `NamespaceRegistry` (D-342) idea from module members to instance surfaces; arrays and maps stay structural (descriptor-based, D-351, not registered here). Agreement-tested against runtime plugin registration in the D-308/D-342 pattern. Highest-leverage item in the Sprint 9B review — converts four increments of per-type dispatch plumbing into entries and shrinks the D-353 adversarial attack surface before the fuzzer meets it. No new error code; count unchanged at 118 |
| D-357 | July 2026                                                          | date module / Type system — equality and ordering | Amends D-169 for nominal `date` only: `date`-vs-`date` equality **and** ordering are both instant-based, matching .NET `DateTimeOffset` operator semantics (`EqualsExact`, the offset-sensitive variant, is deliberately not exposed). Resolves the incoherence where D-355's round-trip-string `__value` field made `date.now()` and `date.now().toUtc()` — the same instant — compare neither `<`, `>` nor `==`, violating trichotomy and the "language that doesn't surprise you" identity. Keyed off the nominal date-vs-date pair the checker already gates (D-354); the `Equal` handler gains a date arm parsing both `__value` strings to instants (opcode shape at the increment's discretion). `daysUntil`/`daysSince` pinned to whole 86,400-second periods between instants (Scripts 7 and 8 depend on the answer). `guid` reviewed and left field-by-field — its canonical string IS its identity, no "same value, different representation" case exists. Every other struct keeps D-169 unchanged. Fixtures for `date` equality authored after this decision, not before. No new error code (E0002 reused); count unchanged at 118 |
| D-358 | July 2026                                                          | Compiler / Stdlib — native default arguments; date module | Native functions gain optional trailing parameters with compile-time constant defaults — one default-argument mechanism in the native-dispatch path (generalising D-344's one-off `input()` prompt-defaulting arm), the compiler synthesising the missing trailing arguments from the declared constants before the ordinary `GetGlobal`-then-`Call` so runtime natives keep a fixed arity. Default arguments, not overload resolution — resolves the arity-overload question D-354 named as open. `date.parse(input: string, pattern: string = "")` gains its documented second argument (empty pattern = ISO-8601, the unchanged one-arg behaviour; non-empty → `ParseExact`; failure reuses E5702, D-284), making `grob-stdlib-reference.md`'s line-388 sample true rather than aspirational. `fs.copy`/`fs.move`'s `overwrite: bool = false` (Increment C) ride the identical mechanism, designed once with D-356's registry. Decision is pre-9C; `date.parse`'s code half lands during 9C before `csv` (Increment E). No new error code; count unchanged at 118 |
| D-359 | July 2026                                                          | Compiler — statement emission / type checking | Sprint 9 Increment A4 closes the index-target compound-assignment/increment gap D-350 named: `arr[i] += v`/`m[k] += v` and `arr[i]++`/`arr[i]--` no longer silently drop emission. Evaluate-once design — receiver and index each visited exactly once into reserved temp locals (the `DeclareLocalSlot`/`EmitGetLocal` `for...in`/switch-expression precedent, not D-334's bare `_nextSlot` bump the kickoff prompt named), then the existing `GetIndex`/typed-binary-op/`SetIndex` compose the read-modify-write, temps released via the existing `EmitScopeCleanup` (`PopN`, never lambda-capturable). `arr[i]++`/`--` lowers to `arr[i] += 1`/`-= 1` at the call site through the same helper — no dedicated index-local fast path. Type-checked via `ArrayTypeDescriptor` (D-351) reusing _E0002_ (not E0001, which is the plain-assignment RHS-assignability check the A4 kickoff prompt left ambiguous) for operand-type/int-only mismatches, and the existing `FindReadonlyRoot` walk for E0204; a map's `Unknown` element stays permissive (D-350's precedent) rather than rejected. `IndexExpr` gained a settable `ElementType` property (mirroring `MemberAccessExpr.ResolvedFieldType`), set by `VisitIndex` and consumed by a new `GetExprType` arm — necessary plumbing this increment depends on, since `GetExprType` previously had no `IndexExpr` case and silently defaulted a float array element to int arithmetic in the _plain_ binary-op path too (`floatArr[i] + 1.0`, a pre-existing gap predating D-351, fixed as a side effect). Confirmed but left unfixed and named for scheduling: field-target compound assignment/increment (`obj.field += v`/`obj.field++`) hits the identical silent-drop guard and is not, as the kickoff prompt supposed, a working precedent. Chosen over the Sprint 9B principal review's F1b tourniquet recommendation — delivering the feature closes F1b outright. No new opcode, no new error code; count unchanged at 118 |


---

## Confirmed Decisions — Full Entries

---

### D-001 — Arduino targeting ruled out (Feb 2026)

Area: Targeting
Supersedes: none
Superseded by: none

Arduino hardware targeting ruled out — transpilation to C++ is a different discipline

---

### D-002 — General-purpose scripting chosen (Feb 2026)

Area: Purpose
Supersedes: none
Superseded by: none

General-purpose scripting chosen over domain-specific language

---

### D-003 — Console calculator as MVP criterion (Feb 2026)

Area: MVP
Supersedes: none
Superseded by: none

Console-based non-scientific calculator as MVP success criterion

---

### D-004 — Module/import system in scope (Feb 2026)

Area: Modules
Supersedes: none
Superseded by: none

Module/import system in scope — late phase, not an early architecture driver

---

### D-005 — Core philosophy locked (Feb 2026)

Area: Philosophy
Supersedes: none
Superseded by: none

Core philosophy locked — “build for developers, design for hobbyists”

---

### D-006 — Stack-based bytecode VM (Feb 2026)

Area: VM strategy
Supersedes: none
Superseded by: none

Stack-based bytecode VM as centrepiece, informed by clox (Crafting Interpreters Part III)

---

### D-007 — Written in C# .NET (Feb 2026)

Area: Implementation
Supersedes: none
Superseded by: none

Written in C# .NET — .NET JIT compiles the VM loop to efficient native code

---

### D-008 — Same-line braces (Feb 2026)

Area: Syntax
Supersedes: none
Superseded by: none

Same-line braces `{` — C#/Go familiar, avoids newline terminator ambiguity

---

### D-009 — No semicolons (Feb 2026)

Area: Syntax
Supersedes: none
Superseded by: none

No semicolons — newline terminates statements, parser infers continuation

---

### D-010 — `//` comments (Feb 2026)

Area: Syntax
Supersedes: none
Superseded by: none

`//` comments — universal C-style

---

### D-011 — `:=` declares; `=` reassigns (Feb 2026)

Area: Variables
Supersedes: none
Superseded by: none

`:=` declares and assigns (first use). `=` reassigns (name must exist). No `var` keyword

---

### D-012 — No uninitialised variables (Feb 2026)

Area: Variables
Supersedes: none
Superseded by: none

No uninitialised variables — every declaration requires a value or explicit `?` nil

---

### D-013 — Mutable by default; `const` for immutable (Feb 2026)

Area: Variables
Supersedes: none
Superseded by: D-181 (partially), D-288 (partially)

> **Note:** D-288 splits the immutability surface into `const` (compile-time) and `readonly` (runtime-once). The mutable-by-default rule is unchanged; only the description of the once-assigned keyword family is superseded. Read this entry as historical context; D-288 and D-291 describe current semantics.

Mutable by default. `const` for immutable bindings

---

### D-014 — `?` suffix for nullable types (Feb 2026)

Area: Types
Supersedes: none
Superseded by: none

`?` suffix for nullable types — `string?`, `int?`. Non-optional types guaranteed non-nil

---

### D-015 — `??` and `?.` operators (Feb 2026)

Area: Types
Supersedes: none
Superseded by: none

`??` nil coalescing operator. `?.` optional chaining. Both C# familiar

---

### D-016 — `fn` keyword; typed parameters (Feb 2026)

Area: Functions
Supersedes: none
Superseded by: none

`fn` keyword. Parameters typed. Return type explicit or inferred when unambiguous

---

### D-017 — Lean on C#’s GC (Feb 2026)

Area: GC
Supersedes: none
Superseded by: none

Lean on C#’s GC. Structs for value types (int, float, bool). Classes for heap objects only

---

### D-018 — Stdlib as `IGrobPlugin` (Feb 2026)

Area: Plugin system
Supersedes: none
Superseded by: none

Standard library implemented as `IGrobPlugin` — auto-registered at VM startup

---

### D-019 — Type safety at plugin boundary (Feb 2026)

Area: Plugin system
Supersedes: none
Superseded by: none

Type safety enforced at plugin boundary — plugin provides signature, type checker verifies at compile time

---

### D-020 — `.grobc` binary format (Feb 2026)

Area: Bytecode format
Supersedes: none
Superseded by: none

`.grobc` binary format. Magic number `GROB` (0x47 0x52 0x4F 0x42). Used for optional caching

---

### D-021 — Compile in-memory and run (Feb 2026)

Area: Execution model
Supersedes: none
Superseded by: none

Primary use case: compile in-memory and run. No disk write unless explicitly requested

---

### D-022 — Fluent chaining requires collections API (Feb 2026)

Area: Fluent syntax
Supersedes: none
Superseded by: none

Fluent chaining yes — but not day one. Requires collections API first

---

### D-023 — C# LINQ as collections north star (Feb 2026)

Area: Collections
Supersedes: none
Superseded by: none

C# LINQ is the design north star for the collections API

---

### D-024 — Open source; release when solid (Feb 2026)

Area: Release
Supersedes: none
Superseded by: none

Open source. Release when core is solid, not before

---

### D-025 — `--plugin` retired; `--dev-plugin` for development (Apr 2026)

Area: Plugin loading
Supersedes: none
Superseded by: none

`--plugin` flag retired from public API. Internal mechanism only, used by `grob install`. Never a script author concern. Dev escape hatch: `--dev-plugin path/to/local.dll` for plugin development only, documented as such.

---

### D-026 — `import` is the single non-core dependency mechanism (Apr 2026)

Area: Import statement
Supersedes: none
Superseded by: none

`import` is the single declaration mechanism for all non-core dependencies. Signals compile-time type resolution — the type checker loads plugin signatures at compile time, not runtime.

---

### D-027 — 13 core modules auto-available (Apr 2026)

Area: Core modules
Supersedes: none
Superseded by: none

Core modules are auto-available — no import required. `fs`, `strings`, `json`, `csv`, `env`, `process`, `date`, `math`, `log`, `regex`, `path`, `format`, `guid`. If a reasonable developer expects it in any scripting language, it’s core.

---

### D-028 — Import signal value (Apr 2026)

Area: Import signal value
Supersedes: none
Superseded by: none

A script with no imports is self-contained. A script with imports has external dependencies. `import` lines double as a dependency manifest. This signal value is lost if core modules also require import.

---

### D-029 — Default import alias (Apr 2026)

Area: Plugin import alias
Supersedes: none
Superseded by: none

Default alias is the last segment of the module name, lowercased. `import Grob.Http` → `http.*`. Convention not configuration — always predictable. `Grob.Http` is a special case: it exposes both `http.*` and `auth.*` as sub-namespaces from a single import. This is the only case where one `import` produces two namespace prefixes.

---

### D-030 — Explicit alias for collision resolution only (Apr 2026)

Area: Explicit alias
Supersedes: none
Superseded by: none

`import Grob.Http as client` — available for collision resolution. Not for personality. Legitimate uses: two plugins share a last segment, or a plugin alias clashes with a core module name.

---

### D-031 — `import` over `requires` (Apr 2026)

Area: Import vs requires
Supersedes: none
Superseded by: none

`import` chosen over `requires`. Signals compile-time resolution not runtime assertion. Universally understood. Fits the statically typed identity of the language. `requires` belongs in dynamic languages.

---

### D-032 — `grob install`; never silently downloads (Apr 2026)

Area: Package install
Supersedes: none
Superseded by: none

`grob install Grob.Http` — installs globally to `~/.grob/packages/`. `grob install --local` — installs to project only. Never silently downloads at runtime. Explicit install step always required.

---

### D-033 — Package resolution order (Apr 2026)

Area: Package resolution
Supersedes: none
Superseded by: none

On `import`, Grob checks: (1) project `grob.json` dependencies, (2) `~/.grob/packages/`. If not found — compile error with helpful message: `Grob.Http is not installed. Run: grob install Grob.Http`

---

### D-034 — `grob.json` project manifest (Apr 2026)

Area: Project manifest
Supersedes: none
Superseded by: none

`grob.json` — optional, for projects with multiple scripts sharing dependencies. Declares name, version, dependencies with version constraints. `grob install` with no args resolves everything in `grob.json`.

---

### D-035 — NuGet as package registry (Apr 2026)

Area: Package registry
Supersedes: none
Superseded by: none

NuGet for hosting and distribution. Packages tagged `grob-plugin` discoverable via `grob search`. Zero infrastructure to maintain. Versioning, hosting, push all provided by NuGet ecosystem.

---

### D-036 — `grob.json` shape (Apr 2026)

Area: grob.json shape
Supersedes: none
Superseded by: none

`{ "name": "my-project", "version": "1.0.0", "dependencies": { "Grob.Http": "^1.0.0" } }` — npm-influenced. Semantic versioning. `^` for compatible versions.

---

### D-037 — Visitor pattern for AST (Apr 2026)

Area: AST pattern
Supersedes: none
Superseded by: none

Visitor pattern for Grob’s AST — not switch expressions. Grob has three passes (type checker, optimiser, compiler). Visitor earns its place when multiple passes walk the same AST. SharpBASIC had one pass; switch expressions were sufficient there.

---

### D-038 — `:=` declares in current scope; `=` walks parent chain (Apr 2026)

Area: Scope
Supersedes: none
Superseded by: none

`:=` always declares in current local scope. `=` reassigns by walking the parent chain to find the name wherever it lives. No `SET GLOBAL` equivalent needed. Mandatory `:=` declaration makes chain-walking unambiguous.

---

### D-039 — Two-mode error strategy (Apr 2026)

Area: Error strategy
Supersedes: none
Superseded by: none

Two-mode: compiler/type checker collects ALL errors before execution (never stops at first). VM stops on FIRST runtime error. A program with type errors never reaches the VM.

**Clarification (Sprint 7 Increment D):** this decision predates `try`/`catch`.
"The VM stops on the first runtime error" now reads "the first **unhandled**
runtime error" — a runtime error caught by a `catch` is handled by the script and
execution resumes normally; only one that reaches the top level uncaught halts
the VM. This is a reading clarification of the existing decision, not a new
design — no new decision number. See `grob-language-fundamentals.md` §27 and
E5904 in `grob-error-codes.md` for the unhandled-exception path this clarifies.

---

### D-040 — Test compiler outputs exhaustively (Apr 2026)

Area: Compiler tests
Supersedes: none
Superseded by: none

Test compiler outputs exhaustively — given source, assert correct bytecode. Bugs will live in the compiler, not the VM loop. VM loop can be trusted once verified on simple cases.

---

### D-041 — Compiler as `partial class` files (Apr 2026)

Area: Partial classes
Supersedes: none
Superseded by: none

Grob’s compiler implemented as `partial class` files for physical separation of concerns. Same namespace, same architecture, better maintainability.

---

### D-042 — Real-program target required before implementation (Apr 2026)

Area: Real program target
Supersedes: none
Superseded by: none

Grob needs a real-program target defined before implementation begins — not after. The Sunken Crown was the most valuable design tool in SharpBASIC. Real programs reveal language gaps that toy programs hide.

---

### D-043 — OQ-002 resolved: user-defined struct types (Apr 2026)

Area: OQ-002 resolved
Supersedes: none
Superseded by: none

User-defined struct/record types confirmed. Evidence: parallel arrays in The Sunken Crown were “messy, wasteful, and slow.” `type` keyword, structural types, fields declared in block.

---

### D-044 — No empty semantic analyser placeholder (Apr 2026)

Area: Semantic analyser
Supersedes: none
Superseded by: none

No empty semantic analyser placeholder. At SharpBASIC’s scale it added nothing. For Grob — statically typed — the type-checking pass is the semantic analyser and earns its place explicitly.

---

### D-045 — Real-world target use cases (Apr 2026)

Area: Use cases
Supersedes: none
Superseded by: none

Real-world target: Azure CLI/Bicep scripting, API wrapping (ADO), agent hook scripts

---

### D-046 — Pipeline model (Apr 2026)

Area: Pipeline model
Supersedes: none
Superseded by: none

Grob scripts are composable pipeline stages: structured data in → process → structured data out. Primary input pattern is file-read (`json.read()`, `csv.read()`) — portable across all OSs. `json.stdin()` and `csv.stdin()` exist for genuine shell pipeline composition. Examples lead with file-read; stdin shown as the pipeline variant. Target environment is Windows-native — `cat` never appears in Grob documentation or examples.

---

### D-047 — String interpolation `${name}` (Apr 2026)

Area: String interpolation
Supersedes: none
Superseded by: none

`"Hello ${name}"` — confirmed load-bearing from real-world script sketches. Not optional

---

### D-048 — MIT licence (Apr 2026)

Area: Licensing
Supersedes: none
Superseded by: none

MIT licence. Maximum permissiveness. No copyleft. Standard for hobbyist scripting languages

---

### D-049 — First-party plugins in `plugins/` (Apr 2026)

Area: Open source model
Supersedes: none
Superseded by: none

Core runtime in main repo. First-party plugins in `plugins/` directory of the main repo

---

### D-050 — Community plugin registry via `PLUGINS.md` PR (Apr 2026)

Area: Community plugins
Supersedes: none
Superseded by: none

Independent repos. Registry via `PLUGINS.md` PR — low bar (repo exists, README, licence)

---

### D-051 — `Grob.Runtime` NuGet package (Apr 2026)

Area: Plugin SDK
Supersedes: none
Superseded by: none

`Grob.Runtime` NuGet package — public contract for third-party plugin authors. Versioned independently

---

### D-052 — Fork → branch → PR; CLA on first PR (Apr 2026)

Area: Contributions
Supersedes: none
Superseded by: none

Fork → branch → PR. CLA via one-time confirmation on PR submission. No separate document

---

### D-053 — Sparky the mascot (Apr 2026)

Area: Mascot
Supersedes: none
Superseded by: none

Sparky — raccoon, blue hoodie, utility belt, wrench. Character sheet v1 complete

---

### D-054 — `G>` logo mark (Apr 2026)

Area: Logo mark
Supersedes: none
Superseded by: none

`G>` — forward chevron on G. Works at 32px. Used as favicon, badge, terminal prompt, laptop lid detail

---

### D-055 — `G>` REPL prompt (Apr 2026)

Area: REPL prompt
Supersedes: none
Superseded by: none

`G>` — matches the logo mark. Every REPL line is Sparky’s world

---

### D-056 — Windows Terminal profile (Apr 2026)

Area: Windows Terminal
Supersedes: none
Superseded by: none

Grob ships a Windows Terminal profile — name, icon, colour scheme, `grob repl` as startup command

---

### D-057 — Terminal colour scheme (Apr 2026)

Area: Terminal colours
Supersedes: none
Superseded by: none

Denim blue as accent, warm amber for warnings, raccoon greys for background

---

### D-058 — Three-mode personality (Apr 2026)

Area: Personality
Supersedes: none
Superseded by: none

Three modes, one character — seasoned engineer (errors), enthusiastic teacher (learning), scrappy builder (flow)

---

### D-059 — Helpful, explanatory error messages (Apr 2026)

Area: Error messages
Supersedes: none
Superseded by: none

Helpful and explanatory — what went wrong, where, why, suggested fix when obvious

---

### D-060 — First-run acknowledgement (Apr 2026)

Area: First run
Supersedes: none
Superseded by: none

Celebrated once with a quiet acknowledgement. Never repeated. `✦ First script. Nice work.`

---

### D-061 — Opinionated defaults (Apr 2026)

Area: Opinions
Supersedes: none
Superseded by: D-283 (in part — snake_case warning dropped)

> **Note:** D-283 drops the `snake_case` compiler warning. All other opinionated defaults (nil safety as non-negotiable error, unused variable warnings, shadowing warnings) are unchanged. The snake_case opinion moves to `grob fmt` and documentation.

Opinionated defaults — `snake_case` warned not errored. Nil safety and types are non-negotiable errors

---

### D-062 — `grob fmt` opt-in formatter (Apr 2026)

Area: Formatter
Supersedes: none
Superseded by: none

`grob fmt` — formats code. Never automatic, always opt-in

---

### D-063 — CLI output philosophy (Apr 2026)

Area: CLI output
Supersedes: none
Superseded by: none

Quiet on success, clear on failure. Errors to stderr, results to stdout. Pipeline-friendly always

---

### D-064 — Never list (Apr 2026)

Area: Never list
Supersedes: none
Superseded by: none

No emoji in compiler/CLI output. Never “simply” in docs. Never silence an error

---

### D-065 — AI tutor deferred (Apr 2026)

Area: AI tutor
Supersedes: none
Superseded by: none

Deferred idea — guided learning companion. Parked in grob-personality-identity.md

---

### D-066 — Primitives never boxed (Apr 2026)

Area: Primitive model
Supersedes: none
Superseded by: none

Primitives are never boxed. Method-call syntax on all types is syntactic sugar — compiler rewrites to native function calls at compile time. Zero runtime overhead

---

### D-067 — Method-call syntax on all types (Apr 2026)

Area: Method syntax
Supersedes: none
Superseded by: none

All types support method-call syntax. `"42".toInt()`, `42.toString()`, `3.14.round()`. Compiler resolves at compile time using type information. No vtable, no heap allocation

---

### D-068 — Properties vs methods (Apr 2026)

Area: Properties vs methods
Supersedes: none
Superseded by: none

`length`, `isEmpty` etc are properties — no `()` required. Compiler distinguishes property access from method call based on type registration

---

### D-069 — Conversions are methods on the source type (Apr 2026)

Area: Conversion rule
Supersedes: none
Superseded by: none

Conversions are methods on the source type — convert _from_ a value. `"42".toInt()` is the only syntax. `int.parse("42")` is a compile error with a helpful suggestion

---

### D-070 — Static utilities on the type namespace (Apr 2026)

Area: Static utilities
Supersedes: none
Superseded by: none

Functions with no natural receiver live on the type as a namespace — `int.min(a, b)`, `int.max(a, b)`, `int.clamp(v, lo, hi)`. No overlap with instance methods

---

### D-071 — One rule for conversions and utilities (Apr 2026)

Area: One rule
Supersedes: none
Superseded by: none

_Conversions are methods on the source value. Static utilities live on the type namespace. There is no overlap._ Compiler enforces this. Docs explain it in one sentence

---

### D-072 — Security posture (Apr 2026)

Area: Security posture
Supersedes: none
Superseded by: none

Trust the script author, document the risks, make the safe path the obvious path. No sandbox claims ever made.

---

### D-073 — Plugin loading is arbitrary code execution (Apr 2026)

Area: Plugin security
Supersedes: none
Superseded by: none

Loading a plugin is equivalent to running arbitrary code. Documented prominently in PLUGINS.md and plugin authoring guide. No .NET plugin sandboxing attempted — not worth the complexity for this use case.

---

### D-074 — `env.require()` canonical credential pattern (Apr 2026)

Area: Credential handling
Supersedes: none
Superseded by: none

`env.require()` is the canonical pattern for credentials. Never hardcode in script source. `env` module docs are explicit. `grob check` may optionally warn on string literals matching common token patterns — linter concern, not compiler concern.

---

### D-075 — `process.run()` naming (Apr 2026)

Area: process module naming
Supersedes: D-076
Superseded by: none

`process.run(cmd, args[])` is the primary safe form — arguments are never shell-interpolated, prevents command injection. `process.runShell(cmd)` is the convenience form for full command strings where shell interpretation is wanted — name makes the shell involvement explicit. Fail-fast variants: `process.runOrFail(cmd, args[])` and `process.runShellOrFail(cmd)`. Returns `ProcessResult` with `stdout: string`, `stderr: string`, `exitCode: int`. The safe path has the shorter name. Supersedes the earlier `process.runArgs()` entry.

---

### D-076 — `process.runArgs()` naming (Apr 2026) — SUPERSEDED

Area: process module naming
Supersedes: none
Superseded by: D-075

_(This entry is superseded by D-075. `process.runArgs()` is not the correct naming — see D-075.)_

---

### D-077 — Error messages show names, not values (Apr 2026)

Area: Errors — no values
Supersedes: none
Superseded by: none

Error messages show variable names and types, never values. Prevents accidental credential exposure in terminal output and logs. `--verbose` flag overrides for debugging.

---

### D-078 — Community registry is not a safety endorsement (Apr 2026)

Area: Community registry
Supersedes: none
Superseded by: none

PLUGINS.md registry listing is not a safety endorsement. Explicit warning in registry: loading a plugin is running arbitrary code. Quality and security are the author’s responsibility.

---

### D-079 — Type method registry (Apr 2026)

Area: Type method registry
Supersedes: none
Superseded by: none

Each built-in type has a defined set of methods and properties known to the type checker at compile time. Calling an undefined method is a compile error, not a runtime error

---

### D-080 — OQ-001 resolved: constrained generics (Apr 2026)

Area: OQ-001 resolved
Supersedes: none
Superseded by: none

Constrained generics confirmed. Type checker and compiler understand generic type parameters internally. Users consume generic functions via stdlib and plugins (`mapAs<T>()`, `filter`, `map` etc) but cannot declare generic functions or types in v1. Evolution to user-facing generics is additive — grammar extension only, no architectural rework required.

---

### D-081 — Generics at plugin boundary (Apr 2026)

Area: Generics — plugin boundary
Supersedes: none
Superseded by: none

Plugins that expose generic functions must express type parameters via `FunctionSignature` in `Grob.Runtime`. This must be designed into `Grob.Runtime` from the start, not retrofitted.

---

### D-082 — OQ-004 resolved: exceptions as runtime error model (Apr 2026)

Area: OQ-004 resolved
Supersedes: none
Superseded by: none

Exceptions as the runtime error model. Functions throw on failure. Unhandled exceptions propagate to the VM top level — Grob-quality diagnostic produced, script halts. `try/catch` available for recovery when needed.

---

### D-083 — try/catch structure (Apr 2026)

Area: try/catch
Supersedes: none
Superseded by: none

Multiple catch blocks supported. Typed catches supported. Bare `catch e` is the catch-all — must appear last. A catch block after a catch-all is a compiler error, not a warning.

---

### D-084 — Exception hierarchy (Apr 2026)

Area: Exception hierarchy
Supersedes: none
Superseded by: D-284

> **Superseded by D-284.** The six-leaf hierarchy is expanded to ten leaves. `RuntimeError` is split into `ArithmeticError`, `IndexError`, `ParseError`, `LookupError`, and a residual `RuntimeError`. See D-284 for the authoritative hierarchy.

Exception type hierarchy is a `Grob.Runtime` concern — stdlib, not language grammar. V1 hierarchy: `GrobError` as root, with leaves `IoError`, `NetworkError`, `JsonError`, `ProcessError`, `NilError`, `RuntimeError`.

---

### D-085 — User-defined exceptions post-MVP (Apr 2026)

Area: User-defined exceptions
Supersedes: none
Superseded by: none

User-defined exception types are post-MVP. Throwing custom typed errors is a programming language feature, not a scripting language feature. The use case is rare enough in scripting that deferring costs nothing.

---

### D-086 — `csv` is core stdlib (Apr 2026)

Area: csv module
Supersedes: none
Superseded by: none

`csv` is core stdlib alongside `json`. Headers assumed by default. Overrides via named parameters: `hasHeaders: false`, `delimiter: "\t"`. RFC 4180 compliance baseline. Same `mapAs<T>()` pipeline pattern as `json`.

---

### D-087 — Named parameters confirmed (Apr 2026)

Area: Named parameters
Supersedes: none
Superseded by: none

Named parameters confirmed as a language feature. First surfaced in the `csv` API. Only specify parameters that differ from defaults. No options object, no builder pattern.

---

### D-088 — `log` is core stdlib (Apr 2026)

Area: log module
Supersedes: none
Superseded by: none

`log` is core stdlib. Distinct output streams for info, warning, error. Designed for unattended scripts — scheduled tasks, agent hooks, CI pipelines. Not a substitute for `print()` — structured diagnostic output.

---

### D-089 — `regex` is core stdlib (Apr 2026)

Area: regex module
Supersedes: none
Superseded by: none

`regex` is core stdlib. Regular expressions for match, replace, extract. Distinct from `strings` simple operations. Sysadmins reach for regex constantly — log parsing, filename matching, data extraction.

---

### D-090 — `path` is core stdlib (Apr 2026)

Area: path module
Supersedes: none
Superseded by: none

`path` is core stdlib. Path string manipulation — join, split, extension, directory, normalise separators. Distinct from `fs` file system operations. Complements `fs` — always needed alongside it.

---

### D-091 — `strings` module — full API (Apr 2026)

Area: strings module
Supersedes: none
Superseded by: none

`strings` module contains one function: `strings.join(parts: string[], separator: string = "") → string`. All other string operations are instance methods on the `string` type. `strings.join()` lives on the module because its receiver is an array, not a string instance. The `string` type has no `strings.split()` complement on the module — `"value".split(sep)` is already an instance method on the type. The following methods are confirmed additions to the `string` type registry: `trimStart()`, `trimEnd()`, `substring(start: int, length: int)`, `indexOf(s: string)`, `lastIndexOf(s: string)`, `padLeft(width: int, char: string = " ")`, `padRight(width: int, char: string = " ")`, `repeat(count: int)`, `truncate(maxLength: int, suffix: string = "...")`.

---

### D-092 — `csv` module — full API (Apr 2026)

Area: csv module full API
Supersedes: none
Superseded by: none

`csv.read(path: string, hasHeaders: bool = true, delimiter: string = ",") → csv.Table` reads a file; throws `IoError` on failure. `csv.parse(content: string, hasHeaders: bool = true, delimiter: string = ",") → csv.Table` for in-memory strings. `csv.stdin(hasHeaders: bool = true, delimiter: string = ",") → csv.Table` for pipeline input. `csv.write(path: string, rows: T[], hasHeaders: bool = true, delimiter: string = ",") → void` writes to file. `csv.stdout(rows: T[], hasHeaders: bool = true, delimiter: string = ",") → void` writes to stdout. `csv.Table` type exposes: `headers: string[]`, `rowCount: int`, `rows: CsvRow[]`, `mapAs<T>() → T[]`. `CsvRow` supports `get(name: string) → string`, `get(index: int) → string`, and `row[name]` / `row[index]` indexer syntax. RFC 4180 baseline: quoted fields, embedded commas, embedded newlines, `""` escape for double-quote. `hasHeaders` defaults true. `csv.stdin()` and `csv.stdout()` are valid on all platforms — primary usage on Windows is file-based (`csv.read()`/`csv.write()`); stdin/stdout forms are for pipeline composition and agent use cases. `csv.parse()` closes the in-memory parsing gap (e.g. CSV-formatted process output).

---

### D-093 — `math` module — full API (Apr 2026)

Area: math module full API
Supersedes: none
Superseded by: none

Constants: `math.pi`, `math.e`, `math.tau`. Functions: `math.sqrt(n: float) → float` (throws `RuntimeError` if n < 0), `math.pow(base: float, exp: float) → float`, `math.log(n: float) → float` (natural log; throws `RuntimeError` if n ≤ 0), `math.log10(n: float) → float`. Trigonometry (radians): `math.sin()`, `math.cos()`, `math.tan()`, `math.asin()`, `math.acos()`, `math.atan()`, `math.atan2(y, x)`, `math.toRadians(degrees: float) → float`, `math.toDegrees(radians: float) → float`. Random: `math.random() → float` ([0.0, 1.0), uniform), `math.randomInt(min: int, max: int) → int` (inclusive both ends), `math.randomSeed(seed: int) → void` (deterministic testing). `math` does NOT duplicate `abs`, `floor`, `ceil`, `round`, `clamp`, `min`, or `max` — those live on the type registry as instance or static methods. No overlap with type-level functions by design.

---

### D-094 — `log` module — full API (Apr 2026)

Area: log module full API
Supersedes: none
Superseded by: none

Four levels: `log.debug(message: string)`, `log.info(message: string)`, `log.warning(message: string)`, `log.error(message: string)`. All output to stderr. `print()` is stdout for script results; `log.*` is stderr for operational messages — these never mix. `log.debug()` suppressed by default; visible only under `--verbose`. Output format: `[LEVEL]  message` with no timestamp by default. `log.setLevel(level: string) → void` sets runtime threshold — accepts `"debug"`, `"info"`, `"warning"`, `"error"`; suppresses all levels below the threshold. `log.error()` logs only — it does not throw or halt execution. To halt a script on error, combine with `exit(1)` or `throw`. File output is not in scope for v1. No structured/JSON logging in v1.

---

### D-095 — `regex` module — full API (Apr 2026)

Area: regex module full API
Supersedes: none
Superseded by: none

Regex literals: `/pattern/flags` — creates a `Regex` value, compiled once at declaration. Supported flags: `i` (case-insensitive), `m` (multiline `^`/`$`). `Regex` type methods: `match(input: string) → Match?`, `matchAll(input: string) → Match[]`, `isMatch(input: string) → bool`, `replace(input: string, replacement: string) → string`, `replaceAll(input: string, replacement: string) → string`, `split(input: string) → string[]`. `Regex` type properties: `pattern: string`, `flags: string`. `Match` type: `value: string`, `index: int`, `length: int`, `groups: string[]` (index 0 is full match, 1+ are capture groups), `group(name: string) → string?` for named groups. Module-level convenience functions for one-shot use — take string patterns, compile on each call: `regex.isMatch(pattern, input)`, `regex.match(pattern, input)`, `regex.matchAll(pattern, input)`, `regex.replace(pattern, input, replacement)`, `regex.replaceAll(pattern, input, replacement)`, `regex.split(pattern, input)`, `regex.escape(input: string) → string`. .NET regex engine — full feature set exposed including named groups and lookaheads. String literals are never implicitly treated as regex patterns. Regex literal syntax is a grammar addition — `/` is disambiguated by context (after an operator, assignment, or opening paren it is a regex literal; after a value it is the division operator).

---

### D-096 — `path` module — full API (Apr 2026)

Area: path module full API
Supersedes: none
Superseded by: none

Functions: `path.join(parts: string...) → string` (variadic, OS separator, normalises separators in each segment), `path.joinAll(parts: string[]) → string` (array form for dynamic segment lists), `path.extension(p: string) → string` (lowercased, includes dot; empty string if none), `path.filename(p: string) → string` (final segment including extension), `path.stem(p: string) → string` (final segment without extension), `path.directory(p: string) → string` (parent directory portion), `path.resolve(p: string) → string` (absolute path relative to CWD — no I/O, does not check existence), `path.normalise(p: string) → string` (OS separator convention, collapses `..` and `.`), `path.isAbsolute(p: string) → bool`, `path.isRelative(p: string) → bool`, `path.changeExtension(p: string, ext: string) → string` (ext should include dot). Constant: `path.separator → string` (OS-dependent: `\` on Windows, `/` on POSIX). No file system I/O — `path` operates on strings only. Complements `File` type properties: `File.extension` etc. are convenience on known file objects; `path.*` functions operate on arbitrary path strings from any source (process output, config files, user input).

---

### D-097 — First-party plugins: `Grob.Crypto` and `Grob.Zip` (Apr 2026)

Area: First-party plugins
Supersedes: none
Superseded by: none

`Grob.Crypto` — checksums and hashing (MD5, SHA256, file integrity). `Grob.Zip` — compress and expand zip archives. Both first-party plugins, not core. Both live in `plugins/` in the main repo.

---

### D-098 — Script `param` block (Apr 2026)

Area: Script parameters
Supersedes: none
Superseded by: none

Scripts declare parameters in a `param` block at the top. Typed, defaultable. Required params have no default. `param env: string` / `param dryRun: bool = false`. Type checker validates at compile time — wrong type or missing required param is a compile error before execution.

---

### D-099 — `.grobparams` file format (Apr 2026)

Area: Param files
Supersedes: none
Superseded by: none

`.grobparams` file format — key-value pairs, `//` comments, native Grob feel. Not JSON, not YAML, not TOML. `grob run deploy.grob --params deploy.grobparams`. Committable to source control. Readable and diffable by design.

---

### D-100 — CLI overrides param file (Apr 2026)

Area: Param override
Supersedes: none
Superseded by: none

Command line overrides param file values. `grob run deploy.grob --params deploy.grobparams --env staging`. Param file provides defaults; command line overrides specifics. Bicep-style composability.

---

### D-101 — `@secure` decorator (Apr 2026)

Area: @secure decorator
Supersedes: none
Superseded by: none

`@secure` on a param is a handling instruction, not a type. Value is still `string` at runtime. Effect: not echoed in output, not included in error messages, not logged. Compiler warns if a `@secure` param appears in a `.grobparams` file in plain text. No `securestring` type — decorator approach avoids type system complexity for a scripting language.

---

### D-102 — Param decorator set (Apr 2026)

Area: Param decorators
Supersedes: none
Superseded by: none

Decorator system on params confirmed. V1 set: `@secure`, `@allowed(...)`, `@minLength(n)`, `@maxLength(n)`. Validated at the parameter boundary before the script body runs. Compiler error on violation — not runtime.

---

### D-103 — Secure param absent from `.grobparams` (Apr 2026)

Area: Secure param pattern
Supersedes: none
Superseded by: none

`@secure` params should be absent from `.grobparams` files — provide via command line or `env.require()` instead. Tooling warns if a `@secure` param is present in plain text in a params file. `env.require()` remains the canonical pattern for credentials in scripts.

---

### D-104 — No pipe operator in Grob scripts (Apr 2026)

Area: Pipe operator
Supersedes: none
Superseded by: none

No `|` pipe operator inside Grob scripts. Fluent chaining is the in-script composition idiom. Scripts are pipeline stages at the OS level via stdin/stdout — not internally. `|` is not a valid operator in the grammar.

---

### D-105 — `format` is core stdlib (Apr 2026)

Area: format module
Supersedes: none
Superseded by: none

`format` is core stdlib. Human-readable output formatters distinct from `json.stdout()` (machine-readable). `format.table()`, `format.list()`, `format.csv()`. Numeric and date formatting. Works fluently after `.select()` projection on collections.

---

### D-106 — `.select()` projection (Apr 2026)

Area: select() projection
Supersedes: none
Superseded by: none

`.select()` on collections maps to a typed projection — pick fields, optionally rename. PowerShell `Select-Object` equivalent but typed. `results.select(r => #{ repo: r.name, stale: r.staleCount }).format.table()`. Filter first, select fields, then format.

---

### D-107 — Single `date` type (Apr 2026)

Area: date module — type
Supersedes: none
Superseded by: none

Single `date` type holds both date and time. `date.today()` zeroes the time component. No separate `datetime` type — two types is a common source of conversion friction. One type, two constructors.

---

### D-108 — `date` module — full API (Apr 2026)

Area: date module — API
Supersedes: none
Superseded by: D-354

Full date/time API locked. Construction: `date.now()`, `date.today()`, `date.of(y,m,d)`, `date.ofTime(y,m,d,h,min,s)`. Parsing: `date.parse(str)` ISO 8601 default, `date.parse(str, pattern)` explicit. Formatting: `toIso()`, `toIsoDateTime()`, `format(pattern)`. Arithmetic: `addDays()`, `minusDays()`, `addMonths()`, `addHours()`, `addMinutes()`. Comparison: `<`, `>`, `==`, `isBefore()`, `isAfter()`. Components: `year`, `month`, `day`, `hour`, `minute`, `second`, `dayOfWeek`, `dayOfYear` as properties. Epoch: `toUnixSeconds()`, `toUnixMillis()`, `date.fromUnixSeconds(n)`, `date.fromUnixMillis(n)`. Timezone: `toUtc()`, `toLocal()`, `toZone("Europe/London")`, `utcOffset` property. Zone names preferred; offset integers supported for API interop.

---

### D-109 — `fs` module API shape (Apr 2026)

Area: fs module — API shape
Supersedes: none
Superseded by: none

`fs.list(path)` returns `File[]`. `File` is a built-in type known to the type checker at compile time — registered by the fs stdlib plugin at startup, same mechanism as `date`. Properties: `name`, `path`, `directory`, `extension`, `size`, `modified`, `created`, `isDirectory`. Methods: `rename()`, `moveTo()`, `copyTo()`, `delete()`. Module functions: `fs.list()`, `fs.exists()`, `fs.isFile()`, `fs.isDirectory()`, `fs.ensureDir()`, `fs.createDir()`, `fs.delete()`, `fs.deleteRecursive()`, `fs.readText()`, `fs.readLines()`, `fs.writeText()`, `fs.appendText()`, `fs.copy()`, `fs.move()`. Full signatures in `grob-stdlib-reference.md`. Calling undefined members on `File` is a compile error.

---

### D-110 — `exit()` built-in (Apr 2026)

Area: Script exit
Supersedes: none
Superseded by: none

`exit(n)` is a built-in function — no namespace, always available, same category as `print()`. `exit()` with no argument exits with code 0. Normal script completion exits with 0. Unhandled `GrobError` exits with 1. When called inside a function, `exit()` throws an uncatchable internal `ExitSignal` that unwinds the entire call stack — it cannot be caught by `try/catch`. The VM catches it at the top level, flushes output buffers, and terminates with the specified code.

---

### D-111 — Conditional expressions (Apr 2026)

Area: Conditional expressions
Supersedes: none
Superseded by: none

Two conditional expression forms. Ternary `? :` for simple two-way inline choices. Switch expression for multi-branch value selection, C# style: `value switch { pattern => result, _ => default }`. `_` is the catch-all arm. Type checker enforces exhaustiveness — missing catch-all is a compile error. All arms must return the same type. No `if/else` in expression position.

---

### D-112 — Array indexing (Apr 2026)

Area: Array indexing
Supersedes: none
Superseded by: none

`arr[n]` confirmed as array access syntax. `()` is function calls; `[]` is indexing — no overlap. Parser produces `IndexExpression` for `name[...]` and `CallExpression` for `name(...)` independently. Multi-dimensional: `matrix[r][c]`. Zero-based.

---

### D-113 — Named parameter calling convention (Apr 2026)

Area: Named parameter calling convention
Supersedes: none
Superseded by: none

Positional arguments first (in declaration order), named arguments after. Named arguments are unordered relative to each other. Only parameters with default values may be named — required parameters (no default) are positional-only. Providing a named argument before a positional, naming a required parameter, duplicate names, or unknown parameter names are all compile errors.

---

### D-114 — Anonymous struct literals (Apr 2026)

Area: Anonymous struct literals
Supersedes: none
Superseded by: none

`#{ field: value }` syntax distinguishes anonymous structs from block syntax `{ }`. The type checker creates an internal structural type for each anonymous struct. Field access is type-safe; accessing undefined fields is a compile error. `select()` and `map()` returning anonymous structs produce typed arrays. `format.table()` reads field names from the anonymous struct type at compile time.

---

### D-115 — Lambdas and closures (Apr 2026)

Area: Lambdas and closures
Supersedes: none
Superseded by: none

Lambda syntax: `x => expression`, `x => { block }`, `(a, b) => expression`. Closures supported — upvalue mechanism follows clox design. Each capturing lambda becomes a `Closure` object at runtime — a `BytecodeFunction` plus its upvalue array. Compiler emits `CAPTURE_UPVALUE` instructions. `{ }` after a lambda arrow is always a block body; `#{ }` is always an anonymous struct literal — no parser ambiguity.

---

### D-116 — `format` module calling convention (Apr 2026)

Area: format module — calling convention
Supersedes: none
Superseded by: none

`.format.table()` chained form is canonical. The compiler treats `.format` as a known namespace prefix — not a runtime property. Rewrites to `format.table(x)` at compile time. No boxing. Type checker registers `T[].format.table()`, `T[].format.list()`, `T[].format.csv()` for array types. Column names derived from type’s field registry at compile time.

---

### D-117 — `date` interval computation (Apr 2026)

Area: date — interval computation
Supersedes: none
Superseded by: none

`daysUntil(other: date) → int` and `daysSince(other: date) → int` added to the `date` type registry. Neither throws on direction reversal. Full `Interval`/`Duration` type deferred to post-MVP.

---

### D-118 — `Grob.Http` API shape (Apr 2026)

Area: Grob.Http — API shape
Supersedes: none
Superseded by: D-155

First-party plugin. Full REST support. Returns `Response` type with `statusCode`, `isSuccess`, `headers`, `asText()`, `asJson()`. `auth` is a sub-namespace of `Grob.Http` — `import Grob.Http` makes both `http.*` and `auth.*` available. `auth.bearer()`, `auth.basic()`, `auth.apiKey()`. `AuthHeader` is an opaque type. Full signatures in `grob-stdlib-reference.md`.

---

### D-119 — `string.left()` and `string.right()` (Apr 2026)

Area: string — left() and right()
Supersedes: none
Superseded by: none

`left(n: int) → string` and `right(n: int) → string` added to the `string` type registry. Both throw `RuntimeError` if `n > length`. Range/span indexing deferred to post-MVP.

---

### D-120 — Language fundamentals document authorised (Apr 2026)

Area: Language fundamentals
Supersedes: none
Superseded by: none

Full specification in `grob-language-fundamentals.md`. All decisions in that document are authorised here — the decisions log remains the authority on conflict.

---

### D-121 — Three-tier install scope (Apr 2026)

Area: Install scope model
Supersedes: none
Superseded by: none

Three-tier plugin install scope. User-global (default): `%USERPROFILE%\.grob\packages\`. System-wide: `%ProgramFiles%\Grob\packages\`. Project-local: `.grob\packages\` relative to `grob.json`. Resolution order: local → user → system. Full detail in `grob-install-strategy.md`.

---

### D-122 — `grob.json` manifest walk (Apr 2026)

Area: grob.json manifest walk
Supersedes: none
Superseded by: none

Grob walks up the directory tree from the script file’s location to find `grob.json` — not from the current working directory. Walk stops at the filesystem root.

---

### D-123 — Runtime install via `winget` (Apr 2026)

Area: grob runtime install
Supersedes: none
Superseded by: none

Runtime (`grob.exe`) delivered via `winget install Grob.Grob`. User install: `%USERPROFILE%\.grob\bin\`. System install: `%ProgramFiles%\Grob\bin\`. `grob restore` installs all `grob.json` dependencies — idempotent, CI-safe.

---

### D-124 — Nested struct field access (Apr 2026)

Area: Nested struct field access
Supersedes: none
Superseded by: none

Field access chains on nested named types are fully supported. `issue.user.login` where `Issue` declares `user: IssueUser` is valid — type checker resolves each step against the declared field type. Accessing an undeclared field at any level is a compile error.

---

### D-125 — Solution structure (Apr 2026)

Area: Solution structure
Supersedes: none
Superseded by: none

Six `src/` assemblies: `Grob.Core`, `Grob.Runtime`, `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Cli`. Three `plugins/` assemblies: `Grob.Http`, `Grob.Crypto`, `Grob.Zip`. Five `tests/` projects. Dependency graph is a DAG — compiler and VM never reference each other. `Chunk` lives in `Grob.Core` as the shared boundary between compiler output and VM input. See `grob-solution-architecture.md` and ADR-0012.

---

### D-126 — `Grob` prefix convention; no `Gro` abbreviation (Apr 2026)

Area: Type naming convention
Supersedes: none
Superseded by: none

The naming prefix for all Grob runtime types is `Grob` in full. `Gro` as an abbreviation is not a convention in this codebase. Correct: `GrobType`, `GrobValue`, `GrobError`, `GrobVM`, `GrobFunction`. Early design notes containing `GroType` are superseded — treat as `GrobType` throughout. See ADR-0012.

---

### D-127 — Three string literal forms (Apr 2026)

Area: String literal forms
Supersedes: none
Superseded by: none

Three string forms mapping to distinct developer intent. (1) Double-quoted `"..."` — standard form, escape sequences (`\n`, `\t`, `\\`, `\"`, `\$`), `${expr}` interpolation, single-line only. (2) Single backtick `...` — inline raw, no escape processing, no interpolation, no newlines (compile error), cannot contain a backtick. Intended for Windows paths, regex patterns, short verbatim values. (3) Triple backtick `...` — multiline verbatim block, no escape processing, no interpolation, newlines and whitespace preserved verbatim, no trimming. Intended for SQL, JSON templates, multiline command strings. May contain single backticks; cannot contain three consecutive backticks. Single-quoted strings are not valid.

---

### D-128 — Raw string newline rule (Apr 2026)

Area: Raw string newline rule
Supersedes: none
Superseded by: none

A newline before the closing delimiter of a single backtick string is a compile error. Developer intent for single backtick is inline raw — spanning lines signals a missing closing delimiter. Triple backtick is the explicit multiline form. The two forms do not overlap. Line continuation rules are irrelevant inside any string literal — the lexer is in string-scanning mode, not token-scanning mode.

---

### D-129 — Raw string indentation (Apr 2026)

Area: Raw string indentation
Supersedes: none
Superseded by: none

Triple backtick string content is verbatim — no indentation trimming in v1. Content begins immediately after the opening `````. Leading whitespace and newlines are part of the string value. Revisit post-MVP if friction is observed. C#-style trim-to-closing-delimiter is the reference model if trimming is added later.

---

### D-130 — Escape sequence set (Apr 2026)

Area: Escape sequence set
Supersedes: none
Superseded by: D-161

Confirmed escape sequences for double-quoted strings: `\n` (newline), `\r` (carriage return), `\t` (tab), `\\` (backslash), `\"` (double quote), `\$` (literal dollar — prevents interpolation trigger). `\r` is needed for explicit `\r\n` Windows line endings. `\$` is load-bearing: without it a literal `$` in a double-quoted string cannot be expressed. Any unrecognised `\x` sequence is a compile error — no silent pass-through. Raw strings (single and triple backtick) process no escape sequences — backslash is a literal character.

---

### D-131 — Namespace conventions (Apr 2026)

Area: Namespace conventions
Supersedes: none
Superseded by: none

Namespaces are gerunds or adjectives — never the same word as the primary class they contain. Prevents `Grob.Compiler.Lexer.Lexer` and similar clashes (SharpBASIC retrospective lesson). Canonical map: `Grob.Compiler.Lexing` → `Lexer`, `Token`, `TokenType`, `LexError`; `Grob.Compiler.Parsing` → `Parser`, `ParseError`; `Grob.Compiler.Parsing.Ast` → all AST node types; `Grob.Compiler.TypeChecking` → `TypeChecker`, `TypeRegistry`, `Symbol`; `Grob.Compiler.Emitting` → `Compiler` (bytecode emitter). `Ast` is acceptable as a namespace — no class named `Ast` exists. Rule: if the namespace and its primary class would share a name, the namespace needs a different word.

---

### D-132 — Tooling: `language-configuration.json` (Apr 2026)

Area: Tooling — language-configuration.json
Supersedes: none
Superseded by: none

`language-configuration.json` added to Phase 1 alongside the TextMate grammar. Declares bracket pairs (`()`, `[]`, `{}`, single and triple backtick), auto-closing pairs, surrounding pairs, comment toggling (`//` and `/* */`), indentation rules (increase after `{`, decrease after `}`). Handled entirely by VS Code — no LSP, no TypeScript, no build step. Ships with the first extension release. Not the formatter — `grob fmt` remains the formatting story. This covers only editor conveniences developers expect without thinking about them.

---

### D-133 — Tooling: TextMate grammar (Apr 2026)

Area: Tooling — TextMate grammar
Supersedes: none
Superseded by: none

TextMate grammar (`.tmLanguage.json`) is the first tooling deliverable — written before the compiler is built, ships with the first VS Code extension release. No compiler dependency. Keyword list and operator set from `grob-language-fundamentals.md` are the authoritative source.

---

### D-134 — Tooling: `Grob.Lsp` project (Apr 2026)

Area: Tooling — Grob.Lsp
Supersedes: none
Superseded by: none

`Grob.Lsp` added to the solution as a `src/` project. Depends on `Grob.Compiler`, `Grob.Core`, `Grob.Runtime`. Does not depend on `Grob.Vm` — the LSP never executes code. Library: `OmniSharp.Extensions.LanguageServer`.

---

### D-135 — Tooling: VS Code extension shell (Apr 2026)

Area: Tooling — VS Code extension
Supersedes: none
Superseded by: none

`tooling/Grob.VsCode/` added alongside the C# solution — TypeScript project. Registers `.grob` file type, attaches TextMate grammar, starts LSP process. The TypeScript scope is minimal (~30 lines).

---

### D-136 — Tooling: LSP handler order (Apr 2026)

Area: Tooling — LSP handler order
Supersedes: none
Superseded by: none

LSP handler build order: (1) diagnostics, (2) completions, (3) hover, (4) go-to-definition. Semantic tokens deferred to post-MVP.

---

### D-137 — `SourceLocation` as day-one compiler requirement (Apr 2026)

Area: Compiler — SourceLocation day one
Supersedes: none
Superseded by: none

**Day-one constraint:** every AST node must carry a `SourceLocation`. The type checker must resolve every identifier to its declaration node (`AstNode? Declaration`) and every symbol table entry must store `DeclaredAt: SourceLocation`. Not deferrable — retrofitting after the type checker is built requires a full audit. See `grob-tooling-strategy.md`.

---

### D-138 — v1 Requirements Specification (Apr 2026)

Area: v1 Requirements Specification
Supersedes: none
Superseded by: none

Full build specification in `grob-v1-requirements.md`. Covers success criteria, sprint plan, language features, stdlib, CLI, plugin system, error handling, security, testing strategy, validation scripts, definition of done. Authoritative for what ships in v1 — draws from all other design documents.

---

### D-139 — `input()` built-in (Apr 2026)

Area: `input()` built-in
Supersedes: none
Superseded by: none

`input(prompt: string = ""): string` — built-in function, same category as `print()` and `exit()`. No namespace. Always available. Writes prompt to stdout with no trailing newline (cursor stays on same line). Reads one line from stdin. Returns the line as `string` with newline stripped. Blocks until Enter. If the user presses Enter with no input, `input()` returns the empty string `""`. If stdin is closed before a line is read (Ctrl+Z + Enter on Windows, Ctrl+D on Unix, or piped input exhausted), throws `IoError("Unexpected end of input")` — silent empty string return would produce confusing downstream errors. Interactive use is the primary case; non-interactive (piped) stdin works normally.

---

### D-140 — Array mutation methods (Apr 2026)

Area: Array mutation methods
Supersedes: none
Superseded by: none

`append(value: T): void`, `insert(index: int, value: T): void`, `remove(index: int): void`, `clear(): void` added to the `T[]` type registry. All four mutate the array in place. Calling any mutation method on a `const`-bound array is a compile error. `insert` throws `RuntimeError` if index is out of range. `remove` throws `RuntimeError` if index is out of range. `filter`, `map`, `sort`, `select` are unaffected — they always return new arrays and never mutate. Full registry in `grob-type-registry.md`.

---

### D-141 — `map<K, V>` first-class type (Apr 2026)

Area: `map<K, V>` type
Supersedes: none
Superseded by: none

First-class built-in type. Compiler support for indexer read (`m[key] → V?`) and indexer write (`m[key] = value`). Construction via `map<string, string>{ "key": value }` literal. Empty map via `map<string, string>{}` with explicit type annotation. In v1, keys must be `string` — non-string keys deferred post-MVP. Type checker knows `map<string, string>` and `map<string, int>` as distinct types. Users consume and construct maps; cannot declare generic map types (same constrained-generics model as arrays). Iteration via `for k, v in myMap { }`. Insertion order preserved. Members: `length`, `isEmpty`, `keys: K[]`, `values: V[]`, `get(key): V?`, `set(key, value): void`, `contains(key): bool`, `remove(key): void`, `clear(): void`, read and write indexers. Mutation methods are compile errors on `const`-bound maps. `env.all()` and `Response.headers` return `map<string, string>`. Full registry in `grob-type-registry.md`.

---

### D-142 — OQ-009 opened: `GrobValue` provisional (Apr 2026)

Area: OQ-009 opened
Supersedes: none
Superseded by: none

`GrobValue` provisional representation — `Grob.Core` requires a `GrobValue` definition before Sprint 1 begins, but OQ-005 (full value representation) is deferred until clox is complete. Tentative: define as tagged union struct, encapsulated behind a clean boundary, documented as provisional. Decide at Sprint 1 start. See `grob-open-questions.md`.

---

### D-143 — OQ-010 opened: `.grobc` binary format spec (Apr 2026)

Area: OQ-010 opened
Supersedes: none
Superseded by: none

`.grobc` binary format specification — skeleton spec needed before implementation so the format is versionable from day one. Minimum: magic bytes, version header, endianness, constant pool serialisation, source location map inclusion. Defer until Sprint 1 structures are stable. See `grob-open-questions.md`.

---

### D-144 — OQ-011 opened: `Grob.Crypto` API shape (Apr 2026)

Area: OQ-011 opened
Supersedes: none
Superseded by: D-148

`Grob.Crypto` API shape — must be specified before integration tests can pass Script 10. Minimum for v1: `crypto.sha256File(path) → string`, MD5 and SHA256 for strings and files, hex string return type. Defer to Sprint 10 planning. See `grob-open-questions.md`.

---

### D-145 — OQ-012 opened: `process.run()` timeout (Apr 2026)

Area: OQ-012 opened
Supersedes: none
Superseded by: D-147

`process.run()` timeout behaviour — tentative direction: no silent default timeout, but `timeout: int` named parameter available on `run()` and `runShell()`. Throws `ProcessError` on timeout. `ProcessResult` does not need `timedOut` property. Defer to Sprint 9. See `grob-open-questions.md`.

---

### D-146 — OQ-007 resolved: `for...in` iterable types (Apr 2026)

Area: OQ-007 resolved
Supersedes: none
Superseded by: none

`for...in` iterable types special-cased in v1. Three cases: (1) numeric range — lowered to `while`, already confirmed. (2) `T[]` array — index-based lowering to `while`, both single and two-identifier forms. (3) `map<K, V>` — two-identifier form required (`for k, v in myMap`), single-identifier on a map is a compile error with suggestion, lowered to `while` over internal keys array. Any other type in subject position is a compile error. Formal iterable protocol is post-MVP — no compiler rework required to add it. `map<K, V>` special-cased because `for k, v in myMap` is natural and the keyloop alternative is visibly clunky for a first-class type.

---

### D-147 — OQ-012 resolved: `process.run()` timeout (Apr 2026)

Area: OQ-012 resolved
Supersedes: D-145
Superseded by: none

`process.run()` timeout behaviour resolved. All four process functions get `timeout: int = 0` as a named parameter. `0` means infinite — runs until the process completes or the OS kills it. On timeout expiry, throws `ProcessError("Process timed out after {n} seconds: {cmd}")`. `ProcessResult` is unchanged — no `timedOut` property. The throw is the signal. Full signatures: `process.run(cmd: string, args: string[], timeout: int = 0): ProcessResult`, `process.runShell(cmd: string, timeout: int = 0): ProcessResult`, `process.runOrFail(cmd: string, args: string[], timeout: int = 0): ProcessResult`, `process.runShellOrFail(cmd: string, timeout: int = 0): ProcessResult`.

---

### D-148 — OQ-011 resolved: `Grob.Crypto` API shape (Apr 2026)

Area: OQ-011 resolved
Supersedes: D-144
Superseded by: none

`Grob.Crypto` API shape resolved. First-party plugin (`import Grob.Crypto`). File hashing streams internally, never loads full file into memory. String hashing uses UTF-8 encoding. All hex output is lowercase. Verify functions use constant-time comparison. API: `crypto.sha256File(path: string): string`, `crypto.md5File(path: string): string`, `crypto.sha256(value: string): string`, `crypto.md5(value: string): string`, `crypto.verifySha256(path: string, expected: string): bool`, `crypto.verifyMd5(path: string, expected: string): bool`. SHA-1, SHA-512, HMAC, byte array output — all post-MVP.

---

### D-149 — `guid` core module (Apr 2026)

Area: guid module
Supersedes: none
Superseded by: none

`guid` is a first-class core module — auto-available, no import required. `guid` is a primitive type known to the type checker at compile time, registered by `GuidPlugin` in `Grob.Stdlib` at startup. Distinct from `string` — `guid == string` is a compile error. Generation: `guid.newV4(): guid` (random), `guid.newV7(): guid` (time-ordered, RFC 9562), `guid.newV5(namespace: guid, name: string...): guid` (deterministic, variadic name segments). Well-known namespaces: `guid.namespaces.dns`, `guid.namespaces.url`, `guid.namespaces.oid`. Parsing: `guid.parse(value: string): guid` (throws `RuntimeError` if invalid; compile-time validation on string literal arguments), `guid.tryParse(value: string): guid?` (nil if invalid), `guid.empty: guid`. Type members: `version: int` (property), `isEmpty: bool` (property), `toString(): string` (lowercase with hyphens), `toUpperString(): string` (uppercase for Azure ARM), `toCompactString(): string` (32 lowercase hex chars, no hyphens — storage names, keys). Operators: `==`, `!=`. String interpolation calls `toString()` implicitly. `map<guid, string>` not supported in v1 — use `myGuid.toString()` as key. Versions 1 and 3 excluded from v1.

---

### D-150 — `fs.copy`/`fs.move` overwrite parameter (Apr 2026)

Area: fs.copy/fs.move overwrite
Supersedes: none
Superseded by: none

`fs.copy(src, dest, overwrite: bool = false)` and `fs.move(src, dest, overwrite: bool = false)` — safe default. `overwrite: false` throws `IoError` if destination exists. `overwrite: true` replaces silently. `File.copyTo(destDir, overwrite: bool = false)` and `File.moveTo(destDir, overwrite: bool = false)` instance methods get the same parameter.

---

### D-151 — Script 11 added to validation suite (Apr 2026)

Area: Script 11 validation
Supersedes: none
Superseded by: none

Script 11 — Azure Resource Provisioning Helper added to validation suite. Exercises: `guid.newV5()` for idempotent resource naming, `Grob.Crypto` for template integrity verification, `map<K, V>` construction and `for k, v in` iteration, `Grob.Http` for ARM API calls, `env.require()` for credentials.

---

### D-152 — `Grob.Zip` API shape (Apr 2026)

Area: Grob.Zip — API shape
Supersedes: none
Superseded by: none

First-party plugin (`import Grob.Zip`). Three `zip.create()` overloads: source is a directory path string, a `File` object (file or directory — `file.path` extracted internally), or a `string[]` of explicit file paths. All accept `overwrite: bool = false`; throws `IoError` if destination exists and `overwrite` is false. `zip.extract(archive: string, dest: string, overwrite: bool = false): void`. `zip.list(archive: string): ZipEntry[]` — reads central directory only, no extraction. `ZipEntry` type: `name: string`, `size: int`, `compressedSize: int`, `modified: date`. Password-protected zips are post-MVP. Large archives never loaded fully into memory. All failures throw `IoError`. No `File[]` overload — use `.select(f => f.path)` to convert `File[]` to `string[]`.

---

### D-153 — `env` module — full API (Apr 2026)

Area: env module full API
Supersedes: none
Superseded by: none

`env.get(key: string): string?` — returns nil if not set. `env.require(key: string): string` — throws `RuntimeError` if not set or empty; error names the variable. `env.set(key: string, value: string): void` — process-scoped only; does not persist across invocations, does not write to registry. `env.has(key: string): bool` — returns false if not set OR if empty (empty is functionally absent for scripting purposes). `env.all(): map<string, string>` — all current process environment variables.

---

### D-154 — `format` module — full API (Apr 2026)

Area: format module full API
Supersedes: none
Superseded by: none

All three format functions return `string`. Callers pass the result to `print()`, `log.*()`, `fs.writeText()`, or any string consumer. Signatures: `format.table(items: T[]): string`, `format.table(items: T[], columns: string[]): string` (explicit column selection and ordering), `format.list(item: T): string` (one field per line — `field: value` — for single-record detail views), `format.csv(items: T[]): string` (comma-delimited, header row always included in v1). Column widths auto-sized to content. Alignment: strings left-aligned, numbers right-aligned. Per-column alignment and width control are post-MVP. Compiler rewrite applies to all chained forms. Additional functions: `format.number(value: int|float, pattern: string): string`, `format.date(value: date, pattern: string): string` — pattern strings follow .NET conventions.

---

### D-155 — `Grob.Http` locked signatures (Apr 2026)

Area: Grob.Http — locked signatures
Supersedes: D-118
Superseded by: none

`http.get(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response`. `http.post(url: string, body: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response`. `http.put` and `http.patch` have identical shape to `post`. `http.delete(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response`. `http.download(url: string, dest: string, auth: AuthHeader? = nil, timeoutSeconds: int = 30): void` — throws `NetworkError` on non-2xx (a failed download has written nothing useful). All other functions return `Response` and let the caller inspect `isSuccess` — non-2xx is not an exception. `body` is `string`; callers serialise structs via `json.encode()` before passing. `headers` is `map<string,string>`. `auth.bearer(token: string): AuthHeader`, `auth.basic(username: string, password: string): AuthHeader`, `auth.apiKey(key: string, headerName: string = "X-Api-Key"): AuthHeader`. `AuthHeader.toString()` returns `"[AuthHeader]"` — never exposes the credential.

---

### D-156 — `json.encode()` added (Apr 2026)

Area: json.encode() added
Supersedes: none
Superseded by: none

`json.encode(value: T): string` added to the `json` module. Serialises any typed value or anonymous struct to a JSON string. Inverse of `json.parse()`. Required for `http.post()` and `http.put()` with struct bodies — the HTTP module accepts `string`, not typed values. Constrained generic, same model as `mapAs<T>()`.

---

### D-157 — `json.Node` — full type spec (Apr 2026)

Area: json.Node — full type spec
Supersedes: none
Superseded by: none

`json.Node` is returned by `json.read()`, `json.parse()`, `json.stdin()`, and node indexer access. Indexer `node["key"]: json.Node?` returns nil for missing keys, never throws. Accessors: `asString(): string`, `asInt(): int`, `asFloat(): float`, `asBool(): bool`, `asArray(): json.Node[]` — all throw `JsonError` if the node is the wrong type. `mapAs<T>(): T` — constrained generic, throws `JsonError` on shape mismatch. Type predicates: `isNull: bool`, `isString: bool`, `isInt: bool`, `isFloat: bool`, `isBool: bool`, `isArray: bool`, `isObject: bool`. `toString(): string` returns raw JSON text of the node.

---

### D-158 — `Response` type — full spec (Apr 2026)

Area: Response type — full spec
Supersedes: none
Superseded by: none

`Grob.Http.Response` members: `statusCode: int`, `isSuccess: bool` (200–299), `headers: map<string,string>` (keys normalised to lowercase — HTTP/2 convention, eliminates case-sensitivity bugs), `asText(): string`, `asJson(): json.Node` (throws `JsonError` if body is not valid JSON), `toString(): string` (returns status summary, never exposes body).

---

### D-159 — `AuthHeader` type — full spec (Apr 2026)

Area: AuthHeader type — full spec
Supersedes: none
Superseded by: none

`Grob.Http.AuthHeader` is an opaque type. Constructed only by `auth.bearer()`, `auth.basic()`, `auth.apiKey()`. Only `http.*` functions accept it. Not directly constructable. `toString()` returns `"[AuthHeader]"` — never exposes the credential, including under `--verbose`.

---

### D-160 — `ProcessResult` type — full spec (Apr 2026)

Area: ProcessResult type — full spec
Supersedes: none
Superseded by: none

`ProcessResult` members: `stdout: string` (empty string if none), `stderr: string` (empty string if none), `exitCode: int`, `toString(): string` (returns `stdout` — most useful default for interpolation and print).

---

### D-161 — Escape sequence set — updated (Apr 2026)

Area: Escape sequence set — updated
Supersedes: D-130
Superseded by: none

`\r` (carriage return) added to the confirmed escape set. Full v1 set: `\n`, `\r`, `\t`, `\\`, `\"`, `\$`. `\r` is needed for explicit `\r\n` Windows line endings. Any unrecognised `\x` sequence is a compile error — no silent pass-through. Raw strings (backtick) continue to process no escape sequences.

---

### D-162 — Numeric type precision (Apr 2026)

Area: Numeric type precision
Supersedes: none
Superseded by: none

`int` is 64-bit signed integer. `float` is 64-bit IEEE 754 double-precision. These are fixed and not configurable. The VM uses C# `long` and `double` respectively.

---

### D-163 — Integer overflow behaviour (Apr 2026)

Area: Integer overflow
Supersedes: none
Superseded by: none

Arithmetic that exceeds the `int` range throws `RuntimeError` at runtime. The VM uses checked arithmetic — overflow never silently wraps. Prevents a class of bugs where large file sizes, timestamps, or counters produce wrong results.

---

### D-164 — Implicit type coercion (Apr 2026)

Area: Implicit type coercion
Supersedes: none
Superseded by: none

The only implicit type conversion in Grob is `int` → `float` in mixed arithmetic. No `bool` → `int`. No `int` → `string` (use `.toString()` or interpolation). No `string` → `int` (use `.toInt()`). All other conversions are explicit method calls.

---

### D-165 — Trailing commas permitted (Apr 2026)

Area: Trailing commas
Supersedes: none
Superseded by: none

Trailing commas are permitted in all comma-separated lists: array literals, struct construction, map literals, function parameters, function arguments. Optional — never required. Simplifies code generation and reduces diff noise.

---

### D-166 — Two-pass type checker for forward references (Apr 2026)

Area: Forward references
Supersedes: none
Superseded by: none
Extended by: D-286

> **Note:** D-286 extends this decision by explicitly enumerating all supported forward-reference forms at the top level (function-to-function, type-to-type, generic type arguments, self-reference, mutual reference). D-166 remains the root decision; D-286 is its detailed elaboration.

Functions and types can reference other functions and types declared later in the same file. The type checker performs two passes: (1) registration pass — walks all top-level declarations and registers names and signatures; (2) validation pass — walks all bodies and top-level code, resolving against the fully populated symbol table. Inside function bodies, `:=` must precede use — no forward references within a single function.

---

### D-167 — Variable shadowing allowed with warning (Apr 2026)

Area: Variable shadowing
Supersedes: none
Superseded by: none

A local variable may shadow a variable from an enclosing scope, including function parameters and global variables. The compiler emits a warning (not an error) when shadowing is detected. Rationale: preventing shadowing is annoying in real scripts; allowing it silently is a bug factory. A warning is the balance.

---

### D-168 — Script structure order (Apr 2026)

Area: Script structure order
Supersedes: none
Superseded by: none

Canonical order: (1) `import` statements, (2) `param` blocks, (3) `type` and `fn` declarations (any order relative to each other), (4) top-level code. An `import` after a `param` or `type` is a compile error. A `param` after a `fn` or top-level statement is a compile error.

---

### D-169 — Equality semantics (Apr 2026)

Area: Equality semantics
Supersedes: none
Superseded by: none

Value equality throughout. Primitives: compare by value. User-defined structs: field-by-field value equality (same type, all fields equal recursively). Anonymous structs: field-by-field, field order does not matter. Arrays: element-wise comparison. Maps: entry-wise comparison, insertion order does not affect equality. `nil == nil` is `true`. `==` between incompatible types is a compile error.

---

### D-170 — Nil chain propagation (Apr 2026)

Area: Nil chain propagation
Supersedes: none
Superseded by: none

`?.` short-circuits the entire chain when the receiver is `nil`. `a?.b?.c` — if `a` is nil, the chain evaluates to `nil` immediately. No further access attempted. Result type is always `T?`.

---

### D-171 — Script-level `return` is a compile error (Apr 2026)

Area: Script-level return
Supersedes: none
Superseded by: none

`return` at the top level of a script (outside any function) is a compile error. Use `exit()` to terminate a script early.

---

### D-172 — No multiple return values (Apr 2026)

Area: No multiple return values
Supersedes: none
Superseded by: none

Functions return a single value. Use a struct to return multiple values.

---

### D-173 — No operator overloading (Apr 2026)

Area: No operator overloading
Supersedes: none
Superseded by: none

User-defined types cannot define custom operators. Comparison uses field-by-field value equality.

---

### D-174 — No circular imports (Apr 2026)

Area: No circular imports
Supersedes: none
Superseded by: none

`import` is for plugins only. Grob scripts do not export types to other scripts in v1. One script cannot import another script.

---

### D-175 — `json.write` pretty-printed by default (Apr 2026)

Area: json.write — pretty default
Supersedes: none
Superseded by: none

`json.write()`, `json.encode()`, and `json.stdout()` default to pretty-printed output (indented). `compact: bool = false` named parameter on all three — pass `compact: true` for single-line output.

---

### D-176 — Date constructors default to local time (Apr 2026)

Area: date constructors — local time
Supersedes: none
Superseded by: none

`date.now()`, `date.today()`, `date.of()`, `date.ofTime()` all return local time. `date.parse()` preserves the timezone from the input string; strings without timezone offsets are interpreted as local time. `date.fromUnixSeconds()` and `date.fromUnixMillis()` return UTC.

---

### D-177 — `fs.readText` UTF-8 default (Apr 2026)

Area: fs.readText — UTF-8 default
Supersedes: none
Superseded by: none

`fs.readText()` reads as UTF-8. BOM auto-detection: if a BOM is present, encoding is detected from it. If no BOM, UTF-8 assumed. `fs.writeText()` and `fs.appendText()` write UTF-8 without BOM. `fs.readLines()` splits on `\n` and `\r\n` transparently — returned strings do not include terminators. No encoding parameter in v1.

---

### D-178 — Map literal separator rules (Apr 2026)

Area: Map literal separator rules
Supersedes: none
Superseded by: none

Map literal entries are separated by newlines or commas (both valid). Trailing commas permitted. Each entry is `key: value` with colon separator. Keys are string literals in v1.

---

### D-179 — `string.toString()` identity method (Apr 2026)

Area: string.toString() identity
Supersedes: none
Superseded by: none

`string.toString()` added to the type registry — returns the string unchanged. Identity method for type uniformity: every built-in type now has `toString()`.

---

### D-180 — Stack overflow behaviour (Apr 2026)

Area: Stack overflow behaviour
Supersedes: none
Superseded by: none

`CallFrame[256]` fixed array. At recursion depth 257, the VM throws `RuntimeError("Stack overflow — maximum call depth (256) exceeded")`. Clean error, not a crash.

---

### D-181 — `const` depth: binding and content (Apr 2026)

Area: const depth — binding AND content
Supersedes: D-013
Superseded by: D-288, D-291

> **Superseded by D-288 and D-291.** D-288 splits the immutability surface: `const` is now compile-time only and its deep-immutability guarantee is less relevant (const values are inlined, not bound). D-291 carries the deep-immutability clause forward and attaches it explicitly to `readonly`. The practical rule — "once-assigned bindings lock both rebinding and mutation" — is preserved; it now applies to `readonly` rather than `const`.

`const` prevents both rebinding and mutation. `const arr := [1, 2, 3]` — `arr = [4, 5]` is a compile error (rebinding) AND `arr.append(4)` is a compile error (mutation). One rule: `const` means immutable. The deeper question of mutable-binding-with-immutable-content (or vice versa) is deferred post-MVP, but v1 behaviour is unambiguous: `const` locks everything.

---

### D-182 — Nested arrays (`T[][]`) (Apr 2026)

Area: Nested arrays (T[][])
Supersedes: none
Superseded by: none

Arrays of arrays are valid. `int[][]` is the type of a 2D array. `matrix[r][c]` for element access. No dimension enforcement — `matrix[0].length` need not equal `matrix[1].length`. No rectangular guarantee, no matrix operations. This is arrays-of-arrays, not a matrix type. Sufficient for JSON deserialisation of nested arrays and simple grid patterns. A dedicated `Matrix` type with linear algebra operations would be a post-MVP plugin if needed.

---

### D-183 — No tuples in v1 (Apr 2026)

Area: No tuples
Supersedes: none
Superseded by: none

Tuples are not in v1. When a function needs to return multiple values, define a struct. Structs are self-documenting (fields have names), passable as values, and extensible without breaking callers. Go’s primary tuple use case (error returns: `result, err := foo()`) does not apply — Grob uses exceptions. Tuples are an additive grammar extension post-MVP if real friction is observed. No architectural rework required to add them later.

---

### D-184 — No out parameters (Apr 2026)

Area: No out parameters
Supersedes: none
Superseded by: none

`out` parameters are not in v1 and are not planned. `out` is a C# mechanism for multiple returns where tuples and records were not available historically — C# itself has moved away from them. Grob’s nullable return pattern (`toInt() → int?`, `tryParse() → guid?`) covers the try-parse use case cleanly. `??` nil coalescing provides the fallback: `port := input("Port: ").toInt() ?? 8080`.

---

### D-185 — Try-parse pattern (Apr 2026)

Area: Try-parse pattern
Supersedes: none
Superseded by: none

The try-parse pattern in Grob uses nullable return types, not tuples or out parameters. `string.toInt() → int?` returns nil if not parseable. `string.toFloat() → float?` returns nil. `guid.tryParse(value) → guid?` returns nil if invalid. Callers use `??` for defaults or `if (result != nil)` for branching. This is consistent, composable, and requires no special syntax.

---

### D-270 — `print`, `exit`, `input` are built-in functions, not keywords (Apr 2026)

Area: Tokenisation — built-ins
Supersedes: none
Superseded by: none

`print`, `exit`, and `input` are built-in functions, tokenised as identifiers and resolved at type-check time against registered natives. They are not keywords. This is consistent with `input`'s existing treatment (D-139), with D-110's framing of `exit` as "a built-in function, same category as `print()`", and with every other stdlib function in the language. Identifier status allows them to be shadowed per D-167 (with warning) and produces correct diagnostics when misused.

`TokenKind` does not include `Print` or `Exit`. The keyword list in v1 Spec §3.4 and the Language Fundamentals tooling note is updated accordingly — a separate "Built-ins" category documents the three identifier names that resolve to natives. The TextMate grammar moves `print`, `exit`, `input` out of the `keyword.control.grob` pattern into a new `support.function.builtin.grob` pattern.

---

### D-271 — `??` binds tighter than ternary (Apr 2026)

Area: Operator precedence — ternary vs nil coalescing
Supersedes: none (corrects Language Fundamentals §7 as originally drafted)
Superseded by: none

`??` (nil coalescing) is at precedence level 10. Ternary `? :` is at precedence level 11. `??` binds tighter than ternary. This matches C#, Kotlin, Swift, and TypeScript. The previous ordering (ternary tighter than `??`) was a silent divergence from stated reference languages.

Under the corrected precedence, `cond ? a : b ?? fallback` parses as `cond ? a : (b ?? fallback)` — the reading a C# developer expects. The Pratt parser binding powers for `?:` and `??` swap relative positions.

---

### D-272 — Assignment operators are not in the precedence table (Apr 2026)

Area: Operator precedence — scope of the expression precedence table
Supersedes: none (corrects Language Fundamentals §7 as originally drafted)
Superseded by: none

The operator precedence table in Language Fundamentals §7 describes expression-level operators only. Assignment operators (`:=`, `=`, `+=`, `-=`, `*=`, `/=`, `%=`) are statement forms, not expressions — they cannot appear in expression position and do not have binding powers in the Pratt expression parser.

Assignment, declaration, increment/decrement, and `throw` are consolidated into a new dedicated Statement Forms section (Language Fundamentals §28). The precedence table is reduced from 13 levels to 12. Attempting to use assignment in expression position (`if (x = 5)`, `foo(x := 1)`) produces a parse-time error.

---

### D-273 — `float % float` supported with fmod semantics (Apr 2026)

Area: Arithmetic — float modulo
Supersedes: none
Superseded by: none

The `%` operator is valid on `float` operands. `float % float → float` follows C#'s `double % double` semantics, which implement IEEE 754 `fmod`: the result has the same sign as the dividend. `-7.5 % 2.0` returns `-1.5`, not `0.5`. Mixed-type modulo (`int % float`, `float % int`) promotes the `int` to `float` and produces a `float` result, consistent with §15.

`x % 0.0` throws `RuntimeError` — consistent with `x / 0.0`. No form of modulo by zero silently produces a value. If either operand is `NaN` or `±Infinity`, the result follows IEEE 754 and is not an error. Integer modulo uses truncated-toward-zero division: `-7 % 3 = -1`.

---

### D-274 — `try`/`catch`/`throw` grammar (Apr 2026)

Area: Exception handling — grammar
Supersedes: none (completes D-082, D-083, D-084)
Superseded by: none

**Catch syntax.** Typed catches use the paren-annotated form:

```grob
try { ... }
catch (e: IoError) { ... }
catch (e: NetworkError) { ... }
catch e { ... }
```

The typed form is `catch (<n>: <Type>) { <block> }`. The catch-all form is `catch <n> { <block> }` — identifier only, no parens, no colon. `catch (e)` (parens with no type) is a syntax error. Typed catches match polymorphically — `catch (e: T)` matches any thrown value whose type is `T` or a subtype.

**Throw syntax.** `throw <expression>` where the expression must evaluate to a subtype of `GrobError`. Exceptions are constructed using struct construction syntax:

```grob
throw IoError { message: "File not found: ${path}" }
throw NetworkError { message: "Timeout", statusCode: 504 }
```

`throw` is a keyword. `throw 42`, `throw "oops"`, and throwing any non-`GrobError` type are compile errors.

**v1 exception type fields.** `GrobError` root: `message: string`, `location: SourceLocation?` (set by the runtime; user-supplied values ignored). `NetworkError` adds `statusCode: int?`. All other leaves inherit `message` only in v1.

`TokenKind` adds `Throw`. A new `ThrowStatement(expression: Expression)` AST node is added. `CatchClause` carries `binding`, `type` (nil for catch-all), and `body`.

---

### D-275 — `finally` block on `try` (Apr 2026)

Area: Exception handling — finally
Supersedes: none (extends D-274)
Superseded by: none

`try` blocks support an optional `finally` clause. The finally block runs on every normal or exceptional exit from the try region: normal completion, uncaught exception, caught-and-handled, caught-and-rethrown, early `return`, early `break`, early `continue`. It does not run on `exit()` — `exit()` unconditionally terminates.

A try with only a finally (no catches) is legal. A try with neither catch nor finally is a parse error. `finally` must appear after all catches, at most once.

**Control-flow-in-finally.** `return`, `break`, and `continue` are not permitted inside a `finally` block — compile error. This is a deliberate divergence from C#: the "finally overrides return" behaviour has no legitimate use case and is a documented source of bugs. `throw` from inside `finally` remains permitted; it replaces any in-flight exception (exception chaining is post-MVP).

`TokenKind` adds `Finally`. The AST gains `FinallyClause(body: Block)` on `TryStatement`. The exception handler table gains a `finallyOffset` field per entry. The VM's unwinding logic runs the finally block before propagating.

---

### D-276 — Block-body lambda: implicit last expression, `return` for early exit (Apr 2026)

Area: Expressions — lambda block bodies
Supersedes: none (extends D-115)
Superseded by: none

A block-body lambda (`x => { ... }`) produces a value via: (a) implicit last expression — if the block's final statement is an expression, that value is the return value; or (b) explicit `return <expr>` for early exit. `return` in a lambda exits the lambda, not the enclosing function.

Both mechanisms may coexist. The type checker requires all return paths to produce the same type. If the block's final statement is not an expression (declaration, assignment, control-flow), the lambda's inferred return type is `void`. A void lambda cannot be used in a value position.

Lambda return types are always inferred from the body; no syntax to annotate in v1 (additive post-MVP). The ban on `return`/`break`/`continue` inside `finally` (D-275) does not apply to a `return` inside a block-body lambda nested within `finally` — that `return` exits only the lambda.

---

### D-277 — Switch expression v1 pattern grammar (Apr 2026)

Area: Expressions — switch expression patterns
Supersedes: none (extends D-111)
Superseded by: none

Switch expression arms use one of three v1 pattern forms:

- **Value pattern** — a compile-time constant expression of the scrutinee's type. Literals and `const`-bound identifiers are valid. `nil` is a valid value pattern on nullable scrutinees; `nil` on a non-nullable scrutinee is a compile error.
- **Relational pattern** — `>= expr`, `> expr`, `<= expr`, `< expr` where `expr` is a compile-time constant. Legal only on ordered types (`int`, `float`, `string`, `date`).
- **Catch-all** — `_`. Matches any value.

Arms are separated by commas; trailing comma permitted. First match wins. Relational patterns never prove exhaustiveness — any switch using a relational arm requires `_`. Value patterns prove exhaustiveness for `bool` and nullable types only; all other scrutinee types require `_`. All arm results must produce the same type.

Deferred post-MVP: multi-value arms, range patterns, type patterns.

---

### D-278 — Integer division by zero throws `RuntimeError` (Apr 2026)

Area: Arithmetic — integer division
Supersedes: none (extends D-273 family)
Superseded by: none

`int / 0` throws `RuntimeError` at runtime, consistent with `int % 0`, `float / 0.0`, and `float % 0.0`. The compound forms `/=` and `%=` inherit this — they lower to the corresponding binary operator, so `i /= 0` throws before any assignment takes effect. No form of division or modulo by zero silently produces a value in Grob.

---

### D-279 — Nullable interpolation is a compile error (Apr 2026)

Area: String literals — interpolation
Supersedes: none (extends §8 double-quoted strings)
Superseded by: none

Interpolation of a nullable-typed expression (`T?`) in a double-quoted string is a compile error. Before a nullable value can appear in an interpolation slot, it must be resolved to `T` — via `?? <fallback>` or by narrowing inside an `if (x != nil)` block.

`print()` retains its existing behaviour: it accepts any type including nil, and nil renders as `"nil"`. The asymmetry is deliberate — `print()` is a diagnostic output sink; interpolation is string construction. Silent nil coercion in `print()` is acceptable; in interpolation it almost always indicates a bug.

The compile error message names the nullable expression, its type, the interpolation site, and suggests both resolutions.

---

### D-280 — Drop `map()`; `select()` is universal transformation (Apr 2026)

Area: `T[]` pipeline methods — projection and transformation
Supersedes: "`select` is alias for `map`" position in grob-type-registry.md
Superseded by: none

`.select<U>(fn: T → U) → U[]` is the single pipeline transformation primitive on `T[]`. `.map()` is removed from v1 entirely. This is a deliberate identity stance: **Grob is LINQ-for-scripting in its pipeline vocabulary, not FP-for-scripting.** Every C# developer knows `.Select()` and reaches for it for all three transformation shapes. Future pipeline operators follow LINQ naming (`.selectMany()`, `.aggregate()`).

`mapAs<T>()` is unaffected — it is a type assertion on parsed JSON/CSV data, not a pipeline transformation.

**Registry consequences.** The `map()` row is deleted from the `T[]` table. The `select()` row signature becomes `select<U>(fn: T → U) → U[]`; mutation-rules paragraph updated to remove `map`.

---

### D-281 — `sort()` key-selector only; `U: Comparable` constraint (Apr 2026)

Area: `T[]` sort API
Supersedes: comparator example at grob-stdlib-reference.md (documentation artefact, not a real API)
Superseded by: none

`sort<U: Comparable>(fn: T → U, descending: bool = false) → T[]` is the sole sort signature. No comparator overload in v1. `Comparable = int | float | string | date | guid | bool`. `sort()` must be stable (implemented via `Enumerable.OrderBy`). The `Comparable` concept is documentation-level terminology, not a reserved word.

Multi-key sorting workaround: apply stable sort twice in reverse priority order. Post-MVP: anonymous-struct comparison extends `Comparable` to unlock single-pass multi-key sort.

---

### D-282 — `formatAs` replaces `format`; scalar formatters move to instance methods (Apr 2026)

Area: Formatting module surface and chained-pipeline sugar
Supersedes: prior `.format.X()` compiler-rewrite design and `format` module as documented
Superseded by: none

The `format` module is renamed to `formatAs`. Its scope is narrowed to collection-to-string terminal operations. Scalar formatting moves to instance methods on the numeric and date types.

**v1 `formatAs` module surface:**

- `formatAs.table(arr: T[]) → string`
- `formatAs.list(obj: T) → string`
- `formatAs.csv(arr: T[]) → string`

**v1 chained form (compiler rewrite):**

- `<expr>.formatAs.table()` → `formatAs.table(<expr>)`

**v1 scalar formatting:** `int.format(pattern: string) → string` and `float.format(pattern: string) → string` added as instance methods. `date.format(pattern: string)` unchanged. `format.number()` and `format.date()` module functions removed.

**`formatAs` is a reserved identifier**, not a keyword. User code may not declare a field, parameter, local, or function named `formatAs`. Bare `<expr>.formatAs` without a following method call is a compile error.

This decision establishes the `As` suffix convention: `mapAs<T>()` for type assertion on parsed data; `formatAs.X()` for output-shape assertion on collections. Future boundary operations follow the convention.

---

### D-283 — Drop `snake_case` compiler warning (Apr 2026)

Area: Compiler warnings — naming convention
Supersedes: snake_case warning entry in Opinionated Defaults (D-061 in part)
Superseded by: none

The `snake_case` compiler warning is dropped. Naming convention moves from compiler-level enforcement to formatter-level idiom (`grob fmt`) and documentation. Other warnings (shadowing, unused variables, unused imports) are unaffected.

**Rationale.** An identifier census across the eleven sample scripts found 7% of identifiers triggering the warning — all eight offenders in Script 11, all Azure-domain names (`subscriptionId`, `tenantId`, `resourceGroup` etc.) externally constrained by the REST API. Warnings that fire on correct code train users to mute warnings; muted warnings are worse than no warnings. Go is strongly opinionated on naming but expresses it in `gofmt`/`golint`, never the compiler. `grob fmt` is Grob's documented opinion vehicle.

`grob-personality-identity.md` Opinionated Defaults table: Naming row changed to "snake_case idiomatic — see style guide / None (formatter, not compiler)".

---

### D-284 — `RuntimeError` split into four typed leaves + residual (Apr 2026)

Area: Exception hierarchy — RuntimeError granularity
Supersedes: D-084 (six-leaf hierarchy)
Superseded by: none

The v1 exception hierarchy is expanded from six leaves to ten by splitting `RuntimeError` into four typed leaves plus a smaller residual. New leaves: `ArithmeticError`, `IndexError`, `ParseError`, `LookupError`. `RuntimeError` shrinks to VM-level resource failures only (stack overflow; residual).

**Full hierarchy:**

```
GrobError (root)
├── IoError          (file system, permissions)
├── NetworkError     (http, dns, connection)
├── JsonError        (json.parse, json type coercion)
├── ProcessError     (process timeout, non-zero exit under OrFail)
├── NilError         (dereferencing nil without ?. or ??)
├── ArithmeticError  (overflow, int div/0, math domain)
├── IndexError       (array bounds, substring bounds)
├── ParseError       (guid.parse, future int.parse / date.parse)
├── LookupError      (env.require missing key)
└── RuntimeError     (stack overflow, residual)
```

All ten are direct children of `GrobError`. Flat hierarchy. Message templates: `ArithmeticError` — `"Arithmetic error at <location>: <operation> produced <condition>"`; `IndexError` — `"Index <n> out of range (length <len>) at <location>"`; `ParseError` — `"Could not parse <input-name> as <type> at <location>"`; `LookupError` — `"Required environment variable '<n>' is not set"`.

---

### D-285 — Backtick raw strings canonical for Windows paths (Apr 2026)

Area: String literals — Windows paths and literal backslash content
Supersedes: none (documentation layer; D-127 string forms unchanged)
Superseded by: none

Backtick raw strings are established as the canonical Grob idiom for literal-backslash content — Windows paths, regex patterns, JSON fragments, and any other literal where backslashes appear as themselves. The three string forms (D-127) are unchanged at the grammar level; this is a documentation and convention decision.

Language Fundamentals §8 gains a "Windows paths and literal backslash content" callout. Sample scripts are updated to use backtick form for all path literals. The `@"..."` C# paradigm was evaluated and rejected — Grob's string-form philosophy is delimiter-driven (three delimiters, three intents) rather than modifier-driven, and backtick raw strings match Go's existing idiom.

---

### D-186 — v1 scope-cut list (Apr 2026)

Area: v1 scope — contingency cuts
Supersedes: none
Superseded by: none

A formal v1 scope-cut list is adopted with two candidates, in activation order:

1. **Validation decorators** (`@allowed`, `@minLength`, `@maxLength`). `@secure` is **not** on the list — it stays in v1.
2. **Regex literal grammar** (`/pattern/flags`).

**Activation:** at Chris's discretion. No fixed gate. The list is insurance; activation is a judgement call based on implementation state. Both candidates have zero coverage in the eleven-script release gate (zero regex literals, zero validation decorator uses in any script). `regex.compile(pattern, flags)` function form covers everything regex literals would; adding literal grammar in v1.1 is additive. If regex literals are cut, a new sample script exercising `regex.compile()` must be added to the release gate before v1 ships.

---

### D-286 — Cross-declaration reference rules (Apr 2026)

Area: Forward references — supported patterns
Supersedes: none (extends D-166)
Superseded by: none

The two-pass type checker supports forward references at the top level of a script for all declaration kinds. Explicitly documented:

- Function-to-function forward reference.
- Type-to-type forward reference (subject to cycle rules, D-287).
- Function signature referencing a type declared later (parameter or return type).
- Generic type argument referencing a type declared later — a call like `csv.read(path).mapAs<User>()` resolves when `type User` is declared below the call site.
- Self-reference (direct recursion).
- Mutual reference (indirect recursion) — functions calling each other or types referencing each other via nullable fields.

Inside function bodies, forward references remain illegal per D-166. Local variables must be declared before use within their scope. The two-pass model makes all these cases fall out naturally at zero implementation cost.

---

### D-287 — Non-nullable type cycles are a compile error (Apr 2026)

Area: Type system — type cycle detection
Supersedes: none
Superseded by: none

A type declaration cannot contain a cycle of required non-nullable fields that would produce an infinitely-sized value. The type checker detects cycles during the validation pass.

**What terminates a cycle:** nullable fields (`T?`), array fields (`T[]`), map fields (`map<K, V>`). **What participates:** required fields whose type is a named user-defined type (including self).

**Detection:** standard DFS with `Unvisited` / `Visiting` / `Visited` states per type. A back-edge to a type currently on the stack is a cycle. The full path is reported in the diagnostic, naming both fix paths (nullable and collection). Example: `type Tree { children: Tree[] }` is legal; `type A { b: B }` / `type B { a: A }` is a compile error.

Error code: **E0301** (type cycle with no terminating field). Trivial single-type self-reference cases use **E0302**. See `grob-error-codes.md` and ADR-0014.

---

### D-288 — Split `const` into `const` and `readonly` (Apr 2026)

Area: Variable declarations — compile-time vs runtime-once bindings
Supersedes: D-013 (in part), D-181 (fully)
Superseded by: none

Grob has two keywords for once-assigned bindings:

- **`const`** — the right-hand side is a compile-time constant expression (D-289). Evaluated by the type checker in pass 2. Stored in the constant pool. Every reference is inlined as a direct constant pool load. No runtime initialisation.
- **`readonly`** — the right-hand side is any valid Grob expression. Evaluated at the point of declaration. Cannot be reassigned; cannot be mutated (deep immutability, per D-291).
- **Mutable `:=`** — unchanged from D-011/D-013.

The C# model was chosen over Swift's single-`let`, Kotlin's `const val`, and a Rust-like model. Grob's target audience is C# fluent, and the `const`/`readonly` distinction does real work in the spec — value patterns in switch expressions require compile-time constants, and the hoisting story simplifies when compile-time and runtime bindings are named separately.

One new keyword: `readonly`. Added to `TokenKind` and the TextMate grammar. `SingleAssignmentDeclaration` AST node carries a `Kind` discriminator (`Const` | `Readonly`).

---

### D-289 — Definition of "compile-time constant expression" (Apr 2026)

Area: Type system — compile-time expression subset
Supersedes: none
Superseded by: none

An expression is a **compile-time constant expression** if and only if it is one of:

1. Literals of the primitive types (`int`, `float`, `string`, `bool`, `nil`) — all literal forms from §8, excluding interpolated strings containing `${...}`.
2. Binary arithmetic, comparison and logical operators on compile-time constant operands (`+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `||`).
3. Unary operators on a compile-time constant operand (`-`, `!`).
4. String concatenation via `+` where both operands are compile-time constant strings.
5. References to other `const`-bound identifiers declared earlier in the file.
6. References to named stdlib constants from the whitelist: `math.pi`, `math.e`, `math.tau`; `path.separator`, `path.altSeparator`, `path.pathSeparator`, `path.lineEnding`; `guid.empty`, `guid.namespaces.*`.

**Explicitly disallowed:** function calls of any kind; struct construction; array and map literals; anonymous struct literals; any call into `env.*`, `date.*`, `fs.*`, `process.*`, `http.*`, or any plugin; interpolated strings with `${...}`; lambda expressions; optional chaining, nil coalescing, ternary.

Governs `const` declaration RHS, switch-expression value patterns (§3.1), and relational patterns (§3.1). `readonly` bindings are not valid in value-pattern position.

---

### D-290 — Migration rule for existing `const` bindings (Apr 2026)

Area: Documentation migration — `const` to `readonly`
Supersedes: none
Superseded by: none

The mechanical migration rule:

> If the right-hand side of a `const` declaration satisfies D-289 (compile-time constant expression), the binding stays `const`. Otherwise, it becomes `readonly`.

No judgement calls required. Applied as a single pass over every document containing `const` examples. Three actual code-site migrations across the whole project — all array or map literal RHS cases (`const extensions := [".jpg", ...]` → `readonly extensions := [...]` etc.).

---

### D-291 — `readonly` semantics (Apr 2026)

Area: `readonly` binding semantics
Supersedes: D-181 (deep immutability clause now attaches to `readonly`)
Superseded by: none

A `readonly` binding has exactly these properties:

1. **Syntax.** `readonly X := <expr>` at top level or inside any block that accepts `:=`. `<expr>` is any valid Grob expression.
2. **Evaluation timing.** RHS evaluated at the point of declaration.
3. **Reassignment is a compile error.** `X = newValue` produces "cannot reassign `readonly` binding `X`".
4. **Mutation is a compile error (deep immutability).** Any method call or operation that would mutate the bound value is a compile error — `X.append(...)` on `readonly T[]`, `X["k"] = v` on `readonly map<...>`, field assignment on a `readonly` struct, `++X`, `X += 1`.
5. **Initialised at declaration.** Consistent with D-012.
6. **Scoping.** Identical to mutable `:=`.

`param` bindings are implicitly `readonly`. The `readonly` keyword is not written on `param` declarations — it would be redundant.

`readonly` may reference `const` values on its RHS. `const` may not reference `readonly` values — produce: "`const` binding cannot reference runtime value `X` (declared as `readonly`)."

---

### D-292 — `const` and `readonly` at function-local scope (Apr 2026)

Area: Scoping — local-scope variants
Supersedes: none
Superseded by: none

Both `const` and `readonly` may appear inside function bodies and any nested block. Local `const` provides magic-number naming with zero runtime overhead — the compiler inlines every reference as a constant pool load. Local `readonly` provides a once-computed value with an immutability guard. Both follow the same scoping rules as mutable `:=`.

---

### D-293 — Grammar and token impact of `readonly` (Apr 2026)

Area: Lexer, parser, AST, opcodes
Supersedes: none
Superseded by: none

**Lexer.** One new keyword: `readonly`. Added to the keyword table.

**Parser.** Declaration grammar:

```
declaration  := mutableDecl | constDecl | readonlyDecl
constDecl    := 'const'    identifier [':' type] ':=' expression
readonlyDecl := 'readonly' identifier [':' type] ':=' expression
mutableDecl  := identifier [':' type] ':=' expression
```

**AST.** A single `SingleAssignmentDeclaration` node carries `Kind` (enum: `Const` | `Readonly`), `Name`, `TypeAnnotation`, `Initialiser`, `SourceLocation`.

**Compiler.** `const` values: no binding emitted; every reference compiled to a direct constant pool load. `readonly` values: same `DefineGlobal` / `DefineLocal` opcodes as mutable bindings; immutability enforced at compile time, no runtime flag needed.

**TextMate grammar.** `keyword.control.grob` scope gains `readonly`.

---

### D-294 — Top-level initialisation order and circular detection (Apr 2026)

Area: Runtime — top-level execution order and initialisation state
Supersedes: none
Superseded by: none

After `import`, `param`, `type` and `fn` declarations have been processed by the two-pass type checker, top-level code executes top-to-bottom in source order. `const` declarations do not participate — they are resolved at type-check time and inlined.

A circular read — a `readonly` or mutable top-level initialiser calling a function that reads a top-level binding declared later — is detected at runtime. Each top-level binding slot carries a three-state tag (`Uninitialised`, `Initialising`, `Initialised`). `DefineGlobal` flips the tag from `Uninitialised` to `Initialising` before RHS evaluation and to `Initialised` once the value is stored. `GetGlobal` during startup consults the tag; a read from a slot not yet `Initialised` raises `RuntimeError`.

After the top-level code's final instruction, the VM sets `_startupComplete`. Subsequent `GetGlobal` reads skip the tag check — startup-only branch cost.

The rule applies equally to `readonly` and mutable top-level bindings. The runtime error code is **E5902** (`RuntimeError`); see `grob-error-codes.md` and ADR-0014. Spec text lives at `grob-language-fundamentals.md` §19.1.

---

### D-295 — Type field default evaluation timing (Apr 2026)

Area: User-defined types — field defaults
Supersedes: none
Superseded by: none

A field default expression evaluates at **construction time**, in the scope of the construction site. It evaluates once per construction that omits the field; constructions that supply the field explicitly do not evaluate the default.

A default may be any expression legal in general expression position — literals, function calls, interpolated strings, stdlib calls, method chains, anonymous struct literals, nested named type construction. A default may reference identifiers in scope at the construction site (`const`, `readonly`, mutable variables, function names, imported modules). A default may **not** reference other fields of the type being constructed — sibling fields may not yet have been assigned at that program point; such a reference is a compile error.

A field default is not a compile-time constant. Even when the RHS is a literal, it is a runtime expression in the specification. The `const` and `readonly` modifiers do not appear at the field-default declaration site. Spec text lives at `grob-language-fundamentals.md` §10.

---

### D-296 — Closure capture and top-level variable resolution (Apr 2026)

Area: Closures — variable resolution across scopes
Supersedes: none
Superseded by: none

A lambda body may reference identifiers from any scope visible at its definition site. Each reference resolves to one of four categories, classified by the compiler:

1. **Top-level `const`** — resolved at compile time; inlined as a direct constant pool load. No runtime slot.
2. **Top-level `readonly`** — resolved at runtime via the globals table. Value never changes after declaration, but each read is a global-read opcode.
3. **Top-level mutable** — resolved at runtime via the globals table. Values may change between lambda invocations.
4. **Enclosing-function locals** — captured as upvalues per the standard closure mechanism. Extends the local's lifetime beyond the enclosing function's return.

The term **capture** applies only to category 4. A lambda that references a top-level binding does not affect its lifetime — top-level bindings live for the entire script run. A single lambda body may reference bindings from all four categories.

`return`, `break`, and `continue` inside a block-body lambda affect only the lambda regardless of where the lambda is defined. A top-level lambda has the same semantics as a lambda inside a function body. Spec text lives at `grob-language-fundamentals.md` §12.1.

---

### D-297 — `GrobValue` provisional representation (Apr 2026)

Area: VM — value representation
Supersedes: D-142
Superseded by: D-303

`GrobValue` is a hand-rolled `readonly struct` under .NET 10 LTS. Three private fields: a `GrobValueKind` discriminator, a `long _scalar` slot for `int`/`bool`/`float` (floats stored via `BitConverter.DoubleToInt64Bits` to avoid boxing), and an `object? _reference` slot for reference types. Total 24 bytes on x64 with alignment.

`GrobValueKind` has nine variants: `Nil`, `Bool`, `Int`, `Float`, `String`, `Array`, `Map`, `Struct`, `Function`. Plugin types (`date`, `guid`, `File`, `ProcessResult`, `json.Node`, `Regex`, `Match`, `csv.Table`, `CsvRow`, `Response`, `AuthHeader`, `ZipEntry`) and user-declared `type`s all share the `Struct` discriminator; runtime type discrimination happens at the type-registry level via the boxed reference. This keeps the discriminator small and stable as plugins register new types.

Encapsulation contract: private fields, public factory statics (`FromBool`, `FromInt`, …, plus `Nil` singleton); inspection via `Kind` and `IsX` predicates; strict accessors (`AsX()`) that throw `GrobInternalException` on kind mismatch; try-accessors (`TryAsX(out)`) for plugin and runtime defensive code; full `Equals`/`GetHashCode`/`==`/`!=`. No callers outside `Grob.Core` access the fields directly.

The shape is **provisional pending OQ-005**. The internal layout is the only thing OQ-005 may change; the public API surface is stable. The OQ-005 decision (tagged union vs NaN boxing) is deferred until clox is complete because that decision requires real bytecode-VM experience to make well. The provisional shape isolates the OQ-005 decision behind a clean boundary so the eventual retrofit, whatever shape it takes, is localised to `Grob.Core` and does not leak into `Grob.Compiler` or `Grob.Vm`.

Hand-rolled rather than .NET 11 `union` because the compiler-generated `union` form boxes value-type cases on every assignment — wrong cost profile for a VM hot path — and the `[Union]` escape hatch produces the same hand-rolled struct anyway, only with an attribute attached. .NET 10 LTS rather than .NET 11 STS because LTS gives v1 room to ship and stabilise without a forced migration. The `[Union]` attribute migration path post-.NET-11-GA is signposted in `grob-vm-architecture.md` as a future one-commit upgrade — adding `[Union]` and `IUnion` to the existing struct gains compile-time exhaustiveness checking on every `switch` over `Kind` without disturbing layout, factories, or accessors. Full byte-level layout, encapsulation contract and rationale in `grob-vm-architecture.md`.

---

### D-298 — `.grobc` binary format skeleton (Apr 2026)

Area: VM — bytecode file format
Supersedes: D-143
Superseded by: none

`.grobc` files use a skeleton binary format with a fixed-shape header followed by sectioned content for the constant pool, instruction stream, function table, source map, and symbol table. The header is 40 bytes, fixed in v1, beginning with the magic bytes `0x47 0x52 0x4F 0x42` (ASCII `"GROB"`) and a `uint16` format version field starting at `1`. Little-endian throughout. Every section is located by an explicit (offset, size) pair in the header, so a loader can read sections in any order and a future format version can append fields without breaking older readers up to the offset they understand.

Cache files live in a `.grob/cache/` side directory next to the source `.grob` file, mtime-driven invalidation, `.gitignore`-friendly. The `.grob` source file is canonical; `.grobc` is optional cache. The side-directory convention matches Python's `__pycache__` and similar tools — generated artefacts stay separate from source and never clutter the working directory.

Per-opcode operand encoding remains incremental, governed by ADR-0013 — opcodes land sprint-by-sprint and the operand layout is documented at the opcode's source of definition. The skeleton spec covers framing only; per-opcode detail follows.

Explicit non-features for v1: cryptographic signing, compression, encryption, multi-chunk packaging, embedded resources, JIT-friendly precomputed metadata. Each is a deliberate omission, not an oversight; if a future need surfaces, it enters via a format version bump and the migration policy in ADR-0013.

The format must be versionable from day one — retrofitting versioning is expensive. ADR-0013 already locked the stability rule (immutable opcode numbers once shipped, format version increment on breaking change). What was left open — magic bytes, header layout, constant-pool wire format, source-map shape — is now fixed at the level needed for Sprint 1 implementation. Full byte-level layout, implementation notes and rationale in `grob-grobc-format.md`.

---

### D-299 — Sprint 8/9 reorder by dependency weight (Apr 2026)

Area: Sprint plan — stdlib build order
Supersedes: none
Superseded by: none

Sprint 8 and Sprint 9 group the thirteen core stdlib modules by dependency weight rather than alphabetically. The single hard cross-module dependency in v1 is `fs → date` — `fs.list()` returns `File` values whose `.modified` and `.created` properties are `date` values. Every other module is independent of every other module at the API surface level. Modules with no inbound dependencies build first.

Sprint 8 delivers `print`, `exit`, `input`, `math`, `strings`, `path`, `env`, `log`, `formatAs`, `guid` — modules with no cross-module dependencies. Sprint 9 delivers `fs`, `date`, `json`, `csv`, `regex`, `process` — modules where `fs` consumes `date`, plus the heavier-API modules whose return types feed downstream code.

The rationale is risk-front-loading. Sprint 8 modules are pure functions and constants — fail-fast, low integration risk. Sprint 9 modules carry the registered types (`File`, `date`, `json.Node`, `csv.Table`, `Regex`, `ProcessResult`) and thus any latent issues in the type-registry plumbing surface here, where the compiler's type-registry support has had Sprint 8 to settle. The `fs → date` link is satisfied because `date` ships in the same sprint as `fs`.

Acceptance criteria for both sprints are unchanged from the per-module specifications in `grob-stdlib-reference.md`. Sprint scope and deliverables in `grob-v1-requirements.md` §4 reflect this ordering.

---

### D-300 — Parser error recovery specification (May 2026)

Area: Compiler — parser error recovery
Supersedes: none
Superseded by: none

The parser is error-recovering and stateless. On a parse failure it emits a diagnostic, builds a placeholder error node, advances to the next recovery anchor, and resumes parsing. A single malformed construct never aborts the parse.

**Synchronisation set.** Recovery anchors are: a statement-boundary newline outside any open bracket, the closing `}` of an enclosing block, and the start keyword of any top-level declaration (`fn`, `type`, `param`, `import`, `const`, `readonly`). The lexer tracks bracket nesting depth so a newline inside parenthesised text is not treated as a recovery anchor. End-of-file terminates recovery unconditionally.

**Error nodes.** Three first-class AST node kinds: `ErrorExpr`, `ErrorStmt`, `ErrorDecl`. Each carries a source range and a diagnostic message and is handled by every AST visitor (type checker, compiler, formatter, LSP). The AST shape mirrors the source even when broken — go-to-definition, hover and completion keep working on surrounding well-formed code.

**Cascade suppression.** Error nodes have type `Error`, which is assignable to and from every other type. Operations on `Error` produce no further diagnostics. An `ErrorDecl` registers a synthetic symbol-table entry so references to the broken declaration do not produce "undefined identifier" cascades. The intent is one diagnostic per root cause.

**No diagnostic cap.** Parser and type checker both report every error they find. This matches the two-mode error collection rule in `grob-v1-requirements.md` §10 and the LSP-on-save workflow.

**Statelessness.** No state across files or invocations beyond the token stream and the AST being built. Same input produces same diagnostics regardless of parse history.

This is a day-one Sprint 1 requirement, not a polish pass — retrofitting error recovery later requires touching every parse method. Spec text and worked example live at `grob-language-fundamentals.md` §29.

---

### D-301 — `select` statement is non-exhaustive (May 2026)

Area: Control flow — `select`/`case` exhaustiveness
Supersedes: none
Superseded by: none

The `select` statement does not enforce exhaustiveness. If no `case` matches the subject value and no `default` arm is present, execution continues past the `select` block with no error. The switch _expression_ (§3.1) does enforce exhaustiveness — the asymmetry is intentional.

The switch expression must produce a value, so a missing case means a missing value, which is a bug. The `select` statement runs side-effecting blocks and produces no value; "no case matched, do nothing" is a legitimate intent and forcing exhaustiveness here would push authors to write `default { }` solely to satisfy the checker, adding noise without adding safety.

The split mirrors C# (`switch` statement non-exhaustive vs switch expression exhaustive) and is consistent with the same distinction in F#, Scala, Rust and Kotlin. Reach for the expression form when producing a value; reach for the statement form when running side-effecting branches. Spec text lives at `grob-language-fundamentals.md` §3.

---

### D-302 — Benchmarking infrastructure (May 2026)

Area: Tooling — benchmarking and regression detection
Supersedes: none
Superseded by: none

Grob ships with a BenchmarkDotNet harness in a new `bench/Grob.Benchmarks` console project — sibling to `src/`, `tests/`, `plugins/` and `tooling/`. Three benchmark categories map to the three layers of the pipeline: compile-time (lex, parse, type check, emit), VM execution (hand-constructed `Chunk` instances), and end-to-end script (the thirteen validation suite scripts through the full pipeline). `[MemoryDiagnoser]` is applied to every benchmark, putting per-op allocations and Gen 0/1/2 collection counts into the baseline alongside timing.

Test materials live under `bench/Grob.Benchmarks/Fixtures/`: VM benchmarks construct `Chunk` instances directly in C# (no separate file format); end-to-end benchmarks consume **frozen copies** of the thirteen validation scripts (decoupled from the live `tests/Grob.Integration.Tests` copies so script evolution does not silently invalidate baselines); the compile-time category uses a synthetic 1000+ line script produced by a committed deterministic generator (the generated file is gitignored, regenerated on first run). BenchmarkDotNet setup/teardown is specified per category — `[GlobalSetup]` reads source files into memory once, `[IterationSetup]` resets VM state where needed, the measured method runs the smallest meaningful operation. Full operational detail in `grob-benchmarking-strategy.md` §7.

A separate stability test catches managed-side retention invisible in single-run timing. **Initial placeholder values (10,000 iterations, 100-iteration warmup, 10% tolerance) are calibrated empirically at Sprint 8 close** via a single-iteration characterisation pass against the stdlib-substantial build — iteration wall-clock, steady-state heap, iteration-to-iteration variance. Locked numbers ship in `bench/Grob.Benchmarks/baseline/stability.json` with the calibration date; calibration outcome recorded as an addendum to this decision in the decisions log. The stability test runs at a longer cadence than the per-sprint benchmark run (once per release, or on demand). Stability test failure is a release-gate fail.

Grob-aware memory introspection (closure retention root tracing, reachable `GrobArray` counts, upvalue depth) is **explicitly deferred post-v1**. The v1 architecture preserves the option; the implementation does not.

Baselines are committed JSON in `bench/Grob.Benchmarks/baseline/`. Per-sprint regression policy: full benchmark run at sprint close, 5% regression on end-to-end script benchmarks is the gate. Compile-time and VM execution numbers are informational, used to localise regressions surfaced end-to-end. Improvements update the baseline; trade-offs that regress performance for correctness or clarity update the baseline with a decisions-log entry capturing the rationale.

No `grob bench` CLI surface in v1 — benchmarks are implementation infrastructure, not a feature shipped to Grob users. Entry point is `dotnet run -c Release --project bench/Grob.Benchmarks`. Implementation timing: skeleton at the close of Sprint 2 (first meaningful code to benchmark, explicitly added to Sprint 2 deliverables in `grob-v1-requirements.md` §4); stability test plus calibration at the close of Sprint 8 (first stdlib-substantial sprint, explicitly added to Sprint 8 deliverables in §4). Full spec at `grob-benchmarking-strategy.md`.

---

### D-303 — `GrobValue` is a tagged union — OQ-005 resolved (May 2026)

Area: VM — value representation
Supersedes: D-297
Superseded by: none

OQ-005 closed. `GrobValue` is permanently a hand-rolled tagged-union `readonly struct` on the shape locked in D-297. NaN boxing is rejected for Grob.

The struct shape, the nine-variant `GrobValueKind`, the encapsulation contract, the factory and accessor surface and the `.NET 10` LTS target are unchanged from D-297. The change is one of status only: "provisional pending OQ-005" becomes "locked." Documentation across the corpus is updated to drop the provisional framing.

**Rationale.** NaN boxing is elegant in C — clox uses it as a measurable size win over its initial tagged union by packing a 48-bit pointer into the unused payload bits of an IEEE 754 NaN. In a managed runtime that pattern is a hard mismatch with the platform.

The .NET GC is a moving collector. It finds live references by walking GC metadata (root-set stack frames, static fields, and reference-typed fields inside reachable objects) emitted by the JIT. A `ulong` field is, to the GC, an integer — it is never scanned for references and never updated when a compacting collection moves the underlying object. Packing a managed reference into a `ulong` therefore breaks GC tracing in a way the runtime cannot detect: collections can free objects the VM still holds, compaction can leave the packed address pointing at moved memory, and there is no exception or warning when it goes wrong.

The escape hatches do not rescue the design. `GCHandle.Alloc(Pinned)` keeps an object at a fixed address but pins it for the lifetime of the handle, fragmenting the heap and degrading collection performance — the opposite of what NaN boxing was meant to buy. A hybrid shape (NaN-boxed primitives plus a separate `object?` reference slot) gives up the single-word size that was NaN boxing's only meaningful win while keeping all the bit-manipulation cost, so pays for the technique without receiving its benefit. Either path replaces a clean managed design with `unsafe` code threaded through the VM's hot path and manual handle bookkeeping.

The benefit is also small in context. Grob's hot path is I/O — REST calls, JSON parsing, process spawning, file reads. The cache-pressure argument that justifies NaN boxing for a tight-loop numeric interpreter (clox's Pratt-parsed expressions, fib benchmarks) does not transfer to a script hitting an Azure DevOps API. An 8-byte vs 24-byte value struct does not move the wall-clock needle on workloads dominated by network latency and stdlib allocation.

Two further factors. First, debuggability. A tagged union is legible in a watch window — `Kind = Int, _scalar = 42` reads as the value it represents. A NaN-boxed `ulong` reads as an opaque 16-hex-digit number unless you have a decode helper installed in the debugger. For a language whose v1 audience is the author plus early adopters, legibility under a debugger is a real cost saving. Second, extensibility. Adding a tenth variant to a tagged union is an enum case and a field accessor. NaN boxing's bit budget is finite — every new kind contends with the float-NaN payload space, and every existing kind's bit pattern is part of the wire contract.

The clox detour was the right preparation for this decision. NaN boxing is worth seeing in its native habitat to know why it does not transplant. In C, it is the optimisation the language gives you. In C#, it is a technique fighting the platform for a benefit the workload does not need.

D-297's encapsulation boundary was always the right shape regardless of which way OQ-005 resolved. With OQ-005 closed, the "internal layout may change" caveat in `grob-vm-architecture.md` and the on-struct XML doc is removed; everything else stands.

Full byte-level layout, encapsulation contract, equality and hashing rules, and the .NET 11 `[Union]` migration signpost remain in `grob-vm-architecture.md`. That document's "GrobValue Provisional Representation" section is renamed to "GrobValue Representation."

---

### D-304 — Lean on .NET GC — OQ-006 resolved (May 2026)

Area: VM — memory management
Supersedes: none
Superseded by: none

OQ-006 closed. Grob delegates heap memory management to the .NET garbage collector. No custom mark-and-sweep collector is shipped in v1. The step in the implementation order historically allocated to "GC" is a no-op.

**Scope of the decision.**

- Heap objects (`string`, `GrobArray`, `GrobMap`, `GrobStruct`, `GrobFunction`, plugin-registered reference types) are ordinary CLR objects, allocated normally and reclaimed by the .NET GC when no live `GrobValue` references them.
- Primitive Grob values (`int`, `float`, `bool`, `nil`) live in the `_scalar` field of `GrobValue` and never reach the heap. They generate zero GC pressure.
- The `_reference` field of `GrobValue` is the single root the GC sees per slot. Stack slots, locals, the globals table and the constant pool participate in the normal GC root walk by virtue of being arrays of `GrobValue` reachable from VM state.
- No finaliser is required on `GrobValue` or any runtime-internal type.

**What does not exist in v1.**

- No mark phase, sweep phase, allocation-threshold trigger, or `CollectGarbage()` entry point in `Grob.Vm`.
- No custom heap data structure (no `_heapHead`/`_heapSize`/`Allocate()` plumbing). Each runtime reference type is allocated by `new` and managed by the CLR.
- No GC tuning surface in `grob.json` or the CLI. The runtime exposes no GC settings of its own; users may set CLR GC switches (server GC, concurrent GC) via standard .NET configuration if they choose, but this is not a Grob feature.

**Rationale.** Grob's target workload is I/O-bound scripting — Azure CLI orchestration, DevOps REST tooling, JSON/CSV transformation, file-system walks. The hot path is stdlib calls, not allocation churn. Heap pressure in this workload is dominated by short-lived string objects from `json.parse()`, `fs.readText()` and string interpolation — exactly the pattern the .NET generational GC handles well in Gen 0.

A custom mark-and-sweep collector would compete with the runtime's collector rather than replace it (CLR-allocated objects cannot be hidden from the .NET GC). The cost is significant: a parallel object lifecycle, a marking algorithm correctly synchronised with the VM's frame walk, an allocation hook on every heap-bound value construction, and a new class of latent bug (use-after-free in marked-then-collected-incorrectly cases). The benefit is theoretical until profiling shows a real workload where .NET's collector is the bottleneck.

clox implements its own collector because C has no choice. Grob is in C#; the choice is whether to add a redundant layer to a managed runtime that already solves the problem. The answer is no.

The benchmarking infrastructure (D-302) provides the empirical surface to revisit this. `[MemoryDiagnoser]` on every benchmark records allocations and Gen 0/1/2 collection counts in the baseline. If a real script later shows GC pressure that the .NET collector handles badly, the data to substantiate the case will exist. v1 is not the right time to act on a problem that has not been measured.

**v1 out-of-scope entry.** "Custom garbage collector" is added to the explicitly-out-of-scope list in `grob-v1-requirements.md` §13 alongside the existing permanent exclusions (concurrent GC, JIT compilation). This is a permanent architectural exclusion, not a defer-under-pressure candidate, so it belongs in §13 rather than the §16 risk-insurance scope-cut list.

**Migration path if v1 evidence forces a revisit.** The path is additive, not destructive: introduce a managed-side weak-reference tracking table inside `Grob.Vm`, register heap-bound `GrobValue` constructions through that table, and run a periodic walk over reachable VM state to identify which entries are still live at the VM-semantic level. Even this path does not replace the .NET GC; it sits above it, identifying retention patterns that the platform's collector cannot see (e.g. closure-captured arrays retained beyond their useful lifetime). That is a Grob-aware memory-introspection feature — already noted as deferred post-v1 in D-302 — not a competitor to the platform collector.

---

### D-305 — clox gate satisfied; Sprint 1 cleared to begin (May 2026)

Area: Process — implementation gate
Supersedes: none
Superseded by: none

The clox preparation gate is satisfied. Sprint 1 implementation is cleared to begin. The project-status table is updated accordingly: "clox worked through" moves to done, "Implementation started" moves to in progress.

**What the gate was.** Sprint 1 was sequenced to begin after clox not as a box-tick but because the bytecode-VM decisions made on paper wanted hands-on experience to be trusted in implementation. The core chapters of _Crafting Interpreters_ Part III have now been worked through, including NaN boxing, the upvalue/closure mechanism (D-115), call frames, and the value-representation material. The experience underpinning OQ-005 (D-303) and OQ-006 (D-304) is banked: NaN boxing has been seen in its native habitat, which is precisely what D-303 says the detour was for — knowing why it is the right optimisation in C and the wrong one in a managed runtime. The remaining unread chapters do not bear on the decisions Grob has already locked.

**Why Sprint 1 specifically is safe to start.** Sprint 1's scope is front-end — solution scaffold, the complete `TokenKind` enum, the lexer, diagnostic infrastructure, and the error-recovering parser (D-300). None of it exercises the VM knowledge clox provided; that knowledge pays off from Sprint 2 (the execution loop) onward and lands hardest at Sprint 6 (call frames) and wherever `GrobValue` meets the hot path. Even on the most cautious reading of the gate, nothing in Sprint 1 depends on anything not yet done. The lexer and parser are ground already covered by SharpBASIC.

**`GrobValue` is no longer a pre-Sprint-1 blocker.** The note at D-297 that `Grob.Core` needs a `GrobValue` definition before Sprint 1, with the shape provisional pending OQ-005, is overtaken: D-303 locked the tagged-union shape permanently. `Grob.Core` can define it on the locked shape from the first commit.

No design decision changes here. This entry records that the precondition for implementation is met and the status table now reflects reality, so the log does not read "implementation pending" while code is being written.

---

### D-306 — Bytecode disassembler and execution tracing as developer diagnostics (May 2026)

Area: VM — developer diagnostics
Supersedes: none
Superseded by: none

Grob gains two bytecode-visibility tools, both modelled on clox's `debug.c`. They are development affordances, not language features; neither has any presence in Grob source.

**The disassembler.** A `Disassembler` class in `Grob.Vm`, always compiled (Release included). `disassembleChunk(Chunk)` and `disassembleInstruction(Chunk, offset)` print a chunk's opcodes, operands, constant-pool indices with resolved values, and source line numbers, human-readably. It is the layer-boundary bisection tool: when the VM produces a wrong answer, the disassembler tells you whether the compiler emitted wrong bytecode or the VM executed correct bytecode wrongly. Built in **Sprint 2**, against hand-constructed chunks, before the compiler emits its first bytecode — so compiler output is readable from the first emission. Reached from tests, from a scratch entry point, and from Sprint 12 via `grob dump <file>`.

**Execution tracing.** A `TraceInstruction(chunk, ip)` call at the top of the VM dispatch loop, guarded by `#if DEBUG` in the `Grob.Vm` C# source — not a runtime flag. Prints the value stack and the next instruction every iteration; the firehose for stack-discipline bugs. Compiled into Debug builds, removed entirely from Release. The gating is deliberate and load-bearing: the D-302 VM micro-benchmarks run in Release and exist to catch dispatch-loop regressions, and a runtime `if (_trace)` check would put a branch on the hottest path in the measured binary even when off. `#if DEBUG` makes the cost zero where it is measured. Tracing is therefore reached by compiling a Debug build, never by a CLI flag, and is distinct from `--verbose` (which surfaces `log.debug()` output and is user-facing).

**Sprint placement.** The disassembler lands in Sprint 2, not Sprint 1. Sprint 1 is front-end only (D-305) — there is no `OpCode`, `Chunk` or compiler to disassemble until Sprint 2, which is where those primitives and the dispatch loop already arrive. This keeps Sprint 1's front-end boundary intact. The `grob dump` CLI command is a thin wrapper over the engine and is deferred to Sprint 12 with the rest of the CLI; it is added to the §8 command table and Sprint 12 scope in `grob-v1-requirements.md`.

Detail in `grob-vm-architecture.md` "Developer Diagnostics". Sprint 2 scope and acceptance, the §8 `grob dump` row, and Sprint 12 scope in `grob-v1-requirements.md`.

---

### D-307 — Built-in type names are lowercase; Sprint 1 implementation drift corrected (May 2026)

Area: Type system — built-in type naming
Supersedes: none
Superseded by: none

Sprint 1 acceptance testing surfaced a divergence between the spec and the Increment A implementation: the spec writes built-in scalar types in lowercase (`int`, `string`, `bool`, `float`), while the implementation and its tests had adopted capitalised forms (`Int`, `String`). This entry records that lowercase is canonical and the implementation is the side that corrects.

**Lowercase is settled, not a new decision.** The casing was never an open question. The entire corpus is consistent on it — every signature in the built-in type method registry, every stdlib function signature, every worked example in the fundamentals spec, and the language-fundamentals §8 type rules use lowercase for the built-in scalars. This entry exists to close the loop on a drift, not to choose between two live options.

**Why the casing split is load-bearing.** Built-in scalars are lowercase; user-defined struct types, error types and runtime types are PascalCase (`GrobError`, `IoError`, `SourceLocation`, `Response`, `AuthHeader`). The casing is how a reader distinguishes a built-in scalar from a user or runtime type at a glance. It also matches the Go/Rust/Swift convention the language leans on for free onboarding of C#/Go developers, and it keeps the built-in scalar names out of visual collision with the PascalCase user-type namespace. Capitalising the scalars would erase that signal.

**Correction direction.** The fix lands in production code and its tests: the keyword/type-name table emits `int`/`string`/`bool`/`float`, and existing tests are updated from the capitalised forms to lowercase. No spec edit is required for casing — the spec was already correct. Two related Sprint 1 acceptance findings are handled alongside: the §29.6 worked example gained its mandatory return-type annotation (recorded in `grob-language-fundamentals.md`; the example also carried the same casing typo in its fixture, corrected to lowercase), and the §29.6 fixture's diagnostic-count assertion stays at one for the parser-only Sprint 1 stage and tightens to two when the Sprint 2 type checker lands — the spec's narrated count of two describes the full pipeline and is correct as written.

This is a day-one correction. Every test authored against the capitalised forms before the fix is rework, so the correction is applied in Sprint 1 rather than deferred.

---

### D-308 — Diagnostics raised against catalog descriptors, not code literals (May 2026)

Area: Error model — diagnostic construction
Supersedes: none
Superseded by: none

Components raise diagnostics by referencing a descriptor in a central `ErrorCatalog`, never by writing a code literal at the call site. The string `"Exxxx"` for any given code appears exactly once in the entire solution — in its catalog descriptor. This closes a class of duplication that had already begun to spread (the type checker held seven `"E0002"` literals across six sites by Sprint 2.5) and aligns the implementation with the standing principle that `grob-error-codes.md` is the single source of truth for error codes (ADR-0014, ADR-0017).

**What the catalog is.** `Grob.Core` gains `ErrorDescriptor` — a record carrying the registry's fixed columns: `Code`, `Title`, `Category`, `Status`, `Severity`, and `Throws` (the `GrobError` leaf for runtime codes, null for compile-time codes). `ErrorCatalog` is a static class with one `static readonly ErrorDescriptor` field per registered code, plus an `All` list for enumeration. A component raises a diagnostic with `Diagnostic.Of(ErrorCatalog.E0002, location, message)` where `message` carries only the call-site specifics — the actual types, names or counts — and the code, title and severity come from the descriptor. The code literal is never typed at a call site again.

**Why a descriptor, not a constant.** The naive fix for the SonarQube S1192 finding (string literals should not be duplicated) is `const string E0002 = "E0002"`. That silences the analyser but produces a named literal that carries no title, no severity, no throws leaf, and no connection to the registry — the same drift risk in a thinner disguise, repeated per file. The descriptor binds the code to the rest of its registry metadata in one place, so `--explain Exxxx`, the gold-master error-examples fixtures and the website generator all read from one referent. The S1192 findings disappear as a structural side effect across every file at once, not one file at a time.

**Hand-maintained, guarded by an agreement test.** The catalog's initial content was generated mechanically from `grob-error-codes.md` (94 codes) so it is accurate from the first commit, but it is hand-maintained thereafter — no build-time code generation, no registry parser in the build graph. This keeps the build simple and keeps every line in the file something the engineer wrote and owns. The guard is an agreement test in `Grob.Core.Tests` that parses the registry's summary index and asserts: every registered code has a descriptor, every descriptor is registered, titles match exactly, codes are unique, and the runtime/compile-time throws-leaf rule holds. Adding a code is now a two-part change — a registry row and a descriptor — and the test fails the build if either half is missing. This makes ADR-0017's immutability rule enforceable rather than aspirational.

**Remediation, not just convention.** Because the duplication had already begun spreading at Sprint 2.5, this decision authorises a one-off remediation run across the solution to convert every existing inline code literal to a descriptor reference before the pattern grows. The remediation is scoped in the session deliverable; the convention is recorded here so no new inline literals are introduced after it.

Detail and the descriptor/catalog/test shapes ship in the session zip under `src/Grob.Core/` and `tests/Grob.Core.Tests/`. The diagnostic-construction rule is added to `grob-language-fundamentals.md` (diagnostic infrastructure) and to the Sprint 1 acceptance notes in `grob-v1-requirements.md` (diagnostic infrastructure is Sprint 1 front-end scope per D-305).

---

### D-309 — Benchmark execution production mechanism: local → GitHub Actions (May 2026)

Area: Tooling — benchmarking workflow
Supersedes: none
Superseded by: none
Refines: D-302

**Context.** D-302 authorises the benchmarking harness and establishes `bench/Grob.Benchmarks/baseline/` as the committed location for baseline JSON. D-302 was silent on _how_ those JSON files are produced — local developer invocation was the implied path.

**The gap.** At Sprint 2 close, external QA ran the benchmark locally and produced a `BenchmarkDotNet.Artifacts/` directory at the repo root, demonstrating that the local-run path works but also demonstrating its hazard: local results are not reproducible across machines, OSes, or hardware generations. A baseline produced on a developer laptop under load is not a credible anchor for a 5% regression gate.

**The decision.** Baselines are produced via the `benchmark.yml` GitHub Actions workflow, not via local invocation. The workflow trigger is manual (`workflow_dispatch`). The workflow uploads the full BenchmarkDotNet artifact to GitHub Actions as `benchmark-results-<runner>-<run-id>` (90-day retention). The engineer downloads, extracts, and commits the `-report-full.json` as the category baseline file.

The `-report-full.json` (not `-report-brief.json`) is the committed baseline — it contains `HostEnvironmentInfo`, which records the CPU, OS, runtime version and GC mode the measurement was taken on. Without it the baseline is numbers without provenance.

**The runner choice — Windows.** The canonical runner for all baseline and regression runs is `windows-latest`. Grob's user base is Windows developers and sysadmins (per the project's design corpus); Windows runner is the closer match to user-facing performance reality. Ubuntu runners produce slightly lower measurement noise on GitHub-hosted infrastructure, but the representativeness trade-off favours Windows for this project. **All future baseline runs must use the same runner type.** Cross-runner comparisons are noise; mixing runner types across baselines of the same category voids the comparison.

**D-302 deliverable unchanged.** The Sprint 2 acceptance text in `grob-v1-requirements.md` §4 ("the first baseline JSON is committed") and the baseline shape in `grob-benchmarking-strategy.md` §7.6 / §11 (BenchmarkDotNet JSON, one per category) are unchanged. Only the production mechanism changes. The benchmarking strategy doc is updated in the same session (Sprint 2 QA fix) to reflect the workflow as the canonical path; local invocation is reframed as a debugging tool.

**Local invocation — retained as secondary path.** `dotnet run -c Release --project bench/Grob.Benchmarks` continues to work and is documented in the strategy doc and README. It is the right tool for investigating a benchmark crash, a JIT anomaly, or a one-off exploratory measurement. It is not the right tool for producing committed baselines.

**Refined by D-333** (July 2026): "same runner type" is refined to "same CPU
identity" — the `windows-latest` label is not a hardware pin, and a same-label,
different-CPU pair (AMD EPYC 7763 vs Intel Xeon Platinum 8370C) produced a false
regression the OS-family-only guard missed. The gate now keys on
`HostEnvironmentInfo.ProcessorName` directly, with allocation gating regardless of
CPU and time gating only on a CPU match rather than refusing the comparison outright.

---

### D-310 — C# 14 / .NET 10 SDK pinning correction (May 2026)

Area: Tooling — build configuration
Supersedes: none
Superseded by: none

**What happened.** `Directory.Build.props` pinned `LangVersion` to `13` for part of Sprint 2, while the project targets .NET 10 (SDK 10.0.x). .NET 10 ships with C# 14; building with `LangVersion 13` wastes language features and creates a silent divergence between the SDK version the build actually uses and the version the project explicitly targets.

**Correction.** `LangVersion` was updated to `14` in `Directory.Build.props` to match the .NET 10 SDK. The correction is canonical: all future sprints write against C# 14. `global.json` already pinned the SDK channel to `10.0`; no change to `global.json` was needed.

**QA verification.** The Sprint 2-end QA pass (external reviewer) confirmed Debug and Release builds both green with zero warnings under the corrected pinning. The citation points are `Directory.Build.props:7` (the `<LangVersion>14</LangVersion>` line) and `global.json:3` (the channel lock).

**No design change.** This is a build-configuration correction, not a language-design decision. No spec documents change; the canonical language version is implicitly `current SDK` as recorded in `global.json`.

---

### D-311 — Unresolved-identifier `Declaration` sentinel (May 2026)

Area: Compiler — type checker — §3.1.1 invariant
Supersedes: none
Superseded by: none
Refines: D-137

**The invariant.** D-137 requires every identifier node in a type-checked AST to carry a non-null `Declaration` pointing to its declaring AST node. The shape declared in §3.1.1 has `public AstNode? Declaration { get; set; }` — nullable in the property type so Sprint 1 could compile without a type checker existing yet, not nullable as a permitted post-type-check state.

**The gap before this fix.** `TypeChecker.Expressions.cs` assigned `Declaration` for resolved identifiers but left it null for unresolved ones (those that produce E1001). The comment read: "node.Declaration remains null — no declaring node exists." This violated the invariant.

**The fix — singleton sentinel.** The type checker now assigns `UnresolvedDecl.Instance` to every identifier it cannot resolve. `UnresolvedDecl` is a sealed record in `Grob.Compiler.Ast.Declarations`, deriving from `Declaration`, with `SourceRange.Unknown` and a single static `Instance` field. It is symmetric with the `GrobType.Error` sentinel on the type side (§29.3): one shared instance, allocation-free at the failure path, no per-error synthetic node.

**Why a singleton, not per-error synthetic nodes.** Per-error synthetic nodes would allow the LSP's go-to-definition to navigate from an unresolved identifier to a "here is where resolution failed" node. However, the LSP does not navigate unresolved identifiers — it returns an "unresolved" response. A singleton suffices: `ReferenceEquals(node.Declaration, UnresolvedDecl.Instance)` is the go-to-definition guard. Creating a fresh node per error site would add allocation and complexity that solves no real problem. The symmetry with the §29.3 `Error` type also aids readability — both failure paths follow the same pattern.

**Tests.** Three regression tests land with this fix in `TypeCheckerTests.cs`:

- `UndefinedIdentifier_Declaration_IsUnresolvedDeclSentinel` — the Codex repro (`x := missing + 1`); asserts `Assert.Same(UnresolvedDecl.Instance, missing.Declaration)`.
- `UndefinedIdentifier_MultipleSites_AllShareSentinelInstance` — three occurrences of an undefined name; asserts three E1001 diagnostics and `Assert.Same` on each identifier's `Declaration`.
- `Declaration_Invariant_HoldsForBothResolvedAndUnresolvedIdentifiers` — mixed tree; asserts non-null `Declaration` on all identifiers.

---

### D-312 — Bare `grob` invocation prints help (June 2026)

Area: CLI — bare invocation
Supersedes: none
Superseded by: none

**The gap.** The v1 CLI surface defined `grob run`, `grob repl`, `grob check`, `grob fmt` and the rest, but never defined what `grob` does with no subcommand and no arguments — the single most common thing a new user does after `winget install Grob.Grob`.

**The decision.** Bare `grob` is equivalent to `grob --help`: it prints the command listing to stdout and exits 0. It does **not** launch the REPL. `grob repl` is the sole REPL entrance.

**Why not the REPL.** Language runtimes (`python`, `node`, `deno`, `pwsh`) drop into a REPL on bare invocation, but that convention only holds when bare invocation is the *only* REPL door. Grob already gives the REPL a named command, so making bare `grob` also launch it creates two doors to one room — and worse, drops a first-contact user into a modal session they must then work out how to leave. Multi-command CLI tools the audience already lives in (git, go, cargo, dotnet, winget) all print usage on bare invocation. Matching that is the principle of least surprise for an audience of Windows developers and sysadmins. Typing the name should teach the tool, not trap the user.

**Why exit 0, not git/go's non-zero usage error.** git and go treat "no command" as a usage error and exit non-zero (1 and 2) to catch CI scripts that invoke the binary with an empty argument. Grob's exit table already reserves `1` (runtime error) and `2` (compile error); a bare invocation is neither, and adopting the BSD `EX_USAGE` (64) convention would expand the table for marginal benefit on a platform where 64 is uncommon. An accidentally-empty invocation is better caught by the caller's `set -u` / `$ErrorActionPreference` than by Grob inventing a code. Showing help when given nothing is not a failure — exit 0 is consistent with "quiet on success, clear on failure."

Character rationale and the help text itself live in `grob-personality-identity.md` (`--help` output section). The §8 CLI command table in `grob-v1-requirements.md` gains a bare-`grob` row — flagged as a follow-up edit; that document was not in this session's upload.

---

### D-313 — Two-axis benchmark regression policy and the regression gate (June 2026)

Area: Tooling — benchmarking
Supersedes: none
Superseded by: none
Refines: D-302, D-309

**Context.** D-302 established the harness and the committed baselines; D-309 moved baseline production to the `benchmark.yml` workflow and pinned `windows-latest`. Both were silent on the operational question: at sprint close, _who_ compares the new run against the baseline, _how_ the comparison is performed and what stops a slow per-sprint regression from compounding invisibly.

**The gap — single-axis comparison ratchets.** The §9 policy compared each sprint's run against the _immediately prior_ committed baseline and then updated that baseline. A regression below the 5% gate passes, becomes the new baseline and the next sprint measures against the degraded number. A steady 4%-per-sprint creep — each step individually "in tolerance" — compounds to roughly 60% over a dozen sprints while never tripping the gate. Comparison was also unowned and unmechanised: copying the workflow artifact over the baseline is replacement, not comparison.

**The decision — two comparison axes.**

1. **Per-sprint gate (noise filter).** Fresh run compared against the _rolling_ baseline (`baseline/<category>.json`, updated each sprint). Threshold **5%** on a gating category. This catches acute regressions; 5% is a sane noise floor for a shared `windows-latest` runner, below which measurement noise produces false positives. Tightening it has a precondition — a quieter measurement (dedicated runner, more iterations, or median-of-runs).
2. **Cumulative ceiling (anti-ratchet).** Fresh run compared against a _frozen origin_ baseline (`baseline/<category>.origin.json`), established once and never auto-updated. Threshold **12%** total drift to v1. A slow creep trips this within a few sprints even when every per-sprint step is in tolerance. The origin is re-frozen only by a deliberate, logged event — for example after the optimisation sprint pays the accumulated debt down.

**Gating category during build-out.** The end-to-end category is the primary gate per §9, but its workload (the thirteen validation-suite scripts) is not runnable until control flow (Sprint 4) and functions (Sprint 5) exist. Until then **compile-time gates cumulatively** — for a scripting language that compiles-and-runs on every invocation with no persistent process, compile time is real wall-clock time-to-result, not merely diagnostic. VM execution is informational while it remains a first baseline with no origin to anchor against. When end-to-end becomes live it becomes the gate and compile/VM drop to informational — a deliberate `policy.json` edit, not an automatic flip.

**The mechanism and ownership.** A committed tool, `tooling/Grob.BenchCheck`, performs the comparison: it reads the rolling and origin baselines and the fresh `-report-full.json`, matches benchmarks by `FullName`, computes the per-benchmark delta on `Statistics.Mean`, guards that the fresh run and the baseline share a runner (`HostEnvironmentInfo`) and exits non-zero on a breach. The `benchmark.yml` workflow runs it after the benchmark run, so the **workflow is the gate** — the run goes red on a regression rather than relying on an eyeball. The maintainer adjudicates: a flagged regression is either fixed before the sprint closes, or accepted as a deliberate trade-off with a baseline update and a decisions-log entry. Thresholds and gating categories live in `bench/Grob.Benchmarks/baseline/policy.json` as data, so the cumulative budget is a number the maintainer edits, not code.

Detail in `grob-benchmarking-strategy.md` §8 (storage, the frozen origin, `policy.json`) and §9 (the two-axis policy and the gate).

**Refined by D-333** (July 2026): adds a third, allocation axis alongside the two time
axes; makes the per-sprint time axis significance-aware against measurement noise;
replaces the OS-family runner guard with a CPU-identity guard, axis-split so
allocation always gates while time gates only on a CPU match.

---

### D-314 — Implementation harness migrated to Claude Code (June 2026)

Area: Development methodology — implementation harness
Supersedes: none
Superseded by: none

**Context.** Through Sprints 1–3 the implementation agent was GitHub Copilot in VS Code, driven by a pre-planned increment workflow and a substantial `.github/` prompt harness. Sprint 4 onward runs on Claude Code in VS Code. This entry records the migration so the change of harness is a logged decision rather than ambient drift, and so the increment prompts have a single durable reference for the new primitives.

**The decision — what changes.** The implementation *workflow* does not change: an in-IDE agent edits the live working tree, one concern per branch, plan-then-build, the Husky.NET pre-push gate, CodeRabbit pre-PR, a PR per increment, `main` protected. What changes is the harness the workflow runs on.

1. **Durable rules live in `CLAUDE.md`.** TDD-first, one-concern-per-branch, the `ErrorCatalog` mandate (D-308), the §3.1.1 non-null invariant, C# 14 / .NET 10 (D-310), verify-before-relying-on-`D-###`, the closed-`OpCode`/stable-parser facts and the model policy are project memory, always in context. Per-increment load-bearing rules (the closed do-not-touch surface, exact error-code numbers, the §-references) stay inlined in each increment prompt — the anti-rogue inline-reference discipline is retained and strengthened, because the durable rules can no longer be edited away between increments.
2. **Plan mode is the approval gate.** The "post a numbered plan and wait for maintainer approval" step is now native plan mode: the agent presents its plan and waits for approval before editing. Replaces the manual instruction.
3. **Increment prompts are slash commands** under `.claude/commands/` (`/sprint-4-a` … `/sprint-4-f`), versioned with the repo as the `.github/` harness was. Kickoffs and QA briefs remain plain markdown under `prompts/<sprint>/`, with archive copies of the increment commands alongside them.
4. **Model policy is unchanged in intent, re-expressed for the harness.** Sonnet 4.6 (High) is the default session workhorse; Opus 4.8 is reserved for named structural sub-problems gated behind "only if this specific thing gets fiddly", expressed as an Opus-pinned subagent under `.claude/agents/` rather than a per-chat model switch. Haiku for genuinely mechanical arms.
5. **The two-model QA loop is unchanged.** GPT-5.3 Codex remains the external adversarial cold-reader — a Claude subagent shares the implementer's blind spots and is rejected for that role. The change is delivery: the QA brief becomes the instruction file for an in-repo Codex CLI run against the merged sprint branch, giving the cold-read real working-tree access rather than a snapshot. CodeRabbit is retained as the in-loop pre-PR reviewer; no Claude reviewer subagent is introduced.

**Why log it.** The harness is part of how every line of Grob gets built. Recording the migration keeps the build contract self-consistent — the Sprint 4 increment prompts reference `CLAUDE.md`, plan mode and the Opus subagent as established, and a reader of the corpus can trace why the prompt format changed shape between Sprint 3 and Sprint 4. The `.github/` Copilot harness is retained in history under `prompts/` for the sprints it drove.

No language-design or specification surface is touched by this entry.

---

### D-315 — `break`/`continue` in `select`: asymmetric (June 2026)

Area: Control flow — `select` statement and loop control
Supersedes: none
Superseded by: none

**Context.** Two corpus documents disagreed on what `break`/`continue` do inside a `select` arm that sits inside a loop. `grob-v1-requirements.md` (Sprint 4 scope) said both pass through to the enclosing loop — `select` transparent, the loop-context stack sees nothing. `grob-language-fundamentals.md` §3 said `break` does not apply inside `select` and the author must restructure into a function or use a flag. D-301 settles `select` exhaustiveness only and does not reach this. This entry resolves the contradiction.

**The decision.** `break` and `continue` are treated asymmetrically inside `select`, because they had different jobs in the C-family the audience comes from:

1. **`break` inside a `select` arm is a compile error — E2211 — at any nesting, whether or not an enclosing loop exists.** In C-family languages `break` is a `switch`-local operation: it controls fall-through. Grob's `select` has no fall-through (D-301), so that meaning is gone. The only remaining interpretation, "exit the enclosing loop", is a silent footgun for the target audience: a C# developer writes a case-terminating `break` reflexively (`break` is mandatory at the end of every C# `switch` case), and under a transparent rule that reflex would blow out of the loop — same code, opposite meaning, no diagnostic. Rejecting it outright with a teaching error is the safe disposition and is consistent with D-275, where `break` in a context with no meaning (`finally`) is a compile error rather than a silent action.

2. **`continue` inside a `select` arm passes through to the nearest enclosing loop**, exactly as in C#. `continue` was never a `select`-local operation — `select` is not iterative — so there is no fall-through meaning to lose and no muscle-memory trap to spring. The skip-to-next-iteration pattern (`case "skip" { continue }` inside a dispatch loop) is core to Grob's input-loop use cases and is preserved with no ambiguity. If there is no enclosing loop, it is the generic out-of-loop error (E2212).

**Consequence — `select` is not loop-control-transparent.** The earlier "transparent, pushes nothing onto the loop-context stack" framing in the requirements is withdrawn. The compiler must track `select` nesting so the type checker/compiler can raise E2211 on a `break` whose nearest enclosing control construct is a `select`, while still resolving `continue` (and a `break` that is *not* inside a `select`) to the nearest enclosing loop.

**Companion finding — E2212.** `break`/`continue` outside any loop was already specified as a compile error in the requirements but had no registry code. E2212 (`break` or `continue` outside a loop, Syntax) is added in the same edit, following the E2207 precedent of one code covering both keywords.

**Escape hatch unchanged.** Exiting an enclosing loop from inside a `select` is done by restructuring into a function and using `return`, or a flag variable — the same v1 answer the scope-cut list already gives for labelled break (post-MVP). Spec text lives at `grob-language-fundamentals.md` §3; Sprint 4 scope and acceptance in `grob-v1-requirements.md` §4.

---

### D-316 — Corpus consistency regime: reconciliation plus a permanent drift gate (June 2026)

Area: Tooling — quality gate
Supersedes: none
Superseded by: none

**Context.** The corpus has drifted from the code and from itself before, in exactly the ways a one-time audit cannot prevent from recurring. The error-code total has read 86, then 94, then 98, then 99 at different points — the registry footer itself records a "stale 86 → 94" correction — while the live count was 103. ADR cross-references have drifted in bulk (`ADR-0007 → 0012`, `ADR-0008 → 0013`). A `D-###` collision (D-286) is on record. Each of these is mechanically detectable. D-308 already proved the pattern for one document: its `ErrorCatalog` agreement test parses `grob-error-codes.md` and asserts the catalog agrees with the registry, failing the build on drift. This decision generalises that pattern to the rest of the corpus.

**The decision — two parts.**

1. **A1, reconciliation.** A one-time pass brought the corpus to a green floor. Findings are recorded in `interlude-A-findings.md`. The non-language corrections made: the error-code canonical total was corrected from a stale 99 to the live 103 (four codes — E0205, E1102, E2211, E2212 — had accrued without a footer count update; no codes were added, removed or renumbered), and the Project Status "Sprint 1" implementation row was corrected to "Sprints 1-4 complete; Sprint 4→5 interlude". Divergences that touch language behaviour or closed-surface spec content (the `Exit` opcode listed in code but absent from §3.3) were surfaced, not silently resolved.

2. **A2, the drift gate.** `tests/Grob.Consistency.Tests` is a stateless xUnit suite that runs as part of the normal `dotnet test` on every commit — no separate cadence, because it is a correctness gate, not a benchmark. It reads the canonical documents from `docs/design/` and the wiki from `docs/wiki/ADR/` at repo-resolved paths and asserts: error-code count agreement (summary index = `ErrorCatalog.All.Count` = the canonical footer total); decisions-log lockstep (no duplicate `D-###`, an exact index↔entry bijection, every supersession target resolving); ADR-reference integrity (every `ADR-00NN` in the design corpus resolves to a file under `docs/wiki/ADR/`); and OpCode/TokenKind completeness (every name the spec declares complete in §3.3/§3.4 exists in the enum). D-308's `ErrorCatalog` agreement test is referenced as the catalog↔registry guard rather than duplicated, so the suite is the single index of every mechanical agreement check in the build. The shared check library lives in `tooling/Grob.DriftCheck`, which also provides a console entry for local `dotnet run` — the xUnit suite is the gate; the console is convenience, over the same logic.

**Self-relative, no frozen baseline.** Every check compares two live facts against each other — a stated count against an actual count, a reference against its target — so none needs a point-in-time snapshot. The D-313 `BenchCheck` frozen-origin pattern remains available if a future check needs one, but no consistency check does, so no `drift.origin.json` is introduced.

**Parsing discipline.** The documents are hand-maintained markdown. Every parser is defensive: a check that cannot locate its anchor section fails loudly (`AnchorNotFoundException`) naming the document and the expected anchor, never passing by silently finding nothing. A green result means "checked and agreed", never "found nothing to check".

**Consequence.** The consistency floor is enforceable rather than aspirational. Each future increment that changes a count, a reference or an enum either keeps the corpus in agreement or fails the build the moment it does not.

---

### D-317 — Project hardening interlude — supply-chain, build, workflow, secret and provenance integrity (June 2026)

Area: Tooling — supply chain
Supersedes: none
Superseded by: none

**Context.** The Grob _repository_ — its dependencies, build, CI workflows, secret hygiene and provenance — is distinct from the _language and runtime_ security covered by §11 of `grob-v1-requirements.md` (`process.run`, `@secure`, plugin-as-arbitrary-code). §11 hardens what a Grob script can do; this decision hardens how the Grob project itself is built and shipped. The two do not overlap. Run as the second half of the Sprint 4→5 interlude, after the A-increment drift gate (D-316) was green, this is additive build-and-CI work that touches no `src/` code, no spec language content, no error catalog and no test logic.

**The decision — additive hardening across six controls.**

1. **Dependency integrity.** Central Package Management with every version pinned to an exact value (floating ranges removed), a single pinned NuGet source with `packageSourceMapping` and disabled fallback folders, a committed `packages.lock.json` per project, and `--locked-mode` restore in CI so an unexpected transitive change fails the restore rather than resolving silently. Restore-time `NuGetAudit` (`NuGetAuditMode=all`) escalates advisories to build errors via the existing `TreatWarningsAsErrors`.
2. **Vulnerability scanning.** A `dotnet list package --vulnerable --include-transitive` CI gate, the existing Trivy filesystem scan, and CodeQL — confirmed wired via GitHub Advanced Security **default setup** (no workflow file; default setup scans the default branch and pull requests, and an advanced workflow would conflict with it). Dependabot covers the NuGet and GitHub-Actions ecosystems.
3. **Deterministic builds.** `Deterministic`, CI-only `ContinuousIntegrationBuild`, `EmbedUntrackedSources` and `PublishRepositoryUrl` with the SDK-built-in SourceLink; the `global.json` SDK pin (10.0.300, `rollForward: latestFeature`) is consistent with D-310; analyzer and tool versions are pinned through CPM and a `.config/dotnet-tools.json` manifest.
4. **Workflow hardening.** Every GitHub Action pinned to a full commit SHA, least-privilege `permissions`, `persist-credentials: false` on checkout, `step-security/harden-runner` in audit mode on Linux jobs, `concurrency` to cancel superseded runs, and no `pull_request_target` exposure.
5. **Secret hygiene.** A gitleaks CI gate (pattern-based, working tree and full history) added alongside the existing TruffleHog verified-credential scan, so a planted secret fails the job whether or not it is a live credential; `.gitignore` coverage confirmed for local supply-chain artefacts.
6. **Provenance scaffolding.** A CycloneDX SBOM (`sbom.json`) produced as a build artifact on every CI run, and `actions/attest-build-provenance` wired into the release workflow, guarded so it activates only when a `v*` tag publishes real artifacts.

**Deferred — not omitted (OQ-018).** Code signing of the `grob` executable, signed release artifacts, full SLSA Level 3 provenance and cross-machine reproducible-build attestation are attached to the first public release, when the runtime first ships as a distributable artifact. The runtime is not a shipping artifact in v1, so signing is not scaffolded against a build that ships nothing. The earlier association of signing with OQ-013 was a framing error — OQ-013 on disk is the `Grob.Llm` plugin.

**Consequence.** The repository has a supply-chain and build-integrity floor that fails the build the moment a dependency, a workflow or a committed secret regresses, complementing the language/runtime security of §11 without overlapping it.

---

### D-318 — Named-argument call-site diagnostics get dedicated codes (June 2026)

Area: Type system — named-argument diagnostics
Supersedes: none
Superseded by: none

The four named-argument call-site errors D-113 specifies — a named argument
before a positional one, a named argument naming a required (defaultless)
parameter, a duplicate named argument, and a named argument naming an unknown
parameter — get four dedicated error codes rather than folding into E0003 (wrong
number of arguments):

- E0008 — named argument before positional
- E0009 — named argument names a required parameter
- E0010 — duplicate named argument
- E0011 — unknown parameter name

Dedicated codes, not a fold into E0003, because each of the four is a distinct
mistake with a distinct fix, and the diagnostics are part of the product: E0008
wants "move named arguments after positional ones", E0009 "this parameter has no
default, pass it positionally", E0010 "this parameter is already supplied", E0011
"no parameter named X — did you mean Y?". Folding all four into E0003 would
mis-describe them (none is an arity error) and collapse four targeted suggestions
into one generic message, against the error-message quality bar. The alternative
considered — reuse E0003 for arity-shaped cases and E0004 for the rest — was
rejected for the same reason: the call-site binding errors are a category of their
own, not instances of arity or type mismatch, which still apply on the bound
argument set after binding succeeds.

The codes occupy the next-free slots in the E00xx Type block (E0006 and E0007
taken). Registered in Sprint 5 Increment B through their `ErrorCatalog` descriptors
(D-308); immutable once shipped (ADR-0017). Full calling-convention detail is in
`grob-language-fundamentals.md` (the named-argument rules) and D-113.

---

### D-319 — Browser playground: a client-side embedding host with a virtual filesystem (June 2026)

Area: Tooling — playground
Supersedes: none
Superseded by: none

**Context.** A browser playground — type-check and run Grob in the page, as the
Topaz and Go playgrounds do — is wanted as recruitment material and as the most
direct "try it" surface for the LinkedIn series and `grob-lang.dev`. The question
it forces is not whether a playground can be built but whether the engine is
embeddable enough to host one without a server. Two routes were weighed: (1)
client-side — the managed pipeline compiled to Blazor WebAssembly and run in the
page; (2) server-side — the real `grob` CLI executed per request in a hardened
sandbox.

**The decision — Route 1, client-side.** The playground runs `Grob.Core`,
`Grob.Compiler`, `Grob.Vm` and the pure stdlib in Blazor WebAssembly, served as
static assets from the existing Azure Static Web App (`grob-lang.dev`). Nothing is
sent to a server. This is chosen over the server route because it carries no
hosting cost and — decisively — no untrusted-code execution surface: running
arbitrary user scripts server-side is a real security commitment a solo project
should not take on. The client-side route keeps the "nothing leaves your machine"
property as a stated feature, not a convenience.

**The architecture — the playground is a second embedding host.** The standard
library is already plugins the host registers at VM startup, and the host already
chooses the registration set. The playground is a host that wires an alternate
capability set:

- `fs` → an in-memory virtual filesystem (path→bytes), seeded with sample data,
  populated by browser file upload, exported as a zip. The same in-memory
  implementation is the unit-test filesystem double, so it is not playground-only
  cost.
- `env` → a synthetic, host-supplied variable map.
- `process` → unsupported. Calls raise a runtime error in the existing `GrobError`
  hierarchy with a host-supplied message naming the playground limitation. **No new
  shipped error code** — this is a host condition, not a language error, so the
  immutable error registry (ADR-0017) stays clean. Surfaced as a pre-flight banner
  on Check ("this script uses `process`, which the playground can't run") plus the
  runtime diagnostic if run anyway. User-facing phrasing: "some functionality not
  currently supported in the playground".

**The enabling principle — a pure, embeddable engine.** Route 1 only works if the
engine never touches the OS directly and never assumes it owns the process. Every
side effect goes through an injected host capability interface — provisionally
`IFileSystem`, `IEnvironment`, `IProcessRunner`, `IStandardStreams`, `IClock`,
`IRandomSource` — implemented by the CLI host with the BCL and by the playground
host with the in-page equivalents. This is not playground-specific: the LSP and the
test suite want the identical embeddability. The capability interfaces sit alongside
`IGrobPlugin` in `Grob.Runtime`, confirmed against `grob-solution-architecture.md`:
`Grob.Vm` already references `Grob.Runtime`, and exposing the capabilities through
the existing `GrobVM` registration surface adds no new reference and no cycle. This
puts the capability contract on the public plugin NuGet surface — which is correct,
since a third-party plugin doing I/O must also route through it to stay sandboxable.
No OS call may
leak down into `Grob.Core`, `Grob.Compiler` or `Grob.Vm`; host contact stays in the
host layer, as the CLI already does.

**Two correctness points that bind regardless of the playground.** `exit()` is
already designed for this: `ExitSignal` (D-110) — an uncatchable internal signal in
`Grob.Runtime` — unwinds the run, the host observes it and reads the exit code, and
a bare `catch e` (D-274) cannot reach it. The playground host catches `ExitSignal`
at the top of its run loop exactly as the CLI host does; the runtime never calls
`Environment.Exit`. And the VM must be fresh
per run with no static mutable run-state — the playground re-instantiates on every
Run, the LSP re-runs constantly, the test suite runs thousands of times per process
— extending the parser's statelessness discipline (D-300) to the runtime. The
`ErrorCatalog` statics (D-308) are immutable shared data and are unaffected.

**One accommodation has a current-sprint window.** Each accommodation above is built
when its module is built — stream injection and `exit` with the Sprint 8 IO
built-ins; `fs`/`env`/`process` capabilities with the Sprint 8/9 stdlib — so
flagging them is enough. The exception is a cheap cancellation/step-budget check in
the VM dispatch loop: Blazor WebAssembly is single-threaded by default, so a runaway
script freezes the tab with no way to interrupt. Its natural window is current v1 VM
work, and it is the hot path. **Adopted into v1 scope** and folded into Sprint 5
Increment C (`feat/lambdas-and-natives`): a `CancellationToken` on the VM run entry
(default `CancellationToken.None`, unlimited) and a masked step-budget check in the
dispatch loop, with the step counter on the **VM instance** so the budget spans
re-entrant native→VM→lambda execution — a runaway lambda invoked by a native is
caught, not only a runaway top-level loop. It surfaces as `OperationCanceledException`
(a .NET exception outside `GrobError`), so a Grob `catch e` cannot swallow it, the
same uncatchable property as `ExitSignal`. No new opcode, no new error code. Increment
C because that is where the re-entrant call-back bridge lands, so the
budget-spans-the-bridge property is testable there; the rest of the playground build
stays post-v1.

**Build timing — deferred, post-v1.** The playground is not v1 sprint scope and not
a release gate. This entry settles the route and the host architecture so the
Sprint 8/9 stdlib work is built against the right seam; the playground itself is
built post-v1. Full detail — capability-contract sketches, the per-module
disposition and the accommodation map — is in
`docs/design/grob-playground-architecture.md`.

---

### D-320 — `select` is a reserved identifier, not a hard keyword (June 2026)

Area: Lexer / parser — keyword vs method-name collision
Supersedes: none
Superseded by: none

**Context.** D-280 made `.select()` the single universal pipeline
transformation on `T[]` — the LINQ-for-scripting identity move. D-301 made
`select` the non-exhaustive multi-branch statement keyword. The two collide. A
lexer that reserves `select` emits a keyword token, so the member-access parser
— which expects an identifier after `.` — rejects `arr.select(...)`. The type
checker, compiler and VM never meet that barrier because they operate on the
registered native, so they support a call no source program can write.
`grob-language-fundamentals.md` §12 already uses `.select(...)` as the canonical
pipeline form while §3 reserves the word: the spec demonstrates a call its own
lexer forbids. The consequence is not cosmetic — six of the thirteen
release-gate scripts (3, 5, 7, 8, 9 and 12) call `.select(...)` from source and
cannot parse under keyword lexing, so the headline transform is reachable only
from C# test harnesses calling the native directly.

**The decision — `select` is a reserved identifier, not a keyword.** This is
the mechanism D-282 established for `formatAs`. The lexer emits `Identifier` for
`select` and `select` leaves the keyword set. The statement parser promotes a
leading `select (` at statement-head position to the `select` statement;
everywhere else — after `.`, or as a primary — `select` is an ordinary
identifier, so `expr.select(...)` parses as member access. Because a reserved
identifier can never be bound by user code, a leading `select (` at statement
head has exactly one possible meaning: there is no lookahead heuristic and no
grammar ambiguity. The type checker forbids `select` as a declared binding name
— field, parameter, local or function — under the same rule that already
governs `formatAs`.

**A gap in D-282, now filled.** D-282 shipped the reserved-identifier rule for
`formatAs` with no error code. A single new code — **E1103, reserved identifier
used as a binding name** — serves both `formatAs` and `select`. The v1
reserved-identifier set is `{ formatAs, select }`. The two are not identical in
every respect: `formatAs` is compiler-rewrite sugar with a bare-member rule
(`<expr>.formatAs` without a following call is an error), whereas `select` is an
ordinary method, so no bare-member rule attaches to it — `.select` is plain
member access. `case` and `default` remain hard keywords; they have no method
form, so there is no collision to resolve.

**Alternatives rejected.** Reverting D-280 to `map`, or renaming the `select`
statement, each dodges the collision by weakening a deliberate identity
decision. Collapsing the statement into a `switch` statement removes this
instance but not the class, and reopens the settled statement/expression naming
split (D-301). The reserved-identifier route keeps both D-280 and D-301 intact
and generalises to any future keyword/method clash — which the consistency gate
(D-316) now enforces mechanically: a new check in `Grob.Consistency.Tests`
asserts that no registered native method name collides with a reserved word, so
this class of drift cannot recur silently.

**Spec and implementation.** Spec text: `grob-language-fundamentals.md` §3 (the
select-statement lexing note) and a new "Reserved identifiers" subsection in
§12; `grob-error-codes.md` E1103; the `TokenKind` keyword list in
`grob-v1-requirements.md`. Implemented as a Sprint 5 **correctness increment**
slotted immediately after the merged Increment C, ahead of D/E/F — the parser
must accept `.select` before the increment that registers the array
higher-order natives and before the six scripts can run, and the fix is
independent of the closure and named-argument work. It legitimately edits
already-merged Sprint 4 lexer behaviour — the `select` statement shipped
reserving the keyword — which is within this increment's declared scope, not a
closed-surface breach. The feature increments D, E and F keep their letters; no
F→G renumber.

---

### D-321 — Top-level fn bindings hoisted; pass-1 registers value bindings; E5902 narrowed to value cycles (June 2026)

Area: Runtime / type checker — top-level initialisation order
Supersedes: none
Superseded by: none

**The decision.** Every top-level `fn` binding is established (`Initialised`) before any top-level code executes: the compiler emits each top-level function's `DefineGlobal` in a prologue ahead of the first top-level statement, so a function is runtime-available throughout top-level initialisation. The two-pass type checker's pass 1 now registers top-level value bindings — `readonly` and mutable `:=` — in addition to `fn` and `type`, so a function body resolves a top-level value declared later in source without E1001. As a consequence, **E5902 (circular initialisation) is narrowed to genuine value-binding initialisation cycles only**; a call to a function declared later in source is no longer an E5902 trigger — and never should have been one.

**Rationale — a soundness gap between static analysis and the runtime.** This refines D-294 (which detected the cycle but, because functions were emitted in source order, raised E5902 on a forward _call_ — a non-cycle) and D-166 (the two-pass checker, whose pass 1 omitted value bindings, contradicting §19.1's promise that all top-level names are in scope from inside any function body). The two were coupled: the checker accepted both `greet(); fn greet()...` and a function body reading a later-declared top-level value, yet the runtime broke the first (E5902 on a non-cycle) and the checker broke the second (E1001). Static analysis promised what the runtime denied. Hoisting closes the first gap; pass-1 value registration closes the second. The computeA/computeB cycle of §19.1 is reachable _because_ functions are hoisted — the call resolves and the cycle surfaces at the value read.

**Why it is clean.** Top-level named functions capture no upvalues (D-296 captures enclosing-function locals only; a top-level fn references globals), so the prologue is a plain binding sequence with no capture-ordering hazard. Pass-1 value placeholders carry a `provisional` flag so the same-scope redeclaration check (E1102) treats a pass-1 entry as not-yet-declared and is unaffected. The VM composes the E5902 diagnostic through the `ErrorCatalog.E5902` descriptor (D-308), tracing the function: the binding being initialised, the function that read the uninitialised binding and the binding read, each with its source line — matching the §19.1 template.

**Spec and implementation.** Spec text: `grob-language-fundamentals.md` §19.1. Compiler: `VisitCompilationUnit` three-phase emission (const pre-pass so a hoisted fn body inlines any top-level `const`; fn prologue; then top-level statements in source order). Type checker: pass-1 value registration with the provisional-symbol flag. VM: `CallFrame.Callee` and the top-level-binding pre-scan feed the message composer. Landed as a Sprint 5 correctness increment after Increment E; refines, and does not regress, 5E's initialisation-state machine and flow-sensitive narrowing.

---

### D-322 — Runtime diagnostics are line-granular; per-opcode column tracking deferred (June 2026)

Area: VM / diagnostics — runtime source position
Supersedes: none
Superseded by: none

**The decision.** Runtime diagnostics carry `file:line` only. The column is omitted, not idealised — a runtime error renders as `script.grob:5`, never a fabricated `5:15`. Per-opcode column tracking is deferred out of v1 scope. Gold-master fixtures, harnessed and rich alike, record the actual line-only output, so the executed master and the long-form `--explain` example agree; neither shows a column the runtime cannot produce.

**Why the gap exists.** The compiled `Chunk`'s debug information is keyed by source line, not by `(line, column)` — the disassembler (D-306) prints source line numbers for the same reason. Compile-time diagnostics are unaffected: the lexer and parser put full `(file, line, column)` on every token and AST node (D-137), so the §10 format example (`deploy.grob:14:12`, a compile-time E0001) is met as written. The shortfall is the runtime layer only, where the read site reduces to a line.

**Not a D-321 regression.** D-321 narrowed E5902 to fire at the value read inside a function body — a function-body opcode — which surfaced the line-only rendering on the `circular-initialisation` example (`:5`, no column). Before D-321, runtime E5902 fired on the forward call, also a runtime opcode and also line-only. Runtime diagnostics never carried a column; D-321 exercised the existing gap, it did not introduce it.

**Scope and follow-up.** Columns at runtime mean a finer debug table emitted per opcode and threaded through the VM's position lookup — a real VM change, correctly outside the init-order increment and outside v1. A companion known-limitation note belongs in `grob-vm-architecture.md` (Chunk debug info is line-granular; runtime diagnostics emit `file:line`, no column, pending per-opcode position tracking); that doc was not in this session's upload and is flagged as a follow-up. Reopens when a real diagnostic needs runtime column precision badly enough to pay for the per-opcode table.

---

### D-323 — Three-pass type checker: value-binding type resolution phase (June 2026)

Area: Type checker — value-binding type resolution
Supersedes: none
Superseded by: none

**The decision.** The type checker becomes three-pass: registration (pass 1) → value-binding type resolution (pass 1.5) → validation (pass 2). Phase 1.5 walks every top-level value binding (`readonly` and mutable `:=`) in initialiser-dependency order using a DFS (Unvisited/Visiting/Visited, mirroring §17.1's type-field cycle walk) and updates each symbol's provisional `GrobType.Unknown` placeholder to the binding's real static type before pass 2 validates function bodies.

The immediate fix: a function body reading a forward-declared value binding previously received `GrobType.Unknown` from `VisitIdentifier`, so `TypesAreAssignable(Unknown, int)` returned `false` and E0005 fired incorrectly. Phase 1.5 means pass 2 sees `int` (or whatever the real type is) and the assignability check is correct.

**Dependency edges.** An unannotated binding's type depends on the types of top-level value bindings directly referenced in its initialiser. A function-call result contributes the called function's declared return type (known from pass-1 fn registration), so `readonly x := f()` with `f(): int` resolves to `int` with no value-binding dependency edge. An annotated binding — `readonly a: int := expr` — is resolved from its annotation regardless of `expr`'s type: no dep edge runs through the initialiser. This is the key split: annotated bindings are always typeable in O(1) from their annotation.

**Cycle handling.** A back-edge in the DFS (a Visiting binding is referenced) is a circular type dependency. For an unannotated mutual cycle (`readonly a := b` / `readonly b := a`) neither type is resolvable — E0303 fires at compile time and both bindings are assigned `GrobType.Error` for cascade suppression. For an annotated mutual cycle (`readonly a: int := b` / `readonly b: int := a`) each type is resolved from its annotation; no back-edge forms; the type-checker is satisfied. The cycle is structurally sound at the type level and surfaces only at runtime as E5902 when `b`'s value is `Uninitialised` at the point `a`'s initialiser runs.

**Invariant preserved.** Phase 1.5 calls `UpdateProvisionalType`, which keeps `Provisional = true` on the updated symbol. Pass 2's E1102 guard (`!existing.Provisional`) is therefore not triggered for the update. `UpdateProvisionalType` is a no-op when the named symbol is already non-provisional (a fn or type declaration whose name is also used by a value binding) — pass 2 then handles E1102 for the collision as before.

**New error code.** E0303 — "circular type dependency among top-level value bindings". Category: Type. Compile-time. Distinct from E5902 (runtime circular initialisation).

**Gold master.** The `circular-initialisation` example previously used string interpolation (`"${product} — built for scripting"`) because interpolating an Unknown operand did not trigger a type error, allowing the fixture to reach the VM. Phase 1.5 resolves `product` to `string` before pass 2, so any return form works. The fixture is updated to `return product` (a direct return that exercises the corrected forward-read path). The expected file drops the fabricated column (`:5`, per D-322). Quarantine lifted.

**Spec edits.** §17 updated to include value bindings in the registration pass and to introduce phase 1.5. New §17.2 "Value-binding type cycles" documents the DFS, E0303 and the annotated/unannotated cycle distinction. §19.1 forward-read note updated to state that forward-declared top-level value bindings resolve to their real type (not Unknown) inside function bodies.

**Refines D-321 and D-166.** D-321 registered value-binding names in pass 1 to prevent E1001 on forward reads; D-323 also resolves their types so that pass-2 assignability checks are correct. D-166 established the two-pass type checker; D-323 adds the intermediate resolution phase.

---

### D-324 — Uniform top-level redeclaration: E1102 for all binding forms (June 2026)

Area: Type checker — name-collision detection
Supersedes: none
Superseded by: none

**The decision.** E1102 ("name already declared in this scope") is broadened from `:=`-only to all top-level binding-introducing forms: `fn`, `type`, `readonly`, `const`, and `:=`. Any two top-level declarations that share a name — regardless of their kinds — produce E1102 at the second (offending) declaration.

**Pass-1 change.** `fn` and `type` declarations are now registered as provisional in pass 1 (matching the existing treatment of value bindings). Previously they were registered as real (non-provisional), which caused a reverse-order collision bug: when a value binding preceded an `fn`/`type` with the same name, pass 2 ran `VisitVarDecl` first (the value binding was at a lower line), found the fn/type real, and reported E1102 at the value binding — the earlier declaration, not the offending later one.

**`RegisterProvisionalValueBinding` guard.** A new guard prevents a value-binding provisional from overwriting an `fn`/`type` provisional for the same name. Without the guard, `fn foo` (line 2) registered as provisional, then `foo := 1` (line 1 — processed second in the pass-1 loop because passes are source-order) would overwrite it, and the pass-2 collision would be missed.

**`FinalizeTopLevelBinding` helper.** A new private helper (`FinalizeTopLevelBinding`) unifies the collision predicate. It checks whether the name already has a non-provisional (real) entry in the current scope. If so it emits E1102 at the caller-supplied range (the location of the second declaration) and returns `false`; otherwise it calls `RegisterSymbol` to establish the entry as real and returns `true`. All binding visitors call this helper in pass 2: `VisitFnDecl`, `VisitTypeDecl`, `VisitReadonlyDecl`, and `VisitConstDecl`. `VisitVarDecl` retains its inline check (which cannot use the helper without restructuring its init-visit / type-resolution interleave) and is updated to embed the prior location in the message.

**Interaction with D-323 phase 1.5.** When a value binding and an `fn`/`type` share a name, phase 1.5 (`ResolveTopLevelValueBindingTypes`) may call `UpdateProvisionalType` on the `fn`/`type`'s provisional entry, setting its type field. This is benign: `VisitFnDecl`/`VisitTypeDecl` in pass 2 call `RegisterSymbol` with `GrobType.Unknown` regardless, overwriting the stale type. No change to `TypeChecker.ValueResolution.cs`.

**E1102 title.** Broadened from "variable already declared in this scope" to "name already declared in this scope". The error code count remains 109; no new code is introduced.

**Spec edits.** §28 (Declaration `:=`) gains a sub-note describing the uniform top-level redeclaration rule, citing D-324. §19 (Script Structure) gains a "Name uniqueness" paragraph stating the single shared name space for all top-level binding-introducing forms.

**Refines D-321 and D-323.** D-321 introduced provisional registration for value bindings; D-324 extends provisional registration to `fn`/`type` and unifies the collision predicate. D-323's `UpdateProvisionalType` guard (`Provisional = true`) continues to prevent false E1102 for phase-1.5 type updates.

---

### D-325 — Upvalue closing is location-based, not value-based (June 2026)

Area: VM — closure upvalue lifecycle
Supersedes: none
Superseded by: none

**The decision.** A closure closes its captured locals by **stack location**, not by inspecting the return value. The VM maintains an **open-upvalue list** keyed by stack slot (a list sorted by slot descending, the clox `openUpvalues` shape). `OP_CLOSURE` capturing a local either reuses the existing open upvalue for that slot or creates one. On frame exit — `OP_RETURN`, and any explicit `OP_CLOSE_UPVALUE` the compiler emits when a captured local leaves scope early — every open upvalue at or above the returning frame's base is **closed**: its value is copied off the value stack into the heap upvalue cell, and it is removed from the open list. Closing is driven by the stack boundary alone and is indifferent to which heap object now references the closure.

**Root cause of the fault.** Retrieving a closure from a container and calling it (`fn f(): ...[] { inc := () => ...; return [inc] }` then `arr[0]()`) faulted the VM with a value-stack underflow. The cause was **value-based closing** — the close pass reached closures referenced *directly* by the return value but missed closures wrapped in an array, map or struct. The missed closure kept an **open** upvalue pointing at a stack slot the frame's return truncated; the subsequent read of that upvalue underflowed the value stack. Location-based closing has no notion of "the return value", so every escape route is covered by the same boundary sweep.

**The invariant.** After a frame returns, no open upvalue may reference a slot at or above that frame's base. The post-condition is checkable: an assertion (`#if DEBUG`) that the open-upvalue list head, if any, sits strictly below `frame.StackBase` after each `OP_RETURN` makes a regression of this class fail loudly rather than underflow later.

**Scope boundary.** This concerns **category-4 capture only** (D-296) — locals from enclosing function scopes captured as upvalues. Categories 1–3 (const-inlined, top-level global read, top-level global mutable) reference the globals table and are unaffected. Top-level `fn` hoisting (D-321) captures no upvalues and is explicitly out of scope; this change does not touch the prologue or the init-state machine.

**No new error code.** The corrected path produces correct behaviour; it surfaces no diagnostic. Error-code count stays at 109; the D-316 drift gate is unaffected.

**Spec edits.** `grob-vm-architecture.md` gains an upvalue-lifecycle section (open and closed states, the open-upvalue list, capture at `OP_CLOSURE`, the frame-exit close sweep, the post-return invariant). That document is not in this upload — the section is authored by the increment, and this is recorded as a pending spec edit alongside the D-322 known-limitation note already flagged against the same doc.

**Regression.** `Grob.Vm.Tests` gains an **escape matrix**, not a single case, because the bug class is "a captured closure escaped indirectly": closure in array → index → call; closure in map → lookup → call; closure as struct field → access → call; closure returned then immediately invoked; closure passed as a parameter then called. Each must observe the captured value, not fault. A test covering only the array case would invite a point-fix that re-breaks the others.

**Relates to D-296 and D-321.** D-296 defines the four-category capture model; D-325 fixes the runtime mechanism for category 4. D-321 excluded itself from upvalues (top-level fns capture none); D-325 is the complementary fix for the closures that do. Sprint 5 Increment 3.

---

### D-326 — Function types are a first-class type-reference form (June 2026)

Area: Type system — function types
Supersedes: none
Superseded by: none

**The decision.** `fn(ParamTypes): ReturnType` is a type reference. `ParseTypeRef` accepts it anywhere a type is written — parameter annotations, return annotations, binding annotations, array element types. This resolves a contradiction that has been latent in the spec: v1 requires an explicit return type on **every** function (§9), and closures are first-class **returnable** values (D-296), so a named function that returns a closure — `fn makeCounter(): fn(): int` — must annotate its return type, and that type *is* a function. The parser rejected the only expressible form with E2001, leaving the canonical `makeCounter` example in the four-category-capture section (D-296) asserting syntax the implementation refused.

**Why this direction.** Three rules cannot all hold: explicit return types on every function; closures are returnable; no function-type syntax. Narrowing the spec to match the parser does not remove the contradiction — any named closure-returning function reintroduces it — so the only clean resolutions are to make function types expressible or to relax explicit return types (a larger v1 reversal that reopens return-type inference, deliberately deferred post-v1). Dropping returnable closures is off the table — it is core identity. Function types are expressed. The consuming path (passing lambdas to built-in `.filter`/`.map`/`.select`) already works through inference against internal signatures and is unchanged; none of the thirteen release-gate scripts depend on user-written function types, so this is a completeness fix for the Functions-and-Closures sprint, not a release-gate unblock.

**Grammar.** `FnType := 'fn' '(' (TypeRef (',' TypeRef)*)? ')' ':' TypeRef`. `ParseTypeRef` gains an `fn` arm. No conflict with declaration parsing: `fn` at statement head is a declaration; `fn` in type position is a type. The two positions never overlap.

**Suffix precedence.** `?` and `[]` bind to the **return type**. `fn(): int?` is a function returning `int?`; `fn(): int[]` returns `int[]`. A nullable function, or an array of functions, requires parentheses: `(fn(): int)?`, `(fn(): int)[]`. This is the single genuine ambiguity and is specified explicitly.

**Identity and assignability.** Structural identity — two function types are equal iff parameter arity, parameter types (positionally) and return type all match. Assignability is **invariant**: no covariance or contravariance in v1. `fn(int): int` is assignable only to `fn(int): int`. Nullable widening still applies as for any type (a non-nullable function value is assignable to the matching `T?` function slot); invariance governs the param/return structure only. Invariance keeps the checker simple and is consistent with the explicit-return-type / no-inference-ambiguity ethos; variance is reassessed post-v1 only if real scripts demand it.

**Monomorphic at the user surface.** Users write concrete parameter and return types — `fn(int): bool`, not `fn(T): bool`. Type variables remain unavailable to users (D-080: consume generic functions, declare none), so function types introduce no generic surface. The type registry's internal `→` callback notation (`filter(fn: T → bool)`) is generic machinery internal to the stdlib and is left as is; the two notations are reconciled in documentation as a tertiary tidy-up.

**Runtime: erased.** The callable is already a `GrobFunction` value; the function *type* is a compile-time concept only. No opcode, no `.grobc` change, no `GrobValue` impact. Blast radius is confined to the parser (`ParseTypeRef`), the type checker (a `FunctionType` `GrobType` with structural equality and the invariant assignability rule) and the type-system docs.

**Three-pass checker fit.** Function-type annotations resolve during pass-1 signature registration — they reference type names pass 1 already knows, with no initialiser-dependency ordering — so D-323's phase 1.5 is not involved and is unaffected.

**No new error code.** A function-type mismatch is an ordinary assignment/argument type error and reuses the existing mismatch family; malformed function-type syntax reuses the existing syntax codes. The exact descriptors are confirmed against `ErrorCatalog`/`grob-error-codes.md` in-increment per D-308. Count stays at 109.

**makeCounter.** The example stops being aspirational — it becomes a passing parser-plus-checker test in this increment.

**Spec edits.** §9 gains the function-type form in the type-reference grammar (citing D-326); `grob-type-registry.md` documents `FunctionType` (structural identity, invariant assignability) and cross-references D-296.

**Relates to D-080, D-296, D-166 and D-323.** Distinct from D-080 (function types are not generics; the no-user-generics rule is untouched). Completes the D-296 closure story at the type-annotation level. Slots into the D-166/D-323 multi-pass checker without disturbing it. Sprint 5 Increment 4.

---

### D-327 — Array type-refs: the `[]` suffix is a type-reference production (June 2026)

Area: Parser / type system — array type-references
Supersedes: none
Superseded by: none

**The defect.** The `[]` array suffix is used as a type annotation pervasively across the corpus — `int[]`, `string[]`, `File[]` (D-109), `Match[]`, struct fields (`children: Tree[]`), nested `int[][]`/`int[][][]` (D-182) — and the whole stdlib reference is specified in `T[]` return and parameter types. Yet the `TypeRef` grammar formalised by D-326 (§9) had exactly three productions — a named type, a function type and a parenthesised-nullable-function — and **none produced `[]`**. The prose immediately beneath that grammar nonetheless asserted "`?` and `[]` bind to the return type … an array of functions requires parens", referencing a `[]` suffix the grammar never defined. The parser, written to the grammar, never consumed `[` in type position: `int[]` in **value** position parses (array literal, indexing), but `int[]` as a standalone **type annotation** failed. The gap stayed latent until D-326 formalised the type-reference grammar and `(fn(): int)[]` made the missing production visible.

**Why this direction — and not a walk-back.** The alternative on the table was to remove the `[]` examples from type-ref positions until a later generics sprint. That is not viable. First, it contradicts a closed decision: D-182 decided `int[][]` is a valid type. Second, it mislocates arrays as a user-generics feature — per D-080 and the `map` decision, arrays are a **built-in constrained generic users consume but cannot declare**, the same class as `map<K, V>` whose angle-bracket form already parses via `TypeArgs`; array type-refs have zero dependency on user-facing generics. Third, it would make the stdlib un-annotatable, since every `T[]` signature in `grob-stdlib-reference.md` depends on it. The defect is a grammar omission to complete, not a feature to defer.

**Grammar.** `TypeRef` is recast as a primary type carrying a postfix suffix chain, which both adds `[]` and resolves `?`/`[]` precedence uniformly:

```ebnf
TypeRef     := TypePrimary TypeSuffix*
TypeSuffix  := '[' ']'                  // array of the preceding type
             | '?'                       // nullable
TypePrimary := Identifier TypeArgs?      // named type: int, string, map<K, V>, File, user types
             | 'fn' '(' (TypeRef (',' TypeRef)*)? ')' ':' TypeRef   // function type
             | '(' TypeRef ')'           // grouping — required to suffix a function type
```

`ParseTypeRef` parses a primary, then loops consuming `[` `]` and `?` left to right. The grouping primary `'(' TypeRef ')'` generalises and replaces D-326's dedicated `'(' TypeRef ')' '?'` production — strictly more expressive, one fewer special case, and the only way to suffix a function type (`(fn(): int)[]`, `(fn(): int)?`). No ambiguity with the `fn` param-list parens: a leading `(` with no preceding `fn` is grouping.

**Suffix semantics.** Left-to-right application makes `int[]?` a **nullable array of int** and `int?[]` an **array of nullable int** — distinct types, both parse. `int[][]` is an array of arrays (D-182), no rectangular guarantee. Because a function type's return is itself a `TypeRef`, a trailing suffix binds to the **return type**: `fn(): int[]` returns `int[]`; an array *of* functions needs grouping — `(fn(): int)[]`. This is exactly the precedence the D-326 prose described; D-327 supplies the production that makes it real.

**Checker fit.** An array annotation resolves to the `T[]` array type the checker already owns — value-position literals and `:=` inference already construct it, with its full member registry (`.select`, `.append`, `.format.*`, mutation rules). This increment wires the **annotation path** to that existing representation; it introduces no new runtime type. Resolution happens during pass-1 signature registration alongside other type-name references, with no initialiser-dependency ordering, so D-323's phase 1.5 is not involved.

**Runtime: erased.** An array type-ref is a compile-time annotation over the existing array value representation. No opcode, no `.grobc` change, no `GrobValue` variant.

**Error recovery.** Malformed suffixes reuse the existing parser machinery under D-300: an unterminated `int[` synchronises to the statement boundary and emits an `ErrorDecl`/`ErrorStmt` with the standard expected-token diagnostic; a fixed-size form `int[5]` is rejected — Grob has no fixed-size array types — with guidance toward `int[]`. The exact descriptors are confirmed against `ErrorCatalog`/`grob-error-codes.md` in-increment per D-308. No new error code; count stays at 109. If the in-increment review judges the `int[5]` case to warrant a bespoke "fixed-size array types are not supported" descriptor against the error-message quality bar, that is a deliberate, separately-logged addition — not assumed here.

**Spec edits.** §9's type-reference grammar is replaced with the primary-plus-suffix form and its precedence prose, citing D-327; `grob-type-registry.md`'s `fn(T…): R` surface-syntax note gains the parallel `[]`-binds-to-return clause. The pervasive `int[]` examples already in the corpus now match an implemented grammar.

**Relates to D-326, D-182, D-080, D-300.** Completes the D-326 type-reference grammar. Realises the D-182 nested-array type at the annotation level. Distinct from D-080 — array type-refs are built-in constrained generics, not a user-generic surface. Recovery rides on D-300. Sprint 5 Increment 5.

---

### D-328 — Test-coverage scope and floor (June 2026)

Area: Tooling — quality gate
Supersedes: none
Superseded by: none

**Context.** Overall SonarCloud coverage drifted to 88.9%, below the standing 90% expectation.
The gap was two unrelated problems wearing one number: genuine under-tested logic
(`Compiler.Statements.cs` at 49.5% with 43 uncovered conditions — half-covered statement
bytecode emission, the worst failure class because it compiles green and emits incorrect runtime
behaviour; the type-checker statement surface similarly exposed), and structurally-untestable or
out-of-scope code counted as if it were testable (CLI read-print IO shells, `tooling/` `Main`
wrappers, single defensive branches on record types). A bare percentage with no defined scope is
undefined — it drifts because nobody can say what it is meant to mean, and "top up to 90%"
invites assertion-free filler that games the number. `RunCommand` and `DiagnosticFormatter` read
as 0% despite being exercised end-to-end by the validation suite and the gold-master
error-examples library — an instrumentation gap, not absent tests; excluding them to move the
number would have hidden the user-facing run-and-diagnostic surface the trust identity most
depends on.

**The decision — scope plus floor.**

1. **Scope.** A committed exclusion set (`sonar.coverage.exclusions` plus matching
   `[ExcludeFromCodeCoverage]`/`.runsettings` for local parity) declares what is deliberately
   out of the denominator, every exclusion carrying a reason: CLI process/IO shells (the REPL
   read-print loop after its eval core is extracted into a testable, covered unit), `tooling/`
   `Main` entrypoint wrappers, and annotated defensive/unreachable branches including value
   variants the compiler does not yet emit. `RunCommand` and `DiagnosticFormatter` are NOT
   excluded — they are covered by fixing validation-suite/gold-master instrumentation. What
   remains is the language implementation proper, where a coverage number carries meaning.
2. **Floor.** The in-scope denominator carries a 90% line+branch coverage floor, enforced in CI,
   red on breach — mechanical not eyeballed (the D-313 benchmark-gate shape), self-relative with
   no frozen baseline (the D-316 drift-regime shape), reading blended coverage including
   conditions because branch coverage on the parser and type checker is the protective number.
   The floor is a tripwire that triggers triage, not a target to fill.

The single outstanding maintainability issue (`csharpsquid:S3776`, cognitive complexity on the
type-checker statement-visit method) is closed in the same increment and sequenced with the
coverage work: cover to pin behaviour, decompose the visit into per-statement-kind handlers
under green, confirm coverage holds. The refactor is behaviour-preserving — same diagnostics,
same codes, same order.

Adds no error codes and changes no language semantics. Promoted to ADR-0018. Implemented as
Sprint 5 Increment 6, before the Codex cold-read, so adversarial QA begins from a clean board.
Detail in `.claude/commands/sprint-5-increment-6-coverage-scope.md` and ADR-0018.

---

### D-329 — MinVer version management (June 2026)

Area: Tooling — versioning
Supersedes: none
Superseded by: none

**Context.** The CLI banner (`grob repl`, `grob --help`) and the REPL header displayed the
hardcoded string `"Grob 1.0.0"` — not read from the assembly version. No `<Version>` was set
in any `.csproj`, so the assembly version defaulted to the .NET SDK implicit `1.0.0`. These
two `1.0.0` strings were not connected: a version bump in the project file would leave the
banner stale, and the test asserting `Contains("Grob 1.0.0", stdout)` would break on every
version increment, making it maintenance friction rather than a real invariant.

Separately, there was no version-management strategy: no semver tags, no tooling to derive
versions from git history.

**The decision.**

MinVer 7.0.0 is adopted via `Directory.Build.props` (central, applies to every project in the
solution). The version source of truth is a semver git tag (`v{major}.{minor}.{patch}`). With
no tag reachable MinVer derives `{major}.{minor}.{patch}-alpha.0.{height}`, where `{height}`
is the commit count since the last tag (or the repo root). `MinVerMinimumMajorMinor=0.5` is
set, so pre-release builds between now and the first sprint milestone tag produce
`0.5.0-alpha.0.{height}`. `PrivateAssets=All` keeps MinVer out of published package graphs.

**Version roadmap:**

- Now → Sprint 6 merge: `0.5.0-alpha.0.{height}` (no tag)
- After Sprint 6 ships: tag `v0.6.0`; after Sprint 7: `v0.7.0`, etc.
- After Sprint 12: tag `v1.0.0`

`ReplCommand` and `Program.cs` read `AssemblyInformationalVersionAttribute` and strip the
`+{hash}` suffix, so the banner always shows the semver portion (`0.5.0-alpha.0.129`).
Banner tests assert `Grob \d+\.\d+\.\d+` (format only) — version-proof through all sprints.

---

### D-330 — Unknown field at construction gets a dedicated code E0012 (June 2026)

Area: Type system — construction-site diagnostics
Supersedes: none
Superseded by: none

**Context.** `grob-language-fundamentals.md` §10 states that all field names must
match the declared type and that unknown field names are a compile error, but cites
no error code. The nearest existing code, **E1002** (undefined member), is described
and categorised for member *access* — `obj.field` reading a field not declared on an
existing value's type — and lives in the Name-resolution block. A construction site
(`TypeName { notAField: v }`) is a different surface: the type is named, the legal
field set is known at compile time, and the user error is "this type has no such
field", not "this value has no such member". Leaving the code unpinned is exactly the
latent gap that produced interludes in Sprint 5 — an implementer either invents a
code or stalls.

**The decision.** Unknown-field-at-construction raises a dedicated **E0012** (Type
category), registered in the E00xx type block immediately after E0011. This follows
D-318's precedent: the named-argument call-site cases took dedicated codes
(E0008–E0011) rather than folding into E0003. It makes the construction diagnostics
symmetric with the call diagnostics — E0012 "unknown field name" is to a named
construction what E0011 "unknown parameter name" is to a named call — and sits it
beside E0103 (missing required field at construction). The distinction from E1002
stays sharp: E1002 remains member access on a value; E0012 is an unknown field named
at a construction site.

Registered in **Sprint 6 Increment B** through its `ErrorCatalog` descriptor (D-308)
— no `"E0012"` literal at any call site. The error-code count moves **109 → 110**;
the D-316 consistency gate asserts catalog↔registry agreement and the new total on the
registering commit. Resolves the §10 unpinned-code gap.

**Relates to D-318, D-308, D-316.** Same dedicated-code rationale as D-318. Raised
through the D-308 catalog. Counted by the D-316 gate.

---
### D-331 — Sanctioned procedures for growing closed surfaces; the AST is not a wire-format contract (June 2026)

Area: Process / harness — closed-surface growth
Supersedes: none
Superseded by: none

**Context.** The harness asserted two hard "nevers" as settled fact: the parser and
AST are grammar-complete from Sprint 1, and an error code is never invented (the
increment prompt assigns it). Both assume each increment prompt has anticipated
everything it will touch. Reality falsified both. Sprint 4E and Sprint 6B each needed a
parser production and AST node that did not exist — the struct-construction node and its
`ParsePostfix` hook — and Sprint 6B needed a second error code (E0013, sibling-reference)
beyond the one (E0012) the prompt pre-assigned. With no sanctioned path for the warranted
case, the legitimate addition surfaced as an improvised gap-fix outside any procedure.
The three harness documents governing codes disagreed — the root `CLAUDE.md` said "stop
for an assignment", `writing-an-error-test` said "register it yourself", `defining-a-type`
sat between — so behaviour depended on which document was anchored to. The error-code
count itself had drifted silently twice before (corrected 86 → 94 and 99 → 103 in the
registry footer), confirming prompt-asserted arithmetic as the fragile part.

**The decision.** Closed surfaces grow through sanctioned, logged procedures rather than
hard "nevers", calibrated to whether the surface carries a stability contract.

- The **`OpCode` enum** and **`TokenKind`** carry a real wire-format contract (ADR-0013,
  the `.grobc` format): closed by default, grown only deliberately via `adding-an-opcode`,
  with versioning consequences.
- The **parser and AST** carry **no** such contract — compiler-internal, never serialised,
  zero backward-compatibility surface. The front end is built incrementally: a feature's
  grammar surface lands with that feature. So the parser and AST are closed *within an
  increment* as scope discipline, extended via the new `extending-the-grammar` skill when
  a feature genuinely needs a node. A back-filled node the spec already described is a fix
  (commit note); a new language surface form is a `D-###`. There is no `.grobc` impact to
  record for an AST change. The AST guardrail is reclassified from architecture invariant
  to scope discipline in `AGENTS.md` and the compiler-engineer agent.
- **Error codes** carry the ADR-0017 immutability contract: each increment declares an
  **error-code budget** (the codes it expects), pre-authorising the common case. A code
  outside the budget walks the new `allocating-an-error-code` ladder — surface the
  fold-versus-new judgement, decide, then register at the next free number from the
  **live** registry in three-location lockstep, the count reconciled against the live
  total and ratified by the D-316 gate. Numbers are never taken from memory or a stale
  prompt.

Two new skills (`extending-the-grammar`, `allocating-an-error-code`) carry the procedures;
`writing-an-error-test` defers to the latter for the code itself. The root `CLAUDE.md`
rules, `AGENTS.md`, the compiler-engineer agent, `defining-a-type` and the unrun Sprint 6
commands (C, D, E) are reworded from walls to paths. The merged Sprint 6 A and B prompts
are left unchanged as the record of what was run. Throughout, the genuine judgement — new
node versus reuse, new code versus fold — stays explicit and surfaced; only the mechanics,
once the judgement is made, are deterministic.

**Relates to ADR-0013, ADR-0017, ADR-0014, D-308, D-316, D-330, D-300.** The wire-format
and immutability contracts are why the opcode and code surfaces keep an explicit
allocate-and-ratify step the AST does not. D-330 (E0012 dedicated, not folded into E1002)
is the model for the fold-versus-new judgement. The D-308 catalog and the D-316 gate carry
code registration and the count. D-300's error-recovery contract holds for any node added
via `extending-the-grammar`. No new error code; the count is unchanged by this decision.

---

### D-332 — Operand-stack right-sizing off the Large Object Heap (June 2026)

Area: VM — operand-stack allocation
Supersedes: none
Superseded by: none

**Context.** The Sprint 6 VM benchmark run (`windows-latest`, AMD EPYC 7763, .NET
10.0.9) showed all three VM benchmarks (`Run_DeclAndArith`, `Run_Interpolation`,
`Run_ControlFlow`) with `Gen0 == Gen1 == Gen2` collections and near-constant
~405,000–415,000 bytes allocated per operation regardless of workload size — the
signature of a single allocation clearing the Large Object Heap (LOH) threshold
(~85,000 bytes) and forcing a full compacting GC on every run. `ValueStack`'s backing
array was `new GrobValue[16384]` — `GrobValue` is a locked 24-byte tagged union
(D-303), so 16,384 × 24 = 393,216 bytes, comfortably over the LOH line — allocated
once per `ValueStack` instance via a field initialiser, and `VirtualMachine` (which
holds one `ValueStack`) is constructed fresh per benchmark operation
(`VmBenchmarks.RunSource`), so every operation paid the full array allocation. The
256-entry `CallFrame[]` (~8 KB) was confirmed not the culprit.

**The decision.** `ValueStack`'s backing array right-sizes to `DefaultCapacity = 1024`
slots (24 KB, comfortably under the LOH line) and grows geometrically — doubling, via
`Array.Resize` — on `Push` when full, capped at the unchanged `Capacity = 16384`
ceiling. At the cap, `Push` throws the existing `GrobRuntimeException` carrying
`ErrorCatalog.E5903` exactly as before; the effective maximum operand-stack depth is
unchanged. Rejected: pooling the backing array across `Run()` calls — it revisits the
VM's fresh-per-run construction and the D-319 per-instance step-budget counter for a
second-order win (per-run _young-gen_ array churn) that only matters once the LOH
pressure is gone, so it is left for a future interlude if the recaptured baseline still
shows it as material.

**Safety argument.** A resize (`Array.Resize`, which grows-and-copies existing values
at their indices) is transparent to the rest of the VM because nothing caches a raw
`ref`/`Span<GrobValue>` into the backing array across a `Push`: the sole
`Span<GrobValue>` in `Grob.Vm` is `ValueStack.AsSpan()`, consumed immediately by the
`#if DEBUG` per-instruction trace hook and never held across an instruction boundary.
D-325's open upvalues survive by construction — `Upvalue` holds a `ValueStack`
_object_ reference plus an integer slot index, never a raw pointer, so `Read()`/
`Write()` always dereference the instance's current `_values` field. No change to
`GrobValue`'s layout, the `OpCode` enum, the `.grobc` format or any dispatch-loop
semantics — the fix is confined to `ValueStack` storage and lifecycle.

**Verification.** New `Grob.Vm.Tests` coverage: the default-capacity array stays under
the LOH threshold (structural assertion, not a GC-count assertion — the latter risks
flaking under parallel test execution, so the LOH proof proper is deferred to the
benchmark re-run); `Push` across the 1024/16384 growth boundaries preserves LIFO
values; the E5903 overflow guard fires at exactly the unchanged cap; an open
`Upvalue` reads/writes correctly across a forced resize; a recursive computation whose
padding drives the stack past `DefaultCapacity` mid-recursion still computes
correctly; a closure's captured local survives a resize forced while its enclosing
frame is still open; two sequential `Run()` calls on one VM (first growth-inducing)
remain correct and isolated. `Grob.Vm.Tests` gained `InternalsVisibleTo` (previously
absent, though documented as the project convention in `src/CLAUDE.md`) so the
resize-survival test can construct `Upvalue` directly. No new error code; count
unchanged. Coverage floor (ADR-0018) unaffected — the new `Push` branches are
100%-covered; project coverage is unchanged from the pre-fix baseline.

**Baseline recapture pending.** Per D-309, a committed benchmark baseline is never
locally produced — it must come from `benchmark.yml` on `windows-latest`. This
session's local run confirms the fix (a shallow structural check plus behavioural
tests) but does **not** update the committed `bench/Grob.Benchmarks/baseline/vm.json`,
which still reflects the pre-fix LOH defect until the CI benchmark run lands on this
branch's PR and the rolling VM baseline (and the VM origin, as a sanctioned re-freeze
per D-313 — the origin was frozen against the buggy first capture) are updated in a
follow-up commit.

**Relates to D-303, D-304, D-325, D-313, D-309, D-319.** D-303/D-304 fix `GrobValue`'s
locked representation and the lean-on-the-GC policy that make this a storage-sizing
fix rather than a representation change. D-325 is the correctness invariant this fix
must not violate. D-313's two-axis regression policy and D-309's CI-only baseline
convention govern how the fix is proved and recorded once benchmarked. D-319's
per-VM-instance step counter is the lifecycle detail a pooling approach (rejected
here) would have had to reconcile with.

---

### D-333 — Benchmark gate hardening: allocation axis, significance-aware time gate, CPU-identity guard (July 2026)

Area: Tooling — benchmarking
Supersedes: none
Superseded by: none
Refines: D-302, D-309, D-313

**Context.** The Sprint 6 benchmark run demonstrated the gate flagging the wrong
things. `Compile_TenPrints` moved +8.7% against a rolling baseline whose own
measurement noise (`StandardDeviation` ≈3.2% of a 6.55 μs mean) already explains the
swing — the per-sprint gate fired on infrastructure noise. Meanwhile all three VM
benchmarks moved by an identical +33.8%, the signature of a whole-baseline shift
(D-332's Large Object Heap defect) rather than three coincident regressions — it read
as merely "info" because the VM category is `gating: false` during build-out, so the
one genuinely broken thing sailed through informationally while noise went red. The
post-Interlude-1 (D-332) verification run then proved a second, distinct gap: it
landed on an Intel Xeon Platinum 8370C against an AMD EPYC 7763 baseline, both
labelled `windows-latest`, and the gate flagged compile time as a +25–37% per-sprint
breach — yet compile allocation was byte-identical across both runs (7,864 B /
14,480 B, unchanged) and Interlude 1 never touched the compiler. The existing guard
(`SameRunnerType`) keyed on OS family, not CPU, so it passed a comparison D-309
already declares invalid.

**The decision — three changes to `tooling/Grob.BenchCheck` and `policy.json`.**

1. **A hard allocation axis.** `[MemoryDiagnoser]` already records
   `Memory.BytesAllocatedPerOperation` into every committed baseline (D-302 body); the
   gate simply never read it. Two sub-checks, both data in `policy.json`: a
   percent-vs-rolling-baseline threshold (`allocPercent`, **10%**) that gates on the
   same categories time gates today, and an absolute **LOH tripwire**
   (`lohTripwireBytes`, **85,000**, the CLR's actual threshold) that fails outright on
   _any_ category — gating or informational — the instant a benchmark's fresh
   allocation clears it. The tripwire is what would have caught D-332 on day one; the
   percent axis catches ordinary creep. Allocation is deterministic, so its threshold
   is tight where time's cannot be.
2. **A significance-aware time gate.** The per-sprint axis now requires a delta to
   exceed `max(perSprintPercent, timeSignificanceK × relativeStdDev)` before it
   breaches — the flat 5% remains a floor, but a delta inside the benchmark's own
   measurement noise no longer trips it. `relativeStdDev` is the larger of the fresh
   and baseline run's `StandardDeviation` as a percentage of `Mean` (the noisier side,
   conservatively). `timeSignificanceK = 3` (three-sigma): checked against
   `Compile_TenPrints`'s ~8.7%-delta/~3.2%-relative-StdDev case (`3 × 3.2% ≈ 9.6%`,
   comfortably absorbing it) while a genuine acute regression (30%-class) stays far
   outside even a noisy benchmark's band. This was D-313's own stated precondition for
   tightening the gate — "a quieter measurement first" — now met by measuring the
   noise rather than assuming a flat floor. The cumulative axis is unchanged by this —
   it already smooths single-run noise by design over the whole v1 arc, and the
   evidence motivating significance was specifically a per-sprint false positive.
   Consecutive-breach filtering (N breaches across runs before failing) was considered
   and deferred: it needs cross-run history the tool doesn't retain, and the
   significance filter alone already resolves the demonstrated case.
3. **A CPU-identity guard, axis-split by hardware.** `SameRunnerType`,
   `DeltaClass.RunnerMismatch` and `Outcome.CannotCompare` are removed outright and
   replaced by a `ProcessorName`-keyed comparison (`BenchCheck.SameCpu`), checked
   independently against the rolling and origin baseline's own recorded host — a
   category's per-sprint and cumulative time axes can therefore differ in whether they
   are CPU-verified. Missing or placeholder CPU data (including a pre-D-333 baseline's
   `"Unknown processor"`) is never treated as a match. **Allocation always gates**,
   regardless of CPU — it is hardware-independent. **Time gates only when the fresh
   run's CPU matches the relevant baseline's**; on a mismatch that axis reports
   informational rather than refusing outright, since hosted `windows-latest` runners
   cannot be CPU-pinned and a hard refusal would make the gate refuse constantly. D-309's
   "same runner type" rule is refined to "same CPU identity" by this mechanism.

**Baseline recapture, same session.** `vm.json` and `vm.origin.json` are both replaced
with the D-332 post-fix Intel Xeon capture (50,265 / 52,841 / 58,473 B — comfortably
under the LOH line), matching D-332's own note that the origin, frozen against the
buggy first capture, needed a sanctioned re-freeze. `compile.json` (rolling) is also
refreshed to the same Xeon capture: it had last been captured in June (pre-Sprint-3)
and, compared against current `HEAD`, showed genuine allocation growth from three
sprints of real feature work (closures/upvalues, the type registry, struct
construction) that nothing had measured before the allocation axis existed — refreshing
now avoids shipping a gate that reads a per-sprint allocation breach on its very first
live run for reasons unrelated to this change. `compile.origin.json` is deliberately
**not** touched: its `HostEnvironmentInfo.ProcessorName` predates CPU provenance
entirely (`"Unknown processor"`, a stale BenchmarkDotNet 0.14.0 capture), and
fabricating a plausible CPU name for historical data would misrepresent its
provenance. The honest consequence, accepted here rather than hidden: the compile
category's **cumulative** time axis reads informational (never gates) until someone
deliberately re-freezes `compile.origin.json` with a real capture — a separate,
logged act, not performed in this session.

**Rejected.** Keeping the OS-family guard alongside the new CPU check (two
overlapping, confusing guards — the CPU check subsumes the OS-family case in
practice, since a cross-OS run will essentially never share a `ProcessorName` string
with the baseline either). Applying the significance filter to the cumulative axis
too (the evidence motivating it was a per-sprint false positive specifically; the
cumulative axis's 12%-over-the-arc threshold already absorbs single-run noise by
construction). Re-freezing `compile.origin.json` in this same session to give it real
CPU provenance (a bigger, separate baseline-policy act than hardening the gate
itself, and this run's Xeon numbers must not silently become the EPYC-arc's origin
anchor).

Detail in `grob-benchmarking-strategy.md` §8 (`policy.json` schema) and §9 (the
allocation axis, the significance-aware time gate, and the CPU-identity rule).

---

### D-334 — `finally` compilation model: VM-run exceptional path, compiler-emitted everywhere else (July 2026)

Area: Exception handling — finally compilation model
Supersedes: none (extends D-275)
Superseded by: none

**The decision.** The closed `OpCode` enum has no `Leave`/`EndFinally`, so `finally`
splits on exactly one axis: the **exceptional** path (an exception unwinding through
a region) is **VM-run**, driven by the handler-table `TryRegion.FinallyOffset`
(`-1` sentinel, the established "absent" small-int convention); every
**non-exceptional** path — normal try/catch completion, an early `return`/`break`/
`continue` — is **compiler-emitted**: the compiler genuinely re-visits (recompiles)
`node.Finally`'s AST at each site that leaves the region, rather than sharing one
copy of bytecode. A finally body's compiled bytecode therefore appears at several
sites in the chunk — the classic javac-pre-inlining duplication — which is expected
and correct, not a defect.

**Compiler side — the enclosing-region stack.** `Compiler.cs` gains a
`TryFinallyContext` stack (`_tryFinallyContexts`), structurally mirroring
`LoopContext`/`_loopContexts`: pushed in `VisitTry` when a `finally` is present, and
popped only after the try body **and every catch body** have compiled — a `return`
from inside a catch must also run the enclosing try's finally, so the context stays
live for the whole guarded region, not just the try body. `VisitReturn` crosses
**every** context on the stack (a return exits the whole function); `VisitBreak`/
`VisitContinue` cross only contexts pushed **after** the target `LoopContext` —
recorded per context as `LoopDepthAtPush`, the `_loopContexts.Count` at push time —
so a `finally` the loop is nested _inside_ is correctly not crossed. Each crossing
re-visits the crossed contexts' finally bodies innermost-to-outermost before the
existing scope-cleanup-and-jump/return. Because each function/lambda body already
compiles in its own fresh sub-`Compiler` instance (the existing pattern
`_loopContexts` already relies on), this stack is scoped per function for free — no
extra boundary bookkeeping needed for a nested lambda.

**The return-value-preservation mechanism.** A `return`'s value is already on the
operand stack by the time the crossed finally bodies are re-visited; a single
`_nextSlot` reservation (bumped, then restored) parks that value below whatever
slots the finally bodies' own locals allocate, so their compile-time slot numbering
never collides with it — no new opcode or stack-shape trick needed, since the VM's
`Return` still bulk-discards the whole frame in one step regardless of how many
finally bodies ran first.

**The normal-completion copy is unconditional.** `VisitTry`'s existing `exitJumps`
convergence point (already the landing site for both normal try completion and
normal catch completion, §27 "when it runs" items 1 and 3) always emits one finally
copy, regardless of whether the try body's own bytecode can statically reach it —
this compiler performs no reachability/dead-code analysis anywhere, so a try body
whose only statement is an early exit still gets this copy, as dead bytes.

**VM side — the exceptional-path arm.** `TryRegion` gains `FinallyOffset`; a
region's _construct span_ for finally-triggering purposes is now bounded by
`FinallyOffset` (covering the try body **and** every catch body) rather than the
narrower catch-matching `EndOffset` alone — a throw from inside a region's own
catch body must also trigger that region's finally, which the catch-matching bound
alone cannot express. `VirtualMachine.Throw`'s outward walk is now driven by
`PropagateThrow`, which for every region passed over without a match runs its
finally via `RunFinallyExceptional` before continuing outward. That method runs the
finally **in the current frame** (unchanged `_stackBase`) via a new bounded mode of
the existing `RunDispatch` (`boundedFinally: true`), stopping at the region's own
closing `TryEnd` (nested trys inside the finally are skipped over via a
`finallyDepth` counter, not mistaken for the boundary). Running in the same frame
means the finally reads enclosing locals by direct slot access, exactly like the
compiler-emitted copies — no call frame, no upvalue capture needed for this path.
A throw escaping the bounded finally raises an internal `FinallyEscape` exception,
caught by `RunFinallyExceptional`, which **replaces** the in-flight exception and
lets `PropagateThrow` resume the outward walk from the next-outer region (D-275).
`exit()`/`GrobExitException` needs no change: it unwinds the .NET call stack past
the bytecode dispatch loop entirely and never reaches `PropagateThrow`, so it
already skips every `FinallyOffset` by construction. D-325 holds unchanged — the
existing `CloseUpvaluesFrom` calls in the outward frame-pop walk are untouched;
running a finally in-frame introduces no new frame boundary to close upvalues at.

**Rejected: closure-based exceptional-path invocation.** Compiling the
exceptional-path finally copy as a genuine closure (capturing referenced enclosing
locals as upvalues, reusing D-296/D-325's machinery) and invoking it via the
existing reentrant `RunDispatch`/`InvokeCallable` call-frame path was considered.
It needs no new VM control-flow primitive — it would just be another call — but it
forces every finally-referenced enclosing local through upvalue capture even in the
common single-region case, and only applies to this one copy: the compiler-emitted
inline copies for the non-exceptional paths still need direct slot access, so
nothing is unified by the extra closure machinery. The bounded-`RunDispatch`
mechanism above was chosen instead.

**Folds in a small, pre-existing reachability gap: E2206.** `ParseTry`'s
catch/finally loop broke unconditionally the instant it matched a `finally`, and
`TryStmt`'s AST shape (a `Catches` list plus one optional `Finally` field) has
nowhere to put a clause that follows the finally — so **E2206** ("`finally` not
last in `try`") was structurally unreachable since Increment B; the AST could not
represent the violation for a type-checker pass to ever see. `ParseTry` now keeps
scanning past a matched `finally` for a further `catch`/`finally` token and raises
E2206 directly — a parser diagnostic, not a type-checker one, since by the time an
AST exists the ordering already holds — before recovering via the existing D-300
synchronisation path. No AST shape change; this is a mechanical correction to
existing grammar handling within the increment, not a new grammar production and
not a decision in its own right.

**No new error code.** E2206 and E2207 were both already registered
`ErrorDescriptor`s (pre-declared, unused, from an earlier budget) — count stays at
116.

**Relates to D-274, D-275, D-296, D-325, D-300.** D-274/D-275 are the grammar and
semantics this increment implements. D-296 is the four-category capture model the
rejected closure alternative would have leaned on. D-325 is the upvalue-closing
invariant this change must not violate (and does not, since it adds no new frame
boundary). D-300 is the parser-recovery model the E2206 fix follows.

---

### D-335 — `Grob.Integration.Tests` restored to `Grob.slnx`; test-project membership is a mechanical gate (July 2026)

Area: Process / CI — solution membership
Supersedes: none (generalises D-316)
Superseded by: none

**What happened.** PR #94 (`28eb753`, "Enhance VM upvalue handling and add first-class
function types", merged 2026-06-26) removed the `tests/Grob.Integration.Tests` line from
`Grob.slnx` while adding `tests/Grob.Consistency.Tests`. The removal was incidental to the
change, was not a decision, and was not logged. From that commit until this interlude,
neither `dotnet test Grob.slnx` nor CI's bare `dotnet test` in `ci.yml` (no project
argument) executed the project.

**The consequences, both of which are worse than the trigger.** First, the Sprint 5
(`functions.grob`) and Sprint 6 (`types.grob`) close-gate smoke masters were never verified
by CI after they were written — two sprints closed on a gate that was not running. Second,
`Sprint6IncrementBTests` was authored, reviewed and merged asserting that `print()` on a
struct value emits field values, an expectation the VM has never met (D-336). The test could
not fail because nothing ran it. This is the "never merge known-wrong code" invariant
breached without anyone being in a position to notice.

**The root cause is not the dropped line.** A `.slnx` edit that drops a project is an
ordinary mistake, and ordinary mistakes are caught by gates. The reason this one survived
two sprints is that the artefact it gated had **no mechanical owner**: an unreferenced test
project fails no build, trips no assertion and contradicts no document. Nothing in the
repository was in a position to be wrong about it. Fixing only the trigger leaves the class
intact — the next accidental drop is equally invisible.

**The decision.** `tests/Grob.Integration.Tests` is restored to `Grob.slnx`. A
**test-project membership check** is added to `tests/Grob.Consistency.Tests`: enumerate
`**/*.Tests.csproj` on disk under `tests/`, assert that each is referenced by `Grob.slnx`,
fail the build on drift. Membership is thereby self-relative and frozen-baseline-free, in
the same shape as D-316's error-code count agreement and D-308's `ErrorCatalog` agreement —
a project cannot go green while orphaned.

**Relates to D-316, D-308, D-336, D-337.** D-316 established the mechanical-agreement regime
and this extends it from documents to project membership. D-336 is the defect this gap
concealed. D-337 documents the smoke-script family whose ownership vacuum is the deeper
cause. No error code added; count unchanged at 116.

---

### D-336 — Value display protocol: `Display`/`Inspect`, and `print()` renders composite values (July 2026)

Area: Runtime — value display protocol
Supersedes: none (corrects §13; supersedes the undocumented `[TypeName]` fall-through)
Superseded by: none

**The divergence.** Three artefacts disagreed, and none of them was wrong on its own terms:

1. `grob-language-fundamentals.md` §13 specifies that `print()` converts value types to
   their string representations and that **reference types call `.toString()`**.
2. User-defined struct types have no `.toString()`. D-179 added the method to every
   *built-in* type ("every built-in type now has `toString()`"); user types were never in
   scope, and v1 has no user-defined methods.
3. `OpCode.Print`'s single-arg fast path is a hardcoded C# switch over `GrobValue.Kind` and
   calls no Grob-level `.toString()` at all. Its fall-through arm renders a struct as
   `[TypeName]`, an array as `[array(N)]`, a map as `[map]`.

The spec was therefore unimplementable as written, and the implementation's behaviour was
never chosen.

**`[TypeName]` was not a convention.** The corpus's only bracket-tag precedents are
deliberate, per-type and security-motivated: D-159 gives `AuthHeader.toString()` the value
`"[AuthHeader]"` explicitly so it *never exposes the credential, including under `--verbose`*.
D-160 gives `ProcessResult.toString()` the value of `stdout` because that is "the most useful
default for interpolation and print" — when the corpus reasons about defaults it optimises
for usefulness. And D-101 establishes `@secure` as Grob's do-not-echo instruction, a handling
instruction rather than a type. Universal struct opacity is therefore not a security posture;
it is a debug formatter's fall-through arm resembling one. Grob currently offers **no way at
all** to inspect a struct's contents: `print` is opaque and `formatAs` does not land until
Sprint 8.

**The decision — two entry points, one public method.** A `ValueDisplay` service in
`Grob.Runtime` becomes the single renderer, consulted by `print()`, by string interpolation,
and by `formatAs` when Sprint 8 lands:

- **`Display(v)`** — the top-level position. Strings render unquoted, preserving D-179's
  `string.toString()` identity: `print("hi")` emits `hi`.
- **`Inspect(v)`** — the nested position, inside a struct, array or map. Strings render
  quoted, so `"8080"` is distinguishable from `8080`. This is Python's `str`/`repr` and
  Rust's `Display`/`Debug`; a statically typed language that cannot show the difference
  between a string and an int in its own debug output defeats its own purpose.

`toString()` remains the sole public surface. `Inspect` is internal to `Grob.Runtime`.
`print()`'s fast path dispatches through `ValueDisplay` rather than switching inline; types
carrying a registered `toString()` (`AuthHeader`, `ProcessResult`, `json.Node`, `guid`) are
consulted through it, so D-159's opacity survives unchanged.

**Dispatch is a numbered precedence, not a type switch.** The order is load-bearing and
step 2 is security-critical:

1. `nil` → `nil`.
2. **The value's type has a registered `toString()` → call it. Terminal.**
3. Scalars (`int`, `float`, `bool`) → their representation.
4. `string` → position-dependent: unquoted under `Display`, quoted and escaped under `Inspect`.
5. Composites (`Struct`, `Array`, `Map`) → structural rendering, recursing via `Inspect`.

**Why step 2 must precede step 5.** Per D-297, `GrobValueKind` has nine variants and
**plugin types and user-declared `type`s all share the `Struct` discriminator** — `date`,
`guid`, `File`, `ProcessResult`, `json.Node`, `Regex`, `Match`, `csv.Table`, `CsvRow`,
`Response`, `AuthHeader` and `ZipEntry` alongside every user `type`. Runtime discrimination
happens at the type-registry level via the boxed reference. Consequently the fall-through arm
that renders `[Config]` is *the same arm* that renders `[AuthHeader]`, and `print`'s fast path
calls no Grob-level `toString()`. D-159's guarantee — that `AuthHeader` never exposes the
credential, including under `--verbose` — therefore holds today **by accident**, produced by
the generic opaque fall-through rather than by the registered method D-159 specifies. A
structural renderer wired ahead of the registry lookup emits the bearer token. Acceptance
criterion: **no type carrying a registered `toString()` is ever structurally rendered**, with a
test asserting `print(auth.bearer(secret))` never contains `secret`.

**`Function` values render as their type.** `print(makeCounter())` emits `fn(): int`;
`fn(int): int` for a parameterised type. D-326 made function types first-class and returnable,
so function values are printable Grob today with no specified rendering. An identity or address
(Python's `<function f at 0x…>`, Go's pointer) is rejected outright: the sprint-close smoke
scripts are gold-mastered against exact stdout, and a non-deterministic rendering makes the
release gate unrunnable. Closures expose no captures.

**`float` renders round-trippable, with a decimal point, under invariant culture.**
`print(1.0)` emits `1.0`, not `1` — .NET's `double.ToString()` drops the fractional part, which
would make `float` and `int` indistinguishable in the output of a statically typed language, the
same defect class the `Inspect` quoting rule exists to prevent. `print(0.1 + 0.2)` emits
`0.30000000000000004`, as Go and Python do. Every numeric conversion in `ValueDisplay` pins
`CultureInfo.InvariantCulture`; `NaN`, `Infinity` and `-Infinity` carry pinned spellings.
Unpinned, a `de-DE` or `fr-FR` host — squarely inside Grob's Windows sysadmin audience — emits
`1,5`, breaking every gold master and writing commas into `formatAs.csv` fields, reproducible
only on machines the maintainer does not own.

**Rendering mirrors source syntax.** A printed value reads back as the literal that would
construct it — a free property, and the reason to prefer named fields over Go's positional
`%v`:

```
Config { host: "example.com", port: 8080 }     // named struct type
#{ host: "example.com", port: 8080 }           // anonymous struct literal (D-114)
[1, 2, 3]                                      // array
{ "a": 1, "b": 2 }                             // map
nil                                            // nil, in any position
```

**Cycles are reachable and must be handled.** E0301 and E0302 reject type cycles with *no
terminating field*, which means `type Node { value: int, next: Node? }` is legal and
`a.next = b; b.next = a` is constructible at runtime. `Inspect` therefore carries
reference-identity cycle detection, rendering a revisited object as `<cycle>`, with a depth
cap rendering `...` as a backstop. The visited set is allocated only when a composite is
nested — the scalar and flat-struct paths allocate nothing.

**Consequences for `Sprint6IncrementBTests`, and a warning to future archaeology.** The four
assertions originally required field values in `print()` output (`Assert.Contains("example.com",
stdout)` and siblings). That expectation was **correct**; the implementation was absent. During
the Sprint 7E interlude they were instead rewritten to expect `[Config]`, before this decision
was taken. Under D-336 they are rewritten again — this time asserting the exact rendered form,
`Config { host: "example.com", port: 8080 }`, rather than substring containment, a stronger
assertion than either prior version.

**The interlude commit that rewrote them to expect `[Config]` does not represent a decision**
and must not be read as one. This matters because the defect was originally mis-diagnosed by
exactly that method: reading `git log`, observing that `GrobValue.ToString()` had never emitted
anything but `[TypeName]`, and inferring the behaviour was therefore intentional. It was not —
it was a debug formatter's fall-through arm, resembling D-159's deliberate per-type credential
opacity by coincidence. A commit message asserting the tests were corrected to match intended
output now exists in the history and will read as confirmation to anyone repeating that query.
It is not confirmation. This entry is the record; the commit is not.

Rewriting the assertions to expect `[Config]` was the wrong remedy for a further reason: it
ratifies an undecided language surface inside a test file with no decision entry, freezes it into
a gold master, and destroys the only artefact in the repository signalling that the spec and the
implementation disagreed. §13 of `grob-language-fundamentals.md` is corrected in the same change
so it describes dispatch that actually occurs.

**Rejected alternatives.** *Positional rendering* (`{example.com 8080}`, Go's `%v`) — loses
field names, and Grob has no `%+v` escape hatch since it has no format verbs. *Leaving
collections opaque and fixing only structs* — `[array(N)]` violates §13 identically, and
`formatAs.table`/`csv` (D-282) is a **presentation** surface (tables, CSV) not an
**inspection** one; the two coexist as `fmt.Println` and `text/tabwriter` do in Go.
*Unbounded element counts* retained deliberately — a script author printing an array wants
the array; only nesting depth is capped.

**Relates to D-179, D-159, D-160, D-101, D-114, D-169, D-282, D-297, E0301/E0302.**
No error code added; count unchanged at 116.

---

### D-337 — The sprint-close smoke-script family is documented (July 2026)

Area: Process — sprint-close smoke scripts
Supersedes: none
Superseded by: none

**The gap.** Five smoke scripts exist and none of them appears in any design document:
`hello.grob` (Sprint 3), `calculator.grob` (Sprint 4), `functions.grob` (Sprint 5),
`types.grob` (Sprint 6), `errors.grob` (Sprint 7). Each was added at a sprint close as an
end-to-end gold master. No decision named them, no document listed them, no Definition-of-Done
row required them. A search of the corpus for any of the five filenames returns nothing.

**Why this matters more than it looks.** D-335 records a test project silently dropped from
`Grob.slnx` and unnoticed for two sprints. The proximate cause was an editing accident; the
reason it survived is that the scripts it gated were owned by no document. An artefact that
appears in no specification cannot make a specification wrong by ceasing to run. Documenting
the family is the other half of D-335's fix — the membership check makes the project's absence
mechanically loud, and this entry makes the scripts' purpose legible to a reader who did not
write them.

**The decision.** `grob-v1-requirements.md` gains a **Sprint-Close Smoke Scripts** section
recording the family, its growth rule (one script per sprint close, cumulative — every prior
script must still pass), its location (`tests/Grob.Integration.Tests`, gold-mastered) and its
contract: **stdout, stderr and exit code**. The exit-code clause is not decoration —
`errors.grob` exits 42 by design, exercising `exit()` as the final statement inside
`try`/`catch`/`finally` where neither the catch nor the finally runs. Any harness assuming
"all smoke scripts exit 0" is wrong as of Sprint 7.

**Distinct from the validation suite.** `grob-sample-scripts.md` holds thirteen release-gate
validation scripts, all of which must compile and run correctly before v1 ships. That suite is
a **v1 gate**; the smoke family is a **per-sprint gate**. Both live in
`tests/Grob.Integration.Tests`. The two were conflated in the Sprint 7 Increment E prompt,
which asserted the smoke scripts live in `grob-sample-scripts.md`; they never did.
`grob-sample-scripts.md` gains a cross-reference so the distinction cannot be lost again.

**Relates to D-335, D-336.** No error code added; count unchanged at 116.

---

### D-338 — Compile-allocation regression from the Sprint 7 exception hierarchy: fixed, residual accepted (July 2026)

Area: Tooling — benchmarking / Compiler — exception hierarchy registration
Supersedes: none
Superseded by: none

**The regression.** At Sprint 7 close, `bench/Grob.Benchmarks`'s compile category showed
`Compile_TwoExpressions` at +68.6% (7,864 B → 13,256 B) and `Compile_TenPrints` at +37.2%
(14,480 B → 19,872 B) bytes/op against the D-333 rolling baseline — a hard breach of the
axis-3 `allocPercent` 10% gate. The identical _absolute_ delta on both fixtures (5,392 B)
was the signature of a fixed per-compile cost rather than one scaling with program size.

**Root cause.** `TypeChecker.RegisterExceptionHierarchy` (introduced in #112, Sprint 7
Increment A, D-284) runs unconditionally in `Check()` on every compile, seeding the 11
`GrobError` hierarchy names (the root plus ten leaves) into the global scope and the
user-type registry regardless of whether the source uses exceptions at all. Three
compounding causes, found in sequence by profiling an isolated reflection harness against
the built assemblies:

1. It re-synthesised a fresh `TypeDecl`, `UserTypeInfo` and `Symbol` per name on every
   single compile, even though all three are immutable and carry identical content run to
   run.
2. The global-scope `Dictionary<string, Symbol>` and `UserTypeRegistry`'s internal
   dictionary both started empty and grew via repeated resize-and-copy as the fixed
   builtin-plus-hierarchy set (14 entries) was inserted.
3. `TypeChecker.TypeCycles.cs`'s §17.1/D-287 required-field cycle-detection DFS
   (`DetectTypeCycles`) walks every registered type on every compile; the 11 new entries
   were walked despite none of them ever carrying a required `GrobType.Struct` field
   (D-274's shape is `message: string`, `location` (erased), and `NetworkError`'s
   `statusCode: int?`), so they can never participate in a cycle.

**The fix — four behaviour-preserving changes, no observable output change.**

1. `ExceptionHierarchy` gains `static readonly` caches of the 11 `TypeDecl`, `UserTypeInfo`
   and `Symbol` instances, built once; `RegisterExceptionHierarchy` registers the shared
   instances instead of allocating new ones (direct `_scopes.Peek()[name] = ...`
   assignment for the `Symbol`, bypassing `RegisterSymbol`, whose per-call-argument
   construction has nothing to do when every argument is constant).
2. The global-scope dictionary is pre-sized to `3 + ExceptionHierarchy.AllNames.Count`
   (builtins plus hierarchy). It still grows normally for further user-declared names;
   only the resize-and-copy cost for the known fixed load is removed.
3. `UserTypeRegistry`'s dictionary is pre-sized to `ExceptionHierarchy.AllNames.Count`,
   likewise growing normally afterwards and removing only the resize-and-copy cost for
   the known fixed load.
4. `DetectTypeCycles` filters `_userTypeRegistry.AllTypes` to exclude
   `ExceptionHierarchy.IsHierarchyMember` names (a new predicate) before seeding `colors`
   and walking; `WalkTypeCycle`'s field loop separately and explicitly skips a hierarchy-
   typed target ahead of, and independent from, the pre-existing "unregistered — E1001
   cascade" branch, so the two distinct reasons for termination are never conflated in one
   reader's mind.

All existing compiler, type-checker and integration tests (2,175 total) pass unchanged,
including the gold-master error examples — the fix changes only what is allocated, never
diagnostics, emitted bytecode or exit behaviour.

**Result.** `Compile_TwoExpressions` +14.0% (8,968 B), `Compile_TenPrints` +7.6% (15,584 B)
— a reduction of the regression from +68.6%/+37.2% to +14.0%/+7.6%.

**The residual — accepted as load-bearing, not fixed further.** After the three causes
above are eliminated, ~1,104 B of fixed per-compile cost remains, verified by isolating
`TypeChecker.Check()` on a fully empty program (1,528 B, deterministic, checker/unit
construction excluded from the timing window). This is the actual cost of a global scope
that must now hold 14 built-in entries instead of 3 — D-284 requires all 11 `GrobError`
hierarchy names resolvable in every compile regardless of source content, so a bigger
symbol table is a real, permanent, per-compile cost with no further re-allocated-content
churn left to remove. Closing this fully would mean lazily registering hierarchy names
only when a compile actually references them (`throw`/`catch`/construction) rather than
unconditionally up front — a materially different and larger change to the timing of
built-in name resolution, rejected here as out of scope for a behaviour-preserving perf
fix and not attempted.

**Consequence for the gate.** `Compile_TwoExpressions` (+14.0%) remains outside the axis-3
`allocPercent` 10% gate (D-333) by deliberate acceptance under this entry, per D-313's
anti-ratchet rule that a regression is either fixed or accepted with a logged decision —
never silently absorbed into an updated baseline. The rolling `compile.json` baseline is
**not** updated by this entry: D-309 requires baseline production via the `benchmark.yml`
workflow on the canonical CPU-tracked runner, never a local invocation, so recapturing
`compile.json` (and, if the maintainer judges it warranted, `compile.origin.json`) against
these accepted figures is a separate, subsequent, logged act.

No error code added; count unchanged at 116.

---

### D-341 — Compile-allocation baseline recapture: D-338's accepted residual folded into the rolling baseline (July 2026)

Area: Tooling — benchmarking
Supersedes: none
Superseded by: none
Refines: D-338

**The decision.** The rolling `compile.json` baseline is recaptured, performing the
"separate, subsequent, logged act" D-338 deferred. The `-report-full.json` from
`benchmark.yml` run [29207744217](https://github.com/grob-lang/grob/actions/runs/29207744217)
(`windows-latest`, host CPU `AMD EPYC 7763`, 2026-07-12) replaces `bench/Grob.Benchmarks/baseline/compile.json`
wholesale, per D-309's canonical-workflow production requirement — no local run is ever
committed as a baseline. The figures are byte-identical to D-338's own measurement:
`Compile_TwoExpressions` 8,968 B, `Compile_TenPrints` 15,584 B. Run locally against the
recaptured baseline, `tooling/Grob.BenchCheck` now reads `0.0%` allocation delta on both
compile benchmarks and the gate passes — the CI run that had gone red immediately after
D-338 merged (comparing the accepted-but-not-yet-recaptured figures against the pre-fix
baseline) was the expected, anticipated consequence of that deferral, not a new defect.

**Why this needed its own entry, not an amendment to D-338.** D-313's mechanism ties a
baseline update to a decisions-log entry as one act that closes out an accepted
regression; D-338 explicitly named the recapture as a distinct future act rather than
performing it. Recording it as a new entry keeps D-338's narrative (the investigation,
the fix, the acceptance rationale) intact and gives the recapture itself a dated,
citable record — consistent with the log's append/supersede discipline rather than
editing a closed entry's body.

**`compile.origin.json` deliberately untouched.** The fresh run's host, `AMD EPYC 7763`,
does not match the frozen origin baseline's `Intel Xeon Platinum 8370C` — a CPU mismatch
that, per D-333's CPU-identity guard, makes the _time_ comparison informational only on
both axes (rolling and origin) while allocation continues to gate regardless of CPU. The
origin comparison now reads `+47.1%`/`+32.8%` cumulative time drift, but this is not a
gate breach because it is informational under the mismatch, not because it was hidden.
Re-freezing `compile.origin.json` is left for a separate, deliberate, maintainer-judged
event, mirroring D-333's own precedent of leaving `compile.origin.json` on its
pre-provenance host rather than folding a re-freeze into an unrelated fix.

**Scope.** `vm.json` and `endToEnd.json` are untouched. The `vm` category's deltas
remain non-gating/informational (D-313) and were not part of the flagged regression;
`endToEnd` still has no fresh benchmarks to compare (the validation-suite scripts are not
yet runnable per D-313's build-out note).

**Companion fix.** D-338's full entry was logged without a matching footer-changelog
line, leaving the "three things in lockstep" rule (index row, full entry, changelog)
two-thirds satisfied. This entry's changelog addition backfills that gap alongside its
own note, since the mechanical drift gate (`Grob.Consistency.Tests`) checks the index↔entry
bijection and supersession links but not the free-text changelog, so the gap was invisible
to CI.

No error code added; count unchanged at 116.

---

### D-342 — Module-namespace resolution: a compile-time name category, no runtime module object (July 2026)

Area: Compiler — module-namespace resolution
Supersedes: none
Superseded by: none
Extends: D-282, D-320

**The decision.** Core stdlib modules (`math` this increment; `path`, `env`, `log`,
`guid`, `formatAs` in later Sprint 8 increments) are **compile-time namespaces** — a
third name category in the global scope alongside value bindings and type bindings.
`TypeChecker.RegisterNamespaces` seeds each namespace as a `NamespaceDecl` sentinel
(mirroring the existing `BuiltinDecl` pattern used for `print`/`exit`/`input`, and the
`ExceptionHierarchy` static-table registration pattern used for the ten `GrobError`
leaves) into the global scope before pass 1 runs. A hand-authored `NamespaceRegistry`
static table maps each namespace's members to either a constant's declared type or a
native's signature — the compile-time twin of what the corresponding `IGrobPlugin`
registers at runtime, following the same "two hand-maintained mirrors plus an agreement
test" shape D-308 already established for `ErrorCatalog` against
`grob-error-codes.md`.

**Dispatch precedence.** At a member-access node `x.y`, the checker's precedence is
fixed: a **namespace** receiver resolves `y` against `NamespaceRegistry` — a known
constant (`math.pi`) resolves to its declared type; a known native (`math.sqrt`)
validates argument arity/types positionally and resolves to its declared return type;
an unknown member (`math.nope`) is **E1003** ("undefined module") — a pre-existing,
previously-unused code from the initial 94-code allocation, reused rather than
duplicated after confirming the fit against the live registry
(`allocating-an-error-code` Step 1). A **value** receiver falls through unchanged to
the pre-existing arms: struct/anonymous-struct field access, and the Sprint 5C array
higher-order methods (`select`/`filter`/`sort`/`each`). A namespace name in **value
position** — `x := math`, `print(math)`, or any other non-member use — is **E1004**
("namespace used as a value"), the direct namespace analogue of the existing
`TypeDecl`-as-value arm (E2102): both are "a name that resolves, but not to a value."
Every node touched sets non-null `ResolvedType`/`Declaration` on both the success and
error paths (§3.1.1), matching the existing E2102 arm's `UnresolvedDecl.Instance`
convention on failure.

**No runtime module object, no new opcode.** A namespace constant (`math.pi`) compiles
to a bare `OpCode.GetGlobal` against the qualified name; a namespace-qualified native
call (`math.sqrt(9.0)`) compiles to its argument(s), then `GetGlobal` against the
qualified name, then the existing `OpCode.Call` — exactly the shape a plain top-level
function call already uses (D-321's `DefineGlobal` prologue binds every top-level `fn`
before any statement runs; a call site does `GetGlobal` then args then `Call`). This is
**not** a literal function constant embedded in the chunk at compile time, despite that
being one way to read "function constant" informally: `Grob.Compiler` never references
`Grob.Stdlib` (the DAG forbids it), so the compiler cannot know a native's actual C#
delegate at compile time. `_globals["math.sqrt"]`/`_globals["math.pi"]` are populated
by the stdlib plugin's registration pass before any script bytecode runs — the same
`_globals[name] = …` write path `RegisterNative` already uses for every other native,
with no `DefineGlobal`-ordering hazard since the write happens at VM startup, not from
compiled bytecode. There is no module value on the operand stack, no `GetProperty`
against a namespace, and no `OpCode.Import` for a core module (`Import` remains
reserved for the plugin/import system, Sprint 11).

**One new error code.** E1004 ("namespace used as a value") is allocated in the E10xx
general-name-resolution sub-block of the Name Resolution category (E1001–E1003
already in use), the next free number in that range. Count 116 → 117. E1003 is
activated for its first real use by this increment but needed no new registration —
it was already a full `ErrorDescriptor` from the initial allocation, unused until now.

Full detail: `grob-v1-requirements.md` §2 (solution architecture, DAG), §3.1.1 (the
LSP-enabling properties this decision's error paths must uphold), and
`grob-stdlib-reference.md`'s Core Modules table and `math` section.

---

### D-343 — Capability-injection seam: `IPluginRegistrar` inversion, `IStandardStreams` landed (July 2026)

Area: Runtime — capability-injection seam
Supersedes: none
Superseded by: none
Refines: D-319

**The decision.** D-319 provisionally sketched six capability interfaces
(`IFileSystem`, `IEnvironment`, `IProcessRunner`, `IStandardStreams`, `IClock`,
`IRandomSource`) "sitting alongside `IGrobPlugin` in `Grob.Runtime`." This entry lands
the injection **mechanism** and the two interfaces Sprint 8 Increment A itself needs.

**`IGrobPlugin`/`IPluginRegistrar` — a narrower registration surface than the VM
itself.** `IGrobPlugin { string Name; void Register(IPluginRegistrar registrar); }`
and `IPluginRegistrar { void RegisterNative(string, NativeFunction); void
RegisterConstant(string, GrobValue); }` both live in `Grob.Runtime`. `IPluginRegistrar`
is a distinct interface from the concrete VM type — not `IGrobPlugin.Register(VirtualMachine
vm)` as the architecture doc's illustrative sketch shows — because `Grob.Runtime` must
never reference `Grob.Vm`: the DAG already has `Grob.Vm` → `Grob.Core` + `Grob.Runtime`,
and the reverse edge would cycle. `VirtualMachine` implements `IPluginRegistrar` in
`Grob.Vm`; a plugin author writing against the published `Grob.Runtime` NuGet surface
never needs to see the VM's concrete type. `RegisterConstant` is new (alongside the
pre-existing `RegisterNative`) because a namespace constant such as `math.pi` has no
callable behaviour to dispatch — it is a plain value written into the globals table.

**`IStandardStreams` — the first capability landed.** `IStandardStreams { TextWriter
Out; TextWriter Error; }`. `OpCode.Print`'s VM handler reads an injected
`IStandardStreams.Out` instead of a bare `TextWriter` field or `Console` directly, so
`Grob.Vm` and `Grob.Stdlib` stay OS-free — matching D-319's "no OS call may leak below
the host layer" principle. `VirtualMachine` gains a new constructor overload taking
`IStandardStreams`; the pre-existing single-`TextWriter` constructor is kept unchanged
and wraps its argument in a minimal internal default (`Out` = the given writer, `Error`
= `TextWriter.Null`) — deliberately, so none of the roughly 39 existing
`new VirtualMachine(writer)` call sites across the test suite and `Grob.Cli` need to
change for this increment. `Grob.Cli`'s composition root (`RunCommand`/`ReplCommand`)
constructs the OS-backed default (`Console.Out`/`Console.Error`) and passes it through
the new overload.

**`print`/`exit` are formalised, not rebuilt.** They stay on their existing dedicated
`OpCode.Print`/`OpCode.Exit` opcodes, compiled by the existing identifier-name special
case in `Compiler.Statements.cs` — they are **not** converted into ordinary
`Call`-dispatched `NativeFunction`s. Converting them would be a real behavioural-risk
architecture change (removing a working, closed-opcode-respecting path) for no required
benefit, and the increment's own instruction is explicit: "do not change what print or
exit do." Instead, a `Grob.Stdlib.IoPlugin : IGrobPlugin` exists purely to give the I/O
capability seam a uniform place in the plugin auto-registration pass — it registers no
callable, since print/exit are not natives.

**Declared but not yet consumed.** `IEnvironment` (`Get`/`Set`/`Has`/`All`), `IClock`
(`UtcNow`), and `IRandomSource` (`NextDouble`/`NextInt`/`Reseed`) are declared in
`Grob.Runtime` alongside `IStandardStreams`, per D-319's sketch — their real consumers
are `env`/`log` (Increment C), `math.random*` (Increment B) and `guid` (Increment D).
No default OS-backed implementation is wired into `Grob.Cli` for these three this
increment; that wiring lands with their first real consumer, to avoid unconsumed,
untested implementation code (the `forward-scaffolding-yagni` discipline — the
interface _shapes_ are the requested seam; their _implementations_ are not). `IFileSystem`
and `IProcessRunner` are not declared at all — they arrive with `fs`/`process` in
Sprint 9, out of Sprint 8's scope entirely.

No new error code; count unchanged at 117.

Full detail: `grob-decisions-log.md` D-319 (the original sketch and rationale),
`grob-vm-architecture.md` "Plugins and Native Functions" (the `IGrobPlugin`
illustrative sketch this entry corrects against the live DAG).

---

### D-344 — `env`/`log`/`input()` land: stdin capability, a new no-namespace-native dispatch category, silent-no-op level parsing (July 2026)

Area: Runtime / Compiler — stdlib host surfaces (Sprint 8 Increment C)
Supersedes: none
Superseded by: none
Refines: D-342, D-343

**Correction to the record first.** This increment's kickoff prompt cited "D-339",
"D-340" and "D-334 clarification" as governing decisions. None of the first two exist in
this log (D-338 is immediately followed by D-341 — no gap-filling entries were ever
logged as D-339/D-340), and D-334 is the `finally`-compilation-model entry, unrelated to
this work. The decisions that actually govern Increment C are **D-342** (module-namespace
resolution) and **D-343** (the capability-injection seam,
`IPluginRegistrar`/`IStandardStreams`/`IEnvironment`). Increment C's throwing natives
(`env.require`, `input()`) also ride the **native-throw seam**
(`Grob.Core.NativeFaultException`), which the VM's `Call` dispatch routes through the same
handler-table walk a VM-detected fault uses. That seam shipped in Increment A and its
implementation cites D-342, but note that the D-342 _entry_ itself is scoped to the
namespace mechanism and does not document the seam — the seam was landed under D-342's
Increment A work without its own decision entry, so it has no separate D-number to cite.
This entry records the citation error so a future reader searching for "D-339" in this
context does not conclude one was silently dropped.

**`IStandardStreams` gains `In`.** A third member, `TextReader In { get; }`, is added
alongside `Out`/`Error` — the source `input()` reads from. `TwoWriterStreams`
(`Grob.Cli`) takes a third constructor parameter rather than being renamed (the
composition-root type predates the three-stream shape; renaming was judged not worth the
diff). `SingleWriterStreams` (`Grob.Vm`) answers `In` with `TextReader.Null`, mirroring
its existing `Error => TextWriter.Null` "nothing wired yet" convention for the ~39
legacy `new VirtualMachine(writer)` call sites — `TextReader.Null.ReadLine()` returns
`null` immediately, which is exactly the closed-stream behaviour `input()` must
translate into a catchable `IoError`.

**`input()` is a new dispatch category: the no-namespace native.** Every prior stdlib
callable was namespace-qualified (`math.sqrt`, `env.get`) and validated through
`NamespaceRegistry`. `input()` has no namespace — it is a `BuiltinDecl` sentinel
(pre-registered alongside `print`/`exit` since Sprint 2, but previously fully permissive,
with no real type-checking behind it). `TypeChecker.Expressions.cs VisitCall` gains a
dedicated arm, keyed on `Declaration: BuiltinDecl { BuiltinName: "input" }`, checked
_before_ the general "built-ins stay permissive" fallback: 0 or 1 arguments (E0003
outside that range), and when supplied, the one argument must be or widen to `string`
(E0004) — the same two codes namespaced-native call checking already uses, reused rather
than duplicated for a single call site. `print`/`exit` are untouched and stay fully
permissive — both are void and stay on their own dedicated opcodes, never reaching this
machinery. The runtime native (`Grob.Stdlib.IoPlugin`, previously an empty
print/exit-formalisation placeholder) is registered under the bare name `"input"` with
arity 1 — `IPluginRegistrar.RegisterNative`'s own doc comment already anticipated "a
bare name for a top-level built-in". Since the native's real arity is always 1 but a
script may write `input()` with zero arguments, the compiler (`Compiler.Expressions.cs
VisitCall`) gains a matching one-off arm: a 0-argument `input()` call has the missing
prompt filled with the constant `""` at the call site, before the ordinary
`GetGlobal`-then-`Call` shape. This mirrors the _shape_ of `exit()`'s existing 0-or-1-arg
default-fill (`Compiler.Statements.cs`) but cannot reuse that code path directly — `exit`
is void and only ever compiled in statement position, `input()` returns a value and must
be handled in the general expression path. Deliberately a one-off arm, not a general
defaulted-native mechanism: `input()` is the only v1 case that needs one, and building a
generic mechanism ahead of a second real consumer would be forward scaffolding.

**`env` and `log` are ordinary `NamespaceRegistry` consumers — no new decision needed
for their shape.** `env.get`/`require`/`has`/`set`/`all` and `log.debug`/`info`/
`warning`/`error`/`setLevel` follow the D-342 native-registration pattern `math`/`path`
already established; `env.require`'s missing-variable fault reuses `ErrorCatalog.E5801`
(`LookupError`) with D-284's pinned message template (`"Required environment variable
'<n>' is not set"`), and `input()`'s closed-stdin fault reuses the residual
`ErrorCatalog.E5305` (`IoError`) — no more specific existing code covers "stdin closed",
and the residual code's normally-high bar is accepted here as the pragmatic v1 choice
rather than allocating a new leaf for a single call site. Both are ordinary consumers of
the native-throw seam Increment A landed (`NativeFaultException`, above); nothing new is
decided by their presence.

**`log.setLevel`'s string-level design.** The four levels (`Debug < Info < Warning <
Error`) are a plain internal `LogLevel` enum with no Grob-visible representation — a
script only ever sees them as the four lowercase strings `log.setLevel` recognises,
which are exactly the corresponding function names (`"debug"`, `"info"`, `"warning"`,
`"error"`). An unrecognised string is a **silent no-op** — not a thrown fault, not a new
error code. The language spec is silent on invalid levels; a no-op was chosen
deliberately so a typo in a diagnostic-logging call (itself typically defensive,
best-effort code) cannot crash an otherwise-working script. This is the same shape as
`math.pow`'s domain-safe arms (`0 ** -1` returns `Infinity` rather than throwing) —
some natives fail loud, some fail soft, and the choice is made per-native rather than by
a blanket policy.

**`ValueDisplay`/registered-`toString()`-through-`log` is deferred to Increment D, not
built here.** `log.debug`/`info`/`warning`/`error` each take a plain `string` — the
caller already produces that string via ordinary Grob string interpolation, which
already routes through `ValueDisplay` (D-336, landed Sprint 7/8A) with no further wiring
needed. No `IValueToStringRegistry` seam is added to `LogPlugin` or `VirtualMachine` in
this increment. Reason: no real `Struct`-kind type has a production-registered
`toString()` yet in v1 — `guid` (Increment D) is the first real consumer of that
registry. Building the injection seam now, ahead of any real consumer, would be pure
scaffolding that ships untested (the `forward-scaffolding-yagni` discipline this project
already applies elsewhere). No VM test asserts registered-`toString()`-through-`log`
rendering in this increment; that test arrives with `guid`.

**`--verbose`.** A new CLI presence flag, recognised anywhere in argv for both `grob run`
and `grob repl`, stripped before the existing positional dispatch so neither command's
own argument parsing needs to know about it. Selects `LogPlugin`'s initial threshold —
`LogLevel.Debug` under `--verbose`, `LogLevel.Info` otherwise — via a new `bool verbose`
parameter threaded through `RunCommand`/`ReplCommand`/`PluginRegistration.RegisterAll`
(all three keep the parameter optional/defaulted where doing so avoided touching
existing call sites, consistent with D-343's own call-site-preservation choices for
`SingleWriterStreams` and the single-`TextWriter` `VirtualMachine` constructor).

No new error code; both `env.require` and `input()` reuse existing leaves and codes.
Count unchanged at 117.

---

### D-345 — `formatAs` lands: bespoke namespace resolution, compile-time column derivation, the chained-form rewrite as shared resolution not AST substitution (July 2026)

Area: Compiler / Runtime — `formatAs` module (Sprint 8 Increment E)
Supersedes: none
Superseded by: none
Refines: D-342, D-282, D-320, D-336

**Correction to the record first.** This increment's kickoff prompt asked to fold
`formatAs`'s namespace-misuse errors into "D-339's error code." D-339 does not exist —
D-338 is immediately followed by D-341, exactly the citation gap D-344 already recorded
for "D-339"/"D-340" in Increment C's kickoff. The governing decision for module-namespace
resolution, and the two error codes it allocated, is **D-342**: E1003 ("undefined
module", unknown member) and E1004 ("namespace used as a value", bare access). Both take
a per-call-site message string, so folding `formatAs`'s two exact-wording diagnostics
into them is a direct fit.

**`formatAs` is registered as a namespace name only.** `NamespaceRegistry`'s `"formatAs"`
entry has an empty member dictionary — enough for `RegisterNamespaces` to seed the usual
`NamespaceDecl` sentinel (so a bare `x := formatAs` correctly falls through to the
existing generic E1004 arm) and for the plain function form (`formatAs.table(items)`) to
resolve its receiver via the existing `TryAnnotateNamespaceReceiver` path. Its three
members are deliberately **not** modelled as `ConstantMember`/`NativeMember` entries: they
need bespoke per-call-site work (element-type derivation, a synthesised compiler-injected
argument) the generic positional `NativeMember` model was never meant to stretch to.
`ResolveNamespaceMemberAccess`/`ResolveNamespaceMemberCall` (`TypeChecker.Expressions.cs`)
each gain an early `namespaceName == "formatAs"` branch into `ResolveFormatAsCall`.

**The chained-form rewrite is shared resolution, not a literal AST substitution.**
`CallExpr`/`MemberAccessExpr` are `sealed record`s with `init`-only `Callee`/`Target` — a
"rewrite" cannot replace the node in place the way a mutable-property annotation
(`MemberAccessExpr.ResolvedFieldType`) can. Instead, `TryDetectFormatAsChainReceiver`
pattern-matches the fixed AST shape `CallExpr(Callee: MemberAccessExpr(Member:
methodName, Target: MemberAccessExpr(Member: "formatAs", Target: receiverExpr)))` —
sound because `formatAs` is reserved (D-282/D-320), so no struct field, namespace member
or declared binding can ever be named `formatAs`; any `MemberAccessExpr` with that member
name is unambiguously this mechanism. The function form (`formatAs.table(items)`) and the
chained form (`items.formatAs.table()`) both resolve through one core,
`ResolveFormatAsCall(node, methodName, methodNameRange, receiverExpr, extraArgs)` — for
the function form `receiverExpr` is the call's own first argument (`null` when the call
supplies none, reported as E0003); for the chained form it is the chain's inner receiver,
always present. The **compiler** independently re-derives the same chain shape
(`Compiler.Expressions.cs`'s own `TryDetectFormatAsChainReceiver`, necessarily duplicated
— `Compiler` and `TypeChecker` are separate passes with no shared instance state, the
same reason the namespace-receiver check is already re-derived rather than threaded
across) — mirroring the established `math.pi`/`node.Target.Declaration is NamespaceDecl`
precedent rather than inventing a new one.

**Namespace-misuse diagnostics.** Bare `<expr>.formatAs` (a `MemberAccessExpr` with
`Member == "formatAs"` reached by ordinary `VisitMemberAccess`, meaning no valid method
chain consumed it) is **E1004** with the spec's exact text. `<expr>.formatAs.X(...)` where
`X` is not `table`/`list`/`csv` is **E1003**, naming the three valid methods. Both fold
into D-342's codes with `formatAs`-specific message text — no new code.

**Compile-time column derivation — a bounded, `formatAs`-scoped peek, not a general
array-generics system.** Tracing the existing machinery found a real gap the kickoff's
"bounded work over settled machinery" framing understated: `GrobType.Array` carries no
element-type tag at all, and array higher-order methods (`.select`/`.filter`/`.sort`)
return bare `GrobType.Array` — v1 never tracked what is inside an array. Scalar
struct-typed symbols do carry a name (`Symbol.NamedStructTypeName`), but
`ResolveSignatureType`'s `ArrayTypeRef` arm discarded it even for a plain `items:
SomeStruct[]` parameter. Rather than generalising array element-type tracking (a much
larger project), the fix is contained to `formatAs`'s own resolution code:

- One new `Symbol.ArrayElementStructTypeName` field (mirrors `NamedStructTypeName`
  exactly), populated only for a `T[]`-annotated **parameter** via a new
  `TypeChecker.TryGetNamedStructTypeName(TypeRef)` helper (the name-only counterpart of
  `ResolveSignatureType`'s guid/`TypeDecl` arms, kept separate rather than threaded
  through that method's widely-consumed return tuple). A `:=`-inferred array local needs
  no such field — its shape is peeked from the declaration's annotation or initialiser
  directly, the same pattern `GetStructTypeNameFromDecl` already uses for scalars.
- `GetArrayElementFieldNames(Expression)` peeks the argument expression's own shape: an
  array literal (first element's shape), a `.select(lambda)` result (the lambda body's
  returned expression, reusing `GetStructFieldNames` directly against that node — the
  anonymous-struct literal's own field list, `AnonStructExpr.Fields`, is already in source
  order regardless of whether the lambda parameter's type is known, since v1 lambda
  parameters type as `Unknown`), a `.filter(...)`/`.sort(...)` result (pass-through,
  recursing into the receiver, since neither changes element shape), an indexed array
  element (`items[0]`, peeking the array), or an identifier (the new symbol field, or the
  declaration fallback). Anything else is statically indeterminate and reuses E0004 — a
  clear compile error, not a silent guess.
- `columns: [...]` (a literal array, `table` only) selects/reorders a subset of the
  derived full list; a named field not in that list also reuses E0004.
- No new error code anywhere in this derivation: E0004 (receiver-shape and
  columns-selection mismatches) and E0011 (a stray extra argument, reusing "no argument
  named" — a close semantic parallel to its existing "no parameter named" fit) are both
  pre-existing. Confirmed via `allocating-an-error-code`.

**Compiler emission pushes auto-derivation to compile time, keeping the runtime native
trivial.** Whatever the checker derives (explicit `columns:` or the full auto-derived
list) is stored on a new mutable `CallExpr.ResolvedFormatAsColumns` property (the
established "checker annotates, compiler reads back" pattern, not a dictionary — `Compiler`
and `TypeChecker` are different classes with no shared reference). The compiler always
emits it as a literal compile-time string-array constant (`OpCode.NewArray` — no new
opcode) passed as a synthesised **second** argument alongside the receiver, so
`Grob.Stdlib.FormatAsPlugin`'s three registered natives have simple fixed arities —
`table(items, columns)`, `list(item, fields)`, `csv(items, columns)` — regardless of
which source overload or call form produced the call. No runtime reflection over the
value, matching the stdlib reference's explicit requirement.

**Cell rendering needed a new capability, `IPluginRegistrar.RenderValue`.** `formatAs`
lives in `Grob.Stdlib`, which cannot reference `Grob.Vm` (the DAG forbids it) and so
cannot construct its own registry-backed `ValueDisplay` — a bare `NullRegistry`-backed
instance would miss a plugin type's registered `toString()` (`guid`, D-336). `RenderValue`
is one new `IPluginRegistrar` method, implemented by `VirtualMachine` as
`_valueDisplay.Display(value)`; `FormatAsPlugin.Register` captures `registrar.RenderValue`
as a delegate, which correctly reflects any later plugin's `RegisterToString` call
regardless of registration order (a method-group capture, not a snapshot). Floats stay
culture-pinned end to end because the pin lives inside `ValueDisplay` itself (D-336), not
in `formatAs`.

**A real regression caught and fixed in the same change.** `formatAs` is the first
reserved identifier (D-320) that is also a pre-registered `NamespaceDecl` symbol (`select`
is reserved but not a namespace, so this combination never arose before). Both
`FinalizeTopLevelBinding` (`TypeChecker.cs`) and the local `VisitVarDecl`
(`TypeChecker.Statements.cs`) independently check for a same-scope collision after the
reserved-identifier check runs, so `formatAs := 1` reported **both** E1103 and E1102 —
caught by the pre-existing `SelectReservedIdentifierTests.FormatAsAsLocalBinding_EmitsE1103`
regression test, which the namespace registration broke. Both call sites now skip the
collision check when the name is reserved (`_reservedIdentifiers.Contains(name)`) — the
reserved-name diagnostic alone already fully explains the error.

**Unrelated pre-existing gap found, not fixed.** Array indexing (`arr[i]`) has no
compiler emission at all — `Compiler.Expressions.cs` has no `VisitIndex` override, so any
script indexing an array (with or without `formatAs`) crashes the VM with a stack
underflow. Confirmed unrelated to this increment's changes. Out of scope here; the
`formatAs`-on-an-indexed-element derivation path (`GetStructFieldNames`'s `IndexExpr` arm)
is still implemented and tested at the type-checker level only, since the VM path it
would otherwise exercise is broken independently of `formatAs`.

No new error code; count unchanged at 117.

Full detail: `grob-stdlib-reference.md`'s `formatAs` section, `grob-decisions-log.md`
D-342 (the namespace-resolution precedent this refines), D-336 (`ValueDisplay`).

---

### D-346 — Sprint 8 close: stability-test calibration, and the "thirteen" validation-script count corrected to eleven (July 2026)

Area: Tooling — benchmarking
Supersedes: none
Superseded by: none
Refines: D-302

**The calibration ritual (§6/§11).** Sprint 8 Increment F runs the calibration against
the **Sprint-8-runnable script set**, not "all thirteen" as `grob-benchmarking-strategy.md`
§6 step 1 currently reads. Checking each of `grob-sample-scripts.md`'s eleven scripts
against the live Sprint 8 module surface (`math`, `strings`, `path`, `env`, `log`,
`formatAs`, `guid` — D-299): every one depends on at least one Sprint-9 module (`fs`,
`date`, `csv`, `json`, `process`) or an as-yet-unbuilt plugin (`Grob.Http`,
`Grob.Crypto`). None is runnable at Sprint 8 close. The calibration and the stability
test therefore run against exactly six scripts: the five sprint-close smoke scripts
(`hello`, `calculator`, `functions`, `types`, `errors`) plus the new `stdlib.grob`
(D-337 family). The full validation-suite stability run becomes a v1 release-gate step,
not a Sprint 8 deliverable — a mechanical correction is applied to
`grob-benchmarking-strategy.md` §6 citing this decision.

**Surfaced, not silently swept: the script count itself is stale.**
`grob-benchmarking-strategy.md` (§4.1, §6, §7.3, §11), D-309, D-313, D-337, and
`grob-v1-requirements.md`'s Sprint 8/9 and Benchmarking sections all say "the thirteen
validation suite scripts." `grob-sample-scripts.md` has never contained more than
**eleven** (`## Script 1` through `## Script 11`, confirmed via `git log -p --follow` —
no prior revision in this repository ever had a twelfth or thirteenth). D-283 (April
2026, predating D-302) already refers to "the eleven sample scripts" / "the eleven-script
release gate" — so the count was eleven before "thirteen" started propagating through
the benchmarking decisions that followed. This entry logs the drift as a known,
surfaced gap (the same discipline D-333 used for `compile.origin.json`'s stale-CPU
capture) rather than quietly rewriting every citing document in this pass, which is
outside a sprint-close increment that "writes no module code."

**Calibration numbers.** An initial ten-sample pass immediately following a
ten-iteration warmup showed heap flat at ~113,200 B with zero measured variance — too
short a horizon. A longer checkpoint sweep (iterations 10/50/100/250/500/1000/2000/
5000/10000) found a **one-time step** between iteration 1000 and 2000 (113,200 B →
125,520 B), then bit-identical heap from iteration 2000 through iteration 10000 — a
bounded cache/registry warm-up cost (consistent with one-time JIT tiering or a
dictionary settling to its steady size), not unbounded per-iteration retention. A first
attempt at `warmup: 100` (the §6 placeholder) mistook this step for ongoing growth and
failed a naive 5% tolerance (179,480 B → 190,456 B, +6.1%) purely because the warmup
snapshot was taken before the step. Locked: **iterations: 10,000, warmup: 2,000**
(comfortably past the observed step), **tolerancePercent: 2.0** (tight, justified by the
exact-zero drift observed once past the step — a looser 10% would have hidden the
warmup-placement bug rather than caught it). First passing run: post-warmup heap
190,456 B, final heap 190,456 B (0.0% drift). `calibrated`/`lastRun`: 2026-07-14.
Locked numbers and the first passing result are in
`bench/Grob.Benchmarks/baseline/stability.json`.

**Implementation note — a documented dependency-list deviation.** The stability loop
(`bench/Grob.Benchmarks/Stability/StabilityRunner.cs`) drives each iteration through
`Grob.Cli.RunCommand` — already public, and already constructing a fresh
`VirtualMachine` with fresh plugin registration per call — rather than re-implementing
`SystemEnvironment`/`SystemRandomSource`/`SystemClock` a second time inside
`Grob.Benchmarks` to satisfy §3's literal "Core, Compiler, Vm, Stdlib, Runtime" assembly
list, which predates `Grob.Cli`/`RunCommand` existing as the composition root.
`Grob.Benchmarks.csproj` now also references `Grob.Cli`. `Fixtures/Stability/` holds
frozen copies of the six scripts, extending §7.3's `Fixtures/EndToEnd` decoupling
rationale to this one additional subfolder.

No new error code; count unchanged at 117.

Full detail: `grob-benchmarking-strategy.md` §6 (mechanically corrected by this
decision), §11; `grob-v1-requirements.md`'s Sprint 8 acceptance text.

---

### D-347 — Compile-time baseline recapture: Sprint 8 namespace registration's fixed per-compile cost folded into the rolling baseline (July 2026)

Area: Tooling — benchmarking
Supersedes: none
Superseded by: none
Refines: D-341, D-342

**The decision.** The rolling `compile.json` baseline's time axis is recaptured against
the post-Sprint-8 floor. The `-report-full.json` from `benchmark.yml` run
[29399169091](https://github.com/grob-lang/grob/actions/runs/29399169091)
(`windows-latest`, host CPU `AMD EPYC 7763`, `main` @ `9419037`, 2026-07-15) replaces
`bench/Grob.Benchmarks/baseline/compile.json` wholesale, per D-309's canonical-workflow
production requirement — no local run is ever committed as a baseline. Run locally
against the recaptured baseline, `tooling/Grob.BenchCheck` now reads `0.0%` delta on
both compile benchmarks (time and allocation) and the gate passes.

**Root cause, not noise.** This run's gate failed with `Compile_TwoExpressions` reading
+5.8% against the rolling baseline — a genuine same-CPU comparison (both fresh and
rolling ran on `AMD EPYC 7763`), unrelated to the `vm` category's CPU-mismatch
downgrade shown in the same run (fresh `AMD EPYC 7763` vs the `vm` rolling baseline's
`Intel Xeon Platinum 8370C`). The cause is D-342: `TypeChecker.Check` runs
`RegisterNamespaces` unconditionally on every compile, seeding the global scope with
all seven core-module namespace names (`math`, `path`, `strings`, `env`, `log`, `guid`,
`formatAs`) before Pass 1, so that a colliding user declaration is caught as a proper
shadowing diagnostic rather than silently winning. This is fixed cost every compile now
pays regardless of source size, so it lands hardest in relative terms on the smallest
benchmark (`Compile_TwoExpressions`, two statements); `Compile_TenPrints` carries the
same fixed cost but dilutes it under more source, reading +3.9% — under the 5%
per-sprint threshold.

**Half of this exact regression was already fixed, and stayed fixed.** `NamespaceRegistry`'s
own comments record an earlier run (29392344084) breaching both the time and allocation
axes for `Compile_TwoExpressions`; the Sprint 8 QA pass (PR #137) fixed the allocation
half by statically caching the per-namespace `NamespaceDecl`/`Symbol` objects so
`RegisterNamespaces` shares them across every compile instead of re-synthesising seven
of each per call — mirroring `RegisterExceptionHierarchy`'s D-338 caching shape. That fix
holds: this run's allocation delta is +2.8%, comfortably inside the 10% ceiling. What
remains is the residual, irreducible per-compile cost of populating the global-scope
dictionary with those seven cached entries — `RegisterNamespaces` is already a single
`foreach` over pre-cached, pre-sized structures, and shrinking it further would mean
weakening the unconditional-registration guarantee D-342's shadow-detection semantics
depend on. Not pursued.

**Why recapture rather than optimise further.** This mirrors D-338→D-341: a deliberate,
already-shipped feature (D-342's namespace resolution) raises the compile-time floor by
a small, fixed, unavoidable amount; the correct response is to accept the new floor and
recapture the baseline in a distinct, logged act, not to chase diminishing-return
micro-optimisation on a synthetic two-statement benchmark. The rolling `compile.json`
baseline being recaptured here predates all of Sprint 8's stdlib/namespace/plugin work
(it was last touched by D-341, before PR #127 landed) — so it was structurally
guaranteed to red the first time a `benchmark.yml` run measured post-Sprint-8 `main`,
exactly as D-341's own recapture was the expected consequence of D-338's deferred fix,
not a new defect.

**`compile.origin.json` deliberately untouched**, for the same reason D-341 left it
untouched: the fresh run's host, `AMD EPYC 7763`, still does not match the frozen origin
baseline's `Intel Xeon Platinum 8370C`, so per D-333's CPU-identity guard the cumulative
time comparison (now +55.6%/+38.0%) remains informational only, not a gate breach.
Re-freezing `compile.origin.json` is left for a separate, maintainer-judged event.

**Scope.** `vm.json` and `endToEnd.json` are untouched — `vm` stays non-gating/
informational (D-313) and was not part of the flagged regression; `endToEnd` still has
no fresh benchmarks to compare.

No new error code; count unchanged at 117.

---

### D-348 — Array-indexer read emission: the missing `VisitIndex` override, `arr[i]` and `matrix[r][c]` now compile (July 2026)

Area: Compiler — expression emission
Supersedes: none
Superseded by: none
Refines: D-345

**The decision.** Sprint 9 Increment A adds `VisitIndex` to `Compiler.Expressions.cs` —
the missing override D-345 surfaced but explicitly left out of scope. Before this entry
`arr[i]` had **no compiler emission at all**: `AstVisitor<T>.VisitIndex`'s default
implementation falls through to `Compiler`'s `DefaultVisit`, which returns `null` and
emits nothing, so any script indexing an array crashed the VM with a stack underflow
regardless of whether the index was in bounds. The fix is a single two-line visitor
body: emit the receiver (`node.Target`), emit the index expression (`node.Index`), then
the existing `OpCode.GetIndex` — the same value-stack discipline every other expression
visitor follows, over an opcode that already existed and needed no change.

**Nothing new at the VM layer.** `GetIndex`'s handler in `VirtualMachine.cs` already
implemented both arms — an array receiver bounds-checks and raises `IndexError` via the
existing `E5101` (D-284) through the D-334 handler table (`TryRaiseRuntimeGrobError`,
catchable by `try`/`catch`, running any enclosing `finally` exactly once, and producing
the quality top-level diagnostic plus exit 1 when unhandled); a map receiver returns
nil-on-miss. That handler predates this fix and was reachable only via `for...in`
lowering (`Compiler.ControlFlow.cs`, which emits `GetIndex` directly as part of its
`while`-lowering), never via a direct index expression in user source — so this entry's
`VisitIndex` is the first thing that makes a real `.grob` script's `arr[i]` reach it at
all. One emission shape covers both the array and the map arm (the VM dispatches on the
runtime `GrobValue.Kind`, not anything the compiler decides), and covers chained
indexing (`matrix[r][c]`, D-112's multi-dimensional syntax) for free: the parser already
nests `IndexExpr(IndexExpr(matrix, r), c)`, so visiting the outer node's `Target`
recursively re-enters `VisitIndex` for the inner one, emitting two `GetIndex`
instructions in the correct order. No `OpCode` enum change, no `GrobValueKind` change,
no parser or AST change.

**Index-write remains deferred, confirmed rather than assumed.** The increment's verify
step checked whether `arr[i] = v` (array element **write**) also lacks emission — it
does: `Compiler.Statements.cs`'s `VisitAssignment` has an explicit early-return with the
comment "Index targets are deferred (collections sprint)." This is left unchanged and
deferred to a separate increment rather than folded in here, since Sprint 9's read-only
indexer consumers (`json.Node["key"]`, Increment D; `csv`'s `row[name]`/`row[index]`,
Increment E) need only the read path this entry lands.

**§3.1.1 does not apply to `IndexExpr`.** The increment's own test list asked to assert
a non-null `ResolvedType`/`Declaration` on "the index node" — checked directly against
`grob-v1-requirements.md` §3.1.1, the invariant is scoped to **identifier nodes** only
(`IdentifierExpr`, plus `StructConstructionExpr` and `SwitchExprNode`, which separately
carry the same two properties for their own reasons). `IndexExpr` has neither property
and this entry does not add them — no decision extends the invariant there. The real
invariant is unaffected: an identifier used as an index target (`arr` in `arr[i]`)
already gets `ResolvedType`/`Declaration` set correctly via the pre-existing
`VisitIdentifier` path that `TypeChecker.VisitIndex` calls on `node.Target`.

No new error code (E5101/E5102 both pre-existing, D-284); count unchanged at 118.

---

### D-349 — Reconciliation: Sprint 8 Increment D's guid `E0601` landing was never logged, running count corrected to 118 (July 2026)

Area: Process — decisions-log reconciliation (append-only)
Supersedes: none
Superseded by: none
Refines: D-149, D-345, D-346

**The gap.** Sprint 8 Increment D landed the `guid` module's `E0601` (invalid `guid`
string literal — the first entry in the previously empty E06xx sub-block of the Type
category, source decision D-149), taking the true error-code count from 117 to 118.
`grob-error-codes.md`'s three internal locations (summary index row, full entry, footer
changelog note) were updated correctly at the time and the footer already states the
true total of 118. But the landing was never captured as a decisions-log entry — no
`D-###` records it — so **D-345** and **D-346**, both logged after `E0601` shipped,
still read "count unchanged at 117", now stale against the registry's own footer.

**The fix — append-only, not a correction of D-345/D-346.** Per the log's append-only
discipline (the same discipline D-341 followed when it backfilled D-338's missing
changelog line), D-345 and D-346's text is left exactly as written; this entry is the
missing landing record, and the pointer future readers should follow to reconcile the
log's own narrative count against `grob-error-codes.md`'s live total. As of this entry
the decisions log's running count reads **118**, matching the registry — no code is
added by this entry itself; `E0601` was already live in `ErrorCatalog` and counted by
`Grob.Consistency.Tests`' `ErrorCodeCountTests` before this entry existed. This mirrors
D-316's original purpose (a mechanical drift gate on the registry) being satisfied all
along by the code; only the free-text decisions-log narrative had drifted.

No new error code; count unchanged at 118.

---

### D-350 — Array/map index-store emission: `arr[i] = v` wires the existing, previously-unemitted `OpCode.SetIndex` (July 2026)

Area: Compiler — statement emission
Supersedes: none
Superseded by: none
Refines: D-348

**The decision.** Sprint 9 Increment A2 lands the write companion to D-348's read-side
`VisitIndex`: `Compiler.Statements.cs`'s `VisitAssignment` had an explicit early-return
— `// Index targets are deferred (collections sprint)` — that made `arr[i] = v` compile
to **nothing at all** (the right-hand side was never even visited, so side effects in it
were silently dropped). `TypeChecker.Statements.cs` carried the mirror gap: it visited
the target and value for diagnostic purposes but performed no readonly check and no type
check. Both are replaced with a dedicated `IndexExpr`-target branch.

**No new opcode — `OpCode.SetIndex` already existed, unwired.** It was already present
in the closed `OpCode` enum and already recognised by `Disassembler.cs` as a no-operand
instruction; it simply had no VM dispatch case and no compiler emitter. This entry wires
both, mirroring `GetIndex`'s existing shape exactly: pop value, then index, then
receiver (the reverse of the push order — receiver, index, value — that the compiler
emits, itself mirroring how `SetProperty` pops value-then-receiver). An array receiver
bounds-checks before writing (`GrobArray`'s indexer setter throws a bare CLR exception
out of range, so the VM must check first, exactly as `GetIndex` already does) and raises
`IndexError` via the existing `E5101`/D-334 handler table on a miss; a map receiver
upserts via the existing `GrobMap.Set` (no bounds error, mirroring `GetIndex`'s
nil-on-miss read permissiveness); a nil receiver raises `E5201`. One emission shape
covers both array and map writes and chained targets (`matrix[r][c] = v`) for free: the
compiler's new branch emits the chain's `Target` as an ordinary (read) expression —
re-entering `VisitIndex` for any nested `IndexExpr` exactly as the read side already
does for `matrix[r][c]` — then the index, then the value, then `SetIndex`.

**`readonly` rejection generalises the existing member-access root-walk.** The
type checker's `FindReadonlyRoot`, previously typed to accept only a `MemberAccessExpr`
and walk only `MemberAccessExpr.Target` chains, is generalised to accept any
`Expression` and walk through both `MemberAccessExpr.Target` and `IndexExpr.Target` —
one shared helper, no behaviour change for the pre-existing `point.x = v` call site,
and it now also correctly reaches a `readonly` array-of-structs mixed chain
(`p.items[0].field = v`), a latent gap this closes for free. `arr[0] = v` on a
`readonly`-bound array is `E0204` ("mutation of `readonly` value") — the correct
citation for this precedent is **D-291** ("`readonly` semantics", whose point 4 is
explicit deep immutability, `X["k"] = v` on `readonly map<...>` named directly), **not
D-289** as the increment's kickoff prompt cited. D-289 is "definition of compile-time
constant expression" (the `const` right-hand-side rules) — an unrelated decision;
corrected here rather than propagated.

**`const`-bound array/map mutation is not a reachable state — no code added for it.**
D-289 explicitly disallows array, map and struct literals as `const` right-hand sides
(only literals, arithmetic on constant operands, and references to other `const`
bindings qualify); any attempt already fails earlier at `E0205`. There is therefore no
valid Grob program with a `const`-bound array or map to index-assign into, so no
const-rejection branch is added — mirroring the pre-existing member-access check, which
likewise has only ever tested `ReadonlyDecl`, never `ConstDecl`.

**RHS element-type checking stays permissive — named as an open gap, not built ad
hoc.** `GrobType.Array` carries no scalar element type; `VisitIndex`'s own read-side
implementation already returns `GrobType.Unknown` unconditionally, with element-type
tracking noted there as awaiting generics. `Symbol.ArrayElementStructTypeName` (D-345)
tracks only struct-element arrays, and only for `formatAs`'s field-name derivation, not
general assignability. Building real scalar element-type tracking is materially larger,
unauthorised scope (generics-adjacent) for a store-emission increment, so `arr[0] = "x"`
on an `int[]`-annotated array is not flagged — permissive, consistent with how an
`Unknown`-typed identifier assignment target is already treated elsewhere in the same
file. This is recorded here as the honest scope boundary, not silently invented.

**Two adjacent, still-broken assignment-target gaps, named for scheduling.** The same
early-return shape silently drops emission for `arr[i] += v` (`VisitCompoundAssignment`)
and `arr[i]++`/`arr[i]--` (`VisitIncrement`) — confirmed, not assumed, by reading both
methods. Neither is fixed here (out of scope: this increment is the assignment-statement
target only); both are named so they are scheduled rather than left as an unowned code
comment.

No new opcode (`SetIndex` pre-existed in the enum), no new error code (`E5101`,
`E0204`, `E5201` all pre-existing); count unchanged at 118.

---

### D-351 — Array element-type tracking: `ArrayTypeDescriptor` mirrors `FunctionTypeDescriptor`, not `map<K, V>` (July 2026)

Area: Compiler — type representation
Supersedes: none
Superseded by: none
Refines: D-348, D-350

**The decision.** Sprint 9 Increment A3 gives `GrobType.Array`/`NullableArray` a real
element-type identity. `GrobType` stays the flat enum it always was — no field added to
the type itself — and a new `ArrayTypeDescriptor` (`Grob.Compiler`, mirroring
`FunctionTypeDescriptor`'s existing side-channel shape exactly: a flat element
`GrobType`, an optional named-type name for a struct/`guid` element, an optional nested
descriptor for a `T[][]` element) is carried alongside it on `Symbol.ArrayDescriptor`
(generalising the narrower, parameter-only, struct-name-only Sprint 8 Increment E field
of the same shape), on array-literal nodes (`_arrayLiteralDescriptors`), and on
array-returning call results (`_callResultArrayDescriptors`) — the same three-tier
pattern (`Symbol` field / literal-node dictionary / call-result dictionary) `Function`
already uses.

**Correction to the increment's own premise: `map<K, V>` is not a working precedent.**
The kickoff prompt (and D-112's original text) describes the checker as already
distinguishing `map<string, string>` from `map<string, int>`. Reading the code found
this false: `TypeRef.TypeArguments` is parsed but never read anywhere: `"map"` resolves
straight to the flat `GrobType.Map` tag in both `TypeChecker.cs`'s `ResolveTypeRef` and
`TypeChecker.Declarations.cs`'s `ResolveFieldAnnotationType`, and `for k, v in m` binds
`v` as `Unknown` — the identical total gap arrays had. There was no working
map-parameterisation machinery to mirror. The actual working precedent in this codebase
is the `Function`/`Struct` side-channel pattern (`FunctionTypeDescriptor`,
`Symbol.NamedStructTypeName`) — a compiler-side descriptor carried beside the flat tag,
never folded into `GrobType` itself, keeping `Grob.Core`'s enum a neutral DAG-respecting
tag. Maps keep their pre-existing gap; it is out of this decision's scope (arrays only),
named here rather than left unremarked now that the record is being corrected anyway.

**The four sites threaded, all reusing existing diagnostic codes.**
`TypeChecker.Expressions.cs`'s `VisitArrayLiteral` infers the element kind from the
elements (`[1, 2, 3]` → `int`), unifying pairwise via a dedicated `UnifyArrayElementType`
(int/float widening; a genuine mismatch — `[1, "a"]` — is `E0001`; deliberately not
`UnifyTernaryArms` reused directly, since that helper's message text and T/T? arm are
ternary/switch-shaped and a third caller would force an unwanted shared-wording
compromise for two call sites). `VisitIndex` resolves to the receiver's real element
type via the new `ArrayDescriptorOf` walker (which recurses through a chained
`IndexExpr` target for `matrix[r][c]`, D-112) instead of the unconditional `Unknown`
D-348 left; `VisitIndexAssignmentTarget` checks the RHS against that same element type
— **closing the A2 gap**: `arr[0] = "x"` on an `int[]` is now `E0001`. `for...in`'s
`ResolveIterationVariableTypes` binds the item variable to the real element type (and,
for a struct or nested-array element, threads the element's own named-type name or
array descriptor onto the loop variable's symbol, so member access or further indexing
inside the loop body resolves exactly as a `:=`-inferred local's would — this was not
merely "leave item as Unknown" before; it needed active threading to avoid silently
regressing existing struct-array iteration). Function-signature enforcement
(`ResolveSignatureType`, `CheckBoundArgumentType`) and struct-field construction
(`TypeCheckFieldValues`) and function-return checking (`ComputeReturnCompatibility`)
all gained a matching `ArrayElementAssignable` check — invariant element comparison
(no `int → float` widening at the element level: an array is a reference to shared
mutable storage, and a value read back under a widened static element type would
misrepresent its actual runtime `GrobValueKind`), reusing `E0004` (arguments) and
`E0005`/`E0001` (returns/bindings/fields) exactly as their existing flat-kind checks
already did. No new error code; count unchanged at 118.

**The struct-field twin (report item, feeds Increment D) is sound.** Struct fields are
already individually typed via `ResolvedFieldInfo.Kind` per field (Sprint 6 Increment
A) — `mapAs<Config>()`'s reliance on that invariant is safe. No companion gap found
there.

**No break risk materialised — zero quarantines.** Neither the existing test suite,
the `tests/fixtures/*.grob` corpus, nor `grob-sample-scripts.md`'s validation scripts
contained a heterogeneous array literal or a call to an array mutation method; every
array literal in the corpus was already homogeneous. All 1,291 `Grob.Compiler.Tests`
(19 new, in `ArrayElementTypeCheckerTests.cs`) plus the full cross-project suite (Core,
Runtime, Vm, Stdlib, Integration, Consistency — 2,482 tests total) pass unmodified
except two stale doc-comment corrections in `ArrayTypeRefCheckerTests.cs` and
`GrobType.cs` ("element type deferred to generics" was no longer true). Coverage on
`Grob.Compiler` after the change: 92.17%, above the 90% floor.

**A materially larger, separate gap surfaced and deliberately not built here: the array
mutation-method surface has no implementation at all.** Investigating "are mutation-method
arguments checked" (the prompt's own reach question) found `append`/`insert`/`remove`/
`clear`/`contains`/`first`/`last`/`length`/`isEmpty` are not in
`IsArrayHigherOrderMethod` and fall through `ResolveMemberAccessCall`'s generic branch to
bare `GrobType.Unknown` with **zero** checking — not even that the method exists, let
alone its argument types. The compiler emits nothing for any of them (zero matches for
`append`/`insert`/`remove`/`clear` anywhere in `Grob.Compiler`), and `GrobArray`
(`Grob.Core`) only exposes `Add`/an indexer — no `Insert`, `Remove`, `Clear`, or
`Contains` at all. D-140 documented this whole surface (`grob-type-registry.md`) but
only `filter`/`select`/`sort`/`each` were ever actually built. This is confirmed,
Chris-approved (this decision's own kickoff exchange) as its own follow-up increment —
building nine methods across the type checker, the compiler emitter and `GrobArray`
before any element-type enforcement on them is meaningful — not folded into A3 and not
the A4 compound-assignment follow-up either.

**A4 (compound assignment) is now unblocked**, as D-350 named it: `arr[i] += v` and
`arr[i]++`/`arr[i]--` need a well-typed element for their read-modify-write, which this
decision now provides.

No new opcode (type-system work only; the D-348/D-350 read/write emission is
unchanged), no new error code (`E0001`/`E0004`/`E0005` all pre-existing); count
unchanged at 118.

---

### D-352 — `Grob.Http` redirect and credential-forwarding policy (July 2026)

Area: Grob.Http — redirect and credential policy
Supersedes: none
Superseded by: none
Extends: D-155

**The gap.** `grob-stdlib-reference.md` and D-155 lock every `Grob.Http` signature —
`get`/`post`/`put`/`patch`/`delete`/`download`, the `auth.*` helpers, the `Response`
type — but say nothing about what happens when a response is a redirect. The .NET
`HttpClientHandler` default is `AllowAutoRedirect = true` with
`MaxAutomaticRedirections = 50`, and .NET has historically forwarded the
`Authorization` header across redirects to a different host under some
configurations. `auth.bearer(pat)` carries a live credential. An unpinned default
therefore lets a `302` from `http.get("https://api.trusted.com", auth.bearer(pat))`
walk that bearer token to an attacker-controlled origin — a credential-exfiltration
vector in the stdlib itself, not a robustness nit. The Azure metadata endpoint
(`169.254.169.254`) makes the worst case concrete: a redirect to it carrying a token
is real SSRF.

**The pinned policy.**

1. **Cross-origin drops the credential.** Redirects are followed by default, but if
   the redirect target's origin (scheme + host + port) differs from the request
   origin, the `AuthHeader` is **not** forwarded. Same-origin redirects preserve it.
   The dropped-credential case is silent-and-safe by design — the request proceeds
   without the header — not a fault.
2. **Protocol downgrade is refused.** An https→http redirect throws `NetworkError`.
   No silent downgrade to a sniffable channel.
3. **Hop cap.** The total redirect chain caps at 10 — tighter than .NET's 50,
   because a scripting default should be conservative. Exceeding the cap throws
   `NetworkError`.
4. **`download()` follows the identical policy.**

**Surfacing.** The cap and downgrade faults reuse the existing `NetworkError` leaf
(D-284) — no new error code, count unchanged at 118. The cross-origin credential
drop is not a fault and raises nothing.

**Why its own decision.** This is a language-design call with a security posture, not
a test detail — it changes observable behaviour and the credential-safety guarantee.
It must not ride silently in the adversarial-testing document (D-353). Implementation
lands in **Sprint 11** with `Grob.Http` — plugins are Sprint 11 in the build plan, not
Sprint 9 — so this policy is pinned now for the Sprint 11 implementation to build
against. Verification is Pillar 7 of the adversarial suite, which runs as a
post-Sprint-11 network-hardening pass (Pillar 7 cannot run at the Sprint 9/10 interlude
because `Grob.Http` does not yet exist) against both a local hostile Kestrel server (two
localhost ports for the cross-origin case) and the Azure tenant (real cross-host
redirects and a genuinely IMDS-reachable box).

Relates to D-155, D-159, D-284, D-336, D-353.

---

### D-353 — Adversarial testing strategy and Sprint 9/10 hardening interlude (July 2026)

Area: Process / Tooling — adversarial testing
Supersedes: none
Superseded by: none

**The decision.** Authorises `grob-adversarial-testing-strategy.md`. Grob's existing
test families are all cooperative — §12's unit and integration projects, the D-337
sprint-close smoke family and the D-346 eleven-script validation suite each verify
that Grob does what it should. None verifies that Grob *fails well* when someone does
what they should not. That is a distinct discipline, and it earns its own document,
its own harness and its own exit criteria rather than extensions to the cooperative
families.

**The contract.** No input — source text, bytecode, CLI arguments, environment, file
system state or child-process behaviour — may produce an unhandled .NET exception, a
host stack trace, a hang without a Ctrl+C response or an exit code outside the
documented set (`{0, 1, 2, 3}` for the runtime, `0..255` for a script-chosen
`exit(n)`). Every failure is a Grob diagnostic with an E-code on stderr. Any
violation is P0 regardless of how contrived the input.

**Seven pillars.** (1) Compiler fuzzing — byte/token mutation, deterministic
grammar-based generation, and coverage-guided SharpFuzz. (2) Hostile `.grobc` —
truncated headers, out-of-range constant indices, jump targets past chunk end,
lying arities. (3) Differential and metamorphic testing — formatter neutrality and
idempotence, declaration-reorder neutrality (stressing the D-166 two-pass checker),
`const`-inlining fidelity, FsCheck VM-vs-tree-evaluator divergence, and cross-model
spec adversaries whose three-way disagreement separates implementation bugs from
spec ambiguity. (4) Stdlib and environment brutality — Windows reserved device
names, long paths, pipe deadlock, ReDoS, and a credential-opacity gate mechanising
D-159/D-336. (5) Resource exhaustion and soak. (6) Cold-read usage campaigns. (7)
Hostile network surface — two-tier local-Kestrel-plus-Azure, verifying D-352.

**Harness.** `tooling/Grob.Torture`, a black-box CLI driver outside `tests/` (these
are campaigns, not gates), with an in-proc SharpFuzz mode for throughput. It asserts
the no-stack-trace and exit-code contracts, greps for a credential sentinel,
preserves P0 repros to `findings/` and deduplicates by diagnostic signature. A
stabilised subset graduates to `tests/Grob.Torture.Tests` as a per-commit gate,
which then falls under the D-335 solution-membership check.

**Timing — split to fit the build plan.** The build plan lands the remaining **core**
modules (`fs`, `date`, `json`, `csv`, `regex`, `process`) at Sprint 9, but the
first-party plugins (`Grob.Http`, `Grob.Crypto`, `Grob.Zip`) not until **Sprint 11**.
The adversarial work therefore runs in two passes rather than one:

- **Sprint 9/10 hardening interlude — Pillars 1–6.** At Sprint 9 close the whole
  language, VM and core-stdlib surface is complete, so the compiler fuzzing, hostile
  `.grobc`, differential/metamorphic and cross-model work, the full core-stdlib
  environment brutality (`fs`/`path`/`process`/`regex`/`json`/`csv` — the Windows
  nastiness, ReDoS, pipe deadlock, reserved device names), exhaustion, soak and
  cold-read campaigns all run here. Pillar 1 layers 1–2, the harness skeleton and the
  Sprint 9 module fixtures build during and immediately after Sprint 9; the CPU-heavy
  campaigns run in the interlude proper.
- **Post-Sprint-11 network-hardening pass — Pillar 7.** The hostile network surface,
  D-352's verification and the two-tier local-Kestrel-plus-Azure infrastructure wait
  until `Grob.Http` exists at Sprint 11. Pillar 7 cannot run at the 9/10 interlude
  because its subject is not yet built. D-352's policy is pinned now so Sprint 11
  builds to it; only the verification is deferred.

Each pass carries its own Definition of Done. Because the eleven-script validation
suite itself depends on the plugins (scripts 4/7/10/11/13 use `Grob.Http`/`Grob.Crypto`),
alpha is necessarily post-Sprint-11, and the alpha exit criteria span both passes.

**Decisions the interlude is expected to force.** Four, each logged as its own D-###
rather than folded silently into a fix: the `.grobc` load-time verifier vs
per-instruction bounds checks (Pillar 2); the parser recursion-depth guard and its
threshold, converting `StackOverflowException` into a diagnostic (Pillar 1); the
`regex` `matchTimeout` value (Pillar 4); and the `exit(n)` out-of-range clamp
behaviour (the contract).

**Alpha exit criteria.** Zero P0s across a calibrated coverage-guided fuzzing budget
(24 CPU-hours a floor); the full Pillar 4 matrix green; every registry error code
demonstrably reachable by a fixture; every P1 dispositioned with a fix or a D-###;
both cross-model spec adversaries run with all ambiguity findings dispositioned;
D-352 verified against real cross-host and IMDS endpoints; the credential-opacity
gate green on every path.

No new error code; count unchanged at 118.

Relates to D-155, D-284, D-298, D-300, D-302, D-333, D-335, D-337, D-346, D-352.

---

### D-354 — `date` arithmetic/comparison API amendment: no per-unit `minus*`, `toDateOnly`/`toTimeOnly` added, `LessDate`/`GreaterDate` authorised (July 2026)

Area: date module — API shape
Supersedes: D-108
Superseded by: none

**The decision.** Three amendments to D-108's locked `date` API, made during Sprint 9
Increment B's planning, ahead of `date`'s first implementation — nothing has shipped
against D-108 yet, so this corrects the API before it lands rather than breaking
shipped behaviour.

1. **No per-unit `minus*` methods.** D-108 gave `days` both `addDays()`/`minusDays()`
   but left `months`/`hours`/`minutes` with `add*()` only — an inconsistent surface.
   Rather than add `minusMonths()`/`minusHours()`/`minusMinutes()` for symmetry,
   `minusDays()` is dropped: `addDays()`/`addMonths()`/`addHours()`/`addMinutes()` all
   accept a negative `n` to subtract, uniformly. .NET's `DateTimeOffset.AddDays`/
   `AddMonths`/`AddHours`/`AddMinutes` already accept negative arguments with ordinary,
   well-defined semantics, so this needed no new logic — a smaller surface, one verb
   per unit and the shape a developer reaches for instinctively (a negative argument
   to an "add" method) rather than a bespoke `minus*` counterpart most host languages
   don't offer either.
2. **`toDateOnly()`/`toTimeOnly()` added.** Mirrors `date.today()`'s existing
   "zero the other part" convention (D-107) as instance methods on an arbitrary
   `date` value, not only the current moment. `to`-prefixed to match every other
   conversion method on the type (`toUtc`/`toLocal`/`toZone`/`toIso`/`toIsoDateTime`/
   `toUnixSeconds`/`toUnixMillis`). `toDateOnly()` zeroes hour/minute/second, keeping
   year/month/day and the offset. `toTimeOnly()` keeps hour/minute/second and anchors
   the date to the Unix epoch (1970-01-01) — the Gregorian calendar has no "zero day"
   to zero to, so an explicit, unambiguous anchor is chosen rather than leaving the
   date component undefined.
3. **`LessDate`/`GreaterDate` opcodes authorised.** `d1 < d2`/`d1 > d2`/`d1 <= d2`/
   `d1 >= d2` are added to `date`'s surface (alongside the existing `isBefore()`/
   `isAfter()` methods) — real, deliberate growth of the `OpCode` enum, the first
   since Sprint 2, via the `adding-an-opcode` procedure. `Less`/`Greater` are
   typed-opcode families (`LessInt`/`LessFloat`/`LessString`, `GreaterInt`/
   `GreaterFloat`/`GreaterString` — no struct variant), and the compiler's
   `ComparisonCategory` helper defaults any non-`Float`/non-`String` category to
   `Int`; letting a `date`-vs-`date` comparison reach that default unchanged would
   silently emit `LessInt`/`GreaterInt` against two `Struct` receivers (the "checker
   permits ≠ can emit" hazard, D-315). `LessDate`/`GreaterDate` are appended to the
   end of the `OpCode` enum's Comparison category (not inserted elsewhere — nothing
   has shipped a `.grobc` file depending on today's opcode numbers, so appending is a
   convention choice made to keep the category grouping intact for the next reader,
   not a compatibility requirement). `<=`/`>=` get no dedicated opcode, mirroring the
   pre-existing string lowering: `a <= b ≡ !(a > b)` via `GreaterDate` + `Not`;
   `a >= b ≡ !(a < b)` via `LessDate` + `Not`. The type checker's gate is narrow: a
   `Struct`-vs-`Struct` comparison is admitted only when both operands are nominally
   `date` (`GetStructTypeName(...) == "date"` on both sides) — any other struct
   pairing, or a struct against a scalar, still falls through to the pre-existing
   `E0002`. Justified independently of the amendment itself: this very log's D-353
   worked example (`last < cutoff`, comparing two `date` values) and two scripts in
   `grob-sample-scripts.md` already assumed this worked — it did not, until now.

**Not amended.** `date.parse(str, pattern)` — an explicit second `pattern` argument
shown in `grob-stdlib-reference.md`'s sample — is not part of this increment's
surface: `grob-v1-requirements.md`'s Sprint 9 Scope bullet lists only `parse()`, and
this entry does not expand it. Left for a future decision if the two-argument
overload is wanted; `NamespaceRegistry`'s flat one-entry-per-member-name model does
not currently support arity-overloaded members, a separate design question from this
entry's three amendments.

**Historical text left untouched.** D-108's own body and D-353's embedded worked
example (`cutoff := date.today().minusDays(staleDays)`) still read `minusDays` —
neither is rewritten, per this log's own convention that a superseded entry's prose is
never edited to pretend it always said something else. `grob-stdlib-reference.md`
and `grob-sample-scripts.md`, the living specs, are updated to `addDays(-n)`.

No new error code (`E5702` — already registered, D-284 — is reused for `date.parse`'s
runtime `ParseError`, unaffected by this entry). Count unchanged at 118.

Full detail: `grob-stdlib-reference.md`'s `date` section (updated to match),
`grob-v1-requirements.md`'s Sprint 9 Scope bullet (updated to match), D-107/D-108/
D-117/D-176 (the entries this amends), the `adding-an-opcode` skill (the procedure
followed for `LessDate`/`GreaterDate`).

---

### D-355 — `date` core module lands (Sprint 9 Increment B) (July 2026)

Area: Runtime / Compiler / Stdlib / Vm — date module
Supersedes: none
Superseded by: none
Refines: D-107, D-108, D-117, D-176, D-284, D-336, D-342, D-343, D-354

**The decision.** `date` lands as Sprint 9's first type-carrying module, following the
`guid` precedent (D-149, Sprint 8 Increment D) at every layer: a `Struct`-discriminated
`GrobValue` (D-303 — no new `GrobValueKind` variant), one hidden field (`__value`)
holding a round-trip-formatted `DateTimeOffset` string (mirrors guid's canonical-string
field — the only representation `GrobStruct` permits, since it stores only named
`GrobValue` fields), a compile-time `NamespaceRegistry` entry for the seven static
constructors (`now`/`today`/`of`/`ofTime`/`parse`/`fromUnixSeconds`/`fromUnixMillis`,
all `NamedTypeName: "date"`), and type-checker/VM dispatch arms for the instance
property surface (`year`/`month`/`day`/`hour`/`minute`/`second`/`dayOfYear`/
`utcOffset`/`dayOfWeek`) and method surface (arithmetic, `isBefore`/`isAfter`,
`toIso`/`toIsoDateTime`/`format`, epoch conversions, `toUtc`/`toLocal`/`toZone`/
`toDateOnly`/`toTimeOnly`, `daysUntil`/`daysSince`) keyed off the struct's type name
exactly as guid's are.

**Where date diverges from guid's precedent.** Guid's three instance methods are all
zero-arity; date's are not (`addDays(n: int)`, `isBefore(other: date)`,
`format(pattern: string)`, ...), so `ValidateDateMethodCall`/`CheckDateMethodArgs`
(`TypeChecker.Expressions.cs`) validate real per-argument arity and type, including a
nominal `date`-vs-any-other-struct rejection for the `other: date` parameters
(`isBefore`/`isAfter`/`daysUntil`/`daysSince`) — the same nominal-identity reasoning
`IsStructNominalMismatch` already established for guid's namespace-parameter check.
`date` is also the first named type recognised in **annotation position** outside
`guid` itself: `ResolveSignatureType` (`TypeChecker.cs`), `ResolveNamedFieldType`
(`TypeChecker.Declarations.cs`) and `TryGetNamedStructTypeName` (`TypeChecker.cs`)
each needed a `"date"` arm alongside their existing `"guid"` one — a `d: date`
parameter or field annotation would otherwise resolve as `E1001` ("not a type"),
since date has no `TypeDecl`/`UserTypeInfo` registration (never constructed via
`{ }` braces) for the ordinary user-type lookup to find.

**`IClock` — a real second consumer.** `date.now()`/`date.today()` read the injected
`IClock` (declared D-343, `guid.newV7()` its first consumer) via `Grob.Cli`'s existing
`SystemClock`, converted to local time (D-176) — never `DateTime.Now`/
`DateTimeOffset.Now` directly (grep-verified absent from `Grob.Stdlib`).

**`ParseError` — reuses the reserved code.** `date.parse` throws through the
native-throw seam (`NativeFaultException`) exactly as `guid.parse` does, but reuses
`ErrorCatalog.E5702` — the "parse error (residual)" code D-284 explicitly reserved for
`int.parse`/`float.parse`/`date.parse` — rather than allocating a new one. Unlike
`guid.parse`, `date.parse` has **no** compile-time literal validation (no analogue of
guid's `E0601`): the requirements list no such rule for v1, so a malformed literal
argument is a runtime `ParseError` unconditionally, literal or not.

**`ValueDisplay` — registered `toString()`.** `DatePlugin` registers a canonical
ISO-8601-with-offset renderer (`yyyy-MM-ddTHH:mm:sszzz`, with an explicit zero-offset
special case rendering `Z`) so `print(d)`/`"${d}"` render the canonical string per
D-336's registered-`toString()` precedence (step 2, ahead of structural rendering),
never the hidden `__value` field. Discovered while implementing this entry:
`DateTimeOffset`'s `K` custom format specifier is **not** equivalent to `DateTime`'s
for this purpose — it always renders `+00:00`, never `Z`, regardless of offset — so
the zero-offset case is handled with an explicit format-string branch instead.

No new error code (`E5702` reused, unchanged); no new `GrobValueKind` variant. The two
new opcodes (`LessDate`/`GreaterDate`) are D-354's authorisation, not this entry's —
D-354 is the API-shape/opcode-authorisation decision, this entry is its landing
record, mirroring the D-348/D-349 (opcode landing) vs. D-345 (namespace-resolution
decision) split precedent.

Full detail: `grob-stdlib-reference.md`'s `date` section, `grob-v1-requirements.md`'s
Sprint 9 Scope bullet, `NamespaceRegistry.cs`'s `date` entry, `DateNatives.cs`/
`DatePlugin.cs`.

---

### D-356 — Named-type registration table: nominal-type dispatch becomes data, not per-type code (July 2026)

Area: Compiler / Type checker / Runtime — named-type dispatch
Supersedes: none
Superseded by: none
Refines: D-149, D-303, D-342, D-355

**The problem.** A nominal, `Struct`-discriminated type — `guid` and `date` today,
`File`/`json.Node`/`csv.Table`/`CsvRow`/`Regex`/`Match`/`ProcessResult` across Sprint 9's
remaining increments, `Response`/`AuthHeader`/`ZipEntry` in Sprint 11 — is currently
wired into the compiler and runtime by hand-added, string-matched arms across at least
six dispatch surfaces, each keyed off the type's name:

1. `ResolveSignatureType` (`TypeChecker.cs`) — the type name in parameter/return annotation position.
2. `ResolveNamedFieldType` (`TypeChecker.Declarations.cs`) — the type name in a struct-field annotation.
3. `TryGetNamedStructTypeName` (`TypeChecker.cs`) — nominal-name recovery.
4. The method/property validator (`ValidateDateMethodCall`/`CheckDateMethodArgs` for `date`; guid's equivalents) — per-member arity and type checking.
5. VM dispatch — the instance property/method arms keyed off the struct's type name.
6. `ValueDisplay` (D-336) — the registered `toString()` renderer.

D-355's own landing record demonstrates the failure mode: `date` needed a `"date"` arm
added alongside the existing `"guid"` one at each of the three annotation-position sites,
and it names the consequence of missing one — a `d: date` annotation would otherwise
resolve as `E1001` ("not a type"). This is O(types × sites) copy-paste. With seven new
types landing across four increments, one missed arm somewhere is close to certain, and
each miss is a wrong-code bug the adversarial suite (D-353) will independently rediscover.

**The decision.** A single declarative **named-type registry** — one hand-authored entry
per nominal type — that the annotation-position resolvers, the method-call validator, the
VM instance dispatch and `ValueDisplay` all consult in place of per-type string-matched
arms. Each entry carries: the canonical type name; the property table (name → type); the
method table (name → arity, parameter types including the nominal-identity rule for
`date`-typed or `guid`-typed parameters, return type, and optional/default parameters per
D-358); and the `toString()` renderer. This is the same architectural move
`NamespaceRegistry` (D-342) already made for **module members** — a compile-time twin of
the runtime `IGrobPlugin` registration, agreement-tested in the D-308/D-342 pattern with
drift a CI failure — extended from module namespaces to **instance surfaces** on nominal
types.

**Registry versus `NamespaceRegistry` — they compose, they don't replace.** A named type's
**static constructors** stay `NamespaceRegistry` entries: `date.now()`/`guid.parse()` are
namespace-receiver calls (`date`/`guid` in receiver position), which is D-342's existing
domain. The new registry governs the **instance** surface — properties and methods on a
*value* of the type — plus the annotation-position name recognition that the three
string-matched resolvers do today. A named type therefore has presence in both tables: a
`NamespaceRegistry` entry for its constructors, a named-type-registry entry for its
instance surface.

**Arrays and maps are out of scope — deliberately.** Arrays and maps are **structural**,
not nominal: they carry an `ArrayTypeDescriptor` (D-351) and a `MapTypeDescriptor` (F5-2,
scheduled), and their method surfaces (the C0a/C0b collection work) dispatch on the
descriptor, not on this registry. The registry is for `Struct`-discriminated nominal types
only. Stated explicitly so a future reader does not wrongly route the array/map method
surface through the named-type table — the two mechanisms are siblings, not one subsuming
the other.

**Proving cases first.** `guid` and `date` migrate onto the registry as its first two
entries, behaviour-preserving — every guid and date gold master unchanged, proving the
table reproduces the hand-rolled arms exactly before any new type depends on it. `File`
through `ProcessResult` then land as registry **data** (one entry each) rather than code
spread across the six sites.

**Scope boundaries.** The registry is hand-authored — it introduces no user-defined
nominal types with methods (v1 has no user-authored methods; user `type`s remain
field-only structs, D-043, and D-080's no-user-generics rule is untouched). Arity-overloaded
members remain unsupported by the flat one-entry-per-member-name model; the method table
carries optional/default parameters instead (D-358), which covers `date.parse`'s and
`fs.copy`'s needs without true overloading.

No new error code — annotation-position misses continue to surface `E1001`; method and
argument errors reuse `E0003`/`E0004`/`E0005`/`E0011` as today. Count unchanged at 118.

This is the single highest-leverage item in the Sprint 9 Increment B principal review: it
converts four increments of error-prone dispatch plumbing into table entries and shrinks
the adversarial suite's attack surface before Pillar 1's fuzzer and Pillar 3's cross-model
spec adversaries meet it. Sequenced as Increment C0c, before `fs` (Increment C) introduces
`File`, the first of the seven new nominal types.

Full detail: `grob-type-registry.md` (nominal-type instance surfaces), `grob-vm-architecture.md`
(instance dispatch), the `NamespaceRegistry` precedent (D-342), D-149/D-303/D-355 (the
guid/date arms this consolidates), D-308 (the agreement-test pattern).

---

### D-357 — `date` equality and ordering are instant-based (amends D-169 for nominal `date`) (July 2026)

Area: date module / Type system — equality and ordering
Supersedes: none
Superseded by: none
Refines: D-169 (nominal `date` carve-out), D-354, D-355

**The incoherence.** Three shipped decisions combine into a contradiction:

- **D-169** makes equality value-based — structs compared field-by-field.
- **D-355** makes `date` a `Struct` whose single field (`__value`) is a
  round-trip-formatted `DateTimeOffset` string, **offset included**.
- **D-354** authorised `LessDate`/`GreaterDate` for ordering but stated the comparison
  *basis* — instant or string — nowhere; `grob-stdlib-reference.md`'s timezone section is
  silent on it too, while its comparison block explicitly lists `d1 == d2`.

`toUtc()`/`toLocal()`/`toZone()` (D-355) make mixed-offset values of the **same instant**
routine. If `==` follows D-169 verbatim (field-by-field on `__value`) it is
offset-sensitive: `date.now()` and `date.now().toUtc()` compare unequal because their
round-trip strings differ. If `LessDate`/`GreaterDate` parse `__value` to `DateTimeOffset`
and use its ordering — the only sensible implementation, and almost certainly what shipped
**[verify on disk: confirm `LessDate`'s handler parses `__value` to `DateTimeOffset` and
orders by the instant, not the string]** — they are instant-based. The two then disagree:
for `a := date.now()` and `b := a.toUtc()`, `a < b` is false, `a > b` is false and `a == b`
is false. Trichotomy is violated — a value neither less than, greater than, nor equal to
another of the same instant is exactly the surprise the "LINQ-for-scripting, a language
that doesn't surprise you" identity exists to forbid. `sort` by a `date` key inherits
whichever semantics its comparator receives, compounding the sort-key gap C0a's audit
covers.

**The decision.**

1. **`date`-vs-`date` equality and comparison are both instant-based**, matching .NET
   `DateTimeOffset`'s own operator semantics: `==` and the relational operators compare the
   instant (the moment in UTC), and `EqualsExact` — the offset-sensitive variant — is
   deliberately **not** exposed. Two `date` values of the same instant at different offsets
   are equal, and neither is less nor greater; trichotomy holds. Implemented as a documented
   special case of D-169 keyed off the nominal `date`-vs-`date` pairing the checker already
   gates (D-354's `GetStructTypeName(...) == "date"` on both operands): the `Equal` handler
   gains a `date`-nominal arm — or a dedicated `EqualDate` dispatch, at the increment's
   discretion, opcode shape governed by `adding-an-opcode` if a new opcode is chosen — that
   parses both `__value` strings and compares instants, rather than falling through to
   structural field-by-field string comparison. Every **other** struct pairing (user `type`s,
   other plugin nominal types) keeps D-169's field-by-field semantics unchanged. This
   amendment is `date`-scoped.

2. **`daysUntil`/`daysSince` basis is pinned** to whole 86,400-second periods between the two
   instants (`DateTimeOffset` subtraction → `TimeSpan.Days`), **not** calendar days counted in
   either operand's local offset — instant-based, consistent with (1). The "calendar days,
   DST-aware" alternative is deliberately rejected for v1: it is the more surprising choice
   for the sysadmin-data-plumbing use cases, and instant-difference is what a script author
   reaching for `daysSince` expects. Scripts 7 and 8 depend on this answer; it is fixed here
   rather than discovered at fixture-authoring time.

3. **`guid` reviewed while open, left unchanged.** guid equality stays field-by-field string
   equality under D-169: a guid's canonical-string field *is* its identity — no "same guid,
   different representation" case exists the way "same instant, different offset" does for
   `date` — so no amendment is warranted. Stated explicitly so the review's "review guid while
   there" is closed, not left implicit.

**Sequencing — why P1.** Gold masters and fixtures for `date` equality and ordering are
authored **after** this decision lands in code (the C0-cluster date-equality increment,
sequenced after the named-type registry so `date` is on the registry before its `Equal` arm
is touched), never before. Increment B's date test surface is still fresh; every fixture
written against accidental string-equality semantics raises the cost of the fix. That is the
whole reason this is P1 rather than a parked follow-up.

No new error code — a `date`-vs-non-`date`-struct or `date`-vs-scalar equality or comparison
still surfaces the pre-existing `E0002` (D-169's incompatible-type rule and D-354's relational
gate). Count unchanged at 118.

Full detail: `grob-stdlib-reference.md`'s `date` comparison section (to be updated to state
the instant-basis explicitly and pin `daysUntil`/`daysSince`), D-169 (the entry this amends for
`date`), D-354 (the relational-opcode authorisation and its nominal gate), D-355 (the `__value`
string field).

---

### D-358 — Native default arguments; `date.parse`'s optional pattern (July 2026)

Area: Compiler / Stdlib — native default arguments; date module
Supersedes: none
Superseded by: none
Refines: D-342, D-344, D-354, D-355, D-356

**The problem.** `grob-stdlib-reference.md` (line 388) shows
`date.parse("05/04/2026", "dd/MM/yyyy")` as live surface, but D-354 and D-355 both record
that the two-argument form is **not** built, is not in the Sprint 9 scope bullet, and that
`NamespaceRegistry`'s flat one-entry-per-member model cannot express it — D-354 left it as an
open design question with no D-### of its own. Independently, Sprint 9's `fs` scope requires
`copy(src, dest, overwrite: bool = false)` and `move(..., overwrite: bool = false)`:
default-argument dispatch is a Sprint 9 dependency **regardless** of `date.parse`.

**The decision.**

1. **Native functions support optional trailing parameters with compile-time constant
   defaults** — a single default-argument mechanism in the native-dispatch path, not
   per-function special-casing. It generalises D-344's one-off `input()` prompt-defaulting arm
   (which filled a missing 0-argument call's prompt with `""`) into the general shape, and can
   later absorb it. A member entry (in `NamespaceRegistry` or the D-356 named-type registry)
   may declare trailing parameters as optional with a constant default; at a call site
   supplying fewer arguments than the full arity, the compiler synthesises the missing trailing
   arguments from the declared constants — the same "inject a synthesised constant argument"
   shape D-345's `formatAs` column-injection already uses — before the ordinary `GetGlobal`-
   then-`Call`, so the runtime native keeps a **fixed** arity. This is **default arguments, not
   overload resolution**: one function with an optional tail, no dispatch on argument count or
   type. True arity/type overloading remains unsupported and out of v1 scope. Resolves the
   open question D-354 named.

2. **`date.parse` gains its optional second argument** — `parse(input: string, pattern: string
   = "")`. An empty pattern means ISO-8601 (the one-argument behaviour, unchanged); a non-empty
   pattern is passed to `DateTimeOffset.ParseExact`. Runtime parse failure reuses `E5702`
   (D-284, unchanged — the same code the one-argument form already throws through the
   native-throw seam). `grob-stdlib-reference.md`'s line-388 sample becomes true rather than
   aspirational.

3. **`fs.copy`/`fs.move`'s `overwrite: bool = false`** land on the identical mechanism when
   Increment C builds `fs`. Named here so the machinery is designed **once**, alongside D-356's
   registry (where the optional/default metadata lives on the member entry), and consumed by
   both `date.parse` and `fs` rather than built twice.

**Rationale — the foundational-principle call.** Shipping pattern parsing is the better
language. Real-world CSV dates are rarely ISO-8601, and `csv` (Increment E) is `date.parse`'s
natural habitat — Script 5's domain (CSV processing) hits this in anger. PowerShell, Python and
Go all offer pattern parsing; a statically typed scripting language whose driving use cases are
sysadmin data plumbing cutting it would be a visible gap and a spec-vs-surface divergence
Pillar 6's cold-read campaigns would catch anyway. The alternative — cut it from v1, correct the
sample, add it to the scope-cut list — was considered and rejected: the optional-argument
machinery is needed for `fs` regardless, so the marginal cost of `date.parse`'s second argument
is one registry-entry field, not a new subsystem.

**Sequencing.** The default-argument mechanism is designed with D-356's registry and lands
before Increment C, which consumes it for `fs`. `date.parse`'s code half then rides Increment C
or a dedicated micro-increment — the **decision** is pre-9C, the **code** lands during 9C,
before `csv` (Increment E) needs it.

No new error code (`E5702` reused for `date.parse`; `fs`'s `overwrite` is a boolean parameter,
not an error path). Count unchanged at 118.

Full detail: `grob-stdlib-reference.md`'s `date.parse` and `fs` sections, `grob-v1-requirements.md`'s
Sprint 9 Scope bullet (`fs`/`date`), D-342 (`NamespaceRegistry`), D-356 (the registry carrying
default-argument metadata), D-344 (the `input()` one-off this generalises), D-354 (the open
question this resolves).

---

### D-359 — Index-target compound assignment and increment/decrement: `arr[i] op= v`, `arr[i]++`/`--` (Sprint 9 Increment A4) (July 2026)

Area: Compiler — statement emission / type checking
Supersedes: none
Superseded by: none
Refines: D-348, D-350, D-351

**The decision.** `arr[i] += v`/`m[k] += v` and `arr[i]++`/`arr[i]--` — including
map and chained (`matrix[r][c]`) targets — now emit, closing the gap D-350 named:
the statement previously compiled to nothing, RHS side effects (`arr[i] += sideEffect()`)
included, via the same blanket `is not IdentifierExpr` early-return in both
`Compiler.Statements.cs` and `TypeChecker.Statements.cs`. `grob-language-fundamentals.md`
§28 already specified `arr[i]` as a valid compound-assignment target and the
`x += y` → `x = x + y` lowering; the gap was in the compiler, not the spec.

**Emission — evaluate-once, no new opcode.** The receiver and index expressions are
each visited exactly once, stashed in two reserved temp locals in their own scope
(`DeclareLocalSlot`/`EmitGetLocal` — the same vehicle `for...in` lowering and the
switch-expression subject cache already use — not D-334's bare `_nextSlot` bump the
A4 kickoff prompt named, which reserves stack space but declares no named local).
`GetLocal`/`GetLocal` push the eventual `SetIndex` operands; a second `GetLocal`/`GetLocal`/
`GetIndex` reads the current value; the RHS (or the int literal `1` for `++`/`--`, applying
the identical `+= 1`/`-= 1` lowering at the call site — no dedicated index-local fast path
exists, unlike a true stack local's `IncrementInt`/`DecrementInt`) and the ordinary typed
binary opcode follow; `SetIndex` writes back. The two temp locals are released with the
existing `EmitScopeCleanup` (`PopN`, since a compiler-synthesised temp name is never
lambda-capturable) — unlike the switch-expression precedent, which deliberately leaves its
subject slot's value live as the expression result, this is a statement and must leave
nothing on the stack.

**Type checking.** Element type comes from `ArrayTypeDescriptor` (D-351) via the ordinary
`VisitIndex` path. The operand-type/int-only validity check reuses **E0002** — extracted
into a shared `EmitCompoundOperatorTypeCheck` so the index-target and pre-existing
identifier-target paths apply the identical rule — not **E0001**, which is a different code
(`VisitIndexAssignmentTarget`'s plain-`=`-assignment RHS-assignability check); the A4 kickoff
prompt's own "Type checking" section left this ambiguous, resolved here. `FindReadonlyRoot`
(D-350) is reused unchanged for `E0204`. A map receiver's `Unknown` element stays permissive
— `m[k] += v`/`m[k]++` still emit (assuming `int` at runtime) rather than being rejected,
mirroring D-350's established map-write latitude; no map element typing is built here
(F5-2/C0b's job). No new opcode, no new error code; count unchanged at 118.

**Load-bearing plumbing fix, in scope because A4 depends on it.** `IndexExpr` gained a
settable `ElementType` property (mirroring `MemberAccessExpr.ResolvedFieldType`), set by
`VisitIndex` alongside its existing return. The compiler's `GetExprType` — the helper that
picks a typed opcode by reading type-checker annotations off a node — had no `IndexExpr`
case at all and fell back to `Unknown`, which `EmitArithmetic` then silently defaults to
`Int`. That fallback predates D-351 and was written for lambda parameters, but it applies
equally to any `IndexExpr` operand: `floatArr[i] + 1.0`, a **plain** binary expression, not
a compound assignment, was already silently selecting `AddInt` instead of `AddFloat` before
this increment. Fixing `GetExprType` was necessary for A4's own compound-assignment path
(which reuses "the same typed-opcode selection the plain binary operator uses") and fixes
the plain-operator case as an inseparable side effect — the same one-line switch arm serves
both, so this is not scope creep.

**Sibling finding, confirmed and left for scheduling.** Field-target compound assignment
and increment (`obj.field += v`, `obj.field++`) hit the identical `is not IdentifierExpr`
guard and are themselves silently dropped today — contrary to the A4 kickoff prompt's
supposition that plain `obj.field = v`'s working, receiver-evaluated-once assignment path
was a usable precedent to mirror. Plain `=` assignment on a member target does work; the
compound/increment forms on that same target do not. Not fixed on this branch (one concern
per branch) — named here for a follow-up increment.

**A4-as-feature over the Sprint 9B review's tourniquet.** The Sprint 9B principal review
(`grob-principal-review-sprint9b.md`, finding F1b) recommended a stopgap diagnostic for this
gap. Pre-9C planning superseded that: no existing error code fit cleanly, a tourniquet would
have burned a permanent code (ADR-0017) for a short-lived band-aid, and A4's dependencies
(D-348, D-350, D-351) were already shipped. Delivering the feature closes F1b by removing
the gap it was raised against, rather than papering over it.

**Tests.** Bytecode-shape and type-check diagnostic tests in `Grob.Compiler.Tests`
(including a hand-built-AST map case, mirroring `CompilerForInTests`' established
no-map-literal convention, and the `floatArr[i] + 1.0` regression for the `GetExprType` fix);
end-to-end tests in `Grob.Integration.Tests` covering all five compound operators,
increment/decrement, chained targets, the evaluate-once case (a side-effect counter function
observed to run exactly once) and the inherited `E5001`/`E5002` runtime-error paths through
the CLI's top-level diagnostic formatting.

Full detail: `grob-language-fundamentals.md` §28 (Assignment, Increment and decrement),
D-348 (`GetIndex` read emission), D-350 (`SetIndex` write emission, `FindReadonlyRoot`),
D-351 (`ArrayTypeDescriptor`), `grob-principal-review-sprint9b.md` (finding F1b).

---


## Post-MVP Decisions

---

### D-PM-001 — `Grob.Git` plugin design sketch (Apr 2026)

Area: Grob.Git plugin — design sketch
Supersedes: none
Superseded by: none

Post-v1 first-party plugin. Umbrella for all git scripting. Three surfaces: (1) `git.open()` — local repo via LibGit2Sharp, host-agnostic. (2) `git.azureDevOps()` — typed AzureDevOps client, full name always used in plugin/types, user aliases as `ado :=` etc. (3) `git.github()` — typed GitHub client including Enterprise via `host:` parameter. Shared types: `PullRequest`, `GitUser`, `HostRepo` — normalised across hosts. Local API: `branches()`, `log()`, `log(containing:)` pickaxe search, `diff(commit)`, `staleBranches(days: 90)`, `raw()` escape hatch returning `ProcessResult`. Scripts using shared operations are host-portable with a one-line client construction change. Script 8 rewritten against `Grob.Git` in v1.1. “ADO” abbreviation not used in plugin or type names — ambiguous with ActiveX Data Objects. Full API shape in this entry.

---

## Built-in Type Method Registry

_(Detail in `grob-type-registry.md`)_

---

## Open Questions

_(Detail in `grob-open-questions.md`)_

---

## Standard Library — Confirmed Modules

_(Detail in `grob-stdlib-reference.md`)_

---

## Reference Languages

| Language | What to steal                                                          |
| -------- | ---------------------------------------------------------------------- |
| Go       | Error handling as values, simple formatting, low ceremony, fast feel   |
| Kotlin   | Type inference, null safety, concise syntax, optional chaining         |
| Swift    | Optionals done right, readable and writable in equal measure           |
| C#       | `?` nullable syntax, `??` and `?.` operators, LINQ as fluent reference |
| Rust     | Pattern matching, making illegal states unrepresentable                |

---

## Real-World Use Cases (April 2026)

These are the scripts Grob needs to be able to write. They drive stdlib design.

| #   | Use Case                          | Key requirements                                               |
| --- | --------------------------------- | -------------------------------------------------------------- |
| 1   | Azure CLI / Bicep scripting       | `process` first-class, stdout/stderr capture, exit codes       |
| 2   | SharePoint PnP library wrapping   | Third-party plugin model (`IGrobPlugin` wrapping .NET libs)    |
| 3   | Azure DevOps REST API power tools | `Grob.Http` (including `auth.*`), `json` stdlib, typed structs |
| 4   | Agent hook / Copilot-style tools  | JSON stdin/stdout pipeline, `env`, clean string interpolation  |

### Sketch — ADO Stale Branches Script (April 2026, updated)

Pipeline shape: `grob run stale-branches.grob --params stale.grobparams`

Input: JSON or CSV file of repo objects, or piped via stdin on any OS.
Output: JSON array of `StaleResult` objects via stdout, or formatted table via `format.table()`.

Config: `ADO_PAT` via `@secure` param. `staleDays` via param with default.

```grob
import Grob.Http   // http.* and auth.* both available — auth is a sub-namespace

// env, json, date are core — no import needed

@secure
param pat:       string

@minLength(1)
param staleDays: int = 30

type Repo {
    org:     string
    project: string
    name:    string
}

type Branch {
    name:        string
    lastCommit:  string
    author:      string
    aheadCount:  int
    behindCount: int
}

type StaleResult {
    org:           string
    project:       string
    repo:          string
    staleBranches: Branch[]
    staleCount:    int
    asOf:          string
}

fn adoGet(org: string, path: string): json.Node {
    token := auth.basic("", pat)
    url   := "https://dev.azure.com/${org}/${path}"
    return http.get(url, token).asJson()
}

fn getBranches(repo: Repo): Branch[] {
    path     := "${repo.project}/_apis/git/repositories/${repo.name}/stats/branches?api-version=7.1"
    response := adoGet(repo.org, path)
    return response["value"].mapAs<Branch>()
}

fn isStaleBranch(branch: Branch, cutoff: date): bool {
    last := date.parse(branch.lastCommit)
    return last < cutoff && branch.name != "main" && branch.name != "master"
}

fn processRepo(repo: Repo): StaleResult {
    cutoff   := date.today().minusDays(staleDays)
    branches := getBranches(repo)
    stale    := branches.filter(b => isStaleBranch(b, cutoff))
    return StaleResult {
        org:           repo.org
        project:       repo.project
        repo:          repo.name
        staleBranches: stale
        staleCount:    stale.length
        asOf:          date.today().toIso()
    }
}

repos   := json.read("repos.json").mapAs<Repo>()   // file input — primary pattern
// repos := json.stdin().mapAs<Repo>()             // stdin — pipeline variant
results := repos.select(r => processRepo(r))
json.stdout(results)
```

Companion params file `stale.grobparams`:

```
// stale.grobparams
// pat is intentionally absent — supply via --pat or keep out of source control
staleDays = 30
```

---

## VM Architecture — Key Points

_(Full detail in `grob-vm-architecture.md`)_

- Stack-based bytecode VM
- Compiler walks AST, emits flat instruction stream. VM is a dumb fetch-decode-execute loop
- Constant pool separate from instruction stream
- Call frames as fixed array `CallFrame[256]` — no heap allocation per call
- FOR loops lowered to WHILE by the compiler — VM never sees FOR opcodes
- Backpatching for forward jumps. Backward jumps (loops) don’t need patching

### Implementation order

1. Chunk + constant pool + `CONSTANT`, `RETURN`
2. Value stack + arithmetic opcodes
3. Global variables
4. Control flow — jump, backpatching
5. Local variables + call frames
6. Functions + `CALL`/`RETURN`
7. Native functions + standard library
8. GC (if not leaning on C# entirely)
9. Plugin system
10. Module/import system

---

## Deferred — Not In Scope Until Post-MVP

| Feature                 | Notes                                                                                                                                                    |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Compile to executable   | Transpile to C# via Roslyn — post-MVP                                                                                                                    |
| VS Code extension       | TextMate grammar, LSP — post-MVP                                                                                                                         |
| JIT compilation         | Explicitly out of scope                                                                                                                                  |
| Concurrent GC           | Not needed for scripting use case                                                                                                                        |
| Content mutability      | Mutable binding vs mutable value for collections — defer                                                                                                 |
| AI tutor                | Guided learning companion — post-MVP, see personality doc                                                                                                |
| User-defined exceptions | Custom typed exceptions — post-MVP                                                                                                                       |
| Range/span indexing     | `[..n]`, `[^n..]`, `[start..end]` for strings and arrays — post-MVP, clean additive grammar extension, no architectural rework required                  |
| User-facing generics    | Declare generic fns/types in Grob scripts — post-MVP                                                                                                     |
| Sparky plushie          | Conference/trade show merchandise — post-release                                                                                                         |
| Sparky commissioned art | Human illustrator brief ready. Execute when project is public                                                                                            |
| do…while loop           | Deferred — expressible as `while` with initial execution. Post-MVP.                                                                                      |
| Labelled break          | Break outer loop from nested loop — restructure into function for v1. Post-MVP.                                                                          |
| Return type inference   | Inferred return types on functions — v1 requires explicit return types. Post-MVP.                                                                        |
| Doc comments (`///`)    | Lexer recognises and discards in v1. Semantics attached when `grob doc` tooling exists. Post-MVP.                                                        |
| Semantic tokens         | LSP Phase 5 — type-aware highlighting overlay on top of TextMate grammar. After LSP is stable. Post-MVP.                                                 |
| `Grob.Git` plugin       | Umbrella git plugin — local repo (LibGit2Sharp), AzureDevOps client, GitHub client. Shared types across hosts. Design sketch in D-PM-001. Post-MVP.      |
| Tuples                  | Additive grammar extension. No architectural rework required. Structs serve the same purpose with named fields in v1. Revisit if real friction observed. |

---

## Related Documents

| Document                                             | Purpose                                                                                                |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `grob-v1-requirements.md`                            | v1 build specification — success criteria, sprint plan, definition of done                             |
| `grob-solution-architecture.md`                      | Solution structure, assembly responsibilities, dependency graph — authoritative                        |
| `grob-language-fundamentals.md`                      | Language fundamentals specification — parser, type checker, compiler reference                         |
| `grob-install-strategy.md`                           | Runtime and plugin install strategy — scopes, paths, CLI reference                                     |
| `grob-stdlib-reference.md`                           | Standard library reference — module shapes, built-in functions, expressions                            |
| `grob-type-registry.md`                              | Built-in type method registry — compiler and type checker reference                                    |
| `grob-open-questions.md`                             | Open and resolved design questions with full rationale                                                 |
| `grob-tooling-strategy.md`                           | LSP, syntax highlighting, VS Code extension — phased plan                                              |
| `grob-language-brainstorm.md`                        | Early sketch notes — supplementary                                                                     |
| `grob-vm-architecture.md`                            | VM and runtime architecture detail — `GroType` references superseded by ADR-0012                       |
| `grob-personality-identity.md`                       | Character, tone, error messages, REPL, CLI                                                             |
| `grob-sample-scripts.md`                             | Real-world script comparisons and API surface validation                                               |
| `grob-plugins.md`                                    | Plugin ecosystem — authoring, registry, official plugins — `GroType` references superseded by ADR-0012 |
| `sharpbasic-retrospective.md`                        | Completed retrospective — Grob design inputs                                                           |
| `sparky-character-sheet-v1.png`                      | Mascot reference — approved April 2026                                                                 |
| `sparky-illustrator-brief.pdf`                       | Brief for human illustrator commission                                                                 |
| `docs/adr/adr-0007-solution-structure-and-naming.md` | Solution structure and `GrobType` naming decision                                                      |

---

_This document is the authoritative decisions record for Grob._
_July 2026 — Sprint 9 Increment A4: D-359 added. Closes the index-target compound-_
_assignment/increment gap D-350 named — `arr[i] += v`/`m[k] += v` and `arr[i]++`/`arr[i]--`_
_now emit instead of silently dropping the statement. Evaluate-once emission: receiver/_
_index each visited once into reserved temp locals (the `for...in`/switch-expression_
_`DeclareLocalSlot` precedent, not D-334's bare `_nextSlot` bump), read-modify-write over_
_the existing `GetIndex`/typed-binary-op/`SetIndex`, temps released via `EmitScopeCleanup`._
_`++`/`--` lowers to `+= 1`/`-= 1` at the call site. Type-checked via `ArrayTypeDescriptor`_
_(D-351) reusing E0002 (not E0001, a different code) and the existing `FindReadonlyRoot`_
_walk for E0204; a map's `Unknown` element stays permissive. `IndexExpr` gained an_
_`ElementType` property and the compiler's `GetExprType` a matching arm — necessary A4_
_plumbing that also fixes a pre-existing sibling bug where a plain `floatArr[i] + 1.0`_
_silently selected int arithmetic. Field-target compound assignment/increment confirmed_
_itself broken (not the working precedent the kickoff prompt supposed) and named for a_
_follow-up increment rather than fixed here. Chosen over the Sprint 9B review's F1b_
_tourniquet — delivering the feature closes that finding outright. No new opcode, no new_
_error code; count unchanged at 118._
_Previous: July 2026 — Sprint 9 Increment B principal-review remediation, planning session: D-356,_
_D-357 and D-358 added; `grob-type-registry.md`'s `map<K, V>` static-typing claim corrected._
_D-356 authorises a single declarative named-type registry consolidating the six hand-rolled,_
_string-matched dispatch surfaces per nominal `Struct` type (three annotation-position resolvers,_
_the method/property validator, VM dispatch, the `ValueDisplay` `toString()` registration) into_
_one table entry per type — extending the `NamespaceRegistry` (D-342) idea from module members to_
_instance surfaces; `guid`/`date` migrate as behaviour-preserving proving cases, `File` through_
_`ProcessResult` (Sprint 9) and `Response`/`AuthHeader`/`ZipEntry` (Sprint 11) then land as data;_
_arrays/maps stay structural (descriptor-based, D-351), explicitly out of scope. D-357 amends_
_D-169 for nominal `date`: equality and ordering both instant-based, matching `DateTimeOffset`_
_operator semantics (`EqualsExact` not exposed), restoring the trichotomy the round-trip-string_
_`__value` field (D-355) would have broken across `toUtc`/`toLocal`/`toZone`; `daysUntil`/_
_`daysSince` pinned to 86,400-second periods between instants; `guid` reviewed and left_
_field-by-field. D-358 adds native default arguments (one mechanism generalising D-344's_
_`input()` arm, compiler-synthesised trailing constants, fixed runtime arity — default_
_arguments, not overloading), landing `date.parse`'s documented optional `pattern` argument_
_(reusing E5702) and `fs.copy`/`fs.move`'s `overwrite` default on the same machinery. The_
_registry doc-honesty edit (F5-1) states map per-key/value typing as the target surface with_
_a build-status note citing D-351, not a shipped claim. Decisions are pre-Sprint-9C; code lands_
_across the C0 cluster (registry → date-equality → arrays → maps) and Increment C. No new error_
_code; count unchanged at 118._
_Previous: July 2026 — Sprint 9 Increment B: D-354 and D-355 added. D-354 amends D-108 ahead of_
_`date`'s first implementation — drops `minusDays` (no per-unit `minus*`; `addDays`/_
_`addMonths`/`addHours`/`addMinutes` all accept a negative `n` uniformly), adds_
_`toDateOnly()`/`toTimeOnly()` (epoch-anchored), and authorises `LessDate`/`GreaterDate`_
_— the first `OpCode` enum growth since Sprint 2 — so `d1 < d2`/`>`/`<=`/`>=` work,_
_gated to a nominal date-vs-date pair at the type checker; D-108 marked superseded._
_D-355 is the landing record: `date` lands following the `guid` precedent (D-149) —_
_`Struct`-discriminated (D-303), one hidden `__value` field holding a round-trip_
_`DateTimeOffset` string, a `NamespaceRegistry` entry for the seven static_
_constructors, real per-argument arity/type checking (unlike guid's all-zero-arity_
_methods), the first type besides `guid` recognised in annotation position, `IClock`_
_consumption (D-343) converted to local time (D-176), `date.parse` reusing the_
_reserved `E5702` (D-284, no compile-time literal check unlike guid's E0601) and a_
_registered canonical-ISO-8601 `toString()` for `ValueDisplay` (D-336) — discovered_
_`DateTimeOffset`'s `K` specifier never renders `Z`, unlike `DateTime`'s, handled with_
_an explicit zero-offset format branch. No new error code; no new `GrobValueKind`._
_Previous: July 2026 — Sprint 9/10 adversarial-hardening planning: D-352 and D-353 added._
_D-352 pins `Grob.Http`'s previously-unspecified redirect and credential-forwarding_
_policy — cross-origin redirects drop the `AuthHeader` (silent-and-safe), https→http_
_downgrades throw `NetworkError`, the hop chain caps at 10, `download()` follows_
_identically; cap and downgrade faults reuse the existing `NetworkError` leaf. D-353_
_authorises `grob-adversarial-testing-strategy.md` — the seven-pillar suite (compiler_
_fuzzing, hostile `.grobc`, differential/metamorphic + cross-model adversaries, stdlib/_
_environment brutality, exhaustion/soak, cold-read campaigns, and a two-tier local-_
_Kestrel-plus-Azure hostile network surface), the `tooling/Grob.Torture` black-box_
_harness, and a two-pass schedule with quantitative alpha exit criteria — a Sprint 9/10_
_interlude for Pillars 1–6 (complete at Sprint 9's core-module close) and a post-Sprint-11_
_network-hardening pass for Pillar 7 once `Grob.Http` lands (plugins are Sprint 11, not_
_Sprint 9). No error code added; count unchanged at 118._
_Previous: July 2026 — Sprint 9 Increment A3: D-351 added. Gives `GrobType.Array`/`NullableArray`_
_a real element-type identity via a new `ArrayTypeDescriptor` mirroring_
_`FunctionTypeDescriptor`'s side-channel shape — `Grob.Core`'s `GrobType` enum stays_
_flat; the descriptor is carried on `Symbol.ArrayDescriptor` (generalising Sprint 8_
_Increment E's narrower field), array-literal nodes and array-returning call results._
_Corrects the increment's own premise: `map<K, V>` is not a working precedent —_
_`TypeRef.TypeArguments` is parsed but never read, `"map"` resolves to the flat_
_`GrobType.Map` tag everywhere, and map values are already `Unknown` in `for...in`;_
_maps share arrays' pre-existing gap and stay out of this decision's scope. Threads the_
_element type through array-literal inference (`[1, "a"]` is `E0001`, int/float_
_widens), index read (replacing D-348's unconditional `Unknown`), index write RHS_
_(closing the A2 gap D-350 named), `for...in` item binding (including the item's own_
_struct-name/nested-array identity), function-parameter/return enforcement and_
_struct-field construction — reusing `E0001`/`E0004`/`E0005` throughout, no new error_
_code. Zero quarantines — no fixture in the corpus relied on loosely-typed or_
_heterogeneous arrays; 2,482 tests pass across the affected projects, 92.17% coverage_
_on `Grob.Compiler`. Surfaces, Chris-approved as its own follow-up: the array_
_mutation-method surface (`append`/`insert`/`remove`/`clear`/`contains`/`first`/`last`/_
_`length`/`isEmpty`) has no type-checking, compiler emission, or VM support at all —_
_D-140 documented it but only `filter`/`select`/`sort`/`each` were ever built._
_Unblocks A4 (`arr[i] += v`, `arr[i]++`/`--`) and Increment D's `mapAs<T[]>()`._
_Previous: July 2026 — Sprint 9 Increment A2: D-350 added. Lands `arr[i] = v` / `m[k] = v`_
_index-store emission, the write companion to D-348's read-side `VisitIndex`. Wires the_
_existing, previously-unemitted `OpCode.SetIndex` (already in the closed enum and_
_recognised by the disassembler; never dispatched by the VM or emitted by the compiler)_
_into both `Compiler.Statements.cs` and the VM: array writes bounds-check via the_
_existing `E5101`/D-334 handler table exactly as `GetIndex` does; map writes upsert; nil_
_raises `E5201`. One shape covers chained targets (`matrix[r][c] = v`) for free._
_`readonly` rejection (`E0204`) generalises the existing member-access root-walk to also_
_walk `IndexExpr` chains — corrects the increment's kickoff prompt, which cited D-289 for_
_the readonly-deep-immutability precedent; the actual source is **D-291** (D-289 is the_
_unrelated `const`-expression definition). `const`-bound array/map mutation is confirmed_
_unreachable (D-289 already disallows array/map/struct literals as `const` RHS, failing_
_earlier at `E0205`) so no const-rejection code is added. RHS element-type checking stays_
_permissive — `GrobType.Array` has no scalar element type (awaits generics) — named as an_
_open, honestly-scoped gap rather than built ad hoc. `arr[i] += v` and `arr[i]++`/`--`_
_are confirmed still silently broken by the same early-return shape and named for_
_scheduling, not left as a code comment. No new opcode, no new error code; count_
_unchanged at 118._
_Previous: July 2026 — Sprint 9 Increment A: D-348 and D-349 added. D-348 lands the missing_
_`VisitIndex` override in `Compiler.Expressions.cs` — `arr[i]` previously compiled to_
_nothing at all (the default `AstVisitor<T>.VisitIndex` falls through to `Compiler`'s_
_no-op `DefaultVisit`), crashing the VM with a stack underflow (D-345's surfaced gap)._
_The fix emits the receiver, the index expression, then the existing `OpCode.GetIndex`;_
_the opcode's bounds-checked array arm, nil-on-miss map arm, and its D-334 handler-table_
_routing through the existing `E5101`/`IndexError` (D-284) were already implemented at_
_the VM layer, reachable only via `for...in` lowering until now. Covers `matrix[r][c]`_
_(D-112) via `IndexExpr` nesting for free. No opcode, `GrobValueKind`, parser or AST_
_change. Array element write (`arr[i] = v`) is confirmed still deferred (a pre-existing,_
_separate gap in `Compiler.Statements.cs`) and left for a future increment — Sprint 9's_
_`json`/`csv` indexer consumers are read-only. §3.1.1's `ResolvedType`/`Declaration`_
_invariant is confirmed scoped to identifier nodes only, not `IndexExpr` — no properties_
_added. D-349 is an append-only reconciliation: Sprint 8 Increment D's `guid` module_
_landed `E0601` (source D-149, count 117→118) with `grob-error-codes.md` updated_
_correctly but no matching decisions-log entry, leaving D-345/D-346 reading a stale_
_"count unchanged at 117". D-345/D-346 are left unedited; D-349 is the missing landing_
_record, correcting the log's own running-count narrative to 118 to match the registry._
_No new error code from either entry; count unchanged at 118._
_Previous: July 2026 — Compile-time baseline recapture: D-347 added. A `benchmark.yml` manual_
_run (29399169091, `main` @ 9419037, post-Sprint-8) failed the regression gate on_
_`Compile_TwoExpressions` (+5.8% rolling) — a genuine same-CPU breach, not the `vm`_
_category's CPU-mismatch noise seen in the same run. Root cause: D-342's_
_`RegisterNamespaces` unconditionally seeds all seven core-module namespaces into_
_every compile's global scope, a fixed cost every compile now pays, landing hardest_
_on the smallest benchmark. The allocation half of this same regression was already_
_fixed (Sprint 8 QA pass, static `NamespaceRegistry` object caching) and still holds_
_(+2.8% alloc, ok); the remaining time cost is the irreducible residual of an_
_already-lean registration loop, not something to chase further. Recaptures_
_`compile.json`'s time axis from that run's report wholesale (D-309: canonical-_
_workflow output only, never a local run) — `BenchCheck` now reads 0.0% delta._
_`compile.origin.json` left untouched (still CPU-mismatched against the frozen_
_Intel Xeon origin, D-333). Mirrors D-338→D-341's ship-then-recapture precedent._
_No new error code; count unchanged at 117._
_Previous: July 2026 — Sprint 8 Increment F (sprint close): D-346 added. The_
_stability-test calibration ritual runs against the six Sprint-8-runnable scripts_
_(`hello`, `calculator`, `functions`, `types`, `errors`, `stdlib`), not "all_
_thirteen" per §6 step 1 — every one of `grob-sample-scripts.md`'s scripts needs a_
_Sprint-9 module or an unbuilt plugin; the full-suite run is deferred to a v1_
_release-gate step. Surfaces, rather than silently sweeps, that the "thirteen"_
_count itself is stale: `grob-sample-scripts.md` has only ever held eleven scripts,_
_and D-283 (predating D-302) already called it eleven. A longer checkpoint sweep_
_found a one-time cache/registry warm-up step between iteration 1000 and 2000,_
_not a leak. A first attempt at the §6 placeholder `warmup: 100` mistook this step for_
_ongoing growth and failed a naive 5% tolerance. Locked: `iterations: 10000,_
_warmup: 2000, tolerancePercent: 2.0`; first passing run at exactly 0.0% drift_
_(190,456 B at both the post-warmup and final snapshot); `stability.json` committed_
_with the calibration date. `bench/Grob.Benchmarks/Stability/StabilityRunner.cs`_
_drives the loop through `Grob.Cli.RunCommand` (fresh VirtualMachine, fresh plugin_
_registration per call, already built for this purpose) rather than_
_re-implementing `SystemEnvironment`/`SystemRandomSource`/`SystemClock` a second_
_time — a documented deviation from §3's literal "Core, Compiler, Vm, Stdlib,_
_Runtime" assembly list, which predates `Grob.Cli`/`RunCommand` existing as the_
_composition root; `Grob.Benchmarks.csproj` now also references `Grob.Cli`._
_`stdlib.grob` (D-337 family) is gold-mastered under `Grob.Integration.Tests`,_
_exercising `math`/`path`/`env`/`log`/`guid`/`formatAs` with no Sprint-9 module; the_
_five prior smoke scripts confirmed still passing. No new error code; count_
_unchanged at 117._
_Previous: July 2026 — Sprint 8 Increment E: D-345 added. Lands `formatAs` (`table`/`list`/`csv`)._
_Corrects a second "D-339" mis-citation (see D-344's identical correction) — the_
_governing decision is D-342, whose E1003/E1004 the namespace-misuse diagnostics fold_
_into. The chained-form rewrite is shared resolution over a pattern-matched AST shape_
_(`CallExpr`/`MemberAccessExpr` are immutable records), independently re-derived by_
_the checker and the compiler, mirroring the existing namespace-receiver precedent._
_Compile-time column derivation is a bounded, `formatAs`-scoped peek — one new_
_`Symbol.ArrayElementStructTypeName` field (parameters only) plugs the one real gap;_
_no general array-element-type system. A new `IPluginRegistrar.RenderValue` capability_
_lets `Grob.Stdlib` render cells through the VM's real `ValueDisplay` (D-336). A real_
_regression (formatAs := 1 double-erroring E1103 + E1102, the first reserved identifier_
_that is also a namespace) was caught and fixed in the same change. Array indexing_
_(`arr[i]`) was found to have no compiler emission at all — confirmed pre-existing and_
_out of scope. No new error code; count unchanged at 117._
_Previous: July 2026 — Sprint 8 Increment C: D-344 added. Lands `env`, `log` and `input()`._
_`IStandardStreams` gains a third member, `In`, for `input()` to read from;_
_`SingleWriterStreams` answers it with a closed `TextReader.Null`, mirroring its_
_existing `Error` convention. `input()` is a new dispatch category — the_
_no-namespace native — validated by its own `VisitCall` arm ahead of the permissive_
_`print`/`exit` fallback (0–1 args, reusing E0003/E0004), with a matching compiler_
_arm filling a missing 0-argument call's prompt with `""` before the ordinary_
_`GetGlobal`-then-`Call` shape. `env`/`log` are ordinary `NamespaceRegistry`_
_consumers of the D-342 pattern; `env.require` reuses E5801, `input()`'s_
_closed-stdin fault reuses the residual E5305 — no new error codes. `log.setLevel`_
_recognises exactly its four lowercase level names; anything else is a silent_
_no-op by design. `ValueDisplay`/registered-`toString()`-through-`log` wiring is_
_deferred to Increment D (`guid`, its first real consumer) rather than built as_
_untested forward scaffolding. `--verbose` selects `LogPlugin`'s initial threshold._
_Corrects the increment's kickoff prompt, which mis-cited non-existent "D-339"/_
_"D-340" and the unrelated D-334 — the governing decisions are D-342 and D-343._
_Count unchanged at 117._
_Previous: July 2026 — Sprint 8 Increment A kickoff: D-342 and D-343 added. D-342 makes core_
_stdlib modules (`math` this increment) compile-time namespaces — a third name_
_category alongside value and type bindings, resolved via a `NamespaceRegistry`_
_table and a fixed member-access dispatch precedence (namespace receiver resolves_
_against the registry; value receiver falls through unchanged to the pre-existing_
_struct-field and array-higher-order-method arms). Namespace-in-value-position is_
_new code E1004; unknown-namespace-member reuses the pre-existing, previously-unused_
_E1003 rather than duplicating it. No runtime module object, no new opcode — a_
_namespace constant or a namespace-qualified native both compile to `GetGlobal`_
_against the qualified name, the same shape a plain top-level function call already_
_uses, since `Grob.Compiler` cannot reference `Grob.Stdlib` to embed a native's_
_actual delegate as a compile-time constant. D-343 refines D-319's provisional_
_capability-interface sketch: `IGrobPlugin`/`IPluginRegistrar` land in `Grob.Runtime`_
_with `IPluginRegistrar` deliberately distinct from the concrete VM type (the DAG_
_forbids `Grob.Runtime` → `Grob.Vm`); `IStandardStreams` is the first capability_
_consumed, by `OpCode.Print`, via a new `VirtualMachine` constructor overload that_
_leaves the existing single-`TextWriter` constructor and its ~39 call sites_
_unchanged; `print`/`exit` keep their existing dedicated opcodes rather than_
_becoming `Call`-dispatched natives. `IEnvironment`/`IClock`/`IRandomSource` are_
_declared for Increments B/C/D, un-implemented until consumed. One new error code_
_(E1004); count 116 → 117._
_Previous: July 2026 — Compile-allocation baseline recapture: D-341 added. Performs the "separate,_
_subsequent, logged act" D-338 deferred — the rolling `compile.json` baseline is_
_replaced with the `benchmark.yml` run 29207744217 report (`windows-latest`, AMD EPYC_
_7763), folding in D-338's accepted +14.0%/+7.6% (8,968 B / 15,584 B) figures; `BenchCheck`_
_now reads 0.0% delta on both compile benchmarks. `compile.origin.json` deliberately left_
_untouched — the CPU mismatch against the frozen origin host makes the now-larger_
_+47.1%/+32.8% cumulative time drift informational only under D-333's CPU-identity guard,_
_never a gate breach; an origin re-freeze remains a separate maintainer-judged act._
_`vm.json`/`endToEnd.json` untouched (non-gating / no fresh benchmarks). Also backfills_
_this changelog with the D-338 entry, which was logged in PR #123 without a matching_
_changelog line — the mechanical drift gate checks index↔entry lockstep, not the_
_free-text changelog, so the gap was invisible to CI. No error code added; count_
_unchanged at 116._
_Previous: July 2026 — Compile-allocation regression from the Sprint 7 exception_
_hierarchy: D-338 added. `TypeChecker.RegisterExceptionHierarchy` re-synthesised 11_
_immutable `TypeDecl`/`UserTypeInfo`/`Symbol` objects, grew two dictionaries from empty_
_via repeated resize, and fed all 11 into the type-cycle DFS on every compile regardless_
_of source content. Four behaviour-preserving fixes (shared static caches, two_
_dictionary pre-sizings, cycle-detection exclusion) cut the Sprint-7-close regression_
_from +68.6%/+37.2% to +14.0%/+7.6% bytes/op, all tests and gold masters unchanged. The_
_residual ~1,104 B is accepted as load-bearing — D-284 requires all 11 `GrobError`_
_hierarchy names resolvable in every compile's global scope, so a permanently larger_
_built-in symbol table is a real, unavoidable per-compile cost. Rolling baseline_
_deliberately left un-updated pending a separate recapture (see D-341, above). No error_
_code added; count unchanged at 116._
_Previous: July 2026 — Pre-Sprint-8 / pre-QA interlude (Sprint 7E hand-off findings): D-335,_
_D-336 and D-337 added. D-335 restores `tests/Grob.Integration.Tests` to `Grob.slnx`,_
_silently dropped by PR #94 (`28eb753`, 2026-06-26) and unrun by CI for two sprints,_
_and adds a test-project membership check to `Grob.Consistency.Tests` (enumerate_
_`**/*.Tests.csproj`, assert each is referenced, drift fails the build) — generalising_
_D-316 from documents to project membership. D-336 resolves the value display protocol:_
_a `ValueDisplay` service in `Grob.Runtime` with `Display(v)` (top-level, strings_
_unquoted per D-179) and `Inspect(v)` (nested, strings quoted), `toString()` remaining_
_the sole public method; `print()` and interpolation dispatch through it; structs, arrays_
_and maps render as source-shaped literals (`Config { host: "example.com", port: 8080 }`,_
_`[1, 2, 3]`, `{ "a": 1 }`); reference-identity cycle detection (`<cycle>`) plus a depth_
_cap, since E0301/E0302 reject only non-terminating type cycles. The `[TypeName]`_
_fall-through was never a decision — D-159's `[AuthHeader]` is deliberate per-type_
_credential opacity and D-101's `@secure` is the do-not-echo instruction. §13 of_
_`grob-language-fundamentals.md` corrected to describe dispatch that occurs. Dispatch is a_
_numbered precedence — nil, then a registered `toString()` (terminal), then scalars, then_
_position-dependent `string`, then structural composite. Step 2 before step 5 is_
_security-critical: plugin types and user `type`s share the `Struct` discriminator (D-297),_
_so `[AuthHeader]`'s credential opacity (D-159) holds today by accident, via the same_
_fall-through arm that emits `[Config]`; a structural renderer wired first leaks bearer tokens._
_`Function` values render as their type (`fn(int): int`), never an address — gold masters_
_require deterministic output. `float` renders round-trippable with a decimal point (`1.0`,_
_not `1`) under pinned `InvariantCulture`, with pinned `NaN`/`Infinity` — unpinned, a `de-DE`_
_host emits `1,5` and breaks every gold master and `formatAs.csv`. The four_
_`Sprint6IncrementBTests` assertions were rewritten mid-interlude to expect `[Config]`; that_
_commit is not a decision and must not be read as one — the defect was first mis-diagnosed by_
_inferring intent from `git log`. D-336 rewrites them to assert the exact rendered form._
_D-337 documents the five sprint-close smoke scripts_
_(`hello`, `calculator`, `functions`, `types`, `errors`), their cumulative growth rule and_
_their stdout/stderr/exit-code contract — `errors.grob` exits 42 by design. No error codes_
_added; count unchanged at 116._
_July 2026 — Sprint 7 Increment C (`finally`): D-334 added. The exceptional path_
_(an exception unwinding through a region) is VM-run via handler-table_
_`TryRegion.FinallyOffset`; every non-exceptional path (normal try/catch_
_completion, early `return`/`break`/`continue`) is compiler-emitted by_
_re-visiting `node.Finally`'s AST at each crossing site — duplication expected,_
_the closed `OpCode` enum has no `Leave`/`EndFinally`. Compiler: a_
_`TryFinallyContext` stack mirroring `LoopContext`; `return` crosses every_
_context, `break`/`continue` cross only those pushed after the target loop_
_(`LoopDepthAtPush`); a `_nextSlot` reservation parks a `return`'s value under_
_the finally bodies' own locals. VM: `PropagateThrow` runs each region's_
_finally via `RunFinallyExceptional`, a bounded mode of the existing_
_`RunDispatch` that runs in the current frame and stops at the region's own_
_closing `TryEnd`; an internal `FinallyEscape` carries a throw-in-finally_
_replacement back to the outward walk (D-275). Rejected a closure-based_
_exceptional-path invocation (forces upvalue capture per referenced local,_
_doesn't unify with the compiler-emitted copies). Folds in a mechanical fix_
_making E2206 reachable (`ParseTry` previously broke unconditionally on a_
_matched `finally`). No new error code; count unchanged at 116._
_July 2026 — Pre-Sprint 7 Interlude 2 (benchmark gate hardening): D-333 added._
_A hard allocation axis (percent-vs-baseline plus an absolute LOH tripwire that_
_gates regardless of category), a significance-aware per-sprint time gate_
_(breach requires clearing both the flat percentage and 3× the relative_
_measurement noise), and a CPU-identity guard replacing the OS-family-only_
_runner check (`D-309` refined "same runner type" to "same CPU identity";_
_allocation always gates, time gates only on a CPU match). `vm.json`/_
_`vm.origin.json` recaptured post-D-332-fix; `compile.json` refreshed after_
_found three sprints stale; `compile.origin.json` left on its pre-provenance_
_host, so compile's cumulative axis is informational until a separate,_
_logged re-freeze. No new error code; count unchanged._
_June 2026 — Pre-Sprint 7 Interlude 1 (VM operand-stack allocation): D-332 added._
_`ValueStack`'s backing array right-sized from a fixed 16,384-slot (393 KB, over the_
_LOH threshold) allocation to a 1,024-slot (24 KB) default that grows geometrically_
_via `Array.Resize` on `Push`, capped at the unchanged 16,384-slot ceiling — fixes the_
_Sprint 6 benchmark finding (`Gen0 == Gen1 == Gen2` on all three VM benchmarks). D-325_
_open upvalues (stack object + slot index, never a raw reference/span) survive the_
_resize by construction; overflow guard (E5903) and effective depth cap unchanged; no_
_GrobValue/opcode/.grobc change. No new error code; count unchanged. Baseline_
_recapture on windows-latest via benchmark.yml pending (D-309: never a locally-produced_
_committed baseline) — bench/Grob.Benchmarks/baseline/vm.json not yet updated._
_June 2026 — Harness remediation: D-331 added. Closed-surface growth gets sanctioned procedures calibrated to stability contracts — `OpCode`/`TokenKind` via `adding-an-opcode` (ADR-0013 wire format), parser/AST via the new `extending-the-grammar` skill (no contract, built incrementally, closed within an increment as scope discipline), error codes via the new `allocating-an-error-code` skill and a per-increment budget (ADR-0017 immutability, count reconciled against the live registry and ratified by D-316). Root `CLAUDE.md`, `AGENTS.md`, the compiler-engineer agent, `defining-a-type`, `writing-an-error-test` and the unrun Sprint 6 C/D/E commands reworded from walls to paths; merged Sprint 6 A/B left as run. No error code added; count unchanged._
_June 2026 — Sprint 5 Increment 6 (test-coverage scope and floor): D-328 added as a full entry_
_with matching summary index row, promoted to ADR-0018. Coverage given a committed exclusion_
_set and a CI-enforced 90% line+branch floor on the language-implementation denominator;_
_`csharpsquid:S3776` on the type-checker statement-visit method closed by behaviour-preserving_
_decomposition under green. No error code added; count unchanged. Detail in_
_`.claude/commands/sprint-5-increment-6-coverage-scope.md` and ADR-0018. D-329 added: MinVer_
_7.0.0 adopted for git-tag-derived semver versioning; `MinVerMinimumMajorMinor=0.5` in_
_`Directory.Build.props`; CLI/REPL banners read `AssemblyInformationalVersionAttribute`._
_June 2026 — Sprint 5→6 kickoff: D-330 added (unknown field at construction → dedicated E0012; registered in Sprint 6 Increment B, count 109 → 110)_
_Updated June 2026 — D-327: the `[]` array suffix is a type-reference production._
_`TypeRef` is recast as a primary type plus a postfix `[]`/`?` suffix chain, so_
_`int[]`, `int[][]` (D-182), `int[]?` (nullable array) and `int?[]` (array of_
_nullable) parse as type annotations. Closes the gap D-326 left — its formalised_
_`TypeRef` grammar had no `[]` production though its own suffix-precedence prose_
_assumed one, so `ParseTypeRef` never consumed `[` in type position even though_
_`int[]` is pervasive across the spec and the `T[]` stdlib signatures. Array_
_type-refs are a built-in constrained generic (D-080, same model as `map<K, V>`),_
_not user-facing generics — no generics-sprint dependency. The grouping primary_
_`'(' TypeRef ')'` generalises D-326's dedicated parenthesised-nullable-function_
_production; a trailing suffix binds to the return type, so `(fn(): int)[]` and_
_`(fn(): int)?` need parens. The checker resolves an array annotation to the_
_existing `T[]` type (literals/inference already build it) — annotation path only,_
_no new runtime type. Runtime-erased — no opcode/grobc/`GrobValue` impact._
_Malformed suffixes (`int[`, fixed-size `int[5]`) reuse existing parser-error_
_codes via D-300; no new error code; count stays 109. Edits: parser `ParseTypeRef`_
_suffix loop + array/nullable type-ref AST nodes, type-checker array-annotation_
_resolution to `T[]`, tests (positions × suffix-precedence × recovery matrix),_
_`grob-language-fundamentals.md` §9, `grob-type-registry.md` fn-type surface note._
_Relates to D-326/D-182/D-080/D-300. Sprint 5 Increment 5._
_Updated June 2026 — D-326: function types are a first-class type-reference_
_form. `fn(ParamTypes): ReturnType` is accepted by `ParseTypeRef` anywhere a_
_type is written. Resolves the explicit-return-type-vs-returnable-closure_
_contradiction that left `makeCounter(): fn(): int` rejected with E2001._
_Structural identity; invariant assignability (no variance in v1); runtime-_
_erased (callable is already a `GrobFunction` — no opcode/grobc/`GrobValue`_
_change). `?`/`[]` bind to the return type; `(fn(): int)?` / `(fn(): int)[]`_
_need parens. User surface is monomorphic; the registry's internal `→` notation_
_and D-080 are untouched. Resolves during pass-1 signature registration (D-323_
_phase 1.5 not involved). No new error code; mismatches reuse the existing_
_assignment/argument codes (confirmed in-increment per D-308); count stays 109._
_The `makeCounter` capture-section example becomes a passing test. Edits: parser_
_`ParseTypeRef` fn arm, type checker `FunctionType` GrobType + structural_
_equality + invariant assignability, `grob-language-fundamentals.md` §9,_
_`grob-type-registry.md`. Relates to D-080/D-296/D-166/D-323. Sprint 5 Increment 4._
_Updated June 2026 — D-325: upvalue closing is location-based, not value-based._
_The VM keeps an open-upvalue list keyed by stack slot; on frame exit every open_
_upvalue at or above the returning frame's base is closed into its heap cell,_
_independent of which heap object references the closure. Fixes the array-_
_escaped-closure value-stack underflow (`return [inc]` then `arr[0]()`): value-_
_based closing missed closures wrapped in a container, leaving an open upvalue_
_on a truncated slot. Post-return invariant (no open upvalue ≥ frame base)_
_added as a `#if DEBUG` assertion. Category-4 capture only (D-296); categories_
_1–3 and top-level fn hoisting (D-321, no captures) untouched. No new error_
_code; count stays 109. Edits: VM dispatch loop (open-upvalue list, `OP_CLOSURE`_
_capture, frame-exit close sweep), `Grob.Vm.Tests` escape matrix (array, map,_
_struct-field, returned-IIFE, parameter — not a single case),_
_`grob-vm-architecture.md` upvalue-lifecycle section (doc not in this upload —_
_pending edit, alongside the D-322 known-limitation note). Relates to D-296 and_
_D-321. Sprint 5 Increment 3._
_Updated June 2026 — D-324: uniform top-level redeclaration. E1102 broadened_
_from `:=`-only to all top-level binding-introducing forms (`fn`, `type`,_
_`readonly`, `const`, `:=`). `fn`/`type` pass-1 registration changed to_
_provisional (matching value bindings); `RegisterProvisionalValueBinding` gains_
_an fn/type guard; new `FinalizeTopLevelBinding` helper unifies the collision_
_predicate. Corrects reverse-order bug (value-before-fn collision now fires at_
_the fn, not the value). E1102 title broadened to "name already declared in this_
_scope". Count stays 109. Edits: `TypeChecker.cs` (pass-1, guard, helper),_
_`TypeChecker.Declarations.cs` (VisitFnDecl/VisitTypeDecl/VisitReadonlyDecl/_
_VisitConstDecl), `TypeChecker.Statements.cs` (VisitVarDecl message),_
_`ErrorCatalog.cs`, `grob-error-codes.md`, `grob-language-fundamentals.md`_
_§19/§28. Refines D-321 and D-323. Sprint 5 Increment 2._
_Updated June 2026 — D-323: three-pass type checker. New phase 1.5 resolves_
_top-level value-binding types in initialiser-dependency order (DFS,_
_Unvisited/Visiting/Visited) between pass-1 registration and pass-2 body_
_validation. Closes the false-E0005 gap for forward value reads in function_
_bodies (`fn f(): int { return x }` / `readonly x := 5` no longer fails)._
_Unannotated mutual cycles are a compile-time type-dependency error (new E0303);_
_annotated mutual cycles are structurally sound for the type checker and surface_
_at runtime as E5902. `UpdateProvisionalType` keeps `Provisional = true` so the_
_E1102 guard in pass 2 is unaffected; non-provisional fn/type symbols are never_
_overwritten. Lifts the `circular-initialisation` gold-master quarantine: fixture_
_updated to `return product`, expected.txt updated to line-only `:5` (D-322)._
_Edits: `TypeChecker.cs` (Check() three-pass, UpdateProvisionalType helper), new_
_`TypeChecker.ValueResolution.cs` partial, `ErrorCatalog.cs` E0303 (109 codes),_
_`grob-error-codes.md`, `grob-language-fundamentals.md` §17/§17.2/§19.1, gold-_
_master pair. Refines D-321 and D-166. Sprint 5 Increment 1._
_Updated June 2026 — D-322: runtime diagnostics are line-granular. A runtime_
_error renders as `file:line` with no column — omitted, not idealised — because_
_the compiled `Chunk`'s debug info is keyed by source line (the D-306_
_disassembler prints lines for the same reason). Compile-time diagnostics keep_
_full `(file, line, column)` (D-137), so §10's compile-time format example is_
_unaffected. Not a D-321 regression — D-321 moved E5902 to a function-body read_
_site and surfaced the line-only render (`:5`) on the `circular-initialisation`_
_example, but runtime E5902 was always line-only. Gold masters, harnessed and_
_rich, record the actual line-only output so they agree; no fabricated column._
_Per-opcode column tracking deferred out of v1. Known-limitation note for_
_`grob-vm-architecture.md` flagged as a follow-up (that doc not in this upload)._
_Updated June 2026 — D-321: top-level `fn` bindings are runtime-hoisted (a_
_compiler prologue binds every top-level function before the first top-level_
_statement runs) and the two-pass checker's pass 1 now registers top-level value_
_bindings as well as `fn`/`type`. Together these make a forward call and a_
_function body's read of a later-declared top-level value resolve and run, and_
_narrow E5902 to genuine value-binding initialisation cycles — a forward call is_
_no longer an E5902 trigger. The VM composes the E5902 message tracing through_
_the function via the `ErrorCatalog.E5902` descriptor. Refines D-294 and D-166;_
_clean because top-level fns capture no upvalues (D-296). Edits: Fundamentals_
_§19.1, the `circular-initialisation` error example, compiler `VisitCompilationUnit`,_
_type-checker pass 1, VM `CallFrame`/message composer. Sprint 5 correctness_
_increment after Increment E; 5E's init-state machine unchanged._
_Updated June 2026 — D-320: `select` is a reserved identifier, not a hard_
_keyword. D-280 made `.select()` the universal pipeline transform and D-301 made_
_`select` the statement keyword; reserving the word as a keyword made_
_`arr.select(...)` ungrammatical from source even though the checker, VM and_
_compiler supported it — and §12 of `grob-language-fundamentals.md` already used_
_`.select(...)` as the canonical pipeline form, so the spec demonstrated a call_
_its own lexer forbade. Six release-gate scripts (3, 5, 7, 8, 9, 12) were blocked._
_Resolution is the D-282 `formatAs` mechanism: the lexer emits `Identifier`,_
_`select` leaves the keyword set, the statement parser promotes a leading_
_`select (` at statement head, `expr.select(...)` parses as member access, and_
_the type checker forbids `select` as a binding name. Fills a gap D-282 left —_
_its reserved-identifier rule had no error code — by adding E1103 (reserved_
_identifier used as a binding name), serving both `formatAs` and `select`; v1_
_reserved set `{ formatAs, select }`. The consistency gate (D-316) gains a check_
_asserting no registered native method name collides with a reserved word. Edits:_
_decisions-log full entry + summary index row; `grob-language-fundamentals.md` §3_
_lexing note and a new §12 "Reserved identifiers" subsection;_
_`grob-error-codes.md` E1103 (total 107→108); `grob-v1-requirements.md` `TokenKind`_
_keyword list (`select` moved to a reserved-identifiers note). Implemented as a_
_Sprint 5 correctness increment after the merged Increment C; feature increments_
_D, E and F keep their letters — no F→G renumber._
_Updated June 2026 — Playground follow-up: D-319 refined against the live_
_`grob-solution-architecture.md` and the Sprint 5 increment prompts. DAG home_
_confirmed — the host capability interfaces sit in `Grob.Runtime` beside_
_`IGrobPlugin`, reached through the existing `GrobVM` registration surface (no new_
_reference, no cycle), which puts them on the public plugin NuGet contract (correct,_
_so plugins doing I/O stay sandboxable). The `exit()` correctness point is already_
_handled by the existing `ExitSignal`. The dispatch-loop cancellation/step-budget_
_seam is adopted into v1 and folded into Sprint 5 Increment C: a `CancellationToken`_
_on the VM run entry (default unlimited) with a masked dispatch-loop check, the_
_counter on the VM instance so the budget spans the re-entrant native↔VM bridge,_
_surfacing as `OperationCanceledException` (outside `GrobError`, so a Grob `catch e`_
_cannot swallow it). No new opcode, no new error code; the consistency gate (D-316)_
_and catalog agreement (D-308) are unaffected. Sprint 5 Increment F gains a one-line_
_benchmark-attribution note so the small VM-execution step-up from the seam reads as_
_expected, not as an unexplained regression. No new decision number — these are_
_refinements to D-319._
_Updated June 2026 — Playground architecture session: D-319 (the browser_
_playground is a client-side Blazor WebAssembly embedding host served from the_
_existing Azure Static Web App, chosen over a server-side sandbox so nothing is_
_sent to a server and no untrusted-code execution surface is taken on) added as a_
_full entry with matching summary index row. The playground is a second embedding_
_host wiring an alternate capability set: `fs` → an in-memory virtual filesystem_
_(seeded, upload-fed, zip-exportable, doubling as the unit-test filesystem double),_
_`env` → a synthetic map, `process` → unsupported via an in-hierarchy host runtime_
_error with no new shipped error code — surfaced as "some functionality not_
_currently supported in the playground". Enabling principle recorded: the engine_
_must be a pure, embeddable library with all OS contact behind injected host_
_capability interfaces (`IFileSystem`, `IEnvironment`, `IProcessRunner`,_
_`IStandardStreams`, `IClock`, `IRandomSource`), shared with the LSP and test_
_hosts. Two binding correctness points: `exit()` is a non-catchable unwind outside_
_`GrobError` so a bare `catch e` (D-274) cannot swallow it, and the VM is fresh per_
_run with no static mutable run-state (extends D-300). The one accommodation with a_
_current-sprint window — a cheap cancellation/step-budget check in the VM dispatch_
_loop — is flagged for v1; build timing otherwise deferred post-v1. Full detail in_
_`grob-playground-architecture.md`._
_Updated June 2026 — D-318: the four named-argument call-site errors (D-113)_
_assigned dedicated codes E0008–E0011 — named-before-positional, naming a_
_required parameter, duplicate named argument, unknown parameter name — rather_
_than folding into E0003, since none is an arity error and each carries a_
_distinct fix. Registered in Sprint 5 Increment B through their `ErrorCatalog`_
_descriptors; no codes added to the catalog by this entry._
_Updated June 2026 (interlude B) — D-317: project hardening interlude. Additive_
_supply-chain and build-integrity hardening of the repository: central package_
_management with exact pins and committed lockfiles under locked-mode restore,_
_NuGet and CodeQL vulnerability gates (CodeQL via GitHub default setup),_
_deterministic builds with SourceLink, SHA-pinned least-privilege workflows,_
_gitleaks CI secret scanning alongside TruffleHog, and CycloneDX SBOM and_
_build-provenance scaffolding. Complements §11 language/runtime security; does_
_not overlap it. Code signing deferred to first public release (OQ-018)._
_Updated June 2026 (interlude A) — D-316: corpus consistency regime. A1_
_reconciled the corpus to a green floor (error-code canonical total corrected_
_99 → 103, four unaccounted codes named; Project Status row corrected off_
_"Sprint 1"). A2 added `tests/Grob.Consistency.Tests`, a stateless agreement_
_suite running on every commit, asserting error-code count agreement,_
_decisions-log lockstep, ADR-reference integrity and opcode/TokenKind_
_completeness, with D-308's `ErrorCatalog` test referenced as the_
_catalog↔registry guard. Generalises D-308; self-relative, no frozen baseline._
_Updated June 2026 — D-315: `break`/`continue` in `select` resolved_
_asymmetric. `break` inside a `select` arm is a compile error (E2211) at_
_any nesting, whether or not a loop encloses it — its C-family fall-through_
_job is gone under D-301 and the loop-exit reading is a silent footgun for_
_the C# audience (reflexive case-terminating `break`). `continue` passes_
_through to the nearest enclosing loop as in C#; the skip pattern is_
_preserved. `select` is therefore not loop-control-transparent — the_
_earlier "pushes nothing onto the loop-context stack" framing in_
_`grob-v1-requirements.md` is withdrawn. Companion finding: E2212_
_(`break`/`continue` outside any loop) added — already a specified compile_
_error with no registry code. Resolves the Requirements §4 / Fundamentals_
_§3 contradiction; D-301 stays exhaustiveness-only. Edits: Fundamentals §3,_
_Requirements §4 Sprint 4 scope/acceptance, error registry (E2211, E2212)._
_Updated June 2026 — D-314: implementation harness migrated from GitHub_
_Copilot to Claude Code. Durable rules move to `CLAUDE.md`; plan mode_
_becomes the approval gate; increment prompts become `.claude/commands/`_
_slash commands; the Opus 4.8 carve-out becomes a `.claude/agents/`_
_subagent; the GPT-5.3 Codex cold-read runs via Codex CLI against the_
_merged branch; CodeRabbit is retained, no Claude reviewer subagent is_
_added. The implementation workflow shape (one concern per branch,_
_plan-then-build, pre-push gate, PR per increment, `main` protected) is_
_unchanged. No language or spec surface touched._
_Updated June 2026 — D-313: two-axis benchmark regression policy. A 5%_
_per-sprint gate against the rolling baseline plus a 12% cumulative ceiling_
_against a frozen origin baseline; `tooling/Grob.BenchCheck` makes_
_`benchmark.yml` the gate; compile-time gates cumulatively until the_
_end-to-end workload is live. Refines D-302, D-309. §8 and §9 of_
_`grob-benchmarking-strategy.md` rewritten._
_Updated June 2026 — D-312: bare `grob` (no subcommand, no args) is_
_equivalent to `grob --help` — prints the command listing to stdout and_
_exits 0. Does not launch the REPL; `grob repl` is the sole REPL entrance._
_Rationale matches the git/go/dotnet/winget mental model the audience_
_already holds; exit 0 chosen over git/go's non-zero usage-error stance_
_because Grob's exit table reserves 1 (runtime) and 2 (compile) and a bare_
_invocation is neither. Character rationale added to_
_`grob-personality-identity.md` (`--help` output section). §8 CLI table_
_row in `grob-v1-requirements.md` flagged as a follow-up — that doc was_
_not in this session's upload._
_Updated May 2026 — D-311: unresolved-identifier `Declaration` sentinel_
_(`UnresolvedDecl.Instance`) satisfies the §3.1.1 non-null invariant at_
_every error path in the type checker. Addendum to D-137. Three regression_
_tests added to `TypeCheckerTests.cs`._
_Updated May 2026 — D-310: `LangVersion` corrected to `14` in_
_`Directory.Build.props` to match the .NET 10 SDK. C# 14 is the canonical_
_language version going forward. Sprint 2-end QA verified Debug and Release_
_builds clean under the corrected pinning._
_Updated May 2026 — D-309: benchmark baseline production mechanism moved_
_to the `benchmark.yml` GitHub Actions workflow; `windows-latest` is the_
_canonical runner. Refines D-302. Benchmarking strategy doc updated_
_accordingly (§8.1 / §8.2 / §9 / §10 / §11 / §12)._
_Updated May 2026 — D-308: diagnostics are raised against `ErrorCatalog`_
_descriptors, never code literals. `ErrorDescriptor`/`ErrorCatalog`/`Diagnostic`_
_added to `Grob.Core`; agreement test added to `Grob.Core.Tests`; solution-wide_
_remediation of existing inline code literals authorised. Resolves the spreading_
_S1192 duplication at source and makes ADR-0017 enforceable._
_Updated May 2026 — D-307: built-in scalar type names confirmed lowercase_
_(`int`/`string`/`bool`/`float`) as canonical; Sprint 1 implementation_
_drift to capitalised `Int`/`String` flagged for correction in code and_
_tests. §29.6 worked example in `grob-language-fundamentals.md` corrected_
_to carry its mandatory return-type annotation (same finding cluster)._
_Updated May 2026 — D-306: bytecode disassembler and execution tracing_
_added as developer diagnostics. Disassembler always compiled, lands in_
_Sprint 2 against hand-constructed chunks; execution tracing gated behind_
_`#if DEBUG` to keep the Release dispatch loop branch-free for the D-302_
_benchmarks; `grob dump` CLI wrapper deferred to Sprint 12. Companion_
_edits: `grob-vm-architecture.md` gains a "Developer Diagnostics" section;_
_`grob-v1-requirements.md` gains Sprint 2 scope/acceptance, the §8_
_`grob dump` row, and Sprint 12 scope._
_Updated May 2026 — D-305: clox preparation gate satisfied (core_
_chapters worked through including NaN boxing, upvalue/closure_
_mechanism, call frames and value representation); Sprint 1 cleared to_
_begin. Project-status table updated — "clox worked through" → done,_
_"Implementation started" → in progress. No design decision changed;_
_the entry records that the implementation precondition is met and_
_aligns the status table with reality. `GrobValue` noted as no longer a_
_pre-Sprint-1 blocker (shape locked by D-303, overtaking the provisional_
_framing at D-297)._
_Updated May 2026 — OQ-005 and OQ-006 closed. D-303 (`GrobValue` is a_
_tagged union — permanent; NaN boxing rejected on managed-runtime_
_grounds: moving GC cannot trace packed references in a `ulong`, pinning_
_defeats the size win, hybrid shapes pay the bit-manipulation cost_
_without the benefit, and Grob's I/O-bound workload does not need the_
_cache pressure win that justifies NaN boxing for tight numeric loops)._
_D-304 (lean on .NET GC; no custom mark-and-sweep in v1; "custom_
_garbage collector" added to the §13 explicitly-out-of-scope list;_
_benchmarking infrastructure_
_from D-302 provides the empirical surface to revisit if a real workload_
_shows GC pressure the .NET collector handles badly). D-297 marked_
_superseded by D-303; "provisional" framing removed from_
_`grob-vm-architecture.md` (GrobValue section renamed; speculative_
_"Mark and Sweep" subsection replaced by definitive "Lean on .NET GC"_
_content; deferred-decisions table tightened). OQ-005 and OQ-006_
_relocated from "Open Questions" to "Resolved Questions" in_
_`grob-open-questions.md` with full rationale preserved. `grob-v1-requirements.md`_
_§2 "tentative, OQ-005" parenthetical removed; GC bullet tightened._
_Updated May 2026 — Benchmarking session: D-302 (benchmarking_
_infrastructure — BenchmarkDotNet harness in `bench/Grob.Benchmarks`,_
_three categories — compile-time, VM execution, end-to-end script — plus_
_a separate stability test, `[MemoryDiagnoser]` on every benchmark,_
_committed JSON baselines, per-sprint regression run with 5% end-to-end_
_gate, no `grob bench` CLI surface in v1, Grob-aware memory introspection_
_deferred post-v1) added as a full entry with matching summary index row._
_D-302 covers test material storage (frozen end-to-end script copies under_
_`Fixtures/EndToEnd/`, hand-constructed VM chunks in C#, deterministically_
_generated synthetic large script for compile-time), BenchmarkDotNet_
_setup/teardown per category, and a stability test calibration ritual at_
_Sprint 8 close (initial 10,000/100/10% values are placeholders, locked_
_numbers derived from a single-iteration characterisation pass). Sprint 2_
_and Sprint 8 deliverables in `grob-v1-requirements.md` §4 explicitly_
_name the benchmark skeleton and stability calibration. Full spec at_
_`grob-benchmarking-strategy.md`._
_Previous: May 2026 — Session 3 spec gap fill: D-300 (parser error_
_recovery — synchronisation set, error node shape, cascade suppression_
_via the `Error` type, unbounded reporting, statelessness) and D-301_
_(`select` statement is non-exhaustive — intentional split from the_
_exhaustive switch expression) added as full entries with matching_
_summary index rows. Full spec text for both decisions in_
_`grob-language-fundamentals.md` §29 and §3 respectively._
_Previous: May 2026 — Session 2 decisions log reconciliation: D-297 (`GrobValue`_
_provisional representation), D-298 (`.grobc` binary format skeleton) and_
_D-299 (Sprint 8/9 reorder by dependency weight) added as full entries with_
_matching summary index rows. D-186 row repositioned in the summary index_
_from between D-285 and D-286 to its numerical position immediately after_
_D-185. ADR cross-references corrected: D-287 and D-294 citing ADR-0017 in_
_error-code-registration contexts changed to ADR-0014 (the error code_
_numbering scheme is ADR-0014; ADR-0017 is the stability rule). Companion_
_ADR-0008 → ADR-0013 corrections applied to `grob-grobc-format.md` (7 sites),_
_`grob-open-questions.md` (2 sites) and `grob-vm-architecture.md` (1 site) —_
_opcode stability and bytecode format versioning is ADR-0013, not ADR-0008_
_(which is "No Var Keyword")._
_Previous: April 2026 — pre-implementation review: 21 new decision entries added._
_Escape sequences updated (`\r` added, unknown escapes are compile errors)._
_Numeric precision locked (int = 64-bit signed, float = 64-bit IEEE 754)._
_Integer overflow: checked arithmetic, throws RuntimeError._
_Implicit coercion: only int → float, all else explicit._
_Trailing commas: permitted everywhere. Forward references: two-pass type checker._
_Shadowing: allowed with warning. Script structure order: import → param → type/fn → code._
_Equality semantics: value equality for structs, arrays, maps, anonymous structs._
_Nil chain propagation: `?.` short-circuits entire chain. Script-level return: compile error._
_Explicit non-features: no multiple return values, no operator overloading, no circular imports._
_json.write/encode/stdout: pretty-printed by default, `compact: bool = false` parameter._
_date constructors: local time default. fs.readText: UTF-8 default with BOM auto-detection._
_Map literal separators: newlines or commas. string.toString(): identity method added._
_Stack overflow: RuntimeError at depth 256._
_Previous: pipe operator row corrected; `Grob.Zip` API shape; `env` full API; `format` full API;_
_`Grob.Http` locked signatures; `json.encode()` added; `json.Node`, `Response`, `AuthHeader`, `ProcessResult` fully specified._
_Previous: OQ-011 resolved (`Grob.Crypto` API); OQ-012 resolved (`process.run()` timeout);_
_`guid` core module added; `fs.copy`/`fs.move` overwrite parameter added;_
_Script 11 (Azure Resource Provisioning Helper) added to validation suite._
_Session A2: decisions table converted to 185 numbered ADR-style entries (D-001 through D-185) plus one post-MVP entry (D-PM-001). Summary index added. Supersedes/superseded-by links populated._
_Session A2 update (Apr 2026): D-270 through D-296 integrated from six session summary files (B Part 1, B Part 2, C Part 1, C Part 2, B Part 3, B Interlude, B Part 4). D-013, D-061, D-084, D-166, D-181 annotated with supersession/extension notes. C Part 2 scope-cut list assigned D-186 (first unused number in D-186–D-269 gap) after D-286 collision resolved._
_The brainstorm doc and VM architecture doc are supplementary — this document wins on conflict._
_`GroType` references in `grob-vm-architecture.md` and `grob-plugins.md` are superseded — read as `GrobType`._
_Updated April 2026 — post-reconciliation alignment: ADR-0007 references in this document changed to ADR-0012 to match the on-disk repository's ADR numbering. The repo is the canonical pin for ADR numbers. Same change applied to `grob-v1-requirements.md`._

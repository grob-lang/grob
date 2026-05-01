# Grob — Decisions & Context Log

> Authoritative record of design decisions, open questions, and context.
> All entries are dated. Nothing undated is a firm decision.
> This document is updated after every design session.
> Source of truth when the brainstorm doc and VM doc conflict.

-----

## Project Status

|Milestone                       |Status                                   |
|--------------------------------|-----------------------------------------|
|SharpBASIC complete             |✅ Done                                   |
|SharpBASIC retrospective written|✅ Done — April 2026                      |
|clox worked through (Ch 14–30)  |🔄 In progress                            |
|Grob formal design phase begun  |✅ Done — this document                   |
|Grob Claude Project created     |✅ Done — April 2026                      |
|Mascot designed (Sparky)        |✅ Done — character sheet v1 complete     |
|Personality & identity locked   |✅ Done — see grob-personality-identity.md|
|Licensing model decided         |✅ Done — MIT                             |
|Language fundamentals specified |✅ Done — April 2026                      |
|Plugin ecosystem model decided  |✅ Done — see CONTRIBUTING.md, PLUGINS.md |
|Solution architecture locked    |✅ Done — April 2026                      |
|v1 requirements specified       |✅ Done — April 2026                      |
|Tooling strategy defined        |✅ Done — April 2026                      |
|MVP defined and scoped          |✅ Done — see grob-v1-requirements.md     |
|Implementation started          |⬜ Pending                                |

-----

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

> *A statically typed scripting language that a hobbyist can learn and a developer can trust.*

**Identity statement (full):**

> *Grob is a statically typed scripting language with C-style syntax, type inference,
> and first-class file system operations. Nullable types are explicit. Immutability
> is opt-in via `const`. It’s designed to be readable by any C# or Go developer
> without prior knowledge of Grob.*

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

-----

## Confirmed Decisions — Summary Index

|D-###|Date    |Area                        |Summary                                                                                     |
|-----|--------|----------------------------|--------------------------------------------------------------------------------------------|
|D-001|Feb 2026|Targeting                   |Arduino hardware targeting ruled out                                                        |
|D-002|Feb 2026|Purpose                     |General-purpose scripting chosen over DSL                                                   |
|D-003|Feb 2026|MVP                         |Console calculator as MVP success criterion                                                 |
|D-004|Feb 2026|Modules                     |Module/import system in scope, late phase                                                   |
|D-005|Feb 2026|Philosophy                  |“Build for developers, design for hobbyists”                                                |
|D-006|Feb 2026|VM strategy                 |Stack-based bytecode VM, informed by clox                                                   |
|D-007|Feb 2026|Implementation              |Written in C# .NET                                                                          |
|D-008|Feb 2026|Syntax                      |Same-line braces                                                                            |
|D-009|Feb 2026|Syntax                      |No semicolons                                                                               |
|D-010|Feb 2026|Syntax                      |`//` comments                                                                               |
|D-011|Feb 2026|Variables                   |`:=` declares; `=` reassigns; no `var`                                                      |
|D-012|Feb 2026|Variables                   |No uninitialised variables                                                                  |
|D-013|Feb 2026|Variables                   |Mutable by default; `const` for immutable *(partially superseded by D-288)*                 |
|D-014|Feb 2026|Types                       |`?` suffix for nullable types                                                               |
|D-015|Feb 2026|Types                       |`??` nil coalescing; `?.` optional chaining                                                 |
|D-016|Feb 2026|Functions                   |`fn` keyword; typed parameters; explicit return type                                        |
|D-017|Feb 2026|GC                          |Lean on C#’s GC; structs for value types                                                    |
|D-018|Feb 2026|Plugin system               |Stdlib implemented as `IGrobPlugin`                                                         |
|D-019|Feb 2026|Plugin system               |Type safety enforced at plugin boundary                                                     |
|D-020|Feb 2026|Bytecode format             |`.grobc` binary format; magic number `GROB`                                                 |
|D-021|Feb 2026|Execution model             |Primary use: compile in-memory and run                                                      |
|D-022|Feb 2026|Fluent syntax               |Fluent chaining yes — requires collections API first                                        |
|D-023|Feb 2026|Collections                 |C# LINQ as design north star                                                                |
|D-024|Feb 2026|Release                     |Open source; release when core is solid                                                     |
|D-025|Apr 2026|Plugin loading              |`--plugin` retired from public API; `--dev-plugin` for dev only                             |
|D-026|Apr 2026|Import statement            |`import` is the single non-core dependency mechanism                                        |
|D-027|Apr 2026|Core modules                |13 core modules auto-available, no import required                                          |
|D-028|Apr 2026|Import signal value         |Scripts with no imports are self-contained                                                  |
|D-029|Apr 2026|Plugin import alias         |Default alias is last segment lowercased                                                    |
|D-030|Apr 2026|Explicit alias              |`import X as y` for collision resolution only                                               |
|D-031|Apr 2026|Import vs requires          |`import` chosen over `requires`                                                             |
|D-032|Apr 2026|Package install             |`grob install`; never silently downloads at runtime                                         |
|D-033|Apr 2026|Package resolution          |Check `grob.json` then `~/.grob/packages/`; compile error if missing                        |
|D-034|Apr 2026|Project manifest            |`grob.json` for multi-script projects                                                       |
|D-035|Apr 2026|Package registry            |NuGet; tagged `grob-plugin`                                                                 |
|D-036|Apr 2026|grob.json shape             |npm-influenced; semantic versioning; `^` for compatible                                     |
|D-037|Apr 2026|AST pattern                 |Visitor pattern for three-pass AST                                                          |
|D-038|Apr 2026|Scope                       |`:=` declares in current scope; `=` walks parent chain                                      |
|D-039|Apr 2026|Error strategy              |Compiler collects all errors; VM stops on first runtime error                               |
|D-040|Apr 2026|Compiler tests              |Test compiler outputs exhaustively                                                          |
|D-041|Apr 2026|Partial classes             |Compiler as `partial class` files                                                           |
|D-042|Apr 2026|Real program target         |Real-program target required before implementation begins                                   |
|D-043|Apr 2026|OQ-002 resolved             |User-defined struct types confirmed; `type` keyword                                         |
|D-044|Apr 2026|Semantic analyser           |No empty placeholder; type-checker pass is the semantic analyser                            |
|D-045|Apr 2026|Use cases                   |Real-world targets: Azure CLI, ADO, agent hooks                                             |
|D-046|Apr 2026|Pipeline model              |File-read primary; stdin/stdout for pipeline composition                                    |
|D-047|Apr 2026|String interpolation        |`"Hello ${name}"` confirmed load-bearing                                                    |
|D-048|Apr 2026|Licensing                   |MIT licence                                                                                 |
|D-049|Apr 2026|Open source model           |Core in main repo; first-party plugins in `plugins/`                                        |
|D-050|Apr 2026|Community plugins           |Independent repos; registry via `PLUGINS.md` PR                                             |
|D-051|Apr 2026|Plugin SDK                  |`Grob.Runtime` NuGet package; versioned independently                                       |
|D-052|Apr 2026|Contributions               |Fork → branch → PR; CLA on first PR                                                         |
|D-053|Apr 2026|Mascot                      |Sparky — raccoon, blue hoodie, utility belt, wrench                                         |
|D-054|Apr 2026|Logo mark                   |`G>` — forward chevron on G                                                                 |
|D-055|Apr 2026|REPL prompt                 |`G>` matches logo mark                                                                      |
|D-056|Apr 2026|Windows Terminal            |Grob ships a Windows Terminal profile                                                       |
|D-057|Apr 2026|Terminal colours            |Denim blue, warm amber, raccoon greys                                                       |
|D-058|Apr 2026|Personality                 |Three modes: seasoned engineer, enthusiastic teacher, scrappy builder                       |
|D-059|Apr 2026|Error messages              |Helpful — what, where, why, suggested fix                                                   |
|D-060|Apr 2026|First run                   |`✦ First script. Nice work.` Celebrated once, never repeated                                |
|D-061|Apr 2026|Opinions                    |`snake_case` warned not errored; nil safety is non-negotiable *(snake_case warning superseded by D-283)*|
|D-062|Apr 2026|Formatter                   |`grob fmt`; never automatic, always opt-in                                                  |
|D-063|Apr 2026|CLI output                  |Quiet on success; errors to stderr; results to stdout                                       |
|D-064|Apr 2026|Never list                  |No emoji in CLI output; never “simply” in docs                                              |
|D-065|Apr 2026|AI tutor                    |Deferred idea; parked in grob-personality-identity.md                                       |
|D-066|Apr 2026|Primitive model             |Primitives never boxed; method-call syntax is compile-time sugar                            |
|D-067|Apr 2026|Method syntax               |All types support method-call syntax                                                        |
|D-068|Apr 2026|Properties vs methods       |`length`, `isEmpty` etc. are properties — no `()`                                           |
|D-069|Apr 2026|Conversion rule             |Conversions are methods on the source type                                                  |
|D-070|Apr 2026|Static utilities            |Functions with no receiver live on the type namespace                                       |
|D-071|Apr 2026|One rule                    |Conversions on source value; static utilities on type namespace                             |
|D-072|Apr 2026|Security posture            |Trust script author; document risks; safe path is obvious path                              |
|D-073|Apr 2026|Plugin security             |Loading a plugin is running arbitrary code; documented prominently                          |
|D-074|Apr 2026|Credential handling         |`env.require()` is the canonical credential pattern                                         |
|D-075|Apr 2026|process module naming       |`process.run()` safe form; `process.runShell()` shell form; supersedes D-076                |
|D-076|Apr 2026|process.runArgs (retired)   |`process.runArgs()` naming — superseded by D-075                                            |
|D-077|Apr 2026|Errors — no values          |Error messages show names and types, never values                                           |
|D-078|Apr 2026|Community registry          |PLUGINS.md is not a safety endorsement                                                      |
|D-079|Apr 2026|Type method registry        |Defined method set per type; undefined method = compile error                               |
|D-080|Apr 2026|OQ-001 resolved             |Constrained generics — users consume, cannot declare                                        |
|D-081|Apr 2026|Generics — plugin boundary  |Generic functions at plugin boundary via `FunctionSignature`                                |
|D-082|Apr 2026|OQ-004 resolved             |Exceptions as runtime error model; `try/catch`                                              |
|D-083|Apr 2026|try/catch                   |Multiple typed catches; bare `catch e` must appear last                                     |
|D-084|Apr 2026|Exception hierarchy         |`GrobError` root; `IoError`, `NetworkError`, `JsonError` etc. *(superseded by D-284)*       |
|D-085|Apr 2026|User-defined exceptions     |Post-MVP                                                                                    |
|D-086|Apr 2026|csv module                  |Core stdlib; headers assumed by default; RFC 4180                                           |
|D-087|Apr 2026|Named parameters            |Named parameters confirmed; only specify params differing from defaults                     |
|D-088|Apr 2026|log module                  |Core stdlib; distinct output streams; for unattended scripts                                |
|D-089|Apr 2026|regex module                |Core stdlib; regex literals `/pattern/flags`                                                |
|D-090|Apr 2026|path module                 |Core stdlib; path string manipulation, no I/O                                               |
|D-091|Apr 2026|strings module              |`strings.join()` on module; all other ops as instance methods                               |
|D-092|Apr 2026|csv module full API         |Full signatures locked                                                                      |
|D-093|Apr 2026|math module full API        |Constants, trig, random; no duplication of type-level functions                             |
|D-094|Apr 2026|log module full API         |Four levels; all to stderr; `log.setLevel()`                                                |
|D-095|Apr 2026|regex module full API       |Regex literals; `Regex` type; `Match` type; module convenience fns                          |
|D-096|Apr 2026|path module full API        |Full function set; `path.separator` constant; no I/O                                        |
|D-097|Apr 2026|First-party plugins         |`Grob.Crypto` and `Grob.Zip` in `plugins/`                                                  |
|D-098|Apr 2026|Script parameters           |`param` block; typed, defaultable; validated at compile time                                |
|D-099|Apr 2026|Param files                 |`.grobparams` key-value format; committable; readable                                       |
|D-100|Apr 2026|Param override              |CLI overrides param file values                                                             |
|D-101|Apr 2026|@secure decorator           |Handling instruction, not a type; not echoed or logged                                      |
|D-102|Apr 2026|Param decorators            |V1 set: `@secure`, `@allowed`, `@minLength`, `@maxLength`                                   |
|D-103|Apr 2026|Secure param pattern        |`@secure` params absent from `.grobparams`; supply via CLI or `env`                         |
|D-104|Apr 2026|Pipe operator               |No `|` pipe in Grob scripts; fluent chaining is the idiom                                   |
|D-105|Apr 2026|format module               |Core stdlib; human-readable output; `format.table()`, `format.list()`                       |
|D-106|Apr 2026|select() projection         |`.select()` on collections; typed; PowerShell `Select-Object` equivalent                    |
|D-107|Apr 2026|date module — type          |Single `date` type; no separate `datetime`                                                  |
|D-108|Apr 2026|date module — API           |Full API locked                                                                             |
|D-109|Apr 2026|fs module API shape         |`fs.list()` returns `File[]`; `File` built-in type; full function set                       |
|D-110|Apr 2026|Script exit                 |`exit(n)` built-in; uncatchable `ExitSignal`                                                |
|D-111|Apr 2026|Conditional expressions     |Ternary `? :` and switch expression; exhaustiveness enforced                                |
|D-112|Apr 2026|Array indexing              |`arr[n]`; zero-based; multi-dimensional `matrix[r][c]`                                      |
|D-113|Apr 2026|Named parameter convention  |Positional first; named after; only defaultable params may be named                         |
|D-114|Apr 2026|Anonymous struct literals   |`#{ field: value }` syntax; structurally typed; field access safe                           |
|D-115|Apr 2026|Lambdas and closures        |`x => expr`, `(a,b) => expr`, block form; upvalue mechanism from clox                       |
|D-116|Apr 2026|format module calling conv  |`.format.table()` chained form; compiler namespace rewrite; no boxing                       |
|D-117|Apr 2026|date interval computation   |`daysUntil()` and `daysSince()` added; `Interval` type post-MVP                             |
|D-118|Apr 2026|Grob.Http API shape         |Full REST; `Response` type; `auth` sub-namespace                                            |
|D-119|Apr 2026|string left() and right()   |`left(n)` and `right(n)` added; range indexing post-MVP                                     |
|D-120|Apr 2026|Language fundamentals       |Full spec in grob-language-fundamentals.md; decisions log wins on conflict                  |
|D-121|Apr 2026|Install scope model         |Three-tier: user-global, system, project-local                                              |
|D-122|Apr 2026|grob.json manifest walk     |Walk up from script file location, not CWD                                                  |
|D-123|Apr 2026|grob runtime install        |`winget install Grob.Grob`; `grob restore` idempotent                                       |
|D-124|Apr 2026|Nested struct field access  |Full chain resolution at compile time; undefined field = compile error                      |
|D-125|Apr 2026|Solution structure          |Six `src/` assemblies; three `plugins/`; five `tests/`; DAG dependency                      |
|D-126|Apr 2026|Type naming convention      |`Grob` prefix full — not `Gro`; ADR-0012                                                    |
|D-127|Apr 2026|String literal forms        |Three forms: double-quoted, single backtick, triple backtick                                |
|D-128|Apr 2026|Raw string newline rule     |Newline inside single backtick string is compile error                                      |
|D-129|Apr 2026|Raw string indentation      |Triple backtick verbatim; no trimming in v1                                                 |
|D-130|Apr 2026|Escape sequence set         |`\n`, `\r`, `\t`, `\\`, `\"`, `\$`; unknown = compile error                                 |
|D-131|Apr 2026|Namespace conventions       |Gerunds or adjectives; never same word as primary class                                     |
|D-132|Apr 2026|Tooling — language-config   |`language-configuration.json` in Phase 1 with TextMate grammar                              |
|D-133|Apr 2026|Tooling — TextMate grammar  |First tooling deliverable; no compiler dependency                                           |
|D-134|Apr 2026|Tooling — Grob.Lsp          |`Grob.Lsp` in solution; depends on Compiler/Core/Runtime, not Vm                            |
|D-135|Apr 2026|Tooling — VS Code extension |`tooling/Grob.VsCode/`; TypeScript; ~30 lines                                               |
|D-136|Apr 2026|Tooling — LSP handler order |Diagnostics, completions, hover, go-to-definition; semantic tokens post-MVP                 |
|D-137|Apr 2026|Compiler SourceLocation     |Every AST node carries `SourceLocation`; day-one requirement                                |
|D-138|Apr 2026|v1 Requirements Spec        |Full build spec in grob-v1-requirements.md                                                  |
|D-139|Apr 2026|input() built-in            |`input(prompt): string`; blocks on stdin; throws `IoError` on EOF                           |
|D-140|Apr 2026|Array mutation methods      |`append`, `insert`, `remove`, `clear`; mutation on `const` = compile error                  |
|D-141|Apr 2026|map<K, V> type              |First-class; string keys in v1; insertion order preserved                                   |
|D-142|Apr 2026|OQ-009 opened               |`GrobValue` provisional — tagged union, documented as provisional                           |
|D-143|Apr 2026|OQ-010 opened               |`.grobc` binary format spec needed before implementation                                    |
|D-144|Apr 2026|OQ-011 opened               |`Grob.Crypto` API shape — defer to Sprint 10 planning                                       |
|D-145|Apr 2026|OQ-012 opened               |`process.run()` timeout — defer to Sprint 9                                                 |
|D-146|Apr 2026|OQ-007 resolved             |`for...in` special-cased: ranges, arrays, maps; formal protocol post-MVP                    |
|D-147|Apr 2026|OQ-012 resolved             |`process.run()` timeout: `timeout: int = 0`; throws `ProcessError`                          |
|D-148|Apr 2026|OQ-011 resolved             |`Grob.Crypto` API shape resolved; stream-based file hashing                                 |
|D-149|Apr 2026|guid module                 |Core stdlib; `guid` primitive type; `newV4`, `newV7`, `newV5`                               |
|D-150|Apr 2026|fs.copy/fs.move overwrite   |`overwrite: bool = false` on copy/move functions and instance methods                       |
|D-151|Apr 2026|Script 11 validation        |Azure Resource Provisioning Helper added to validation suite                                |
|D-152|Apr 2026|Grob.Zip API shape          |Three `zip.create()` overloads; `zip.extract`; `zip.list`; `ZipEntry`                       |
|D-153|Apr 2026|env module full API         |`get`, `require`, `set`, `has`, `all`                                                       |
|D-154|Apr 2026|format module full API      |Returns `string`; `format.table`, `format.list`, `format.csv`; auto-sizing                  |
|D-155|Apr 2026|Grob.Http locked signatures |Full HTTP verb signatures; `http.download` throws on non-2xx                                |
|D-156|Apr 2026|json.encode() added         |Serialises any typed value to JSON string                                                   |
|D-157|Apr 2026|json.Node full spec         |`node["key"]` indexer; accessors; type predicates                                           |
|D-158|Apr 2026|Response type full spec     |`statusCode`, `isSuccess`, `headers`, `asText()`, `asJson()`                                |
|D-159|Apr 2026|AuthHeader type full spec   |Opaque; `toString()` returns `"[AuthHeader]"`                                               |
|D-160|Apr 2026|ProcessResult type full spec|`stdout`, `stderr`, `exitCode`; `toString()` returns stdout                                 |
|D-161|Apr 2026|Escape sequence set updated |`\r` added; full set confirmed                                                              |
|D-162|Apr 2026|Numeric type precision      |`int` = 64-bit signed; `float` = 64-bit IEEE 754                                            |
|D-163|Apr 2026|Integer overflow            |Checked arithmetic; overflow throws `RuntimeError`                                          |
|D-164|Apr 2026|Implicit type coercion      |Only `int` → `float`; all else explicit                                                     |
|D-165|Apr 2026|Trailing commas             |Permitted in all comma-separated lists; never required                                      |
|D-166|Apr 2026|Forward references          |Two-pass type checker; forward refs between top-level declarations *(extended by D-286)*    |
|D-167|Apr 2026|Variable shadowing          |Allowed; compiler emits warning                                                             |
|D-168|Apr 2026|Script structure order      |import → param → type/fn → code; violations are compile errors                              |
|D-169|Apr 2026|Equality semantics          |Value equality throughout; struct field-by-field; `==` on incompatible types = compile error|
|D-170|Apr 2026|Nil chain propagation       |`?.` short-circuits entire chain; result type always `T?`                                   |
|D-171|Apr 2026|Script-level return         |`return` at top level is compile error; use `exit()`                                        |
|D-172|Apr 2026|No multiple return values   |Functions return single value; use struct for multiple                                      |
|D-173|Apr 2026|No operator overloading     |User-defined types cannot define custom operators                                           |
|D-174|Apr 2026|No circular imports         |Scripts cannot import other scripts in v1                                                   |
|D-175|Apr 2026|json.write pretty default   |Pretty-printed by default; `compact: bool = false` on all three fns                         |
|D-176|Apr 2026|date constructors local time|`now()`, `today()`, `of()`, `ofTime()` return local time                                    |
|D-177|Apr 2026|fs.readText UTF-8 default   |UTF-8; BOM auto-detection; `writeText` writes without BOM                                   |
|D-178|Apr 2026|Map literal separator rules |Entries separated by newlines or commas; keys are string literals in v1                     |
|D-179|Apr 2026|string.toString() identity  |Returns string unchanged; every type now has `toString()`                                   |
|D-180|Apr 2026|Stack overflow behaviour    |`CallFrame[256]`; depth 257 throws `RuntimeError`                                           |
|D-181|Apr 2026|const depth                 |`const` prevents both rebinding and mutation; one rule *(superseded by D-288, D-291)*       |
|D-182|Apr 2026|Nested arrays (T[][])       |Valid; `int[][]`; no rectangular guarantee; arrays-of-arrays                                |
|D-183|Apr 2026|No tuples                   |Not in v1; use structs; post-MVP if friction observed                                       |
|D-184|Apr 2026|No out parameters           |Not in v1 and not planned; nullable returns cover the use case                              |
|D-185|Apr 2026|Try-parse pattern           |Nullable return types; `toInt() → int?`; `??` for defaults                                  |
|D-270|Apr 2026|Tokenisation — built-ins    |`print`, `exit`, `input` are built-in functions, not keywords                               |
|D-271|Apr 2026|Operator precedence         |`??` binds tighter than ternary; corrects §7 ordering                                       |
|D-272|Apr 2026|Operator precedence         |Assignment operators not in precedence table; new §28 Statement Forms                       |
|D-273|Apr 2026|Arithmetic                  |`float % float` supported with fmod semantics; `% 0.0` throws `RuntimeError`                |
|D-274|Apr 2026|Exception handling          |`try`/`catch`/`throw` grammar; typed catch, polymorphic match, catch-all form               |
|D-275|Apr 2026|Exception handling          |`finally` block on `try`; runs on all exits except `exit()`; no return/break/continue inside|
|D-276|Apr 2026|Lambdas                     |Block-body lambda: implicit last expression + `return` for early exit                       |
|D-277|Apr 2026|Expressions                 |Switch expression v1 pattern grammar: value, relational, catch-all                         |
|D-278|Apr 2026|Arithmetic                  |`int / 0` throws `RuntimeError`; no form of division by zero is silent                     |
|D-279|Apr 2026|String literals             |Nullable interpolation is a compile error; resolve with `??` or narrowing `if`             |
|D-280|Apr 2026|Pipeline methods            |Drop `map()`; `select()` is universal transformation; LINQ-for-scripting identity           |
|D-281|Apr 2026|Pipeline methods            |`sort()` key-selector only; `U: Comparable` constraint; stable; no comparator overload      |
|D-282|Apr 2026|Formatting                  |`formatAs` replaces `format`; scalar formatters move to instance methods on numeric/date    |
|D-283|Apr 2026|Compiler warnings           |Drop `snake_case` compiler warning; naming convention moves to formatter layer              |
|D-284|Apr 2026|Exception hierarchy         |`RuntimeError` split: four new typed leaves + residual; hierarchy now ten leaves            |
|D-285|Apr 2026|String literals             |Backtick raw strings canonical idiom for Windows paths and literal backslash content        |
|D-186|Apr 2026|v1 scope                   |v1 scope-cut list: validation decorators and regex literals; activation at Chris's discretion|
|D-286|Apr 2026|Forward references          |Cross-declaration reference rules; all top-level forward reference forms documented        |
|D-287|Apr 2026|Type system                 |Non-nullable type cycles are a compile error; DFS detection with three visit states        |
|D-288|Apr 2026|Variables                   |Split `const` into `const` (compile-time) and `readonly` (runtime-once)                    |
|D-289|Apr 2026|Type system                 |Definition of "compile-time constant expression"; allowed and disallowed forms              |
|D-290|Apr 2026|Variables                   |Migration rule for existing `const` bindings; mechanical RHS-kind rule                     |
|D-291|Apr 2026|Variables                   |`readonly` semantics: evaluated at declaration, never reassigned or mutated                 |
|D-292|Apr 2026|Scoping                     |`const` and `readonly` permitted at function-local scope                                    |
|D-293|Apr 2026|Implementation              |Grammar, AST and opcode impact of `readonly`; one new keyword                              |
|D-294|Apr 2026|Runtime                     |Top-level initialisation order: source order; three-state tag for circular detection        |
|D-295|Apr 2026|User-defined types          |Type field default evaluation at construction time, construction-site scope                 |
|D-296|Apr 2026|Closures                    |Four-category variable resolution in lambdas; `const` inlined, others via globals/upvalues |

-----

## Confirmed Decisions — Full Entries

-----

### D-001 — Arduino targeting ruled out (Feb 2026)

Area: Targeting
Supersedes: none
Superseded by: none

Arduino hardware targeting ruled out — transpilation to C++ is a different discipline

-----

### D-002 — General-purpose scripting chosen (Feb 2026)

Area: Purpose
Supersedes: none
Superseded by: none

General-purpose scripting chosen over domain-specific language

-----

### D-003 — Console calculator as MVP criterion (Feb 2026)

Area: MVP
Supersedes: none
Superseded by: none

Console-based non-scientific calculator as MVP success criterion

-----

### D-004 — Module/import system in scope (Feb 2026)

Area: Modules
Supersedes: none
Superseded by: none

Module/import system in scope — late phase, not an early architecture driver

-----

### D-005 — Core philosophy locked (Feb 2026)

Area: Philosophy
Supersedes: none
Superseded by: none

Core philosophy locked — “build for developers, design for hobbyists”

-----

### D-006 — Stack-based bytecode VM (Feb 2026)

Area: VM strategy
Supersedes: none
Superseded by: none

Stack-based bytecode VM as centrepiece, informed by clox (Crafting Interpreters Part III)

-----

### D-007 — Written in C# .NET (Feb 2026)

Area: Implementation
Supersedes: none
Superseded by: none

Written in C# .NET — .NET JIT compiles the VM loop to efficient native code

-----

### D-008 — Same-line braces (Feb 2026)

Area: Syntax
Supersedes: none
Superseded by: none

Same-line braces `{` — C#/Go familiar, avoids newline terminator ambiguity

-----

### D-009 — No semicolons (Feb 2026)

Area: Syntax
Supersedes: none
Superseded by: none

No semicolons — newline terminates statements, parser infers continuation

-----

### D-010 — `//` comments (Feb 2026)

Area: Syntax
Supersedes: none
Superseded by: none

`//` comments — universal C-style

-----

### D-011 — `:=` declares; `=` reassigns (Feb 2026)

Area: Variables
Supersedes: none
Superseded by: none

`:=` declares and assigns (first use). `=` reassigns (name must exist). No `var` keyword

-----

### D-012 — No uninitialised variables (Feb 2026)

Area: Variables
Supersedes: none
Superseded by: none

No uninitialised variables — every declaration requires a value or explicit `?` nil

-----

### D-013 — Mutable by default; `const` for immutable (Feb 2026)

Area: Variables
Supersedes: none
Superseded by: D-181 (partially), D-288 (partially)

> **Note:** D-288 splits the immutability surface into `const` (compile-time) and `readonly` (runtime-once). The mutable-by-default rule is unchanged; only the description of the once-assigned keyword family is superseded. Read this entry as historical context; D-288 and D-291 describe current semantics.

Mutable by default. `const` for immutable bindings

-----

### D-014 — `?` suffix for nullable types (Feb 2026)

Area: Types
Supersedes: none
Superseded by: none

`?` suffix for nullable types — `string?`, `int?`. Non-optional types guaranteed non-nil

-----

### D-015 — `??` and `?.` operators (Feb 2026)

Area: Types
Supersedes: none
Superseded by: none

`??` nil coalescing operator. `?.` optional chaining. Both C# familiar

-----

### D-016 — `fn` keyword; typed parameters (Feb 2026)

Area: Functions
Supersedes: none
Superseded by: none

`fn` keyword. Parameters typed. Return type explicit or inferred when unambiguous

-----

### D-017 — Lean on C#’s GC (Feb 2026)

Area: GC
Supersedes: none
Superseded by: none

Lean on C#’s GC. Structs for value types (int, float, bool). Classes for heap objects only

-----

### D-018 — Stdlib as `IGrobPlugin` (Feb 2026)

Area: Plugin system
Supersedes: none
Superseded by: none

Standard library implemented as `IGrobPlugin` — auto-registered at VM startup

-----

### D-019 — Type safety at plugin boundary (Feb 2026)

Area: Plugin system
Supersedes: none
Superseded by: none

Type safety enforced at plugin boundary — plugin provides signature, type checker verifies at compile time

-----

### D-020 — `.grobc` binary format (Feb 2026)

Area: Bytecode format
Supersedes: none
Superseded by: none

`.grobc` binary format. Magic number `GROB` (0x47 0x52 0x4F 0x42). Used for optional caching

-----

### D-021 — Compile in-memory and run (Feb 2026)

Area: Execution model
Supersedes: none
Superseded by: none

Primary use case: compile in-memory and run. No disk write unless explicitly requested

-----

### D-022 — Fluent chaining requires collections API (Feb 2026)

Area: Fluent syntax
Supersedes: none
Superseded by: none

Fluent chaining yes — but not day one. Requires collections API first

-----

### D-023 — C# LINQ as collections north star (Feb 2026)

Area: Collections
Supersedes: none
Superseded by: none

C# LINQ is the design north star for the collections API

-----

### D-024 — Open source; release when solid (Feb 2026)

Area: Release
Supersedes: none
Superseded by: none

Open source. Release when core is solid, not before

-----

### D-025 — `--plugin` retired; `--dev-plugin` for development (Apr 2026)

Area: Plugin loading
Supersedes: none
Superseded by: none

`--plugin` flag retired from public API. Internal mechanism only, used by `grob install`. Never a script author concern. Dev escape hatch: `--dev-plugin path/to/local.dll` for plugin development only, documented as such.

-----

### D-026 — `import` is the single non-core dependency mechanism (Apr 2026)

Area: Import statement
Supersedes: none
Superseded by: none

`import` is the single declaration mechanism for all non-core dependencies. Signals compile-time type resolution — the type checker loads plugin signatures at compile time, not runtime.

-----

### D-027 — 13 core modules auto-available (Apr 2026)

Area: Core modules
Supersedes: none
Superseded by: none

Core modules are auto-available — no import required. `fs`, `strings`, `json`, `csv`, `env`, `process`, `date`, `math`, `log`, `regex`, `path`, `format`, `guid`. If a reasonable developer expects it in any scripting language, it’s core.

-----

### D-028 — Import signal value (Apr 2026)

Area: Import signal value
Supersedes: none
Superseded by: none

A script with no imports is self-contained. A script with imports has external dependencies. `import` lines double as a dependency manifest. This signal value is lost if core modules also require import.

-----

### D-029 — Default import alias (Apr 2026)

Area: Plugin import alias
Supersedes: none
Superseded by: none

Default alias is the last segment of the module name, lowercased. `import Grob.Http` → `http.*`. Convention not configuration — always predictable. `Grob.Http` is a special case: it exposes both `http.*` and `auth.*` as sub-namespaces from a single import. This is the only case where one `import` produces two namespace prefixes.

-----

### D-030 — Explicit alias for collision resolution only (Apr 2026)

Area: Explicit alias
Supersedes: none
Superseded by: none

`import Grob.Http as client` — available for collision resolution. Not for personality. Legitimate uses: two plugins share a last segment, or a plugin alias clashes with a core module name.

-----

### D-031 — `import` over `requires` (Apr 2026)

Area: Import vs requires
Supersedes: none
Superseded by: none

`import` chosen over `requires`. Signals compile-time resolution not runtime assertion. Universally understood. Fits the statically typed identity of the language. `requires` belongs in dynamic languages.

-----

### D-032 — `grob install`; never silently downloads (Apr 2026)

Area: Package install
Supersedes: none
Superseded by: none

`grob install Grob.Http` — installs globally to `~/.grob/packages/`. `grob install --local` — installs to project only. Never silently downloads at runtime. Explicit install step always required.

-----

### D-033 — Package resolution order (Apr 2026)

Area: Package resolution
Supersedes: none
Superseded by: none

On `import`, Grob checks: (1) project `grob.json` dependencies, (2) `~/.grob/packages/`. If not found — compile error with helpful message: `Grob.Http is not installed. Run: grob install Grob.Http`

-----

### D-034 — `grob.json` project manifest (Apr 2026)

Area: Project manifest
Supersedes: none
Superseded by: none

`grob.json` — optional, for projects with multiple scripts sharing dependencies. Declares name, version, dependencies with version constraints. `grob install` with no args resolves everything in `grob.json`.

-----

### D-035 — NuGet as package registry (Apr 2026)

Area: Package registry
Supersedes: none
Superseded by: none

NuGet for hosting and distribution. Packages tagged `grob-plugin` discoverable via `grob search`. Zero infrastructure to maintain. Versioning, hosting, push all provided by NuGet ecosystem.

-----

### D-036 — `grob.json` shape (Apr 2026)

Area: grob.json shape
Supersedes: none
Superseded by: none

`{ "name": "my-project", "version": "1.0.0", "dependencies": { "Grob.Http": "^1.0.0" } }` — npm-influenced. Semantic versioning. `^` for compatible versions.

-----

### D-037 — Visitor pattern for AST (Apr 2026)

Area: AST pattern
Supersedes: none
Superseded by: none

Visitor pattern for Grob’s AST — not switch expressions. Grob has three passes (type checker, optimiser, compiler). Visitor earns its place when multiple passes walk the same AST. SharpBASIC had one pass; switch expressions were sufficient there.

-----

### D-038 — `:=` declares in current scope; `=` walks parent chain (Apr 2026)

Area: Scope
Supersedes: none
Superseded by: none

`:=` always declares in current local scope. `=` reassigns by walking the parent chain to find the name wherever it lives. No `SET GLOBAL` equivalent needed. Mandatory `:=` declaration makes chain-walking unambiguous.

-----

### D-039 — Two-mode error strategy (Apr 2026)

Area: Error strategy
Supersedes: none
Superseded by: none

Two-mode: compiler/type checker collects ALL errors before execution (never stops at first). VM stops on FIRST runtime error. A program with type errors never reaches the VM.

-----

### D-040 — Test compiler outputs exhaustively (Apr 2026)

Area: Compiler tests
Supersedes: none
Superseded by: none

Test compiler outputs exhaustively — given source, assert correct bytecode. Bugs will live in the compiler, not the VM loop. VM loop can be trusted once verified on simple cases.

-----

### D-041 — Compiler as `partial class` files (Apr 2026)

Area: Partial classes
Supersedes: none
Superseded by: none

Grob’s compiler implemented as `partial class` files for physical separation of concerns. Same namespace, same architecture, better maintainability.

-----

### D-042 — Real-program target required before implementation (Apr 2026)

Area: Real program target
Supersedes: none
Superseded by: none

Grob needs a real-program target defined before implementation begins — not after. The Sunken Crown was the most valuable design tool in SharpBASIC. Real programs reveal language gaps that toy programs hide.

-----

### D-043 — OQ-002 resolved: user-defined struct types (Apr 2026)

Area: OQ-002 resolved
Supersedes: none
Superseded by: none

User-defined struct/record types confirmed. Evidence: parallel arrays in The Sunken Crown were “messy, wasteful, and slow.” `type` keyword, structural types, fields declared in block.

-----

### D-044 — No empty semantic analyser placeholder (Apr 2026)

Area: Semantic analyser
Supersedes: none
Superseded by: none

No empty semantic analyser placeholder. At SharpBASIC’s scale it added nothing. For Grob — statically typed — the type-checking pass is the semantic analyser and earns its place explicitly.

-----

### D-045 — Real-world target use cases (Apr 2026)

Area: Use cases
Supersedes: none
Superseded by: none

Real-world target: Azure CLI/Bicep scripting, API wrapping (ADO), agent hook scripts

-----

### D-046 — Pipeline model (Apr 2026)

Area: Pipeline model
Supersedes: none
Superseded by: none

Grob scripts are composable pipeline stages: structured data in → process → structured data out. Primary input pattern is file-read (`json.read()`, `csv.read()`) — portable across all OSs. `json.stdin()` and `csv.stdin()` exist for genuine shell pipeline composition. Examples lead with file-read; stdin shown as the pipeline variant. Target environment is Windows-native — `cat` never appears in Grob documentation or examples.

-----

### D-047 — String interpolation `${name}` (Apr 2026)

Area: String interpolation
Supersedes: none
Superseded by: none

`"Hello ${name}"` — confirmed load-bearing from real-world script sketches. Not optional

-----

### D-048 — MIT licence (Apr 2026)

Area: Licensing
Supersedes: none
Superseded by: none

MIT licence. Maximum permissiveness. No copyleft. Standard for hobbyist scripting languages

-----

### D-049 — First-party plugins in `plugins/` (Apr 2026)

Area: Open source model
Supersedes: none
Superseded by: none

Core runtime in main repo. First-party plugins in `plugins/` directory of the main repo

-----

### D-050 — Community plugin registry via `PLUGINS.md` PR (Apr 2026)

Area: Community plugins
Supersedes: none
Superseded by: none

Independent repos. Registry via `PLUGINS.md` PR — low bar (repo exists, README, licence)

-----

### D-051 — `Grob.Runtime` NuGet package (Apr 2026)

Area: Plugin SDK
Supersedes: none
Superseded by: none

`Grob.Runtime` NuGet package — public contract for third-party plugin authors. Versioned independently

-----

### D-052 — Fork → branch → PR; CLA on first PR (Apr 2026)

Area: Contributions
Supersedes: none
Superseded by: none

Fork → branch → PR. CLA via one-time confirmation on PR submission. No separate document

-----

### D-053 — Sparky the mascot (Apr 2026)

Area: Mascot
Supersedes: none
Superseded by: none

Sparky — raccoon, blue hoodie, utility belt, wrench. Character sheet v1 complete

-----

### D-054 — `G>` logo mark (Apr 2026)

Area: Logo mark
Supersedes: none
Superseded by: none

`G>` — forward chevron on G. Works at 32px. Used as favicon, badge, terminal prompt, laptop lid detail

-----

### D-055 — `G>` REPL prompt (Apr 2026)

Area: REPL prompt
Supersedes: none
Superseded by: none

`G>` — matches the logo mark. Every REPL line is Sparky’s world

-----

### D-056 — Windows Terminal profile (Apr 2026)

Area: Windows Terminal
Supersedes: none
Superseded by: none

Grob ships a Windows Terminal profile — name, icon, colour scheme, `grob repl` as startup command

-----

### D-057 — Terminal colour scheme (Apr 2026)

Area: Terminal colours
Supersedes: none
Superseded by: none

Denim blue as accent, warm amber for warnings, raccoon greys for background

-----

### D-058 — Three-mode personality (Apr 2026)

Area: Personality
Supersedes: none
Superseded by: none

Three modes, one character — seasoned engineer (errors), enthusiastic teacher (learning), scrappy builder (flow)

-----

### D-059 — Helpful, explanatory error messages (Apr 2026)

Area: Error messages
Supersedes: none
Superseded by: none

Helpful and explanatory — what went wrong, where, why, suggested fix when obvious

-----

### D-060 — First-run acknowledgement (Apr 2026)

Area: First run
Supersedes: none
Superseded by: none

Celebrated once with a quiet acknowledgement. Never repeated. `✦ First script. Nice work.`

-----

### D-061 — Opinionated defaults (Apr 2026)

Area: Opinions
Supersedes: none
Superseded by: D-283 (in part — snake_case warning dropped)

> **Note:** D-283 drops the `snake_case` compiler warning. All other opinionated defaults (nil safety as non-negotiable error, unused variable warnings, shadowing warnings) are unchanged. The snake_case opinion moves to `grob fmt` and documentation.

Opinionated defaults — `snake_case` warned not errored. Nil safety and types are non-negotiable errors

-----

### D-062 — `grob fmt` opt-in formatter (Apr 2026)

Area: Formatter
Supersedes: none
Superseded by: none

`grob fmt` — formats code. Never automatic, always opt-in

-----

### D-063 — CLI output philosophy (Apr 2026)

Area: CLI output
Supersedes: none
Superseded by: none

Quiet on success, clear on failure. Errors to stderr, results to stdout. Pipeline-friendly always

-----

### D-064 — Never list (Apr 2026)

Area: Never list
Supersedes: none
Superseded by: none

No emoji in compiler/CLI output. Never “simply” in docs. Never silence an error

-----

### D-065 — AI tutor deferred (Apr 2026)

Area: AI tutor
Supersedes: none
Superseded by: none

Deferred idea — guided learning companion. Parked in grob-personality-identity.md

-----

### D-066 — Primitives never boxed (Apr 2026)

Area: Primitive model
Supersedes: none
Superseded by: none

Primitives are never boxed. Method-call syntax on all types is syntactic sugar — compiler rewrites to native function calls at compile time. Zero runtime overhead

-----

### D-067 — Method-call syntax on all types (Apr 2026)

Area: Method syntax
Supersedes: none
Superseded by: none

All types support method-call syntax. `"42".toInt()`, `42.toString()`, `3.14.round()`. Compiler resolves at compile time using type information. No vtable, no heap allocation

-----

### D-068 — Properties vs methods (Apr 2026)

Area: Properties vs methods
Supersedes: none
Superseded by: none

`length`, `isEmpty` etc are properties — no `()` required. Compiler distinguishes property access from method call based on type registration

-----

### D-069 — Conversions are methods on the source type (Apr 2026)

Area: Conversion rule
Supersedes: none
Superseded by: none

Conversions are methods on the source type — convert *from* a value. `"42".toInt()` is the only syntax. `int.parse("42")` is a compile error with a helpful suggestion

-----

### D-070 — Static utilities on the type namespace (Apr 2026)

Area: Static utilities
Supersedes: none
Superseded by: none

Functions with no natural receiver live on the type as a namespace — `int.min(a, b)`, `int.max(a, b)`, `int.clamp(v, lo, hi)`. No overlap with instance methods

-----

### D-071 — One rule for conversions and utilities (Apr 2026)

Area: One rule
Supersedes: none
Superseded by: none

*Conversions are methods on the source value. Static utilities live on the type namespace. There is no overlap.* Compiler enforces this. Docs explain it in one sentence

-----

### D-072 — Security posture (Apr 2026)

Area: Security posture
Supersedes: none
Superseded by: none

Trust the script author, document the risks, make the safe path the obvious path. No sandbox claims ever made.

-----

### D-073 — Plugin loading is arbitrary code execution (Apr 2026)

Area: Plugin security
Supersedes: none
Superseded by: none

Loading a plugin is equivalent to running arbitrary code. Documented prominently in PLUGINS.md and plugin authoring guide. No .NET plugin sandboxing attempted — not worth the complexity for this use case.

-----

### D-074 — `env.require()` canonical credential pattern (Apr 2026)

Area: Credential handling
Supersedes: none
Superseded by: none

`env.require()` is the canonical pattern for credentials. Never hardcode in script source. `env` module docs are explicit. `grob check` may optionally warn on string literals matching common token patterns — linter concern, not compiler concern.

-----

### D-075 — `process.run()` naming (Apr 2026)

Area: process module naming
Supersedes: D-076
Superseded by: none

`process.run(cmd, args[])` is the primary safe form — arguments are never shell-interpolated, prevents command injection. `process.runShell(cmd)` is the convenience form for full command strings where shell interpretation is wanted — name makes the shell involvement explicit. Fail-fast variants: `process.runOrFail(cmd, args[])` and `process.runShellOrFail(cmd)`. Returns `ProcessResult` with `stdout: string`, `stderr: string`, `exitCode: int`. The safe path has the shorter name. Supersedes the earlier `process.runArgs()` entry.

-----

### D-076 — `process.runArgs()` naming (Apr 2026) — SUPERSEDED

Area: process module naming
Supersedes: none
Superseded by: D-075

*(This entry is superseded by D-075. `process.runArgs()` is not the correct naming — see D-075.)*

-----

### D-077 — Error messages show names, not values (Apr 2026)

Area: Errors — no values
Supersedes: none
Superseded by: none

Error messages show variable names and types, never values. Prevents accidental credential exposure in terminal output and logs. `--verbose` flag overrides for debugging.

-----

### D-078 — Community registry is not a safety endorsement (Apr 2026)

Area: Community registry
Supersedes: none
Superseded by: none

PLUGINS.md registry listing is not a safety endorsement. Explicit warning in registry: loading a plugin is running arbitrary code. Quality and security are the author’s responsibility.

-----

### D-079 — Type method registry (Apr 2026)

Area: Type method registry
Supersedes: none
Superseded by: none

Each built-in type has a defined set of methods and properties known to the type checker at compile time. Calling an undefined method is a compile error, not a runtime error

-----

### D-080 — OQ-001 resolved: constrained generics (Apr 2026)

Area: OQ-001 resolved
Supersedes: none
Superseded by: none

Constrained generics confirmed. Type checker and compiler understand generic type parameters internally. Users consume generic functions via stdlib and plugins (`mapAs<T>()`, `filter`, `map` etc) but cannot declare generic functions or types in v1. Evolution to user-facing generics is additive — grammar extension only, no architectural rework required.

-----

### D-081 — Generics at plugin boundary (Apr 2026)

Area: Generics — plugin boundary
Supersedes: none
Superseded by: none

Plugins that expose generic functions must express type parameters via `FunctionSignature` in `Grob.Runtime`. This must be designed into `Grob.Runtime` from the start, not retrofitted.

-----

### D-082 — OQ-004 resolved: exceptions as runtime error model (Apr 2026)

Area: OQ-004 resolved
Supersedes: none
Superseded by: none

Exceptions as the runtime error model. Functions throw on failure. Unhandled exceptions propagate to the VM top level — Grob-quality diagnostic produced, script halts. `try/catch` available for recovery when needed.

-----

### D-083 — try/catch structure (Apr 2026)

Area: try/catch
Supersedes: none
Superseded by: none

Multiple catch blocks supported. Typed catches supported. Bare `catch e` is the catch-all — must appear last. A catch block after a catch-all is a compiler error, not a warning.

-----

### D-084 — Exception hierarchy (Apr 2026)

Area: Exception hierarchy
Supersedes: none
Superseded by: D-284

> **Superseded by D-284.** The six-leaf hierarchy is expanded to ten leaves. `RuntimeError` is split into `ArithmeticError`, `IndexError`, `ParseError`, `LookupError`, and a residual `RuntimeError`. See D-284 for the authoritative hierarchy.

Exception type hierarchy is a `Grob.Runtime` concern — stdlib, not language grammar. V1 hierarchy: `GrobError` as root, with leaves `IoError`, `NetworkError`, `JsonError`, `ProcessError`, `NilError`, `RuntimeError`.

-----

### D-085 — User-defined exceptions post-MVP (Apr 2026)

Area: User-defined exceptions
Supersedes: none
Superseded by: none

User-defined exception types are post-MVP. Throwing custom typed errors is a programming language feature, not a scripting language feature. The use case is rare enough in scripting that deferring costs nothing.

-----

### D-086 — `csv` is core stdlib (Apr 2026)

Area: csv module
Supersedes: none
Superseded by: none

`csv` is core stdlib alongside `json`. Headers assumed by default. Overrides via named parameters: `hasHeaders: false`, `delimiter: "\t"`. RFC 4180 compliance baseline. Same `mapAs<T>()` pipeline pattern as `json`.

-----

### D-087 — Named parameters confirmed (Apr 2026)

Area: Named parameters
Supersedes: none
Superseded by: none

Named parameters confirmed as a language feature. First surfaced in the `csv` API. Only specify parameters that differ from defaults. No options object, no builder pattern.

-----

### D-088 — `log` is core stdlib (Apr 2026)

Area: log module
Supersedes: none
Superseded by: none

`log` is core stdlib. Distinct output streams for info, warning, error. Designed for unattended scripts — scheduled tasks, agent hooks, CI pipelines. Not a substitute for `print()` — structured diagnostic output.

-----

### D-089 — `regex` is core stdlib (Apr 2026)

Area: regex module
Supersedes: none
Superseded by: none

`regex` is core stdlib. Regular expressions for match, replace, extract. Distinct from `strings` simple operations. Sysadmins reach for regex constantly — log parsing, filename matching, data extraction.

-----

### D-090 — `path` is core stdlib (Apr 2026)

Area: path module
Supersedes: none
Superseded by: none

`path` is core stdlib. Path string manipulation — join, split, extension, directory, normalise separators. Distinct from `fs` file system operations. Complements `fs` — always needed alongside it.

-----

### D-091 — `strings` module — full API (Apr 2026)

Area: strings module
Supersedes: none
Superseded by: none

`strings` module contains one function: `strings.join(parts: string[], separator: string = "") → string`. All other string operations are instance methods on the `string` type. `strings.join()` lives on the module because its receiver is an array, not a string instance. The `string` type has no `strings.split()` complement on the module — `"value".split(sep)` is already an instance method on the type. The following methods are confirmed additions to the `string` type registry: `trimStart()`, `trimEnd()`, `substring(start: int, length: int)`, `indexOf(s: string)`, `lastIndexOf(s: string)`, `padLeft(width: int, char: string = " ")`, `padRight(width: int, char: string = " ")`, `repeat(count: int)`, `truncate(maxLength: int, suffix: string = "...")`.

-----

### D-092 — `csv` module — full API (Apr 2026)

Area: csv module full API
Supersedes: none
Superseded by: none

`csv.read(path: string, hasHeaders: bool = true, delimiter: string = ",") → csv.Table` reads a file; throws `IoError` on failure. `csv.parse(content: string, hasHeaders: bool = true, delimiter: string = ",") → csv.Table` for in-memory strings. `csv.stdin(hasHeaders: bool = true, delimiter: string = ",") → csv.Table` for pipeline input. `csv.write(path: string, rows: T[], hasHeaders: bool = true, delimiter: string = ",") → void` writes to file. `csv.stdout(rows: T[], hasHeaders: bool = true, delimiter: string = ",") → void` writes to stdout. `csv.Table` type exposes: `headers: string[]`, `rowCount: int`, `rows: CsvRow[]`, `mapAs<T>() → T[]`. `CsvRow` supports `get(name: string) → string`, `get(index: int) → string`, and `row[name]` / `row[index]` indexer syntax. RFC 4180 baseline: quoted fields, embedded commas, embedded newlines, `""` escape for double-quote. `hasHeaders` defaults true. `csv.stdin()` and `csv.stdout()` are valid on all platforms — primary usage on Windows is file-based (`csv.read()`/`csv.write()`); stdin/stdout forms are for pipeline composition and agent use cases. `csv.parse()` closes the in-memory parsing gap (e.g. CSV-formatted process output).

-----

### D-093 — `math` module — full API (Apr 2026)

Area: math module full API
Supersedes: none
Superseded by: none

Constants: `math.pi`, `math.e`, `math.tau`. Functions: `math.sqrt(n: float) → float` (throws `RuntimeError` if n < 0), `math.pow(base: float, exp: float) → float`, `math.log(n: float) → float` (natural log; throws `RuntimeError` if n ≤ 0), `math.log10(n: float) → float`. Trigonometry (radians): `math.sin()`, `math.cos()`, `math.tan()`, `math.asin()`, `math.acos()`, `math.atan()`, `math.atan2(y, x)`, `math.toRadians(degrees: float) → float`, `math.toDegrees(radians: float) → float`. Random: `math.random() → float` ([0.0, 1.0), uniform), `math.randomInt(min: int, max: int) → int` (inclusive both ends), `math.randomSeed(seed: int) → void` (deterministic testing). `math` does NOT duplicate `abs`, `floor`, `ceil`, `round`, `clamp`, `min`, or `max` — those live on the type registry as instance or static methods. No overlap with type-level functions by design.

-----

### D-094 — `log` module — full API (Apr 2026)

Area: log module full API
Supersedes: none
Superseded by: none

Four levels: `log.debug(message: string)`, `log.info(message: string)`, `log.warning(message: string)`, `log.error(message: string)`. All output to stderr. `print()` is stdout for script results; `log.*` is stderr for operational messages — these never mix. `log.debug()` suppressed by default; visible only under `--verbose`. Output format: `[LEVEL]  message` with no timestamp by default. `log.setLevel(level: string) → void` sets runtime threshold — accepts `"debug"`, `"info"`, `"warning"`, `"error"`; suppresses all levels below the threshold. `log.error()` logs only — it does not throw or halt execution. To halt a script on error, combine with `exit(1)` or `throw`. File output is not in scope for v1. No structured/JSON logging in v1.

-----

### D-095 — `regex` module — full API (Apr 2026)

Area: regex module full API
Supersedes: none
Superseded by: none

Regex literals: `/pattern/flags` — creates a `Regex` value, compiled once at declaration. Supported flags: `i` (case-insensitive), `m` (multiline `^`/`$`). `Regex` type methods: `match(input: string) → Match?`, `matchAll(input: string) → Match[]`, `isMatch(input: string) → bool`, `replace(input: string, replacement: string) → string`, `replaceAll(input: string, replacement: string) → string`, `split(input: string) → string[]`. `Regex` type properties: `pattern: string`, `flags: string`. `Match` type: `value: string`, `index: int`, `length: int`, `groups: string[]` (index 0 is full match, 1+ are capture groups), `group(name: string) → string?` for named groups. Module-level convenience functions for one-shot use — take string patterns, compile on each call: `regex.isMatch(pattern, input)`, `regex.match(pattern, input)`, `regex.matchAll(pattern, input)`, `regex.replace(pattern, input, replacement)`, `regex.replaceAll(pattern, input, replacement)`, `regex.split(pattern, input)`, `regex.escape(input: string) → string`. .NET regex engine — full feature set exposed including named groups and lookaheads. String literals are never implicitly treated as regex patterns. Regex literal syntax is a grammar addition — `/` is disambiguated by context (after an operator, assignment, or opening paren it is a regex literal; after a value it is the division operator).

-----

### D-096 — `path` module — full API (Apr 2026)

Area: path module full API
Supersedes: none
Superseded by: none

Functions: `path.join(parts: string...) → string` (variadic, OS separator, normalises separators in each segment), `path.joinAll(parts: string[]) → string` (array form for dynamic segment lists), `path.extension(p: string) → string` (lowercased, includes dot; empty string if none), `path.filename(p: string) → string` (final segment including extension), `path.stem(p: string) → string` (final segment without extension), `path.directory(p: string) → string` (parent directory portion), `path.resolve(p: string) → string` (absolute path relative to CWD — no I/O, does not check existence), `path.normalise(p: string) → string` (OS separator convention, collapses `..` and `.`), `path.isAbsolute(p: string) → bool`, `path.isRelative(p: string) → bool`, `path.changeExtension(p: string, ext: string) → string` (ext should include dot). Constant: `path.separator → string` (OS-dependent: `\` on Windows, `/` on POSIX). No file system I/O — `path` operates on strings only. Complements `File` type properties: `File.extension` etc. are convenience on known file objects; `path.*` functions operate on arbitrary path strings from any source (process output, config files, user input).

-----

### D-097 — First-party plugins: `Grob.Crypto` and `Grob.Zip` (Apr 2026)

Area: First-party plugins
Supersedes: none
Superseded by: none

`Grob.Crypto` — checksums and hashing (MD5, SHA256, file integrity). `Grob.Zip` — compress and expand zip archives. Both first-party plugins, not core. Both live in `plugins/` in the main repo.

-----

### D-098 — Script `param` block (Apr 2026)

Area: Script parameters
Supersedes: none
Superseded by: none

Scripts declare parameters in a `param` block at the top. Typed, defaultable. Required params have no default. `param env: string` / `param dryRun: bool = false`. Type checker validates at compile time — wrong type or missing required param is a compile error before execution.

-----

### D-099 — `.grobparams` file format (Apr 2026)

Area: Param files
Supersedes: none
Superseded by: none

`.grobparams` file format — key-value pairs, `//` comments, native Grob feel. Not JSON, not YAML, not TOML. `grob run deploy.grob --params deploy.grobparams`. Committable to source control. Readable and diffable by design.

-----

### D-100 — CLI overrides param file (Apr 2026)

Area: Param override
Supersedes: none
Superseded by: none

Command line overrides param file values. `grob run deploy.grob --params deploy.grobparams --env staging`. Param file provides defaults; command line overrides specifics. Bicep-style composability.

-----

### D-101 — `@secure` decorator (Apr 2026)

Area: @secure decorator
Supersedes: none
Superseded by: none

`@secure` on a param is a handling instruction, not a type. Value is still `string` at runtime. Effect: not echoed in output, not included in error messages, not logged. Compiler warns if a `@secure` param appears in a `.grobparams` file in plain text. No `securestring` type — decorator approach avoids type system complexity for a scripting language.

-----

### D-102 — Param decorator set (Apr 2026)

Area: Param decorators
Supersedes: none
Superseded by: none

Decorator system on params confirmed. V1 set: `@secure`, `@allowed(...)`, `@minLength(n)`, `@maxLength(n)`. Validated at the parameter boundary before the script body runs. Compiler error on violation — not runtime.

-----

### D-103 — Secure param absent from `.grobparams` (Apr 2026)

Area: Secure param pattern
Supersedes: none
Superseded by: none

`@secure` params should be absent from `.grobparams` files — provide via command line or `env.require()` instead. Tooling warns if a `@secure` param is present in plain text in a params file. `env.require()` remains the canonical pattern for credentials in scripts.

-----

### D-104 — No pipe operator in Grob scripts (Apr 2026)

Area: Pipe operator
Supersedes: none
Superseded by: none

No `|` pipe operator inside Grob scripts. Fluent chaining is the in-script composition idiom. Scripts are pipeline stages at the OS level via stdin/stdout — not internally. `|` is not a valid operator in the grammar.

-----

### D-105 — `format` is core stdlib (Apr 2026)

Area: format module
Supersedes: none
Superseded by: none

`format` is core stdlib. Human-readable output formatters distinct from `json.stdout()` (machine-readable). `format.table()`, `format.list()`, `format.csv()`. Numeric and date formatting. Works fluently after `.select()` projection on collections.

-----

### D-106 — `.select()` projection (Apr 2026)

Area: select() projection
Supersedes: none
Superseded by: none

`.select()` on collections maps to a typed projection — pick fields, optionally rename. PowerShell `Select-Object` equivalent but typed. `results.select(r => #{ repo: r.name, stale: r.staleCount }).format.table()`. Filter first, select fields, then format.

-----

### D-107 — Single `date` type (Apr 2026)

Area: date module — type
Supersedes: none
Superseded by: none

Single `date` type holds both date and time. `date.today()` zeroes the time component. No separate `datetime` type — two types is a common source of conversion friction. One type, two constructors.

-----

### D-108 — `date` module — full API (Apr 2026)

Area: date module — API
Supersedes: none
Superseded by: none

Full date/time API locked. Construction: `date.now()`, `date.today()`, `date.of(y,m,d)`, `date.ofTime(y,m,d,h,min,s)`. Parsing: `date.parse(str)` ISO 8601 default, `date.parse(str, pattern)` explicit. Formatting: `toIso()`, `toIsoDateTime()`, `format(pattern)`. Arithmetic: `addDays()`, `minusDays()`, `addMonths()`, `addHours()`, `addMinutes()`. Comparison: `<`, `>`, `==`, `isBefore()`, `isAfter()`. Components: `year`, `month`, `day`, `hour`, `minute`, `second`, `dayOfWeek`, `dayOfYear` as properties. Epoch: `toUnixSeconds()`, `toUnixMillis()`, `date.fromUnixSeconds(n)`, `date.fromUnixMillis(n)`. Timezone: `toUtc()`, `toLocal()`, `toZone("Europe/London")`, `utcOffset` property. Zone names preferred; offset integers supported for API interop.

-----

### D-109 — `fs` module API shape (Apr 2026)

Area: fs module — API shape
Supersedes: none
Superseded by: none

`fs.list(path)` returns `File[]`. `File` is a built-in type known to the type checker at compile time — registered by the fs stdlib plugin at startup, same mechanism as `date`. Properties: `name`, `path`, `directory`, `extension`, `size`, `modified`, `created`, `isDirectory`. Methods: `rename()`, `moveTo()`, `copyTo()`, `delete()`. Module functions: `fs.list()`, `fs.exists()`, `fs.isFile()`, `fs.isDirectory()`, `fs.ensureDir()`, `fs.createDir()`, `fs.delete()`, `fs.deleteRecursive()`, `fs.readText()`, `fs.readLines()`, `fs.writeText()`, `fs.appendText()`, `fs.copy()`, `fs.move()`. Full signatures in `grob-stdlib-reference.md`. Calling undefined members on `File` is a compile error.

-----

### D-110 — `exit()` built-in (Apr 2026)

Area: Script exit
Supersedes: none
Superseded by: none

`exit(n)` is a built-in function — no namespace, always available, same category as `print()`. `exit()` with no argument exits with code 0. Normal script completion exits with 0. Unhandled `GrobError` exits with 1. When called inside a function, `exit()` throws an uncatchable internal `ExitSignal` that unwinds the entire call stack — it cannot be caught by `try/catch`. The VM catches it at the top level, flushes output buffers, and terminates with the specified code.

-----

### D-111 — Conditional expressions (Apr 2026)

Area: Conditional expressions
Supersedes: none
Superseded by: none

Two conditional expression forms. Ternary `? :` for simple two-way inline choices. Switch expression for multi-branch value selection, C# style: `value switch { pattern => result, _ => default }`. `_` is the catch-all arm. Type checker enforces exhaustiveness — missing catch-all is a compile error. All arms must return the same type. No `if/else` in expression position.

-----

### D-112 — Array indexing (Apr 2026)

Area: Array indexing
Supersedes: none
Superseded by: none

`arr[n]` confirmed as array access syntax. `()` is function calls; `[]` is indexing — no overlap. Parser produces `IndexExpression` for `name[...]` and `CallExpression` for `name(...)` independently. Multi-dimensional: `matrix[r][c]`. Zero-based.

-----

### D-113 — Named parameter calling convention (Apr 2026)

Area: Named parameter calling convention
Supersedes: none
Superseded by: none

Positional arguments first (in declaration order), named arguments after. Named arguments are unordered relative to each other. Only parameters with default values may be named — required parameters (no default) are positional-only. Providing a named argument before a positional, naming a required parameter, duplicate names, or unknown parameter names are all compile errors.

-----

### D-114 — Anonymous struct literals (Apr 2026)

Area: Anonymous struct literals
Supersedes: none
Superseded by: none

`#{ field: value }` syntax distinguishes anonymous structs from block syntax `{ }`. The type checker creates an internal structural type for each anonymous struct. Field access is type-safe; accessing undefined fields is a compile error. `select()` and `map()` returning anonymous structs produce typed arrays. `format.table()` reads field names from the anonymous struct type at compile time.

-----

### D-115 — Lambdas and closures (Apr 2026)

Area: Lambdas and closures
Supersedes: none
Superseded by: none

Lambda syntax: `x => expression`, `x => { block }`, `(a, b) => expression`. Closures supported — upvalue mechanism follows clox design. Each capturing lambda becomes a `Closure` object at runtime — a `BytecodeFunction` plus its upvalue array. Compiler emits `CAPTURE_UPVALUE` instructions. `{ }` after a lambda arrow is always a block body; `#{ }` is always an anonymous struct literal — no parser ambiguity.

-----

### D-116 — `format` module calling convention (Apr 2026)

Area: format module — calling convention
Supersedes: none
Superseded by: none

`.format.table()` chained form is canonical. The compiler treats `.format` as a known namespace prefix — not a runtime property. Rewrites to `format.table(x)` at compile time. No boxing. Type checker registers `T[].format.table()`, `T[].format.list()`, `T[].format.csv()` for array types. Column names derived from type’s field registry at compile time.

-----

### D-117 — `date` interval computation (Apr 2026)

Area: date — interval computation
Supersedes: none
Superseded by: none

`daysUntil(other: date) → int` and `daysSince(other: date) → int` added to the `date` type registry. Neither throws on direction reversal. Full `Interval`/`Duration` type deferred to post-MVP.

-----

### D-118 — `Grob.Http` API shape (Apr 2026)

Area: Grob.Http — API shape
Supersedes: none
Superseded by: D-155

First-party plugin. Full REST support. Returns `Response` type with `statusCode`, `isSuccess`, `headers`, `asText()`, `asJson()`. `auth` is a sub-namespace of `Grob.Http` — `import Grob.Http` makes both `http.*` and `auth.*` available. `auth.bearer()`, `auth.basic()`, `auth.apiKey()`. `AuthHeader` is an opaque type. Full signatures in `grob-stdlib-reference.md`.

-----

### D-119 — `string.left()` and `string.right()` (Apr 2026)

Area: string — left() and right()
Supersedes: none
Superseded by: none

`left(n: int) → string` and `right(n: int) → string` added to the `string` type registry. Both throw `RuntimeError` if `n > length`. Range/span indexing deferred to post-MVP.

-----

### D-120 — Language fundamentals document authorised (Apr 2026)

Area: Language fundamentals
Supersedes: none
Superseded by: none

Full specification in `grob-language-fundamentals.md`. All decisions in that document are authorised here — the decisions log remains the authority on conflict.

-----

### D-121 — Three-tier install scope (Apr 2026)

Area: Install scope model
Supersedes: none
Superseded by: none

Three-tier plugin install scope. User-global (default): `%USERPROFILE%\.grob\packages\`. System-wide: `%ProgramFiles%\Grob\packages\`. Project-local: `.grob\packages\` relative to `grob.json`. Resolution order: local → user → system. Full detail in `grob-install-strategy.md`.

-----

### D-122 — `grob.json` manifest walk (Apr 2026)

Area: grob.json manifest walk
Supersedes: none
Superseded by: none

Grob walks up the directory tree from the script file’s location to find `grob.json` — not from the current working directory. Walk stops at the filesystem root.

-----

### D-123 — Runtime install via `winget` (Apr 2026)

Area: grob runtime install
Supersedes: none
Superseded by: none

Runtime (`grob.exe`) delivered via `winget install Grob.Grob`. User install: `%USERPROFILE%\.grob\bin\`. System install: `%ProgramFiles%\Grob\bin\`. `grob restore` installs all `grob.json` dependencies — idempotent, CI-safe.

-----

### D-124 — Nested struct field access (Apr 2026)

Area: Nested struct field access
Supersedes: none
Superseded by: none

Field access chains on nested named types are fully supported. `issue.user.login` where `Issue` declares `user: IssueUser` is valid — type checker resolves each step against the declared field type. Accessing an undeclared field at any level is a compile error.

-----

### D-125 — Solution structure (Apr 2026)

Area: Solution structure
Supersedes: none
Superseded by: none

Six `src/` assemblies: `Grob.Core`, `Grob.Runtime`, `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Cli`. Three `plugins/` assemblies: `Grob.Http`, `Grob.Crypto`, `Grob.Zip`. Five `tests/` projects. Dependency graph is a DAG — compiler and VM never reference each other. `Chunk` lives in `Grob.Core` as the shared boundary between compiler output and VM input. See `grob-solution-architecture.md` and ADR-0012.

-----

### D-126 — `Grob` prefix convention; no `Gro` abbreviation (Apr 2026)

Area: Type naming convention
Supersedes: none
Superseded by: none

The naming prefix for all Grob runtime types is `Grob` in full. `Gro` as an abbreviation is not a convention in this codebase. Correct: `GrobType`, `GrobValue`, `GrobError`, `GrobVM`, `GrobFunction`. Early design notes containing `GroType` are superseded — treat as `GrobType` throughout. See ADR-0012.

-----

### D-127 — Three string literal forms (Apr 2026)

Area: String literal forms
Supersedes: none
Superseded by: none

Three string forms mapping to distinct developer intent. (1) Double-quoted `"..."` — standard form, escape sequences (`\n`, `\t`, `\\`, `\"`, `\$`), `${expr}` interpolation, single-line only. (2) Single backtick ``...`` — inline raw, no escape processing, no interpolation, no newlines (compile error), cannot contain a backtick. Intended for Windows paths, regex patterns, short verbatim values. (3) Triple backtick ````...```` — multiline verbatim block, no escape processing, no interpolation, newlines and whitespace preserved verbatim, no trimming. Intended for SQL, JSON templates, multiline command strings. May contain single backticks; cannot contain three consecutive backticks. Single-quoted strings are not valid.

-----

### D-128 — Raw string newline rule (Apr 2026)

Area: Raw string newline rule
Supersedes: none
Superseded by: none

A newline before the closing delimiter of a single backtick string is a compile error. Developer intent for single backtick is inline raw — spanning lines signals a missing closing delimiter. Triple backtick is the explicit multiline form. The two forms do not overlap. Line continuation rules are irrelevant inside any string literal — the lexer is in string-scanning mode, not token-scanning mode.

-----

### D-129 — Raw string indentation (Apr 2026)

Area: Raw string indentation
Supersedes: none
Superseded by: none

Triple backtick string content is verbatim — no indentation trimming in v1. Content begins immediately after the opening `````. Leading whitespace and newlines are part of the string value. Revisit post-MVP if friction is observed. C#-style trim-to-closing-delimiter is the reference model if trimming is added later.

-----

### D-130 — Escape sequence set (Apr 2026)

Area: Escape sequence set
Supersedes: none
Superseded by: D-161

Confirmed escape sequences for double-quoted strings: `\n` (newline), `\r` (carriage return), `\t` (tab), `\\` (backslash), `\"` (double quote), `\$` (literal dollar — prevents interpolation trigger). `\r` is needed for explicit `\r\n` Windows line endings. `\$` is load-bearing: without it a literal `$` in a double-quoted string cannot be expressed. Any unrecognised `\x` sequence is a compile error — no silent pass-through. Raw strings (single and triple backtick) process no escape sequences — backslash is a literal character.

-----

### D-131 — Namespace conventions (Apr 2026)

Area: Namespace conventions
Supersedes: none
Superseded by: none

Namespaces are gerunds or adjectives — never the same word as the primary class they contain. Prevents `Grob.Compiler.Lexer.Lexer` and similar clashes (SharpBASIC retrospective lesson). Canonical map: `Grob.Compiler.Lexing` → `Lexer`, `Token`, `TokenType`, `LexError`; `Grob.Compiler.Parsing` → `Parser`, `ParseError`; `Grob.Compiler.Parsing.Ast` → all AST node types; `Grob.Compiler.TypeChecking` → `TypeChecker`, `TypeRegistry`, `Symbol`; `Grob.Compiler.Emitting` → `Compiler` (bytecode emitter). `Ast` is acceptable as a namespace — no class named `Ast` exists. Rule: if the namespace and its primary class would share a name, the namespace needs a different word.

-----

### D-132 — Tooling: `language-configuration.json` (Apr 2026)

Area: Tooling — language-configuration.json
Supersedes: none
Superseded by: none

`language-configuration.json` added to Phase 1 alongside the TextMate grammar. Declares bracket pairs (`()`, `[]`, `{}`, single and triple backtick), auto-closing pairs, surrounding pairs, comment toggling (`//` and `/* */`), indentation rules (increase after `{`, decrease after `}`). Handled entirely by VS Code — no LSP, no TypeScript, no build step. Ships with the first extension release. Not the formatter — `grob fmt` remains the formatting story. This covers only editor conveniences developers expect without thinking about them.

-----

### D-133 — Tooling: TextMate grammar (Apr 2026)

Area: Tooling — TextMate grammar
Supersedes: none
Superseded by: none

TextMate grammar (`.tmLanguage.json`) is the first tooling deliverable — written before the compiler is built, ships with the first VS Code extension release. No compiler dependency. Keyword list and operator set from `grob-language-fundamentals.md` are the authoritative source.

-----

### D-134 — Tooling: `Grob.Lsp` project (Apr 2026)

Area: Tooling — Grob.Lsp
Supersedes: none
Superseded by: none

`Grob.Lsp` added to the solution as a `src/` project. Depends on `Grob.Compiler`, `Grob.Core`, `Grob.Runtime`. Does not depend on `Grob.Vm` — the LSP never executes code. Library: `OmniSharp.Extensions.LanguageServer`.

-----

### D-135 — Tooling: VS Code extension shell (Apr 2026)

Area: Tooling — VS Code extension
Supersedes: none
Superseded by: none

`tooling/Grob.VsCode/` added alongside the C# solution — TypeScript project. Registers `.grob` file type, attaches TextMate grammar, starts LSP process. The TypeScript scope is minimal (~30 lines).

-----

### D-136 — Tooling: LSP handler order (Apr 2026)

Area: Tooling — LSP handler order
Supersedes: none
Superseded by: none

LSP handler build order: (1) diagnostics, (2) completions, (3) hover, (4) go-to-definition. Semantic tokens deferred to post-MVP.

-----

### D-137 — `SourceLocation` as day-one compiler requirement (Apr 2026)

Area: Compiler — SourceLocation day one
Supersedes: none
Superseded by: none

**Day-one constraint:** every AST node must carry a `SourceLocation`. The type checker must resolve every identifier to its declaration node (`AstNode? Declaration`) and every symbol table entry must store `DeclaredAt: SourceLocation`. Not deferrable — retrofitting after the type checker is built requires a full audit. See `grob-tooling-strategy.md`.

-----

### D-138 — v1 Requirements Specification (Apr 2026)

Area: v1 Requirements Specification
Supersedes: none
Superseded by: none

Full build specification in `grob-v1-requirements.md`. Covers success criteria, sprint plan, language features, stdlib, CLI, plugin system, error handling, security, testing strategy, validation scripts, definition of done. Authoritative for what ships in v1 — draws from all other design documents.

-----

### D-139 — `input()` built-in (Apr 2026)

Area: `input()` built-in
Supersedes: none
Superseded by: none

`input(prompt: string = ""): string` — built-in function, same category as `print()` and `exit()`. No namespace. Always available. Writes prompt to stdout with no trailing newline (cursor stays on same line). Reads one line from stdin. Returns the line as `string` with newline stripped. Blocks until Enter. If the user presses Enter with no input, `input()` returns the empty string `""`. If stdin is closed before a line is read (Ctrl+Z + Enter on Windows, Ctrl+D on Unix, or piped input exhausted), throws `IoError("Unexpected end of input")` — silent empty string return would produce confusing downstream errors. Interactive use is the primary case; non-interactive (piped) stdin works normally.

-----

### D-140 — Array mutation methods (Apr 2026)

Area: Array mutation methods
Supersedes: none
Superseded by: none

`append(value: T): void`, `insert(index: int, value: T): void`, `remove(index: int): void`, `clear(): void` added to the `T[]` type registry. All four mutate the array in place. Calling any mutation method on a `const`-bound array is a compile error. `insert` throws `RuntimeError` if index is out of range. `remove` throws `RuntimeError` if index is out of range. `filter`, `map`, `sort`, `select` are unaffected — they always return new arrays and never mutate. Full registry in `grob-type-registry.md`.

-----

### D-141 — `map<K, V>` first-class type (Apr 2026)

Area: `map<K, V>` type
Supersedes: none
Superseded by: none

First-class built-in type. Compiler support for indexer read (`m[key] → V?`) and indexer write (`m[key] = value`). Construction via `map<string, string>{ "key": value }` literal. Empty map via `map<string, string>{}` with explicit type annotation. In v1, keys must be `string` — non-string keys deferred post-MVP. Type checker knows `map<string, string>` and `map<string, int>` as distinct types. Users consume and construct maps; cannot declare generic map types (same constrained-generics model as arrays). Iteration via `for k, v in myMap { }`. Insertion order preserved. Members: `length`, `isEmpty`, `keys: K[]`, `values: V[]`, `get(key): V?`, `set(key, value): void`, `contains(key): bool`, `remove(key): void`, `clear(): void`, read and write indexers. Mutation methods are compile errors on `const`-bound maps. `env.all()` and `Response.headers` return `map<string, string>`. Full registry in `grob-type-registry.md`.

-----

### D-142 — OQ-009 opened: `GrobValue` provisional (Apr 2026)

Area: OQ-009 opened
Supersedes: none
Superseded by: none

`GrobValue` provisional representation — `Grob.Core` requires a `GrobValue` definition before Sprint 1 begins, but OQ-005 (full value representation) is deferred until clox is complete. Tentative: define as tagged union struct, encapsulated behind a clean boundary, documented as provisional. Decide at Sprint 1 start. See `grob-open-questions.md`.

-----

### D-143 — OQ-010 opened: `.grobc` binary format spec (Apr 2026)

Area: OQ-010 opened
Supersedes: none
Superseded by: none

`.grobc` binary format specification — skeleton spec needed before implementation so the format is versionable from day one. Minimum: magic bytes, version header, endianness, constant pool serialisation, source location map inclusion. Defer until Sprint 1 structures are stable. See `grob-open-questions.md`.

-----

### D-144 — OQ-011 opened: `Grob.Crypto` API shape (Apr 2026)

Area: OQ-011 opened
Supersedes: none
Superseded by: D-148

`Grob.Crypto` API shape — must be specified before integration tests can pass Script 10. Minimum for v1: `crypto.sha256File(path) → string`, MD5 and SHA256 for strings and files, hex string return type. Defer to Sprint 10 planning. See `grob-open-questions.md`.

-----

### D-145 — OQ-012 opened: `process.run()` timeout (Apr 2026)

Area: OQ-012 opened
Supersedes: none
Superseded by: D-147

`process.run()` timeout behaviour — tentative direction: no silent default timeout, but `timeout: int` named parameter available on `run()` and `runShell()`. Throws `ProcessError` on timeout. `ProcessResult` does not need `timedOut` property. Defer to Sprint 9. See `grob-open-questions.md`.

-----

### D-146 — OQ-007 resolved: `for...in` iterable types (Apr 2026)

Area: OQ-007 resolved
Supersedes: none
Superseded by: none

`for...in` iterable types special-cased in v1. Three cases: (1) numeric range — lowered to `while`, already confirmed. (2) `T[]` array — index-based lowering to `while`, both single and two-identifier forms. (3) `map<K, V>` — two-identifier form required (`for k, v in myMap`), single-identifier on a map is a compile error with suggestion, lowered to `while` over internal keys array. Any other type in subject position is a compile error. Formal iterable protocol is post-MVP — no compiler rework required to add it. `map<K, V>` special-cased because `for k, v in myMap` is natural and the keyloop alternative is visibly clunky for a first-class type.

-----

### D-147 — OQ-012 resolved: `process.run()` timeout (Apr 2026)

Area: OQ-012 resolved
Supersedes: D-145
Superseded by: none

`process.run()` timeout behaviour resolved. All four process functions get `timeout: int = 0` as a named parameter. `0` means infinite — runs until the process completes or the OS kills it. On timeout expiry, throws `ProcessError("Process timed out after {n} seconds: {cmd}")`. `ProcessResult` is unchanged — no `timedOut` property. The throw is the signal. Full signatures: `process.run(cmd: string, args: string[], timeout: int = 0): ProcessResult`, `process.runShell(cmd: string, timeout: int = 0): ProcessResult`, `process.runOrFail(cmd: string, args: string[], timeout: int = 0): ProcessResult`, `process.runShellOrFail(cmd: string, timeout: int = 0): ProcessResult`.

-----

### D-148 — OQ-011 resolved: `Grob.Crypto` API shape (Apr 2026)

Area: OQ-011 resolved
Supersedes: D-144
Superseded by: none

`Grob.Crypto` API shape resolved. First-party plugin (`import Grob.Crypto`). File hashing streams internally, never loads full file into memory. String hashing uses UTF-8 encoding. All hex output is lowercase. Verify functions use constant-time comparison. API: `crypto.sha256File(path: string): string`, `crypto.md5File(path: string): string`, `crypto.sha256(value: string): string`, `crypto.md5(value: string): string`, `crypto.verifySha256(path: string, expected: string): bool`, `crypto.verifyMd5(path: string, expected: string): bool`. SHA-1, SHA-512, HMAC, byte array output — all post-MVP.

-----

### D-149 — `guid` core module (Apr 2026)

Area: guid module
Supersedes: none
Superseded by: none

`guid` is a first-class core module — auto-available, no import required. `guid` is a primitive type known to the type checker at compile time, registered by `GuidPlugin` in `Grob.Stdlib` at startup. Distinct from `string` — `guid == string` is a compile error. Generation: `guid.newV4(): guid` (random), `guid.newV7(): guid` (time-ordered, RFC 9562), `guid.newV5(namespace: guid, name: string...): guid` (deterministic, variadic name segments). Well-known namespaces: `guid.namespaces.dns`, `guid.namespaces.url`, `guid.namespaces.oid`. Parsing: `guid.parse(value: string): guid` (throws `RuntimeError` if invalid; compile-time validation on string literal arguments), `guid.tryParse(value: string): guid?` (nil if invalid), `guid.empty: guid`. Type members: `version: int` (property), `isEmpty: bool` (property), `toString(): string` (lowercase with hyphens), `toUpperString(): string` (uppercase for Azure ARM), `toCompactString(): string` (32 lowercase hex chars, no hyphens — storage names, keys). Operators: `==`, `!=`. String interpolation calls `toString()` implicitly. `map<guid, string>` not supported in v1 — use `myGuid.toString()` as key. Versions 1 and 3 excluded from v1.

-----

### D-150 — `fs.copy`/`fs.move` overwrite parameter (Apr 2026)

Area: fs.copy/fs.move overwrite
Supersedes: none
Superseded by: none

`fs.copy(src, dest, overwrite: bool = false)` and `fs.move(src, dest, overwrite: bool = false)` — safe default. `overwrite: false` throws `IoError` if destination exists. `overwrite: true` replaces silently. `File.copyTo(destDir, overwrite: bool = false)` and `File.moveTo(destDir, overwrite: bool = false)` instance methods get the same parameter.

-----

### D-151 — Script 11 added to validation suite (Apr 2026)

Area: Script 11 validation
Supersedes: none
Superseded by: none

Script 11 — Azure Resource Provisioning Helper added to validation suite. Exercises: `guid.newV5()` for idempotent resource naming, `Grob.Crypto` for template integrity verification, `map<K, V>` construction and `for k, v in` iteration, `Grob.Http` for ARM API calls, `env.require()` for credentials.

-----

### D-152 — `Grob.Zip` API shape (Apr 2026)

Area: Grob.Zip — API shape
Supersedes: none
Superseded by: none

First-party plugin (`import Grob.Zip`). Three `zip.create()` overloads: source is a directory path string, a `File` object (file or directory — `file.path` extracted internally), or a `string[]` of explicit file paths. All accept `overwrite: bool = false`; throws `IoError` if destination exists and `overwrite` is false. `zip.extract(archive: string, dest: string, overwrite: bool = false): void`. `zip.list(archive: string): ZipEntry[]` — reads central directory only, no extraction. `ZipEntry` type: `name: string`, `size: int`, `compressedSize: int`, `modified: date`. Password-protected zips are post-MVP. Large archives never loaded fully into memory. All failures throw `IoError`. No `File[]` overload — use `.map(f => f.path)` to convert `File[]` to `string[]`.

-----

### D-153 — `env` module — full API (Apr 2026)

Area: env module full API
Supersedes: none
Superseded by: none

`env.get(key: string): string?` — returns nil if not set. `env.require(key: string): string` — throws `RuntimeError` if not set or empty; error names the variable. `env.set(key: string, value: string): void` — process-scoped only; does not persist across invocations, does not write to registry. `env.has(key: string): bool` — returns false if not set OR if empty (empty is functionally absent for scripting purposes). `env.all(): map<string, string>` — all current process environment variables.

-----

### D-154 — `format` module — full API (Apr 2026)

Area: format module full API
Supersedes: none
Superseded by: none

All three format functions return `string`. Callers pass the result to `print()`, `log.*()`, `fs.writeText()`, or any string consumer. Signatures: `format.table(items: T[]): string`, `format.table(items: T[], columns: string[]): string` (explicit column selection and ordering), `format.list(item: T): string` (one field per line — `field: value` — for single-record detail views), `format.csv(items: T[]): string` (comma-delimited, header row always included in v1). Column widths auto-sized to content. Alignment: strings left-aligned, numbers right-aligned. Per-column alignment and width control are post-MVP. Compiler rewrite applies to all chained forms. Additional functions: `format.number(value: int|float, pattern: string): string`, `format.date(value: date, pattern: string): string` — pattern strings follow .NET conventions.

-----

### D-155 — `Grob.Http` locked signatures (Apr 2026)

Area: Grob.Http — locked signatures
Supersedes: D-118
Superseded by: none

`http.get(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response`. `http.post(url: string, body: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response`. `http.put` and `http.patch` have identical shape to `post`. `http.delete(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response`. `http.download(url: string, dest: string, auth: AuthHeader? = nil, timeoutSeconds: int = 30): void` — throws `NetworkError` on non-2xx (a failed download has written nothing useful). All other functions return `Response` and let the caller inspect `isSuccess` — non-2xx is not an exception. `body` is `string`; callers serialise structs via `json.encode()` before passing. `headers` is `map<string,string>`. `auth.bearer(token: string): AuthHeader`, `auth.basic(username: string, password: string): AuthHeader`, `auth.apiKey(key: string, headerName: string = "X-Api-Key"): AuthHeader`. `AuthHeader.toString()` returns `"[AuthHeader]"` — never exposes the credential.

-----

### D-156 — `json.encode()` added (Apr 2026)

Area: json.encode() added
Supersedes: none
Superseded by: none

`json.encode(value: T): string` added to the `json` module. Serialises any typed value or anonymous struct to a JSON string. Inverse of `json.parse()`. Required for `http.post()` and `http.put()` with struct bodies — the HTTP module accepts `string`, not typed values. Constrained generic, same model as `mapAs<T>()`.

-----

### D-157 — `json.Node` — full type spec (Apr 2026)

Area: json.Node — full type spec
Supersedes: none
Superseded by: none

`json.Node` is returned by `json.read()`, `json.parse()`, `json.stdin()`, and node indexer access. Indexer `node["key"]: json.Node?` returns nil for missing keys, never throws. Accessors: `asString(): string`, `asInt(): int`, `asFloat(): float`, `asBool(): bool`, `asArray(): json.Node[]` — all throw `JsonError` if the node is the wrong type. `mapAs<T>(): T` — constrained generic, throws `JsonError` on shape mismatch. Type predicates: `isNull: bool`, `isString: bool`, `isInt: bool`, `isFloat: bool`, `isBool: bool`, `isArray: bool`, `isObject: bool`. `toString(): string` returns raw JSON text of the node.

-----

### D-158 — `Response` type — full spec (Apr 2026)

Area: Response type — full spec
Supersedes: none
Superseded by: none

`Grob.Http.Response` members: `statusCode: int`, `isSuccess: bool` (200–299), `headers: map<string,string>` (keys normalised to lowercase — HTTP/2 convention, eliminates case-sensitivity bugs), `asText(): string`, `asJson(): json.Node` (throws `JsonError` if body is not valid JSON), `toString(): string` (returns status summary, never exposes body).

-----

### D-159 — `AuthHeader` type — full spec (Apr 2026)

Area: AuthHeader type — full spec
Supersedes: none
Superseded by: none

`Grob.Http.AuthHeader` is an opaque type. Constructed only by `auth.bearer()`, `auth.basic()`, `auth.apiKey()`. Only `http.*` functions accept it. Not directly constructable. `toString()` returns `"[AuthHeader]"` — never exposes the credential, including under `--verbose`.

-----

### D-160 — `ProcessResult` type — full spec (Apr 2026)

Area: ProcessResult type — full spec
Supersedes: none
Superseded by: none

`ProcessResult` members: `stdout: string` (empty string if none), `stderr: string` (empty string if none), `exitCode: int`, `toString(): string` (returns `stdout` — most useful default for interpolation and print).

-----

### D-161 — Escape sequence set — updated (Apr 2026)

Area: Escape sequence set — updated
Supersedes: D-130
Superseded by: none

`\r` (carriage return) added to the confirmed escape set. Full v1 set: `\n`, `\r`, `\t`, `\\`, `\"`, `\$`. `\r` is needed for explicit `\r\n` Windows line endings. Any unrecognised `\x` sequence is a compile error — no silent pass-through. Raw strings (backtick) continue to process no escape sequences.

-----

### D-162 — Numeric type precision (Apr 2026)

Area: Numeric type precision
Supersedes: none
Superseded by: none

`int` is 64-bit signed integer. `float` is 64-bit IEEE 754 double-precision. These are fixed and not configurable. The VM uses C# `long` and `double` respectively.

-----

### D-163 — Integer overflow behaviour (Apr 2026)

Area: Integer overflow
Supersedes: none
Superseded by: none

Arithmetic that exceeds the `int` range throws `RuntimeError` at runtime. The VM uses checked arithmetic — overflow never silently wraps. Prevents a class of bugs where large file sizes, timestamps, or counters produce wrong results.

-----

### D-164 — Implicit type coercion (Apr 2026)

Area: Implicit type coercion
Supersedes: none
Superseded by: none

The only implicit type conversion in Grob is `int` → `float` in mixed arithmetic. No `bool` → `int`. No `int` → `string` (use `.toString()` or interpolation). No `string` → `int` (use `.toInt()`). All other conversions are explicit method calls.

-----

### D-165 — Trailing commas permitted (Apr 2026)

Area: Trailing commas
Supersedes: none
Superseded by: none

Trailing commas are permitted in all comma-separated lists: array literals, struct construction, map literals, function parameters, function arguments. Optional — never required. Simplifies code generation and reduces diff noise.

-----

### D-166 — Two-pass type checker for forward references (Apr 2026)

Area: Forward references
Supersedes: none
Superseded by: none
Extended by: D-286

> **Note:** D-286 extends this decision by explicitly enumerating all supported forward-reference forms at the top level (function-to-function, type-to-type, generic type arguments, self-reference, mutual reference). D-166 remains the root decision; D-286 is its detailed elaboration.

Functions and types can reference other functions and types declared later in the same file. The type checker performs two passes: (1) registration pass — walks all top-level declarations and registers names and signatures; (2) validation pass — walks all bodies and top-level code, resolving against the fully populated symbol table. Inside function bodies, `:=` must precede use — no forward references within a single function.

-----

### D-167 — Variable shadowing allowed with warning (Apr 2026)

Area: Variable shadowing
Supersedes: none
Superseded by: none

A local variable may shadow a variable from an enclosing scope, including function parameters and global variables. The compiler emits a warning (not an error) when shadowing is detected. Rationale: preventing shadowing is annoying in real scripts; allowing it silently is a bug factory. A warning is the balance.

-----

### D-168 — Script structure order (Apr 2026)

Area: Script structure order
Supersedes: none
Superseded by: none

Canonical order: (1) `import` statements, (2) `param` blocks, (3) `type` and `fn` declarations (any order relative to each other), (4) top-level code. An `import` after a `param` or `type` is a compile error. A `param` after a `fn` or top-level statement is a compile error.

-----

### D-169 — Equality semantics (Apr 2026)

Area: Equality semantics
Supersedes: none
Superseded by: none

Value equality throughout. Primitives: compare by value. User-defined structs: field-by-field value equality (same type, all fields equal recursively). Anonymous structs: field-by-field, field order does not matter. Arrays: element-wise comparison. Maps: entry-wise comparison, insertion order does not affect equality. `nil == nil` is `true`. `==` between incompatible types is a compile error.

-----

### D-170 — Nil chain propagation (Apr 2026)

Area: Nil chain propagation
Supersedes: none
Superseded by: none

`?.` short-circuits the entire chain when the receiver is `nil`. `a?.b?.c` — if `a` is nil, the chain evaluates to `nil` immediately. No further access attempted. Result type is always `T?`.

-----

### D-171 — Script-level `return` is a compile error (Apr 2026)

Area: Script-level return
Supersedes: none
Superseded by: none

`return` at the top level of a script (outside any function) is a compile error. Use `exit()` to terminate a script early.

-----

### D-172 — No multiple return values (Apr 2026)

Area: No multiple return values
Supersedes: none
Superseded by: none

Functions return a single value. Use a struct to return multiple values.

-----

### D-173 — No operator overloading (Apr 2026)

Area: No operator overloading
Supersedes: none
Superseded by: none

User-defined types cannot define custom operators. Comparison uses field-by-field value equality.

-----

### D-174 — No circular imports (Apr 2026)

Area: No circular imports
Supersedes: none
Superseded by: none

`import` is for plugins only. Grob scripts do not export types to other scripts in v1. One script cannot import another script.

-----

### D-175 — `json.write` pretty-printed by default (Apr 2026)

Area: json.write — pretty default
Supersedes: none
Superseded by: none

`json.write()`, `json.encode()`, and `json.stdout()` default to pretty-printed output (indented). `compact: bool = false` named parameter on all three — pass `compact: true` for single-line output.

-----

### D-176 — Date constructors default to local time (Apr 2026)

Area: date constructors — local time
Supersedes: none
Superseded by: none

`date.now()`, `date.today()`, `date.of()`, `date.ofTime()` all return local time. `date.parse()` preserves the timezone from the input string; strings without timezone offsets are interpreted as local time. `date.fromUnixSeconds()` and `date.fromUnixMillis()` return UTC.

-----

### D-177 — `fs.readText` UTF-8 default (Apr 2026)

Area: fs.readText — UTF-8 default
Supersedes: none
Superseded by: none

`fs.readText()` reads as UTF-8. BOM auto-detection: if a BOM is present, encoding is detected from it. If no BOM, UTF-8 assumed. `fs.writeText()` and `fs.appendText()` write UTF-8 without BOM. `fs.readLines()` splits on `\n` and `\r\n` transparently — returned strings do not include terminators. No encoding parameter in v1.

-----

### D-178 — Map literal separator rules (Apr 2026)

Area: Map literal separator rules
Supersedes: none
Superseded by: none

Map literal entries are separated by newlines or commas (both valid). Trailing commas permitted. Each entry is `key: value` with colon separator. Keys are string literals in v1.

-----

### D-179 — `string.toString()` identity method (Apr 2026)

Area: string.toString() identity
Supersedes: none
Superseded by: none

`string.toString()` added to the type registry — returns the string unchanged. Identity method for type uniformity: every built-in type now has `toString()`.

-----

### D-180 — Stack overflow behaviour (Apr 2026)

Area: Stack overflow behaviour
Supersedes: none
Superseded by: none

`CallFrame[256]` fixed array. At recursion depth 257, the VM throws `RuntimeError("Stack overflow — maximum call depth (256) exceeded")`. Clean error, not a crash.

-----

### D-181 — `const` depth: binding and content (Apr 2026)

Area: const depth — binding AND content
Supersedes: D-013
Superseded by: D-288, D-291

> **Superseded by D-288 and D-291.** D-288 splits the immutability surface: `const` is now compile-time only and its deep-immutability guarantee is less relevant (const values are inlined, not bound). D-291 carries the deep-immutability clause forward and attaches it explicitly to `readonly`. The practical rule — "once-assigned bindings lock both rebinding and mutation" — is preserved; it now applies to `readonly` rather than `const`.

`const` prevents both rebinding and mutation. `const arr := [1, 2, 3]` — `arr = [4, 5]` is a compile error (rebinding) AND `arr.append(4)` is a compile error (mutation). One rule: `const` means immutable. The deeper question of mutable-binding-with-immutable-content (or vice versa) is deferred post-MVP, but v1 behaviour is unambiguous: `const` locks everything.

-----

### D-182 — Nested arrays (`T[][]`) (Apr 2026)

Area: Nested arrays (T[][])
Supersedes: none
Superseded by: none

Arrays of arrays are valid. `int[][]` is the type of a 2D array. `matrix[r][c]` for element access. No dimension enforcement — `matrix[0].length` need not equal `matrix[1].length`. No rectangular guarantee, no matrix operations. This is arrays-of-arrays, not a matrix type. Sufficient for JSON deserialisation of nested arrays and simple grid patterns. A dedicated `Matrix` type with linear algebra operations would be a post-MVP plugin if needed.

-----

### D-183 — No tuples in v1 (Apr 2026)

Area: No tuples
Supersedes: none
Superseded by: none

Tuples are not in v1. When a function needs to return multiple values, define a struct. Structs are self-documenting (fields have names), passable as values, and extensible without breaking callers. Go’s primary tuple use case (error returns: `result, err := foo()`) does not apply — Grob uses exceptions. Tuples are an additive grammar extension post-MVP if real friction is observed. No architectural rework required to add them later.

-----

### D-184 — No out parameters (Apr 2026)

Area: No out parameters
Supersedes: none
Superseded by: none

`out` parameters are not in v1 and are not planned. `out` is a C# mechanism for multiple returns where tuples and records were not available historically — C# itself has moved away from them. Grob’s nullable return pattern (`toInt() → int?`, `tryParse() → guid?`) covers the try-parse use case cleanly. `??` nil coalescing provides the fallback: `port := input("Port: ").toInt() ?? 8080`.

-----

### D-185 — Try-parse pattern (Apr 2026)

Area: Try-parse pattern
Supersedes: none
Superseded by: none

The try-parse pattern in Grob uses nullable return types, not tuples or out parameters. `string.toInt() → int?` returns nil if not parseable. `string.toFloat() → float?` returns nil. `guid.tryParse(value) → guid?` returns nil if invalid. Callers use `??` for defaults or `if (result != nil)` for branching. This is consistent, composable, and requires no special syntax.

-----

### D-270 — `print`, `exit`, `input` are built-in functions, not keywords (Apr 2026)

Area: Tokenisation — built-ins
Supersedes: none
Superseded by: none

`print`, `exit`, and `input` are built-in functions, tokenised as identifiers and resolved at type-check time against registered natives. They are not keywords. This is consistent with `input`'s existing treatment (D-139), with D-110's framing of `exit` as "a built-in function, same category as `print()`", and with every other stdlib function in the language. Identifier status allows them to be shadowed per D-167 (with warning) and produces correct diagnostics when misused.

`TokenKind` does not include `Print` or `Exit`. The keyword list in v1 Spec §3.4 and the Language Fundamentals tooling note is updated accordingly — a separate "Built-ins" category documents the three identifier names that resolve to natives. The TextMate grammar moves `print`, `exit`, `input` out of the `keyword.control.grob` pattern into a new `support.function.builtin.grob` pattern.

-----

### D-271 — `??` binds tighter than ternary (Apr 2026)

Area: Operator precedence — ternary vs nil coalescing
Supersedes: none (corrects Language Fundamentals §7 as originally drafted)
Superseded by: none

`??` (nil coalescing) is at precedence level 10. Ternary `? :` is at precedence level 11. `??` binds tighter than ternary. This matches C#, Kotlin, Swift, and TypeScript. The previous ordering (ternary tighter than `??`) was a silent divergence from stated reference languages.

Under the corrected precedence, `cond ? a : b ?? fallback` parses as `cond ? a : (b ?? fallback)` — the reading a C# developer expects. The Pratt parser binding powers for `?:` and `??` swap relative positions.

-----

### D-272 — Assignment operators are not in the precedence table (Apr 2026)

Area: Operator precedence — scope of the expression precedence table
Supersedes: none (corrects Language Fundamentals §7 as originally drafted)
Superseded by: none

The operator precedence table in Language Fundamentals §7 describes expression-level operators only. Assignment operators (`:=`, `=`, `+=`, `-=`, `*=`, `/=`, `%=`) are statement forms, not expressions — they cannot appear in expression position and do not have binding powers in the Pratt expression parser.

Assignment, declaration, increment/decrement, and `throw` are consolidated into a new dedicated Statement Forms section (Language Fundamentals §28). The precedence table is reduced from 13 levels to 12. Attempting to use assignment in expression position (`if (x = 5)`, `foo(x := 1)`) produces a parse-time error.

-----

### D-273 — `float % float` supported with fmod semantics (Apr 2026)

Area: Arithmetic — float modulo
Supersedes: none
Superseded by: none

The `%` operator is valid on `float` operands. `float % float → float` follows C#'s `double % double` semantics, which implement IEEE 754 `fmod`: the result has the same sign as the dividend. `-7.5 % 2.0` returns `-1.5`, not `0.5`. Mixed-type modulo (`int % float`, `float % int`) promotes the `int` to `float` and produces a `float` result, consistent with §15.

`x % 0.0` throws `RuntimeError` — consistent with `x / 0.0`. No form of modulo by zero silently produces a value. If either operand is `NaN` or `±Infinity`, the result follows IEEE 754 and is not an error. Integer modulo uses truncated-toward-zero division: `-7 % 3 = -1`.

-----

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

-----

### D-275 — `finally` block on `try` (Apr 2026)

Area: Exception handling — finally
Supersedes: none (extends D-274)
Superseded by: none

`try` blocks support an optional `finally` clause. The finally block runs on every normal or exceptional exit from the try region: normal completion, uncaught exception, caught-and-handled, caught-and-rethrown, early `return`, early `break`, early `continue`. It does not run on `exit()` — `exit()` unconditionally terminates.

A try with only a finally (no catches) is legal. A try with neither catch nor finally is a parse error. `finally` must appear after all catches, at most once.

**Control-flow-in-finally.** `return`, `break`, and `continue` are not permitted inside a `finally` block — compile error. This is a deliberate divergence from C#: the "finally overrides return" behaviour has no legitimate use case and is a documented source of bugs. `throw` from inside `finally` remains permitted; it replaces any in-flight exception (exception chaining is post-MVP).

`TokenKind` adds `Finally`. The AST gains `FinallyClause(body: Block)` on `TryStatement`. The exception handler table gains a `finallyOffset` field per entry. The VM's unwinding logic runs the finally block before propagating.

-----

### D-276 — Block-body lambda: implicit last expression, `return` for early exit (Apr 2026)

Area: Expressions — lambda block bodies
Supersedes: none (extends D-115)
Superseded by: none

A block-body lambda (`x => { ... }`) produces a value via: (a) implicit last expression — if the block's final statement is an expression, that value is the return value; or (b) explicit `return <expr>` for early exit. `return` in a lambda exits the lambda, not the enclosing function.

Both mechanisms may coexist. The type checker requires all return paths to produce the same type. If the block's final statement is not an expression (declaration, assignment, control-flow), the lambda's inferred return type is `void`. A void lambda cannot be used in a value position.

Lambda return types are always inferred from the body; no syntax to annotate in v1 (additive post-MVP). The ban on `return`/`break`/`continue` inside `finally` (D-275) does not apply to a `return` inside a block-body lambda nested within `finally` — that `return` exits only the lambda.

-----

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

-----

### D-278 — Integer division by zero throws `RuntimeError` (Apr 2026)

Area: Arithmetic — integer division
Supersedes: none (extends D-273 family)
Superseded by: none

`int / 0` throws `RuntimeError` at runtime, consistent with `int % 0`, `float / 0.0`, and `float % 0.0`. The compound forms `/=` and `%=` inherit this — they lower to the corresponding binary operator, so `i /= 0` throws before any assignment takes effect. No form of division or modulo by zero silently produces a value in Grob.

-----

### D-279 — Nullable interpolation is a compile error (Apr 2026)

Area: String literals — interpolation
Supersedes: none (extends §8 double-quoted strings)
Superseded by: none

Interpolation of a nullable-typed expression (`T?`) in a double-quoted string is a compile error. Before a nullable value can appear in an interpolation slot, it must be resolved to `T` — via `?? <fallback>` or by narrowing inside an `if (x != nil)` block.

`print()` retains its existing behaviour: it accepts any type including nil, and nil renders as `"nil"`. The asymmetry is deliberate — `print()` is a diagnostic output sink; interpolation is string construction. Silent nil coercion in `print()` is acceptable; in interpolation it almost always indicates a bug.

The compile error message names the nullable expression, its type, the interpolation site, and suggests both resolutions.

-----

### D-280 — Drop `map()`; `select()` is universal transformation (Apr 2026)

Area: `T[]` pipeline methods — projection and transformation
Supersedes: "`select` is alias for `map`" position in grob-type-registry.md
Superseded by: none

`.select<U>(fn: T → U) → U[]` is the single pipeline transformation primitive on `T[]`. `.map()` is removed from v1 entirely. This is a deliberate identity stance: **Grob is LINQ-for-scripting in its pipeline vocabulary, not FP-for-scripting.** Every C# developer knows `.Select()` and reaches for it for all three transformation shapes. Future pipeline operators follow LINQ naming (`.selectMany()`, `.aggregate()`).

`mapAs<T>()` is unaffected — it is a type assertion on parsed JSON/CSV data, not a pipeline transformation.

**Registry consequences.** The `map()` row is deleted from the `T[]` table. The `select()` row signature becomes `select<U>(fn: T → U) → U[]`; mutation-rules paragraph updated to remove `map`.

-----

### D-281 — `sort()` key-selector only; `U: Comparable` constraint (Apr 2026)

Area: `T[]` sort API
Supersedes: comparator example at grob-stdlib-reference.md (documentation artefact, not a real API)
Superseded by: none

`sort<U: Comparable>(fn: T → U, descending: bool = false) → T[]` is the sole sort signature. No comparator overload in v1. `Comparable = int | float | string | date | guid | bool`. `sort()` must be stable (implemented via `Enumerable.OrderBy`). The `Comparable` concept is documentation-level terminology, not a reserved word.

Multi-key sorting workaround: apply stable sort twice in reverse priority order. Post-MVP: anonymous-struct comparison extends `Comparable` to unlock single-pass multi-key sort.

-----

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

-----

### D-283 — Drop `snake_case` compiler warning (Apr 2026)

Area: Compiler warnings — naming convention
Supersedes: snake_case warning entry in Opinionated Defaults (D-061 in part)
Superseded by: none

The `snake_case` compiler warning is dropped. Naming convention moves from compiler-level enforcement to formatter-level idiom (`grob fmt`) and documentation. Other warnings (shadowing, unused variables, unused imports) are unaffected.

**Rationale.** An identifier census across the eleven sample scripts found 7% of identifiers triggering the warning — all eight offenders in Script 11, all Azure-domain names (`subscriptionId`, `tenantId`, `resourceGroup` etc.) externally constrained by the REST API. Warnings that fire on correct code train users to mute warnings; muted warnings are worse than no warnings. Go is strongly opinionated on naming but expresses it in `gofmt`/`golint`, never the compiler. `grob fmt` is Grob's documented opinion vehicle.

`grob-personality-identity.md` Opinionated Defaults table: Naming row changed to "snake_case idiomatic — see style guide / None (formatter, not compiler)".

-----

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

-----

### D-285 — Backtick raw strings canonical for Windows paths (Apr 2026)

Area: String literals — Windows paths and literal backslash content
Supersedes: none (documentation layer; D-127 string forms unchanged)
Superseded by: none

Backtick raw strings are established as the canonical Grob idiom for literal-backslash content — Windows paths, regex patterns, JSON fragments, and any other literal where backslashes appear as themselves. The three string forms (D-127) are unchanged at the grammar level; this is a documentation and convention decision.

Language Fundamentals §8 gains a "Windows paths and literal backslash content" callout. Sample scripts are updated to use backtick form for all path literals. The `@"..."` C# paradigm was evaluated and rejected — Grob's string-form philosophy is delimiter-driven (three delimiters, three intents) rather than modifier-driven, and backtick raw strings match Go's existing idiom.

-----

### D-186 — v1 scope-cut list (Apr 2026)

Area: v1 scope — contingency cuts
Supersedes: none
Superseded by: none

A formal v1 scope-cut list is adopted with two candidates, in activation order:

1. **Validation decorators** (`@allowed`, `@minLength`, `@maxLength`). `@secure` is **not** on the list — it stays in v1.
2. **Regex literal grammar** (`/pattern/flags`).

**Activation:** at Chris's discretion. No fixed gate. The list is insurance; activation is a judgement call based on implementation state. Both candidates have zero coverage in the eleven-script release gate (zero regex literals, zero validation decorator uses in any script). `regex.compile(pattern, flags)` function form covers everything regex literals would; adding literal grammar in v1.1 is additive. If regex literals are cut, a new sample script exercising `regex.compile()` must be added to the release gate before v1 ships.

-----

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

-----

### D-287 — Non-nullable type cycles are a compile error (Apr 2026)

Area: Type system — type cycle detection
Supersedes: none
Superseded by: none

A type declaration cannot contain a cycle of required non-nullable fields that would produce an infinitely-sized value. The type checker detects cycles during the validation pass.

**What terminates a cycle:** nullable fields (`T?`), array fields (`T[]`), map fields (`map<K, V>`). **What participates:** required fields whose type is a named user-defined type (including self).

**Detection:** standard DFS with `Unvisited` / `Visiting` / `Visited` states per type. A back-edge to a type currently on the stack is a cycle. The full path is reported in the diagnostic, naming both fix paths (nullable and collection). Example: `type Tree { children: Tree[] }` is legal; `type A { b: B }` / `type B { a: A }` is a compile error.

Error code: **E0301** (type cycle with no terminating field). Trivial single-type self-reference cases use **E0302**. See `grob-error-codes.md` and ADR-0017.

-----

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

-----

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

-----

### D-290 — Migration rule for existing `const` bindings (Apr 2026)

Area: Documentation migration — `const` to `readonly`
Supersedes: none
Superseded by: none

The mechanical migration rule:

> If the right-hand side of a `const` declaration satisfies D-289 (compile-time constant expression), the binding stays `const`. Otherwise, it becomes `readonly`.

No judgement calls required. Applied as a single pass over every document containing `const` examples. Three actual code-site migrations across the whole project — all array or map literal RHS cases (`const extensions := [".jpg", ...]` → `readonly extensions := [...]` etc.).

-----

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

-----

### D-292 — `const` and `readonly` at function-local scope (Apr 2026)

Area: Scoping — local-scope variants
Supersedes: none
Superseded by: none

Both `const` and `readonly` may appear inside function bodies and any nested block. Local `const` provides magic-number naming with zero runtime overhead — the compiler inlines every reference as a constant pool load. Local `readonly` provides a once-computed value with an immutability guard. Both follow the same scoping rules as mutable `:=`.

-----

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

-----

### D-294 — Top-level initialisation order and circular detection (Apr 2026)

Area: Runtime — top-level execution order and initialisation state
Supersedes: none
Superseded by: none

After `import`, `param`, `type` and `fn` declarations have been processed by the two-pass type checker, top-level code executes top-to-bottom in source order. `const` declarations do not participate — they are resolved at type-check time and inlined.

A circular read — a `readonly` or mutable top-level initialiser calling a function that reads a top-level binding declared later — is detected at runtime. Each top-level binding slot carries a three-state tag (`Uninitialised`, `Initialising`, `Initialised`). `DefineGlobal` flips the tag from `Uninitialised` to `Initialising` before RHS evaluation and to `Initialised` once the value is stored. `GetGlobal` during startup consults the tag; a read from a slot not yet `Initialised` raises `RuntimeError`.

After the top-level code's final instruction, the VM sets `_startupComplete`. Subsequent `GetGlobal` reads skip the tag check — startup-only branch cost.

The rule applies equally to `readonly` and mutable top-level bindings. The runtime error code is **E5902** (`RuntimeError`); see `grob-error-codes.md` and ADR-0017. Spec text lives at `grob-language-fundamentals.md` §19.1.

-----

### D-295 — Type field default evaluation timing (Apr 2026)

Area: User-defined types — field defaults
Supersedes: none
Superseded by: none

A field default expression evaluates at **construction time**, in the scope of the construction site. It evaluates once per construction that omits the field; constructions that supply the field explicitly do not evaluate the default.

A default may be any expression legal in general expression position — literals, function calls, interpolated strings, stdlib calls, method chains, anonymous struct literals, nested named type construction. A default may reference identifiers in scope at the construction site (`const`, `readonly`, mutable variables, function names, imported modules). A default may **not** reference other fields of the type being constructed — sibling fields may not yet have been assigned at that program point; such a reference is a compile error.

A field default is not a compile-time constant. Even when the RHS is a literal, it is a runtime expression in the specification. The `const` and `readonly` modifiers do not appear at the field-default declaration site. Spec text lives at `grob-language-fundamentals.md` §10.

-----

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

-----

## Post-MVP Decisions

-----

### D-PM-001 — `Grob.Git` plugin design sketch (Apr 2026)

Area: Grob.Git plugin — design sketch
Supersedes: none
Superseded by: none

Post-v1 first-party plugin. Umbrella for all git scripting. Three surfaces: (1) `git.open()` — local repo via LibGit2Sharp, host-agnostic. (2) `git.azureDevOps()` — typed AzureDevOps client, full name always used in plugin/types, user aliases as `ado :=` etc. (3) `git.github()` — typed GitHub client including Enterprise via `host:` parameter. Shared types: `PullRequest`, `GitUser`, `HostRepo` — normalised across hosts. Local API: `branches()`, `log()`, `log(containing:)` pickaxe search, `diff(commit)`, `staleBranches(days: 90)`, `raw()` escape hatch returning `ProcessResult`. Scripts using shared operations are host-portable with a one-line client construction change. Script 8 rewritten against `Grob.Git` in v1.1. “ADO” abbreviation not used in plugin or type names — ambiguous with ActiveX Data Objects. Full API shape in this entry.

-----

## Built-in Type Method Registry

*(Detail in `grob-type-registry.md`)*

-----

## Open Questions

*(Detail in `grob-open-questions.md`)*

-----

## Standard Library — Confirmed Modules

*(Detail in `grob-stdlib-reference.md`)*

-----

## Reference Languages

|Language|What to steal                                                         |
|--------|----------------------------------------------------------------------|
|Go      |Error handling as values, simple formatting, low ceremony, fast feel  |
|Kotlin  |Type inference, null safety, concise syntax, optional chaining        |
|Swift   |Optionals done right, readable and writable in equal measure          |
|C#      |`?` nullable syntax, `??` and `?.` operators, LINQ as fluent reference|
|Rust    |Pattern matching, making illegal states unrepresentable               |

-----

## Real-World Use Cases (April 2026)

These are the scripts Grob needs to be able to write. They drive stdlib design.

|#|Use Case                         |Key requirements                                              |
|-|---------------------------------|--------------------------------------------------------------|
|1|Azure CLI / Bicep scripting      |`process` first-class, stdout/stderr capture, exit codes      |
|2|SharePoint PnP library wrapping  |Third-party plugin model (`IGrobPlugin` wrapping .NET libs)   |
|3|Azure DevOps REST API power tools|`Grob.Http` (including `auth.*`), `json` stdlib, typed structs|
|4|Agent hook / Copilot-style tools |JSON stdin/stdout pipeline, `env`, clean string interpolation |

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
results := repos.map(r => processRepo(r))
json.stdout(results)
```

Companion params file `stale.grobparams`:

```
// stale.grobparams
// pat is intentionally absent — supply via --pat or keep out of source control
staleDays = 30
```

-----

## VM Architecture — Key Points

*(Full detail in `grob-vm-architecture.md`)*

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

-----

## Deferred — Not In Scope Until Post-MVP

|Feature                |Notes                                                                                                                                                   |
|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
|Compile to executable  |Transpile to C# via Roslyn — post-MVP                                                                                                                   |
|VS Code extension      |TextMate grammar, LSP — post-MVP                                                                                                                        |
|JIT compilation        |Explicitly out of scope                                                                                                                                 |
|Concurrent GC          |Not needed for scripting use case                                                                                                                       |
|Content mutability     |Mutable binding vs mutable value for collections — defer                                                                                                |
|AI tutor               |Guided learning companion — post-MVP, see personality doc                                                                                               |
|User-defined exceptions|Custom typed exceptions — post-MVP                                                                                                                      |
|Range/span indexing    |`[..n]`, `[^n..]`, `[start..end]` for strings and arrays — post-MVP, clean additive grammar extension, no architectural rework required                 |
|User-facing generics   |Declare generic fns/types in Grob scripts — post-MVP                                                                                                    |
|Sparky plushie         |Conference/trade show merchandise — post-release                                                                                                        |
|Sparky commissioned art|Human illustrator brief ready. Execute when project is public                                                                                           |
|do…while loop          |Deferred — expressible as `while` with initial execution. Post-MVP.                                                                                     |
|Labelled break         |Break outer loop from nested loop — restructure into function for v1. Post-MVP.                                                                         |
|Return type inference  |Inferred return types on functions — v1 requires explicit return types. Post-MVP.                                                                       |
|Doc comments (`///`)   |Lexer recognises and discards in v1. Semantics attached when `grob doc` tooling exists. Post-MVP.                                                       |
|Semantic tokens        |LSP Phase 5 — type-aware highlighting overlay on top of TextMate grammar. After LSP is stable. Post-MVP.                                                |
|`Grob.Git` plugin      |Umbrella git plugin — local repo (LibGit2Sharp), AzureDevOps client, GitHub client. Shared types across hosts. Design sketch in D-PM-001. Post-MVP.     |
|Tuples                 |Additive grammar extension. No architectural rework required. Structs serve the same purpose with named fields in v1. Revisit if real friction observed.|

-----

## Related Documents

|Document                                            |Purpose                                                                                               |
|----------------------------------------------------|------------------------------------------------------------------------------------------------------|
|`grob-v1-requirements.md`                           |v1 build specification — success criteria, sprint plan, definition of done                            |
|`grob-solution-architecture.md`                     |Solution structure, assembly responsibilities, dependency graph — authoritative                       |
|`grob-language-fundamentals.md`                     |Language fundamentals specification — parser, type checker, compiler reference                        |
|`grob-install-strategy.md`                          |Runtime and plugin install strategy — scopes, paths, CLI reference                                    |
|`grob-stdlib-reference.md`                          |Standard library reference — module shapes, built-in functions, expressions                           |
|`grob-type-registry.md`                             |Built-in type method registry — compiler and type checker reference                                   |
|`grob-open-questions.md`                            |Open and resolved design questions with full rationale                                                |
|`grob-tooling-strategy.md`                          |LSP, syntax highlighting, VS Code extension — phased plan                                             |
|`grob-language-brainstorm.md`                       |Early sketch notes — supplementary                                                                    |
|`grob-vm-architecture.md`                           |VM and runtime architecture detail — `GroType` references superseded by ADR-0012                      |
|`grob-personality-identity.md`                      |Character, tone, error messages, REPL, CLI                                                            |
|`grob-sample-scripts.md`                            |Real-world script comparisons and API surface validation                                              |
|`grob-plugins.md`                                   |Plugin ecosystem — authoring, registry, official plugins — `GroType` references superseded by ADR-0012|
|`sharpbasic-retrospective.md`                       |Completed retrospective — Grob design inputs                                                          |
|`sparky-character-sheet-v1.png`                     |Mascot reference — approved April 2026                                                                |
|`sparky-illustrator-brief.pdf`                      |Brief for human illustrator commission                                                                |
|`docs/adr/adr-0007-solution-structure-and-naming.md`|Solution structure and `GrobType` naming decision                                                     |

-----

*This document is the authoritative decisions record for Grob.*
*Updated April 2026 — pre-implementation review: 21 new decision entries added.*
*Escape sequences updated (`\r` added, unknown escapes are compile errors).*
*Numeric precision locked (int = 64-bit signed, float = 64-bit IEEE 754).*
*Integer overflow: checked arithmetic, throws RuntimeError.*
*Implicit coercion: only int → float, all else explicit.*
*Trailing commas: permitted everywhere. Forward references: two-pass type checker.*
*Shadowing: allowed with warning. Script structure order: import → param → type/fn → code.*
*Equality semantics: value equality for structs, arrays, maps, anonymous structs.*
*Nil chain propagation: `?.` short-circuits entire chain. Script-level return: compile error.*
*Explicit non-features: no multiple return values, no operator overloading, no circular imports.*
*json.write/encode/stdout: pretty-printed by default, `compact: bool = false` parameter.*
*date constructors: local time default. fs.readText: UTF-8 default with BOM auto-detection.*
*Map literal separators: newlines or commas. string.toString(): identity method added.*
*Stack overflow: RuntimeError at depth 256.*
*Previous: pipe operator row corrected; `Grob.Zip` API shape; `env` full API; `format` full API;*
*`Grob.Http` locked signatures; `json.encode()` added; `json.Node`, `Response`, `AuthHeader`, `ProcessResult` fully specified.*
*Previous: OQ-011 resolved (`Grob.Crypto` API); OQ-012 resolved (`process.run()` timeout);*
*`guid` core module added; `fs.copy`/`fs.move` overwrite parameter added;*
*Script 11 (Azure Resource Provisioning Helper) added to validation suite.*
*Session A2: decisions table converted to 185 numbered ADR-style entries (D-001 through D-185) plus one post-MVP entry (D-PM-001). Summary index added. Supersedes/superseded-by links populated.*
*Session A2 update (Apr 2026): D-270 through D-296 integrated from six session summary files (B Part 1, B Part 2, C Part 1, C Part 2, B Part 3, B Interlude, B Part 4). D-013, D-061, D-084, D-166, D-181 annotated with supersession/extension notes. C Part 2 scope-cut list assigned D-186 (first unused number in D-186–D-269 gap) after D-286 collision resolved.*
*The brainstorm doc and VM architecture doc are supplementary — this document wins on conflict.*
*`GroType` references in `grob-vm-architecture.md` and `grob-plugins.md` are superseded — read as `GrobType`.*
*Updated April 2026 — post-reconciliation alignment: ADR-0007 references in this document changed to ADR-0012 to match the on-disk repository's ADR numbering. The repo is the canonical pin for ADR numbers. Same change applied to `grob-v1-requirements.md`.*
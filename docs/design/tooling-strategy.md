# Grob — Tooling Strategy

> This document covers the editor tooling plan for Grob: syntax highlighting,
> the VS Code extension, and the Language Server Protocol implementation.
> It is a companion to `Grob___Solution_Architecture.md`.
> The decisions log is the authority — this document is supplementary.

-----

## Foundational Constraint — Source Location From Day One

**This is not a tooling concern. It is a compiler construction constraint.**

Every AST node must carry a `SourceLocation` from the first line of compiler
implementation. Every identifier the type checker resolves must carry a back-reference
to its declaration node. If this is not built in from the start, the LSP requires
a significant rework pass over the entire type checker later.

`SourceLocation` already lives in `Grob.Core` — it is the right home. The
commitment is ensuring it flows through the entire compiler pipeline from day one,
not just to error reporting.

### Required Shapes

**In `Grob.Core`:**

```csharp
record SourceLocation(string File, int Line, int Column);
```

**AST base node — in `Grob.Compiler`:**

```csharp
abstract class AstNode
{
    public SourceLocation Location { get; init; }
}
```

No AST node is constructed without a `SourceLocation`. This is enforced by the
constructor — not convention.

**Identifier nodes carry declaration back-references:**

```csharp
class IdentifierNode : AstNode
{
    public string Name { get; init; }
    public GrobType ResolvedType { get; set; }    // set by type checker
    public AstNode? Declaration { get; set; }      // set by type checker
}
```

**Symbol table entries carry declaration locations:**

```csharp
class Symbol
{
    public string Name { get; init; }
    public GrobType Type { get; init; }
    public SourceLocation DeclaredAt { get; init; }
}
```

### Why This Cannot Be Deferred

Go-to-definition returns `Declaration.Location`. If `Declaration` is not set
during the type checker pass, there is nothing to return. Retrofitting this
means revisiting every identifier resolution site in the type checker — a
pass that will have grown significantly by that point.

The cost of adding it now is one field per AST node and one assignment per
identifier resolution. The cost of adding it later is a full type checker audit.

**This is a day-one requirement, not a tooling phase concern.**

-----

## Phases

### Phase 1 — TextMate Grammar and Language Configuration

**When:** Now. Before the compiler is built. No dependencies.

**What it is:** Two static files that together give VS Code enough information
to provide a useful editing experience before the LSP exists. No compiler
dependency. No TypeScript. No build step.

**File 1 — `grob.tmLanguage.json`**

A static TextMate grammar defining token patterns using regex. Runs entirely
in the editor. Gives users keyword colouring, string literals, comments and
number literals immediately.

Covers:

- Keywords: `fn`, `if`, `else`, `for`, `in`, `while`, `const`, `type`, `import`,
  `param`, `return`, `try`, `catch`, `select`, `case`, `break`, `continue`,
  `nil`, `true`, `false`, `step`, `switch`, `exit`, `print`
- Literals: double-quoted strings with `${...}` interpolation regions highlighted
  as an embedded expression scope; single backtick raw strings; triple backtick
  raw block strings; integers (decimal, hex `0x`, binary `0b`, underscore-separated);
  floats
- Comments: `//` line comments, `/* */` block comments, `///` doc comment reserved
- Operators and punctuation including `#{` as a distinct token
- Type annotations: `: TypeName` pattern — type names distinguished from identifiers
- Function declarations: `fn name(` heuristic for function name detection
- Type declarations: `type Name` heuristic for type name detection

**File 2 — `language-configuration.json`**

A static JSON file declaring editor behaviours that VS Code handles natively —
no LSP, no TypeScript involvement. Gives developers automatic bracket matching,
comment toggling and basic indentation from the moment the extension is installed.

Covers:

- **Bracket pairs:** `()`, `[]`, `{}`, ``` ``` (single backtick),
  ````` ````` (triple backtick)
- **Auto-closing pairs:** `"` → `"`, ``` → ```, `(` → `)`, `[` → `]`,
  `{` → `}` — editor inserts the closing delimiter automatically
- **Surrounding pairs:** same set — wraps selected text when delimiter is typed
- **Comment toggling:** line comment `//`, block comment `/* */`
- **Indentation rules:** increase indent after `{`, decrease after `}`
- **Word pattern:** identifiers including `_` separator

This file requires no maintenance as the language evolves — bracket pairs and
comment syntax are stable. It is not the formatter; `grob fmt` remains the
formatting story. This covers only the editor conveniences developers expect
without thinking about them.

**Deliverables:** `grob.tmLanguage.json` and `language-configuration.json`
in the `Grob.VsCode` extension project.

**Ships with:** The first VS Code extension release. Users get readable,
editor-friendly `.grob` files before the LSP exists.

-----

### Phase 2 — Source Location in the Compiler

**When:** During compiler construction. This is not a separate phase — it is a
discipline applied throughout compiler build.

**See:** The “Foundational Constraint” section above. This is the only phase
with a hard dependency on current implementation work.

**Verification:** `Grob.Compiler.Tests` should assert that every node in a parsed
AST carries a non-default `SourceLocation`. This makes the constraint testable
and prevents regression.

-----

### Phase 3 — VS Code Extension Shell

**When:** After the compiler is stable.

**What it is:** A small TypeScript project. Its job is to register the `.grob`
file type with VS Code, attach the TextMate grammar, and start the language server
process. The TypeScript is roughly 30 lines.

**Solution location:** `tooling/Grob.VsCode/` — outside the C# solution, alongside it.

**Structure:**

```
tooling/
└── Grob.VsCode/
    ├── package.json                      ← extension manifest — language ID, file extension, grammar
    ├── language-configuration.json       ← bracket pairs, comment toggling, indentation rules
    ├── syntaxes/
    │   └── grob.tmLanguage.json          ← from Phase 1
    └── src/
        └── extension.ts                  ← start the LSP process, wire to VS Code
```

**`package.json` declares:**

- Language ID: `grob`
- File extension: `.grob`
- TextMate grammar path
- Language server executable path

**`extension.ts`** starts `Grob.Lsp` as a child process and connects its
stdin/stdout to the VS Code LSP client. This is the complete TypeScript scope.

-----

### Phase 4 — `Grob.Lsp` Project

**When:** After the compiler is stable. After Phase 3 shell exists.

**Solution location:** `src/Grob.Lsp/` — inside the C# solution alongside the
other `src/` projects.

**Dependencies:** `Grob.Compiler`, `Grob.Core`, `Grob.Runtime`. The LSP is a
consumer of the compiler pipeline — it does not reimplement it.

**Library:** `OmniSharp.Extensions.LanguageServer` NuGet package. Handles all
JSON-RPC plumbing. Grob implements handlers.

**Entry point:**

```csharp
var server = await LanguageServer.From(options => options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithHandler<DiagnosticsHandler>()
    .WithHandler<CompletionHandler>()
    .WithHandler<HoverHandler>()
    .WithHandler<DefinitionHandler>()
);
await server.WaitForExit;
```

**Handlers — build in this order:**

#### 1. Diagnostics

Run the type checker on file save. Push errors to the editor as squiggles.

This is the highest-value capability and the simplest to implement — the type
checker already collects all errors and never stops at first. The LSP just
forwards them.

```csharp
// On document save or change:
var tokens  = new Lexer(source, filePath).Tokenise();
var ast     = new Parser(tokens).Parse();
var errors  = new TypeChecker(ast).Check();  // returns all errors

// Convert GrobDiagnostic → LSP Diagnostic and publish
```

#### 2. Completions

Parse to cursor position, resolve the type of the expression left of `.`,
query the type registry, return method and property names.

The `42.toString()` scenario works correctly because the lexer lookahead rule
that handles integer-vs-float disambiguation is the same rule used here — the
LSP shares the lexer via `Grob.Compiler`.

```csharp
// On textDocument/completion:
var node    = typedAst.NodeAt(request.Position);
var type    = node.ResolvedType;
var members = TypeRegistry.MembersFor(type);
return new CompletionList(members.Select(ToCompletionItem));
```

#### 3. Hover

Cursor over an identifier — return its resolved type as a tooltip. One line
of type information is enough. Minimal implementation cost, high daily value.

```csharp
// On textDocument/hover:
var node = typedAst.NodeAt(request.Position);
return new Hover($"{node.Name}: {node.ResolvedType}");
```

#### 4. Go to Definition

Cursor over an identifier — return the source location of its declaration.
Trivial once Phase 2 is in place. The entire implementation is a lookup.

```csharp
// On textDocument/definition:
var node = typedAst.NodeAt(request.Position);
var decl = node.Declaration;
return new Location(decl.Location.File, decl.Location.Line, decl.Location.Column);
```

For v1, go-to-definition and go-to-implementation are the same handler.
Grob has no interface/implementation split.

-----

### Phase 5 — Semantic Tokens (Post-MVP)

**When:** After the LSP is stable. Post-MVP.

Semantic tokens override the TextMate grammar colours with type-aware highlighting.
Local variables, function names, type names, and parameters can be distinguished
by colour rather than just by pattern. The TextMate grammar works by regex heuristic.
Semantic tokens work by actual type resolution.

Implemented as an additional handler on the existing LSP:

```csharp
.WithHandler<SemanticTokensHandler>()
```

This is the only phase that is explicitly post-MVP.

-----

## Updated Solution Structure

```
Grob.sln
├── src/
│   ├── Grob.Core/
│   ├── Grob.Runtime/
│   ├── Grob.Compiler/
│   ├── Grob.Vm/
│   ├── Grob.Stdlib/
│   ├── Grob.Cli/
│   └── Grob.Lsp/               ← Phase 4 addition
├── plugins/
│   ├── Grob.Http/
│   ├── Grob.Crypto/
│   └── Grob.Zip/
├── tests/
│   ├── Grob.Core.Tests/
│   ├── Grob.Compiler.Tests/
│   ├── Grob.Vm.Tests/
│   ├── Grob.Stdlib.Tests/
│   └── Grob.Integration.Tests/
└── tooling/
    └── Grob.VsCode/             ← Phase 1 + Phase 3
        ├── package.json
        ├── language-configuration.json
        ├── syntaxes/
        │   └── grob.tmLanguage.json
        └── src/
            └── extension.ts
```

-----

## Dependency Graph Addition

`Grob.Lsp` follows the same pattern as `Grob.Cli` — a consumer of the compiler
pipeline, not part of it.

```
Grob.Lsp
  ├── Grob.Compiler
  │     ├── Grob.Core
  │     └── Grob.Runtime
  ├── Grob.Core
  └── Grob.Runtime
```

`Grob.Lsp` does not reference `Grob.Vm` — it never executes code, only analyses it.

-----

## Phase Summary

|Phase                                 |What                                                          |When                 |Hard dependency             |
|--------------------------------------|--------------------------------------------------------------|---------------------|----------------------------|
|1 — TextMate grammar + language config|Static syntax highlighting, bracket pairs, comment toggling   |Now                  |None                        |
|2 — Source locations                  |`SourceLocation` on all AST nodes, declaration back-references|During compiler build|Day one — cannot be deferred|
|3 — VS Code extension shell           |TypeScript wrapper, starts LSP process                        |After compiler stable|Phase 1                     |
|4 — `Grob.Lsp`                        |Diagnostics, completions, hover, go-to-definition             |After compiler stable|Phase 2                     |
|5 — Semantic tokens                   |Type-aware highlighting overlay                               |Post-MVP             |Phase 4 stable              |

-----

## Related Documents

|Document                           |Relevance                                                                |
|-----------------------------------|-------------------------------------------------------------------------|
|`Grob___Solution_Architecture.md`  |Solution structure, `Grob.Lsp` fits the same pattern as `Grob.Cli`       |
|`Grob___Language_Fundamentals.md`  |Keyword list and operator set — authoritative source for TextMate grammar|
|`Grob___Type___Registry.md`        |Type methods and properties — completions and hover data source          |
|`Grob___Decisions___Context_Log.md`|Authority — this document is supplementary                               |

-----

*Created April 2026 — tooling strategy session.*
*Updated April 2026 — language-configuration.json added to Phase 1; string literal*
*coverage updated for single and triple backtick forms; keyword list updated.*
*Phase 2 (source location) is a day-one compiler construction requirement.*
*`Grob.Lsp` added to solution structure. `tooling/Grob.VsCode` added alongside solution.*
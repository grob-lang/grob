# Grob — Claude Code Project Instructions

You are a contributor to **Grob**, a statically typed scripting language with a
bytecode VM, written in C# on .NET 10. These instructions are always active. They
are the foundation; nested `CLAUDE.md` files add detail for specific parts of the
tree (`src/` for the C# host code, `tests/`, `plugins/`), the skills under
`.claude/skills/` carry task-scoped procedures, and the sub-agents under
`.claude/agents/` carry deeper context for particular jobs.

Read this whole file before writing code. It is short on purpose.

-----

## What Grob is

A statically typed scripting language that a hobbyist can learn and a developer can
trust. C-style syntax, type inference, explicit nullability, opt-in immutability.
The design target is to stand next to Go, PowerShell and Python as a credible answer
to "what should I use for this scripting task?" — Go is too ceremonious, PowerShell's
syntax is hostile, Python is dynamically typed and clunky at scale. Grob fills that
gap. Primary users are Windows developers and sysadmins.

Grob is a serious project, not a toy and not a learning exercise. When a choice
exists between the approach that is easier to build and the approach that produces a
better language, choose the better language. Design decisions are made as if they
will ship, because they will.

This is AI-augmented development, not vibe coding. The maintainer understands and
owns every line. Your job is to suggest, implement and review against a stated
rationale — never to produce code that gets accepted without being understood.
Explain your reasoning. Surface trade-offs. If a direction is weak, say so.

-----

## The authority chain

When documents conflict, resolve in this order:

1. **`docs/design/grob-decisions-log.md`** — the authority. Numbered `D-###`
   ADR-style entries. If a decision is recorded here, it is settled.
2. **`docs/design/grob-v1-requirements.md`** — the build guide. Sprint scope,
   acceptance criteria, Definition of Done, what is explicitly out of scope.
3. **`docs/design/grob-solution-architecture.md`** — the project graph and
   assembly responsibilities.
4. The remaining design docs under `docs/design/` for their specific areas.

Never invent a resolution to a contradiction. If the code and a design doc disagree,
or two docs disagree, **stop and flag it** with the specific files and lines. Verify
file state before claiming drift — a session summary saying an edit landed is not
proof the edit landed.

When you implement something that a decision authorised, reference the `D-###` in
the commit message and in a code comment where it clarifies intent. When you find
yourself wanting a behaviour the docs do not cover, that is a design question — raise
it, do not silently decide it.

-----

## The two languages in this repository

This repo contains **C# host code** (the compiler, VM and stdlib that *implement*
Grob) and **`.grob` scripts** (programs *written in* Grob — samples, tests,
fixtures). They have different rules. Be aware which one you are touching:

- `**/*.cs` — C# host code. Rules live in `src/CLAUDE.md` (and `tests/CLAUDE.md`,
  `plugins/CLAUDE.md`), loaded automatically when you work in those trees.
- `**/*.grob` — Grob language source. The `writing-grob-source` skill activates
  when a `.grob` file is in play; it carries the syntax and idioms.

Do not let Grob language conventions leak into C#, or vice versa. A `.grob` file uses
`:=`, backtick raw strings for Windows paths and `select`/`case`; a `.cs` file is
ordinary modern C#.

-----

## Solution shape (do not violate the DAG)

```
src/
  Grob.Core/        Chunk, OpCode, GrobType, GrobValue, ConstantPool, SourceLocation
  Grob.Runtime/     IGrobPlugin, GrobVM registration surface, FunctionSignature, GrobError hierarchy
  Grob.Compiler/    Lexer, Parser, AST, TypeChecker, Compiler (partial classes by concern)
  Grob.Vm/          VirtualMachine, ValueStack, CallFrame[256], Globals, PluginLoader, Upvalue
  Grob.Stdlib/      13 core modules, one IGrobPlugin per module
  Grob.Cli/         grob.exe — composition root, CLI commands, REPL, error formatting
  Grob.Lsp/         Language server (post-MVP) — consumes Grob.Compiler, never runs scripts
plugins/            Grob.Http (reference impl), Grob.Crypto, Grob.Zip — reference Grob.Runtime only
tests/              Grob.Core.Tests, Grob.Compiler.Tests, Grob.Vm.Tests, Grob.Stdlib.Tests, Grob.Integration.Tests
bench/Grob.Benchmarks/   BenchmarkDotNet harness (D-302)
tooling/Grob.VsCode/     VS Code extension (TypeScript)
```

**The critical rule: `Grob.Compiler` and `Grob.Vm` never reference each other.**
`Grob.Core` is the only shared ground between them. `Chunk` is the boundary — the
compiler produces it, the VM consumes it, neither knows about the other. If you find
yourself adding a project reference that would couple Compiler and Vm, you have made
a mistake; the type you want belongs in `Grob.Core`.

The graph is a DAG with no cycles. `Grob.Cli` is the only composition point and the
only project that references everything. Nothing references `Grob.Cli`. The full
project graph, central package management and namespace rules live in `src/CLAUDE.md`.

-----

## Non-negotiables (these hold across every sprint)

- **Source location everywhere, day one.** Every `Token` carries `(file, line,
  column)`. Every AST node carries a `SourceLocation`. Every bytecode instruction
  carries a line number in the chunk's parallel line array. This is not retrofittable
  — adding it later means touching every node constructor.
- **LSP-enabling fields, day one.** The type checker sets `ResolvedType` and a
  `Declaration` back-reference on every identifier node. `Grob.Compiler.Tests`
  asserts these are non-null after type checking. They cost one field and one
  assignment; they are not used by the v1 runtime but exist so the LSP never needs a
  type-checker audit to retrofit.
- **Two-mode error handling.** The lexer, parser and type checker collect **all**
  errors before execution — never stop at the first. There is no diagnostic cap. The
  VM stops on the **first** runtime error. (D-039)
- **Error-recovering, stateless parser.** On a parse failure the parser emits a
  diagnostic, builds an `ErrorExpr` / `ErrorStmt` / `ErrorDecl` placeholder, advances
  to the next synchronisation anchor, and resumes. A single malformed construct never
  aborts the parse. Error nodes have type `Error`, assignable to and from every type,
  so one parse error does not cascade into a wall of type diagnostics. (D-300, spec
  in `grob-language-fundamentals.md` §29)
- **Complete enums, defined once.** `OpCode` and `TokenKind` are written out in full
  when first introduced — never grown incrementally. The full sets live in
  `grob-v1-requirements.md` §3.3 and §3.4. Adding a case later is a deliberate,
  decision-logged act, not casual drift.
- **Typed opcodes, no runtime type checks.** The compiler emits `AddInt` vs
  `AddFloat` vs `Concat` using type-checker annotations. The VM trusts them. The type
  checker already proved correctness.
- **Structs for value types, classes for heap objects only.** `GrobValue` is a
  hand-rolled `readonly struct` tagged union (D-303). NaN boxing is rejected — do not
  propose it. Lean on the .NET GC; there is no custom collector (D-304).
- **Visitor pattern + partial classes in the compiler.** Three passes walk the same
  AST. `Grob.Compiler` splits by concern across `partial class` files.

-----

## Output and tone conventions (these are the product's personality)

These apply to anything the CLI, compiler or REPL prints, and to docs and comments:

- **British English throughout.** Colour, behaviour, initialise, recognise.
- **Never the Oxford comma.**
- **Never the word "simply"** (or "just", "obviously", "of course") in user-facing
  text or docs.
- **Errors to stderr, results to stdout.** Always pipeline-friendly.
- **Quiet on success, clear on failure.** The compiler is strict where it matters and
  quiet where it does not.
- **No emoji in compiler errors or CLI output.** The REPL may, sparingly. Source
  comments and docs: none.
- **Error messages never show variable values** — only names and types — unless
  `--verbose` is set. This is a security decision.
- Every error message includes file, line, column, what went wrong, and a suggested
  fix when the fix is obvious.

-----

## How to work in this repo

- **Build:** `dotnet build` from the solution root. **Test:** `dotnet test`. Both
  must pass before any change is considered done.
- **Tests are not optional and not retroactive.** Every change ships with tests for
  the behaviour it adds. Compiler output tests (source → assert bytecode) are the
  highest-priority bug surface — that is where bugs live.
- **Windows-native.** This is a Windows-first project. Use Windows path conventions
  and Windows idioms in examples, fixtures and docs. No Unix paths in any Grob-facing
  context.
- **Match the existing code.** Read the surrounding file before writing. Follow its
  naming, its structure, its partial-class organisation.
- **XML doc comments on every public API.** Every public type, method and property
  in C# host code carries an XML doc comment (`/// <summary>...`). Internal members
  are documented when their purpose is not obvious from the name. This applies to
  every new file and every new public member added to an existing file — it is not
  a "later" item.
- **The `Grob` prefix is always spelled in full** — `GrobType`, `GrobValue`,
  `GrobError`. `Gro` is not a convention in this codebase.
- When a task is large, propose the smallest working increment first. "Working" means
  the test suite passes and the increment runs something meaningful.

-----

## Driving Grob in Claude Code

The way of working does not change from how the maintainer drove it in the editor —
the harness moved, the workflow did not.

- **Increment prompts are the unit of work.** Each Sprint increment has a dense,
  self-contained prompt under `prompts/` (e.g. `prompts/sprint-4-a-*.md`) carrying
  the read-list, the inline reference blocks, the deliverable, the out-of-scope
  closed surface, the tests and the acceptance gate. The maintainer drives one by
  pointing you at it: **"read `prompts/<increment>.md` in full and execute."** Treat
  the named files in its read-list as mandatory, the inline reference blocks as
  authoritative, and the verify-before-relying notes as binding. Work the increment
  exactly as written; surface any contradiction with the decisions log rather than
  resolving it.
- **One increment per session**, sequential, on a fresh branch. Never on `main`.
- **Reusable slash commands** live in `.claude/commands/`: `/start-branch`,
  `/propose-change`, `/commit-message`, `/sprint-plan`, `/consistency-review`,
  `/model-select`.
- **Sub-agents** live in `.claude/agents/`: `grob-compiler-engineer` (the host-code
  implementer the increment prompts name), `grob-reviewer` (the self-review pass
  before the maintainer sees a diff) and `grob-design-reviewer` (read-only corpus
  review against the log).
- **Skills** under `.claude/skills/` activate by description when the task matches —
  `adding-an-opcode`, `adding-a-stdlib-function`, `authoring-a-plugin`,
  `writing-an-error-test`, `logging-a-decision`, `tdd-cycle`, `trunk-flow`,
  `grob-spec-lookup`, `writing-grob-source`.
- **Model.** Default to Sonnet. Reach for Opus only on the named load-bearing
  triggers (see `/model-select`); use Haiku for genuinely mechanical, self-contained
  work. The increment prompts state the model per task.
- **MCP.** `.mcp.json` configures the Microsoft Learn server. For .NET 10 BCL
  specifics, ground against Microsoft Learn rather than recalling signatures.
- **GitHub via `gh`.** There is no GitHub MCP server — GitHub work goes through the
  `gh` CLI over Bash (`gh issue list`, `gh pr view`, `gh pr create`, `gh run list` for
  CI status), using the maintainer's existing `gh auth login`. Reach for `gh` rather
  than expecting a GitHub tool surface. Opening PRs and merging remain the
  maintainer's actions.

-----

## Codebase orientation before writing

The solution shape above tells you *what* lives where. The existing files tell you
*how* the patterns are applied. Before writing in a project, open the existing files
in that project and read them — naming, partial-class layout, test structure, error
patterns. Match what is there.

If the increment's prompt names specific files to read first, those are mandatory,
not suggestions. They are listed because the prompt author already knows which
existing patterns matter for the increment in hand.

The cost of skipping this step is rework. It is almost always cheaper to spend ten
minutes reading three existing files than to write something that does not match and
get it corrected later.

-----

## When stuck, exhaust these before asking

Most stalls resolve on this list. Work through it before raising a clarifying
question to the maintainer.

- Run `dotnet build`. Read the actual error and what it points at.
- Run `dotnet test`. Read the actual failure, not your model of it.
- Run the pre-commit hooks (whitespace, formatting, linters). If the maintainer
  asks for a "format fix" or "whitespace fix", running the hooks *is* the first
  move — not a question about what they mean.
- Run `git diff` to see what actually changed in the working tree versus what you
  intended to change.
- Open the existing nearest similar code and copy the pattern. New compiler test?
  Open the existing VM tests in `Grob.Vm.Tests`. New partial class? Open the
  existing partial classes in the same project.
- Re-read the relevant `D-###` decision in the decisions log rather than guessing
  what it says.

After all of that, if you are still stuck, ask **one specific question**: name the
file, the line, and the choice you cannot resolve. Do not loop on context-gathering
when the answer is a single command away.

-----

When you are unsure which sprint a piece of work belongs to, or whether a feature is
in v1 scope, check `grob-v1-requirements.md` §4 (sprint breakdown) and §13 (out of
scope) before writing anything.

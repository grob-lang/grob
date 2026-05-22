# AGENTS.md — Grob

Grob is a statically typed scripting language with a stack-based bytecode VM, written
in C# on .NET 10. It aims to stand next to Go, PowerShell and Python as a credible
scripting choice. Primary users are Windows developers and sysadmins. This is a serious
project built as if it will ship; it is AI-augmented, not vibe-coded — the maintainer
understands and owns every line, so explain your reasoning and never produce code meant
to be accepted on trust.

This file is the broad-compatibility entry point read by various agents. The detailed,
layered configuration lives under `.github/` and is described in
`.github/COPILOT-SETUP.md`. Read that for the full picture; the essentials are below.

## Authority chain (resolve conflicts in this order)

1. `docs/design/grob-decisions-log.md` — the authority. Numbered `D-###` decisions.
2. `docs/design/grob-v1-requirements.md` — sprint scope, acceptance criteria, what is
   out of scope.
3. `docs/design/grob-solution-architecture.md` — the assembly graph (a DAG).
4. The other `docs/design/` specs for their areas.

Never invent a resolution to a contradiction. Verify file state before claiming drift,
then flag it with file and line. A new behaviour the docs do not cover is a design
question for the maintainer, not a thing to decide silently.

## Two languages in this repo

- `**/*.cs` — the C# that *implements* Grob. Ordinary modern C# plus Grob's invariants.
- `**/*.grob` — programs *written in* Grob. Idiomatic language source.

Detailed rules for each are in `.github/instructions/`. Do not let one's conventions
leak into the other.

## Architecture invariants (do not break these)

- **`Grob.Compiler` and `Grob.Vm` never reference each other.** Shared types go in
  `Grob.Core`; `Chunk` is the boundary. The dependency graph is a DAG.
- Source location on every AST node and every instruction, day one.
- Type checker sets `ResolvedType` and `Declaration` on every identifier node.
- All compile-time errors collected (no cap); the VM stops on the first runtime error.
- Error-recovering, stateless parser (D-300): `ErrorExpr`/`ErrorStmt`/`ErrorDecl`,
  cascade-suppressed via the `Error` type; every visitor handles them.
- `OpCode` and `TokenKind` complete and defined once, never grown additively.
- Typed opcodes — no runtime type checks; the type checker already proved correctness.
- `GrobValue` is a tagged-union `readonly struct`; NaN boxing is rejected; lean on the
  .NET GC, no custom collector.

## Conventions

- British English. Never the Oxford comma. Never "simply".
- Errors to stderr, results to stdout. No emoji in compiler or CLI output.
- The `Grob` prefix is always spelled in full (`GrobType`, not `GroType`).
- Windows path conventions in all examples, fixtures and docs.

## Build, test, done

- Build: `dotnet build`. Test: `dotnet test`. Both must pass before anything is done.
- Every change ships with tests in the project that owns the layer. Compiler output
  tests (source → assert bytecode) are the highest-priority bug surface.
- Stay inside the task; a focused diff is a reviewable diff.

## Working procedures

For repeatable jobs, follow the matching skill in `.github/skills/`: adding an opcode,
authoring a plugin, writing a gold-master error test, adding a stdlib function, logging
a decision. For grounding .NET APIs, prefer the Microsoft Learn MCP server over recall.

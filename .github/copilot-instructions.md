# Grob — Copilot instructions

You are working on **Grob**, a statically typed scripting language with a bytecode
VM, written in C# .NET 10. The design is complete. Implementation is what you do.

The decisions log (`docs/design/grob-decisions-log.md`) is the authority. When this
file or any other says one thing and the decisions log says another, the decisions
log wins. Fetch it when precision matters.

## Hard rules

1. **Never start work on `main`.** Always create a short-lived branch first. See
   `.github/skills/trunk-flow/SKILL.md`. If you find yourself on `main`, stop and
   say so before doing anything else.
2. **No code lands without Chris's approval.** Propose first for anything
   multi-file, anything introducing abstractions, anything affecting public API.
   For obvious mechanical work — a single-method body, an obvious wiring — ask
   "this looks mechanical, should I implement it directly?" rather than walking
   through a full proposal.
3. **TDD: red, green, refactor — always.** Test first, watch it fail, implement
   the minimum to pass, refactor. The only exceptions are TDD-awkward structural
   work (solution skeleton, csproj generation, directory scaffolding) — name the
   exception when you take it. See `.github/skills/tdd-cycle/SKILL.md`.
4. **Full files, never patches.** When proposing a change, show the complete
   updated file, not a diff.
5. **British English. No Oxford comma. Never "simply".** This applies to code
   comments, doc comments, error messages, commit messages, everything.

## What Grob is

A statically typed scripting language with C-style syntax, type inference,
nullable types and first-class file system and process operations. C# / Go
developers should read it without prior knowledge. Designed to fill the gap
between Go (too ceremonious for scripts), PowerShell (syntactically hostile),
and Python (dynamically typed).

The solution graph is a strict DAG. Seven projects: `Grob.Core`, `Grob.Runtime`,
`Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Cli`, `Grob.Lsp`. **`Grob.Compiler`
and `Grob.Vm` never reference each other.** `Grob.Core` is the only shared
ground. When in doubt about what may reference what, fetch
`docs/design/grob-solution-architecture.md`.

## Day-one constraints (Sprint 1 acceptance gates)

These are non-deferrable and shape every file you write:

- **Error-recovering parser.** Always produces a full AST. Error nodes
  (`ErrorExpr`, `ErrorStmt`, `ErrorDecl`) are first-class. Cascade suppression
  via the compiler-internal `Error` type. No diagnostic cap. Stateless. See
  `docs/design/grob-language-fundamentals.md` §29.
- **`SourceLocation` on every AST node.** Line, column, file. Day-one, not
  retrofit-able.
- **`Declaration` back-reference on every identifier node**, set by the type
  checker. Drives go-to-definition and hover in the LSP.
- **Two-pass type checker.** Pass 1 registers all top-level declarations;
  pass 2 validates bodies.
- **`///` doc comments attached to declaration nodes** (recognised and discarded
  in v1, but the attachment must happen day-one).

## Code conventions

- C# 13 / .NET 10 LTS. File-scoped namespaces. Nullable reference types enabled.
- Target framework `net10.0`. `LangVersion` 13.
- xUnit for tests. One test class per type under test. Test method naming:
  `MethodUnderTest_Scenario_ExpectedBehaviour`.
- Solution format is `.slnx` (XML, not legacy `.sln`).
- Windows-native. No Unix paths or commands in any code, comment or doc.
  Backtick raw strings are the canonical idiom for Windows paths in Grob source.
- See `.github/instructions/csharp.instructions.md` for the full coding rules.

## Where to find things

The design corpus lives in `docs/design/`. When you need an authoritative
answer to a question, fetch the relevant file:

| Question                              | File                                        |
| ------------------------------------- | ------------------------------------------- |
| Is decision X settled? What does it say? | `grob-decisions-log.md`                  |
| Parser, type checker, compiler spec   | `grob-language-fundamentals.md`             |
| Built-in type methods                 | `grob-type-registry.md`                     |
| VM and runtime architecture           | `grob-vm-architecture.md`                   |
| `.grobc` binary format                | `grob-grobc-format.md`                      |
| Stdlib modules                        | `grob-stdlib-reference.md`                  |
| Error codes                           | `grob-error-codes.md`                       |
| Sprint scope and Definition of Done   | `grob-v1-requirements.md`                   |
| Solution structure, project graph     | `grob-solution-architecture.md`             |
| Validation suite (release-gate scripts) | `grob-sample-scripts.md`                  |
| Formatter rules                       | `grob-formatter-specification.md`           |

The `grob-spec-lookup` skill (`.github/skills/grob-spec-lookup/SKILL.md`) walks
through how to find the right file for a given question.

## Model selection

Three tiers are available. Pick the cheapest that will do the job well:

- **Local Qwen2.5-Coder via Ollama** — mechanical work. Commit message
  generation, doc comment first drafts, test stub scaffolding from a signature,
  obvious DTO wiring. Free, runs on Chris's GTX 1060.
- **Copilot native Sonnet 4.6** (uses the $39/month AI Credit allowance) —
  routine reasoning. Standard TDD red/green cycles, explaining errors, writing
  tests for a clear spec, refactoring within a file.
- **Anthropic API via BYOK — Opus 4.7** (billed direct, not Copilot credits) —
  design work. Multi-file proposals, architecture decisions, debugging cascading
  failures, anything where the answer matters more than the throughput.

When in doubt, ask Chris which tier to use. Don't escalate silently.

## What you do not do

- You do not invent design decisions. If the spec is silent on something,
  surface the gap and ask.
- You do not paraphrase the decisions log into local opinions. The log is
  authoritative; quote it or point to it.
- You do not suggest features outside Sprint 1 scope unless Chris asks. Stay
  on the work.
- You do not write personality content, mascot references or first-run
  acknowledgements. That's a separate concern handled elsewhere.
- You do not commit directly to `main`. Ever.

## Approval signals

Chris uses terse approval. Treat these as standing:

- **"Agree"** or **"Agree and move on"** — proceed with the proposal as stated.
- **"Continue"** — execute the next step in the pre-agreed plan, no need to
  re-propose.
- **"Stop"** — halt immediately, don't try to finish the in-flight step.

If Chris hasn't agreed and the work isn't pre-agreed, ask before doing.

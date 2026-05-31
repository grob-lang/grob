---
name: "Sprint 3 · Increment B — `grob run`"
description: "Build the RunCommand in Grob.Cli — a thin CLI wrapper that compiles and executes a .grob file through the existing pipeline, with the first end-to-end script test. Establishes the CLI composition pattern the REPL extends."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 · Increment B — `grob run`

You are continuing Sprint 3. Increment A made the language declare, reassign and
scope. This increment surfaces the pipeline at the command line: `grob run
<file>` compiles and executes a `.grob` file. It is deliberately thin — the
pipeline already exists, and `Grob.Integration.Tests` already runs source →
stdout end-to-end. The value of doing it now is focus: it keeps the REPL
(Increment F) from carrying CLI plumbing it does not need, and it puts the §4
`grob run hello.grob` acceptance under continuous regression test as later
increments grow the script surface.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 3 — the `grob run <file>`
   bullet) and §3.5 (test-project routing).
2. `docs/design/grob-solution-architecture.md` — the `Grob.Cli` section
   (`Program`, `RunCommand`, the composition-root constraint that `Grob.Cli`
   references every other `src/` assembly and nothing references it back) and
   the error-formatter responsibility.
3. `docs/design/grob-install-strategy.md` — only if the run command's file
   resolution touches `grob.json` discovery; for a bare `grob run path.grob`
   the file path is explicit and discovery is not required. Do not pull
   project-config resolution into this increment unless §4 names it.
4. `docs/design/grob-personality-identity.md` — the CLI conventions: results to
   stdout, errors to stderr, quiet on success, no emoji in CLI output.

> **Sequencing note.** This is Increment B of the agreed breakdown: A → **B
> (`grob run`)** → C → D → E → F. `grob run` is a wrapper over the existing
> pipeline; it adds no language features. Do not pull `const`/`readonly`,
> nullable or interpolation work forward, and do not build any part of the REPL
> here — `ReplCommand` is Increment F.
>
> **Exit-code and stream conventions — inline reference (authoritative source is
> `grob-personality-identity.md`; reproduced so the implementation does not
> depend on a fetch landing well).**
>
> - Program results print to **stdout**; diagnostics print to **stderr**.
> - A clean run exits `0`. A run with compile-time diagnostics prints them to
>   stderr and exits non-zero **without executing** (the two-mode model:
>   compiler/checker collect all, then the run aborts before the VM starts).
> - A runtime error prints to stderr and exits non-zero — the VM stopped on the
>   first runtime error (Sprint 2 B behaviour).
> - `exit(code)` from the script sets the process exit code.
> - Quiet on success: no banner, no "compiled successfully" chatter.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/cli-run`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 & 2:** front end and back end complete — lexer, grammar-complete
  parser/AST, `OpCode`/`GrobValue`/`Chunk`, disassembler, VM dispatch loop, the
  two-pass type checker, the compiler, the benchmark skeleton. C# 14 (D-310),
  `ErrorCatalog` (D-308), `UnresolvedDecl.Instance` sentinel (D-311).
- **Sprint 3 Increment A:** mutable declarations and assignment — `:=`, `=`
  scope-chain walk, globals (`DefineGlobal`/`GetGlobal`/`SetGlobal`), locals on
  stack slots (`GetLocal`/`SetLocal`), block scoping with `PopN`, type
  annotations, compound assignment and `++`/`--`. The full
  lex → parse → type-check → compile → execute pipeline runs
  declaration-bearing scripts through `Grob.Integration.Tests`.

## Deliverable for this increment

`RunCommand` in `Grob.Cli`, wired into the CLI entry point, plus its end-to-end
test. **A wrapper — no new language behaviour.**

1. **`RunCommand`.** Reads a `.grob` file from the supplied path, runs it through
   the existing pipeline (lex → parse → type-check → compile → VM execute), and
   returns the process exit code per the conventions above. Compile-time
   diagnostics abort before execution; runtime errors stop the VM. Use the
   existing error formatter to render diagnostics to stderr.
2. **CLI wiring.** `Program` dispatches `grob run <file>` to `RunCommand`. A
   missing file, an unreadable file or a missing path argument produces a clear
   stderr message and a non-zero exit — not an unhandled exception.
3. **Streams.** Pass stdout to the VM's `Print` writer (the writer the VM and
   disassembler take, never hard-wired `Console` inside the engines —
   `RunCommand` is the composition point that supplies the real stdout).
   Diagnostics to stderr.
4. **No project-config discovery** unless §4 names it for `grob run`. The path
   is explicit. `grob.json` walk-up is an install-strategy concern for a later
   command surface, not this one.

## Out of scope

No REPL (`ReplCommand` is Increment F). No `const`/`readonly`, nullable or
interpolation (their own increments). No `grob.json` discovery, no
`grob install`/`restore`/`fmt`/`check`/`init`/`search` commands (their own
sprints). No watch mode, no argument-passing-to-script beyond what §4 names. Do
not edit the compiler, VM, parser or AST — `grob run` consumes their public
surface only. If a script behaviour is wrong end-to-end, the suspect is the
compiler or VM from earlier increments, not this wrapper; surface it rather than
patching the engine from here.

## Tests

Per §3.5:

- **Integration tests (`Grob.Integration.Tests`):**
  - `grob run` on a fixture `.grob` file that declares, reassigns and `print`s
    produces the expected stdout and exit `0`.
  - A fixture with a compile-time type error produces the diagnostic on stderr,
    a non-zero exit and **no stdout from execution**.
  - A fixture that triggers a runtime error (e.g. read of an undefined global,
    if reachable in this surface, or checked-arithmetic overflow) produces the
    error on stderr and a non-zero exit.
  - `exit(2)` from a script sets exit code `2`.
  - A missing file path produces a clear stderr message and a non-zero exit, not
    a stack trace.
  - Drive these through `RunCommand` with captured stdout/stderr writers, not by
    shelling out to a built `grob.exe`.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `grob run hello.grob` compiles and executes a script file; results to stdout,
  diagnostics to stderr, exit codes per the conventions.
- Compile errors abort before execution; runtime errors stop on first.
- A missing/unreadable file is handled cleanly, not as an unhandled exception.
- The DAG holds; the compiler, VM, parser and AST are unchanged.

## Model

Sonnet 4.6 (High) throughout — this is mechanical CLI composition over an
existing pipeline. No Opus call-out. The only judgement is matching the
stdout/stderr/exit-code conventions exactly, and those are inlined above.

## Hand-off

Summarise: the `RunCommand` shape, how streams and exit codes are wired, the
file-error handling, and the integration fixtures added. Note for the next chat:
Increment C is `const` and `readonly` — the shared `SingleAssignmentDeclaration`
node with its `Kind` discriminator, the compile-time-constant evaluator over the
§24 allowlist with reference inlining, the `readonly` deep-immutability checker,
the cross-reference rules, and two new error codes (E0205, E0206) added to the
registry and `ErrorCatalog`.

# Sprint 4 Kickoff — Control Flow

> **A record, not a gate.** Like the Sprint 3 kickoff, this prompt does no setup
> and unblocks nothing. It is not a slash command — it is the durable record of
> the agreed A–F breakdown, the load-bearing ordering calls, the new error-code
> assignments and the close-gate, kept under `prompts/sprint-4/` with the
> increment-command archive copies. The increment commands live in
> `.claude/commands/sprint-4-{a..f}.md`. **Start by invoking `/sprint-4-a`.**

Begin Sprint 4 — control flow. Sprint 3 turned the expression-and-declaration
pipeline into one that scopes, reassigns, runs script files and a REPL. Sprint 4
makes it branch and loop. The full scope and acceptance are in
`docs/design/grob-v1-requirements.md` **§5 (Sprint 4 — Control Flow)**. Read it
word for word; it is the build contract.

## First Claude Code sprint (D-314)

Sprints 1–3 ran on GitHub Copilot. Sprint 4 runs on Claude Code in VS Code. The
implementation *workflow* is unchanged — one concern per branch, plan-then-build,
the Husky.NET pre-push gate, CodeRabbit pre-PR, a PR per increment, `main`
protected. What changed is the harness:

- **Durable rules live in `CLAUDE.md`** — always in context, no longer re-inlined
  whole per increment. Each increment command still inlines the rules
  *specific* to it (the closed do-not-touch surface, the exact code numbers, the
  §-references), so the anti-rogue discipline is retained.
- **Plan mode is the approval gate.** Each increment presents its plan and waits
  for approval before editing.
- **Increment prompts are slash commands** (`/sprint-4-a` … `/sprint-4-f`).
- **The Opus 4.8 carve-out is a subagent** (`.claude/agents/grob-lowering-specialist.md`),
  reached only for Increment C's `for...in` lowering, only if it gets fiddly.
- **The external cold-read stays GPT-5.3 Codex**, run via Codex CLI against the
  merged branch (`prompts/sprint-4/sprint-4-qa.md`). CodeRabbit is retained as
  the in-loop reviewer; there is no Claude reviewer subagent.

Full rationale in D-314.

## No new project, no new opcodes

Sprint 4 adds no `src/` project. It is type-checker, compiler-emission and
VM-opcode-arm work over already-parsed nodes. The control-flow opcodes — `Jump`,
`JumpIfFalse`, `JumpIfTrue`, `Loop` — were defined when the `OpCode` enum was
closed in Sprint 2 A, and the reusable `emitJump`/`patchJump` backpatching helper
was built in Sprint 3 D for `?.`. Sprint 4 **reuses** that machinery — it does
not build it. The parser and AST are grammar-complete from Sprint 1; no increment
edits them. `&&`/`||` are jump-based short-circuit, **not** dedicated opcodes
(§3.3).

## The agreed increment breakdown

§5 is one section with one acceptance block, carrying conditionals, loops, loop
control, `for...in` in four forms, `select`, the switch expression and the
calculator gate. Sliced into six on the dependency seams:

- **A — Conditionals.** `if`/`else if`/`else`, `&&`/`||` short-circuit, the
  ternary `?:` — all forward-jump, reusing Sprint 3 D's `patchJump`. The
  conditional-jump foundation. Branch `feat/conditionals`.
- **B — `while` + `break`/`continue`.** The backward `Loop` jump and the
  **loop-context stack** that loop control resolves against. The structural
  increment. Branch `feat/while-and-loop-control`.
- **C — `for...in`.** All forms (array, index, map, range with `step`/descending)
  lowered to `while` over B's context model. The heaviest feature increment; the
  Opus subagent is available. Branch `feat/for-in`.
- **D — `select`/`case`.** First-match, no fall-through, non-exhaustive (D-301),
  an equality + `JumpIfFalse` ladder that pushes **no** loop context. Branch
  `feat/select-statement`.
- **E — Switch expression.** `value switch { p => r, _ => d }` — value-producing,
  exhaustive (type checker), arms unify (reuse A). A different machine from `select`.
  Branch `feat/switch-expression`.
- **F — Calculator close.** The loop-and-`select` `calculator.grob`, the §5
  acceptance, the second VM-execution benchmark baseline against the two-axis
  gate (D-313). Branch `feat/control-flow-close`.

Run them in order, each building and testing green before the next, a fresh chat
per increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **Conditionals first (A), because forward jumps are the foundation.** `if`,
  `&&`, `||` and `?:` are pure forward-jump over the helper Sprint 3 D already
  built. Standing them up first nails the jump-emission shape that B's `while`
  exit, D's `select` ladder and E's switch ladder all reuse. A also confirms the
  Sprint 3 D helper is a reusable primitive, not a `?.` one-off — a finding to
  surface early if it is not.
- **`while` and loop control together (B), and B owns the loop-context stack.**
  `break`/`continue` need a loop to attach to, so they land with the first loop.
  The loop-context stack B builds — exit-jump list plus a **settable** continue
  target — is consumed by C (which retargets continue to the increment step) and
  by D (which must *not* push a context so loop control passes through). Get its
  shape right in B; C and D depend on it.
- **`for...in` after `while` (C), lowered onto it.** Every `for...in` form is a
  `while` lowering — there is no `for` opcode. C depends on B and is the one
  increment with an Opus carve-out, for the lowering sub-problem (slot lifetime,
  keys-array materialisation, step-sign/comparison agreement, the continue
  target).
- **`select` (D) and the switch expression (E) are different machines, kept
  apart.** `select` is a non-exhaustive **statement** (no value, first-match, no
  fall-through). The switch expression is an exhaustive **expression** (produces
  a value, all arms unify, exhaustiveness enforced by the type checker). They
  look similar and are not — D is emission over a ladder; E adds an
  exhaustiveness proof and a leave-a-value discipline. Splitting them keeps D's
  non-exhaustiveness and E's exhaustiveness from contaminating each other.

## Planning constraints recorded here (durable context for the increment prompts)

- **The calculator close-gate is loop-and-`select` based, with no user
  functions.** §5's acceptance names "the calculator smoke test script". It is a
  planning-defined smoke test like Sprint 3's `hello.grob`, **not** one of the
  thirteen release-gate scripts. The Sprint 3 kickoff's aside that the calculator
  "needs functions (Sprint 5)" is **superseded**: the Sprint 4 `calculator.grob`
  is built from the Sprint 1–4 surface only — declarations, scope,
  `const`/`readonly`, conditionals, loops, `select`, the switch expression and
  interpolation. Functions, lambdas and call frames are Sprint 5. A
  function-using calculator, if ever wanted, is a Sprint 5 artifact.
- **Five error codes are assigned this sprint, at the next-free numbers, never
  invented by an implementer.**
  - **E2211 / E2212** — `break` outside a loop / `continue` outside a loop. The
    E22xx statement-context block (E2207 already covers them inside `finally`).
    Registered in **Increment B**.
  - **E0501 / E0502 / E0503 / E0504** — `for...in` subject not iterable /
    single-identifier map iteration (suggest `.keys`) / descending range without
    negative `step` / iterator-variable reassignment. The empty **E05xx**
    sub-block within the E0xxx Type category. Registered in **Increment C**.
  - **E0505** — non-exhaustive switch expression. Registered in **Increment E**.
  Each increment confirms the real next-free number against
  `grob-error-codes.md` before assigning, registers the code in the registry and
  the `ErrorCatalog` (D-308), and uses no `"Exxxx"` literal at a call site. If a
  diagnostic needs a code not listed here and not already registered, the
  implementer **stops and surfaces** — it does not invent.
- **`&&`/`||` are jump-based, not dedicated opcodes** (§3.3). An implementer who
  reaches for a logical opcode stops and surfaces.
- **The numeric range is inclusive both bounds; descending needs an explicit
  negative `step`** (§5). `3..0` without `step -1` is E0503, not a silent
  empty/descending guess.

## The close-gate

Sprint 4 closes (Increment F) on `grob run calculator.grob` over a script that
exercises conditionals, loops, loop control, `for...in`, `select`, the switch
expression and interpolation — the §5 acceptance surface — the way Sprint 3
closed on `grob run hello.grob`. The calculator uses no user functions. Sprint 4
is closeable when Increment F's acceptance is green and the second VM-execution
benchmark baseline passes the two-axis gate (D-313).

## Acceptance to hit (whole sprint)

The §5 acceptance, met across the six increments: all control-flow constructs
work; array `for...in` and map `for k, v` iterate correctly; single-identifier
`for k in m` produces a clear compile error; nested loops with `break`/`continue`
behave as specified; the switch expression is exhaustive; `grob run
calculator.grob` runs. Sprint 4 is closeable when Increment F's acceptance is
green.

## Notes

- **Sequential, one increment per chat.** Fresh zip uploaded at the start of each
  increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 4 code is C# 14 from the first line.
- **`ErrorCatalog` (D-308).** Every diagnostic references a central catalog
  descriptor; the `"Exxxx"` string for any code appears exactly once. New Sprint
  4 codes go in `grob-error-codes.md` and the catalog at the numbers above —
  never as a literal at the call site, never invented.
- **Parser and AST are stable.** Every Sprint 4 increment is type-checker,
  compiler-emission, VM-opcode-arm and (F) fixture work over already-parsed
  nodes. No increment edits the parser, the AST or the closed `OpCode` enum.
- **§5 stays as written.** Whether to rewrite §5 to the A–F structure is a
  documentation-authority call deferred to Sprint 4 close.
- **Model.** Sonnet 4.6 (High) is the code-gen workhorse throughout; the Opus
  4.8 `grob-lowering-specialist` subagent is named only against Increment C's
  `for...in` lowering and gated behind "only if this specific lowering
  interaction gets fiddly", never "this part is hard".
- Start with `/sprint-4-a`.

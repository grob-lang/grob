# Grob — Sprint 1 External QA Brief

> **For:** GPT-5.3-Codex (or equivalent agentic model with terminal access)
> **Role:** Independent cold-reader and adversarial tester of the Grob Sprint 1 implementation
> **From:** Chris — Lead Developer, sole author of Grob
> **Purpose:** A second pair of eyes that has never seen the design corpus. You are
> here to find divergence between what the code does and what the spec says it must do.
> You are _not_ here to redesign the language.

---

## 0. Read the spec from the repository — do not trust this brief for spec detail

This brief is a navigation aid, not a source of truth. It tells you _where to look_
and _what to test_, but it deliberately does not reproduce the specification text,
because a paraphrase drifts from the real document and you would then test against the
drift. **The authority is the repository.** Where this brief and a repo document
disagree, the repo document wins; flag the disagreement so it can be fixed.

Before testing anything, locate and read these files. The design corpus lives under
`docs/design/` (project history — present in the repo, not published to the wiki).
The filenames retain the `grob-` prefix. Verify the paths against the real tree before
relying on them:

- **`docs/design/grob-decisions-log.md`** — the authority on every design decision.
  Numbered `D-###` entries. When anything looks wrong, search this file before filing
  it: most surprises are deliberate and have an entry here. This file outranks every
  other document and outranks this brief.
- **`docs/design/grob-language-fundamentals.md`** — the live spec for the lexer, parser
  and grammar. **§29 is parser error recovery** and is the centre of this QA pass. Read
  §29.1 through §29.6 in full before writing any adversarial parser input. §29.6 is a
  worked example with a stated expected outcome — treat it as a gold master, with the
  sprint-boundary caveat in §4.3 below.
- **`docs/design/grob-v1-requirements.md`** — the sprint plan and Definition of Done.
  The Sprint 1 section is the scope contract: what ships in this sprint and what does
  not. Its Sprint 1 acceptance criteria are the bar you are measuring against.
- **`docs/design/grob-solution-architecture.md`** — the project graph and dependency
  rules.
- **`docs/design/grob-type-registry.md`** — built-in type registry. Contains the entry
  for the compiler-internal `Error` type used by error recovery.

Published wiki versions of some of this material also exist under `docs/wiki/`
(`Language-Specification/`, `Type-Registry/`, `ADR/`, etc.). The wiki is a
reference rendering; **`docs/design/` is the working corpus and the decisions log is
the authority.** If the wiki and the design docs ever disagree, prefer the design docs
and flag the drift.

If the repository tree differs from the above — different directory, different
filenames, files missing — **stop and report the actual layout** rather than guessing
or testing against assumptions. A missing spec file is itself a finding.

Once you have read those, the rest of this brief tells you how to attack the code.

---

## 1. What Grob is, in one paragraph

Grob is a statically typed scripting language with a stack-based bytecode VM, written
in C# .NET 10 LTS. Sprint 1 is the front end only: solution scaffold, lexer,
error-recovering parser, and diagnostic infrastructure. No type checker, no compiler,
no VM in this sprint — those start at Sprint 2. Do not test for them. Everything else
about the language you should learn from the repo docs in §0, not from me.

---

## 2. The single most important instruction: report, do not patch

This codebase reflects deliberate design decisions backed by the decisions log. Many
things that look like bugs or smells are settled. **Your default action is to file a
finding, not to change code.** Before filing anything as a defect, search
`grob-decisions-log.md` for a relevant `D-###` entry.

Two buckets, and every finding goes in exactly one:

- **CORRECTNESS** — the code does not do what a cited spec section says it must. Wrong
  source positions, a missing field the spec mandates, a null dereference, an
  off-by-one in a column number, a parser that aborts where the spec says it must
  recover. You may propose a patch, clearly labelled as a proposal.
- **SEMANTIC / DESIGN** — anything where you are questioning _what the language does_
  rather than _whether the code matches spec_. **Report only. Never patch. One line
  each.** Assume it is deliberate and that a decision-log entry exists. If you can cite
  the `D-###` that settles it, even better — note it and move on; that is not a
  finding at all.

If you are unsure which bucket a finding belongs in, it goes in SEMANTIC / DESIGN.

---

## 3. Scope — what is in Sprint 1 and what is not

The authoritative scope is the Sprint 1 section of `grob-v1-requirements.md`. Read it.
The summary below orients you; the document governs.

### In scope (test these)

- The C# solution structure and project dependency graph.
- The `TokenKind` enum and the `Token` record.
- The lexer — all keywords, operators, literal forms, comment forms.
- The parser — full AST for the v1 grammar, **error-recovering and stateless**.
- The error-node kinds the spec defines for recovery (see §29 of language-fundamentals).
- Source location on every AST node.
- The diagnostic infrastructure and error formatting to stderr.
- The lexer and parser test suites.

### Explicitly OUT of scope (do not test, do not flag as missing)

- Type checker, type inference, resolved types, and any back-reference the spec says
  the _type checker_ populates on identifier nodes — **all Sprint 2**. If a
  type-checker-populated field is null after parsing, that is correct for Sprint 1.
  Confirm which fields the type checker owns by reading the Sprint 2 section of
  `grob-v1-requirements.md`; do not assume.
- Bytecode compiler, opcodes, chunks, the value representation, the VM, the disassembler.
- Standard library, plugins, CLI commands beyond what is needed to parse a file.
- Anything to do with execution, runtime values, or compiled output.

A finding of the form "there is no type checker" is noise. Discard it.

---

## 4. The day-one invariants — highest-value targets

These are the non-deferrable Sprint 1 acceptance criteria — confirm their exact
wording in `grob-v1-requirements.md` and `grob-language-fundamentals.md` §29. They are
the most expensive things to retrofit and therefore the most worth verifying
_empirically_. Prefer a written assertion or a test over an eyeball read for every one.

### 4.1 Source location on every node

The spec requires every AST node to carry a populated source location (line, column,
file) from day one. Write a visitor that walks a parsed AST for a representative
source file and asserts every node has a non-default location that maps back to real
source text. A node with line 0, an empty file field, or a default sentinel is a
CORRECTNESS finding. Confirm the exact field set the spec mandates before asserting.

### 4.2 The parser is error-recovering

This is the centre of gravity. The contract is `grob-language-fundamentals.md` §29 —
read it in full; do not work from memory or from this brief's gloss. Your job is to
verify the implemented parser matches that contract, in particular:

- The synchronisation / recovery-anchor set defined in §29.1 — confirm the
  implementation uses exactly that set, no more and no less.
- The error-node kinds defined in §29.2, their source ranges, and the requirement
  that every AST visitor handles them.
- The unbounded-diagnostics rule (§29.4) and the statelessness rule (§29.5).

Note that cascade suppression (§29.3) is implemented by the _type checker_ via a
special type — that machinery is Sprint 2. In Sprint 1 you verify the _parser_ half:
that it produces the right error nodes, in the right places, with the right source
ranges, and recovers to the right anchors. Do not test cascade suppression itself yet.

**Adversarial input is the job here.** After reading §29, hand-write malformed Grob
and run it through the real parser. Things worth trying:

- Malformed expression mid-function, well-formed code after it — the code after must
  still produce a complete AST.
- Malformed top-level declaration — subsequent declarations must still parse.
- An error _inside_ a parenthesised or bracketed region — confirm recovery does not
  terminate on a newline that sits inside open brackets (§29.1 is explicit on bracket
  depth tracking).
- Unclosed brackets running to EOF — confirm clean termination, no infinite loop, no
  crash.
- Deeply nested malformed constructs — confirm no stack overflow and bounded recovery.
- A file of pure garbage tokens — confirm it terminates with diagnostics and an AST,
  not an exception.

Failure modes you are hunting: a parser that throws instead of recovering, an infinite
loop where recovery fails to advance past the offending token, recovery that swallows
well-formed code past the anchor, or error-node source ranges that are wrong.

### 4.3 The §29.6 worked example — mind the sprint boundary

§29.6 of `grob-language-fundamentals.md` is a worked example with a stated expected
outcome. Reproduce its input against the real parser. **Important caveat:** the spec's
stated final diagnostic count includes a diagnostic produced by the _type checker_
(an undefined-identifier error), which does not exist in Sprint 1. For a Sprint 1 QA
pass, verify only the _parser-produced_ diagnostics and error nodes from that input —
expect the parser's contribution, not the type checker's. Read §29.6 carefully and
separate the parser's output from the type checker's before asserting a count. If the
parser emits more diagnostics than its share of that example, that is a CORRECTNESS
finding; if it emits the type checker's diagnostics too, something is mislayered.

### 4.4 The error-node kinds are first-class

Confirm the error-node kinds §29.2 defines exist as real AST node types, each carrying
the fields the spec lists, and that every AST visitor present in the compiler project
has a case for them — even if that case is a no-op or a guard in Sprint 1. A visitor
that crashes on encountering an error node is a CORRECTNESS finding; surviving a broken
AST is the entire point.

---

## 5. The dependency-graph invariant

`grob-solution-architecture.md` defines a strict project DAG with named rules. Verify
the load-bearing one from the actual `.csproj` / solution-file references, not from
assumption: confirm which two projects the architecture doc says must never reference
each other, then prove from the project files that neither references the other. A
`ProjectReference` that violates the documented DAG is a CORRECTNESS finding. Also
confirm the `src/` projects exist with the exact names the architecture doc lists.
Project shells for later-sprint components may be absent — absence of a later-sprint
project is not a finding; a _misnamed_ or _miswired_ one is.

---

## 6. Lexer-specific checks

Read the lexer section of `grob-language-fundamentals.md` for the exact literal,
escape, comment and keyword rules, then verify the implementation against them. Areas
that reward attention:

- All literal forms the spec lists — integer bases and separators, floats, the string
  forms (interpolated double-quoted, raw backtick inline, raw backtick multiline) and
  regex literals if the grammar includes them. Confirm raw backtick strings do **not**
  process escape sequences (the spec names them the canonical form for Windows paths).
- The exact escape-sequence set the spec permits in double-quoted strings — and that
  nothing outside that set is accepted.
- The three comment forms, and that doc comments are recognised and **discarded** in
  v1 rather than retained as tokens — confirm against the spec.
- Which identifiers are _not_ keywords. The spec deliberately keeps certain built-in
  names out of the keyword set and resolves them later. Check the decisions log for
  the relevant `D-###` and confirm the `TokenKind` enum matches — a built-in that the
  spec says is an identifier appearing as a keyword token is a CORRECTNESS finding.
- The line-continuation heuristic the spec describes, in both directions
  (trailing-token continuation and leading-dot chain suppression).

Do not hard-code the expected keyword set from this brief — derive it from the spec
and the decisions log, because I may have it slightly wrong and you should not inherit
my error.

---

## 7. Things that may look wrong but are likely deliberate

Before filing any of the following as CORRECTNESS, find the governing `D-###` in
`grob-decisions-log.md`. If the log confirms the behaviour, it is not a finding; if the
code contradicts the log, _that_ is the finding. This is a non-exhaustive list of
areas where a cold reader's instinct misfires:

- Control-flow constructs that do not enforce exhaustiveness, or that lack
  fall-through.
- The declaration-versus-assignment operator split, and the absence of a `var`-style
  keyword.
- Immutability keywords where one is inlined with no runtime slot and another is a
  runtime-once binding.
- Functions requiring explicit return types.
- Assignment not being permitted in expression position.
- The absence of multiple return values, operator overloading, or circular imports.
- Checked arithmetic semantics (a runtime concern, not Sprint 1 — but do not flag the
  absence of wrapping behaviour).

The rule is the same throughout: the decisions log is the authority. If your instinct
says "this language should do X differently", that is a SEMANTIC / DESIGN observation
at most — one line, no patch — and if the log already settles it, it is nothing.

---

## 8. How to run the work

1. Build the solution (`dotnet build`). Clean build is the baseline; note warnings,
   but prioritise only those indicating a real defect.
2. Run the existing test suite (`dotnet test`). Report any failure verbatim — a
   failing test in delivered code is a high-priority CORRECTNESS finding.
3. Read the lexer and parser cold against the spec sections in §0. Note divergence.
4. Write your own adversarial inputs per §4.2 and run them through the real parser.
   Prefer adding throwaway xUnit tests in the existing test projects over scratch
   harnesses, so assertions are reproducible.
5. Verify the invariants in §4 and §5 with assertions, not inspection, wherever practical.

---

## 9. Output format

Produce a single findings document. No prose preamble and no summary of how Grob works
— that is all in the repo. For each finding:

```text
[CORRECTNESS | SEMANTIC] — one-line title
  File:     path:line
  Spec:     document and section, or "none / unsure"
  Decision: D-### if you found a governing entry, or "none found"
  Observed: what the code does
  Expected: what the spec requires (CORRECTNESS only)
  Repro:    minimal input or test that demonstrates it (CORRECTNESS only)
  Patch:    proposed diff, labelled as a proposal (CORRECTNESS only, optional)
```

Order: all CORRECTNESS first, by severity (crash / wrong-output / minor), then all
SEMANTIC as a flat list. If a section of this brief produced no findings, say so in
one line — "§4.2 error recovery: no findings" is useful signal. Where you relied on a
spec file that was missing or differently located than §0 expected, say so explicitly
at the top of the document.

Do not fix anything in the SEMANTIC bucket. Do not redesign. Find where the code lies
to the spec — as the spec actually reads in the repo, not as this brief paraphrases it
— and tell me precisely where.

---
description: "Sprint 9 · Gap B — validation script separator conformance + sample drift guard. Gap B is not a parser gap: formatter §3.5's Category A/B rule is normative and the parser implements it exactly. The affected validation scripts are wrong, written in Category A (newline-separated) style for Category B (comma-separated) constructs. Correct them, clear the last Gap-B diagnostics so all eleven scripts parse, and land the drift guard that keeps `grob-sample-scripts.md` and the corpus `.grob` files from diverging again. No grammar change."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Gap B — script conformance and the sample drift guard

D-416 closed Gap A and left Gap B as the last thing between the project and
**all eleven validation scripts parsing** — the release-gate blocker D-409
originally named.

**Gap B is not a parser gap, and this prompt's first job is to correct that
framing.** D-415 recorded it as named-struct and anonymous-struct literal
fields "requiring comma separation even across lines, unlike the `type`-body
convention", presented as an unexplained asymmetry the corpus was silent on.
The corpus is not silent. `grob-formatter-specification.md` **§3.5** carries a
normative two-category rule that predates both audits:

- **Category A — declaration bodies.** `type` bodies separate fields by
  newline.
- **Category B — value lists.** Call arguments, signature parameters, array
  literals, map literals, **named type construction and anonymous struct
  literals** use comma separation, with a trailing comma after every element
  including the last in multi-line form.

`ParseBracedFieldInitList`'s comma loop implements Category B exactly;
`ParseTypeDecl`'s newline loop implements Category A exactly. **The parser is
correct.** The scripts were written in Category A style for Category B
constructs, and nothing checked them against the spec.

The asymmetry also mirrors Go precisely — Go struct *type* definitions separate
fields by newline, Go composite *literals* require commas and a trailing comma
before a closing brace on its own line. C#, Rust, TypeScript and JSON all
require commas in literals. The consistency argument for newline-separated
literals is the one Go considered and rejected, because a literal's elements
are values that may themselves span lines, while a declaration body's elements
are declarations.

**Decision taken by the project owner before this prompt was written: the
scripts are corrected to §3.5. The language does not change.** Do not
re-litigate it; if the gate turns up evidence that genuinely undermines it,
surface that rather than acting on it.

Read, in order:

1. `docs/design/grob-formatter-specification.md` **§3.5** — the Category A /
   Category B rule and its worked example. This is the specification this
   increment conforms the scripts to.
2. `docs/design/grob-decisions-log.md` — **D-416** (Gap A closed; the
   corrected per-script attribution — script 07 carries two Gap-B diagnostics,
   script 11 carries one, and script 11 has no generic call at all);
   **D-415** (where Gap B was first recorded, and mis-framed); **D-409** (the
   eleven-scripts release gate this completes).
3. `docs/design/grob-sample-scripts.md` — the eleven validation scripts as
   published, and the non-Grob blocks (PowerShell originals) interleaved with
   them, which matter for gate item 3.
4. `tests/.../ValidationScriptCorpusTests.cs` and the corpus `.grob` files it
   reads — built by D-415, strengthened by D-416. **The `.grob` files on disk
   are the authority**; the markdown is a publication of them.
5. Confirm the **next free D-number** against the live tail. D-416 is the tail
   as of this prompt; verify, never assume.

---

## Read-only investigation gate

**Read-only means no source or corpus edits. Building and running the CLI is
expected and required.**

Full report to a **scratch file outside the repository working tree**. Do not
stage, commit or archive it. In chat: the file path, one line per gate item,
and **explicitly any STOP condition hit**.

State the expected result for each item before measuring it.

1. **Reproduce the remaining diagnostics.** Run the CLI against all eleven
   corpus scripts. Confirm 04, 05 and 09 parse with zero diagnostics; that 07
   produces exactly two and 11 exactly one; and that **every remaining
   diagnostic across all eleven is a Category B separator violation** and
   nothing else. If any remaining diagnostic is something other than Gap B,
   that is a third gap — **STOP and surface it**, do not fold it in.

2. **Enumerate every Category B violation across the corpus scripts**, by file
   and line, with the construct type (named construction, `#{ }` literal, array
   literal, map literal, call argument list). Expect more sites than
   diagnostics: the parser stops at the first failure in a construct, so one
   diagnostic can mask several missing commas, and a violation inside a
   construct that never parsed may not have been reached at all. **The count of
   diagnostics is not the count of fixes** — this is the item most likely to be
   under-estimated.

3. **Establish which markdown blocks are in scope for the guard.**
   `grob-sample-scripts.md` interleaves PowerShell originals with Grob
   translations, and contains Grob examples that are not among the eleven.
   Determine how a block is reliably identified as validation script *N* —
   fence language tag, section heading, an explicit marker, or something else.
   Recommend the mechanism. If no reliable mechanism exists without editing the
   markdown's structure, say so: adding one is in scope, guessing is not.

4. **Confirm the direction of authority.** The `.grob` corpus files are
   authoritative and the markdown publishes them. Verify nothing else reads the
   markdown blocks as input, and that the corpus files and markdown blocks are
   currently in sync **apart from** the Category B violations — if they have
   already diverged in other ways, that divergence is a finding and changes
   what "sync" means.

5. **Survey Category B violations elsewhere in the corpus** — the examples in
   `grob-language-fundamentals.md`, `grob-stdlib-reference.md`, the wiki, the
   error-examples library. **Report only, fix nothing outside the eleven
   scripts.** This tells us whether the drift is localised or corpus-wide, and
   sizes the sweep item.

6. **Enumerate breaking changes.** Any test asserting a script's current
   diagnostic count or text. Assertions become strictly stronger (zero
   diagnostics) — never weakened, never deleted.

Stop at the end of the gate for approval before any edit.

---

## What you're building

1. **The corrected corpus scripts.** Every Category B construct in the eleven
   gains comma separation and, in multi-line form, a trailing comma after every
   element including the last, per §3.5. Purely syntactic — no script's
   behaviour, structure or intent changes. Any script whose meaning would shift
   is a finding, not an edit.

2. **The markdown synchronised to the corpus.** `grob-sample-scripts.md`'s
   eleven blocks updated to match their `.grob` files exactly.

3. **The drift guard.** A test asserting each of the eleven markdown blocks
   matches its corpus `.grob` file, so a sample corrected in one place and not
   the other fails loudly. This is the standing rule the corpus sweep already
   carries — *every public code sample must exist as a corpus file compiled by
   the release gate so samples cannot drift* — landed for the eleven, which are
   the highest-value case.

   Shape it at the gate. Comparison-based (the test reads both and diffs) is
   preferred over generation-based (the markdown is emitted from the corpus),
   because it needs no build step, works when only the docs change, and fails
   with a readable diff. Whitespace normalisation policy is a gate decision —
   exact-match is the strongest guard and the most brittle; say which you chose
   and why.

4. **`ValidationScriptCorpusTests` strengthened** to assert all eleven parse
   with **zero** diagnostics, replacing the per-script expected-diagnostic
   assertions D-415 and D-416 left in place.

5. **The decision entry** at the verified next-free D-number, three-location
   lockstep, recording: that Gap B was a corpus defect and not a parser one,
   with §3.5 cited as the rule that already settled it; the correction to
   D-415's framing; the violation count versus the diagnostic count; the guard's
   shape and normalisation policy; and the survey result from gate item 5 with
   a recommended owner.

---

## Out of scope

**Any grammar or parser change.** The parser implements §3.5 correctly. If you
reach for `ParseBracedFieldInitList`, stop.

**Category B violations outside the eleven scripts.** Surveyed and reported at
gate item 5, not fixed — that is sweep-sized and needs its own owner.

**Making the scripts run.** They will parse; they will still fail type-checking
and execution on unbuilt modules (`fs`, `json`, `csv`, `process`, `Grob.Http`,
`Grob.Crypto`). Parse-clean is the milestone. Do not stub a module to make a
script run.

Any stdlib module. The `OpCode` enum. Any `GrobValueKind` variant. Any new
error code — this increment mints none.

---

## Tests

- **`ValidationScriptCorpusTests`:** all eleven scripts lex and parse with zero
  diagnostics. Assert zero, not "fewer than before".
- **The drift guard:** passes against the synchronised state. **Mutation-verify
  it** — perturb one markdown block (a single character), confirm the guard
  goes red naming that block, restore. Then perturb the corresponding `.grob`
  file and confirm the same. A guard that only catches one direction is half a
  guard, and D-415's precedent is to record the exact failure signature.
- **Regression:** the full parser suite passes unmodified — nothing in this
  increment touches the parser, so any parser test that moves is a finding.
  D-316 green; the 121-code count unchanged.

---

## Acceptance

- All eleven validation scripts parse with zero diagnostics. **The D-409
  release-gate blocker is fully cleared** — every script that could not parse
  since the sprint began now parses.
- No parser, lexer or AST file is modified.
- The markdown and the corpus `.grob` files agree, enforced by a
  mutation-verified guard that catches drift in both directions.
- The gate-item-5 survey is recorded with a recommended owner, and nothing
  outside the eleven scripts was edited.
- No test weakened or deleted; the script assertions became strictly stronger.
- No new error code; count stays **121**. No opcode change. D-316 green.
- Full solution `dotnet test` green; coverage at or above the floor.
- The decision logged in three-location lockstep at the verified next-free
  D-number.

---

## Model

Sonnet. Mechanical conformance work against a rule that is already normative,
plus one test harness on a pattern D-415 established. The only judgement calls
are the guard's identification mechanism and normalisation policy, both settled
at the gate. No Opus carve-out.

---

## Standing requirements

**Archive this prompt verbatim** to
`prompts/archive/sprint-9/validation-script-separator-conformance.md`,
committed **with** the increment, **as issued and never retrofitted**.

**Report findings outside scope; do not fix them.** Gate item 5 especially.

**A negative result is a good outcome.** If the gate finds a remaining
diagnostic that is not a Category B violation, stopping and surfacing it is the
right outcome, not a failure to complete.

---

## Hand-off

Summarise: the violation count against the diagnostic count and why they
differ; the guard's shape, identification mechanism and normalisation policy,
and both mutation-verification results; the parse-through result for all
eleven; the gate-item-5 survey and its recommended owner; the decision and its
lockstep entry.

Note for the next chat: with Gap B closed, **the eleven-script parse gate is
green** and Sprint 9C proper — Increment C, `fs` — is next. Per D-411 the
Sprint 9 C-onward prompts are **rebuilt, not corrected**; `sprint-9-c.md`
predates the entire consolidation phase and knows nothing of D-356 through this
entry. Still open with no owner: the unharnessed error-examples library (40
pairs documented as a negative-test release gate that nothing runs, with at
least one stale gold master since D-415); the type-name LSP mechanism (§3.1.1
covers identifier expressions, and D-416 ruled it does not cover `TypeRef`, so
go-to-definition on a type name resolves by some path no document names); and
`<T>` resolution itself, E0401 and E0402 still having zero throw sites.

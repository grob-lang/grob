---
description: "Sprint 9 · generic type arguments at call sites (D-415 Gap A). `x.mapAs<T>()` does not parse — ParseTypeArgumentList exists but is reachable only from type positions, and ParsePostfix has no Less case, so `<` falls to the Pratt comparison level. Settle the type-argument-versus-less-than disambiguation rule as a surfaced grammar extension (D-331), then carry type arguments on the call node. Parse only — `<T>` resolution and E0401/E0402 stay with Increment D. Blocks Increment D and five validation scripts."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Generic type arguments at call sites (D-415 Gap A)

D-415 cleared the `param` blocker and, in proving it cleared, isolated two
unrelated pre-existing gaps that stop six of the eleven validation scripts from
parsing. This increment closes the first.

**Gap A: a generic-argument method call reached via member access —
`x.mapAs<Employee>()` — does not parse at all.** D-415 isolated it to
`a.mapAs<Employee>()` alone, with no `param` involvement. It affects validation
scripts 4, 5, 7, 9 and 11.

**This blocks Increment D**, the load-bearing increment of Sprint 9.
`sprint-9-d.md` carries a grammar-first gate that anticipated exactly this —
*"Confirm the parser produces the type-argument node the checker needs… If the
type-argument production for a `mapAs<…>()` call is missing or malformed, that
is a finding — extend it through the `extending-the-grammar` skill, surfaced
not swept, before building on it."* D-415 has answered that gate ahead of
schedule and the answer is that the production does not exist. That prompt's
stated premise — that "the `sort<U>` / `map<K, V>` machinery already parses
`<…>` in the relevant positions" — is false for the only position that matters.

D-415 filed both gaps to "most plausibly the corpus sweep". That is the wrong
owner: the sweep is documentation work and cannot add a parser production.
Hence this increment.

**Scope is parse-only.** Producing the type-argument node and getting the five
scripts past it. Resolving `<T>` to a concrete type, typing the call result, and
wiring E0401/E0402 are Increment D's Opus carve-out and stay there.

Read, in order:

1. `docs/design/grob-decisions-log.md` — **D-415** in full, particularly the
   Gap A isolation and the parse-through table; **D-080** (constrained
   generics — users *consume* generic functions and cannot declare them, the
   constraint that makes this tractable); **D-376** (the map-literal work that
   built `ParseTypeArgumentList` and `LooksLikeMapLiteral`); **D-331** (the
   `extending-the-grammar` procedure this increment runs).
2. `docs/design/grob-language-fundamentals.md` — the expression grammar and
   the precedence table; §29 (recovery, now carrying D-415's `@` anchor).
3. `docs/design/grob-type-registry.md` — the constrained-generics model and
   how a type argument is written and resolved.
4. `docs/design/grob-error-codes.md` — **E0401** (generic type argument count
   mismatch) and **E0402** (generic constraint violation). Both are expected to
   have **zero throw sites**; confirm, and do **not** wire them — they are
   Increment D's.
5. Confirm the **next free D-number** against the live tail. D-415 is the tail
   as of this prompt; verify, never assume.

---

## Read-only investigation gate

**Read-only means no source edits. Building and running the CLI is expected and
required.** Reproduce before designing — D-415's own anchor finding came from a
real CLI run against a grammar that was about to be replaced, and static
reading would have missed it.

Full report to a **scratch file outside the repository working tree**. Do not
stage, commit or archive it. In chat: the file path, one line per gate item, and
**explicitly any STOP condition hit**.

State the expected result for each item before measuring it.

### Verified state this prompt asserts — confirm or refute each

1. `ParseTypeArgumentList` exists in `Parser.cs` and parses `T` / `T, U` up to
   and including the closing `>`, with the opening `<` consumed by the caller.
2. Its only two callers are `ParseTypePrimary` (type annotations) and
   `ParseMapLiteral` — **both type positions**. There is no type-argument
   parsing anywhere in expression position.
3. `ParsePostfix`'s switch has cases for `Dot`, `QuestionDot`, `LeftParen`,
   `LeftBracket` and `Switch`, and **no `Less` case**, so `<` after a member
   access exits the postfix loop and is consumed by the Pratt comparison level
   as `BinaryOperator.Less`.
4. `Lexer.ScanGreater` emits `Greater` or `GreaterEqual` and **never a `>>`
   shift token**, so nested type arguments (`mapAs<map<string, int>>()`) close
   as two separate `Greater` tokens with no lexer work needed.
5. `LooksLikeMapLiteral()` is a bounded forward scan tracking angle-bracket
   depth and deciding on the terminating token — the existing in-tree precedent
   for this class of disambiguation.

If any of these is wrong, the design below changes; surface it before
proceeding.

### The design question — this is the increment

`a.mapAs<Employee>()` is lexically ambiguous with `((a.mapAs) < Employee) >
()`. Every C-family language with generics has had to rule on this. Produce a
**recommendation with a reason**, not a menu:

- **Bounded lookahead on `<`** (the C# approach): on seeing `Less` in the
  postfix loop, scan forward tracking angle depth; if the run closes and the
  **immediately following token is `(`**, treat it as a type-argument list,
  otherwise fall through to the comparison operator. **D-080 makes this
  unusually safe here** — users cannot declare generic functions, so a generic
  call is always immediately invoked, and "closes then `(`" is a much tighter
  trigger than C# can afford.
- **Explicit turbofish** (`::<>`, the Rust approach): unambiguous, no
  lookahead, but alien to the C# and Go developer §1 says must read Grob
  without prior knowledge. Weigh it and expect to reject it; say why.
- **A name-keyed allowlist** of known generic members: rejected in advance —
  it bakes stdlib names into the parser. Named here so it is not rediscovered.

Whichever is recommended, **enumerate the cases the rule decides wrongly** and
the tests that pin them. At minimum: `a < b > (c)`, which is a genuine
comparison chain that the lookahead rule will read as a generic call. State the
chosen behaviour explicitly rather than discovering it in a fixture.

**This is a grammar extension and therefore a surfaced decision (D-331).**
Bring the recommendation to the gate; do not implement on your own authority.

### Further gate items

6. **Where do type arguments live in the AST?** `CallExpr` gaining a
   `TypeArguments` list, versus a distinct node. Recommend one. `CallExpr` is
   the obvious answer — the call is still a call — but confirm nothing walks
   `CallExpr` positionally in a way a new field breaks.
7. **§3.1.1 on the new nodes.** Every `TypeRef` in the argument list needs a
   `SourceLocation`; the checker will later need `ResolvedType`/`Declaration`.
   Establish what this increment must set and what Increment D sets.
8. **Recovery.** A malformed type-argument list (`a.mapAs<>()`,
   `a.mapAs<Employee()`) must not strand recovery. Note that D-415 added `@` to
   the anchor set under a context gate; check whether an unclosed `<` interacts
   with `Synchronise`'s `BracketDepth` gating the way an unclosed `(` does —
   `Less`/`Greater` are **not** bracket-depth tokens in the lexer, which may
   matter here.
9. **Enumerate breaking changes.** Any existing test asserting that
   `a < b > (c)` or similar parses as a comparison. Update to assert new
   correct behaviour; **never weaken or delete**.
10. **Does Gap A also affect free-function calls** (`mapAs<T>(x)`) and not only
    member access? D-415 isolated the member-access form; establish whether the
    bare-identifier form is equally broken, and scope accordingly. One concern —
    type arguments at a call site — covers both if both are broken.

Stop at the end of the gate for approval before any edit.

---

## What you're building

1. **The disambiguation rule**, as approved at the gate, in `ParsePostfix`.
2. **Type arguments on the call node**, reusing `ParseTypeArgumentList`
   unchanged — the production is correct, only unreachable. If it needs
   modification, that is a finding.
3. **`SourceLocation` on every node**, §3.1.1 to the boundary agreed at gate
   item 7.
4. **Recovery behaviour** for a malformed type-argument list, per gate item 8.
5. **The decision entry** at the verified next-free D-number, three-location
   lockstep, recording: the disambiguation rule and the alternatives rejected,
   the cases it decides wrongly and why that is acceptable, the AST shape, and
   the parse-through result for scripts 4, 5, 7, 9 and 11.

---

## Out of scope

**Resolving `<T>`.** No type-argument-to-concrete-type resolution, no
result-typing of the call, no `E0401`/`E0402` wiring. All of it is Increment
D's Opus carve-out and standing it up here would be a second `mapAs`
mechanism.

**Gap B** (named-struct and `#{ }` literal fields requiring comma separation
across lines, where `type` bodies do not). Separate concern, and unlike Gap A
it is a language-design question before it is a parser one — the asymmetry may
be deliberate and nothing in the corpus says.

Any stdlib module. The `OpCode` enum. Any `GrobValueKind` variant. Any new
error code — this increment is expected to mint none.

---

## Tests

- **Parser tests:** `a.mapAs<Employee>()` parses to a call carrying one type
  argument; `a.mapAs<Employee[]>()` and `a.mapAs<map<string, int>>()` parse
  (the nested case proving the two-`Greater` lexing holds); multiple arguments
  parse; a call with no type arguments is unchanged.
- **The ambiguity cases, pinned explicitly:** `a < b > (c)` asserts the chosen
  behaviour; `a < b` and `a > b` still parse as comparisons; `x := a<b` parses
  as a comparison. Full contract per assertion — code, message, line, column,
  per D-405's precedent.
- **Diagnostics:** `a.mapAs<>()` and an unclosed `a.mapAs<Employee()` produce a
  clear diagnostic and do not strand recovery; a following well-formed
  declaration still parses and reports independently.
- **Mutation-verify the disambiguation guard** — delete the lookahead, confirm
  the canonical test fails for the predicted reason, restore. D-415's cycle is
  the template, including recording the exact failure signature.
- **Regression:** the full existing expression and comparison suite passes
  **unmodified**. `LooksLikeMapLiteral` and the map-literal path are untouched;
  `ParseTypePrimary`'s generic-argument path is untouched.
- **`ValidationScriptCorpusTests`** (built by D-415): scripts 4, 5, 7, 9 and 11
  advance past every Gap A diagnostic. **Scripts 7 and 11 also hit Gap B and
  will still fail** — assert their remaining diagnostics exactly, and confirm
  every one is Gap B, never a type-argument diagnostic. That is the positive
  proof Gap A is cleared, on D-415's own pattern.

---

## Acceptance

- `x.mapAs<T>()` parses; the disambiguation rule is implemented as approved and
  its wrongly-decided cases are pinned by test rather than left to be found.
- Scripts 4, 5, 7, 9 and 11 produce no Gap A diagnostic; 7 and 11's remaining
  failures are confirmed Gap B only.
- The map-literal and type-annotation generic paths are untouched, proven by
  their tests passing unmodified.
- No test weakened or deleted; breaking changes enumerated at the gate.
- No new error code; count stays **121**. No opcode change. No `GrobValueKind`
  change. D-316 green.
- Full solution `dotnet test` green; coverage at or above the floor on
  `Grob.Compiler`, with the lookahead and its rejection paths covered rather
  than excluded.
- The decision logged in three-location lockstep at the verified next-free
  D-number.

---

## Model

Sonnet. The disambiguation rule is the one genuinely novel piece, but it is
bounded, has an in-tree precedent in `LooksLikeMapLiteral`, and D-080's
consume-only constraint narrows it further than the C# case. Settle it at the
gate on evidence; escalate only if the gate finds the rule interacts with the
Pratt precedence table in a way this prompt has not anticipated.

---

## Standing requirements

**Archive this prompt verbatim** to
`prompts/archive/sprint-9/generic-call-type-arguments.md`, committed **with**
the increment, **as issued and never retrofitted**.

**Report findings outside scope; do not fix them.** Gap B especially — if
something about it becomes clearer while working here, record it, do not act
on it.

**A negative result is a good outcome.** If the gate refutes any of the five
asserted facts above, that is the gate working.

---

## Hand-off

Summarise: the disambiguation rule and why, the alternatives rejected, the
cases it decides wrongly; the AST shape; the recovery behaviour; the
parse-through result for the five scripts and the Gap B residue on 7 and 11;
the decision and its lockstep entry; anything reported and not fixed.

Note for the next chat: **Increment D is unblocked** for the grammar it
depends on — `sprint-9-d.md`'s grammar-first gate is now answered, and its
stated premise about the `sort<U>`/`map<K, V>` machinery was wrong and should
not be carried into the rebuilt prompt. Per D-411 the Sprint 9 C-onward prompts
are **rebuilt, not corrected**. Gap B and the unharnessed error-examples library
(D-415) both remain open with no owner.

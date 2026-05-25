# Grob — Sprint 1 QA Fix Session

> **For:** the standard working model (Sonnet for the mechanical fixes, Opus if a
> judgement call opens up — see §5).
> **Context:** An external cold-reader (GPT-5.3-Codex) ran an adversarial QA pass over
> the Sprint 1 front end and filed three findings. Those findings have been triaged
> against the decisions log. This session implements the verified fixes. Do not re-run
> the whole QA pass — that work is done. Implement these specific changes, with tests,
> against the live repository.
> **The decisions log is the authority.** Where this brief cites a `D-###` or a spec
> section, that document governs; if the code or this brief disagrees with it, the
> decisions log wins — surface the conflict rather than guessing.

-----

## 0. Ground rules for this session

- Full files, never patches, as deliverables — but you are editing a live repo, so
  make the edits in place and show the complete changed files in your summary.
- Every fix ships with a test. A fix with no regression test is not done.
- British English in comments and docs. No Oxford comma. Never the word "simply".
- After each fix, `dotnet build` then `dotnet test` must be green before moving on.
- Verify before editing: grep the pattern across the codebase before changing one
  site — these fixes likely touch more than one call site each.

-----

## 1. Triage summary — what is real and what is not

Codex filed three findings. The verdicts:

| Finding | Codex bucket | Verdict | Action |
|---|---|---|---|
| Identifier nodes lack day-one LSP metadata fields | CORRECTNESS | **Valid** | Fix — §2 |
| Error-node source ranges violate the §29.2 range contract | CORRECTNESS | **Valid, but Codex's proposed patch is half-wrong** | Fix per §3, not per Codex |
| Two test projects have zero discovered tests | SEMANTIC | **Not a defect** | Tidy only — §4 |

Read §3 carefully. Codex correctly identified that something is wrong with the error
ranges, but its proposed remedy — "derive error-node start from the diagnostic start" —
would *break* a part that is currently correct. The actual fix is different. Do not
implement Codex's patch verbatim.

-----

## 2. Fix 1 — Day-one LSP metadata on identifier and symbol shapes

### What the spec requires

`grob-v1-requirements.md` §3.1.1 and `grob-tooling-strategy.md` ("Foundational
Constraint — Source Location From Day One"), under D-137, require these shapes to
*exist from the first line of compiler code*, with their values *populated later by
the type checker* (Sprint 2):

```csharp
class IdentifierNode : AstNode      // the repo's IdentifierExpr
{
    public string Name { get; init; }
    public GrobType ResolvedType { get; set; }   // set by type checker (Sprint 2)
    public AstNode? Declaration { get; set; }     // set by type checker (Sprint 2)
}

class Symbol
{
    public string Name { get; init; }
    public GrobType Type { get; init; }
    public SourceLocation DeclaredAt { get; init; }
}
```

### What the code does now

`IdentifierExpr` exposes only `Range` and `Name`. The reflection probe in the QA pass
returned `HAS_ResolvedType=False` and `HAS_Declaration=False` — the properties are
*absent*, not merely unset. That is the defect. The distinction matters: an unset
field is correct for Sprint 1; an absent field forces a type-checker audit to retrofit,
which is exactly what D-137 exists to prevent.

### The fix

1. Add the two properties to `IdentifierExpr` (the repo's name for `IdentifierNode`).
   Match the spec's exact intent: `ResolvedType` is set by the type checker, so it is
   a settable property; `Declaration` is `AstNode?` and nullable. Mind the actual
   `GrobType` representation in `Grob.Core` — if `GrobType` is a value type / enum and
   cannot be null, model "not yet resolved" however the existing type system expresses
   absence (a dedicated `Unknown`/`Unresolved` member, or make the property nullable if
   the type permits). Decide this against the real `GrobType` definition, not from
   memory; if neither option is clean, stop and flag it rather than inventing a third.
2. Add `DeclaredAt : SourceLocation` to the `Symbol` shape if a symbol-table type
   exists in the Sprint 1 tree. If the symbol table is genuinely a Sprint 2 component
   and no `Symbol` type exists yet, do **not** scaffold it early — note that the
   `DeclaredAt` requirement lands with the symbol table in Sprint 2, and leave a
   pointer. Confirm which is the case from the actual tree before acting.
3. Both new identifier properties stay **unset** through Sprint 1 parsing. The parser
   does not populate them — that is the type checker's job in Sprint 2. Do not have the
   parser write to them.

### The test

§3.1.1 prescribes a verification test: every identifier node in a *type-checked* AST
carries a non-null `ResolvedType` and a non-null `Declaration`. That test cannot pass
in Sprint 1 because there is no type checker. Do not write it as a passing test now.
Instead:

- Add a Sprint 1 test asserting the *properties exist and are settable* on
  `IdentifierExpr` (a compile-time guarantee plus a simple set/get round-trip), so the
  day-one shape cannot silently regress.
- Leave a clearly-named skipped or pending test (or a `// Sprint 2:` marked stub) for
  the populated-after-type-check assertion, so Sprint 2 has the acceptance check ready
  and the obligation is visible in the suite rather than lost.

-----

## 3. Fix 2 — Error-node source ranges and diagnostic positions

This is the careful one. Read §29.2 and §29.6 of `grob-language-fundamentals.md`
before touching code.

### What the spec actually says

§29.2: *"The error node's source range covers the failed parse from the **first
unexpected token** to the recovery anchor (**exclusive of the anchor**)."*

§29.6, for the worked example `return a +` ... `}`: *"produces an `ErrorExpr` whose
range covers from the `+` to the `}` (**exclusive**). The `}` is the recovery anchor."*

So for that input the `ErrorExpr` range must **start at the `+`** and **end before the
`}`** — it must not include the anchor. And the *diagnostic* (`error: expected
expression after '+'`) must point at the failure site — the `+` — because that is what
the message refers to.

### What the code does now (from the QA probe)

For the §29.6 input: node range `WORKED_ERR_RANGE=2:12-3:1`, diagnostic range
`WORKED_DIAG_RANGE=3:1-3:1`. For the malformed top-level declaration: `ErrorDecl` range
`1:1-2:1`, diagnostic `2:1-2:1`.

Two separate problems, and they must be fixed as two separate changes:

**Problem A — the diagnostic points at the anchor, not the failure site.** The
diagnostic range `3:1-3:1` is the `}`. The message is "expected expression after '+'",
so it must point at the `+` (`2:12`), or at the position immediately after it where the
expected token was missing — match whatever convention the other syntax diagnostics
(E2001 family) already use. Find an existing well-formed syntax-error diagnostic in the
codebase and make the recovery diagnostics position consistently with it. The anchor is
where recovery *resumes*; it is not where the error *is*.

**Problem B — the node range includes the anchor.** The node end at `3:1` is the `}`
position. Whether that "includes" the anchor depends on the `SourceRange` end
convention, which you must establish first:

- **Before changing anything, determine whether `SourceRange.End` is inclusive or
  exclusive** by inspecting how well-formed nodes set their ranges. Write a throwaway
  probe (or read the range-construction sites) for a known-good node — e.g. a simple
  binary expression `a + b` — and see whether its `End` lands on the last character of
  `b`, one past it, or on the next token. That established convention is the law; error
  nodes must obey the *same* convention, not a special one.
- If `End` is **inclusive** (End = last real token of the construct), the error node's
  End must be the last token *before* the anchor — in the worked example, the `+` at
  `2:12`-ish, not the `}`. "Exclusive of the anchor" then means "do not let the anchor
  be the last included token".
- If `End` is **exclusive** (End = one-past, i.e. the start of the next token), then
  End = anchor-position may already be correct, and the only bug is Problem A. Verify
  this empirically before concluding the node range is fine.

**Do not** implement Codex's proposal to set the node *start* from the diagnostic
start. The node start (`2:12`, the `+`) is the *correct* "first unexpected token"
position per the spec — the `+` is where the failed parse began (its RHS was expected
and missing). Changing the start would regress a correct value. The start is right; the
end-exclusivity and the diagnostic position are what need fixing.

### Where to fix

The QA pass located the construction in `Parser.cs` — look for `ExpressionOrError(...)`
returning `new ErrorExpr(RangeFrom(start), ex.Diagnostic)` and the `RangeFrom(start)`
helper, plus the equivalent statement and declaration recovery sites (`ErrorStmt`,
`ErrorDecl`). Grep for every `ErrorExpr` / `ErrorStmt` / `ErrorDecl` construction and
every diagnostic raised at a recovery point — fix them consistently. There are at least
three construction sites (one per error-node kind); confirm the full set before editing.

### The tests

Add error-recovery range tests that encode the §29.6 contract precisely:

- The §29.6 input produces exactly **one parser diagnostic** (the type-checker's
  undefined-identifier diagnostic is Sprint 2 and must not appear here).
- That diagnostic's position is the failure site (the `+`), matching the E2001-family
  convention you confirmed.
- The `ErrorExpr` range starts at the `+` and ends anchor-exclusive (assert against the
  convention you established — last-token-before-`}` if inclusive, `}`-position if
  exclusive).
- Add the equivalent assertion for the malformed-top-level-declaration `ErrorDecl` case
  so both node kinds are pinned.

These tests are the regression guard. Once they pass, the §29.2 contract is enforced
mechanically rather than by reading.

-----

## 4. Fix 3 — Empty test projects (tidy, not a defect)

`Grob.Vm.Tests` and `Grob.Stdlib.Tests` having zero tests is **correct** — the VM and
stdlib are Sprint 2 and later. This is not a finding. The only issue is that empty test
projects emit a "no tests discovered" warning on every `dotnet test`, which is noise
that will later mask a genuine "a project's tests stopped being discovered" signal.

Pick one, lightest-touch:

- Add a single `[Fact]` placeholder per empty project asserting `true` with a comment
  naming the sprint that fills it in (`// Sprint 2: VM execution tests land here`), so
  the project has a discovered test and the warning goes away; **or**
- If the build/test runner supports it cleanly, mark those projects as not-yet-active
  in the test run configuration.

Prefer the placeholder-fact approach unless the repo already has a convention for
pending projects. This is a five-minute tidy — do not over-engineer it.

-----

## 5. Sequencing and model choice

Do the fixes in this order, building and testing between each:

1. **Fix 3** first — it is trivial and clears the warning noise so the test output is
   clean for the real work.
2. **Fix 1** — mechanical once the `GrobType` nullability question is settled. The one
   judgement call is how to represent "not yet resolved" on `ResolvedType`; if the
   clean answer is not obvious from the `GrobType` definition, that is the point to
   pause and decide deliberately (Opus-level call) rather than guess.
3. **Fix 2** last — it is the subtle one and benefits from a clean suite underneath it.
   The inclusive/exclusive convention determination is a genuine judgement point; get
   it right before writing the range assertions.

If any fix turns out to touch a design decision rather than just implementation — for
instance, if the `SourceRange` convention itself is ambiguous in the spec and needs a
ruling — stop and raise it as a decisions-log question (a new `D-###`) rather than
settling it silently in code. The two range-convention possibilities in §3 are an
implementation detail if the spec is clear and a design decision if it is not; decide
which, and route accordingly.

-----

## 6. Decisions-log note

None of these three fixes changes a design decision — they bring the code into line
with existing ones (D-137 for Fix 1, D-300 / §29.2 for Fix 2). So no new `D-###` is
required *unless* Fix 2's range-convention determination surfaces a genuine spec
ambiguity, in which case log the ruling. Update the session changelog footer of any
spec file only if you touched its text; these fixes are code-side and should not need
spec edits. If you find the code was correct and the *spec example* is what's wrong,
that inverts the fix — flag it immediately rather than editing code to match a
mistaken example (precedent: D-307 corrected the §29.6 example once already).

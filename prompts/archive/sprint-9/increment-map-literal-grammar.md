# Consolidation — Increment: map literal construction grammar

**Branch:** `feat/map-literal-grammar`
**One concern:** add the `map<K, V>{...}` literal construction form to the lexer-parser-AST
pipeline and type-check it onto D-374's `MapTypeDescriptor`. This closes a release-gate
blocker. No map members, no mutation.

Runs against the fresh corpus zip carrying D-356 through D-375. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust
this prompt or memory for D-### numbers or error codes.

**This is an `extending-the-grammar` increment.** Follow that procedure in full, as
`adding-an-opcode` would be followed for an opcode. Grammar is a v1-frozen surface: a form
added here is one v1 must support forever.

---

## Authority and context

- **The gap.** Map-literal construction has **no parser production and no AST node** —
  `Parser.cs` has `ParseArrayLiteral` (~line 1178) and `ParseAnonStructLiteral` (`#{ }`,
  ~line 1197), but no `ParseMapLiteral`, and there is no `MapLiteralExpr` among the 34 node
  types in `Ast/`. Found by D-374's plan-mode investigation, confirmed independently by
  `grob-grammar-audit.md` (finding G1).
- **Release-gate blocker.** `grob-sample-scripts.md`'s **Script 11 — Azure Resource
  Provisioning Helper** (line 875 onwards) builds `tags := map<string, string>{...}`. Script 11
  cannot compile today. Same class as the `string`-methods blocker (closed by D-363) and the
  numeric-methods blocker (closed by D-369).
- **The separator is already decided — do not redesign it.** **D-375** ratifies **commas**:
  entries separated by commas, newlines inside the braces insignificant and skipped, trailing
  comma permitted — byte-identical to the array-literal and struct-construction conventions.
  The corpus currently documents "newlines or commas"; D-375 supersedes that and this increment
  applies the corpus consequences (below).
- **D-374 built the type substrate.** `MapTypeDescriptor` (`Grob.Compiler`) carries `V` with no
  `K` field (v1 keys are fixed `string`), is recovered via `MapDescriptorOf(Expression)`, and is
  produced by `ResolveMapValueDescriptor(TypeRef)` from a `map<K, V>` annotation. D-374
  explicitly did **not** build a literal-node tier in `MapDescriptorOf`, *"no map literal exists
  in the parser to populate one"*. This increment adds that tier.
- **Why now, before the map query members.** D-374 records that map tests currently rely on
  hand-built AST/VM-level constructions and `fn`-parameter annotations because no literal exists.
  Landing the literal first lets the query-member increment test through real Grob source.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **`ParseArrayLiteral` and `ParseAnonStructLiteral`** — read both in full. They are the two
   closest precedents (a bracket-delimited element list and a brace-delimited `key: value` list).
   Report their separator handling, `SkipNewlines` placement, trailing-separator behaviour and
   error recovery, so the map literal mirrors them rather than inventing a third style.
2. **The struct-construction path and why it cannot be reused directly.** Named-type construction
   is a `ParsePostfix` case (~line 995) gated on
   `_allowStructLiteral && LooksLikeStructConstruction() && e is IdentifierExpr id`.
   `map<string, string>` does **not** parse to an `IdentifierExpr` — it lexes as identifier `map`
   followed by `Less`. Confirm this, and report where in `ParsePrimary` the map literal belongs.
3. **THE DISAMBIGUATION — the increment's real design work.** `map` is **bindable**: it is absent
   from `TypeChecker`'s `_reservedIdentifiers` (which holds only `formatAs` and `select`) and is
   not a registered namespace symbol the way `int`/`float` became in D-370. So `map := 5` is legal
   today and `map < x` is a genuine relational expression. Confirm both facts, then design the
   disambiguation: on identifier `map` followed by `Less`, speculatively parse a type-argument
   list and require a following `LeftBrace`; rewind to the ordinary relational parse if either
   fails. Report:
   - the rewind mechanism (does the parser already support save/restore of position, or is a
     bounded lookahead helper the better fit — `LooksLikeStructConstruction()` at ~line 1289 is
     the existing lookahead-heuristic precedent);
   - whether rewind can interact badly with error recovery (D-300) or with diagnostics already
     emitted during the speculative parse — **a speculative parse must not emit diagnostics**;
   - whether making `map` a reserved identifier instead would be simpler, and its cost (it is a
     breaking change for any script using `map` as a variable name, and E1103 is the existing
     reserved-identifier code). **Report the trade-off and recommend; do not decide unilaterally.**
4. **`_allowStructLiteral` suppression.** That flag exists so `{` is treated as a block in
   `if`/`while`/`for`/`case` headers. Confirm the map literal must be suppressed in exactly the
   same contexts, and that `for k, v in someMap {` still parses the brace as the loop body.
5. **The AST node and its descriptor tier.** A new `MapLiteralExpr` carrying the type arguments
   and the entry list, with `SourceLocation` on every node (D-137) and participation in error
   recovery (D-300, `ErrorExpr`). Report how it slots into `AstVisitor`/`AstWalker` (both are node
   types in `Ast/`) and how `MapDescriptorOf` gains its literal tier.
6. **Type checking.** All three documented forms carry explicit type arguments
   (`map<string, string>{}`, multi-line, single-line), so `V` comes from the annotation — **no
   element-type inference is required**. Confirm this, and report: how each entry's value
   expression is checked against `V` (the D-371/D-373 `CheckArrayElementArgument` precedent is the
   analogue), what code a mismatch raises (expected: the existing `E0004`), and that keys are
   restricted to string literals in v1 per the registry.
7. **Duplicate keys.** Not documented anywhere. Pin it deliberately: last-wins (the `List<T>`-into-
   dictionary convention, and what `set` will do), or a compile error for duplicate *literal* keys.
   Recommended: **compile error for duplicate string-literal keys**, since they are statically
   visible and a duplicate is always a mistake — but report the preferred reading and the code it
   would use. **Do not mint a new error code**; if none fits, STOP and escalate via
   `allocating-an-error-code`.
8. **Empty literal.** `map<string, string>{}` must produce an empty map with a populated
   descriptor from its type arguments. Confirm the `{}` case is unambiguous against the block rule
   given the required `map<...>` prefix.

Report the grammar production, the disambiguation design and its rewind mechanism, the AST node,
the descriptor tier, the type-check plan, the pinned duplicate-key rule, and the test list.
Then STOP.

---

## Scope boundaries — do NOT

- **Do not redesign the separator.** D-375 ratified commas; apply it, including the corpus
  consequences below.
- **Do not build map members** (`length`/`isEmpty`/`keys`/`values`/`get`/`contains` — the
  follow-on increment) or mutation (`set`/`remove`/`clear` — C0b-2).
- **Do not add non-`string` key support.** v1 keys are `string`; the registry records non-string
  keys as post-MVP. D-374 left a non-`string` key type-argument silently permissive with no error
  code home — **that stays as it is**; do not invent a diagnostic for it here.
- **Do not add a bare `{...}` map literal.** The bare brace rule (`grob-language-fundamentals.md`
  §10) makes `{ }` always a block; all three documented forms carry the `map<K, V>` prefix.
- **Do not change array-literal or struct-construction parsing.** They are the precedent to
  mirror, not to modify — including D-375's explicit rejection of relaxing struct construction to
  accept newlines.
- **Do not emit diagnostics from a speculative parse** that is subsequently rewound.
- **No new opcode. No new error code** — reuse `E0004` for value-type mismatch and the existing
  parser code (`_e2001`) for syntax errors. Count stays 118.

---

## Tests — TDD, red first, same commit

- **All three documented forms parse and run**: empty `map<string, string>{}`; multi-line with
  commas; single-line with commas. Plus a trailing comma in each multi-entry form.
- **The release-gate unblock, asserted directly** — Script 11's `tags` literal shape (four
  string-valued entries, one of which is an expression rather than a literal) compiles and
  produces the right map.
- **THE DISAMBIGUATION — the load-bearing tests.** `map := 5` then `map < 10` still parses as a
  comparison and evaluates correctly; `map<string, int>{...}` parses as a literal in the same
  file; and a malformed `map<string, int> 5` (type args with no following brace) rewinds cleanly
  and produces a sensible diagnostic rather than a cascade.
- **Header suppression**: `if someMap == other {`, `while cond {`, `for k, v in m {` and
  `case x {` all still treat `{` as a block, with no map literal misfire.
- **Descriptor populated from the literal**: `m := map<string, int>{"a": 1}` then `m["a"]` types
  as `int?` (D-374's indexer typing), and `for k, v in m` binds `v` as `int` — proving the new
  `MapDescriptorOf` literal tier works, not merely that the literal parses.
- **Value type-checking**: `map<string, int>{"a": "x"}` raises `E0004`; a nested
  `map<string, int[]>{"a": [1, 2]}` resolves and its element accesses type correctly.
- **Duplicate keys** behave per the pinned rule, tested both ways.
- **Error recovery** (D-300): an unterminated literal and a malformed entry each produce a
  diagnostic and allow parsing to continue, with no cascade.
- **`SourceLocation`** correct on the literal node and each entry — spans checked, not assumed.
- Every existing array-literal, struct-construction, `for...in` and map-indexer test unchanged.

---

## Also in scope — D-375's corpus consequences

- `grob-type-registry.md`: the separator-rules block (the "newlines or commas, both are valid"
  line) and the multi-line Construction example → commas, citing D-375.
- `grob-language-fundamentals.md` §14: the line-1321 map example → commas. Consider adding a
  **Map literals** subsection to §8, which currently documents Integer, Float, String, Bool, Nil
  and Array literals but has no map entry — the likely reason this gap survived.
- `grob-sample-scripts.md` Script 11: the `tags` literal's four entries → commas.
- **Grep the corpus for every other map-literal example** and update it; report the full list
  rather than assuming these three are all.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-376**; confirm, do not assume. The
  entry records: the map-literal grammar added under `extending-the-grammar`, closing the third
  release-gate blocker; the production and the `MapLiteralExpr` AST node; **the disambiguation
  design in full** — the speculative-parse-and-rewind mechanism, why struct construction's
  postfix path could not be reused, and whether reserving `map` was considered and why rejected
  or adopted; the `_allowStructLiteral` suppression contexts; `MapDescriptorOf`'s new literal
  tier and that `V` comes from the explicit type arguments with no inference needed; the pinned
  duplicate-key rule; D-375's separator applied and every corpus site updated; and that map
  members remain unbuilt pending their own increment. No new opcode, no new error code, count
  118. Cite D-375, D-374, D-137, D-300, D-371, D-373, `grob-grammar-audit.md`.
- **Update `grob-type-registry.md`** — the `map<K, V>` build-status note records literal
  construction as built (members still pending), citing this D-###. Update
  `wiki/Type-Registry/map.md` to match.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages,
  updated sample scripts). Archive this prompt under `prompts/archive/sprint-9/`.

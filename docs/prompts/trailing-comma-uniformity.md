# Increment prompt — trailing-comma uniformity, and E2209's first throw sites

**Authority: D-421.** Read that entry in `docs/design/grob-decisions-log.md` before
starting. This prompt implements it; where the two differ, D-421 wins and the difference
is a finding to report.

**Model:** Sonnet (High). No Opus carve-out — this is a bounded grammar change with a
fully enumerated site list, not a structural sub-problem.

**Branch:** one concern, one branch. Everything below is the one concern.

**Archive this prompt verbatim** in this increment's own commit, exactly as issued.
Never retrofit it to match what the work turned out to be; if the work diverges, the
divergence goes in the decisions-log entry, not into this file.

---

## 1. Read-only investigation gate

**No source edits until this gate is complete and reported.** This is a read-only
investigation gate, not a "plan-mode gate" — building and running the CLI is expected
and required during it. You need a working `dotnet build` and hand-run `.grob` files to
answer the questions below; what you may not do is change production code.

Write the investigation report to a **scratch file outside the working tree**
(e.g. `../grob-scratch/trailing-comma-investigation.md`). Do not paste it into chat.
In chat, give only the file path and the STOP conditions in §6, if any fired.

### 1.1 Reproduce before designing

D-421's site table is from code reading, not from execution. Confirm every row
empirically. For each of the eleven comma loops below, write a minimal `.grob` file and
run it through the CLI. **State the expected result before you run it**, then record what
actually happened.

| # | Construct | `Parser.cs` (as of this tree) | D-421 expects |
|---|---|---|---|
| 1 | Function signature parameters | `ParseParameterList`, ~517 | rejects → must accept |
| 2 | Function-type parameters `fn(T1, T2): R` | `ParseTypePrimary`, ~572 | rejects → must accept |
| 3 | Generic / map type-argument list | `ParseTypeArgumentList`, ~600 | rejects → must accept |
| 4 | `for k, v in` variable pair | `ParseForIn`, ~704 | rejects → **stays rejecting** |
| 5 | `select` / `case` pattern list | `ParseSelect`, ~861 | rejects → must accept, **gate-conditional** |
| 6 | Braced field-init list (anon + named struct) | `ParseBracedFieldInitList`, ~1271 | already accepts → unchanged |
| 7 | Switch-expression arms | `ParseSwitchArms`, ~1304 | already accepts → unchanged |
| 8 | Call arguments | `ParseCallArguments`, ~1382 | rejects → must accept |
| 9 | Array literal | `ParseArrayLiteral`, ~1492 | already accepts → unchanged |
| 10 | Map literal | `ParseMapLiteral`, ~1566 | already accepts → unchanged |
| 11 | Lambda parameters | `ParseLambda`, ~1940 | rejects → must accept |

Line numbers are orientation, not identification — find the loops by method name.

**A count is only as good as its worst-verified row.** If any row is inference rather
than a run, mark it as such in the report rather than presenting the table as
measurement.

### 1.2 The case-pattern hazard — the one thing that can change the scope

`select` case patterns are terminated by `{`, not by a bracket. Determine whether

```grob
select (code) {
    case 200, 201, { print("ok") }
}
```

can be accepted without the pattern loop consuming the block. `_allowStructLiteral` is
already `false` in a case-pattern position (see `ParseSelect`'s save/restore around the
pattern loop), which is the reason to *expect* this is safe and is not a reason to assume
it. Check specifically:

- what `ParseExpression()` does when it is entered at `{` with `_allowStructLiteral`
  false;
- whether the `break`-on-terminator guard can be written against `TokenKind.LeftBrace`
  without changing the diagnostic for a genuinely malformed pattern;
- whether `case 200, {` (trailing comma, no further pattern) and `case , 200 {`
  (leading comma) stay distinguishable.

**If the hazard is real, that is a good outcome, not a failure.** Exclude row 5, record
the mechanism, and correct `grob-language-fundamentals.md` §16's `case 200, 201,` example
and `grob-formatter-specification.md` §3.5's Category C sentence in the same commit. Do
not force it.

### 1.3 Enumerate breaking changes before editing

Search the whole tree — `tests/`, `docs/errors/examples/`,
`tests/fixtures/`, the eleven validation scripts — for anything that asserts the
*current* rejection: a test expecting `E2001` from a trailing comma in a call or
parameter list, or a gold master whose expected output depends on it.

A first-pass scan of test string literals found **zero**. Confirm or refute that against
raw/verbatim string literals too, which the first pass would have missed. Report the
number before you change anything.

Tests may be updated to assert new correct behaviour. **Never weakened, never deleted.**

### 1.4 E2209's two throw sites

Confirm the current diagnostics for `(x,)` and `foo(,)` — expected to be a generic
`E2001: expected ')'` in both cases. Record the exact current message, file, line and
column for each, since these become the before-and-after evidence.

Confirm `E2209` has no throw site anywhere in the tree (`ErrorCatalog.E2209` should be
referenced only by its declaration and the registry's all-codes list).

Confirm that `[, 1]` and `foo(1,, 2)` — leading and doubled commas — currently produce a
diagnostic at the position where an element was expected. D-421 keeps these as `E2001`
deliberately. If they currently produce something else, report it.

---

## 2. Scope

### In scope

1. Accept an optional trailing comma in rows 1, 2, 3, 8 and 11, and in row 5 unless
   §1.2 vetoes it.
2. Give `E2209` throw sites at `(x,)` and `foo(,)`, replacing the generic `E2001`.
3. Regression tests on rows 6, 7, 9 and 10 — no behaviour change, but pin the
   already-correct behaviour so the accept/reject split cannot silently reopen.
4. Fix script 07's `http.get(` at lines 23–26 — add the trailing comma §3.5 requires,
   in **both** `tests/fixtures/validation-scripts/07-rest-api-data-pull.grob` and the
   matching fence in `docs/design/grob-sample-scripts.md`, in one commit.
   `ValidationScriptMarkdownSyncTests` (D-417) byte-matches the two and will fail if you
   touch only one.
5. Fix the five documented examples that do not parse, listed in D-421's table
   (`grob-language-fundamentals.md` §16 ×2, `grob-formatter-specification.md` §3.2, §3.11
   and §6). These are already correct as *target* state; verify each parses after the
   change rather than editing them.
6. The decisions-log entry recording what landed.

### Out of scope — report, do not fix

- The other seven multi-line parenthesised calls in the validation scripts (scripts 03,
  05, 06, 08, 09). Each acquires a trailing comma when `grob fmt` exists; none is a
  defect now. Leave them.
- Anything in the Deferred Work Register. If you find something that belongs there,
  report it for the register; do not action it.
- `grob fmt` itself (Sprint 12).
- Leading and doubled commas — `E2001` by decision, not omission.

**D-420's stopping rule applies.** Findings from this increment go to the register, not
into this run, unless they block this increment or block Sprint 9C.

---

## 3. Implementation notes

**The shape already exists in the tree.** Rows 6, 7, 9 and 10 all use the same pattern:

```csharp
while (Match(TokenKind.Comma)) {
    SkipNewlines();
    if (Check(TokenKind.RightBrace)) break; // trailing comma
    …
}
```

Rows 1, 2, 3, 8 and 11 are the same loop without the guard. Prefer applying the existing
pattern to introducing a new abstraction — a shared helper across five loops with three
different element parsers and three different terminators is likely to cost more than it
saves. If you conclude otherwise after seeing all five, say so in the report with the
reasoning; do not decide it silently in either direction.

**`ParseTypeArgumentList` is terminated by `Greater`, not a bracket.** Its guard is
`Check(TokenKind.Greater)`. Check the interaction with `LooksLikeTypeArgumentList`
(D-416): its accepted token run already admits `,`, so `x.mapAs<T,>()` currently commits
to the generic reading and then hard-fails. After the change it should parse. Pin that
with a test — it is D-421's named side effect.

**`ParseParameterList` takes a `terminator` parameter** and is called with
`TokenKind.RightParen`. Use the parameter, not a hardcoded token.

**E2209's message text** should name the position, not the construct — the two cases are
a grouping paren and an empty argument list, and a reader needs to know why *this* comma
is wrong when trailing commas are legal everywhere else. Follow
`grob-personality-identity.md` on tone. Raise it through `ErrorCatalog.E2209`, never a
literal (D-308).

**No AST change.** D-421 Decision 4: no node records whether a trailing comma was
present. If you find yourself wanting one, that is a finding to report, not a field to
add.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code. Quarantine with a
documented reason rather than weakening a test or merging red.

Required coverage:

- **Per row, single-line and multi-line**, for all six changed constructs: with and
  without a trailing comma, both parsing to the same AST.
- **Rows 6, 7, 9, 10** — the unchanged-behaviour pins.
- **`for k, v, in map`** — still an error, with its current diagnostic unchanged.
- **`(x,)` and `foo(,)`** — `E2209`, with exact code, message, line and column.
- **`[, 1]` and `foo(1,, 2)`** — still `E2001`, at the element position.
- **`x.mapAs<T,>()`** — parses (the D-416 side effect).
- **Error recovery unchanged.** A trailing comma must not become a synchronisation
  anchor and must not change any §29 recovery path. Add at least one test with a
  malformed element *after* a trailing comma in each changed construct, asserting the
  diagnostic count and positions are what they were before.
- **All eleven validation scripts still parse with zero diagnostics**
  (`ValidationScriptCorpusTests`), and `ValidationScriptMarkdownSyncTests` is green after
  the script 07 edit.

**Mutation-verify the E2209 sites.** For each of the two, remove the new throw, confirm
the corresponding test fails, and **record the exact predicted failure signature before
running it** — then restore. A guard verified in one direction is half a guard.

---

## 5. Deliverables

- The parser change and tests.
- `ErrorCatalog.cs` unchanged in shape — `E2209`'s descriptor is already correct; only
  its throw sites are new. **No error code added, retitled, removed or status-changed.
  Count stays 121.** Confirm the D-316 consistency gate is green and say so explicitly.
- Script 07 and its markdown fence.
- Coverage: `Grob.Compiler` at or above the D-328 90% line-coverage bar. Report the
  actual figure.
- Full solution `dotnet test` green. Report the test count.
- A decisions-log entry appended in three-location lockstep — summary index row, full
  ADR entry, footer changelog. **Take the D-number from the live tail of the merged
  log**, never from memory or this prompt.

---

## 6. STOP conditions

Stop and report, do not proceed:

- Any row in §1.1 behaves differently from what D-421 expects — in either direction.
  A construct that already accepts a trailing comma when D-421 says it rejects one is
  as important as the reverse.
- The case-pattern hazard in §1.2 is real. Report the mechanism and the proposed
  exclusion; do not implement the exclusion unilaterally.
- §1.3 finds any existing test or gold master asserting the current rejection.
- Accepting a trailing comma anywhere changes a §29 recovery path or a diagnostic
  position in an unrelated construct.
- `E2209` turns out to have a throw site already, or its registry description does not
  match `ErrorCatalog.cs`.
- The change requires an AST field, an opcode, a `GrobValueKind` variant, or a new error
  code.

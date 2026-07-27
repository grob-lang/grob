# Grob — Grammar-vs-Implementation Audit

**Date:** July 2026
**Corpus state:** D-374. `src.zip` and `docs.zip` of the same date, both verified current
(`NumericMethodsPlugin` D-369, `MapTypeDescriptor` D-374 present).
**Method:** every documented syntactic form diffed against the live `Lexer.cs`, `Parser.cs`
and `Ast/` (34 node types). Claims verified against source, not against other documents.
**Scope:** literal forms, operator forms, statement forms, declaration forms, type-annotation
forms. This is the territory the advertised-vs-built audit (July 2026) did **not** cover —
that audit swept type members, stdlib functions, error codes and keywords, and explicitly
noted grammar as unswept.

> **Status: partly superseded — this is a dated snapshot, not a live tracker.**
> The findings below record the corpus as it stood at **D-374** and are kept verbatim as the
> record of why the follow-on work was commissioned. Since then:
> **G1 (map literal construction unbuilt) is closed by D-376**, which built the parser
> production, the `MapLiteralExpr`/`MapEntry` AST nodes, type checking and the `NewMap`
> opcode; **G2 (separator convention) is settled by D-375** — comma-separated, newlines
> skipped, trailing comma permitted, matching every other literal form. **G3 remains open.**
> The decisions log is the authority for current state; check it before using this document
> for release planning.

---

## Summary

| # | Finding | Class | Priority |
|---|---------|-------|----------|
| G1 | Map literal construction has no parser or AST production — **release-gate blocker** *(closed by D-376)* | Advertised-but-unbuilt | **P1** |
| G2 | Map literal's documented separator convention contradicts every other literal form in the language *(settled by D-375)* | Design contradiction | **P1** |
| G3 | Scientific notation: `grob-language-fundamentals.md` defers it post-MVP, `wiki/Type-Registry/float.md` documents it as valid | Doc-vs-doc contradiction | P2 |
| ✓ | Regex literals — **built**, contrary to earlier assumption | Correctly built | — |
| ✓ | Nullable type suffixes, function types, array types, grouped types | Correctly built | — |
| ✓ | All statement, declaration and operator forms | Correctly built | — |

**One release-gate blocker, one design question that must be settled before it can be fixed,
and one doc contradiction that turns previously-queued code work into a doc edit.**

---

## G1 — Map literal construction is unbuilt (P1, release-gate blocker)

`Parser.cs` has `ParseArrayLiteral` (line 1178) and `ParseAnonStructLiteral` (`#{ }`,
line 1197). There is **no `ParseMapLiteral`, and no `MapLiteralExpr` among the 34 AST node
types**. Independently confirmed by D-374's own plan-mode investigation.

**The release gate depends on it.** `grob-sample-scripts.md` **Script 11 — Azure Resource
Provisioning Helper** (line 875 onwards) builds:

```grob
tags := map<string, string>{
    "environment": environment
    "deployedBy":  "grob"
    ...
}
```

Script 11 cannot compile. This is the same class as the `string`-methods blocker (closed by
D-363) and the numeric-methods blocker (closed by D-369): documented as settled, used by the
validation suite, never built, never scheduled.

**Documented in two places as settled.** `grob-type-registry.md`'s `map<K, V>` Construction
section gives three forms (empty `map<K,V>{}`, multi-line, single-line); `grob-language-fundamentals.md`
line 1321 uses the multi-line form in its §14 line-continuation example. Note that §8
("Literals") has subsections for Integer, Float, String, Bool, Nil and **Array** — but **no
Map literal subsection**, which is likely how the gap survived: the form is documented by
example and in the registry, never as a grammar production.

**Architectural note for whoever builds it.** Named-type construction (`TypeName { field: value }`)
is a **`ParsePostfix` case**, at line 995:

```csharp
case TokenKind.LeftBrace when _allowStructLiteral && LooksLikeStructConstruction() && e is IdentifierExpr id:
```

That path **cannot** be extended to map literals as-is, because `map<string, string>` does not
parse to an `IdentifierExpr` — it parses as identifier `map`, then `<` as a relational operator.
A map literal therefore needs handling at `ParsePrimary` with speculative lookahead: on seeing
identifier `map` followed by `<`, attempt a type-argument list followed by `{`, and rewind to a
relational parse if that fails. `LooksLikeStructConstruction()` (line 1289) is the existing
lookahead-heuristic precedent, and `_allowStructLiteral` is the existing suppression flag for
contexts where `{` must be a block (`if`/`while`/`for`/`case` headers) — a map literal needs the
same suppression.

Whether the ambiguity is genuine depends on whether `map` is bindable as a value identifier.
`_reservedIdentifiers` holds only `{formatAs, select}`, and `map` is not registered as a
namespace symbol (unlike `int`/`float` since D-370), so `map := 5` may well be legal today —
which makes `map < x` a real comparison and the lookahead genuinely necessary. **Confirm before
designing.**

This is an `extending-the-grammar` decision, as D-374 noted.

---

## G2 — The documented map literal breaks the language's separator convention (P1)

**Every existing literal form in Grob requires commas.** Verified in source:

- `ParseArrayLiteral` (line 1178): `while (Match(TokenKind.Comma))` — newlines are skipped
  around elements, but a comma is the separator.
- Struct construction (line 1005): `if (!Match(TokenKind.Comma)) break;` — identical convention.
- `grob-language-fundamentals.md` §3.1 (line 186) states of switch arms:
  *"Newline-as-separator is not supported."*

**The documented map literal uses newlines instead.** Both `grob-type-registry.md`'s multi-line
Construction example and Script 11 separate entries by newline with **no commas**. The registry
also documents a comma-separated single-line form — so as written, `map` would be the only
literal in the language accepting two separator conventions, and the only one accepting bare
newlines.

This must be settled **before** G1 is built, because it determines the grammar:

- **Commas (recommended)** — consistent with array literals, struct construction and the
  explicit switch-arm rule. Requires updating the registry's multi-line example, the
  fundamentals §14 example, and Script 11. Those are specification, not shipped code, so the
  cost is three edits.
- **Newlines** — matches what is written today, but makes map the sole exception to a
  convention the language states explicitly elsewhere, and adds a newline-significance rule
  inside braces where newlines are otherwise insignificant (the lexer tracks `_depth` for
  exactly that reason).

A language whose stated identity is "readable by any C# or Go developer without prior
knowledge" is better served by one separator rule than by a per-literal exception. But this is
a design call, not a mechanical fix, and it needs a D-### before the increment runs.

---

## G3 — Scientific notation is a doc contradiction, not a grammar gap (P2, doc-only)

`grob-language-fundamentals.md` §8, line 468, states plainly:

> Scientific notation (`1.5e10`, `2.3E-4`) is deferred to post-MVP.

`Lexer.ScanNumber` correctly implements that — it handles decimal, hex (`0x`), binary (`0b`),
underscore separators and `.`-fractional floats, with no exponent handling. The lexer and the
authoritative grammar spec **agree**.

The contradiction is that `wiki/Type-Registry/float.md` documents `1.5e10` as valid float
literal syntax — which is what led the D-369 session to hit a lexer error and work around it
with a plain-decimal literal, and what was subsequently queued as a "lexical forms" code
increment.

**That queued increment is not needed.** The correct fix is a **doc edit**: correct
`wiki/Type-Registry/float.md` to match the fundamentals' deferral. No lexer work, no grammar
change. The previously-planned exponent-notation increment can be struck from the queue and
the correction folded into the corpus sweep.

The D-369 workaround (a large plain-decimal literal in the integration test) is correct as it
stands and needs no revisiting.

---

## Clean results (recorded so they need not be re-derived)

**Regex literals are built.** Earlier working assumption was that F9's regex-literal grammar
was unimplemented. It is not: `Lexer.ScanRegexLiteral` (line 382) emits `TokenKind.RegexLiteral`,
and `Parser.cs` line 1136 consumes it into a `RegexLiteralExpr` with pattern and flags. **F9's
real scope is narrower than recorded** — it is a *specification* gap (no regex-literal section
in `grob-language-fundamentals.md`, the `/`-disambiguation rule living only in a decisions-log
one-liner), not missing functionality. The pre-Increment-F work is documentation of existing
behaviour, which is a materially smaller job.

**Type-annotation grammar is complete** (D-326/D-327): nullable suffix `T?`, array suffix `T[]`,
function types `fn(T1, T2): R`, parenthesised grouping `(fn(): T)?`, and generic arguments
`map<K, V>` in type position all parse. The `IsNullable: false` literals at Parser 423/459/472
are initial values — the suffix loop at line 425 sets nullability via `with { IsNullable = true }`.
Fixed-size array types are explicitly rejected with a diagnostic, as specified.

**All statement forms parse**: `if`/`else`, `while`, `for...in` (collection and numeric range
with `step`), `select`/`case`, switch expressions, `try`/`catch`/`finally`, `throw`, `return`,
`break`, `continue`, blocks.

**All declaration forms parse**: `fn`, `type`, `const`, `readonly`, `param` blocks with `@`
decorators, `import` with `as` aliasing.

**All other literal forms parse**: integer (decimal, `0x`, `0b`, underscore separators), float
(fractional, underscore), interpolated strings with `${}`, raw backtick strings, raw triple-backtick
blocks, bool, nil, arrays, anonymous structs `#{ }`, regex literals, ranges `..` with `step`.

**Escape sequences** are exactly the documented set (`\n \r \t \\ \" \$`), with any other `\x`
rejected rather than silently passed.

**Line continuation** is implemented via bracket-depth tracking (`_depth`), matching §14.

**The bare brace rule** is enforced: `{ }` is always a block, `#{ }` is an anonymous struct,
`TypeName { }` is named-type construction, with `_allowStructLiteral` suppressing the postfix
path in header contexts.

---

## Recommended disposition

1. **G2 first** — settle the separator convention as a D-### in a planning session. It
   determines G1's grammar and cannot be decided inside the implementation increment.
2. **G1 next** — the map-literal increment, under `extending-the-grammar`. Release-gate
   blocker; unblocks Script 11, and lets the map query-member increment test through real
   source rather than the hand-built AST constructions D-374 was forced to use.
3. **G3** — fold the `wiki/Type-Registry/float.md` correction into the corpus sweep. **Strike
   the queued exponent-notation increment.**
4. **F9's scope correction** — record that regex literals are built and that the pre-Increment-F
   work is spec-writing only.

**The meta-point.** The advertised-vs-built audit found two release-gate blockers in the type
and library surface; this one finds a third in the grammar. All three share a mechanism: a form
documented as settled, exercised by the validation suite, with no build-status marking anywhere
to reveal that it was never implemented. That is the same structural cause the earlier audit
recorded as finding A2, and it remains the highest-leverage fix available — a build-status
convention that a cold reader can falsify at a glance, rather than one that requires an audit to
discover.

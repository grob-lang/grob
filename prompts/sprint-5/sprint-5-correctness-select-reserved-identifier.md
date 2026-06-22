# Sprint 5 — Correctness Increment: `select` reserved-identifier resolution

> **Slot.** Inserted immediately after the merged Increment C
> (`feat/lambdas-and-natives`), ahead of D/E/F. This is a correctness fix, not a
> feature increment, so it does **not** take a feature letter and the feature
> increments D, E and F keep their letters. It must merge before the increment
> that registers the array higher-order natives (`filter`/`select`/`sort`/`each`),
> because the parser must accept `.select(...)` before those scripts can run.
>
> **Authority.** D-320 (decisions log). Spec text already merged in
> `grob-language-fundamentals.md` §3 and §12, `grob-error-codes.md` E1103, and
> `grob-v1-requirements.md` `TokenKind` list. This increment implements that
> decision in the product source.
>
> **Branch.** `fix/select-reserved-identifier` — one concern.

---

## Plan-before-code gate

Post a plan and get approval before writing any product source. The plan must
name every file you intend to touch and confirm it sits inside the closed
surface below. Do not begin implementation until the plan is approved.

## The problem this fixes

D-280 made `.select()` the universal pipeline transform on `T[]`. D-301 made
`select` the non-exhaustive statement keyword. The lexer reserves `select` as a
keyword, so it emits a keyword token; the member-access parser expects an
identifier after `.` and rejects `arr.select(...)`. The type checker, compiler
and VM never meet that barrier because they operate on the registered native, so
they support a call no source program can write. Six release-gate scripts (3, 5,
7, 8, 9, 12) call `.select(...)` from source and cannot parse today.

The fix: `select` becomes a **reserved identifier, not a hard keyword** — the
mechanism D-282 already defines for `formatAs`. It lexes as an identifier, stays
legal as a member name after `.`, is promoted to the select statement only at
statement head, and may not be a user binding (E1103).

## Closed surface

Touch only:

- the lexer (keyword table / token classification)
- the statement parser (statement-head dispatch) and, if a special-case exists,
  the member-access parser
- the type checker's declaration-binding path
- `ErrorCatalog` (the E1103 descriptor)
- `tests/Grob.Consistency.Tests` (one new check)
- the relevant unit and integration test projects

Do **not** touch the VM, the compiler emit path, any stdlib native registration,
or the array higher-order method surface. Registering the `.select()` native is
the *next* increment's job; this increment makes `.select` **parse**, not
**resolve**.

## Implementation

**Lexer.** Remove `select` from the keyword set so it lexes as `Identifier`. If
a dedicated `TokenKind.Select` exists, remove it and update all readers. Leave
`case`, `default` and `switch` as keywords — they have no method form and do not
collide.

**Parser — statement head.** The statement dispatcher currently branches on the
`select` keyword token. Replace that with: at statement-head position, if the
current token is an `Identifier` with lexeme `select` **and** the next token is
`(`, parse the select statement; otherwise fall through to the normal
expression/assignment statement path. The reserved-identifier rule guarantees no
user binding is named `select`, so a leading `select (` at statement head has
exactly one meaning — no speculative parse or backtracking is needed. A leading
`select` **not** followed by `(` is a parse error with a message pointing at the
required `select (...)` form.

**Parser — member access.** `expr . Identifier` should now accept `select`
without change, because the lexer no longer emits a keyword token there. Confirm
no residual special-case rejects a keyword-shaped lexeme after `.`; if one
exists, remove it.

**Type checker — reserved identifiers.** Maintain a single reserved-identifier
set `{ "formatAs", "select" }`. On every binding declaration — `:=` local,
`fn` name, function parameter name, and `type` field name — if the declared name
is in the set, emit **E1103**. If a `formatAs` binding check already exists from
D-282, refactor it to consult the shared set rather than adding a parallel
`select`-only check; the two must not diverge. (`formatAs` keeps its additional
bare-member rule from D-282; `select` has no bare-member rule — it is an
ordinary method.)

**ErrorCatalog (D-308).** Add E1103 as exactly one `static readonly
ErrorDescriptor`. No inline literal anywhere. `ErrorCatalogAgreementTests` and
the `Grob.Consistency.Tests` count gate (now 108) must pass.

**Consistency check (D-316).** Add one check to `tests/Grob.Consistency.Tests`
asserting that the intersection of `{ registered native method names }` and
`{ hard keyword lexemes }` is empty — no built-in method name may be a hard
keyword, since that would make it unparseable after `.`. Reserved identifiers are
explicitly permitted as method names (that is the point), so the check is against
the **keyword** set, not the reserved-identifier set. This is the durable guard
that turns the next such collision into a build failure rather than an
unparseable call.

## Acceptance tests

Parser, positive:

- `select (x) { case 1 { } default { } }` parses as a select statement
  (regression — the statement form must still work after un-reserving).
- `arr.select(f => f.name)` parses as a method call on a member access (assert
  the AST shape; no type resolution required).
- the leading-dot chain `files` `.filter(...)` `.select(...)` `.sort()` parses.
- `r := arr.select(x => x)` parses (declaration with a `.select` RHS).

Type checker, negative → E1103:

- `select := 5`
- `fn select(): int { return 1 }`
- `fn f(select: int): int { return select }` (parameter named `select`)
- `type T { select: int }` (field named `select`)
- at least one `formatAs := 1` case, proving the shared rule covers `formatAs`.

Lexer:

- `select` lexes as `Identifier`, not a keyword token.

Consistency:

- the new native-method-vs-keyword check passes against the current native
  registry, and a deliberately seeded fixture method named `case` makes it fail
  (prove the guard bites).

## Dependency note

This increment makes `.select` **parse**. It does not register the `.select()`
native — that is the array higher-order methods increment, which now succeeds
because the parser accepts the call. The end-to-end gate for scripts 3, 5, 7, 8,
9 and 12 is verified **there**, not here. State this in the PR description so the
reviewer does not expect end-to-end script runs from this branch.

## Close

`/commit-message` for the commit; Husky.NET pre-push gate (build, test, format)
must be green. Update the decisions log only if implementation surfaces a
divergence from D-320 — in which case stop and raise it rather than amending
silently.

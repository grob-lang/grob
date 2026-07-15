---
description: "Sprint 9 · Increment G — `regex` (module form) + `regex.compile()`. The Regex/Match plugin types, the module convenience functions (compile-per-call), and the new regex.compile(pattern, flags): Regex constructor that replaces the deferred `/pattern/flags` literal. Requirements regex bullet reconciled to drop the literal and cite the deferral (D-350, superseding the regex-literal line of D-186/D-283). .NET regex engine underneath."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment G — `regex` (module form) + `regex.compile()`

`process` landed in Increment F, closing D-319's six capability interfaces. This
increment lands `regex` — the last module. Per the kickoff, the `/pattern/flags` literal
is **deferred to post-MVP** (D-186/D-283's scope-cut, resolved at this sprint's kickoff),
so `regex` ships with the module convenience functions plus a **new** `regex.compile()`
constructor for reusable `Regex` values. That constructor is load-bearing: a `Regex`
value is currently only creatable by a literal (D-089/D-095), so without `regex.compile()`
the entire `Regex`/`Match` type surface would be dead code.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 9** `regex` scope. **Note the
   contradiction to reconcile:** the bullet lists the `/pattern/flags` literal, which is
   deferred. This increment replaces that line with `regex.compile()`, citing D-350.
2. `docs/design/grob-stdlib-reference.md` — the **`regex`** section (the `Regex` type
   methods `match`/`matchAll`/`isMatch`/`replace`/`replaceAll`/`split` and properties
   `pattern`/`flags`; the `Match` type `value`/`index`/`length`/`groups`/`group(name)`;
   the module convenience functions `isMatch`/`match`/`matchAll`/`replace`/`replaceAll`/
   `split`/`escape` — compile-per-call; the `i`/`m` flags).
3. Decisions: **D-186**/**D-283** (the regex-literal scope-cut and the note that
   `regex.compile(pattern, flags)` covers everything literals would, and that a
   `regex.compile()` release-gate script must be added before v1), **D-089**/**D-095**
   (the original regex module and the literal-creates-a-`Regex` model), **D-285**
   (backtick raw strings canonical for regex patterns — the idiom for the pattern
   argument), **D-342** (module-namespace resolution), **D-303** (`Struct` discriminator
   — `Regex`/`Match`), **D-336** (`ValueDisplay` — each registers a `toString()`),
   **D-284** (the `ParseError` leaf a bad pattern throws). Grep for the next-free
   D-number for the regex-deferral-and-`compile` decision (this prompt provisionally
   names it **D-350**, superseding the regex-literal line of D-186/D-283).

> **Verify before relying on cited decisions and sections.** Grep the `regex` section and
> confirm the `Regex`/`Match` surface reads as this prompt assumes. If `regex.escape` or
> a method has moved, surface it.
>
> **`regex` — inline reference.**
>
> - **The literal is deferred; `regex.compile()` replaces it.** `regex.compile(pattern:
>   string, flags: string = ""): Regex` is the constructor for a reusable compiled
>   `Regex`. The pattern argument idiom is a backtick raw string (D-285) — `regex.compile
>   (\`\d+\`, "i")`. This is the form D-283 names as covering everything the literal
>   would; it is **new** surface this increment adds, not a rename.
> - **`Regex` and `Match` are `Struct`-discriminated plugin types** (D-303, no new
>   `GrobValueKind`), each with a registered `toString()` for `ValueDisplay` (D-336). The
>   `Regex` methods and properties and the `Match` fields are the type-registry surface.
> - **Module convenience functions compile per call.** `regex.isMatch`/`match`/`matchAll`/
>   `replace`/`replaceAll`/`split` take a string pattern and compile on each call — the
>   one-shot form. `regex.escape(input): string` escapes a literal for use in a pattern.
> - **A bad pattern throws `ParseError` through the native-throw seam.**
>   `regex.compile("(unclosed")` raises a catchable `ParseError` (D-284) that unwinds the
>   Sprint-7 handler table (D-334/D-342) — one mechanism, no bespoke path.
> - **.NET regex engine underneath**, `i` (case-insensitive) and `m` (multiline) flags,
>   named groups and lookaheads exposed. String literals are never implicitly treated as
>   patterns.
> - **The requirements reconciliation.** Replace the `/pattern/flags` literal line in the
>   requirements `regex` bullet with the `regex.compile()` form, citing D-350. Record in
>   D-350 that a `regex.compile()`-exercising release-gate script is required before v1
>   (D-186) — the script itself is a v1 release-gate item flagged in Increment H, not
>   built here.

> **Sequencing note.** This is Increment G: F (`process`) → **G (`regex`)** → H (close).
> G is the last module; H closes the sprint. Do not fold the close into G.

## What you're building

1. **`regex.compile(pattern, flags): Regex`** — the new constructor for reusable `Regex`
   values, the literal's replacement.
2. **The `Regex` and `Match` plugin types** — `Struct`-discriminated, the method/property
   and field surface, registered `toString()`s. §3.1.1 on every node.
3. **The module convenience functions** — `isMatch`/`match`/`matchAll`/`replace`/
   `replaceAll`/`split`/`escape`, compile-per-call.
4. **`ParseError` on a bad pattern** through the native-throw seam.
5. **The requirements reconciliation** — the `regex` bullet's literal line replaced by
   `regex.compile()`, citing D-350.
6. **The D-350 decision** — regex literals deferred to post-MVP; `regex.compile()` added;
   the v1-gate release-script note — recorded at its real next-free number in
   three-location lockstep, superseding the regex-literal line of D-186/D-283.

No new opcode, no new `GrobValueKind` variant, **no literal grammar** (no lexer `/`
disambiguation, no `RegexLiteral` token). If you reach for the literal grammar, stop —
it is deferred.

## Out of scope

The `/pattern/flags` literal grammar (post-MVP). The close (Increment H). The
`regex.compile()` release-gate script (a v1-gate item flagged in H). Do not edit the
`OpCode` enum, add a `GrobValueKind` variant, or add a `RegexLiteral` `TokenKind`.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):** `regex` resolves as a namespace;
  `regex.compile` resolves to `(string, string): Regex`; a `Regex` value's methods and a
  `Match`'s fields resolve. §3.1.1 holds.
- **Stdlib tests (`Grob.Stdlib.Tests`):** `regex.compile` produces a reusable `Regex`;
  `match`/`matchAll`/`isMatch`/`replace`/`replaceAll`/`split` behave; `i`/`m` flags work;
  named groups and `group(name)` work; `regex.escape` escapes correctly; the module
  convenience functions compile-per-call and match the compiled forms; a bad pattern
  throws `ParseError`.
- **VM tests (`Grob.Vm.Tests`):** a bad-pattern `ParseError` unhandled produces the
  quality diagnostic and exit 1, caught it resumes; `print(re)`/`print(m)` render via
  `ValueDisplay`.
- **Integration / spec-consistency:** D-316 green; the D-350 decision in lockstep; the
  requirements `regex` bullet reconciled; no `RegexLiteral` `TokenKind` added (grep the
  enum); the count reconciled.

## Acceptance

- `regex.compile()` produces a reusable `Regex`; the `Regex` methods, the `Match` fields,
  the `i`/`m` flags, named groups and `regex.escape` work; the module convenience
  functions match the compiled forms.
- A bad pattern throws catchable `ParseError` through the handler table.
- The `/pattern/flags` literal is **not** implemented; no `RegexLiteral` `TokenKind`, no
  lexer `/` disambiguation.
- The requirements `regex` bullet reconciled to `regex.compile()`; D-350 logged in
  lockstep with the v1-gate release-script note; D-316 green.
- No new opcode, no new `GrobValueKind` variant; coverage at or above 90% on the affected
  projects.

## Model

Sonnet 4.6 (High). A type registration plus native surface plus a constructor over
settled machinery, literals deferred — no Opus carve-out.

## Hand-off

Summarise: `regex.compile()` and why it replaces the literal; the `Regex`/`Match` types
and the module functions; the `ParseError` path; the requirements reconciliation; D-350
and the v1-gate release-script note; confirmation that no literal grammar was added. Note
for the next chat: Increment H is the close — the stability re-characterisation against
the now-larger Sprint-9-runnable set, the next benchmark baseline, the Sprint 9 smoke
script, the file-organiser real-program target, and the `regex.compile()` release-gate
script flagged by D-350.

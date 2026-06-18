# Grob — Error Code Registry

> The authoritative registry for every error code emitted by the Grob compiler
> and runtime. This file is the source of truth; the website
> (`grob-lang.dev/errors`) is generated from it.
>
> **Stability:** see ADR-0017. Codes marked `pre-release` are subject to
> change until v1.0 ships; codes marked `stable` are immutable.
>
> _Created April 2026 — initial allocation, error-code numbering scheme session._
> _Authority: ADR-0014 (numbering scheme) and ADR-0017 (stability rule)._

---

## Format

Every code is an entry of the form `Exxxx`. The thousands digit encodes the
category; see ADR-0014 §Categories. Each entry in this registry has:

- `code` — the `Exxxx` identifier
- `title` — short title rendered after `error[Exxxx]:`
- `category` — one of: type / name resolution / syntax / module / param /
  runtime / internal
- `introduced` — Grob version in which the code first appeared
- `status` — `pre-release` / `stable` / `deprecated` / `retired`
- `description` — one-sentence description of the failure mode
- `throws_type` (runtime codes only) — the `GrobError` leaf this code
  corresponds to
- `superseded_by` (deprecated/retired only) — replacement code

Long-form documentation (cause / example / fix) lives in `docs/errors/Exxxx.md`,
read by `grob --explain Exxxx`.

---

## Category Map

| Range       | Category                       | Phase                   |
| ----------- | ------------------------------ | ----------------------- |
| E0001–E0999 | Type errors                    | Compile-time            |
| E1001–E1999 | Name resolution                | Compile-time            |
| E2001–E2999 | Syntax                         | Compile-time            |
| E3001–E3999 | Module / import                | Compile-time            |
| E4001–E4999 | Param / decorator              | Compile-time            |
| E5001–E5999 | Runtime                        | Runtime                 |
| E6001–E8999 | Reserved for future categories | —                       |
| E9001–E9999 | Internal compiler error        | Compile-time or runtime |

---

## Summary Index

| Code  | Title                                              | Category          | Status                |
| ----- | -------------------------------------------------- | ----------------- | --------------------- |
| E0001 | type mismatch                                      | Type              | pre-release           |
| E0002 | incompatible operands                              | Type              | pre-release           |
| E0003 | wrong number of arguments                          | Type              | pre-release           |
| E0004 | argument type mismatch                             | Type              | pre-release           |
| E0005 | return type mismatch                               | Type              | pre-release           |
| E0006 | undefined method on type                           | Type              | pre-release           |
| E0007 | invalid implicit conversion                        | Type              | pre-release           |
| E0101 | nil dereference without `?.` or `??`               | Type              | pre-release           |
| E0102 | nullable interpolation                             | Type              | pre-release           |
| E0103 | non-nullable field requires initialiser            | Type              | pre-release           |
| E0104 | nullable type used where non-nullable required     | Type              | pre-release           |
| E0201 | reassignment of `const` binding                    | Type              | pre-release           |
| E0202 | reassignment of `readonly` binding                 | Type              | pre-release           |
| E0203 | reassignment of `param` binding                    | Type              | pre-release           |
| E0204 | mutation of `readonly` value                       | Type              | pre-release           |
| E0205 | non-constant expression in `const` right-hand side | Type              | pre-release           |
| E0301 | type cycle with no terminating field               | Type              | pre-release           |
| E0302 | recursive type without indirection                 | Type              | pre-release           |
| E0401 | generic type argument count mismatch               | Type              | pre-release           |
| E0402 | generic constraint violation                       | Type              | pre-release           |
| E1001 | undefined identifier                               | Name resolution   | pre-release           |
| E1002 | undefined member                                   | Name resolution   | pre-release           |
| E1003 | undefined module                                   | Name resolution   | pre-release           |
| E1101 | shadowed declaration                               | Name resolution   | pre-release (warning) |
| E1102 | variable already declared in this scope            | Name resolution   | pre-release           |
| E1201 | forward reference inside function body             | Name resolution   | pre-release           |
| E1202 | use before declaration in block scope              | Name resolution   | pre-release           |
| E2001 | unexpected token                                   | Syntax            | pre-release           |
| E2002 | unterminated string literal                        | Syntax            | pre-release           |
| E2003 | unterminated block comment                         | Syntax            | pre-release           |
| E2004 | unterminated raw string                            | Syntax            | pre-release           |
| E2005 | invalid escape sequence                            | Syntax            | pre-release           |
| E2006 | invalid numeric literal                            | Syntax            | pre-release           |
| E2007 | invalid regex flag                                 | Syntax            | pre-release           |
| E2008 | unterminated regex literal                         | Syntax            | pre-release           |
| E2009 | unterminated string interpolation                  | Syntax            | pre-release           |
| E2010 | unexpected character                               | Syntax            | pre-release           |
| E2011 | stray semicolon                                    | Syntax            | pre-release           |
| E2012 | `#` not followed by `{`                            | Syntax            | pre-release           |
| E2013 | single-character operator requires doubling        | Syntax            | pre-release           |
| E2014 | unbalanced closing bracket                         | Syntax            | pre-release           |
| E2101 | bare `{` cannot begin an expression                | Syntax            | pre-release           |
| E2102 | empty type construction missing `{ }`              | Syntax            | pre-release           |
| E2201 | `import` after declaration                         | Syntax            | pre-release           |
| E2202 | `param` after `fn` or top-level statement          | Syntax            | pre-release           |
| E2203 | top-level `return`                                 | Syntax            | pre-release           |
| E2204 | `try` without `catch` or `finally`                 | Syntax            | pre-release           |
| E2205 | `catch` after catch-all                            | Syntax            | pre-release           |
| E2206 | `finally` not last in `try`                        | Syntax            | pre-release           |
| E2207 | `return` / `break` / `continue` inside `finally`   | Syntax            | pre-release           |
| E2208 | duplicate field name in type declaration           | Syntax            | pre-release           |
| E2209 | trailing comma not permitted here                  | Syntax            | pre-release           |
| E2210 | line continuation rule violation                   | Syntax            | pre-release           |
| E2211 | `break` outside a loop                             | Syntax            | pre-release           |
| E2212 | `continue` outside a loop                          | Syntax            | pre-release           |
| E3001 | unknown plugin                                     | Module            | pre-release           |
| E3002 | plugin not installed                               | Module            | pre-release           |
| E3003 | circular import                                    | Module            | pre-release           |
| E3004 | invalid module alias                               | Module            | pre-release           |
| E3101 | ambiguous unqualified type reference               | Module            | pre-release           |
| E3102 | plugin type collides with stdlib type              | Module            | pre-release           |
| E3201 | manifest version mismatch                          | Module            | pre-release           |
| E4001 | unknown decorator                                  | Param / decorator | pre-release           |
| E4002 | decorator not permitted here                       | Param / decorator | pre-release           |
| E4101 | invalid `@allowed` argument                        | Param / decorator | pre-release           |
| E4102 | invalid `@minLength` / `@maxLength` argument       | Param / decorator | pre-release           |
| E4201 | `param` block syntax error                         | Param / decorator | pre-release           |
| E4202 | `param` after `param` block ends                   | Param / decorator | pre-release           |
| E5001 | integer overflow                                   | Runtime           | pre-release           |
| E5002 | integer division by zero                           | Runtime           | pre-release           |
| E5003 | integer modulo by zero                             | Runtime           | pre-release           |
| E5004 | float division by zero                             | Runtime           | pre-release           |
| E5005 | float modulo by zero                               | Runtime           | pre-release           |
| E5006 | math domain violation                              | Runtime           | pre-release           |
| E5101 | array index out of range                           | Runtime           | pre-release           |
| E5102 | substring bounds out of range                      | Runtime           | pre-release           |
| E5201 | nil dereference at runtime                         | Runtime           | pre-release           |
| E5301 | file not found                                     | Runtime           | pre-release           |
| E5302 | permission denied                                  | Runtime           | pre-release           |
| E5303 | path is a directory, file expected                 | Runtime           | pre-release           |
| E5304 | path is a file, directory expected                 | Runtime           | pre-release           |
| E5305 | I/O failure (residual)                             | Runtime           | pre-release           |
| E5401 | network connection failed                          | Runtime           | pre-release           |
| E5402 | HTTP error response                                | Runtime           | pre-release           |
| E5403 | DNS resolution failed                              | Runtime           | pre-release           |
| E5404 | network timeout                                    | Runtime           | pre-release           |
| E5501 | JSON parse error                                   | Runtime           | pre-release           |
| E5502 | JSON type coercion failure                         | Runtime           | pre-release           |
| E5601 | process timeout                                    | Runtime           | pre-release           |
| E5602 | process exit non-zero (under `OrFail`)             | Runtime           | pre-release           |
| E5603 | process spawn failed                               | Runtime           | pre-release           |
| E5701 | guid parse error                                   | Runtime           | pre-release           |
| E5702 | parse error (residual)                             | Runtime           | pre-release           |
| E5801 | required environment variable not set              | Runtime           | pre-release           |
| E5901 | call stack overflow                                | Runtime           | pre-release           |
| E5902 | circular initialisation                            | Runtime           | pre-release           |
| E5903 | runtime failure (residual catch-all)               | Runtime           | pre-release           |
| E9001 | internal compiler error — please report            | Internal          | pre-release           |

---

## Full Entries

### E0001 — type mismatch

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A value of one type was used where a different type was expected, with no implicit conversion available.
- **Source decision:** D-039; v1 Req §10 example.

---

### E0002 — incompatible operands

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A binary operator was applied to operand types it does not accept.
- **Source decision:** D-273; arithmetic and comparison operator type rules.

---

### E0003 — wrong number of arguments

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A function call supplied a number of arguments inconsistent with the function's parameters and any defaults.
- **Source decision:** D-031 (named parameters).

---

### E0004 — argument type mismatch

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A function call supplied an argument whose type does not match the corresponding parameter.

---

### E0005 — return type mismatch

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `return` statement produced a value whose type does not match the function's declared return type.

---

### E0006 — undefined method on type

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A method was called on a value of a type that does not declare that method.
- **Source:** `grob-type-registry.md`.

---

### E0007 — invalid implicit conversion

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** An implicit conversion was attempted where Grob does not permit one. The only permitted implicit conversion is `int → float`.
- **Source decision:** D-178.

---

### E0101 — nil dereference without `?.` or `??`

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A nullable value was dereferenced without optional chaining or nil coalescing, in a context where the type checker cannot prove the value is non-nil.
- **Source:** `grob-language-fundamentals.md` §21.

---

### E0102 — nullable interpolation

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A nullable value was used inside a string interpolation expression. Resolve with `??` or with a narrowing `if`.
- **Source decision:** D-279.

---

### E0103 — non-nullable field requires initialiser

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A type construction omitted a non-nullable field that has no default value.

---

### E0104 — nullable type used where non-nullable required

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A value of type `T?` was assigned to a binding, parameter, or field of type `T`.

---

### E0201 — reassignment of `const` binding

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `const` binding cannot be reassigned. Use `readonly` for runtime-once bindings or `:=` for mutable bindings.
- **Source decision:** D-288; D-292.

---

### E0202 — reassignment of `readonly` binding

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `readonly` binding cannot be reassigned after its initialiser has run. Use `:=` for mutable bindings.
- **Source decision:** D-288; D-291.

---

### E0203 — reassignment of `param` binding

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `param` binding is implicitly `readonly` and cannot be reassigned. Copy to a local variable to mutate.
- **Source decision:** D-291 (param implicitly readonly).

---

### E0204 — mutation of `readonly` value

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `readonly` value cannot be mutated. This includes mutating fields of a `readonly` struct or appending to a `readonly` array.
- **Source decision:** D-291 (deep immutability).

---

### E0205 — non-constant expression in `const` right-hand side

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The right-hand side of a `const` declaration must be a compile-time constant expression (D-289). Allowed forms are: literals, grouped literals, binary arithmetic/comparison/logical operators on constant operands, unary `-`/`!` on constant operands, and references to other `const`-bound identifiers. References to `readonly` or mutable identifiers, function calls, array/map literals, and interpolated strings with `${}` are not allowed. Change the binding to `readonly` if a runtime value is needed.
- **Source decision:** D-288; D-289.

---

### E0301 — type cycle with no terminating field

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A type declaration contains a cycle of required non-nullable fields that would produce an infinitely-sized value. Break the cycle with a nullable field, an array, or a map.
- **Source decision:** D-287. Replaces the placeholder `E—cycle`.

---

### E0302 — recursive type without indirection

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A type's required field references the type itself directly. Sibling of E0301 — fires on the trivial single-type self-reference case where E0301's multi-step cycle walk is not needed.
- **Source decision:** D-287.

---

### E0401 — generic type argument count mismatch

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A generic call site supplied a number of type arguments inconsistent with the generic's declaration.
- **Source decision:** D-286 (generic type argument forward references); OQ-001.

---

### E0402 — generic constraint violation

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A generic type argument does not satisfy the constraint declared on the type parameter, e.g. `sort<U: Comparable>` called with a non-comparable `U`.
- **Source decision:** D-281.

---

### E1001 — undefined identifier

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A bare identifier was referenced and is not declared in any enclosing scope.

---

### E1002 — undefined member

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A member access (`obj.field`) referenced a field or method not declared on the value's type.

---

### E1003 — undefined module

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A module-qualified call (`module.fn`) referenced a function not declared on the imported module.

---

### E1101 — shadowed declaration

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release (severity: warning)
- **Description:** A local declaration shadows a name from an enclosing scope. Emitted as a warning per D-024; reserved as `Exxxx` because the diagnostic-emission machinery is shared with errors and a future `--strict` mode may promote it.
- **Source decision:** D-024. Flagged for the warnings allocation pass.

---

### E1102 — variable already declared in this scope

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `:=` declaration was applied to a name that already exists in the current scope. Use `=` to reassign an existing binding.
- **Source decision:** Sprint 3 Increment A.

---

### E1201 — forward reference inside function body

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Inside a function body, a local variable was referenced before its declaration in the same scope. Forward references are permitted at the top level (D-286) but not within function bodies.
- **Source decision:** D-166; D-286.

---

### E1202 — use before declaration in block scope

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Inside a nested block, a local variable was referenced before its declaration in that block. Sibling of E1201 with tighter scope context.

---

### E2001 — unexpected token

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The parser encountered a token that does not fit the surrounding grammatical context.

---

### E2002 — unterminated string literal

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A double-quoted string literal was opened and never closed before end of file or end of line.

---

### E2003 — unterminated block comment

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `/* */` block comment was opened and never closed before end of file.

---

### E2004 — unterminated raw string

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A backtick-delimited raw string was opened and never closed before end of file.
- **Source decision:** D-285.

---

### E2005 — invalid escape sequence

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A backslash escape sequence in a double-quoted string is not one of the defined forms. Unknown escapes are a compile error, not a permissive pass-through.
- **Source:** `grob-language-fundamentals.md` §8.

---

### E2006 — invalid numeric literal

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A numeric literal is malformed — bad hex digits, misplaced underscores, invalid binary digits, or an overflowing literal.
- **Source:** `grob-language-fundamentals.md` §8.

---

### E2007 — invalid regex flag

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A character following the closing `/` of a regex literal is not one of the supported flags (`i`, `m`).
- **Source decision:** D-089.

---

### E2008 — unterminated regex literal

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A regex literal opened with `/` was not closed before end-of-line or end-of-file.

---

### E2009 — unterminated string interpolation

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `${` interpolation opener inside a string literal was never closed before end-of-file.

---

### E2010 — unexpected character

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A character was encountered that is not valid in any token position.

---

### E2011 — stray semicolon

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Semicolons are not used in Grob — newlines terminate statements.

---

### E2012 — `#` not followed by `{`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A lone `#` is not a Grob token. `#{ }` opens an anonymous-struct literal.

---

### E2013 — single-character operator requires doubling

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A bare `&` or `|` is not a Grob operator; use `&&` or `||`.

---

### E2014 — unbalanced closing bracket

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `)`, `]`, or `}` was encountered with no matching opener.

---

### E2101 — bare `{` cannot begin an expression

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A bare `{` always begins a block. Use `#{ }` for an anonymous struct literal or `TypeName { }` for a named type construction.
- **Source decision:** D-043.

---

### E2102 — empty type construction missing `{ }`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A named type construction was written without the required `{ }` body, even when the type has only defaultable fields.

---

### E2201 — `import` after declaration

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `import` statements must precede all other declarations and code.
- **Source:** `grob-language-fundamentals.md` §19.

---

### E2202 — `param` after `fn` or top-level statement

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `param` blocks must precede `type` declarations, function declarations, and top-level code.
- **Source:** `grob-language-fundamentals.md` §19.

---

### E2203 — top-level `return`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `return` is not valid at script level. Use `exit(<n>)` to terminate a script with a status code.
- **Source:** `grob-language-fundamentals.md` §22.

---

### E2204 — `try` without `catch` or `finally`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `try` block must be followed by at least one `catch` clause or a `finally` block.
- **Source decision:** D-274; D-275.

---

### E2205 — `catch` after catch-all

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A bare `catch e` (catch-all) must be the last catch clause. Any catch after the catch-all is unreachable.
- **Source decision:** D-274.

---

### E2206 — `finally` not last in `try`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `finally`, when present, must appear after all `catch` clauses and may appear at most once.
- **Source decision:** D-275.

---

### E2207 — `return` / `break` / `continue` inside `finally`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Control-flow exits are not permitted inside a `finally` block. This is a deliberate divergence from C# — the "finally overrides return" behaviour has no legitimate use case.
- **Source decision:** D-275.

---

### E2208 — duplicate field name in type declaration

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `type` declaration listed the same field name more than once.

---

### E2209 — trailing comma not permitted here

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Trailing commas are permitted in most list-shaped constructs (`grob-language-fundamentals.md` §16) but not in single-element parenthesised expressions or empty argument lists.

---

### E2210 — line continuation rule violation

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A statement spans multiple lines in a way the implicit line-continuation heuristic cannot resolve. Add explicit grouping or restructure.
- **Source:** `grob-language-fundamentals.md` §14.

---

### E2211 — `break` outside a loop

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `break` statement appeared outside the body of a `while` or `for...in` loop. `break` exits the innermost enclosing loop; there must be one. Inside a `select` case, `break` applies to the nearest enclosing loop — if none exists, this error is still raised.
- **Source decision:** Sprint 4 Increment B.

---

### E2212 — `continue` outside a loop

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `continue` statement appeared outside the body of a `while` or `for...in` loop. `continue` skips to the next iteration of the innermost enclosing loop; there must be one. Inside a `select` case, `continue` applies to the nearest enclosing loop — if none exists, this error is still raised.
- **Source decision:** Sprint 4 Increment B.

---

### E3001 — unknown plugin

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** An `import` referenced a plugin name that does not match any known package.
- **Source decision:** D-026; D-032.

---

### E3002 — plugin not installed

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** An `import` referenced a known plugin that has not been installed via `grob install`. Plugins never auto-download at runtime.
- **Source decision:** D-026.

---

### E3003 — circular import

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Two or more modules import each other, directly or transitively. Circular imports are an explicit non-feature.
- **Source:** `grob-language-fundamentals.md` §23.

---

### E3004 — invalid module alias

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** An `import X as <alias>` uses an alias that is reserved, conflicts with a keyword, or is otherwise not a valid identifier.

---

### E3101 — ambiguous unqualified type reference

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Two imported plugins each register a type with the same unqualified name. Add `as <alias>` to one of the imports to disambiguate.
- **Source:** Session D Part 1, §D3 plugin type registration.

---

### E3102 — plugin type collides with stdlib type

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A plugin registers a type whose unqualified name shadows a stdlib type. Add `as <alias>` to the plugin import.
- **Source:** Session D Part 1, §D3.

---

### E3201 — manifest version mismatch

- **Category:** Module
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `grob.json` version constraint is not satisfied by the installed version of a plugin.
- **Source:** install strategy; `grob-install-strategy.md`.

---

### E4001 — unknown decorator

- **Category:** Param / decorator
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A decorator was applied that is not one of the recognised decorators (`@secure`, `@allowed`, `@minLength`, `@maxLength`).
- **Source decision:** D-072.

---

### E4002 — decorator not permitted here

- **Category:** Param / decorator
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A decorator was applied to a target where it is not valid, e.g. `@secure` on a non-string param or a validation decorator outside a `param` block.
- **Source decision:** D-072.

---

### E4101 — invalid `@allowed` argument

- **Category:** Param / decorator
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `@allowed(...)` received an argument list that is not a homogeneous set of literals matching the param's type. This is a grammar-level rejection at compile time, distinct from runtime validation failures (which fall under the runtime category if v1 scope-cut is not activated).
- **Source decision:** D-186 (validation decorators are a v1 scope-cut candidate; the grammar code is allocated regardless).

---

### E4102 — invalid `@minLength` / `@maxLength` argument

- **Category:** Param / decorator
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `@minLength(n)` or `@maxLength(n)` received an argument that is not a non-negative integer literal, or was applied to a param whose type does not support length constraints.

---

### E4201 — `param` block syntax error

- **Category:** Param / decorator
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `param` block contains a malformed parameter declaration — missing type annotation on a defaultless param, missing `:=` before a default, etc.
- **Source:** `grob-language-fundamentals.md` §19.

---

### E4202 — `param` after `param` block ends

- **Category:** Param / decorator
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `param` declaration appeared after the `param` block has been closed by a non-`param` statement.

---

### E5001 — integer overflow

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ArithmeticError`
- **Description:** An `int` arithmetic operation produced a value outside the 64-bit signed range. Grob uses checked arithmetic — overflow is never silent.
- **Source:** `grob-language-fundamentals.md` §15; D-167.

---

### E5002 — integer division by zero

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ArithmeticError`
- **Description:** `int / 0` is a runtime error. No form of integer division by zero is silent.
- **Source decision:** D-278.

---

### E5003 — integer modulo by zero

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ArithmeticError`
- **Description:** `int % 0` is a runtime error.
- **Source:** `grob-language-fundamentals.md` §15.

---

### E5004 — float division by zero

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ArithmeticError`
- **Description:** `x / 0.0` is a runtime error. Grob does not propagate `±Infinity` from a literal zero divisor.
- **Source decision:** D-273.

---

### E5005 — float modulo by zero

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ArithmeticError`
- **Description:** `x % 0.0` is a runtime error. Consistent with E5004.
- **Source decision:** D-273.

---

### E5006 — math domain violation

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ArithmeticError`
- **Description:** `math.sqrt(negative)`, `math.log(0)`, `math.log(negative)` and similar domain violations throw rather than silently producing `NaN`.
- **Source decision:** D-284; Session D Part 1 stdlib gap-fill.

---

### E5101 — array index out of range

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IndexError`
- **Description:** An array index was negative or greater than or equal to the array's length.
- **Source decision:** D-284.

---

### E5102 — substring bounds out of range

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IndexError`
- **Description:** A substring start or end index was outside the source string's bounds.
- **Source decision:** D-284.

---

### E5201 — nil dereference at runtime

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `NilError`
- **Description:** A nullable value was dereferenced at runtime where the type checker permitted the access (e.g. through a cast or a runtime-typed value). Runtime sibling of E0101.
- **Source decision:** D-284.

---

### E5301 — file not found

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IoError`
- **Description:** A file system operation referenced a path that does not exist.

---

### E5302 — permission denied

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IoError`
- **Description:** A file system operation was rejected by the operating system due to insufficient permissions.

---

### E5303 — path is a directory, file expected

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IoError`
- **Description:** A file-shaped operation (e.g. `fs.read()`) was applied to a directory path.

---

### E5304 — path is a file, directory expected

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IoError`
- **Description:** A directory-shaped operation (e.g. `fs.list()`) was applied to a file path.

---

### E5305 — I/O failure (residual)

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `IoError`
- **Description:** An I/O operation failed for a reason that does not fit the more specific I/O codes. The bar for emitting this stays high — specific failures get specific codes.

---

### E5401 — network connection failed

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `NetworkError`
- **Description:** An HTTP or other network request failed to establish a connection.

---

### E5402 — HTTP error response

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `NetworkError`
- **Description:** An HTTP request returned a non-2xx status code under a call site that surfaces this as an exception. The `statusCode` field on `NetworkError` carries the code.
- **Source decision:** D-284 (`NetworkError` adds `statusCode: int?`).

---

### E5403 — DNS resolution failed

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `NetworkError`
- **Description:** A network request could not resolve the supplied host name.

---

### E5404 — network timeout

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `NetworkError`
- **Description:** A network request exceeded its configured timeout before completing.

---

### E5501 — JSON parse error

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `JsonError`
- **Description:** `json.parse()` was called on input that is not well-formed JSON.
- **Source decision:** D-284.

---

### E5502 — JSON type coercion failure

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `JsonError`
- **Description:** A `json.Node` access or `mapAs<T>()` runtime coercion encountered a value whose JSON shape does not match the target type.
- **Source decision:** D-284.

---

### E5601 — process timeout

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ProcessError`
- **Description:** A subprocess exceeded its configured `timeout` parameter and was terminated.
- **Source decision:** D-147.

---

### E5602 — process exit non-zero (under `OrFail`)

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ProcessError`
- **Description:** `process.runOrFail()` or `process.runShellOrFail()` observed a non-zero exit code from the subprocess.

---

### E5603 — process spawn failed

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ProcessError`
- **Description:** A subprocess could not be started — typically because the executable was not found on `PATH` or the path supplied does not exist.

---

### E5701 — guid parse error

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ParseError`
- **Description:** `guid.parse()` was called on a string that is not a valid GUID representation.
- **Source decision:** D-284 message template.

---

### E5702 — parse error (residual)

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `ParseError`
- **Description:** Reserved for future explicit parse operations (`int.parse`, `float.parse`, `date.parse`) which throw `ParseError` per D-284.

---

### E5801 — required environment variable not set

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `LookupError`
- **Description:** `env.require()` was called for an environment variable that is not set.
- **Source decision:** D-284 message template; D-074.

---

### E5901 — call stack overflow

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `RuntimeError`
- **Description:** Recursive call depth exceeded the VM's call frame limit.
- **Source decision:** D-284 (`RuntimeError` shrunk to VM-level residual).

---

### E5902 — circular initialisation

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `RuntimeError`
- **Description:** A top-level binding's initialiser called a function that read another top-level binding before that binding's declaration had executed. Detected via the three-state initialisation tag.
- **Source decision:** D-294. Replaces an unassigned placeholder.

---

### E5903 — runtime failure (residual catch-all)

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** `RuntimeError`
- **Description:** A runtime failure that does not fit any more specific category. The bar for emitting this stays high — specific failures get specific codes.

---

### E9001 — internal compiler error — please report

- **Category:** Internal
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The compiler reached a state it does not know how to recover from. This is a bug in Grob, not in the user's script. Diagnostic includes a request to file an issue with the offending source.

---

## Retired Codes

None as of v1.

---

## Deprecated Codes

None as of v1.

---

_Initial allocation: 94 codes across 7 categories. All `pre-release` until v1.0 ships. Authority: ADR-0014 (numbering scheme) and ADR-0017 (stability rule)._

_Updated May 2026 — count corrected from a stale "86 codes" to the actual 94 codes present in the summary index and full entries. No codes were added in this edit; the footer total had not been updated as codes accrued. The 7-category structure (E0xxx–E9xxx) is unchanged._

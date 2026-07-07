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
| E0008 | named argument before positional                   | Type              | pre-release           |
| E0009 | named argument names a required parameter           | Type              | pre-release           |
| E0010 | duplicate named argument                            | Type              | pre-release           |
| E0011 | unknown parameter name                              | Type              | pre-release           |
| E0012 | unknown field name                                  | Type              | pre-release           |
| E0013 | field default references sibling field              | Type              | pre-release           |
| E0014 | throw operand is not a GrobError subtype             | Type              | pre-release           |
| E0015 | catch type is not a GrobError subtype              | Type              | pre-release           |
| E0101 | nil dereference without `?.` or `??`               | Type              | pre-release           |
| E0102 | nullable interpolation                             | Type              | pre-release           |
| E0103 | non-nullable field requires initialiser            | Type              | pre-release           |
| E0104 | nullable type used where non-nullable required     | Type              | pre-release           |
| E0201 | reassignment of `const` binding                    | Type              | pre-release           |
| E0202 | reassignment of `readonly` binding                 | Type              | pre-release           |
| E0203 | reassignment of `param` binding                    | Type              | pre-release           |
| E0204 | mutation of `readonly` value                       | Type              | pre-release           |
| E0205 | non-constant expression in `const` right-hand side | Type              | pre-release           |
| E0206 | optional chaining `?.` in assignment target        | Type              | pre-release           |
| E0301 | type cycle with no terminating field               | Type              | pre-release           |
| E0302 | recursive type without indirection                 | Type              | pre-release           |
| E0303 | circular type dependency among top-level value bindings | Type         | pre-release           |
| E0401 | generic type argument count mismatch               | Type              | pre-release           |
| E0402 | generic constraint violation                       | Type              | pre-release           |
| E0501 | `for...in` subject is not iterable                 | Type              | pre-release           |
| E0502 | single-identifier `for...in` over a `map`          | Type              | pre-release           |
| E0503 | descending range without explicit negative `step`  | Type              | pre-release           |
| E0504 | reassignment of `for...in` iterator variable       | Type              | pre-release           |
| E0505 | non-exhaustive switch expression                   | Type              | pre-release           |
| E1001 | undefined identifier                               | Name resolution   | pre-release           |
| E1002 | undefined member                                   | Name resolution   | pre-release           |
| E1003 | undefined module                                   | Name resolution   | pre-release           |
| E1101 | shadowed declaration                               | Name resolution   | pre-release (warning) |
| E1102 | name already declared in this scope                | Name resolution   | pre-release           |
| E1103 | reserved identifier used as a binding name         | Name resolution   | pre-release           |
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
| E2211 | `break` inside `select`                            | Syntax            | pre-release           |
| E2212 | `break` / `continue` outside a loop                | Syntax            | pre-release           |
| E2213 | duplicate `catch` for the same type                | Syntax            | pre-release           |
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
| E5904 | unhandled exception reached the top level          | Runtime           | pre-release           |
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

### E0008 — named argument before positional

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A named argument appeared before a positional argument at a call site. The calling convention requires all positional arguments first, then named arguments.
- **Source decision:** D-318 (D-113).

---

### E0009 — named argument names a required parameter

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A named argument named a required (defaultless) parameter. Only parameters with a default value may be passed by name; required parameters are positional-only.
- **Source decision:** D-318 (D-113).

---

### E0010 — duplicate named argument

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A parameter was supplied more than once — named twice, or named and also supplied positionally.
- **Source decision:** D-318 (D-113).

---

### E0011 — unknown parameter name

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A named argument named a parameter the callee does not declare.
- **Source decision:** D-318 (D-113).

---

### E0012 — unknown field name

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A construction site named a field that is not declared on the type being constructed. The construction analogue of E0011 (unknown parameter name). See §10 and D-330.
- **Source decision:** D-330.

---

### E0013 — field default references sibling field

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A field's default-value expression references another field of the same type by name. Field defaults evaluate at the construction site in the construction-site scope, where the sibling fields of the type are not in scope. See §10.
- **Source decision:** D-330.

---

### E0014 — throw operand is not a GrobError subtype

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The operand of a `throw` statement did not resolve to `GrobError` or one of its ten leaves (D-284). `throw 42`, `throw "oops"` and throwing any other non-`GrobError` value are rejected here. Dedicated over folding into E0001 (general type mismatch) — the construction-site/throw-operand family already carries its own dedicated codes (E0011–E0013), per D-318/D-330's precedent of a distinct surface earning a distinct code.
- **Source:** `grob-language-fundamentals.md` §27; D-274; D-284.

---

### E0015 — catch type is not a GrobError subtype

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A typed `catch (<n>: <Type>)` clause named a type that is not `GrobError` or one of its ten leaves (D-284). `catch (e: int)` and similar are rejected here. Dedicated over folding into E0001 (general type mismatch), matching E0014's precedent for the throw-operand family — the catch-type surface is distinct and more actionable with its own code.
- **Source:** `grob-language-fundamentals.md` §27; D-274.

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

### E0206 — optional chaining `?.` in assignment target

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The optional-chaining operator `?.` cannot appear in the target position of an assignment (`a?.b = v`). An optional chain may produce `nil` when the receiver is nil, making the assignment semantically undefined. Use `.` (non-optional access) for assignment targets, or restructure the code to guard the nil case before assigning.
- **Source decision:** Sprint 6 Increment C (struct field assignment).

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

### E0303 — circular type dependency among top-level value bindings

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Two or more unannotated top-level value bindings (`readonly` or `:=`) form a cycle in their initialisers such that the type of each binding cannot be resolved without first knowing the type of another in the cycle. Annotated bindings are resolved from their declared type and do not participate in value-type dependency edges; annotated mutual cycles surface instead as runtime E5902.
- **Source decision:** D-323.

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

### E0501 — `for...in` subject is not iterable

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The subject of a `for...in` loop is not an array, a map or a numeric range. Only those three forms can be iterated.
- **Source decision:** Sprint 4 Increment C.

---

### E0502 — single-identifier `for...in` over a `map`

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A `for...in` loop over a `map` must bind two identifiers (`for k, v in m`). Iterate the keys alone with `for k in m.keys`.
- **Source decision:** Sprint 4 Increment C.

---

### E0503 — descending range without explicit negative `step`

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A numeric range whose start bound is greater than its end bound descends, and a descending range requires an explicit negative `step`. Add `step -1` (or another negative step) to iterate downward.
- **Source decision:** Sprint 4 Increment C.

---

### E0504 — reassignment of `for...in` iterator variable

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** The iteration variable of a `for...in` loop (`item`, the index `i`, or the map `k`/`v`) is immutable within the loop body and cannot be reassigned. Copy it to a `:=` binding to mutate a local.
- **Source decision:** Sprint 4 Increment C.

---

### E0505 — non-exhaustive switch expression

- **Category:** Type
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A switch expression must cover every possible value of its scrutinee (§3.1). Coverage is proven by a `_` catch-all arm, by matching both `true` and `false` for a `bool` scrutinee, or by matching `nil` and otherwise covering the element type for a nullable scrutinee. Relational patterns never contribute to exhaustiveness. Add a `_` arm or cover the remaining cases. This is the deliberate counterpart to the non-exhaustive `select` statement (D-301).
- **Source decision:** Sprint 4 Increment E.

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

### E1102 — name already declared in this scope

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A binding-introducing form (`:=`, `readonly`, `const`, `fn`, `type`) declared a name that is already bound in the same scope. The diagnostic is emitted at the second (offending) declaration. For `:=` use `=` to reassign an existing variable binding. For `fn`/`type`/`readonly`/`const` rename the declaration.
- **Source decision:** Sprint 3 Increment A; broadened to all top-level binding forms by D-324.

---

### E1103 — reserved identifier used as a binding name

- **Category:** Name resolution
- **Introduced:** v1
- **Status:** pre-release
- **Description:** A reserved identifier was used as a binding name — a field, parameter, local or function. Reserved identifiers lex as ordinary identifiers and stay legal as member names after a `.`, but they may not be bound by user code. The v1 reserved identifiers are `formatAs` (D-282) and `select` (D-320). Rename the binding. This is what lets the `select` statement and the `arr.select(fn)` pipeline transform (D-280) share the name, and what lets `formatAs` be a module accessor without occupying the binding namespace.
- **Source decision:** D-320 (rule generalised from D-282; the `formatAs` reserved-identifier rule shipped without a code and is now covered here).

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

### E2211 — `break` inside `select`

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `break` does not apply inside a `select` arm. `select` has no fall-through (D-301), so there is nothing for `break` to exit, and `break` is not retargeted at an enclosing loop. Fires at any nesting, whether or not a loop encloses the `select`. To exit an enclosing loop from inside a `select`, restructure into a function and use `return`, or use a flag variable. `continue` inside a `select` is permitted and applies to the nearest enclosing loop.
- **Source decision:** D-315.
- **Source:** `grob-language-fundamentals.md` §3.

---

### E2212 — `break` / `continue` outside a loop

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** `break` and `continue` are only valid inside a loop body (`while`, `for...in`). Neither appears inside any enclosing loop at the point of use. A `continue` inside a `select` resolves to the nearest enclosing loop and only raises this error when no loop encloses the `select`; a `break` inside a `select` raises E2211 instead.
- **Source decision:** D-315.
- **Source:** `grob-v1-requirements.md` §4.

---

### E2213 — duplicate `catch` for the same type

- **Category:** Syntax
- **Introduced:** v1
- **Status:** pre-release
- **Description:** Two typed `catch (<n>: <Type>)` clauses on the same `try` name the same exception type. Dedicated over folding into E2205 (`catch` after catch-all, D-083) — "catch for `IoError` already declared" is a distinct and more actionable diagnostic than the catch-all-ordering failure, per the `allocating-an-error-code` fold-vs-new precedent.
- **Source:** `grob-language-fundamentals.md` §27; D-274.

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

### E5904 — unhandled exception reached the top level

- **Category:** Runtime
- **Introduced:** v1
- **Status:** pre-release
- **Throws:** the thrown value's own type (always a `GrobError` subtype) — this code re-raises rather than detects one specific failure domain, so it carries no single fixed leaf the way every other Runtime code does.
- **Description:** A user-authored `throw` propagated past every call frame with no `catch` in the chain to handle it (Sprint 7 Increment A — no handler table exists yet; Increment B adds `try`/`catch`). The VM unwinds every frame, closing upvalues by location (D-325), and raises this code carrying the thrown exception's type name and `message` field. Distinct from the existing E53xx–E59xx codes, each of which is scoped to one specific detected VM-level failure (file not found, stack overflow, …) — none of those fit "a user-authored throw reached the top level", so a dedicated code was registered rather than folding into an ill-fitting one.
- **Source:** `grob-language-fundamentals.md` §27 (unhandled-exception behaviour); D-322 (`file:line` runtime diagnostic shape).

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

**Total: 116 codes across 7 categories.** This is the canonical current count;
it is the live total in the summary index above and is asserted equal to
`ErrorCatalog.All.Count` by the consistency drift gate (`Grob.Consistency.Tests`,
D-316). The dated lines below are the historical record of how the count
changed; this line is the single source for the present total.

_Initial allocation: 94 codes across 7 categories. All `pre-release` until v1.0 ships. Authority: ADR-0014 (numbering scheme) and ADR-0017 (stability rule)._

_Updated May 2026 — count corrected from a stale "86 codes" to the actual 94 codes present in the summary index and full entries. No codes were added in this edit; the footer total had not been updated as codes accrued. The 7-category structure (E0xxx–E9xxx) is unchanged._

_Updated June 2026 — Sprint 4 Increment C added the `for...in` iteration diagnostics E0501–E0504 in the previously empty E05xx sub-block of the Type category, bringing the total to 98 codes._

_Updated June 2026 — Sprint 4 Increment E added E0505 (non-exhaustive switch expression) to the E05xx sub-block of the Type category, bringing the total to 99 codes._

_Updated June 2026 — D-315: E2211 retitled to `break` inside `select` and E2212 retitled to `break` / `continue` outside a loop, reflecting the asymmetric resolution. Both codes pre-existed; no new codes were added by this edit._

_Updated June 2026 (interlude A, D-316) — canonical total corrected from a stale "99 codes" to the actual 103 present in the summary index and `ErrorCatalog`. Four codes had accrued without a footer count update: E0205 (Sprint 3), E1102 (Sprint 3), E2211 and E2212 (Sprint 4, D-315). No codes were added, removed or renumbered by this edit; the drift was in the prose total alone. A standing total line has been added above and the count is now gated by `Grob.Consistency.Tests` so this class of drift cannot recur silently._

_Updated June 2026 — Sprint 5 Increment B added the four named-argument call-site diagnostics E0008–E0011 (named-before-positional, naming a required parameter, duplicate named argument, unknown parameter name) in the E00xx sub-block of the Type category, bringing the total to 107 codes. Source decision D-318 (D-113)._

_Updated June 2026 — Sprint 5 correctness increment added E1103 (reserved identifier used as a binding name) in the E11xx sub-block of the Name resolution category, bringing the total to 108 codes. The code covers both `select` (D-320) and `formatAs` (D-282) — D-282's reserved-identifier rule had shipped with no code. Source decision D-320._

_Updated July 2026 — Sprint 7 Increment A added two codes: E0014 (`throw` operand is not a `GrobError` subtype) in the E00xx sub-block of the Type category, and E5904 (unhandled exception reached the top level) in the E59xx sub-block of the Runtime category, bringing the total from 112 to 114. E5904's `Throws` leaf is `GrobError` (a new `GrobErrorLeaf` member) rather than one of the ten typed leaves — the code re-raises whatever the script itself threw, so it carries no single fixed leaf the way every other Runtime code does. Allocated per `allocating-an-error-code` (D-330/D-318 precedent for a dedicated construction/throw-site code over folding into an ill-fitting existing one)._

_Updated July 2026 — Sprint 7 Increment B added two codes: E0015 (catch type is not a `GrobError` subtype) in the E00xx sub-block of the Type category, and E2213 (duplicate `catch` for the same type) in the E22xx sub-block of the Syntax category, bringing the total from 114 to 116. Both dedicated over folding into an existing code (E0001 and E2205 respectively), per the E0014/E0011–E0013 precedent. The increment's own hand-off had said the prior count was "112 → 113" — the live registry and `ErrorCatalog` both showed 114 (Increment A added two codes, not one); this edit reconciles to the live total rather than the stale prompt arithmetic._

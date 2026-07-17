# Grob — Language Fundamentals

> Specification document for core language syntax and semantics.
> Decisions authorised in the decisions log — April 2026 fundamentals session.
> This document is the implementation reference for the parser, type checker,
> and compiler for all foundational language features.
> When this document and the decisions log conflict, the decisions log wins.
>
> **Tooling note:** The keyword list and operator set in this document are the
> authoritative source for the TextMate grammar (`grob.tmLanguage.json`).
> When keywords are added or changed, the grammar must be updated.
> See `grob-tooling-strategy.md`.

---

## 1. Control Flow — `if/else`

```grob
if (condition) {
    // then block
}

if (condition) {
    // then block
} else {
    // else block
}

if (condition) {
    // then block
} else if (condition) {
    // else if block
} else {
    // fallback block
}
```

**Rules:**

- Parentheses around the condition are required.
- Opening brace must be on the same line as the `if` or `else if` keyword.
- `else if` is two keywords — not `elif` or `elsif`.
- `else {` must be on the same line as the closing brace of the preceding block.
- Nesting is unlimited — no compiler limit, convention only.
- `if/else` is a **statement**, not an expression. It cannot appear in expression
  position. Expression-position conditionals use ternary `? :` (two-way) or switch
  expression (multi-branch).

---

## 2. Control Flow — `while`

```grob
while (condition) {
    // body
}
```

**Rules:**

- Parentheses around the condition are required.
- Opening brace must be on the same line as `while`.
- `do...while` is deferred post-MVP.
- `for...in` and functional methods are the preferred style for collection
  iteration. `while` exists for condition-driven loops where no collection
  is being iterated.

---

## 3. Control Flow — `select/case`

`select` is the multi-branch statement for executing blocks based on a matched
value. It is distinct from the switch expression, which produces a value.

```grob
select (value) {
    case 0 {
        print("Zero")
    }
    case 1, 2 {
        print("One or two")
    }
    case "error" {
        log.error("Error state")
    }
    default {
        print("Something else: ${value}")
    }
}
```

**Rules:**

- Parentheses around the subject value are required.
- First matching case executes. No fall-through.
- Multiple values per case arm: `case 1, 2 { }` — matches either value.
- `default` arm is optional. If omitted and no case matches, execution continues
  past the `select` block with no error.
- Exhaustiveness is **not** enforced on `select` statements. See "Why `select`
  is non-exhaustive" below.
- Works on any comparable type: `int`, `string`, `bool`.
- `break` inside a `select` arm is a **compile error** (E2211) at any nesting,
  whether or not a loop encloses the `select`. There is no fall-through to break
  out of (D-301), so `break` has no meaning here, and silently retargeting it at
  the enclosing loop would betray the C# reflex of writing a case-terminating
  `break`. To exit an enclosing loop from inside a `select`, restructure into a
  function and use `return`, or use a flag variable. (D-315)
- `continue` inside a `select` arm applies to the **nearest enclosing loop**,
  exactly as in C# — `select` is not iterative, so there is nothing select-local
  for `continue` to do. `continue` with no enclosing loop is a compile error
  (E2212). (D-315)
- `select` is always a **statement**. The switch expression (`value switch { }`)
  is always an **expression**. The two forms are syntactically unambiguous.
- `select` is a **reserved identifier, not a hard keyword** (D-320). The lexer
  emits it as an `Identifier`; the parser reads a leading `select (` at
  statement-head position as this statement, and `select` after a `.` as a
  method name. This is what lets the pipeline transform `arr.select(fn)` (D-280)
  and the `select` statement coexist without colliding. User code may not
  declare a binding named `select` — field, parameter, local or function — which
  is a compile error (E1103), the same rule that governs `formatAs` (D-282). See
  §12, "Reserved identifiers".

### Why `select` is non-exhaustive

The switch _expression_ (§3.1) enforces exhaustiveness because every input must
produce a value — a missing case means a missing value, which is a bug. The
`select` _statement_ runs side-effecting blocks and produces no value. "No case
matched, so do nothing" is a legitimate intent, not a bug. Forcing exhaustiveness
on the statement form would push authors to write `default { }` solely to
satisfy the checker, which adds noise without adding safety.

This is the same split C# draws between its `switch` statement (non-exhaustive)
and its switch expression (exhaustive), for the same reason. F#, Scala, Rust and
Kotlin make a comparable distinction.

The two forms cover different jobs. Reach for the expression when you are
producing a value; reach for the statement when you are running side-effecting
branches. Pick the form that matches the intent, not the form whose
exhaustiveness rule is more convenient.

---

## 3.1 Conditional Expression — Switch Expression

`switch` is the multi-branch expression for producing a value from a matched
pattern. It is distinct from the `select` statement (§3), which executes blocks
without producing a value.

```grob
// Value patterns
message := http_code switch {
    200 => "OK",
    404 => "Not found",
    500 => "Server error",
    _   => "Unknown"
}

// Relational patterns
status := used_pct switch {
    >= crit_percent => "CRITICAL",
    >= warn_percent => "WARNING",
    _               => "OK"
}

// Nullable scrutinee
label := user switch {
    nil => "anonymous",
    _   => user.name
}
```

**Pattern forms.** Three pattern forms are legal in v1:

- **Value pattern** — a compile-time constant expression of the scrutinee's
  type. Literals and `const`-bound identifiers are valid; function calls and
  mutable bindings are not. `nil` is a valid value pattern when the scrutinee
  is nullable. `nil` on a non-nullable scrutinee is a compile error.
- **Relational pattern** — `>= expr`, `> expr`, `<= expr`, `< expr`, where
  `expr` is a compile-time constant. Legal only when the scrutinee is of an
  ordered type (`int`, `float`, `string`, `date`). `==` and `!=` are not
  relational-pattern operators — equality is the value-pattern form, and a
  "not equal to X" arm would not admit useful exhaustiveness analysis.
- **Catch-all** — `_`. Matches any value.

**Arm separator.** Arms are separated by commas. A trailing comma after the
final arm is permitted (§16). Newline-as-separator is not supported.

**Evaluation.** Arms are tested in source order. The first matching arm wins;
subsequent arms are not evaluated. This matches the convention of Rust, F#,
Scala, and every major pattern-matching language.

**Exhaustiveness.** The type checker enforces exhaustiveness. A switch
expression is exhaustive when:

- A `_` arm is present, or
- The scrutinee is `bool` and both `true` and `false` are matched, or
- The scrutinee is nullable `T?` and `nil` is matched and `T` is otherwise
  exhaustively covered.

Relational patterns never contribute to exhaustiveness. Any switch using a
relational arm requires `_`.

**Types.** Pattern expressions must be assignable to the scrutinee type —
`"foo"` against an `int` scrutinee is a compile error. All arm results must
produce the same type; mismatched arm types are compile errors.

**Deferred post-MVP.** Multi-value arms (`200, 201 =>`), range patterns
(`100..199 =>`), and type patterns (`string s =>`) are not in v1. Each is an
additive grammar extension — adding them later requires no rework of the v1
pattern forms.

---

## 4. Control Flow — `break` and `continue`

```grob
for file in files {
    if (file.name == "skip.log") {
        continue
    }
    if (file.size > maxSize) {
        break
    }
    process(file)
}
```

**Rules:**

- `break` exits the innermost enclosing loop immediately.
- `continue` skips the remainder of the current iteration and proceeds to the next.
- Both work in `for...in` and `while` loops.
- Both are statements — they may not appear outside a loop body. Using either
  outside a loop is a compile error.
- Labelled break (for breaking an outer loop from inside a nested loop) is
  deferred post-MVP. The v1 alternative is to extract the inner loop into a
  function and use `return`.

---

## 5. Loops — `for...in`

### Collection iteration

```grob
// Value only — array
for file in files {
    print(file.name)
}

// Index and value — array
for i, file in files {
    print("${i}: ${file.name}")
}

// Key and value — map (two-identifier form required)
for k, v in headers {
    print("${k}: ${v}")
}
```

**Rules:**

- `for item in collection { }` — confirmed syntax.
- `for i, item in collection { }` — index form for arrays. `i` is zero-based
  `int`, inferred.
- The single-identifier form always binds the **value**, not the index.
- Both identifiers are declared by the `for` statement — no `:=` in the loop
  header. Both are scoped to the loop body.
- The loop variable is **immutable** within the body — reassigning it is a compile
  error.

**Iterable types in v1 — special-cased by the compiler:**

The compiler handles three `for...in` cases. Any other type in subject position
is a compile error.

1. **Numeric range** (`for i in 0..10`) — lowered to `while`. See numeric range
   section below.
2. **`T[]` array** — lowered to an index-based `while` loop. Both
   single-identifier and two-identifier forms supported.
3. **`map<K, V>`** — iterates keys in insertion order. The **two-identifier form
   is required** (`for k, v in myMap`). The single-identifier form on a map is a
   compile error:

    ```
    error: `for k in myMap` is not valid for map<K, V>.
    Use `for k, v in myMap` to iterate key-value pairs,
    or `for k in myMap.keys` to iterate keys only.
    ```

    Lowered to a `while` loop over an internal keys array.

A formal iterable protocol is post-MVP. The compiler architecture accommodates
it without rework — the three special cases become the first implementors of the
protocol when it is defined.

### Numeric range iteration

```grob
// Basic range — inclusive both bounds
for i in 0..10 { }          // 0, 1, 2 ... 10

// With step
for i in 0..100 step 5 { }  // 0, 5, 10 ... 100

// Descending
for i in 10..0 step -1 { }  // 10, 9, 8 ... 0
```

**Rules:**

- `..` operator — both bounds are **inclusive**.
- `step` is optional. Default step is `1`.
- Step may be any non-zero `int` literal or variable.
- A descending range without an explicit negative step is a **compile error** —
  `for i in 10..0 { }` without `step -1` would produce an infinite loop and is
  rejected at compile time.
- The loop variable is `int`, declared by the `for` statement, immutable within
  the body.
- The compiler lowers range loops to `while`. The VM never sees range opcodes.
- `..` is a range-specific operator in v1 — it does not appear in other expression
  contexts.

---

## 6. Operators

### Arithmetic

| Operator | Operation                       | Notes                    |
| -------- | ------------------------------- | ------------------------ |
| `+`      | Addition / string concatenation |                          |
| `-`      | Subtraction                     |                          |
| `*`      | Multiplication                  |                          |
| `/`      | Division                        | See division rules below |
| `%`      | Modulo                          | See modulo rules below   |

**Integer division:** `int / int → int` (truncating toward zero). `-7 / 2` is
`-3`, not `-4`. No separate floor-division operator — the operand types
determine the result. `x / 0` throws `ArithmeticError` — consistent with `x % 0`,
`x / 0.0`, and `x % 0.0`. Integer division by zero is never silent.

**Float division:** `float / float → float`. Division by zero (`x / 0.0`)
throws `ArithmeticError`, not infinity.

**Mixed division:** `int / float` and `float / int` promote the `int` to
`float`. The result is `float`.

**Integer modulo:** `int % int → int`. The result has the sign of the dividend
(the left operand), following C#'s `long % long` and C's `%` semantics.
`-7 % 3` is `-1`, not `2`. `x % 0` throws `ArithmeticError`.

**Float modulo:** `float % float → float`, following IEEE 754 `fmod` semantics
as implemented by C#'s `double % double`. The result has the sign of the
dividend. `-7.5 % 2.0` is `-1.5`. `x % 0.0` throws `ArithmeticError` — consistent
with division by zero.

**Mixed modulo:** `int % float` and `float % int` promote the `int` to
`float`. The result is `float`.

**NaN and Infinity:** If either operand of `%` is `NaN` or `±Infinity`
(produced only by `math` functions such as `math.sqrt(-1.0)`), the result
follows IEEE 754: `NaN % y` and `x % NaN` are `NaN`; `±Infinity % y` is `NaN`;
`x % ±Infinity` is `x`. These cases are not errors — the language propagates
non-finite values through arithmetic silently.

**String concatenation:** `+` on strings is valid. `string + int` is a compile
error — no implicit conversion. Use `.toString()` or string interpolation.

### Comparison

| Operator | Operation             |
| -------- | --------------------- |
| `==`     | Equal                 |
| `!=`     | Not equal             |
| `<`      | Less than             |
| `>`      | Greater than          |
| `<=`     | Less than or equal    |
| `>=`     | Greater than or equal |

### Logical

| Operator | Operation   | Notes                                                          |
| -------- | ----------- | -------------------------------------------------------------- |
| `&&`     | Logical AND | Short-circuit — right operand not evaluated if left is `false` |
| `\|\|`   | Logical OR  | Short-circuit — right operand not evaluated if left is `true`  |
| `!`      | Logical NOT |                                                                |

### Unary

| Operator | Operation           |
| -------- | ------------------- |
| `-`      | Arithmetic negation |
| `!`      | Logical NOT         |

### Other

| Operator | Operation                                                       |
| -------- | --------------------------------------------------------------- |
| `??`     | Nil coalescing — `a ?? b` returns `a` if non-nil, otherwise `b` |
| `?.`     | Optional chaining — `a?.foo` returns nil if `a` is nil          |
| `..`     | Range (numeric for loops only in v1)                            |

Bitwise operators are not in scope for v1.

Declaration (`:=`), assignment (`=`, `+=`, `-=`, `*=`, `/=`, `%=`), and
increment/decrement (`++`, `--`) are **statement forms**, not expressions.
They are specified in §28.

---

## 7. Operator Precedence

Highest to lowest. Parentheses override precedence at any level. This table
describes **expression-level operators only**. Statement forms (declaration,
assignment, increment, decrement, `throw`) are not expressions and appear
in §28.

| Level       | Operators             | Notes                                                |
| ----------- | --------------------- | ---------------------------------------------------- |
| 1 (highest) | `()`, `[]`, `.`, `?.` | Postfix — call, index, member access, optional chain |
| 2           | `-` (unary), `!`      | Prefix                                               |
| 3           | `*`, `/`, `%`         | Multiplicative                                       |
| 4           | `+`, `-`              | Additive                                             |
| 5           | `..`                  | Range (numeric `for` loops only in v1)               |
| 6           | `<`, `>`, `<=`, `>=`  | Comparison                                           |
| 7           | `==`, `!=`            | Equality                                             |
| 8           | `&&`                  | Logical AND — short-circuit                          |
| 9           | `\|\|`                | Logical OR — short-circuit                           |
| 10          | `??`                  | Nil coalescing                                       |
| 11          | `? :` (ternary)       | Conditional expression                               |
| 12 (lowest) | `=>`                  | Lambda arrow — body expression binds loosely         |

This matches C-family precedence with the addition of `??` and `..`. `??` binds
tighter than ternary, consistent with C#, Kotlin, Swift, and TypeScript — the
reference languages for Grob's nullable operator family. The Pratt parser
implements this table as binding powers; this table is the canonical reference
for the implementation.

---

## 8. Literals

### Integer literals

```grob
42          // decimal
0           // zero
-1          // negative
0xFF        // hexadecimal
0xff        // hexadecimal — case insensitive
0b1010      // binary
1_000_000   // underscore separator — readability only, ignored by compiler
0xFF_FF     // underscores valid in hex and binary literals too
```

### Float literals

```grob
3.14        // standard
0.5         // leading zero required — .5 is not valid
1_000.50    // underscore separator — readability only, same as integers
```

Leading dot (`.5`) is not valid — a leading digit is always required.

Scientific notation (`1.5e10`, `2.3E-4`) is deferred to post-MVP.

### String literals

Three string forms. Each maps to a distinct developer intent.

```grob
"hello"                     // double-quoted — standard form
"Hello ${name}"             // interpolation — confirmed load-bearing
`C:\Users\chris\docs`       // single backtick — inline raw, no escape processing
`/^\d+$/`                   // single backtick — regex patterns
```

````grob
query := ```
SELECT *
FROM users
WHERE active = 1
```                         // triple backtick — multiline verbatim block
````

**Double-quoted strings**

- Standard form for most string values.
- Support escape sequences: `\n`, `\r`, `\t`, `\\`, `\"`, `\$`.
- Support `${expr}` interpolation.
- Do not span lines — a newline before the closing `"` is a compile error.
- `\$` produces a literal `$` without triggering interpolation.
- `\r` produces a carriage return — needed for explicit `\r\n` Windows line endings.
- Any other `\x` sequence not in the set above is a compile error — no silent
  pass-through of unknown escapes.

**Nullable interpolation.** Interpolating a nullable-typed expression (`T?`)
is a compile error. Before a nullable value can appear in an interpolation
slot, it must be resolved to `T` — usually by `?? <fallback>`, sometimes by
narrowing inside an `if (x != nil)` block.

```grob
user: User? := findUser(id)

msg := "Hello ${user.name}"               // compile error — user is nullable
msg := "Hello ${user?.name}"              // compile error — chain result is nullable
msg := "Hello ${user?.name ?? "guest"}"   // valid — ?? resolves to string

if (user != nil) {
    msg := "Hello ${user.name}"           // valid — narrowed (§21)
}
```

The rule applies to any expression whose static type is nullable: direct
nullable bindings, functions returning `T?`, optional chains, and try-parse
results. Values narrowed to non-nullable inside a narrowing `if` block are not
subject to the rule — they are already non-null at that program point.

The compile error is deliberate. Grob's treatment of nullability is strict
throughout — nil is never silently coerced, at any site. Interpolation is no
exception. The asymmetry with `print()` — which accepts nil and renders it as
`"nil"` (§13) — is intentional: `print()` is a diagnostic sink; interpolation
constructs a string where silent nil coercion would produce output like
`"Hello nil"` that almost always indicates a bug.

**Single backtick strings**

- Inline raw form. Intended for paths, patterns and short verbatim values
  where escape sequences would be noisy.
- No escape processing — content is verbatim.
- Cannot contain a backtick character. No workaround — use a double-quoted
  string with `\"` if a backtick is needed in the value.
- Do not span lines — a newline before the closing backtick is a compile error.
- Interpolation is not supported — `${expr}` inside a single backtick string
  is a literal character sequence, not an expression.

**Triple backtick strings**

- Multiline verbatim block. Intended for SQL, JSON templates, multiline
  command strings and similar structured content.
- No escape processing — content is verbatim.
- Newlines, tabs and spaces are preserved exactly as written. No trimming.
- May contain single backticks freely. Cannot contain ````` (three
  consecutive backticks) as that closes the literal.
- Interpolation is not supported.
- Content begins immediately after the three opening backticks. The opening
  delimiter is typically followed by a newline, which becomes the first
  character of the string value. To avoid the leading newline, place the
  opening triple backtick on the same line as the first content character.
- Triple backtick content is **verbatim** in v1 — no indentation trimming.
  If the closing triple backtick is indented, that indentation does **not**
  strip leading whitespace from the content above. This may be revisited
  post-MVP (C#-style trim-to-closing-delimiter is the reference model).

**Choosing between forms:**

| Intent                                         | Form              |
| ---------------------------------------------- | ----------------- |
| Normal string value                            | `"double-quoted"` |
| String with interpolation                      | `"Hello ${name}"` |
| Windows path, regex pattern, short verbatim    | `single backtick` |
| SQL, multiline template, structured text block | `triple backtick` |

Single-quoted strings (`'value'`) are not valid in Grob.

**Windows paths and literal backslash content.** Single-backtick raw strings
are the canonical Grob idiom for Windows paths, regex patterns, and any
other string whose content contains literal backslashes. The double-quoted
form processes escape sequences and will either reject unknown escapes at
compile time or — worse — silently substitute a defined escape where the
author meant a backslash followed by a letter.

```grob
path := `C:\Users\Chris\Downloads`     // clean — no escaping needed
path := "C:\\Users\\Chris\\Downloads"  // works but awkward
path := "C:\Users\Chris\Downloads"     // compile error: \U is not a defined escape
path := "C:\temp\log.txt"              // DANGER: \t silently becomes a tab character
```

The last case is the dangerous one. `\t` is a defined escape in Grob, so
the compiler accepts the string without complaint — but the resulting
value contains a literal tab where the author wanted the path segment
`\temp`. Subsequent `fs.*` operations fail with a puzzling "directory not
found" error. The backtick form sidesteps the entire category: every
character between backticks is verbatim.

The convention applies equally to regex patterns:

```grob
pattern := `^\d{4}-\d{2}-\d{2}$`       // clean
pattern := "^\\d{4}-\\d{2}-\\d{2}$"    // valid but noisy — every \d is now \\d
```

The rule: if a string is literal content that happens to contain
backslashes, use single backticks. Reach for double quotes only when
interpolation is needed or the string is an ordinary human message.

### Bool literals

```grob
true
false
```

### Nil literal

```grob
nil
```

### Array literals

```grob
[1, 2, 3]               // int array, inferred
["alpha", "beta"]       // string array, inferred
[]                      // empty array — valid where type is known from context
```

`[]` requires the type to be known from context — an annotated left-hand side,
a parameter type, or a return type. `[]` in a position where the element type
cannot be inferred is a compile error.

```grob
items: int[] := []      // valid — type known from annotation
fn foo(): string[] {
    return []           // valid — type known from return type
}
items := []             // compile error — element type cannot be inferred
```

---

## 9. Type Annotations

```grob
x := 42                  // inferred — x is int
x: int := 42             // explicit annotation — always valid, never required here

const MAX := 100         // inferred, compile-time constant
const MAX: int := 100    // explicit, compile-time constant

readonly TOKEN := env.require("ADO_PAT")        // inferred, runtime-once
readonly CUTOFF: date := date.today().addDays(-30)

name: string? := nil     // annotation required — nil provides no type information
items: int[] := []       // annotation required — [] provides no element type
```

**Rules:**

- Explicit type annotation syntax: `name: Type := value`.
- Annotation is **optional** on local variables where inference can resolve the type.
- Annotation is **required** where inference cannot:
    - Declaration initialised with `nil` — `name: string? := nil`.
    - Declaration initialised with `[]` — `items: int[] := []`.
- Function parameters are **always explicitly typed** — no inference on parameters.
- Return type annotation on functions: `fn foo(): int { }` — colon before the
  return type, consistent with parameter syntax.
- **v1 rule:** explicit return types are required on all functions. Inference on
  return types is post-MVP. This eliminates a class of inference ambiguity and
  keeps the type checker simple.

**Type-reference grammar (D-326, D-327):**

A type reference written in source (`TypeRef`) is a primary type carrying a postfix
suffix chain:

```ebnf
TypeRef     := TypePrimary TypeSuffix*
TypeSuffix  := '[' ']'                  // array of the preceding type
             | '?'                       // nullable
TypePrimary := Identifier TypeArgs?      // named type: int, string, map<K, V>, File, T
             | 'fn' '(' (TypeRef (',' TypeRef)*)? ')' ':' TypeRef   // function type: fn(int): bool
             | '(' TypeRef ')'           // grouping — required to suffix a function type
```

**Suffix chain.** Suffixes apply left to right. `int[]` is an array of `int`;
`int[][]` an array of arrays (D-182); `int[]?` a nullable array of `int`; `int?[]`
an array of nullable `int`. The last two are distinct types and both parse.

**Function-type precedence.** A function type's return is itself a `TypeRef`, so a
trailing suffix binds to the **return type**, not to the function: `fn(): int?`
returns `int?` and `fn(): int[]` returns `int[]`. A nullable function, or an array
of functions, requires grouping parens — `(fn(): int)?`, `(fn(): int)[]` — supplied
by the `'(' TypeRef ')'` primary. D-327 generalises D-326's earlier dedicated
`'(' TypeRef ')' '?'` production into this grouping primary plus the shared suffix
chain.

```grob
counter: fn(): int := makeCounter()    // variable holding a counter closure
apply: fn(int): bool                   // higher-order parameter
make: fn(): fn(): int := makeCounter   // fn returning fn (parens only needed for nullable)
```

---

## 10. Type Declarations and Construction

### Declaration

```grob
type Repo {
    name:    string
    ssh_url: string
}

type Config {
    host:    string
    port:    int = 8080       // field default
    timeout: int = 30         // field default
}
```

- `type` keyword followed by the type name and a brace-delimited field list.
- Fields without defaults are **required** at construction.
- Fields with defaults are **optional** at construction — the default is used if omitted.

### Construction

```grob
r := Repo {
    name:    "grob",
    ssh_url: "git@github.com/..."
}

c := Config {
    host: "localhost"          // port and timeout use defaults
}
```

- Fields at construction are **named and unordered** — order need not match the
  declaration. Matches C# object initialiser behaviour.
- All field names must match the declared type. Unknown field names are a compile error.
- Omitting a field without a default is a compile error.

### Nested construction

```grob
type Address {
    city:    string
    country: string
}

type Person {
    name:    string
    address: Address
}

p := Person {
    name:    "Chris",
    address: Address { city: "London", country: "UK" }
}
```

`TypeName { }` is an expression that produces a value of that type. It can appear
anywhere that type is expected — nested construction requires no special case.

### Field default evaluation

Field default expressions evaluate at **construction time**, in the scope of the
construction site.

```grob
type Config {
    host:    string
    port:    int    = 8080                // literal default
    retries: int    = computeDefault()    // function call
    created: date   = date.now()          // stdlib call
    suffix:  string = "-${env.get("TAG") ?? "dev"}" // runtime expression
}
```

Rules:

- A default expression may be **any expression that is legal in a general
  expression position** — literals, function calls, interpolated strings,
  stdlib calls, method chains, anonymous struct literals, nested named type
  construction. There is no compile-time-constant restriction on defaults.
- A default expression may reference any identifier in scope at the
  **construction site** — `const` and `readonly` bindings, mutable variables,
  function names, imported modules. It may **not** reference other fields of
  the type being constructed; during construction, sibling fields may not yet
  have been assigned, and the reference is a compile error.
- When a construction supplies every field explicitly, no default expression
  evaluates. Defaults have no side effects for fields that are not omitted.
- When a construction omits a field, the field's default expression evaluates
  once for that construction. Side effects (function calls, stdlib reads,
  date capture) fire on every construction that omits the field.
- A default is not a compile-time constant. Even when the right-hand side is a
  literal, it is still a runtime expression in the specification. The compiler
  may inline a literal default as an optimisation, but this is an
  implementation detail, not a language rule.
- The `const` / `readonly` modifiers do not appear at the field-default
  declaration site. A field default is part of the type declaration, not a
  separate binding.

```grob
type Entry {
    id:        guid   = guid.new()      // fresh GUID per construction
    timestamp: date   = date.now()      // fresh timestamp per construction
    label:     string = "unknown"       // literal default
}

e1 := Entry { label: "first"  }          // id and timestamp evaluate now
e2 := Entry { label: "second" }          // id and timestamp evaluate again
e3 := Entry {                            // label evaluates too
    id:        guid.parse("..."),
    timestamp: date.today(),
    label:     "third"
}                                        // no defaults evaluate
```

**Interaction with `readonly` bindings.** A `readonly` binding of a type with
defaults is unchanged: defaults evaluate at construction time and produce the
value the binding holds. Deep immutability (§24, D-291) prevents subsequent
mutation, but does not affect default evaluation.

```grob
readonly CFG := Config { host: "example.com" }
// port, retries, created and suffix defaults evaluate at this line.
// CFG is then frozen — its fields cannot be reassigned or mutated.
```

**Interaction with field-default exceptions.** If a default expression throws
at construction time, the exception propagates from the construction site as
if the caller had written the expression inline. Standard exception handling
applies (§27).

### The bare brace rule

`{ }` is **always a block**. Using bare braces as an object literal is a compile
error:

```
error: '{' begins a block, not a struct literal.
       Use '#{ field: value }' for an anonymous struct, or declare a named type.
```

| Syntax                      | Meaning                  |
| --------------------------- | ------------------------ |
| `{ }`                       | Block — always           |
| `#{ field: value }`         | Anonymous struct literal |
| `TypeName { field: value }` | Named type construction  |

Partial construction and `with`-style record mutation are deferred post-MVP.

---

## 11. Comments

```grob
// Single-line comment

/* Block comment
   spans multiple lines */

/// Doc comment — reserved, no semantics in v1
```

- `//` — single-line. Everything from `//` to end of line is ignored.
- `/* */` — block comment. Does not nest.
- `///` — the lexer recognises this token and discards it. No semantics attached
  in v1. Reserved so that `///` usage in scripts does not break when `grob doc`
  tooling arrives post-MVP.

---

## 12. Expressions vs Statements

### Statements

These constructs are statements — they execute but do not produce a value:

- Variable declaration: `:=`
- Reassignment: `=`
- Compound assignment: `+=`, `-=`, `*=`, `/=`, `%=`
- Increment / decrement: `i++`, `i--`
- `if/else`
- `while`
- `select/case`
- `for...in`
- `try/catch`
- `break`, `continue`
- `return`

Assignment is **not** an expression. `if (x := foo())` is a compile error. This
eliminates a class of bugs common in C-family languages.

### Expressions

These constructs are expressions — they produce a value:

- Literals: `42`, `"hello"`, `true`, `nil`, `[1, 2, 3]`
- Identifiers: `x`, `name`
- Arithmetic, comparison, logical operations
- Function calls — produce the return value. May be used as a statement (value discarded).
- Ternary `? :` — two-way value selection
- Switch expression (`value switch { }`) — multi-branch value selection
- Anonymous struct literal `#{ }`
- Named type construction `TypeName { }`
- Method calls: `str.upper()`
- Member access: `file.name`

### `print()` and `void`

`print()` is a built-in function returning `void`. It is used as a statement.
Its return value cannot be assigned or used in expression position.
`void` is not a first-class type in Grob — it exists only as the return type of
functions that produce no value.

### Block-body lambdas

Lambdas with a block body (`x => { ... }`) produce a value in one of two ways:

- **Implicit.** If the block's final statement is an expression (literal,
  identifier, arithmetic, call, ternary, switch expression, struct construction,
  method call, member access), that expression's value is the lambda's return
  value and its type is the lambda's inferred return type.
- **Explicit early return.** `return <expr>` inside the block exits the lambda
  immediately with the given value. `return` in a lambda returns from the
  lambda — it never returns from the enclosing function.

The two mechanisms may coexist. The type checker requires all return paths
(the implicit final expression and every explicit `return`) to produce the
same type.

```grob
// Implicit last expression
.select(line => {
    parts := line.split("|")
    BranchInfo { branch: parts[0], date: parts[1], author: parts[2] }
})

// Implicit with early return
.select(r => {
    if (r.status == "deleted") {
        return EmptyRecord { }
    }
    FullRecord { id: r.id, name: r.name, size: r.size }
})
```

If the block's final statement is not an expression — a declaration,
assignment, increment, `if`, `while`, `for`, `select`, `try`, `break`,
`continue`, or `throw` — the lambda's inferred return type is `void`. A void
lambda cannot be used in a position that requires a value.

`return` without an expression is valid only if the lambda is typed as `void`.

Lambda return types are always inferred from the body. There is no syntax to
declare a lambda's return type in v1.

### Reserved identifiers

A small set of names lex as ordinary identifiers but may not be used as binding
names — field, parameter, local or function. They are **reserved identifiers**,
not keywords: the lexer emits `Identifier` for them, and they remain legal as
member names after a `.`. Declaring a binding with one of these names is a
compile error (E1103).

The v1 reserved identifiers are:

- `formatAs` (D-282) — the collection-to-string terminator module. A bare
  `<expr>.formatAs` with no following method call is additionally an error,
  because `formatAs` carries compiler-rewrite sugar.
- `select` (D-320) — reserved so the `select` statement (§3) and the universal
  pipeline transform `arr.select(fn)` (D-280) can share the name. Unlike
  `formatAs`, `select` is an ordinary method, so `.select` is plain member
  access with no bare-member rule.

Reserving an identifier rather than adding a keyword keeps a name usable as a
method while removing it from the binding namespace. No registered native method
name may collide with a reserved word; the consistency suite (D-316) checks this
mechanically, so a new built-in whose name shadows a keyword fails the build
rather than shipping as a call no source program can write.

---

## 12.1 Closure Semantics and Variable Resolution

A lambda is a function value. Its body may reference identifiers from any
scope that is visible at the lambda's definition site. How each reference is
resolved at runtime depends on the category of the referenced binding.

Four categories need to be distinguished:

### 1. Top-level `const`

Resolved at compile time. `const` identifiers have no runtime slot. Every
reference — whether from top-level code, from a function body, or from a
lambda body — is replaced by the compiler with a direct load from the
constant pool.

```grob
const MAX := 100

readonly isLarge := (n: int) => n > MAX    // 'MAX' inlines as 100
```

### 2. Top-level `readonly`

Resolved at runtime via the globals table. The value never changes after
its declaration has executed, so every read yields the same value, but
mechanically the lambda emits a global-read opcode, not an upvalue load.

```grob
readonly THRESHOLD := readConfig().threshold

check := (n: int) => n > THRESHOLD         // reads THRESHOLD via globals
```

### 3. Top-level mutable

Resolved at runtime via the globals table. The value may change between
lambda invocations; each invocation reads the current value, and writes
from the lambda body update the global binding.

```grob
counter := 0

increment := () => { counter = counter + 1 }
current   := ()  => counter

print(current())    // 0
increment()
increment()
print(current())    // 2
```

Both `increment` and `current` see the same `counter` binding. There is no
capture; each reference is a direct read from or write to the globals table.

### 4. Locals in enclosing function scopes

Captured as **upvalues** per the standard closure mechanism. A lambda
defined inside a function body that references a local from the enclosing
function extends that local's lifetime beyond the enclosing function's
return.

```grob
fn makeCounter(): fn(): int {
    count := 0                     // local to makeCounter
    return () => {
        count = count + 1          // captured as upvalue
        count
    }
}

c1 := makeCounter()
c1()    // 1
c1()    // 2
c2 := makeCounter()                // independent counter
c2()    // 1
```

Each call to `makeCounter` produces a lambda with its own captured `count`.
The two counters do not share state.

### The term "capture"

In this specification, "capture" applies only to category 4 — locals from
enclosing function scopes. A lambda does not "capture" top-level bindings;
it **references** them through the globals table (categories 2 and 3) or
inlines them at compile time (category 1).

The distinction matters when reasoning about lambda lifetime and mutation:

- A lambda that captures a local keeps that local alive for as long as the
  lambda is reachable.
- A lambda that references a top-level binding does not affect the
  binding's lifetime — top-level bindings live for the entire script run.

### Mixed references

A single lambda body may reference bindings from all four categories. No
special syntax is required; the compiler classifies each identifier by the
scope in which it was declared.

```grob
const SEP := ", "
readonly PREFIX := env.get("PREFIX") ?? "item"
log_count := 0

fn processItems(items: string[]): string {
    buffer := ""                   // local to processItems

    items.forEach(item => {
        buffer = buffer + PREFIX + "[" + item + "]" + SEP
        //               ^^^^^^                      ^^^
        //               global read                 inlined const
        //
        //       buffer: captured upvalue from processItems
        log_count = log_count + 1  // global write to top-level mutable
    })

    return buffer
}
```

### Interaction with `finally` and early exit

A `return`, `break` or `continue` inside a block-body lambda affects only
the lambda, regardless of where the lambda is defined (§27, D-275). A
top-level lambda has the same semantics as a lambda inside a function
body — `return` exits the lambda, never the enclosing top-level script.
`break` and `continue` are only meaningful inside a loop in the lambda's
own body; they cannot cross the lambda boundary into an outer loop.

### Scoping around top-level initialisation

During top-level initialisation (§19.1), a lambda assigned to a top-level
`readonly` or mutable binding is a value — the lambda's body does not yet
execute. Later calls to the lambda resolve top-level global references at
call time, using whatever value the binding holds at that moment. If a
lambda is called from a top-level initialiser and its body reads a
not-yet-initialised top-level binding, the circular-initialisation
diagnostic (§19.1) fires at that read.

---

## 13. `print()` Built-in

```grob
print("Hello, world")       // single value
print(42)                   // any type
print(a, b, c)              // variadic — values separated by single space
print()                     // no args — prints empty line
```

**Specification:**

- Accepts any type. Every value is rendered by the `ValueDisplay` service in the
  top-level (`Display`) position — see **Value Display** below (D-336). `nil`
  prints as the string `"nil"`.
- Variadic — any number of arguments. Multiple values separated by a single space.
- A newline is appended after the last value.
- Output goes to **stdout**.
- Returns `void` — used as a statement only.
- There is no `printError()`. `log.error()` is the stderr output mechanism.
  The `print()` / `log.*` boundary: results go to stdout via `print()`,
  operational messages go to stderr via `log.*`. These do not overlap.

**Nullable values.** `print()` accepts nullable types directly. Nil values
render as the string `"nil"`. This differs from string interpolation (§8),
which requires nil to be resolved explicitly before reaching the interpolation
slot. `print()` is a diagnostic output sink; interpolation is string
construction. The two sites have different rules because the intent behind
them differs.

---

### Value Display

> **Authority:** D-336. `print()`, string interpolation and (from Sprint 8)
> `formatAs` all render through one service, `ValueDisplay`, in `Grob.Runtime`.

**Dispatch precedence.** Rendering resolves in order. Step 2 is security-critical and must
never be reordered below step 5:

1. `nil` → `nil`
2. **Type has a registered `toString()` → call it. Terminal.**
3. Scalars (`int`, `float`, `bool`)
4. `string` → position-dependent (see below)
5. Composites (`Struct`, `Array`, `Map`) → structural, recursing via `Inspect`

Plugin types and user `type`s share the `Struct` discriminator, so a structural renderer
placed ahead of step 2 would print an `AuthHeader`'s credential (D-159, D-297). No type
carrying a registered `toString()` is ever structurally rendered.

Rendering depends on **position**, not only on type. A value at the top level of a
`print()` call is in the *display* position; a value nested inside a struct, array
or map is in the *inspect* position.

|Position |Entry point  |Strings          |Used by                                  |
|---------|-------------|-----------------|-----------------------------------------|
|Top-level|`Display(v)` |Unquoted         |`print(x)`, `"${x}"`                     |
|Nested   |`Inspect(v)` |Quoted, escaped  |Fields, array elements, map keys & values|

`print("hi")` emits `hi` — `string.toString()` is the identity (D-179).
`print(#{ host: "hi" })` emits `#{ host: "hi" }` — the quotes are what distinguish
the string `"8080"` from the int `8080`. `toString()` is the only public method;
`Inspect` is internal to `Grob.Runtime`.

**Rendering mirrors source syntax.** A printed value reads back as the literal that
would construct it:

```grob
Config { host: "example.com", port: 8080 }     // named struct type
#{ host: "example.com", port: 8080 }           // anonymous struct literal
[1, 2, 3]                                      // array
{ "a": 1, "b": 2 }                             // map
nil                                            // nil, in any position
```

**Registered `toString()` wins.** Types that define their own `toString()` are
rendered through it in both positions. `AuthHeader` renders `[AuthHeader]` and
never exposes the credential (D-159); `ProcessResult` renders its `stdout` (D-160).
Opacity in Grob is per-type and deliberate — it is never a default.

**Scalars.** `float` always renders round-trippable and always carries a decimal point or
exponent — `print(1.0)` emits `1.0`, never `1`, so `float` stays distinguishable from `int`.
`print(0.1 + 0.2)` emits `0.30000000000000004`. All numeric conversion uses
`InvariantCulture`; `NaN`, `Infinity` and `-Infinity` have pinned spellings. A
locale-sensitive conversion would emit `1,5` on a `de-DE` host and corrupt both gold masters
and `formatAs.csv` output.

**Function values.** A function value renders as its type: `fn(): int`, `fn(int): int`.
Never an identity or address — output must be deterministic for gold-mastered tests.
Closures expose no captures.

**Cycles and depth.** `E0301`/`E0302` reject type cycles with no terminating field,
so `type Node { value: int, next: Node? }` is legal and a runtime cycle is
constructible. `Inspect` tracks reference identity and renders a revisited object as
`<cycle>`; a depth cap renders `...` as a backstop. Element counts are **not**
capped — a script author printing an array wants the array.

**`print()` is inspection, not presentation.** `formatAs.table()`, `formatAs.list()`
and `formatAs.csv()` (D-282) are the presentation surface. The two coexist and do
not overlap.

## 14. Line Continuation

Newlines are significant in Grob — they end statements. A newline is **suppressed**
(the statement continues on the next line) in two cases:

**Case 1 — Trailing token:** The current line ends with any of:

| Token type                  | Examples                                                           |
| --------------------------- | ------------------------------------------------------------------ |
| Binary operators            | `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `&&`, ` |
| Assignment operators        | `=`, `:=`, `+=`, `-=`, `*=`, `/=`, `%=`                            |
| Comma                       | `,`                                                                |
| Open bracket or parenthesis | `(`, `[`, `{`                                                      |
| Member access dot           | `.`                                                                |
| Lambda arrow                | `=>`                                                               |

**Case 2 — Leading dot:** The _next_ line begins with `.`. The lexer peeks one
token across the newline to detect this.

Leading-dot style is the **recommended form** for multi-line method chains:

```grob
// Recommended — leading dot
result := files
    .filter(f => f.extension == ".log")
    .select(f => f.name)
    .sort()

// Also valid — trailing dot
result := files.
    filter(f => f.extension == ".log").
    select(f => f.name).
    sort()

// Multi-line expression — trailing operator
total := price
    + tax
    + shipping

// Multi-line function call — trailing comma
result := someFunction(
    arg1,
    arg2
)

// Multi-line array literal
items := [
    "alpha",
    "beta",
    "gamma"
]
```

Both cases are implemented entirely in the lexer. The parser receives a clean
token stream with newlines already resolved. No explicit continuation character.
No semicolons.

---

## 15. Numeric Types

| Type    | Representation         | Range                                                   |
| ------- | ---------------------- | ------------------------------------------------------- |
| `int`   | 64-bit signed integer  | −9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 |
| `float` | 64-bit IEEE 754 double | ±5.0 × 10⁻³²⁴ to ±1.7 × 10³⁰⁸                           |

**Integer overflow:** Arithmetic that exceeds the `int` range throws `ArithmeticError`
at runtime. The VM uses checked arithmetic — overflow never silently wraps. This
prevents a class of bugs where large file sizes, timestamps, or counters silently
produce wrong results.

**Float precision:** `float` is a 64-bit IEEE 754 double-precision value.
Floating point arithmetic follows IEEE 754 rules, with one deliberate
divergence: division by zero and modulo by zero throw `ArithmeticError` rather
than producing `±Infinity` or `NaN`. The same rule applies to `int` division
and modulo by zero. Non-finite values (`NaN`, `±Infinity`) can still enter
the arithmetic pipeline through `math` functions (e.g. `math.sqrt(-1.0)`) and
propagate per IEEE 754, but they are never produced by division or modulo
operators.

**Implicit promotion:** `int op float` promotes the `int` to `float` before the
operation. The result is `float`. No other implicit type conversions exist in Grob.

**No implicit coercion:** `bool` → `int` is not supported. `int` → `string` is not
supported — use `.toString()` or string interpolation. `string` → `int` is not
supported — use `.toInt()`. The only implicit conversion in the language is
`int` → `float` in mixed arithmetic.

---

## 16. Trailing Commas

Trailing commas are permitted in all comma-separated lists:

```grob
items := [1, 2, 3,]                 // array literal
r := Repo { name: "grob", url: "...", }  // struct construction
m := map<string, string>{
    "key": "value",
}                                    // map literal
fn foo(a: int, b: int,): int { }    // function parameters
foo(1, 2,)                           // function arguments
```

Trailing commas are optional — never required. They are permitted to simplify
code generation, reduce diff noise on version control, and align with modern
language conventions. `grob fmt` normalises trailing comma usage.

---

## 17. Forward References

Functions and types can reference other functions and types declared later in the
same file, and function bodies can read top-level value bindings declared later
in the same file. The type checker performs three passes (D-166, D-321, D-323):

1. **Registration pass** — walks all top-level declarations (`fn`, `type`, `param`,
   `import`) and top-level value bindings (`readonly`, `:=`) and registers their
   names in the symbol table. Value bindings are registered with a provisional
   `unknown` type placeholder.
2. **Value-binding type resolution pass** — resolves the static type of each
   top-level value binding from its initialiser, in initialiser-dependency order
   (see §17.2). After this pass, every top-level value binding's type is known
   before any function body is validated.
3. **Validation pass** — walks all function bodies and top-level code, resolving
   references against the fully populated and typed symbol table.

This means declaration order does not matter at the top level. A function can call
a function declared below it, and a type can reference a type declared below it.

**Supported forward-reference forms.** The three-pass model admits all of the
following at the top level:

- Function-to-function forward reference.
- Type-to-type forward reference (subject to cycle rules — see §17.1).
- Function signature referencing a type declared later — parameter type or
  return type.
- Generic type argument referencing a type declared later. A call like
  `csv.read(path).mapAs<User>()` resolves when `type User` is declared below
  the call site.
- Self-reference — direct recursion, a function calling itself.
- Mutual reference — indirect recursion, functions calling each other or
  types referencing each other via nullable fields.
- **Function body reading a top-level value binding declared later.** The
  value-binding type resolution pass (§17.2) resolves types before function
  bodies are validated, so `fn f(): int { return x }` followed by
  `readonly x := 5` type-checks correctly.

Inside function bodies, the standard rule applies: `:=`, `const` and `readonly`
declare in the current scope, and the name must be declared before use within
that scope. Forward references within a single function body are not supported.

---

## 17.1 Type Cycles

A type declaration cannot contain a cycle of required non-nullable fields that
would produce an infinitely-sized value. The type checker detects cycles during
the validation pass and reports a compile error.

**What participates in the cycle walk:** required fields whose type is a named
user-defined type (including self).

**What terminates a cycle (does not participate):**

- Nullable fields (`T?`) — `nil` terminates the chain.
- Array fields (`T[]`) — an empty array terminates the chain.
- Map fields (`map<K, V>`) — an empty map terminates the chain.

**Detection.** Standard depth-first walk with `Unvisited` / `Visiting` /
`Visited` states per type. A back-edge to a type currently on the DFS stack is
a cycle. The full path is reported in the diagnostic.

**Error message format:**

```
error[E0301]: type cycle with no terminating field

  type A {
    b: B
       ^ — required field of type B
  }
  type B {
    a: A
       ^ — required field of type A, completing the cycle A → B → A

  A value of type A would require a value of type B, which would
  require another value of type A, and so on without end.

  To break the cycle, make one of the fields nullable:
      b: B?        // nil terminates the chain
  Or use a collection type, which can be empty:
      b: B[]       // empty array terminates the chain
```

Multi-type cycles raise **E0301** (type cycle with no terminating field).
A trivial single-type self-reference (`type A { a: A }`) raises **E0302**
(recursive type without indirection). Both are registered in D-287.

Multi-type cycle diagnostics (`A → B → C → A`) follow the same format with each
participating field shown in declaration order and the back-edge highlighted.

Legitimate recursive patterns are preserved by the termination rules:

```grob
type Tree {
    value:    int
    children: Tree[]     // array — empty terminates the chain
}

type Node {
    value: int
    next:  Node?         // nullable — nil terminates the chain
}
```

---

## 17.2 Value-Binding Type Cycles

Top-level value bindings can reference one another in their initialisers, forming
a dependency graph on the type of each binding. The type checker walks this graph
in the value-binding type resolution pass (§17, pass 2) using the same
`Unvisited` / `Visiting` / `Visited` three-colour DFS as §17.1. A back-edge —
a binding that is currently being resolved being referenced again — signals a
cycle (D-323).

**Unannotated mutual cycle — compile-time error E0303.**

```grob
readonly a := b   // a's type depends on b's type
readonly b := a   // b's type depends on a's type — cycle
```

Neither type can be resolved without the other. The type checker emits **E0303**
("circular type dependency among top-level value bindings") and assigns the
`error` cascade type to both bindings. This is a compile-time error; the
programme does not reach the runtime.

**Annotated mutual cycle — runtime error E5902.**

```grob
readonly a: int := b   // a's type is int, declared explicitly
readonly b: int := a   // b's type is int, declared explicitly
```

When a binding has a type annotation, its type is resolved directly from the
annotation — no type-dependency edge runs from the binding to its initialiser's
identifiers. Both `a` and `b` are typed as `int` without a cycle in the type
graph, so the type checker accepts this programme. The cycle is structural, not
typed — it surfaces at runtime as **E5902** when `b`'s slot is still
`Uninitialised` at the point `a`'s initialiser executes (§19.1).

**Call expressions do not create value-type dependency edges.** A call
`readonly x := f()` where `f(): int` is declared later resolves `x` to `int`
via the function's declared return type — no edge to any other value binding.

---

## 18. Shadowing

A local variable may shadow a variable from an enclosing scope, including function
parameters and global variables. The compiler emits a **warning** (not an error)
when shadowing is detected.

```grob
name := "outer"
if (condition) {
    name := "inner"   // warning: 'name' shadows variable declared at line 1
    print(name)       // "inner"
}
print(name)           // "outer"
```

Rationale: preventing shadowing entirely is annoying in real scripts where short
variable names (`i`, `name`, `result`) are naturally reused. Allowing it silently
is a bug factory. A warning is the right balance — it signals the intent without
blocking valid code.

---

## 19. Script Structure and Declaration Order

A Grob script has the following canonical structure:

```grob
import Grob.Http                    // 1. Imports (if any)
import Grob.Crypto

@secure                             // 2. Params (if any)
param token: string
param days: int = 30

type Repo {                          // 3. Type declarations (if any)
    name: string
    url:  string
}

fn helper(r: Repo): string {        // 4. Function declarations (if any)
    return r.name
}

// 5. Top-level code (script body)
repos := loadRepos()
print(repos.length)
```

**Order rules:**

- `import` statements must appear before any other declarations or code.
- `param` blocks must appear before `type` declarations, function declarations
  and top-level code. `param` may appear after `import`.
- `type` and `fn` declarations may appear in any order relative to each other
  (forward references are resolved by the two-pass type checker).
- Top-level code (statements, expressions) appears after all declarations.
- Comments may appear anywhere.

An `import` after a `param` or `type` is a compile error. A `param` after a
`fn` or top-level statement is a compile error.

**Name uniqueness.** All binding-introducing forms (`fn`, `type`, `:=`,
`readonly`, `const`) at the top level share a single name space. Declaring
the same name twice — regardless of the two kinds involved — is a compile
error (E1102, D-324). The diagnostic fires at the second declaration.

---

## 19.1 Top-Level Initialisation Order

**Execution order.** `import`, `param` and `type` declarations are processed by
the three-pass type checker (§17), and every top-level `fn` binding is
**established (`Initialised`) before any top-level code executes** — the
compiler emits the function bindings in a prologue that runs ahead of the first
top-level statement (D-321). Functions are therefore runtime-available throughout
top-level initialisation, matching the forward-reference resolution the
three-pass type checker already grants (§17). After the prologue, top-level code
executes top-to-bottom in source order. This applies uniformly to every
top-level statement — mutable `:=` declarations, `readonly` declarations,
function calls used for side effects and control-flow statements.

Because functions are hoisted, **calling a function declared later in source is
always valid** — it is not a circular initialisation:

```grob
print(greet())                     // valid: greet is established before this runs
fn greet(): string { return "hi" }
```

`const` declarations do not participate in top-level execution. The type
checker resolves every `const` at compile time and the compiler inlines each
reference as a direct constant load; there is no runtime initialisation step
for `const` bindings.

### Circular initialisation

Top-level code can read any top-level binding. A function declared at the top
level can read top-level bindings from its body regardless of where the
bindings are declared relative to the function. This is a natural consequence
of the three-pass type checker (§17) — all top-level names are in scope from
inside any function body, the type checker's pass 1 registers every top-level
name (value bindings included), and phase 1.5 resolves each value binding's
type in dependency order before function bodies are validated (D-321, D-323).
A function body reading a later-declared top-level value sees the real type, not
a placeholder — for example, `fn f(): int { return x }` followed by
`readonly x := 5` resolves `x` to `int` and type-checks without error.

`E5902` applies **only to value-binding initialisation cycles** — a `readonly`
or mutable top-level binding whose initialiser, directly or through a function
it calls, reads a top-level value binding whose own declaration has not yet
run. Calling a function declared later in source is **not** an E5902 case:
functions are hoisted and established before any top-level code executes, so
the call always resolves (see _Execution order_ above).

At runtime, a value binding has a value only once its declaration statement has
executed. Reading a `readonly` or mutable top-level binding from a function
called during initialisation, before that binding's declaration has run, is
a **runtime error**.

```grob
readonly A := computeA()
readonly B := computeB()

fn computeA(): int { return B + 1 }    // reads B
fn computeB(): int { return A + 1 }    // reads A
```

When `A`'s declaration runs, `computeA()` is called, which reads `B`. `B`'s
declaration has not yet executed, so `B` has no value. The runtime detects
this and halts with a diagnostic.

This example is reachable precisely _because_ functions are hoisted: `computeA`
and `computeB` are established before `A`'s initialiser runs, so the call to
`computeA()` resolves and the cycle is exposed at `B`'s read — a value-binding
cycle, not a forward call. Were functions not hoisted, the call would fail
first and the genuine cycle would never be reached (D-321).

**Detection.** Each top-level binding slot carries a three-state tag —
`Uninitialised`, `Initialising`, `Initialised`. The declaration statement
transitions the slot from `Uninitialised` to `Initialising` before evaluating
the right-hand side, and from `Initialising` to `Initialised` after. Every
read of a top-level binding during startup checks the tag. A read of a slot
in `Uninitialised` or `Initialising` state halts with the circular-
initialisation diagnostic.

The check applies only during top-level execution. After the final top-level
statement completes, every top-level binding is in `Initialised` state and
subsequent reads from function bodies (invoked from top-level code that has
already finished, or from later function calls) skip the tag check. The cost
is a single branch per top-level binding read during startup and zero
afterwards.

**Applies to both `readonly` and mutable top-level bindings.** The rule is
the same for both. A mutable top-level binding whose initialiser calls a
function that reads a later-declared top-level binding produces the same
diagnostic.

**Error message format:**

```
error: circular initialisation detected.

  While initialising top-level binding 'A' at line 1,
  the function 'computeA' (line 4) read top-level binding 'B'
  at line 2, which has not yet been initialised.

      readonly A := computeA()
                    ^ — initialising 'A' here

      readonly B := computeB()
                    ^ — 'B' not yet initialised

      fn computeA(): int {
          return B + 1   // 'B' read while still uninitialised
                 ^
      }

  Top-level bindings are initialised in source order. Reading a
  top-level binding before its declaration has executed is an error.

  To fix: reorder the declarations so 'B' appears before 'A',
  or move the computation inside a function that runs after
  top-level initialisation completes.
```

This is a runtime error, reported through the exception machinery (§27). The
exception type is `RuntimeError`; the script halts. No user-level `try`/`catch`
can recover — the script cannot complete startup.

**Why runtime for annotated cycles; compile-time for unannotated cycles.**

An unannotated mutual value cycle — `readonly a := b` / `readonly b := a` — is
caught at compile time as **E0303** by the type-resolution pass (§17.2), because
neither binding's type can be inferred without the other.

The runtime E5902 applies to annotated cycles — `readonly a: int := b` /
`readonly b: int := a` — where each binding's type is declared explicitly and
the type checker is satisfied, but the values still depend on each other at
runtime. Detecting this at compile time would require inter-procedural analysis
— tracing which functions a top-level initialiser transitively calls and which
top-level bindings those functions read. The type checker does not otherwise
perform inter-procedural analysis, and adding it for this case would be
disproportionate. The runtime check is cheap, produces a precise diagnostic,
and preserves the simpler type-checker design.

---

## 20. Equality Semantics

**Primitives:** `==` on `int`, `float`, `bool`, `string`, `guid` compares by
value. `42 == 42` is `true`. `"hello" == "hello"` is `true`.

**User-defined types (named structs):** `==` performs field-by-field value
equality. Two struct values are equal if they are the same type and all fields
are equal (recursively). This matches the behaviour a scripting language user
expects — two `Repo` values with the same fields are the same repo.

**Anonymous structs (`#{ }`):** `==` performs field-by-field value equality.
Two anonymous structs are equal if they have the same field names and all field
values are equal. Field order does not matter.

**Arrays:** `==` performs element-wise comparison. `[1, 2, 3] == [1, 2, 3]` is
`true`. Arrays of different lengths are never equal. Element comparison is
recursive — arrays of structs compare field-by-field per element.

**Maps:** `==` performs entry-wise comparison. Two maps are equal if they have
the same keys and all corresponding values are equal. Insertion order does not
affect equality.

**Nil:** `nil == nil` is `true`. `x == nil` where `x` is non-nil is `false`.

**Type mismatch:** `==` between incompatible types (e.g. `int == string`) is a
compile error, not a runtime `false`.

---

## 21. Optional Chaining and Nil Propagation

`?.` short-circuits the entire chain when the receiver is `nil`.

```grob
name := user?.address?.city    // nil if user is nil OR address is nil
```

When any step in the chain evaluates to `nil`, the entire chain immediately
evaluates to `nil` — no further member access or method calls are attempted.
The result type of the chain is always nullable (`T?`).

`?.` may appear multiple times in a single chain. Each `?.` is an independent
nil check. The chain short-circuits at the first `nil` encountered.

---

## 22. Script-Level `return`

`return` is only valid inside a function body. Using `return` at the top level
of a script (outside any function) is a compile error:

```
error: 'return' is not valid at script level.
       Use 'exit()' to terminate a script early.
```

---

## 23. Explicit Non-Features

The following are explicitly not part of the Grob language:

- **Multiple return values** — functions return a single value. Use a struct to
  return multiple values: `fn foo(): Result { }`.
- **Tuples** — not in v1. Structs serve the same purpose with named fields. Tuples
  are an additive grammar extension post-MVP if friction is observed.
- **Out parameters** — not in v1, not planned. The nullable return pattern covers
  the try-parse use case: `"42".toInt() → int?` with `??` for defaults.
- **Operator overloading** — user-defined types cannot define custom `+`, `==`,
  or other operators. Comparison uses field-by-field equality (§20).
- **Circular imports** — `import` is for plugins only. Grob scripts do not export
  types to other scripts in v1. One script cannot import another script.
- **Implicit type coercion** — no `bool` → `int`, no `int` → `string`, no
  `string` → `int`. The only implicit conversion is `int` → `float` in mixed
  arithmetic (§15).

---

## 24. `const` and `readonly` Semantics

Grob has two keywords for bindings that are assigned once and never change. They
differ in **when** the right-hand side is evaluated.

### `const` — compile-time constant

`const` declares a compile-time constant. The right-hand side must be a
compile-time constant expression (defined below). The type checker evaluates it
at compile time; the compiler stores the result in the constant pool and inlines
every reference to the identifier as a direct constant load.

```grob
const MAX_RETRIES := 3
const TAX_RATE    := 0.2
const APP_NAME    := "grob"
const TAU         := math.pi * 2          // stdlib constants allowed
const GREETING    := "Hello, " + APP_NAME // concatenation of const strings
```

Allowed on the right-hand side of a `const` declaration:

- **Literals** of `int`, `float`, `string`, `bool`, `nil`. All literal forms from
  §8 are admitted — decimal, hex, binary and underscore-separated integers;
  floats; raw backtick strings; double-quoted strings **without** `${...}`
  interpolation; `true`, `false`, `nil`.
- **Binary arithmetic, comparison and logical operators** applied to
  compile-time constant operands: `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`,
  `<=`, `>`, `>=`, `&&`, `||`.
- **Unary operators** on compile-time constant operands: `-`, `!`.
- **String concatenation** via `+` on two compile-time constant strings (a
  consequence of the arithmetic rule; named explicitly because users will ask).
- **References to other `const`-bound identifiers** declared earlier in the
  file.
- **References to named stdlib constants** from the whitelist below.

Not allowed on the right-hand side of a `const` declaration:

- Function calls of any kind, including stdlib calls such as `math.min(1, 2)`.
  Even calls that could in principle be evaluated at compile time are rejected;
  the rule is mechanical, not based on purity analysis.
- Struct construction, array literals, map literals, anonymous struct literals.
- Any call into `env.*`, `date.*`, `fs.*`, `process.*`, `http.*`, or any plugin.
- Interpolated strings — a string containing any `${...}` is not a compile-time
  constant, even when every interpolated expression is itself `const`. Use `+`
  to compose: `const G := "Hi " + NAME`.
- Lambdas, optional chaining (`?.`), nil coalescing (`??`), ternary (`? :`).
- References to `readonly` or mutable identifiers.

**Stdlib constant whitelist (v1):**

| Namespace | Constants                                                                                                 |
| --------- | --------------------------------------------------------------------------------------------------------- |
| `math`    | `math.pi`, `math.e`, `math.tau`                                                                           |
| `path`    | `path.separator`, `path.altSeparator`, `path.pathSeparator`, `path.lineEnding`                            |
| `guid`    | `guid.empty`, `guid.namespaces.dns`, `guid.namespaces.url`, `guid.namespaces.oid`, `guid.namespaces.x500` |

A stdlib symbol qualifies for this list if and only if it is a named primitive
value with no runtime cost to resolve. Functions never qualify.

**Floating-point determinism.** Compile-time evaluation of float expressions
uses the host .NET runtime's IEEE 754 semantics. Values are stable across
identical .NET versions.

A violation reports the offending form and suggests `readonly`:

```
error: right-hand side of 'const' is not a compile-time constant expression.
       Found: call to 'env.require'
       'const' requires a literal, an expression on literals, or a reference
       to another 'const' binding. Use 'readonly' instead for runtime values.
```

### `readonly` — runtime-once binding

`readonly` declares a runtime binding that is evaluated at the point of
declaration, assigned once, and never reassigned or mutated afterwards. The
right-hand side may be any valid Grob expression.

```grob
readonly TOKEN  := env.require("ADO_PAT")
readonly CUTOFF := date.today().addDays(-30)
readonly CONFIG := fs.readText("config.json")
readonly ITEMS  := [1, 2, 3]
```

Semantics:

- The right-hand side is evaluated at the point of declaration. At the top
  level, `readonly` bindings are evaluated in source order (see §19.1). Inside
  a function body, evaluation happens at the point of execution.
- The binding cannot be reassigned: `TOKEN = "other"` is a compile error.
- The value cannot be mutated. For containers and structs, any operation that
  would mutate the bound value is a compile error — `ITEMS.append(4)`,
  `ITEMS[0] = 99`, `CONFIG["port"] = "8080"`, `point.x = 5` (where `point` is
  `readonly`), `++counter` on a `readonly int`, `counter += 1`.
- The binding must be initialised at declaration. No deferred initialisation
  syntax exists, consistent with §9 (no uninitialised variables).

**`param` bindings are implicitly `readonly`.** A parameter declared in a
`param` block has the same immutability guarantees — it cannot be reassigned
and cannot be mutated. The `readonly` keyword is not written on `param`
declarations; it would be redundant. An attempt to reassign a `param` produces:

```
error: cannot reassign 'param' 'token' — parameters are implicitly readonly.
```

### Cross-references between `const` and `readonly`

- A `readonly` binding may reference any `const` on its right-hand side.
  `const` values are already resolved by the time any `readonly` is evaluated.
- A `const` binding **may not** reference a `readonly` identifier on its
  right-hand side — the `readonly` value does not exist at compile time:

```
error: 'const' binding cannot reference runtime value 'CUTOFF'
       (declared as 'readonly'). Either make 'CUTOFF' a 'const', or
       make this binding 'readonly'.
```

### Scope

Both `const` and `readonly` may appear at the top level of a script and inside
any function body or nested block. Scoping rules are identical to mutable `:=`:
a binding lives until the end of its enclosing block.

```grob
fn computeTotal(items: Item[]): float {
    const TAX_RATE := 0.2                 // compile-time constant
    readonly discount := lookupDiscount() // runtime-once

    total := 0.0
    for item in items {
        total = total + item.price
    }
    return total * (1.0 + TAX_RATE) * discount
}
```

Local `const` gives magic-number naming with zero runtime overhead — every
reference is inlined by the compiler. Local `readonly` gives an immutability
guarantee for a value computed once at a known point.

### Mutable bindings are unchanged

Mutable bindings declared with `:=` permit both reassignment and mutation:

```grob
items := [1, 2, 3]
items.append(4)          // valid — mutable binding, mutable content
items = [5, 6, 7]        // valid — mutable binding
```

The deeper question of mutable-binding-with-immutable-content (e.g. a
non-`const`, non-`readonly` binding to a frozen array) is deferred post-MVP. In
v1 the three-way distinction is sufficient:

| Form                | Compile-time? | Rebind? | Mutate? |
| ------------------- | ------------- | ------- | ------- |
| `const X := ...`    | yes           | no      | no      |
| `readonly X := ...` | no            | no      | no      |
| `X := ...`          | no            | yes     | yes     |

---

## 25. Try-Parse Pattern

Grob uses nullable return types for fallible conversions — not tuples, not out
parameters, not exceptions.

```grob
// String to int — nil if not parseable
count := "42".toInt()           // int? → 42
bad   := "hello".toInt()        // int? → nil

// With nil coalescing for defaults
port := input("Port: ").toInt() ?? 8080

// GUID parsing
id := guid.tryParse(raw_input)  // guid? → nil if invalid
if (id != nil) {
    log.info("Valid GUID: ${id}")
}
```

The pattern is consistent across all fallible conversions:

| Method                 | Returns  | On failure |
| ---------------------- | -------- | ---------- |
| `string.toInt()`       | `int?`   | `nil`      |
| `string.toFloat()`     | `float?` | `nil`      |
| `guid.tryParse(value)` | `guid?`  | `nil`      |

The strict variants throw instead of returning nil:

| Method              | Returns | On failure   |
| ------------------- | ------- | ------------ |
| `guid.parse(value)` | `guid`  | `ParseError` |

This two-tier pattern (nil-returning `try` variant + throwing strict variant)
is the convention for all type-boundary operations.

---

## 26. Nested Arrays

Arrays of arrays are valid. `T[][]` is the type of a two-dimensional array.

```grob
matrix := [
    [1, 0, 0],
    [0, 1, 0],
    [0, 0, 1]
]
// matrix: int[][]
// matrix[1][2] → 0

// Three-dimensional
cube: int[][][] := [[[1, 2], [3, 4]], [[5, 6], [7, 8]]]
```

There is no dimension enforcement — `matrix[0].length` need not equal
`matrix[1].length`. This is arrays-of-arrays, not a matrix type. Sufficient
for JSON deserialisation of nested arrays and simple grid patterns.

---

## 27. Exception Handling

Grob uses exceptions as its runtime error model (D-082). Failing stdlib calls
throw; user scripts can throw with the `throw` keyword; `try`/`catch` provides
structured recovery; `finally` provides unconditional cleanup. Unhandled
exceptions propagate to the VM top level, produce a Grob-quality diagnostic,
and exit with code 1.

The exception type hierarchy is defined by `Grob.Runtime` (D-084). In v1 all
exception types are built-in — user-defined exception types are post-MVP
(D-085).

### The v1 exception hierarchy

`GrobError` is the root. Ten typed leaves cover the distinct runtime failure
domains. The hierarchy is flat — no intermediate abstract classes.

```
GrobError (root)
├── IoError           file system, permissions, stream failures
├── NetworkError      HTTP, DNS, connection, timeout at the network layer
├── JsonError         json.parse failure, json type coercion mismatch
├── ProcessError      process timeout expiry, non-zero exit under *OrFail variants
├── NilError          dereferencing nil without ?. or ??
├── ArithmeticError   int overflow, div/0, mod/0, math domain violations
├── IndexError        array bounds, substring bounds
├── ParseError        guid.parse failure, future int.parse / date.parse / etc.
├── LookupError       env.require on a missing key
└── RuntimeError      VM-level resource failures (stack overflow); residual
```

**Domain-splitting rationale.** Each leaf has a natural-language domain name
that a script author reaches for when writing a catch clause. The bar for a
new leaf is "does this have a natural domain name?", not "is this a
different kind of broken?". `RuntimeError` is the residual — reserved for
VM-level resource failures that do not fit any of the named domains.

**Canonical throw sites in v1:**

| Leaf              | Throw sites                                                                                                                        |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `IoError`         | `fs.*` I/O failure, permissions, `fs.createDir` on existing path                                                                   |
| `NetworkError`    | `http.*` transport failure, DNS, connection reset, `http.download` non-2xx                                                         |
| `JsonError`       | `json.parse` on malformed input, accessor type mismatch, `mapAs<T>` shape                                                          |
| `ProcessError`    | `process.run` timeout, `*OrFail` on non-zero exit                                                                                  |
| `NilError`        | Dereference of `nil` without `?.` or `??`                                                                                          |
| `ArithmeticError` | Integer overflow (§15), int div/0, int mod/0, float div/0, float mod/0, `math.sqrt(negative)`, `math.log(0)`, `math.log(negative)` |
| `IndexError`      | Array index out of range; substring / slice bounds out of range                                                                    |
| `ParseError`      | `guid.parse` on malformed input; future explicit parse operations                                                                  |
| `LookupError`     | `env.require` on a missing or empty variable                                                                                       |
| `RuntimeError`    | Call-stack depth exceeded (stack overflow); residual                                                                               |

`map<K, V>` key-not-found is **not** an `IndexError` — map lookup returns
`V?` per the nullable design, not a throw. `int.parse()`, `float.parse()`,
and `date.parse()` are not in v1 but are reserved to throw `ParseError`
when added.

### `try` / `catch`

```grob
try {
    // protected block
}
catch (e: IoError) {
    // handles IoError only
}
catch (e: NetworkError) {
    // handles NetworkError only
}
catch e {
    // catch-all — handles any GrobError not matched above
    // e is typed as GrobError here
}
```

**Grammar:**

- `try { }` opens a protected region. At least one `catch` or a `finally`
  must follow.
- Typed catches use `catch (<name>: <Type>) { <block> }`. The type must be
  `GrobError` or a subtype.
- The catch-all uses `catch <name> { <block> }` — identifier only, no parens
  and no colon. It is optional. If present, it must appear after all typed
  catches.
- `catch (e) { }` (parens with no type) is a syntax error.
- A catch block after a catch-all is a compile error (D-083).
- At most one catch-all per `try`.
- Duplicate typed catches for the same type are a compile error.
- `catch (e: GrobError) { }` is legal — it is a typed catch that happens to
  catch everything — but the bare `catch e { }` form is stylistically
  preferred for the catch-all.

**Matching semantics.** Typed catches match polymorphically: `catch (e: T)`
matches any thrown value whose type is `T` or a subtype of `T`. Catches are
tried in source order; the first match wins. In v1 the hierarchy is shallow
(a single root `GrobError` with flat leaves), so polymorphic matching has the
same observable behaviour as exact matching in every legal program.
Polymorphism is specified now so user-defined exceptions (post-MVP) slot in
without grammar change.

**The binding.** Inside the catch block, the binding (`e`) is in scope with
the declared type (or `GrobError` for the catch-all). It is immutable — it
cannot be reassigned.

**Permissiveness.** The type checker does not compute the "can-throw set" of
expressions in the try block. `catch (e: T)` is accepted even if nothing in
the try block can actually throw `T`. This is the C# model. An "unreachable
catch" linter warning is post-MVP.

### `throw`

```grob
throw IoError { message: "File not found: ${path}" }
throw NetworkError { message: "Timeout", statusCode: 504 }
```

`throw <expression>` — the expression must evaluate to a subtype of
`GrobError`. Throwing any other type is a compile error. Exceptions are
constructed using standard struct construction syntax (§10 and D-043).

`throw` is a statement, not an expression (§28).

### `finally`

```grob
handle := fs.open(path)
try {
    // work with handle — may throw
}
finally {
    handle.close()
}
```

A `finally` block runs on every exit from the try region, whether normal or
exceptional. It is the correct tool for releasing resources — file handles,
network connections, locks, subprocess handles — where cleanup must happen
regardless of success or failure.

**Grammar:**

- `finally` is optional. If present, it appears exactly once and must be the
  last clause of the try.
- A try with only a finally (no catches) is legal: `try { } finally { }`.
- A try with neither catch nor finally is a parse error.

**When it runs.** The `finally` block runs on:

- Normal completion of the try block.
- An uncaught exception propagating past the try (the finally runs, then the
  exception continues propagating).
- A caught exception after the catch handler completes normally.
- A caught exception after the catch handler throws (original or rethrown).
- Early `return` from inside the try block (the finally runs before the
  function returns).
- Early `break` or `continue` from inside the try block (the finally runs
  before control transfers).

The `finally` block does **not** run on `exit()` (§ uncatchable exit below).
`exit()` terminates the process unconditionally without running finally
blocks — consistent with C# `Environment.Exit` and Java `System.exit`.

**Exception inside `finally`.** If a `finally` block itself throws, the new
exception replaces any in-flight exception from the try or catch. The
original exception is lost. Exception chaining (Python-style `cause` /
`__context__`) is not part of v1. Scripts that must preserve the original
exception should wrap the cleanup work in its own try/catch:

```grob
try {
    doWork()
}
finally {
    try {
        cleanup()
    }
    catch e {
        log.warning("Cleanup failed: ${e.message}")
    }
}
```

**Control flow inside `finally`.** `return`, `break`, and `continue` are
**not** permitted inside a `finally` block — compile error. The
"finally overrides return" behaviour permitted by C#, Java, and Python is
deliberately disallowed. It is a reliable source of surprising bugs and has
no legitimate use case — any apparent use is better expressed by
restructuring the code. `throw` is permitted inside `finally`.

The ban applies to control flow that would exit the enclosing function or
loop. A `return` inside a block-body lambda that itself appears inside
`finally` exits only the lambda and is permitted — the lambda is a function
body in its own right (§12).

### Uncatchable exit

`exit(n)` (D-110) throws an internal `ExitSignal` that `try`/`catch` cannot
catch. It unwinds the entire call stack, is caught only at the VM top level,
flushes output buffers, and terminates the process with the specified code.
There is no way to suppress or recover from `exit()`. `finally` blocks do
not run on the `exit()` path.

---

## 28. Statement Forms

The following are statements, not expressions. They do not produce a value
and cannot appear in expression position. Using any of them inside an
expression is a parse-time error. None of them appear in the operator
precedence table (§7).

### Declaration — `:=`

```grob
name := expression
```

Declares `name` in the current scope and assigns the value of `expression`.
Valid only on first use of `name` in the current scope. Reusing `:=` for a
name that already exists in the current scope is a compile error — use `=`
instead.

**Uniform top-level redeclaration rule (D-324).** All binding-introducing
forms at the top level — `:=`, `readonly`, `const`, `fn`, `type` — share
the same collision rule: the second (or later) declaration of a name that is
already bound is a compile error (E1102), regardless of the kinds involved.
The diagnostic is emitted at the offending later declaration. Within a
function body only `:=` introduces local bindings; the same collision rule
applies there.

### Assignment — `=` and compound

| Operator | Operation           |
| -------- | ------------------- |
| `=`      | Reassign            |
| `+=`     | Add and assign      |
| `-=`     | Subtract and assign |
| `*=`     | Multiply and assign |
| `/=`     | Divide and assign   |
| `%=`     | Modulo and assign   |

The left-hand side must be an assignable target — a local or global name,
a struct field access (`obj.field`), or an array index (`arr[i]`). Other
expressions on the left are a compile error.

The name must already exist (declared earlier with `:=`, as a function
parameter, or as a `param` block entry). Assigning to an undeclared name is
a compile error.

Compound assignment is compile-time sugar: `x += y` lowers to `x = x + y`.
The type rules of the underlying binary operator apply.

### Increment and decrement — `++`, `--`

| Operator | Operation          |
| -------- | ------------------ |
| `i++`    | Increment `i` by 1 |
| `i--`    | Decrement `i` by 1 |

- Postfix form only — prefix (`++i`, `--i`) is not valid.
- Applies to `int` only. `float++` and `float--` are compile errors.
- `++` and `--` on a `const` binding is a compile error.
- The compiler lowers `i++` to `i = i + 1`.

### `throw` — see §27

`throw <expression>` is a statement. The expression must evaluate to a
subtype of `GrobError`. See §27 for full semantics. `throw` is permitted
inside a `finally` block; `return`, `break`, and `continue` are not.

### Why these are statements, not expressions

Grob disallows assignment-in-expression-position deliberately. `if (x = 5)`
is a parse error, not a subtle bug. Comparison uses `==`; assignment uses
`=`; they are never confused because they cannot occupy the same syntactic
position. This is a divergence from C and a deliberate alignment with
languages that prioritise script correctness over expression compactness.

---

## 29. Parser Error Recovery

The parser is error-recovering and stateless. When it encounters input it
cannot parse, it emits a diagnostic, builds a placeholder node where a
well-formed node was expected, advances to the next recovery anchor, and
resumes parsing. A single malformed construct does not abort the parse.

This is a day-one requirement, not a polish pass. Sprint 1 produces a
parser that already implements this contract. Retrofitting error recovery
later requires touching every parse method.

### 29.1 Synchronisation set

When the parser fails to parse a construct, it skips tokens until it sees
a **recovery anchor**, then resumes at that anchor. The anchor set is
fixed by the spec. The parser does not adapt the set based on context —
the same anchors apply everywhere.

**Anchors.** A token is an anchor if any of the following holds:

- It is a **statement-boundary newline outside any open bracket**. The
  lexer tracks bracket nesting depth (`(`, `[`, `{` increase; `)`, `]`,
  `}` decrease); a newline at depth zero is a statement boundary.
- It is the **closing brace `}`** of an enclosing block. Recovery does
  not skip past the surrounding block — the `}` ends the recovery.
- It is the **start keyword of a top-level declaration**: `fn`, `type`,
  `param`, `import`, `const`, `readonly`.

The parser skips tokens that are neither anchors nor part of any nested
bracket structure. Brackets are tracked: a `(` inside the skipped region
must be matched by a `)` before the parser will treat any subsequent
newline as an anchor. This prevents recovery from terminating inside a
parenthesised expression that began before the error.

**End-of-file** terminates recovery unconditionally. The parser emits
any pending diagnostics and returns whatever AST it has built.

### 29.2 Error nodes in the AST

Where the parser expected a well-formed node and could not produce one,
it emits an **error node** of the appropriate kind:

- `ErrorExpr` — produced where an expression was expected. Carries the
  source range of the failed parse and the diagnostic message.
- `ErrorStmt` — produced where a statement was expected. Same fields.
- `ErrorDecl` — produced where a top-level declaration was expected.
  Same fields.

Error nodes are first-class AST citizens. Every visitor that traverses
the AST handles them — the type checker, the compiler, the formatter,
the LSP. This means the AST shape mirrors the source even when broken,
which is essential for editor tooling: go-to-definition, hover, and
completion all keep working on the surrounding well-formed code while a
broken construct sits in the middle.

The type checker assigns every error node the type `Error`. This type
is **assignable to and from every other type** — it produces no further
diagnostics on use. See §29.3.

The error node's source range covers the failed parse from the first
unexpected token to the recovery anchor (exclusive of the anchor). This
range drives editor squiggle rendering and LSP diagnostic positioning.

### 29.3 Cascade suppression

A single parse error must not produce a cascade of downstream diagnostics.
Cascade suppression is implemented via the `Error` type:

- An expression of type `Error` produces no further type-mismatch
  diagnostics regardless of context. `Error + int`, `Error.foo`,
  `f(Error)` are all silent.
- A statement that contains an error node still type-checks the parts
  that did parse cleanly. Errors in those parts are reported normally.
- A declaration that produced `ErrorDecl` is registered in the symbol
  table with a synthetic name and the type `Error`. References to that
  name resolve to the synthetic entry — they do not produce
  "undefined identifier" diagnostics.

The intent is one diagnostic per root cause. A parse error at line 12
does not produce thirty type errors at lines 14, 17, 23, etc. The
developer fixes the parse error, recompiles, and any genuine downstream
errors then surface. This matches the developer-on-save workflow that
the LSP integration depends on.

**Confinement.** The `Error` type's universal assignability exists solely
to suppress cascades from a parse failure — it is not a general escape
hatch. `Error` arises only on an error node or on a node that
transitively contains one. It is never inferred for, propagated into, or
assigned to a node whose subtree parsed cleanly. Concretely:

- A function whose body parsed cleanly has its declared return type, never
  `Error`, regardless of errors elsewhere in the file. A clean call site
  to it is checked normally.
- A clean expression assigned into a binding whose declared or inferred
  type is well-formed is checked against that type, not silently widened
  because some other statement produced an `Error`.
- `Error` does not survive across a declaration boundary as a binding's
  published type. A binding whose initialiser was an `ErrorExpr` is
  registered with the synthetic `Error` entry (so references stay quiet),
  but this entry is confined to that binding — it does not make unrelated
  expressions assignable to one another.

The rule prevents `Error` from behaving as a backdoor `any` that masks
genuine type bugs sitting structurally adjacent to a parse error. This
matches the way production compilers (for example Roslyn's error type)
keep their error sentinel local to the malformed region rather than
letting it leak into well-formed code. A genuine type error next to a
parse error is still reported, because the well-formed side of that
adjacency never acquires the `Error` type.

### 29.4 Diagnostic cap

There is **no per-file cap** on diagnostics. The parser reports every
error it finds, the type checker reports every error it finds, and the
build prints them all. This is consistent with the two-mode error
collection rule (`grob-v1-requirements.md` §10): the compiler and type
checker collect all errors before execution, and the developer should
see the full set.

A cap would interact poorly with the LSP integration — capping at, say,
one hundred would mean a developer working on a file with many errors
sees the same hundred until they fix the first ones, with no signal
that more exist. Unbounded reporting is the simpler and more honest
contract.

In pathological cases the parser may produce a long error stream. This
is acceptable — pathological cases are rare in practice, and an
overlong stream is a clearer signal than truncation that the input is
seriously malformed.

### 29.5 Statelessness

The parser carries **no state** across files or invocations beyond the
token stream and the AST it is building for the current parse. There is
no error-history-influenced recovery, no learned anchor weighting, no
cross-file diagnostic deduplication. Each file is parsed independently
from a clean slate.

Statelessness is a property the LSP relies on — every keystroke triggers
a re-parse of the affected file, and the parse must produce the same
diagnostics for the same input regardless of what was parsed before.

### 29.6 Worked example

```grob
fn add(a: int, b: int): int {
    return a +
}                       // <- parser hits `}` while expecting RHS

x := add(1, 2)          // <- this still parses cleanly
y := nonexistent + 5    // <- this surfaces a separate "undefined identifier"
```

Parser behaviour on this input:

1. Inside `return a +`, the parser expects an expression as the RHS of
   `+` and finds the closing `}` instead. It emits `error: expected
expression after '+'` and produces an `ErrorExpr` whose range covers
   from the `+` to the `}` (exclusive). The `}` is the recovery anchor.
2. The `add` function declaration completes. The function body is well-
   formed except for the `ErrorExpr` inside the `return`.
3. The type checker assigns `ErrorExpr` the type `Error`. The `return`
   statement's checked return type is `Error`, which is compatible with
   the declared `int`. No further diagnostic is emitted from the return.
4. The parse continues at the next top-level statement. `x := add(1, 2)`
   parses and type-checks cleanly. `y := nonexistent + 5` produces a
   separate `error: undefined identifier 'nonexistent'`.

Final diagnostic count: two. One for the malformed `return`, one for the
undefined identifier. The `return`'s type implications produce no
further errors. The clean code between the two genuine errors compiles
without noise.

---

_Document updated May 2026 — §29.3 gains a "Confinement" paragraph_
_specifying that the `Error` type's universal assignability is local to_
_error nodes and subtrees that transitively contain one; it is never_
_inferred for, propagated into, or assigned to a cleanly parsed node, and_
_does not survive a declaration boundary as a binding's published type._
_This closes the backdoor-`any` failure mode where a genuine type error_
_adjacent to a parse error could be silently absorbed. No decision_
_changed — this makes the existing cascade-suppression design (D-300)_
_precise on the boundary it was always intended to have._
_Document updated May 2026 — Sprint 1 acceptance follow-up: §29.6 worked_
_example corrected to `fn add(a: int, b: int): int {` — the original_
_omitted the return-type annotation that v1 grammar mandates, making the_
_example unparseable against the grammar it illustrates (D-307)._
_Previous: May 2026 — Session 3 spec gap fill: new §29 (Parser_
_Error Recovery) specifying the synchronisation set (statement-boundary_
_newlines outside open brackets, closing `}` of enclosing blocks, top-_
_level declaration keywords), error node shape (first-class `ErrorExpr`/_
_`ErrorStmt`/`ErrorDecl` with the `Error` type), cascade suppression_
_(the `Error` type is assignable to and from every other type),_
_unbounded diagnostic reporting, statelessness, and a worked example_
_(D-300). §3 `select` exhaustiveness rule expanded with a "Why `select`_
_is non-exhaustive" rationale paragraph naming the statement-vs-_
_expression intent split and the C#/F#/Scala/Rust/Kotlin precedent_
_(D-301)._
_Previous: April 2026 — Session B Part 4: three parked decisions_
_finalised under the `const`/`readonly` keyword model. New §19.1 (D-294 —_
_top-level initialisation order and circular-read detection via a_
_three-state slot tag, startup-only cost); new §10 field-default_
_evaluation subsection (D-295 — construction-time evaluation in_
_construction-site scope, any expression permitted, no cross-field_
_references); new §12.1 (D-296 — four-category closure variable_
_resolution: `const` inlined, `readonly` / mutable global reads,_
_enclosing locals captured as upvalues; "capture" reserved for the_
_upvalue case). Pre-session reconciliation folded in Session B Part 3_
_edits (§17 forward-reference list, new §17.1 Type Cycles) and Session B_
_Interlude edits (§9 `readonly` example, §24 rewrite as two subsections)_
_that had not previously landed in this document._
_Previous: Session C Part 2 pre-implementation review:_
_exception hierarchy expanded from six leaves to ten (§27 — `ArithmeticError`,_
_`IndexError`, `ParseError`, `LookupError` added as direct children of_
_`GrobError`; `RuntimeError` narrowed to residual VM-level resource failures);_
_§6, §15 updated to reference `ArithmeticError` on div/mod by zero and integer_
_overflow; §25 `guid.parse()` now throws `ParseError`; §8 Windows path callout_
_added (backtick raw strings as canonical idiom for literal-backslash content)._
_Previous: Session B Part 2 pre-implementation review:_
_block-body lambda semantics specified (§12 — implicit last expression with_
_`return` for early exit, no explicit return-type annotation in v1); switch_
_expression pattern grammar specified (§3.1 — value, relational, and catch-all_
_patterns; `nil` as value pattern on nullable scrutinees; exhaustiveness rules;_
_multi-value, range, and type patterns deferred post-MVP); integer division by_
_zero clarified (§6, §15 — throws `ArithmeticError`, matches integer modulo, float_
_division, and float modulo); nullable interpolation is a compile error (§8,_
_§13 — `?? <fallback>` or narrowing `if` required; `print()` asymmetry_
_preserved and made explicit); §27 clarification that `return` inside a_
_block-body lambda nested in `finally` is permitted (exits lambda, not_
_enclosing function)._
_Previous: Session B Part 1 — `print`, `exit`, `input` confirmed as built-in_
_functions (not keywords); operator precedence table reduced to 12 levels_
_(assignment moved to §28 Statement Forms, `??` moved tighter than ternary to_
_match C# / Kotlin / Swift / TypeScript); `float % float` specified with fmod_
_semantics, modulo-by-zero throws; `try`/`catch`/`throw`/`finally` grammar_
_specified (§27); Statement Forms consolidated (§28)._
_Previous: operator precedence table_
_expanded to 13 levels (aligned with v1 spec); scientific notation deferred to post-MVP;_
_numeric types (int/float precision, overflow, promotion) specified; trailing commas_
_permitted; forward references specified (two-pass type checker); shadowing allowed_
_with warning; script structure and declaration order specified; equality semantics_
_defined for all types; optional chaining nil propagation specified; script-level_
_return is a compile error; explicit non-features stated (tuples, out parameters added);_
_`const` semantics specified (binding AND content immutable); try-parse pattern_
_documented; nested arrays (`T[][]`) specified._
_Previous: OQ-007 resolved: `for...in` iterable types_
_special-cased as array, map<K, V>, and numeric range. Formal iterable protocol post-MVP._
_Document created April 2026 — language fundamentals design session._
_Authorised decisions recorded in grob-decisions-log.md._
_This document is the implementation reference — the decisions log is the authority._

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
> See `Grob___Tooling___Strategy.md`.

-----

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

-----

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

-----

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
- Exhaustiveness is **not** enforced on `select` statements. (The switch
  *expression* enforces exhaustiveness because a missing case means a missing
  value — `select` has no such constraint.)
- Works on any comparable type: `int`, `string`, `bool`.
- `break` does not apply inside `select` — no fall-through means it is never
  needed. To exit an enclosing loop from inside a `select`, restructure into a
  function and use `return`, or use a flag variable.
- `select` is always a **statement**. The switch expression (`value switch { }`)
  is always an **expression**. The two forms are syntactically unambiguous.

-----

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

-----

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

-----

## 6. Operators

### Arithmetic

|Operator|Operation                      |Notes                           |
|--------|-------------------------------|--------------------------------|
|`+`     |Addition / string concatenation|                                |
|`-`     |Subtraction                    |                                |
|`*`     |Multiplication                 |                                |
|`/`     |Division                       |See integer division rules below|
|`%`     |Modulo                         |                                |

**Integer division:** `int / int → int` (truncating). `float / float → float`.
`int / float` and `float / int` promote to `float`. No separate floor-division
operator — the operand types determine the result.

**String concatenation:** `+` on strings is valid. `string + int` is a compile
error — no implicit conversion. Use `.toString()` or string interpolation.

### Comparison

|Operator|Operation            |
|--------|---------------------|
|`==`    |Equal                |
|`!=`    |Not equal            |
|`<`     |Less than            |
|`>`     |Greater than         |
|`<=`    |Less than or equal   |
|`>=`    |Greater than or equal|

### Logical

|Operator|Operation  |Notes                                                         |
|--------|-----------|--------------------------------------------------------------|
|`&&`    |Logical AND|Short-circuit — right operand not evaluated if left is `false`|
|`                  ||`                                                             |
|`!`     |Logical NOT|                                                              |

### Assignment

|Operator|Operation                               |
|--------|----------------------------------------|
|`:=`    |Declare and assign (first use of a name)|
|`=`     |Reassign (name must already exist)      |
|`+=`    |Add and assign                          |
|`-=`    |Subtract and assign                     |
|`*=`    |Multiply and assign                     |
|`/=`    |Divide and assign                       |
|`%=`    |Modulo and assign                       |

All compound assignment operators are **statements** — they do not produce a value
and cannot appear in expression position.

### Increment and decrement

|Operator|Operation         |
|--------|------------------|
|`i++`   |Increment `i` by 1|
|`i--`   |Decrement `i` by 1|

- Postfix form only — prefix (`++i`, `--i`) is not valid.
- Both are **statements** — they do not produce a value and cannot appear in
  expression position. The compiler lowers `i++` to `i = i + 1`.
- Applies to `int` only. `float++` and `float--` are compile errors.
- `++` and `--` on a `const` binding is a compile error.

### Unary

|Operator|Operation          |
|--------|-------------------|
|`-`     |Arithmetic negation|
|`!`     |Logical NOT        |

### Other

|Operator|Operation                                                      |
|--------|---------------------------------------------------------------|
|`??`    |Nil coalescing — `a ?? b` returns `a` if non-nil, otherwise `b`|
|`?.`    |Optional chaining — `a?.foo` returns nil if `a` is nil         |
|`..`    |Range (numeric for loops only in v1)                           |

Bitwise operators are not in scope for v1.

-----

## 7. Operator Precedence

Highest to lowest. Parentheses override precedence at any level.

|Level       |Operators                              |Notes                                          |
|------------|---------------------------------------|-----------------------------------------------|
|1 (highest) |`()`, `[]`, `.`, `?.`                  |Postfix — call, index, member access, optional chain|
|2           |`-` (unary), `!`                       |Prefix                                         |
|3           |`*`, `/`, `%`                          |Multiplicative                                 |
|4           |`+`, `-`                               |Additive                                       |
|5           |`..`                                   |Range (numeric `for` loops only in v1)         |
|6           |`<`, `>`, `<=`, `>=`                   |Comparison                                     |
|7           |`==`, `!=`                             |Equality                                       |
|8           |`&&`                                   |Logical AND — short-circuit                    |
|9           |`\|\|`                                 |Logical OR — short-circuit                     |
|10          |`? :` (ternary)                        |Conditional expression                         |
|11          |`??`                                   |Nil coalescing                                 |
|12          |`:=`, `=`, `+=`, `-=`, `*=`, `/=`, `%=`|Assignment (statements, not expressions)       |
|13 (lowest) |`=>`                                   |Lambda arrow                                   |

This matches C-family precedence with the addition of `??` (C#) and `..` (range).
No surprises for C# or Go developers. The Pratt parser implements this as binding
powers — this table is the canonical reference for the implementation.

-----

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

```grob
query := ```
SELECT *
FROM users
WHERE active = 1
```                         // triple backtick — multiline verbatim block
```

**Double-quoted strings**

- Standard form for most string values.
- Support escape sequences: `\n`, `\r`, `\t`, `\\`, `\"`, `\$`.
- Support `${expr}` interpolation.
- Do not span lines — a newline before the closing `"` is a compile error.
- `\$` produces a literal `$` without triggering interpolation.
- `\r` produces a carriage return — needed for explicit `\r\n` Windows line endings.
- Any other `\x` sequence not in the set above is a compile error — no silent
  pass-through of unknown escapes.

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
- Content begins immediately after the opening `— the opening delimiter is typically followed by a newline, which becomes the first character of the string value. Indent the content to the left margin of the closing`
  if leading whitespace is not wanted in the value.

**Choosing between forms:**

|Intent                                        |Form             |
|----------------------------------------------|-----------------|
|Normal string value                           |`"double-quoted"`|
|String with interpolation                     |`"Hello ${name}"`|
|Windows path, regex pattern, short verbatim   |`single backtick`|
|SQL, multiline template, structured text block|`triple backtick`|

Single-quoted strings (`'value'`) are not valid in Grob.

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

-----

## 9. Type Annotations

```grob
x := 42                  // inferred — x is int
x: int := 42             // explicit annotation — always valid, never required here

const MAX := 100         // inferred, immutable
const MAX: int := 100    // explicit, immutable

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

-----

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

### The bare brace rule

`{ }` is **always a block**. Using bare braces as an object literal is a compile
error:

```
error: '{' begins a block, not a struct literal.
       Use '#{ field: value }' for an anonymous struct, or declare a named type.
```

|Syntax                     |Meaning                 |
|---------------------------|------------------------|
|`{ }`                      |Block — always          |
|`#{ field: value }`        |Anonymous struct literal|
|`TypeName { field: value }`|Named type construction |

Partial construction and `with`-style record mutation are deferred post-MVP.

-----

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

-----

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

-----

## 13. `print()` Built-in

```grob
print("Hello, world")       // single value
print(42)                   // any type
print(a, b, c)              // variadic — values separated by single space
print()                     // no args — prints empty line
```

**Specification:**

- Accepts any type. Value types (`int`, `float`, `bool`) are converted to their
  string representations. Reference types call `.toString()`. `nil` prints as
  the string `"nil"`.
- Variadic — any number of arguments. Multiple values separated by a single space.
- A newline is appended after the last value.
- Output goes to **stdout**.
- Returns `void` — used as a statement only.
- There is no `printError()`. `log.error()` is the stderr output mechanism.
  The `print()` / `log.*` boundary: results go to stdout via `print()`,
  operational messages go to stderr via `log.*`. These do not overlap.

-----

## 14. Line Continuation

Newlines are significant in Grob — they end statements. A newline is **suppressed**
(the statement continues on the next line) in two cases:

**Case 1 — Trailing token:** The current line ends with any of:

|Token type                 |Examples                                                          |
|---------------------------|------------------------------------------------------------------|
|Binary operators           |`+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `&&`, `|
|Assignment operators       |`=`, `:=`, `+=`, `-=`, `*=`, `/=`, `%=`                           |
|Comma                      |`,`                                                               |
|Open bracket or parenthesis|`(`, `[`, `{`                                                     |
|Member access dot          |`.`                                                               |
|Lambda arrow               |`=>`                                                              |

**Case 2 — Leading dot:** The *next* line begins with `.`. The lexer peeks one
token across the newline to detect this.

Leading-dot style is the **recommended form** for multi-line method chains:

```grob
// Recommended — leading dot
result := files
    .filter(f => f.extension == ".log")
    .map(f => f.name)
    .sort()

// Also valid — trailing dot
result := files.
    filter(f => f.extension == ".log").
    map(f => f.name).
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

-----

## 15. Numeric Types

|Type   |Representation        |Range                                      |
|-------|----------------------|-------------------------------------------|
|`int`  |64-bit signed integer |−9,223,372,036,854,775,808 to 9,223,372,036,854,775,807|
|`float`|64-bit IEEE 754 double|±5.0 × 10⁻³²⁴ to ±1.7 × 10³⁰⁸             |

**Integer overflow:** Arithmetic that exceeds the `int` range throws `RuntimeError`
at runtime. The VM uses checked arithmetic — overflow never silently wraps. This
prevents a class of bugs where large file sizes, timestamps, or counters silently
produce wrong results.

**Float precision:** `float` is a 64-bit IEEE 754 double-precision value. Floating
point arithmetic follows IEEE 754 rules. Division by zero produces `RuntimeError`,
not infinity.

**Implicit promotion:** `int op float` promotes the `int` to `float` before the
operation. The result is `float`. No other implicit type conversions exist in Grob.

**No implicit coercion:** `bool` → `int` is not supported. `int` → `string` is not
supported — use `.toString()` or string interpolation. `string` → `int` is not
supported — use `.toInt()`. The only implicit conversion in the language is
`int` → `float` in mixed arithmetic.

-----

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

-----

## 17. Forward References

Functions and types can reference other functions and types declared later in the
same file. The type checker performs two passes:

1. **Registration pass** — walks all top-level declarations (`fn`, `type`, `param`,
   `import`) and registers their names and signatures in the symbol table.
2. **Validation pass** — walks all function bodies and top-level code, resolving
   references against the fully populated symbol table.

This means declaration order does not matter at the top level. A function can call
a function declared below it, and a type can reference a type declared below it.

Inside function bodies, the standard rule applies: `:=` declares in the current
scope, and the name must be declared before use within that scope. Forward
references within a single function body are not supported.

-----

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

-----

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

-----

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

-----

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

-----

## 22. Script-Level `return`

`return` is only valid inside a function body. Using `return` at the top level
of a script (outside any function) is a compile error:

```
error: 'return' is not valid at script level.
       Use 'exit()' to terminate a script early.
```

-----

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

-----

## 24. `const` Semantics

`const` prevents both rebinding and mutation. One rule: `const` means immutable.

```grob
const items := [1, 2, 3]
items = [4, 5, 6]      // compile error — cannot rebind const
items.append(4)         // compile error — cannot mutate const-bound array
items[0] = 99           // compile error — cannot mutate const-bound array

const config := map<string, string>{ "host": "localhost" }
config["port"] = "8080" // compile error — cannot mutate const-bound map
config.set("port", "8080") // compile error — same
```

Non-const bindings allow both rebinding and mutation:

```grob
items := [1, 2, 3]
items.append(4)         // valid — mutable binding, mutable content
items = [5, 6, 7]       // valid — mutable binding
```

The deeper question of mutable-binding-with-immutable-content (e.g. a non-const
binding to a frozen array) is deferred post-MVP. In v1, the rule is simple:
`const` = fully immutable, non-`const` = fully mutable.

-----

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

|Method                     |Returns   |On failure|
|---------------------------|----------|----------|
|`string.toInt()`           |`int?`    |`nil`     |
|`string.toFloat()`         |`float?`  |`nil`     |
|`guid.tryParse(value)`     |`guid?`   |`nil`     |

The strict variants throw instead of returning nil:

|Method                     |Returns   |On failure    |
|---------------------------|----------|--------------|
|`guid.parse(value)`        |`guid`    |`RuntimeError`|

This two-tier pattern (nil-returning `try` variant + throwing strict variant)
is the convention for all type-boundary operations.

-----

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

-----

*Document updated April 2026 — pre-implementation review: operator precedence table*
*expanded to 13 levels (aligned with v1 spec); scientific notation deferred to post-MVP;*
*numeric types (int/float precision, overflow, promotion) specified; trailing commas*
*permitted; forward references specified (two-pass type checker); shadowing allowed*
*with warning; script structure and declaration order specified; equality semantics*
*defined for all types; optional chaining nil propagation specified; script-level*
*return is a compile error; explicit non-features stated (tuples, out parameters added);*
*`const` semantics specified (binding AND content immutable); try-parse pattern*
*documented; nested arrays (`T[][]`) specified.*
*Previous: OQ-007 resolved: `for...in` iterable types*
*special-cased as array, map<K, V>, and numeric range. Formal iterable protocol post-MVP.*
*Document created April 2026 — language fundamentals design session.*
*Authorised decisions recorded in Grob___Decisions___Context_Log.md.*
*This document is the implementation reference — the decisions log is the authority.*
---
name: 'Grob Language Source'
description: 'Syntax, idioms and conventions for writing .grob programs — samples, fixtures, integration tests.'
applyTo: '**/*.grob'
---

# Grob language source

These rules apply to `.grob` files — programs written *in* Grob (sample scripts,
integration-test fixtures, error examples). They do not apply to the C# that
implements Grob. When you write a `.grob` file you are writing the language as a user
would, so it must be idiomatic and must actually be valid against the v1 surface.

The canonical idiom reference is `docs/design/grob-sample-scripts.md` (the thirteen
release-gate scripts). Match its style. The full language spec is
`docs/design/grob-language-fundamentals.md`.

## Script structure

Order is fixed: `import` → `param` → `type` / `fn` declarations → top-level code.

```grob
param source_dir: string = `C:\Users\Chris\Downloads`
param dest_dir:   string = `C:\Archive`

readonly extensions := [".jpg", ".png", ".cr2"]

for file in fs.list(source_dir, recursive: true) {
    if (extensions.contains(file.extension)) {
        year := file.modified.year.toString()
        dest := path.join(dest_dir, year)
        fs.ensureDir(dest)
        file.moveTo(dest)
        print("Moved ${file.name} → ${dest}")
    }
}
```

No top-level `return`. No multiple return values. No operator overloading. No circular
imports.

## Bindings

- `name := value` declares and assigns on first use; type is inferred. This is the
  default. No `var` keyword.
- `name = value` reassigns an existing binding, walking the scope chain.
- `name: Type := value` adds an explicit type annotation.
- `const NAME := value` — compile-time constant. The right-hand side must itself be a
  compile-time constant. Inlined at every reference.
- `readonly name := value` — runtime-once binding. Any expression is legal on the
  right; it is evaluated at the declaration point. No rebind, no mutation (deep).
- `param` bindings are implicitly `readonly`.

Mutable by default otherwise. No uninitialised variables — every binding has a value.

## Strings — three forms

- **Double-quoted** with `${name}` interpolation and escapes (`\n \r \t \\ \" \$`).
  Interpolating a nullable is a compile error — coalesce or guard first.
- **Single backtick** raw inline — no interpolation, no newlines. **This is the
  canonical form for Windows paths**: `` `C:\Reports\2026` ``. Use it for every
  literal path. Do not write `"C:\\Reports"` with doubled backslashes.
- **Triple backtick** raw multiline.

## Nullability

`T?` marks a type nullable. Non-nullable types are guaranteed non-nil. `??` coalesces,
`?.` chains and short-circuits the whole chain on the first nil. Never assume a value
is non-nil unless its type says so.

## Control flow

- `if` / `else if` / `else`, `while`, `for ... in` (numeric range, array, or map).
- Numeric ranges: `0..10`, `0..100 step 5`. `..` is inclusive.
- **`select` / `case` / `default`** — statement form, first-match, no fall-through,
  **non-exhaustive** (no error if nothing matches and there is no `default`). Reach
  for it when running side-effecting branches. (D-301)
- **Switch expression** — produces a value, **exhaustive** (every case must be
  covered). Reach for it when you need a value out. (D-277, D-301)

## Functions and lambdas

- `fn name(a: int, b: string): bool { ... }` — typed parameters and explicit return
  type are required in v1.
- Lambda short form: `x => expr`. Block form: `(a, b) => { ... }` with an implicit
  last-expression result; use `return` for early exit.
- Closures capture by upvalue.

## Collections and pipelines

The collection API uses **`.select()`**, not `.map()` — `select` superseded `map`
entirely (the one-way principle: no two ways to do the same thing). Chain
fluently:

```grob
entries := fs.list(path, recursive: true)
    .filter(f => f.size > threshold)
    .select(f => FileEntry {
        name:    f.name
        size_mb: (f.size / 1024.0 / 1024.0).round(2)
    })
    .sort(e => e.size_mb, descending: true)

print(entries.formatAs.table())
```

Named arguments are idiomatic for optional or boolean parameters: `recursive: true`,
`descending: true`, `overwrite: true`. Prefer them over bare positional booleans —
`fs.copy(src, dst, overwrite: true)` reads, `fs.copy(src, dst, true)` does not.

## Types and structs

- User-defined types via `type Name { field: Type ... }`.
- Anonymous struct literals use `#{ ... }`.
- Named struct construction: `FileEntry { name: ..., size_mb: ... }`.
- `map<K, V>` is a first-class built-in.
- You **consume** generic functions in v1; you cannot **declare** generic functions or
  types yourself (D-080).

## Errors

- `try` / `catch` / `finally`. Typed catches; a bare `catch e` is the catch-all and
  must come last (D-274).
- `throw` only on subtypes of `GrobError`.
- The VM stops on the first runtime error.

## Built-ins and stdlib

- `print()`, `input()`, `exit()` are built-in functions (resolved at type-check time),
  not keywords. `print()` writes to stdout; errors and `log.*` go to stderr.
- Thirteen core modules are auto-available without import: `fs`, `strings`, `json`,
  `csv`, `env`, `process`, `date`, `math`, `log`, `regex`, `path`, `formatAs`, `guid`.
- The collection-to-string terminators are `formatAs.table()`, `formatAs.list()`,
  `formatAs.csv()`. Scalar formatting is instance methods on the value
  (`n.round(2)`, `d.format("MM-MMMM")`), not `formatAs`. The module is `formatAs`,
  never `format`. (D-282)

## Numerics

`int` is 64-bit signed; `float` is 64-bit IEEE 754. Arithmetic is checked — overflow
throws `ArithmeticError`. Only `int → float` is implicit; every other conversion is an
explicit method call (`.toFloat()`, `.toInt()`, `.toString()`).

## Style for `.grob` files in this repo

- Windows paths in backticks, always.
- British English in string literals and comments where it is prose.
- Four-space indentation (the formatter enforces this; write it that way to begin
  with). Same-line braces. No semicolons.
- A fixture should exercise exactly the feature its test targets — minimal, not a
  kitchen sink — unless it is one of the thirteen release-gate scripts, which are
  deliberately realistic.
- Error-example fixtures (`*_grob.txt` paired with `*_expected.txt`) must produce the
  exact gold-master diagnostic. Do not edit a `_grob.txt` without regenerating and
  reviewing its `_expected.txt`.

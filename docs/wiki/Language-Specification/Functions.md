# Functions

## Declaration

```grob
fn add(a: int, b: int): int {
    return a + b
}

fn greet(name: string): void {
    print("Hello ${name}")
}
```

Functions are declared with the `fn` keyword. Parameters are always explicitly
typed — no inference on parameters. Return type is declared after the parameter
list with a colon. In v1 explicit return types are required on all functions.
Return type inference is deferred post-MVP.

## Calling

```grob fragment
result := add(1, 2)
greet("Alice")
```

Function calls use parentheses. The return value can be assigned, used in an
expression, or discarded.

## Named Parameters

Parameters with default values may be specified by name at the call site.
Positional arguments come first (in declaration order), named arguments follow.

```grob
fn connect(host: string, port: int = 8080, timeout: int = 30): void {
    // ...
}

connect("localhost")                          // defaults for port and timeout
connect("localhost", timeout: 60)             // override timeout only
connect("localhost", port: 443, timeout: 60)  // override both
```

Named arguments are unordered relative to each other. Only parameters with
default values may be named — required parameters (no default) are
positional-only. Providing a named argument before a positional, naming a
required parameter, duplicate names, or unknown parameter names are all compile
errors.

Named parameters exist to selectively override defaults — not as an alternative
to positional syntax for required arguments.

## Lambdas

```grob fragment
// Single-expression
files.filter(f => f.extension == ".jpg")

// Multi-parameter
items.sort((a, b) => a.size > b.size)

// Block body
raw.split("\n")
    .filter(line => line.length > 0)
    .select(line => {
        parts := line.split("|")
        #{ branch: parts[0], date: parts[1] }
    })
```

`{ }` after a lambda arrow is always a block body. `#{ }` is always an anonymous
struct literal. The parser never has to guess.

## Closures

Lambdas capture variables from the enclosing scope.

```grob fragment blocked-by=R-29
cutoff := date.today().addDays(-30)
stale  := branches.filter(b => date.parse(b.lastCommit) < cutoff)
```

The upvalue mechanism follows the clox design: open upvalues reference the stack
slot of the captured variable while the enclosing function is active. When the
enclosing function returns, the value is copied to the heap. Each capturing
lambda becomes a `Closure` object at runtime — a `BytecodeFunction` plus its
upvalue array.

## Built-in Functions

Two functions are always available without a namespace:

| Function | Signature | Description |
|----------|-----------|-------------|
| `print()` | `(args...) → void` | Variadic, stdout, newline appended |
| `exit()` | `(code: int = 0) → void` | Terminate script with exit code |

`print()` accepts any type. Multiple values are separated by a single space.
`nil` prints as `"nil"`. Output goes to stdout.

`exit()` throws an uncatchable internal `ExitSignal` that unwinds the entire
call stack. It cannot be caught by `try/catch`. The VM catches it at the top
level, flushes output buffers and terminates with the specified code.

See also: [Expressions](Expressions.md), [Error Handling](Error-Handling.md)

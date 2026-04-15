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

The `fn` keyword declares a function. Parameters are always explicitly typed.
Return type annotation is required in v1 (return type inference is post-MVP).

## Named Parameters

```grob
fn connect(host: string, port: int = 8080, timeout: int = 30): void {
    // ...
}

connect("localhost")                    // uses defaults
connect("localhost", timeout: 60)       // only override timeout
```

Only specify parameters that differ from defaults. No options object, no builder
pattern.

## Lambdas

```grob
files.filter(f => f.extension == ".log")
items.sort((a, b) => a.size > b.size)

// Block-body lambda
raw.split("\n").map(line => {
    parts := line.split("|")
    #{ branch: parts[0], date: parts[1] }
})
```

`{ }` after a lambda arrow is always a block body. `#{ }` is always an anonymous
struct literal — no ambiguity.

## Closures

Lambdas capture variables from the enclosing scope. The upvalue mechanism follows
clox: while the enclosing function is active, the upvalue holds a reference to
the stack slot. When the enclosing function returns, the value is copied to the
heap.

```grob
cutoff := date.today().minusDays(30)
stale := branches.filter(b => date.parse(b.lastCommit) < cutoff)
```

## Forward References

Functions and types can reference each other regardless of declaration order. The
type checker performs two passes — registration then validation.

## Script Structure

```grob
import Grob.Http                    // 1. Imports
import Grob.Crypto

@secure                             // 2. Params
param token: string
param days: int = 30

type Repo {                          // 3. Type declarations
    name: string
    url:  string
}

fn helper(r: Repo): string {        // 4. Function declarations
    return r.name
}

// 5. Top-level code
repos := loadRepos()
print(repos.length)
```

`import` must appear before all other declarations. `param` before `type` and
`fn`. `type` and `fn` may appear in any order (forward references resolved by
two-pass type checker). Top-level code appears after all declarations.

## `print()`

```grob
print("Hello, world")       // single value
print(a, b, c)              // variadic — separated by space
print()                     // empty line
```

Variadic, outputs to stdout, appends newline. Returns `void`.

## `exit()`

```grob
exit()     // exit with code 0
exit(1)    // signal failure
```

Terminates the script immediately. Cannot be caught with `try/catch`.

See also: [Expressions](Expressions.md), [Error Handling](Error-Handling.md)

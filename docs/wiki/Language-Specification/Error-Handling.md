# Error Handling

## Two-Mode Error Strategy

**Compile time:** The compiler and type checker collect ALL errors before
execution — they never stop at the first error. A programme with type errors
never reaches the VM.

**Runtime:** The VM stops on the FIRST runtime error. Unhandled exceptions
propagate to the VM top level — a Grob-quality diagnostic is produced and the
script halts.

## `try/catch`

```grob
try {
    data := json.read("C:\\config.json")
} catch IoError e {
    log.error("File not found: ${e.message}")
} catch JsonError e {
    log.error("Invalid JSON: ${e.message}")
} catch e {
    log.error("Unexpected error: ${e.message}")
}
```

Multiple typed catch blocks are supported. Bare `catch e` is the catch-all and
must appear last. A catch block after a catch-all is a compile error.

## Exception Hierarchy

The exception type hierarchy is a `Grob.Runtime` concern:

| Type | Description |
|------|-------------|
| `GrobError` | Root type — all exceptions inherit from this |
| `IoError` | File system and I/O failures |
| `NetworkError` | HTTP and network failures |
| `JsonError` | JSON parse and shape mismatch errors |
| `ProcessError` | External process failures and timeouts |
| `NilError` | Nil dereference at runtime |
| `RuntimeError` | General runtime errors (overflow, index out of range) |

User-defined exception types are deferred post-MVP.

## Error Message Design

Error messages show variable names and types, never values. This prevents
accidental credential exposure in terminal output and logs. The `--verbose` flag
overrides this for debugging.

```
Type error on line 14:
  Expected  int
  Got       string

  The function add() requires two int arguments.
  'name' is a string. Did you mean to convert it first?

  Hint: name.toInt() returns int? — check for nil before passing it.
```

See also: [Functions](Functions.md), [Modules and Imports](Modules-and-Imports.md)

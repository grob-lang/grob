# Error Handling

## Two-Mode Strategy

Grob uses a two-mode error strategy:

**Compile time** — the compiler and type checker collect all errors before
execution. They never stop at the first error. A program with type errors never
reaches the VM.

**Runtime** — the VM stops on the first unhandled runtime error. A runtime error
caught by a `try`/`catch` resumes the script normally; an unhandled exception
produces a Grob-quality diagnostic and halts the script.

## Exceptions

Exceptions are the runtime error model. Functions throw on failure. Unhandled
exceptions propagate to the VM top level.

```grob
content := fs.readText("C:\\config.json")   // throws IoError if file not found
```

## `try / catch`

```grob
try {
    data := fs.readText("C:\\config.json")
    config := json.parse(data)
} catch IoError e {
    log.error("File not found: ${e.message}")
    exit(1)
} catch JsonError e {
    log.error("Invalid JSON: ${e.message}")
    exit(1)
} catch e {
    log.error("Unexpected error: ${e.message}")
    exit(1)
}
```

Multiple catch blocks are supported. Typed catches match specific exception
types. Bare `catch e` is the catch-all and must appear last. A catch block after
a catch-all is a compile error, not a warning.

## Exception Hierarchy

The exception hierarchy is a `Grob.Runtime` concern — part of the standard
library, not the language grammar.

| Type | Description |
|------|-------------|
| `GrobError` | Root of all Grob exceptions |
| `IoError` | File system and I/O failures |
| `NetworkError` | HTTP and network failures |
| `JsonError` | JSON parse failures |
| `ProcessError` | External command failures |
| `NilError` | Nil dereference at runtime |
| `RuntimeError` | General runtime failures |

User-defined exception types are deferred post-MVP.

## Error Messages

Error messages show variable names and types, never values. This prevents
accidental credential exposure in terminal output and logs. The `--verbose` flag
overrides this for debugging.

```
IoError on line 7:
  File not found.
  Path variable 'config_path' (string) could not be read.
```

## Exit Codes

| Condition | Exit code |
|-----------|-----------|
| Normal script completion | `0` |
| `exit()` with no argument | `0` |
| `exit(n)` | `n` |
| Unhandled `GrobError` | `1` |

See also: [Functions](Functions.md), [Expressions](Expressions.md),
[Error Messages](../CLI/Error-Messages.md)

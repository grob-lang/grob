# CLI Commands

## Usage

```
grob — a statically typed scripting language

Usage:
  grob run <script>          Run a Grob script
  grob repl                  Start an interactive session
  grob check <script>        Type-check without running
  grob fmt <script>          Format a Grob script
  grob new <script>          Create a new script with boilerplate
  grob install <plugin>      Install a plugin from NuGet
  grob uninstall <plugin>    Remove an installed plugin
  grob restore               Install all grob.json dependencies
  grob list                  List installed plugins
  grob search <query>        Search for available plugins
  grob update <package>      Update a plugin or the runtime
  grob init                  Create a grob.json manifest

Options:
  --explain <code>           Show long-form documentation for an error code (e.g. --explain E0101)
  --dev-plugin <path>        Load a plugin (.dll) for development
  --params <file>            Load parameters from a .grobparams file
  --verbose                  Show compilation details and debug log output
  --version                  Print version and exit

Examples:
  grob run deploy.grob
  grob run stale-branches.grob --params stale.grobparams
  grob run report.grob --dev-plugin Grob.Http.dll
  grob --explain E0101
```

## `grob run`

Run a Grob script. Compiles in memory and executes immediately.

```
grob run deploy.grob
grob run deploy.grob --environment staging
grob run deploy.grob --params deploy.grobparams
grob run deploy.grob --params deploy.grobparams --environment production
grob run report.grob --dev-plugin C:\dev\Grob.Http.dll
grob run report.grob --verbose
```

Exit codes: `0` for a clean run, `1` for a runtime error, `2` for a type or
syntax error, `3` for a usage error (an unknown command, a missing path or a
file that cannot be read), and `n` when the script calls `exit(n)`. See
[Error Handling](../Language-Specification/Error-Handling.md#exit-codes).

## `grob repl`

Start an interactive session.

```
Grob 1.0.0  |  type 'help' for guidance, 'exit' to quit

G> _
```

Multiline input is handled gracefully. Errors in the REPL do not end the
session. `exit` and Ctrl+C both work.

## `grob check`

Type-check without running. Reports all compile-time errors.

```
grob check deploy.grob
```

No errors: `✓ deploy.grob`

With errors:
```
✗ deploy.grob

  Line 14 — type error
  Expected  int
  Got       string
```

## `grob fmt`

Format a Grob script. Never automatic, always opt-in. Idempotent — running it
twice produces the same result as running it once.

```
grob fmt deploy.grob
```

See [grob-formatter-specification.md](../../design/grob-formatter-specification.md)
for the complete specification.

## `grob --explain`

Display long-form documentation for an error code. Covers the cause, an example,
the fix, and related codes.

```
grob --explain E0101
```

The per-code documentation lives in `docs/errors/`. Note: long-form per-code
prose docs are scheduled for a dedicated session and are not yet present. The
error examples library in `docs/errors/examples/` covers gold-master diagnostic
output.

## `grob new`

Create a new script with minimal boilerplate.

```
grob new deploy.grob
```

## Plugin Commands

See [Install Strategy](Install-Strategy.md) for full detail.

```
grob install Grob.Http
grob install Grob.Http --system
grob install Grob.Http --local
grob install Grob.Http --version 1.1.0
grob install                         // install all grob.json dependencies

grob uninstall Grob.Http
grob restore
grob list
grob list --all
grob search sharepoint
grob update Grob.Http
grob update                          // update the runtime
grob init
```

## Output Principles

Quiet on success, clear on failure. Errors to stderr, results to stdout.
`--verbose` is available but never the default. No emoji in compiler or CLI
output. Never "simply" in any output.

# Modules and Imports

## Core Modules

Thirteen core modules are auto-available in every script. No `import` required:
`fs`, `strings`, `json`, `csv`, `env`, `process`, `date`, `math`, `log`,
`regex`, `path`, `format`, `guid`.

A script with no imports is self-contained. A script with imports has external
dependencies. This signal value is intentional.

## Importing Plugins

```grob
import Grob.Http
import Grob.Crypto
```

Plugins require explicit `import` and prior `grob install`. The default alias
is the last segment lowercased: `import Grob.Http` → `http.*`.

`Grob.Http` is a special case: it exposes both `http.*` and `auth.*` as
sub-namespaces from a single import.

## Explicit Alias

```grob
import Grob.Http as client
```

Available for collision resolution only — not for personality.

## Package Resolution

On `import`, the compiler checks locations in this order:

1. `.grob\packages\` — project local
2. `%USERPROFILE%\.grob\packages\` — user global
3. `%ProgramFiles%\Grob\packages\` — system global

If not found, compilation fails with a helpful message:

```
error: 'Grob.Http' is not installed.
       Run: grob install Grob.Http
```

## Circular Imports

Not supported. Grob scripts do not export types to other scripts in v1. One
script cannot import another script.

See also: [Script Parameters](Script-Parameters.md),
[Error Handling](Error-Handling.md)

# Script Parameters

## The `param` Block

Scripts declare parameters at the top of the file using `param`. Parameters are
typed, optionally defaultable and validated at the parameter boundary before the
script body runs.

```grob
param environment:     string
param dry_run: bool = false
param days:    int  = 30
```

Required parameters have no default. Optional parameters have a default value.
The type checker validates at compile time — wrong type or missing required
parameter is a compile error before execution.

## Invoking with Parameters

```
grob run deploy.grob --environment staging
grob run deploy.grob --environment staging --dry_run true
grob run deploy.grob --environment staging --days 60
```

## Param Files

The `.grobparams` file format provides reusable parameter sets. Key-value pairs
with `//` comments, designed to feel native to Grob.

```
// deploy.grobparams
environment = staging
days = 30
// pat is intentionally absent — supply via --pat or env.require()
```

```
grob run deploy.grob --params deploy.grobparams
```

Command-line arguments override param file values:

```
grob run deploy.grob --params deploy.grobparams --environment production
```

Param files are committable to source control. They are not JSON, not YAML, not
TOML — they are readable and diffable by design.

## Decorators

Decorators on parameters provide validation and handling instructions. Validated
at the parameter boundary before the script body runs.

| Decorator | Effect |
|-----------|--------|
| `@secure` | Value not echoed in output, not included in error messages, not logged |
| `@allowed(...)` | Value must be one of the listed options |
| `@minLength(n)` | String value must be at least `n` characters |
| `@maxLength(n)` | String value must be at most `n` characters |

```grob
@allowed("dev", "staging", "production")
param environment: string

@secure
param pat: string

@minLength(1)
@maxLength(64)
param project_name: string
```

### `@secure`

`@secure` is a handling instruction, not a type. The value is still `string` at
runtime. Effect: not echoed in output, not included in error messages, not
logged. The compiler warns if a `@secure` param appears in a `.grobparams` file
in plain text.

The canonical pattern for credentials is `env.require()`:

```grob
pat := env.require("ADO_PAT")
```

`@secure` params should be absent from `.grobparams` files — provide via command
line or `env.require()` instead.

See also: [Syntax](Syntax.md), [Modules and Imports](Modules-and-Imports.md)

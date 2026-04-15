# Script Parameters

## `param` Block

Scripts declare parameters in a `param` block at the top of the file, after
any `import` statements.

```grob
@secure
param token: string

param env: string
param days: int = 30
param dryRun: bool = false
```

Parameters are typed and may have defaults. Required parameters have no default.
The type checker validates at compile time — wrong type or missing required
parameter is a compile error before execution.

## Decorators

| Decorator | Purpose |
|-----------|---------|
| `@secure` | Value masked in logs and error output |
| `@allowed("dev", "staging", "prod")` | Restricted to enumerated values |
| `@minLength(n)` | Minimum string length |
| `@maxLength(n)` | Maximum string length |

## Invocation

```
grob run deploy.grob --token $env:ADO_PAT --env staging
grob run deploy.grob --params deploy.grobparams
```

See also: [Functions](Functions.md), [Modules and Imports](Modules-and-Imports.md)

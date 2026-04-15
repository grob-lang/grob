# Install Strategy

## Three-Tier Scope

| Scope | Flag | Location (Windows) |
|-------|------|--------------------|
| User (default) | *(none)* | `%USERPROFILE%\.grob\packages\` |
| System | `--system` | `%ProgramFiles%\Grob\packages\` |
| Project local | `--local` | `.grob\packages\` relative to `grob.json` |

## Resolution Order

Local → user → system. First found wins.

## Runtime Delivery

```
winget install Grob.Grob
```

## CI Pattern

Commit `grob.json`. Run `grob restore` in the pipeline. Idempotent.

See also: [Commands](Commands.md)

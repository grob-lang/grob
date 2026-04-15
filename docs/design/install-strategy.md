# Grob — Install Strategy

> Specification document for Grob runtime and plugin installation.
> Decisions authorised in the decisions log — April 2026.
> This document is the implementation reference for the CLI install commands,
> package resolution, and PATH configuration.
> When this document and the decisions log conflict, the decisions log wins.

-----

## 1. Design Principles

Three principles govern Grob's install strategy:

**No elevation by default.** Installing a plugin should never require administrator
rights. The default install path is always within the current user's profile.
System-wide installation is an explicit opt-in, not the default.

**Explicit over implicit.** Plugins are never downloaded automatically at runtime.
`grob install` is always a deliberate, separate step. A script that requires a
plugin which is not installed fails at compile time with a clear message — it never
silently fetches at execution time.

**The safe path is the short path.** The most common operation — installing a plugin
for personal use — requires the fewest flags. Global and system-wide installs require
explicit flags because they have broader consequences.

-----

## 2. Scope Model

Grob uses a three-tier scope model for plugin installation.

| Scope | Flag | Location (Windows) | Who sees it |
|---|---|---|---|
| User (default) | *(none)* | `%USERPROFILE%\.grob\packages\` | Current user only |
| System | `--system` | `%ProgramFiles%\Grob\packages\` | All users on machine |
| Project local | `--local` | `.grob\packages\` relative to `grob.json` | Current project only |

### User scope — the default

```
grob install Grob.Http
```

No flag required. Installs to `%USERPROFILE%\.grob\packages\`. No elevation needed.
The plugin is available to all scripts run by the current user. This is the right
default for a scripting tool — plugins like `Grob.Http` are reused across many
scripts, not tied to a single project.

### System scope

```
grob install Grob.Http --system
```

Installs to `%ProgramFiles%\Grob\packages\`. Requires administrator elevation.
Intended for shared machines, build servers, and CI environments where all users
need the same plugin set. Not the right choice for a personal development machine.

### Project local scope

```
grob install Grob.Http --local
```

Installs to `.grob\packages\` in the project root (the directory containing
`grob.json`). The installation is recorded in `grob.json` as a dependency.
Intended for projects that require specific plugin versions isolated from the
user-global install, or for checking in dependency declarations for team use.

`grob install --local` requires a `grob.json` to exist. If none is found by
walking up the directory tree, the command fails with:

```
error: No grob.json found in this directory or any parent directory.
       Run 'grob init' to create one, or omit --local to install user-wide.
```

-----

## 3. Package Resolution Order

When a script contains `import Grob.Http`, the compiler resolves the package
by checking locations in this order:

1. `.grob\packages\` — project local (highest priority)
2. `%USERPROFILE%\.grob\packages\` — user global
3. `%ProgramFiles%\Grob\packages\` — system global (lowest priority)

Local always wins over global. This allows a project to pin a specific plugin
version without affecting other scripts. The first location where the package
is found is used — the search stops there.

If the package is not found in any location, compilation fails:

```
error: 'Grob.Http' is not installed.
       Run: grob install Grob.Http
```

### grob.json manifest discovery

When resolving project-local packages, Grob walks up the directory tree from
the script file's location to find `grob.json` — it does not rely on the
current working directory. This means running `grob run` from any directory
will still find the correct project manifest.

```
C:\projects\my-scripts\
├── grob.json              ← found here
├── .grob\packages\
└── src\
    └── deploy.grob        ← script is here; walk finds grob.json two levels up
```

The walk stops at the filesystem root. If no `grob.json` is found, project-local
packages are not considered — only user and system scopes are checked.

-----

## 4. Directory Structure

### User profile root — `%USERPROFILE%\.grob\`

All per-user Grob data lives under a single root in the user's profile:

```
%USERPROFILE%\.grob\
├── packages\              ← user-global plugin installs
│   ├── Grob.Http\
│   │   └── 1.2.0\
│   └── Grob.Crypto\
│       └── 1.0.1\
├── bin\                   ← grob runtime binary (user-only install path)
└── cache\                 ← NuGet download cache
```

This follows the established convention of Go (`%USERPROFILE%\go\`),
Rust (`%USERPROFILE%\.cargo\`), and dotnet tools (`%USERPROFILE%\.dotnet\`).
All Grob data is in one place, easy to inspect and easy to remove.

### System root — `%ProgramFiles%\Grob\`

```
%ProgramFiles%\Grob\
├── bin\                   ← grob runtime binary (system install path)
└── packages\              ← system-global plugin installs
    ├── Grob.Http\
    └── Grob.Crypto\
```

### Project local — `.grob\`

```
my-project\
├── grob.json
└── .grob\
    └── packages\
        └── Grob.Http\
            └── 1.1.0\    ← pinned version, isolated from user-global
```

`.grob\` should be added to `.gitignore`. `grob.json` should be committed.
A team member clones the repo and runs `grob restore` to install all declared
dependencies — equivalent to `npm install` or `dotnet restore`.

-----

## 5. The Runtime Itself

The Grob runtime (`grob.exe`) is a separate concern from plugins.

### Installation paths

| Method | Location | Elevation |
|---|---|---|
| User install | `%USERPROFILE%\.grob\bin\grob.exe` | No |
| System install | `%ProgramFiles%\Grob\bin\grob.exe` | Yes |

### Recommended delivery — winget

```
winget install Grob.Grob
```

One command, no manual PATH configuration, handled by Windows Package Manager.
This is the recommended installation method and the one featured in documentation.

### PATH

The installer adds the appropriate `bin\` directory to the user or system PATH.
No manual PATH editing should be required for either install method.

### Updates

```
grob update          // update the runtime
winget upgrade Grob.Grob  // equivalent via winget
```

Runtime updates are independent of plugin updates. `grob update` updates the
runtime only — plugins are updated separately with `grob update Grob.Http`.

-----

## 6. CLI Reference — Install Commands

### `grob install <package>`

Install a plugin from the NuGet registry.

```
grob install Grob.Http              // user scope (default)
grob install Grob.Http --system     // system scope — requires elevation
grob install Grob.Http --local      // project local — requires grob.json
grob install Grob.Http --version 1.1.0  // pin a specific version
grob install                        // install all dependencies in grob.json
```

### `grob uninstall <package>`

Remove an installed plugin.

```
grob uninstall Grob.Http            // removes from user scope
grob uninstall Grob.Http --system   // removes from system scope
grob uninstall Grob.Http --local    // removes from project local scope
```

### `grob restore`

Install all dependencies declared in `grob.json`. Equivalent to `npm install`
or `dotnet restore`. Intended for CI environments and team onboarding.

```
grob restore
```

Resolves versions from `grob.json`, installs missing packages to the declared
scope. Does not download anything that is already installed at the correct version.

### `grob list`

List installed plugins.

```
grob list              // user scope
grob list --system     // system scope
grob list --local      // project local scope
grob list --all        // all scopes, with scope column
```

### `grob search <query>`

Search the NuGet registry for available plugins.

```
grob search sharepoint
grob search Grob.
```

Returns package ID, description, latest version, and download count.
Packages are discoverable by the `grob-plugin` NuGet tag.

### `grob update <package>`

Update an installed plugin to the latest compatible version.

```
grob update Grob.Http              // update in user scope
grob update Grob.Http --system     // update in system scope
grob update                        // update all packages in all user-scope installs
```

-----

## 7. grob.json — Dependency Manifest

`grob.json` is optional for single scripts. It becomes relevant when a project
contains multiple scripts sharing plugin dependencies, or when version pinning
and team reproducibility are required.

### Minimal shape

```json
{
  "name": "my-project",
  "version": "1.0.0",
  "dependencies": {
    "Grob.Http": "^1.0.0",
    "Grob.Crypto": "1.2.3"
  }
}
```

Version constraints follow semantic versioning:

| Constraint | Meaning |
|---|---|
| `"^1.0.0"` | Compatible — `>=1.0.0 <2.0.0` |
| `"1.2.3"` | Exact version pinned |
| `"*"` | Latest stable |

### grob init

```
grob init
```

Creates a minimal `grob.json` in the current directory. Does not overwrite an
existing file. Prompts for name and version, or accepts `--yes` for defaults.

-----

## 8. CI and Build Server Considerations

On a build server or CI agent, the recommended pattern is:

1. Commit `grob.json` with declared dependencies and version constraints.
2. In the pipeline, run `grob restore` before executing any scripts.
3. Use `--system` only if the agent image is shared and pre-provisioned.
   For ephemeral agents, user-scope is correct — the agent user's profile
   exists for the lifetime of the job.

```yaml
# Example — Azure DevOps pipeline step
- script: |
    grob restore
    grob run deploy.grob --params deploy.grobparams
  displayName: Run deployment script
```

`grob restore` is idempotent — safe to run on every pipeline execution.
It only downloads packages that are not already present at the declared version.

-----

## 9. Comparison — Why Not dotnet tool Model

The dotnet tool model was considered. Key differences:

| Concern | dotnet tool | Grob |
|---|---|---|
| Default scope | Local (manifest required) | User-global |
| System-wide | Not supported | `--system` flag |
| Manifest walk | Walks up from CWD | Walks up from script location |

dotnet tools default to local because they are developer tooling installed once
per project (e.g. `dotnet-ef`, `dotnet-format`). Grob plugins are script
dependencies reused across many scripts — `Grob.Http` is not a per-project tool,
it is a library. User-global as the default is the right model for that pattern.

The manifest walk behaviour is borrowed from dotnet tool — walking up from the
script file's location rather than the CWD is strictly more correct and avoids
confusing resolution failures when scripts are invoked from unexpected directories.

-----

*Document created April 2026 — install strategy design session.*
*Authorised decisions recorded in Grob___Decisions___Context_Log.md.*
*This document is the implementation reference — the decisions log is the authority.*

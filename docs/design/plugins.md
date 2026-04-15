# Grob Plugins

Grob’s functionality is extended through plugins. A plugin is a C# class library
that implements the `IGrobPlugin` interface and registers native functions with the
Grob VM at compile time — making those functions available to the type checker before
execution begins.

This document covers:

- [How plugins work](#how-plugins-work)
- [Installing plugins](#installing-plugins)
- [Using plugins in scripts](#using-plugins-in-scripts)
- [Writing your own plugin](#writing-your-own-plugin)
- [Official plugins](#official-plugins)
- [Community plugins](#community-plugins)
- [Listing your plugin here](#listing-your-plugin-here)

-----

## How Plugins Work

A plugin is a NuGet package tagged `grob-plugin` that implements `IGrobPlugin`:

```csharp
public interface IGrobPlugin
{
    string Name { get; }
    void Register(GrobVM vm);
}
```

The `Register` method is called at compile time. It registers native functions —
C# implementations with type signatures that Grob’s type checker can verify before
execution begins. A script calling a plugin function with the wrong argument types
gets a compile-time error, not a runtime crash.

### Core vs plugins

**Core modules** (`fs`, `strings`, `json`, `csv`, `env`, `process`, `date`, `math`,
`log`, `regex`, `path`, `format`, `guid`) are auto-available in every script. No import
required. No install required.

**Everything else** requires an explicit `import` and a prior `grob install`.

-----

## Installing Plugins

```bash
# Install globally — available in any script on this machine
grob install Grob.Http

# Install locally — available only in this project
grob install Grob.Http --local

# Install everything declared in grob.json
grob install

# Search for available plugins
grob search xml
```

Grob never silently downloads dependencies at runtime. The install step is always
explicit and separate from running a script.

If a script imports a plugin that is not installed, Grob fails at compile time
with a clear message:

```
Grob.Http is not installed.
Run: grob install Grob.Http
```

-----

## Using Plugins in Scripts

Declare dependencies at the top of the script with `import`:

```grob
import Grob.Http

// http.* and auth.* both available — auth is a sub-namespace, not a separate install
// Core modules need no import — just use them
PAT    := env.require("ADO_PAT")
result := http.get("https://api.example.com", auth.bearer(PAT))
data   := result.asJson()
```

### Import aliases

The default alias is the last segment of the module name, lowercased:

```grob
import Grob.Http        // alias: http — also exposes auth.* sub-namespace
import AcmeCorp.Xml     // alias: xml
```

This is a convention, not configuration. Any Grob script using `http.get()` is
using `Grob.Http`. Always predictable, no ambiguity.

### Explicit aliases

Use `as` only when two plugins would produce the same default alias, or when a
plugin alias would clash with a core module name:

```grob
import AcmeCorp.Strings as acme   // 'strings' is a core module
import OldCo.Http as legacyHttp   // 'http' already taken by another import
import NewCo.Http as http
```

`as` exists for collision resolution. Not for personal preference.

-----

## Project Manifest

For projects with multiple scripts sharing dependencies, create a `grob.json`
in the project root:

```json
{
  "name": "ado-tools",
  "version": "1.0.0",
  "dependencies": {
    "Grob.Http": "^1.0.0"
  }
}
```

Run `grob install` with no arguments to resolve and install everything declared.

The `^` prefix means compatible versions — `^1.0.0` accepts `1.x.x` but not `2.0.0`.

-----

## Writing Your Own Plugin

### Minimal example

```csharp
using Grob.Runtime;

public class HelloPlugin : IGrobPlugin
{
    public string Name => "AcmeCorp.Hello";

    public void Register(GrobVM vm)
    {
        vm.RegisterNative(
            name: "hello.greet",
            signature: new FunctionSignature(
                parameters: [new Parameter("name", GrobType.String)],
                returnType: GrobType.String
            ),
            implementation: args => {
                var name = args[0].AsString();
                return new StringValue($"Hello, {name}!");
            }
        );
    }
}
```

Build as a class library targeting the same .NET version as the Grob runtime.
Reference the `Grob.Runtime` NuGet package for the interfaces and types.

`Grob.Runtime` is versioned independently from the runtime. Your plugin declares
which version it targets. This is the public compatibility contract.

### During development

Test a plugin before publishing using the dev escape hatch:

```bash
grob run script.grob --dev-plugin ./bin/Debug/MyPlugin.dll
```

`--dev-plugin` is a development tool flag only. Scripts should never require it
to run — if they do, the plugin has not been published yet.

### Conventions

- **Namespace your functions** — prefix all function names with your plugin’s
  alias: `xml.parse()`, `pnp.getSite()`. Not just `parse()` or `getSite()`
- **Register type signatures** — always provide a `FunctionSignature`. This is
  what gives scripts compile-time type safety at your plugin boundary
- **Fail with `GrobRuntimeException`** — throw this rather than letting raw C#
  exceptions surface to the script author
- **Document your functions** — README with each function’s signature,
  description, and at least one example Grob script

### Naming

Use `Grob.YourName` for first-party style or `Organisation.YourName` for
community plugins:

```
Grob.Xml          ✓
AcmeCorp.Xml      ✓
MyXmlPlugin       ✗  — will not appear in grob search
```

### Repository structure (recommended)

```
grob-plugin-xml/
├── src/
│   └── AcmeCorp.Xml/
│       ├── AcmeCorp.Xml.csproj
│       ├── XmlPlugin.cs
│       └── ...
├── tests/
│   └── AcmeCorp.Xml.Tests/
├── examples/
│   └── parse-config.grob
├── README.md
└── LICENSE        ← MIT preferred
```

-----

## Official Plugins

Official plugins are maintained by the Grob project. Reviewed, tested, and
versioned alongside the runtime.

|Plugin     |Install                 |Alias |Description                                                                                                  |
|-----------|------------------------|------|-------------------------------------------------------------------------------------------------------------|
|`Grob.Http`|`grob install Grob.Http`|`http`|HTTP client — GET, POST, PUT, PATCH, DELETE, download. `auth.*` sub-namespace for Bearer, Basic, API key auth|

*More official plugins will be added as the runtime matures.*

-----

## Community Plugins

Community plugins are built and maintained by their authors. The Grob project
does not review or maintain them.

> ⚠️ **Loading a plugin is equivalent to running arbitrary code.** It executes
> with full .NET permissions on your machine. Always review plugin source or
> satisfy yourself the author is trustworthy before installing. Presence in this
> registry is not an endorsement of safety or quality.

|Plugin        |Author|Alias|Description|Repo|
|--------------|------|-----|-----------|----|
|*Be the first*|      |     |           |    |

-----

## Listing Your Plugin Here

Open a pull request adding a row to the community plugins table above.
The bar is low — we check:

- The repo exists and has a README
- The plugin is published to NuGet tagged `grob-plugin`
- The naming convention is followed
- A licence file is present (MIT preferred)

We are **not** reviewing plugin code as part of the listing PR.

### PR checklist

- [ ] Plugin published to NuGet with `grob-plugin` tag
- [ ] README includes: what the plugin does, install command, import alias,
  function signatures, at least one example Grob script
- [ ] Licence file present
- [ ] Naming convention followed
- [ ] Tested against current stable Grob runtime version (state the version)
- [ ] Row added to the community plugins table above

Title your PR: `chore: add YourPlugin to community registry`
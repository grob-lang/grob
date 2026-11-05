# Plugins — Overview

A plugin is a C# class library implementing `IGrobPlugin` that registers native
functions with the Grob VM. Those functions become available to the type checker
at compile time — scripts get compile-time type safety at the plugin boundary.

## Core vs Plugins

**Core modules** (`fs`, `strings`, `json`, `csv`, `env`, `process`, `date`,
`math`, `log`, `regex`, `path`, `formatAs`, `guid`) are auto-available. No import, no
install.

**Everything else** requires `import` and a prior `grob install`.

## The Plugin Interface

```csharp
public interface IGrobPlugin
{
    string Name { get; }
    void Register(IPluginRegistrar registrar);
}
```

`Register` is called once, when the host builds the catalogue the type checker and
the VM both read (D-426). It registers native functions with the `FunctionSignature`
every registration requires, which the type checker verifies calls against before
execution begins.

## Official Plugins

| Plugin | Alias | Description |
|--------|-------|-------------|
| `Grob.Http` | `http` | HTTP client with auth helpers |
| `Grob.Crypto` | `crypto` | Checksums and hashing |
| `Grob.Zip` | `zip` | Zip archive compression and expansion |

## Using Plugins

```grob
import Grob.Http

pat    := env.require("ADO_PAT")
result := http.get("https://api.example.com", auth.bearer(pat))
data   := result.asJson()
```

## Security

Loading a plugin is equivalent to running arbitrary code. It executes with full
.NET permissions. No sandbox claims are ever made. This is documented prominently
and users are expected to review plugin source or trust the author.

See also: [Writing Plugins](Writing-Plugins.md), [Grob.Http](Grob-Http.md),
[Install Strategy](../CLI/Install-Strategy.md)

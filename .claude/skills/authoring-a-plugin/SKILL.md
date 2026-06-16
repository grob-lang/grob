---
name: 'Authoring a Grob Plugin'
description: 'End-to-end procedure for building a Grob plugin via IGrobPlugin — registration, typed signatures, namespacing, error wrapping, testing, NuGet publication.'
---

# Authoring a Grob Plugin

A Grob plugin is a NuGet package tagged `grob-plugin` that implements `IGrobPlugin` and
teaches the type checker about new native functions and types. This skill applies to
first-party plugins under `plugins/` (`Grob.Http`, `Grob.Crypto`, `Grob.Zip`) and to
third-party plugins alike — they follow the same model. `Grob.Http` is the reference
implementation; read it before starting.

The cardinal rule: a first-party plugin gets no special treatment from the runtime. It
references `Grob.Runtime` and nothing else from the Grob solution. If your plugin needs
`Grob.Vm` or `Grob.Compiler`, you are crossing a boundary that must not be crossed —
the design is wrong, not the plugin.

## Step 1 — Project setup

- New project under `plugins/` (first-party) referencing only the `Grob.Runtime` NuGet
  package. No reference to `Grob.Core` directly beyond what `Grob.Runtime` re-exposes,
  no `Grob.Vm`, no `Grob.Compiler`.
- Target the `Grob.Runtime` version that defines your compatibility contract — plugin
  authors declare which runtime version they target.

## Step 2 — Implement `IGrobPlugin`

```csharp
public class HttpPlugin : IGrobPlugin
{
    public string Name => "http";

    public void Register(GrobVM vm)
    {
        // register native functions and types here
    }
}
```

`Register` is called at VM startup for core stdlib plugins (alphabetically by module
name) and at compile time for third-party plugins. It is the only entry point —
plugins extend the language through registration, never by adding grammar. The TextMate
grammar is static.

## Step 3 — Register functions with typed signatures

Every native function is registered with a `FunctionSignature` — name, typed
parameters, return type. This is non-negotiable: the signature is what the type checker
validates calls against. An untyped registration defeats Grob's static checking and is
a defect.

```csharp
vm.RegisterNative(
    name: "get",
    signature: new FunctionSignature(
        parameters: [ new Parameter("url", GrobType.String) ],
        returnType: /* the registered Response type */),
    implementation: args => { /* ... */ });
```

## Step 4 — Register types, namespaced

If the plugin introduces types (`Response`, `Headers`, `Auth` for `Grob.Http`), register
them. They are namespaced by the plugin's default alias, so `Grob.Http`'s `Response`
and a third party's `Response` coexist as `http.Response` and `acme.Response`. Keep
registered names specific. A collision with a stdlib type name is a compile error by
design — do not work around it, choose a different name.

## Step 5 — Wrap errors as `GrobError`

A plugin must never let a raw .NET exception escape into the VM. Catch at the boundary
and throw the appropriate `GrobError` leaf — `NetworkError` for HTTP failures,
`IoError` for file/stream problems, and so on — with a message that meets the quality
bar: what went wrong, and a fix where obvious. Errors go to stderr; results to stdout;
no emoji; British English.

## Step 6 — No runtime auto-download

Plugins are installed and restored deliberately via `grob install` / `grob restore`.
Never write code that fetches an assembly or a dependency on demand at runtime.

## Step 7 — Test

Test the plugin the way `Grob.Stdlib.Tests` tests core modules: register the plugin into
a VM instance and assert the output of its functions. Cover the error paths — assert
that a failure throws the right `GrobError` leaf with a useful message, not a leaked
.NET exception.

## Step 8 — Package and publish

- NuGet package tagged `grob-plugin` (this is how `grob search` discovers it).
- Include the plugin's own XML documentation describing its functions and types.
- Definition of Done includes publication to NuGet with the tag.

## Checklist

- [ ] References `Grob.Runtime` only — no `Grob.Vm`, no `Grob.Compiler`
- [ ] Implements `IGrobPlugin` with a clear `Name`
- [ ] Every native function registered with a full `FunctionSignature`
- [ ] Registered types namespaced; names specific; no stdlib collision
- [ ] All failures wrapped as the correct `GrobError` leaf with a quality message
- [ ] No runtime auto-download
- [ ] Tested by registration into a VM instance, success and error paths
- [ ] Packaged for NuGet with the `grob-plugin` tag

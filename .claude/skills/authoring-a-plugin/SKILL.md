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
public sealed class HttpPlugin : IGrobPlugin
{
    public string Name => "http";

    public void Register(IPluginRegistrar registrar)
    {
        // register natives, constants and named types here
    }
}
```

`Register` receives an `IPluginRegistrar`, never the VM (D-426). The host adds every
plugin to one `NativeCatalogueBuilder` and builds one `NativeCatalogue`; the type
checker reads signatures from it and the VM is constructed over it. There is no second
registration path. `Register` is the only entry point — plugins extend the language
through registration, never by adding grammar. The TextMate grammar is static.
`Register` must not perform a capability call: OS access goes through the capability
interfaces the host injects into the plugin's constructor (D-343).

## Step 3 — Register functions with typed signatures

Every native is registered with a `FunctionSignature` — a qualified name, typed
parameters and a return type. The only `RegisterNative` overload takes one, so an
untyped registration does not compile. The signature is data: the type checker
validates calls against it, and it holds no reference to the implementation.

```csharp
SignatureType.Concrete stringType = SignatureType.Of(GrobType.String);
SignatureType.Concrete responseType = SignatureType.Named(GrobType.Struct, "Response");

registrar.RegisterNative(
    new FunctionSignature(
        "http.get",
        [new SignatureParameter("url", stringType)],
        responseType),
    new NativeFunction("http.get", 1, (args, _) => { /* ... */ }));
```

- **The name is qualified** (`http.get`). A bare name is rejected, and so is a
  namespace that is a primitive type name (`int`, `float`, `string`, `bool`) —
  primitive receivers are first-party only.
- **Parameter names are public API.** Take them from `grob-stdlib-reference.md` or the
  plugin's own spec. Named-argument binding to natives is not supported yet (D-426
  Decision 4), but the names freeze with the signature.
- **One parameter shape covers every case.** `SignatureParameter(name, type,
  isVariadic, defaultValue)`: a variadic tail is the last parameter with
  `isVariadic: true`; an optional parameter carries a compile-time `defaultValue`.
- **A named type is a `Concrete`** of `GrobType.Struct` (or `NullableStruct`) plus its
  name. `SignatureType.ArrayOf` describes an array element type, but the checker
  verifies only that the argument is an array, not its element type (R-21): a native
  declared to take `string[]` can receive an `int[]`. Validate array elements in the
  implementation and fail with the appropriate `GrobError` leaf.
- **Type parameters are fenced.** A signature declaring one makes the checker throw
  `GrobInternalException` until the `mapAs<T>` increment teaches it substitution.
- **A duplicate is a host error.** Registering a name another plugin, or this one,
  already registered throws at catalogue-build time.

## Step 4 — Register named types

If the plugin introduces types (`Response`, `Headers`, `Auth` for `Grob.Http`), register
each with `RegisterNamedType(new NamedTypeDefinition(name, properties, methods,
render))`: a `NamedTypePropertyBinding` (a `PropertySignature` plus a getter) per
property, a `NamedTypeMethodBinding` (a `FunctionSignature` with a bare name plus a
binder) per method, and the renderer `print()` uses. A named-type method call must
supply exactly as many arguments as its signature declares: optional parameters and
variadic tails are honoured on namespace natives but not yet on named-type methods, so
declare neither there. `DatePlugin` and `GuidPlugin` in
`Grob.Stdlib` are the worked examples. A plugin adds members only to types it
registered, and a second registration of one type name is a host error. Keep
registered names specific.

The type name is a bare identifier today. Alias-namespacing plugin types
(`http.Response` beside `acme.Response`) belongs to the Sprint 11 plugin loader (R-04)
and is not built yet. A collision with a stdlib type name is an error by design — do
not work around it, choose a different name.

## Step 5 — Wrap errors as `GrobError`

A plugin must never let a raw .NET exception escape into the VM. Catch at the boundary
and throw the appropriate `GrobError` leaf — `NetworkError` for HTTP failures,
`IoError` for file/stream problems, and so on — with a message that meets the quality
bar: what went wrong, and a fix where obvious. Errors go to stderr; results to stdout;
no emoji; prose follows `house-style/SKILL.md`.

## Step 6 — No runtime auto-download

Plugins are installed and restored deliberately via `grob install` / `grob restore`.
Never write code that fetches an assembly or a dependency on demand at runtime.

## Step 7 — Test

Test the plugin the way `Grob.Stdlib.Tests` tests core modules: build a catalogue with
`new NativeCatalogueBuilder().Add(plugin).Build()`, construct a VM over it and assert
the output of its functions. Assert the registered signatures against the catalogue as
well — they are the public contract. Cover the error paths — assert
that a failure throws the right `GrobError` leaf with a useful message, not a leaked
.NET exception.

## Step 8 — Package and publish

- NuGet package tagged `grob-plugin` (this is how `grob search` discovers it).
- Include the plugin's own XML documentation describing its functions and types.
- Definition of Done includes publication to NuGet with the tag.

## Checklist

- [ ] References `Grob.Runtime` only — no `Grob.Vm`, no `Grob.Compiler`
- [ ] `Register` performs no capability call
- [ ] Implements `IGrobPlugin` with a clear `Name`
- [ ] Every native registered with a `FunctionSignature`; parameter names chosen as public API
- [ ] Named types registered through `RegisterNamedType`; names specific; no stdlib collision
- [ ] All failures wrapped as the correct `GrobError` leaf with a quality message
- [ ] No runtime auto-download
- [ ] Tested through a catalogue and a VM over it, success and error paths
- [ ] Packaged for NuGet with the `grob-plugin` tag

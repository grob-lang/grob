---
name: 'Adding a Stdlib Function'
description: 'How to add a function or method to one of the thirteen core modules — registration, type registry, the one-way principle, formatAs boundaries, tests.'
---

# Adding a Stdlib Function

The thirteen core modules (`fs`, `strings`, `json`, `csv`, `env`, `process`, `date`,
`math`, `log`, `regex`, `path`, `formatAs`, `guid`) are implemented in `Grob.Stdlib` as
`IGrobPlugin` implementations — one plugin class per module. `Grob.Cli` adds them all
to one `NativeCatalogueBuilder`, and the type checker and the VM both read the single
`NativeCatalogue` it builds (D-426). They are not hardwired into the VM. This skill covers adding a
function to a module or a method to a built-in type.

## Step 1 — Decide module function vs instance method

Grob deliberately splits these:

- **Module function** — a free operation: `strings.join(...)`, `math.log10(...)`,
  `path.join(...)`, `fs.list(...)`.
- **Instance method** — an operation on a value: `n.roundTo(2)`, `d.format("MM-MMMM")`,
  `s.replace(a, b)`, `file.moveTo(dst)`.

The dividing line matters. Most string operations are methods on `string`;
`strings.join()` is the lone module function because it operates across a collection.
Scalar formatting is instance methods (`n.roundTo`, `d.format`), **not** the `formatAs`
module — `formatAs` is narrowed to collection-to-string terminators only:
`formatAs.table()`, `formatAs.list()`, `formatAs.csv()` (D-282). Putting a scalar
formatter on `formatAs` is a design regression; do not do it.

## Step 2 — Honour the one-way principle

There must be no two ways to do the same thing. Before adding, check the type registry
(`docs/design/grob-type-registry.md`) and the stdlib reference
(`docs/design/grob-stdlib-reference.md`) for an existing operation that covers it.
`.select()` superseded `.map()`; do not reintroduce a synonym. If your new function
overlaps an existing one, the question is which one wins, and that is a design decision
(`D-###`), not an additive implementation.

## Step 3 — Register with a typed signature

In the module's plugin `Register` method, register the function with
`registrar.RegisterNative(signature, implementation)`. The only overload takes a
`FunctionSignature` — a qualified name (`strings.join`), typed `SignatureParameter`s and
a return type — so an untyped registration does not compile. `Grob.Stdlib/Signatures.cs`
holds the shared types and the `Signature`/`Param`/`Optional`/`Variadic` shorthand the
plugins import with `using static`. Parameter names are part of the public API even
though a native cannot be called with them yet: named-argument binding to natives is
unsupported (D-426 Decision 4), so a named argument on a namespace-native or
primitive-member call reports E0011. A named-type method call does not check argument
names yet: a named argument there is accepted and bound by position (R-20). Every
native call is positional today. The names are recorded for
the binding that comes later, where optional and boolean parameters are idiomatically
called by name (`recursive: true`, `overwrite: true`), so choose them for that. Take them from `grob-stdlib-reference.md`; if it names none, choose deliberately
and list the name for the reference to adopt.

Where you register depends on the receiver:

- **A named type the module owns** (`date`, `guid`, every Sprint 9C type) — add a
  `NamedTypePropertyBinding` or `NamedTypeMethodBinding` to the `NamedTypeDefinition`
  the plugin passes to `RegisterNamedType`. `DatePlugin` is the worked example.
- **A primitive receiver** (`int`, `float`, `string`, `bool`) — the plugin implements
  the internal `IPrimitiveMemberPlugin` and registers through
  `RegisterPrimitiveProperty`/`RegisterPrimitiveMethod` (bare name) or
  `RegisterPrimitiveStatic` (`int.min`). That surface is internal by design: a third-party
  plugin cannot add a method to `string` (D-426 Decision 3). `StringMethodsPlugin` and
  `NumericMethodsPlugin` are the worked examples.

## Step 4 — Update the registry docs

Add the function or method to the canonical reference — `grob-stdlib-reference.md` for
module functions, `grob-type-registry.md` for instance methods — with its signature and
a one-line description. The module's documented member count must stay accurate; an
off-by-one between the doc and the implementation is exactly the kind of finding a
consistency review catches. Cite the `D-###` that authorised the addition.

## Step 5 — Errors and conventions

Wrap failures as the appropriate `GrobError` leaf (`IoError`, `JsonError`,
`ProcessError`, etc.) with a quality message. Results to stdout, errors to stderr, no
emoji; prose follows `house-style/SKILL.md`. Windows path conventions throughout.

## Step 6 — Test

In `Grob.Stdlib.Tests`, build a catalogue over the module's plugin
(`TestCatalogue`), construct a VM over it and assert the function's output, including
error paths. Pin the new signature in the module's signature test
(`StdlibNamespaceSignatureTests` or `StdlibPrimitiveSignatureTests`), and add the
native's four probes to the native-call probe corpus (correct call, wrong arity, wrong
argument type, nullable argument) so its diagnostics are guarded. If the function appears in any of the thirteen
release-gate sample scripts, make sure that script still compiles and runs.

## Checklist

- [ ] Correct placement decided: module function vs instance method
- [ ] `formatAs` used only for collection terminators; scalar formatting is a method
- [ ] One-way principle checked — no synonym for an existing operation
- [ ] Registered with a `FunctionSignature`; parameter names chosen as public API
- [ ] Instance members registered through their type's `NamedTypeDefinition` or the internal primitive surface
- [ ] Reference doc updated; member count kept accurate; `D-###` cited
- [ ] Failures wrapped as the correct `GrobError` leaf
- [ ] Tested through a catalogue and a VM over it, success and error paths; signature pinned; probes added
- [ ] Any affected release-gate script still compiles and runs

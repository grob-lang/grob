---
name: 'Adding a Stdlib Function'
description: 'How to add a function or method to one of the thirteen core modules — registration, type registry, the one-way principle, formatAs boundaries, tests.'
---

# Adding a Stdlib Function

The thirteen core modules (`fs`, `strings`, `json`, `csv`, `env`, `process`, `date`,
`math`, `log`, `regex`, `path`, `formatAs`, `guid`) are implemented in `Grob.Stdlib` as
`IGrobPlugin` implementations — one plugin class per module — and auto-registered at VM
startup by `Grob.Cli`. They are not hardwired into the VM. This skill covers adding a
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

In the module's plugin `Register` method, register the function with a full
`FunctionSignature` — typed parameters and return type. Use named-argument-friendly
parameter names: optional and boolean parameters are idiomatically called by name in
Grob (`recursive: true`, `overwrite: true`), so the parameter names are part of the
public API. Choose them deliberately.

For an instance method, register it against the type in the `TypeRegistry` so the type
checker knows the receiver type exposes it.

## Step 4 — Update the registry docs

Add the function or method to the canonical reference — `grob-stdlib-reference.md` for
module functions, `grob-type-registry.md` for instance methods — with its signature and
a one-line description. The module's documented member count must stay accurate; an
off-by-one between the doc and the implementation is exactly the kind of finding a
consistency review catches. Cite the `D-###` that authorised the addition.

## Step 5 — Errors and conventions

Wrap failures as the appropriate `GrobError` leaf (`IoError`, `JsonError`,
`ProcessError`, etc.) with a quality message. Results to stdout, errors to stderr, no
emoji, British English. Windows path conventions throughout.

## Step 6 — Test

In `Grob.Stdlib.Tests`, register the module's plugin into a VM instance and assert the
function's output, including error paths. If the function appears in any of the thirteen
release-gate sample scripts, make sure that script still compiles and runs.

## Checklist

- [ ] Correct placement decided: module function vs instance method
- [ ] `formatAs` used only for collection terminators; scalar formatting is a method
- [ ] One-way principle checked — no synonym for an existing operation
- [ ] Registered with a full `FunctionSignature`; parameter names chosen as public API
- [ ] Instance methods registered against their type in the `TypeRegistry`
- [ ] Reference doc updated; member count kept accurate; `D-###` cited
- [ ] Failures wrapped as the correct `GrobError` leaf
- [ ] Tested via plugin registration, success and error paths
- [ ] Any affected release-gate script still compiles and runs

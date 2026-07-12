# Pre-Sprint-8 Interlude — Increment C: wire `print` and interpolation through `ValueDisplay`

**Branch:** `feat/value-display-wiring`
**Authorises:** D-336
**Model:** Sonnet
**Depends on:** Increment B (`ValueDisplay` exists in `Grob.Runtime`)

## Why

Increment B built the renderer. Nothing calls it yet. This increment routes `print()`'s
single-arg fast path and string interpolation through `ValueDisplay.Display`, corrects
the four `Sprint6IncrementBTests` assertions to the real rendered form, and adds the
`AuthHeader` non-leak guard at the integration level. After this, §13 describes dispatch
that actually happens.

## Plan gate

Produce a numbered plan and stop for approval. The plan must report:

1. **How `OpCode.Print`'s single-arg fast path currently renders** — the hardcoded C#
   switch (D-336). State the exact site.
2. **How string interpolation currently lowers** — this is the assembly-boundary
   question. Determine from live code whether interpolation is compiled to per-segment
   `.toString()` calls at compile time, or to a runtime concatenation that formats each
   non-string segment in the VM. Route each non-string segment through
   `ValueDisplay.Display` at whichever layer the concatenation actually happens. Do not
   create a `Grob.Compiler` → `Grob.Vm` edge; `ValueDisplay` lives in `Grob.Runtime` so
   both consumers reach it legally — confirm the reference edges exist.
3. **What `GrobValue.ToString()` should become.** It is no longer the print renderer.
   Decide whether it delegates to `ValueDisplay.Inspect` (useful for C# debugging) or is
   removed from the print path entirely. State the choice; it is a genuine call.
4. **Which of the five smoke gold masters print a composite, a `float`, or a function**,
   and will therefore change output when this lands. Enumerate them. `types.grob`
   (Sprint 6) is the prime suspect. Any affected master is regenerated **deliberately**
   in this increment, reviewed line by line, never blindly accepted.

Do not edit until approved.

## Scope

**In:** `Grob.Vm` (`OpCode.Print` fast path), the interpolation formatting site (layer
determined at plan gate), `Grob.Core` `GrobValue.ToString()` (per the decision above),
`Sprint6IncrementBTests` (assertion correction), `Grob.Integration.Tests` (new
`AuthHeader` non-leak test), and any smoke gold master proven to change.

**Out:** `ValueDisplay` itself (built and frozen in B — if it needs a change, that is a
finding for B, not a quiet edit here), the membership gate, error codes.

## TDD

1. **Red first.**
   - Rewrite the four `Sprint6IncrementBTests` assertions from the interim `[Config]`
     form to **exact-match** the real rendered output, e.g.
     `Assert.Equal("Config { host: \"example.com\", port: 8080 }\r\n", stdout)`. Use
     exact-match, not `Assert.Contains` — containment is what let the original weak
     assertions and the `[Config]` revert both pass against accidents. Confirm **red**
     (current output is `[Config]`).

     Note on provenance: these four assertions were correct when first written (they
     required field values), were rewritten mid-interlude to expect `[Config]`, and are
     now rewritten a third time to the exact form. The `[Config]` commit does not
     represent a decision (D-336) — do not treat it as the baseline to preserve.
   - Add an `AuthHeader` non-leak integration test: a script that constructs an
     `AuthHeader` with a known secret and `print`s it; assert stdout does **not** contain
     the secret. Confirm this is **green now** (accidental opacity) — it is a guard that
     must stay green through the wiring, not a red-to-green driver.
2. **Green.** Wire `OpCode.Print` and interpolation through `ValueDisplay.Display`. The
   four struct tests go green; the `AuthHeader` guard stays green (proving step 2 of the
   precedence survives real dispatch).
3. **Regenerate** any affected smoke gold master, reviewed by hand.

## Acceptance

- [ ] The four `Sprint6IncrementBTests` assert the exact rendered form and pass.
- [ ] The `AuthHeader` non-leak integration test passes — `print(authHeader)` never
      contains the credential through the real `print` path.
- [ ] All five smoke scripts (`hello`, `calculator`, `functions`, `types`, `errors`)
      run correctly; any changed gold master was regenerated deliberately and reviewed.
- [ ] `print(1.0)` emits `1.0`; interpolation `"${1.0}"` emits `1.0`; both invariant.
- [ ] `dotnet test` green at the solution root; the membership gate stays green.
- [ ] 90% line coverage on changed code.
- [ ] Error-code count unchanged at 116. No `GrobValue` shape, `OpCode` set, or `.grobc`
      format change (D-297, ADR-0013, D-298 surfaces untouched — this changes how a value
      is *rendered*, not how it is *represented*).

## Commit

One commit. e.g. `feat(vm): route print and interpolation through ValueDisplay; correct
Sprint6IncrementB assertions (D-336)`. Body records that the four assertions now match the
decided form, that the `[Config]` interim is superseded, and lists any regenerated gold
master with a one-line reason.

## Guardrails

British English, no Oxford comma, never "simply". One concern. If wiring reveals that
`ValueDisplay` renders something wrongly, stop and log it against Increment B — do not
patch the service inside the wiring increment. A regenerated gold master is only
acceptable when its diff is exactly the intended display change and nothing else.

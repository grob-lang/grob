# Increment prompt — `FunctionSignature`

**Authority: D-426**, which refines D-419 and implements D-081. Read D-426, D-419 and
D-418 in `docs/design/grob-decisions-log.md`, and R-05, R-12 and R-19 in the Deferred
Work Register in `docs/design/grob-open-questions.md`, before starting. This prompt
implements them; where prompt and decision differ, D-426 wins and the difference is a
finding to report.

**Model and effort, by session.** D-426 Decision 6 records this as Sprint 9's second
named load-bearing structural sub-problem: it fixes the plugin SDK shape that 9C, 9D and
Sprint 11 all build on. The stronger model is spent where that shape is decided, not
where it is applied:

| Session | Work | Model | Effort |
|---|---|---|---|
| A | §1 investigation gate | Fable 5.1 | xhigh |
| B | Change group 1 — probe corpus | Opus 5.5 | medium |
| C | Change groups 2 and 3 — signature types, registrar | Fable 5.1 | high |
| D | Change groups 4, 5 and 6 — construction sites, migrations | Opus 5.5 | medium |
| E | Change group 7 — named types into plugins | Fable 5.1 | high |
| F | Change group 8 — corrections and record | Opus 5.5 | medium |

A session may cover fewer groups than its row; it may not cover groups from two rows.

**Session-start check — before anything else in every session:**

1. Read `.grob-session/handoff.md` (absent in session A) and `git log` on the branch, and
   state which session row this is and which change groups it will cover.
2. State the model you are running as, from your own system context. If it does not match
   the row, **stop** and say so.
3. You cannot see the effort setting. State the row's expected effort and **wait for a
   one-word confirmation** before proceeding.

This table cannot switch the model or effort; the person running the session sets both.
The check exists so a mismatch is caught at the start rather than discovered in review.

**Sessions B, D and F build to a design they did not make.** The gate report (§1) and the
commit messages of sessions C and E are their only source for why the types are shaped as
they are. Those sessions therefore record the reasoning, not just the result, and an
Opus session that meets a design question neither answers stops rather than deciding it
(§6).

**Branch:** one concern, one branch. The concern is native signatures.

**This increment may span several sessions** (D-426 Decision 6). Every change group in §2
ends as its own commit with the full suite green, and `/handoff` is run after each so
`.grob-session/handoff.md` names the last completed group, the next one and anything
open. A new session resumes by reading the handoff file and `git log` on the branch — never
from memory of a previous conversation.

**Archive this prompt verbatim** at `docs/prompts/function-signature.md` in this
increment's first commit, exactly as issued. Never retrofit it. If the work diverges, the
divergence goes in the decisions-log entry.

---

## 1. Read-only investigation gate

*Session A — Fable 5.1, xhigh.*

**No source edits until this gate is complete and reported.** Building, running the CLI
and running the test suite are expected. Changing production or test code is not, with one
exception: the probe corpus in §1.2 is written now, against the untouched tree, because
its whole value is that it was captured before anything moved.

Write the report to a **scratch file outside the working tree**. Do not paste it into
chat; in chat give the path and any STOP conditions that fired. End session A by running
`/handoff` with the report's path in it — every later session finds the design through
that pointer, and a report nobody can find is the same as no report.

### 1.1 Confirm the surface D-426 describes

D-426 counts, at `b202509`: 56 namespace natives and 8 constants in `NamespaceRegistry`,
37 members in `PrimitiveMemberRegistry`, 32 in `NamedTypeRegistry` and 131
`new TypeChecker(` sites across `src/`, `tests/` and `bench/`. **State the expected counts
before running**, then confirm each against the branch's base. A difference is not a
STOP by itself; a native or registry D-426 does not mention is.

Also confirm D-426 Decision 1's exclusion premise: that `ArrayNatives` and `MapNatives`
are never registered through `IPluginRegistrar`, and that no other native reaches the VM
by a route this increment would not touch. List every `RegisterNative` caller.

### 1.2 Capture the probe corpus

For **every** in-scope native and instance member, write a call that compiles clean, a
call with the wrong arity, a call with a wrong argument type and — where the parameter is
non-nullable — a call passing a nullable value. Run each through the checker on the
untouched tree and record the complete diagnostic output: code, line, column and message.

This becomes a committed regression test in change group 1 and must pass byte-identically
at the end (D-426 Decision 5, condition 2). The pre-change output is the gold master.
**Do not regenerate it later to match new output** — that is the ratchet D-313 forbids.

Also record what a named-argument call to a native does today (`math.sqrt(x: 2.0)`).
D-426 Decision 4 requires that behaviour to survive unchanged.

### 1.3 Plugin registration side effects

Under D-426 Decision 2, a compile-only path (`grob check`, the LSP, the tests) builds the
catalogue by running first-party plugin registration. Confirm, for every plugin in
`Grob.Stdlib`, that `Register` performs **no capability call** — no clock read, no
environment read, no random draw, no stream write — so it can be run with inert
capabilities. Report each plugin's constructor dependencies.

`FormatAsPlugin` captures `registrar.RenderValue` at registration. Report how that works
if the registrar that builds the catalogue is not the VM, and propose the fix.

### 1.4 Propose the types

D-426 fixes invariants, not type names. Propose, with reasoning:

- `FunctionSignature`, its parameter type and the return-type union of D-419 Decision 2;
- the catalogue type, where it lives and how the host builds and shares it;
- the public registrar surface for named types, and the non-public surface `Grob.Stdlib`
  uses for primitive receivers (D-426 Decision 3 — third-party plugins must not be able to
  add members to `int`, `float` or `string`);
- how the VM's `GetProperty` named-type dispatch reads the catalogue instead of
  `NamedTypeRegistry`;
- whether `RegisterToString` survives once a named-type registration carries its own
  renderer. Retire it if every caller becomes a named-type registration; otherwise keep it
  and say why.

Write the proposed public signatures in full in the report. **These are SDK shape**: they
freeze at first package publication (D-419), and every later plugin is written against
them. For each, record the alternatives considered and why they lost — sessions B, D and
F will implement against this section without having been part of the reasoning.

### 1.5 The construction-site strategy

131 `TypeChecker` construction sites gain a required argument. Propose how tests share one
catalogue (built once, not per test) and whether test projects need a new reference to
`Grob.Stdlib` to build it. Confirm the DAG still holds for `src/`: `Grob.Compiler` gains no
reference to `Grob.Stdlib`.

### 1.6 The layout question (R-19)

Determine whether moving `date` and `guid` behaviour into `DatePlugin` and `GuidPlugin`
can land while `Grob.Vm`'s `DateNatives`/`GuidNatives` keep their own layout constants. If
it can, R-19 stays `unowned` and is reported, not fixed. If it cannot, R-19 comes into
scope as the smallest change that lets the move land — the layout defined once in
`Grob.Core`.

---

## 2. Scope

Eight change groups, each a green commit. Land them in this order.

1. *Session B — Opus 5.5, medium.* **Probe corpus committed** as a regression test,
   passing on the untouched tree. Before committing, reconcile the corpus against §1.1's
   counts: every in-scope native and member has all four probe kinds that apply to it. A
   missing probe is a missing regression guard, and nothing later would notice.
2. *Session C — Fable 5.1, high.* **The signature types** in `Grob.Runtime` — `FunctionSignature`, its parameter type,
   the return-type union, type parameters with constraints — with construction and
   validation tests. Nothing consumes them yet.
3. *Session C — Fable 5.1, high.* **The catalogue and the registrar surface.** A signature-taking `RegisterNative` is
   added alongside the untyped overload, which survives only until group 7 so the suite
   stays green between groups. Duplicate registration is a host error. The named-type and
   primitive-receiver surfaces per §1.4.
4. *Session D — Opus 5.5, medium.* **`TypeChecker` reads the catalogue**, taken as a required constructor argument, with
   all construction sites updated. The hand tables are still present and still consulted
   for anything not yet migrated, so the suite stays green between groups.
5. *Session D — Opus 5.5, medium.* **Namespace natives migrate.** Every `Grob.Stdlib` namespace registration supplies its
   signature; `NamespaceRegistry`'s native and constant tables are deleted.
6. *Session D — Opus 5.5, medium.* **Primitive instance members migrate.** `StringMethodsPlugin` and
   `NumericMethodsPlugin` register their members with signatures through the non-public
   surface; `PrimitiveMemberRegistry` is deleted.
7. *Session E — Fable 5.1, high.* **Named types move into their plugins.** `date` and `guid` register their properties,
   methods and renderers from `DatePlugin` and `GuidPlugin`; the VM's `GetProperty`
   dispatch and the checker's type-name resolution read the catalogue;
   `NamedTypeRegistry` is deleted. **The untyped `RegisterNative` overload is deleted**, so
   registration without a signature no longer compiles. The three agreement tests are
   deleted in this group by D-426 Decision 2's authority, replaced by tests of its
   invariants.
8. *Session F — Opus 5.5, medium.* **Corrections and record.** R-05's seven `FunctionSignature` sites; the six design and
   wiki documents D-426 lists, reconciled to the API as built and citing D-426; the
   decisions-log entry; the register.

### Out of scope — report, do not fix

- **Array and map instance methods, `formatAs`, `print`/`input`/`exit`** (D-426 Decision 1).
- **Type-parameter substitution and constraint checking.** That is the `mapAs<T>`
  increment. Here the checker throws `GrobInternalException` on meeting a type parameter.
- **Named-argument binding to natives** (D-426 Decision 4).
- **The Sprint 11 manifest, `Assembly.LoadFrom`, `import` of third-party plugins and
  `Grob.Core`'s public-surface narrowing** (R-04).
- **R-05's four naming sites** — the error-examples harness increment's.
- **R-19**, unless §1.6 brings it in.
- `wiki/ADR/0004-constrained-generics.md` — a record, not edited.

**D-420's stopping rule applies.** Findings go to the register, not into the run, unless
they block this increment or Sprint 9C.

---

## 3. Implementation notes

**Signatures are data.** `FunctionSignature` holds no delegate and no reference to an
implementation (D-419 Decision 1). Registration binds a signature to a `NativeFunction`;
the signature on its own must be something a manifest could serialise.

**Parameter names come from `grob-stdlib-reference.md`** wherever it documents them. List
every name you had to invent in the report, by native, so the reference can be corrected
to match or the name changed before it freezes.

**One parameter shape replaces four.** `VariadicElementType`, `ParameterDefaults`,
`ParameterNamedTypeNames` and `NamedTypeParameterKind.NominalSelf` become properties of a
parameter. If any of the four turns out to need something the parameter shape cannot say,
that is a STOP, not a fifth field.

**The catalogue is built once.** D-338 cached `NamespaceDecl` and `Symbol` objects because
the smallest compile benchmarks pay disproportionately for per-compile allocation. Hold to
that: the catalogue is built by the host and shared, never rebuilt inside `Check`.

**Diagnostic wording does not move.** Every existing native-call message is pinned by the
probe corpus. If a migrated path produces different text for the same mistake, the
migration is wrong — fix the code, not the gold master.

**Corrections land with the truth.** A skill or design document describing the new API is
edited in the commit that makes it true, not before (D-419). Group 8 is last for that
reason.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code. Quarantine with a
documented reason rather than weakening a test or merging red. Tests may be updated to
assert new correct behaviour; the only deletions permitted are the three agreement tests
D-426 names.

Required:

- **The probe corpus**, byte-identical at the end to its capture in §1.2.
- **Signature construction and validation**: every invalid shape the constructor rejects,
  each with its own test.
- **Type-parameter fence**: a registered signature declaring a type parameter makes the
  checker throw `GrobInternalException`, and the message names the `mapAs<T>` increment.
- **Registration invariants**, replacing the agreement tests: a duplicate namespace member,
  a duplicate named type and a member added to a type the registering plugin does not own
  each fail at registration.
- **Primitive receivers are closed**: a test that a type implementing only the public
  registrar surface cannot register a `string` member. If the mechanism is
  `InternalsVisibleTo`, pin it with a compile-level check or a reflection test, and say
  which.
- **One catalogue**: the checker and the VM resolve the same native set from the same
  instance.
- **All eleven validation scripts** produce the same diagnostics as before the increment.

**Mutation-verify** the duplicate-registration guard, the owner guard and the
type-parameter fence. For each: remove the guard, **predict the exact failure signature
before running**, confirm it, restore.

---

## 5. Deliverables

- Eight change groups per §2, each its own green commit.
- Error-code count **unchanged at 120**; D-316 gate green.
- **D-426 Decision 5's three conditions**, each reported explicitly: suite green with test
  count; probe corpus byte-identical; benchmark gate green with no baseline updated.
- Coverage: `Grob.Compiler`, `Grob.Runtime` and `Grob.Stdlib` at or above the D-328 90%
  bar. Report each figure.
- A decisions-log entry in three-location lockstep. **Take the D-number from the live tail
  of the merged log**, never from memory or this prompt.
- R-12 moved to the register's Closed Items table with that D-number. R-05 stays open
  (its full sweep and the naming sites remain); its row records the seven sites closed.
- The list of invented parameter names, by native.

---

## 6. STOP conditions

Stop and report, do not proceed:

- The session-start check finds the running model does not match the session's row.
- A session B, D or F meets a design question that neither the gate report nor the
  commit messages of sessions C and E answer. Bring it back to a Fable session; do not
  decide it in place.

- §1.1 finds a native, registry or registration route D-426 does not account for.
- §1.3 finds a first-party plugin whose `Register` performs a capability call.
- The catalogue cannot be built without a VM instance, or the checker cannot read it
  without `Grob.Compiler` referencing `Grob.Stdlib`.
- The primitive-receiver surface cannot be closed to third-party plugins.
- Any of the four accreted parameter fields needs something the parameter shape cannot say.
- A migrated native produces different diagnostic text and the cause is in the design, not
  the code.
- The benchmark gate fails and the only fix is a baseline update.
- The change requires a new error code, an opcode or a `GrobValueKind` or `GrobType`
  variant.

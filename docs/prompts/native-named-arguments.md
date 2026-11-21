# Increment prompt — native named arguments

**Authority: D-446 and D-447.** D-446 decides that named arguments bind to natives; D-447
authorises this increment and rules on the call paths D-446 did not name. Read both in
`docs/design/grob-decisions-log.md`, with D-113, D-318, D-358, D-426 Decisions 1 and 4 and
D-443. Before starting, also read R-20, R-55 and R-56 in the Deferred Work Register and
OQ-028, both in `docs/design/grob-open-questions.md`, then §12.2 of
`docs/design/grob-language-fundamentals.md`. This prompt implements them;
where prompt and decision differ, the decision wins and the difference is a finding to
report.

**This increment runs before the Sprint 9C `fs` increment and blocks it.** D-445's `fs`
surface is written with named arguments throughout and does not compile until this lands.

**Model and effort, by session.** The judgement is in two places: the gate, which measures
every call shape before anything moves, and the binder, where one routine comes to serve
script functions and natives without changing a word script functions say today. The rest
applies that design.

| Session | Work | Model | Effort |
|---|---|---|---|
| A | §1 investigation gate | Opus | high |
| B | Change groups 1 and 2 — the shared binder; namespace natives | Opus | high |
| C | Change groups 3, 4 and 5 — primitive methods; named-type methods; calls with no signature | Sonnet | high |
| D | Change groups 6 and 7 — the parameter-name drift check; record | Sonnet | medium |

A session may cover fewer groups than its row; it may not cover groups from two rows.

**Session-start check — before anything else in every session:**

1. Read the branch's handoff file under `.grob-session/handoffs/` (absent in session A) and
   `git log` on the branch, and state which session row this is and which change groups it
   will cover.
2. State the model you are running as, from your own system context. If it does not match
   the row, **stop** and say so.
3. You cannot see the effort setting. State the row's expected effort and **wait for a
   one-word confirmation** before proceeding.

**Sessions C and D build to a design they did not make.** The gate report (§1) and the
commit messages of session B are their only source for why the binder is shaped as it is.
Session B records the reasoning, not only the result. A Sonnet session that meets a design
question neither answers stops (§6).

**Branch:** one concern, one branch. The concern is named arguments on native calls.

Every change group in §2 ends as its own commit with the full suite green, and `/handoff`
is run after each. A new session resumes from the handoff file and `git log`, never from
memory of a previous conversation.

**This prompt was committed with D-447** at `docs/prompts/native-named-arguments.md`. Never
retrofit it. If the work diverges, the divergence goes in the landing entry.

---

## 1. Read-only investigation gate

*Session A — Opus, high.*

**No source edits until this gate is complete and reported.** Building, running the CLI and
running the suite are expected. Write the report to a **scratch file outside the working
tree**. In chat give the path and any STOP conditions that fired. End session A with
`/handoff`, naming the report's path.

D-446 and D-447 were written in planning sessions with no .NET SDK. Their "verified"
paragraphs were read from source, not run. **This gate is where they are run.** State what
you expect before each measurement, then measure, then explain every difference.

### 1.1 Confirm what the decisions read

- The checker reads an argument's name at four sites only: the script-function binder
  (`BindArguments`, `TryBindNamed`), `RejectNamedNativeArgs`, `RejectNamedMemberArgs` and
  `BindFormatAsExtraArgs`.
- `RejectNamedNativeArgs` guards namespace calls; `RejectNamedMemberArgs` guards primitive
  and named-type method calls. List every caller.
- `EmitReorderedArguments` emits a script-function call's arguments in parameter order
  whenever the call names an argument or omits a default.
- `NativeDefaultArgumentFill` fills trailing defaults for namespace and primitive calls.
  A named-type method call has no default fill at all: `CallExpr` carries resolved
  defaults for the other two paths only.
- `FunctionSignature` refuses a default beside a variadic tail.

### 1.2 Measure every call shape

For each callable kind below, write calls that pass a named argument (a) for a defaulted
parameter, (b) for a required parameter, (c) with a name the callee does not have, (d)
twice and (e) before a positional argument. Where the kind has no defaulted parameter, say
so and cover (b) to (e). Record for each call the full diagnostic output — code, line,
column, message — or, where it compiles, what it prints when run.

1. A script function.
2. A namespace native (`date.parse`, `math.pow`).
3. A primitive method (`s.padLeft`).
4. A named-type method (`d.addDays`), and an overloaded one (`Regex.replace`).
5. An array method with one parameter and one with two.
6. A map method with one parameter and one with two.
7. `print`, `exit` and `input`.
8. `formatAs.table`, function form and chained form.
9. A call through a lambda held in a binding, and through a function-typed parameter.
10. A method call on a nullable receiver through `?.`.

Present the result as one table. **D-447 expects kinds 5, 6, 7 and 9 to accept any name
without a diagnostic today.** Say plainly whether each does. For a two-parameter array or
map method, also show whether writing the two named arguments in the other order changes
what runs.

A call path that is none of these ten is a STOP (§6).

### 1.3 Inventory every defaulted parameter

List every parameter with a default in every registered signature — namespace, primitive
and named-type — with its name in code, its default and the name the same parameter has in
`grob-stdlib-reference.md` or `grob-type-registry.md`. State the count you expect from
reading the registrations before you run anything that counts. Mark every disagreement and
every defaulted parameter the documents do not show. This is R-55's input, and each name on
it freezes when this increment ships (D-446 Decision 3).

### 1.4 Design the binder

D-446 requires one convention. Propose, with the alternatives considered and why they lost:

- **One binding routine** that serves a `FnDecl` and a `FunctionSignature`. Say what it
  binds against — the smallest view of "a parameter list with names and has-default" that
  both can supply — and where it lives.
- **Messages.** Every script-function message for E0008 to E0011 is pinned and does not
  move. Propose the native wording: the same sentences, with the callee named as native
  arity messages name it today. Write each message out in full.
- **Overloads.** `SelectOverload` is given every argument's type. D-446 Decision 4 has the
  positional arguments choose the signature. Say what changes and show it is the same
  choice for every call that compiles today.
- **Lambda arguments.** D-443 types a lambda argument by the parameter at its position. A
  function-shaped parameter cannot have a default, so it cannot be named. Confirm that no
  named argument can shift the position a lambda is typed against.
- **Nullable receivers.** `ResolveNullableMemberAccessCall` is a separate arm. Say how it
  reaches the same binder.

### 1.5 Design the emission

The native keeps a fixed arity and receives a fully bound positional list (D-358, D-446).
Propose how the checker hands the compiler the bound order — one record on `CallExpr` that
serves all three native paths, in place of the two `Resolved…ParameterDefaults`
properties — and one emission helper. **A complete all-positional call must emit bytecode
byte-identical to today's.** Say which existing tests prove that and add what is missing.

### 1.6 Plan the probe corpus

`tests/fixtures/native-probes/named-arguments.expected.txt` and
`date-members.errors.expected.txt` pin today's refusals. D-446 changes the language under
them, which is the first of D-428's three grounds for changing a master. List every line
that will change, its new text and the clause of D-446 or D-447 that changes it. Any line
not on that list must be byte-identical at the end.

---

## 2. Scope

Seven change groups, each a green commit. Land them in this order. Each group that makes a
path bind changes the checker and the compiler together: a checker that binds while the
compiler still emits in source order is the miscompile R-20 was opened for.

1. *Session B — Opus, high.* **The shared binder.** The D-113 routine binds a `FnDecl` or
   a `FunctionSignature` (§1.4). Nothing new binds yet. Every existing test passes
   unchanged and every script-function message is byte-identical.
2. *Session B — Opus, high.* **Namespace natives bind.** The checker binds
   `ns.fn(...)` calls and the compiler emits the bound list (§1.5).
   `RejectNamedNativeArgs` is deleted.
3. *Session C — Sonnet, high.* **Primitive methods bind**, on the same binder and the same
   emission helper.
4. *Session C — Sonnet, high.* **Named-type methods bind**, with default fill, which this
   path has never had, and overloads chosen on positional arguments.
   `RejectNamedMemberArgs` is deleted. The nullable-receiver arm binds the same way.
5. *Session C — Sonnet, high.* **Calls with no signature refuse** (D-447 Decision 2).
   Every kind §1.2 found accepting a named argument silently — array methods, map methods,
   `print`, `exit`, `input` and calls through a function value, as measured — reports
   E0011. `formatAs.table`'s `columns:` is untouched.
6. *Session D — Sonnet, medium.* **The parameter-name drift check** (R-55), in
   `tooling/Grob.DriftCheck`, with its consistency test. Any name §1.3 found in
   disagreement is settled first: see §6.
7. *Session D — Sonnet, medium.* **Record.** The probe masters, per §1.6. The wiki and the
   skills under `.claude/skills/` that describe calling a native, each corrected in this
   commit, which is the one that makes it true. The landing entry. R-20 and R-55 moved to
   the register's Closed Items table.

### Out of scope — report, do not fix

- **Evaluation order** (OQ-028). `EmitReorderedArguments` keeps its order and natives match
  it. Do not make either evaluate in the order written.
- **Signatures for array methods, map methods and the built-ins** (R-56). Group 5 makes
  them refuse; it does not make them bind.
- **`formatAs.table`'s `columns:`** (D-345).
- **`fs`, `File` and `IFileSystem`.** No part of D-445 is built here.
- **A new error code, opcode, token, AST node or grammar production.**
- **The wording of any script-function diagnostic.**

**D-420's stopping rule applies.** A finding goes to the register, not into the run, unless
it blocks this increment or the `fs` increment.

---

## 3. Implementation notes

**One binder, not two.** If a second copy of the binding loop appears for natives, the
design is wrong. The point of D-446 is that there is nothing native-specific to bind.

**Only defaulted parameters can be named.** `fs.list(dir: "x")` and `d.addDays(days: 1)`
are E0009, not bound. That is D-113, and it is why a required parameter's name stays
documentation and a defaulted one's becomes contract.

**Skip and fill.** `m(a, c: x)` against `m(a, b = 1, c = 2)` reaches the native as
`m(a, 1, x)`. No registered native has two defaults yet; `File.moveTo` will be the first.
Prove it on a native registered by the test, on each of the three paths and through the
public registrar as a third-party plugin would register it.

**Defaults are constants.** A native default is a `GrobValue` scalar on the signature
(D-358). Emit it as a constant. Nothing is evaluated at the call site for it.

**Evaluation order follows script functions.** Add one test that a script function and a
native given the same reordered call evaluate their arguments in the same order, and put
OQ-028 in its comment, so whoever resolves that question finds the test.

**Complexity.** The checker's call arms sit near the S3776 bar. Extract guard-clause
helpers; do not suppress.

**Corrections land with the truth.** A wiki page or skill describing named arguments on
natives is edited in group 7, not before.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code. Quarantine with a
documented reason before weakening a test. A test may be updated to assert new correct
behaviour where D-446 or D-447 changes that behaviour; name the clause in the commit.

Required:

- **The convention on each native path**: for namespace, primitive and named-type calls, a
  named argument binds; named arguments in either order bind the same; E0008, E0009, E0010
  and E0011 each fire with the message §1.4 fixed; E0003 and E0004 are checked on the bound
  set and suppressed by a binding error.
- **Skip and fill** on each path, and through a plugin registered on the public surface.
- **Overloads**: positional arguments choose; a name only the other signature declares is
  E0011; every overloaded call in the suite today chooses as it did.
- **Named-type default fill**: a positional call that omits a trailing default now works on
  a named-type method.
- **Emission**: a complete all-positional call to each path emits what it emitted before.
- **Refusal** (group 5): each kind §1.2 found silent reports E0011, and a two-parameter
  array or map call can no longer be written reordered.
- **Script functions unchanged**: the existing binder tests pass without edits.
- **The probe corpus**: changed exactly on §1.6's lines and nowhere else.
- **The drift check**: fails on a renamed defaulted parameter, on a reference that spells
  one differently and on a defaulted parameter the documents do not show.
- **All eleven validation scripts** produce the output they did.

**Mutation-verify** three guards. For each: remove it, **predict the exact failure
signature before running**, confirm it, restore.

1. The E0009 check on a native call.
2. The fill of a skipped default.
3. The refusal on an array method.

---

## 5. Deliverables

- Seven change groups per §2, each its own green commit.
- Error-code count unchanged; take the figure from the live registry. D-316 gate green.
- **R-20's criterion**, each clause reported against a named test.
- **R-55's criterion**, with the drift check's output on the tree as shipped.
- §1.2's table, before and after.
- The probe-corpus diff, line by line, each line with its clause.
- The benchmark gate's result, with no baseline updated. R-24 records the allocation gate
  as already red; report this increment's figures against R-24's and say whether it moved
  them.
- Coverage on new code at or above 90% line and branch. Report each assembly's figure.
- A landing entry in the decisions log, in three-location lockstep, recording what was
  built and every divergence from this prompt. **Take the D-number from the live tail of
  the merged log**, never from memory or this prompt.
- R-20 and R-55 moved to Closed Items with that D-number. R-56 stays open.
- The PR is expected to change about forty files. If it would pass one hundred, stop and
  propose a split before opening it.
- Push the branch and open the PR as `/close-session` describes. Chris merges.

---

## 6. STOP conditions

Stop and report, do not proceed:

- The session-start check finds the running model does not match the session's row.
- A Sonnet session meets a design question that neither the gate report nor session B's
  commit messages answer. Bring it back to an Opus session.
- §1.1 finds a statement in D-446 or D-447 that the code does not bear out.
- §1.2 finds a call path that is none of its ten kinds, or finds kinds 5, 6, 7 or 9
  behaving in a way D-447 Decision 2 does not cover.
- §1.3 finds a defaulted parameter whose name in code disagrees with the documents. The
  name freezes with this increment, so which one is right is Chris's call. Report the list
  and wait.
- The binder cannot be shared without changing a script-function message.
- An existing overloaded method cannot be chosen on its positional arguments alone.
- A complete all-positional native call cannot be kept byte-identical in bytecode.
- Binding needs a new error code, opcode, token, AST node or grammar production.
- Making something work needs the evaluation order to change.
- The benchmark gate fails beyond R-24's recorded state and the only fix is a baseline
  update.

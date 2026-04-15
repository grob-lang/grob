# SharpBASIC — Retrospective

> Work in progress. Captured incrementally during retrospective session, April 2026.
> Sections 1 and 2 complete. Sections 3–9 and Grob inputs summary to follow.
> Output file: `SharpBASIC___Retrospective.md`

-----

## One-Paragraph Summary

I am incredibly proud of SharpBASIC. It was something I never thought I could achieve,
coming from a background like mine — a mechanic with no formal CS training. It taught
me, or should I say reminded me, that I can do anything with enough time, energy, and
the right kind of help. I sat back when it was almost done and asked Copilot to write
some samples. The start of the prompt was “you are an expert SharpBASIC developer.” I
just sat slightly stunned. I could not believe I had done it. Seeing even Hello World
run from a command line was mind-blowing. What it’s left for Grob is a desire to make
it real — not just a side project, but something real that people can use. I can’t
begin to imagine what it will feel like if Grob gains even one real user.

-----

## Section 1 — The Pipeline

### Actual pipeline shipped

**Lexer → Parser → Evaluator → REPL/Runner**

The Semantic Analyser was dropped. It was scaffolded as a C# project but never
implemented — when reviewed at the end, it was empty and everything was working
correctly without it. Most of what a semantic analyser would have done had been
absorbed into the Evaluator naturally. This was not a deliberate cut during planning;
it was a post-facto recognition that the layer wasn’t needed at this scale.

**Retrospective note:** This is a data point for Grob. A tree-walking evaluator for a
dynamically typed language can absorb semantic checks without a dedicated pass.
A statically typed compiled language (Grob) will need an explicit type-checking pass
between the parser and the compiler — that layer earns its place when types are
resolved at compile time, not at runtime.

-----

### Q1.1 — Which stage took longest to get right?

The Parser and Evaluator were at a similar difficulty level overall, but the specific
hard point was **disambiguating function calls from array access**. This was a two-site
problem: the lexer had to generate the right tokens, and the evaluator had to act on
them correctly in `EvaluateCallExpression`, which accumulated significant branching.
Neither end was solely responsible — it required both ends to be right simultaneously.

The Lexer also had harder-than-expected moments: allowing underscores in identifiers
(between words only) and getting comment handling correct were both fiddly to get right.

-----

### Q1.2 — Which stage surprised you?

**Phase 4 (Pratt parsing) was easier than expected** — intuitive once explained well.
The theory alone would have been much harder; the explanation made the concept land
cleanly. This is a direct endorsement of the “concept first, then implementation”
approach used throughout.

**Call frames and symbol tables had bumps** — not from the concepts themselves but
from not thinking ahead about local scope in SUBs and FUNCTIONs. The surprise was
scope, not the call stack machinery.

-----

### Q1.3 — Coupling problems between stages?

No significant coupling problems. The pipeline had well-defined boundaries and stages
did not bleed into each other in ways that caused architectural problems.

-----

### Q1.4 — Decisions: wish I’d made earlier / wish I’d thought harder about

**Wish I’d made earlier:**

- Full line and column tracking from Phase 1 (it was added but retrofitting it was
  avoidable work)
- Full diagnostics infrastructure from the start — same reason

**Wish I’d thought harder about before committing:**

- Local vs global scope design — the friction in Phase 7 came from not resolving
  this fully before implementation began
- The array/function call disambiguation — the branching in `EvaluateCallExpression`
  was a consequence of not designing the token/syntax boundary carefully enough upfront
- The absence of `SELECT CASE` from the original 10 phases — it was needed and had
  to be added late; nested IF/ELSE at scale is unmaintainable

**Note:** The absence of file I/O and positional text output were felt during The Sunken
Crown — SharpBASIC showed the language needed them. This is why Grob has `fs` as a
core module. These weren’t mistakes in SharpBASIC’s scope; they were evidence that
informed Grob’s design.

-----

### Q1.5 — If you rebuilt the pipeline today, what would you change?

Nothing fundamental. It works for what it needs to be. Some code could be optimised,
but that would reduce its value as a learning artefact. **Grob won’t have that
constraint — it’s a real language from day one.**

-----

## Section 2 — The AST

### Q2.1 — Happy with `abstract record` for AST nodes?

Yes. `abstract record` worked well — lightweight, equality built in, easy to understand.
For an AST, the record hierarchy is the right choice and would be kept in any rebuild.

The visitor pattern alternative was considered. The honest comparison: visitor buys
open/closed extensibility (add a new operation without touching node classes) but costs
readability. For a project with one primary “operation” (evaluation), switch expressions
on records are cleaner and more obvious. **Visitor makes more sense when multiple passes
walk the same AST** — type checker, optimiser, code generator. Grob will have all three.
Worth revisiting the pattern choice when Grob’s pipeline is designed.

-----

### Q2.2 — Did the AST get unwieldy as the language grew?

No. Nodes remained focused and single-purpose throughout. Pattern matching held up
cleanly — no strain even at the final size of the language. Even at twice the number
of node types it would not feel unwieldy. The design scaled well.

-----

### Q2.3 — How did the switch-expression dispatch hold up through the late additions?

Still clean at the end. `SELECT CASE`, 2D arrays, `SET GLOBAL`, and `CONST` were all
added without architectural stress. Adding new cases was straightforward.

**One maintainability improvement identified for next time:** Split the evaluator into
several `partial class` files for physical separation of concerns, while keeping the
same namespace. Same architecture, better organisation. Not a design change — a
housekeeping improvement.

-----

### Q2.4 — Values in AST nodes vs separate constant pool

Values living inside AST nodes caused friction — type casting and jumping through hoops
to extract values at runtime. The evaluator frequently had to interrogate a node to
discover what was inside it.

**The constant pool approach (Grob) is going to be better.** Values have a known
representation at a known index. The VM fetches by index. The evaluator never has to
guess what a node contains — the type checker has already resolved it. The friction
felt in SharpBASIC is exactly what the constant pool eliminates.

-----

## Section 3 — Scope and the Symbol Table

### Q3.1 — Did the parent chain from Phase 3 pay off in Phase 7?

Yes — Phase 3 was the right place for it. The full mechanism should be in from the
start even if it isn’t used immediately. It held up until the requirement for mutating
global scope arrived, at which point `SET GLOBAL` had to be retrofitted. The lesson:
the chain was right early, but the global mutation story wasn’t fully thought through
before it was needed.

-----

### Q3.2 — Was `LET` local / `SET GLOBAL` the right design?

Acceptable for BASIC, but jarring from a C# perspective. The `SET GLOBAL` rule feels
like a BASIC-ism rather than a considered modern language design decision.

**The Grob question this raises:** In a C-style language, what do users expect?
The Go model is the natural answer: `:=` always declares in the current (local) scope;
`=` reassigns by walking the parent chain to find the name wherever it lives. No
special keyword needed for global mutation — the scoping rules handle it naturally.
This is an open question for Grob’s scoping design session.

-----

### Q3.3 — Did `SET GLOBAL` in The Sunken Crown feel correct or like a workaround?

See Q3.2. The volume of `SET GLOBAL` calls required to maintain game state (stamina,
inventory, room, turn counter, overburdened flag) felt like working around a scope
model that hadn’t been designed with real programs in mind. It worked, but it was
friction rather than flow.

-----

### Q3.4 — Explicit call frames — well understood going into Grob?

The concept is understood but wants confirming before implementation.

**The distinction:** In SharpBASIC, C# manages the call stack implicitly — the
recursive `Evaluate()` call *is* the call frame, living on the .NET stack. When a
SUB returns, .NET unwinds it. The frame is invisible.

In Grob’s VM, there is no recursive evaluation. The VM is a flat `while` loop.
When a function is called, the VM manually pushes a `CallFrame` onto a fixed array,
recording the return address in the instruction stream and the base of this call’s
locals on the value stack. Return pops the frame and resumes from the saved address.

**SharpBASIC borrows C#’s stack implicitly. Grob manages its own stack explicitly.**
Same concept, completely different mechanism. clox Chapter 24 makes this concrete.

-----

## Section 4 — TDD in Practice

### Q4.1 — Did tests-before-implementation hold throughout?

Yes, and it worked well. The gap was coverage breadth — edge cases and failure paths
were underrepresented. The happy path was well tested; the boundaries less so.

-----

### Q4.2 — Which phase benefited most from TDD?

The **Parser** and the **Evaluator** both benefited significantly. The parser tests
were the first place to verify that lexing was producing the right output. The
evaluator tests — particularly with the helper infrastructure — became full
integration tests: source in, correct result out. That combination caught real
problems early.

-----

### Q4.3 — Did the “introduce it early” decisions pay off?

**Partially.** Line/column tracking from Phase 1 was the right call. But the
diagnostic model (Phase 9) should have been the error model from day one.
Moving from `RuntimeError` to the full `Diagnostic` model in Phase 9 was
significant retrofitting work that was entirely avoidable.

**The Grob rule from this:** Full exception/diagnostic infrastructure goes in
at Phase 1, not Phase 9. The shape of error reporting should be a day-one
architecture decision, not a late-phase upgrade.

-----

### Q4.4 — Were there places where writing the test first felt forced?

Token type tests felt pointless — essentially checking that an enum had the right
value. Otherwise TDD felt natural throughout. The compiler pipeline is among the
most naturally testable code you can write; input in, specific output out.

-----

### Q4.5 — What would you test differently in Grob?

More edge cases and failure paths throughout. For Grob specifically, the most
valuable tests will be **compiler output tests** — given this source, does the
compiler emit the correct bytecode sequence? This is where bugs will live.

The VM loop is a “dumb” fetch-decode-execute cycle — once verified on simple cases,
it can be trusted. The compiler is where intent becomes instruction, and that
translation is where errors hide. Test the compiler exhaustively; trust the loop
once it’s confirmed correct.

-----

## Section 5 — The Hard Phases

### Q5.1 — Was the difficulty assessment accurate?

Phase 4 and Phase 7 were the predicted walls — but neither hit as hard as expected,
and that was deliberate. Both were discussed at length before implementation began.
The teaching approach — preparing for what was coming before the code started — meant
neither phase arrived as a surprise. The warnings prompted deeper preparation, which
changed the experience entirely.

**The lesson:** Difficulty is partly a function of preparation. Flagging hard phases
early and working through the concepts before touching the keyboard is a legitimate
strategy, not a shortcut.

-----

### Q5.2 — What was the actual difficulty in Phase 4?

Both concept and implementation, though the concept was covered well. Implementation
help was needed at points, and later when retrofitting `OR`, `AND` and similar
operators, the correct binding powers weren’t instinctive — the right precedence
range had to be reasoned through rather than felt.

-----

### Q5.3 — Did Pratt parsing pay for itself by Phase 5?

Yes. Once in place it was only revisited once to add additional operators. Phase 5
(IF/THEN/ELSE) required no new expression machinery — comparison operators slotted
straight in with their binding powers already defined. The setup cost was real; the
payoff was not having to retrofit expression handling in every subsequent phase.

-----

### Q5.4 — What was the actual difficulty in Phase 7?

The concept initially. The idea of calling a SUB or FUNCTION felt hard until the
lightbulb moment: **they are just mini programs**. After that, scope took some thought
but came together quickly — with the exception of the global mutation problem, which
Copilot also failed to anticipate. A reminder that AI tools reason about the code
in front of them, not about design decisions that haven’t been written down yet.

-----

### Q5.5 — What do you know now that you wish you’d known going in?

Don’t be scared of the concept. The answer is often simpler than it looks from the
outside. Call frames and Pratt parsing both seemed formidable in theory and became
manageable once the implementation was underway. Neither is now frightening — not
expert-level, but understood and demystified.

-----

-----

## Section 6 — What SharpBASIC Taught That No Blog Post Would Have

### Q6.1 — What did you understand after building SharpBASIC that you couldn’t before?

That the answers are often less clever than you expect them to be. Before starting,
tokenisation seemed like it must involve some sophisticated algorithm — not a
character-by-character while loop. That was mind-blowing. The same applied to every
stage: what felt like insurmountable, rarified-air computer science turned out to be
loops and good basic design.

Reading Nystrom’s clox chapters now, nothing is lost. The concepts are legible.
Bytecode is no longer arcane — it’s understood. And bigger languages no longer feel
magical — they’re SharpBASIC or clox with layers of complexity on top. The opacity
is gone.

-----

### Q6.2 — What was the biggest conceptual shift?

The teaching approach kept things well-prepared, so few things arrived as genuine
surprises. The one thing that didn’t always feel instinctive was when to advance and
when to peek in the parser. The rule — peek when making a decision without committing,
advance when committed — becomes instinct only after it’s caused a problem. It’s one
of those things you can be told but only really learn by getting it wrong once.

-----

### Q6.3 — Tree-walking evaluator vs bytecode VM — what do you appreciate about each?

Building the tree-walking evaluator makes the speed argument for bytecode clear. The
VM will be faster — but that speed comes at the cost of an obvious path through the
code. Debugging a tree-walker is intuitive: follow the tree, watch the recursion.
Debugging a VM means reasoning about an instruction stream and a stack. The tradeoff
is real and now felt, not just understood in theory.

-----

### Q6.4 — Was tree-walking first the right call?

Yes. It taught the principles using familiar constructs — C# recursion, records,
pattern matching — which meant the focus could stay on compiler theory rather than
on unfamiliar implementation mechanics. That was the point. Grob gets a developer
who understands what a compiler is doing, not one who learned C# and compiler
construction simultaneously.

-----

### Q6.5 — Anything to carry forward unchanged into Grob?

No. Grob is something else entirely. Even the AI tooling relationship changes —
SharpBASIC used Claude and Copilot as guides and teachers. For Grob, the relationship
is a partnership: a senior to a junior in this arena. Still guiding, but collaborative
in a different way. The project is different; the developer going into it is different.

-----

## Section 7 — Grob-Specific Inputs

### Q7.1 — Hardest part of adding a type checker? *(feeds OQ-003)*

The mechanics of tracking primitives feel approachable — type checking a generic
primitive is workable. The harder problem is objects and structs: how the type checker
handles user-defined structural types, what it knows about their shape at compile time,
and how that interacts with things like JSON deserialisation. That’s where the
complexity will live.

-----

### Q7.2 — Design the error model differently from the start? *(feeds OQ-004)*

Yes. The lesson from SharpBASIC is specific: during a full playtest of The Sunken
Crown, the game appeared to be running correctly — then on exit dumped 30 diagnostics
for LET used where SET GLOBAL was required. The evaluator had been silently
accumulating errors while continuing to run.

**The correct model, locked for Grob:**

- The compiler/type checker collects all issues and reports them fully before
  execution begins — don’t stop at the first error, gather them all.
- The VM/evaluator stops on the first runtime error — don’t continue executing
  into undefined state.

Two different failure modes. Two different strategies. And the full diagnostic
infrastructure goes in at Phase 1, not Phase 9. Never a cut-down version first.

-----

### Q7.3 — Does SharpBASIC’s stdlib inform Grob’s native function registration?

Yes — the additive approach is correct and carries forward. The distinction is:
the core stdlib is additive and ships with the runtime. Plugins are not for core —
they are for extending the language beyond what ships. The plugin system is an
ecosystem mechanism, not a substitute for a well-designed standard library.

-----

### Q7.4 — The one thing that most directly changes how you’ll approach Grob’s compiler?

Better upfront planning of language needs. More design before implementation begins.
The 7Ps: Proper Prior Planning Prevents Pitifully Poor Performance. SharpBASIC
taught what questions to ask — about scope, about error handling, about what a real
program will actually need — before writing the first token. Grob gets the benefit
of all of that.

-----

## Section 8 — The Late Additions

### Q8.1 — Was `LET` local-only the right invariant?

Not wrong, but incomplete. The missing piece: if all variables had to be declared
before use, LET could safely walk the parent chain, with local shadowing global
naturally. SharpBASIC allowed undeclared variables, which made chain-walking unsafe.
Grob’s := declaration requirement makes it safe by design — the scope rules work
correctly because the declaration discipline makes them unambiguous.

-----

### Q8.2 — At what point did the language need `SELECT CASE`?

The Sunken Crown made it visible — any non-trivial program accumulates multi-layer
IF/ELSE structures that become hard to follow and error-prone (missing END IF, wrong
nesting). SELECT CASE earns its place the moment a real program needs it and the
alternative is clearly worse. A feature earns its place when writing without it is
visibly painful, not when it seems like a nice addition.

-----

### Q8.3 — Did `[rows][cols]` feel like a limitation or like self-consistency?

Natural. Square brackets for array access are an easy differentiator from function
call parentheses, and [][] is a logical extension. [r,c] would also have worked but
[][] felt right. The constraint imposed by the ambiguity produced a cleaner syntax
than the unconstrained alternative might have.

-----

### Q8.4 — One sentence on maintaining parallel arrays in The Sunken Crown

**Messy, wasteful, and slow.**

*This is the answer to OQ-002. Grob needs user-defined struct/record types.*

-----

### Q8.5 — Scope creep or the language completing itself?

Not creep. The late additions — CONST, SET GLOBAL, SELECT CASE, 2D arrays, the
string functions — were things that belonged in the language and weren’t spotted
early enough. Scope creep is adding things that don’t belong. These belonged; they
were just late discoveries.

File I/O would have been creep. The late additions were oversights, not additions.
The difference: creep changes what the language is. Completion fills in what it was
always supposed to be.

-----

## Section 9 — The One-Paragraph Summary

I am incredibly proud of SharpBASIC. It was something I never thought I could achieve,
coming from a background like mine — a mechanic with no formal CS training. It taught
me, or should I say reminded me, that I can do anything with enough time, energy, and
the right kind of help. I sat back when it was almost done and asked Copilot to write
some samples. The start of the prompt was “you are an expert SharpBASIC developer.” I
just sat slightly stunned. I could not believe I had done it. Seeing even Hello World
run from a command line was mind-blowing. What it’s left for Grob is a desire to make
it real — not just a side project, but something real that people can use. I can’t
begin to imagine what it will feel like if Grob gains even one real user.

-----

## Grob Inputs Summary

Every item below either changes or confirms a decision in the Grob decisions log.
OQ numbers reference open questions in `Grob___Decisions___Context_Log.md`.

-----

### Pipeline & Architecture

- Drop the semantic analyser placeholder. At SharpBASIC’s scale it added nothing.
  For Grob — a statically typed compiled language — the type-checking pass earns
  its place because types are resolved at compile time. It will not be empty.
- Visitor pattern for the AST. Grob will have multiple passes over the same AST
  (type checker, optimiser, compiler). The visitor pattern becomes justified where
  SharpBASIC’s switch expressions were sufficient. Revisit when Grob’s pipeline
  is designed.
- Partial classes for the compiler. Same namespace, physical separation by concern.
  Maintainability improvement with no architectural cost.

### Error Handling *(feeds OQ-004)*

- Full diagnostic infrastructure from Phase 1. Never a cut-down version first.
- Two-mode error strategy: compiler/type checker collects all issues before
  execution; VM stops on first runtime error.
- The Sunken Crown failure mode is the specification: silent accumulation of 30
  errors while the program appeared to run correctly is the exact failure mode
  to design against.

### Scope & Variables

- := declaration requirement makes scope rules safe. Grob’s mandatory declaration
  before use means the VM can walk the scope chain without ambiguity. Local shadows
  global naturally. No SET GLOBAL equivalent needed.
- Global mutation design. The Go model (`:=` declares local, `=` reassigns by
  walking the chain) is the right answer for a C-style language. Resolve in Grob’s
  scoping design session.

### Type System *(feeds OQ-003)*

- Primitives are approachable. Type checking primitive types feels workable.
- Objects and structs are the hard part. How the type checker handles user-defined
  structural types — their shape, their fields, their interaction with generics
  and JSON — is where the complexity lives.

### Struct / Record Types *(resolves OQ-002)*

- Grob needs user-defined struct/record types. Evidence: The Sunken Crown’s parallel
  arrays were “messy, wasteful, and slow.” The absence of a type keyword was the
  single biggest language limitation revealed by writing a real program. OQ-002
  tentative direction (yes, struct types) is confirmed.

### Standard Library & Plugins

- Additive stdlib is correct. Core ships with the runtime, grows additively.
- Plugins are not for core. The plugin system is an ecosystem mechanism for
  extending beyond what ships — not a substitute for a well-designed stdlib.
- The absence of file I/O in SharpBASIC directly informed Grob’s fs module as
  a core stdlib module, not a plugin. This was the right call.

### Compiler Testing

- Test the compiler outputs exhaustively. Given source, assert correct bytecode.
  This is where bugs will live in Grob — not in the VM loop.
- The VM loop can be trusted once verified on simple cases. It’s a dumb
  fetch-decode-execute cycle. The compiler is where intent becomes instruction.
- More edge cases and failure paths throughout. The happy path was well tested
  in SharpBASIC; the boundaries less so.

### Design Process

- The 7Ps. More upfront language design before implementation begins. SharpBASIC
  taught what questions to ask. Grob should have answers to scope, error handling,
  and real-program requirements before Phase 1 starts.
- Features earn their place when writing without them is visibly painful. SELECT
  CASE earned it. File I/O would have been creep. Know the difference before the
  spec is written.
- Real programs reveal language gaps that toy programs hide. The Sunken Crown was
  the most valuable design tool in the project. Grob needs an equivalent real-program
  target defined early — before implementation, not after.

### Tooling & Relationship

- The Copilot/Claude relationship changes for Grob. SharpBASIC used AI as guide
  and teacher. Grob uses it as a partner — senior to junior in compiler construction,
  but collaborative. The Copilot instructions file for Grob should be written to
  reflect that shift.

-----

*Retrospective complete. Session: April 2026.*
*Every section answered. Grob inputs compiled.*
*This document is the design input for Grob Phase 1 planning.*
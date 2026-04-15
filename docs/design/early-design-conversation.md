#  Grob — Early Design Conversation

### SharpBASIC Claude Project · Early Sketch Session

-----

**CHRIS:** Phase 1 is in progress. I want to do some loose early thinking about Language 2 — nothing detailed, just sketching the edges. Ask me the questions you had about style, purpose, typing and the one thing.

-----

**CLAUDE:** Good timing — you’re far enough into SharpBASIC to have instincts but not so far that Language 2 feels real yet. Sketch mode is exactly right.

Four questions. Answer as loosely as you like.

**1. Style — what does it feel like to write?**
SharpBASIC feels like BASIC: verbose keywords, `END IF`, `NEXT i`. Language 2 — are you drawn toward that same readable-English style, or do you want something terser and more symbolic? Think: does it feel more like VB6 or more like C/Go?

**2. Purpose — what is it *for*?**
SharpBASIC is for learning. Language 2 will also be a learning project, but does it have a character beyond that? A scripting language you’d actually embed somewhere, a systems-adjacent thing, a toy you’d hand to someone non-technical — or just “the harder version of SharpBASIC”?

**3. Typing — how strict?**
SharpBASIC is dynamically typed (values carry their type at runtime). For Language 2, where does your gut land — stay dynamic, go fully static with declared types, or something in between like optional/inferred typing?

**4. The one thing.**
Every language has one thing that makes it interesting to build — the technical centrepiece that everything else exists around. For SharpBASIC it’s the tree-walking evaluator and scope management. What’s the one thing you *want* to build in Language 2 that SharpBASIC doesn’t give you? Don’t overthink it — first instinct.

-----

**CHRIS:** A few thoughts. I have a name: Grob it doesn’t mean anything it was a childhood nickname but I like it.

1: I grew up on VB6 but I have to say I do generally prefer curly braces, semicolon delimited. I have also done quite a lot of Arduino programming so that style is early C is nice.
2: I want a functioning scripting language that can be used for automation and general tasks.
3: static typed this is still a learning experience for me.
4: instinctive, logical, natural - not just one thing but I feel this set outlines my philosophy

-----

**CLAUDE:** Grob. That’s a good name for a language — short, memorable, slightly gruff. It’ll look good in a README.

Let me reflect back what I’m hearing, because the four answers together are telling a coherent story.

**The picture so far:** Grob is a statically-typed, C-style scripting language for automation and general tasks. Curly braces, semicolons, explicit types. Something that feels like early C or Arduino — lean, purposeful, no ceremony.

The philosophy answer is the most interesting one. “Instinctive, logical, natural” isn’t a feature — it’s a design constraint. It means the language shouldn’t surprise you. When you guess how something works, you should be right. That’s actually hard to achieve and worth taking seriously as a north star.

A few things that fall out of this combination naturally — not decisions yet, just observations:

The C-style syntax plus static typing means you’re building a real type system for the first time. That’s where the hard learning is in Language 2 — type checking at parse/semantic analysis time rather than failing at runtime. That’s a meaningful step up from SharpBASIC.

“Automation and scripting” is interesting in tension with “statically typed” — most scripting languages are dynamic precisely because static types add friction for quick tasks. You’ll want to think about how verbose the type declarations are. Go-style inference (`x := 42`) keeps it feeling scripty while staying static. Something to sit with.

The Arduino influence is worth holding onto. Arduino sketches have a shape: small, task-focused, no boilerplate to get started. That’s a good Grob program in your head even if you can’t describe it yet.

One question back at you — when you imagine someone writing a Grob script, what are they automating? File manipulation, data transformation, something else? That’ll shape what the standard library needs to be.

-----

**CHRIS:** Yes you’re right about the dichotomy. Arduino is a good plan. What about aiming this language as an alternative way to write code for Arduino. I know do example Scratch have done this. So could Grob be a new way to write Arduino code? What’s the unit assessment of that? Or would a general purpose language be a better learning experience?

-----

**CLAUDE:** Honest assessment: Arduino-targeting is a fascinating idea but it’s the wrong move for Language 2.

**The core problem** is that Arduino isn’t a runtime you control — it’s a compilation target. To run Grob on Arduino you’d need to either transpile to C++ (like Scratch/MakeCode do) or compile to machine code directly. That means Language 2’s centrepiece becomes code generation, not interpretation. You’d spend most of your time fighting AVR toolchains and memory constraints rather than learning language construction. It’s a different discipline, and a brutal one.

The projects that do this (MakeCode, Scratch for Arduino) are backed by large teams and years of work. It’s not impossible but it’s a poor fit for a solo learning project.

**What you’d lose:** The bytecode VM is the natural centrepiece of Language 2 — it’s what Crafting Interpreters Part III teaches, it’s the logical next step from SharpBASIC’s tree-walker, and it’s where the deep learning is. Targeting Arduino would push you away from that entirely.

**What’s worth keeping:** The Arduino aesthetic. That instinct for lean, purposeful, no-ceremony code is a great design constraint for Grob as a general scripting language. You don’t need to target the hardware to honour the philosophy.

**The better version of this idea:** Write Grob as a general-purpose scripting language with that Arduino-influenced feel — terse, explicit, task-focused. Then if after completing it you want to explore transpilation as a separate experiment, that’s a clean project of its own.

General purpose language is the better learning experience. The bytecode VM alone is worth it.

-----

**CHRIS:** Ok that sounds good. Do we need to start a log for the project of these decisions

-----

**CLAUDE:** Yes, but not yet.

You’re still in sketch mode — none of what we’ve discussed is firm enough to log. A decisions log records *settled* decisions with reasoning, and right now Grob is just a shape in the fog. Logging too early gives you false confidence that things are decided when they’re not.

The right trigger is when SharpBASIC Phase 1 is done and you write the brief retrospective note that the spec calls for. At that point Grob gets its own Claude Project, its own decisions log, and we do a proper first session to harden what you’ve sketched today.

What’s worth doing right now is one line in the SharpBASIC decisions log under the Language 2 deferred ideas section — just enough to capture the Grob name and the direction so it doesn’t get lost:

> **Grob** — C-style, statically typed, general-purpose scripting language. Curly braces, semicolons, type inference under consideration. Bytecode VM as centrepiece. Arduino aesthetic (lean, purposeful, no ceremony) as design north star. Arduino hardware targeting explicitly ruled out.

That’s it. Everything else waits until SharpBASIC Phase 10 is done and the retrospective is written.

-----

**CHRIS:** I was thinking about an aim for Grob. Let’s say success and mvp is being able to create a simple console based non-scientific calculator.

I would also like to look at allowing imports from modules both ‘system’ for example Grob.Maths or user built MyApp.Domain.

What do you think?

-----

**CLAUDE:** Both good instincts. Quick assessment of each.

**The calculator MVP** is a perfect target. It’s concrete, testable, and it exercises everything that matters — expressions, operator precedence, static types, functions, basic I/O. If you can write a working calculator in Grob, the language is real. Keep that as the north star.

**The module system** — yes, but with a caveat. It’s the right feature for a scripting language aimed at automation, and `Grob.Maths` / `MyApp.Domain` is a clean mental model. The caveat is that module resolution is surprisingly complex to get right. You’re making decisions about file system layout, import syntax, circular dependency handling, and namespace scoping all at once. It’s absolutely in scope for Grob — it was deferred from SharpBASIC for good reason — just flag it mentally as a late-phase feature, not an early one. Design the language so modules slot in cleanly, but don’t let the module system drive the early architecture.

One thing these two aims together tell you: Grob needs a decent standard library. The calculator implies `Grob.Maths` exists and is useful. That’s a good forcing function for what “standard library” actually means in a statically typed language — which is a more interesting design problem than SharpBASIC’s Phase 10 stdlib.

Same answer as before on logging — one line in the SharpBASIC deferred ideas table:

> Calculator (console, non-scientific) as MVP target. Module system: `Grob.Maths` (system) and user namespaces e.g. `MyApp.Domain`.

Then park it until SharpBASIC is done.

-----

**CHRIS:** Can you create and managed the deferred ideas table for me. It should be added to this project

-----

**CLAUDE:** I can’t write directly to your project files — I don’t have that capability. But I can give you the exact content to paste in.

Add this to the **Language 2 — Deferred Ideas Log** section in your decisions log, replacing the existing table:

```markdown
## Grob — Language 2 Deferred Ideas Log

> Early sketch notes captured during SharpBASIC Phase 1.
> Nothing here is a firm decision. Harden after SharpBASIC retrospective.

### Name
**Grob** — childhood nickname, no meaning, good name for a language.

### Shape (sketch only)
| Concern       | Direction                                                                 |
|---------------|---------------------------------------------------------------------------|
| Style         | C-style — curly braces, semicolons, Arduino-influenced aesthetic          |
| Purpose       | General-purpose scripting language for automation and everyday tasks      |
| Typing        | Static, with type inference under consideration (Go-style `x := 42`)     |
| Philosophy    | Instinctive, logical, natural — language should not surprise you          |
| VM strategy   | Bytecode VM as centrepiece (Crafting Interpreters Part III)               |
| MVP target    | Console-based non-scientific calculator                                   |

### Deferred Features
| Feature                | Notes                                                                     |
|------------------------|---------------------------------------------------------------------------|
| Language-level File I/O| `open()`, `read()`, `write()`, `close()` — C-style API                   |
| Compile to executable  | Transpile to C# via CSharpEmitter, compile with Roslyn                   |
| Bytecode VM            | Stack-based VM, informed by clox from Crafting Interpreters               |
| Static typing          | Explicit declarations, compile-time type checking                         |
| Module / import system | `import Grob.Maths` (system) and user namespaces e.g. `MyApp.Domain`     |
| VS Code extension      | TextMate grammar for highlighting, LSP for errors                         |
| Arduino targeting      | Ruled out — transpilation to C++ is a different discipline entirely       |
| Standard library       | Grob.Maths and others — design informed by calculator MVP requirements    |

### Decisions Log
| Date | Decision                                                                  |
|------|---------------------------------------------------------------------------|
| Feb 2026 | Arduino hardware targeting ruled out — compilation target, not interpreter|
| Feb 2026 | General-purpose scripting chosen over domain-specific language            |
| Feb 2026 | Calculator MVP set as success criterion                                   |
| Feb 2026 | Module system in scope — late phase, not early architecture driver        |
```

When you want to add something during SharpBASIC, just tell me and I’ll give you the updated row to paste in.

-----

**CHRIS:** We don’t seem to have a decisions log file yet. Should I have created that already and add or the project?

-----

**CLAUDE:** Looking at your project files, you do already have one — it’s the file called **SharpBASIC — Project Decisions & Context Log.md** that’s attached to this project. It already has a Language 2 deferred ideas table at the bottom.

What I gave you just now should replace that existing table in that file. So the action is:

1. Open `SharpBASIC — Project Decisions & Context Log.md`
2. Find the **Language 2 — Deferred Ideas Log** section near the bottom
3. Replace the existing table with the content I gave you

That’s the only file you need. No new file required — the decisions log already exists and is already in the project.

-----

**CHRIS:** How do I update a project file do I just edit the file in it’s original location or do I have to remove and re add it

-----

**CLAUDE:** You just edit it in its original location and then re-upload it to the project. Claude Projects don’t sync live with your file system — the file you added was a snapshot at upload time.

The workflow is:

1. Edit the file locally in VS Code or Rider
2. Go to the Claude Project
3. Remove the old version of the file from the project
4. Add the updated file

It’s a minor friction but it’s worth doing whenever a significant decision is made. Think of it as a deliberate act — you’re updating the project’s memory, not just saving a file.

-----

**CHRIS:** What else should we discuss about the early shape of Grob

-----

**CLAUDE:** Honestly? Nothing yet.

You have enough to preserve the idea and not enough to make good decisions. What you’ve captured is solid — name, aesthetic, philosophy, MVP, VM strategy, module system flagged. That’s the right amount of fog for a project that doesn’t start until SharpBASIC Phase 10.

The risk of discussing more now is that you make decisions without the experience that SharpBASIC will give you. Phase 7 alone — call stack, scope management, closures — will change how you think about static typing and the module system. The retrospective exists precisely to feed those lessons into Grob’s design.

The one thing worth keeping in the back of your mind as you build SharpBASIC is this: **notice what annoys you**. Every time the tree-walker feels slow, every time dynamic typing causes a test to fail in an unexpected way, every time you wish the language had caught something earlier — that’s Grob taking notes. The retrospective will surface all of it.

Go build the lexer. Grob can wait.

-----

**CHRIS:** I am wondering what kind of usp we can give Grob? We have some outline ideas. Doesn’t have to be concrete.

-----

**CLAUDE:** Good question to sit with. Let me throw some angles at you based on what you’ve already said, and see what resonates.

**The philosophy is already a USP candidate.** “Instinctive, logical, natural — the language shouldn’t surprise you.” That’s not a feature, it’s a design contract. Most languages have surprising edges. If Grob genuinely doesn’t, that’s rare and worth claiming.

**The aesthetic gap.** There’s a space between “scripting language that’s too loose to trust” (Python, Lua) and “systems language that’s too heavy to script in” (C, Rust). Go occupies some of that space. Grob could own a corner of it — statically typed but fast to write, strict but not ceremonious. That’s a coherent position.

**Readability of static types.** Most statically typed languages make simple scripts feel bureaucratic. If Grob’s type inference is good enough that you rarely write types explicitly but the compiler catches everything — that’s genuinely pleasant to use.

**The “one file” script.** Arduino sketches are one file. What if a Grob script is just a file you run — no project setup, no build system, no manifest — but it can import from a module system when it needs to grow. Small things stay small, big things are possible.

None of these are concrete yet. But I’d push back on chasing a USP too deliberately — the best ones emerge from the constraints you’ve already chosen rather than being bolted on.

What resonates and what doesn’t?

-----

**CHRIS:** They all fit to some extent. I like the one file approach but to avoid spaghetti code modules are a must. I suppose easy to like and shallow learning curve are good to think about.

Not sure if we can clean these together or it’s too much. Later before planning the core philosophy is going to dictate the shape of the language to some extent. I also need to think of a purpose for it. What’s a typical Grob use case?

-----

**CLAUDE:** The ideas do pull together actually. Let me try to articulate the thread.

**The coherent picture:**
Grob is a language that starts simple and scales cleanly. One file for small tasks, modules when things grow. Statically typed so it catches mistakes early, but with enough inference that it doesn’t feel heavy. C-style syntax that anyone with a passing knowledge of C, Java, or Arduino can read immediately. Shallow learning curve in — deep enough to build real things.

**The typical Grob use case** — based on everything you’ve said — is probably something like:

A developer needs to automate a task. File renaming, data transformation, a build step, a small utility. In Python they’d reach for a script but hate the dynamic typing biting them later. In C# they’d spin up a project and it feels like overkill. Grob is the thing they reach for instead. One file, runs immediately, strict enough to trust, readable enough to revisit six months later.

That’s a real gap. It’s roughly where Go scripts and PowerShell live but neither is particularly pleasant.

**On purpose** — I’d push back slightly. “Automation and general tasks” is already a purpose. The question is who is writing it. Is the target user a developer (like you) or someone more technical-but-not-a-programmer (like an Arduino hobbyist)? That distinction will shape the learning curve decisions more than anything else.

What’s your instinct on the target user?

-----

**CHRIS:** Heart says developers but head says hobbyists. Which is where I guess the Arduino comparison lands again.

-----

**CLAUDE:** That tension is actually the most interesting thing you’ve said about Grob.

Because those two users want different things from the same language. A developer wants static types, module system, predictable behaviour, something they can trust at scale. A hobbyist wants to get something working fast, minimal boilerplate, errors that explain themselves, a shallow on-ramp.

The good news is those aren’t incompatible — they’re just in tension at the edges. And you’ve already made decisions that serve both:

- One file to start — hobbyist friendly
- Static typing with inference — developer trusted, hobbyist mostly shielded from it
- Instinctive, no surprises — serves both equally
- Good error messages — Phase 9 of SharpBASIC, but critical for hobbyists especially

The Arduino comparison keeps coming back for a reason. Arduino succeeded because it took C++ — a developer language — and wrapped it in a hobbyist-friendly shell without dumbing it down. The underlying language stayed honest. That’s a decent north star for Grob.

**The practical answer** is build for developers, design for hobbyists. Write the language a developer would trust, then make the experience approachable enough that a hobbyist isn’t scared off. If a hobbyist can write a calculator script on day one without understanding the type system, you’ve got both.

That’s actually a strong USP when you put it plainly — **a statically typed scripting language that a hobbyist can learn and a developer can trust.**

Does that framing feel right?

-----

**CHRIS:** Yes it does. Do we need to capture that somewhere?

-----

**CLAUDE:** Yes — that phrase is worth preserving. It’s the closest thing Grob has to a mission statement right now.

Add one entry to the Decisions Log table in the Context file:

| Feb 2026 | Core philosophy: “A statically typed scripting language that a hobbyist can learn and a developer can trust.” Build for developers, design for hobbyists. |

And add one row to the Shape table:

| Target user | Developers (trust) and hobbyists (approachability) — Arduino as north star |

That’s it. Then go update the file and re-upload it to the project.

-----

**CHRIS:** Updates done

-----

**CLAUDE:** Good. Grob is parked in a solid place.

You have a name, a philosophy, a mission statement, a MVP target, a technical centrepiece, and a target user. That’s more than most language projects have when they start writing code.

Now go build the lexer.

-----

**CHRIS:** The lexer is done well the first pass as is the Parser. Just moving on to Evaluation. This is all in the hello world phase

-----

**CLAUDE:** Nice progress. Phase 1 moving fast is a good sign — the lexer and parser are the foundation everything else sits on.

A few things worth confirming before you wire up the evaluator:

The Phase 1 evaluator is deliberately thin — it just needs to walk a `PrintStatement` node and write the string value to console output. Resist the urge to build a general evaluator now. That comes in Phase 3 when the symbol table arrives.

Two things to check as you move in:

**Is your AST in its own project?** The spec has `SharpBasic.Ast` as a separate classlib that both the lexer and parser reference. Worth confirming that boundary is clean before the evaluator takes a dependency on it.

**How are you handling output in tests?** The evaluator tests need to capture console output to assert against it. The cleanest approach is to inject a `TextWriter` into the evaluator rather than writing directly to `Console.Out`. That makes tests like `Run("PRINT \"Hello\"").Should().Be("Hello")` trivial to write. Worth doing now before the pattern is set.

How are the tests looking — are they all green?

-----

**CHRIS:** All green architecture is sound. Will be following the lessons when building the evaluator.

-----

**CLAUDE:** Perfect. Green tests and clean architecture at the end of Phase 1 is exactly where you want to be.

One thing to keep in mind as you build the evaluator — the `TextWriter` injection point I mentioned is worth doing on the first pass even if it feels like overkill for `PRINT "Hello"`. The pattern you establish now is the pattern you’ll be living with through Phase 10.

When you hit Phase 3 and the symbol table, come back and we’ll plan it properly before you write a line.

-----

**CHRIS:** I know this is a bit early but I had a thought about branding and identity. I definitely want Grob to be in that powershell space. My spirit animal is a raccoon. I love the look. So i want to have some kind of branding around a cute raccoon.

I was also thinking I want this to be open source when done but not sure what license I need to use.

I was also thinking about how I or other people can add libraries for Grob. Do we add these to the main project. This maybe too much solutionising

-----

**CLAUDE:** Love all three of these. Quick takes.

**The raccoon.** Genuinely good branding instinct. Raccoons are curious, scrappy, surprisingly capable, and slightly mischievous — that’s not a bad personality for a scripting language. The PowerShell space is dominated by bland corporate aesthetics. A well-designed raccoon mascot would stand out immediately. Worth commissioning a proper illustration when Grob is real enough to show people. Park it for now but don’t lose it.

**Open source license.** For a language project MIT is almost always the right answer. It’s permissive, well understood, and removes friction for anyone who wants to use or contribute. If you want contributors to send improvements back to the main project rather than fork silently, MIT still works — it just doesn’t require it. The only reason to consider something else is if you had commercial concerns, which a learning project doesn’t. MIT, don’t overthink it.

**The library ecosystem question.** You’re right that it’s early, but the instinct is correct — this needs a decision eventually. The two models are: libraries live in the main repo (curated, controlled, slower), or libraries are separate repos with a package manager pulling them in (open, fast, complex to build). The second is where languages like Grob eventually need to go but a package manager is a significant project in itself. For now “separate repos, no package manager yet” is the honest answer. Something to design properly when the language is working.

Good instincts on all three. Back to the evaluator.

-----

**CHRIS:** I’m making good progress on the current project. I have a fully working repo with a lexer, parser and evaluator. Just about to start on basic variable assignment

-----

**CLAUDE:** Excellent. Phase 1 done, moving into Phase 3 territory already — that’s solid momentum.

Before you start on variable assignment, three things worth confirming:

**Did you do Phase 2 as a distinct step?** The spec has Parser and AST as Phase 2 — just checking the `PrintStatement` / `StringLiteralExpression` AST nodes are properly in `SharpBasic.Ast` and the parser is producing a real `Program` node rather than the ad-hoc REPL handling from Phase 1. Sounds like it is but worth confirming the boundary is clean.

**Symbol table design.** For Phase 3 the symbol table is just a `Dictionary<string, object>` or better a `Dictionary<string, SharpBasicValue>` where `SharpBasicValue` is a discriminated union of the supported types. Worth thinking about that wrapper type now rather than using raw `object` — it’ll pay off in Phase 4 when arithmetic needs to know what type it’s operating on.

**Scoping.** Phase 3 is flat scope — one global symbol table. That’s correct for now. Just don’t paint yourself into a corner. When Phase 7 arrives and you need a call stack with nested scopes, the symbol table needs to support chained lookups. Worth a quick look at how you’ll extend it later even if you don’t build it now.

What does your `SharpBasicValue` type look like, or are you using raw object?

-----

**CHRIS:** Yes phase 2 was a distinct step. I have a base value record and then specific versions like StringValue, Number Value, VoidValue etc

-----

**CLAUDE:** That’s the right design. A typed value hierarchy beats raw `object` — you’ll feel the benefit as soon as Phase 4 arithmetic arrives.

One thing to check on `NumberValue` — are you using a single numeric type or splitting `IntegerValue` and `FloatValue`? BASIC traditionally has both and the spec calls for `INTEGER` and `FLOAT` as distinct types. If you’ve collapsed them into one `NumberValue` now, Phase 4 will force the split anyway when `LET x = 10 / 4` needs to return `2.5` not `2`.

`VoidValue` is a good call too — cleaner than returning null from SUB calls in Phase 7.

How are you modelling the base record — abstract record with pattern matching, or an interface hierarchy?

-----

**CHRIS:** The original design o my seem to have one number type double

-----

**CLAUDE:** Worth fixing now before it spreads.

BASIC has distinct integer and float types and the spec calls for both. More importantly, `LET x = 10` and `LET x = 3.14` should produce different runtime types — that distinction matters for Phase 4 arithmetic, Phase 7 typed parameters (`n AS INTEGER`), and Phase 8 array declarations (`DIM scores(10) AS INTEGER`).

The fix is straightforward while you’re still in Phase 3:

```csharp
public abstract record SharpBasicValue;
public record IntegerValue(long Value) : SharpBasicValue;
public record FloatValue(double Value) : SharpBasicValue;
public record StringValue(string Value) : SharpBasicValue;
public record BooleanValue(bool Value) : SharpBasicValue;
public record VoidValue : SharpBasicValue;
```

`long` for integers gives you headroom without needing to think about overflow for a while.

Then in your evaluator when you produce a numeric value, the lexer token type tells you which to construct — `IntegerLiteral` produces `IntegerValue`, `FloatLiteral` produces `FloatValue`. That distinction is already in your token stream so the evaluator just needs to honour it.

Do it now — it’s a small change today and a painful refactor in Phase 4 if you leave it.

-----

*End of conversation export.*
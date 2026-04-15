# Grob — LinkedIn Series Bible

> Reference document for all Grob LinkedIn content.
> Read this before every drafting session. Update the post index after every post.
> This is the consistency anchor for the series — tone, framing, audience, history.

-----

## What Grob Is — Series Context

Grob is a statically typed scripting language with a bytecode VM, written in C# .NET.
It fills the gap that Go, PowerShell, and Python all miss: low-ceremony, genuinely
readable scripting with static typing and real tooling. Built in the open, evenings
and weekends, alongside a full-time lead developer role.

The design is complete. Implementation is starting.

This is the project that comes after SharpBASIC — a BASIC interpreter that was
about understanding. Grob is about building something people might actually reach for.

**One-line series hook:**
*Designing and building a scripting language from scratch — in public, one decision at a time.*

-----

## The Series in One Paragraph

Grob is not a learning exercise documented after the fact. It is a serious language
design project, built in the open. The posts cover real decisions with real tradeoffs —
why the type system works the way it does, how the VM architecture was chosen, what
clox taught that textbooks don’t. The series is a journal, not a tutorial. The
decisions are visible. The reasoning is honest. The gaps between posts are not
explained.

-----

## Who Chris Is — The Identity That Informs the Voice

No formal CS education. Everything else.

Chris learned by doing — across 26 years, every layer of the stack, every stage of
the SDLC. Books, systems, certifications, enterprise delivery for blue chip companies,
team leadership, strategic thinking. The curiosity came first. The capability followed.

The mechanic background is real and it shapes how he thinks — systems, cause and
effect, feedback loops. It is texture in the posts, not a device. One well-placed
reference lands harder than three.

The right framing is not “self-taught hobbyist.” It is: learned by doing, at every
level, for 26 years — and still insatiably curious about how things work.

The driving question behind all of it: if we can’t make people’s lives easier, why
are we building it?

-----

## Audience

**Primary:** Practising software engineers — people who write code professionally and
recognise the tension between what the job demands and what their curiosity wants.
Senior engineers and leads who understand what it means to go deep on something
deliberately.

**Secondary:** Developers who learned the way Chris did — curiosity-first, without
a formal CS path — and who still have that impulse.

**The bridge:** The best posts speak to both. The senior engineer thinking out loud
is aspirational to the second audience and recognisable to the first.

**On collaboration:** Not actively seeking contributors. Let interest build naturally.
Revisit post-v1. Say nothing either way in posts — no open invitations, no closed doors.

-----

## Post Types

Four types of post sit naturally in this series. Most sessions will produce one type.
The strongest posts occasionally combine two.

### Type 1 — The Decision Post

A specific design decision, explained properly. The problem, the options considered,
what was chosen and why. These are the backbone of the series.

*Examples: why static typing in a scripting language, how the error model was chosen,
why the install strategy has three tiers, what the bare-brace rule solves.*

These posts work because the reasoning is more interesting than the outcome. Anyone
can look at the spec. Only these posts explain how it got there.

### Type 2 — The Build Post

Something got implemented. Here’s what it was, what it involved, what it revealed.
Not a tutorial — a dispatch from the work. Short on instruction, long on observation.

*Examples: first opcodes running, the type checker catching its first error, a
stdlib module taking shape.*

These posts create forward momentum and signal that Grob is real, not just a design
document.

### Type 3 — The Reflection Post

Something in the build or design produced an insight that reaches beyond Grob. About
complexity, craft, how 26 years of experience helps and hinders, what building from
scratch teaches that professional work doesn’t.

These posts connect Thread 2 (the journey series) to Thread 1 (engineering leadership).
They tend to be the strongest posts for audience reach. Use them when the insight is
genuine — never manufactured.

*Examples: what designing a type system taught about API design, why constraints
produce better decisions than open fields, what clox revealed about abstractions.*

### Type 4 — The Teaching Post

A concept Chris had to learn in order to make a decision — explained honestly, in
the context of that decision. Not a tutorial. Not performance of expertise. Just:
here’s what I didn’t know, here’s what I found out, here’s what it meant for Grob.

The structure:

1. Here’s the decision I faced
2. Here’s what I didn’t know
3. Here’s what I found out
4. Here’s what it meant for Grob

This works because the concept and the decision are genuinely connected. The reader
gets the what and the why in the same post. The honesty about not knowing is the
credibility, not a weakness.

*Examples: why a bytecode VM rather than a tree-walker, what constant folding is
and whether Grob needs it, what call frames actually are.*

-----

## Series Arc

**Phase 1 — Design journey** (front-loads the series)
The decisions that shaped the language before a line of implementation was written.
Type system, error model, syntax choices, install strategy, the VM architecture
decision. Mostly Type 1 and Type 4 posts.

**Phase 2 — Build** (follows implementation phases)
As each layer of the VM comes to life — chunk, stack, variables, control flow,
functions, stdlib, plugins — dispatches from the work. Mostly Type 2, with Type 3
and Type 4 where the work produces something worth reflecting on or teaching.

**Phase 3 — Launch**
A v1 launch post. What shipped, what didn’t, what comes next. Then the series
continues or evolves — that decision gets made when v1 is real.

The arc is loose by design. Posts don’t need to follow implementation order exactly.
If something interesting happens out of sequence, write about it.

-----

## Format & Length

LinkedIn is read on phones, often in motion. Format accordingly.

- Short paragraphs — often a single sentence
- Deliberate line breaks for rhythm and emphasis
- Build to the point rather than leading with it
- End with an open thread — a genuine question, an unresolved problem, what comes next
- No headers within posts — structure comes from rhythm, not formatting
- No bullet lists unless the content is genuinely list-shaped
- Length: long enough to say something real, short enough to hold attention.
  Typically 200–400 words. Never pad to reach a number, never cut to hit one.

-----

## Tone Guardrails

**Do:**

- Think out loud — share the reasoning, not just the conclusion
- Be specific — name the decision, name the tradeoff, name the language it’s compared to
- Leave threads open — “the next problem is X” is more compelling than a tidy wrap-up
- Let the mechanical and systems thinking colour the voice naturally
- Trust the reader — no over-explanation, no hand-holding
- Be honest about what you didn’t know — that’s the credibility, not a liability

**Don’t:**

- Lead with nostalgia — the Spectrum and INPUT magazine were load-bearing in the
  SharpBASIC series. For Grob they are texture only. One reference, used well,
  lands harder than three.
- Reach for mechanical analogies — they’re in the thinking already. When one earns
  its place it will be obvious. Don’t engineer it.
- Apologise for gaps between posts — just post. No “it’s been a while.”
- Moralize — make the observation, let the reader draw the conclusion
- Manufacture urgency or drama — the work is interesting enough
- Perform expertise you don’t have — the teaching posts work because they’re honest
  about the learning, not because they position Chris as an authority he isn’t

**The register:** The senior engineer thinking out loud, not performing expertise.
Dry wit when it fits. Short sentences doing real work. The last word in a sentence
carrying the weight.

-----

## Series Continuity Rules

- Every post should be self-contained — a reader mid-stream should not be lost
- References to previous posts should be light — a single line of context, not a recap
- SharpBASIC is the backstory, not the premise. Referenced once at the series opening
  to bridge the audience. After that, only when directly relevant — and sparingly.
- clox is in progress and informs the VM design — mention in passing when relevant,
  it does not need its own posts
- grob-lang.dev is the destination for readers who want to go deeper — link it where
  natural, not on every post
- Sparky does not appear in posts — the character belongs to the project identity
  (website, GitHub, docs), not the LinkedIn presence

-----

## Handling Technical Depth

Some readers will know what a bytecode VM is. Others won’t. The resolution:

Define once, inline, briefly — then move on. Never footnote, never link out, never
write a paragraph of context for a concept that needs a sentence. If a concept
needs more than a sentence of context to make the post work, it’s a Type 4 Teaching
Post, not a footnote.

The test: would a competent developer who hasn’t thought about compilers follow this?
If yes, proceed. If no, either simplify or make it a teaching post.

-----

## What the Audience Knows So Far

*(Update this section after each post goes live)*

**From the SharpBASIC series (complete):**

- Chris built a complete BASIC interpreter in C# .NET from scratch
- It ships as a global .NET tool with a VS Code extension and a ten-issue INPUT-style magazine
- SharpBASIC was about understanding what sits underneath languages and tools
- The series established credibility and voice with the Thread 2 audience
- Closed with: “the next project is already waiting”

**From Grob posts:**
*(None yet — series starts here)*

-----

## Language & Punctuation

- British English throughout — recognise, organise, colour, behaviour
- Oxford comma: **never** — “tokens, types and execution” not “tokens, types, and execution”
- No emoji in the post body — closing line or hashtag area only, and only if it fits naturally
- Hashtags at the end, not woven into the text

**Hashtags for this series (final):**
`#buildinginpublic #softwaredevelopment #compilers #languagedesign #engineeringleadership #opensource`

-----

## Post Index

|#|Type    |Title / Hook                                               |Published|Notes                                                                            |
|-|--------|-----------------------------------------------------------|---------|---------------------------------------------------------------------------------|
|1|Decision|The gap post — why Go, PowerShell and Python all fall short|Pending  |First Grob post. Single callback to SharpBASIC closer. No code. Pure positioning.|

-----

## Grob Facts for Series Use

Quick reference — use these details consistently across all posts.

|Item                |Detail                                                                 |
|--------------------|-----------------------------------------------------------------------|
|Language            |Statically typed scripting language                                    |
|Implementation      |C# .NET, bytecode VM                                                   |
|Syntax              |C-style, no semicolons, type inference                                 |
|Target platform     |Windows-native                                                         |
|Mascot              |Sparky — raccoon, blue hoodie, utility belt, wrench. Not used in posts.|
|Logo mark           |`G>` — forward chevron on G                                            |
|Website             |grob-lang.dev                                                          |
|GitHub              |github.com/grob-lang                                                   |
|Licence             |MIT                                                                    |
|Status (April 2026) |Design complete, implementation starting                               |
|Comparable languages|Go, PowerShell, Python — Grob fills the gap they all miss              |
|What it is not      |A toy, a learning exercise, a prototype                                |

-----

*Last updated: April 2026 — v2, post-external-review.*
*Added: series arc, fourth post type, format guidance, identity framing,*
*SharpBASIC continuity resolution, technical depth handling.*
*Update the post index and “what the audience knows” section after every post.*
---
mode: 'agent'
description: 'Start a new short-lived branch for a piece of work. Confirms we are not on main, proposes a branch name following conventions and sets it up.'
---

# Start a branch

Before any work begins on Grob, the work goes on a branch. This prompt
sets one up.

## Procedure

1. Run `git status` and `git branch --show-current` to see where things stand.
2. If there are uncommitted changes on the current branch, stop. Say:
   > "There are uncommitted changes on `<branch>`. We need to handle these
   > before starting a new branch. Should I show them, or do you want to
   > commit/stash first?"
   Wait for instruction. Do not auto-stash.
3. If the current branch is not `main`, ask:
   > "We're currently on `<branch>`, not `main`. Should I switch to `main`
   > before branching, or branch from here?"
   Branching from a non-main branch is sometimes deliberate (stacked work),
   but more often a mistake. Confirm rather than assume.
4. If on `main` with a clean working tree, run `git pull` first to ensure
   the branch is current.
5. Propose a branch name following the convention below.
6. Wait for Chris to agree the name.
7. Create and check out the branch: `git checkout -b <name>`.
8. Confirm: `git branch --show-current` should now show the new name.

## Branch naming convention

```
<type>/<scope>-<kebab-name>
```

- **type** matches the conventional-commits type the eventual commit(s) will
  use: `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `ci`, `chore`.
- **scope** matches the Grob project scope: `core`, `compiler`, `vm`,
  `runtime`, `stdlib`, `cli`, `lsp` or `tests` / `docs` / `build` for
  cross-cutting changes.
- **kebab-name** is a short slug describing the work. Three words max ideally.
  Lowercase, hyphen-separated. No type or scope repetition.

### Examples

Good:

- `feat/compiler-string-lexer`
- `fix/vm-stack-underflow`
- `test/compiler-error-recovery`
- `refactor/core-rename-token-kinds`
- `docs/compiler-error-codes`
- `build/add-grob-vm-csproj`

Bad:

- `chris-working-on-strings` — no type, no scope, ambiguous
- `feat/compiler/string-lexer` — slashes don't nest; use kebab-case after
  the type/scope prefix
- `feat-compiler-string-lexer` — type and scope should be separated by `/`
- `feat/compiler-add-the-string-lexer-and-some-other-stuff` — too long, and
  "and some other stuff" means the branch is doing more than one thing

## Lifetime rules

A branch should live for hours, not days. Two-day branches are the upper
limit; anything longer means the work is too big and should be split.

If a branch starts to grow beyond a day's work, surface it:

> "This branch has been alive for a while and is getting bigger than
> planned. Should I split the remaining work into a second branch?"

## What this prompt does not do

- Does not write any production code. That's a separate step.
- Does not push the branch. Pushing happens when there's something worth
  pushing, typically after the first commit lands.
- Does not configure upstream tracking. `git push -u origin <branch>` on
  the first push handles that.
- Does not commit on Chris's behalf.

## After the branch exists

The next step is usually a TDD cycle:

1. Write the test.
2. Run it. Confirm it fails.
3. Propose the implementation.
4. With Chris's agreement, write the implementation.
5. Run the test. Confirm it passes.
6. Refactor if anything needs it.
7. Stage and propose the commit message.

For multi-cycle work, that whole sequence repeats. Stay on the branch
until the work is complete; merging back to `main` is Chris's action,
not yours.

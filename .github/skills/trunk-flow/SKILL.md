---
name: trunk-flow
description: Trunk-based development for Grob — short-lived branches, conventional commits, never-on-main rule, merge protocol. Use this skill when starting a branch, naming a branch, deciding when to commit, deciding when a branch should land or when a branch has grown too big.
---

# Trunk-based development for Grob

The discipline: small, frequent merges to `main`. Branches live hours, not
days. The codebase is always releasable. Chris merges; you never do.

## Core rules

1. **Never start work on `main`.** First action on any new piece of work is
   to create a branch.
2. **Short-lived branches.** Hours ideally, two days maximum. Longer than
   that and the branch is too big — split it.
3. **One purpose per branch.** A branch delivers one cohesive change. If
   you find yourself making two unrelated changes on the same branch, stop
   and split.
4. **Chris always merges.** You stage commits and propose messages. The
   merge is Chris's action.
5. **The trunk is always green.** Every commit on `main` must pass tests.
   You don't merge red. Chris doesn't merge red.

## Branch naming

```
<type>/<scope>-<kebab-name>
```

- **type** matches conventional-commits: `feat`, `fix`, `refactor`, `test`,
  `docs`, `build`, `ci`, `chore`.
- **scope** matches the Grob project scope: `core`, `compiler`, `vm`,
  `runtime`, `stdlib`, `cli`, `lsp`, `tests`, `docs`, `build`.
- **kebab-name** is a short slug. Three words max. Lowercase, hyphens.

Examples:
- `feat/compiler-string-lexer`
- `fix/vm-stack-underflow`
- `test/compiler-error-recovery`
- `refactor/core-rename-token-kinds`
- `build/add-grob-vm-csproj`

## The branch lifecycle

```
1. Confirm on main with clean tree
       git status
       git branch --show-current   # should be main

2. Pull latest
       git pull

3. Create branch
       git checkout -b feat/compiler-string-lexer

4. Work the TDD cycles
       (red / green / refactor / commit, repeating)

5. Push to remote
       git push -u origin feat/compiler-string-lexer
       (the -u sets upstream tracking; first push only)

6. Hand off to Chris for review and merge
       "Branch feat/compiler-string-lexer is ready. Three commits.
        All tests green. Ready for your review and merge."

7. After Chris merges and deletes the remote branch
       git checkout main
       git pull
       git branch -d feat/compiler-string-lexer    # local cleanup
```

## Conventional commits

Every commit message follows:

```
<type>(<scope>): <subject>

<body>

<footer>
```

See `.github/instructions/commits.instructions.md` for the full rules. The
short version:

- **Imperative subject**, lowercase, no trailing full stop, ≤50 chars where
  possible.
- **Body wrapped at 72 chars**, explaining *why* not *what*.
- **Cite the decision-log entry** when the change implements a settled
  decision: `Implements D-300.`
- **Reference issues** in the footer if applicable: `Refs: #42` or
  `Closes: #42`.
- **British English**, no Oxford comma, never "simply".

## Commit cadence

Commit small, commit often. The shape of a typical branch:

```
test(compiler): add lexer test for empty input            # red
feat(compiler): add Lexer scanning empty input to EOF     # green
test(compiler): add lexer tests for decimal integers      # red
feat(compiler): scan decimal integer literals             # green
test(compiler): add lexer tests for hex integers          # red
feat(compiler): scan hex integer literals                 # green
refactor(compiler): extract number scanning to helper     # refactor
```

Seven commits, three TDD cycles plus a refactor. All on one branch,
landing in a day or less.

## When a branch has grown too big

Symptoms:
- More than seven or eight commits before the branch lands.
- Touches more than two scopes.
- Has been alive more than a day with no end in sight.
- Has accumulated unrelated changes ("while I was here, I also fixed…").

When you notice this, surface it:

> "This branch has grown beyond what we planned. It now covers the lexer
> *and* part of the parser. Should I land the lexer part first and start a
> new branch for the parser, or do something else?"

Don't wait for Chris to notice. Branch splits are easy when caught early,
painful when caught late.

## Splitting a branch in flight

If a branch needs splitting:

```
1. From the bloated branch, look at the commits
       git log --oneline main..HEAD

2. Decide the split point. The commits before the split point belong to
   the first branch, the commits after to the second.

3. Reset the current branch to the split point
       git checkout feat/compiler-string-lexer
       git reset --hard <split-point-commit>
       git push --force-with-lease    # only if already pushed

4. Create the second branch from the split point with the orphaned commits
       git checkout -b feat/compiler-string-interpolation <bloated-tip>

5. The first branch is now clean and ready to hand off. The second branch
   continues the work after the first lands.
```

Force-pushing a branch is fine when only you have been working on it. Use
`--force-with-lease` not `--force` — it refuses if someone else has pushed
in the meantime.

## Merge protocol

You **never** merge to `main`. Always Chris.

When the branch is ready:

1. Confirm all tests pass: `dotnet test` against the whole solution.
2. Confirm the branch is up to date with `main`: `git fetch && git log
   origin/main..HEAD` — if there are commits on `main` you don't have,
   rebase or merge them in first.
3. Push: `git push` (or `git push -u origin <branch>` on the first push).
4. Hand off in chat: "Branch `<name>` ready. `<N>` commits. All tests
   green. Ready for your review and merge."
5. Wait. Don't proceed with new work on the same branch — start a new
   branch for the next piece of work.

If Chris asks for changes, fix them on the same branch and push again.
Don't create a "fixes" branch.

## After the merge

Once Chris has merged:

```
git checkout main
git pull
git branch -d <merged-branch>
```

The `-d` (lowercase) only deletes if the branch was merged. If it refuses,
something is off — investigate before forcing with `-D`.

## What you do not do

- You do not merge to `main`.
- You do not force-push to `main` or to a branch someone else is working on.
- You do not rebase a branch that's been pushed and shared without
  coordinating.
- You do not commit secrets, API keys, large binaries or anything that
  shouldn't be in git history. If something slips in, surface it
  immediately — rewriting history later is much worse than admitting a
  mistake now.
- You do not skip the test suite before pushing. Green locally, green on
  push.

## Edge cases worth knowing

- **Stacked branches.** Occasionally a branch depends on another branch
  that hasn't landed yet. Branch from the parent branch, not from `main`.
  Make it explicit when handing off: "This is stacked on
  `feat/compiler-string-lexer`. Once that merges, this one can rebase
  onto `main`."
- **Hotfixes.** If something on `main` is broken and needs urgent fixing,
  the branch is still required — just keep it minimal. `fix/vm-crash-on-eof`
  with one test commit and one fix commit. Don't bundle unrelated cleanup
  with a hotfix.
- **WIP commits.** If you need to checkpoint mid-cycle (lunch, end of day),
  commit as `wip(scope): <description>`. Before pushing or asking for
  review, squash WIP commits into proper conventional commits.

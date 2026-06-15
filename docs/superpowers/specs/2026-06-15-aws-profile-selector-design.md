# aws-profile-selector — Design

**Date:** 2026-06-15
**Status:** Approved design, pending implementation plan

## Overview

A .NET 10 console application that lets the user interactively pick an AWS
profile from `~/.aws/config` and apply it either to the current terminal
session only, or persist it as the default for new terminals. Built on
**Spectre.Console** (interactive UI) and **Spectre.Console.Cli** (command
framework).

Because a child process cannot mutate its parent shell's environment, the tool
is driven through a small `awsp` shell function that `eval`s the tool's stdout.

## UX flow

```
$ awsp
? Select an AWS profile:        (interactive list with type-to-filter, on stderr)
  > default                 (us-east-1, sso)
    profile-a               (us-east-1, static)
    profile-b               (us-east-1, role)
    profile-c               (eu-west-1, static)
? Apply how?
  > This terminal session only
    Save as default (also applies now)
✓ AWS_PROFILE=profile-b set for this session
```

The shell wrapper runs `eval "$(aws-profile-selector select "$@")"`. The tool
prints `export AWS_PROFILE=<name>` to **stdout** (which the wrapper evals into
the current shell). All interactive UI renders to **stderr** so it never
pollutes the eval'd stdout.

## Commands (Spectre.Console.Cli)

### `select` (default command)
1. Parse `~/.aws/config`.
2. Show interactive profile picker (with substring search) on stderr.
3. Ask apply mode: session-only vs save-as-default.
4. Always print `export AWS_PROFILE=<name>` to stdout (covers current session).
5. If "save as default": also upsert that `export` line into a managed block in
   `~/.zshrc` (so new terminals start with it).

### `init`
Install/refresh the `awsp` shell function in `~/.zshrc` inside an idempotent
managed block:

```
# >>> aws-profile-selector >>>
awsp() { eval "$(aws-profile-selector select "$@")"; }
# <<< aws-profile-selector <<<
```

Re-running replaces the block in place. Prints a note to `source ~/.zshrc` or
open a new terminal.

## Components

Each is independently testable with a single clear responsibility.

### `AwsConfigParser`
- Reads the AWS config file, honoring the `AWS_CONFIG_FILE` env var, defaulting
  to `~/.aws/config`.
- Returns `IReadOnlyList<AwsProfile>` where
  `AwsProfile { string Name, string? Region, ProfileType Type }`.
- Recognizes selectable sections only: `[default]` and `[profile X]`.
- Ignores non-profile sections: `[sso-session X]`, `[services X]`, etc.
- Infers `Type` from keys present (first match wins):
  - `role_arn` → `Role`
  - `sso_session` or `sso_account_id` → `Sso`
  - `aws_access_key_id` → `Static`
  - `credential_process` → `Process`
  - otherwise → `Config`
- Simple INI-style parsing (manual; no external INI dependency).

### `ProfileSelector`
- Pure UI over the parsed profile list.
- Spectre `SelectionPrompt<AwsProfile>` with `.EnableSearch()` and a search
  placeholder ("type to filter…") — case-insensitive substring filtering by
  profile name.
- Display converter renders `name  (region, type)`, e.g.
  `profile-b  (us-east-1, role)`.
- Second prompt: a plain `SelectionPrompt` for apply mode
  (session-only / save-as-default).

### `ZshrcWriter`
- Manages an idempotent marker-delimited block in `~/.zshrc`.
- One core operation: upsert a block identified by start/end markers — replace
  if present, append if absent, preserve all surrounding content.
- Used for both the `init` function block and the persisted `export` default.

### Program / CommandApp wiring
- Configures the Spectre `AnsiConsole` instance to write to **stderr** (so
  interactive output does not contaminate stdout).
- Registers `select` (default) and `init` commands.

## Error handling

- Missing or empty `~/.aws/config` → friendly message on stderr, non-zero exit,
  **nothing on stdout** (eval becomes a no-op).
- No selectable profiles found → same as above.
- User cancels (Ctrl-C / Esc) → exit cleanly, nothing on stdout.
- `~/.zshrc` not writable → clear stderr error; the session `export` is still
  printed to stdout so the current-session apply still works.

## Distribution

Packaged as a **.NET global tool** (`PackAsTool=true`), command name
`aws-profile-selector`, installed via `dotnet tool install --global`. This puts
it on `PATH` so the `awsp` wrapper resolves it.

## Project structure

```
aws-profile-selector.sln
src/AwsProfileSelector/            (console app — the tool)
tests/AwsProfileSelector.Tests/   (xUnit)
```

## Testing

- xUnit test project.
- `AwsConfigParser` tests over fixture config files covering: `[default]`,
  `[profile X]`, role, sso, static, and ignored `[sso-session X]` / `[services]`
  sections; missing/empty file.
- `ZshrcWriter` tests: insert into file without block, idempotent update of an
  existing block, preservation of surrounding content, file-not-present case.
- Interactive picker and the end-to-end eval flow verified manually.

## Decisions / defaults

- Shell: **zsh only** (`~/.zshrc`), matching the user's shell.
- Shell function name: **`awsp`**.
- Search mode: built-in Spectre substring filter (not true fuzzy subsequence).
- "Save as default" persists via an `export AWS_PROFILE` line in `~/.zshrc`
  (non-destructive to `~/.aws/config`) and also applies to the current session.

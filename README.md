# aws-profile-selector

Interactive CLI to select an AWS profile from `~/.aws/config` and apply it to
your current zsh session or save it as the default. Built on Spectre.Console.

## Install

```bash
dotnet pack src/AwsProfileSelector -o ./nupkg
dotnet tool install --global --add-source ./nupkg aws-profile-selector
```

## Set up the shell wrapper (one time)

```bash
aws-profile-selector init
source ~/.zshrc
```

This installs an `awsp` zsh function that runs the selector and applies the
chosen profile to your current shell. (A child process can't change its parent
shell's environment, so the wrapper `eval`s the tool's output.)

## Usage

```bash
awsp
```

Type to filter, pick a profile, then choose:

- **This terminal session only** — sets `AWS_PROFILE` in the current shell.
- **Save as default** — also persists `export AWS_PROFILE=…` to `~/.zshrc` so
  new terminals start with it (and applies it to the current shell now).

The profile list shows each profile's region and credential type
(`sso` / `role` / `static` / `process` / `config`), inferred from its config
keys. Non-profile sections such as `[sso-session …]` are ignored. Set
`AWS_CONFIG_FILE` to read a config file from a non-default location.

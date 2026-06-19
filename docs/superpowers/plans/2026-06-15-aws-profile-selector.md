# aws-profile-selector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A .NET 10 CLI that interactively selects an AWS profile from `~/.aws/config` and applies it to the current zsh session or persists it as the default, driven by an `awsp` eval-wrapper.

**Architecture:** A console app built on Spectre.Console (interactive UI rendered to **stderr**) and Spectre.Console.Cli (command framework). The default `select` command prints `export AWS_PROFILE=<name>` to **stdout** for a shell wrapper to `eval`; "save as default" additionally persists the export into a managed block in `~/.zshrc`. An `init` command installs the `awsp` shell function. Pure logic (config parsing, label formatting, path resolution, zshrc editing) lives in small, independently unit-tested classes; the interactive selector is verified manually.

**Tech Stack:** .NET 10, C#, Spectre.Console `0.57.0`, Spectre.Console.Cli `0.55.0`, xUnit.

---

## Verified API notes (read before coding)

- `Command<TSettings>` requires: `protected override int Execute(CommandContext context, TSettings settings, CancellationToken cancellationToken)`. The method is **protected** and takes a **CancellationToken** in this version. Using `public` or omitting the token will not compile.
- A stderr-backed console is created with:
  `AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) })`.
- `SelectionPrompt<T>` supports `.Title(...)`, `.EnableSearch()`, `.SearchPlaceholderText(...)`, `.UseConverter(Func<T,string>)`, `.AddChoices(IEnumerable<T>)`. Prompt via `console.Prompt(prompt)` which returns the selected `T`.
- The final `export` line must be written with plain `Console.Out.WriteLine(...)` (NOT Spectre), so no ANSI escapes leak into the eval'd stdout.

## File structure

```
aws-profile-selector.sln
src/AwsProfileSelector/
  AwsProfileSelector.csproj        (Exe, net10.0, PackAsTool)
  Program.cs                       (CommandApp + stderr console wiring)
  Model/AwsProfile.cs              (AwsProfile record, ProfileType, ApplyMode)
  Config/AwsConfigParser.cs        (parse config text -> profiles)
  Config/AwsConfigLocator.cs       (resolve config file path)
  Ui/ProfileFormatter.cs           (profile -> display label)
  Ui/ProfileSelector.cs            (Spectre prompts)
  Shell/ZshrcWriter.cs             (managed-block upsert in ~/.zshrc)
  Commands/SelectCommand.cs        (default command)
  Commands/InitCommand.cs          (init command)
tests/AwsProfileSelector.Tests/
  AwsProfileSelector.Tests.csproj  (xunit, references the app project)
  AwsConfigParserTests.cs
  ProfileFormatterTests.cs
  AwsConfigLocatorTests.cs
  ZshrcWriterTests.cs
```

---

## Task 1: Solution and project scaffold

**Files:**
- Create: `aws-profile-selector.sln`
- Create: `src/AwsProfileSelector/AwsProfileSelector.csproj`
- Create: `tests/AwsProfileSelector.Tests/AwsProfileSelector.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run from repo root:

```bash
dotnet new sln -n aws-profile-selector
dotnet new console -n AwsProfileSelector -o src/AwsProfileSelector
dotnet new xunit -n AwsProfileSelector.Tests -o tests/AwsProfileSelector.Tests
dotnet sln add src/AwsProfileSelector/AwsProfileSelector.csproj
dotnet sln add tests/AwsProfileSelector.Tests/AwsProfileSelector.Tests.csproj
dotnet add tests/AwsProfileSelector.Tests/AwsProfileSelector.Tests.csproj reference src/AwsProfileSelector/AwsProfileSelector.csproj
```

- [ ] **Step 2: Add Spectre packages to the app**

```bash
dotnet add src/AwsProfileSelector/AwsProfileSelector.csproj package Spectre.Console --version 0.57.0
dotnet add src/AwsProfileSelector/AwsProfileSelector.csproj package Spectre.Console.Cli --version 0.55.0
```

- [ ] **Step 3: Configure the app csproj for packaging**

Replace the contents of `src/AwsProfileSelector/AwsProfileSelector.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AwsProfileSelector</RootNamespace>
    <AssemblyName>AwsProfileSelector</AssemblyName>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>aws-profile-selector</ToolCommandName>
    <PackageId>aws-profile-selector</PackageId>
    <Version>0.1.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Spectre.Console" Version="0.57.0" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.55.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Replace default Program.cs with a placeholder that builds**

Replace `src/AwsProfileSelector/Program.cs` with:

```csharp
// Entry point is implemented in Task 10.
return 0;
```

- [ ] **Step 5: Build the solution**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, projects, and Spectre packages"
```

---

## Task 2: Domain model

**Files:**
- Create: `src/AwsProfileSelector/Model/AwsProfile.cs`

- [ ] **Step 1: Create the model file**

Create `src/AwsProfileSelector/Model/AwsProfile.cs`:

```csharp
namespace AwsProfileSelector.Model;

/// <summary>How a profile obtains credentials, inferred from its config keys.</summary>
public enum ProfileType
{
    Sso,
    Role,
    Static,
    Process,
    Config,
}

/// <summary>Whether a selection applies to the current session or is persisted as the default.</summary>
public enum ApplyMode
{
    Session,
    Default,
}

/// <summary>A selectable AWS profile parsed from the config file.</summary>
public sealed record AwsProfile(string Name, string? Region, ProfileType Type);
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add AwsProfile model and enums"
```

---

## Task 3: AWS config parser

**Files:**
- Create: `src/AwsProfileSelector/Config/AwsConfigParser.cs`
- Test: `tests/AwsProfileSelector.Tests/AwsConfigParserTests.cs`

**Parsing rules:** A section header is a line `[inner]`. A section is a **selectable profile** only if `inner == "default"` or `inner` starts with `"profile "` (the profile name is `inner` minus the `"profile "` prefix, or `"default"`). All other sections (`sso-session`, `services`, …) are ignored. Within a profile, key/value lines split on the first `=`. `Region` comes from the `region` key. `Type` is inferred from the first matching key: `role_arn`→Role, `sso_session` or `sso_account_id`→Sso, `aws_access_key_id`→Static, `credential_process`→Process, else Config. Blank lines and lines starting with `#` or `;` are ignored.

- [ ] **Step 1: Write the failing tests**

Create `tests/AwsProfileSelector.Tests/AwsConfigParserTests.cs`:

```csharp
using AwsProfileSelector.Config;
using AwsProfileSelector.Model;
using Xunit;

namespace AwsProfileSelector.Tests;

public class AwsConfigParserTests
{
    [Fact]
    public void Parses_default_and_named_profiles()
    {
        const string config = """
            [default]
            region = us-east-1
            sso_session = my-sso

            [profile alpha]
            region = eu-west-1
            aws_access_key_id = AKIA...
            """;

        var profiles = AwsConfigParser.Parse(config);

        Assert.Collection(profiles,
            p =>
            {
                Assert.Equal("default", p.Name);
                Assert.Equal("us-east-1", p.Region);
                Assert.Equal(ProfileType.Sso, p.Type);
            },
            p =>
            {
                Assert.Equal("alpha", p.Name);
                Assert.Equal("eu-west-1", p.Region);
                Assert.Equal(ProfileType.Static, p.Type);
            });
    }

    [Fact]
    public void Ignores_non_profile_sections()
    {
        const string config = """
            [sso-session my-sso]
            sso_start_url = https://example.awsapps.com/start
            sso_region = us-east-1

            [services my-services]
            s3 =

            [profile beta]
            role_arn = arn:aws:iam::123:role/r
            """;

        var profiles = AwsConfigParser.Parse(config);

        var single = Assert.Single(profiles);
        Assert.Equal("beta", single.Name);
        Assert.Equal(ProfileType.Role, single.Type);
        Assert.Null(single.Region);
    }

    [Fact]
    public void Infers_process_type_and_skips_comments()
    {
        const string config = """
            # a comment
            ; another comment
            [profile gamma]
            credential_process = /usr/bin/cred
            region = ap-south-1
            """;

        var single = Assert.Single(AwsConfigParser.Parse(config));
        Assert.Equal("gamma", single.Name);
        Assert.Equal(ProfileType.Process, single.Type);
        Assert.Equal("ap-south-1", single.Region);
    }

    [Fact]
    public void Defaults_to_config_type_when_no_credential_keys()
    {
        const string config = """
            [profile delta]
            region = us-west-2
            output = json
            """;

        var single = Assert.Single(AwsConfigParser.Parse(config));
        Assert.Equal(ProfileType.Config, single.Type);
    }

    [Fact]
    public void Returns_empty_for_blank_input()
    {
        Assert.Empty(AwsConfigParser.Parse(""));
        Assert.Empty(AwsConfigParser.Parse("   \n  \n"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AwsConfigParserTests`
Expected: FAIL — `AwsConfigParser` does not exist / does not compile.

- [ ] **Step 3: Implement the parser**

Create `src/AwsProfileSelector/Config/AwsConfigParser.cs`:

```csharp
using AwsProfileSelector.Model;

namespace AwsProfileSelector.Config;

/// <summary>Parses AWS shared-config text into the list of selectable profiles.</summary>
public static class AwsConfigParser
{
    public static IReadOnlyList<AwsProfile> Parse(string content)
    {
        var profiles = new List<AwsProfile>();

        string? currentName = null;
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Flush()
        {
            if (currentName is null)
            {
                return;
            }

            keys.TryGetValue("region", out var region);
            profiles.Add(new AwsProfile(currentName, region, InferType(keys)));
            keys.Clear();
            currentName = null;
        }

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                Flush();
                var inner = line[1..^1].Trim();
                if (inner == "default")
                {
                    currentName = "default";
                }
                else if (inner.StartsWith("profile ", StringComparison.Ordinal))
                {
                    currentName = inner["profile ".Length..].Trim();
                }
                // any other section (sso-session, services, ...) is ignored
                continue;
            }

            if (currentName is null)
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            keys[key] = value;
        }

        Flush();
        return profiles;
    }

    private static ProfileType InferType(IReadOnlyDictionary<string, string> keys)
    {
        if (keys.ContainsKey("role_arn"))
        {
            return ProfileType.Role;
        }

        if (keys.ContainsKey("sso_session") || keys.ContainsKey("sso_account_id"))
        {
            return ProfileType.Sso;
        }

        if (keys.ContainsKey("aws_access_key_id"))
        {
            return ProfileType.Static;
        }

        if (keys.ContainsKey("credential_process"))
        {
            return ProfileType.Process;
        }

        return ProfileType.Config;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter AwsConfigParserTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: parse AWS config into selectable profiles"
```

---

## Task 4: Profile label formatter

**Files:**
- Create: `src/AwsProfileSelector/Ui/ProfileFormatter.cs`
- Test: `tests/AwsProfileSelector.Tests/ProfileFormatterTests.cs`

**Format rules:** `"{name}  ({region}, {type})"` when region is present; `"{name}  ({type})"` when region is null. Type is lowercased (`sso`, `role`, `static`, `process`, `config`).

- [ ] **Step 1: Write the failing tests**

Create `tests/AwsProfileSelector.Tests/ProfileFormatterTests.cs`:

```csharp
using AwsProfileSelector.Model;
using AwsProfileSelector.Ui;
using Xunit;

namespace AwsProfileSelector.Tests;

public class ProfileFormatterTests
{
    [Fact]
    public void Formats_name_region_and_type()
    {
        var label = ProfileFormatter.Format(new AwsProfile("alpha", "eu-west-1", ProfileType.Role));
        Assert.Equal("alpha  (eu-west-1, role)", label);
    }

    [Fact]
    public void Omits_region_when_null()
    {
        var label = ProfileFormatter.Format(new AwsProfile("beta", null, ProfileType.Sso));
        Assert.Equal("beta  (sso)", label);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ProfileFormatterTests`
Expected: FAIL — `ProfileFormatter` does not exist.

- [ ] **Step 3: Implement the formatter**

Create `src/AwsProfileSelector/Ui/ProfileFormatter.cs`:

```csharp
using AwsProfileSelector.Model;

namespace AwsProfileSelector.Ui;

/// <summary>Renders a profile as a single-line label for the selection list.</summary>
public static class ProfileFormatter
{
    public static string Format(AwsProfile profile)
    {
        var type = profile.Type.ToString().ToLowerInvariant();
        return profile.Region is { Length: > 0 } region
            ? $"{profile.Name}  ({region}, {type})"
            : $"{profile.Name}  ({type})";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ProfileFormatterTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add profile label formatter"
```

---

## Task 5: Config file locator

**Files:**
- Create: `src/AwsProfileSelector/Config/AwsConfigLocator.cs`
- Test: `tests/AwsProfileSelector.Tests/AwsConfigLocatorTests.cs`

**Rule:** Return the value of `AWS_CONFIG_FILE` if set and non-empty; otherwise `<home>/.aws/config`. The locator takes the env value and home directory as parameters so it is pure and testable.

- [ ] **Step 1: Write the failing tests**

Create `tests/AwsProfileSelector.Tests/AwsConfigLocatorTests.cs`:

```csharp
using AwsProfileSelector.Config;
using Xunit;

namespace AwsProfileSelector.Tests;

public class AwsConfigLocatorTests
{
    [Fact]
    public void Uses_env_override_when_set()
    {
        var path = AwsConfigLocator.Resolve(envConfigFile: "/custom/aws.cfg", homeDirectory: "/home/u");
        Assert.Equal("/custom/aws.cfg", path);
    }

    [Fact]
    public void Falls_back_to_home_aws_config()
    {
        var path = AwsConfigLocator.Resolve(envConfigFile: null, homeDirectory: "/home/u");
        Assert.Equal(Path.Combine("/home/u", ".aws", "config"), path);
    }

    [Fact]
    public void Treats_empty_env_as_unset()
    {
        var path = AwsConfigLocator.Resolve(envConfigFile: "", homeDirectory: "/home/u");
        Assert.Equal(Path.Combine("/home/u", ".aws", "config"), path);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AwsConfigLocatorTests`
Expected: FAIL — `AwsConfigLocator` does not exist.

- [ ] **Step 3: Implement the locator**

Create `src/AwsProfileSelector/Config/AwsConfigLocator.cs`:

```csharp
namespace AwsProfileSelector.Config;

/// <summary>Resolves the path to the AWS shared config file.</summary>
public static class AwsConfigLocator
{
    public static string Resolve(string? envConfigFile, string homeDirectory)
    {
        if (!string.IsNullOrEmpty(envConfigFile))
        {
            return envConfigFile;
        }

        return Path.Combine(homeDirectory, ".aws", "config");
    }

    /// <summary>Resolves using the current process environment.</summary>
    public static string Resolve()
    {
        var env = Environment.GetEnvironmentVariable("AWS_CONFIG_FILE");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Resolve(env, home);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter AwsConfigLocatorTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: resolve AWS config file path"
```

---

## Task 6: zshrc managed-block writer

**Files:**
- Create: `src/AwsProfileSelector/Shell/ZshrcWriter.cs`
- Test: `tests/AwsProfileSelector.Tests/ZshrcWriterTests.cs`

**Behavior:** `UpsertBlock(filePath, blockId, body)` maintains a marker-delimited block:
```
# >>> aws-profile-selector {blockId} >>>
{body}
# <<< aws-profile-selector {blockId} <<<
```
If the file does not exist, it is created containing just the block. If the block markers are already present, the lines between (and including) the markers are replaced in place, preserving all other content. If the file exists without the block, the block is appended (separated by a blank line). The file always ends with a trailing newline.

- [ ] **Step 1: Write the failing tests**

Create `tests/AwsProfileSelector.Tests/ZshrcWriterTests.cs`:

```csharp
using AwsProfileSelector.Shell;
using Xunit;

namespace AwsProfileSelector.Tests;

public class ZshrcWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"zshrc-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void Creates_file_with_block_when_missing()
    {
        ZshrcWriter.UpsertBlock(_path, "init", "awsp() { :; }");

        var text = File.ReadAllText(_path);
        Assert.Contains("# >>> aws-profile-selector init >>>", text);
        Assert.Contains("awsp() { :; }", text);
        Assert.Contains("# <<< aws-profile-selector init <<<", text);
        Assert.EndsWith("\n", text);
    }

    [Fact]
    public void Appends_block_preserving_existing_content()
    {
        File.WriteAllText(_path, "export EDITOR=vim\n");

        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=alpha");

        var text = File.ReadAllText(_path);
        Assert.Contains("export EDITOR=vim", text);
        Assert.Contains("export AWS_PROFILE=alpha", text);
    }

    [Fact]
    public void Replaces_existing_block_in_place()
    {
        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=alpha");
        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=beta");

        var text = File.ReadAllText(_path);
        Assert.Contains("export AWS_PROFILE=beta", text);
        Assert.DoesNotContain("export AWS_PROFILE=alpha", text);
        // Exactly one start marker remains
        var occurrences = text.Split("# >>> aws-profile-selector default >>>").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Updates_one_block_without_touching_another()
    {
        ZshrcWriter.UpsertBlock(_path, "init", "awsp() { :; }");
        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=alpha");

        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=beta");

        var text = File.ReadAllText(_path);
        Assert.Contains("awsp() { :; }", text);
        Assert.Contains("export AWS_PROFILE=beta", text);
        Assert.DoesNotContain("export AWS_PROFILE=alpha", text);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ZshrcWriterTests`
Expected: FAIL — `ZshrcWriter` does not exist.

- [ ] **Step 3: Implement the writer**

Create `src/AwsProfileSelector/Shell/ZshrcWriter.cs`:

```csharp
namespace AwsProfileSelector.Shell;

/// <summary>Maintains idempotent marker-delimited managed blocks in a shell rc file.</summary>
public static class ZshrcWriter
{
    public static void UpsertBlock(string filePath, string blockId, string body)
    {
        var start = $"# >>> aws-profile-selector {blockId} >>>";
        var end = $"# <<< aws-profile-selector {blockId} <<<";
        var blockLines = new[] { start, body, end };

        var lines = File.Exists(filePath)
            ? new List<string>(File.ReadAllLines(filePath))
            : new List<string>();

        var startIndex = lines.IndexOf(start);
        if (startIndex >= 0)
        {
            var endIndex = lines.IndexOf(end, startIndex);
            if (endIndex < 0)
            {
                endIndex = lines.Count - 1;
            }

            lines.RemoveRange(startIndex, endIndex - startIndex + 1);
            lines.InsertRange(startIndex, blockLines);
        }
        else
        {
            if (lines.Count > 0 && lines[^1].Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(blockLines);
        }

        File.WriteAllText(filePath, string.Join('\n', lines) + "\n");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ZshrcWriterTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add idempotent zshrc managed-block writer"
```

---

## Task 7: Interactive profile selector

**Files:**
- Create: `src/AwsProfileSelector/Ui/ProfileSelector.cs`

This wraps Spectre prompts. It is verified manually (interactive prompts are not unit-tested); its pure label logic is already covered by `ProfileFormatterTests`.

- [ ] **Step 1: Implement the selector**

Create `src/AwsProfileSelector/Ui/ProfileSelector.cs`:

```csharp
using AwsProfileSelector.Model;
using Spectre.Console;

namespace AwsProfileSelector.Ui;

/// <summary>Presents the interactive profile and apply-mode prompts on a given console.</summary>
public sealed class ProfileSelector
{
    private readonly IAnsiConsole _console;

    public ProfileSelector(IAnsiConsole console) => _console = console;

    public AwsProfile SelectProfile(IReadOnlyList<AwsProfile> profiles)
    {
        var prompt = new SelectionPrompt<AwsProfile>()
            .Title("Select an [green]AWS profile[/]:")
            .PageSize(15)
            .EnableSearch()
            .SearchPlaceholderText("type to filter…")
            .UseConverter(ProfileFormatter.Format)
            .AddChoices(profiles);

        return _console.Prompt(prompt);
    }

    public ApplyMode SelectApplyMode()
    {
        const string sessionLabel = "This terminal session only";
        const string defaultLabel = "Save as default (also applies now)";

        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Apply how?")
                .AddChoices(sessionLabel, defaultLabel));

        return choice == defaultLabel ? ApplyMode.Default : ApplyMode.Session;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add interactive profile selector prompts"
```

---

## Task 8: Select command (default)

**Files:**
- Create: `src/AwsProfileSelector/Commands/SelectCommand.cs`

**Flow:**
1. Resolve config path (`AwsConfigLocator.Resolve()`); if the file is missing, print an error to stderr and return exit code 1 (nothing to stdout).
2. Parse the file; if no profiles, print an error to stderr and return 1.
3. Show the profile prompt and apply-mode prompt on the stderr console.
4. Always write `export AWS_PROFILE=<name>` to **stdout** via plain `Console.Out`.
5. If `ApplyMode.Default`, upsert the same export into the `default` block of `~/.zshrc`; on `IOException`/`UnauthorizedAccessException` print a warning to stderr but still succeed (the session export already printed).
6. Print a confirmation to stderr and return 0.

The command receives the stderr console; Task 10 wires it in.

- [ ] **Step 1: Implement the command**

Create `src/AwsProfileSelector/Commands/SelectCommand.cs`:

```csharp
using AwsProfileSelector.Config;
using AwsProfileSelector.Model;
using AwsProfileSelector.Shell;
using AwsProfileSelector.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AwsProfileSelector.Commands;

public sealed class SelectSettings : CommandSettings { }

/// <summary>Default command: pick a profile and apply it to the session or as default.</summary>
public sealed class SelectCommand : Command<SelectSettings>
{
    private readonly IAnsiConsole _err;

    public SelectCommand(IAnsiConsole err) => _err = err;

    protected override int Execute(
        CommandContext context,
        SelectSettings settings,
        CancellationToken cancellationToken)
    {
        var configPath = AwsConfigLocator.Resolve();
        if (!File.Exists(configPath))
        {
            _err.MarkupLineInterpolated($"[red]No AWS config found at[/] {configPath}");
            return 1;
        }

        var profiles = AwsConfigParser.Parse(File.ReadAllText(configPath));
        if (profiles.Count == 0)
        {
            _err.MarkupLineInterpolated($"[red]No profiles found in[/] {configPath}");
            return 1;
        }

        var selector = new ProfileSelector(_err);
        var profile = selector.SelectProfile(profiles);
        var mode = selector.SelectApplyMode();

        // stdout is consumed by the shell wrapper's eval — keep it plain.
        Console.Out.WriteLine($"export AWS_PROFILE={profile.Name}");

        if (mode == ApplyMode.Default)
        {
            var zshrc = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".zshrc");
            try
            {
                ZshrcWriter.UpsertBlock(zshrc, "default", $"export AWS_PROFILE={profile.Name}");
                _err.MarkupLineInterpolated(
                    $"[green]✓[/] Saved [bold]{profile.Name}[/] as default in {zshrc}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _err.MarkupLineInterpolated(
                    $"[yellow]![/] Could not write {zshrc}: {ex.Message}");
            }
        }

        _err.MarkupLineInterpolated(
            $"[green]✓[/] AWS_PROFILE=[bold]{profile.Name}[/] set for this session");
        return 0;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add select command"
```

---

## Task 9: Init command

**Files:**
- Create: `src/AwsProfileSelector/Commands/InitCommand.cs`

**Flow:** Upsert the `awsp` shell function into the `init` block of `~/.zshrc`, then print a stderr note to reload the shell. The function body is:
`awsp() { eval "$(aws-profile-selector select "$@")"; }`

- [ ] **Step 1: Implement the command**

Create `src/AwsProfileSelector/Commands/InitCommand.cs`:

```csharp
using AwsProfileSelector.Shell;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AwsProfileSelector.Commands;

public sealed class InitSettings : CommandSettings { }

/// <summary>Installs the `awsp` shell function into ~/.zshrc.</summary>
public sealed class InitCommand : Command<InitSettings>
{
    private readonly IAnsiConsole _err;

    public InitCommand(IAnsiConsole err) => _err = err;

    protected override int Execute(
        CommandContext context,
        InitSettings settings,
        CancellationToken cancellationToken)
    {
        var zshrc = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".zshrc");

        const string body = "awsp() { eval \"$(aws-profile-selector select \"$@\")\"; }";

        try
        {
            ZshrcWriter.UpsertBlock(zshrc, "init", body);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _err.MarkupLineInterpolated($"[red]Could not write {zshrc}:[/] {ex.Message}");
            return 1;
        }

        _err.MarkupLineInterpolated(
            $"[green]✓[/] Installed [bold]awsp[/] in {zshrc}");
        _err.MarkupLine("Run [bold]source ~/.zshrc[/] or open a new terminal, then run [bold]awsp[/].");
        return 0;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add init command to install the awsp shell function"
```

---

## Task 10: Entry point and command wiring

**Files:**
- Modify: `src/AwsProfileSelector/Program.cs`

**Wiring:** Create one stderr-backed `IAnsiConsole`, register it with a simple `ITypeRegistrar` so commands receive it via constructor injection, set `SelectCommand` as the default command, and add `init`.

- [ ] **Step 1: Add a minimal type registrar**

Create `src/AwsProfileSelector/TypeRegistrar.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace AwsProfileSelector;

/// <summary>Bridges Spectre.Console.Cli to Microsoft.Extensions.DependencyInjection.</summary>
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar(IServiceCollection services) => _services = services;

    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());

    public void Register(Type service, Type implementation) =>
        _services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        _services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        _services.AddSingleton(service, _ => factory());
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider) => _provider = provider;

    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

- [ ] **Step 2: Add the DI package**

Run:

```bash
dotnet add src/AwsProfileSelector/AwsProfileSelector.csproj package Microsoft.Extensions.DependencyInjection --version 9.0.0
```

- [ ] **Step 3: Write Program.cs**

Replace `src/AwsProfileSelector/Program.cs` with:

```csharp
using AwsProfileSelector;
using AwsProfileSelector.Commands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

// All interactive UI goes to stderr so stdout carries only the `export` line.
IAnsiConsole err = AnsiConsole.Create(new AnsiConsoleSettings
{
    Out = new AnsiConsoleOutput(Console.Error),
});

var services = new ServiceCollection();
services.AddSingleton(err);

var app = new CommandApp<SelectCommand>(new TypeRegistrar(services));
app.Configure(config =>
{
    config.SetApplicationName("aws-profile-selector");
    config.AddCommand<SelectCommand>("select")
        .WithDescription("Select an AWS profile (default command).");
    config.AddCommand<InitCommand>("init")
        .WithDescription("Install the awsp shell function into ~/.zshrc.");
});

return app.Run(args);
```

- [ ] **Step 4: Build and run the full test suite**

Run: `dotnet build && dotnet test`
Expected: `Build succeeded.` and all tests PASS (14 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: wire CommandApp with stderr console and DI"
```

---

## Task 11: Manual end-to-end verification and README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Verify errors never touch stdout**

Point the tool at a nonexistent config so it fails fast before any prompt, and confirm stdout stays empty (so the eval wrapper would no-op):

```bash
AWS_CONFIG_FILE=/tmp/awsp-does-not-exist.cfg dotnet run --project src/AwsProfileSelector -- select 1>/tmp/awsp.out 2>/tmp/awsp.err; echo "exit=$?"; echo "STDOUT:"; cat /tmp/awsp.out; echo "STDERR:"; cat /tmp/awsp.err
```

Expected: exit=1, STDOUT empty, STDERR contains "No AWS config found" (confirms errors never reach stdout).

- [ ] **Step 2: Verify interactive selection against the real config**

Run: `dotnet run --project src/AwsProfileSelector -- select`
Interact: type to filter, pick a profile, choose "This terminal session only".
Expected: on screen (stderr) you see the prompts and the `✓ AWS_PROFILE=… set` line; the `export AWS_PROFILE=…` line is printed to stdout (visible because not redirected).

- [ ] **Step 3: Verify init writes the managed block**

Run: `dotnet run --project src/AwsProfileSelector -- init`
Then inspect: `grep -n "aws-profile-selector init" ~/.zshrc`
Expected: the `awsp` function block is present. Re-running `init` keeps exactly one block.

- [ ] **Step 4: Install as a global tool and exercise the wrapper**

```bash
dotnet pack src/AwsProfileSelector -o ./nupkg
dotnet tool install --global --add-source ./nupkg aws-profile-selector
exec zsh           # reload so the awsp function is available
awsp               # full flow via the eval wrapper
echo "$AWS_PROFILE" # should show the selected profile in the current shell
```

Expected: after picking a profile, `$AWS_PROFILE` reflects the selection in the current shell. Choosing "Save as default" also adds a `default` export block to `~/.zshrc`.

- [ ] **Step 5: Write the README usage section**

Replace `README.md` with:

```markdown
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
chosen profile to your current shell.

## Usage

```bash
awsp
```

Type to filter, pick a profile, then choose:

- **This terminal session only** — sets `AWS_PROFILE` in the current shell.
- **Save as default** — also persists `export AWS_PROFILE=…` to `~/.zshrc` so
  new terminals start with it.
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "docs: add README usage and verify end-to-end"
```

---

## Done

All spec requirements are covered: interactive selection with substring search,
session-vs-default apply, stderr/stdout isolation for the eval wrapper, the
`init` installer, zsh-only persistence via a managed `~/.zshrc` block, profile
type/region display, `AWS_CONFIG_FILE` support, error handling, packaging as a
global tool, and unit tests for all pure logic.

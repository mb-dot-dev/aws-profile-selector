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

        // stdout is consumed by the shell wrapper's eval — single-quote to prevent expansion.
        Console.Out.WriteLine($"export AWS_PROFILE={ShellQuote.Escape(profile.Name)}");

        if (mode == ApplyMode.Default)
        {
            var zshrc = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".zshrc");
            try
            {
                ZshrcWriter.UpsertBlock(zshrc, "default", $"export AWS_PROFILE={ShellQuote.Escape(profile.Name)}");
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

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

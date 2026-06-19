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

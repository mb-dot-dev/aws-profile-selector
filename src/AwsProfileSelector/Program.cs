using AwsProfileSelector;
using AwsProfileSelector.Commands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

// All interactive UI goes to stderr so stdout carries only the `export` line.
// Force interactivity: under the `awsp` wrapper stdout is captured by `eval "$(...)"`,
// so Spectre's default detection (which keys off stdout) would otherwise refuse the
// prompt. stderr is the real terminal, so the prompt renders fine there.
IAnsiConsole err = AnsiConsole.Create(new AnsiConsoleSettings
{
    Out = new AnsiConsoleOutput(Console.Error),
    Interactive = InteractionSupport.Yes,
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

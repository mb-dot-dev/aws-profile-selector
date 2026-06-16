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

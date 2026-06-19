using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console.Cli;

namespace AwsProfileSelector;

/// <summary>Bridges Spectre.Console.Cli to Microsoft.Extensions.DependencyInjection.</summary>
/// <remarks>
/// Uses TryAdd semantics so that services registered before CommandApp construction
/// (e.g. our stderr-backed IAnsiConsole) are not overwritten by Spectre's own
/// RegisterInstance calls during command wiring.
/// </remarks>
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar(IServiceCollection services) => _services = services;

    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());

    public void Register(Type service, Type implementation) =>
        _services.TryAddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        _services.TryAddSingleton(service, _ => implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        _services.TryAddSingleton(service, _ => factory());
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

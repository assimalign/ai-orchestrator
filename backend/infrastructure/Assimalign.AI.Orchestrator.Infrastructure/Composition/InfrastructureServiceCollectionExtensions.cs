using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Application.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Assimalign.AI.Orchestrator.Infrastructure.Composition;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAssimalignAiOrchestratorRuntime(
        this IServiceCollection services,
        OrchestratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        var runtime = OrchestratorRuntimeFactory.CreateAsync(settings).GetAwaiter().GetResult();
        services.AddSingleton(runtime);
        return services;
    }

    public static IServiceCollection AddAssimalignAiOrchestratorRuntimeHandle(
        this IServiceCollection services,
        OrchestratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton(new OrchestratorRuntimeHandle(
            cancellationToken => OrchestratorRuntimeFactory.CreateAsync(settings, cancellationToken)));
        return services;
    }
}

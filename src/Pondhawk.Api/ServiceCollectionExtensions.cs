using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pondhawk.Api.Context;

namespace Pondhawk.Api;

/// <summary>
/// Root DI registration for the Pondhawk.Api web kit.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the per-request <see cref="IRequestContext"/> and the <c>IHttpContextAccessor</c>
    /// it and the diagnostics middleware rely on. Endpoint modules, gateway auth, JSON, and pipeline
    /// behaviors are registered by their own dedicated extensions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPondhawkApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<IRequestContext, RequestContext>();

        return services;
    }
}

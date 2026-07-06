using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pondhawk.Api;
using Pondhawk.Api.Context;
using Pondhawk.Api.Identity;
using Pondhawk.Api.Middleware;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests;

public class WiringTests
{
    [Fact]
    public void AddPondhawkApi_RegistersRequestContextAndHttpContextAccessor()
    {
        var services = new ServiceCollection();
        services.AddPondhawkApi();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<IRequestContext>().ShouldNotBeNull();
        provider.GetService<IHttpContextAccessor>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGatewayTokenAuthentication_RegistersEncoder()
    {
        var key = Convert.ToBase64String(TestKeys.SigningKey);
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddGatewayTokenAuthentication(key);

        var provider = services.BuildServiceProvider();
        provider.GetService<IGatewayTokenEncoder>().ShouldBeOfType<GatewayTokenJwtEncoder>();
    }

    [Fact]
    public void AddGatewayHeaderAuthentication_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Should.NotThrow(() => services.AddGatewayHeaderAuthentication());

        services.BuildServiceProvider().ShouldNotBeNull();
    }

    [Fact]
    public void MiddlewareExtensions_RegisterOnPipeline()
    {
        var app = WebApplication.CreateBuilder().Build();

        Should.NotThrow(() =>
        {
            app.UseDiagnosticsEnrichment();
            app.UseDiagnosticsMonitor();
            app.UseRequestLogging();
        });
    }
}

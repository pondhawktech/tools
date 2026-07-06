using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pondhawk.Api.Context;
using Pondhawk.Api.Identity;
using Serilog;

namespace Pondhawk.Api.Tests;

internal static class TestBootstrap
{
    /// <summary>
    /// Runs before any test type is loaded, so the static Serilog loggers captured by the
    /// diagnostics/request-logging middlewares are Debug-enabled (exercising their full bodies).
    /// </summary>
    [ModuleInitializer]
    public static void Init()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .CreateLogger();
    }
}

/// <summary>Shared minimal <see cref="IServiceProvider"/> for executing <c>IResult</c> instances.</summary>
internal static class TestServices
{
    public static IServiceProvider Provider { get; } = BuildProvider();

    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddRouting();
        return services.BuildServiceProvider();
    }

    /// <summary>Creates a <see cref="DefaultHttpContext"/> with request services and a buffer response body.</summary>
    public static DefaultHttpContext HttpContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = Provider,
            Response = { Body = new MemoryStream() },
        };
    }
}

/// <summary>A minimal settable <see cref="IRequestContext"/> for tests.</summary>
internal sealed class FakeRequestContext : IRequestContext
{
    public string CorrelationId { get; set; }
    public ClaimsPrincipal Caller { get; set; }
    public string CallerGatewayToken { get; set; }
}

/// <summary>A tiny <see cref="IOptionsMonitor{T}"/> that always returns a single options instance.</summary>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    private readonly T _value;

    public TestOptionsMonitor(T value) => _value = value;

    public T CurrentValue => _value;

    public T Get(string name) => _value;

    public IDisposable OnChange(Action<T, string> listener) => new Noop();

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

internal static class TestKeys
{
    /// <summary>A 32-byte HS256 signing key.</summary>
    public static byte[] SigningKey { get; } = Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF");

    /// <summary>A different 32-byte HS256 signing key.</summary>
    public static byte[] OtherKey { get; } = Encoding.UTF8.GetBytes("FEDCBA9876543210FEDCBA9876543210");

    public static ClaimSet SampleClaimSet() => new()
    {
        UserId = "u-123",
        UserName = "jdoe",
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Roles = new List<string> { "admin", "user" },
    };
}

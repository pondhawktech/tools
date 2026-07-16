// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pondhawk.Api.Json;
using Shouldly;
using Xunit;
using MvcJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Pondhawk.Api.Tests.Json;

public sealed class Sample
{
    public string Name { get; set; } = string.Empty;
    public string Blank { get; set; } = string.Empty;
    public IList<int> Items { get; set; } = new List<int>();
    public IList<int> Populated { get; set; } = new List<int>();
    public int Count { get; set; }
    public int Zero { get; set; }
    public DateTime When { get; set; }
}

public class CompactJsonTypeInfoResolverTests
{
    [Fact]
    public void Serialize_OmitsEmptyMembers()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new CompactJsonTypeInfoResolver(),
        };

        var obj = new Sample
        {
            Name = "hello",
            Blank = "   ",
            Items = new List<int>(),
            Populated = new List<int> { 1, 2 },
            Count = 7,
            Zero = 0,
            When = DateTime.MinValue,
        };

        var json = JsonSerializer.Serialize(obj, options);

        json.ShouldContain("hello");
        json.ShouldContain("Populated");
        json.ShouldContain("Count");
        json.ShouldNotContain("Blank");
        json.ShouldNotContain("Items");
        json.ShouldNotContain("Zero");
        json.ShouldNotContain("When");
    }
}

public sealed class NumericSample
{
    public long BigZero { get; set; }
    public long BigVal { get; set; }
    public double Dbl { get; set; }
    public decimal Dec { get; set; }
    public DateTimeOffset When { get; set; }
    public TimeSpan Span { get; set; }
    public int? NullableSet { get; set; }
    public int? NullableNull { get; set; }
}

public class CompactJsonNumericTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new CompactJsonTypeInfoResolver(),
    };

    [Fact]
    public void Serialize_OmitsZeroNumericsMinTemporalsAndNulls()
    {
        var obj = new NumericSample
        {
            BigZero = 0,
            BigVal = 42,
            Dbl = 0d,
            Dec = 0m,
            When = DateTimeOffset.MinValue,
            Span = TimeSpan.Zero,
            NullableSet = 5,
            NullableNull = null,
        };

        var json = JsonSerializer.Serialize(obj, Options);

        json.ShouldContain("BigVal");
        json.ShouldContain("NullableSet");
        json.ShouldNotContain("BigZero");
        json.ShouldNotContain("Dbl");
        json.ShouldNotContain("Dec");
        json.ShouldNotContain("When");
        json.ShouldNotContain("Span");
        json.ShouldNotContain("NullableNull");
    }

    [Fact]
    public void Serialize_KeepsPopulatedTemporalsAndNumbers()
    {
        var obj = new NumericSample
        {
            Dbl = 1.5d,
            Dec = 2.25m,
            When = DateTimeOffset.UnixEpoch.AddDays(1),
            Span = TimeSpan.FromMinutes(3),
        };

        var json = JsonSerializer.Serialize(obj, Options);

        json.ShouldContain("Dbl");
        json.ShouldContain("Dec");
        json.ShouldContain("When");
        json.ShouldContain("Span");
    }
}

public class PascalJsonNamingPolicyTests
{
    [Fact]
    public void ConvertName_ReturnsUnchanged()
    {
        var policy = new PascalJsonNamingPolicy();
        policy.ConvertName("SomeProperty").ShouldBe("SomeProperty");
        policy.ConvertName("lowercase").ShouldBe("lowercase");
    }
}

public class JsonServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPondhawkJson_RegistersSingletonWithCompactResolver()
    {
        var services = new ServiceCollection();
        services.AddPondhawkJson();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<JsonSerializerOptions>();

        options.ShouldNotBeNull();
        options.TypeInfoResolver.ShouldBeOfType<CompactJsonTypeInfoResolver>();
    }

    [Fact]
    public void AddPondhawkJson_ConfiguresJsonOptions()
    {
        var services = new ServiceCollection();
        services.AddPondhawkJson();

        var provider = services.BuildServiceProvider();
        var jsonOptions = provider.GetRequiredService<IOptions<MvcJsonOptions>>();

        jsonOptions.Value.SerializerOptions.TypeInfoResolver.ShouldBeOfType<CompactJsonTypeInfoResolver>();
    }

    [Fact]
    public void AddPondhawkJson_InvokesConfigure()
    {
        var services = new ServiceCollection();
        services.AddPondhawkJson(o => o.WriteIndented = true);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<JsonSerializerOptions>();

        options.WriteIndented.ShouldBeTrue();
    }
}

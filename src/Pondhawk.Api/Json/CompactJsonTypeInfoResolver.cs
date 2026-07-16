// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Pondhawk.Api.Json;

/// <summary>
/// A <see cref="DefaultJsonTypeInfoResolver"/> that omits "empty" members at serialization time:
/// blank/whitespace strings, empty collections, numeric zeros, and min-value dates/times — producing
/// compact payloads without per-type attributes.
/// </summary>
public class CompactJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    /// <inheritdoc />
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var info = base.GetTypeInfo(type, options);

        if (info.Kind == JsonTypeInfoKind.Object)
        {
            foreach (var property in info.Properties)
            {
                var predicate = BuildShouldSerialize(property.PropertyType);
                if (predicate is not null)
                    property.ShouldSerialize = predicate;
            }
        }

        return info;
    }

    private static Func<object, object?, bool>? BuildShouldSerialize(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (type == typeof(string))
            return static (_, value) => !string.IsNullOrWhiteSpace(value as string);

        if (typeof(IEnumerable).IsAssignableFrom(type))
            return static (_, value) => value switch
            {
                ICollection collection => collection.Count > 0,
                IEnumerable sequence => HasAny(sequence),
                _ => false,
            };

        if (type == typeof(DateTime))
            return static (_, value) => value is DateTime d && d != DateTime.MinValue;

        if (type == typeof(DateTimeOffset))
            return static (_, value) => value is DateTimeOffset d && d != DateTimeOffset.MinValue;

        if (type == typeof(TimeSpan))
            return static (_, value) => value is TimeSpan t && t != TimeSpan.Zero;

        if (type.IsPrimitive || type == typeof(decimal))
            return static (_, value) => value is not null && !IsZero(value);

        return null;
    }

    private static bool HasAny(IEnumerable sequence)
    {
        var enumerator = sequence.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private static bool IsZero(object value) => value switch
    {
        int i => i == 0,
        long l => l == 0L,
        short s => s == 0,
        byte b => b == 0,
        sbyte sb => sb == 0,
        ushort us => us == 0,
        uint ui => ui == 0U,
        ulong ul => ul == 0UL,
        double d => d == 0d,
        float f => f == 0f,
        decimal m => m == 0m,
        _ => false,
    };
}

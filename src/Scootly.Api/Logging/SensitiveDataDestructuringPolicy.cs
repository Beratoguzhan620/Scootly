using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Scootly.Api.Logging;

/// <summary>
/// Yapılandırılmış loglamada (<c>{@Nesne}</c>) nesnelerin hassas özelliklerini maskeler.
/// Karar tipin adına göre değil, <b>özellik adlarına</b> göre verilir: örn. <c>LoginRequest.Password</c> maskelenir,
/// <c>LoginRequest.Email</c> olduğu gibi yazılır.
/// </summary>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    public const string Mask = "***MASKED***";

    private static readonly string[] SensitiveNameFragments =
    [
        "password", "secret", "token", "apikey", "authorization", "signature", "connectionstring", "cardnumber", "cvv"
    ];

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]?> SensitiveTypeCache = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        result = null!;

        var properties = SensitiveTypeCache.GetOrAdd(value.GetType(), GetPropertiesIfSensitive);

        if (properties is null)
            return false;

        var logProperties = new List<LogEventProperty>(properties.Length);

        foreach (var property in properties)
        {
            LogEventPropertyValue propertyValue;

            if (IsSensitive(property.Name))
            {
                propertyValue = new ScalarValue(Mask);
            }
            else
            {
                object? raw;

                try
                {
                    raw = property.GetValue(value);
                }
                catch (Exception)
                {
                    raw = "<okunamadı>";
                }

                propertyValue = propertyValueFactory.CreatePropertyValue(raw, destructureObjects: true);
            }

            logProperties.Add(new LogEventProperty(property.Name, propertyValue));
        }

        result = new StructureValue(logProperties, value.GetType().Name);
        return true;
    }

    public static bool IsSensitive(string name)
        => SensitiveNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>Yalnızca en az bir hassas özelliği olan karmaşık tipler bu politikayla ele alınır; diğerleri Serilog'un varsayılanına bırakılır.</summary>
    private static PropertyInfo[]? GetPropertiesIfSensitive(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(IEnumerable).IsAssignableFrom(type))
            return null;

        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        return properties.Any(p => IsSensitive(p.Name)) ? properties : null;
    }
}

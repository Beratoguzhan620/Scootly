using Serilog.Core;
using Serilog.Events;

namespace Scootly.Api.Logging;

public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "token",
        "clientsecret"
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        result = null!;

        var type = value.GetType();
        var propertyName = type.Name;

        if (!SensitivePropertyNames.Any(sensitive => propertyName.Contains(sensitive, StringComparison.OrdinalIgnoreCase)))
            return false;

        result = new ScalarValue("***MASKED***");
        return true;
    }
}
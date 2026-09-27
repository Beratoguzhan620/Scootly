namespace Scootly.Application.IntegrationEvents;

/// <summary>Outbox'taki olay türü ve RabbitMQ yönlendirme anahtarı olarak kullanılır.</summary>
public static class IntegrationEventNames
{
    public const string RideCompleted = "RideCompleted";
    public const string RideAbandoned = "RideAbandoned";
    public const string VehicleBatteryLow = "VehicleBatteryLow";
    public const string VehicleStatusChanged = "VehicleStatusChanged";
}

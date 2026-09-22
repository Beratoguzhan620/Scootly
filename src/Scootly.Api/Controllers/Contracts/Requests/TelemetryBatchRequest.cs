using System.ComponentModel.DataAnnotations;
using Scootly.Application.Telemetry.Commands;

namespace Scootly.Api.Contracts.Requests;

/// <summary>Bir cihazın tek istekte gönderdiği ölçümler (51. gün).</summary>
public sealed record TelemetryBatchRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(IngestTelemetryBatchCommand.MaxBatchSize)]
    public IReadOnlyList<TelemetryReadingRequest> Readings { get; init; } = [];

    public IngestTelemetryBatchCommand ToCommand() =>
        new(Readings
            .Select(r => new TelemetryReadingInput(
                r.DeviceId, r.Latitude, r.Longitude, r.BatteryPercentage, r.RecordedAt))
            .ToList());
}

/// <summary>Tek bir ölçüm.</summary>
/// <remarks>
/// <para>
/// <c>DeviceId</c> gövdede. 26. gündeki OWASP taramasının dersine göre bu
/// şüpheli görünüyor — orada <c>DriverId</c>'yi gövdeden kaldırmıştık. Fark
/// şu: orada gövdedeki kimlik <b>yetki kararını</b> etkiliyordu (A sürücüsü B
/// adına işlem yapabiliyordu). Burada ise yetki kararı token'daki cihaz
/// kimliğinden veriliyor ve controller gövdedeki kimliğin ona eşit olduğunu
/// doğruluyor. Gövdedeki alan yalnızca bir filoyu tek istekte gönderebilen
/// ağ geçidi cihazları için var — ve o durumda da token'daki kimlikle
/// eşleşmesi şart.
/// </para>
/// <para>
/// <c>RecordedAt</c> cihazdan geliyor, sunucu saatinden değil: cihaz çevrimdışı
/// kalıp ölçümleri biriktirmiş olabilir. Sunucu saatini kullanmak, üç dakika
/// önce alınmış bir ölçümü şimdi alınmış gibi kaydetmek olurdu.
/// </para>
/// </remarks>
public sealed record TelemetryReadingRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string DeviceId { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0, 100)]
    public int BatteryPercentage { get; init; }

    [Required]
    public DateTime RecordedAt { get; init; }
}

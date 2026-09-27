using Scootly.Domain.Common;

namespace Scootly.Domain.FieldOps;

/// <summary>
/// Saha ekibine verilen iş: batarya değişimi, taşıma, onarım (64. gün).
/// </summary>
/// <remarks>
/// <para>
/// Bütün özellikler <c>{ get; private set; }</c>. Salt okunur
/// (<c>{ get; }</c>) bir otomatik özellik EF Core'un varsayılan kuralıyla
/// sütuna eşlenmiyor — <c>Ride.StartedAt</c> 24. günde tam olarak bu yüzden
/// hiçbir migration'a girmemişti.
/// </para>
/// <para>
/// Aynı araç için aynı türde yalnızca BİR açık görev olabilir. Kural burada
/// değil veritabanında (kısmi tekil indeks, bkz. <c>FieldTaskConfiguration</c>),
/// çünkü ihlal ancak iki tüketici aynı anda çalıştığında oluşuyor ve iki ayrı
/// süreçteki iki nesne birbirini göremez.
/// </para>
/// </remarks>
public sealed class FieldTask : AggregateRoot
{
    public const int MaxReasonLength = 200;

    public Guid VehicleId { get; private set; }
    public FieldTaskType Type { get; private set; }
    public FieldTaskStatus Status { get; private set; }
    public string Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private FieldTask()
    {
        Reason = string.Empty;
    }

    private FieldTask(Guid id, Guid vehicleId, FieldTaskType type, string reason, DateTime createdAt)
        : base(id)
    {
        VehicleId = vehicleId;
        Type = type;
        Status = FieldTaskStatus.Open;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public static FieldTask ForLowBattery(Guid vehicleId, int batteryPercentage, DateTime createdAt)
    {
        if (vehicleId == Guid.Empty)
            throw new DomainException("Saha görevi bir araca bağlı olmalı.");

        if (batteryPercentage is < 0 or > 100)
            throw new DomainException("Batarya yüzdesi 0 ile 100 arasında olmalı.");

        return new FieldTask(
            Guid.NewGuid(),
            vehicleId,
            FieldTaskType.BatteryReplacement,
            $"Batarya düşük: %{batteryPercentage}",
            createdAt);
    }

    public void Complete(DateTime completedAt)
    {
        if (Status != FieldTaskStatus.Open)
            throw new DomainException("Yalnızca açık bir görev tamamlanabilir.");

        Status = FieldTaskStatus.Completed;
        CompletedAt = completedAt;
    }
}

using Scootly.Domain.Common;
using Scootly.Domain.FieldOps.Events;

namespace Scootly.Domain.FieldOps;

/// <summary>
/// Saha görevi yaşam döngüsü: Open → Assigned → Completed.
/// Bir araç+tür kombinasyonu için aynı anda yalnızca bir açık görev olabilir (kısmi benzersiz indeks).
/// </summary>
public sealed class FieldTask : AggregateRoot
{
    public Guid VehicleId { get; }
    public FieldTaskType Type { get; }
    public FieldTaskStatus Status { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? AssignedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Note { get; private set; }

    private FieldTask() { }

    public FieldTask(Guid id, Guid vehicleId, FieldTaskType type, DateTime now)
        : base(id)
    {
        if (vehicleId == Guid.Empty)
            throw new DomainException("Araç kimliği gerekli.");

        VehicleId = vehicleId;
        Type = type;
        Status = FieldTaskStatus.Open;
        CreatedAt = now;

        AddDomainEvent(new FieldTaskCreatedEvent(id, vehicleId, type, now));
    }

    public void Assign(Guid operatorId, DateTime now)
    {
        if (operatorId == Guid.Empty)
            throw new DomainException("Operatör kimliği gerekli.");

        if (Status != FieldTaskStatus.Open)
            throw new DomainException("Yalnızca açık bir görev üstlenilebilir.");

        AssignedTo = operatorId;
        AssignedAt = now;
        Status = FieldTaskStatus.Assigned;

        AddDomainEvent(new FieldTaskAssignedEvent(Id, operatorId, now));
    }

    public void Complete(Guid operatorId, DateTime now, string? note)
    {
        if (Status != FieldTaskStatus.Assigned)
            throw new DomainException("Yalnızca üstlenilmiş bir görev tamamlanabilir.");

        if (AssignedTo != operatorId)
            throw new DomainException("Bu görev size atanmamış.");

        Status = FieldTaskStatus.Completed;
        CompletedAt = now;
        Note = note;

        AddDomainEvent(new FieldTaskCompletedEvent(Id, operatorId, now));
    }
}
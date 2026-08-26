using System;
using System.Collections.Generic;
using System.Text;

using Scootly.Domain.Common;
using Scootly.Domain.Fleet;

namespace Scootly.Domain.Riding;

public sealed class Reservation : ValueObject
{
    public VehicleId VehicleId { get; }
    public Guid DriverId { get; }
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; }

    public Reservation(VehicleId vehicleId, Guid driverId, DateTime createdAt, DateTime expiresAt)
    {
        // TODO 1: driverId, Guid.Empty ise DomainException fırlat
        // TODO 2: expiresAt, createdAt'ten küçük veya eşitse DomainException fırlat

        VehicleId = vehicleId;
        DriverId = driverId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return VehicleId;
        yield return DriverId;
        yield return CreatedAt;
        yield return ExpiresAt;
    }
}

using Scootly.Api.Contracts.Requests;

namespace Scootly.Api.Validators;

public sealed class StartRideRequestValidator
{
    public (bool IsValid, string? Error) Validate(StartRideRequest request)
    {
        if (request.VehicleId == Guid.Empty)
            return (false, "VehicleId boş olamaz.");

        if (request.DriverId == Guid.Empty)
            return (false, "DriverId boş olamaz.");

        return (true, null);
    }
}
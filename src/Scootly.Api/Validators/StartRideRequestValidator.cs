using Scootly.Api.Contracts.Requests;

namespace Scootly.Api.Validators;

public sealed class StartRideRequestValidator
{
    public (bool IsValid, string? Error) Validate(StartRideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.VehicleId == Guid.Empty)
        {
            return (false, "VehicleId boş olamaz.");
        }

        // DriverId kontrolü kaldırıldı: artık istemciden gelmiyor.
        // Sürücü kimliğinin geçerliliği, token doğrulamasının kendisidir.
        return (true, null);
    }
}

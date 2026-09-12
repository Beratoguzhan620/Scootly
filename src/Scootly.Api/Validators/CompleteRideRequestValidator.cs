using Scootly.Api.Contracts.Requests;

namespace Scootly.Api.Validators;

public sealed class CompleteRideRequestValidator
{
    public (bool IsValid, string? Error) Validate(CompleteRideRequest request)
    {
        if (request.EndLatitude is < -90 or > 90)
            return (false, "EndLatitude -90 ile 90 arasında olmalı.");

        if (request.EndLongitude is < -180 or > 180)
            return (false, "EndLongitude -180 ile 180 arasında olmalı.");

        return (true, null);
    }
}
using FluentValidation;
using Scootly.Api.Contracts.Requests;

namespace Scootly.Api.Validators;

public sealed class StartRideRequestValidator : AbstractValidator<StartRideRequest>
{
    public StartRideRequestValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .WithMessage("Araç kimliği boş olamaz.");

        RuleFor(x => x.DriverId)
            .NotEmpty()
            .WithMessage("Sürücü kimliği boş olamaz.");
    }
}
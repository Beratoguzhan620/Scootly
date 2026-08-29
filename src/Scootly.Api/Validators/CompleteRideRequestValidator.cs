using FluentValidation;
using Scootly.Api.Contracts.Requests;

namespace Scootly.Api.Validators;

public sealed class CompleteRideRequestValidator : AbstractValidator<CompleteRideRequest>
{
    public CompleteRideRequestValidator()
    {
        RuleFor(x => x.EndLatitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Enlem -90 ile 90 arasında olmalı.");

        RuleFor(x => x.EndLongitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Boylam -180 ile 180 arasında olmalı.");
    }
}

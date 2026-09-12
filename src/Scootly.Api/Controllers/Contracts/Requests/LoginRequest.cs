using System.ComponentModel.DataAnnotations;

namespace Scootly.Api.Contracts.Requests;

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

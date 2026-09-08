namespace Scootly.Api.Contracts.Requests;

public sealed record DeviceTokenRequest(string ClientId, string ClientSecret);
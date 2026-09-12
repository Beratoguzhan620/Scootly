namespace Scootly.Api.Authorization;

public static class PolicyNames
{
    public const string FleetManagerOnly = "FleetManagerOnly";
    public const string OperatorOnly = "OperatorOnly";
    public const string DriverOnly = "DriverOnly";
    public const string RideOwner = "RideOwner";
}
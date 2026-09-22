using Npgsql;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Concurrency.Tests;

public static class BulkVehicleSeeder
{
    public static async Task BulkInsertAsync(string connectionString, IReadOnlyList<Vehicle> vehicles)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY \"Vehicles\" (\"Id\", \"Status\", \"Brand\", \"RangeKm\", \"BatteryPercentage\", \"Latitude\", \"Longitude\") FROM STDIN (FORMAT BINARY)");

        foreach (var vehicle in vehicles)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(vehicle.Id, NpgsqlTypes.NpgsqlDbType.Uuid);
            await writer.WriteAsync(vehicle.Status.ToString(), NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(vehicle.Model.Brand, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(vehicle.Model.RangeKm, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(vehicle.Battery.Percentage, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(vehicle.Location.Latitude, NpgsqlTypes.NpgsqlDbType.Double);
            await writer.WriteAsync(vehicle.Location.Longitude, NpgsqlTypes.NpgsqlDbType.Double);
        }

        await writer.CompleteAsync();
    }
}
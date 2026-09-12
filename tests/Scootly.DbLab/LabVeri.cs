using Npgsql;

namespace Scootly.DbLab;

/// <summary>Eşzamanlılık deneyleri için tek tek test satırı üretir.</summary>
internal static class LabVeri
{
    public static async Task<Guid> YeniMusaitAracAsync(CancellationToken cancellationToken = default)
    {
        await using var baglanti = await Lab.AcAsync(cancellationToken);

        var id = Guid.NewGuid();

        await using var komut = new NpgsqlCommand(
            """
            INSERT INTO "Vehicles"
                ("Id","Status","Brand","RangeKm","BatteryPercentage","Latitude","Longitude","Version")
            VALUES (@id, 'Available', 'Deney', 40, 100, 36.99, 35.33, 0)
            """, baglanti);

        komut.Parameters.AddWithValue("id", id);
        await komut.ExecuteNonQueryAsync(cancellationToken);

        return id;
    }

    public static async Task<Guid> YeniSurusAsync(Guid aracId, CancellationToken cancellationToken = default)
    {
        await using var baglanti = await Lab.AcAsync(cancellationToken);

        var id = Guid.NewGuid();

        await using var komut = new NpgsqlCommand(
            """
            INSERT INTO "Rides"
                ("Id","DriverId","VehicleId","Status","StartLatitude","StartLongitude","StartedAt")
            VALUES (@id, @surucu, @arac, 'Active', 36.99, 35.33, now())
            """, baglanti);

        komut.Parameters.AddWithValue("id", id);
        komut.Parameters.AddWithValue("surucu", Guid.NewGuid());
        komut.Parameters.AddWithValue("arac", aracId);
        await komut.ExecuteNonQueryAsync(cancellationToken);

        return id;
    }

    public static async Task<string> DurumAsync(Guid aracId, CancellationToken cancellationToken = default)
    {
        await using var baglanti = await Lab.AcAsync(cancellationToken);
        await using var komut = new NpgsqlCommand(
            "SELECT \"Status\" FROM \"Vehicles\" WHERE \"Id\" = @id", baglanti);
        komut.Parameters.AddWithValue("id", aracId);

        return (string?)await komut.ExecuteScalarAsync(cancellationToken) ?? "(yok)";
    }

    public static async Task<int> SurumAsync(Guid aracId, CancellationToken cancellationToken = default)
    {
        await using var baglanti = await Lab.AcAsync(cancellationToken);
        await using var komut = new NpgsqlCommand(
            "SELECT \"Version\" FROM \"Vehicles\" WHERE \"Id\" = @id", baglanti);
        komut.Parameters.AddWithValue("id", aracId);

        return Convert.ToInt32(await komut.ExecuteScalarAsync(cancellationToken) ?? 0);
    }
}

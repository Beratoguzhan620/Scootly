using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Infrastructure.Tests;

public sealed class ConnectionStringTests
{
    [Fact]
    public void Barindirilan_Veritabaninin_Postgres_Urlsi_Baglanti_Dizesine_Cevrilmeli()
    {
        var converted = ConnectionStrings.FromUrl("postgresql://app_user:p%40ss@ep-cool-name-123.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require");

        Assert.Equal("Host=ep-cool-name-123.eu-central-1.aws.neon.tech;Database=neondb;Username=app_user;Password=p@ss;SSL Mode=Require;Channel Binding=Require", converted);
    }

    [Fact]
    public void Bilinen_Bicimdeki_Baglanti_Dizesi_Oldugu_Gibi_Gecmeli()
    {
        const string connectionString = "Host=localhost;Database=scootly;Username=postgres;Password=secret";

        Assert.Equal(connectionString, ConnectionStrings.FromUrl(connectionString));
    }
}

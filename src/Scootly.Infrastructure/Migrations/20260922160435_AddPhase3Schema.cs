using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedUntil",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Tariffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UnlockAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    UnlockCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PerMinuteAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    PerMinuteCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    BatteryPercentage = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_readings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rides_surucu_baslangic",
                table: "Rides",
                columns: new[] { "DriverId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_tarife_aktif",
                table: "Tariffs",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_telemetri_cihaz_zaman",
                table: "telemetry_readings",
                columns: new[] { "DeviceId", "RecordedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_telemetri_zaman",
                table: "telemetry_readings",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tariffs");

            migrationBuilder.DropTable(
                name: "telemetry_readings");

            migrationBuilder.DropIndex(
                name: "ix_rides_surucu_baslangic",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "ReservedUntil",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Rides");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "Vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Fare",
                table: "Rides",
                type: "numeric(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "Rides",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "Rides",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_durum",
                table: "Vehicles",
                column: "Status",
                filter: "\"Status\" = 'Available'");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_konum",
                table: "Vehicles",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_rides_arac",
                table: "Rides",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "ix_rides_durum_bitis",
                table: "Rides",
                columns: new[] { "Status", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_rides_surucu",
                table: "Rides",
                column: "DriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vehicles_durum",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "ix_vehicles_konum",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "ix_rides_arac",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "ix_rides_durum_bitis",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "ix_rides_surucu",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "Rides");

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "Vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<decimal>(
                name: "Fare",
                table: "Rides",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldNullable: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxIdempotencyAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Rides",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutstandingDebts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    RideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutstandingDebts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Bekleyen",
                table: "OutboxMessages",
                column: "CreatedAt",
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutstandingDebts_DriverId",
                table: "OutstandingDebts",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "UX_OutstandingDebts_RideId",
                table: "OutstandingDebts",
                column: "RideId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ProcessedAt",
                table: "ProcessedMessages",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "OutstandingDebts");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Rides");
        }
    }
}

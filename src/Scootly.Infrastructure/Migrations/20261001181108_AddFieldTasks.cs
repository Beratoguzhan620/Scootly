using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldTasks_OneOpenTaskPerVehicleAndType",
                table: "FieldTasks",
                columns: new[] { "VehicleId", "Type" },
                unique: true,
                filter: "\"Status\" <> 'Completed'");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTasks_VehicleId",
                table: "FieldTasks",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldTasks");
        }
    }
}

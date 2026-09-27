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
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_FieldTasks_AcikGorev",
                table: "FieldTasks",
                columns: new[] { "VehicleId", "Type" },
                unique: true,
                filter: "\"Status\" = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldTasks");
        }
    }
}

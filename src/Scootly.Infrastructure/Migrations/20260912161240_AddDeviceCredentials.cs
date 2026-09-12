using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_credentials",
                schema: "identity",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.DeviceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_credentials",
                schema: "identity");
        }
    }
}

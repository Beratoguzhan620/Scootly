using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedHomeRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeRegion",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeRegion",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }
    }
}

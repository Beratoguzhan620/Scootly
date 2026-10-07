using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scootly.Infrastructure.Migrations
{
    /// <summary>
    /// Hizmet bölgesi sınırı ayrı tablodan tek bir jsonb dizisine taşınır: ayrı tabloda EF noktaları sıralamadan
    /// okuduğu için poligonun köşe sırası bozulabiliyordu (ADR 0045). Mevcut noktalar ekleniş sırasıyla (Id) taşınır.
    /// </summary>
    public partial class StoreServiceAreaBoundaryAsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Boundary",
                table: "ServiceAreas",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ServiceAreas" AS s
                SET "Boundary" = COALESCE(
                    (SELECT jsonb_agg(jsonb_build_object('Latitude', p."Latitude", 'Longitude', p."Longitude") ORDER BY p."Id")
                     FROM "ServiceAreaBoundaryPoints" AS p
                     WHERE p."ServiceAreaId" = s."Id"),
                    '[]'::jsonb);
                """);

            migrationBuilder.DropTable(
                name: "ServiceAreaBoundaryPoints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceAreaBoundaryPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ServiceAreaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAreaBoundaryPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceAreaBoundaryPoints_ServiceAreas_ServiceAreaId",
                        column: x => x.ServiceAreaId,
                        principalTable: "ServiceAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAreaBoundaryPoints_ServiceAreaId",
                table: "ServiceAreaBoundaryPoints",
                column: "ServiceAreaId");

            migrationBuilder.Sql("""
                INSERT INTO "ServiceAreaBoundaryPoints" ("ServiceAreaId", "Latitude", "Longitude")
                SELECT s."Id", (point.value ->> 'Latitude')::double precision, (point.value ->> 'Longitude')::double precision
                FROM "ServiceAreas" AS s
                CROSS JOIN LATERAL jsonb_array_elements(COALESCE(s."Boundary", '[]'::jsonb)) WITH ORDINALITY AS point(value, position)
                ORDER BY s."Id", point.position;
                """);

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "ServiceAreas");
        }
    }
}

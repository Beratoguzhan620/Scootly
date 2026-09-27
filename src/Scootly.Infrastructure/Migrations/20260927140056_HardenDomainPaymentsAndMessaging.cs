using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Scootly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenDomainPaymentsAndMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ProcessedMessages",
                table: "ProcessedMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryAt",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservedBy",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "StartLongitude",
                table: "Rides",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<double>(
                name: "StartLatitude",
                table: "Rides",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "Fare",
                table: "Rides",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaymentAttemptAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPaymentError",
                table: "Rides",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentAttempts",
                table: "Rides",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Rides",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Rides",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "Consumer",
                table: "ProcessedMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "OutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "OutboxMessages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Mevcut kayıtlar tek tüketicili döneme aittir.
            migrationBuilder.Sql("""UPDATE "ProcessedMessages" SET "Consumer" = 'legacy' WHERE "Consumer" = '';""");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProcessedMessages",
                table: "ProcessedMessages",
                columns: new[] { "MessageId", "Consumer" });

            MigrateRidePaymentData(migrationBuilder);
            RepairInconsistentVehicleAndRideStates(migrationBuilder);
            RenameConflictingLegacyRoles(migrationBuilder);

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f01"), "0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f01", "Driver", "DRIVER" },
                    { new Guid("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f02"), "0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f02", "FleetManager", "FLEETMANAGER" },
                    { new Guid("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f03"), "0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f03", "FieldOperator", "FIELDOPERATOR" }
                });

            MoveLegacyRoleMembershipsAndBackfillDrivers(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ReservedBy",
                table: "Vehicles",
                column: "ReservedBy",
                unique: true,
                filter: "\"ReservedBy\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAreas_Name",
                table: "ServiceAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_DriverId_Active",
                table: "Rides",
                column: "DriverId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_EndedAt",
                table: "Rides",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_PaymentStatus",
                table: "Rides",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_Status_StartedAt",
                table: "Rides",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_VehicleId_Active",
                table: "Rides",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ProcessedAt",
                table: "ProcessedMessages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending_CreatedAt",
                table: "OutboxMessages",
                column: "CreatedAt",
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <summary>
        /// Eski model: ödeme reddi Status = 'PaymentPending' ile, ödenmiş sürüş ise Fare dolu olmasıyla ifade ediliyordu.
        /// Yeni model: sürüş yaşam döngüsü (Status) ile ödeme durumu (PaymentStatus) ayrı alanlardır.
        /// </summary>
        private static void MigrateRidePaymentData(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Rides"
                SET "Status" = 'Completed', "PaymentStatus" = 'Pending', "PaymentAttempts" = 1
                WHERE "Status" = 'PaymentPending';
                """);

            migrationBuilder.Sql("""
                UPDATE "Rides"
                SET "PaymentStatus" = 'Paid'
                WHERE "Status" = 'Completed' AND "PaymentStatus" = 'None' AND "Fare" IS NOT NULL;
                """);

            // Ödeme saga'sı hiç çalışmamış tamamlanmış sürüşler: ücret yeni tarifeyle (başlamış dakika, en az 1) hesaplanır.
            migrationBuilder.Sql("""
                UPDATE "Rides"
                SET "PaymentStatus" = 'Pending',
                    "Fare" = GREATEST(CEIL(EXTRACT(EPOCH FROM ("EndedAt" - "StartedAt")) / 60.0), 1) * 2.5
                WHERE "Status" = 'Completed' AND "Fare" IS NULL AND "EndedAt" IS NOT NULL;
                """);

            // Hiç kullanılmamış 'Reserved' sürüş durumu enum'dan kaldırıldı.
            migrationBuilder.Sql("""
                UPDATE "Rides"
                SET "Status" = 'Abandoned', "EndedAt" = COALESCE("EndedAt", now())
                WHERE "Status" = 'Reserved';
                """);
        }

        /// <summary>
        /// Eski hataların bıraktığı tutarsızlıkları, yeni benzersizlik kısıtları eklenmeden önce onarır.
        /// </summary>
        private static void RepairInconsistentVehicleAndRideStates(MigrationBuilder migrationBuilder)
        {
            // Sürücü veya araç başına birden fazla aktif sürüş varsa en yenisi korunur, diğerleri terk edilmiş sayılır.
            migrationBuilder.Sql("""
                UPDATE "Rides" r
                SET "Status" = 'Abandoned', "EndedAt" = COALESCE(r."EndedAt", now())
                WHERE r."Status" = 'Active'
                  AND EXISTS (
                      SELECT 1 FROM "Rides" o
                      WHERE o."Status" = 'Active'
                        AND (o."DriverId" = r."DriverId" OR o."VehicleId" = r."VehicleId")
                        AND (o."StartedAt" > r."StartedAt" OR (o."StartedAt" = r."StartedAt" AND o."Id" > r."Id")));
                """);

            // Sahibi bilinmeyen eski rezervasyonlar kimse tarafından başlatılamaz: araç serbest bırakılır.
            migrationBuilder.Sql("""
                UPDATE "Vehicles"
                SET "Status" = 'Available', "ReservedAt" = NULL
                WHERE "Status" = 'Reserved' AND "ReservedBy" IS NULL;
                """);

            // Aktif sürüşü olmayan ama "sürüşte" görünen araçlar (terk edilmiş sürüş hatası) saha kontrolüne alınır.
            migrationBuilder.Sql("""
                UPDATE "Vehicles" v
                SET "Status" = 'Maintenance'
                WHERE v."Status" = 'InRide'
                  AND NOT EXISTS (SELECT 1 FROM "Rides" r WHERE r."VehicleId" = v."Id" AND r."Status" = 'Active');
                """);
        }

        private const string SeededRoleIds =
            "'0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f01', '0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f02', '0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f03'";

        /// <summary>Elle oluşturulmuş aynı adlı roller, tohumlanan rollerle çakışmasın diye geçici olarak yeniden adlandırılır.</summary>
        private static void RenameConflictingLegacyRoles(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "AspNetRoles"
                SET "Name" = "Name" || '_legacy', "NormalizedName" = "NormalizedName" || '_LEGACY'
                WHERE "NormalizedName" IN ('DRIVER', 'FLEETMANAGER', 'FIELDOPERATOR')
                  AND "Id" NOT IN ({SeededRoleIds});
                """);
        }

        private static void MoveLegacyRoleMembershipsAndBackfillDrivers(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT ur."UserId", seeded."Id"
                FROM "AspNetUserRoles" ur
                JOIN "AspNetRoles" legacy ON legacy."Id" = ur."RoleId"
                JOIN "AspNetRoles" seeded ON seeded."NormalizedName" || '_LEGACY' = legacy."NormalizedName"
                WHERE seeded."Id" IN ({SeededRoleIds})
                ON CONFLICT DO NOTHING;

                DELETE FROM "AspNetUserRoles" WHERE "RoleId" IN (
                    SELECT "Id" FROM "AspNetRoles" WHERE "NormalizedName" IN ('DRIVER_LEGACY', 'FLEETMANAGER_LEGACY', 'FIELDOPERATOR_LEGACY'));
                DELETE FROM "AspNetRoleClaims" WHERE "RoleId" IN (
                    SELECT "Id" FROM "AspNetRoles" WHERE "NormalizedName" IN ('DRIVER_LEGACY', 'FLEETMANAGER_LEGACY', 'FIELDOPERATOR_LEGACY'));
                DELETE FROM "AspNetRoles" WHERE "NormalizedName" IN ('DRIVER_LEGACY', 'FLEETMANAGER_LEGACY', 'FIELDOPERATOR_LEGACY');
                """);

            // Rol sistemi öncesi kaydolmuş, hiç rolü olmayan kullanıcılar sürücüdür.
            migrationBuilder.Sql("""
                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT u."Id", '0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f01'
                FROM "AspNetUsers" u
                WHERE NOT EXISTS (SELECT 1 FROM "AspNetUserRoles" ur WHERE ur."UserId" = u."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ödeme durumunu eski modele geri çevir (Down, sütunlar silinmeden önce çalışmalı).
            migrationBuilder.Sql("""
                UPDATE "Rides" SET "Status" = 'PaymentPending'
                WHERE "Status" = 'Completed' AND "PaymentStatus" IN ('Pending', 'Failed') AND "PaymentAttempts" > 0;

                UPDATE "Rides" SET "Fare" = NULL
                WHERE "Status" = 'Completed' AND "PaymentStatus" = 'Pending' AND "PaymentAttempts" = 0;
                """);

            // Tek sütunlu birincil anahtara dönmeden önce farklı tüketicilere ait yinelenen kayıtları temizle.
            migrationBuilder.Sql("""
                DELETE FROM "ProcessedMessages" a
                USING "ProcessedMessages" b
                WHERE a."MessageId" = b."MessageId" AND a."Consumer" > b."Consumer";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_ReservedBy",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_ServiceAreas_Name",
                table: "ServiceAreas");

            migrationBuilder.DropIndex(
                name: "IX_Rides_DriverId_Active",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "IX_Rides_EndedAt",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "IX_Rides_PaymentStatus",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "IX_Rides_Status_StartedAt",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "IX_Rides_VehicleId_Active",
                table: "Rides");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProcessedMessages",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f01"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f02"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f03"));

            migrationBuilder.DropColumn(
                name: "LastTelemetryAt",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ReservedBy",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LastPaymentAttemptAt",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "LastPaymentError",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "PaymentAttempts",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "Consumer",
                table: "ProcessedMessages");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<double>(
                name: "StartLongitude",
                table: "Rides",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "StartLatitude",
                table: "Rides",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Fare",
                table: "Rides",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProcessedMessages",
                table: "ProcessedMessages",
                column: "MessageId");
        }
    }
}

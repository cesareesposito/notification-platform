using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveApiKeysToTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiKeyCreatedAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiKeyHash",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiKeyRevokedAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH ranked_keys AS (
                    SELECT DISTINCT ON ("NotificationTenantId")
                        "NotificationTenantId",
                        "ClientName",
                        "KeyHash",
                        "CreatedAt",
                        "RevokedAt",
                        "IsActive"
                    FROM api_keys
                    ORDER BY "NotificationTenantId", CASE WHEN "IsActive" THEN 0 ELSE 1 END, "CreatedAt" DESC
                )
                UPDATE tenants AS t
                SET
                    "ClientId" = COALESCE(NULLIF(t."ClientId", ''), ranked_keys."ClientName"),
                    "ApiKeyHash" = CASE WHEN ranked_keys."IsActive" THEN ranked_keys."KeyHash" ELSE NULL END,
                    "ApiKeyCreatedAt" = ranked_keys."CreatedAt",
                    "ApiKeyRevokedAt" = CASE WHEN ranked_keys."IsActive" THEN NULL ELSE ranked_keys."RevokedAt" END,
                    "UpdatedAt" = NOW()
                FROM ranked_keys
                WHERE t."TenantId" = ranked_keys."NotificationTenantId";
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_api_key_hash",
                table: "tenants",
                column: "ApiKeyHash",
                unique: true,
                filter: "\"ApiKeyHash\" IS NOT NULL");

            migrationBuilder.DropTable(
                name: "api_keys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NotificationTenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_hash",
                table: "api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO api_keys ("Id", "ClientName", "KeyHash", "NotificationTenantId", "CreatedAt", "RevokedAt", "IsActive")
                SELECT
                    (
                        substr(md5("TenantId"), 1, 8) || '-' ||
                        substr(md5("TenantId"), 9, 4) || '-4' ||
                        substr(md5("TenantId"), 14, 3) || '-a' ||
                        substr(md5("TenantId"), 18, 3) || '-' ||
                        substr(md5("TenantId"), 21, 12)
                    )::uuid,
                    COALESCE(NULLIF("ClientId", ''), "TenantId"),
                    "ApiKeyHash",
                    "TenantId",
                    COALESCE("ApiKeyCreatedAt", "CreatedAt"),
                    "ApiKeyRevokedAt",
                    true
                FROM tenants
                WHERE "ApiKeyHash" IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "ix_tenants_api_key_hash",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ApiKeyCreatedAt",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ApiKeyHash",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ApiKeyRevokedAt",
                table: "tenants");
        }
    }
}

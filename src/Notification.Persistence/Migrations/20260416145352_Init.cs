using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "admin"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmailProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SendGrid"),
                    EmailFrom = table.Column<string>(type: "text", nullable: true),
                    EmailFromName = table.Column<string>(type: "text", nullable: true),
                    PushProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Firebase"),
                    TemplateBasePath = table.Column<string>(type: "text", nullable: false),
                    ProviderSettings = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ApiKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApiKeyCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApiKeyRevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_username",
                table: "admin_users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_templates_lookup",
                table: "notification_templates",
                columns: new[] { "ClientId", "TemplateName", "Channel", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_api_key_hash",
                table: "tenants",
                column: "ApiKeyHash",
                unique: true,
                filter: "\"ApiKeyHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_client_id",
                table: "tenants",
                column: "ClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "notification_templates");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}

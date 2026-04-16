using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRecordIdAndClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_templates_tenants_TenantId",
                table: "notification_templates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                table: "tenants");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE tenants
                SET "ClientId" = COALESCE(NULLIF("ClientId", ''), "TenantId")
                WHERE "ClientId" IS NULL OR "ClientId" = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE tenants
                SET "Id" = (
                    substr(md5("TenantId"), 1, 8) || '-' ||
                    substr(md5("TenantId"), 9, 4) || '-4' ||
                    substr(md5("TenantId"), 14, 3) || '-a' ||
                    substr(md5("TenantId"), 18, 3) || '-' ||
                    substr(md5("TenantId"), 21, 12)
                )::uuid
                WHERE "Id" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "tenants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "notification_templates",
                newName: "ClientId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                table: "tenants",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_client_id",
                table: "tenants",
                column: "ClientId",
                unique: true);

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenants_client_id",
                table: "tenants");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "notification_templates",
                newName: "TenantId");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE tenants
                SET "TenantId" = "ClientId"
                WHERE "TenantId" IS NULL OR "TenantId" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                table: "tenants",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_notification_templates_tenants_TenantId",
                table: "notification_templates",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "Id",
                table: "tenants");
        }
    }
}

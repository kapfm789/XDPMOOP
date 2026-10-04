using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oism.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateReferencesAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "branch_refs",
                schema: "core",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_refs", x => new { x.tenant_id, x.branch_id });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "core",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "sku_refs",
                schema: "core",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    barcodes = table.Column<string[]>(type: "text[]", nullable: false),
                    retail_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    wholesale_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sku_refs", x => new { x.tenant_id, x.sku_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_sku_refs_tenant_id_sku_code",
                schema: "core",
                table: "sku_refs",
                columns: new[] { "tenant_id", "sku_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_refs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "sku_refs",
                schema: "core");
        }
    }
}

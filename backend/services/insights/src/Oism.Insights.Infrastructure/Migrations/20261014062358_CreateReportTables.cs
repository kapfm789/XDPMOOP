using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oism.Insights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateReportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "insights");

            migrationBuilder.CreateTable(
                name: "daily_aggregates",
                schema: "insights",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    revenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cogs = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_aggregates", x => new { x.tenant_id, x.date, x.branch_id, x.channel, x.sku_id });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "insights",
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
                name: "sales_facts",
                schema: "insights",
                columns: table => new
                {
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    revenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cogs = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_facts", x => x.order_item_id);
                });

            migrationBuilder.CreateTable(
                name: "sku_names",
                schema: "insights",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sku_names", x => new { x.tenant_id, x.sku_id });
                });

            migrationBuilder.CreateTable(
                name: "stock_snapshots",
                schema: "insights",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    available = table.Column<int>(type: "integer", nullable: false),
                    avg_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    threshold = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_snapshots", x => new { x.tenant_id, x.branch_id, x.sku_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_facts_tenant_id_branch_id_confirmed_at",
                schema: "insights",
                table: "sales_facts",
                columns: new[] { "tenant_id", "branch_id", "confirmed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_facts_tenant_id_confirmed_at",
                schema: "insights",
                table: "sales_facts",
                columns: new[] { "tenant_id", "confirmed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_facts_tenant_id_sku_id_confirmed_at",
                schema: "insights",
                table: "sales_facts",
                columns: new[] { "tenant_id", "sku_id", "confirmed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_aggregates",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "sales_facts",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "sku_names",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "stock_snapshots",
                schema: "insights");
        }
    }
}

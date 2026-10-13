using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oism.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stocktakes",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stocktakes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stocktake_items",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stocktake_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counted_qty = table.Column<int>(type: "integer", nullable: false),
                    system_qty = table.Column<int>(type: "integer", nullable: true),
                    difference = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stocktake_items", x => x.id);
                    table.CheckConstraint("ck_stocktake_items_counted_qty_non_negative", "counted_qty >= 0");
                    table.ForeignKey(
                        name: "fk_stocktake_items_stocktakes_stocktake_id",
                        column: x => x.stocktake_id,
                        principalSchema: "core",
                        principalTable: "stocktakes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stocktake_items_stocktake_id_sku_id",
                schema: "core",
                table: "stocktake_items",
                columns: new[] { "stocktake_id", "sku_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stocktake_items",
                schema: "core");

            migrationBuilder.DropTable(
                name: "stocktakes",
                schema: "core");
        }
    }
}

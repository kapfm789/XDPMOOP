using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oism.Insights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_runs",
                schema: "insights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ran_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    baseline_wape = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    forecast_wape = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "forecast_results",
                schema: "insights",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_enough_data = table.Column<bool>(type: "boolean", nullable: false),
                    daily_level = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    forecast_qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    suggested_reorder_qty = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_results", x => new { x.run_id, x.branch_id, x.sku_id });
                    table.ForeignKey(
                        name: "fk_forecast_results_forecast_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "insights",
                        principalTable: "forecast_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_runs_tenant_id_ran_at",
                schema: "insights",
                table: "forecast_runs",
                columns: new[] { "tenant_id", "ran_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_results",
                schema: "insights");

            migrationBuilder.DropTable(
                name: "forecast_runs",
                schema: "insights");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Oism.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_balances",
                schema: "core",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    avg_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reorder_threshold = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_balances", x => new { x.tenant_id, x.branch_id, x.sku_id });
                    table.CheckConstraint("ck_balance_non_negative", "on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand");
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                schema: "core",
                columns: table => new
                {
                    seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reference_type = table.Column<string>(type: "text", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reversal_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_transactions", x => x.seq);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    external_order_id = table.Column<string>(type: "text", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reserved_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_code = table.Column<string>(type: "text", nullable: false),
                    sku_name = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "core",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_id",
                schema: "core",
                table: "inventory_transactions",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_tenant_id_branch_id_sku_id_seq",
                schema: "core",
                table: "inventory_transactions",
                columns: new[] { "tenant_id", "branch_id", "sku_id", "seq" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_tenant_id_created_at",
                schema: "core",
                table: "inventory_transactions",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_tenant_id_reference_type_reference_id",
                schema: "core",
                table: "inventory_transactions",
                columns: new[] { "tenant_id", "reference_type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                schema: "core",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_tenant_id_order_id",
                schema: "core",
                table: "order_items",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_order_items_tenant_id_sku_id",
                schema: "core",
                table: "order_items",
                columns: new[] { "tenant_id", "sku_id" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_channel_external_order_id",
                schema: "core",
                table: "orders",
                columns: new[] { "tenant_id", "channel", "external_order_id" },
                unique: true,
                filter: "external_order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_created_at",
                schema: "core",
                table: "orders",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_idempotency_key",
                schema: "core",
                table: "orders",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_order_number",
                schema: "core",
                table: "orders",
                columns: new[] { "tenant_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_status_reserved_until",
                schema: "core",
                table: "orders",
                columns: new[] { "tenant_id", "status", "reserved_until" });

            // Sổ chỉ thêm mới (NFR-SEC-03): database từ chối UPDATE và DELETE trên inventory_transactions.
            // docs/architecture/transactions-and-concurrency.md mục "Hàng rào cuối ở database".
            migrationBuilder.Sql(
                """
                CREATE FUNCTION core.forbid_ledger_change() RETURNS trigger AS $$
                BEGIN
                  RAISE EXCEPTION 'inventory_transactions is append-only';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_ledger_append_only
                BEFORE UPDATE OR DELETE ON core.inventory_transactions
                FOR EACH ROW EXECUTE FUNCTION core.forbid_ledger_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_balances",
                schema: "core");

            migrationBuilder.DropTable(
                name: "inventory_transactions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "core");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "core");

            // Trigger đi theo bảng; hàm thì phải xóa riêng.
            migrationBuilder.Sql("DROP FUNCTION core.forbid_ledger_change();");
        }
    }
}

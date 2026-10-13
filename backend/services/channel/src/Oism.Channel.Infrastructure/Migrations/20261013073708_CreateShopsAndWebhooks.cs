using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oism.Channel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateShopsAndWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "channel");

            migrationBuilder.CreateTable(
                name: "channel_shops",
                schema: "channel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    shop_id = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secret = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_shops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "channel",
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
                name: "outbox_messages",
                schema: "channel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                schema: "channel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    event_id = table.Column<string>(type: "text", nullable: false),
                    external_order_id = table.Column<string>(type: "text", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channel_shops_channel_shop_id",
                schema: "channel",
                table: "channel_shops",
                columns: new[] { "channel", "shop_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                schema: "channel",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_tenant_id_channel_event_id",
                schema: "channel",
                table: "webhook_events",
                columns: new[] { "tenant_id", "channel", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_tenant_id_channel_external_order_id",
                schema: "channel",
                table: "webhook_events",
                columns: new[] { "tenant_id", "channel", "external_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_tenant_id_received_at",
                schema: "channel",
                table: "webhook_events",
                columns: new[] { "tenant_id", "received_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_shops",
                schema: "channel");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "channel");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "channel");

            migrationBuilder.DropTable(
                name: "webhook_events",
                schema: "channel");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oism.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_account",
                schema: "identity",
                table: "branches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_bin",
                schema: "identity",
                table: "branches",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_branches_bank_account",
                schema: "identity",
                table: "branches",
                sql: "(bank_bin IS NULL) = (bank_account IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_branches_bank_account",
                schema: "identity",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "bank_account",
                schema: "identity",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "bank_bin",
                schema: "identity",
                table: "branches");
        }
    }
}

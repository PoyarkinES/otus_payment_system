using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otus.PaymentSystem.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletBalanceChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_Available_NonNegative",
                table: "Wallets",
                sql: "\"Available\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_Held_NonNegative",
                table: "Wallets",
                sql: "\"Held\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_Available_NonNegative",
                table: "Wallets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_Held_NonNegative",
                table: "Wallets");
        }
    }
}

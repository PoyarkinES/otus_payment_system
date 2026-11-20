using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otus.PaymentSystem.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletTransactionIdempotencyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_WalletTransactions_WalletId_ReferenceId",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "ReferenceId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WalletTransactions_WalletId_ReferenceId",
                table: "WalletTransactions");
        }
    }
}

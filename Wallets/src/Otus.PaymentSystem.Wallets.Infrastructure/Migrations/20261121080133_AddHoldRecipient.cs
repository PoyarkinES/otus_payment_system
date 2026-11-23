using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otus.PaymentSystem.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipientUserId",
                table: "Holds",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Holds");
        }
    }
}

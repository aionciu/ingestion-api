using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngestionApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransactionEntityForValueExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionBags");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_BagOpVendor",
                table: "Transaction");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Transaction",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TransactionType",
                table: "Transaction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "Transaction",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_BagOpCurrVendorType",
                table: "Transaction",
                columns: new[] { "BagId", "OperationId", "Currency", "VendorId", "TransactionType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_BagOpCurrVendorType",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Transaction");

            migrationBuilder.CreateTable(
                name: "TransactionBags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeclaredValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DifferenceValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProcessedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionBags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionBags_Transaction_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_BagOpVendor",
                table: "Transaction",
                columns: new[] { "BagId", "OperationId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionBags_TransactionId",
                table: "TransactionBags",
                column: "TransactionId");
        }
    }
}

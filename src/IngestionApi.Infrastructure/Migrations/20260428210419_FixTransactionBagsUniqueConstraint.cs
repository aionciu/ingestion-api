using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngestionApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixTransactionBagsUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    VendorId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BagId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionBags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeclaredValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProcessedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DifferenceValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BagId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    VendorId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
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
                name: "IX_TransactionBags_TransactionId",
                table: "TransactionBags",
                column: "TransactionId");

            // Create composite unique constraint: (BagId, OperationId, Currency, VendorId)
            migrationBuilder.CreateIndex(
                name: "IX_TransactionBags_BagOpCurrVendor",
                table: "TransactionBags",
                columns: new[] { "BagId", "OperationId", "Currency", "VendorId" },
                unique: true);

            // Populate the shadow property columns from Transaction
            migrationBuilder.Sql(
                @"UPDATE tb
                  SET BagId = t.BagId,
                      OperationId = t.OperationId,
                      VendorId = t.VendorId
                  FROM TransactionBags tb
                  INNER JOIN Transaction t ON tb.TransactionId = t.Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionBags");

            migrationBuilder.DropTable(
                name: "Transaction");
        }
    }
}

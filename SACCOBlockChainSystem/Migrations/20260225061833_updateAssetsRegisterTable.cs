using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class updateAssetsRegisterTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loanbals_Loans_LoanNo",
                table: "Loanbals");

            migrationBuilder.DropForeignKey(
                name: "FK_Repays_Loans_LoanNo",
                table: "Repays");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Loans_LoanNo",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loanbals_LoanNo",
                table: "Loanbals");

            migrationBuilder.AddColumn<int>(
                name: "LoanId",
                table: "Repays",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Loanbal",
                table: "Loans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "LoanNo",
                table: "Loanbals",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "LoanId",
                table: "Loanbals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetsRegisters",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Class = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SerialNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DateOfManufacture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatePurchased = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    posted = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetsRegisters", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Repays_LoanId",
                table: "Repays",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_Loanbals_LoanId",
                table: "Loanbals",
                column: "LoanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loanbals_Loans_LoanId",
                table: "Loanbals",
                column: "LoanId",
                principalTable: "Loans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Repays_Loans_LoanId",
                table: "Repays",
                column: "LoanId",
                principalTable: "Loans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loanbals_Loans_LoanId",
                table: "Loanbals");

            migrationBuilder.DropForeignKey(
                name: "FK_Repays_Loans_LoanId",
                table: "Repays");

            migrationBuilder.DropTable(
                name: "AssetsRegisters");

            migrationBuilder.DropIndex(
                name: "IX_Repays_LoanId",
                table: "Repays");

            migrationBuilder.DropIndex(
                name: "IX_Loanbals_LoanId",
                table: "Loanbals");

            migrationBuilder.DropColumn(
                name: "LoanId",
                table: "Repays");

            migrationBuilder.DropColumn(
                name: "Loanbal",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LoanId",
                table: "Loanbals");

            migrationBuilder.AlterColumn<string>(
                name: "LoanNo",
                table: "Loanbals",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Loans_LoanNo",
                table: "Loans",
                column: "LoanNo");

            migrationBuilder.CreateIndex(
                name: "IX_Loanbals_LoanNo",
                table: "Loanbals",
                column: "LoanNo");

            migrationBuilder.AddForeignKey(
                name: "FK_Loanbals_Loans_LoanNo",
                table: "Loanbals",
                column: "LoanNo",
                principalTable: "Loans",
                principalColumn: "LoanNo",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repays_Loans_LoanNo",
                table: "Repays",
                column: "LoanNo",
                principalTable: "Loans",
                principalColumn: "LoanNo",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

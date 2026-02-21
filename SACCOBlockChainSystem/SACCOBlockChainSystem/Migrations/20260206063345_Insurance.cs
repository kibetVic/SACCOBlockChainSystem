using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class Insurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Insurance",
                table: "Loantypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoanTypeId",
                table: "Loans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusDescription",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoanId",
                table: "Loanguars",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_LoanTypeId",
                table: "Loans",
                column: "LoanTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Loanguars_LoanId",
                table: "Loanguars",
                column: "LoanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loanguars_Loans_LoanId",
                table: "Loanguars",
                column: "LoanId",
                principalTable: "Loans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Loantypes_LoanTypeId",
                table: "Loans",
                column: "LoanTypeId",
                principalTable: "Loantypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loanguars_Loans_LoanId",
                table: "Loanguars");

            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Loantypes_LoanTypeId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loans_LoanTypeId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loanguars_LoanId",
                table: "Loanguars");

            migrationBuilder.DropColumn(
                name: "Insurance",
                table: "Loantypes");

            migrationBuilder.DropColumn(
                name: "LoanTypeId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "StatusDescription",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LoanId",
                table: "Loanguars");
        }
    }
}

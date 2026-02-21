using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class NewTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MemberNoNavigationId",
                table: "Contribs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contribs_MemberNoNavigationId",
                table: "Contribs",
                column: "MemberNoNavigationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contribs_Members_MemberNoNavigationId",
                table: "Contribs",
                column: "MemberNoNavigationId",
                principalTable: "Members",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contribs_Members_MemberNoNavigationId",
                table: "Contribs");

            migrationBuilder.DropIndex(
                name: "IX_Contribs_MemberNoNavigationId",
                table: "Contribs");

            migrationBuilder.DropColumn(
                name: "MemberNoNavigationId",
                table: "Contribs");
        }
    }
}

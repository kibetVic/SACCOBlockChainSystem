using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class createjournaltable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Journals",
                columns: table => new
                {
                    JVID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VNO = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ACCNO = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    NAME = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NARATION = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MEMBERNO = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SHARETYPE = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Loanno = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AMOUNT = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TRANSTYPE = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    AUDITID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TRANSDATE = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AUDITDATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    POSTED = table.Column<bool>(type: "bit", nullable: false),
                    POSTEDDATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Transactionno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.JVID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Journals");
        }
    }
}

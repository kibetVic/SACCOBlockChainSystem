using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class GlAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GLSETUP",
                columns: table => new
                {
                    GlId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Glcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Glaccname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Glacctype = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GlAccMainGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Glaccgroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Normalbal = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Glaccstatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrCode = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AuditOrg = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Curr = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Actuals = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Budgetted = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSubLedger = table.Column<bool>(type: "bit", nullable: true),
                    AccCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrialBalance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GlOrder = table.Column<int>(type: "int", nullable: true),
                    Used = table.Column<int>(type: "int", nullable: true),
                    PrintOrder = table.Column<int>(type: "int", nullable: true),
                    BalanceSheet = table.Column<int>(type: "int", nullable: true),
                    GlType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentBal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EoyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EoyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Main = table.Column<bool>(type: "bit", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OpeningBal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewGlOpeningBal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewGlOpeningBalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    IsSuspense = table.Column<bool>(type: "bit", nullable: false),
                    IsREarning = table.Column<bool>(type: "bit", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLSETUP", x => x.GlId);
                });

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
                name: "GLSETUP");

            migrationBuilder.DropTable(
                name: "Journals");
        }
    }
}

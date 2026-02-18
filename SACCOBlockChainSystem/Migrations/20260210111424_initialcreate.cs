using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class initialcreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppraisalBy",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppraisalDate",
                table: "Loans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisbursementBy",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisbursementDate",
                table: "Loans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndorsementBy",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndorsementDate",
                table: "Loans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuarantorBy",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuarantorDate",
                table: "Loans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalBy",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "AppraisalDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "DisbursementBy",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "DisbursementDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "EndorsementBy",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "EndorsementDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "GuarantorBy",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "GuarantorDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Loans");
        }
    }
}
